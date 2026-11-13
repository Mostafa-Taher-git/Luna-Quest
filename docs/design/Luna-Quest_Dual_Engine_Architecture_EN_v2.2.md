# Luna-Quest — Dual-Engine Technical Architecture

**Version:** 2.2 EN  
**Reference date:** 13 November 2026  
**Target engines:** Unity 6.3 LTS and Godot 4.7.2 .NET  
**Shared gameplay language:** C#  
**Shared-core target:** .NET Standard 2.1

> This architecture supports two separate Luna-Quest implementations. It does not run Unity and Godot together inside one game, and a project created for one engine does not open directly in the other. Gameplay rules, data contracts and tests can be shared; rendering, scenes, physics, navigation, UI, audio and engine-specific saving are implemented through separate adapters.

## 1. Engine and platform decision

| Area | Decision | Implementation note |
|---|---|---|
| Unity | Unity 6.3 LTS + C# | Recommended LTS baseline for production lock at the document reference date. |
| Godot | Godot 4.7.2 .NET + C# | Stable .NET baseline at the reference date. Lock the compatible .NET SDK. |
| Shared core | C# / .NET Standard 2.1 | Must not reference `UnityEngine` or `Godot.*`. |
| Desktop | Unity and Godot .NET | Two separate native builds with equivalent gameplay rules and content. |
| Browser/Web | Unity Web is the selected route when browser delivery is required | Validate WebAssembly/WebGL memory, load time, browser support and performance on target devices. |
| Godot Web + C# | Not supported by Godot 4 at the reference date | Requires a separate GDScript implementation/adapter or a future documented change in official support. |

**Do not promise a Godot 4 C# browser export.** If browser delivery is a release requirement, use Unity for the Web build and keep Godot .NET as a desktop implementation that follows the same shared rules and content contracts.

## 2. Shared responsibilities vs engine-specific responsibilities

### Shared between both engines

- Simulation rules: power, water, air, thermal control, dust, food, radiation and communications.
- Construction rules: grid placement, site constraints, costs, build stages, upgrades and utility connections.
- Character data: Player/NPC profiles, roles, motivations, schedules, relationships, story arcs and knowledge boundaries.
- Incident system: triggering conditions, priorities, state transitions, outcomes and recovery rules.
- Learning objectives, hints, knowledge journal and evidence-based progress tracking.
- Save contracts, schema versions and save migration rules.
- Building, room, entrance/exit and interaction definitions in shared data formats.
- Deterministic tests for simulation logic and gameplay calculations.

### Implemented separately by each engine

| Function | Unity adapter | Godot .NET adapter |
|---|---|---|
| World and scenes | Scene / GameObject / Prefab | Scene / Node3D / PackedScene |
| Player movement | CharacterController or Rigidbody | CharacterBody3D or RigidBody3D |
| Door interaction | Trigger / Collider / Component | Area3D / CollisionShape3D / Signal |
| NPC navigation | NavMeshAgent | NavigationAgent3D |
| UI | UI Toolkit or Canvas | Control / CanvasLayer |
| Animation | Animator | AnimationTree / AnimationPlayer |
| Audio | AudioMixer / AudioSource | AudioBus / AudioStreamPlayer |
| Local save path | Application.persistentDataPath | `user://` through FileAccess |
| Materials and shaders | Material / Shader Graph | StandardMaterial3D / ShaderMaterial |

## 3. Recommended repository structure

```text
Luna-Quest/
├── shared/
│   ├── LunaQuest.Core/          # netstandard2.1: rules, simulation, NPC domain
│   ├── LunaQuest.Contracts/     # DTOs, interfaces and save contracts
│   ├── LunaQuest.Content/       # JSON schemas and content validation
│   └── LunaQuest.Tests/         # deterministic tests independent of an engine
│
├── engines/
│   ├── unity/
│   │   ├── LunaQuest.Unity/     # scenes, prefabs, MonoBehaviours and UI
│   │   └── LunaQuest.Unity.Tests/
│   └── godot/
│       ├── LunaQuest.Godot/     # .NET project, scenes, nodes and resources
│       └── LunaQuest.Godot.Tests/
│
├── content/
│   ├── player/  ├── npcs/  ├── buildings/  ├── interiors/
│   ├── crops/   ├── incidents/  ├── research/  └── learning-objectives/
│
├── assets/
│   ├── blender-source/  ├── fbx-interchange/  ├── glb-gltf/
│   ├── textures/  ├── audio/  └── ui/
│
├── tests/
│   ├── simulation-scenarios/  ├── building-validation/
│   ├── npc-validation/  └── save-migration/
│
└── docs/
    ├── GDD/  ├── SDD/  └── science-research/
```

## 4. Shared core boundaries

`LunaQuest.Core` contains logic that does not require a 3D scene:

- `ColonySimulation`: updates colony state over simulation time.
- `ResourceLedger`: tracks water, food, gases, power and construction materials.
- `PowerModel`: generation, storage, loads, reserves and priorities.
- `WaterModel`: storage, filtration, recycling, flow and quality.
- `LifeSupportModel`: cabin pressure, atmosphere composition, oxygen and CO₂ monitoring.
- `ThermalModel`: temperature, insulation and thermal loads.
- `DustModel`: dust accumulation and effects on panels, joints, seals and airlocks.
- `FoodProductionModel`: crop cycles, nutrients, growth stages and harvest.
- `RadiationWeatherModel`: educational space-weather alerts with units, provenance and explicit limits.
- `IncidentDirector`: incident conditions, priority, state and recovery.
- `NpcDomain`: human NPC goals, schedules, motives, relationships and knowledge boundaries.
- `ConstructionRules`: site suitability, costs, utility connections and construction phases.
- `LearningDomain`: learning goals, hint depth, evidence and progress.

The shared core must not use `Time.deltaTime`, `Node` or `MonoBehaviour`. The engine adapter supplies abstract time steps and inputs; the core returns deterministic state changes and events that can be tested without rendering.

## 5. Enterable building requirement — mandatory acceptance rule

Every functional building, whether present at game start or constructed by the player, must include:

1. A shared `BuildingDefinition` describing identity, function, costs, site requirements and utility needs.
2. An exterior scene: Unity Prefab/Scene or Godot PackedScene.
3. An `InteriorDefinition` and a separate, enterable interior scene.
4. Stable entrance/exit points, room IDs, corridor links and navigation points.
5. Real interior interactions: machinery, lockers, workstations, samples, beds, NPCs or data consoles.
6. Saved interior state: doors, equipment, inventory, crops, maintenance, projects and NPC locations.
7. Entry/exit and save/load tests in both engine implementations.

**Construction rule:** A gameplay `BuildingDefinition` cannot be approved unless it references a valid interior scene and a defined interaction set. A non-enterable building is permitted only when explicitly classified as `Decorative`, not as a functional facility.

## 6. Human NPCs and robots

- The player is the mission commander and selects a display name when starting a new game. `Amara Okafor` is the editable default archetype, not an NPC.
- There are eight human crew NPCs. They have schedules, memories, goals, relationships, professional responsibilities and story arcs.
- Do not call human characters “bots”. Robots, rovers, drones and robotic arms are autonomous equipment.
- Both engine implementations share NPC decision rules in the core; movement, pathfinding, collision and animation remain engine-specific.
- An NPC only knows what they observed, recorded or were told by another crew member.
- Child-facing dialogue must use reviewed contextual content; unrestricted open-ended generation is not part of the core release.

## 7. Multi-engine asset pipeline

1. Blender `.blend` files are the editable source masters.
2. FBX is the primary interchange route for rigged characters and animation where needed.
3. GLB/glTF 2.0 is suitable for static props and previews and can be imported into Godot. Unity may use a vetted glTF importer or a controlled FBX conversion pipeline.
4. Do not assume material or shader parity; create separate material profiles per engine.
5. Standardize real-world scale, pivots, forward axis, stable asset IDs, LODs and separate collision meshes.
6. Keep visual references and test captures for every building interior in both engines.

## 8. Save data and persistence

The logical save model includes:

- `PlayerState`: chosen display name, player location, tools, status and preferences.
- `WorldState`: simulation time, space weather, loaded sectors and active incidents.
- `BuildingState`: each building, upgrades, utilities and exterior condition.
- `InteriorState`: rooms, equipment, doors, inventory and player location inside buildings.
- `NpcState`: location, routine, goals, relationships, memory and personal story events.
- `ResourceState`: power, water, air, food and materials.
- `LearningState`: evidence, practiced skills, hints and knowledge journal.
- `ResearchJournal`: samples, readings, sources, units and timestamps.

Each save includes `schemaVersion` and migration tests. Each engine has its own file I/O adapter, while using the same DTOs and migration results.

## 9. Shared acceptance tests

- Run shared-core tests with the .NET test runner without opening either game engine.
- Run the same power, water and incident scenarios in Unity and Godot and compare outputs within declared tolerances.
- Start a new game with a custom player name and verify it persists in both engines.
- Enter and exit every functional building, then save/load while the player is inside.
- Verify all eight human NPCs continue their schedules and interactions when the player is absent.
- Verify every player-built functional building has an interior, interactions and valid utility connections.
- Validate asset import, scale, materials and animation in both engines.
- Test Unity Web in target browsers if browser delivery remains in release scope.
- Test Godot .NET Desktop on the supported operating systems.

## 10. Official engine references

- Godot 4.7.2 archive: https://godotengine.org/download/archive/
- Godot C#/.NET documentation: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html
- Godot Web export: https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html
- Unity 6 release support: https://unity.com/releases/unity-6/support
- Unity scripting backends: https://docs.unity3d.com/Manual/scripting-backends-intro.html
- Unity .NET Standard profile: https://docs.unity3d.com/ScriptReference/ApiCompatibilityLevel.NET_Standard.html

**Version-lock policy:** At production start, lock exact engine patch versions, the .NET SDK, packages and export templates in source control. Do not use floating “latest” versions. Verify official documentation before every engine or SDK upgrade.
