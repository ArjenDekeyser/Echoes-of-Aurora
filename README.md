# Echoes of Aurora

This project is a real desktop 3D game prototype built with C# and Raylib. It is a PC game, not a browser app, and is intended to be a playable MVP inspired by the design brief for an Aurora exploration survival game.

## Run locally

From the project folder, run:

```bash
dotnet run
```

## Controls

- W/A/S/D: move
- Left Shift: sprint
- Right mouse: orbit camera
- E: interact / collect / activate
- 1: craft basic tool
- 2: craft Aurora scanner
- 3: craft shelter kit
- B: toggle build mode
- M: open field map
- J: open journal
- F5: save game to `aurora-save.json`
- F9: load game from `aurora-save.json`
- Left mouse / Space: attack with the basic tool
- 4: eat berries
- 5: drink water

## Included MVP features

- 3D third-person movement
- Forest terrain and simple shoreline environment
- Collectible resources
- Inventory and crafting
- Camp building via shelter placement
- NPC interaction and story progression
- Day/night cycle and atmospheric color shift
- Quest-style progression
- Multi-stage Aurora story with Mira, Pip and Orin
- Stamina sprint system
- Field map and journal overlays
- Local save/load support
- Night-active Gloom Stalker enemies
- Melee combat, damage and respawn
- Hunger, thirst and consumable survival resources
- Patrol and pursuit behavior for enemies
- Procedural idle animations and enemy health bars
- Native OBJ asset pipeline for player, NPC and enemy models
- Native WASAPI audio device with generated collection, combat and finale sounds
- Native desktop execution on Windows

## Notes

This is still a compact MVP, but it is an actual local PC game project and can be expanded into a larger production-ready game later.


