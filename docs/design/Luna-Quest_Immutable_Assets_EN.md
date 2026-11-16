# Immutable Assets: The Intro Film and the Cute Astronaut

**Status: permanent project rule.** This applies to every engine, every branch, every future
release, and every future change to the game, including changes to the model, the story, the
colony, the gameplay, the physics or the rendering pipeline.

The two assets below are deliberately frozen. They are not placeholders, not drafts and not
candidates for replacement. Work on Luna-Quest improves the world *around* them.

---

## 1. The intro film

The existing intro film is final and is liked as it is.

- **Never** re-render, re-encode, re-time, trim, extend, colour-grade, upscale, replace or
  otherwise modify any of the existing film files.
- **Never** create another intro video. This holds even if the astronaut model is fixed, the
  game is rewritten, the engine is swapped, the story changes, or the colony is rebuilt.
  There is one film, and it stays the one film.
- The only code that may be edited around the film is the **playback chain**: how it is started,
  skipped, remembered as seen, and how the game falls back when autoplay is refused or a codec
  is unavailable. None of those changes may produce, convert or substitute a video.
- The retired web build preferred VP9/WebM and fell back to H.264/MP4; the Godot build uses
  Theora/OGV. These are three containers of the same existing film, not three different films.
- The poster frame (`archive/web/static/intro-poster.jpg`) is a still taken from the existing film. It
  may be copied by the asset sync scripts but never regenerated.

### Authoritative copies

| Path | Format | Role |
|---|---|---|
| `assets/video/intro.mp4` | H.264 / AAC, 1280x720, 24 fps, 72.000 s | web fallback |
| `assets/video/intro.webm` | VP9 / Opus, 1280x720, 24 fps, 72.008 s | web primary |
| `assets/video/intro.ogv` | Theora / Vorbis, 72.000 s | Godot |
| `engines/godot/LunaQuest.Godot/assets/video/intro.ogv` | Theora | Godot project copy |
| `archive/web/static/intro-poster.jpg` | JPEG | poster frame |

The retired browser edition generated copies under `web/public/` and `web/dist/` with
`web/scripts/sync-assets.mjs` and `npm run build`. They were byte-identical build
artefacts, not new films, and left the tree with the edition's move to `archive/web/`.

---

## 2. The Cute Astronaut

The existing **Cute Astronaut** is the one and only human character model in Luna-Quest. It is
close to the intended look and keeps its identity.

- **Every astronaut uses this model**: the player commander, all named crew, and any human
  character added in the future. No exception, no per-role variant mesh, no "emergency" suit.
- **Never replace it.** Do not download, import, purchase, generate or bake another astronaut
  model, and do not add a second astronaut model to the repository.
- **Fixing it is allowed and encouraged.** A fix means work that keeps the same GLB as the
  source: correcting scale and height, correcting the rig or the folded-leg idle pose, hiding a
  detached duplicate mesh, tuning materials, adding separate procedural fittings (chest pack,
  life-support backpack, visor glass, helmet light, shoulder badges), driving the existing
  animation instead of leaving it inert, and making every engine load the same file at the
  same scale.
- The Unity `cute_astronaut_baked.obj` is a static bake of **the same model** at the frozen
  `0.4 s` idle pose produced by `tools/bake_astronaut_obj.py`. It is a format conversion, not a
  second model. Its geometry must never be reshaped into a different character.
- `assets/models/lunar_colony/eva-suit-23f03707.glb` is a decorative kit prop that is present in
  the asset library and referenced by **no** runtime code. It is explicitly **not** a character
  and must never enter a character loading path. The guard script enforces this.

### Source and credit

- Author: **haefu**
- Source: <https://sketchfab.com/3d-models/cute-astronaut-816bd786fe8b4559a009de6a95582003>
- Model id: `816bd786fe8b4559a009de6a95582003`
- License: **CC BY 4.0**, <https://creativecommons.org/licenses/by/4.0/>
- Attribution lives in the repository root `CREDITS.md` and must stay there.

The GLB contains two meshes (`Astronaut_Astronaut_0`, `Astronaut_visier_0`), two materials, one
skin and one animation clip. It has no embedded textures; the suit, visor and fittings are
authored in code. See also `docs/TEST_REPORT.md`.

### Character loading paths

| Runtime | Path |
|---|---|
| Web (Three.js, retired) | `archive/web/src/world/astronaut.js` loads `models/astronaut/cute_astronaut.glb` |
| Godot 4 .NET | `engines/godot/LunaQuest.Godot/scripts/WorldBuilder.cs` loads `res://assets/astronaut/cute_astronaut.glb` |
| Godot GDScript | `engines/godot-gdscript/LunaQuest.GDScript/scripts/crew.gd` loads `res://assets/astronaut/cute_astronaut.glb` |
| Unity 6 | `engines/unity/LunaQuest.Unity/Assets/Scripts/ColonyWorld.cs` loads `Art/Astronaut/cute_astronaut_baked` |

`tools/sync-content.sh` and `tools/sync-gdscript-assets.sh` copy the authoritative GLB into the
engine projects. A fresh clone of the pure GDScript project has no `assets/` folder; run
`tools/sync-gdscript-assets.sh` once before launching it, otherwise the guard and the game must
report a missing model rather than silently drawing something else.

---

## 3. Allowed and forbidden

| Change | Allowed |
|---|---|
| Fix scale, height, pose, materials, fittings, animation of the existing model | Yes |
| Make all four runtimes load the same model at the same scale | Yes |
| Improve colony buildings, terrain, textures, lighting, layout | Yes |
| Improve story, tasks, dialogue, HUD readouts, movement and physics | Yes |
| Improve playback / skip / error handling around the film | Yes |
| Re-render, re-encode, trim or replace the film | **No** |
| Create any additional intro video, for any reason | **No** |
| Download, import or bake another astronaut model | **No** |
| Add a second human character mesh or a procedural "emergency" suit | **No** |
| Let a missing GLB silently fall back to a differently shaped human | **No** |
| Use the decorative EVA suit prop as a character | **No** |

---

## 4. Locked inventory

Every tracked copy is pinned by SHA-256 in `docs/design/asset-lock.json`. Generated copies are
listed there too and are verified whenever they exist. The authoritative astronaut GLB hash is
`86ae33158e9e7656410cde7c19db14ba8640ef808476afad4f50b4227a6841a2` (957,708 bytes).

---

## 5. Guard

```bash
./tools/check-immutable-assets.sh
```

The guard verifies that every locked file exists with the exact recorded size and hash, that no
unlisted intro video or astronaut mesh file has appeared, that every generated copy matches its
source, and that the decorative EVA suit prop is not referenced by any runtime code. Run it
before committing art or engine changes. It exits non-zero on any violation.

`node tools/check-content.mjs` remains the general content consistency check; this guard is
specifically about the two frozen assets.

---

## 6. If a fix is genuinely needed

1. Confirm the change is a **fix** (Section 3, allowed), not a replacement.
2. Fix the code or the runtime transform first. Prefer not to touch the GLB bytes at all.
3. If the GLB bytes themselves must change (for example repairing an index), make the minimal
   edit, re-bake the Unity OBJ from it, update every copy listed in `asset-lock.json`, and update
   the hashes in the same commit with a written justification.
4. Re-run the guard. A hash change without an updated lock file and a justification is a
   failed build, not a silent drift.
5. Never couple such a fix to the intro film. The film is not part of that change.

---

## 7. Why this rule exists

The film and the astronaut are the two things a player recognises first. Keeping them fixed
means all future effort goes into the parts that still need work: the buildings and their
shapes, the realism of the colony, the textures, the tasks the player performs, the setting,
the movement and the physics. Continuity here is a feature, not a limitation.
