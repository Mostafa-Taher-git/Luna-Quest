# Luna-Quest documentation

This folder keeps the source design and research material that the game is built from.

## Design

| File | What it is |
|---|---|
| `design/Luna-Quest_GDD_AR_v2.2.pdf` | The Arabic game design document, v2.2 |
| `design/Luna-Quest_GDD_AR_v2.2.docx` | Editable Word version of the GDD |
| `design/Luna-Quest_Dual_Engine_Architecture_EN_v2.2.md` | Shared-core and Godot/Unity architecture |
| `design/Luna-Quest_CHANGELOG_EN_v2.2.md` | What changed in the design |
| `design/Luna-Quest_Player_NPC_Roster_AR_v2.2.json` | Player/NPC roster source data |
| `design/Roles and thing  i want to have in my game.md` | Team requirements note |
| `design/Luna-Quest_Immutable_Assets_EN.md` | Permanent rule: the intro film and the Cute Astronaut are frozen |
| `design/asset-lock.json` | SHA-256 pins for every copy of the two frozen assets |

`tools/check-immutable-assets.sh` enforces the lock. Run it before committing art or engine
changes: it verifies the pinned hashes, rejects any additional intro video or astronaut mesh,
and keeps the decorative EVA suit prop out of every character loading path.

## Research

`research/` contains the Luna-Quest research notes and diagrams. They retain their NASA,
CSA, DONKI, OSDR and other source links. The game numbers are anchored to those sources;
the research is not a flight-certified design.

## Screenshots

| File | View |
|---|---|
| `screenshots/colony.png` | Default spawn, third-person, Shackleton rim |
| `screenshots/portrait.png` | Close-up of the astronaut and visor |
| `screenshots/astronaut-detail.png` | Crop of the backpack and suit details |

## Licenses

- `licenses/DejaVuSans-copyright.txt` — DejaVu font license
- `licenses/Noto-copyright.txt` — Noto font license

Third-party attribution is in the repository root `CREDITS.md`. The CC BY 4.0 licenses for
the astronaut and Earth texture are linked there.
