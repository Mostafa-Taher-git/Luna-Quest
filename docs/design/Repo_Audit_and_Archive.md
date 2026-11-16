# Luna-Quest — Repository Audit and Archive Plan

**Phase V deliverable — READ-ONLY VERIFICATION.**
Status: **COMMITTED deliverable** (T6). Its Phase V verification was read-only; no asset was
moved or deleted by this document. It records the evidence behind Phase T (T1–T6).

- Repository: `Mostafa-Taher-git/Luna-Quest`
- Local clone: `.` (this repository, repository root)
- Audited at: `7262236` on `main`
- Governing brief: **FINAL CONSOLIDATED BRIEF** (supersedes the earlier "LOCAL FINISHING PASS",
  tasks A–F, v3.0.0)
- Method: V1 → V5, all read-only. Every claim below was run or read live, not recalled.

---

## 1. Inventory — tracked files per top-level folder

`git ls-files | awk … | uniq -c` → **1014 tracked files**, matching the brief exactly.

| Path | Tracked | What it is | Role after Phase T |
|---|---:|---|---|
| `engines/` | 702 | Three engines: `godot/` (.NET), `godot-gdscript/`, `unity/` | **KEEP — the product** |
| `assets/` | 161 | Authoritative media: astronaut GLB, intro film ×3, real PBR sets, kit models, voice pack, music, fonts | **KEEP** (contains 6 of the 8 locked files) |
| `docs/` | 47 | `design/` 12, `research/` 21, `screenshots/` 9, `licenses/` 3, `README.md`, `TEST_REPORT.md` | **KEEP**, minus archived screenshots (§5) |
| `web/` | 41 | Retired Three.js browser edition (already self-declared `DEPRECATED.md`) | **ARCHIVE → `archive/web/`** (carries the locked poster) |
| `shared/` | 27 | `LunaQuest.Core` (25 source files) + `LunaQuest.Tests` | **KEEP — source of truth (H4)** |
| `tools/` | 12 | 6 guards/sync, 2 packaging, 4 asset/voice helpers | **KEEP** (1 candidate → archive, §5) |
| `content/` | 8 | The one content set (H5) | **KEEP** |
| `packaging/` | 6 | `.deb` control, desktop entry, postinst, prerm, icon, README | **KEEP — required by T3** |
| `.github/` | 5 | `ci.yml`, `godot-dotnet.yml`, `godot-gdscript.yml`, `release.yml`, `screenshots.yml` | **KEEP**, edited in T2a/T3 |
| *(root)* | 5 | `README.md`, `CREDITS.md`, `LICENSE`, `.gitignore`, `.gitattributes` | **KEEP** (README rewritten in T6) |
| `archive/` | — | **ABSENT** | **CREATE in T2b** |

No Git LFS (`.gitattributes` uses plain binary markers) — confirmed.

---

## 2. V1 — State and tools

```
git rev-parse HEAD        → 7262236b1c6bdc054ce3a9460d85299c935183e1
git branch --show-current → main
git status --porcelain    → (empty)
git log --oneline -5      → 7262236, 69eca04, 2016400, f1c3f63, 1cb430e
git ls-files | wc -l      → 1014
```

| Tool | Required | Found | Verdict |
|---|---|---|---|
| `godot` | 4.7.2 STANDARD | `4.7.2.stable.official.ed1daf0bf` | ✅ |
| `godot_mono` | 4.7.2 MONO | `4.7.2.stable.mono.official.ed1daf0bf` | ✅ |
| `dotnet` | 8 | `8.0.428` | ✅ |
| `node` | present | `v26.10.0` | ✅ |
| `python3` | present | `3.14.4` | ✅ |
| `git` | present | `2.53.0` | ✅ |
| `gh` | present | `2.104.0` (2026-11-12) | ✅ |
| `xvfb-run`, `unzip`, `curl`, `wget` | present | all present | ✅ |
| **export templates** | matching 4.7.2 | **0 files at audit start** | ⚠️ see below |

Both Godot binaries are already the correct versions and are symlinked into `~/.local/bin`
(`godot`, `godot_mono`, `dotnet`) — installed before approval as V1 permits.

**Export templates.** The user's Godot `export_templates/` directory contained 0 files. The release
URLs resolve correctly (HTTP 302 → 200 verified with `curl -sSIL`). Both `.tpz` files
(`Godot_v4.7.2-stable_export_templates.tpz` and `…_mono_export_templates.tpz`) were re-launched
into a temporary directory outside the repository and will be unpacked into the user's
`export_templates/4.7.2.stable{,.mono}/` directories.

> **Impact assessment: none on Phase V.** Export templates are only needed to *export* a build
> (T3). Every V4 suite — import, selftest, audit, 400-frame run, rover test — ran green without
> them. `release.yml` fetches its own templates in CI (lines 103–113), so CI is unaffected too.

---

## 3. V2 — Remote truth

```
git fetch --tags --force   → clean
git tag --list             → v3.0  v3.1  v3.2  v3.3
git ls-remote --tags origin→ v3.0, v3.1, v3.1^{}, v3.2, v3.2^{}, v3.3   (identical)
```

`gh` is authenticated as **`Mostafa-Taher-git`** (keyring, scopes `gist read:org repo`).

`gh release list --limit 30`:

| Release | Tag | Date |
|---|---|---|
| Luna-Quest v3.3 *(Latest)* | `v3.3` | 2026-11-15 |
| Luna-Quest v3.2 | `v3.2` | 2026-11-14 |
| Luna-Quest v3.1 | `v3.1` | 2026-11-14 |
| Luna-Quest v3.0 - Six crew + NEXO | `v3.0` | 2026-11-14 |

`gh release view v3.3` → https://github.com/Mostafa-Taher-git/Luna-Quest/releases/tag/v3.3

Assets on v3.3 (11): `intro.mp4`, `LunaQuest-GDScript-{Linux,Web,Windows}.zip`,
`LunaQuest-Linux-amd64.deb`, `LunaQuest-Linux-x86_64`, `LunaQuest-Linux.zip`,
`LunaQuest-Web.zip`, `LunaQuest-Windows.exe`, `LunaQuest-Windows.zip`, `SHA256SUMS`.

Body opens *"Play in the browser: open the Pages deployment… unzip `LunaQuest-Web.zip`…"* and
contains *"The GDScript web zip is a self-contained site: unzip it behind any static server and
play."* → **all browser/web text, to be removed in T3.**

**The real diff since the last release:**

```
git log v3.3..main --oneline   → 65 commits
git diff --shortstat v3.3..main→ 76 files changed, 4644 insertions(+), 500 deletions(-)
```

**Conclusion: next version is `v3.4`.** Two-component scheme, tag `v3.4`, title
"Luna-Quest v3.4". Never `v3.0.0`, never ≤ `v3.3`. (Full commit list in §6.)

---

## 4. V3 — Files read in full (no skimming)

| File | Lines | Key finding |
|---|---:|---|
| `.github/workflows/ci.yml` | 56 | Jobs `content`, `core`, `parity` only. No GPU, no licence. **Keep as-is.** |
| `.github/workflows/godot-dotnet.yml` | 148 | `build` job: Debug+Release compile, import, 600 frames, `--rover-test`. **No web references — clean.** |
| `.github/workflows/godot-gdscript.yml` | 231 | **Web at 19 lines**: 9, 10, 20, 23, 24, 136, 165, 166, 169, 170, 177, 191, 194, 202, 205, 206, 210, 212, 224. Job `Export web, Windows and Linux`; uploads `gdscript-web` artifact; `deploy_web` input → GitHub Pages. **T3 target.** |
| `.github/workflows/release.yml` | 187 | `film` / `godot` / `gdscript` / `publish`. Web export step + `LunaQuest-GDScript-Web.zip` + web publish body. Publish attaches **only** `artifacts/godot-builds/*.zip`, `artifacts/gdscript-builds/*.zip`, `artifacts/intro-film/*.mp4`. **No `.deb`, no raw Linux binary, no `SHA256SUMS`, no Unity job.** `generate_release_notes: true`. **T3 target.** |
| `.github/workflows/screenshots.yml` | 93 | `workflow_dispatch` only. npm + playwright against `web/package-lock.json` and `web/dist`, runs `tools/capture-readme.mjs`. **T2a target.** |
| `engines/godot-gdscript/.../export_presets.cfg` | 108 | **Exactly 3 presets**: `Web` (1–46), `Linux` (47–76), `Windows` (77–108). **T3: delete `preset.0`.** |
| `engines/godot/.../export_presets.cfg` | 86 | **Exactly 2 presets**: `Windows Desktop`, `Linux`. **No Web preset.** Line 6 comment still says *"repository also ships a native browser build under `web/`"* → stale, fix in T3. |
| `tools/package-desktop.sh` | 171 | `bash tools/package-desktop.sh <linux-binary> <version> <output.deb>`; uses `packaging/` for byte-identical layout; `dpkg-deb --root-owner-group` (no fakeroot). **Does not make `SHA256SUMS`.** Ready for T3. |
| `packaging/README.md` `control.template` `luna-quest.desktop` `postinst` `prerm` (+`luna-quest.png`) | 6 files | `.deb` metadata for `luna-quest` amd64; deps list covers Godot's X11/Wayland/ALSA stack; postinst only chmods + refreshes desktop/icon DBs; prerm is a no-op. **All consistent — KEEP.** |
| `tools/capture-readme.mjs` | 491 | Serves **`web/dist`** (line 31), drives `window.lunaQuest`, 14 shots (`hero-1..6`, `console-1..3`, `crew-1..5`). **Cannot survive `web/` archiving → archive in T2b.** |
| `engines/godot/.../scripts/Main.cs` | 744 | CLI flags confirmed exactly as briefed: `--autostart --no-intro --spawn= --look= --screenshot= --shot-delay= --portrait --first-person --cinematic --rover-test --test-dialogue= --voice-check --voice-demo`. `--screenshot` defaults to a 4.0 s timer, `CaptureAndQuit()` saves PNG. **Ready for T2a.** |
| `engines/godot/.../scripts/RoverAudit.cs` | — | Holds `main.Hud`; T1 assertion host. |
| `engines/unity/.../RoverPlayTest.cs` | 205 | No save/load test, no board test. **T4 target.** |
| `engines/godot-gdscript/.../scripts/hud.gd` | 665 | **Has the counter**: `mission_label` (28, 144–148), `"Tasks: %d of %d …"` at **256** (EN) / **251** (AR), also 424 and 598. `_next_objective` at 263. **No shift board.** |
| `engines/godot/.../scripts/Hud.cs` | 1121 | **`grep -i tasks` → NO MATCH.** `_mission` = `"Day N / 30"` (line 712), `UpdateState()` at 706. **No counter, no board.** |
| `engines/unity/.../ColonyHud.cs` | 838 | `grep -i "tasks\|shift board"` → **NO MATCH.** `Draw()` 168, `DrawStatusPanel` 449, `DrawLogPanel` 502, journal 637–658, help 660, math 691, dialogue 781. **No board.** |
| `README.md` | 507 | Hero = `docs/screenshots/preview-candidates.jpg` (line 5); browser claims 7–13; stale `544/190` counts; `web/` in repo map line 372. **T6 target.** |
| `docs/design/asset-lock.json` | 112 | **8 locked** (astronaut GLB ×2, Unity OBJ, intro mp4/webm/ogv + Godot ogv, poster) / **11 derived** = 1 GDScript `intro.ogv` (exists after sync) + **10 `web/` paths that do not exist on disk**. Confirms the brief. |
| `tools/check-immutable-assets.sh` | 153 | 7 checks; reads lock, hashes, scans for new intro videos / astronaut meshes / EVA-suit props / `FallbackSuit` builders. Read-only. |

### 4.1 Where the code contradicts the brief — TRUST THE CODE

| # | Brief says | Code says | Resolution |
|---|---|---|---|
| C1 | "T1 … **Replace the GDScript/.NET counter**" | GDScript has the counter (`hud.gd:256`); **.NET `Hud.cs` has no `Tasks` string at all**; Unity has none | T1 **replaces** the GDScript counter and **adds** the board to .NET and Unity. No .NET counter exists to replace. |
| C2 | "export_presets.cfg presets: Web, Linux, Windows, **Luna-Quest**" | GDScript has **exactly 3**: Web, Linux, Windows. No preset is named `Luna-Quest` anywhere. | Remove `preset.0` (Web) only. Ignore the phantom 4th preset. |
| C3 | "`check-immutable-assets.sh` (8 locked files, **10** hash checks)" | Prints **"8 locked files, 12 hash checks"** after a sync | The count is *state-dependent*: 8 locked + 1 present derived (`GDScript intro.ogv`) + 3 `cute_astronaut.glb` copies = 12. Not a failure; report the live number. |
| C4 | (implied) web is fully retired | `release.yml` still runs `--export-release "Web"` and ships `LunaQuest-GDScript-Web.zip`; commit `e0ad65a` **deliberately kept it**: *"The GDScript web export of the .NET-free edition is a separate engine build and stays."* | The repo distinguishes the **retired `web/` folder** (removed) from the **Godot Web export** (kept). **H1 is stricter than the repo's own policy** and removes both. This is a *new* requirement, not a correction. |
| C5 | `docs/design/Luna-Quest_Architecture_Review_EN_v3.md:36`: *"the release workflow no longer builds or publishes `web/`"* | True for the `web/` folder; the Godot Web export remains | Doc is accurate *as written* (backticked `web/`), but reads as "no web builds at all". Fix the wording in T3 alongside the export removal. |

---

## 5. V4 — Baseline suites (REAL counts, all green)

Run in order, at `7262236`, with `git status --porcelain` **empty before and after**.

| # | Command | Result | Exit |
|---|---|---|---:|
| 1 | `node tools/check-content.mjs` | `16 buildings, 7 crew, 15 incident rules, 11 objectives, 17 research notes, 44 dialogue topics — all consistent`, `94 voice lines verified` | 0 |
| 2 | `dotnet run --project shared/LunaQuest.Tests/… -c Release` | **426 passed, 0 failed** | 0 |
| 3 | `python3 tools/check-engine-parity.py` | `core mirror: 23 files` · `content: 8 files x 3 engine homes` · `terrain worst difference 4.81e-13 m` · **`PARITY OK: one simulation, one content set, one Moon.`** | 0 |
| 4 | `bash tools/check-immutable-assets.sh` | **`8 locked files, 12 hash checks, 1 intro film, 1 astronaut model - all pinned and unchanged`** | 0 |
| 5 | `bash tools/sync-gdscript-assets.sh` | Synced `assets/{astronaut,models,video,voice,textures,real}` + `data/*.json` | 0 |
| 6 | `godot --headless --path $P --import` | `DONE` (192 reimport steps) | 0 |
| 7 | `godot --headless --path $P -- --selftest` | **`SELFTEST OK — 564 checks passed`** (30 simulated days, 81 events, 51 beats) | 0 |
| 8 | `godot --headless --path $P -- --audit` | **`AUDIT OK — 221 checks passed`** · `16 buildings, 45 consoles` | 0 |
| 9 | `godot --headless --path $P --quit-after 400` | `34 kit modules` · `16 buildings, 45 consoles` · `7 crew` · `11 tasks` | 0 |
| 10 | `dotnet build engines/godot/…/LunaQuest.Godot.csproj -c Release` | **`0 Warning(s) 0 Error(s)`** | 0 |
| 11 | `godot_mono --headless --path engines/godot/… -- --rover-test` | **`ROVER TEST OK — 30 checks passed`** · `16 buildings, 7 crew, 15 incident rules` | 0 |

**Post-run integrity:** `git status --porcelain` → **empty**; `git ls-files | wc -l` → **1014**;
`git rev-parse --short HEAD` → **7262236**. Sync/import wrote gitignored files only, exactly as
the brief allows. **No STOP condition triggered.**

> Note on exit codes: runs 8 and 9 print Godot shutdown noise (`Leaked instance dependency`,
> `ObjectDB instances were leaked`, `PagedAllocator`) — these are the standard headless-editor
> teardown messages, exit status is `0`, and the audit/selftest verdict lines are what matter.

**H9 confirmed live:** 16 buildings / 45 consoles (audit) and 11 objectives (content check).

---

## 6. V5 — Archive candidates (grep + read before classifying)

Rule applied: **KEEP unless proven unused.**

| Candidate | Evidence | Verdict | Reason |
|---|---|---|---|
| **`web/`** (41 files) | Own `web/DEPRECATED.md` declares it *"RETIRED (2026-11) … no longer developed, built or deployed"*, kept **only** because `static/intro-poster.jpg` is locked and `test/` holds historical tests. Referenced outside itself by 18 sites (§6.1). | **ARCHIVE → `archive/web/`** | Self-proven retired. Moves with the locked poster; `asset-lock.json` updated in the same commit (H2/T2b). |
| **`tools/capture-readme.mjs`** (491 lines) | **Exactly one** external reference: `.github/workflows/screenshots.yml:51`. Line 31 `const ROOT = resolve('web/dist')` — the directory it serves will not exist after T2b. | **ARCHIVE** | Cannot function once `web/` archives; replaced by `tools/capture-screenshots.sh` in T2a. |
| **`tools/migrate_crew_v3.py`** (105 lines) | **Zero references anywhere.** Read in full: a *completed* one-shot migration (6 crew + NEXO), **hardcoded `root = pathlib.Path(...)`** (line 6) pointing at a foreign absolute path that does not exist in this clone, and it *mutates* `content/*.json` if ever run. Crew v3 landed long ago (`npcs.json` already has the 7 crew). | **ARCHIVE** | Proven unused **and** unrunnable here (wrong absolute path). Archiving preserves provenance; keeping it in `tools/` invites an accidental content-mutating run. |
| **`docs/screenshots/web-{airlock,crew,overview,plaza}.png`** (4 files, 1024×576 / 1280×720) | **Zero references** to `screenshots/web-` anywhere in the repo. | **ARCHIVE** | Web-era renders, unreferenced. |
| **`docs/screenshots/preview-candidates.jpg`** (1480×928) | **One** reference: `README.md:5` (the hero). Read the image: it is a **3×3 contact sheet of WEB-edition frames** (`candidate-hero-a/b/c`, `candidate-scene-a/b/c`, `candidate-crew-a/b/c`) showing the web HUD (`COLONY STATUS` / `COLONY LOG` / **`SHIFT BOARD`**). | **ARCHIVE** | Web contact sheet; README hero must be swapped to a real .NET frame first (T2a/T6). Bonus: it is visual proof the web **SHIFT BOARD** already exists — the T1 reference. |
| **`docs/screenshots/candidate-crew-b.jpg`** (480×270) | **Zero references.** It is one of the 9 cells of the sheet above. | **ARCHIVE** | Unreferenced web-era candidate. |

### 6.1 Every repo-wide reference to `web/` outside itself (T2b must fix all)

```
.github/workflows/screenshots.yml:34      cache-dependency-path: web/package-lock.json
.gitignore:61-64                          web/node_modules|public|dist|.vite
CREDITS.md:119                            … by tools/sync-gdscript-assets.sh and web/scripts/sync-assets.mjs
docs/design/asset-lock.json:50,64,68,72,76,80,84,88,92,96,100   (1 locked + 10 derived)
docs/design/CI_Parity_Job.md:32            see web/DEPRECATED.md
docs/design/Luna-Quest_Architecture_Review_EN_v3.md:5,7,36
docs/design/Luna-Quest_Immutable_Assets_EN.md:26,37,39,82
docs/design/Luna-Quest_Rover_and_Realism_EN.md:52
docs/design/Luna-Quest_Verification_Report_EN.md:55
docs/research/Luna-Quest_Surface_Habitat_Design_EN.md:56,80
docs/TEST_REPORT.md:25                     web/dist builds clean
engines/godot/…/export_presets.cfg:6        comment: "native browser build under web/"
engines/godot-gdscript/…/export_presets.cfg:12  export_path="build/web/index.html"
engines/godot-gdscript/…/README.md:18       "ported from … web/src/sim"
engines/godot-gdscript/…/scripts/crew.gd:23 "reads the same value from web/src/world/crew-metrics.js"
README.md:78,79,372                         "web/ folder stays …", repo map line
tools/capture-readme.mjs:7,31
```

### 6.2 KEEP — explicitly confirmed active

| Item | Proof it is used |
|---|---|
| `tools/bake_astronaut_obj.py` | 5 references: `tools/sync-content.sh:54`, `ColonyWorld.cs:855` (errors if the bake is missing), `README.md:417`, Unity `README.md:115`, `Luna-Quest_Immutable_Assets_EN.md:60`. **Provenance of the locked OBJ — KEEP.** |
| `packaging/` (6 files) | Read in full; consumed by `tools/package-desktop.sh` → required by **T3**. **KEEP.** |
| `tools/check-{content.mjs,engine-parity.py,immutable-assets.sh}` | H10 guards, run green in §5. **KEEP.** |
| `tools/sync-{content.sh,gdscript-assets.sh}` | H4/H5/H7 sync, run green in §5. **KEEP.** |
| `tools/{fetch_real_assets.py,generate_voice_pack.py,glb_math.py}` | `glb_math.py` is imported by `bake_astronaut_obj.py`; voice pack feeds the 94 verified lines. **KEEP.** |
| `docs/research/` (21), `docs/design/` GDD files | Design record; several are cited by guards (`Luna-Quest_Immutable_Assets_EN.md` is the lock policy). **KEEP.** |
| Unity's 54 kit GLBs | Staged for a future glTF step — brief says KEEP. **KEEP.** |
| All 8 locked files | **H2 — never moved, edited or deleted.** |
| `docs/screenshots/{colony,portrait,astronaut-detail}.png` | Referenced: `docs/README.md:36,37,38`. **KEEP** (T2a replaces their *contents* with real .NET frames; `rover-driving.png` is new). |

**Net effect of Phase T on `tools/`:** 12 → 11 (`migrate_crew_v3.py` archived) → 12 again
(`capture-readme.mjs` archived, `capture-screenshots.sh` added).

---

## 7. The real diff since v3.3 (65 commits → next = **v3.4**)

`git log v3.3..main --oneline` — grouped:

**Rover (the big feature of this cycle)** — driveable crew rover across all three engines:
`78c39c8` feat(unity), `6c11e9f` feat(godot .NET), `bc1f130`+`b38e7af` feat(gdscript),
`d120a9b`+`c56832c` feat(web), `7ca2ba6`/`8cd90a4`/`5c00222` fixes, `e864ad1`/`b28fb5a`/
`6743c18`/`3fab9f2`/`008b55f`/`10cafc7` fixes, `4d8e4fa` fix(web).

**One Moon (H6)** — `8600881` exact GDScript port of `LunarTerrain`, `81b76ac` Unity ground from
the shared heightfield, `60a7b52` collision matches, `1a6afd0`+`610b4ec` ground winding fixed
(was face-down), `8a1d618` Unity surface check, `80ac9c4` parity guard proves it numerically,
`5a70e19` selftest holds the port.

**Immutable assets (H2)** — `64f6ad0` lock intro film + Cute Astronaut, `b07992a`/`8f202c3`
one astronaut model at 1.75 m, `7795f91` pin the GDScript intro copy.

**CI** — `6f8f7e4` parity + asset-lock job, **removed `pages.yml` and the `web` CI job**,
`e0ad65a` removed the `web:` job from `release.yml`, `3d1d8d5`…`c91549b` the whole
`godot-dotnet.yml` bring-up, `957998e` `--rover-test` in CI.

**Tests** — `df23cab` Unity `RoverPlayTest`, `c42b6eb` audit drives the T5 checklist,
`1cb430e`/`ead01d1`/`69eca04` save/restore + `RestartMission` fixes.

**Docs** — `fd13516` architecture review v3, `8491f1e` retire the browser edition (docs),
`76bfc3b` rover controls + credits, `f1c3f63`/`2016400` verification report.

**Assets** — `3ac7087`/`c0def45` Poly Haven `moon_02` regolith PBR (CC0, 2K), `361a942` wear it,
`2a419bb`/`69c3fe4` real CC0 kit rovers + photoscanned rocks.

**76 files changed: +4644 / −500.** Notable: `pages.yml` **deleted**; `godot-dotnet.yml` and
`RoverAudit.cs`/`RoverController.cs`/`CrewRover.cs`/`RoverPlayTest.cs`/`terrain.gd` added;
`web/DEPRECATED.md` added; 20+ `.uid`/`.import` sidecars committed (per the gotcha).

> Nothing in this range has shipped. **All 65 commits are unreleased** — they are the substance
> of the v3.4 changelog.

---

## 8. Proposed v3.4 scope (what T1–T6 will actually change)

| Task | Scope | Proof of done |
|---|---|---|
| **T1** | 14-row shift task board in `hud.gd`, `Hud.cs`, `ColonyHud.cs` (11 objectives + mission line + 2 rover tasks), bilingual, current-shift first; extend `selftest`/`audit`/`--rover-test`/`RoverPlayTest`. One commit per engine. | 3 engines show 14 rows; `learning.json` stays 11; suites green |
| **T2a** | `tools/capture-screenshots.sh` + `screenshots.yml` rewritten to Godot .NET `Main.cs` flags; real `colony.png`, `portrait.png`, `astronaut-detail.png`, `rover-driving.png`. | PNGs are .NET frames, README points at them |
| **T2b** | `archive/` created; `git mv` of approved candidates; **`web/` + locked poster moved with `asset-lock.json` updated in the same commit, hash unchanged**; every reference in §6.1 fixed. Archive moves committed separately from the web/+lock commit. | `check-immutable-assets.sh` green at the new poster path |
| **T3** | Remove `preset.0` (Web) + all web steps from `godot-gdscript.yml` and `release.yml`; add `.deb` via `package-desktop.sh` + `SHA256SUMS`; attach `.deb`, raw Linux binary, zips, `SHA256SUMS`, `intro.mp4`; rewrite the publish body (no browser text); film job stays a pure copy. | CI dry-run produces the full v3.3-equivalent set minus web |
| **T4** | `RoverPlayTest.cs`: save/restore round-trip (position, battery, occupied, `HasDriven`, `ReachedIce`), old-save-without-rover-keys loads, 14-row board in both languages. Code only — no glTFast, no render-pipeline change, no new Input System. | Compile-clean by review; Unity runtime still pending |
| **T5** | Merge once → guards on main → **owner confirms tag** → `git tag -a v3.4 -m "Luna-Quest v3.4"` → push → `CHANGELOG.md` from `git log v3.3..main`, bilingual summary. | Release URL with **no web artifact of any kind** |
| **T6** | README truth: real counts, three engines, rover controls (`E`, `W/S`, `A/D`, `Shift`, `Space`, `E`), no browser/Node instructions, `archive/` in the repo map, new .NET hero/gallery, links to this file + CHANGELOG + v3.4 + `SHA256SUMS`. Unity row: *"harness ready; build locally in Unity 6000.3.0f1; runtime verification pending."* | Every claim re-verified against the tree |

**Not in scope:** any Unity CI job (licence/secrets = owner decision), any change to H1–H11,
any re-render of the intro film, any replacement of the Cute Astronaut, any change to the save
schema (stays 2), any simplification of `terrain.gd`'s uint32 masks.

---

## 9. Disclosures (things done before this brief arrived)

1. **A stale git worktree was removed.** `.kilo/worktrees/abrasive-homburg` was deleted *before*
   the consolidated brief landed. At the time it was a fully-merged ancestor of `main` with
   **0 unique commits and 0 uncommitted changes**, and it actively broke
   `tools/check-immutable-assets.sh` (it carried an old `FallbackSuit` string that trips check
   #7). Contents backed up to a directory outside the repository; the branch
   `abrasive-homburg` was **preserved** (never deleted). No commit, tag or release was affected.
   *If the owner wants it restored, the backup is intact.*
2. **`git config --global --add safe.directory <worktree path>`** was added for that worktree.
   Now moot, left in place.
3. **Tool symlinks** `godot`, `godot_mono`, `dotnet` into `~/.local/bin` — required by V1 and
   permitted before approval. Binaries are the correct 4.7.2 standard/mono pair.
4. **Export templates** were missing (0 files); downloads re-started (§2). Not needed for any
   V4 suite.
5. **No commit, push, merge, tag, release, workflow edit, move or deletion has been made.**
   `git status --porcelain` is empty and HEAD is still `7262236`.

---

## 10. Recommendation

Phase V is complete and **all gates are green**. The brief's verified facts hold, with five
corrections recorded in §4.1 (C1–C5) — none of which weaken H1–H11.

**Requesting owner approval to begin Phase T (T1 → T6), in order, one focused commit each,
branch → single merge → guards on main → tag `v3.4` only after explicit confirmation.**

---

## 11. T5 — A2 shipped-vs-new determination (v3.4 vs v3.3)

**Method.** `git log --oneline v3.3..phase-t-v3.4` and `git diff --name-status v3.3..HEAD`:
**73 commits**, **125 files changed** (46 added, 36 modified, 1 deleted, 42 renamed into
`archive/`). Only what is absent at `v3.3` is claimed as new. Anything already shipped at
`v3.3` is listed separately so the release notes do not re-announce it.

### 11.1 Genuinely new at v3.4 (absent from `v3.3`)

| Group | Files |
|---|---|
| Driveable crew rover | `engines/godot-gdscript/LunaQuest.GDScript/scripts/rover.gd` (+ `.uid`), `engines/godot/LunaQuest.Godot/scripts/RoverController.cs` (+ `.uid`), `engines/unity/LunaQuest.Unity/Assets/Scripts/CrewRover.cs`, `archive/web/src/world/rover.js` |
| Shared-terrain ports | `engines/godot-gdscript/LunaQuest.GDScript/scripts/terrain.gd` (+ `.uid`) |
| Headless playtests | `engines/godot/LunaQuest.Godot/scripts/RoverAudit.cs` (+ `.uid`), `engines/unity/LunaQuest.Unity/Assets/Scripts/RoverPlayTest.cs` |
| Guards and docs | `tools/check-engine-parity.py`, `tools/check-immutable-assets.sh`, `docs/design/asset-lock.json`, `docs/design/CI_Parity_Job.md`, `docs/design/Luna-Quest_Architecture_Review_EN_v3.md`, `docs/design/Luna-Quest_Verification_Report_EN.md`, `docs/design/Luna-Quest_Rover_and_Realism_EN.md`, `docs/design/Luna-Quest_Immutable_Assets_EN.md`, `docs/research/Luna-Quest_Surface_Habitat_Design_EN.md`, `archive/web/DEPRECATED.md` |
| Assets | the Poly Haven `moon_02` PBR set — 5 JPGs under `engines/godot/LunaQuest.Godot/assets/real/regolith/moon_02/` (**0 files at `v3.3`**) |
| Capture tooling | `tools/capture-screenshots.sh`, `docs/screenshots/rover-driving.png` |
| CI | `.github/workflows/godot-dotnet.yml` added; `.github/workflows/pages.yml` deleted |
| Archive moves | `web/**` → `archive/web/**` (41 files), six web-era screenshots → `archive/docs/screenshots/`, two superseded tools → `archive/tools/` |

### 11.2 Already shipped at `v3.3` — do **not** re-announce

`packaging/`, `tools/package-desktop.sh`, `docs/screenshots/preview-candidates.jpg`,
`docs/screenshots/candidate-crew-b.jpg` (all four archived in T2b), the
`assets/models/lunar_colony/*.glb` decorative/regolith kit (unchanged since `v3.3`),
`shared/LunaQuest.Core/Terrain/LunarTerrain.cs`, and `assets/astronaut/cute_astronaut.glb`.

**Correction to the working note.** The earlier note recorded "`assets/real/` (0 at v3.3)" for
the *top-level* directory only. `engines/godot/LunaQuest.Godot/assets/real/{earth,regolith,rocks}`
already existed at `v3.3` (75 files); what is genuinely new is the `moon_02` set inside
`regolith/` (0 at `v3.3`). No top-level `assets/real/` exists now or at `v3.3`.

### 11.3 Screenshot note — expected differences, not defects

The four frames in `docs/screenshots/` were regenerated in T2a by running the real Godot .NET
build (`tools/capture-screenshots.sh`). They replace retired Three.js renders, so differences
between old and new frames are expected rather than regressions: the live game reports
**7 CREW + NEXO** (`content/npcs.json` = six named humans + NEXO) while the retired web material
still describes **nine people** (`README.md:42`, `packaging/control.template:11`), and the art
pass has changed since those frames were rendered. In some frames the suit renders very bright
against bloom; that is a Compatibility-renderer capture artefact, not a game defect. T6 fixes
the stale "nine people" text.

### 11.4 Open decision for the owner — `moon_02` import sidecars

Five untracked sidecars remain by design:
`engines/godot/LunaQuest.Godot/assets/real/regolith/moon_02/*.jpg.import`. Godot regenerates
them on first import, so a fresh clone works without them; committing them would pin the import
settings. **Recommendation:** decide in a later focused commit; they are deliberately not part
of any Phase T commit.

### 11.5 Branch state at T5

`phase-t-v3.4` is **8 commits ahead of `main`** (`7262236`): `1fb85ce` → `bd0123b` →
`37254f8` → `f2bfb82` → `058ee1a` → `f593dfe` → `5ee9180` → `74d15db`. Guards green after
every commit (content 16/7/15/11/17/44, parity OK, immutable assets 8 locked / 12 hash checks).
