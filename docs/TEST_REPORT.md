# Luna-Quest test report

**Build:** gameplay and voice pass, 14 November 2026
**Repository:** <https://github.com/Mostafa-Taher-git/Luna-Quest>
**Primary runtime:** Godot 4.7.2 .NET on Linux (Mesa Intel HD 4600, Vulkan)
**Secondary runtime:** Unity 6.3 LTS project, compile-checked only

## Automated simulation tests

```bash
dotnet run --project shared/LunaQuest.Tests/LunaQuest.Tests.csproj
```

Result: **426 passed, 0 failed**.

The suite covers:

- clock conversion, lunar day mapping and determinism;
- power, water, air, food, thermal, dust, radiation and comms conservation;
- crop growth, harvest, food reserve and plant water/CO₂ exchange;
- incidents, safe-next-step evidence and learning entries;
- NPC trust, memory and schedule state;
- save round-trips, schema rejection and corrupt-file rejection;
- every console in `buildings.json` mapping to a real action;
- comms desk, antenna, airlock dust, grow lights, medical check-up and regolith intake;
- a full 30-day simulated mission with finite, in-range resources.

## Engine builds

| Check | Result |
|---|---|
| Godot C# build | 0 errors, 0 warnings |
| Unity adapter compile check | 0 errors, nullable-analysis warnings only |
| Godot asset import | completed; 55 voice MP3s imported |
| Voice pack key/file check (`--voice-check`) | 55 lines, 0 problems |
| Voice playback path (`--voice-demo`) | `playing=True` for Dr. Safaa's greeting |

The Unity check compiles `Assets/Scripts/**/*.cs` against the `UnityEngine.Modules`
2021.3.33 reference assembly set. It is not a substitute for opening the project in a Unity
6.3 editor, which is not installed in this environment.

## Live Godot checks

Commands used:

```bash
Godot --path engines/godot/LunaQuest.Godot --autostart \
  --screenshot=/tmp/opencode/verify.png --shot-delay=6
Godot --path engines/godot/LunaQuest.Godot --autostart --first-person \
  --screenshot=/tmp/opencode/fp.png --shot-delay=6
Godot --path engines/godot/LunaQuest.Godot --autostart --first-person \
  --spawn=-2.9,15.2 --look=0,0 \
  --screenshot=/tmp/opencode/station.png --shot-delay=6
Godot --path engines/godot/LunaQuest.Godot --autostart --voice-demo \
  --first-person --screenshot=/tmp/opencode/demo.png --shot-delay=7
```

Observed:

- the colony builds with 16 buildings, 8 crew and 15 incident rules;
- third-person camera shows the astronaut in the colony;
- first-person camera shows the helmet overlay and crosshair;
- walking up to a station shows the short `E <station>` prompt;
- the pause screen stops the clock (resource draws are behind the pause check);
- the voice demo printed `playing=True` and the subtitle appeared in the HUD.

Screenshots from this pass are kept outside the repository under `/tmp/opencode/` during
testing. The repository screenshot in `docs/screenshots/colony.png` predates this pass.

## Manual checks still recommended

These need a human at the keyboard or a Unity editor:

- subjective audio balance between voice, ambient hum and a quiet room;
- mouse-look feel at different sensitivities and frame rates;
- jumping and slope handling on the actual level geometry;
- all eight crew greetings in sequence, in both languages;
- Unity Play Mode and its IMGUI layout at 16:9 and ultrawide;
- save produced by Godot loaded in Unity, and the reverse.

## Known limitations

- Unity has not been opened in this environment.
- The voice pack is English speech with Arabic subtitles; there is no Arabic TTS.
- Dialogue remains bark-based: a greeting and four rotating lines per NPC, not a branching
  conversation tree.
- The 30-day mission is a deterministic single-zone approximation, not a flight-certified
  life-support model.
