# Multi-Catapult Support — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Learning-project rule (`CLAUDE.md`):** the *user* implements every step. Never write files or make scene changes for them — hand them the plan and explain.

**Goal:** a scene can hold N catapults; arrows switch the active one (wrap-around); the camera glides to the active catapult's anchor (straight or along a designer path) then softly follows its aim; the level chooses shared vs. per-catapult ammo.

**Architecture:** a top-level `CatapultManager` owns the catapult registry, the one global `ProjectileSelector`, and builds the ammo pools (shared or per-catapult). A top-level `CameraRig` owns the camera's world pose and moves it between per-catapult `CameraAnchor` poses, then lags behind the aim. Existing scripts (`AmmoManager`, `Projectile`, `ProjectileSpawner`, `ProjectileLuncher`, `InputController`, `ProjectileCarouselUI`) are refactored to act on *the active catapult* and *explicit projectile types* instead of assuming "the one catapult".

**Tech Stack:** Unity 6000.6 (URP), C#, NUnit EditMode tests against the existing `CastleCrusher.Core` asmdef. No new packages (`com.unity.splines` is NOT installed — the spline is hand-rolled in `Core`).

## Global Constraints

- Unity **6000.6+**, URP, C#. No new packages.
- `CastleCrusher.Core` (asmdef) must stay free of game types (`SO_ProjectileType`, `AmmoManager`, …): an asmdef assembly **cannot reference** the predefined `Assembly-CSharp`. Engine maths (`Vector3`, `Mathf`) are fine (`noEngineReferences: false`).
- **No auto-commit.** After each task: `git add` the touched files and propose the commit message; the user commits.
- GDD sync (Phase 6) is required by `CLAUDE.md` — this feature changes design.
- GDD language: English headers, Persian body. Code and comments: English.

## Current repo state (verified 2026-11-08)

- `Assets/Scripts/Core/AmmoPool.cs` — **drafted**, compiles, but has two latent bugs this plan fixes: `Get`/`Consume` on an unknown key throw `KeyNotFoundException`, and `Has` means "key exists", not "count > 0".
- `Assets/Scripts/Core/CameraPath.cs` — **drafted**; `PolylinePath` crashes on an empty/1-point list (`points[0]` index out of range).
- `Assets/Scripts/Core/RingIndex.cs` — **empty skeleton** (method bodies missing) — the project does not compile until Task 1.1 fills it.
- No test files exist for the three Core classes yet.
- Verified by grep: `GetJumpBtn`/`_jumpBtn` in `InputController` have **no readers** anywhere — safe to delete. `DecreaseCurrentAmmo` has exactly **one** caller (`Projectile.Shoot`) and `GetCurrentAmmoCount` has **zero** callers — safe to delete.

## Decisions already made (the plan builds on these)

| Topic | Decision |
|---|---|
| Camera structure | **Top-level rig + per-catapult anchors.** A `CameraRig` GameObject owns the camera; each catapult carries a `CameraAnchor` describing the camera pose. |
| Camera during aim | **Soft follow:** while aiming, the camera follows the catapult's yaw *with a lag* (slower than the catapult itself) so it feels pleasing, not rigid. |
| Arrow ends | **Wrap around (cyclic):** right arrow from the last catapult goes to the first, matching how the projectile carousel already wraps. |
| Ammo model | **Each catapult gets its own copy** of the level loadout when the level asks for it; one shared pool otherwise. Chosen per level in `SO_LevelData`. |
| Type selection | **Global.** One `ProjectileSelector` for the whole level — the selected type is the same no matter which catapult is active. |
| Spline | **Custom Catmull-Rom in `CastleCrusher.Core`** — pure maths, unit-testable, no new packages. |

## Target architecture

```
Scene
├── CameraRig                        [CatapultCameraRig]   ← top level, NOT under a catapult
│   └── Main Camera                  [CameraShakeManager]  ← local pose (0,0,0); rig owns the world pose
├── CatapultManager                  [CatapultManager]     ← level brain: registry, active one, ammo pools
│   │                                [InputController]     ← moved here (was on the catapult root)
│   └── ProjectileSelector           [ProjectileSelector]  ← the ONE global type list
├── SiegeCatapult_A                  ← FBX instance
│   ├── ProjectileLuncher            [ProjectileLuncher]
│   ├── Catapult                     [Catapult]            ← new: per-catapult facade
│   ├── CameraAnchor                 [CameraAnchor]        ← new: local camera pose + optional path points
│   ├── Highlight                    (optional visual)
│   └── catapult (head)              ← FBX child
│       ├── AmmoManager              [AmmoManager]         ← holds a *pool reference*, not the pool itself
│       └── Spawner                  [ProjectileSpawner]
└── SiegeCatapult_B                  ← duplicated FBX instance, same component set
```

Data flow on a switch:

```
Arrow button ─► CatapultManager.NextCatapult()
                    ├─ ActiveCatapult = B
                    ├─ B.launcher.LoadProjectile()      (only the active one loads)
                    ├─ OnActiveCatapultChanged(B)
                    │     ├─► ProjectileCarouselUI      → re-subscribe to B's pool, refresh numbers
                    │     ├─► InputController           → drops any in-flight drag
                    │     └─► CatapultCameraRig.MoveTo(B)
                    └─ CatapultCameraRig.MoveTo(B)
                          start = rig current pose
                          end   = B.CameraAnchor pose (evaluated in B's rest frame)
                          path  = B.CameraAnchor.pathPoints (2+ → Catmull-Rom, else straight)
```

## Order of work

| # | Phase | Why in this order |
|---|---|---|
| 1 | **Phase 0** (fix the stale scene data) | Cheap; the scene gets re-saved anyway before new components can be wired. |
| 2 | **Phase 1** (Core maths + tests) | Pure code, no scene work — green tests before touching the Editor. Also un-blocks compilation (`RingIndex` is empty). |
| 3 | **Phase 2.1** (`SO_LevelData.ammoMode`) | One enum field; everything downstream reads it. |
| 4 | **Phase 2.2–2.5** (`Catapult`, `CatapultManager`, `CameraAnchor`, `CatapultCameraRig`) | The spine of the feature. |
| 5 | **Phase 3** (refactors) | The manager needs these to hand out pools and reload projectiles. |
| 6 | **Phase 4** (arrow UI) | Pure presentation on top of a working manager. |
| 7 | **Phase 5** (scene wiring) | One sitting — the move-then-duplicate order matters. |
| 8 | **Phase 6** (GDD) | Last, so the badges describe what actually shipped. |

---

## Phase 0 — Unblock: fix what is already broken

These are real bugs (verified in the committed scene) that will bite the moment a second catapult appears. Do them first, in the Editor, because fixing them requires re-saving the scene.

### Task 0.1: The scene stores fields that no longer exist on the scripts

**Files:**
- Modify: `Assets/Scripts/Projectile/ProjectileLuncher.cs` (one attribute)
- Re-save: `Assets/Scenes/SampleScene.unity`

**What is wrong.** `Assets/Scenes/SampleScene.unity` still serialises:

* `bs: {fileID: 799082386}` on the launcher — the field was renamed to `projectileSpawner` in commit `649c4dd`, but the scene was never re-saved after the rename, so the reference deserialises as **null**. `ProjectileLuncher.DelaySpawn` would throw a `NullReferenceException` on the first shot.
* `uiManager: {fileID: 2123016087}` on `AmmoManager` and `ScoreManager` — the field was removed in commit `8217392`. Unity ignores unknown fields, so it is harmless noise, but it is stale.
* `m_EditorClassIdentifier: Assembly-CSharp::BuletLuncher` on the launcher — the class was renamed to `ProjectileLuncher` in `649c4dd`. Harmless (Unity resolves components through the script's GUID, not the class name), but it is a useful *timestamp*: proof the scene was last written before the rename.

**Teaching note — why this happens.** A scene file is a *serialised snapshot*. Renaming or deleting a `public`/`[SerializeField]` field does **not** rewrite the scene; Unity only reconciles when you open the scene and save it. Two ways to survive a rename:

* `[FormerlySerializedAs("bs")]` — Unity keeps the old data in the scene and maps it onto the new field. Best when the old scene must keep working.
* Just re-open and re-save — Unity drops the unknown key. Fine for a project in progress.

**Interfaces:**
- Produces: a scene where `ProjectileLuncher.projectileSpawner` actually references the Spawner object. Everything in Phase 5 depends on this.

- [ ] **Step 1: Add the attribute** (belt and braces — it also documents the history for anyone reading the code). At the top of `ProjectileLuncher.cs`, add the using and mark the field:

```csharp
using UnityEngine;
using UnityEngine.Serialization;   // NEW

public class ProjectileLuncher : MonoBehaviour
{
    [FormerlySerializedAs("bs")]   // NEW — the field was called "bs" before commit 649c4dd
    public ProjectileSpawner projectileSpawner;
    // ... rest unchanged
```

*Why:* `[FormerlySerializedAs("oldName")]` tells the serialiser "if the scene file has a `bs:` entry, load it into this field". Without it, Unity looks for `projectileSpawner:` in the scene, finds nothing, and leaves the field null.

- [ ] **Step 2: Open `SampleScene`, let Unity recompile, and save the scene** (`Ctrl+S`). After the save, open the scene YAML and confirm: no `bs:` key, no `uiManager:` key, no `BuletLuncher` identifier remain. (Or simply trust the Inspector — see Step 3.)

- [ ] **Step 3: Verify.** In the Hierarchy select the catapult root → look at the `Projectile Spawner` field on `ProjectileLuncher` — it must show the `Spawner` object, not `None`. Select `AmmoManager` — the ghost `Ui Manager` row should be gone.

- [ ] **Step 4: Stage & propose commit**

```bash
git add Assets/Scripts/Projectile/ProjectileLuncher.cs Assets/Scenes/SampleScene.unity
```
Proposed message: `fix(scene): restore spawner reference lost in rename, re-save stale scene`

> **Be careful with `SampleScene.unity`.** It is a ~13,000-line YAML file and Unity rewrites large parts of it on save. Commit it only after saving from the Editor — never hand-edit it — and check `git diff --stat` first so the change is roughly the size you expect, not a wholesale rewrite.

### Task 0.2: The EventSystem's input actions point at the package's defaults

**Files:**
- Modify: the `EventSystem` object in `SampleScene` (Inspector only)

**What is wrong.** `InputSystemUIInputModule` on the `EventSystem` references action fileIDs under GUID `ca9f5fa95ffab41fb9a615ab714db018` — the Input System *package's* shipped `DefaultInputActions.inputactions`. Its UI map has `Navigate / Submit / Cancel / Point / Click / ScrollWheel / …` but **no `LeftClick`**. The project's own `Assets/InputSystem_Actions.inputactions` (with proper `Player` and `UI` maps) is referenced by nothing — the scene was copied from a sample and never re-pointed.

- [ ] **Step 1:** In the Editor, select the `EventSystem`, open `Input System UI Input Module`, and click **"Assign Default Actions"** (or point `Actions Asset` at `Assets/InputSystem_Actions.inputactions` and fill the action fields).

- [ ] **Step 2: Verify.** Press Play and click a projectile button in the carousel — it must respond to a left click. (If clicks already worked for you, this was cosmetic — fix it anyway so the new arrows are guaranteed to work on desktop.)

- [ ] **Step 3:** Save the scene; stage with Task 0.1's commit.

### Task 0.3: Mouse aiming does not exist (the project claims it does)

`InputController` only reads `Input.GetTouch(0)`, so on desktop `touchCount` is always 0 and you cannot aim — even though `CLAUDE.md` and GDD §3 both say mouse is supported. **No separate fix here:** Task 3.4 (the `InputController` rewrite) adds one unified pointer path that reads touch *or* mouse. `ProjectSettings.asset` has `activeInputHandler: 2` ("Both"), so the legacy `Input` API works alongside the Input System package.

---

## Phase 1 — Core maths (`CastleCrusher.Core`) + unit tests

Everything here is pure C#: no `MonoBehaviour`, no scene objects. That is what makes it unit-testable
in **EditMode** — tests run in milliseconds without entering Play mode. The code lives in
`namespace Core` and must not reference `SO_ProjectileType` or anything else from `Assembly-CSharp`
(an asmdef assembly cannot reference the predefined `Assembly-CSharp`; it *can* use `Vector3` /
`Mathf` because `CastleCrusher.Core.asmdef` has `"noEngineReferences": false`).

**Two of the three files already exist as drafts** (from an earlier session): `AmmoPool.cs` and
`CameraPath.cs` compile but hide real bugs; `RingIndex.cs` is an **empty skeleton** — a C# method
with no `return` is a compile error (`CS0161`), so **the whole project fails to compile right now**.
That compile error is today's "red" — Task 1.1 turns it green first.

**Teaching note — how to run the tests.** `Window ▸ General ▸ Test Runner ▸ EditMode tab ▸ Run All`.
Every test is a plain method marked `[Test]` in a class next to the existing
`CarouselWindowTests` (same `CastleCrusher.EditModeTests` assembly, which already references
`CastleCrusher.Core` — check its asmdef `references` if a new test can't find `Core` types).
Test methods read as sentences: `Consume_AtZero_ReturnsZero` — *given* X *when* Y *then* Z.

### Task 1.1: `RingIndex` — fill the skeleton (compile blocker)

**Files:**
- Modify: `Assets/Scripts/Core/RingIndex.cs`
- Test: `Assets/Tests/EditMode/RingIndexTests.cs` (new)

**Interfaces:**
- Produces: `Core.RingIndex.Wrap(int index, int count)` and `Core.RingIndex.Next(int current, int count, int delta)` — both `static`, both returning a valid index in `[0, count)` (or `0` when `count <= 0`). Task 2.3 (`CatapultManager.NextCatapult/PreviousCatapult`) and Task 1.1's own tests consume this.

- [ ] **Step 1: Write the tests**

```csharp
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class RingIndexTests
    {
        [Test]
        public void Wrap_NormalisesAnyIndexIntoRange()
        {
            Assert.AreEqual(0, RingIndex.Wrap(0, 3));
            Assert.AreEqual(2, RingIndex.Wrap(2, 3));
            Assert.AreEqual(0, RingIndex.Wrap(3, 3));   // one past the end wraps to the start
            Assert.AreEqual(1, RingIndex.Wrap(4, 3));
        }

        [Test]
        public void Wrap_HandlesNegativeIndices()
        {
            Assert.AreEqual(2, RingIndex.Wrap(-1, 3));  // one before the start wraps to the end
            Assert.AreEqual(2, RingIndex.Wrap(-4, 3));  // even a full loop backwards
        }

        [Test]
        public void Next_StepsForwardAndWraps()
        {
            Assert.AreEqual(1, RingIndex.Next(0, 3, 1));
            Assert.AreEqual(0, RingIndex.Next(2, 3, 1));   // last → first (right-arrow wrap)
        }

        [Test]
        public void Next_StepsBackwardAndWraps()
        {
            Assert.AreEqual(2, RingIndex.Next(0, 3, -1));  // first → last (left-arrow wrap)
            Assert.AreEqual(0, RingIndex.Next(1, 3, -1));
        }

        [Test]
        public void SingleItemRing_AlwaysWrapsToZero()
        {
            Assert.AreEqual(0, RingIndex.Next(0, 1, 1));
            Assert.AreEqual(0, RingIndex.Next(0, 1, -1));
        }

        [Test]
        public void EmptyRing_ReturnsZeroWithoutDividingByZero()
        {
            // A future caller might not guard against count == 0; RingIndex must not throw.
            Assert.AreEqual(0, RingIndex.Wrap(5, 0));
            Assert.AreEqual(0, RingIndex.Next(0, 0, 1));
        }
    }
}
```

*Why these specific cases:* the wrap-at-both-ends pair (`Next(2,3,1) == 0` and `Next(0,3,-1) == 2`)
is exactly the behaviour the catapult arrows promise ("wrap around"); the empty-ring pair guards
against a `DivideByZeroException` hiding in the modulo.

- [ ] **Step 2: Implement — fill the skeleton**

```csharp
namespace Core
{
    public static class RingIndex
    {
        // Wrap-around index maths for a ring of `count` items.
        public static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;   // empty ring: nothing to wrap into, stay at 0
            return ((index % count) + count) % count;
        }

        public static int Next(int current, int count, int delta)
        {
            return Wrap(current + delta, count);
        }
    }
}
```

Two changes from the skeleton you have: the class becomes `static` (a class that only holds static
members should *be* static — it can then never be instantiated by accident), and both bodies are
filled.

*Line-by-line:*
- `count <= 0` → return `0`: an empty ring has no valid index, `0` is the harmless answer. Without
  this guard, `count == 0` would divide by zero inside `%`.
- `((index % count) + count) % count` is the **positive modulo** trick. Plain `%` in C# keeps the
  sign of the *left* operand: `-1 % 3` is `-1`, not `2`. Adding `count` once pushes any single
  negative step back into range (`-1 + 3 = 2`), and the outer `% count` re-normalises results that
  were already positive (e.g. `4 % 3 = 1`). For any `index > -count` one `+ count` suffices; the
  form above is the standard belt-and-braces version.
- `Next` is just `Wrap(current + delta, count)` — a left-arrow press is `delta = -1`, a right-arrow
  press is `delta = +1`.

**Teaching note — reuse.** `ProjectileSelector.SelectIndex` already inlines this same trick
(`((index % availableTypes.Count) + availableTypes.Count) % availableTypes.Count`). Leave it alone
(its context is slightly different — it early-outs when the index is unchanged); new code uses
`RingIndex`.

- [ ] **Step 3: Run the tests** — Test Runner ▸ EditMode ▸ Run All.
Expected: `RingIndexTests` **6/6 PASS**, and the project compiles again. (Before Step 2 the whole
test assembly failed to build — that was the red.)

- [ ] **Step 4: Stage & propose commit**

```bash
git add Assets/Scripts/Core/RingIndex.cs Assets/Tests/EditMode/RingIndexTests.cs
```
Proposed message: `feat(core): implement RingIndex wrap-around maths with tests`

### Task 1.2: `AmmoPool` — fix the two latent bugs, prove with tests

**Files:**
- Modify: `Assets/Scripts/Core/AmmoPool.cs`
- Test: `Assets/Tests/EditMode/AmmoPoolTests.cs` (new)

**Interfaces:**
- Produces: `Core.AmmoPool<TKey>` with `Set(TKey, int)`, `int Get(TKey)`, `bool Has(TKey)`, `int Consume(TKey)`, `int TotalRemaining()`, `void CopyFrom(IReadOnlyList<KeyValuePair<TKey,int>>)`, and `event Action<TKey,int> OnChanged`. **Contract:** `Has` means "count > 0"; `Get`/`Consume` on an unknown key return `0` and never throw. Phase 3.1 (`AmmoManager.AttachPool` / `HasAmmo` / `Consume`) and Phase 2.3 (pool construction) consume exactly this.

**What is wrong with the draft.** Two bugs that unit tests exist to catch:

1. `Get` and `Consume` use the indexer `_counts[key]`, which **throws `KeyNotFoundException`** for a
   key that was never set. In the game this fires when a loadout omits a type and the player somehow
   fires it — a crash instead of a graceful "0".
2. `Has` is `_counts.ContainsKey(key)` — "*the key exists*", not "*there is ammo left*". After
   consuming the last Boulder, `Has(Boulder)` would still return `true` and the UI would keep the
   button clickable.

**Teaching note — generics as a decoupling tool.** `AmmoPool<SO_ProjectileType>` would drag
`Assembly-CSharp` into Core and break the test assembly. `AmmoPool<TKey>` keeps Core pure, and the
tests instantiate `AmmoPool<string>` to prove the behaviour without any game types. The game layer
(Task 3.1) decides the key is a `SO_ProjectileType`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class AmmoPoolTests
    {
        private AmmoPool<string> _pool;

        [SetUp]
        public void SetUp() => _pool = new AmmoPool<string>();

        [Test]
        public void Set_ThenGet_ReturnsCount()
        {
            _pool.Set("wood", 7);
            Assert.AreEqual(7, _pool.Get("wood"));
        }

        [Test]
        public void Get_UnknownKey_ReturnsZero()
        {
            Assert.AreEqual(0, _pool.Get("nothing"));
        }

        [Test]
        public void Has_MeansCountAboveZero()
        {
            _pool.Set("wood", 2);
            Assert.IsTrue(_pool.Has("wood"));

            _pool.Set("stone", 0);
            Assert.IsFalse(_pool.Has("stone"));       // exists but empty → not "has ammo"

            Assert.IsFalse(_pool.Has("never-added")); // unknown key → not "has ammo"
        }

        [Test]
        public void Consume_DecrementsAndReturnsNewValue()
        {
            _pool.Set("wood", 3);
            Assert.AreEqual(2, _pool.Consume("wood"));
            Assert.AreEqual(2, _pool.Get("wood"));
        }

        [Test]
        public void Consume_AtZero_ReturnsZeroAndStaysZero()
        {
            _pool.Set("wood", 0);
            Assert.AreEqual(0, _pool.Consume("wood"));
            Assert.AreEqual(0, _pool.Get("wood"));    // never goes negative
        }

        [Test]
        public void Consume_UnknownKey_ReturnsZeroWithoutThrowing()
        {
            Assert.DoesNotThrow(() => _pool.Consume("nothing"));
            Assert.AreEqual(0, _pool.Consume("nothing"));
        }

        [Test]
        public void Consume_RaisesOnChanged_WithNewValue()
        {
            string lastKey = null;
            int lastValue = -1;
            _pool.OnChanged += (key, value) => { lastKey = key; lastValue = value; };

            _pool.Set("wood", 2);
            Assert.AreEqual(("wood", 2), (lastKey, lastValue));   // Set raises too

            _pool.Consume("wood");
            Assert.AreEqual(("wood", 1), (lastKey, lastValue));
        }

        [Test]
        public void TotalRemaining_SumsAllKeys()
        {
            _pool.Set("wood", 2);
            _pool.Set("stone", 5);
            Assert.AreEqual(7, _pool.TotalRemaining());
        }

        [Test]
        public void CopyFrom_ReplacesContents()
        {
            _pool.Set("old", 9);
            _pool.CopyFrom(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("wood", 3),
                new KeyValuePair<string, int>("stone", 1),
            });

            Assert.AreEqual(0, _pool.Get("old"));     // old contents are gone
            Assert.AreEqual(3, _pool.Get("wood"));
            Assert.AreEqual(4, _pool.TotalRemaining());
        }
    }
}
```

*Teaching note — `[SetUp]`.* NUnit runs `[SetUp]` before **every** `[Test]`, so each test starts
with a fresh empty pool — tests never depend on each other's leftovers. The tuple assertion
`Assert.AreEqual(("wood", 2), (lastKey, lastValue))` compares both event fields in one line (C#
tuples have built-in equality).

- [ ] **Step 2: Run the tests.** Expected: **exactly 3 FAIL** —
  `Get_UnknownKey_ReturnsZero` and `Consume_UnknownKey_ReturnsZeroWithoutThrowing` (the draft's
  `_counts[key]` indexer throws `KeyNotFoundException`), and `Has_MeansCountAboveZero` (the draft's
  `ContainsKey` returns `true` for a key set to 0). The other six pass — the draft already handles
  the at-zero consume and the event.

- [ ] **Step 3: Fix `AmmoPool` — the minimal diff is three methods**

```csharp
public int Get(TKey key)
{
    return _counts.TryGetValue(key, out int count) ? count : 0;
}

// "has ammo" = the key exists AND the count is above zero
public bool Has(TKey key)
{
    return _counts.TryGetValue(key, out int count) && count > 0;
}

public int Consume(TKey key)
{
    if (!Has(key)) return 0;   // unknown key or already empty

    int newValue = --_counts[key];
    OnChanged?.Invoke(key, newValue);
    return newValue;
}
```

*Line-by-line:*
- `TryGetValue(key, out int count)` is the safe dictionary lookup: it returns `false` instead of
  throwing when the key is missing, and hands you the value through the `out` variable when it
  succeeds. The ternary `? count : 0` maps "missing" to `0` — an unknown projectile type simply has
  no ammo.
- `Has` adds `&& count > 0` to the same lookup — one line turns "exists" into "usable".
- `Consume` now guards with `Has`, so the `--_counts[key]` indexer that follows can never hit a
  missing key (guard clause first) and never goes below zero (Has is false at 0).
- `Set`, `TotalRemaining`, `CopyFrom` and the `OnChanged` event stay exactly as drafted.

**Teaching note — deliberate design choice:** `Set` stores what you give it, even a negative —
the pool is a dumb container. Clamping (`Mathf.Max(0, count)`) happens once at *build* time in
`CatapultManager` (Task 2.3), which is the only place loadout numbers enter the pool. One clamp at
the boundary beats clamping in every method.

- [ ] **Step 4: Run the tests again.** Expected: `AmmoPoolTests` **9/9 PASS** (and `RingIndexTests` still green).

- [ ] **Step 5: Stage & propose commit**

```bash
git add Assets/Scripts/Core/AmmoPool.cs Assets/Tests/EditMode/AmmoPoolTests.cs
```
Proposed message: `fix(core): make AmmoPool.Has mean count>0, guard Get/Consume against unknown keys`

### Task 1.3: `CameraPath` — guard the empty path, prove the maths

**Files:**
- Modify: `Assets/Scripts/Core/CameraPath.cs` (constructor guard only)
- Test: `Assets/Tests/EditMode/CameraPathTests.cs` (new)

**Interfaces:**
- Produces: `Core.CatmullRom.Sample(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment)` → `List<Vector3>` (starts at controlPoints[0], ends at controlPoints[^1]); `Core.PolylinePath` with `new PolylinePath(IReadOnlyList<Vector3>)`, `float TotalLength`, `Vector3 Evaluate(float t)` — constant-speed, safe for 0/1-point inputs. Phase 2.5 (`CatapultCameraRig`) consumes both.

**What is wrong with the draft.** `PolylinePath`'s constructor indexes `points[0]` unguarded — an
empty (or null) list crashes. `CatapultCameraRig` will construct paths from `CameraAnchor.pathPoints`,
and an anchor with no points must not be able to crash the camera.

**Teaching note — what the two classes do, in one breath each.** `CatmullRom.Sample` turns a handful
of control points into a dense polyline (the curve passes *through* every point; the tangent at each
point is the average of its neighbours' direction). `PolylinePath` then turns any polyline into a
**constant-speed track**: it pre-measures the cumulative distance of every sample, and
`Evaluate(t)` answers "where am I after covering `t × TotalLength` units?" — so `t = 0.5` is always
*half the distance*, not half the parameter (uniform `t` on a curve speeds up on long segments and
crawls on short ones). The Persian appendix at the bottom of this document derives all of this from
zero.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.EditMode
{
    public class CameraPathTests
    {
        // ---- CatmullRom.Sample -------------------------------------------------

        [Test]
        public void Sample_StartsAtFirstPoint_EndsAtLastPoint()
        {
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 1, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            // (count - 1) segments × samplesPerSegment, plus the final point
            Assert.AreEqual((points.Count - 1) * 10 + 1, samples.Count);
            Assert.AreEqual(points[0], samples[0]);
            Assert.AreEqual(points[points.Count - 1], samples[samples.Count - 1]);
        }

        [Test]
        public void Sample_CollinearPoints_StaysOnTheLine()
        {
            // A straight line is the one case where the exact answer is known:
            // the spline must NOT overshoot it. 4 collinear points 1 unit apart,
            // 10 samples per segment → samples every 0.1 units from 0 to 3.
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(2, 0, 0), new Vector3(3, 0, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            Assert.AreEqual(new Vector3(1.5f, 0, 0), samples[15], "midpoint drifted off the line");
            Assert.AreEqual(0.1f, samples[10].x, 1e-3f);   // first sample of segment 2
        }

        [Test]
        public void Sample_FewerThanTwoPoints_ReturnsEmpty()
        {
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3>(), 10).Count);
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3> { Vector3.zero }, 10).Count);
        }

        // ---- PolylinePath ------------------------------------------------------

        [Test]
        public void StraightPath_EndpointsAreExact()
        {
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(10f, path.TotalLength, 1e-4f);
            Assert.AreEqual(new Vector3(0, 0, 0), path.Evaluate(0f));
            Assert.AreEqual(new Vector3(10, 0, 0), path.Evaluate(1f));
        }

        [Test]
        public void StraightPath_MidpointIsHalfTheDistance()
        {
            // The arc-length promise: t = 0.5 means "half the DISTANCE travelled",
            // which on a straight 10-unit path is exactly x = 5.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(5f, path.Evaluate(0.5f).x, 1e-4f);
        }

        [Test]
        public void BentPath_MidpointIsHalfTheTotalDistance()
        {
            // Two legs: A→B is 3 units, B→C is 4 units. Half of 7 units = 3.5,
            // which lands 0.5 units past B along the second leg.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 4, 0),
            });

            Vector3 mid = path.Evaluate(0.5f);
            Assert.AreEqual(3f, mid.x, 1e-4f);
            Assert.AreEqual(0.5f, mid.y, 1e-4f);
        }

        [Test]
        public void EmptyOrSinglePoint_DoesNotThrow()
        {
            // An anchor with no path points must not be able to crash the camera.
            var empty = new PolylinePath(new Vector3[0]);
            Assert.AreEqual(0f, empty.TotalLength);
            Assert.AreEqual(Vector3.zero, empty.Evaluate(0.5f));

            var single = new PolylinePath(new[] { new Vector3(2, 3, 4) });
            Assert.AreEqual(0f, single.TotalLength);
            Assert.AreEqual(new Vector3(2, 3, 4), single.Evaluate(0.5f));
        }
    }
}
```

*Why the bent-path test matters:* it is the only test that can catch an arc-length bug a straight
line cannot — on a straight path, uniform-`t` and uniform-distance happen to agree. The 3+4 legs
make `Evaluate(0.5f)` land in the *second* segment at a spot nothing but correct cumulative-distance
maths can produce.

- [ ] **Step 2: Run the tests.** Expected: `EmptyOrSinglePoint_DoesNotThrow` **FAILS**
  (`IndexOutOfRangeException` in the constructor). The maths tests should already pass — the draft
  implementation is correct.

- [ ] **Step 3: Guard the constructor** — replace the top of `PolylinePath`'s constructor:

```csharp
public PolylinePath(IReadOnlyList<Vector3> points)
{
    int count = points?.Count ?? 0;
    if (count == 0)
    {
        // Degenerate path: stand still. Evaluate() returns _points[0] because TotalLength is 0.
        _points = new[] { Vector3.zero };
        _cumDist = new float[] { 0f };
        TotalLength = 0f;
        return;
    }

    _points = new Vector3[count];
    _cumDist = new float[count];
    // ... rest of the constructor unchanged
```

*Line-by-line:*
- `points?.Count ?? 0` — the null-conditional `?.` yields `null` for a null list, and `??` (the
  null-coalescing operator) substitutes `0`. One line handles both "no list" and "empty list".
- The degenerate branch builds a valid one-point path at the origin, so `Evaluate` (which already
  early-outs when `TotalLength <= 0`) returns `Vector3.zero` without any further special-casing.

- [ ] **Step 4: Run the tests.** Expected: `CameraPathTests` **8/8 PASS** (in Step 2 only
  `EmptyOrSinglePoint_DoesNotThrow` failed — the draft's maths was already correct).

- [ ] **Step 5: Stage & propose commit**

```bash
git add Assets/Scripts/Core/CameraPath.cs Assets/Tests/EditMode/CameraPathTests.cs
```
Proposed message: `fix(core): guard PolylinePath against empty paths, add camera path tests`

---

## Phase 2 — New runtime components

All of these live in `Assembly-CSharp` (no asmdef) under `Assets/Scripts/Catapult/` (new folder) and
`Assets/Scripts/Camera/` (new folder). Remember the rule: **create the `.cs` file in the Editor**
(right-click the Project window ▸ Create ▸ C# Script) so Unity generates the `.meta` with its GUID
— never create script files outside Unity.

### Task 2.1: `SO_LevelData.ammoMode` — the per-level switch

**Files:**
- Modify: `Assets/Scripts/ScriptableObjects/Level/SO_LevelData.cs`

**Interfaces:**
- Produces: `enum AmmoMode { Shared, PerCatapult }` and `SO_LevelData.ammoMode` (default `Shared`). Task 2.3 reads it to decide how many pools to build.

**Teaching note — enums in ScriptableObjects.** Unity serialises an enum field as an integer and
draws it as a dropdown in the Inspector, so designers get a readable choice with no extra UI code.
Because `Shared` is the first value (`0`), every existing asset keeps `Shared` when the field is
added — old levels do not silently change behaviour. And `Shared` *is* today's behaviour: one
catapult, one pool. Making `PerCatapult` the default would silently double the ammo of every
existing level.

- [ ] **Step 1: Add the enum and the field**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public enum AmmoMode { Shared, PerCatapult }

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
public class SO_LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();

    [Tooltip("Shared = all catapults fire from one pool. PerCatapult = each catapult gets a full copy of the loadout.")]
    public AmmoMode ammoMode = AmmoMode.Shared;
}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}
```

(Only the two marked lines are new; `ProjectileLoadout` is untouched.)

- [ ] **Step 2: Verify in the Inspector.** Select the `DefaultLevelData` asset — a new **Ammo Mode**
  dropdown reading `Shared` must appear. No other asset changes.

- [ ] **Step 3: Stage & propose commit**

```bash
git add Assets/Scripts/ScriptableObjects/Level/SO_LevelData.cs
```
Proposed message: `feat(level): add AmmoMode switch to SO_LevelData`

### Task 2.2: `Catapult` — the per-catapult facade

**Files:**
- Create: `Assets/Scripts/Catapult/Catapult.cs`

**Interfaces:**
- Consumes: nothing yet (fields wired in Phase 5).
- Produces: `Catapult` with serialized fields `launcher, spawner, ammoManager, cameraAnchor, highlightObject`; properties `int Index { get; internal set; }`, `bool IsActive { get; internal set; }`, `Vector3 RestPosition { get; }`, `Quaternion RestRotation { get; }`; methods `void CaptureRestPose()`, `void SetHighlight(bool)`. Task 2.3 (manager), 2.4/2.5 (camera), 3.5 (carousel) consume these.

**Teaching note — what a "facade" is.** A facade is a small object that *forwards* to the real ones
behind it. `Catapult` owns no behaviour of its own; it is the one place game code can ask "give me
the launcher of *this* catapult" without knowing the scene layout or component names. That keeps
`InputController` and the UI from reaching into the hierarchy with `FindObjectOfType`.

**Teaching note — `internal set`.** The property is readable everywhere in the assembly but
writable only from inside it — how `CatapultManager` sets `Index`/`IsActive` without letting
arbitrary game code corrupt the registry.

- [ ] **Step 1: Create the script**

```csharp
using UnityEngine;

// Per-catapult facade: one place to ask for this catapult's launcher/spawner/ammo/anchor.
// Owns no behaviour; CatapultManager drives it.
public class Catapult : MonoBehaviour
{
    public ProjectileLuncher launcher;
    public ProjectileSpawner spawner;
    public AmmoManager ammoManager;
    public CameraAnchor cameraAnchor;
    public GameObject highlightObject;   // optional visual; toggled on/off

    public int Index { get; internal set; }
    public bool IsActive { get; internal set; }

    // The authored (un-aimed) pose. The camera anchor is evaluated against this frame,
    // so the camera's destination is stable even while the body yaws during aiming.
    public Vector3 RestPosition { get; private set; }
    public Quaternion RestRotation { get; private set; }

    // Called by CatapultManager.Awake — deliberately NOT Awake() on this component.
    // See "The lifecycle-ordering trap" in the plan for why the manager must call this.
    public void CaptureRestPose()
    {
        RestPosition = transform.position;
        RestRotation = transform.rotation;
    }

    public void SetHighlight(bool on)
    {
        if (highlightObject) highlightObject.SetActive(on);
    }
}
```

- [ ] **Step 2: Stage & propose commit** (the `cameraAnchor` field's type is created in Task 2.4, so this task alone shows red squiggles — expected; write 2.4 before compiling.)

```bash
git add Assets/Scripts/Catapult/Catapult.cs Assets/Scripts/Catapult/Catapult.cs.meta
```
Proposed message: `feat(catapult): add Catapult facade component`

**Why `CaptureRestPose` is a method the manager calls, not this component's own `Awake`:** Unity
calls `Awake` on every object before any `Start`, but the *relative order of two Awakes is
undefined*. If `Catapult.Awake` captured the pose and the manager's `Awake` happened to run first,
the manager would read `RestPosition == (0,0,0)` and snap the camera to the world origin — a bug
that appears only on some Play sessions. An explicit `CaptureRestPose()` called by the manager
(before it reads the values) is order-independent: whoever needs the value asks for it first, and
nothing has moved yet because no gameplay code runs before `Start`.

### Task 2.3: `CatapultManager` — the level brain

**Files:**
- Create: `Assets/Scripts/Catapult/CatapultManager.cs`

**Interfaces:**
- Consumes: `Catapult` (Task 2.2), `Core.AmmoPool<SO_ProjectileType>` (Task 1.2), `RingIndex` (Task 1.1), `SO_LevelData.ammoMode` (Task 2.1), existing `ProjectileSelector.Initialize`.
- Produces: `CatapultManager` with `SO_LevelData levelData`, `ProjectileSelector selector`, `CatapultCameraRig cameraRig`; `event Action<Catapult> OnActiveCatapultChanged`; `Catapult ActiveCatapult { get; }`; `IReadOnlyList<Catapult> Catapults { get; }`; `void SetActive(Catapult)`, `void NextCatapult()`, `void PreviousCatapult()`. Tasks 2.4 (camera), 3.4 (launcher), 3.5 (input), 3.6 (carousel), 4.3 (switcher UI) consume these.

**Teaching note — `event Action<Catapult>`.** This is the project's existing pub-sub idiom (see
`ProjectileSelector.OnSelectionChanged`, `AmmoManager.OnAmmoChanged`). The manager does not know
that a carousel or a camera rig exists; it announces "the active catapult changed" and subscribers
react. That *loose coupling* is why the UI and camera never need hard references to the manager's
insides.

- [ ] **Step 1: Create the script**

```csharp
using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Level brain: finds the catapults, owns the ONE global type list, builds the ammo
// pool(s) according to SO_LevelData.ammoMode, and tracks which catapult is active.
public class CatapultManager : MonoBehaviour
{
    public SO_LevelData levelData;
    public ProjectileSelector selector;          // the ONE global selector
    public CatapultCameraRig cameraRig;          // optional in code; assign in the scene

    public event Action<Catapult> OnActiveCatapultChanged;

    public Catapult ActiveCatapult { get; private set; }
    public IReadOnlyList<Catapult> Catapults => _catapults;

    private readonly List<Catapult> _catapults = new List<Catapult>();

    private void Awake()
    {
        FindCatapults();

        if (_catapults.Count == 0)
        {
            Debug.LogError("CatapultManager: no Catapult components in the scene. Arrows and aiming are disabled.", this);
            return;
        }

        List<SO_ProjectileType> types = BuildTypeList();   // the ONE place the type list is built

        List<AmmoPool<SO_ProjectileType>> pools = BuildPools(types);
        AttachPools(pools);

        selector.Initialize(types);

        SetActive(_catapults[0]);   // first activation snaps the rig (no glide on frame one)
    }

    private void FindCatapults()
    {
        _catapults.AddRange(FindObjectsByType<Catapult>(FindObjectsSortMode.None));
        for (int i = 0; i < _catapults.Count; i++)
        {
            Catapult catapult = _catapults[i];
            catapult.Index = i;
            catapult.CaptureRestPose();          // BEFORE anything reads RestPosition/RestRotation
            catapult.SetHighlight(false);
        }
    }

    // Loadout wins when non-empty; otherwise fall back to the selector's pre-authored
    // availableTypes + each type's own ammoCount — exactly what AmmoManager.Awake did pre-refactor.
    private List<SO_ProjectileType> BuildTypeList()
    {
        List<SO_ProjectileType> types = new List<SO_ProjectileType>();

        if (levelData && levelData.projectileLoadouts.Count > 0)
        {
            foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                if (entry.projectileType) types.Add(entry.projectileType);
        }
        else
        {
            types.AddRange(selector.availableTypes);
        }

        return types;
    }

    private List<AmmoPool<SO_ProjectileType>> BuildPools(List<SO_ProjectileType> types)
    {
        // The loadout numbers (or each type's own ammoCount when there is no loadout).
        int CountFor(SO_ProjectileType type)
        {
            if (levelData && levelData.projectileLoadouts.Count > 0)
            {
                foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                    if (entry.projectileType == type) return Mathf.Max(0, entry.ammoCount);
                return 0;
            }
            return Mathf.Max(0, type.ammoCount);
        }

        var pools = new List<AmmoPool<SO_ProjectileType>>();

        if (!levelData || levelData.ammoMode == AmmoMode.Shared)
        {
            var shared = new AmmoPool<SO_ProjectileType>();
            foreach (SO_ProjectileType type in types) shared.Set(type, CountFor(type));
            pools.Add(shared);                                     // ONE pool...
        }
        else
        {
            for (int i = 0; i < _catapults.Count; i++)
            {
                var pool = new AmmoPool<SO_ProjectileType>();
                foreach (SO_ProjectileType type in types) pool.Set(type, CountFor(type));
                pools.Add(pool);                                   // ...or one per catapult
            }
        }

        return pools;
    }

    private void AttachPools(List<AmmoPool<SO_ProjectileType>> pools)
    {
        for (int i = 0; i < _catapults.Count; i++)
        {
            // Shared mode: everyone indexes [0] and shares the same pool object.
            _catapults[i].ammoManager.AttachPool(pools[i % pools.Count]);
        }
    }

    public void SetActive(Catapult catapult)
    {
        if (catapult == null || catapult == ActiveCatapult) return;

        bool firstActivation = ActiveCatapult == null;

        if (ActiveCatapult)
        {
            ActiveCatapult.IsActive = false;
            ActiveCatapult.SetHighlight(false);
        }

        ActiveCatapult = catapult;
        catapult.IsActive = true;
        catapult.SetHighlight(true);

        // Only the active catapult loads a projectile on demand — inactive ones may have
        // an empty pool for this type (PerCatapult mode) and must not pre-load.
        if (catapult.launcher) catapult.launcher.LoadProjectile();

        OnActiveCatapultChanged?.Invoke(catapult);

        if (!cameraRig) return;
        if (firstActivation) cameraRig.SnapTo(catapult);   // the camera should just BE there at level start
        else cameraRig.MoveTo(catapult);
    }

    // Wrap-around is deliberate: from the last catapult, next goes to the first.
    public void NextCatapult()
    {
        if (ActiveCatapult == null) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, 1)]);
    }

    public void PreviousCatapult()
    {
        if (ActiveCatapult == null) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, -1)]);
    }
}
```

- [ ] **Step 2: Stage & propose commit** (this task will not compile until Task 2.5's `CatapultCameraRig` and Task 3.1's `AttachPool`/`LoadProjectile` exist — either write those tasks first in the same session, or accept the red squiggles and commit everything together after Task 2.5).

**Line-by-line notes on the parts that matter:**

*`FindObjectsByType<Catapult>(FindObjectsSortMode.None)`* — the Unity 6 replacement for the
deprecated `FindObjectsOfType`. The `FindObjectsSortMode` argument controls sorting
(`None` is cheapest). *The order of the array is not guaranteed* — "catapult 0" is not necessarily
the leftmost. The plan uses the array as-is and the *arrows* navigate it, so order only affects
which catapult starts active. If you want a deterministic order, sort by `transform.position.x`
after finding.

*`BuildTypeList` is the one and only place the type list is built.* The old `AmmoManager.Awake()`
call to `projectileSelector.Initialize(types)` is deleted (Task 3.1). **Teaching note — order of
`Awake` calls is not guaranteed:** Unity runs every `Awake` before any `Start`, but the relative
order between two objects' `Awake`s is undefined, so nothing may depend on "object X's `Awake` ran
first". Build the data in one place, hand it to everyone else — that is this class's whole job.

*`CountFor` is a **local function**.* A method declared inside another method — it captures
`levelData` directly instead of passing it, and it does not clutter the class's public surface.
C# has had these since 7.0; Unity supports them.

*Why the pool is a separate object, not a field inside `AmmoManager`:* if the pool lived inside
`AmmoManager`, "shared" would mean "all catapults point at the same `AmmoManager` component" —
different *scene wiring* per mode, undecidable at runtime. With the pool as a plain object, the
scene wiring is **identical in both modes** (every catapult always has its own `AmmoManager`) and
only the runtime assignment differs. `pools[i % pools.Count]` expresses that in one line: in
shared mode `pools.Count == 1`, so every index maps to pool 0; in per-catapult mode
`pools.Count == _catapults.Count`, so each catapult gets its own.

*Why `SetActive` calls `LoadProjectile()` before raising the event:* the carousel's
`OnActiveCatapultChanged` handler re-reads counts from `ActiveCatapult.ammoManager`; the launcher
load is independent of that. Order between the two does not matter — but calling `LoadProjectile`
first means that if any event listener inspects the loaded projectile, it is already there.

*Edge cases handled explicitly:*
- **Zero catapults** → log a clear error, bail out of `Awake`. `ActiveCatapult` stays null;
  `NextCatapult`/`PreviousCatapult` guard on it, so no `NullReferenceException` in `Update`.
- **A catapult with an unassigned `ammoManager`** → `AttachPools` would `NullReferenceException`
  mid-setup and leave the level half-initialised. Fail loudly instead, right after finding:
  `Debug.LogError($"Catapult '{catapult.name}' has no AmmoManager assigned.", catapult); return;`
  inside the `FindCatapults` loop (before `CaptureRestPose` is worth anything).
- **A catapult with no `CameraAnchor`** → handled in Task 2.5 (the rig stays put and warns).
- **`levelData` null** → falls back to the selector's pre-authored `availableTypes` + `ammoCount` (today's behaviour) and a shared pool.
- **A loadout entry with a missing type** → skipped in `BuildTypeList` (`if (entry.projectileType)`), same as today's `InitializeAmmo`.
- **A type with no `prefab`** → still listed and counted, but `ProjectileSpawner.InstantiateBullet` already returns `null`; Task 3.2/3.3 keep that null-safe.

## The lifecycle-ordering trap (read this before writing any of the code)

This is the single easiest thing to get wrong in this feature, and it is invisible until the symptom
appears as "sometimes the wrong catapult loads" or "the camera snapped to the origin".

**What Unity guarantees:**
* For a *single* object: `Awake` → `OnEnable` → `Start` → `Update`.
* Across *all* objects: **every `Awake` runs before any `Start`.**
* Across all objects: **the relative order of two `Awake`s is undefined.** Same for two `Start`s,
  and same for `OnEnable`s.

**The three concrete rules for this feature:**

1. **All setup goes in `CatapultManager.Awake`, and everything that *needs* that setup runs in
   `Start` or later.** Because all `Awake`s precede all `Start`s, the launcher's `Start` is
   *guaranteed* to run after the manager's `Awake` has attached pools and initialised the selector. ✅
2. **The rest pose is captured by the manager, not by `Catapult.Awake`.** If `Catapult.Awake`
   captured it and the manager's `Awake` ran first, the manager would read `RestPosition == (0,0,0)`
   and snap the camera to the origin — on *some* Plays, not all, which is the worst kind of bug.
   Hence the explicit `CaptureRestPose()` from Task 2.2.
3. **Do not rely on `OnSelectionChanged` for the *initial* selection.** `selector.Initialize(types)`
   raises the event, but a launcher that has not run `OnEnable` yet never hears it. That is why
   `ProjectileLuncher.Start` calls `LoadProjectile()` explicitly (Task 3.4). Events are for
   *changes*, not for initial state.

**One harmless wrinkle:** the very first projectile may be instantiated twice — once by
`HandleSelectionChanged` (if the launcher was enabled before the manager initialised the selector)
and once by `Start`. `InstantiateBulletSafe` destroys the previous one, so the result is correct;
it costs one extra `Instantiate`. If that ever bothers you, add a `_hasLoaded` flag. **Do not**
try to "fix" it by reordering `Awake` calls — you cannot.

**Teaching note — how to *see* the order instead of guessing.** Add a temporary
`Debug.Log($"{name}.Awake");` to `CatapultManager`, `AmmoManager` and `ProjectileLuncher` and watch
the Console on the first Play frame. Do this once — the same trap will bite you in every Unity
project.

---

### Task 2.4: `CameraAnchor` — where the camera should be

**Files:**
- Create: `Assets/Scripts/Camera/CameraAnchor.cs`

**Interfaces:**
- Produces: `CameraAnchor` (MonoBehaviour) with `List<Transform> pathPoints`; its own transform *is* the desired camera pose. Tasks 2.5 and 2.6 consume it.

```csharp
using System.Collections.Generic;
using UnityEngine;

// Child of a catapult root. Its local transform IS the desired camera pose.
// pathPoints (optional) are children of the SAME catapult root: 2+ of them makes
// the camera glide along a Catmull-Rom path; 0–1 makes it glide straight.
public class CameraAnchor : MonoBehaviour
{
    public List<Transform> pathPoints = new List<Transform>();
}
```

The anchor carries no offset fields — you place it in the Scene view like any other object, and its
`localPosition`/`localRotation` relative to the catapult root *are* the numbers.

**The one rule for everything camera-related:** the anchor and its path waypoints are all **children
of the catapult root**, and they are all interpreted in the catapult's **rest** (un-aimed) frame.

**Teaching note — world pose vs. local pose.** A child of the catapult inherits its rotation, so
the anchor's *world* pose changes while the catapult yaws during aiming — that would drag the
camera's destination around. To keep the destination stable, `Catapult` captured its rest pose
(Task 2.2) and the rig evaluates the anchor against that frozen frame:

```
worldPos  = RestPosition + RestRotation * anchor.localPosition
worldRot  = RestRotation * anchor.localRotation
waypoint  = RestPosition + RestRotation * waypoint.localPosition
```

**Teaching note — why not `Transform.TransformPoint`?** `TransformPoint` converts local → world
using the transform's *current* rotation — and "current" is exactly what we want to freeze. Doing
the multiplication by hand with the captured rest rotation is one extra line and gives a
destination that never drifts. (Bonus: because the waypoints are children of the catapult *root*,
not of the anchor, their `localPosition` is already in the same frame as the anchor's — no nested
transform maths.)

- [ ] **Step 1: Create the script.** No behaviour to test — the class is two fields; correctness comes in Task 2.5's rig.
- [ ] **Step 2: Stage & propose commit**

```bash
git add Assets/Scripts/Camera/CameraAnchor.cs Assets/Scripts/Camera/CameraAnchor.cs.meta
```
Proposed message: `feat(camera): add CameraAnchor component`

### Task 2.5: `CatapultCameraRig` — the camera mover

**Files:**
- Create: `Assets/Scripts/Camera/CatapultCameraRig.cs`

**Interfaces:**
- Consumes: `Catapult` + `CameraAnchor` (Tasks 2.2/2.4), `Core.CatmullRom.Sample` + `Core.PolylinePath` (Task 1.3).
- Produces: `CatapultCameraRig` with `[Range] float followSharpness = 3f`, `float moveDuration`, `AnimationCurve ease`; `void SnapTo(Catapult)` (instant), `void MoveTo(Catapult)` (animated glide, then follow). `CatapultManager` (2.3) already calls both; the aim-follow behaviour lands in Task 2.6.

**What it does.** The rig GameObject owns the camera as a child at local `(0,0,0)` — so *the rig's
own transform is the camera's world pose*. `MoveTo` glides that transform to the active catapult's
anchor pose: straight, or along a Catmull-Rom path built from `[startPos, …waypoints, endPos]`.

**Teaching note — the straight move is the same code path.** A straight A→B move is just a
`PolylinePath` with two points. So the rig has exactly one code path: *build a path, then walk it*.
`pathPoints.Count >= 2` → Catmull-Rom over the waypoints; anything less → the 2-point straight path.
No branching in the tween itself.

**Teaching note — why a coroutine and not `async/await`.** Unity's `async/await` resumes on a
thread-pool thread by default, and Unity API calls are not thread-safe. A coroutine
(`IEnumerator` + `yield return null`) always resumes on the **main thread** on the next frame — the
only place you may touch `transform`. It is the standard Unity idiom for timed animation; this
project already uses it in `ProjectileLuncher.ResetHeadRotationRoutine` and
`ProjectileCarouselUI.TweenButtons`.

**Teaching note — `Quaternion.Slerp`.** Interpolating rotations with `Vector3.Lerp` is wrong:
Euler angles wrap around 360° and do not take the shortest arc. `Quaternion.Slerp` (spherical
linear interpolation) rotates along the shortest arc at constant angular speed. The project already
uses it in `ResetHeadRotationRoutine`. The rotation is *not* taken from the path tangent on
purpose — look-along-curve breaks when a path doubles back, and `Slerp` start→end is predictable.

- [ ] **Step 1: Create the script**

```csharp
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Top-level camera owner. The camera is a child at local (0,0,0), so this
// transform IS the camera's world pose. Glides between per-catapult anchors;
// straight when the anchor has < 2 path points, Catmull-Rom otherwise.
public class CatapultCameraRig : MonoBehaviour
{
    public float moveDuration = 1.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0.1f, 20f)] public float followSharpness = 3f;   // aim-follow lag (used in Task 2.6)

    private enum RigState { Idle, Moving, Following }
    private RigState _state = RigState.Idle;

    private Coroutine _moveRoutine;
    private Catapult _followTarget;

    public void SnapTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = null;

        EvaluateAnchorPose(catapult, out Vector3 pos, out Quaternion rot);
        transform.SetPositionAndRotation(pos, rot);

        _followTarget = catapult;
        _state = RigState.Following;
    }

    public void MoveTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        _followTarget = catapult;

        // Capture BEFORE StopCoroutine: the new glide starts from where the camera
        // is right now, so interrupting a move never snaps back to the old start.
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        EvaluateAnchorPose(catapult, out Vector3 endPos, out Quaternion endRot);

        var controlPoints = new List<Vector3> { startPos };
        foreach (Transform waypoint in catapult.cameraAnchor.pathPoints)
        {
            if (!waypoint) continue;
            controlPoints.Add(RelativeToRestFrame(catapult, waypoint.localPosition));
        }
        controlPoints.Add(endPos);

        PolylinePath path = new PolylinePath(controlPoints);

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(path, startRot, endRot));
    }

    private IEnumerator MoveRoutine(PolylinePath path, Quaternion startRot, Quaternion endRot)
    {
        _state = RigState.Moving;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float eased = ease.Evaluate(Mathf.Clamp01(elapsed / moveDuration));
            transform.position = path.Evaluate(eased);
            transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }

        // Snap to the exact end pose so no float drift is left behind
        // (same trick as ProjectileLuncher.ResetHeadRotationRoutine).
        transform.SetPositionAndRotation(path.Evaluate(1f), endRot);
        _moveRoutine = null;
        _state = RigState.Following;
    }

    private static void WarnNoAnchor(Catapult catapult)
    {
        if (catapult)
            Debug.LogWarning($"CatapultCameraRig: '{catapult.name}' has no CameraAnchor — camera stays put.", catapult);
    }

    // The anchor is authored as a child of the catapult root, in the catapult's REST
    // frame. RestRotation * localRotation maps it to world space frozen at that rest pose.
    // Both entry points already guarded cameraAnchor != null, so this can assume it.
    private static void EvaluateAnchorPose(Catapult catapult, out Vector3 pos, out Quaternion rot)
    {
        CameraAnchor anchor = catapult.cameraAnchor;
        pos = catapult.RestPosition + catapult.RestRotation * anchor.transform.localPosition;
        rot = catapult.RestRotation * anchor.transform.localRotation;
    }

    private static Vector3 RelativeToRestFrame(Catapult catapult, Vector3 localPosition)
    {
        return catapult.RestPosition + catapult.RestRotation * localPosition;
    }
}
```

**Line-by-line notes on the parts that matter:**

*`_state` enum instead of booleans.* With `_isMoving` + `_isFollowing` you can create impossible
states (moving *and* following), and both blocks writing `transform.position` each frame would
fight. An enum makes the states mutually exclusive by construction: the coroutine sets `Moving`
on its first frame and `Following` on its last, `SnapTo` jumps straight to `Following`. The follow
block (Task 2.6) runs only in `Following` — so a glide and the follow can never write
`transform.position` in the same frame.

*No anchor → the camera does not move.* Both entry points guard `cameraAnchor == null` and warn,
so a designer who forgot the anchor gets one clear Console line instead of a camera teleporting to
the origin. The guard lives at the door, which is why `EvaluateAnchorPose` can assume the anchor
exists.

*Interrupting a move.* `MoveTo` captures `startPos`/`startRot` from the rig's **current** transform
*before* `StopCoroutine` kills the previous glide — so a rapid re-click of the arrow starts the new
path from where the camera actually is, never snapping back to where the old move began. This is
why there is deliberately no "if already moving, just retarget the target field" early-out: a
running coroutine keeps flying toward its captured end pose, so it must be *restarted* from the
current pose, not merely re-pointed.

*Why `Evaluate` at eased `t`, and `Slerp` with the same eased `t`* — both position and rotation
progress on the same eased clock, so the camera arrives facing the anchor exactly when it arrives
at the anchor.

- [ ] **Step 2: Stage & propose commit**

```bash
git add Assets/Scripts/Camera/CatapultCameraRig.cs Assets/Scripts/Camera/CatapultCameraRig.cs.meta
```
Proposed message: `feat(camera): add CatapultCameraRig with anchor glide (straight or Catmull-Rom path)`

### Task 2.6: Aim follow — the soft "lagging" camera (extends `CatapultCameraRig`)

**Files:**
- Modify: `Assets/Scripts/Camera/CatapultCameraRig.cs`

**Interfaces:**
- Consumes: `Catapult.RestPosition/RestRotation` (Task 2.2), `_state`/`_followTarget` from Task 2.5.
- Produces: while `_state == Following`, the rig turns after the active catapult's yaw with a tunable lag. Nothing else consumes this — it is pure feel.

**The behaviour.** While the player aims, the camera should slowly turn with the catapult — not
rigidly (that is what the old parent-child hierarchy did accidentally), but **slower**, so the
catapult visibly leads and the camera settles in behind it.

- [ ] **Step 1: Add the follow block** — a `LateUpdate` to `CatapultCameraRig`:

```csharp
private void LateUpdate()
{
    if (_state != RigState.Following || _followTarget == null || _followTarget.cameraAnchor == null)
        return;

    CameraAnchor anchor = _followTarget.cameraAnchor;

    // 1. "How far has the body rotated away from its rest pose", in world space.
    Quaternion bodyDelta = _followTarget.transform.rotation * Quaternion.Inverse(_followTarget.RestRotation);

    // 2. Where the anchor would be if it were welded to that rotation.
    Vector3 anchorRestLocal = anchor.transform.localPosition;
    Vector3 anchorRestWorld = _followTarget.RestPosition + _followTarget.RestRotation * anchorRestLocal;
    Vector3 targetPos = _followTarget.RestPosition + bodyDelta * (anchorRestWorld - _followTarget.RestPosition);
    Quaternion targetRot = bodyDelta * _followTarget.RestRotation * anchor.transform.localRotation;

    // 3. Move toward it at a RATE, not a ratio — frame-rate independent.
    float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
    transform.position = Vector3.Lerp(transform.position, targetPos, k);

    // 4. Kill the drift once it is imperceptible.
    if (Quaternion.Angle(transform.rotation, targetRot) < 0.05f)
    {
        transform.SetPositionAndRotation(targetPos, targetRot);
    }
}
```

**Line-by-line notes — this is the main tuning block of the whole feature:**

*`bodyDelta` — "how far has it rotated since rest".* You compute the offset between two rotations
by multiplying one by the *inverse* of the other: `current * Inverse(rest)`. The result is a
rotation you can apply to anything authored in the rest pose, and it lands where it would be if it
had been welded to the catapult all along — "subtraction" for rotations, but valid in 3D without
worrying about axes or wrap. *Why not just read the yaw and rebuild it?* Because
`RotateCatapultBody` writes a **world** rotation (`transform.rotation = Quaternion.Euler(...)`), so
"the yaw" is not a single clean number; `bodyDelta` is derived from the transforms themselves and
stays correct no matter how the launcher rotates the body. Fewer assumptions, less to break.

*Why `anchorRestWorld - RestPosition`.* A rotation about the catapult's origin does not move the
origin — it moves everything *around* it. To reproduce that for the camera you rotate the
**offset** from the origin, then add the origin back. Rotating the absolute world position instead
would orbit the camera around the *world* origin — vaguely right when the catapult sits at
`(0,0,0)`, completely wrong the moment it does not.

*Why "rate, not ratio".* The naive version is
`transform.rotation = Quaternion.Slerp(current, target, 0.1f)` — but that `0.1` is *per frame*, so
at 120 fps the camera catches up twice as fast as at 60 fps and the game feels different on every
device. `1 - exp(-sharpness * dt)` is the frame-rate-independent version: the fraction covered
depends on **elapsed time**, not frame count. If you remember one line from this plan, remember
this one. (Same fixed-timestep thinking as `CameraShakeManager`'s spring integration.)

*Why `LateUpdate` and not `Update`.* Unity runs all `Update`s first, then all `LateUpdate`s.
`InputController` rotates the catapult in `Update`; if the camera also followed in `Update`, the
relative order would be undefined and the camera would lag a random extra frame (visible as
jitter). `LateUpdate` guarantees the camera reads the catapult's *final* pose for this frame — the
same reason `CameraShakeManager` works in `LateUpdate`.

*Why the position moves too, not just the rotation.* The anchor sits offset from the catapult's
pivot (about 3.6 units to the side). Rotating the rig in place would orbit the camera around its
own position, not around the catapult — the view would swing sideways in a way that reads wrong at
close range. Moving both with the same fraction `k` keeps the anchor "attached" to the yawed body
while still lagging.

*Why this coexists with `CameraShakeManager`.* The shake shakes the camera in **local** space and
re-records its rest pose every frame while settled. The camera is a child of the rig at local
`(0,0,0)`, so its local rest pose is constant no matter where the rig goes — the two systems do not
fight. (Verified against `CameraShakeManager.RecordRest`/`Apply`.)

*Why it must not fight a switch.* While a `MoveTo` coroutine runs, `_state == Moving` and this
whole block is skipped — otherwise both would write `transform.position` every frame. That is the
state enum from Task 2.5 paying for itself.

- [ ] **Step 2: Stage & propose commit**

```bash
git add Assets/Scripts/Camera/CatapultCameraRig.cs
```
Proposed message: `feat(camera): add frame-rate-independent soft aim follow to camera rig`

---

## Phase 3 — Refactors to the existing scripts

These tasks change existing files. Grep was done for every deleted member: `DecreaseCurrentAmmo`
has exactly one caller (`Projectile.Shoot`), `GetCurrentAmmoCount` has zero — both are safe to
delete. Nothing else in the project reads `AmmoManager.levelData`.

### Task 3.1: `AmmoManager` — from pool owner to pool holder

**Files:**
- Modify: `Assets/Scripts/Projectile/AmmoManager.cs` (rewrite — the file shrinks)

**Interfaces:**
- Consumes: `Core.AmmoPool<SO_ProjectileType>` (Task 1.2 contract: `Has` = count > 0, `Get`/`Consume` never throw).
- Produces: `void AttachPool(AmmoPool<SO_ProjectileType>)`, `bool HasAmmo(SO_ProjectileType)`, `int GetRemaining(SO_ProjectileType)`, `int Consume(SO_ProjectileType)`, `event Action<SO_ProjectileType,int> OnAmmoChanged`. `Projectile` (3.2), `ProjectileSpawner` (3.3), `CatapultManager` (2.3) and `ProjectileCarouselUI` (3.6) consume these.

**The bug being removed.** Today `AmmoManager.Awake()` *re-initialises the shared
`ProjectileSelector`*. Two catapults = two `AmmoManager`s = two components fighting over one list
and one selected index — whoever's `Awake` runs last wins, and the other's loadout silently
disappears. After this task, `AmmoManager` holds a pool it is *given* and answers questions about
it; it never builds type lists and never touches the selection.

- [ ] **Step 1: Replace the whole file with:**

```csharp
using System;
using Core;
using UnityEngine;

// Holds a REFERENCE to the level's ammo pool (built by CatapultManager) and
// re-raises its changes for the UI. Deliberately cannot create or select types —
// that is CatapultManager/ProjectileSelector's job.
public class AmmoManager : MonoBehaviour
{
    public ProjectileSelector projectileSelector;   // read-only use: answer "is the selected type available?"

    public event Action<SO_ProjectileType, int> OnAmmoChanged;

    private AmmoPool<SO_ProjectileType> _pool;

    public void AttachPool(AmmoPool<SO_ProjectileType> pool)
    {
        if (_pool != null) _pool.OnChanged -= HandlePoolChanged;   // re-attach must not double-subscribe
        _pool = pool;
        _pool.OnChanged += HandlePoolChanged;
    }

    // Re-raise in the SAME signature the UI already knows, so the carousel needs no changes.
    private void HandlePoolChanged(SO_ProjectileType type, int remaining)
    {
        OnAmmoChanged?.Invoke(type, remaining);
    }

    public bool HasAmmo(SO_ProjectileType type)
    {
        return type && _pool != null && _pool.Has(type);
    }

    public int GetRemaining(SO_ProjectileType type)
    {
        return type && _pool != null ? _pool.Get(type) : 0;
    }

    public int Consume(SO_ProjectileType type)
    {
        return _pool != null ? _pool.Consume(type) : 0;
    }
}
```

**What was deleted, and why it is safe:**
* `Awake()` + `InitializeAmmo()` — the type list now lives in `CatapultManager.BuildTypeList` (Task 2.3), the ONE place it is built.
* `levelData` field — one owner (`CatapultManager`), one source of truth. Two components reading the same `SO_LevelData` and each deciding what it means is exactly how the old bug happened.
* `DecreaseCurrentAmmo()` / `GetCurrentAmmoCount()` — they encoded the "wrong type" bug: they looked up `projectileSelector.SelectedType` *at the moment of the call*, so switching type between loading and firing consumed ammo from the wrong pool. The new `Consume(type)` takes the type explicitly (Task 3.2 passes it), removing the timing dependency entirely.
* `_ammo` dictionary — replaced by the pool. Note the old `DecreaseAmmo` did `--_ammo[type]`, which threw `KeyNotFoundException` for an unknown type and went negative at 0; the pool (Task 1.2) does neither.

**Teaching note — why re-raise instead of exposing the pool's event directly.** The UI talks to
`AmmoManager` and knows nothing about `AmmoPool`. Forwarding the *same* event signature it already
uses keeps the carousel's subscription code unchanged — `AmmoManager` stays the facade over the
pool, the same way `Catapult` is the facade over launcher/spawner/ammo.

**Teaching note — the re-attach guard.** In PerCatapult mode a manager is attached exactly once,
but defensive `-= before +=` costs one line and makes `AttachPool` idempotent — calling it twice
would otherwise double every future event.

- [ ] **Step 2: This breaks the build** (`Projectile.Shoot` calls the deleted `DecreaseCurrentAmmo`, `DelaySpawn` still compiles). That is expected — Tasks 3.2–3.4 fix the callers. Do **not** commit yet; commit after Task 3.4.

### Task 3.2: `Projectile` — carry its own type

**Files:**
- Modify: `Assets/Scripts/Projectile/Projectile.cs` (three lines)

**Interfaces:**
- Produces: `public SO_ProjectileType projectileType;` on `Projectile` — set by the spawner (3.3), read by the launcher's auto-switch (3.4). `Shoot(force)` signature unchanged.

- [ ] **Step 1: Add the field** next to the other serialized fields:

```csharp
public class Projectile : MonoBehaviour
{
    public float baseForcePower = 1500;
    public float delayBeforeDestroy = 0.5f;
    public float shakeIntensity = 1f;
    public AmmoManager ammoManager;
    public SO_ProjectileType projectileType;   // NEW — set by ProjectileSpawner at instantiation
    // ...
```

- [ ] **Step 2: Change `Shoot` to consume the carried type** — replace the last block:

```csharp
        if (ammoManager)
        {
            ammoManager.Consume(projectileType);   // was: DecreaseCurrentAmmo() — the "wrong type" bug
        }
```

*Why this fixes a real bug:* `DecreaseCurrentAmmo()` read `projectileSelector.SelectedType` at
shot-time. Load Boulder, switch the carousel to Explosive mid-aim, release → the shot fires a
Boulder but subtracts an Explosive. Now the projectile remembers what it *is*.

### Task 3.3: `ProjectileSpawner` — stop throwing the type away

**Files:**
- Modify: `Assets/Scripts/Projectile/ProjectileSpawner.cs` (rewrite — the file shrinks)

**Interfaces:**
- Consumes: `Projectile.projectileType` (3.2).
- Produces: `Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)` — same signature as today. `null` return now means "this catapult has no ammo for the requested type" and **never** changes the global selection.

**The behaviour being removed.** Today `InstantiateBullet`'s no-ammo branch calls
`projectileSelector.SelectNextWithAmmo(...)`. In PerCatapult mode that is dangerous: every
catapult's launcher subscribes to the global `OnSelectionChanged` and pre-loads; catapult B might
have 0 Boulder while A has 5. When the player selects Boulder, B's launcher calls
`InstantiateBulletSafe(Boulder)`, hits the no-ammo branch, and *changes the global selection out
from under the player, who is aiming with A*. With the fallback removed, B simply loads nothing and
waits; Task 3.4's reload-on-activate handles it. The auto-switch moves to the *active* launcher
(3.4), considering only the pool the player actually shoots from.

- [ ] **Step 1: Replace the whole file with:**

```csharp
using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public AmmoManager ammoManager;

    private Projectile _currentProjectile;
    private bool _isSwitching;

    private Projectile InstantiateBullet(SO_ProjectileType type)
    {
        // No global auto-switch here — an INACTIVE catapult must never change the
        // global selection. The active launcher auto-switches after a shot (3.4).
        if (!type || !type.prefab || !ammoManager.HasAmmo(type))
        {
            return null;
        }

        Projectile currentProjectile = Instantiate(type.prefab, transform.position, transform.rotation, transform);
        currentProjectile.ammoManager = ammoManager;
        currentProjectile.projectileType = type;   // the projectile remembers what it is (3.2)

        return currentProjectile;
    }

    public Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)
    {
        // Guard against re-entrant calls: selection events can re-enter synchronously.
        if (_isSwitching)
        {
            return _currentProjectile;
        }

        _isSwitching = true;

        // Replace the loaded projectile only if it hasn't been launched yet.
        // Projectile.Shoot() unparents the instance, so launched projectiles are no
        // longer children of this spawner and must not be destroyed here.
        if (_currentProjectile && _currentProjectile.transform.parent == transform)
        {
            Destroy(_currentProjectile.gameObject);
        }

        _currentProjectile = InstantiateBullet(pType ?? projectileSelector.SelectedType);

        _isSwitching = false;

        return _currentProjectile;
    }
}
```

*Differences from the current file:* `InstantiateBullet` now **requires** a type (the
`pType ?? SelectedType` default moved up into `InstantiateBulletSafe`, one call site instead of
two); the `SelectNextWithAmmo` fallback is gone; `currentProjectile.projectileType = type` is new.
The `_isSwitching` re-entrancy guard and the parent-check destroy are kept exactly as they are —
they already handle the subtle cases.

- [ ] **Step 2: Do not commit yet** — `ProjectileLuncher` still compiles, but its `DelaySpawn` calls `InstantiateBulletSafe()` with no argument, which now means "whatever the selector points at now" (fine), and the auto-switch is still missing. Task 3.4 completes the refactor.

### Task 3.4: `ProjectileLuncher` — load on demand, auto-switch after a shot

**Files:**
- Modify: `Assets/Scripts/Projectile/ProjectileLuncher.cs`

**Interfaces:**
- Consumes: `ProjectileSpawner.InstantiateBulletSafe(type)` (3.3), `Projectile.projectileType` (3.2), `AmmoManager.HasAmmo` (3.1, via `projectileSpawner.ammoManager`), `ProjectileSelector.SelectNextWithAmmo(Func<SO_ProjectileType,bool>)` (existing).
- Produces: `public void LoadProjectile()` — called by `CatapultManager.SetActive` (2.3), by `Start`, by `HandleSelectionChanged`, and by `DelaySpawn`.

- [ ] **Step 1: Add `LoadProjectile` and route all loading through it.** Add after `Start()`:

```csharp
    // The ONE place a projectile gets loaded. Every caller goes through here so the
    // guards can never disagree. CatapultManager.SetActive calls this on the newly
    // active catapult — in PerCatapult mode an inactive catapult may have had no ammo
    // for the selected type and is holding nothing.
    public void LoadProjectile()
    {
        if (_isOnDelay || _isCatapultMoving) return;
        _activeProjectile = projectileSpawner.InstantiateBulletSafe(projectileSelector.SelectedType);
    }
```

Then simplify the two existing load sites to call it:

```csharp
    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        LoadProjectile();   // was: duplicate guard + InstantiateBulletSafe(type)
    }
```

```csharp
    IEnumerator DelaySpawn(float delayTime)
    {
        _isOnDelay = true;
        yield return new WaitForSeconds(delayTime);
        _isOnDelay = false;
        LoadProjectile();   // was: InstantiateBulletSafe() with no argument
    }
```

And in `Start()`, add one line after the rest-pose capture:

```csharp
    void Start()
    {
        if (catapultHead)
        {
            _initiateCatapultHeadRotation = catapultHead.transform.localRotation;
            _initiateCatapultBodyRotation = transform.localRotation;
        }

        LoadProjectile();   // NEW — see "lifecycle-ordering trap" rule 3: events are for changes, not initial state
    }
```

*Teaching note — why the guard is `if (_isOnDelay || _isCatapultMoving) return`.* The same guard
the code already used in `HandleSelectionChanged`, kept in ONE place: `DelaySpawn` re-spawns after
the cooldown, and if a drag is in progress at that moment the new projectile must not replace the
one about to be fired. Three call sites, one guard — they can never disagree.

*Teaching note — why not rely on the event alone.* `selector.Initialize(types)` raises
`OnSelectionChanged`, but the launcher subscribes in `OnEnable`, and `OnEnable` of the launcher may
run before `Awake` of the manager (relative order undefined). `Start` is guaranteed to run after
*every* `Awake` — so the explicit `LoadProjectile()` in `Start` removes the ordering dependency.
(The known wrinkle: the very first projectile may be built twice — once by the event, once by
`Start` — and `InstantiateBulletSafe` destroys the first. Correct, one wasted `Instantiate`.)

- [ ] **Step 2: Auto-switch after the shot.** In `ResetCatapultHeadRotation`, replace the shot block:

```csharp
    public void ResetCatapultHeadRotation()
    {
        _isCatapultMoving = false;
        if (_activeProjectile)
        {
            SO_ProjectileType firedType = _activeProjectile.projectileType;   // NEW — remember before Shoot

            float mag = 1 - (Mathf.Clamp(catapultHead.transform.localRotation.eulerAngles.magnitude, 0, 60) / 60);
            if (mag > 0.1f)
            {
                if (luncherReleaseAudio && audioConfig)
                {
                    audioConfig.Play(luncherReleaseAudio, mag);
                }

                _activeProjectile.Shoot(mag);
                StartCoroutine(DelaySpawn(cooldown));

                // GDD §5.2: after the shot that empties a type, switch to the next type
                // with ammo IN THIS CATAPULT'S POOL (the predicate decides whose ammo counts).
                AmmoManager ammo = projectileSpawner ? projectileSpawner.ammoManager : null;
                if (ammo && !ammo.HasAmmo(firedType))
                {
                    projectileSelector.SelectNextWithAmmo(ammo.HasAmmo);
                }
            }
        }

        if (_headResetRoutine != null)
        {
            StopCoroutine(_headResetRoutine);
        }
        _headResetRoutine = StartCoroutine(ResetHeadRotationRoutine());
    }
```

*Why read `projectileType` before `Shoot`:* `Shoot` destroys the projectile after
`delayBeforeDestroy`, but the field is read here, immediately — either order works; reading before
is simply clearer.

*Why `ammo.HasAmmo` after `Shoot`:* `Projectile.Shoot` consumes the ammo *synchronously* before
returning, so the check after it sees the post-shot count. If the pool is exhausted for that type,
`SelectNextWithAmmo(predicate)` walks the ring and selects the next type with ammo — in *this*
catapult's pool, because `ammo` resolves through this catapult's own spawner.

*Teaching note — `SelectNextWithAmmo` takes a predicate, not a list.* That is what makes it usable
with per-catapult pools: the caller decides **whose** ammo counts. Passing `ammo.HasAmmo` (a
*method group* — a method name used as a `Func<...,bool>` value) binds it to this catapult's pool.
This preserved behaviour is documented in GDD §5.2.

*Why via `projectileSpawner.ammoManager` and not a new field:* the launcher already has the
spawner, the spawner already has the manager. One more serialized field would be one more slot to
wire (and forget) in Phase 5.

- [ ] **Step 3: Compile check + manual smoke.** The project must compile. Press Play with the
  *current* (single-catapult) scene: aiming, firing, cooldown reload, and auto-switch-on-empty must
  all still behave exactly as before (the refactor preserves behaviour; the multi-catapult wiring
  comes in Phase 5).

- [ ] **Step 4: Stage & propose commit** (Tasks 3.1–3.4 together)

```bash
git add Assets/Scripts/Projectile/AmmoManager.cs Assets/Scripts/Projectile/Projectile.cs Assets/Scripts/Projectile/ProjectileSpawner.cs Assets/Scripts/Projectile/ProjectileLuncher.cs
```
Proposed message: `refactor(projectile): explicit ammo pool + per-projectile type, remove selector re-init`

### Task 3.5: `InputController` — drive the *active* catapult, support the mouse

**Files:**
- Modify: `Assets/Scripts/InputController.cs` (rewrite — shrinks)
- Scene change (Phase 5): the component moves from the catapult root to the `CatapultManager` GameObject.

**Interfaces:**
- Consumes: `CatapultManager.ActiveCatapult` + `Catapult.launcher` (2.2/2.3), `ProjectileLuncher.RotateCatapultHead/Body/ResetCatapultHeadRotation` (existing).
- Produces: serialized `CatapultManager catapultManager` field (wired in Phase 5). Behaviour: drag anywhere that is not UI aims the active catapult; touch or mouse.

**Teaching note — serialized reference vs `GetComponent`.** `GetComponent<ProjectileLuncher>()`
searches *this* object. Once the launcher lives on the catapult's head (a child of a different
root), that breaks. A serialized field is explicit — you drag the manager onto the slot and can
see at a glance what it is wired to.

- [ ] **Step 1: Replace the whole file with:**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

// Aims the ACTIVE catapult (CatapultManager.ActiveCatapult). Reads touch OR mouse
// through one pointer path; ignores presses that land on uGUI (arrows, carousel).
public class InputController : MonoBehaviour
{
    public CatapultManager catapultManager;   // wired in the Inspector (Phase 5)

    public float touchSensitivityMinY = 1f;
    public float touchSensitivityMaxY = 4f;

    public float touchSensitivityX = 50;

    private float _touchSensitivityY = 1;

    private Vector2 _startPos;
    private bool _dragging;

    private void Update()
    {
        // A switch mid-drag drops the drag: the new active catapult must not inherit
        // the old drag's rotation deltas.
        if (catapultManager && catapultManager.ActiveCatapult == null)
        {
            _dragging = false;
            return;
        }

        if (!TryGetPointer(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame))
        {
            return;
        }

        // Ignore presses that land on a UI element (the arrows, the carousel, ...).
        // Legacy Input and uGUI never talk to each other — uGUI "consuming" a click
        // does NOT stop Input.GetMouseButton from reporting it. We must ask ourselves.
        if (pressedThisFrame && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (pressedThisFrame)
        {
            _startPos = position;
            _dragging = true;
            return;
        }

        if (releasedThisFrame)
        {
            if (_dragging)
            {
                _dragging = false;
                Launch(catapultManager.ActiveCatapult.launcher);
            }
            return;
        }

        if (_dragging)
        {
            Aim(position);
        }
    }

    // One pointer path: touch wins when a finger is down, otherwise the mouse.
    private static bool TryGetPointer(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            pressedThisFrame = touch.phase == TouchPhase.Began;
            releasedThisFrame = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        position = Input.mousePosition;
        pressedThisFrame = Input.GetMouseButtonDown(0);
        releasedThisFrame = Input.GetMouseButtonUp(0);
        bool held = Input.GetMouseButton(0);
        return held || releasedThisFrame;
    }

    private void Aim(Vector2 position)
    {
        ProjectileLuncher launcher = catapultManager.ActiveCatapult.launcher;
        if (!launcher) return;

        float windowWidth = Screen.width;
        float windowHeight = Screen.height;

        Vector2 dragPos = position - _startPos;

        // Sensitivity maths kept exactly as it was — only the pointer source changed.
        dragPos.x = (dragPos.x / windowWidth) * touchSensitivityX;
        _touchSensitivityY = Mathf.Lerp(touchSensitivityMaxY, touchSensitivityMinY, dragPos.y / windowHeight);
        dragPos.y = Mathf.Clamp(((dragPos.y / windowHeight) * _touchSensitivityY * 60) + 60, 0, 60);

        launcher.RotateCatapultHead(dragPos.y);
        launcher.RotateCatapultBody(dragPos.x);
    }

    private static void Launch(ProjectileLuncher launcher)
    {
        if (launcher) launcher.ResetCatapultHeadRotation();
    }
}
```

**What changed, line by line:**

*`catapultManager.ActiveCatapult.launcher` replaces `GetComponent<ProjectileLuncher>()`* — the
input handler no longer knows *which* catapult it drives; it asks the manager every frame. (Also
why the component must move off the catapult root in Phase 5: duplicating a catapult would
duplicate its `InputController` and give you N handlers all aiming their own catapult.)

*`TryGetPointer` unifies touch and mouse.* Touch branch: when any finger is down, its
`TouchPhase` gives the press/release edges. Mouse branch: `GetMouseButton(0)` for "held",
`GetMouseButtonDown/Up(0)` for the edges. `Input.mousePosition` needs no phase — the *held* flag
is the state. *Teaching note:* the legacy `Input` API still works because
`activeInputHandler: 2` ("Both") in `ProjectSettings`. The "modern" equivalent
(`Mouse.current.leftButton`, `EnhancedTouch`) is a separate migration task — the goal here is only
that the mouse works so you can test in the Editor.

*`_dragging` + explicit `_startPos` replace the `TouchPhase` switch.* The old code relied on touch
phases carrying the state; a mouse has no phases, so the state now lives in one bool. Behaviour
matches: press = remember start, hold-and-move = aim, release = fire.

*`IsPointerOverGameObject` guard is not optional.* Without it, tapping the right arrow would
*also* start an aim-drag on the catapult you are switching away from, and tapping a carousel
button would nudge the aim. **Teaching note — why the UI "consuming" the click does not protect
you:** uGUI's `EventSystem` + `GraphicRaycaster` notice a click on a `Button` and run its
`onClick`, but legacy `Input` is a separate, lower-level system reporting every raw press. The two
never talk; you must ask "is the pointer over UI?" yourself and skip the drag. The guard also
dodges the frame-ordering problem: whether the button's `onClick` or your `Update` runs first in a
frame is undefined, but a press that started on UI is ignored either way.

*Deleted as dead code:* `_jumpBtn`, `GetAxisRaw("Jump")`, `GetJumpBtn()` — grep confirmed nothing
reads them. Also deleted: the commented-out `ResetCatapultBodyRotation` call.

*Edge case — switch mid-drag:* if `ActiveCatapult` becomes null or changes, the drag is dropped
(the early return resets `_dragging`; the released branch only fires when a drag was in progress
on *this* handler). Task 4.3's UI + this = clean rapid switching.

- [ ] **Step 2: Verify (single catapult, current scene).** Play: drag with the mouse → the
  catapult aims; release → fires; drag starting on a carousel button → no aim. No exceptions.

- [ ] **Step 3: Stage & propose commit**

```bash
git add Assets/Scripts/InputController.cs
```
Proposed message: `feat(input): unified mouse/touch pointer driving the active catapult, ignore UI presses`

### Task 3.6: `ProjectileCarouselUI` — follow the active catapult's pool

**Files:**
- Modify: `Assets/Scripts/UI/ProjectileCarouselUI.cs`

**Interfaces:**
- Consumes: `CatapultManager.OnActiveCatapultChanged` + `Catapult.ammoManager` (2.2/2.3).
- Produces: serialized `CatapultManager catapultManager` field (wired in Phase 5). Also fixes the name-based type lookup with a dictionary.

**The part that is easy to miss.** Today `OnEnable` does
`ammoManager.OnAmmoChanged += HandleAmmoChanged;` — subscribed to *one* `AmmoManager` forever.
When the player switches catapults, the carousel must **unsubscribe from the old and subscribe to
the new**, or it keeps showing catapult A's counts and never hears about catapult B's shots.

**Teaching note — why the subscription must be moved, not just refreshed.** `OnAmmoChanged` is a
C# `event` — a list of callbacks. Adding a second subscription without removing the first makes
the callback fire twice per change (the classic leak), and the old `AmmoManager` keeps the carousel
alive in memory after scene teardown. That is why every `+=` in this project is paired with a
`-=`.

- [ ] **Step 1: Add the fields and the ammo-source property.** In the `[Header("Wiring")]` block:

```csharp
    [Header("Wiring")]
    public ProjectileSelector selector;
    public AmmoManager ammoManager;             // fallback when no manager is assigned
    public CatapultManager catapultManager;     // NEW — wired in Phase 5
    public RectTransform contentRoot;
    public GameObject buttonPrefab;
```

And add the property + a new event handler next to the other handlers:

```csharp
    // The ammo source is the ACTIVE catapult's manager; fall back to the serialized one
    // so the component stays usable in a test scene without a CatapultManager.
    private AmmoManager ActiveAmmo => catapultManager && catapultManager.ActiveCatapult
        ? catapultManager.ActiveCatapult.ammoManager
        : ammoManager;
```

*Teaching note — expression-bodied property.* `=> expr;` on a property is shorthand for a
getter that returns `expr`. It re-evaluates on every access, which is what we want: "who owns the
ammo *right now*".

- [ ] **Step 2: Route every `ammoManager` use through `ActiveAmmo` and re-subscribe on switch.**
  Replace `OnEnable`/`OnDisable` and the two handlers:

```csharp
    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
        if (catapultManager) catapultManager.OnActiveCatapultChanged += HandleActiveCatapultChanged;
        SubscribeAmmo();
        UpdateVisibleSideCount();
        Rebuild(false);
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
        if (catapultManager) catapultManager.OnActiveCatapultChanged -= HandleActiveCatapultChanged;
        UnsubscribeAmmo();
    }

    private void SubscribeAmmo()
    {
        AmmoManager ammo = ActiveAmmo;
        if (ammo) ammo.OnAmmoChanged += HandleAmmoChanged;
    }

    private void UnsubscribeAmmo()
    {
        AmmoManager ammo = ActiveAmmo;
        if (ammo) ammo.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void HandleActiveCatapultChanged(Catapult catapult)
    {
        // Move the ammo subscription to the new catapult's pool, then refresh the
        // NUMBERS only — a full Rebuild would make the row animate for no reason.
        UnsubscribeAmmo();
        SubscribeAmmo();
        RefreshAmmoDisplays();
    }

    private void HandleSelectionChanged(SO_ProjectileType type) => Rebuild(true);

    private void HandleAmmoChanged(SO_ProjectileType type, int remaining) => RefreshAmmoDisplays();
```

*Why `RefreshAmmoDisplays` and not `Rebuild(true)`:* only the ammo numbers and `interactable`
flags change on a switch — the buttons themselves are the same types. `RefreshAmmoDisplays`
already updates `interactable` (existing line, `uiButton.interactable = ammoManager.HasAmmo(type)`).

- [ ] **Step 3: Replace the remaining `ammoManager` references** inside `Rebuild` with
  `ActiveAmmo` — three places: the `if (ammoText && ammoManager) ammoText.text = …` guard, the
  `if (ammoManager) uiButton.interactable = …` guard (both rewritten in Step 4), and any direct
  `ammoManager.GetRemaining(type)` call. Mechanical rename — same calls, different source. Leave
  the subscription sites as written in Step 2 (they resolve `ActiveAmmo` themselves).

- [ ] **Step 4: Replace the name round-trip with a dictionary.** Today `FindTypeForButton` re-parses
  the GameObject name (`"ProjectileButton_" + type.displayName`) — which breaks if two types share a
  `displayName`. Add a field:

```csharp
    private readonly Dictionary<Button, SO_ProjectileType> _buttonTypes = new Dictionary<Button, SO_ProjectileType>();
```

Fill it in `Rebuild` where the button is configured:

```csharp
            Button uiButton = button.GetComponent<Button>();
            if (uiButton)
            {
                _buttonTypes[uiButton] = type;   // NEW — replaces the name round-trip
                uiButton.onClick.AddListener(() => selector.SelectIndex(typeIndex));
                AmmoManager ammo = ActiveAmmo;
                if (ammo) uiButton.interactable = ammo.HasAmmo(type);
            }
```

Clear it at the top of `Rebuild` (next to `_buttons.Clear()`):

```csharp
        _buttons.Clear();
        _buttonTypes.Clear();
```

Then simplify the two lookups:

```csharp
    private void CapturePreviousSlotPositions()
    {
        _previousSlotX.Clear();
        foreach (GameObject button in _buttons)
        {
            if (!button.TryGetComponent(out Button uiButton)) continue;
            if (!_buttonTypes.TryGetValue(uiButton, out SO_ProjectileType type) || !type) continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect) _previousSlotX[type] = rect.anchoredPosition.x;
        }
    }
```

```csharp
    private void RefreshAmmoDisplays()
    {
        AmmoManager ammo = ActiveAmmo;
        if (!ammo || !selector) return;
        foreach (GameObject button in _buttons)
        {
            if (!button.TryGetComponent(out Button uiButton)) continue;
            if (!_buttonTypes.TryGetValue(uiButton, out SO_ProjectileType type) || !type) continue;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText) ammoText.text = ammo.GetRemaining(type).ToString();

            uiButton.interactable = ammo.HasAmmo(type);
        }
    }
```

And delete `FindTypeForButton` entirely. Keyed by `Button`, not `GameObject`, so the refresh is one
dictionary lookup per button.

*Teaching note — `TryGetComponent`.* `GetComponent` + null-check is the old idiom; `TryGetComponent(out var x)` does both in one expression and reads better in guards.

- [ ] **Step 5: Verify (single catapult).** Play: carousel still renders, switching types still
  animates, firing decrements the numbers, emptying a type greys the button. (PerCatapult behaviour
  is verified in Phase 5's manual checklist.)

- [ ] **Step 6: Stage & propose commit**

```bash
git add Assets/Scripts/UI/ProjectileCarouselUI.cs
```
Proposed message: `refactor(ui): carousel follows active catapult's ammo pool, dictionary-based button types`

---

## Phase 4 — UI: the arrows

### Task 4.1: The arrow button prefab

**Files:**
- Create (in Editor): `Assets/Prefabs/UI/CatapultArrowButton.prefab`

**Do not reuse `ProjectileTypeButton.prefab`** — it is a 400×400 icon/name/ammo button; the arrows
need to be small and plain.

**Use `<` / `>` — not `‹` / `›` and not `◀` / `▶`.** (The old draft of this plan had `‹`/`›` in one
diagram and `<`/`>` in the rule — `<`/`>` is the rule.) The project's TMP font asset
`LiberationSans SDF` contains ASCII `<` (0x3C) and `>` (0x3E) but **not** the typographic
`‹`/`›` (0x2039/0x203A) or the block arrows `◀`/`▶` (0x25C0/0x25B6). A glyph the font lacks renders
as a hollow box. (If you want a real arrow instead: re-import a Kenney arrow PNG as **Sprite
(2D and UI)** — the current Kenney PNGs are imported as plain textures and will not drop into a
uGUI `Image` slot without re-importing.)

- [ ] **Step 1:** `GameObject ▸ UI ▸ Button (TextMeshPro)` (it may ask to import TMP essentials — the project already has them, so this should not appear). Name it `CatapultArrowButton`. Size **96×96** (width/height in the `RectTransform`).
- [ ] **Step 2:** Leave `Image.Source Image` empty — a uGUI `Button` with no sprite draws the built-in white rectangle; give it a white-ish tint.
- [ ] **Step 3:** Set the TMP child's text to `<`, font size ~48, alignment center/middle.
- [ ] **Step 4:** Drag the object into `Assets/Prefabs/UI/` to create the prefab, then delete the scene instance (Task 4.2 re-creates instances under the switcher).
- [ ] **Step 5:** No script in this task — `CatapultSwitcherUI` (Task 4.3) wires the buttons at the scene level.

### Task 4.2: The `CatapultSwitcher` scene object (layout)

**Files:**
- Scene change (do it in Phase 5 alongside the rest of the wiring — listed here because the layout decisions belong to this phase)

```
UI (Canvas)
├── ScoreLable, Score              (existing, top)
├── ProjectileButtons              (existing, bottom strip: bottom-anchored, 400 tall)
└── CatapultSwitcher                (new — full-canvas container)
    ├── LeftArrow    (Button + TMP "<")
    ├── RightArrow   (Button + TMP ">")
    └── Counter      (TMP "1 / 2")
```

**Verified facts about the existing canvas** (from `SampleScene.unity`):

* `CanvasScaler`: `m_UiScaleMode: 0` (**Constant Pixel Size**), reference resolution 800×600 →
  **1 canvas unit = 1 real pixel**, and the UI does not scale with the screen. Keep the arrows
  anchored to the edges so they work at any size.
* `ProjectileButtons` occupies the band **400–800 units up from the bottom** (anchor min `(0,0)`,
  max `(1,0)`, sizeDelta `(0,400)`, anchoredPosition `(0,400)`).

**Therefore the arrows belong at the left/right edges at mid-height** — clear of the score row
(top) and the projectile strip (bottom band):

| Object | Anchor min=max | Pivot | Anchored pos | Size |
|---|---|---|---|---|
| `CatapultSwitcher` | stretched `(0,0)→(1,1)` | `(0.5,0.5)` | `(0,0)`, sizeDelta `(0,0)` | — |
| `LeftArrow` | `(0, 0.5)` | `(0, 0.5)` | `(24, 0)` | 96×96 |
| `RightArrow` | `(1, 0.5)` | `(1, 0.5)` | `(-24, 0)` | 96×96 |
| `Counter` | `(0, 0)` | `(0, 0)` | `(24, 24)` | auto |

(The `Counter` sits bottom-left, below the projectile strip, which starts 400 units up.)

**Teaching note — anchors vs. positions.** An *anchor* is a normalised point on the parent
(`0,0` = bottom-left, `1,1` = top-right); the *anchored position* is the offset from it. "Anchor
`(1, 0.5)`, anchored position `(-24, 0)`" = "24 units left of the right edge, half way up" — and it
*stays there at any screen size*, because the anchor is a proportion while the offset is constant.
Anchor to edges; fix positions only for truly centred things.

- [ ] Do the actual object creation in Phase 5, Step 5 — but read this table there.

### Task 4.3: `CatapultSwitcherUI` — the script

**Files:**
- Create: `Assets/Scripts/UI/CatapultSwitcherUI.cs`

**Interfaces:**
- Consumes: `CatapultManager.NextCatapult/PreviousCatapult/OnActiveCatapultChanged/Catapults/ActiveCatapult` (Task 2.3).
- Produces: nothing — terminal UI consumer.

**Teaching note — `Button.onClick` vs `EventTrigger`.** `onClick` is the simplest way to react to
a tap; `EventTrigger` is the general-purpose alternative for other gestures. Two plain buttons →
`onClick`.

- [ ] **Step 1: Create the script**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Left/right arrows + "current / total" counter. Wrap-around means the arrows are
// never disabled at the ends — only when there is exactly one catapult.
public class CatapultSwitcherUI : MonoBehaviour
{
    public CatapultManager manager;
    public Button leftButton;
    public Button rightButton;
    public TMP_Text counterText;

    private void OnEnable()
    {
        if (manager) manager.OnActiveCatapultChanged += HandleActiveCatapultChanged;
        if (leftButton) leftButton.onClick.AddListener(manager.PreviousCatapult);
        if (rightButton) rightButton.onClick.AddListener(manager.NextCatapult);
        Refresh();
    }

    private void OnDisable()
    {
        if (manager) manager.OnActiveCatapultChanged -= HandleActiveCatapultChanged;
        if (leftButton) leftButton.onClick.RemoveListener(manager.PreviousCatapult);
        if (rightButton) rightButton.onClick.RemoveListener(manager.NextCatapult);
    }

    private void HandleActiveCatapultChanged(Catapult catapult) => Refresh();

    private void Refresh()
    {
        if (!manager || counterText == null) return;

        int count = manager.Catapults.Count;
        int index = manager.ActiveCatapult ? manager.ActiveCatapult.Index : 0;
        counterText.text = $"{index + 1} / {count}";

        // Only a single-catapult level gets non-interactable arrows — wrap-around
        // makes the ends safe (1/3 → left → 3/3), so never grey them out mid-ring.
        bool multiple = count > 1;
        if (leftButton) leftButton.interactable = multiple;
        if (rightButton) rightButton.interactable = multiple;
    }
}
```

**Line-by-line notes:**

*`onClick.AddListener(manager.PreviousCatapult)`* — a *method group* passed as the listener: the
button calls it on every click. The matching `RemoveListener(manager.PreviousCatapult)` with the
*same* method group removes exactly that entry (this pairing rule is the same as the `+=`/`-=` of
C# events).

*`Refresh` in `OnEnable`* — the manager raises `OnActiveCatapultChanged` in its own `Awake`, which
runs **before** this UI's `OnEnable`, so this component never sees the initial event. Every
event-driven UI in this feature has the same rule: *refresh once on enable, then trust events for
changes* (the launcher's `Start → LoadProjectile` is the same pattern from the "lifecycle-ordering
trap").

*Why `manager` null-guard but not `manager.ActiveCatapult` null-guard in the listeners:*
`NextCatapult`/`PreviousCatapult` already guard (`if (ActiveCatapult == null) return;`) — the
manager owns that invariant; the UI does not repeat it.

- [ ] **Step 2: Stage & propose commit**

```bash
git add Assets/Scripts/UI/CatapultSwitcherUI.cs Assets/Scripts/UI/CatapultSwitcherUI.cs.meta Assets/Prefabs/UI/CatapultArrowButton.prefab Assets/Prefabs/UI/CatapultArrowButton.prefab.meta
```
Proposed message: `feat(ui): catapult switcher arrows with wrap-around and counter`

---

## Phase 5 — Scene & prefab wiring (Editor steps, in this order)

All Editor work in one sitting. The order matters in two places, and both are the same lesson:
**a duplicated hierarchy duplicates its components and its internal references, but keeps external
ones.**

### Step 1 — Move the two level-level components out of the catapult

1. Select `ProjectileSelector` on the catapult **head** (`catapult` child) → `Ctrl+X` (Cut) →
   select the new empty `CatapultManager` GameObject → `Ctrl+V`. Unity keeps the component and its
   script GUID.
2. Move `InputController` from the catapult root to the `CatapultManager` GameObject the same way.
3. Create the empty `CatapultManager` GameObject **first** (scene root, no parent), add the
   `CatapultManager` component, and assign `levelData` (`DefaultLevelData`).
4. Re-point the references that used to point at the head's selector:
   * `ProjectileLuncher.projectileSelector` → the manager's selector
   * `ProjectileSpawner.projectileSelector` → the manager's selector
   * `AmmoManager.projectileSelector` → the manager's selector
   * `ProjectileCarouselUI.selector` → the manager's selector

> **Do this before duplicating the catapult.** Duplicate first and the copy brings its own
> `ProjectileSelector`; move first and the duplicate's references point at the single
> manager-level selector.

### Step 2 — Build the camera rig

1. Create empty `CameraRig` at the scene root (**sibling** of the catapult, not a child).
2. Drag `Main Camera` onto `CameraRig` so it becomes a child of the rig.
3. Set the camera's **local** position `(0,0,0)` and **local** rotation identity. The rig's own
   transform is now the camera's world pose (this is what lets `CameraShakeManager` keep shaking in
   local space, untouched).
4. Add `CatapultCameraRig` to `CameraRig`; set `moveDuration` ≈ `1.2`.
5. Create empty child `CameraAnchor` under the catapult **root**, add `CameraAnchor`, and place it
   at the pose the camera used to have: the old camera was a child of the catapult root at local
   position `(3.59, 2.93, 0)`, local euler `(8.71, -90, 0)` — copy those numbers so the first
   catapult's view is unchanged.

### Step 3 — Add the `Catapult` component and wire the manager

1. Add `Catapult` to the catapult **root**; assign `launcher`, `spawner`, `ammoManager` (head) and
   `cameraAnchor`. Optional: assign `highlightObject` if you have a Highlight object.
2. On the `CatapultManager` object: assign `selector` (the moved one) and `cameraRig`. Its `Awake`
   does the rest (rest poses, pools, `selector.Initialize`, first `SetActive`, `SnapTo`).

### Step 4 — Duplicate the catapult

1. Select the catapult root → `Ctrl+D`. Move the copy (e.g. the other side of the field) and give
   it a different yaw.
2. On the **copy**: it already has a `Catapult` component (duplicated) — verify its
   `launcher`/`spawner`/`ammoManager` point at the **copy's own** components (Unity should have
   done this — verify), and `cameraAnchor`… the duplicated `CameraAnchor` child comes along too;
   keep it or re-pose it — each catapult can have its own.
3. Add 2–3 empty children to the copy's `CameraAnchor`, position them in an arc between the two
   catapults, and drag them into its `pathPoints` list — this is what exercises the Catmull-Rom
   glide. (Remove them to test the straight glide.)

### Step 5 — The arrows

1. Instantiate `CatapultArrowButton.prefab` twice under a new full-canvas `CatapultSwitcher`
   object under the `UI` canvas, using the layout table from Task 4.2 (left/right, 96×96, edge
   anchors); rename to `LeftArrow` / `RightArrow`, set the glyphs `<` / `>`.
2. Add a `Counter` TMP text per the table.
3. Add `CatapultSwitcherUI` to `CatapultSwitcher`; wire `manager`, `leftButton`, `rightButton`,
   `counterText`.
4. On the `ProjectileButtons` object (`ProjectileCarouselUI`): assign `catapultManager`.
5. On the moved `InputController` (now on `CatapultManager`): assign `catapultManager`.
6. Set `DefaultLevelData.ammoMode` to taste (`Shared` first, `PerCatapult` to test the other mode).

### Step 6 — Re-save and check the console

Open `SampleScene`, make a trivial change, `Ctrl+S`. Then check the Console: no exceptions, no
missing-reference warnings. Press Play once before committing anything.

---

## Phase 6 — GDD sync (required by `CLAUDE.md`)

`docs/GDD.md` — English headers, Persian body, truthful badges. Update **only after** everything
above actually ships:

1. **§7 Multi-Catapult Levels** — badge `[Planned]` → `[Implemented]`. Rewrite the «انتخاب منجنیق»
   bullet: switching is the **arrows on the left/right of the screen** (with wrap-around), not
   tapping the catapult (tapping becomes `[Future]`). Add the camera behaviour (glide to the active
   catapult, straight or along a designer path; then a soft lagged follow of the aim) and the ammo
   modes (shared vs. per-catapult, set per level).
   The line **«محدودیت تیر: سقف کل مرحله است (نه به‌ازای هر منجنیق)»** must be **rewritten**, not
   amended: with `ammoMode` the cap can be level-wide (`Shared`) or per-catapult (`PerCatapult`),
   and the level decides; the old sentence remains true as the default (`Shared`).
2. **§5.2 مدیریت تیر** — add a bullet for the shared/per-catapult mode; correct the bullet calling
   the pool «جدا برای هر پرابه» — that is per *type* (still true), the new axis is per *catapult*.
   Keep the two ideas distinct.
3. **§3 Controls & Camera** — the mouse claim becomes true once Task 3.5 lands; update the camera
   section from «دنبال‌کردن اکشن» to rig + anchor + optional path + **soft aim follow with
   configurable lag**.
4. **§9 Scoring** — leftover bonus is still not wired to end-of-level (stays `[Partial]`); add a
   note that with per-catapult pools the bonus must be **summed across the pools**. Do not mark it
   implemented.
5. **§10 Meta & UI** — add a row for the catapult switcher (arrows + `n / m` counter), marked
   `[Implemented]` only once it is actually in the build.
6. **§2 Core Loop** — the first step «انتخاب منجنیق (در سطوح چندمنجنیقی)» is now real; keep the
   wording but as an arrow switch rather than a tap.

---

## Verification

### Automated (EditMode)

`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`. Expected green: the three new files
(`RingIndexTests` 6, `AmmoPoolTests` 9, `CameraPathTests` 8) plus the existing
`CarouselWindowTests`, `CarouselLayoutCalculatorTests`, `LeftoverScoreCalculatorTests`.

### Manual (Editor, Game view) — after Phase 5

1. **One catapult still behaves** — Play with only catapult A enabled. Aiming, firing, carousel:
   as before. The camera sits in the same *place* but now *lags* behind the aim (the intended
   change from Task 2.6).
2. **Switching, with wrap-around** — right arrow: counter `1 / 2 → 2 / 2`, camera glides behind B.
   From the last catapult, right wraps to the first; left wraps the other way. Arrows
   non-interactable only when exactly one catapult exists.
3. **Straight vs. path** — with B's `pathPoints` filled, switching to B follows the arc and
   arrives at B's anchor pose. Remove the points, switch, confirm straight.
4. **Ammo — Shared** (default): empty a type on A, switch to B — same type shows `0` for B too;
   the counts are one pool.
5. **Ammo — PerCatapult**: set `DefaultLevelData.ammoMode = PerCatapult`. Empty Wood on A, switch
   to B — B still shows the full count. Both shoot independently.
6. **Global type selection** — pick Boulder in the carousel; both catapults load a Boulder (the
   inactive one pre-loads via `OnSelectionChanged`; the active one definitely has one via
   `LoadProjectile`).
7. **Soft aim follow** — drag horizontally: the catapult yaws immediately, the camera turns after
   it, visibly lagging, then settles. Tune `followSharpness` (`1` ≈ floaty, `8` ≈ almost rigid,
   `3` is the starting point). Release: the camera glides back with the head reset.
8. **UI clicks do not aim** — tap the right arrow and drag slightly: the catapult must **not**
   rotate. Tap a carousel button: no rotation either. (The `IsPointerOverGameObject` guard.)
9. **Switching mid-drag** — start a drag on A, click the arrow while holding (multi-touch or
   second mouse button): clean switch, no exceptions, the drag is dropped.
10. **Rapid switching** — click the arrow three times fast: the camera re-targets from its
    *current* pose each time (no snap-back), the counter ends on the right number.
11. **Inactive catapult with an empty pool for the type** — PerCatapult mode, empty Wood on A,
    switch to B: B loads a Wood. Empty Wood on B too, switch back to A: no projectile on either,
    **no exceptions** (spawner returns null; launcher holds nothing).

---

## Commit

Per your standing rule, nothing is committed automatically. Stage and review the message:

```
feat(catapult): add multi-catapult support with camera rig and per-level ammo modes
```

Expected in it: the new scripts (`Catapult/`, `Camera/`, `CatapultSwitcherUI`, tests), the modified
scripts (`AmmoManager`, `Projectile`, `ProjectileSpawner`, `ProjectileLuncher`, `InputController`,
`ProjectileCarouselUI`, `SO_LevelData`), `SampleScene.unity`, `CatapultArrowButton.prefab`,
`DefaultLevelData.asset` (ammoMode), `docs/GDD.md`, and this plan.

> **`SampleScene.unity` again:** ~13,000 lines of YAML; Unity rewrites large parts on save. Commit
> it only after saving from the Editor, and check `git diff --stat` first — expect "a component
> move + a few new objects", not a wholesale rewrite.

---

## Appendix A — concept cheat sheet

| Concept | Why it is here |
|---|---|
| **Assembly definition (`asmdef`)** | `CastleCrusher.Core` holds pure maths with no scene dependency, testable in EditMode. An asmdef **cannot reference `Assembly-CSharp`** — hence generic `AmmoPool<TKey>`. It *can* use `Vector3`/`Mathf` (`noEngineReferences: false`). |
| **Generics** | `AmmoPool<TKey>` keeps Core free of game types while staying fully typed at the call site. |
| **Unity lifecycle order** | `Awake` → `OnEnable` → `Start`; all `Awake`s before all `Start`s; relative order of two `Awake`s undefined. Never depend on it — hand data over explicitly (`CaptureRestPose`, `AttachPool`, `LoadProjectile` in `Start`). |
| **Events (`Action<T>`)** | Manager announces "active catapult changed"; camera and UI subscribe. Loose coupling. |
| **Coroutine (`IEnumerator`)** | The main-thread-safe way to animate over time. `async/await` resumes on a thread-pool thread by default. |
| **`Quaternion.Slerp`** | Correct rotation interpolation (shortest arc). `Vector3.Lerp` on Euler angles is not. |
| **Arc-length parameterisation** | "Progress" = distance travelled, so the camera moves at constant speed. `PolylinePath`. |
| **Local vs. world transform** | `localPosition/localRotation` are parent-relative; `position/rotation` are world. The anchor is authored locally, evaluated against the catapult's *rest* frame; the aim-follow is a deliberate lag on top. |
| **`FindObjectsByType` / `FindAnyObjectByType`** | Unity 6 replacements for the deprecated `FindObjectsOfType`/`FindObjectOfType`. |
| **`[FormerlySerializedAs]`** | Keeps scene data alive across a field rename. Alternative: re-save the scene. |
| **ScriptableObject enum** | Designer-facing dropdown, serialised as int; the first value is what old assets keep (`Shared`). |
| **uGUI `Button` + `onClick`** | Standard tap response. Pair `AddListener`/`RemoveListener` like `+=`/`-=`. |
| **`EventSystem.current.IsPointerOverGameObject()`** | The only reliable way to know a press landed on UI — legacy `Input` and uGUI never talk to each other. |
| **`LateUpdate` ordering** | Every `Update` before every `LateUpdate`; cameras read the frame's final pose there. |
| **Frame-rate-independent smoothing** | `1 - exp(-k * dt)` covers the same fraction per *second* regardless of fps. Never tune feel with a bare per-frame factor. |
| **`Quaternion.Inverse`** | Rotation "subtraction": `current * Inverse(rest)` = "how far it has rotated since rest". |
| **Local function** | A method declared inside another method (`CountFor` in `CatapultManager`); captures locals, keeps the class surface small. |
| **Method group** | A method name used as a delegate value (`ammo.HasAmmo` as `Func<SO_ProjectileType,bool>`); defers "whose ammo counts?" to the caller. |
| **`TryGetValue` / `TryGetComponent`** | Safe lookup + value in one expression; replaces indexer-throw and null-check idioms. |
| **Anchors vs. anchored position (uGUI)** | Anchor = proportion of the parent (edge-hugging); anchored position = constant offset from it. |

---

## Appendix B — اسپلاین از صفر — اول مفهوم، بعد کد

(The spline derivation from the previous draft, kept unchanged — it explains *why*
`CatmullRom.Sample` and `PolylinePath` look the way they do.)

## ۱. مسئله: ما فقط چند نقطه داریم، نه منحنی

کامپیوتر نمی‌تواند «یک منحنی» را ذخیره کند. چیزی که ما داریم چند **نقطه** است (waypoint های مسیر دوربین). پس سؤال اصلی این است:

> تابعی بساز که به ازای هر عدد `t` بین ۰ و ۱، یک «مکان روی مسیر» برگرداند — طوری که در `t=0` روی نقطه اول باشیم، در `t=1` روی نقطه آخر، و بین این دو، مسیر **نرم** باشد.

به این کار **درون‌یابی (interpolation)** می‌گویند: ساختن مقادیر میانی از روی مقادیر معلوم.

## ۲. پایه‌ای‌ترین ابزار: تابع پارامتریک و Lerp

ساده‌ترین حالت — حرکت خطی بین دو نقطه `A` و `B`:

```
q(t) = (1−t)·A + t·B
```

مثال عددی: `A=(0,0)`، `B=(10,0)`، `t=0.25` → `q=(2.5, 0)`.

- `t` را مثل یک **دستگاه زمان** ببین: از ۰ تا ۱ می‌چرخانی، و خروجی `q(t)` نقطه‌ای است که روی صفحه جابه‌جا می‌شود.
- چرا فرمول این شکلی است؟ در `t=0` وزنِ `A` یک و وزنِ `B` صفر است → دقیقاً روی `A`. در `t=1` برعکس. بین این دو، وزن‌ها کم‌کم جابه‌جا می‌شوند.
- `Vector3.Lerp` یونیتی دقیقاً همین تابع است.

نکته کلیدی: `q` تابعی است که به جای عدد، **مکان** برمی‌گرداند. به چنین توابعی «پارامتریک» می‌گویند و همه‌ی اسپلاین‌ها همین شکل‌اند: **یک dial به نام t، که هر جایش یک نقطه تحویل می‌دهد.**

## ۳. مشکل: چند نقطه = زیگزاگ

اگر چند نقطه را با Lerp به هم وصل کنیم، مسیر شکسته می‌شود و دوربین در هر گوشه ناگهان می‌پیچد.

راه‌حل اولی که به ذهن می‌رسد: *یک* چندجمله‌ای بزرگ که از همه نقاط رد شود. مشکلش این است که چندجمله‌ایِ درجه‌بالا بین نقاط **نوسان** می‌کند — دوربین وسط راه اوج می‌گیرد و پایین می‌آید، در حالی که نقاطت اصلاً این را نمی‌گویند.

راه درست (که قرن‌هاست مهندسان استفاده می‌کنند): **قطعه‌قطعه ساختن (piecewise)**. بین هر دو نقطه‌ی مجاور یک «قطعه»ی کوچک می‌سازیم و قطعه‌ها را در محل اتصال نرم به هم می‌دوزیم. اسم انگلیسیش هم از همین‌جا آمده: طراحان نقشه‌کشی خط‌کش چوبی انعطاف‌پذیری داشتند به نام spline که زیرش وزنه می‌گذاشتند تا از نقاط دلخواه عبور کند — خود خط‌کش به‌طور طبیعی منحنی نرم می‌گرفت.

## ۴. راز نرمی: در محل اتصال چه شرطی لازم است؟

دو قطعه داریم: قطعه ۱ به نقطه `P` می‌رسد و قطعه ۲ از `P` شروع می‌شود. دو سطح کیفیت:

- **اتصال موقعیتی (C0):** هر دو دقیقاً از `P` رد شوند — منحنی «پاره» نیست. کافی نیست! هنوز گوشه تیز داریم.
- **اتصال جهت‌دار (C1):** **جهت حرکت** هنگام رسیدن به `P`، با جهت حرکت هنگام ترک `P` یکی باشد. مثل رانندگی: فرمان ناگهان نمی‌چرخد.

یک تعریف ساده لازم داریم: **تانجانت (tangent)** = بردار «سرعت» نقطه متحرک در آن لحظه — یعنی به چه سمتی و با چه تمایلی دارد می‌رود. (این همان مفهوم «مشتق» است؛ فعلاً فقط همین تصویرِ سرعت را نگه دار.)

هدف ما: منحنی‌ای که همه اتصال‌هایش C1 باشد. **Catmull-Rom دقیقاً همین است.**

## ۵. ساختن یک قطعه: قرارداد هرمیت

حالا فقط بین دو نقطه `p1` و `p2`. چند «خواسته» داریم؟

1. از `p1` شروع شود
2. به `p2` برسد
3. با سرعت اولیه `m1` راه بیفتد (تانجانت شروع)
4. با سرعت `m2` برسد (تانجانت پایان)

چندجمله‌ای درجه ۳ این شکلی است: `q(t) = a·t³ + b·t² + c·t + d` — دقیقاً **۴ ضریب مجهول برای ۴ خواسته**. این تطابق عددی است که «مکعبی» بودن را انتخاب می‌کند:

- خط (درجه ۱): ۲ مجهول → فقط دو سر را می‌تواند تنظیم کند، اصلاً نمی‌خمد.
- درجه ۲: ۳ مجهول → کم می‌آورد.
- درجه ۳: ۴ مجهول → نه کم، نه زیاد.

اگر دستگاه ۴ معادله را حل کنی (الان بی‌اثبات قبول کن)، به فرم **هرمیت** می‌رسی:

```
q(t) = h00(t)·p1 + h10(t)·m1 + h01(t)·p2 + h11(t)·m2
```

شهودِ واقعی این‌جاست — هر `h` یک چندجمله‌ای کوچک است که مثل «وزن» عمل می‌کند:

| تابع | رفتارش | نقشش |
|---|---|---|
| `h00 = 2t³−3t²+1` | از ۱ شروع، به ۰ می‌رسد | وزنِ نقطه شروع که کم‌کم **محو** می‌شود |
| `h01 = −2t³+3t²` | از ۰ به ۱ می‌رسد | وزنِ نقطه پایان که کم‌کم **ظاهر** می‌شود |
| `h10 = t³−2t²+t` | خودش صفر است ولی شیبش در `t=0` برابر ۱ است | «ضربه اولیه» سرعت `m1` |
| `h11 = t³−t²` | خودش صفر است ولی شیبش در `t=1` برابر ۱ است | اثر سرعت `m2` موقع رسیدن |

یعنی منحنی = مخلوط وزن‌دارِ چهار اثر: *«کجا شروع کردم، به کجا می‌روم، چطور راه افتادم، چطور می‌رسم.»*

## ۶. تانجانت‌ها را از کجا بیاوریم؟ ترفند Catmull-Rom

هرمیت می‌گوید «تانجانت بده»، ولی ما فقط **نقطه** داریم. Catmull-Rom یک قاعده ساده دارد: تانجانت هر نقطه = جهت از نقطه‌ی قبل به نقطه‌ی بعد، نصف‌شده:

```
m = (p_next − p_prev) / 2
```

چرا این منطقی است؟ در نقطه `p1`، جهتِ ورود `(p1−p0)` و جهتِ خروج `(p2−p1)` است. میانگین‌شان:

```
((p1−p0) + (p2−p1)) / 2  =  (p2−p0) / 2
```

و این دقیقاً همان چیزی است که C1 را «مجانی» می‌کند: قطعه‌ی چپِ `p1` سرعتِ پایانش را از همین میانگین می‌گیرد، قطعه‌ی راست سرعتِ شروعش را از *همان* میانگین → هر دو یک جهت → پیوستگی برقرار، بدون هیچ هماهنگی اضافه.

دو انتها که همسایه ندارند: نقطه اول را دوبار حساب می‌کنیم (`p0 = p1`) و نقطه آخر را هم (`p3 = p2`) — منحنی ته‌وبته می‌ماند و از نقاط واقعی بیرون نمی‌زند. (در کد: `points[Mathf.Max(0, i-1)]` و `points[Mathf.Min(count-1, i+2)]`.)

## ۷. رساندن هرمیت به فرمول معروف

حالا در فرمول هرمیت، جای `m1` و `m2` همین انتخاب Catmull-Rom را بگذار و جمله‌ها را بر حسب `p0..p3` جمع کن. حاصل، همان فرمول پلن است:

```
q(t) = 0.5 · ( 2·p1 + (−p0+p2)·t + (2p0−5p1+4p2−p3)·t² + (−p0+3p1−3p2+p3)·t³ )
```

خواندنش حالا این است:
- جمله `2p1` (بدون t): «از کجا شروع می‌کنم»
- جمله `t`: «با چه سرعت اولیه»
- جمله‌های `t²` و `t³`: تصحیح‌هایی که کاری می‌کنند دقیقاً به `p2` برسی *و* با سرعت درستِ `m2` برسی.

محک خودت (این را واقعاً انجام بده، دو دقیقه است): در فرمول `t=0` بگذار — همه جملات جز `0.5·2p1` صفر می‌شوند → `q(0) = p1` ✅. بعد `t=1` بگذار و ساده کن → `p2` درمی‌آید. اگر این دو تا دستت آمد، فرمول را فهمیده‌ای — و دقیقاً همین دو مورد را در `Sample_StartsAtFirstPoint_EndsAtLastPoint` تست کرده‌ایم.

## ۸. از فرمول به کامپیوتر: نمونه‌برداری

فرمول بی‌نهایت نقطه تولید می‌کند؛ ما نمی‌توانیم بی‌نهایت نقطه داشته باشیم. راه‌حل: `t` را در گام‌های ریز (مثلاً ۲۰ بار در هر قطعه) ارزیابی می‌کنیم و نقاط را با پاره‌خط‌های خیلی کوتاه به هم وصل می‌کنیم — چشم فرقش را نمی‌بیند.

این دقیقاً کار `CatmullRom.Sample(points, samplesPerSegment)` است: برای هر قطعه، `samplesPerSegment` نقطه می‌سازد. خروجی یک **چندخطی (polyline)** است: لیست مرتبی از نقاط که پشت‌سرهم وصل‌اند.

## ۹. تله مهم: t = سرعت نیست! (طول کمان / arc length)

قدم‌های برابر در `t`، فاصله‌های برابر روی منحنی **نمی‌دهند** — «سرعت» نقطه متحرک در طول قطعه ثابت نیست؛ جاهایی که منحنی کشیده است، سریع‌تر رد می‌شود.

اثر عملی برای ما: اگر دوربین با `t` یکنواخت حرکت کند، وسط مسیر تند و ته مسیر کند می‌شود — شتابِ بی‌دلیل.

راه‌حل: **اندازه بگیر.**

1. منحنی را به پاره‌خط‌های ریز تبدیل کن (همان خروجی `Sample`).
2. طول هر پاره‌خط را حساب کن و **جدول فاصله تجمعی** بساز. مثلاً برای نقاط `A→B→C` که `|AB|=3` و `|BC|=4`: جدول `[0, 3, 7]` — یعنی «نقطه i چند واحد از مبدا دور است».
3. حالا پیشروی را با «متر» تعریف کن، نه با `t`: وقتی گفتی ۵۰٪ مسیر، یعنی «نصفِ طول کل واحد رفته‌ام». در جدول پیدا کن این مقدار بین کدام دو ورودی می‌افتد، و داخل همان پاره‌خط یک Lerp کوچک بزن.

نتیجه: حرکت با **سرعت ثابت**. این دقیقاً کار `PolylinePath` است — سازنده‌اش جدول را می‌سازد و `Evaluate(t)` همان جست‌وجو در جدول است. و حرکت مستقیم؟ همان چیزی است با دو نقطه — برای همین دوربین فقط یک مسیر کد دارد: *بساز، بعد قدم بزن.*

## ۱۰. پل به کد

| مفهوم ریاضی | کجا می‌نشیند |
|---|---|
| تابع پارامتریک `q(t)` | `PolylinePath.Evaluate(float t)` |
| فرمول مکعبی روی ۴ نقطه | بدنه داخلی `CatmullRom.Sample` |
| تکرار انتها برای `p0`/`p3` | `points[Mathf.Max(0, i−1)]` و `points[Mathf.Min(count−1, i+2)]` |
| نمونه‌برداری | حلقه on `t` داخل `Sample` |
| جدول فاصله تجمعی | سازنده `PolylinePath` |
| «چند واحد رفته‌ام» → کدام پاره‌خط | `Evaluate` |







