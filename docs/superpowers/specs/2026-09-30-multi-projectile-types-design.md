# Multi-Projectile Types — Design Spec

Date: 2026-09-30
Status: Approved design, pending implementation plan

## Goal

Add multiple projectile types (Boulder, Heavy, Explosive) with a HUD carousel for selection, per-type ammo pools, and a per-type leftover-score multiplier. Queue-based selection is explicitly out of scope for v1 (may come later).

## Decisions (from brainstorming)

| Topic | Decision |
|---|---|
| Architecture | Prefab per type + `SO_ProjectileType` descriptor (ScriptableObject) |
| v1 roster | Boulder (existing), Heavy, Explosive |
| Selection UX | Bottom-of-screen horizontal carousel; selected type in center at full size/opacity, side types smaller + semi-transparent; visible side buttons adapt to device width (1–3) |
| Ammo | Per-type ammo pool; HUD counter shows selected type's remaining ammo |
| Explosion reuse | Extract shared blast logic from `ExplosiveObject` into reusable form; projectile explosive reuses it |
| Score multiplier | Per-type, applies **only** to leftover-shot bonus; min 0, default 1; 0 = no score from leftover shots of that type |

---

## 1. Architecture & Data

### `SO_ProjectileType` (new ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Castle Crusher/Projectile Type")]
public class SO_ProjectileType : ScriptableObject
{
    public string displayName;        // "Boulder", "Heavy", "Explosive"
    public Sprite icon;               // HUD carousel button icon
    public int ammoCount;             // per-type pool size (default for loadout)
    public Projectile prefab;         // prefab instantiated on fire
    [Min(0f)]
    public float scoreMultiplier = 1f;// leftover-shot bonus weight; 0 = no bonus
}
```

### Prefabs per type

Each type reuses `Projectile.cs` with per-prefab tuning:

| Prefab | Differences vs Boulder |
|---|---|
| `Projectile_Boulder.prefab` | existing boulder, values unchanged |
| `Projectile_Heavy.prefab` | higher Rigidbody mass; higher `baseForcePower` (or lower launch speed for same momentum feel); bigger `shakeIntensity` |
| `Projectile_Explosive.prefab` | adds `ExplodeOnImpact` component; moderate mass |

---

## 2. Explosion on Impact

Split `ExplosiveObject`:

- Extract blast core (radius, maxForce, explosionDamage, upwardBias, affectedLayers + the OverlapSphere/damage/force loop) into a shared static helper or small component, e.g. `Explosion.TriggerBlast(Vector3 origin, BlastData data)` where `BlastData` is a plain serializable struct.
- `ExplosiveObject` (TNT) keeps current behavior — subscribes to `DOHealth.OnDeath`, calls `TriggerBlast`. **TNT behavior unchanged.**
- New `ExplodeOnImpact` (on the explosive projectile prefab): on `OnCollisionEnter` (after `Projectile`'s hit-sound / delay-destroy logic runs), calls `TriggerBlast` with its own values, spawns particles, shakes camera, destroys projectile.

Chain reactions stay free: a blast that kills a TNT block goes through the existing `DOHealth.OnDeath` → `ExplosiveObject.Explode` path.

---

## 3. Selection System & Spawner Flow

### `ProjectileSelector` (new component)

- Holds `List<SO_ProjectileType> availableTypes` + selected index
- `SelectNext()` / `SelectIndex(i)` — called by HUD carousel buttons
- Event `OnSelectionChanged(SO_ProjectileType)`

### `BulletSpawner` changes

- `InstantiateBullet(SO_ProjectileType type)` replaces the single serialized prefab field usage
- Ammo becomes `Dictionary<SO_ProjectileType, int>`, initialized per level from the loadout
- HUD `SetBulletCountText` shows selected type's remaining ammo
- When a type's ammo hits 0: its button grays out; after the shot that empties it, auto-switch to next type with ammo

### `BuletLuncher` changes

- `Start()` spawns initial bullet of the selected type; `DelaySpawn` respawns selected type after each shot
- Switching types allowed anytime **before aiming starts**; if a bullet is already loaded/being aimed, current bullet finishes as loaded type, next spawn uses new selection (no mid-drag swap)

---

## 4. HUD Carousel

```
ProjectileCarousel (horizontal, centered)
  ├── Button (type N-1)  [scale ~0.65, alpha ~0.4]
  ├── Button (selected)  [scale 1.0, alpha 1.0]
  └── Button (type N+1)  [scale ~0.65, alpha ~0.4]
```

- Adaptive side-button count based on device width: smaller devices show fewer, larger devices show more (max 3). Beyond the visible ones, swipe/arrow later if needed
- Each button: icon + small ammo-count badge; grayed/disabled at 0 ammo
- Clicking a side button selects that type and re-centers it
- Auto-select next available type if the selected type runs out

Adaptive visibility (on the carousel component):
- `maxVisibleSideButtons` (3) — hard cap
- `minVisibleSideButtons` (1) — floor for very narrow screens
- Recomputed on `RectTransform` resize / orientation change via width thresholds; each visible side button reserves a fixed slice of the canvas width, so the count = how many slices fit between the min and max bounds

Configurable parameters on the carousel component:
- `centerScale` (default 1.0)
- `sideScale` (default 0.65)
- `sideAlpha` (default 0.4)
- `animationDuration` (~0.15s centering tween)
- `minVisibleSideButtons` / `maxVisibleSideButtons` (adaptive range, see above)

---

## 5. Level Data & Loadout

```csharp
[CreateAssetMenu]
public class LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts;
}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}
```

Spawner/selector reads the level's loadout at level start. `ammoCount` here overrides the SO's default `ammoCount` per level.

---

## 6. Scoring (leftover bonus only)

```csharp
leftoverBonus = Σ over all types:
    remainingAmmo(type) × scorePerUnusedShot × type.scoreMultiplier
```

- `scorePerUnusedShot` — serialized field on `ScoreManager` (e.g. default 100, tune during balancing)
- `scoreMultiplier` lives on `SO_ProjectileType`; `[Min(0f)]`, default 1
- Destruction score stays untouched — multiplier does NOT affect damage/popups
- GDD section 9's "شلیک‌های باقی‌مانده" row becomes per-type weighted

---

## 7. Files

### New
| File | Purpose |
|---|---|
| `SO_ProjectileType.cs` | Descriptor SO (name, icon, ammo, prefab, scoreMultiplier) |
| `Explosion.cs` | Shared blast logic (extracted from ExplosiveObject) |
| `ExplodeOnImpact.cs` | Projectile component triggering blast on collision |
| `ProjectileSelector.cs` | Selection state + events |
| `ProjectileCarouselUI.cs` | Horizontal carousel with adaptive side-button count (device width) |
| `LevelData.cs` | Per-level projectile loadout |
| `Projectile_Boulder.prefab` | Existing boulder prefab (renamed/configured) |
| `Projectile_Heavy.prefab` | Heavy variant |
| `Projectile_Explosive.prefab` | Explosive variant with ExplodeOnImpact |
| `SO_ProjectileType` assets | Boulder, Heavy, Explosive |

### Modified
| File | Changes |
|---|---|
| `BulletSpawner.cs` | Per-type ammo dict, `InstantiateBullet(SO_ProjectileType)` |
| `BuletLuncher.cs` | Spawn selected type; pre-shot swap rule |
| `ExplosiveObject.cs` | Call shared `Explosion.TriggerBlast()` |
| `UIManager.cs` | Carousel hooks / ammo refresh |
| `ScoreManager.cs` | Leftover bonus with multipliers |

---

## Testing

- Manual scene tests: fire each type; verify heavy breaks stone (once stone exists), explosive blast damages neighbors
- TNT chain reaction still works after refactor
- Carousel: select each side button, run a type to 0 ammo, confirm auto-switch
- Adaptive carousel: verify side-button count changes across narrow/wide resolutions (phone portrait vs tablet landscape, Unity Device Simulator or manual Game view resizing)
- Score: verify leftover bonus math with mixed multipliers incl. 0
