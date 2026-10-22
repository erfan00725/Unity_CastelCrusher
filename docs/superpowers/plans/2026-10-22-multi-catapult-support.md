# Multi-Catapult Support — Implementation Plan

## Context

The scene today has exactly one catapult, and "the catapult" is hard-coded in three different places
(`InputController` finds its launcher with `GetComponent`, `AmmoManager` owns the *only* copy of the
projectile-type list, and the camera is parented to the catapult root). You want the scene to hold
several catapults, let the player hop between them with arrows on the left and right of the screen,
fly the camera behind whichever catapult is active (straight, or along a designer-authored path), and
let each level decide whether the projectile ammo is one shared pool or one pool per catapult.

Two things make this more than "add a second catapult":

1. **Ownership.** Today `AmmoManager.Awake()` *re-initialises the shared `ProjectileSelector`*. Two
   catapults = two `AmmoManager`s = two components fighting over one list and one selected index.
   Before adding catapults, the ammo data needs one clear owner.
2. **The camera is a child of the catapult root.** That is *why* horizontal drag currently spins the
   camera around with the catapult — it is an accidental side effect of the hierarchy. You want the
   camera to keep following the aim, but **softly**, lagging behind the catapult's rotation instead
   of being welded to it. A top-level rig lets the camera own its own pose, so "follow the aim at a
   slower rate" becomes a deliberate, tunable behaviour rather than a side effect of parenting.

**Outcome:** a scene can hold N catapults; the arrows switch the active one (wrapping around at the
ends); the camera glides to the active catapult's anchor (or along a path) and then softly follows
its aim; the level chooses shared vs. per-catapult ammo.

---

## Decisions you already made (yours answers — the plan builds on these)

| Topic | Decision |
|---|---|
| Camera structure | **Top-level rig + per-catapult anchors.** A `CameraRig` GameObject owns the camera; each catapult carries a `CameraAnchor` describing the camera pose. |
| Camera during aim | **Soft follow:** while aiming, the camera follows the catapult's yaw *with a lag* (slower than the catapult itself) so it feels pleasing, not rigid. |
| Arrow ends | **Wrap around (cyclic):** right arrow from the last catapult goes to the first, matching how the projectile carousel already wraps. |
| Ammo model | **Each catapult gets its own copy** of the level loadout when the level asks for it; one shared pool otherwise. Chosen per level in `SO_LevelData`. |
| Type selection | **Global.** One `ProjectileSelector` for the whole level — the selected projectile type is the same no matter which catapult is active. |
| Spline | **Custom Catmull-Rom in `CastleCrusher.Core`** — pure maths, unit-testable, no new packages (`com.unity.splines` is not installed). |

---

## Target architecture

```
Scene
├── CameraRig                        [CatapultCameraRig]   ← top level, NOT under a catapult
│   └── Main Camera                  [CameraShakeManager]  ← local pose (0,0,0); rig owns the world pose
├── CatapultManager                  [CatapultManager]     ← level brain: registry, active one, ammo pools
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
                    ├─ A.Highlight(false) / B.Highlight(true)
                    ├─ OnActiveCatapultChanged(B)
                    │     ├─► ProjectileCarouselUI  → re-reads ammo from B's pool
                    │     └─► CatapultCameraRig    → MoveTo(B)
                    └─ CatapultCameraRig.MoveTo(B)
                          start = rig current pose
                          end   = B.CameraAnchor pose (evaluated in B's rest frame)
                          path  = B.CameraAnchor.pathPoints (2+ → Catmull-Rom, else straight)
```

---

## Suggested order of work

| # | Phase | Why in this order |
|---|---|---|
| 1 | **Phase 0** (fix the stale scene data) | Cheap, and you need the scene re-saved anyway before the new components can be wired. |
| 2 | **Phase 1** (Core maths + tests) | Pure code, no scene work — you get a green test run before touching the Editor. |
| 3 | **Phase 2.5** (`SO_LevelData.ammoMode`) | One enum field; everything downstream reads it. |
| 4 | **Phase 2.1–2.2** (`Catapult` + `CatapultManager`) | The spine of the feature. |
| 5 | **Phase 3** (refactors: `AmmoManager`, `Projectile`, `ProjectileSpawner`, `ProjectileLuncher`) | The manager needs these to hand out pools and to reload projectiles. |
| 6 | **Phase 2.3–2.4** (`CameraAnchor` + `CatapultCameraRig`, including the soft aim follow in 2.4b) | Needs the manager to tell it where to go. |
| 7 | **Phase 3.5–3.6** (`InputController`, `ProjectileCarouselUI`) | Now that there is an "active catapult" to drive. |
| 8 | **Phase 4** (arrow UI) | Pure presentation on top of a working manager. |
| 9 | **Phase 5** (scene wiring) | Do it in one sitting — the move-then-duplicate order matters. |
| 10 | **Phase 6** (GDD) | Last, so the badges describe what actually shipped. |

---

## Phase 0 — Unblock: fix what is already broken

These are real bugs (verified in the committed scene) that will bite you the moment a second catapult
appears. Do them first, in the Editor, because fixing them requires re-saving the scene.

### 0.1 The scene stores two fields that no longer exist on the scripts

`Assets/Scenes/SampleScene.unity` still serialises:

* `bs: {fileID: 799082386}` on the launcher — the field was renamed to `projectileSpawner` in commit
  `649c4dd`, but the scene was never re-saved after the rename, so the reference deserialises as
  **null**. `ProjectileLuncher.DelaySpawn` would throw a `NullReferenceException` on the first shot.
* `uiManager: {fileID: 2123016087}` on `AmmoManager` and on `ScoreManager` — the field was removed in
  commit `8217392`; Unity ignores unknown fields, so it is harmless noise, but it is stale.
* `m_EditorClassIdentifier: Assembly-CSharp::BuletLuncher` on the launcher — the class itself was
  renamed to `ProjectileLuncher` in the same commit `649c4dd`. This one is harmless (Unity resolves
  the component through the script's GUID, not the class name), but it is a useful *timestamp*:
  it proves the scene was last written before the rename.

**Teaching note — why this happens.** A scene file is a *serialised snapshot*. Renaming or deleting a
`public`/`[SerializeField]` field in a script does **not** rewrite the scene; Unity only reconciles
the scene when you open it and save it. There are two ways to survive a rename:

* `[FormerlySerializedAs("bs")] public ProjectileSpawner projectileSpawner;` — Unity keeps the old
  data in the scene and maps it onto the new field. Best when the old scene must keep working.
* Just re-open and re-save the scene — Unity drops the unknown key. Fine for a project in progress.

**Fix (choose one):**
* Add `[FormerlySerializedAs("bs")]` to `ProjectileLuncher.projectileSpawner` **and** re-save the
  scene (belt and braces; the attribute documents the history for anyone reading the code), **or**
* Skip the attribute and simply re-open `SampleScene` and re-save it.

**How to verify:** open the scene, click the catapult root, look at the `Projectile Spawner` field on
the `ProjectileLuncher` component — it must show the `Spawner` object, not `None`. Same for
`AmmoManager`'s now-removed `uiManager` row (it should vanish after re-save).

### 0.2 The EventSystem's input actions point at the package's defaults, not the project's own asset

`InputSystemUIInputModule` on the `EventSystem` references action fileIDs under GUID
`ca9f5fa95ffab41fb9a615ab714db018` — that file *does* exist, but it is the Input System package's
shipped `DefaultInputActions.inputactions` (it lives in `Library/PackageCache/…/PlayerInput/`). Its
UI map has `Navigate / Submit / Cancel / Point / Click / ScrollWheel / MiddleClick / RightClick` but
**no `LeftClick`**. The project's own `Assets/InputSystem_Actions.inputactions`
(GUID `052faaac586de48259a63d0c4782560b`, with `Player` and `UI` maps) is referenced by **nothing** —
so the scene was clearly copied from a sample scene and never re-pointed.

**Fix:** in the Editor, select the `EventSystem`, open `Input System UI Input Module`, and click
**"Assign Default Actions"** (or point `Actions Asset` at `Assets/InputSystem_Actions.inputactions`
and fill the action fields). Do this *before* you rely on mouse clicks for the new arrow buttons.

> If mouse clicks on the carousel already work for you today, this is cosmetic — but fix it anyway so
> the arrows are guaranteed to work on desktop.

### 0.3 Mouse aiming does not exist (the project claims it does)

`InputController` only reads `Input.GetTouch(0)`. `ProjectSettings.asset` has no `enableMouseTouch` /
`simulateMouse`, so on desktop `touchCount` is always 0 and you cannot aim with a mouse — even though
`CLAUDE.md` and `docs/GDD.md` §3 both say mouse is supported.

**Fix:** see Phase 3.5 — one unified pointer path that reads touch *or* mouse. `activeInputHandler: 2`
means "Both", so the legacy `Input` API still works alongside the Input System package.

---

## Phase 1 — Core maths (`CastleCrusher.Core`) + unit tests

Pure C#, no `MonoBehaviour`, no Unity scene dependency — that is what makes it unit-testable in
EditMode without entering Play mode. It lives in `namespace Core` and must not reference
`SO_ProjectileType` or anything else from `Assembly-CSharp` (an asmdef assembly **cannot** reference
the predefined `Assembly-CSharp`, so the Core code has to stay generic).

**Follow the house style already in `Assets/Scripts/Core/`:** a plain `public class` in
`namespace Core` with static methods (`CarouselWindow` is exactly this shape), and NUnit test files
in `namespace Tests.EditMode` next to the existing ones.

**Teaching note — `Vector3` in Core is fine.** `CastleCrusher.Core.asmdef` has
`"noEngineReferences": false`, so the UnityEngine assembly is referenced and `Vector3` /
`Vector2` / `Mathf` are available inside Core. (`LeftoverScoreCalculator` already uses C# tuples
the same way — `IReadOnlyList<(int, float)>`.) What Core must **not** do is reference *your game
types* (`SO_ProjectileType`, `AmmoManager`, …), because an asmdef assembly cannot see the
predefined `Assembly-CSharp`. That is exactly why `AmmoPool` is generic.

### 1.1 `Assets/Scripts/Core/AmmoPool.cs`

A generic, key-agnostic ammo container.

```csharp
using System;
using System.Collections.Generic;

namespace Core
{
    public class AmmoPool<TKey>
    {
        private readonly Dictionary<TKey, int> _counts = new Dictionary<TKey, int>();
        public event Action<TKey, int> OnChanged;

        public void Set(TKey key, int count);
        public int Get(TKey key);
        public bool Has(TKey key);
        public int Consume(TKey key);   // decrement, never below 0, raises OnChanged
        public int TotalRemaining();
        public void CopyFrom(IReadOnlyList<KeyValuePair<TKey,int>> entries);
    }
}
```

**Teaching note — generics as a decoupling tool.** `AmmoPool<SO_ProjectileType>` would be readable but
would drag `Assembly-CSharp` into Core and break the test assembly. `AmmoPool<TKey>` lets Core stay
pure, and the EditMode tests can instantiate `AmmoPool<string>` to prove the behaviour without any
game types at all. The "key" is then just a dictionary key; the game layer decides the key is a
`SO_ProjectileType`.

**Teaching note — `IReadOnlyList`.** It is the "read but don't modify" view of a collection. Using it
in a method signature documents that the method will not mutate what you pass, and lets you pass a
`List<T>` or an array interchangeably.

### 1.2 `Assets/Scripts/Core/CameraPath.cs`

Two helpers, both pure:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class CatmullRom
    {
        // Samples the curve through `controlPoints` (>= 2) into a dense polyline.
        public static List<Vector3> Sample(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment);
    }

    public class PolylinePath
    {
        public PolylinePath(IReadOnlyList<Vector3> points);
        public float TotalLength { get; }
        public Vector3 Evaluate(float t);   // t in [0,1], constant speed by arc length
    }
}
```

**Teaching note — what a spline actually is.** A Catmull-Rom spline is a curve that passes *through*
every control point, with the tangent at each point estimated from its neighbours. The classic
formula for one segment between `p1` and `p2` (with `p0`/`p3` as the surrounding points):

```
q(t) = 0.5 * ( (2*p1) + (-p0 + p2)*t + (2*p0 - 5*p1 + 4*p2 - p3)*t² + (-p0 + 3*p1 - 3*p2 + p3)*t³ )
```

with `t` running 0→1 across that one segment. You do not need this formula in the game code — you
need it inside `Sample`, which walks the control points segment by segment.

**Teaching note — why arc length matters.** If you evaluate the curve with a uniform `t`, the camera
*speeds up* on long segments and slows down on short ones, because a uniform `t` is a uniform
*parameter*, not a uniform *distance*. `PolylinePath` fixes this the way a racing game does: sample
the curve into many short segments, measure the distance of every sample from the start, and then
treat progress as "how many units have I travelled". Given `t`, find the sample whose cumulative
distance matches `t * TotalLength` and interpolate between the two neighbouring samples. The result
is a camera that travels at a constant speed.

**Teaching note — the straight move is the same code path.** A straight move from A to B is just a
`PolylinePath` with two points. So the rig has exactly one code path: *build a path, then walk it*.
`pathPoints.Count == 0` on the anchor → build the 2-point path (straight). `pathPoints.Count >= 2`
→ build the Catmull-Rom path. No branching in the tween.

### 1.3 `Assets/Scripts/Core/RingIndex.cs`

```csharp
using System;

namespace Core
{
    public static class RingIndex
    {
        // Wrap-around index maths for a ring of `count` items.
        public static int Wrap(int index, int count);
        public static int Next(int current, int count, int delta);
    }
}
```

**Teaching note — wrap-around arithmetic.** `((index % count) + count) % count` is the standard
"positive modulo" trick in C#: plain `%` returns a *negative* result for negative operands, and the
double `%` brings it back into range. `ProjectileSelector.SelectIndex` already uses it; the new
`RingIndex` just names the pattern and lets both the carousel and the catapult arrows share it.

**Teaching note — `delta` can be negative.** `Next(current, count, delta)` is `Wrap(current + delta, count)`.
A left-arrow press is `delta = -1`. The positive-modulo trick is what makes `Wrap(-1, 3)` come out
as `2` instead of `-1`, which is the only reason the wrap-around works in both directions.

**Teaching note — empty ring.** `Wrap(x, 0)` and `Next(x, 0, d)` must return `0` without dividing
by zero. `RingIndexTests` should cover that case explicitly, because `ProjectileSelector` guards
against `availableTypes.Count == 0` and the arrows guard against `Catapults.Count <= 1` — but a
future caller might not.

### 1.4 EditMode tests — `Assets/Tests/EditMode/`

Add three files next to the existing ones (same `CastleCrusher.EditModeTests` assembly):

* `AmmoPoolTests.cs` — `Set/Get/Has`, `Consume` never goes below zero, `Consume` raises `OnChanged`,
  `TotalRemaining` sums, `CopyFrom` replaces the contents.
* `CameraPathTests.cs` —
  * a 4-point Catmull-Rom sample list starts at the first point and ends at the last;
  * a straight 2-point `PolylinePath` returns the exact endpoints at `t = 0` and `t = 1`;
  * the midpoint of a straight 10-unit path is exactly 5 units along (arc-length check);
  * four collinear control points produce a path whose samples are evenly spaced in distance
    (i.e. the Catmull-Rom does not overshoot a straight line).
* `RingIndexTests.cs` — wrap-around forward/backward, single-item ring, empty ring (must not divide
  by zero), and `Next(current, count, -1)` from index 0 landing on `count - 1`.

**Teaching note — how to run them.** `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`. These
run in milliseconds without entering Play mode because they never touch a scene object. That is the
whole reason the maths lives in `Core` instead of inside a `MonoBehaviour`.

---

## Phase 2 — New runtime components

All of these live in `Assembly-CSharp` (no asmdef) and go under `Assets/Scripts/Catapult/` (new
folder) and `Assets/Scripts/Camera/`.

### 2.1 `Assets/Scripts/Catapult/Catapult.cs` — the per-catapult facade

```csharp
public class Catapult : MonoBehaviour
{
    public ProjectileLuncher launcher;
    public ProjectileSpawner spawner;
    public AmmoManager ammoManager;
    public CameraAnchor cameraAnchor;
    public GameObject highlightObject;   // optional; toggled on/off

    public int Index { get; internal set; }
    public bool IsActive { get; internal set; }

    // The catapult's pose before the player aims it — the frame the camera anchor lives in
    public Vector3 RestPosition { get; private set; }
    public Quaternion RestRotation { get; private set; }

    public void CaptureRestPose();     // called by CatapultManager.Awake — see "lifecycle-ordering trap"
    public void SetHighlight(bool on);
}
```

**Teaching note — what a "facade" is.** A facade is a small object that forwards calls to the real
ones behind it. `Catapult` owns no behaviour of its own; it is the one place the game code can ask
"give me the launcher of *this* catapult" without knowing the scene layout or the component names.
That keeps `InputController` and the UI from reaching into the scene hierarchy with
`FindObjectOfType` calls.

**Teaching note — `internal set`.** The property is readable by everyone in the assembly but only
writable inside it, which is how `CatapultManager` sets `Index`/`IsActive` without letting arbitrary
game code corrupt the registry.

**Teaching note — capturing the rest pose.** `ProjectileLuncher.RotateCatapultBody` yaws the
catapult while aiming, so by the time you want to know "where is this catapult's camera anchor?",
`transform.rotation` is the *aiming* rotation, not the authored one. `CaptureRestPose()` stores the
authored pose once, before any aiming happens, and gives the camera a stable destination (§2.3).
Note that it is a **method the manager calls**, not an `Awake` — see "The lifecycle-ordering trap"
below for why that distinction matters.

### 2.2 `Assets/Scripts/Catapult/CatapultManager.cs` — the level brain

```csharp
public class CatapultManager : MonoBehaviour
{
    public SO_LevelData levelData;
    public ProjectileSelector selector;          // the ONE global selector
    public CatapultCameraRig cameraRig;

    public event Action<Catapult> OnActiveCatapultChanged;
    public Catapult ActiveCatapult { get; private set; }
    public IReadOnlyList<Catapult> Catapults { get; }

    public void SetActive(Catapult catapult);
    public void NextCatapult();
    public void PreviousCatapult();
}
```

`Awake()` does the whole setup, in this exact order (the order matters — see the teaching note):

1. **Find the catapults.** `FindObjectsByType<Catapult>(FindObjectsSortMode.None)`, then call
   `CaptureRestPose()` on each one (see "The lifecycle-ordering trap" below).
   *Teaching note — Unity 6 API.* `FindObjectsOfType` is the old one and is now deprecated in favour
   of `FindObjectsByType`. The `FindObjectsSortMode` argument controls whether Unity sorts by
   distance/instance id (`FindObjectsSortMode.None` = no sorting, cheapest). `FindAnyObjectByType`
   is the single-object version that `Projectile.Shoot` already uses.*
   *Teaching note — the order of the array is not guaranteed.* `FindObjectsByType` with
   `FindObjectsSortMode.None` returns objects in an unspecified order, so "catapult 0" is not
   necessarily the leftmost one. If you want a deterministic order, sort the array by
   `transform.position.x` yourself (or use `FindObjectsSortMode.InstanceID`, which is stable but
   not meaningful). The plan uses the array as-is and the *arrows* are what navigate it, so order
   only affects which catapult is active at level start.*
2. **Work out the projectile type list** — from `levelData.projectileLoadouts` if it is non-empty,
   otherwise from the selector's own pre-authored `availableTypes` (today's fallback behaviour).
   **This is the one and only place the type list is built.** The old `AmmoManager.Awake()` call to
   `projectileSelector.Initialize(types)` is deleted.
   *Teaching note — order of `Awake` calls is not guaranteed.* Unity calls `Awake` on every object
   before any `Start`, but the *relative* order of `Awake` between two objects is undefined. So
   nothing may depend on "object X's `Awake` ran before object Y's". Build the data in one place,
   hand it to everyone else.*
3. **Build the pools.** `levelData.ammoMode` decides:
   * `Shared` → create **one** `AmmoPool<SO_ProjectileType>` and hand it to *every* catapult.
   * `PerCatapult` → create **one pool per catapult**, each filled with a full copy of the loadout.
   `AmmoManager.AttachPool(pool)` is called on each catapult's `AmmoManager`.
   *Teaching note — why the pool is a separate object.* If the pool lived *inside* `AmmoManager`,
   then "shared" would have to mean "all catapults point at the same `AmmoManager` component",
   which is a different wiring per level and cannot be decided at runtime. With the pool as a
   separate object, the wiring in the scene is **identical in both modes** — every catapult always
   has its own `AmmoManager` — and only the runtime assignment differs.*
4. **Initialise the selector once:** `selector.Initialize(types)`.
5. **Pick the first catapult as active** and raise `OnActiveCatapultChanged`.
6. **Snap the rig to the first catapult's anchor** (no glide on the very first frame — the camera
   should just *be* there when the level starts). `CatapultCameraRig.SnapTo(catapult)` does this
   without animation; `MoveTo` is the animated version.

**Edge cases to handle explicitly (do not leave these to chance):**

* **Zero catapults in the scene** → log a clear error and disable the arrows. Do not
  `NullReferenceException` in `Update`.
* **A catapult with no `CameraAnchor`** → fall back to the rig's current pose (the camera simply
  does not move), and log a warning once so the level designer notices the missing anchor.
* **`levelData` is null** → fall back to the selector's pre-authored `availableTypes` and
  `ammoCount` (today's behaviour). The manager must work with no level data assigned.
* **`levelData.projectileLoadouts` is empty** → same fallback: use `availableTypes` and each type's
  own `ammoCount`. This is exactly what `AmmoManager.Awake` does today, so the behaviour is
  preserved.
* **A type in the loadout has no `prefab`** → it is still listed (so the count is visible) but
  cannot be fired. `ProjectileSpawner.InstantiateBullet` already returns `null` for that case;
  `LoadProjectile` must handle a `null` result without throwing.

`SetActive(catapult)` is the only place the active catapult changes:

* set `IsActive` / `highlightObject` on the old and the new catapult;
* raise `OnActiveCatapultChanged`;
* call `cameraRig?.MoveTo(catapult)`.

`NextCatapult()` / `PreviousCatapult()` use `RingIndex.Next(ActiveCatapult.Index, count, +1 / -1)`
and call `SetActive`. **Wrap-around is the chosen behaviour:** from the last catapult, next goes to
the first, and vice versa. That means the arrows are never disabled for having more than one
catapult — only when there is exactly one. It also means `RingIndex` must handle a negative index
correctly (the "positive modulo" trick in §1.3), which is exactly why that helper exists.
*Teaching note — `RingIndex` is shared with the carousel for a reason.* Both the projectile carousel
and the catapult arrows are "step through a cyclic list". Writing the wrap maths once, in `Core`,
and unit-testing it once, means the two features cannot drift apart.

**Teaching note — why the *active* catapult owns the loaded projectile.** Every catapult's launcher
subscribes to the global `OnSelectionChanged` and pre-loads the next projectile, so all of them
have a projectile parented to their spawner at all times. That is wasteful but harmless — until the
ammo pools differ. In `PerCatapult` mode, catapult B may have 0 of the selected type, so B's
spawner holds *no* projectile. The moment B becomes active it must load one; the inactive
catapults do not need to.

So the rule is: **only the active catapult fires, and only the active catapult loads a projectile
on demand.** Give `ProjectileLuncher` a public `LoadProjectile()` that asks the spawner for the
currently selected type, and have `CatapultManager.SetActive` call it on the new active catapult
*after* raising the event. An inactive launcher that is mid-`DelaySpawn` also has to be told to
reload when it wakes up — §3.4 covers that.

**Teaching note — `event Action<Catapult>`.** This is the project's existing pub-sub idiom (see
`ProjectileSelector.OnSelectionChanged` and `AmmoManager.OnAmmoChanged`). The manager does not know
that a UI carousel or a camera rig exists; it just announces "the active catapult changed" and
whoever cares subscribes. That is *loose coupling*, and it is why the UI and the camera do not have
to be referenced from the manager in both directions.

---

## The lifecycle-ordering trap (read this before writing any of the code)

This is the single easiest thing to get wrong in this feature, and it is invisible until the
symptom appears as "sometimes the wrong catapult loads" or "the camera snapped to the wrong spot".

**What Unity guarantees:**
* For a *single* object: `Awake` → `OnEnable` → `Start` → `Update`.
* Across *all* objects: **every `Awake` runs before any `Start`.**
* Across all objects: **the relative order of two `Awake`s is undefined.** Same for two `Start`s.

**What that means here — three concrete rules:**

1. **All setup goes in `CatapultManager.Awake`, and everything that needs the setup runs in `Start`
   or later.** Because all `Awake`s precede all `Start`s, the launcher's `Start` is *guaranteed* to
   run after the manager's `Awake` has attached the pools and initialised the selector. ✅
2. **The rest pose is captured by the manager, not by `Catapult.Awake`.** If `Catapult.Awake`
   captured it and the manager's `Awake` ran first, the manager would read a
   `RestPosition` of `(0,0,0)` and snap the camera to the origin. Instead, `Catapult` exposes a
   plain method:
   ```csharp
   public void CaptureRestPose()
   {
       _restPosition = transform.position;
       _restRotation = transform.rotation;
   }
   ```
   and `CatapultManager.Awake` calls it on every catapult **before** it reads it. This is safe
   because nothing has moved yet — no gameplay code runs before `Start`.
   *Teaching note — a method the manager calls beats a method Unity calls, when the value matters to
   someone else.* `Awake` is a callback *you* own; it is free to make it order-dependent. An
   explicit `CaptureRestPose()` call is order-*independent*: whoever needs the value asks for it
   first.*
3. **Do not rely on `OnSelectionChanged` for the *initial* selection.** `selector.Initialize(types)`
   raises the event, but a launcher that has not run `OnEnable` yet never hears it. That is exactly
   why `ProjectileLuncher.Start` calls `LoadProjectile()` explicitly. The event is for *changes*,
   not for the initial state.

**One harmless wrinkle:** the very first projectile may be instantiated twice — once by
`HandleSelectionChanged` (if the launcher was already enabled when the manager initialised the
selector) and once by `Start`. `InstantiateBulletSafe` destroys the previous one, so the result is
correct; it just costs one extra `Instantiate`. If that bothers you, add a `_hasLoaded` flag to the
launcher. Do **not** try to "fix" it by reordering `Awake` calls — you cannot.

**Teaching note — how to *see* the order instead of guessing.** Add a temporary
`Debug.Log($"{name}.Awake")` to each of the three components and watch the Console on the first
Play frame. It is the fastest way to internalise the rule, and you should do it once for this
feature — the same trap will bite you in every Unity project you write.

### 2.3 `Assets/Scripts/Camera/CameraAnchor.cs` — where the camera should be

```csharp
public class CameraAnchor : MonoBehaviour
{
    public List<Transform> pathPoints = new List<Transform>();  // optional camera route
}
```

The anchor is a **child GameObject of the catapult root** whose local transform *is* the desired
camera pose. No extra offset fields: you place it in the Scene view like any other object.

**The one rule for everything camera-related:** *the anchor and its path waypoints are all children
of the catapult root, and they are all interpreted in the catapult's **rest** (un-aimed) frame.*

**Teaching note — world pose vs. local pose.** A child of the catapult inherits the catapult's
rotation, so the anchor's *world* pose changes when the catapult yaws during aiming. That would drag
the camera's destination around. To keep the destination stable, `Catapult` captures its **rest**
position/rotation in `Awake()` (before any gameplay rotation happens), and the rig evaluates the
anchor against that rest frame:

```
worldPos  = restPosition + restRotation * anchor.localPosition
worldRot  = restRotation * anchor.localRotation
waypoint  = restPosition + restRotation * waypoint.localPosition
```

Because the waypoints are children of the catapult **root** (not of the anchor), their
`localPosition` is already in the same frame as the anchor's — no nested-transform maths needed.

**Teaching note — `Transform.TransformPoint` vs. manual maths.** `TransformPoint` converts a local
point to world space using the transform's *current* rotation. We deliberately do **not** use it,
because "current" is exactly the thing we want to freeze. Doing the multiplication by hand with the
captured rest rotation is one extra line and gives a destination that never drifts.

### 2.4 `Assets/Scripts/Camera/CatapultCameraRig.cs` — the camera mover + aim follower

```csharp
public class CatapultCameraRig : MonoBehaviour
{
    public float moveDuration = 1.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0.1f, 20f)] public float followSharpness = 3f;   // aim-follow lag; see 2.4b

    public void SnapTo(Catapult catapult);   // instant, used at level start
    public void MoveTo(Catapult catapult);    // animated glide, then Following
}
```

`MoveTo`:

1. `startPos = transform.position`, `startRot = transform.rotation` (the rig's *own* transform is the
   camera pose — the camera is a child at local `(0,0,0)`).
2. `endPos/endRot` from the catapult's anchor (rest frame, see 2.3).
3. Build the path:
   * `anchor.pathPoints.Count >= 2` → `CatmullRom.Sample` over
     `[startPos, ...pathPoints, endPos]`, wrapped in a `PolylinePath`;
   * otherwise → `new PolylinePath(new[] { startPos, endPos })` (straight).
4. Run a coroutine that, each frame, computes `eased = ease.Evaluate(t)` and sets
   `transform.position = path.Evaluate(eased)` and
   `transform.rotation = Quaternion.Slerp(startRot, endRot, eased)`.
5. On arrival, snap to the exact end pose (kills float drift — the same trick
   `ProjectileLuncher.ResetHeadRotationRoutine` uses).

**Teaching note — interrupting a move.** If the player clicks the next arrow while the camera is
still gliding, `MoveTo` is called again. Stop the previous coroutine before starting the new one
(`if (_moveRoutine != null) StopCoroutine(_moveRoutine);`) and start the new move from the rig's
**current** pose — not from the original start pose. Otherwise the camera jumps back to where the
old move began.

**Teaching note — why a coroutine and not `async`/`await`.** Unity's `async/await` works, but it
resumes on a thread-pool thread by default, and Unity API calls are not thread-safe. A coroutine
(`IEnumerator` + `yield return`) always resumes on the **main thread** on the next frame, which is
the only place you may touch `transform`. This is the standard Unity idiom for timed animation.

**Teaching note — `Quaternion.Slerp`.** Interpolating rotations with `Vector3.Lerp` is wrong (angles
wrap around 360° and the path between them is not the shortest arc). `Quaternion.Slerp` (spherical
linear interpolation) rotates along the shortest arc at constant angular speed. The project already
uses it in `ProjectileLuncher`.

**Teaching note — why the rotation is *not* taken from the path tangent.** A nicer effect is to
look along the curve. That needs a look-ahead vector and breaks when the path doubles back on
itself. For this feature, `Slerp` from the start pose to the end pose is predictable and reads well.

**Teaching note — how this coexists with `CameraShakeManager`.** `CameraShakeManager` shakes the
camera in **local** space and re-records its "rest" pose every frame while it is settled. Because
the camera is a child of the rig at local `(0,0,0)`, its local rest pose is constant, so the shake
is unaffected by the rig moving. The two systems do not fight.

### 2.4b Aim follow — the soft "lagging" camera

While the player aims, the camera should slowly turn with the catapult. Not rigidly (that is what
the old parent-child hierarchy did) — **slower**, so the catapult visibly leads the camera.

After the move coroutine finishes, the rig switches to a **follow mode** for the active catapult.
Each `LateUpdate` (not `Update` — see below) it computes:

```csharp
// 1. How far the catapult has rotated away from its rest pose, in world space
Quaternion bodyDelta = catapult.transform.rotation * Quaternion.Inverse(catapult.RestRotation);

// 2. Where the anchor would be if it were welded to that rotation
Vector3 anchorRestWorld = catapult.RestPosition
                        + catapult.RestRotation * anchor.localPosition;
Vector3 targetPos = catapult.RestPosition + bodyDelta * (anchorRestWorld - catapult.RestPosition);
Quaternion targetRot = bodyDelta * catapult.RestRotation * anchor.localRotation;

// 3. Move toward it at a rate, not at a ratio
float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
transform.position = Vector3.Lerp(transform.position, targetPos, k);

// 4. Kill the drift once it is imperceptible
if (Quaternion.Angle(transform.rotation, targetRot) < 0.05f)
    transform.rotation = targetRot;
```

Exposed as `[Range(0.1f, 20f)] public float followSharpness = 3f;` — small values feel floaty, large
values feel rigid; ~3 is a visible but pleasant lag. **This is the main tuning knob for the feel.**

**Teaching note — `bodyDelta`: "how far has it rotated since rest".** You can compute the offset
between two rotations by multiplying one by the *inverse* of the other:
`catapult.transform.rotation * Quaternion.Inverse(catapult.RestRotation)`. The result is a rotation
you can apply to anything that was authored in the rest pose, and it will end up where it would be
if it had been welded to the catapult all along. This is the same trick as "subtracting" angles, but
it works in 3D without worrying about axes or wrapping.
*Why not just expose the yaw and rebuild it?* Because `RotateCatapultBody` writes a **world**
rotation (`transform.rotation = Quaternion.Euler(...)`), not a local one, so "the yaw" is not a
single number you can re-apply cleanly. `bodyDelta` is derived from the transforms themselves, so
it is correct regardless of how the launcher chooses to rotate the body. Fewer assumptions, less to
break.

**Teaching note — why `anchorRestWorld - RestPosition`.** A rotation about the catapult's origin
does not move the origin; it moves everything *around* it. To reproduce that for the camera you
rotate the **offset** (`anchorRestWorld - RestPosition`), then add the origin back. Rotating the
absolute world position instead would orbit the camera around the world origin — which looks vaguely
right at first and completely wrong the moment the catapult is not at `(0,0,0)`.

**Teaching note — why "rate, not ratio".** The naive version is
`transform.rotation = Quaternion.Slerp(current, target, 0.1f)` — but that 0.1 is *per frame*, so at
120 fps the camera catches up twice as fast as at 60 fps. The game feels different on every device.
`1 - exp(-sharpness * dt)` is the frame-rate-independent version: the fraction you cover depends on
**elapsed time**, not on how many frames elapsed. This is the same fixed-timestep idea used in
`CameraShakeManager`'s spring integration. If you only remember one line from this plan, remember
this one — it is the difference between "tuned on my machine" and "tuned".

**Teaching note — why `LateUpdate` and not `Update`.** Unity runs all `Update`s first, then all
`LateUpdate`s. `InputController` rotates the catapult in `Update`; if the camera also followed in
`Update`, the order between the two would be undefined and the camera would lag by a random extra
frame (visible as jitter). `LateUpdate` guarantees the camera reads the catapult's *final* pose for
this frame. This is exactly what `CameraShakeManager` already does — check its `LateUpdate`.

**Teaching note — why the position moves too, not just the rotation.** The anchor is offset from
the catapult's pivot (about 3.6 units to the side). Rotating the rig in place would orbit the camera
around its own position, not around the catapult — the view would swing sideways in a way that
reads as wrong at close range. Moving both the position and the rotation with the same eased
fraction keeps the anchor "attached" to the yawed catapult while still lagging.

*The behaviour this produces:* the catapult's body yaw leads; the camera chases it at
`followSharpness` and settles just behind it. Because the target is rebuilt from the catapult's
*actual* transform every frame, the camera also follows when the head resets after a shot (the body
yaw does not change on release, but `ResetCatapultBodyRotation` does reset it if you ever call it) —
no extra wiring needed.

**A subtlety: the follow must not fight a switch.** While a `MoveTo` coroutine is running, the
follow mode must be off, or both would write `transform.position` every frame. Keep one state:

```csharp
private enum RigState { Idle, Moving, Following }
```

`MoveTo` sets `Moving`; the coroutine's last line sets `Following` (and snaps to the anchor). The
follow block runs only when state is `Following`. `SnapTo` sets position/rotation instantly and goes
straight to `Following`.

**Teaching note — why a state enum beats three booleans.** With `_isMoving`, `_isFollowing` and
`_hasSnapped` you can create impossible states (moving *and* following). An `enum` makes the
states mutually exclusive by construction, and a `switch` on it reads as a description of the
system.

### 2.5 `Assets/Scripts/ScriptableObjects/Level/SO_LevelData.cs` — the per-level switch

```csharp
public enum AmmoMode { Shared, PerCatapult }

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
public class SO_LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();
    public AmmoMode ammoMode = AmmoMode.Shared;   // default keeps today's behaviour
}
```

**Teaching note — enums in ScriptableObjects.** Unity serialises an enum field as an integer and
draws it as a dropdown in the Inspector, so designers get a readable choice with no extra UI code.
Because `Shared` is the first value (0), every asset that already exists keeps `Shared` when the
field is added — old levels do not silently change behaviour.

**Teaching note — why `Shared` is the default.** Today there is one catapult and one pool; that *is*
"shared". Making `PerCatapult` the default would silently double the ammo of every existing level.

---

## Phase 3 — Refactors to the existing scripts

### 3.1 `AmmoManager` — from pool owner to pool holder

```csharp
using Core;

public class AmmoManager : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public event Action<SO_ProjectileType, int> OnAmmoChanged;

    public void AttachPool(AmmoPool<SO_ProjectileType> pool);
    public bool HasAmmo(SO_ProjectileType type);
    public int GetRemaining(SO_ProjectileType type);
    public int Consume(SO_ProjectileType type);
}
```

Changes:

* **Delete** the whole `Awake()` body — it no longer builds the type list and no longer calls
  `projectileSelector.Initialize(...)`. That call was the bug: two catapults would overwrite the
  global selection.
* **Delete the `levelData` field.** The level data now belongs to `CatapultManager` — one owner, one
  source of truth. Two components reading the same `SO_LevelData` and both deciding what it means is
  exactly how the current bug happened. (`AmmoManager` keeps `projectileSelector` because it needs
  it to answer "is the *currently selected* type available?" — but it must not be allowed to
  *change* the selection.)
* `AttachPool(pool)` stores the pool and subscribes to `pool.OnChanged`, re-raising
  `OnAmmoChanged(type, remaining)` for the UI. On attach it also raises the event **once** for the
  first type, so any listener that was already subscribed refreshes immediately.
  *Teaching note — why re-raise instead of exposing the pool's event directly.* The UI already
  talks to `AmmoManager` and knows nothing about `AmmoPool`. If `AmmoManager` simply forwarded the
  pool's event, the UI would need to know that a pool exists; by re-raising the *same* event
  signature it already uses, the UI keeps working unchanged. The `AmmoManager` stays the facade
  over the pool — same idea as `Catapult` being the facade over the launcher/spawner/ammo.*
* `Consume(type)` decrements **the type you pass**, not "whatever the selector currently points at".
* `DecreaseCurrentAmmo()` and `GetCurrentAmmoCount()` are removed — they encode the old bug.
  *Teaching note — `DecreaseAmmo` currently does `--_ammo[type]`, which throws
  `KeyNotFoundException` for a type that was never in the dictionary, and goes negative for a type
  that is already at 0. `Consume` should use `TryGetValue` and clamp at 0 instead.*

**Teaching note — the "wrong type" bug.** `Projectile.Shoot` used to call
`ammoManager.DecreaseCurrentAmmo()`, which looked up `projectileSelector.SelectedType` *at the
moment of the shot*. If the player switched type between loading and firing, the shot consumed ammo
from the wrong pool. Passing the type explicitly with the projectile removes the timing dependency
entirely.

### 3.2 `Projectile` — carry its own type

Add `public SO_ProjectileType projectileType;` and change `Shoot` to
`ammoManager.Consume(projectileType)`. The spawner sets `projectileType` when it instantiates.

### 3.3 `ProjectileSpawner` — stop throwing the type away

```csharp
public Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)
{
    ...
    _currentProjectile = InstantiateBullet(pType);   // <- was InstantiateBullet() with no argument
}
```

Also in `InstantiateBullet`:

* set `currentProjectile.projectileType = type;`
* **remove** the `projectileSelector.SelectNextWithAmmo(ammoManager.HasAmmo)` fallback. It made an
  *inactive* catapult change the *global* type selection when that catapult ran out of a type. The
  auto-switch now lives in the active catapult's launcher (3.4) and only ever considers the pool
  the player is actually shooting from.
* if the requested type has no ammo in this catapult's pool → return `null` without touching the
  global selection.

**Teaching note — why the fallback is dangerous here.** `InstantiateBullet` is called by *every*
catapult's launcher (each one subscribes to the global `OnSelectionChanged` so it can pre-load the
next projectile). In `PerCatapult` mode, catapult B might have 0 Boulder ammo while catapult A has
5. When the player selects Boulder, B's launcher calls `InstantiateBulletSafe(Boulder)`, hits the
"no ammo" branch, and calls `SelectNextWithAmmo` — which changes the **global** selection out from
under the player, who is aiming with A. With the fallback removed, B simply loads nothing and
waits; §3.4's reload-on-activate handles it.

### 3.4 `ProjectileLuncher` — load on demand, auto-switch after a shot

* **Expose the current body yaw** for the camera's follow mode (§2.4b). `RotateCatapultBody` already
  computes it:
  ```csharp
  public float CurrentYaw { get; private set; }   // degrees; the clamped value it applies
  ```
  Set it in `RotateCatapultBody` (`CurrentYaw = yRot;`) and initialise it to the rest yaw in `Start`.
  The rig does not strictly need it (it reads `transform.rotation` directly), but it is useful for
  debugging and for any future "how far am I aiming?" logic — and exposing it keeps `RotateCatapultBody`
  the single place that decides what the yaw is.
* Add a public `LoadProjectile()`:
  ```csharp
  public void LoadProjectile()
  {
      if (_isOnDelay || _isCatapultMoving) return;
      _activeProjectile = projectileSpawner.InstantiateBulletSafe(projectileSelector.SelectedType);
  }
  ```
  `HandleSelectionChanged` and `Start` both call it. `Start` also captures the rest head/body
  rotations as it does today.
  *Teaching note — why not rely on the event alone.* `ProjectileSelector.Initialize` raises
  `OnSelectionChanged`, but the launcher subscribes in `OnEnable`, and `OnEnable` may run before the
  manager has initialised the selector. Calling `LoadProjectile()` from `Start` (guaranteed to run
  after every `Awake`) removes the ordering dependency.*
  *Teaching note — why the guard is `if (_isOnDelay || _isCatapultMoving) return`.* This is the same
  guard the code already uses, and it exists for a reason: `DelaySpawn` re-spawns after the
  cooldown, and if the player is mid-drag at that moment the new projectile would replace the one
  they are about to fire. Keeping the guard in one place (`LoadProjectile`) means the three callers
  can never disagree about it.*
* **Reload when this catapult becomes active** — `CatapultManager.SetActive` calls
  `catapult.launcher.LoadProjectile()` on the new active catapult. This is what makes the
  `PerCatapult` mode work: catapult B's pool may have had 0 of the selected type, so B has no
  projectile loaded; on activation it loads one.
* **Reload after the cooldown** — `DelaySpawn` currently calls
  `projectileSpawner.InstantiateBulletSafe()` with no argument, which means "whatever the selector
  points at *now*". That is fine, but it must also go through `LoadProjectile()` so the same
  guards apply. Note the subtlety: `DelaySpawn` runs on the catapult that *fired*, which is
  always the active one, so this is safe.
* After a shot, if the fired type's ammo in this catapult's pool hit zero, switch the global
  selection to the next type that still has ammo *in this catapult's pool*:
  `projectileSelector.SelectNextWithAmmo(ammoManager.HasAmmo);`
  This preserves the behaviour documented in GDD §5.2 ("after the shot that empties a type, the game
  auto-switches to the next type with ammo").
  *Teaching note — `SelectNextWithAmmo` takes a `Func<SO_ProjectileType, bool>` predicate, not a
  list. That is what makes it usable with per-catapult pools: the caller decides **whose** ammo
  counts. Passing `ammoManager.HasAmmo` binds it to *this* catapult's pool; passing a method group
  of a different catapult would bind it to that one. This is a good example of a callback doing
  exactly one job: it defers the question "what counts as having ammo?" to the caller.*

### 3.5 `InputController` — drive the *active* catapult, and support the mouse

* **Move the component off the catapult root** onto the `CatapultManager` GameObject. Otherwise
  duplicating a catapult duplicates its `InputController` and you get N input handlers all aiming
  their own catapult.
* Replace `GetComponent<ProjectileLuncher>()` with a **serialized** reference
  `public CatapultManager catapultManager;`, and aim through
  `catapultManager.ActiveCatapult.launcher`.
  *Teaching note — serialized reference vs. `GetComponent`.* `GetComponent` searches *this* object
  and its children. Once the launcher lives on a different object (the catapult's head, a child of
  the root), `GetComponent` on the root still finds it — but that only works because of the
  hierarchy, and it breaks the moment the input handler is not on the catapult. A serialized field
  is explicit: you drag the manager onto the slot in the Inspector and you can see, at a glance,
  what it is wired to.*
* Unify touch and mouse into one pointer:

```csharp
using UnityEngine.EventSystems;

private bool TryGetPointer(out Vector2 position, out bool pressedThisFrame)
```

  * touch branch: `Input.touchCount > 0` → `Input.GetTouch(0)`
  * mouse branch: `Input.GetMouseButton(0)` → `Input.mousePosition`
  * `Input.GetMouseButtonDown(0)` / `GetMouseButtonUp(0)` for the press/release edges.

  *Teaching note — legacy `Input` vs the Input System.* The project has the Input System package
  installed and `activeInputHandler: 2` ("Both"), so the legacy `Input` API still works — including
  `Input.GetMouseButton`, which the Input System does not emulate for you. The new API would be
  `Mouse.current.leftButton` / `EnhancedTouch`, but switching the whole project over is a separate
  task; the goal here is only that the mouse works so you can test in the Editor.*
  *Teaching note — `Input.GetAxisRaw("Jump")` and `GetJumpBtn()` are dead code today (nothing reads
  the value). Delete them while you are in the file.*
* **Ignore drags that start on top of the UI.** This is not optional — it is a direct consequence of
  adding the arrow buttons. The legacy `Input.GetMouseButton(0)` fires whenever the button is
  pressed, *even if the uGUI button consumed the click*. So without a guard, tapping the right
  arrow would also start an aim-drag on the catapult you are trying to switch away from, and
  tapping a projectile in the carousel would nudge the catapult's aim.
  ```csharp
  // Ignore presses that land on a UI element (the arrows, the carousel, ...)
  if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      return;
  ```
  Put this at the top of the touch/mouse branch in `Update`, before the drag maths.
  *Teaching note — why the UI "consuming" the click does not protect you.* In uGUI, the
  `EventSystem` + `GraphicRaycaster` work out that a click landed on a `Button` and run that
  button's `onClick`. But the legacy `Input` API is a completely separate, lower-level system — it
  reports every raw press regardless of what the UI did with it. The two systems never talk to each
  other, so *you* have to ask "is the pointer over UI?" and skip the drag yourself.
  `EventSystem.current.IsPointerOverGameObject()` is the standard way to ask that question, and it
  works with the new Input System's `InputSystemUIInputModule` too.
  *Teaching note — a related one.* Because the `InputController` runs in `Update` and the uGUI
  `Button` runs its `onClick` from the `EventSystem`'s own update, the *order* between "the arrow
  switched the catapult" and "the drag rotated the catapult" is not guaranteed within a frame. The
  `IsPointerOverGameObject` guard removes the problem at the source, which is much better than
  trying to order the two updates.*
* Keep the existing sensitivity maths exactly as it is; only the pointer source changes.

### 3.6 `ProjectileCarouselUI` — follow the active catapult's pool

* Add `public CatapultManager catapultManager;`.
* Resolve the ammo source as "the active catapult's `AmmoManager`", falling back to a serialized
  `ammoManager` if there is no manager (keeps the component usable in a test scene). A small
  private property does this:
  ```csharp
  private AmmoManager ActiveAmmo => catapultManager && catapultManager.ActiveCatapult
      ? catapultManager.ActiveCatapult.ammoManager
      : ammoManager;
  ```
* **Re-point the event subscription when the active catapult changes.** This is the part that is
  easy to miss: today `OnEnable` does
  `ammoManager.OnAmmoChanged += HandleAmmoChanged;` — subscribed to *one* `AmmoManager` forever.
  When the player switches catapults, the carousel must **unsubscribe from the old pool and
  subscribe to the new one**, or it will keep showing catapult A's counts and will never hear about
  catapult B's shots.
  ```csharp
  private void OnActiveCatapultChanged(Catapult catapult)
  {
      UnsubscribeAmmo();
      SubscribeAmmo();
      RefreshAmmoDisplays();
  }
  ```
  *Teaching note — why the event subscription must be moved, not just refreshed.* `OnAmmoChanged`
  is a C# `event` — a list of callbacks. If you add a second subscription without removing the
  first, the callback fires **twice** per change (harmless here, but it is the classic leak) and the
  old `AmmoManager` keeps the carousel alive in memory even after the scene is torn down. This is
  why every `+=` in this project is paired with a matching `-=` in `OnDisable`.*
* In `PerCatapult` mode this is the visible difference: catapult A's exhausted Boulder shows `0` and
  is not clickable, while catapult B still shows `10`.
* **Do not** rebuild the whole carousel on a catapult switch — only the ammo numbers change. Call
  `RefreshAmmoDisplays()`; a full `Rebuild()` would make the row animate for no reason.
* The `interactable` state of a button is set at build time in `Rebuild` and refreshed in
  `RefreshAmmoDisplays` — make sure `RefreshAmmoDisplays` also updates `interactable` (it already
  does, line 189).

**Teaching note — `FindTypeForButton` reverse lookup.** The carousel finds a button's type by
re-parsing its GameObject name (`"ProjectileButton_" + type.displayName`). That breaks if two types
share a `displayName`. Since you are already in this file, replace the name round-trip with a
`Dictionary<Button, SO_ProjectileType>` filled during `Rebuild` — a small cleanup that removes a
whole class of bug. Keep the `Dictionary` keyed by `Button` (not by `GameObject`) so
`RefreshAmmoDisplays` can do `_buttonTypes[uiButton]` in one lookup.

---

## Phase 4 — UI: the arrows

### 4.1 A new small prefab — `Assets/Prefabs/UI/CatapultArrowButton.prefab`

Do **not** reuse `ProjectileTypeButton.prefab` — it is a 400×400 icon/name/ammo button and the arrows
need to be small and plain.

Create it from scratch in the Editor: `UI ▸ Button` (or an empty object with `Image` + `Button`),
plus a `TextMeshProUGUI` child with the glyph `<` (left) / `>` (right). Size it around **96×96**.

**Use `<` / `>` — not `‹` / `›` or `◀` / `▶`.** The project's TMP font asset
`LiberationSans SDF` has the ASCII `<` (0x3C) and `>` (0x3E), but it does **not** contain the
typographic `‹`/`›` (0x2039/0x203A) or the block arrows `◀`/`▶` (0x25C0/0x25B6). If you pick a glyph
the font lacks, TMP draws a hollow box instead of an arrow. The safest arrow is a plain `<` / `>`.
(If you want a real arrow asset instead, re-import a Kenney arrow PNG as a **Sprite** — the current
Kenney button/arrow PNGs are imported as plain *textures*, not `Sprite(2D)`, so they will not drop
into a uGUI `Image.m_Sprite` without re-importing them.)

Give the `Image` a white-ish tint (a `Button` with no sprite assigned already draws the built-in
white rectangle, so you can leave `Source Image` empty) and place the `TMP_Text` child over it.

### 4.2 A new scene object — `CatapultSwitcher` under the `UI` Canvas

```
UI (Canvas)
├── ScoreLable, Score              (existing, top)
├── ProjectileButtons              (existing, bottom strip: bottom-anchored, 400 tall)
└── CatapultSwitcher                (new)
    ├── LeftArrow    (Button + TMP "‹")
    ├── RightArrow   (Button + TMP "›")
    └── Counter      (TMP "1 / 2")
```

**Verified facts about the existing canvas** (from `SampleScene.unity`):

* `CanvasScaler`: `m_UiScaleMode: 0` (**Constant Pixel Size**), reference resolution `800×600`,
  `m_ScreenMatchMode: 0`, `m_MatchWidthOrHeight: 0`. → **1 canvas unit = 1 real pixel.** On a
  1080×2340 phone the canvas is 1080 wide and 2340 tall, and the UI does **not** scale with the
  screen. Keep the arrows small and anchored to the edges so they work at any size.
* `ProjectileButtons` (`RectTransform` `1685549658`): anchor min `(0,0)`, max `(1,0)` (bottom
  edge, full width), `sizeDelta (0, 400)`, `anchoredPosition (0, 400)` → it occupies the band
  **400 to 800 units up from the bottom of the screen**.
* `ProjectileCarouselUI` is on that same object (`contentRoot` points at itself) with
  `sideButtonWidth: 400`, `minVisibleSideButtons: 1`, `maxVisibleSideButtons: 3`. With three
  projectile types that is three 400-wide buttons in a row, centred on the canvas — they overflow
  the 800-wide reference horizontally but stay inside the 400-tall bottom band.

**Therefore the arrows belong at the left and right edges, at mid-height** — vertically clear of
both the score row (top) and the projectile strip (bottom band):

* `CatapultSwitcher`: `RectTransform` stretched to fill the canvas (anchors `0,0 → 1,1`,
  `sizeDelta (0,0)`), so its children can anchor to the screen edges.
* `LeftArrow`: anchor `(0, 0.5)`, pivot `(0, 0.5)`, anchored position `(24, 0)`, size `96×96`.
* `RightArrow`: anchor `(1, 0.5)`, pivot `(1, 0.5)`, anchored position `(-24, 0)`, size `96×96`.
* `Counter`: anchor `(0, 0)`, pivot `(0, 0)`, anchored position `(24, 24)` → bottom-left corner,
  below the projectile strip (which starts 400 units up). Text like `2 / 3`.

**Teaching note — anchors vs. positions.** An *anchor* is a normalised point on the parent
(`0,0` = bottom-left, `1,1` = top-right); the *anchored position* is the offset from that anchor.
So "anchor `(1, 0.5)`, anchored position `(-24, 0)`" means "24 units to the left of the right edge,
half way up" — and it stays there at any screen size, because the anchor is a *proportion* of the
parent while the offset is a constant. Use anchors for anything that should hug an edge, and fixed
positions only for things that are truly centred.

### 4.3 `Assets/Scripts/UI/CatapultSwitcherUI.cs`

```csharp
public class CatapultSwitcherUI : MonoBehaviour
{
    public CatapultManager manager;
    public Button leftButton;
    public Button rightButton;
    public TMP_Text counterText;
}
```

* `OnEnable` → subscribe to `manager.OnActiveCatapultChanged`, wire the buttons' `onClick` to
  `manager.PreviousCatapult()` / `manager.NextCatapult()`, and refresh.
* Refresh → `counterText.text = $"{index + 1} / {count}"`, and `leftButton.interactable =
  rightButton.interactable = count > 1`.
* **Wrap-around means the arrows are never disabled at the ends** — only when the level has a single
  catapult (where they would be noise). Do not grey them out when the counter reads `1 / 3` or
  `3 / 3`; pressing left from `1 / 3` must land on `3 / 3`.

**Teaching note — `Button.onClick` vs `EventTrigger`.** `onClick` is the simplest way to react to a
tap; `EventTrigger` is the general-purpose alternative for other gestures. For two plain buttons,
`onClick` is the right tool.

---

## Phase 5 — Scene & prefab wiring (Editor steps, in this order)

The order matters in two places, and both are the same lesson: **a duplicated hierarchy duplicates
its components and its internal references, but keeps external ones.**

### Step 1 — Move the two level-level components out of the catapult

1. Select the `ProjectileSelector` on the catapult **head** (`catapult` child) → `Ctrl+X` (Cut) →
   select the `CatapultManager` GameObject → `Ctrl+V`. Unity keeps the component and its GUID.
2. Move the `InputController` from the catapult root to the `CatapultManager` GameObject the same way.
3. Re-point the references that used to point at the head's selector:
   * `ProjectileLuncher.projectileSelector` → the manager's selector.
   * `ProjectileSpawner.projectileSelector` → the manager's selector.
   * `AmmoManager.projectileSelector` → the manager's selector.
   * `ProjectileCarouselUI.selector` → the manager's selector.

> **Do this before duplicating the catapult.** If you duplicate first, the copy brings its own
> `ProjectileSelector` component along and you end up with two selectors. Moving it out first means
> the duplicate's references still point at the single manager-level selector.

### Step 2 — Build the camera rig

1. Create an empty GameObject `CameraRig` at the scene root (sibling of the catapult, **not** a
   child of it).
2. Select the `Main Camera` prefab instance → drag it onto `CameraRig` (or `Ctrl+V` it there) so it
   becomes a child of the rig.
3. Set the camera's **local** position to `(0, 0, 0)` and **local** rotation to identity. The rig's own
   transform is now the camera's world pose.
4. Add `CatapultCameraRig` to `CameraRig` and set `moveDuration` (1.0–1.5 s feels good).
5. Add `CameraAnchor` to a new empty child of the catapult named `CameraAnchor`, and place it at the
   pose the camera used to have. The old camera was a child of the catapult root at local position
   `(3.59, 2.93, 0)` and local euler `(8.71, -90, 0)` — copy those numbers so the first catapult's
   view is unchanged.

### Step 3 — Add the `Catapult` component and the manager

1. Add `Catapult` to the catapult root; assign `launcher`, `spawner`, `ammoManager` (on the head) and
   `cameraAnchor`.
2. Create `CatapultManager` (empty GameObject) at the scene root; add the component; assign
   `levelData` (`DefaultLevelData`), `selector` and `cameraRig`.

### Step 4 — Duplicate the catapult

1. Select the catapult root → `Ctrl+D`. Move the copy (e.g. to the other side of the field) and give
   it a different yaw.
2. On the **copy**, add a `CameraAnchor` child and give it a different pose (or the same pose — your
   call, the point is that each catapult can have its own).
3. Check the copy's `ProjectileLuncher` / `ProjectileSpawner` / `AmmoManager` references: the
   launcher/spawner/ammo-manager must point at the **copy's own** components, and the selector at the
   **manager's** selector. Unity should have done this automatically — verify.

### Step 5 — The arrows

1. Create the two arrow buttons from `CatapultArrowButton.prefab` (or create them directly in the
   scene and save the prefab afterwards).
2. Parent them under `CatapultSwitcher` under the `UI` canvas; add `CatapultSwitcherUI` to
   `CatapultSwitcher`; wire `manager`, `leftButton`, `rightButton`, `counterText`.
3. Add `CatapultManager` to `ProjectileCarouselUI` (the `ProjectileButtons` object).

### Step 6 — Re-save the scene and check the console

Open `SampleScene`, make a trivial change, save. Then check the Console for exceptions.

---

## Phase 6 — GDD sync (required by `CLAUDE.md`)

`docs/GDD.md` — English headers, Persian body, truthful badges. After you finish the implementation,
update:

1. **§7 Multi-Catapult Levels** — badge `[Planned]` → `[Implemented]`. Rewrite the "انتخاب منجنیق"
   bullet: the switch is now the **arrows on the left/right of the screen** (with wrap-around), not
   tapping the catapult (tapping becomes `[Future]`). Add the camera behaviour (glide to the active
   catapult, straight or along a designer path; then a soft lagged follow of the aim) and the ammo
   modes (shared vs. per-catapult, set per level).
   The existing line **«محدودیت تیر: سقف کل مرحله است (نه به‌ازای هر منجنیق)»** must be **rewritten**,
   not just amended: with `ammoMode` the cap can now be either level-wide (Shared) or per-catapult
   (PerCatapult), and the level decides. Say so plainly, and keep the old sentence as the default
   behaviour (`Shared`).
2. **§5.2 مدیریت تیر** — add a bullet for the shared / per-catapult ammo mode, and correct the
   existing bullet that says the pool is "جدا برای هر پرابه" — that is per *type*, which is still
   true, but the new axis is per *catapult*. Keep the two ideas distinct so the doc does not become
   ambiguous.
3. **§3 Controls & Camera** — the mouse claim becomes true once Phase 3.5 lands; update the camera
   section from "دنبال‌کردن اکشن" to the rig + anchor + optional path + **soft aim follow with a
   configurable lag**.
4. **§9 Scoring** — the leftover bonus is still not wired to end-of-level; add a note that with
   per-catapult pools the bonus will have to be summed across the pools (do not mark it implemented).
5. **§10 Meta & UI** — add a row for the catapult switcher (arrows + `n / m` counter), marked
   `[Implemented]` **only once it is actually in the build**; leave it `[Planned]` until then.
6. **§2 Core Loop** — the first step currently reads `انتخاب منجنیق (در سطوح چندمنجنیقی)`. It is now
   real; keep the wording but it should read as an arrow switch rather than a tap.

---

## Verification

### Automated (EditMode)

`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`. The three new test files must pass and the
existing `CarouselWindowTests`, `CarouselLayoutCalculatorTests`, `LeftoverScoreCalculatorTests` must
still pass.

### Manual (in the Editor, Game view)

1. **One catapult** — press Play with the manager present but only one catapult. Aiming, firing and
   the carousel should behave exactly as before. The camera should be in the same *place*, but it now
   *lags* behind the aim instead of being welded to it (that is the intended change from §2.4b).
2. **Switching, with wrap-around** — click the right arrow: the active catapult changes, the camera
   glides behind it, and the counter goes `1 / 2 → 2 / 2`. From the last catapult, the right arrow
   must wrap to the first (`2 / 2 → 1 / 2`), and the left arrow must wrap the other way. The arrows
   are only non-interactable when there is exactly one catapult.
3. **Straight vs. path** — give catapult B three `pathPoints` empties in an arc, switch to B and
   confirm the camera follows the arc and arrives at the anchor's pose. Remove the points and
   confirm it goes straight.
4. **Ammo — Shared mode** (the default): fire from A until a type is empty, switch to B: the same
   type still shows `0`. Fire from A, switch to B: B's count is the same pool.
5. **Ammo — PerCatapult mode**: set `DefaultLevelData.ammoMode = PerCatapult`. Fire from A until
   Wood is `0`, switch to B: B still shows the full `10`. Both catapults shoot independently.
6. **The global type selection** — pick Boulder in the carousel; both catapults load a Boulder.
7. **Soft aim follow** — drag horizontally while aiming: the catapult yaws immediately, and the
   camera turns after it, visibly lagging by a fraction of a second and then settling. Tune
   `followSharpness` until it feels right (`1` ≈ floaty, `8` ≈ almost rigid). Stop aiming (release the
   drag) and confirm the camera glides back with the head as the head returns to rest.
8. **UI clicks do not aim** — tap the right arrow and drag slightly: the catapult must **not** rotate.
   Tap a projectile in the carousel: the catapult must not rotate either. (This is the
   `IsPointerOverGameObject` guard from §3.5.)
9. **Switching mid-drag** — start a drag on catapult A, and while still holding, click the arrow
   (multi-touch or a second mouse button). The active catapult changes cleanly, no exceptions.
10. **Rapid switching** — click the arrow three times quickly. The camera should re-target from its
    *current* pose each time (no snap-back), and the counter should end on the right number.
11. **An inactive catapult with a different pool** — in `PerCatapult` mode, empty Wood on A, switch
    to B: B must load a Wood projectile (it still has ammo) and the carousel shows B's counts.
    Empty Wood on B as well, then switch back to A: A has none either, and the game must not
    throw.

---

## Commit

Per your standing rule, nothing is committed automatically. Stage the work and review the message:

```
feat(catapult): add multi-catapult support with camera rig and per-level ammo modes
```

Files that should be in it: the new scripts, the modified scripts, `SampleScene.unity`, the new
prefab, `DefaultLevelData.asset`, the new EditMode tests, `docs/GDD.md`, and the plan/spec documents.

> **Be careful with `SampleScene.unity`.** It is a 13,000-line YAML file and Unity rewrites large
> parts of it when you save. Commit it **only** after opening and saving it from the Editor — never
> hand-edit it — and check `git diff --stat` before staging so you can see that the change is
> roughly the size you expect (a component move + a few new objects), not a wholesale rewrite.

---

## Appendix — concept cheat sheet

| Concept | Why it is here |
|---|---|
| **Assembly definition (`asmdef`)** | `CastleCrusher.Core` holds pure maths with no Unity-scene dependency, so it can be unit-tested in EditMode. An asmdef assembly **cannot reference the predefined `Assembly-CSharp`**, which is why `Core` code is generic — but it *can* use `Vector3`/`Mathf`, because `noEngineReferences` is `false`. |
| **Generics** | `AmmoPool<TKey>` keeps Core free of game types while staying fully typed at the call site. |
| **Unity lifecycle order** | `Awake` → `OnEnable` → `Start`, all `Awake`s before all `Start`s, but the relative order of two `Awake`s is undefined. Never depend on it; hand data over explicitly. |
| **Events (`Action<T>`)** | The manager announces "active catapult changed"; the camera and the UI subscribe. Nobody holds a hard reference to everybody else. |
| **Coroutine (`IEnumerator`)** | The only main-thread-safe way to animate over time in Unity. `async/await` resumes on a thread-pool thread by default. |
| **`Quaternion.Slerp`** | The correct way to interpolate rotations (shortest arc). `Vector3.Lerp` on Euler angles is not. |
| **Arc-length parameterisation** | Turns "progress along a curve" into "distance travelled", so the camera moves at a constant speed. |
| **Local vs. world transform** | `localPosition/localRotation` are relative to the parent; `position/rotation` are world-space. The anchor is authored locally and evaluated against the catapult's *rest* frame, so the **destination** of a switch is stable — the aim-follow (§2.4b) is a separate, deliberate lag applied on top. |
| **`FindObjectsByType` / `FindAnyObjectByType`** | Unity 6 replacements for the deprecated `FindObjectsOfType` / `FindObjectOfType`. `FindObjectsSortMode.None` skips sorting. |
| **`[FormerlySerializedAs]`** | Keeps scene data alive across a field rename. The alternative is re-saving the scene. |
| **ScriptableObject enum** | A designer-facing dropdown that serialises as an integer; the first value is what old assets keep. |
| **uGUI `Button` + `onClick`** | The standard way to make a UI element respond to a tap. |
| **`EventSystem.current.IsPointerOverGameObject()`** | The only reliable way to know whether the current press landed on the UI. Legacy `Input` and uGUI never talk to each other. |
| **`LateUpdate` ordering** | Every `Update` runs before every `LateUpdate`. Cameras read the final pose of the frame in `LateUpdate`. |
| **Frame-rate-independent smoothing** | `1 - exp(-k * dt)` covers the same fraction *per second* regardless of fps. Never use a bare per-frame `Slerp` factor for feel tuning. |
| **`Quaternion.Inverse`** | Rotation "subtraction": `current * Inverse(rest)` is "how far it has rotated since rest". |
| **Anchors vs. anchored position (uGUI)** | The anchor is a proportion of the parent (edge-hugging); the anchored position is a constant offset from it. |
