# Luna-Quest test report

**Build:** full dialogue system and complete 8-NPC roster pass, 14 November 2026  
**Repository:** <https://github.com/Mostafa-Taher-git/Luna-Quest>  
**Primary runtime:** Godot 4.7.2 .NET on Linux (Mesa Intel HD 4600, Vulkan)  
**Secondary runtime:** Unity 6.3 LTS project (dual-engine architecture)

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

## Official 8-Crew Roster

1. **Dr. Safaa**: Mission Physician · Medical Clinic (Emergency Medicine, Surgical Basics, Radiation Health, Dentistry, Telemedicine)
2. **Noor**: Life Support Engineer · Air and Water Station (Chemical Engineering, Plumbing and Pumps, Sensors, Rapid Repair)
3. **Dr. Alaa**: Geologist and Resource Officer · Ice Mining Site (Geology, Drilling, Chemistry, Rover Operation)
4. **Abdulaziz**: Power Systems Engineer · Energy Farm (Electrical Engineering, Nuclear Safety, Batteries, High-Voltage Operations)
5. **Ahmed**: Robotics and Vehicle Lead · Vehicle Garage (Robotics, Mechanics, AI-based Control, Remote Operation)
6. **Mostafa**: Construction Engineer · Soil Printer (Structural Engineering, 3D Printing, Heavy Machinery, Welding)
7. **NEXO** (robot): Colony Robot · Greenhouse and Comms Tower (Hydroponics telemetry, Antenna and relay checks, Sensor calibration, Data logging)
