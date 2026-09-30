# Multi-Projectile Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add selectable projectile types (Boulder, Heavy, Explosive) with per-type ammo pools, an adaptive bottom-screen carousel HUD, impact explosion for the explosive type, and a per-type leftover-shot score multiplier.

**Architecture:** Each projectile type = one prefab + one `SO_ProjectileType` ScriptableObject (name/icon/ammo/prefab/scoreMultiplier). A `ProjectileSelector` owns selection state; `BulletSpawner` owns per-type ammo and spawns `selector.SelectedType`. Explosion logic is extracted from `ExplosiveObject` into a shared static `Explosion.TriggerBlast` so TNT chains are unchanged and the new `ExplodeOnImpact` projectile component reuses it. Pure math (score bonus, carousel layout) lives in a new `CastleCrusher.Core` asmdef covered by EditMode tests; Unity wiring (scene, prefabs, UI) is verified manually in play mode.

**Tech Stack:** Unity 6000.6 (URP), C#, Unity Test Framework 1.8.0 (installed), uGUI + TextMeshPro, Input System (unchanged).

**Spec:** `docs/superpowers/specs/2026-09-30-multi-projectile-types-design.md`

## Global Constraints

- Score multiplier: `[Min(0f)]`, default `1f`, applies **only** to leftover-shot bonus — never to destruction/popups.
- TNT explosion behavior must remain byte-for-byte equivalent after the `Explosion` extraction (existing serialized field names/values on `ExplosiveObject` are preserved).
- Carousel: max 3 visible side buttons, min 1, adaptive to device width; center button scale 1.0/alpha 1.0, side scale 0.65/alpha 0.4, tween ~0.15s.
- Per-type ammo pools (not shared pool); HUD counter shows the selected type's remaining ammo.
- Selection switch allowed anytime; the already-loaded bullet fires as its loaded type, the next spawn uses the new selection.
- **Never commit without user approval:** stage changes and present the suggested commit message (ending with `Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>`); commit only after the user approves.
- Tests run via Unity: `Window → General → Test Runner → EditMode → Run All`.
- GDD must be updated in the same session (CLAUDE.md GDD Sync Rule).

---

### Task 1: Core assembly + leftover score calculator (TDD)

**Files:**
- Create: `Assets/Scripts/Core/CastleCrusher.Core.asmdef`
- Create: `Assets/Scripts/Core/LeftoverScoreCalculator.cs`
- Create: `Assets/Tests/EditMode/CastleCrusher.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/LeftoverScoreCalculatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `CastleCrusher.Core.LeftoverScoreCalculator.Compute(IReadOnlyList<(int remaining, float multiplier)> entries, float scorePerUnusedShot) : float` — used by Task 8 (`ScoreManager`).
- Also produces: assembly `CastleCrusher.Core` (auto-referenced by Assembly-CSharp) and test assembly `CastleCrusher.EditModeTests`.

- [ ] **Step 1: Create the Core asmdef**

Create `Assets/Scripts/Core/CastleCrusher.Core.asmdef`:

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

- [ ] **Step 2: Create the test asmdef**

Create `Assets/Tests/EditMode/CastleCrusher.EditModeTests.asmdef`:

```json
{
    "name": "CastleCrusher.EditModeTests",
    "rootNamespace": "",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "CastleCrusher.Core"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Write the failing test**

Create `Assets/Tests/EditMode/LeftoverScoreCalculatorTests.cs`:

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
    public void FractionalMultiplier_RoundTrips()
    {
        var entries = new List<(int remaining, float multiplier)> { (4, 0.5f) };
        float result = LeftoverScoreCalculator.Compute(entries, 50f);
        Assert.AreEqual(100f, result);
    }

    [Test]
    public void EmptyEntries_ReturnZero()
    {
        float result = LeftoverScoreCalculator.Compute(new List<(int, float)>(), 100f);
        Assert.AreEqual(0f, result);
    }
}
```

- [ ] **Step 4: Verify red**

Switch to Unity and let it compile. Expected: compile error `The type or namespace name 'LeftoverScoreCalculator' could not be found` (in the test file). The Test Runner cannot run the tests yet — that is the red state.

- [ ] **Step 5: Implement**

Create `Assets/Scripts/Core/LeftoverScoreCalculator.cs`:

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

- [ ] **Step 6: Verify green**

Unity: `Window → General → Test Runner → EditMode → Run All`. Expected: 4/4 PASS.

- [ ] **Step 7: Stage and present commit message**

`git add Assets/Scripts/Core Assets/Tests` then present to the user:

```
add Core assembly and leftover score calculator with tests

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

Commit only after user approval.

---

### Task 2: Carousel layout calculator (TDD)

**Files:**
- Create: `Assets/Scripts/Core/CarouselLayoutCalculator.cs`
- Create: `Assets/Tests/EditMode/CarouselLayoutCalculatorTests.cs`

**Interfaces:**
- Consumes: nothing (Task 1's Core assembly).
- Produces: `CastleCrusher.Core.CarouselLayoutCalculator.AvailableSideWidth(float totalWidth, float centerButtonWidth) : float` and `CastleCrusher.Core.CarouselLayoutCalculator.VisibleSideButtons(float availableSideWidth, float buttonWidth, int minVisible, int maxVisible) : int` — used by Task 11 (carousel adaptive sizing).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/CarouselLayoutCalculatorTests.cs`:

```csharp
using NUnit.Framework;
using CastleCrusher.Core;

public class CarouselLayoutCalculatorTests
{
    [Test]
    public void AvailableSideWidth_HalfRemainder()
    {
        // 1000 wide screen, 100 wide center button -> (1000-100)/2 = 450 per side
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
        // 450 available, 100 per button -> floor 4, clamped to max 3
        Assert.AreEqual(3, CarouselLayoutCalculator.VisibleSideButtons(450f, 100f, 1, 3));
    }

    [Test]
    public void VisibleSideButtons_NarrowScreen_FloorsAtMin()
    {
        // 40 available -> floor 0, clamped to min 1
        Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(40f, 100f, 1, 3));
    }

    [Test]
    public void VisibleSideButtons_MediumScreen_MatchesFloor()
    {
        // 250 available -> floor 2
        Assert.AreEqual(2, CarouselLayoutCalculator.VisibleSideButtons(250f, 100f, 1, 3));
    }

    [Test]
    public void VisibleSideButtons_ZeroButtonWidth_FloorsAtMin()
    {
        Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(100f, 0f, 1, 3));
    }
}
```

- [ ] **Step 2: Verify red**

Unity compile. Expected: `CarouselLayoutCalculator` not found.

- [ ] **Step 3: Implement**

Create `Assets/Scripts/Core/CarouselLayoutCalculator.cs`:

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

- [ ] **Step 4: Verify green**

Test Runner → EditMode → Run All. Expected: all PASS (Task 1's 4 + this task's 6).

- [ ] **Step 5: Stage and present commit message**

```
add carousel layout calculator with tests

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 3: Extract shared blast logic (Explosion + BlastData)

**Files:**
- Create: `Assets/Scripts/DestructableObjects/SpecialObjects/Explosive/Explosion.cs`
- Modify: `Assets/Scripts/DestructableObjects/SpecialObjects/Explosive/ExplosiveObject.cs`

**Interfaces:**
- Consumes: `DOHealth.TakeDamage(float)`, `DOHealth.IsDead()`, `DOHealth.Die()` (existing, unchanged).
- Produces:
  - `BlastData` serializable struct: `radius`, `maxForce`, `explosionDamage`, `upwardBias`, `affectedLayers`
  - `Explosion.TriggerBlast(Vector3 origin, BlastData data, Transform ignoreRoot = null) : void` — used by Task 4 (`ExplodeOnImpact`).
- Regression constraint: TNT explosion must behave identically (same damage/force/presentation).

- [ ] **Step 1: Create Explosion.cs**

Create `Assets/Scripts/DestructableObjects/SpecialObjects/Explosive/Explosion.cs` — move the OverlapSphere loop **verbatim** out of `ExplosiveObject.Explode`:

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

            // Damage first, and independently of whether the target can be pushed,
            // so static scenery still takes blast damage without moving.
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

- [ ] **Step 2: Refactor ExplosiveObject to call it**

Replace the loop inside `Explode()` with a call. `ExplosiveObject` keeps **all existing serialized field names unchanged** (so scene/prefab values survive), keeps its `PlayEffects`, keeps `OnDeath` subscription. Only the body of `Explode` changes:

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

Keep `BuildBlastData` private; keep the existing serialized fields (`radius`, `maxForce`, `explosionDamage`, `upwardBias`, `affectedLayers`, `explosionParticle`, `shakeForce`, `particleLifetime`) exactly as they are.

- [ ] **Step 3: Verify — compile + TNT regression in play mode**

Unity compile (no errors). Then open `Assets/Scenes/SampleScene.unity`, press Play, shoot the boulder at a TNT block (or let one be destroyed by fall damage). Expected: TNT explodes exactly as before, chains to neighboring TNT, red tint/popup still work.

- [ ] **Step 4: Stage and present commit message**

```
extract shared Explosion.TriggerBlast from ExplosiveObject

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 4: ExplodeOnImpact projectile component

**Files:**
- Create: `Assets/Scripts/Projectile/ExplodeOnImpact.cs`

**Interfaces:**
- Consumes: `Explosion.TriggerBlast(Vector3, BlastData, Transform)` and `BlastData` (Task 3); `Projectile`'s existing `"Start"` tag convention (existing `Projectile.OnCollisionEnter` skips `collision.gameObject.CompareTag("Start")`); `CameraShakeManager.Shake(float)` (existing); `ExplosionSoundManager.PlayExplosionSound(float)` (existing, optional component).
- Produces: `ExplodeOnImpact` MonoBehaviour — attached to the explosive projectile prefab in Task 12. Serialized fields: `blast` (BlastData), `explosionParticle`, `shakeForce`, `particleLifetime`.

- [ ] **Step 1: Implement**

Create `Assets/Scripts/Projectile/ExplodeOnImpact.cs`:

```csharp
using System.Collections;
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

        // Projectile's own hit sound plays from its OnCollisionEnter (same frame).
        // Projectile also schedules DelayDestroy(0.5); destroying here kills that
        // coroutine with the GameObject, so the projectile disappears on impact.
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

- [ ] **Step 2: Verify compile**

Unity compile. Expected: no errors. (Prefab wiring happens in Task 12; behavior verified in Task 13.)

- [ ] **Step 3: Stage and present commit message**

```
add ExplodeOnImpact component for explosive projectile

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 5: SO_ProjectileType + LevelData

**Files:**
- Create: `Assets/Scripts/Projectile/SO_ProjectileType.cs`
- Create: `Assets/Scripts/Level/LevelData.cs`

**Interfaces:**
- Consumes: `Projectile` (existing).
- Produces (relied on by Tasks 7, 8, 10, 11, 12):
  - `SO_ProjectileType : ScriptableObject` — fields `string displayName`, `Sprite icon`, `int ammoCount`, `Projectile prefab`, `[Min(0f)] float scoreMultiplier = 1f`
  - `LevelData : ScriptableObject` — field `List<ProjectileLoadout> projectileLoadouts`
  - `ProjectileLoadout` — `[Serializable]`, fields `SO_ProjectileType projectileType`, `int ammoCount`

- [ ] **Step 1: Implement both scripts**

Create `Assets/Scripts/Projectile/SO_ProjectileType.cs`:

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

Create `Assets/Scripts/Level/LevelData.cs`:

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

- [ ] **Step 2: Verify compile**

Unity compile. Expected: no errors.

- [ ] **Step 3: Stage and present commit message**

```
add SO_ProjectileType and LevelData scriptable objects

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 6: ProjectileSelector

**Files:**
- Create: `Assets/Scripts/Projectile/ProjectileSelector.cs`

**Interfaces:**
- Consumes: `SO_ProjectileType` (Task 5).
- Produces (relied on by Tasks 8, 11):
  - `ProjectileSelector : MonoBehaviour`
  - `List<SO_ProjectileType> availableTypes` (serialized field)
  - `event Action<SO_ProjectileType> OnSelectionChanged`
  - `int SelectedIndex { get; }`
  - `SO_ProjectileType SelectedType { get; }` (null when empty)
  - `void Initialize(List<SO_ProjectileType> types)` — replaces list, resets index to 0, fires `OnSelectionChanged`
  - `void SelectIndex(int index)` — wraps negative/overflow indices, no-op if same index or list empty
  - `void SelectNext()` — wraps forward by 1
  - `bool SelectNextWithAmmo(Func<SO_ProjectileType, bool> hasAmmo)` — walks forward from current; selects the first type with ammo and returns true; if none (including current caller-checked), returns false without changing selection

- [ ] **Step 1: Implement**

Create `Assets/Scripts/Projectile/ProjectileSelector.cs`:

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

- [ ] **Step 2: Verify compile**

Unity compile. Expected: no errors. (Logic is exercised in play mode in Task 13; pure-logic coverage is provided by Tasks 1/2 calculators.)

- [ ] **Step 3: Stage and present commit message**

```
add ProjectileSelector with wraparound and ammo-aware switching

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 7: BulletSpawner per-type ammo + BuletLuncher integration

**Files:**
- Modify: `Assets/Scripts/Projectile/BulletSpawner.cs` (full rewrite of fields/methods shown below)
- Modify: `Assets/Scripts/Projectile/BuletLuncher.cs` — **no code change required** if `InstantiateBullet()` keeps its parameterless signature (verify only; see Step 4).

**Interfaces:**
- Consumes: `ProjectileSelector` (Task 6), `LevelData`/`ProjectileLoadout`/`SO_ProjectileType` (Task 5), `UIManager.SetBulletCountText(int)` (existing).
- Produces (relied on by Tasks 8, 11, 13):
  - `public LevelData levelData` (serialized; optional)
  - `public ProjectileSelector selector` (serialized; required)
  - `event Action<SO_ProjectileType, int> OnAmmoChanged`
  - `void InitializeAmmo(List<ProjectileLoadout> loadout)` (called internally from Awake; also public for tests/resets)
  - `bool HasAmmo(SO_ProjectileType type)`
  - `int GetRemaining(SO_ProjectileType type)`
  - `Projectile InstantiateBullet()` — spawns `selector.SelectedType`; auto-switches when selected type is out of ammo; returns null when nothing has ammo

- [ ] **Step 1: Rewrite BulletSpawner**

`Assets/Scripts/Projectile/BulletSpawner.cs`:

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

- [ ] **Step 2: Verify BuletLuncher compatibility**

`BuletLuncher.Start()` and `DelaySpawn()` call `bs.InstantiateBullet()` with no arguments and assign to `Projectile`. The new signature is identical. **No `BuletLuncher` edit should be necessary.** If the compiler flags anything, fix only that reference — do not otherwise touch `BuletLuncher`.

- [ ] **Step 3: Verify old field removal**

Removed fields: `projectilePrefab`, `maxBulletsCount`, `_currentBulletsCount`. If Unity reports a missing-script-field warning it is harmless; the scene reference cleanup happens in Task 12 (remove the stale `projectilePrefab` reference if the Inspector shows it).

- [ ] **Step 4: Verify compile**

Unity compile. Expected: no errors. (Scene currently has no `selector` assigned — play mode will NRE until Task 13 wires it. Do not press Play yet.)

- [ ] **Step 5: Stage and present commit message**

```
refactor BulletSpawner to per-type ammo pools

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 8: ScoreManager leftover-shot bonus

**Files:**
- Modify: `Assets/Scripts/ScoreManager.cs`

**Interfaces:**
- Consumes: `CastleCrusher.Core.LeftoverScoreCalculator.Compute` (Task 1); `SO_ProjectileType.scoreMultiplier` (Task 5).
- Produces: `public float scorePerUnusedShot = 100f;` and `public void AddLeftoverShotBonus(IReadOnlyList<(int remaining, float multiplier)> entries)` on `ScoreManager` — the future results screen will call it; nothing calls it in this plan (results screen is `[Planned]` in the GDD and out of scope).

- [ ] **Step 1: Implement**

Add to `Assets/Scripts/ScoreManager.cs`:

```csharp
using System.Collections.Generic;
using CastleCrusher.Core;
// (existing using UnityEngine; stays)

    [Header("Leftover shots")]
    public float scorePerUnusedShot = 100f;

    public void AddLeftoverShotBonus(IReadOnlyList<(int remaining, float multiplier)> entries)
    {
        int bonus = Mathf.RoundToInt(LeftoverScoreCalculator.Compute(entries, scorePerUnusedShot));
        if (bonus > 0) AddScore(bonus);
    }
```

- [ ] **Step 2: Verify compile + no behavior change**

Unity compile. Existing gameplay unchanged (method has no callers yet — this is deliberate; the results screen task will wire it).

- [ ] **Step 3: Stage and present commit message**

```
add leftover-shot bonus scoring to ScoreManager

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 9: CarouselWindow helper (TDD)

**Files:**
- Create: `Assets/Scripts/Core/CarouselWindow.cs`
- Create: `Assets/Tests/EditMode/CarouselWindowTests.cs`

**Interfaces:**
- Consumes: Task 1's Core assembly.
- Produces: `CastleCrusher.Core.CarouselWindow.Window(int selectedIndex, int typeCount, int visiblePerSide) : int[]` — used by Task 10 (carousel rebuild). Returns the ordered list of type indices to display, centered on `selectedIndex`, wrapping around modulo `typeCount`; array length is `typeCount` when `typeCount <= 2 * visiblePerSide + 1`, otherwise exactly `2 * visiblePerSide + 1`. Returns empty array when `typeCount <= 0`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/CarouselWindowTests.cs`:

```csharp
using NUnit.Framework;
using CastleCrusher.Core;

public class CarouselWindowTests
{
    [Test]
    public void ThreeTypes_AllShown()
    {
        // All types fit (3 <= 7) -> show all in natural order 0,1,2
        int[] window = CarouselWindow.Window(1, 3, 3);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, window);
    }

    [Test]
    public void WindowClampsToTypeCountOrder()
    {
        // selected 0 of 5 with 1 side -> 4, 0, 1
        int[] window = CarouselWindow.Window(0, 5, 1);
        CollectionAssert.AreEqual(new[] { 4, 0, 1 }, window);
    }

    [Test]
    public void WindowWrapsAtEnd()
    {
        // selected 4 of 5 with 1 side -> 3, 4, 0
        int[] window = CarouselWindow.Window(4, 5, 1);
        CollectionAssert.AreEqual(new[] { 3, 4, 0 }, window);
    }

    [Test]
    public void CenterOfLargeWindow()
    {
        // selected 2 of 10 with 2 sides -> 0,1,2,3,4
        int[] window = CarouselWindow.Window(2, 10, 2);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, window);
    }

    [Test]
    public void Empty_ReturnsEmpty()
    {
        int[] window = CarouselWindow.Window(0, 0, 2);
        Assert.AreEqual(0, window.Length);
    }
}
```

- [ ] **Step 2: Verify red**

Unity compile. Expected: `CarouselWindow` not found.

- [ ] **Step 3: Implement**

Create `Assets/Scripts/Core/CarouselWindow.cs`:

```csharp
using System;

namespace CastleCrusher.Core
{
    public static class CarouselWindow
    {
        public static int[] Window(int selectedIndex, int typeCount, int visiblePerSide)
        {
            if (typeCount <= 0) return Array.Empty<int>();

            int width = Math.Min(typeCount, visiblePerSide * 2 + 1);
            
            // If all types fit in the window, just show them all in order starting from 0
            if (typeCount <= visiblePerSide * 2 + 1)
            {
                int[] all = new int[typeCount];
                for (int i = 0; i < typeCount; i++) all[i] = i;
                return all;
            }
            
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

- [ ] **Step 4: Verify green**

Test Runner → EditMode → Run All. Expected: all PASS.

- [ ] **Step 5: Stage and present commit message**

```
add CarouselWindow helper with tests

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 10: ProjectileCarouselUI — core (rebuild, click, scaling)

**Files:**
- Create: `Assets/Scripts/UI/ProjectileCarouselUI.cs`

**Interfaces:**
- Consumes: `ProjectileSelector` (Task 6: `SelectedIndex`, `SelectedType`, `availableTypes`, `SelectIndex`, `OnSelectionChanged`), `CarouselWindow.Window` (Task 9), `SO_ProjectileType.icon`/`displayName` (Task 5).
- Produces: `ProjectileCarouselUI : MonoBehaviour` — serialized fields listed in Step 1; public methods `Rebuild()` (internal use is fine) and fields `currentVisibleSideButtons` used by Task 11. Attached/wired in Task 12; ammo + adaptive width behavior completed in Task 11.
- Button prefab contract (created in Task 12): root has `Image`, `Button`, `CanvasGroup`; children: `Icon` (`Image`), `Ammo` (TMP_Text), optional `Name` (TMP_Text). Code resolves by `transform.Find`.

- [ ] **Step 1: Implement**

Create `Assets/Scripts/UI/ProjectileCarouselUI.cs`:

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
    public BulletSpawner spawner;          // optional until Task 11; used for ammo text
    public RectTransform contentRoot;      // this RectTransform (width drives adaptive count)
    public GameObject buttonPrefab;        // Image + Button + CanvasGroup; Icon/Ammo children

    [Header("Adaptive size")]
    public int minVisibleSideButtons = 1;
    public int maxVisibleSideButtons = 3;
    public float sideButtonWidth = 100f;   // reserved slice per button

    [Header("Look")]
    public float centerScale = 1f;
    public float sideScale = 0.65f;
    [Range(0f, 1f)] public float sideAlpha = 0.4f;
    public float animationDuration = 0.15f;

    public int currentVisibleSideButtons { get; private set; } = 2;

    private readonly List<GameObject> _buttons = new List<GameObject>();
    private Coroutine _tweenRoutine;

    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
        UpdateVisibleSideCount();
        Rebuild();
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
    }

    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        Rebuild();
    }

    public void UpdateVisibleSideCount()
    {
        float totalWidth = contentRoot.rect.width;
        float available = CarouselLayoutCalculator.AvailableSideWidth(totalWidth, sideButtonWidth);
        currentVisibleSideButtons = CarouselLayoutCalculator.VisibleSideButtons(
            available, sideButtonWidth, minVisibleSideButtons, maxVisibleSideButtons);
    }

    public void Rebuild()
    {
        if (!selector || !buttonPrefab || !contentRoot) return;

        foreach (GameObject button in _buttons) Destroy(button);
        _buttons.Clear();

        int[] window = CarouselWindow.Window(
            selector.SelectedIndex, selector.availableTypes.Count, currentVisibleSideButtons);

        for (int i = 0; i < window.Length; i++)
        {
            int typeIndex = window[i];
            SO_ProjectileType type = selector.availableTypes[typeIndex];
            bool isCenter = typeIndex == selector.SelectedIndex;

            GameObject button = Instantiate(buttonPrefab, contentRoot);
            button.name = $"ProjectileButton_{type.displayName}";

            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon && type.icon) icon.sprite = type.icon;

            TMP_Text nameText = button.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameText) nameText.text = type.displayName;

            Button uiButton = button.GetComponent<Button>();
            int capturedIndex = typeIndex;
            uiButton.onClick.AddListener(() => selector.SelectIndex(capturedIndex));

            // Ammo badge + grayed-out state (live refresh added in Task 11)
            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText && spawner) ammoText.text = spawner.GetRemaining(type).ToString();
            if (uiButton && spawner) uiButton.interactable = spawner.HasAmmo(type);

            Transform t = button.transform;
            float targetScale = isCenter ? centerScale : sideScale;
            float targetAlpha = isCenter ? 1f : sideAlpha;
            t.localScale = Vector3.one * targetScale;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group) group.alpha = targetAlpha;

            _buttons.Add(button);
        }

        // Order siblings so the window reads left-to-right (Instantiate appends, so already ordered).
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].transform.SetSiblingIndex(i);
        }
    }
}
```

Note: `_tweenRoutine`/`Coroutine` import kept for Task 11's animated rebuild; if the compiler warns about the unused field, leave it — Task 11 uses it.

- [ ] **Step 2: Verify compile**

Unity compile. Expected: no errors.

- [ ] **Step 3: Stage and present commit message**

```
add ProjectileCarouselUI core rebuild and selection

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 11: Carousel — adaptive width + ammo refresh + tween

**Files:**
- Modify: `Assets/Scripts/UI/ProjectileCarouselUI.cs`

**Interfaces:**
- Consumes: `BulletSpawner.OnAmmoChanged` / `HasAmmo` / `GetRemaining` (Task 7), `CarouselLayoutCalculator` (Task 2).
- Produces: responsive carousel — side count recomputed on RectTransform resize; ammo badge live-updates; out-of-ammo buttons disable; selection changes tween over `animationDuration`.

- [ ] **Step 1: Add resize handling and animated rebuild**

Append to `ProjectileCarouselUI` (and change `Rebuild()` into `Rebuild(bool animate)` called as `Rebuild(true)` from `HandleSelectionChanged`, `Rebuild(false)` from `OnEnable`):

```csharp
    private int _lastSeenWidth = -1;

    private void OnRectTransformDimensionsChange()
    {
        int width = Mathf.RoundToInt(contentRoot.rect.width);
        if (width == _lastSeenWidth) return;
        _lastSeenWidth = width;

        UpdateVisibleSideCount();
        Rebuild(false);
    }

    // In the existing OnEnable, add the spawner subscription after the selector line:
    //   if (spawner) spawner.OnAmmoChanged += HandleAmmoChanged;
    // In the existing OnDisable, add the matching unsubscribe:
    //   if (spawner) spawner.OnAmmoChanged -= HandleAmmoChanged;
    // Existing bodies:
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

    private void HandleAmmoChanged(SO_ProjectileType type, int remaining)
    {
        RefreshAmmoDisplays();
    }

    private void RefreshAmmoDisplays()
    {
        foreach (GameObject button in _buttons)
        {
            if (!spawner) break;
            int index = SelectorIndexFromButtonName(button.name);
            if (index < 0) continue;

            SO_ProjectileType type = selector.availableTypes[index];
            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText) ammoText.text = spawner.GetRemaining(type).ToString();

            Button uiButton = button.GetComponent<Button>();
            if (uiButton) uiButton.interactable = spawner.HasAmmo(type);
        }
    }

    private int SelectorIndexFromButtonName(string buttonName)
    {
        // name format: ProjectileButton_<displayName>
        for (int i = 0; i < selector.availableTypes.Count; i++)
        {
            if (buttonName == "ProjectileButton_" + selector.availableTypes[i].displayName) return i;
        }
        return -1;
    }
```

For the tween, replace the immediate scale/alpha assignment in `Rebuild` (the lines `t.localScale = ...` / `group.alpha = ...`) with capture-and-animate:

```csharp
            // collect (transform, CanvasGroup, targetScale, targetAlpha) tuples into a local list,
            // then after the instantiate loop:
            if (_tweenRoutine != null) StopCoroutine(_tweenRoutine);
            if (animate && animationDuration > 0f)
                _tweenRoutine = StartCoroutine(TweenButtons(targets));
```

with:

```csharp
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
                float s = Mathf.Lerp(startScale[i], targets[i].scale, t);
                targets[i].transform.localScale = Vector3.one * s;
                if (targets[i].group) targets[i].group.alpha = Mathf.Lerp(startAlpha[i], targets[i].alpha, t);
            }
            yield return null;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].transform.localScale = Vector3.one * targets[i].scale;
            if (targets[i].group) targets[i].group.alpha = targets[i].alpha;
        }
        _tweenRoutine = null;
    }
```

- [ ] **Step 2: Verify compile**

Unity compile. Expected: no errors.

- [ ] **Step 3: Stage and present commit message**

```
add adaptive sizing, ammo refresh and tween to projectile carousel

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 12: Unity assets and scene wiring (manual editor work)

**Files (assets, created in Unity Editor):**
- Create: `Assets/Prefabs/weapon-ammo-boulder.prefab` — **already exists**; if the current prefab is referenced by `BulletSpawner.projectilePrefab`, keep it and create a copy-based new prefab (next bullets) as described below.
- Create: `Assets/Prefabs/Projectile/Projectile_Boulder.prefab`
- Create: `Assets/Prefabs/Projectile/Projectile_Heavy.prefab`
- Create: `Assets/Prefabs/Projectile/Projectile_Explosive.prefab`
- Create: `Assets/Prefabs/UI/ProjectileTypeButton.prefab`
- Create: `Assets/ScriptableObjects/ProjectileTypes/Boulder.asset`, `Heavy.asset`, `Explosive.asset` (SO_ProjectileType)
- Create: `Assets/ScriptableObjects/LevelData_01.asset` (LevelData)
- Modify: `Assets/Scenes/SampleScene.unity` (wire components)

**Interfaces:**
- Consumes: everything from Tasks 3–11.
- Produces: a playable scene; `ProjectileTypeButton.prefab` matching Task 10's contract (root: Image/Button/CanvasGroup; children named exactly `Icon` (Image), `Ammo` (TMP_Text), `Name` (TMP_Text)).

- [ ] **Step 1: Create the three projectile prefabs**

1. In Project window, duplicate `Assets/Prefabs/weapon-ammo-boulder.prefab` three times; rename to `Projectile_Boulder`, `Projectile_Heavy`, `Projectile_Explosive` under `Assets/Prefabs/Projectile/`.
2. `Projectile_Boulder` — leave values as the current boulder.
3. `Projectile_Heavy` — on its Rigidbody: **Mass = 3** (vs boulder's current mass). On `Projectile`: `baseForcePower = 1500` (same as boulder — heavier mass at same impulse feels slower/stronger) and `shakeIntensity = 1.5`.
4. `Projectile_Explosive` — Mass = 1 (same as boulder). Add Component → `ExplodeOnImpact`. Set `blast.affectedLayers` to the **Physical** layer mask (the layer recently added for blocks and enemies — the same mask the TNT `ExplosiveObject` uses in the scene; copy it by inspecting a TNT block's `ExplosiveObject` values: radius/maxForce/explosionDamage as reference, then set the projectile's own values to: radius 2.5, maxForce 150, explosionDamage 120, upwardBias 0.3). Assign the same explosion particle prefab used by TNT's `ExplosiveObject` to `explosionParticle`. Add Component → `AudioSource` (Spatial Blend 0, Play On Awake off) + `ExplosionSoundManager` with the explosion `SO_AudioConfigBase` (the one used by TNT).

- [ ] **Step 2: Create the three SO_ProjectileType assets**

Project → Right Click → Create → Castle Crusher → Projectile Type, three times:

| Asset | displayName | ammoCount | prefab | scoreMultiplier |
|---|---|---|---|---|
| `Boulder.asset` | Boulder | 8 | Projectile_Boulder | 1 |
| `Heavy.asset` | Heavy | 3 | Projectile_Heavy | 1 |
| `Explosive.asset` | Explosive | 2 | Projectile_Explosive | 1 |

Icons: leave null unless you already have sprites; empty icon is handled by Task 10 (`if (icon && type.icon)`). (Balancing values are starting points; tune later.)

- [ ] **Step 3: Create the level data asset**

Create → Castle Crusher → Level Data → `LevelData_01.asset`. Add 3 entries to `projectileLoadouts`: Boulder/8, Heavy/3, Explosive/2 (same as the SO defaults).

- [ ] **Step 4: Create the button prefab**

In `Assets/Prefabs/UI/` create `ProjectileTypeButton`:
1. UI → Button (TextMeshPro), rename root `ProjectileTypeButton`. Set its RectTransform size to 100×100. On the root keep `Image`, `Button`, and Add Component → `CanvasGroup`.
2. Rename the auto-created text child to `Ammo` (TMP_Text, bottom-aligned small text, e.g. anchored bottom-center, fontSize 20).
3. Create a child Image named `Icon` (48×48, centered, any placeholder sprite/color).
4. Create a child TMP text named `Name` (anchored top-center, fontSize 14).
5. Drag the hierarchy onto `Assets/Prefabs/UI/` to save as prefab; delete the scene copy.

- [ ] **Step 5: Add carousel to the Canvas**

In `SampleScene`, select the existing `Canvas`:
1. Create empty child UI object `ProjectileCarousel`. RectTransform: anchor **stretch horizontally** (Anchor Min (0, 0), Max (1, 0)), Pivot (0.5, 0), Anchored Position (0, 20), Height 130, Left/Right 0. Stretching is required — the adaptive side-button count reads this RectTransform's width, so it must track the screen width.
2. Add Component → `ProjectileCarouselUI`.
3. Wire: `selector` = (Step 6), `spawner` = (Step 6), `contentRoot` = ProjectileCarousel's RectTransform, `buttonPrefab` = ProjectileTypeButton prefab. Leave min/max at 1/3, sideButtonWidth 100, look values at defaults.

- [ ] **Step 6: Wire the gameplay components**

1. Select the GameObject that has `BuletLuncher` (the catapult). Add Component → `ProjectileSelector` and Add Component → `BulletSpawner` if `BulletSpawner` is not already there (it may live on the same or a child object — check where the existing `BulletSpawner` sits and add `ProjectileSelector` beside it).
2. `BulletSpawner`: set `levelData` = LevelData_01, `selector` = the ProjectileSelector component, `uiManager` = existing UIManager reference. If the Inspector still shows a stale `projectilePrefab` field, clear/remove it (leftover from the old serialization).
3. `ProjectileSelector`: leave `availableTypes` empty (BulletSpawner.Awake populates it from LevelData).
4. Back on ProjectileCarouselUI, drag the ProjectileSelector and BulletSpawner into `selector` and `spawner`.

- [ ] **Step 7: Verify no missing references**

Enter Play mode briefly and exit. Expected: no `MissingReferenceException` / NRE in Console; a boulder loads on the catapult; carousel shows 3 buttons (Boulder center). If Console errors appear, fix wiring before continuing.

- [ ] **Step 8: Stage and present commit message**

```
add projectile prefabs, type assets and scene wiring for multi-projectile

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 13: Play-mode end-to-end verification

**Files:** none (verification only; fix bugs found in the listed files).

**Interfaces:**
- Consumes: complete implementation (Tasks 1–12).
- Produces: confirmed working feature; bug fixes applied where needed.

- [ ] **Step 1: Run the EditMode test suite**

Test Runner → EditMode → Run All. Expected: all PASS (leftover score 4, layout 6, window 5 = 15 tests).

- [ ] **Step 2: Play SampleScene — shooting**

Press Play. For each type: tap a side button (it becomes center), drag-aim, release. Expected:
- Boulder: behaves exactly like the pre-change boulder.
- Heavy: visibly heavier/stronger impact; more camera shake.
- Explosive: on first impact (not the Start/sling trigger) → blast + explosion particle + sound, projectile disappears; blocks/enemies within radius take damage and get pushed; adjacent TNT chains.
- HUD counter under the score updates to the selected type's remaining ammo and decrements per shot.

- [ ] **Step 3: Play — carousel rules**

Expected:
- Center button full size/opaque, sides ~0.65 scale / 0.4 alpha; tapping a side button promotes it with a quick tween.
- Shoot a type to 0 ammo → its button disables; the spawner auto-switches to the next type with ammo; counter follows.
- Switch selection mid-session (after a shot, before the next drag): the newly spawned bullet is the newly selected type.

- [ ] **Step 4: Play — adaptive width**

Game view: set resolutions 375×667 (small phone), 640×960, and 1920×1080. Expected: visible side-button count = 1 / 2 / 3 respectively (calculator: floor of ((width − 100) ÷ 2) ÷ 100, clamped 1–3 → 137→1, 270→2, 910→3) without restart. Rotate/resize the Game view while playing to trigger `OnRectTransformDimensionsChange`.

- [ ] **Step 5: Fix any failures found**

Fix in the owning file (spawner logic → Task 7 file; carousel → Task 10/11 file; explosion values → prefab/ExplodeOnImpact). Re-run the relevant check.

- [ ] **Step 6: Stage and present commit message (if fixes were made)**

```
fix issues found during multi-projectile play-mode verification

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

### Task 14: GDD sync (required by CLAUDE.md)

**Files:**
- Modify: `docs/GDD.md`

**Interfaces:**
- Consumes: shipped behavior from Tasks 1–13.
- Produces: truthful status badges per the GDD Sync Rule.

- [ ] **Step 1: Update section 5 (Projectiles)**

- Table 5.1: Boulder row stays `[Implemented]`; Heavy row → `[Implemented]` (behavior: سنگین‌تر با جرم/قدرت بالاتر); Explosive row → `[Implemented]` (behavior: انفجار هنگام برخورد — `ExplodeOnImpact`).
- Section 5.2: keep projectile limit + cooldown as-is; replace the selection bullet (`انتخاب پرابه: ... تصمیم نهایی هنگام پیاده‌سازی`) with: **کاروسل افقی در پایین صفحه — دکمه انتخاب‌شده وسط، دکمه‌های کناری کوچک‌تر و نیمه‌شفاف؛ تعداد دکمه‌های کناری بر اساس عرض دستگاه (۱ تا ۳) متغیر** `[Implemented]`.
- Add a bullet under 5.2: **هر نوع پرابه استخر تیر مخصوص به خود را دارد (per-type ammo)؛ شمارنده HUD تیرهای باقی‌مانده نوع انتخابی را نشان می‌دهد** `[Implemented]`.

- [ ] **Step 2: Update section 9 (Scoring)**

- Change the `شلیک‌های باقی‌مانده` row calculation to: **مجموع (باقی‌مانده هر نوع × scorePerUnusedShot × scoreMultiplier همان نوع)** and set its status to `[Partial]` (محاسبه پیاده‌سازی شده؛ اعمال در پایان مرحله نیازمند صفحه نتایج است).
- Add a short note: **`scoreMultiplier` روی SO هر نوع پرابه تعریف می‌شود (پیش‌فرض ۱، حداقل ۰) و فقط روی امتیاز پرتابه‌های باقی‌مانده اثر دارد — نه روی امتیاز تخریب.**

- [ ] **Step 3: Update section 12 (Future / Not Decided)**

- Remove the checkbox `چیدمان دقیق انتخاب پرابه در مرحله` (now decided and implemented). Leave `فهرست کامل پرابه‌های نهایی` unchecked (split/multi-shot etc. are still future).

- [ ] **Step 4: Stage and present commit message**

```
update GDD for projectile types, carousel selection and leftover scoring

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

---

## Task dependency map

```
Task 1 (Core+score calc) ──┐
Task 2 (layout calc) ──────┼── Task 9 (CarouselWindow) ── Task 10 ── Task 11 ──┐
Task 3 (Explosion) ── Task 4 (ExplodeOnImpact) ───────────────────────────────┤
Task 5 (SO/LevelData) ── Task 6 (Selector) ── Task 7 (Spawner) ── Task 8 ─────┼── Task 12 (assets/scene) ── Task 13 (verify) ── Task 14 (GDD)
```

Tasks 1–6 are independent of each other (can run in any order or parallel); 7 needs 5+6; 9 needs 1; 10 needs 6+9; 11 needs 10+7; 12 needs everything before it; 13 and 14 come last.
