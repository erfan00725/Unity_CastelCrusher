# Castle Crusher

[![Unity](https://img.shields.io/badge/Unity%206-6000.6-black?logo=unity)](https://unity.com/releases/unity-6)
[![C#](https://img.shields.io/badge/C%23-5391FE?logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)

[![Play on itch.io](https://img.shields.io/badge/Play%20on-itch.io-FA5C5C?logo=itchdotio&logoColor=white)](YOUR_ITCH_URL_HERE)

![Castle Crusher gameplay](docs/screenshots/hero.png)

**Castle Crusher** is a mobile-first catapult siege game built in Unity 6. Drag to aim your catapult, release to launch, and demolish waves of orcs and destructible castle walls — all wrapped in juicy particle effects, camera shake, and punchy sound design.

## Features

- **Drag-to-aim catapult** — drag distance determines launch power, vertical drag sets the firing angle, and the catapult head springs back with a smooth animation on release
- **Health & destruction system** — destructible walls take damage, flash red as their health drops, and shatter on impact
- **Data-driven audio** — hit sounds for goblins, wood, and projectiles are configured through ScriptableObject audio assets, no code changes needed to retune them
- **Game feel** — impact particles, dynamic camera shake, low-health visual feedback, and a background soundtrack
- **Touch-first input** — built on Unity's Input System with mobile drag handling (also works with a mouse)
- **Projectile management** — spawner with projectile limits and cooldown-driven launching

## Play

Castle Crusher is available on itch.io:

**[▶ Play in your browser](YOUR_ITCH_URL_HERE)**

## Tech Stack

| | |
|---|---|
| **Engine** | Unity 6000.6 (Unity 6) with URP |
| **Language** | C# |
| **Input** | Unity Input System (touch-first) |
| **Assets** | [Kenney](https://kenney.nl) castle kit, mini-dungeon kit, and tower-defense kit |

## Screenshots

| Gameplay | Destruction |
|---|---|
| ![Gameplay screenshot](docs/screenshots/gameplay.png) | ![Destruction screenshot](docs/screenshots/destruction.png) |

## Getting Started

1. Clone the repository
2. Open the project in **Unity 6000.6 or newer** (Unity Hub will prompt the correct version)
3. Open `Assets/Scenes/SampleScene.unity`
4. Press **Play**

## License & Attribution

Source code is released for portfolio and educational purposes.

Art assets are from [Kenney.nl](https://kenney.nl) and are licensed under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Thanks, Kenney!
