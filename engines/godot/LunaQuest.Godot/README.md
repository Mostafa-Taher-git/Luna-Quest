# Luna-Quest — Godot adapter

This is the Godot 4.7.2 .NET side of Luna-Quest. It is a complete project: the world is built
procedurally at runtime, the astronaut is the repaired Sketchfab GLB driven by looping idle
and walk cycles, and the HUD is built in C# with Godot `Control` nodes.

---

## Open and run

1. Install **Godot 4.7.2 .NET (mono)** and a .NET SDK (8 or newer).
2. Open this folder in Godot: `engines/godot/LunaQuest.Godot/project.godot`.
3. Wait for import, then press **F5**.

From a terminal:

```bash
dotnet build engines/godot/LunaQuest.Godot/LunaQuest.Godot.csproj

/path/to/Godot_v4.7.2-stable_mono_linux.x86_64 \
  --path engines/godot/LunaQuest.Godot
```

The first open generates `.godot/` and the import cache. Those folders are ignored by git and
can be deleted at any time; Godot rebuilds them.

---

## Project layout

```text
LunaQuest.Godot/
├── project.godot                 main settings, main scene, lunar gravity
├── LunaQuest.Godot.csproj        net8.0, references shared/LunaQuest.Core
├── scenes/Main.tscn              one Node3D with Main.cs
├── scripts/
│   ├── Main.cs                   entry point, save/load, input, dev flags
│   ├── WorldBuilder.cs           procedural colony, terrain, sky, astronaut
│   ├── PlayerController.cs       CharacterBody3D, camera, lunar movement
│   ├── Hud.cs                    programmatic HUD, journal, resource math
│   ├── NpcAgent.cs               crew schedules and labels
│   ├── VoicePlayer.cs            MP3 barks + subtitles
│   ├── Interactable.cs           station interaction wrapper
│   └── L.cs                      bilingual helper and font selection
├── data/                         six content JSON files (synced from content/)
└── assets/
    ├── astronaut/cute_astronaut.glb
    ├── textures/earth.jpg
    ├── voice/                    MP3 pack + subtitles JSON
    └── noto-sans-*.woff          UI fonts
```

`data/` and `assets/` are copied by `tools/sync-content.sh` from the repository root. The
source of truth is `content/` and `assets/`; edit those, then run the sync script.

The core simulation is not copied into this project. The `.csproj` references
`shared/LunaQuest.Core/LunaQuest.Core.csproj` directly, so Godot and the tests compile the
same source.

---

## Controls

| Action | Key |
|---|---|
| Move | `W A S D` |
| Run | `Shift` |
| Jump | `Space` |
| Look | Mouse |
| Use station | `E` |
| First / third person | `V` |
| Journal | `J` |
| Resource math | `G` |
| Help | `H` |
| Save / load | `F5` / `F9` |
| Language | `L` |
| Release mouse / close panel | `Esc` |

Click the window to capture the mouse. The start screen takes the name before the simulation
begins.

---

## Developer flags

```bash
# Skip the name screen and take a screenshot after six seconds
Godot --path engines/godot/LunaQuest.Godot \
  --autostart --screenshot=/tmp/luna.png --shot-delay=6

# Close-up portrait for astronaut checks
Godot --path engines/godot/LunaQuest.Godot \
  --autostart --portrait --screenshot=/tmp/portrait.png --shot-delay=5
```

These flags are inert during normal play. The screenshot path can be absolute or a
`res://`/`user://` path.

---

## Save file

`user://lunaquest-save.json`

Schema version 2, shared with the Unity adapter. On Linux this resolves under the Godot user
data directory. The mission autosaves when the day changes; `F5` and `F9` save and load
immediately.

---

## Notes

- The world is procedural. There are no imported scenes or prefabs beyond `Main.tscn`; this
  keeps the project robust when files are copied through services that drop editor metadata.
- The astronaut GLB is repaired and driven by `AstronautAnimator`. The source file ships one
  long Sketchfab action; the animator loops two sections of it as `LunaQuestIdle`
  (`0.75–1.75 s`) and `LunaQuestWalk` (`2.50–5.00 s`). The walk section is one complete,
  seamlessly repeating cycle, and its playback speed scales with ground speed. The player and
  every crew member use the same rig.
- The standing pose has the visor facing `+Z`. The player visual is turned 180° so the trailing
  camera sees the back and the backpack.
- The HUD is bilingual. The `L` key switches English and Arabic; Arabic uses the Noto Naskh
  Arabic font and right-to-left layout.
- The game uses Godot’s built-in `FontFile`/`Font` loading from `.woff` assets. If the fonts
  are missing, the theme falls back to Godot’s default font.
- The voice pack is inherited from the team’s V1 prototype; its redistribution rights are
  unconfirmed. The source repository is public, but keep shipped builds private until
  cleared.

See the repository root `README.md`, `CREDITS.md` and `LICENSE` for the full project notes.
