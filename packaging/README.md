# `packaging/` — the desktop install assets

Everything the `.deb` needs, kept in the repository so a CI build and a local
build produce the same package. `tools/package-desktop.sh` copies these files
into the dpkg tree; it does not generate any of them at build time.

| File | Where it ends up in the package | Purpose |
|---|---|---|
| `luna-quest.desktop` | `usr/share/applications/luna-quest.desktop` | Application-menu entry (`Exec=luna-quest`, `Icon=luna-quest`) |
| `control.template` | `DEBIAN/control` | Metadata template. `@@VERSION@@` and `@@INSTALLED_SIZE@@` are filled in by the script |
| `postinst` | `DEBIAN/postinst` | Re-asserts 755 modes, refreshes the desktop/icon caches when available |
| `prerm` | `DEBIAN/prerm` | No system state to remove; exits cleanly |
| `luna-quest.png` | `usr/share/icons/hicolor/256x256/apps/luna-quest.png` | 256x256 menu icon, deep blue-black with `#e08a3c` accents |

The launcher (`usr/bin/luna-quest`), the `copyright` file and the short
`usr/share/doc/luna-quest/README` are written by `tools/package-desktop.sh`
because they are trivial and contain no project-specific assets.

## Local build

```bash
# 1. Export the pure-GDScript Linux preset once (Godot 4.7.2 standard build):
./godot --headless --path engines/godot-gdscript/LunaQuest.GDScript \
  --export-release "Linux" build/linux/LunaQuest.x86_64

# 2. Package it:
bash tools/package-desktop.sh \
  engines/godot-gdscript/LunaQuest.GDScript/build/linux/LunaQuest.x86_64 \
  3.3 /tmp/LunaQuest-Linux-amd64.deb

# 3. Inspect / install:
dpkg-deb --info /tmp/LunaQuest-Linux-amd64.deb
dpkg-deb --contents /tmp/LunaQuest-Linux-amd64.deb
sudo apt install /tmp/LunaQuest-Linux-amd64.deb
```

No `sudo`, `fakeroot` or network access is needed to *build* the package:
`dpkg-deb --build --root-owner-group` forces root ownership of the payload.
Only `dpkg-deb`, `coreutils` and `bash` are required.

The build passes `-Zgzip`. Newer `dpkg-deb` (Ubuntu 24.04, used by
`ubuntu-latest`) defaults to zstd, whose `control.tar.zst` / `data.tar.zst`
Debian 11 and older `dpkg` cannot read. Ubuntu 22.04's `dpkg` does support
zstd, but gzip is the maximum-compatibility choice: it is understood by every
dpkg, so the same `.deb` installs everywhere.

## Why this package works without installing anything else

`engines/godot-gdscript/LunaQuest.GDScript/export_presets.cfg` sets
`binary_format/embed_pck=true` for both desktop presets, so the exported
binary contains the whole game (PCK embedded). There is no `.pck` to keep
beside it, no Godot editor, no .NET/Mono runtime and no Python. The `.deb`
just places that one binary in `/opt/luna-quest/`, adds a launcher on `PATH`
and registers a menu entry. Godot saves games under `user://`
(`~/.local/share/godot/app_userdata`), so the game never writes into `/opt`
and never needs elevated privileges.

`Depends:` lists the stock X11/OpenGL/ALSA libraries a Godot 4 Linux binary
links against. `libasound2t64 | libasound2` covers Ubuntu 24.04+ (where the
library was renamed with the 64-bit time_t transition) and older releases.
The `|` alternative is Debian syntax, not a shell pipe.

## Version

The `.deb` version is the release tag without its leading `v` (`v3.3` ->
`3.3`). `release.yml` sanitises it to digits, dots, pluses and dashes and
falls back to `3.3.0`. The script rejects anything that is not a valid
Debian version, so a malformed tag fails loudly instead of producing a
package that `apt` refuses to install.

## Regenerating the icon

`luna-quest.png` is committed as a binary. It was drawn with Python/PIL at
4x and downsampled to 256x256 with LANCZOS; this is the exact generator:

```python
"""Generate packaging/luna-quest.png — a 256x256 moon-colony icon.

Deep blue-black sky, orange (#e08a3c) accents, a habitat dome on a lunar
horizon and a starfield. Drawn at 4x and downsampled with LANCZOS so the
edges stay smooth at 256px.
"""
from PIL import Image, ImageDraw, ImageFilter
import random

S = 4
W = 256 * S
ORANGE = (224, 138, 60)
ORANGE_LIGHT = (247, 186, 110)
DOME = (206, 216, 231)
DOME_SHADE = (150, 165, 188)
GROUND = (48, 59, 79)
GROUND_LIT = (64, 77, 99)

img = Image.new("RGB", (W, W), (5, 9, 20))
d = ImageDraw.Draw(img)

# --- sky gradient -----------------------------------------------------------
for y in range(W):
    t = y / (W - 1)
    d.line([(0, y), (W, y)], fill=(int(5 + 10 * t), int(9 + 16 * t), int(20 + 27 * t)))

# --- starfield --------------------------------------------------------------
random.seed(2026)
for _ in range(110):
    x = random.randint(0, W)
    y = random.randint(0, int(W * 0.72))
    r = random.choice([1, 1, 1, 1, 2]) * S
    colour = random.choice([(188, 205, 232), (150, 174, 214), (224, 233, 250), (224, 233, 250), ORANGE_LIGHT])
    d.ellipse((x - r, y - r, x + r, y + r), fill=colour)

# --- a small Earth in the sky -----------------------------------------------
ex, ey, er = int(W * 0.79), int(W * 0.19), int(W * 0.072)
d.ellipse((ex - er, ey - er, ex + er, ey + er), fill=(36, 82, 132))
d.ellipse((ex - er, ey - er, ex + int(er * 0.35), ey + er), fill=(66, 132, 184))
d.arc((ex - er, ey - er, ex + er, ey + er), 200, 340, fill=(120, 180, 218), width=3 * S)

# --- soft orange glow behind the habitat ------------------------------------
glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
cx = int(W * 0.50)
horizon = int(W * 0.70)
gd.ellipse((cx - W * 0.36, horizon - W * 0.30, cx + W * 0.36, horizon + W * 0.10),
           fill=(224, 138, 60, 55))
glow = glow.filter(ImageFilter.GaussianBlur(W * 0.035))
img = Image.alpha_composite(img.convert("RGBA"), glow).convert("RGB")
d = ImageDraw.Draw(img)

# --- lunar ground (a wide, gently curved horizon) ---------------------------
d.ellipse((-int(W * 0.30), horizon, int(W * 1.30), horizon + int(W * 0.85)),
          fill=GROUND)
# lit rim along the horizon
d.arc((-int(W * 0.30), horizon, int(W * 1.30), horizon + int(W * 0.85)),
      188, 352, fill=GROUND_LIT, width=3 * S)
# craters
for (px, py, pw, ph) in [
    (0.10, 0.83, 0.13, 0.028), (0.31, 0.91, 0.09, 0.020),
    (0.71, 0.84, 0.15, 0.032), (0.87, 0.92, 0.08, 0.018),
    (0.47, 0.95, 0.11, 0.022),
]:
    x0, y0 = int(W * px), int(W * py)
    d.ellipse((x0, y0, x0 + int(W * pw), y0 + int(W * ph)), fill=(38, 47, 65))

# --- habitat: side modules, dome, windows, mast -----------------------------
base_y = horizon + int(W * 0.055)

# side modules (rounded rectangles with orange bands)
for left in (int(W * 0.235), int(W * 0.625)):
    top = base_y - int(W * 0.075)
    right = left + int(W * 0.14)
    bottom = base_y + int(W * 0.015)
    d.rounded_rectangle((left, top, right, bottom), radius=int(W * 0.025),
                        fill=DOME_SHADE, outline=(228, 234, 244), width=2 * S)
    d.rectangle((left + int(W * 0.012), top + int(W * 0.045),
                 right - int(W * 0.012), top + int(W * 0.058)), fill=ORANGE)

# connecting corridor behind the dome
d.rectangle((int(W * 0.37), base_y - int(W * 0.055),
             int(W * 0.63), base_y + int(W * 0.010)), fill=DOME_SHADE)

# main dome
dw, dh = int(W * 0.185), int(W * 0.155)
box = (cx - dw, base_y - dh * 2, cx + dw, base_y)
d.pieslice(box, 180, 360, fill=DOME, outline=(238, 243, 252), width=3 * S)
d.arc(box, 180, 360, fill=(255, 255, 255), width=2 * S)
# dome ribs
for frac in (-0.55, 0.0, 0.55):
    x = cx + int(dw * frac)
    d.line((x, base_y - int(dh * 1.72), x, base_y), fill=DOME_SHADE, width=2 * S)
# base line
d.line((cx - dw - int(W * 0.02), base_y, cx + dw + int(W * 0.02), base_y),
       fill=(236, 241, 250), width=3 * S)

# orange windows on the dome
for (wx, wy) in [(0.44, 0.635), (0.50, 0.655), (0.56, 0.635), (0.47, 0.685), (0.53, 0.685)]:
    x, y = int(W * wx), int(W * wy)
    r = int(W * 0.013)
    d.rounded_rectangle((x - r, y - r, x + r, y + r), radius=int(r * 0.4),
                        fill=ORANGE, outline=ORANGE_LIGHT, width=1 * S)

# antenna mast with an orange beacon
mx = cx + int(W * 0.125)
d.line((mx, base_y - dh * 2 + int(W * 0.01), mx + int(W * 0.02), base_y - dh * 2 - int(W * 0.085)),
       fill=(226, 234, 246), width=3 * S)
bx, by = mx + int(W * 0.02), base_y - dh * 2 - int(W * 0.085)
br = int(W * 0.014)
d.ellipse((bx - br, by - br, bx + br, by + br), fill=ORANGE_LIGHT)

# a small solar panel to the left, orange-framed
sx, sy = int(W * 0.075), base_y - int(W * 0.045)
d.polygon([(sx, sy), (sx + int(W * 0.105), sy - int(W * 0.025)),
           (sx + int(W * 0.105), sy + int(W * 0.045)), (sx, sy + int(W * 0.070))],
          fill=(60, 92, 132), outline=ORANGE)
d.line((sx + int(W * 0.05), sy - int(W * 0.012), sx + int(W * 0.05), sy + int(W * 0.057)),
       fill=ORANGE, width=1 * S)

# --- orange accent arc at the bottom (subtle brand mark) --------------------
d.arc((int(W * 0.30), horizon - int(W * 0.11), int(W * 0.70), horizon + int(W * 0.29)),
      200, 340, fill=ORANGE, width=2 * S)

out = img.resize((256, 256), Image.LANCZOS)
out.save("packaging/luna-quest.png", "PNG", optimize=True)
print("wrote packaging/luna-quest.png", out.size, out.mode)
```

Requires `python3 -c "import PIL"` (Pillow). The generator is not committed
as a separate script: copy the Python block above into a file (for example
`/tmp/make-icon.py`), then run `python3 /tmp/make-icon.py` from the repository
root. It writes `packaging/luna-quest.png`.
