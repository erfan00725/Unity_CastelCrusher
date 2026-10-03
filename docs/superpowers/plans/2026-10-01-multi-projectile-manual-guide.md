# Multi-Projectile Types — Learning Implementation Guide

This guide walks you through building the multi-projectile system **yourself**, teaching the Unity and C# concepts behind each piece as you go. Every step follows the same pattern:

1. **Concept** — what Unity/C# feature you're about to learn and why it exists
2. **The code** — what to write, in full
3. **Walkthrough** — what each part of the code actually does

Reference docs:
- Design decisions (the *what/why* of the feature): `docs/superpowers/specs/2026-09-30-multi-projectile-types-design.md`
- Task-numbered plan (kept for reference): `docs/superpowers/plans/2026-09-30-multi-projectile-types.md`

> **Working style:** type the code yourself rather than pasting where you can — muscle memory is part of learning. When you want to check your typing against the reference, the plan doc has the same code blocks.

---

## The big picture (read this first)

You're building this system:

```
                    ┌─────────────────────┐
                    │  LevelData (SO)     │  "This level gives 8 boulders,
                    │  projectileLoadouts │   3 heavy, 2 explosive"
                    └─────────┬───────────┘
                              │ read on Awake
                              ▼
┌──────────────┐  selection   ┌─────────────────┐  spawn prefab of   ┌──────────────┐
│ Projectile   │◄────────────►│  BulletSpawner   │──────────────────►│  Projectile  │
│ Carousel UI  │  events      │  (per-type ammo) │                   │  (boulder /  │
│ (bottom bar) │              └─────────────────┘                   │   heavy /    │
└──────────────┘                      ▲                             │   explosive) │
       │                              │                               └──────┬───────┘
       │ reads state                  │ owns state                            │ on impact
       ▼                              │                                       ▼
┌──────────────────────────────────────────┐                          ┌────────────────┐
│        ProjectileSelector               │                          │ Explosion      │
│  "which type is selected right now"     │                          │ .TriggerBlast  │
└──────────────────────────────────────────┘                          │ (shared blast) │
                                                                      └────────────────┘
```

Three ideas hold it together — you'll meet them in every file:

1. **ScriptableObjects (SO)** are data assets. They're classes that live as `.asset` files in your project, not as objects in a scene. Think of them as "designer-editable config cards" — the same code reads different data depending on which asset you hand it. Your project already uses one: `SO_AudioConfigBase`. You'll create two new kinds: `SO_ProjectileType` (one per projectile) and `LevelData` (per-level ammo loadout).

2. **Events** let objects talk without knowing about each other. The carousel needs to repaint when the selection changes, and the spawner needs to know — but neither should hold a hard reference to the other's logic. Instead, the selector *announces* "selection changed!" and anyone interested can listen. You'll use the C# `event` / `Action<T>` pattern, which is Unity's backbone (think `OnCollisionEnter`, `DOHealth.OnDeath` in your code already).

3. **Separation of math from Unity objects.** Pure calculations (how many buttons fit on screen, how much leftover score is worth) go in plain static classes you can unit-test without entering Play mode. MonoBehaviours handle only the Unity-facing parts (spawning, UI, collisions). This is why the guide starts with test-verified math before any scene work.

**Phase order:** ① write + test the code → ② create assets and wire the scene in the Editor → ③ play and verify → ④ update the GDD.

---

# Phase 1 — Code

## Step 0: Assembly definitions (asmdef)

**Concept — assemblies.** Unity compiles all your scripts into DLLs called *assemblies*. By default, everything in `Assets/` (except `Editor/` folders) compiles into one big predefined assembly called `Assembly-CSharp`. An **assembly definition** file (`.asmdef`) says: "these folders compile into their own separate assembly." Why would you want that?

- **Faster iteration** — changing one assembly doesn't recompile everything.
- **Boundaries** — code in assembly A can't see code in assembly B unless it *references* it (like `using` between projects).
- **Testability** — Unity's Test Runner can only test code that's in an asmdef assembly. `Assembly-CSharp` can't be referenced by test code — so to unit-test anything, the code under test must live in its own assembly.

That's why Step 1 creates `CastleCrusher.Core`: your pure-math code moves there so tests can see it. Your MonoBehaviours stay in `Assembly-CSharp`, which *auto-references* custom assemblies (that's `"autoReferenced": true`), so `ScoreManager` can still `using CastleCrusher.Core`.

**The code** — create `Assets/Scripts/Core/CastleCrusher.Core.asmdef` (right-click folder → Create → Assembly Definition, or create a text file and rename; contents below):

```json
{
    "name": "CastleCrusher.Core",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Then create `Assets/Tests/EditMode/CastleCrusher.EditModeTests.asmdef`:

```json
{
    "name": "CastleCrusher.EditModeTests",
    "rootNamespace": "",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "CastleCrusher.Core"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Walkthrough:**
- `"name"` — the assembly's identity; other asmdefs refer to it by this string.
- `"references"` — which other assemblies this one can see. Your test assembly needs the NUnit framework (that's `nunit.framework.dll` in `precompiledReferences`), Unity's test runners, and `CastleCrusher.Core` (the code it will test).
- `"includePlatforms": ["Editor"]` — this assembly only compiles in the Unity Editor, never in a device build. Tests shouldn't ship to phones.
- `"autoReferenced": false` — don't let `Assembly-CSharp` automatically see the test assembly (keeps test code out of your game).
- `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` — only compile when Unity is in a test-including context (another safety rail so tests never ship).

⚠️ Note the folder split: `Assets/Scripts/Core/` (game code, any platform) vs `Assets/Tests/EditMode/` (editor-only tests). Unity maps folders to assemblies, so this structure matters.

---

## Step 1: Leftover score calculator (your first unit test)

**Concept — unit tests and TDD.** A *unit test* is a small program that calls your code with known inputs and asserts the output. Unity ships **NUnit** for this. Tests live in `Assets/Tests/`, run from `Window → General → Test Runner`, and execute in **EditMode** (no Play button needed — they just run your classes like any C# program). The red-green cycle: write the test first, watch it fail because the code doesn't exist (*red*), write the code, watch it pass (*green*). This guarantees the test actually tests something.

**Concept — tuples.** `(int remaining, float multiplier)` is a C# *tuple* — two values bundled into one. It's perfect here because each "entry" the calculator needs is a pair, and creating a full class for it would be overkill.

**What this file does:** computes the end-of-level bonus for shots you never fired, weighted per projectile type.

**The code:**

```csharp
using System.Collections.Generic;

namespace CastleCrusher.Core
{
    public static class LeftoverScoreCalculator
    {
        public static float Compute(IReadOnlyList<(int remaining, float multiplier)> entries, float scorePerUnusedShot)
        {
            float total = 0f;
            foreach (var entry in entries)
            {
                total += entry.remaining * scorePerUnusedShot * entry.multiplier;
            }
            return total;
        }
    }
}
```

**Walkthrough:**
- `namespace CastleCrusher.Core` — a namespace groups types so `LeftoverScoreCalculator` never collides with something else of the same name. It also matches the asmdef folder, which is convention. Consumers will write `using CastleCrusher.Core;`.
- `public static class` — *static* means it has no instances; you call `LeftoverScoreCalculator.Compute(...)` directly. Stateless math belongs here — no GameObject, no Inspector, trivially testable.
- `IReadOnlyList<...>` — "a list I promise only to read." More honest than `List<...>` when you won't modify the caller's data.
- The loop is the whole algorithm: `remaining × points-per-shot × type-multiplier`, summed. A multiplier of `0` naturally zeroes that line — that's why your design says "0 = leftover of this type scores nothing" with no special-case `if`.

**Now write the test first** (this is the red step — it won't compile yet, which *is* the failure). Create `Assets/Tests/EditMode/LeftoverScoreCalculatorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using CastleCrusher.Core;

public class LeftoverScoreCalculatorTests
{
    [Test]
    public void DefaultMultiplier_CountsLeftover()
    {
        var entries = new List<(int remaining, float multiplier)> { (3, 1f) };
        float result = LeftoverScoreCalculator.Compute(entries, 100f);
        Assert.AreEqual(300f, result);
    }

    [Test]
    public void ZeroMultiplier_ContributesNothing()
    {
        var entries = new List<(int remaining, float multiplier)>
        {
            (2, 0f),
            (1, 2f)
        };
        float result = LeftoverScoreCalculator.Compute(entries, 100f);
        Assert.AreEqual(200f, result);
    }

    [Test]
    public void EmptyEntries_ReturnZero()
    {
        float result = LeftoverScoreCalculator.Compute(new List<(int, float)>(), 100f);
        Assert.AreEqual(0f, result);
    }
}
```

**Walkthrough:**
- `[Test]` marks a method as a test case — Test Runner discovers methods by this attribute.
- `Assert.AreEqual(expected, actual)` fails the test if they differ.
- Three behaviors pinned down: normal math, the 0-multiplier rule from your design, and the empty-list edge case. Notice the test names are sentences — future-you will thank present-you.
- **Do this now:** switch to Unity, wait for compile. The test file should produce a red error (`LeftoverScoreCalculator` not found). That's green-worthy red. Then create the calculator file from above, recompile, open **Test Runner → EditMode → Run All** → 3/3 pass.

---

## Step 2: Carousel layout calculator

**What this file does:** answers "how many side buttons fit on this device?" in two pure functions — the adaptive-visibility QoL feature you added to the spec.

**Concept — `Mathf`.** Unity's `Mathf` is just math helpers operating on floats (`FloorToInt` rounds down, `Clamp` clamps to a range, `Max` guards negatives). Using it instead of `System.Math` avoids casting dance between `double` and `float` — Unity's world is floats.

**The code** — `Assets/Scripts/Core/CarouselLayoutCalculator.cs`:

```csharp
using UnityEngine;

namespace CastleCrusher.Core
{
    public static class CarouselLayoutCalculator
    {
        public static float AvailableSideWidth(float totalWidth, float centerButtonWidth)
        {
            return Mathf.Max(0f, (totalWidth - centerButtonWidth) * 0.5f);
        }

        public static int VisibleSideButtons(float availableSideWidth, float buttonWidth, int minVisible, int maxVisible)
        {
            if (buttonWidth <= 0f) return minVisible;
            int count = Mathf.FloorToInt(availableSideWidth / buttonWidth);
            return Mathf.Clamp(count, minVisible, maxVisible);
        }
    }
}
```

**Walkthrough:**
- `AvailableSideWidth`: the screen minus the center button, split between both sides. `(1000 - 100) / 2 = 450` per side. The `Mathf.Max(0f, ...)` prevents a negative width if the center button somehow exceeds the screen (defensive math costs one line).
- `VisibleSideButtons`: how many `buttonWidth`-sized slices fit in the available width — integer division floors it (450 ÷ 100 = 4.5 → 4) — then `Clamp` enforces the design bounds `[min, max]` = `[1, 3]`.
- The `buttonWidth <= 0f` guard prevents a division by zero if someone fat-fingers the Inspector field. Never trust Inspector input.

**Test** — `Assets/Tests/EditMode/CarouselLayoutCalculatorTests.cs`:

```csharp
using NUnit.Framework;
using CastleCrusher.Core;

public class CarouselLayoutCalculatorTests
{
    [Test]
    public void AvailableSideWidth_HalfRemainder()
    {
        Assert.AreEqual(450f, CarouselLayoutCalculator.AvailableSideWidth(1000f, 100f));
    }

    [Test]
    public void AvailableSideWidth_NeverNegative()
    {
        Assert.AreEqual(0f, CarouselLayoutCalculator.AvailableSideWidth(50f, 100f));
    }

    [Test]
    public void VisibleSideButtons_WideScreen_CapsAtMax()
    {
        Assert.AreEqual(3, CarouselLayoutCalculator.VisibleSideButtons(450f, 100f, 1, 3));
    }

    [Test]
    public void VisibleSideButtons_NarrowScreen_FloorsAtMin()
    {
        Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(40f, 100f, 1, 3));
    }

    [Test]
    public void VisibleSideButtons_ZeroButtonWidth_FloorsAtMin()
    {
        Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(100f, 0f, 1, 3));
    }
}
```

Each test documents a design rule: wide screens cap at 3, narrow ones floor at 1, degenerate input doesn't crash. Run Test Runner → now 8 passing (3 + 5).

---

## Step 3: Extract the explosion into shared code

**Concept — refactoring.** You're not changing behavior; you're *moving* it. TNT's explosion (`ExplosiveObject`) and your new explosive projectile need identical blast logic. Copy-pasting it would give you two sources of truth — fix a bug in one and forget the other. The fix: extract the blast into one static method both call.

**Concept — structs.** `BlastData` is a `struct`, not a `class`. Structs are small value types you can bundle fields into; here it's just a typed parameter bag so `TriggerBlast` doesn't need five separate arguments. `[System.Serializable]` makes it show up nested in the Inspector — Unity's serializer draws fields of serializable structs inline.

**What this file does:** `Explosion.TriggerBlast` damages and pushes everything in a radius, with distance falloff.

**The code** — `Assets/Scripts/DestructableObjects/SpecialObjects/Explosive/Explosion.cs`:

```csharp
using UnityEngine;

[System.Serializable]
public struct BlastData
{
    public float radius;
    public float maxForce;
    public float explosionDamage;
    public float upwardBias;
    public LayerMask affectedLayers;
}

public static class Explosion
{
    public static void TriggerBlast(Vector3 origin, BlastData data, Transform ignoreRoot = null)
    {
        Collider[] hits = Physics.OverlapSphere(origin, data.radius, data.affectedLayers);

        foreach (Collider hit in hits)
        {
            if (ignoreRoot != null && (hit.transform == ignoreRoot || hit.transform.IsChildOf(ignoreRoot))) continue;

            Vector3 toTarget = hit.bounds.center - origin;
            float distance = toTarget.magnitude;
            float falloff = Mathf.Clamp01(1f - distance / data.radius);
            if (falloff <= 0f) continue;

            DOHealth targetHealth = hit.GetComponentInParent<DOHealth>();
            if (targetHealth)
            {
                targetHealth.TakeDamage(data.explosionDamage * falloff);
                if (targetHealth.IsDead()) targetHealth.Die();
            }

            Rigidbody body = hit.attachedRigidbody;
            if (!body || body.isKinematic) continue;

            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.up;
            direction = (direction + Vector3.up * data.upwardBias).normalized;

            body.AddForceAtPosition(
                direction * (data.maxForce * falloff), hit.bounds.center, ForceMode.Impulse);
        }
    }
}
```

**Walkthrough** (this code moved from `ExplosiveObject` mostly verbatim — so your TNT behavior is preserved by construction):
- **`Physics.OverlapSphere(pos, radius, mask)`** — physics query returning every collider within `radius` of `pos`, filtered to `affectedLayers`. This is how explosions "see."
- **`ignoreRoot`** — skips the exploding object itself and its children, so a TNT block doesn't damage *itself* again (it's already dead). The `= null` makes it an *optional parameter*: callers who don't care write `TriggerBlast(pos, data)`.
- **Falloff** — `Clamp01(1 - distance/radius)` gives 1.0 at the epicenter, 0.0 at the edge. Damage and push both scale with it, so close things get hurt more.
- **Damage before push** — `GetComponentInParent<DOHealth>()` walks *up* the hierarchy (blocks may have health on a parent). Damage applies even to objects that can't be pushed (static scenery).
- **`hit.attachedRigidbody`** — the Rigidbody the collider rides on (null for static objects). `isKinematic` bodies are frozen — skip them.
- **`AddForceAtPosition(..., ForceMode.Impulse)`** — a one-shot kick at the hit point so things tumble realistically. `upwardBias` adds a little `Vector3.up` so debris pops *up*, not just sideways.

**Now refactor `ExplosiveObject.cs`** — replace the body of `Explode()` with:

```csharp
    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Explosion.TriggerBlast(transform.position, BuildBlastData(), transform);
        PlayEffects(transform.position);
    }

    private BlastData BuildBlastData()
    {
        return new BlastData
        {
            radius = radius,
            maxForce = maxForce,
            explosionDamage = explosionDamage,
            upwardBias = upwardBias,
            affectedLayers = affectedLayers
        };
    }
```

⚠️ **Critical:** keep every existing serialized field name (`radius`, `maxForce`, etc.) exactly as-is. Unity stores Inspector values in the scene file *keyed by field name* — rename one and every TNT block in your scene silently reverts to defaults. `BuildBlastData()` converts your existing fields into the struct, so nothing in the Inspector changes.

The loop you deleted now lives in `Explosion`; `PlayEffects` (particles/shake/sound) stays local because presentation isn't shared logic.

**Checkpoint:** Unity compiles; enter Play and blow up a TNT — it must chain and pop up exactly like before. Commit when ready: `extract shared Explosion.TriggerBlast from ExplosiveObject`.

---

## Step 4: ExplodeOnImpact component

**Concept — MonoBehaviour composition over inheritance.** You might reach for `class ExplosiveProjectile : Projectile`. Resist it: Unity's serialization and prefab workflow plays badly with deep inheritance, and your types share *behaviors* not *categories* (tomorrow you may want heavy + explosive). Instead: keep one `Projectile` and **compose** it with small components — `ExplodeOnImpact` is a component that adds "and also explode" to whatever it sits next to. `[RequireComponent(typeof(Projectile))]` auto-adds/guards the dependency.

**Concept — Unity messages.** `OnCollisionEnter(Collision)` isn't called by your code — Unity *messages* it whenever physics reports a new contact (requires a Collider + non-kinematic Rigidbody, which your projectile has). `Collision` carries details: the other object, relative velocity, contact points. You've seen this already in `Projectile`.

**What this file does:** when the explosive projectile hits anything (except the launch pad), it triggers the shared blast and destroys itself.

**The code** — `Assets/Scripts/Projectile/ExplodeOnImpact.cs`:

```csharp
using UnityEngine;

[RequireComponent(typeof(Projectile))]
public class ExplodeOnImpact : MonoBehaviour
{
    [Header("Blast")]
    [SerializeField] private BlastData blast = new BlastData
    {
        radius = 2.5f,
        maxForce = 150f,
        explosionDamage = 120f,
        upwardBias = 0.3f,
        affectedLayers = ~0
    };

    [Header("Presentation")]
    [SerializeField] private ParticleSystem explosionParticle;
    [SerializeField] private float shakeForce = 2f;
    [SerializeField] private float particleLifetime = 3f;
    [SerializeField] private float delayBeforeDestroy = 0.1f;

    private bool _hasExploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Start")) return;
        if (_hasExploded) return;
        _hasExploded = true;

        Explosion.TriggerBlast(transform.position, blast, transform);
        PlayEffects();

        Destroy(gameObject, delayBeforeDestroy);
    }

    private void PlayEffects()
    {
        if (explosionParticle)
        {
            ParticleSystem effect = Instantiate(explosionParticle, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, particleLifetime);
        }

        CameraShakeManager shake = FindAnyObjectByType<CameraShakeManager>();
        if (shake) shake.Shake(shakeForce);

        ExplosionSoundManager sound = GetComponent<ExplosionSoundManager>();
        if (sound) sound.PlayExplosionSound(1f);
    }
}
```

**Walkthrough:**
- **`[SerializeField] private`** — Unity concept: `private` hides the field from other classes, but `[SerializeField]` still shows it in the Inspector. Best of both worlds — encapsulated *and* designer-editable. This is the pattern to prefer over `public` fields.
- **`[Header("Blast")]`** — cosmetic Inspector group headers.
- **Field initializer `new BlastData { ... }`** — object-initializer syntax; these become the defaults for new components. `affectedLayers = ~0` is "everything" (bitwise NOT of 0 flips all mask bits) — you'll narrow it to the Physical layer in the Editor later.
- **Guard flags:** `CompareTag("Start")` skips the sling pad (same convention `Projectile` uses); `_hasExploded` ensures one explosion even if physics reports several contacts in the same tick. `if (_hasExploded) return;` before setting it true — the *guard clause* pattern, identical to `ExplosiveObject`.
- **`Destroy(gameObject, delayBeforeDestroy)`** — Unity's deferred destroy: the object lives `0.1s` more (gives the hit-sound from `Projectile` a frame to play) then vanishes. Side effect worth knowing: destroying the GameObject also stops its coroutines — so `Projectile`'s scheduled 0.5s self-destruct becomes moot. One destroy wins; no double-free.
- **Presentation mirrors TNT:** instantiate a *copy* of the particle prefab (then destroy the copy after `particleLifetime` — copies aren't pooled, this is the simple lifecycle), shake via your existing singleton-ish `CameraShakeManager`, sound via `GetComponent` which returns `null` if absent — guarded by `if (sound)`, so the component is optional.

**Checkpoint:** compiles; behavior comes in Phase 2 after the prefab exists.

---

## Step 5: The two ScriptableObjects

**Concept — ScriptableObjects as config.** A `ScriptableObject` is a class deriving from `ScriptableObject` that you instantiate as an **asset file**, not a scene object. Key properties for your design:
- **Shared:** every reference to `Boulder.asset` reads the same instance — change `ammoCount` once, all levels see it.
- **No scene dependency:** assets survive scene edits; levels reference them.
- **Designer-friendly:** they show in a clean Inspector via `[CreateAssetMenu]`, which adds your entries to the right-click Create menu.

Your project already follows this pattern with `SO_AudioConfigBase` — you're extending it.

**The code** — `Assets/Scripts/Projectile/SO_ProjectileType.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileType", menuName = "Castle Crusher/Projectile Type")]
public class SO_ProjectileType : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public int ammoCount = 5;
    public Projectile prefab;
    [Min(0f)] public float scoreMultiplier = 1f;
}
```

**Walkthrough:**
- `[CreateAssetMenu(menuName = "Castle Crusher/Projectile Type")]` — adds **Create → Castle Crusher → Projectile Type** to the Project window's context menu. `fileName` is the default asset name.
- `Projectile prefab` — this is the linchpin of your chosen architecture: **each type carries its own prefab**. The spawner never hardcodes behavior; it just instantiates `type.prefab`. Adding a 4th type later = new prefab + new asset, zero code.
- `[Min(0f)]` — Inspector validator: the field can't be typed below 0 (your design: minimum 0). Unity draws a slider-ish constraint; it also documents intent to readers.
- Note there are **no methods** — this type is pure data. Behavior lives on the prefabs (components), values live here. That split is the whole design.

`Assets/Scripts/Level/LevelData.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Castle Crusher/Level Data")]
public class LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();
}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}
```

**Walkthrough:**
- **A second SO**, this one per-level: "what ammo does level 1-3 grant?" The list overrides the SO's default `ammoCount`, so one `SO_ProjectileType` can be granted 8 shots in level 1 and 3 in level 5 without touching the shared asset.
- **`[Serializable]` on a plain class** — trick worth knowing: non-Unity classes don't appear in Inspectors unless marked `[Serializable]`. Once marked, Unity serializes its public fields — that's why `ProjectileLoadout` becomes an editable row in `LevelData`'s list automatically.
- `new List<ProjectileLoadout>()` — field initializer so the list is never null when Unity deserializes a fresh asset.

---

## Step 6: ProjectileSelector — state + events

**Concept — the observer pattern with C# events.** Selection state must be readable by the carousel, the spawner, and the launcher — but none of them should *own* it. Classic solution: one owner, and everyone else subscribes to changes.

```csharp
public event Action<SO_ProjectileType> OnSelectionChanged;
```

Decoded piece by piece:
- `Action<SO_ProjectileType>` — a *delegate type*: a variable that holds a list of methods taking one `SO_ProjectileType` and returning void.
- `event` — restricts outsiders to `+=` (subscribe) and `-=` (unsubscribe). Only the declaring class can `Invoke` it. This prevents accidental `= null` wipes by third parties.
- The convention `OnXxx` signals "this is an event others can hear."

**Why must subscribers unsubscribe?** Unity objects that are destroyed (scene unload, disabling) become "fake null." A lingering subscription keeps the *publisher's* delegate list pointing at a dead target → `MissingReferenceException` on next invoke. `OnEnable`/`OnDisable` are the canonical subscribe/unsubscribe pair in Unity — they run every time the object activates.

**The code** — `Assets/Scripts/Projectile/ProjectileSelector.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileSelector : MonoBehaviour
{
    public List<SO_ProjectileType> availableTypes = new List<SO_ProjectileType>();

    public event Action<SO_ProjectileType> OnSelectionChanged;

    public int SelectedIndex { get; private set; }
    public SO_ProjectileType SelectedType =>
        availableTypes.Count > 0 ? availableTypes[SelectedIndex] : null;

    public void Initialize(List<SO_ProjectileType> types)
    {
        availableTypes = types ?? new List<SO_ProjectileType>();
        SelectedIndex = 0;
        OnSelectionChanged?.Invoke(SelectedType);
    }

    public void SelectIndex(int index)
    {
        if (availableTypes.Count == 0) return;
        if (index == SelectedIndex) return;

        SelectedIndex = ((index % availableTypes.Count) + availableTypes.Count) % availableTypes.Count;
        OnSelectionChanged?.Invoke(SelectedType);
    }

    public void SelectNext()
    {
        if (availableTypes.Count == 0) return;
        SelectIndex((SelectedIndex + 1) % availableTypes.Count);
    }

    public bool SelectNextWithAmmo(Func<SO_ProjectileType, bool> hasAmmo)
    {
        if (availableTypes.Count == 0) return false;

        for (int i = 1; i <= availableTypes.Count; i++)
        {
            int idx = (SelectedIndex + i) % availableTypes.Count;
            if (hasAmmo(availableTypes[idx]))
            {
                SelectIndex(idx);
                return true;
            }
        }
        return false;
    }
}
```

**Walkthrough:**
- **Properties with `{ get; private set; }`** — outsiders can *read* `SelectedIndex` but only this class writes it. State is protected; mutation goes through methods that enforce invariants (like firing the event). Note `SelectedType` is an *expression-bodied* computed property (`=>`): no backing field — it's derived on every read, so it can never be stale.
- **`selectedType` null-when-empty** — `availableTypes.Count > 0 ? ... : null` avoids `ArgumentOutOfRangeException` before initialization. Returning `null` from Unity-adjacent code is fine; callers use `if (type)` guards (Unity's `==` overload handles destroyed/null objects).
- **`types ?? new List<...>()`** — the *null-coalescing* operator: use the right side if the left is null. Defensive init in one expression.
- **Wrap-around math** `((index % count) + count) % count` — the double-modulo idiom. The first `% count` handles overflow (index 5 of 3 types), the `+ count` handles *negatives* (C# `%` keeps the dividend's sign: `-1 % 3 == -1`), the second `%` finishes the job. `(−1 + 3) % 3 = 2` ✓. This lets callers pass any integer without pre-validating.
- **`SelectIndex` early-returns** on same-index: re-selecting the current type shouldn't spam `OnSelectionChanged` → no pointless UI rebuilds. (Guard-clause style, same as `_hasExploded`.)
- **`OnSelectionChanged?.Invoke(...)`** — the `?.` is the null-conditional operator: only invoke if the delegate isn't null (i.e., someone subscribed). Without it, invoking an empty event throws `NullReferenceException`. This exact idiom is everywhere in Unity code — you already have `OnDeath?.Invoke()` in `DOHealth`.
- **`SelectNextWithAmmo(Func<...>)`** — introduces *function parameters*. Instead of the selector knowing what "ammo" means (that's the spawner's domain), the caller *passes the question* as a lambda: `selector.SelectNextWithAmmo(spawner.HasAmmo)`. Method group → `Func<>` conversion. This keeps the selector decoupled — testable with a fake `x => true`.

---

## Step 7: BulletSpawner — per-type ammo

**Concept — dictionaries.** `Dictionary<TKey, TValue>` is a hash map: instant lookup by key. Here: `SO_ProjectileType → int` remaining. Compare to a `List<int>` with parallel indices — the dictionary makes `GetRemaining(boulderType)` self-documenting and immune to order bugs.

**Concept — events with two parameters.** `Action<SO_ProjectileType, int>` — same delegate idea as before, now carrying (type, new-count) so the UI knows exactly which badge to refresh.

**What this file does:** owns ammo, fills itself from `LevelData` on Awake, spawns the selected type's prefab, auto-switches when a type runs dry.

**The code** — rewrite `Assets/Scripts/Projectile/BulletSpawner.cs` fully:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class BulletSpawner : MonoBehaviour
{
    public LevelData levelData;
    public ProjectileSelector selector;
    public UIManager uiManager;

    public event Action<SO_ProjectileType, int> OnAmmoChanged;

    private readonly Dictionary<SO_ProjectileType, int> _ammo = new Dictionary<SO_ProjectileType, int>();

    private void Awake()
    {
        List<ProjectileLoadout> loadout;
        if (levelData != null && levelData.projectileLoadouts.Count > 0)
        {
            loadout = levelData.projectileLoadouts;
        }
        else
        {
            loadout = new List<ProjectileLoadout>();
            foreach (SO_ProjectileType type in selector.availableTypes)
            {
                loadout.Add(new ProjectileLoadout { projectileType = type, ammoCount = type.ammoCount });
            }
        }

        InitializeAmmo(loadout);
    }

    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
    }

    public void InitializeAmmo(List<ProjectileLoadout> loadout)
    {
        _ammo.Clear();

        List<SO_ProjectileType> types = new List<SO_ProjectileType>();
        foreach (ProjectileLoadout entry in loadout)
        {
            if (entry.projectileType == null) continue;
            types.Add(entry.projectileType);
            _ammo[entry.projectileType] = Mathf.Max(0, entry.ammoCount);
        }

        selector.Initialize(types);
        RefreshSelectedAmmoText();
    }

    public bool HasAmmo(SO_ProjectileType type)
    {
        return type != null && _ammo.TryGetValue(type, out int remaining) && remaining > 0;
    }

    public int GetRemaining(SO_ProjectileType type)
    {
        return type != null && _ammo.TryGetValue(type, out int remaining) ? remaining : 0;
    }

    public Projectile InstantiateBullet()
    {
        SO_ProjectileType type = selector.SelectedType;

        if (type != null && !HasAmmo(type))
        {
            if (!selector.SelectNextWithAmmo(HasAmmo))
            {
                if (uiManager) uiManager.SetBulletCountText(0);
                return null;
            }
            type = selector.SelectedType;
        }

        if (type == null || !HasAmmo(type))
        {
            if (uiManager) uiManager.SetBulletCountText(0);
            return null;
        }

        _ammo[type]--;
        OnAmmoChanged?.Invoke(type, _ammo[type]);
        RefreshSelectedAmmoText();

        return Instantiate(type.prefab, transform.position, transform.rotation, transform);
    }

    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        RefreshSelectedAmmoText();
    }

    private void RefreshSelectedAmmoText()
    {
        if (uiManager)
        {
            uiManager.SetBulletCountText(GetRemaining(selector.SelectedType));
        }
    }
}
```

**Walkthrough:**
- **`Awake()` vs `Start()`** — Unity lifecycle: all objects' `Awake` run before any `Start`. The spawner initializes ammo (and calls `selector.Initialize`, which fires the selection event) in `Awake`, so by the time `BuletLuncher.Start` calls `InstantiateBullet()`, everything is ready. Getting this order right is half of Unity programming.
- **The Awake fallback path** — if no `levelData` asset is assigned (early prototyping), it builds the loadout from the selector's own list using each SO's default `ammoCount`. One entry point (`InitializeAmmo`) keeps the logic in a single place.
- **`_ammo.TryGet(key, out int remaining)`** — the idiomatic way to read a dictionary: returns `false` if the key is missing instead of throwing. The `out` parameter hands you the value directly. Notice the reusable pattern in both `HasAmmo` and `GetRemaining`.
- **`_ammo[type]--;` then `OnAmmoChanged?.Invoke(...)`** — mutate first, *then* announce — subscribers always observe the new state.
- **`InstantiateBullet()` flow:**
  1. Take the selected type; if it's out of ammo, ask the selector to hop to the next type that has some (`SelectNextWithAmmo(HasAmmo)` — passing the method group, the lambda-free form).
  2. Still nothing? Return `null`. Your existing `BuletLuncher` already null-checks (`if (_activeBullet)`) — the ammo-exhausted state degrades gracefully, no new failure mode.
  3. Decrement, announce, update HUD text, spawn `type.prefab` — parented to the spawner (`Instantiate(prefab, pos, rot, parent)` overload) so the loaded bullet rides the catapult, exactly like the old code.
- **`private readonly Dictionary...`** — `readonly` = assigned once (here, at field init) and never re-pointed. The *contents* still mutate — readonly protects the reference, not the data.
- **Naming:** `_ammo` with underscore prefix is the private-field convention already used across your codebase (`_rb`, `_health`).
- ⚠️ Your old fields (`projectilePrefab`, `maxBulletsCount`) are gone — Unity will show a harmless "missing serialized field" note on the scene component. Cleared in Phase 2.

**Checkpoint:** compiles. ⛔ Don't press Play yet — `selector` isn't wired; it would NRE.

---

## Step 8: ScoreManager bonus method

**Concept — thin wrappers over pure logic.** `ScoreManager` is a MonoBehaviour with UI side effects; the *math* lives in the tested calculator. The method below is glue: convert the calculator's float to an int score and push it through `AddScore` (which already updates the HUD).

⚠️ Note: **nothing calls this yet** — the results screen that should is `[Planned]` in the GDD and out of scope. You're shipping a tested, documented API for it to use later. That's deliberate, not dead code.

Add to `Assets/Scripts/ScoreManager.cs`:

```csharp
using System.Collections.Generic;
using CastleCrusher.Core;

    [Header("Leftover shots")]
    public float scorePerUnusedShot = 100f;

    public void AddLeftoverShotBonus(IReadOnlyList<(int remaining, float multiplier)> entries)
    {
        int bonus = Mathf.RoundToInt(LeftoverScoreCalculator.Compute(entries, scorePerUnusedShot));
        if (bonus > 0) AddScore(bonus);
    }
```

**Walkthrough:**
- `using CastleCrusher.Core;` at the top of the file — this works because `Assembly-CSharp` auto-references your Core asmdef (the `"autoReferenced": true` from Step 0 — now you see why it mattered).
- `[Header(...)]` groups the field in the Inspector for later balancing.
- `Mathf.RoundToInt` — score is an `int` in your UI; round once at the boundary rather than inside the calculator (floats stay floats until presentation demands otherwise).
- `if (bonus > 0)` — skipping the `AddScore` call for zero avoids a pointless HUD refresh.

---

## Step 9: CarouselWindow — which buttons are visible

**Concept — pure windowing logic.** The carousel shows a *window* of types centered on the selection, wrapping around the ends (classic circular list). If 3 of 10 types fit, selecting index 9 shows `[8, 9, 0]`. Computing that is pure arithmetic — so it goes in Core with tests, not buried in a MonoBehaviour.

**The code** — `Assets/Scripts/Core/CarouselWindow.cs`:

```csharp
using System;

namespace CastleCrusher.Core
{
    public static class CarouselWindow
    {
        public static int[] Window(int selectedIndex, int typeCount, int visiblePerSide)
        {
            if (typeCount <= 0) return Array.Empty<int>();

            // All types fit -> show them all in natural order
            if (typeCount <= visiblePerSide * 2 + 1)
            {
                int[] all = new int[typeCount];
                for (int i = 0; i < typeCount; i++) all[i] = i;
                return all;
            }

            int width = visiblePerSide * 2 + 1;
            int start = selectedIndex - visiblePerSide;
            int[] result = new int[width];
            for (int i = 0; i < width; i++)
            {
                int idx = start + i;
                result[i] = ((idx % typeCount) + typeCount) % typeCount;
            }
            return result;
        }
    }
}
```

**Walkthrough:**
- `Array.Empty<int>()` — the modern way to return an empty array (no allocation, no `null` to guard against).
- **The all-fit special case** — with 3 types and room for 7, every layout is "all of them," and natural order `[0,1,2]` is what you want regardless of selection. (Without this branch, selecting index 1 would return `[1,2,0]` — correct wrap math, wrong visual order. You'd catch this in the test below.)
- Otherwise: window `width = 2×side + 1`, start at `selected − side`, and reuse the double-modulo idiom from `SelectIndex` to wrap. Example: `Window(4, 5, 1)` → start 3 → `[3,4,0]`.

**Test** — `Assets/Tests/EditMode/CarouselWindowTests.cs`:

```csharp
using NUnit.Framework;
using CastleCrusher.Core;

public class CarouselWindowTests
{
    [Test]
    public void ThreeTypes_AllShown()
    {
        int[] window = CarouselWindow.Window(1, 3, 3);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, window);
    }

    [Test]
    public void WindowWrapsAtStart()
    {
        int[] window = CarouselWindow.Window(0, 5, 1);
        CollectionAssert.AreEqual(new[] { 4, 0, 1 }, window);
    }

    [Test]
    public void WindowWrapsAtEnd()
    {
        int[] window = CarouselWindow.Window(4, 5, 1);
        CollectionAssert.AreEqual(new[] { 3, 4, 0 }, window);
    }

    [Test]
    public void CenterOfLargeWindow()
    {
        int[] window = CarouselWindow.Window(2, 10, 2);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, window);
    }

    [Test]
    public void Empty_ReturnsEmpty()
    {
        Assert.AreEqual(0, CarouselWindow.Window(0, 0, 2).Length);
    }
}
```

**Checkpoint:** Test Runner → EditMode → Run All → **13/13 pass** (3 leftover-score + 5 layout + 5 window). All green, zero failures.

---

## Step 10: ProjectileCarouselUI — the HUD (biggest file)

**Concept — you'll use four Unity ideas together here:**

1. **`OnRectTransformDimensionsChange`** — Unity message fired when a RectTransform resizes (device rotate, Game view change). This is the hook for your adaptive-width feature: recompute the visible count whenever width changes. (Guard with `_lastSeenWidth` because Unity can fire it redundantly.)

2. **Coroutines** — `IEnumerator`-based functions that can `yield return` across frames without blocking. `StartCoroutine(TweenButtons(...))` begins it; `yield return null` waits one frame; `StopCoroutine` cancels. Tweens (animations driven by math over time) are the canonical use. Compare with `DelaySpawn` in your `BuletLuncher` — same mechanic.

3. **`Mathf.Lerp(a, b, t)`** — linear interpolation: `t=0` → a, `t=1` → b, in between → blend. Feed it `elapsed/duration` (clamped 0..1) and you have an animation curve.

4. **Closures** — in `onClick.AddListener(() => selector.SelectIndex(capturedIndex))`, the lambda *captures* `capturedIndex`. The `captured` copy exists because the loop variable `typeIndex` changes each iteration; without the copy, every button would capture the final value (classic closure-in-a-loop bug). Worth internalizing — it bites everyone once.

**The code** — `Assets/Scripts/UI/ProjectileCarouselUI.cs`. (This is the finished file — it merges what the plan calls Tasks 10 and 11; you only write it once.)

```csharp
using System.Collections;
using System.Collections.Generic;
using CastleCrusher.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProjectileCarouselUI : MonoBehaviour
{
    [Header("Wiring")]
    public ProjectileSelector selector;
    public BulletSpawner spawner;
    public RectTransform contentRoot;
    public GameObject buttonPrefab;

    [Header("Adaptive size")]
    public int minVisibleSideButtons = 1;
    public int maxVisibleSideButtons = 3;
    public float sideButtonWidth = 100f;

    [Header("Look")]
    public float centerScale = 1f;
    public float sideScale = 0.65f;
    [Range(0f, 1f)] public float sideAlpha = 0.4f;
    public float animationDuration = 0.15f;

    public int currentVisibleSideButtons { get; private set; } = 2;

    private readonly List<GameObject> _buttons = new List<GameObject>();
    private Coroutine _tweenRoutine;
    private int _lastSeenWidth = -1;

    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
        if (spawner) spawner.OnAmmoChanged += HandleAmmoChanged;
        UpdateVisibleSideCount();
        Rebuild(false);
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
        if (spawner) spawner.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void HandleSelectionChanged(SO_ProjectileType type) => Rebuild(true);

    private void HandleAmmoChanged(SO_ProjectileType type, int remaining) => RefreshAmmoDisplays();

    private void OnRectTransformDimensionsChange()
    {
        if (!contentRoot) return;
        int width = Mathf.RoundToInt(contentRoot.rect.width);
        if (width == _lastSeenWidth) return;
        _lastSeenWidth = width;

        UpdateVisibleSideCount();
        Rebuild(false);
    }

    public void UpdateVisibleSideCount()
    {
        float available = CarouselLayoutCalculator.AvailableSideWidth(contentRoot.rect.width, sideButtonWidth);
        currentVisibleSideButtons = CarouselLayoutCalculator.VisibleSideButtons(
            available, sideButtonWidth, minVisibleSideButtons, maxVisibleSideButtons);
    }

    public void Rebuild(bool animate)
    {
        if (!selector || !buttonPrefab || !contentRoot) return;

        foreach (GameObject button in _buttons) Destroy(button);
        _buttons.Clear();

        int[] window = CarouselWindow.Window(
            selector.SelectedIndex, selector.availableTypes.Count, currentVisibleSideButtons);

        var targets = new List<ButtonTarget>();

        for (int i = 0; i < window.Length; i++)
        {
            int typeIndex = window[i];
            SO_ProjectileType type = selector.availableTypes[typeIndex];
            bool isCenter = typeIndex == selector.SelectedIndex;

            GameObject button = Instantiate(buttonPrefab, contentRoot);
            button.name = "ProjectileButton_" + type.displayName;

            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon && type.icon) icon.sprite = type.icon;

            TMP_Text nameText = button.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameText) nameText.text = type.displayName;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText && spawner) ammoText.text = spawner.GetRemaining(type).ToString();

            Button uiButton = button.GetComponent<Button>();
            int capturedIndex = typeIndex;   // capture: lambda would otherwise see the loop variable
            if (uiButton)
            {
                uiButton.onClick.AddListener(() => selector.SelectIndex(capturedIndex));
                if (spawner) uiButton.interactable = spawner.HasAmmo(type);
            }

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            targets.Add(new ButtonTarget
            {
                transform = button.transform,
                group = group,
                scale = isCenter ? centerScale : sideScale,
                alpha = isCenter ? 1f : sideAlpha
            });

            _buttons.Add(button);
        }

        if (_tweenRoutine != null) StopCoroutine(_tweenRoutine);
        if (animate && animationDuration > 0f && isActiveAndEnabled)
            _tweenRoutine = StartCoroutine(TweenButtons(targets));
        else
            ApplyTargets(targets);
    }

    private void RefreshAmmoDisplays()
    {
        if (!spawner || !selector) return;
        foreach (GameObject button in _buttons)
        {
            SO_ProjectileType type = FindTypeForButton(button.name);
            if (type == null) continue;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText) ammoText.text = spawner.GetRemaining(type).ToString();

            Button uiButton = button.GetComponent<Button>();
            if (uiButton) uiButton.interactable = spawner.HasAmmo(type);
        }
    }

    private SO_ProjectileType FindTypeForButton(string buttonName)
    {
        foreach (SO_ProjectileType type in selector.availableTypes)
            if (buttonName == "ProjectileButton_" + type.displayName) return type;
        return null;
    }

    private void ApplyTargets(List<ButtonTarget> targets)
    {
        foreach (var t in targets)
        {
            t.transform.localScale = Vector3.one * t.scale;
            if (t.group) t.group.alpha = t.alpha;
        }
    }

    private struct ButtonTarget
    {
        public Transform transform;
        public CanvasGroup group;
        public float scale;
        public float alpha;
    }

    private IEnumerator TweenButtons(List<ButtonTarget> targets)
    {
        float[] startScale = new float[targets.Count];
        float[] startAlpha = new float[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            startScale[i] = targets[i].transform.localScale.x;
            startAlpha[i] = targets[i].group ? targets[i].group.alpha : 1f;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].transform.localScale =
                    Vector3.one * Mathf.Lerp(startScale[i], targets[i].scale, t);
                if (targets[i].group)
                    targets[i].group.alpha = Mathf.Lerp(startAlpha[i], targets[i].alpha, t);
            }
            yield return null;
        }

        ApplyTargets(targets);   // snap to exact final values (kills float drift)
        _tweenRoutine = null;
    }
}
```

**Walkthrough, organized by concern:**

- **Lifecycle wiring (`OnEnable`/`OnDisable`)** — subscribe both events; on enable, also compute width-based count and do the first (non-animated) build. `=> Rebuild(true)` expression-bodied one-liners are the event handlers.

- **Resize handler** — reads `contentRoot.rect.width`, early-outs if unchanged, recomputes `currentVisibleSideButtons` via your tested calculator, rebuilds *without* animation (resizes shouldn't bounce).

- **`Rebuild(animate)`** — the core:
  1. **Destroy old buttons** (`Destroy` is deferred to end-of-frame — safe to destroy then re-instantiate in the same call).
  2. Ask `CarouselWindow` which indices to show.
  3. For each: instantiate, name it `ProjectileButton_<type>` (the name becomes a lookup key later), fill Icon/Name/Ammo via `transform.Find("Icon")?.GetComponent<Image>()` — the `?.` chain reads: "find child; if it exists, get the Image; if that's missing, null." Paired with `if (icon)` guards, so a button prefab missing an optional child degrades instead of throwing.
  4. Click handler with **captured index**; `interactable = HasAmmo(type)` grays out empty types immediately.
  5. Record the target scale/alpha into `ButtonTarget` structs — data-only, no side effects yet.
  6. **Animate or snap:** selection changes tween (with `StopCoroutine` first — a new selection interrupts an in-flight tween); structural rebuilds (enable, resize) snap. The `isActiveAndEnabled` check prevents starting coroutines on a disabled object (Unity would warn).

- **`CanvasGroup`** — a component that controls alpha (and interactivity) for a whole UI subtree in one go. That's why buttons need it: scaling is `localScale`, fading is `CanvasGroup.alpha`, and both tween together.

- **`RefreshAmmoDisplays`** — driven by `OnAmmoChanged`: no rebuild needed, just re-read numbers and `interactable` on the buttons that already exist. The name→type lookup (`FindTypeForButton`) reverses the naming convention from Rebuild.

- **The coroutine** — snapshot starts, loop until duration (adding `Time.deltaTime` each frame — *frame-rate independent*, so the tween takes 0.15s on 30fps and 144fps alike), `Clamp01` guards overshoot, snap at the end so the last frame lands exactly on target.

**Checkpoint:** compiles. Now for the fun part.

---

# Phase 2 — Unity Editor: assets and wiring

## Step 11: Projectile prefabs

1. Duplicate `Assets/Prefabs/weapon-ammo-boulder.prefab` three times; move copies into a new folder `Assets/Prefabs/Projectile/` named `Projectile_Boulder`, `Projectile_Heavy`, `Projectile_Explosive`.
   *(Prefab concept: a saved GameObject template. Scene instances reference it and can override components; editing the prefab propagates. Duplicating gives you three independent templates from your proven boulder.)*
2. **Projectile_Boulder** — don't touch it.
3. **Projectile_Heavy** — Inspector: Rigidbody **Mass = 3** (vs boulder's default). On the `Projectile` component: `shakeIntensity = 1.5`. Keep `baseForcePower = 1500` — the design trick is *same impulse, heavier mass* → slower but harder-hitting, which is exactly the "breaks stone" fantasy.
4. **Projectile_Explosive** — Mass = 1. **Add Component → ExplodeOnImpact**. Fill it in:
   - Look at a TNT block in the scene for reference (`ExplosiveObject`'s values), then set the projectile's own: radius **2.5**, maxForce **150**, explosionDamage **120**, upwardBias **0.3**.
   - `affectedLayers` → **Physical** layer only (the layer you recently added for blocks/enemies — uncheck everything else). Layer masks are bitfields: each layer is a bit; `Physics.OverlapSphere` skips anything outside the mask. This stops the blast from wasting queries on scenery/UI.
   - `explosionParticle` → assign the same particle prefab TNT uses.
   - **Add Component → AudioSource**: Spatial Blend = 0 (2D — UI-style sound, no falloff), Play On Awake = off. **Add Component → ExplosionSoundManager**, assign TNT's explosion `SO_AudioConfigBase`.

## Step 12: SO assets + level data

Right-click in `Assets/ScriptableObjects/` → **Create → Castle Crusher → Projectile Type** (your `[CreateAssetMenu]` in action), ×3:

| Asset | displayName | ammoCount | prefab | scoreMultiplier |
|---|---|---|---|---|
| `Boulder` | Boulder | 8 | Projectile_Boulder | 1 |
| `Heavy` | Heavy | 3 | Projectile_Heavy | 1 |
| `Explosive` | Explosive | 2 | Projectile_Explosive | 1 |

Then **Create → Castle Crusher → Level Data** → `LevelData_01`, add three rows in the same order with the same counts (these override the SO defaults per-level, remember).

Icons: leave empty for now — the carousel code guards `if (type.icon)`. A placeholder Image color will show; swap sprites later.

## Step 13: Button prefab

In the scene: **UI → Button (TextMeshPro)**, then:

1. Root rename `ProjectileTypeButton`, RectTransform 100×100. **Add Component → CanvasGroup** (required — the alpha tween reads it).
2. Restructure the auto-created child: rename the text to **`Ammo`** (names here are a *contract* — `transform.Find("Ammo")` looks up these exact strings), anchor bottom-center, fontSize ~20.
3. Create child **Image → `Icon`**, 48×48, centered.
4. Create child **TMP text → `Name`**, top-center, fontSize ~14.
5. Drag the root from Hierarchy into `Assets/Prefabs/UI/` to save as a prefab; delete the scene copy.

## Step 14: Carousel on the Canvas

1. Under the Canvas: empty UI object → `ProjectileCarousel`.
   ⚠️ **Anchors: stretch horizontally** (Anchor Min (0,0), Max (1,0)), Pivot (0.5, 0), Pos Y = 20, Height 130, Left/Right 0. This matters: `UpdateVisibleSideCount` reads `contentRoot.rect.width` — with stretch anchors it always equals the canvas width, so the adaptive logic actually sees device changes. A fixed width would freeze it.
2. **Add Component → ProjectileCarouselUI** and wire:
   - `selector` / `spawner` → next step
   - `contentRoot` → its own RectTransform (drag the object onto the field)
   - `buttonPrefab` → ProjectileTypeButton
   - Leave Adaptive/Look fields at defaults (1/3, 100, 1, 0.65, 0.4, 0.15).

## Step 15: Gameplay wiring

1. Find the catapult object (has `BuletLuncher` + `BulletSpawner`) → **Add Component → ProjectileSelector**.
2. On `BulletSpawner`: `levelData` = LevelData_01, `selector` = that component, `uiManager` = existing reference. Clear the stale `projectilePrefab` slot if the Inspector shows it (leftover serialization from the old code — Unity keeps unknown fields until you touch the component).
3. Leave `ProjectileSelector.availableTypes` **empty** — you designed `BulletSpawner.Awake` → `InitializeAmmo` → `selector.Initialize(types)` to fill it from LevelData. Single source of truth.
4. Back on ProjectileCarouselUI: drag in `selector` and `spawner`.

**Checkpoint:** Play briefly — no console errors, boulder loads on the catapult, 3 carousel buttons (Boulder centered). Exit.

---

# Phase 3 — Play and verify

- [ ] **Boulder** — old behavior, unchanged.
- [ ] **Heavy** — tap it to center; visibly thuddier impact, stronger shake.
- [ ] **Explosive** — first real impact (not the sling pad) → blast + particle + sound, projectile gone, radius damage/push, **TNT chains still work** (the regression that Step 3 guarded).
- [ ] **HUD counter** — selected type's ammo, decrements per shot.
- [ ] **Carousel** — side tap promotes with tween; type hits 0 → button disables + auto-switch to next type with ammo; HUD follows.
- [ ] **Adaptive width** — Game view at 375 / 640 / 1920 wide → **1 / 2 / 3** side buttons, live while resizing (calculator: floor((w−100)/2 ÷ 100), clamped 1–3).
- [ ] Any failures → debug, re-run just that check.

---

# Phase 4 — GDD sync (required by CLAUDE.md)

- [ ] §5.1: Heavy + Explosive rows → `[Implemented]`
- [ ] §5.2: replace the "selection undecided" bullet with: carousel at bottom, center = selected, sides smaller/translucent, count adapts to device width (1–3) → `[Implemented]`; add per-type ammo bullet → `[Implemented]`
- [ ] §9 leftover row: formula → `Σ (remaining per type × scorePerUnusedShot × type's scoreMultiplier)`; status → `[Partial]` (calculated, but needs the results screen to apply); add the scoreMultiplier note (min 0, default 1, leftover-only)
- [ ] §12: remove the `چیدمان دقیق انتخاب پرابه در مرحله` checkbox (decided + implemented)

---

## Concepts cheat sheet (what you learned)

| Concept | Where you met it |
|---|---|
| Assemblies & asmdef, why tests need them | Step 0 |
| Unit tests, red-green cycle, NUnit `[Test]`/`Assert` | Steps 1, 2, 9 |
| Tuples `(a, b)`, `IReadOnlyList` | Step 1 |
| `static` classes for pure logic vs MonoBehaviours | Steps 1, 3 |
| Refactoring by extracting shared behavior | Step 3 |
| `struct` + `[Serializable]` Inspector nesting | Step 3 |
| `Physics.OverlapSphere`, falloff, `ForceMode.Impulse` | Step 3 |
| Component composition (`[RequireComponent]`) over inheritance | Step 4 |
| Unity messages (`OnCollisionEnter`), guard clauses | Step 4 |
| `[SerializeField] private` vs public fields | Step 4 |
| ScriptableObjects as shared config assets | Step 5 |
| `[CreateAssetMenu]`, `[Min]`, `[Header]`, `[Range]` | Steps 5, 10 |
| C# `event` + `Action<...>`, `+=`/`-=` discipline | Step 6 |
| `?.Invoke()`, null-conditional `?.` | Steps 6, 10 |
| Wrap-around modulo idiom | Steps 6, 9 |
| `Func<>` parameters (dependency as argument) | Step 6 |
| Dictionaries, `TryGetValue` + `out` | Step 7 |
| Awake-before-Start ordering | Step 7 |
| Method groups (`spawner.HasAmmo` as `Func`) | Step 7 |
| Coroutines, `Time.deltaTime`, `Mathf.Lerp` tweening | Step 10 |
| Closures in loops (`capturedIndex`) | Step 10 |
| `OnRectTransformDimensionsChange` for responsive UI | Step 10 |
| `CanvasGroup` alpha, `Button.interactable` | Step 10 |
| Prefabs, layer masks, Inspector wiring | Phase 2 |
