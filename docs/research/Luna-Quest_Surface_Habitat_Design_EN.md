# Surface Habitat Design Notes (Browser Research)

**Status: design reference, not a flight-certified design.** The numbers the simulation uses
are anchored to the sources in `content/research.json` and the long notes in `docs/research/`.
This file records the architectural cues gathered from the NASA Johnson Space Center surface
habitat material and explains how they shape the colony's buildings, layout and textures.

Source consulted in the browser:
<https://www.nasa.gov/reference/jsc-surface-habitats/>

---

## 1. What the source actually says

The JSC page describes capabilities and test facilities rather than a single finished base
design. The cues that are useful to a game world are:

- **Pressurised softgoods structures.** Decades of work on crewed inflatable softgoods, including
  habitats, airlocks and *pressurised transfer tunnels* between elements. Restraint layers,
  seams, folding, deployment, leak and burst testing, and hypervelocity impact testing all
  matter. Multi-layer insulation (MLI) thermal blankets are among the softgoods products.
- **Windows are engineered parts.** JSC maintains a Window Material Database; a standard
  material thickness (`0.5 in`) is used for characterisation so window designs can be compared
  like for like. Windows are an integrated subsystem, not a hole in a wall.
- **HERA**, the three-storey Human Exploration Research Analog, gives a concrete internal
  programme for a small isolated crew:
  - four individual crew sleeping quarters (six crew for shorter stays);
  - a galley with a plumbed water supply, a shower/sink with hot and cold running water;
  - a medical workstation, exercise equipment (aerobic and resistive), actigraphy and heart-rate
    monitoring;
  - a simulated ECLSS, a stowage module that passes hardware, samples and trash through;
  - a Mission Control Center, communication delay up to 23 minutes each way, self-scheduling
    tools and crew-timelining;
  - virtual windows, adjustable LED lighting, VR EVA task simulation and a two-crew rover.

  Note: the 23-minute delay is HERA's configurable Mars/transit analogue, not the game's value.
  Luna-Quest uses the ~1.3 s one-way Earth-Moon light time recorded in
  `docs/research/Luna_Academy_Moon_Environment_Research.md`.
- **Space architecture practice** (the Center for Design and Space Architecture): interior
  architecture, functional allocation, volumetric analysis, human-in-the-loop task simulation,
  full-scale mockups and iterative CAD/VR review.
- **Outdoor EVA proving grounds** with lunar and Mars rock yards, control stations and robot
  mock-ups of crew access hatches and logistics stowage trainers.

Two further points that follow from these and from the Moon environment notes already in this
repository: regolith is fine, abrasive and attacks seals, joints and instruments, so dust
mitigation at the hatch is a design problem; and in-situ regolith is the obvious shielding mass
for a surface habitat.

---

## 2. How the colony already follows it

| Cue | Where it appears in Luna-Quest |
|---|---|
| Sealed horizontal pressure modules | `web/src/world/buildings.js` barrel vaults, arch ribs and hatch bulkheads; `parts.js` `pressureHull()` |
| Airlocks and EVA access | `airlock` building, `portico()` canopies, hazard sills on `hatch()` |
| MLI thermal blankets | `mats.mli` gold blanket panels on habitable modules |
| Engineered window bands | `parts.js` `windowBand()`, `materials.js` glass |
| Radiators, tanks, coolant and power runs | `radiator()`, `tank()`, `pipeRun()`, `solarArrayRow()` |
| Landing and surface operations | `buildLandingPad()`, approach beacons, rovers, rock yard dressing in `props.js` |
| Crew quarters, galley, medical corner, exercise | `content/buildings.json` rooms for `habitat` and `medical`; 45 consoles |
| Shielding mass | `bermSegment()` blast berms in `props.js` |
| Delayed comms, ECLSS, scheduling | `content/npcs.json` schedules, `sim/models.js` comms and air models |
| Volumetric / functional allocation | `content/buildings.json` footprints and room sizes; `tools/check-content.mjs` enforces walkable footprints |

---

## 3. Improvements made from this research

1. **Closed vault bulkheads.** An open half-arch reads as a canopy. Both ends of every barrel
   vault now have a half-ellipse bulkhead, so the module reads as a sealed horizontal cylinder.
   A ceiling collider stops the commander from jumping through the shell.
2. **Dust-mitigation porch.** Every entrance gets a grated sill, a marked cleaning line and
   brush posts between the outside and the pressure hatch. This is the single most important
   surface-operation detail that was missing.
3. **Regolith shielding on the lee side.** The habitat, life support, medical and storm shelter
   modules get a sintered berm on the side away from the plaza, kept clear of the hatch and the
   approach route.
4. **The scanned height map is finally used.** `web/src/world/environment.js` loaded five PBR
   regolith maps and used four; the height map now drives a low-amplitude bump on the near field
   with its own lower repeat on the far ring, so the ground catches the grazing polar light.

These change geometry, materials and lighting only. The intro film and the Cute Astronaut model
are untouched; see `docs/design/Luna-Quest_Immutable_Assets_EN.md`.

---

## 4. Still open

- **Pressurised transfer tunnels** between adjacent modules. The source treats them as first-class
  habitat elements. They would need shared door normals in `content/buildings.json` so both the
  web and GDScript builders agree on which walls to join; doing that badly produces tunnels
  through rooms.
- **Volumetric review** of each interior against its room list (clearances around consoles and
  bunks), following the space-architecture practice of checking a mockup before building it.
- **EVA task boards** at the airlock and the rock yard, so the player has an exterior job list
  rather than only interior consoles.
- **Dust accumulation** on solar arrays and radiators as a visible material state, not only a
  number in the simulation.

---

## 5. Boundary

These notes are art direction, not engineering. The Moon environment research in this repository
keeps its original source links and evidence grades; nothing here is a flight-certified habitat
design, and no game number in the simulation was changed to satisfy a visual cue.
