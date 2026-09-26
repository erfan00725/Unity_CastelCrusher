# TNT Explosion Implementation Plan

> **For the person executing this:** This is a manual, step-by-step plan. Work
> top to bottom and tick each checkbox as you go. Tasks 1–5 are the logic phase
> and produce a fully testable feature with no visuals. Tasks 6–8 are the
> presentation phase and can be done later without redoing anything.
>
> **Do not skip Task 1.** Every later task depends on the `OnDeath` event, and
> the code will not compile without it.

**Goal:** Make TNT blocks explode when destroyed, pushing nearby Rigidbodies away
and damaging nearby blocks and enemies, with explosion chaining through clusters
of TNT.

**Architecture:** `DOHealth` gains a single `OnDeath` event raised inside its
existing `DieRoutine`. A new `ExplosiveObject` component subscribes to it and, on
death, runs one `Physics.OverlapSphere` pass that damages and pushes everything in
radius. Because chain reactions happen through the existing damage path — A's
blast damages B → B's health hits zero → B's `Die()` fires → B's `OnDeath`
explodes — **no chaining code exists anywhere**. Re-entrancy is already guarded by
the `_isDead` flag in `DOHealth.Die()`.

**Tech Stack:** Unity 6 (URP), C#, Unity Input System. No new packages.

**Spec:** `docs/superpowers/specs/2026-09-26-tnt-explosion-design.md`

## Global Constraints

- Project has **no test framework**. Verification is manual, in `SampleScene`.
  Do not add a test framework for this feature.
- **Never mark TNT `[Implemented]` in `docs/GDD.md` until it actually works in
  the build.** The GDD sync rule in `CLAUDE.md` applies to Task 9.
- All tunable values must be `[SerializeField]` private fields, so they are
  editable in the Inspector without recompiling.
- `SO_AudioConfigBase.Play` computes `source.volume = volume * volumeMagnitude`
  with **no clamping** (`Assets/ScriptableObjects/Audio/SO_AudioConfigBase.cs:15`).
  Always pass a normalized 0–1 value, never a raw intensity.
- `CameraShakeManager.Shake` multiplies by `impulseStrength = 15f` and clamps
  camera offset to `0.6`. A full-power player shot passes `~1`
  (`Projectile.shakeIntensity = 1`). Use `2` for an explosion, not a large number.
- The world is tiled static geometry with **zero Rigidbodies in the scene**.
  Anything that must be pushed needs a non-kinematic Rigidbody.

---

### Task 1: Add the `OnDeath` event to `DOHealth`

This is the single hook the whole feature hangs off. Without it nothing else
compiles.

**Files:**
- Modify: `Assets/Scripts/DestructableObjects/DOHealth.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public event System.Action OnDeath` on `DOHealth`, invoked inside
  `DieRoutine()` immediately after `_isDead = true` and **before** scoring.
  Task 3 subscribes to it.

- [ ] **Step 1: Add the event declaration**

Open `Assets/Scripts/DestructableObjects/DOHealth.cs`. Below the existing public
fields (after `public ScorePopup scorePopupPrefab;`), add:

```csharp
    public event System.Action OnDeath;
```

- [ ] **Step 2: Raise the event inside `DieRoutine`**

`DieRoutine` currently starts like this:

```csharp
    IEnumerator DieRoutine()
    {
        if (_isDead) yield break;
        
        _isDead = true;
        
        ScoreManager.I.AddScore(deathScoreValue);
```

Change it to raise the event right after `_isDead = true`:

```csharp
    IEnumerator DieRoutine()
    {
        if (_isDead) yield break;
        
        _isDead = true;
        
        OnDeath?.Invoke();
        
        ScoreManager.I.AddScore(deathScoreValue);
```

Raise it **before** scoring so the blast resolves before the scoreboard settles.
This is a feel decision, not a correctness one — if you dislike the ordering, move
the line, but keep it inside `DieRoutine`.

- [ ] **Step 3: Save and let Unity compile**

Save the file and switch to the Unity Editor. Wait for the compile spinner in the
bottom-right to finish.

Expected: **no errors** in the Console. (Nothing subscribes to `OnDeath` yet, so
the event is inert — that is correct at this stage.)

- [ ] **Step 4: Verify nothing regressed**

Play `SampleScene`. Launch a boulder at a tower block until it dies.

Expected: the block is destroyed, score increases, score popup appears — exactly
as before. The event does not change existing behaviour.

- [ ] **Step 5: Stop play mode and commit**

Stop play mode first, so the scene file is not left dirty.

```bash
git add Assets/Scripts/DestructableObjects/DOHealth.cs
git commit -m "add OnDeath event to DOHealth"
```

---

### Task 2: Add the `ExplosiveSoundManager` component

Written now, ahead of the VFX, so `ExplosiveObject` in Task 3 can reference it
without a second compile pass later. It contains no sound logic yet — it just
holds a config and plays it.

**Files:**
- Create: `Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveSoundManager.cs`

**Interfaces:**
- Consumes: `SO_AudioConfigBase.Play(AudioSource, float)` (existing).
- Produces: `public void PlayExplosionSound(float volumeMagnitude = 1f)` on
  `ExplosiveSoundManager`. Task 3's `PlayEffects` calls it; Task 7 wires the asset.

- [ ] **Step 1: Create the file**

In the Project window, navigate to
`Assets/Scripts/DestructableObjects/SpecialObjects/` (it exists — it holds
`ExplosiveObject.cs`). Right-click → Create → C# Script, name it exactly
`ExplosiveSoundManager`. Replace the generated contents with:

```csharp
using UnityEngine;

public class ExplosiveSoundManager : MonoBehaviour
{
    public SO_AudioConfigBase audioConfig;

    private AudioSource _audioSource;

    private void Awake() => _audioSource = GetComponent<AudioSource>();

    public void PlayExplosionSound(float volumeMagnitude = 1f)
    {
        if (_audioSource && audioConfig) audioConfig.Play(_audioSource, volumeMagnitude);
    }
}
```

This mirrors `DOSoundManager` deliberately, so it slots into the existing audio
pattern rather than inventing a new one. The null checks matter: there is no
explosion clip in the project yet, so `audioConfig` will be empty for a while and
the feature must still run silently.

- [ ] **Step 2: Save and let Unity compile**

Expected: no errors in the Console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveSoundManager.cs
git commit -m "add ExplosiveSoundManager component"
```

---

### Task 3: Implement `ExplosiveObject`

The core of the feature. The file already exists at
`Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveObject.cs` as an empty
`MonoBehaviour` stub — you are replacing its body.

**Files:**
- Modify: `Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveObject.cs`

**Interfaces:**
- Consumes: `DOHealth.OnDeath` (Task 1), `DOHealth.TakeDamage(float)`,
  `DOHealth.IsDead()`, `DOHealth.Die()` (existing),
  `ExplosiveSoundManager.PlayExplosionSound(float)` (Task 2),
  `CameraShakeManager.Shake(float)` (existing).
- Produces: serialized fields `radius`, `maxForce`, `explosionDamage`,
  `upwardBias`, `affectedLayers`, `explosionParticle`, `shakeForce`,
  `particleLifetime`. Tasks 4–8 set these in the Inspector.

- [ ] **Step 1: Replace the stub with the implementation**

Open `Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveObject.cs` and
replace the **entire file** with:

```csharp
using UnityEngine;

[RequireComponent(typeof(DOHealth))]
public class ExplosiveObject : MonoBehaviour
{
    [Header("Blast")]
    [SerializeField] private float radius = 3f;
    [SerializeField] private float maxForce = 1000f;
    [SerializeField] private float explosionDamage = 150f;
    [SerializeField] private float upwardBias = 0.35f;
    [SerializeField] private LayerMask affectedLayers = ~0;

    [Header("Presentation")]
    [SerializeField] private ParticleSystem explosionParticle;
    [SerializeField] private float shakeForce = 2f;
    [SerializeField] private float particleLifetime = 3f;

    private DOHealth _health;
    private bool _hasExploded;

    private void Awake() => _health = GetComponent<DOHealth>();
    private void OnEnable() => _health.OnDeath += Explode;
    private void OnDisable() => _health.OnDeath -= Explode;

    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, affectedLayers);
        Vector3 origin = transform.position;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            Vector3 toTarget = hit.bounds.center - origin;
            float distance = toTarget.magnitude;
            float falloff = Mathf.Clamp01(1f - distance / radius);
            if (falloff <= 0f) continue;

            // Damage first, and independently of whether the target can be pushed,
            // so static scenery still takes blast damage without moving.
            DOHealth targetHealth = hit.GetComponentInParent<DOHealth>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(explosionDamage * falloff);
                if (targetHealth.IsDead()) targetHealth.Die();
            }

            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body.isKinematic) continue;

            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.up;
            direction = (direction + Vector3.up * upwardBias).normalized;

            body.AddForceAtPosition(
                direction * (maxForce * falloff), hit.bounds.center, ForceMode.Impulse);
        }

        PlayEffects(origin);
    }

    private void PlayEffects(Vector3 origin)
    {
        if (explosionParticle != null)
        {
            ParticleSystem effect = Instantiate(explosionParticle, origin, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, particleLifetime);
        }

        CameraShakeManager shake = FindAnyObjectByType<CameraShakeManager>();
        if (shake != null) shake.Shake(shakeForce);

        ExplosiveSoundManager sound = GetComponent<ExplosiveSoundManager>();
        if (sound != null) sound.PlayExplosionSound(1f);
    }
}
```

Why specific choices were made, so you can tune with intent rather than guessing:

- **`AddForceAtPosition`, not `AddForce`.** Force applied at a position generates
  torque, so blocks tumble and spin. This is the single biggest contributor to the
  blast reading as an explosion; without it everything slides outward flatly.
- **`ForceMode.Impulse`, not `Force`.** `Force` is continuous and scales by frame
  time; `Impulse` is an instant kick. An explosion is an instant event.
- **`upwardBias` added to the direction before normalizing.** Without it, objects
  below the blast origin get pushed into the floor.
- **`hit.attachedRigidbody`, not `hit.GetComponent<Rigidbody>()`.** Resolves
  correctly for child colliders sharing a parent body.
- **Damage applied before the kinematic check.** A static object can't be pushed,
  but it should still take blast damage.
- **Linear falloff** `clamp01(1 - d/r)`: full force at the centre, zero at the
  edge. A flat force would make a block 10 cm away react like one 3 m away.

- [ ] **Step 2: Save and let Unity compile**

Save, switch to Unity, wait for the compile spinner.

Expected: **no errors.** If you see `'DOHealth' does not contain a definition for
'OnDeath'`, Task 1 was not completed — go back and do it.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveObject.cs
git commit -m "implement ExplosiveObject blast physics and area damage"
```

---

### Task 4: Create the `Destructible` layer and assign it

`ExplosiveObject.affectedLayers` defaults to `~0` (everything). Left that way, the
blast would also hit the catapult/spawner, which carries a Rigidbody at runtime —
your own TNT would launch your own catapult.

**Files:**
- Modify: `ProjectSettings/TagManager.asset` (via the Editor UI — do not hand-edit)
- Modify: layer assignment on building and enemy prefabs

**Interfaces:**
- Consumes: `affectedLayers` field (Task 3).
- Produces: a named layer `Destructible` that Tasks 5–8 assign.

- [ ] **Step 1: Add the layer**

In Unity: **Edit → Project Settings → Tags and Layers**. Under **Layers**, find
the first empty user slot (layer 8 is `User Layer 8`). Name it `Destructible`.

- [ ] **Step 2: Assign the layer to the destructible prefabs**

For each of these, select the prefab and set the **Layer** dropdown at the top of
the Inspector to `Destructible`:

- `Assets/Prefabs/Building/P_tower-square-mid.prefab`
- `Assets/Prefabs/Building/PV_tower-square-mid Variant.prefab`
- `Assets/Prefabs/Building/PV_tower-square-mid-color.prefab`
- `Assets/Prefabs/Building/PV_tower-square-top-roof-high.prefab`
- `Assets/Prefabs/Building/PV_wall.prefab`
- `Assets/Prefabs/Building/PV_TNT_Cube.prefab`
- `Assets/Prefabs/Enemy/character-orc.prefab`

Unity will ask whether to apply to children too — choose **Yes, change children**.
Children that are only visual geometry may stay on `Default` if you prefer; the
collider is what the physics query needs.

Leave the `Tiles` ground on `Default`. It has no Rigidbody, so it cannot be pushed
either way — the layer is about keeping the loop cheap and excluding the catapult.

- [ ] **Step 3: Verify the layer took**

Select `PV_TNT_Cube.prefab` and confirm the Inspector's Layer dropdown reads
`Destructible`, not `Default`.

- [ ] **Step 4: Commit**

```bash
git add ProjectSettings/TagManager.asset Assets/Prefabs
git commit -m "add Destructible layer and assign to blocks and enemies"
```

---

### Task 5: Set up the TNT prefab and verify the logic core

This task makes TNT actually explode. **Do not add particles or sound yet** — the
point is to feel the raw physics and tune it, before VFX covers up the feel.

**Files:**
- Modify: `Assets/Prefabs/Building/PV_TNT_Cube.prefab`
- Modify: `Assets/Scenes/SampleScene.unity` (place a test cluster)

**Interfaces:**
- Consumes: `ExplosiveObject` and `ExplosiveSoundManager` components (Tasks 2–3),
  `Destructible` layer (Task 4).
- Produces: a working TNT prefab, and tuned `radius` / `maxForce` /
  `explosionDamage` / `upwardBias` values that Task 6's VFX will be authored to
  match.

- [ ] **Step 1: Add the components to the TNT prefab**

Open `Assets/Prefabs/Building/PV_TNT_Cube.prefab` in Prefab Mode (double-click it).
Add to the root GameObject:

1. `ExplosiveObject` (Add Component → search "Explosive") — `DOHealth` is
   already present (verified: `PV_TNT_Cube` is a variant of
   `P_tower-square-mid`, which carries `DOHealth`, `DOManageImpact`, one
   `BoxCollider`, and a dynamic Rigidbody). The
   `[RequireComponent(typeof(DOHealth))]` attribute relies on that inheritance.
2. `ExplosiveSoundManager`
3. `AudioSource` — set **Spatial Blend** to `1` (3D) and **Play On Awake** to off.

Set `affectedLayers` on `ExplosiveObject` to `Destructible` only.

- [ ] **Step 2: Lower TNT health so one solid hit kills it**

On the same prefab, find the `DOHealth` component and set:

- `maxHealth` = `30` (default is `100` — normal blocks should survive a hit, TNT
  should not)
- `deathScoreValue` = `100` (keep as-is; tune later if you want TNT to be worth more)

Thirty is chosen so a full-power boulder reliably detonates it in one shot, while
a glancing blow does not.

- [ ] **Step 3: Place a test cluster in the scene**

Exit Prefab Mode. In `SampleScene`, drag `PV_TNT_Cube` into the scene three times
in a row, lined up next to a tower, roughly one block apart (about 1–1.5 m gaps,
inside the 3 m radius). Save the scene.

This is a throwaway test rig — delete it in Task 8 once tuning is done, or keep it
as a sandbox. Do not commit the scene with a stray cluster unless you want it.

- [ ] **Step 4: Test a single explosion**

Play the scene. Aim a full-power shot at the first TNT cube.

Expected:
- The cube explodes on death.
- Neighbouring blocks are thrown outward and **tumble/spin**, not slide flatly.
- Debris **arcs upward** — nothing is driven down into the floor.
- Blocks near the blast visibly die.

If nothing moves: check the cube's Rigidbody is not kinematic, and that
`affectedLayers` genuinely includes `Destructible`. `Physics.OverlapSphere`
returning nothing is almost always a layer mask mistake.

- [ ] **Step 5: Test chaining**

Still playing (or restart and shoot again), hit the first TNT cube — the one that
breaks the chain.

Expected: the second TNT cube detonates, which detonates the third. Each explodes
exactly **once**. Watch the score: three TNT cubes should award their score once
each, not repeatedly.

If a cube refuses to chain: its health is too high to be killed by the blast.
Raise `explosionDamage` or lower TNT `maxHealth`. If a cube explodes twice, the
`_hasExploded` guard is not working — that would mean Task 3's file was not
replaced wholesale.

- [ ] **Step 6: Confirm the catapult survives**

Aim at TNT placed close to your own catapult and detonate it.

Expected: the catapult does **not** get launched. If it does, `affectedLayers`
includes more than `Destructible`.

- [ ] **Step 7: Tune the four numbers**

Now adjust in the Inspector, replaying between changes. Work in this order,
because they interact:

1. `radius` — how far the blast reaches. Start at 3; the tower blocks are ~1–2 m.
2. `maxForce` — how violently things are thrown. 1000 is a starting point.
3. `upwardBias` — the arc. 0.35 lifts debris; lower it for a flatter, more
   ground-hugging blast, raise it to launch things skyward.
4. `explosionDamage` — how lethal point-blank is. 150 kills default 100-health
   blocks outright.

Change one at a time. Note the values you settle on — Task 6 authors the particle
size to match `radius`.

- [ ] **Step 8: Stop play mode and commit**

```bash
git add Assets/Prefabs/Building/PV_TNT_Cube.prefab
git commit -m "add ExplosiveObject to TNT prefab and tune blast"
```

Note: `maxHealth = 30` here makes TNT die-triggered by design. TNT is *not* using
`DOManageImpact`'s collision-threshold path — it explodes from its own death,
which is what makes chaining work.

---

### Task 6: Explosion particle effect

**Files:**
- Create: `Assets/Prefabs/ExplosionParticle.prefab`
- Modify: `Assets/Prefabs/Building/PV_TNT_Cube.prefab` (assign the reference)

**Interfaces:**
- Consumes: `explosionParticle` and `particleLifetime` fields (Task 3), tuned
  `radius` (Task 5).
- Produces: a particle prefab referenced by the TNT.

Per your usual workflow, this is authored **by hand in the Inspector from the
checklist below** rather than by editing prefab YAML.

- [ ] **Step 1: Create the prefab shell**

In the Hierarchy, right-click → **Effects → Particle System**. Rename it
`ExplosionParticle`. Drag it into `Assets/Prefabs/` to make it a prefab, then
delete the instance from the scene.

- [ ] **Step 2: Configure the fireball system**

Select the prefab in the Project window and click **Open** to enter Prefab Mode.
On the Particle System, set:

| Section | Setting | Value |
|---|---|---|
| Main | Duration | `0.6` |
| Main | Looping | **off** |
| Main | Start Lifetime | `0.45` |
| Main | Start Speed | `4` |
| Main | Start Size | `1.5` — scale this to roughly match your tuned `radius` |
| Main | Start Color | warm orange/yellow |
| Main | Max Particles | `200` |
| Main | Stop Action | **Destroy** |
| Emission | Rate over Time | `0` |
| Emission | Bursts | one burst, Time `0`, Count `30` |
| Shape | Shape | `Sphere` |
| Shape | Radius | `0.2` |
| Color over Lifetime | gradient | opaque → transparent, fading out |
| Size over Lifetime | curve | large → small |

`Start Size` should read as **at least** as wide as the physical `radius` from
Task 5. The blast should never look weaker than the force it applies.

- [ ] **Step 3: Add a smoke system as a second emitter**

Right-click the Particle System in the Hierarchy → **Create Empty Child**, add a
Particle System to it, and configure for lingering smoke:

| Section | Setting | Value |
|---|---|---|
| Main | Duration | `1.5` |
| Main | Looping | **off** |
| Main | Start Lifetime | `1.2` |
| Main | Start Speed | `1.2` |
| Main | Start Size | `2` |
| Main | Start Color | dark grey, low alpha |
| Main | Stop Action | **Destroy** |
| Emission | Bursts | one burst, Time `0`, Count `12` |
| Shape | Shape | `Sphere` |

Smoke outliving the fireball is what makes the explosion read as an event rather
than a flash.

- [ ] **Step 4: Add a spark burst (optional but recommended)**

Repeat Step 3 with a third system: high `Start Speed` (`8`), tiny `Start Size`
(`0.08`), warm colour, `Start Lifetime` `0.5`, burst count `40`. Sparks sell the
impact.

- [ ] **Step 5: Assign the prefab to the TNT**

Exit Prefab Mode. Open `PV_TNT_Cube.prefab` in Prefab Mode, select the root, and
drag `ExplosionParticle` from the Project window into the `Explosion Particle`
field of the `ExplosiveObject` component.

Set `particleLifetime` to `4` — long enough for the smoke to finish. The particle
systems' own **Stop Action → Destroy** is a backstop; `particleLifetime` is the
guaranteed cleanup.

- [ ] **Step 6: Test**

Play, detonate TNT near a tower. Expected: a fireball at the blast origin, smoke
lingering and fading, sparks. Confirm in the Hierarchy that the instantiated
particle objects disappear after a few seconds rather than accumulating. If they
pile up, you have a memory leak — check `particleLifetime` is set.

- [ ] **Step 7: Commit**

```bash
git add Assets/Prefabs/ExplosionParticle.prefab Assets/Prefabs/Building/PV_TNT_Cube.prefab
git commit -m "add explosion particle effect to TNT"
```

---

### Task 7: Camera shake

Camera shake is already implemented. This task only connects it, so it is small.

**Files:**
- Modify: `Assets/Prefabs/Building/PV_TNT_Cube.prefab` (tune `shakeForce`)

**Interfaces:**
- Consumes: `CameraShakeManager.Shake(float)` (existing), `shakeForce` (Task 3).
- Produces: nothing consumed downstream.

- [ ] **Step 1: Understand the scale before tuning**

`CameraShakeManager.Shake` multiplies the incoming value by `impulseStrength`,
which is `15`. The camera offset is clamped to `maxOffset = 0.6`. A full-power
player shot passes about `1` (`Projectile.shakeIntensity = 1`). So an explosion at
`shakeForce = 2` shakes roughly twice as hard as a normal shot — which is the
intent. Do **not** pass `50`; the clamp will eat it and it will look identical to
a much smaller value.

- [ ] **Step 2: Test and tune**

Play, detonate TNT. Expected: the camera kicks harder than a normal shot, then
springs back smoothly.

Adjust `shakeForce` on the TNT's `ExplosiveObject` if it is too subtle or too
violent. If the camera feels like it is being flung regardless of the value, you
have hit `maxOffset` — the fix is lowering `impulseStrength` on the
`CameraShakeManager`, not raising `shakeForce`.

- [ ] **Step 3: Watch the chaining case**

Detonate a three-cube chain. Expected: you get a shake per explosion. Three shakes
in quick succession is correct and feels good; if it is excessive, this is your
signal that chaining should award a reduced shake for secondary explosions — a
future tweak, not a bug. Do not change it now.

- [ ] **Step 4: Commit**

```bash
git add Assets/Prefabs/Building/PV_TNT_Cube.prefab
git commit -m "tune TNT camera shake"
```

---

### Task 8: Explosion sound

**Soft dependency.** The explosion SFX asset does not exist yet, and the feature
runs silently without it. Do not block on this task — do it whenever you have a
clip.

**Files:**
- Create: `Assets/ScriptableObjects/Audio/ExplosionSFX.asset`
- Modify: `Assets/Prefabs/Building/PV_TNT_Cube.prefab` (assign the asset)

**Interfaces:**
- Consumes: `ExplosiveSoundManager.audioConfig` (Task 2),
  `PlayExplosionSound(float)` called with `1f` from Task 3.
- Produces: nothing consumed downstream.

- [ ] **Step 1: Source a clip**

`Assets/SFX/` currently holds `Impact`, `Whoosh`, `Wood_Hit`, `BM` — nothing that
fits a blast. Add one to `Assets/SFX/` (the project uses CC0/royalty-free audio;
match that).

- [ ] **Step 2: Create the config asset**

In the Project window, right-click inside `Assets/ScriptableObjects/Audio/` →
**Create → Scriptable Objects → Audio → SO_AudioConfigBase**. Name it exactly
`ExplosionSFX`. Set:

- **Clips** — add your explosion clip(s). If you add several, one is picked at
  random per explosion.
- **Volume** — start at `0.8`. **Leave headroom**: for a chained blast, several
  audio sources fire within a few frames and each plays at full volume.
- **Min Pitch / Max Pitch** — `0.95` / `1.05` gives subtle variation so repeated
  explosions do not sound mechanical.

- [ ] **Step 3: Assign it to the TNT**

Open `PV_TNT_Cube.prefab` in Prefab Mode, select the root, and drag `ExplosionSFX`
into the `Audio Config` field of the `ExplosiveSoundManager`.

- [ ] **Step 4: Test**

Play and detonate TNT. Expected: the blast plays once per explosion. Confirm the
sound is 3D-positioned (it should attenuate with distance) and audibly does not
clip when three cubes chain.

Note: `SO_AudioConfigBase.Play` returns early if `source.isPlaying` — so a rapid
chain on the *same* cube cannot overlap its own sound. Different cubes each have
their own `AudioSource`, so a chain is several overlapping sources. That is
intended; it is what makes a chain sound bigger than a single blast.

- [ ] **Step 5: Commit**

```bash
git add Assets/SFX Assets/ScriptableObjects/Audio/ExplosionSFX.asset Assets/Prefabs/Building/PV_TNT_Cube.prefab
git commit -m "add explosion sound effect"
```

---

### Task 9: Update the GDD

Required by the GDD sync rule in `CLAUDE.md`. The language convention is
**English headers, Persian body** — match the existing file.

**Files:**
- Modify: `docs/GDD.md`

**Interfaces:**
- Consumes: the finished, tested feature.
- Produces: documentation only.

- [ ] **Step 1: Update the TNT row in §4.1**

In the block table, the TNT row currently reads:

```
| **TNT** | — | هنگام برخورد **منفجر میشود**؛ آسیب منطقهای به اطراف | `[Planned]` |
```

The behavior is now **death-triggered**, not impact-triggered — you chose
`Die-triggered` deliberately. Correct the description, and set the status badge to
reflect reality.

Use `[Partial]` if any part is still unfinished (e.g. sound not yet added), and
`[Implemented]` only once the whole thing works in the build. **Never mark it
`[Implemented]` based on this plan alone.**

- [ ] **Step 2: Add the chaining rule to §4.2**

§4.2 currently says the TNT explosion must affect neighbouring blocks and enemies.
Add the newly decided rule: explosions **chain** through clusters of TNT — a blast
that kills one TNT detonates it in turn. Write the body line in Persian, matching
the surrounding style.

- [ ] **Step 3: Commit**

```bash
git add docs/GDD.md
git commit -m "update GDD with TNT explosion and chaining rules"
```

---

## Verification Checklist

Run this after all tasks. Each line maps to a spec test.

**Logic:**
- [ ] A full-power shot at a TNT cube detonates it.
- [ ] Neighbouring blocks are thrown outward and **tumble**, not slide.
- [ ] Debris arcs upward; nothing is driven into the floor.
- [ ] A block adjacent to TNT dies without being hit directly.
- [ ] The tiled ground does not move.
- [ ] Two adjacent TNT cubes: one detonating destroys the other.
- [ ] Three in a row: the chain propagates through all three, each exactly once.
- [ ] Score is awarded once per object, not repeatedly.
- [ ] Static scenery near the blast takes damage but does not move.
- [ ] Objects outside `radius` are untouched.
- [ ] Your own catapult is not launched by your own TNT.

**Presentation:**
- [ ] The particle burst appears at the blast origin and self-destructs.
- [ ] Camera shake fires and is stronger than a normal shot, without flinging the view.
- [ ] Explosion SFX plays once per explosion and does not clip.

**Documentation:**
- [ ] TNT's GDD status badge matches reality.
- [ ] The chaining rule is documented in §4.2.

## Known Non-Blocking Notes

- **`PV_TNT_Cube.prefab` inherits its Rigidbody** from `P_tower-square-mid`
  (`m_IsKinematic: 0`). If an override is ever added making it kinematic, the push
  silently stops working — nothing errors, it just stops moving. Worth remembering
  if TNT one day "stops being thrown".
- **`DOHealth.DieRoutine`'s guard was `yield return null;`** (a no-op that waited a
  frame and continued) and was corrected to `yield break;` before this plan was
  written. It is unreachable anyway, since `Die()` guards first.
- **Multi-collider objects would take the blast twice.** Each block has exactly one
  `BoxCollider` and one `Rigidbody` on the same GameObject, so this does not occur
  today. If a future prefab has multiple colliders on one body, the loop needs
  deduplication by `attachedRigidbody`.