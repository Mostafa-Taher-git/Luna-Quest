#!/usr/bin/env python3
"""Engine parity guard for Luna-Quest (Godot .NET / Godot GDScript / Unity).

The architecture only holds if there is ONE simulation, ONE content set and ONE Moon.
This script fails (exit 1) if any engine has drifted from the single source of truth:

  1. Unity's Assets/Scripts/Core must be a byte-identical mirror of shared/LunaQuest.Core
     (Unity cannot compile C# outside Assets, so tools/sync-content.sh mirrors it).
  2. content/*.json must be identical in content/ and in all three engine data homes.
  3. The GDScript terrain port (scripts/terrain.gd) must keep the shared LunarTerrain
     constants (seed, flat centre/radius, fade width).

Run from anywhere:  python3 tools/check-engine-parity.py [repo-root]
No third-party dependencies. Wire it into CI (see docs/design/CI_Parity_Job.md).
"""
import os
import re
import sys

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1
                       else os.path.join(os.path.dirname(__file__), ".."))
FAIL = []


def read(path):
    with open(os.path.join(ROOT, path), "rb") as f:
        return f.read()


def check_core_mirror():
    shared_root = "shared/LunaQuest.Core"
    unity_core = "engines/unity/LunaQuest.Unity/Assets/Scripts/Core"
    shared = []
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, shared_root)):
        dirnames[:] = [d for d in dirnames if d not in ("obj", "bin")]
        for fn in filenames:
            if fn.endswith(".cs"):
                shared.append(os.path.relpath(os.path.join(dirpath, fn), ROOT))
    seen = {}
    for rel in shared:
        base = os.path.basename(rel)
        if base in seen:
            FAIL.append(f"core: duplicate basename {base} ({seen[base]} and {rel})")
        seen[base] = rel
        mirror = f"{unity_core}/{base}"
        if not os.path.exists(os.path.join(ROOT, mirror)):
            FAIL.append(f"core: Unity mirror missing {mirror}")
        elif read(rel) != read(mirror):
            FAIL.append(f"core: Unity mirror drifted from {rel} -> run tools/sync-content.sh")
    print(f"core mirror: {len(shared)} files checked")


def check_content():
    names = [f for f in os.listdir(os.path.join(ROOT, "content")) if f.endswith(".json")]
    homes = [
        "engines/godot/LunaQuest.Godot/data",
        "engines/godot-gdscript/LunaQuest.GDScript/data",
        "engines/unity/LunaQuest.Unity/Assets/StreamingAssets/content",
    ]
    for name in names:
        src = read(f"content/{name}")
        for home in homes:
            path = f"{home}/{name}"
            if not os.path.exists(os.path.join(ROOT, path)):
                FAIL.append(f"content: missing {path}")
            elif read(path) != src:
                FAIL.append(f"content: {path} drifted -> run tools/sync-content.sh")
    print(f"content: {len(names)} files x {len(homes)} engine homes checked")


def check_terrain_constants():
    cs = read("shared/LunaQuest.Core/Terrain/LunarTerrain.cs").decode("utf-8")
    gd = read("engines/godot-gdscript/LunaQuest.GDScript/scripts/terrain.gd").decode("utf-8")
    pairs = [
        (r"flatCenterZ = ([0-9.]+)", r"p_fcz: float = ([0-9.]+)", "flat centre Z"),
        (r"flatRadius = ([0-9.]+)", r"p_flat: float = ([0-9.]+)", "flat radius"),
        (r"fadeWidth = ([0-9.]+)", r"p_fade: float = ([0-9.]+)", "fade width"),
    ]
    for cs_re, gd_re, label in pairs:
        a = re.search(cs_re, cs)
        b = re.search(gd_re, gd)
        if not a or not b:
            FAIL.append(f"terrain: could not read {label} constant")
        elif float(a.group(1)) != float(b.group(1)):
            FAIL.append(f"terrain: {label} differs (C# {a.group(1)} vs GDScript {b.group(1)})")
    if "SITE_SEED := 1337" not in gd:
        FAIL.append("terrain: GDScript site seed is not 1337")
    print("terrain: shared constants checked")


def main():
    check_core_mirror()
    check_content()
    check_terrain_constants()
    if FAIL:
        print("\nPARITY FAILED:")
        for f in FAIL:
            print("  - " + f)
        sys.exit(1)
    print("\nPARITY OK: one simulation, one content set, one Moon.")


if __name__ == "__main__":
    main()
