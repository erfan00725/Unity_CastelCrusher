# CLAUDE.md — Castle Crusher

Instructions for AI assistants working in this repository.

## Project

**Castle Crusher** is a mobile-first 3D catapult-siege game inspired by Angry Birds, built in **Unity 6 (URP)** with **C#**. The player drag-aims catapults, launches projectiles, and destroys blocks and enemies. Goal: kill all enemies with the fewest shots and the most destruction (remaining shots + destruction = score).

Art assets are from [Kenney.nl](https://kenney.nl) (CC0).

## Tech Stack

- **Engine:** Unity 6000.6+ with URP
- **Language:** C#
- **Input:** Unity Input System (touch-first, mouse supported)
- **Data:** ScriptableObject-driven audio configs (`Assets/ScriptableObjects/`)

## Structure

```
Assets/
  Scripts/
    InputController.cs        # drag-to-aim / touch input
    ScoreManager.cs           # score tracking
    CameraShakeManager.cs     # shake on shoot/impact
    Projectile/               # Projectile, BulletSpawner, BuletLuncher,
                              # particle + sound managers for projectiles
    DestructableObjects/      # DOHealth, DOManageImpact,
                              # DOLowHealthRedTint, DOSoundManager
    UI/                       # UIManager, ScorePopup
  Prefabs/
    Building/                 # walls, towers (destructible structures)
    Enemy/                    # character-orc, etc.
    UI/                       # ScorePopup
    weapon-ammo-boulder.prefab
  ScriptableObjects/
    Audio/                    # SO_AudioConfigBase + hit SFX assets
  Scenes/
    SampleScene.unity         # main (only) scene
  Sounds/  SFX/  Animators/   # audio + animator assets
docs/
  GDD.md                      # Game Design Document — source of truth for design
README.md
```

## GDD Sync Rule (important)

**When a change notably alters game design, update `docs/GDD.md` in the same session.**

Notably altering changes include:
- adding or removing a mechanic (e.g. multi-catapult switching, new projectile behavior)
- changing scoring, win/lose, or progression rules
- adding/removing block types, projectile types, or enemy types
- changing level structure or meta systems (menus, stars, unlocks)

When updating the GDD:
1. Edit the affected section's content (Persian body under English headers).
2. Keep the **status badge** truthful: `[Implemented]` · `[Partial]` · `[Planned]` · `[Future]`.
3. Do **not** invent features the user hasn't decided — leave those under `Future / Not Decided`.

Notable changes that do **not** require a GDD edit: pure refactors, bug fixes that restore intended behavior, cosmetic/VFX tuning, code comments, asset swaps that don't change design.

## Conventions

- GDD language: **English headers, Persian body**. CLAUDE.md and code stay English.
- Status badges must reflect reality — never mark something `[Implemented]` unless it exists in the build.
- Commit messages: plain imperative English, end with `Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>` when created with AI assistance.
