# TNT Explosion — Design

**Date:** 2026-09-26
**Status:** Approved, implementation plan pending
**Related:** `docs/GDD.md` §4.1 (blocks), §4.2 (rules)

## Goal

TNT blocks explode when destroyed, pushing nearby Rigidbodies away and damaging
nearby blocks and enemies. Explosions chain-react through clusters of TNT.

## Decisions

| Question | Decision |
|---|---|
| Chain reactions | **Yes.** A blast that kills another TNT detonates it in turn. |
| Trigger | **Die-triggered.** TNT has HP like any block; explosions happen on death. |
| Scope | **Full presentation**, physics and logic built and tuned first. |
| Falloff | **Linear**, `clamp01(1 - distance / radius)`. |
| Force mode | `ForceMode.Impulse` via `AddForceAtPosition`. |

## Why die-triggered

Every death in the project already funnels through `DOHealth.Die()`
(`Assets/Scripts/DestructableObjects/DOHealth.cs:38`). Hooking the explosion
there means:

- No new trigger-detection code (no velocity thresholds, no `OnCollisionEnter`).
- Damage, score, and score popup all come from the existing `DOHealth` path.
- **Chain reactions require no chaining code.** Explosion A damages B through the
  area-damage pass; B's health reaches zero; B's `Die()` fires; B explodes.
- Re-entrancy is already guarded: `Die()` returns early when `_isDead` is true,
  so a cube cannot detonate twice.

## Architecture

```
DOManageImpact  ──collision velocity──▶ DOHealth.TakeDamage()
                                              │
                                    health <= 0 ──▶ DOHealth.Die()
                                                        │
                                                        ├─ ScoreManager.AddScore()
                                                        ├─ ScorePopup
                                                        ├─ OnDeath event  ──▶ ExplosiveObject.Explode()
                                                        └─ Destroy after delayBeforeDestroy
                                                                                    │
                                          ┌─────────────────────────────────────────┤
                                          ▼                                         ▼
                              OverlapSphere → push + damage              VFX · shake · SFX
                                          │
                              (damage kills other TNT → their Die() → their Explode())
```

`DOHealth` gains one event. `ExplosiveObject` subscribes in `OnEnable` and
unsubscribes in `OnDisable`. Nothing else in the existing code changes.

### Explosion sequence

1. Guard: if already exploded, return. Set the flag immediately (prevents
   self-re-entry within the same frame).
2. `Physics.OverlapSphere(position, radius, layerMask)`.
3. Per hit, filter: skip self; skip colliders with no `Rigidbody`; skip
   kinematic bodies.
4. Compute `falloff`, direction, then push. Damage in the same loop.
5. After the physics pass: spawn VFX, shake, play SFX.

Steps 1–4 are the logic phase and are independently tunable. Step 5 is the
presentation phase and touches nothing in the loop.

## Components

### `ExplosiveObject` (rewrite of existing stub)

`Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveObject.cs`
— currently an empty `MonoBehaviour` stub.

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

Notes on specific choices:

- `AddForceAtPosition` at `hit.bounds.center`, not `AddForce`. Force applied at a
  position generates torque, so blocks tumble instead of sliding flatly. This is
  the largest single contributor to the blast reading as an explosion.
- `upwardBias` is added to the direction *before* normalizing. Without it, blocks
  below the blast origin get pushed into the floor.
- `hit.attachedRigidbody` rather than `hit.GetComponent<Rigidbody>()` — resolves
  correctly for child colliders that share a parent body. This matters because
  several building prefabs are prefab variants with nested colliders.
- `GetComponentInParent<DOHealth>()` matches how `DOManageImpact` reaches its own
  components.
- Damage is applied even when the impulse was skipped for a kinematic body, so
  static scenery still takes blast damage.

### `ExplosiveSoundManager` (new)

`Assets/Scripts/DestructableObjects/SpecialObjects/ExplosiveSoundManager.cs`
— mirrors `DOSoundManager` exactly, so it slots into the existing audio pattern.

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

**Important:** `SO_AudioConfigBase.Play` computes `source.volume = volume * volumeMagnitude`
(`Assets/ScriptableObjects/Audio/SO_AudioConfigBase.cs:15`) with no clamping. Pass a
normalized 0–1 value, never a raw explosion intensity, or volume exceeds 1.0.

### `DOHealth` change (targeted, minimal)

Add one event and raise it inside `DieRoutine`:

```csharp
public event System.Action OnDeath;

IEnumerator DieRoutine()
{
    if (_isDead) yield break;
    _isDead = true;

    OnDeath?.Invoke();

    ScoreManager.I.AddScore(deathScoreValue);
    // ...rest unchanged
}
```

`OnDeath?.Invoke()` is raised **before** score and popup, so the blast resolves
before the scoreboard settles. Ordering is a feel decision, not a correctness
one; it is easy to move.

The no-op guard `if (_isDead) yield return null;` was already corrected to
`yield break` by the user on 2026-09-26, so only the event needs adding.

## Tuning starting values

All `[SerializeField]` so they can be tuned in the Inspector without recompiling.

| Field | Start | Reasoning |
|---|---|---|
| `radius` | 3 | Tower blocks are roughly 1–2 m; reaches immediate neighbors. |
| `maxForce` | 1000 | `ForceMode.Impulse`; enough to visibly launch a wood block. |
| `explosionDamage` | 150 | Blocks default to `maxHealth = 100`, so a point-blank hit kills. |
| `upwardBias` | 0.35 | Lifts debris into an arc rather than into the floor. |
| `shakeForce` | 2 | `CameraShakeManager` multiplies by `impulseStrength = 15f` and clamps offset to 0.6. A full-power player shot passes ~1. `Projectile.Shoot` → `shakeIntensity = 1`. |
| TNT `maxHealth` | 30 | Lower than a normal block so a solid projectile hit reliably kills it in one shot. |

Radius serves physics and VFX separately by design: the particle effect is
typically authored **larger** than `radius`, because the visual should not look
weaker than the force it applies.

## Unity setup (manual, in the Editor)

These are Editor steps, not code. They are listed here because the feature does
not work without them.

1. **New layer** — `ProjectSettings` → Tags and Layers → add `Destructible`.
   The project has no free named layer (`TagManager.asset` defines the tags
   `Plane` and `Start`, and layers 8+ are all unnamed).
2. **Assign layers** — put building blocks and enemies on `Destructible`, then
   set `affectedLayers` on each `ExplosiveObject` to `Destructible` only.

   **What this actually protects against.** The world is tiled static FBX
   geometry (`kenney_tower-defense-kit/Models/FBX/tile-tree.fbx`) on layer
   `Default`, with colliders but **no Rigidbody** — the scene contains zero
   Rigidbodies. The `null`-Rigidbody guard in `Explode()` therefore already
   stops the ground from being pushed, so the layer mask is not what saves the
   ground.

   The mask earns its place for two other reasons: it stops the loop spending
   work on tens of static tiles, and it keeps the blast away from
   the `Spawner`/catapult (tagged `Start`), which does carry a Rigidbody at
   runtime and would otherwise be launched by the player's own TNT.

   The `Plane` tag is defined in `TagManager.asset` but applied to nothing in
   `SampleScene`. It should not be relied on.
3. **TNT prefab** — on `Assets/Prefabs/Building/PV_TNT_Cube.prefab`: add
   `ExplosiveObject`, `ExplosiveSoundManager`, an `AudioSource`, set
   `maxHealth = 30`, and assign the layer. The prefab is currently a variant of
   `P_tower-square-mid` with `m_AddedComponents: []`.
   **Note:** the Rigidbody is inherited from the base tower prefab
   (`m_IsKinematic: 0`). If an override is ever added that makes it kinematic,
   the push silently stops working for that object.
4. **Explosion SFX** — create an `SO_AudioConfigBase` asset at
   `Assets/ScriptableObjects/Audio/ExplosionSFX.asset` (right-click → Create →
   Scriptable Objects → Audio → SO_AudioConfigBase), drop in the clip(s), assign
   it to the TNT's `ExplosiveSoundManager`.
   No explosion clip exists yet in `Assets/SFX/`. The user will source and add
   one later — this is a soft dependency, not a blocker: the `ExplosiveSoundManager`
   null-guards its config and audio source, so the rest of the feature works and
   is testable without it.
5. **Particle prefab** — author `Assets/Prefabs/ExplosionParticle.prefab` in the
   Inspector: a fireball burst + smoke, with Stop Action → Destroy as a
   belt-and-braces alongside `particleLifetime`. Assign to `explosionParticle`.
   Per prior preference, this is built by hand in the Editor from a checklist
   rather than by editing prefab YAML.

## Testing

No test framework exists in this project, so verification is manual in
`SampleScene`.

**Logic phase (verify before adding VFX):**

1. Aim at a TNT cube with a full-power shot → it explodes; neighboring blocks are
   thrown outward and tumble, not slide.
2. A block adjacent to TNT dies without being directly hit.
3. Debris arcs upward; nothing is driven downward into the floor.
4. The tiled ground is unaffected (it has no Rigidbody, so it cannot be pushed
   regardless — this also confirms the `null` guard).
5. Place two TNT cubes adjacent: destroying one detonates the other (chain).
6. Place three in a row: the chain propagates through all three, each exactly once.
7. Explosion is not triggered twice by one cube (score awarded once).
8. Static scenery near the blast takes damage but does not move.
9. Blocks far outside `radius` are untouched.
10. The catapult/Spawner is not thrown by your own TNT (confirms the layer mask
    excludes it).

**Presentation phase:**

10. Particle burst appears at the blast origin and self-destructs.
11. Camera shake fires and is stronger than a normal shot, without flinging the view.
12. Explosion SFX plays once per explosion and does not clip.

## Scope boundaries

Deliberately **not** included:

- Explosive *projectiles* (GDD §5.1 Special #3) — separate feature, same
  `ExplosiveObject` reusable later.
- Pushback on the catapult or camera. The player's own equipment is not affected.
- Destruction debris fragments (the block is destroyed normally via `DOHealth`).
- Any change to `DOManageImpact` — the existing collision-damage path is relied
  on as-is, and rocket-assisted destruction of neighbors by debris is an emergent
  side effect, not a separate mechanic.

## GDD sync

This change notably alters game design (the TNT block moves from `[Planned]` to
implemented, and chain reactions are a new decided rule). Per the CLAUDE.md GDD
sync rule, `docs/GDD.md` must be updated in the same session:

- §4.1 block table: TNT row `[Planned]` → `[Partial]` or `[Implemented]` once
  the build actually supports it. Do not mark `[Implemented]` until it works.
- §4.2 rules: add the chaining rule (explosions propagate through TNT clusters).
- Body text stays Persian under English headers.