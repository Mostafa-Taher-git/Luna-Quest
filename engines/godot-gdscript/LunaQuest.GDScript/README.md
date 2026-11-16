# Luna-Quest — the GDScript edition

The same game as the Godot .NET and Unity projects, written in plain GDScript and built with a
stock Godot 4.7.2 editor. No .NET SDK, no Mono, no C# toolchain, no compilation step: open the
folder, press **F5**, and you are standing on the Shackleton rim with a colony to keep alive.

It exists for two reasons. The .NET build cannot be exported to the web — Godot's web export has
no .NET support — so this edition is what CI ships as `LunaQuest-GDScript-Web.zip`. And opening the
game to change one number should not require installing a compiler.

```
engines/godot-gdscript/LunaQuest.GDScript
├── project.godot            stock Godot 4.7.2, Compatibility renderer, 1600×900
├── scenes/Main.tscn         one Node3D; every other node is built in code
├── scripts/
│   ├── content.gd           autoload Content — reads data/*.json, the source of truth
│   ├── clock.gd             the mission clock: 12 simulated minutes per real second
│   ├── sim.gd               the rule set, ported from shared/LunaQuest.Core and the
│   │                         retired browser sim (now archive/web/src/sim)
│   ├── world.gd             the colony: terrain, sixteen modules, consoles, rovers, perimeter
│   ├── player.gd            the commander, at 1.62 m/s² and 1.66 m eye height
│   ├── crew.gd              six crew and NEXO, on their schedule, wearing their own trim colours
│   ├── hud.gd               gauges, prompts, subtitles, journal, title card, mission report
│   ├── voice.gd             autoload Voice — plays assets/voice by key
│   ├── main.gd              the session: intro, fixed step, input, save/load, the end
│   ├── selftest.gd          flies thirty days and checks every rule (CI gates on it)
│   └── audit.gd             walks the finished colony with physics queries (CI gates on it)
├── data/*.json              the shared content, copied from ../../content by the sync script
└── assets/                  astronaut, kit models, intro film, voice pack (synced, not committed)
```

## Running it

1. Install [Godot 4.7.2 **standard**](https://godotengine.org/download) — *not* the .NET build.
2. From the repository root, copy the shared assets in once:

   ```bash
   bash tools/sync-gdscript-assets.sh
   ```

   The 77 MB of astronaut, kit models, intro film and voice lines live in the .NET project; the
   script copies them and `content/*.json` into this project. Without them the game still runs —
   crew fall back to a blocky emergency suit and the intro is skipped — but run it.
3. Open `engines/godot-gdscript/LunaQuest.GDScript/project.godot` and press **F5**.

### Controls

| | |
|---|---|
| `W A S D` | walk (hold `Shift` to run) |
| Mouse | look; click to capture the pointer |
| `Space` | hop — lunar gravity, about a metre and a second of hang time |
| `E` | use the console you are looking at, or talk to a crew member |
| `V` | first / third person |
| `J` | journal — every task you have finished, with the skill it credited |
| `H` | controls |
| `Tab` or `T` | hold to fast-forward the mission |
| `L` | English / العربية |
| `F5` / `F9` | save / load (`user://lunaquest-save.json`) |
| `F11` | fullscreen |
| `Esc` | pause |

## The mission

Thirty Earth days at 89.9° S, six crew, one commander, one robot. The simulation runs on a fixed
one-second step, twelve simulated minutes per real second, so a full mission is about an hour of
walking around — and `Tab` compresses it.

Nothing is a fail state. Power, water, air, food and heat are gauges you watch and adjust, not a
score you lose: let the battery fall and the colony gets cold and the crew gets tired, and the HUD
turns amber. Read a console, change one variable, wait, read it again. That is the whole game, and
it is the same loop as the other two editions — the incident table, the fifteen scripted pressure
beats, the eleven learning objectives, the forty-four dialogue topics and the ninety-four voice
lines all come from `content/*.json`, not from code.

## Save files

Schema 2, identical keys to `shared/LunaQuest.Core` and the browser edition. A save written by
this build loads in the Godot .NET build and vice versa.

## Verification

There is no GPU in CI, so the build is checked two ways that do not need one:

```bash
godot --headless --path . --import                     # parses every script, imports every asset
godot --headless --path . -- --selftest                # 544 checks: the whole thirty days
godot --headless --path . -- --audit                   # 190 checks: doorways, consoles, spawn, crew, HUD
godot --headless --path . --quit-after 400             # boots the real scene and runs it
```

`--selftest` runs the full mission, asserts every scripted beat fires and every tank stays finite,
opens all forty-five consoles, tries all eighteen actions, talks through all forty-four topics,
verifies the voice pack resolves, and round-trips a save through JSON into a fresh simulation.
`--audit` then walks the built scene: a ray through every doorway, the crosshair on every console
from where a player stands, the spawn point clear and floored, no crew member inside a wall, and
the traverse markers closed on twelve headings.

Both run in `.github/workflows/godot-gdscript.yml` before anything is exported, and the same
workflow exports Web, Windows and Linux. `release.yml` attaches all three to the GitHub Release.
