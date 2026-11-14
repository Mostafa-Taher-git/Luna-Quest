# Luna-Quest test report

**Build:** v3.2 — GDScript port, browser edition, crew and colony art pass, release automation, 14 November 2026  
**Repository:** <https://github.com/Mostafa-Taher-git/Luna-Quest>  
**Primary runtime:** Godot 4.7.2 .NET on Linux (Mesa Intel HD 4600, Vulkan)  
**Secondary runtime:** Unity 6.3 LTS project (dual-engine architecture)  
**Third runtime:** Browser (Three.js + Vite), headless Chromium verification  
**Fourth runtime:** Stock Godot 4.7.2 GDScript (no .NET), headless import, self-test, scene audit and Web/Windows/Linux exports

## Browser edition (Three.js)

The browser build is the one that needs no install, so it is tested on its own terms.

```bash
cd web
node --test test/*.test.mjs     # simulation, world build, content wiring
node ../tools/check-content.mjs # content/JSON consistency
npm run build                   # production bundle
```

| Check | Result |
|---|---|
| `web` unit tests | **36 passed, 0 failed** (`sim.test.mjs`, `world.test.mjs`) |
| Content consistency (`tools/check-content.mjs`) | exit 0 — 16 buildings, 7 crew, 15 incident rules, 11 objectives, 17 research notes, 44 dialogue topics, 94 voiced lines |
| Production build | `web/dist` builds clean: 811 kB JS (219 kB gzip), 12 kB CSS, Noto Sans Latin and Arabic |
| Static payload | ~60 MB — the Sketchfab kit models and the intro film dominate; only the kit stems referenced by `props.js` are copied |
| Headless Chromium run | boots, plays the intro film, reaches the start screen and a playable colony with no page errors |
| Console on load | `16 buildings, 7 crew, 36 kit props, 26 rocks, 45 consoles`; `dressing 15 materials from 369 meshes (0 kept whole)` |
| Render budget | 552 draw calls, 957 k triangles at quality 2 (colony 346 meshes) |

What the headless run verified, in a real Chromium with SwiftShader:

- the intro film plays (VP9/WebM, H.264/MP4 as a second `<source>`; the fallback matters —
  Chromium's bundled build refuses the MP4);
- the start screen, the HUD, the journal and the pause screen all render;
- a walkable colony: 16 buildings with interiors, 45 consoles, 36 kit props, 26 rocks, and the
  paved plaza spine;
- all seven NPCs stand at 1.75 m with their feet on the surface (measured by raycast, not by
  eye — an earlier build had them four times too tall because a rig's bind pose was measured
  instead of its skeleton);
- the Cute Astronaut is the only human model, dressed with the fabric map, a painted visor
  (a mirrored material renders black without an environment probe), the orange visor frame,
  chest unit and life-support pack;
- quality adaptation steps 2 → 1 → 0 (shadow map off, then the far terrain band off).

### Known limitations of the web edition

- SwiftShader is software rendering; the measured frame rate there (about 1 fps) says nothing
  about a real GPU. Performance work was judged by draw calls and triangle counts instead.
- The Cute Astronaut GLB is a rigid mesh with a folded-legged pose and a stray visor copy
  parked several metres from the body; the suit is posed as authored, and the visor is rebuilt
  in-engine.
- Effects are conservative on purpose: no point lights per room, no transmission glass, no
  post-processing, so the build stays playable on integrated graphics.

---

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
- 8-crew NPC trust, memory and schedule state;
- save round-trips, schema rejection and corrupt-file rejection;
- every console in `buildings.json` mapping to a real action;
- comms desk, antenna, airlock dust, grow lights, medical check-up and regolith intake;
- a full 30-day simulated mission with finite, in-range resources.

## Engine builds

| Check | Result |
|---|---|
| Godot C# build | 0 errors, 0 warnings |
| Unity adapter compile check | 0 errors; IMGUI dialogue and voice system synchronized |
| Godot asset import | completed; 103 voice MP3s imported |
| Voice pack key/file check (`--voice-check`) | 103 lines, 0 problems |
| Dialogue UI rendering (`--test-dialogue=npc_safaa`) | verified; renders profile, skills, responsibilities, greeting, and 6 topics |
| Voice playback path (`--voice-demo`) | `playing=True` for Dr. Safaa's greeting |

## Live Godot checks

Commands used:

```bash
Godot --path engines/godot/LunaQuest.Godot --auto-start \
  --screenshot=/tmp/game_screenshot.png --shot-delay=3
Godot --path engines/godot/LunaQuest.Godot --test-dialogue=npc_safaa \
  --screenshot=/tmp/dialogue_screenshot.png --shot-delay=2
```

Observed:

- the colony builds with 16 buildings, 8 crew and 15 incident rules;
- third-person camera shows the astronaut with animated idle/walk cycles;
- first-person camera shows the helmet visor overlay, flashlight, and crosshair;
- walking up to an NPC and pressing `E` opens the dedicated conversation panel;
- the conversation panel displays the character's name, role, skills, what they can do (responsibilities), their spoken greeting, and 6 interactive topic buttons;
- selecting a topic plays the character's voiced dialogue audio and displays the bilingual subtitle;
- the pause screen stops the clock and resource consumption;
- voice pack integrity check validates 103/103 audio files with 0 missing keys.

## GDScript edition (stock Godot 4.7.2, no .NET)

The third engine is checked without a GPU and without a display, because that is what CI has — and
because a game that only works on the machine it was written on is not finished.

```bash
bash tools/sync-gdscript-assets.sh
godot --headless --path engines/godot-gdscript/LunaQuest.GDScript --import
godot --headless --path engines/godot-gdscript/LunaQuest.GDScript -- --selftest
godot --headless --path engines/godot-gdscript/LunaQuest.GDScript -- --audit
godot --headless --path engines/godot-gdscript/LunaQuest.GDScript --quit-after 400
```

| Check | Result |
|---|---|
| Import (`--import`) | every script parsed, every asset imported; no `Parse Error`, no `SCRIPT ERROR` |
| Thirty-day self-test (`-- --selftest`) | **544 checks passed**, 0 failed |
| Mission flown | 30 simulated days, **81 events raised**, **51 scripted beats fired** (all ten named beats plus threshold notices) |
| Resource floors over a full mission | battery, oxygen and water reach 0% for a commander who touches nothing — by design, there is no fail state; cabin holds 13.3–21.0 °C |
| Whole content exercised | all 16 buildings, 45 consoles, 18 actions, 44 dialogue topics, 11 objectives unlocked, 7 crew |
| Voice pack | 79 spoken character and dialogue lines resolve to files; the 15 incident headlines are narration and degrade to subtitles |
| Save contract | schema 2 round-trips through JSON into a fresh simulation: clock, battery, journal, crew trust and player name all restored |
| Scene audit (`-- --audit`) | **174 checks passed**: 16 doorways walkable, 45 consoles reachable from standing height, spawn clear and floored, no crew inside a wall, 12/12 headings closed by the traverse markers |
| Headless boot (`--quit-after 400`) | the real scene builds 16 modules, 7 crew and 45 consoles and runs 400 frames with no script errors |
| Web export | `index.html`, `index.wasm`, `index.pck` produced (exit 0) |
| Linux export | `luna-quest-gdscript.x86_64` produced (exit 0) |
| Windows export | `luna-quest-gdscript.exe` produced (exit 0) |
| CI | `.github/workflows/godot-gdscript.yml` — validate on every push and pull request; exports on `main`, tags and manual runs; optional Pages publish |

Not verified here, and stated plainly: **this build environment cannot render the GDScript edition.**
There is no display, no GPU, and the Godot editor download host is blocked, so the scene is verified
by physics queries (the audit) and by the rules it runs (the self-test) rather than by looking at
it. The same content, crew, buildings and materials are rendered and screenshotted in the browser
edition, which is built from the identical JSON and is checked with headless Chromium.

## Official 8-Crew Roster

1. **Dr. Safaa**: Mission Physician · Medical Clinic (Emergency Medicine, Surgical Basics, Radiation Health, Dentistry, Telemedicine)
2. **Noor**: Life Support Engineer · Air and Water Station (Chemical Engineering, Plumbing and Pumps, Sensors, Rapid Repair)
3. **Dr. Alaa**: Geologist and Resource Officer · Ice Mining Site (Geology, Drilling, Chemistry, Rover Operation)
4. **Abdulaziz**: Power Systems Engineer · Energy Farm (Electrical Engineering, Nuclear Safety, Batteries, High-Voltage Operations)
5. **Ahmed**: Robotics and Vehicle Lead · Vehicle Garage (Robotics, Mechanics, AI-based Control, Remote Operation)
6. **Mostafa**: Construction Engineer · Soil Printer (Structural Engineering, 3D Printing, Heavy Machinery, Welding)
7. **NEXO** (robot): Colony Robot · Greenhouse and Comms Tower (Hydroponics telemetry, Antenna and relay checks, Sensor calibration, Data logging)
