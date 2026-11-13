# Luna-Quest — Version 2.2 Change Log (English Edition)

**Reference date:** 13 November 2026  
**Document version:** 2.2 EN

## Identity and player character
- Official title: **Luna-Quest: The Living Moon Colony**.
- The player controls one mission commander and chooses a display name at first launch.
- `Amara Okafor` is the editable default story archetype, not an NPC.
- Player duties include command, daily operations, emergency coordination, lander scenarios, cargo and inventory, navigation, rover/EVA planning, construction approvals and Earth communications.

## Human NPC roster
- The approved cast model is one player-controlled human plus eight human NPC crew members.
- Confirmed names: Dr. Safaa, Noor, Dr. Alaa, Abdulaziz, Ahmed and Mostafa.
- The Agricultural Engineer and the Communications/Software Engineer remain unnamed pending team approval. Do not invent names.
- Human crew members have motives, memories, relationships, schedules, professional limits and personal story arcs.
- Robots, rovers, drones and robotic arms are equipment/autonomous systems, not human crew NPCs.

## Engine decision
- Support Unity 6.3 LTS and Godot 4.7.2 .NET using C#.
- Shared Core targets .NET Standard 2.1 and contains simulation, resources, incidents, NPC domain rules and save contracts.
- Each engine has a separate adapter for scenes, physics, navigation, UI, audio and persistence.
- The engines do not run together inside one game build.
- Unity is the Web route when browser delivery is required; Godot 4 C# is a desktop target at the reference date.

## World and buildings
- The game remains a Moon-only open sandbox with no age-selection screen and no mandatory linear level sequence.
- Every functional building, whether pre-existing or player-built, must have a real enterable interior with rooms, interactions and saved state.
- Decorative non-enterable objects must be explicitly tagged `Decorative` and cannot be presented as functional facilities.
- Base building borrows planning, production and upgrade loops from base-building games, but uses lunar engineering constraints and excludes raids, PvP and combat.

## Scientific evidence
- Research integration includes NASA RadLab/CRaTER, Chang'e 4 LND, DONKI, OSDR, Moon Trek, LOLA, Diviner, JPL Horizons, PDS and Canadian Space Agency resources.
- Every derived value must preserve source, unit, date range, location, evidence class and interpretation limits.
- Direct lunar measurements must remain separate from orbital remote sensing, ISS/Antarctic analogues, Earth laboratory experiments, future concepts and game simplifications.

## Implementation status
This version updates the design documents, architecture, player/NPC data and research references. It does not claim that a complete production game has already been built in Unity or Godot. The previous browser MVP is retained only as a legacy reference.
