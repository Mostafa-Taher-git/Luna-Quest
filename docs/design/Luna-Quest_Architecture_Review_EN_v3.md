# Luna-Quest Architecture Review — v3 (three engines)

**Scope decision (owner, 2026-11):** the game targets exactly three engines —
**Godot .NET**, **Godot GDScript** and **Unity**. The browser edition is retired from
active development; `web/` stays in the tree only because `web/static/intro-poster.jpg`
is a locked asset (`docs/design/asset-lock.json`) and the folder holds historical tests.
No new feature work lands in `web/`.

This review records what was found, what was changed, and the rules that keep the three
engines honest. It supersedes the "dual engine" framing of v2.2.

## The intended architecture (unchanged, and correct)

```
content/*.json          <- single source of truth: buildings, crew, incidents, crops...
shared/LunaQuest.Core   <- single C# simulation: Sim, Domain, Save, Terrain, Time
        |
        +- Godot .NET   compiles the core directly (csproj reference)
        +- Unity        Assets/Scripts/Core is a byte-identical mirror (tools/sync-content.sh)
        +- GDScript     scripts/sim.gd is a hand port, guarded by selftest.gd (190 checks)
```

Determinism is the contract: the same seed and the same inputs must produce the same
colony in every engine, and a save never stores terrain. That only holds if **one**
heightfield, **one** simulation and **one** content set exist.

## What the review found

| # | Finding | Severity | Status |
|---|---|---|---|
| 1 | **GDScript terrain was a different Moon.** `world.gd` used `FastNoiseLite` (flat radius 110 around the origin) instead of the shared `LunarTerrain` (seed 1337, flat radius 88 around (0, 8), crater layers). The other engines draw the shared surface; GDScript did not. | High | Fixed: `scripts/terrain.gd` is an exact port of `LunarTerrain.cs`, verified against the C# hash/noise arithmetic. |
| 2 | **GDScript ground collision was a flat slab under a rolling mesh.** Beyond the pad the visual terrain rose up to 16 m while physics stayed at y = 0 — the commander walked through hills and floated over bowls. | High | Fixed: the ground collider is now a trimesh baked from the same mesh that is drawn. |
| 3 | **Unity ground was a flat 1600 m plane.** `ColonyWorld.BuildGround` never used the mirrored `Core/LunarTerrain.cs`; Unity had no craters, no relief, and a different horizon from Godot. | High | Fixed: Unity generates its ground mesh + MeshCollider from the shared `LunarTerrain`. |
| 4 | **Nothing enforced the Unity core mirror.** `tools/sync-content.sh` copies `shared/LunaQuest.Core` into Unity, but CI never checked drift — a hand edit in Unity would silently fork the simulation. | High | Fixed: `tools/check-engine-parity.py` fails CI on any byte difference (core mirror, content JSON in all four homes). |
| 5 | **The immutable-asset guard was not run in CI.** `tools/check-immutable-assets.sh` exists but no workflow invoked it. | Medium | **Done:** the `parity` job in `ci.yml` runs the parity script and the guard on every push. |
| 6 | **CI and Pages still built/deployed the retired browser edition.** | Medium | **Done:** `pages.yml` deleted, the CI `web` job deleted, and the release workflow no longer builds or publishes `web/`. |
| 7 | **The crew rover was driveable in GDScript only.** Feature parity across the three engines was broken. | Medium | Fixed: `RoverController.cs` (Godot .NET) and `CrewRover.cs` (Unity) mirror the GDScript vehicle: same kit mesh where the engine can load GLB, same lunar physics constants, same battery/garage/ice-site rules, same save keys. |
| 8 | Unity cannot import `.glb` without a glTF package (none is in `Packages/manifest.json`), so the 54 kit GLBs sit unused there. | Low | Documented. The Unity rover uses the procedural LTV fallback (the same fallback GDScript uses when the kit is absent). Adding `com.unity.cloud.gltfast` is the optional next step; it is an editor-side decision, not an architecture blocker. |

> **Token note (resolved):** the earlier push token had Contents write but not Workflows write.
> The workflow edits described in `CI_Parity_Job.md` are now applied, so that document is kept
> only as the historical note it is. A `godot-dotnet` workflow was added as well: it compiles and
> runs the C# edition on every push, which no job did before.
>
> **Runtime pass:** everything in this review has since been run — see
> `Luna-Quest_Verification_Report_EN.md` for the commands, the nine defects the runs found and
> the per-engine verification status.

## Rules going forward

1. **Never fork the simulation.** Edit `shared/LunaQuest.Core`, run `tools/sync-content.sh`,
   and let CI's parity job prove the mirror is byte-identical. The GDScript port changes in
   the same PR as the C# change, with `selftest.gd` updated.
2. **One Moon.** Any terrain change happens in `LunarTerrain.cs` first; the GDScript port
   (`scripts/terrain.gd`) is regenerated from it and checked against the same constants.
3. **Content is data.** `content/*.json` is authored once and synced; no engine hand-edits
   its copy.
4. **The locks still hold.** The intro film and the Cute Astronaut are immutable
   (`asset-lock.json`); CI now enforces that on every push.
5. **Three engines, one feature set.** A gameplay feature is "done" when Godot .NET,
   GDScript and Unity all have it, with the same constants and the same save keys.

## Save compatibility

The shared save schema (schema 2) is unchanged. Rover state lives in `extras` under flat
keys (`roverX/roverY/roverZ/roverYaw/roverBattery/roverDriven/roverIce/roverOccupied` in
the C# engines; a `rover` dictionary in GDScript), so old saves load untouched and the
deterministic core never sees engine-local vehicle state.
