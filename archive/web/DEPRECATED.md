# Browser edition - RETIRED (2026-11)

Luna-Quest targets exactly three engines: **Godot .NET**, **Godot GDScript** and **Unity**.
This folder is no longer developed, built or deployed.

Why it is still here:
- `static/intro-poster.jpg` is a **locked asset** pinned by SHA-256 in
  `docs/design/asset-lock.json`; deleting it would break the immutable-assets guard.
- `test/` holds the historical simulation/world tests that were used during development.

Do not add features here. The Pages workflow and the CI web job have been removed.
See `docs/design/Luna-Quest_Architecture_Review_EN_v3.md`.
