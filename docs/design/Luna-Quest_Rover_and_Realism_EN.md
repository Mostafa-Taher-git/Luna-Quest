# The Crew Rover and the Realism Pass

**Status: implemented in the browser and the shipped GDScript edition.** This note records what
changed, why, and the rules it respects. It complements the GDD and the immutable-assets policy.

## The one rule that does not bend

The intro film and the Cute Astronaut are frozen
(`docs/design/Luna-Quest_Immutable_Assets_EN.md`, pinned in `docs/design/asset-lock.json`).
Nothing in this pass re-renders, re-encodes, trims or replaces the film, and nothing replaces the
astronaut. Every astronaut — the commander at the wheel included — is the same
`cute_astronaut.glb` at 1.75 m. The GLB's meshes are not skinned, so the driving seat pose is a
**placement and recline of the whole suit**, never a reshaped character. The film's SHA-256 and
the astronaut's SHA-256 are unchanged.

## The crew rover you can actually drive

The rideable rover is the CC0 kit's **open unpressurised rover** — a real LTV-style mesh with
cleanly named sub-meshes (`wheel-front-left`, `steer-rear-right`, `hand-controller`,
`antenna-dish`). It is parked on the garage apron.

- **Enter / exit:** walk up and press `E`; `E` again parks and steps out beside it.
- **Drive:** `W`/`S` throttle and reverse, `A`/`D` steer, `Shift` boost, `Space` handbrake.
- **Physics:** lunar gravity (1.62 m/s²), low regolith traction, speed-sensitive steering,
  four-corner steer (rear corners counter-steer for a tight turning circle), soft suspension that
  leans the chassis into crater rims, and ballistic dust that falls fast because there is no air.
- **Energy:** an engine-local battery drains with road speed and recharges on the garage apron.
  It lives in the save extras, not the shared colony core, so the deterministic simulation and its
  save schema are untouched.
- **Camera:** first-person at the hand controller, or a chase boom in third person.

### A reason to drive

The ice prospecting site sits ~70 m from the plaza. Two afternoon field tasks use the rover:
take it off the garage apron, and drive it out to the ice site, then park, step out and recover a
sample at the drill shack. The first drive and the ice-site arrival are logged in the journal.

## The realism pass

- **Regolith:** the shipped GDScript colony wore flat vertex colours; it now wears the Poly Haven
  **moon_02** PBR set (albedo + OpenGL normal + roughness + AO + displacement, CC0), falling back
  to `moon_dusted_03` and then to clay. The browser prefers the same `moon_02` set.
- **Rocks:** the seven Poly Haven photoscanned `moon_rock` glTF scans replace the boulder spheres.
- **Hardware:** the box rovers are replaced by the real kit rover, and a kit dressing pass places
  the cargo landers, haulers, power yard (vertical solar tower), ISRU skids, motor pool and relay
  mast around the walkable colony — the same layout the browser uses.
- **Buildings:** the walkable modules stay data-driven (the building you enter is the building the
  resource model knows), now surrounded by realistic pressurised hardware rather than empty dust.

## Engine parity and safety

- Browser (retired, archived): `archive/web/src/world/rover.js` + wiring in `player.js`, `main.js`, `props.js`, `hud.js`.
- GDScript: `scripts/rover.gd` (`ColonyRover`, a `CharacterBody3D`) + `player.gd`, `main.gd`,
  `world.gd`.
- Every new asset load is guarded: a missing kit module or texture falls back to the previous
  procedural form, so a fresh clone still boots and the headless scene audit (spawn, doorways,
  consoles, crew) is unaffected. Kit dressing is visual-only and adds no colliders.
- The Godot .NET and Unity adapters keep their existing movement; the shared simulation core,
  content JSON and save schema are unchanged by this pass.

## Sources and licenses

See `CREDITS.md`: the kit is CC0 (3dassets.dev), the regolith and rocks are CC0 (Poly Haven), the
Earth is public domain (NASA). The Cute Astronaut remains CC BY 4.0 (haefu, Sketchfab).
