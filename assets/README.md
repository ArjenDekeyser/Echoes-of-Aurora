# Echoes of Aurora assets

The OBJ files in this folder are the first native model assets used by the Raylib renderer:

- `player.obj`: player character mesh
- `aurora_npc.obj`: reusable NPC mesh
- `gloom_stalker.obj`: enemy mesh

`Program.cs` loads these files from the build output and falls back to primitive geometry if an asset is missing. Production art can replace these files while keeping the gameplay code and asset contract unchanged.
