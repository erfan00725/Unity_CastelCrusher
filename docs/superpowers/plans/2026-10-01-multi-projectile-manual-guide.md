# Multi-Projectile Types — Manual Implementation Guide

A human-friendly walkthrough. Full code for every file is in the plan:
`docs/superpowers/plans/2026-09-30-multi-projectile-types.md` (task numbers below refer to it).
Design decisions: `docs/superpowers/specs/2026-09-30-multi-projectile-types-design.md`

Work in order. Each phase ends with something you can test.

---

## Phase 1 — Code files (copy from plan, then compile)

Create these files, copying the code blocks from the referenced plan tasks:

| # | File | From plan | Notes |
|---|---|---|---|
| 1 | `Assets/Scripts/Core/CastleCrusher.Core.asmdef` | Task 1 Step 1 | plain JSON |
| 2 | `Assets/Tests/EditMode/CastleCrusher.EditModeTests.asmdef` | Task 1 Step 2 | plain JSON |
| 3 | `Assets/Tests/EditMode/LeftoverScoreCalculatorTests.cs` | Task 1 Step 3 | test |
| 4 | `Assets/Scripts/Core/LeftoverScoreCalculator.cs` | Task 1 Step 5 | |
| 5 | `Assets/Tests/EditMode/CarouselLayoutCalculatorTests.cs` | Task 2 Step 1 | test |
| 6 | `Assets/Scripts/Core/CarouselLayoutCalculator.cs` | Task 2 Step 3 | |
| 7 | `Assets/Scripts/DestructableObjects/SpecialObjects/Explosive/Explosion.cs` | Task 3 Step 1 | contains `BlastData` struct |
| 8 | `Assets/Scripts/Projectile/ExplodeOnImpact.cs` | Task 4 Step 1 | |
| 9 | `Assets/Scripts/Projectile/SO_ProjectileType.cs` | Task 5 Step 1 | |
| 10 | `Assets/Scripts/Level/LevelData.cs` | Task 5 Step 1 | |
| 11 | `Assets/Scripts/Projectile/ProjectileSelector.cs` | Task 6 Step 1 | |
| 12 | `Assets/Scripts/UI/ProjectileCarouselUI.cs` | Tasks 10 + 11 | **write the Task 11 version directly** (with `Rebuild(bool animate)`, resize handling, tween, ammo refresh). Task 10 is the intermediate version — skip it. |
| 13 | `Assets/Scripts/Core/CarouselWindow.cs` | Task 9 Step 3 | |
| 14 | `Assets/Tests/EditMode/CarouselWindowTests.cs` | Task 9 Step 1 | test |

**Then edit two existing files:**

- **`ExplosiveObject.cs`** (Task 3 Step 2): replace the body of `Explode()` with the `TriggerBlast` call + `BuildBlastData()`. ⚠️ Do **not** rename or remove any serialized fields — scene values survive only if the names stay identical.
- **`BulletSpawner.cs`** (Task 7 Step 1): full rewrite as shown. ⚠️ If you have `BuletLuncher` compile errors, fix only that reference — the signature stays `InstantiateBullet()`.

**Checkpoints:**
- [ ] Unity compiles with no errors (Expect a one-time "missing field projectilePrefab" Inspector warning on BulletSpawner — harmless, cleaned in Phase 2)
- [ ] Test Runner → EditMode → Run All: **15/15 pass** (4 leftover + 6 layout + 5 window)
- [ ] Commit checkpoint (your call when): `add core calculators, explosion extraction and projectile type system`

---

## Phase 2 — Unity Editor work (plan Task 12)

### 2.1 Projectile prefabs
1. Duplicate `Assets/Prefabs/weapon-ammo-boulder.prefab` ×3 → `Assets/Prefabs/Projectile/Projectile_Boulder / _Heavy / _Explosive`.
2. **Boulder:** leave untouched.
3. **Heavy:** Rigidbody Mass = **3**; `Projectile.shakeIntensity` = **1.5** (baseForcePower stays 1500).
4. **Explosive:** Mass = 1. Add `ExplodeOnImpact`. Copy the blast values you see on a scene TNT block's `ExplosiveObject` as reference, then set the projectile's: radius 2.5, maxForce 150, damage 120, upwardBias 0.3, `affectedLayers` = the **Physical** layer mask (same as TNT uses). Assign TNT's explosion particle to `explosionParticle`. Add `AudioSource` (2D, Play On Awake off) + `ExplosionSoundManager` with TNT's explosion audio config.

### 2.2 ScriptableObjects
Create → Castle Crusher → Projectile Type, ×3:

| Asset | displayName | ammoCount | prefab | scoreMultiplier |
|---|---|---|---|---|
| Boulder | Boulder | 8 | Projectile_Boulder | 1 |
| Heavy | Heavy | 3 | Projectile_Heavy | 1 |
| Explosive | Explosive | 2 | Projectile_Explosive | 1 |

Create → Castle Crusher → Level Data → `LevelData_01` with those 3 entries in the same order.

### 2.3 Button prefab
1. In the scene: UI → Button (TextMeshPro). Root: rename `ProjectileTypeButton`, size 100×100, add `CanvasGroup`.
2. Children (exact names — code looks them up by name):
   - `Icon` — Image 48×48 centered (placeholder sprite ok)
   - `Ammo` — TMP text, bottom-center, fontSize 20
   - `Name` — TMP text, top-center, fontSize 14
3. Drag into `Assets/Prefabs/UI/`, delete the scene copy.

### 2.4 Carousel on Canvas
1. Empty UI child of the Canvas: `ProjectileCarousel`.
   ⚠️ **Anchor: stretch horizontally** (Min (0,0), Max (1,0)), Pivot (0.5, 0), Pos (0, 20), Height 130, Left/Right 0. The adaptive side-count reads this width — a fixed width would break it.
2. Add `ProjectileCarouselUI`; wire `selector`, `spawner` (next step), `contentRoot` = its own RectTransform, `buttonPrefab` = ProjectileTypeButton.

### 2.5 Gameplay wiring
1. On the catapult object (has `BuletLuncher` + `BulletSpawner`): add `ProjectileSelector`.
2. `BulletSpawner`: `levelData` = LevelData_01, `selector` = that component, `uiManager` = existing reference. Clear the stale `projectilePrefab` slot if shown.
3. Leave `ProjectileSelector.availableTypes` empty — the spawner fills it from LevelData on Awake.

**Checkpoint:** Enter Play briefly — no console errors, boulder loads, carousel shows 3 buttons (Boulder centered).

---

## Phase 3 — Verify (plan Task 13)

- [ ] **Each type shoots:** Boulder = old behavior; Heavy = heavier + stronger shake; Explosive = blast/particle/sound on first impact, pushes + damages nearby, TNT chains still work.
- [ ] **HUD counter** shows selected type's ammo, decrements per shot.
- [ ] **Carousel:** tap side button → promotes with tween; run a type to 0 → button grays out + auto-switch to next with ammo.
- [ ] **Adaptive width:** Game view 375 wide → 1 side button; 640 → 2; 1920 → 3, live while playing.
- [ ] Any fix → re-test the affected check.

---

## Phase 4 — GDD sync (plan Task 14, required by CLAUDE.md)

- [ ] §5.1: Heavy + Explosive → `[Implemented]`
- [ ] §5.2: replace "selection undecided" bullet with the carousel description + per-type ammo note, `[Implemented]`
- [ ] §9: leftover row → per-type weighted formula, `[Partial]` (needs results screen to apply); add scoreMultiplier note
- [ ] §12: remove "چیدمان دقیق انتخاب پرابه" checkbox
