# Credits and third-party notices

Luna-Quest is a team project. The source code is MIT-licensed (see `LICENSE`). The assets
below keep their own licenses; the MIT license does not replace them.

---

## Astronaut — “Cute Astronaut”

- **Author:** haefu
- **Source:** <https://sketchfab.com/3d-models/cute-astronaut-816bd786fe8b4559a009de6a95582003>
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0)
  <https://creativecommons.org/licenses/by/4.0/>
- **Files:** `assets/astronaut/cute_astronaut.glb`,
  `engines/godot/LunaQuest.Godot/assets/astronaut/cute_astronaut.glb`,
  `engines/unity/LunaQuest.Unity/Assets/Resources/Art/Astronaut/cute_astronaut_baked.obj`

**Changes made for Luna-Quest:**

- Repaired three triangle indices in the source GLB so the model renders without holes.
- Recoloured the visor surface and restored a dark glass dome inside the visor rim.
- Added a chest control pack, screen, orange tabs and a rear life-support backpack with
  tanks and bands. These are separate procedural meshes, not edits to the original scan.
- Froze the single Sketchfab action at `0.4 s` (standing idle) for both engines.
- Baked a static OBJ at that pose for Unity; Godot loads the GLB and freezes the same pose.

The original model remains available from the source URL under CC BY 4.0. If you reuse the
astronaut, credit **haefu** and link the Sketchfab page and the CC BY 4.0 license.

---

## Earth texture

- **Source:** Solar System Scope — <https://www.solarsystemscope.com/textures/>
- **License:** CC BY 4.0 — <https://creativecommons.org/licenses/by/4.0/>
- **Files:** `assets/textures/earth.jpg`,
  `engines/godot/LunaQuest.Godot/assets/textures/earth.jpg`,
  `engines/unity/LunaQuest.Unity/Assets/Resources/Textures/earth.jpg`

The texture is used as the Earth disc seen from the Moon. It is not a NASA image and is not
presented as one. Credit **Solar System Scope** when redistributing the texture.

---

## Fonts

### DejaVu Sans

- **Files:** `assets/fonts/DejaVuSans.ttf`,
  `engines/unity/LunaQuest.Unity/Assets/Resources/Fonts/DejaVuSans.ttf`
- **License:** Bitstream Vera / DejaVu license (permissive, attribution required).
- **License text:** `docs/licenses/DejaVuSans-copyright.txt`

### Noto Naskh Arabic

- **Files:** `assets/fonts/NotoNaskhArabic.ttf`,
  `engines/unity/LunaQuest.Unity/Assets/Resources/Fonts/NotoNaskhArabic.ttf`
- **License:** SIL Open Font License 1.1.
- **License text:** `docs/licenses/Noto-copyright.txt`

The Godot project uses the `.woff` builds of the same font families
(`assets/noto-sans-latin.woff`, `assets/noto-sans-arabic.woff`). They are covered by the same
font licenses.

---

## Crew voice pack

- **Files:** `assets/audio/voice/*.mp3`, `assets/audio/voice/pack.json`,
  `assets/audio/voice/manifest.json`, and the copies under both engine projects.
- **Authoring manifest:** `content/voices.json`.
- **Generator:** `tools/generate_voice_pack.py` (development-time only).
- **Engine:** [Piper](https://github.com/rhasspy/piper), MIT license, running locally.
- **Runtime:** the game plays committed MP3 files. It does **not** call a cloud TTS service
  and does not synthesise speech while running.

The 55 lines were generated for this build from open voice models. Every line is English
speech with an English and Arabic subtitle in `pack.json`. The voice-model licenses are:

| Crew member | Piper model | Dataset / model license |
|---|---|---|
| Dr. Safaa | `en_GB-cori-high` | Public domain (LibriVox) |
| Noor | `en_US-john-medium` | Public domain (LibriVox) |
| Dr. Alaa | `en_US-norman-medium` | Public domain (LibriVox) |
| Abdel Aziz | `en_US-joe-medium` | CC0 (OHF-Voice voice datasets) |
| Ahmed | `en_US-bryce-medium` | Public domain (upstream model card) |
| NEXO (robot) | AI studio voice "Puck" + robot filter (ffmpeg) | Generated for this project |
| Mostafa | `en_US-mike-medium` | CC0 (OHF-Voice voice datasets) |

Model cards and downloads: <https://huggingface.co/rhasspy/piper-voices>. Full notices:
[`docs/licenses/piper-voices.md`](docs/licenses/piper-voices.md).

The generated performances are synthetic. They are not recordings of the named crew, and
the speaker names in `content/npcs.json` are fictional colony characters. Keep the
attribution above if you redistribute the MP3 files, especially the CC BY 4.0 voice.

The game is fully playable with the sound off: every line also appears as a short subtitle.

---

## Design documents and research

- `docs/design/` — the Luna-Quest GDD v2.2 (Arabic), dual-engine architecture v2.2,
  changelog, roster and the “Roles and things I want” design note. These are team documents.
- `docs/research/` — Luna-Quest research notes and diagrams. They retain their NASA, CSA and
  other source links. NASA and CSA source material is used for reference and attribution;
  the notes themselves are team work.

The mission numbers in the simulation are anchored to published values, including:

- Chang'e-4 Lunar Neutron Detector surface dose rate: `13.2 µGy/h`.
- NASA Moon facts: synodic month `29.53059` Earth days.
- ISS-class water recovery: about `93%`.
- Crew metabolic rates: about `0.84 kg` O₂ and `1.0 kg` CO₂ per person per day.
- Crew water use: about `11 L` per person per day gross.
- Crop reference cycles and yields from the CSA lunar food-production research in
  `docs/research/`.

The simulation is an educational approximation, not a flight-certified life-support model.

---

## Engines and tools

- **Godot Engine** — © Godot Engine contributors, MIT license.
  <https://godotengine.org/>
- **Unity** — Unity is a trademark of Unity Technologies. The Unity project uses only
  built-in Unity modules.
- **.NET** — © .NET Foundation and contributors, MIT license.

---

## Real-world 3D assets and textures (v3)
| Asset | Source | License |
|---|---|---|
| Moon Rock 01-07 photoscanned boulders (`assets/real/rocks`) | [Poly Haven](https://polyhaven.com/models/rocks) | CC0 |
| Moon Dusted 03 regolith PBR set (`assets/real/regolith`) | [Poly Haven](https://polyhaven.com/a/moon_dusted_03) | CC0 |
| Blue Marble Earth (`assets/real/earth`) | NASA Earth Observatory, via Wikimedia Commons | Public domain |

Re-download with `python3 tools/fetch_real_assets.py`.

## How to attribute this project

If you publish a build or a derivative, keep the credits above and include:

> Astronaut “Cute Astronaut” by haefu, CC BY 4.0, via Sketchfab. Earth texture by Solar
> System Scope, CC BY 4.0. Crew voice models from the Piper project and the upstream
> datasets listed above. Luna-Quest design and code by the Luna-Quest team.

If you reuse the voice pack, keep the licensing table and the CC BY 4.0 attribution for the
`en_GB-alba-medium` voice. The other seven models used here are public domain or CC0.
