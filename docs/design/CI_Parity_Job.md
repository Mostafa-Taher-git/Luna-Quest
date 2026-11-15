# CI parity job — apply with a Workflows-capable token

The push token used for the architecture pass has **Contents** write but not **Workflows**
write, so GitHub rejects any change under `.github/workflows/` (HTTP 403). The parity guard
script (`tools/check-engine-parity.py`) is committed and ready; the workflow wiring below must
be applied by a token that has the **Workflows** permission (fine-grained: Repository
permissions -> Workflows: Read and write; or a classic token with the `workflow` scope).

## 1. Add this job to `.github/workflows/ci.yml`

    parity:
      name: Engine parity and asset locks
      runs-on: ubuntu-latest
      steps:
        - uses: actions/checkout@v4
        - uses: actions/setup-python@v5
          with:
            python-version: '3.12'
        - name: One simulation, one content set, one Moon
          run: python3 tools/check-engine-parity.py
        - name: Immutable assets (intro film + Cute Astronaut)
          run: bash tools/check-immutable-assets.sh

## 2. Remove the retired browser job

Delete the whole `web:` job from `.github/workflows/ci.yml` (the browser edition is retired;
see `web/DEPRECATED.md`). Keep the `content` and `core` jobs.

## 3. Delete the Pages workflow

Delete `.github/workflows/pages.yml` — it only built/deployed the retired browser edition.

## Why this matters

`tools/sync-content.sh` mirrors `shared/LunaQuest.Core` into Unity and copies `content/` into
every engine, but nothing enforced it. A hand edit in Unity would silently fork the simulation.
The parity job makes drift a build failure, and runs the immutable-asset guard on every push so
the intro film and the Cute Astronaut can never be replaced unnoticed.
