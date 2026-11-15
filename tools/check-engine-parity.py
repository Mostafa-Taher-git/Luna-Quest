#!/usr/bin/env python3
"""Engine parity guard for Luna-Quest (Godot .NET / Godot GDScript / Unity).

The architecture only holds if there is ONE simulation, ONE content set and ONE Moon.
This script fails (exit 1) if any engine has drifted from the single source of truth:

  1. Unity's Assets/Scripts/Core must be a byte-identical mirror of shared/LunaQuest.Core
     (Unity cannot compile C# outside Assets, so tools/sync-content.sh mirrors it).
  2. content/*.json must be identical in content/ and in all three engine data homes.
  3. The GDScript terrain port (scripts/terrain.gd) must keep the shared LunarTerrain
     constants (seed, flat centre/radius, fade width).
  4. The shared heightfield and the terrain that is actually drawn must agree: the reference
     heights in the GDScript self-test (which the engine produces with `-- --selftest --print-
     terrain` against scripts/terrain.gd) are recomputed here, straight from the algorithm in
     shared/LunaQuest.Core/Terrain/LunarTerrain.cs. Three engines, one Moon — proven on every
     push instead of on trust.

Run from anywhere:  python3 tools/check-engine-parity.py [repo-root]
No third-party dependencies. Wire it into CI (see docs/design/CI_Parity_Job.md).
"""
import math
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


# ---- one Moon, numerically ---------------------------------------------------

# The points the brief names, plus a few more in the open highlands.
PARITY_POINTS = [
    (0.0, 23.0), (52.0, -34.0), (95.0, 8.0), (200.0, 200.0), (-350.0, 120.0),
    (300.0, -300.0), (1000.0, 0.0),
]
SEED = 1337


class ReferenceTerrain:
    """The algorithm of shared/LunaQuest.Core/Terrain/LunarTerrain.cs, in Python.

    Deliberately a transcription of the C# rather than a call into it: the point is to have an
    independent implementation that scripts/terrain.gd (and the C# itself) must match.
    """

    M = 0xFFFFFFFF

    def __init__(self, seed, fcx=0.0, fcz=8.0, flat=88.0, fade=90.0, rug=1.0):
        self.seed, self.fcx, self.fcz = seed, fcx, fcz
        self.flat, self.fade, self.rug = flat, max(1.0, fade), rug

    def height(self, x, z):
        dx, dz = x - self.fcx, z - self.fcz
        dist = math.sqrt(dx * dx + dz * dz)
        blend = self._smooth((dist - self.flat) / self.fade)
        if blend <= 0.0:
            return 0.0
        return (self._relief(x, z) + self._craters(x, z)) * blend * self.rug

    @staticmethod
    def _smooth(t):
        if t <= 0.0:
            return 0.0
        if t >= 1.0:
            return 1.0
        return t * t * (3.0 - 2.0 * t)

    def _relief(self, x, z):
        h = 0.0
        h += (self._fbm(x / 420.0, z / 420.0, 11, 4) - 0.5) * 2.0 * 16.0
        h += (self._fbm(x / 70.0, z / 70.0, 23, 3) - 0.5) * 2.0 * 1.4
        h += (self._fbm(x / 9.0, z / 9.0, 37, 2) - 0.5) * 2.0 * 0.12
        return h

    def _craters(self, x, z):
        return (self._layer(x, z, 520.0, 70.0, 210.0, 0.55, 101)
                + self._layer(x, z, 150.0, 14.0, 52.0, 0.75, 202)
                + self._layer(x, z, 42.0, 2.5, 13.0, 0.80, 303))

    def _layer(self, x, z, cell, r_min, r_max, presence, salt):
        cx, cz = int(math.floor(x / cell)), int(math.floor(z / cell))
        total = 0.0
        for i in (-1, 0, 1):
            for j in (-1, 0, 1):
                gx, gz = cx + i, cz + j
                h = self._hash32(gx, gz, self.seed + salt)
                if self._unit(h) > presence:
                    continue
                r = r_min + (r_max - r_min) * (self._unit(self._next32(h, 1)) ** 2.2)
                px = (gx + 0.15 + 0.7 * self._unit(self._next32(h, 2))) * cell
                pz = (gz + 0.15 + 0.7 * self._unit(self._next32(h, 3))) * cell
                t = math.sqrt((x - px) ** 2 + (z - pz) ** 2) / r
                if t > 2.6:
                    continue
                depth = r * 0.19 * (0.55 + 0.45 * self._unit(self._next32(h, 4)))
                rim = depth * 0.22
                bowl = depth * (t * t - 1.0) if t < 1.0 else 0.0
                ring = rim * math.exp(-((t - 1.0) / 0.30) ** 2.0)
                ejecta = rim * 0.35 * math.exp(-(t - 1.0) * 2.4) if t > 1.0 else 0.0
                total += bowl + ring + ejecta
        return total

    def _fbm(self, x, z, salt, octaves):
        total, amp, norm = 0.0, 0.5, 0.0
        for o in range(octaves):
            total += amp * self._value_noise(x, z, salt + o * 17)
            norm += amp
            x, z, amp = x * 2.03, z * 2.03, amp * 0.5
        return total / norm

    def _value_noise(self, x, z, salt):
        xi, zi = int(math.floor(x)), int(math.floor(z))
        fx, fz = x - xi, z - zi
        u = fx * fx * (3 - 2 * fx)
        v = fz * fz * (3 - 2 * fz)
        a = self._unit(self._hash32(xi, zi, self.seed + salt))
        b = self._unit(self._hash32(xi + 1, zi, self.seed + salt))
        c = self._unit(self._hash32(xi, zi + 1, self.seed + salt))
        d = self._unit(self._hash32(xi + 1, zi + 1, self.seed + salt))
        top = a + (b - a) * u
        bottom = c + (d - c) * u
        return top + (bottom - top) * v

    @classmethod
    def _hash32(cls, x, z, salt):
        h = (salt * 374761393 + x * 668265263 + z * 2246822519) & cls.M
        h = ((h ^ (h >> 13)) * 1274126177) & cls.M
        return (h ^ (h >> 16)) & cls.M

    @classmethod
    def _next32(cls, h, n):
        h = (h + n * 0x9E3779B9) & cls.M
        h = ((h ^ (h >> 15)) * 2246822519) & cls.M
        h = ((h ^ (h >> 13)) * 3266489917) & cls.M
        return (h ^ (h >> 16)) & cls.M

    @classmethod
    def _unit(cls, h):
        return (h & 0xFFFFFF) / float(0x1000000)


def check_terrain_values():
    reference = ReferenceTerrain(SEED)
    selftest = read("engines/godot-gdscript/LunaQuest.GDScript/scripts/selftest.gd").decode("utf-8")
    # Read the table between its `const TERRAIN_PARITY := [` and the matching `]` at line start.
    start = selftest.find("const TERRAIN_PARITY")
    if start < 0:
        FAIL.append("terrain: the GDScript self-test has no TERRAIN_PARITY table to check")
        return
    end = selftest.find("\n]", start)
    block = selftest[start:end] if end > 0 else ""
    if not block:
        FAIL.append("terrain: the GDScript parity table is not terminated")
        return
    if not block:
        FAIL.append("terrain: the GDScript self-test has no TERRAIN_PARITY table to check")
        return
    rows = re.findall(r"\[\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*\]", block)
    if len(rows) < len(PARITY_POINTS):
        FAIL.append(f"terrain: the parity table has {len(rows)} points, expected "
                    f"{len(PARITY_POINTS)}")
        return
    worst = 0.0
    for x, z, expected in rows:
        x, z, expected = float(x), float(z), float(expected)
        actual = reference.height(x, z)
        worst = max(worst, abs(actual - expected))
        # (0, 23) and (52, -34) are inside the flat pad: exactly zero, both sides.
        inside_pad = math.hypot(x, z - 8.0) <= 88.0
        if inside_pad and abs(expected) != 0.0:
            FAIL.append(f"terrain: ({x:g}, {z:g}) is inside the flat pad but the table says "
                        f"{expected}")
        if abs(actual - expected) > 1e-3:
            FAIL.append(f"terrain: ({x:g}, {z:g}) — the engine reported {expected:.6f}, the "
                        f"shared heightfield says {actual:.6f}")
    print(f"terrain values: {len(rows)} points, worst difference {worst:.2e} m")


def main():
    check_core_mirror()
    check_content()
    check_terrain_constants()
    check_terrain_values()
    if FAIL:
        print("\nPARITY FAILED:")
        for f in FAIL:
            print("  - " + f)
        sys.exit(1)
    print("\nPARITY OK: one simulation, one content set, one Moon.")


if __name__ == "__main__":
    main()
