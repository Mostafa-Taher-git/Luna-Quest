# Luna-Quest — runtime verification report (three engines)

**Branch:** `arena/ff75f963-luna-quest`, merged to `main` when every check below is green.
**Toolchains actually used:** Godot 4.7.2 (built from source in the sandbox, no `.NET`), Node 22,
Python 3.11, plus the .NET 8 + Godot .NET 4.7.2 pair through GitHub Actions (see "What this
sandbox cannot run").

This report is the record of what was *run*, not what was *written*. Every claim below has the
command that proves it.

---

## 1. What runs, and where

| Suite | Command | Result on this branch |
|---|---|---|
| Content | `node tools/check-content.mjs` | 16 buildings, 7 crew, 15 incident rules, 11 objectives, 17 research notes, 44 dialogue topics, 94 voice lines — consistent |
| Shared simulation (C#) | `dotnet run --project shared/LunaQuest.Tests` (CI `core` job) | 426 checks pass (CI) |
| Engine parity | `python3 tools/check-engine-parity.py` | `PARITY OK` — 23 core files, 8 content files × 3 homes, terrain constants **and 7 terrain values** (worst Δ 4.8e-13 m) |
| Immutable assets | `bash tools/check-immutable-assets.sh` | 8 locked files, 12 hash checks, intro film + Cute Astronaut unchanged |
| GDScript import | `./godot --headless --path engines/godot-gdscript/LunaQuest.GDScript --import` | clean: no parse error, no script error |
| GDScript selftest | `… -- --selftest` | **`SELFTEST OK — 564 checks passed`** (0.85 s) |
| GDScript audit | `… -- --audit` | **`AUDIT OK — 221 checks passed`** (4.75 s) |
| GDScript smoke | `… --quit-after 400` | 400 frames, 7.4 s, no script errors |
| Godot .NET build | CI `dotnet build … -c Debug` and `-c Release` | both clean, no C# warnings left |
| Godot .NET run | CI, `--headless --quit-after 600` | `Luna-Quest ready: 16 buildings, 7 crew, 15 incident rules.` |
| Godot .NET rover | CI, `-- --rover-test` | **`ROVER TEST OK — 30 checks passed`** |
| Unity | `RoverPlayTest.Run` (batch mode, see §5) | added; **not executed here** (no Unity editor in the sandbox) |

## 2. Defects the runtime found (and fixed)

The previous pass wrote engine code without running it. Running it found nine real defects, four
of them game-stopping:

| # | Engine | Defect | How it was found |
|---|---|---|---|
| 1 | GDScript | `rover.gd` did not parse (`var front := corner.begins_with(...)` on an untyped `Array`), so `main.gd` failed to compile. CI had been red on `main` for this since the rover landed. | CI annotation from the failed GDScript job |
| 2 | GDScript | **Every terrain triangle was wound face-down.** The ground mesh was drawn inside-out and its trimesh collider could only be hit from *below*: the commander and six crew members had no floor. `AUDIT FAILED — 8 of 189`. | `-- --audit` |
| 3 | GDScript | `crew.gd`/`main.gd` measured a suit's bounds with `global_transform` before the node was in the tree (invalid, logs `Condition "!is_inside_tree()"`), then shifted the root by an already-scaled offset. | `--quit-after 400` log |
| 4 | GDScript | `main.gd` touched `sim`/`world` after `--selftest` had quit the tree (`Invalid access to property or key 'clock' on Nil`). | `-- --selftest` output |
| 5 | GDScript | `ColonyRover.exit()` measured the step-out from the node transform, which the physics server has not consumed yet — the suit could be dropped inside the rover's own box. | the new rover audit check |
| 6 | Godot .NET | `RoverController.Restore(Dictionary<string, object>)` cannot take `SaveData.Extras` (`Dictionary<string, object?>)` — CS8620. | new `godot-dotnet` CI job (first compiler this code ever saw) |
| 7 | Godot .NET | the headless editor loads `.godot/mono/temp/bin/**Debug**/`; only Release was ever built, so `Main.cs` could not be instantiated. | CI annotations |
| 8 | Unity | **`BuildGround` wound every triangle face-down** (Unity's front face is clockwise): a `MeshCollider` from it cannot be hit from above. The guard meant to catch this tested `mesh.normals[0].y`, which is always positive on this grid, so it never fired. | transcription of the C# grid in Python against the shared heightfield |
| 9 | Unity | **Steering was mirrored.** Both Godot editions yaw by `+(speed/2.3)·tan(steer)` (counter-clockwise); Unity's yaw is clockwise, so A steered right in Unity and left in Godot, with the rear counter-steer flipped too. | comparing the three implementations |
| 10 | Unity | `CrewRover.Exit` placed the commander at `transform.position.y = 0.05` after a `CharacterController` with no floor had drifted upwards, and inside the rover's own capsule. | reading the physics path |

Plus the rover bugs from the brief: BUG-1 (`InputActive` gate in Unity), BUG-2 (Unity
`SetDriving`), BUG-3 (GDScript collision layer/mask while seated), BUG-4 (milestones reset on a
new mission), BUG-5 verified geometrically (§4).

## 3. New guards, so none of this can come back silently

* **`ci.yml` → `parity` job** (T1): parity + immutable assets on every push. The retired browser
  job and `pages.yml` are gone; from v3.4 the release workflow builds no web export either —
  the GDScript `Web` preset was deleted with it (H1/A1).
* **`godot-dotnet.yml`** (new): builds the C# edition (Debug *and* Release), imports the project,
  runs 600 frames and then `-- --rover-test`. This is the first CI job that ever compiled this
  engine.
* **`--audit`** gained 33 checks: 24 terrain probes proving the ground faces up (hit from above,
  not from below — verified to fail 9 checks when the winding is reverted) and the full rover
  checklist (35 checks) driven through `ColonyRover._step`.
* **`--selftest`** gained 17 checks: `TERRAIN_PARITY` — the seven brief points plus a ring inside
  the flat pad, asserted against the shared C# numbers.
* **`check-engine-parity.py`** now recomputes those seven heights from an independent Python
  transcription of `LunarTerrain.cs`. Changing a table value fails parity; changing the port fails
  the selftest. Both directions were tested.

## 4. Terrain parity numbers (T4)

`LQTerrain.new(1337).height(x, z)`, run inside Godot 4.7.2, against the shared algorithm:

| (x, z) | height (m) | note |
|---|---|---|
| (0, 23) | 0.000000000000 | inside the flat pad — exactly zero |
| (52, -34) | 0.000000000000 | inside the flat pad (the ice site) — exactly zero |
| (95, 8) | -0.066552338049 | on the fade |
| (200, 200) | -7.724299839182 | open highlands |
| (-350, 120) | 3.014551430629 | open highlands |
| (300, -300) | 0.332602820384 | open highlands |
| (1000, 0) | 4.653101754171 | far streamed ground |

Worst difference against the Python reference: **4.8e-13 m** (float64 GDScript vs float64
Python; the brief allows 1e-3). The .NET and Unity editions call the shared C# directly, so they
are the reference by construction.

**Seat lean (BUG-5).** Modelled rather than guessed, because the two engines use opposite X
rotation senses. Godot `rotation.x = -0.32` and Unity `Euler(18, 180, 0)` produce the *same* world
vector for the suit's head — `(0, +0.31, -0.95)`, i.e. tipped back onto the seat — while the
opposite signs produce `(0, -0.31, -0.95)`, a forward slump. Both implementations are correct and
the `--rover-test`/playtest now assert the resulting angle.

## 5. T5 checklist per engine

| Item | GDScript | Godot .NET | Unity |
|---|---|---|---|
| Prompt near the rover, EN/AR | code + HUD check | code | code |
| E enters, suit at the controller | audit | `--rover-test` | `RoverPlayTest` |
| W/S, A/D (rear counter-steer), Shift, Space | audit | `--rover-test` | `RoverPlayTest` |
| Wheels spin, front wheels steer | audit (`spin`, `_steers`) | `--rover-test` | `RoverPlayTest` |
| km/h + battery in the prompt, drain | audit | `--rover-test` | `RoverPlayTest` |
| Garage apron recharges | audit | `--rover-test` | code |
| Ice site sets the milestone | audit | `--rover-test` | code |
| E exits beside it, upright, solid, no jitter | audit | `--rover-test` | `RoverPlayTest` |
| F5/F9 restores rover state; old saves load | audit | `--rover-test` | code |
| Restart: apron, full battery, milestones reset | audit | `--rover-test` | `RoverPlayTest` |
| Dialogue/pause does not drive it | `InputActive`-equivalent gate | code | `RoverPlayTest` |
| selftest + audit still pass | yes | n/a | n/a |

## 6. What this sandbox cannot run (stated plainly)

* **No `dotnet`, no Unity editor, no `Godot .NET` binary locally.** The sandbox has no network
  access to Microsoft's feeds, Unity's hub or GitHub's release assets, so those toolchains could
  not be installed here. Godot 4.7.2 (standard build) was compiled from source (1,801 build steps,
  41 minutes) and is what ran every GDScript number above. The C# edition was compiled and run by
  the new `godot-dotnet` CI job on GitHub's runners — that is where its 30 rover checks passed.
* **Unity is not executed at all.** `RoverPlayTest` is the harness for it:
  `Unity -batchmode -quit -projectPath engines/unity/LunaQuest.Unity -executeMethod LunaQuest.Unity.RoverPlayTest.Run -logFile rover.log`.
  Its C# is syntax-checked (tree-sitter), the fixes are reasoned from Unity's documented
  conventions, and the ground winding was verified numerically against the shared heightfield — but
  the four Unity rows marked "`RoverPlayTest`" have not been executed. That is the one gap in this
  report.
* **No GPU, no display.** "Visual" checks are physics queries, transforms and log lines, not eyes.
  Third-person/screenshot review still needs a machine with a display.

## 7. Optional upgrades (T7) — not done

T7a/T7b (glTFast in Unity) and T7c (NASA LRV) were deliberately left out: T7a changes the Unity
render path and needs the editor to verify, and neither is required by the definition of done.
`docs/design/Luna-Quest_Rover_and_Realism_EN.md` still describes the Unity fallback LTV correctly.
