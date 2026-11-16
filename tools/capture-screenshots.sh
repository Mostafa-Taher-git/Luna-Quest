#!/usr/bin/env bash
#
# Capture the four README frames (docs/screenshots/*.png) from the Godot .NET edition.
# No browser anywhere (H1/A1): the game renders its own frames through Main.cs's capture
# flags (--autostart, --portrait, --drive, --no-board, --look, --screenshot), Xvfb supplies
# a fixed display, and Pillow crops the detail frame. One run leaves every frame ready to
# commit.
#
#   tools/capture-screenshots.sh                 # all four frames into docs/screenshots/
#   tools/capture-screenshots.sh portrait        # only frames whose name contains this
#   GODOT=/path/to/Godot tools/capture-screenshots.sh
#   OUT_DIR=/tmp/shots tools/capture-screenshots.sh
#
# Needs: dotnet, xvfb-run, python3 with Pillow, and a Godot .NET binary
# ($GODOT, default godot_mono on PATH).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PROJECT="engines/godot/LunaQuest.Godot"
OUT="${OUT_DIR:-docs/screenshots}"
GODOT_BIN="${GODOT:-godot_mono}"

for need in xvfb-run dotnet python3; do
  command -v "$need" >/dev/null || { echo "capture: '$need' is required" >&2; exit 1; }
done
command -v "$GODOT_BIN" >/dev/null || {
  echo "capture: '$GODOT_BIN' not found — set GODOT=/path/to/Godot_v4.7.2-stable_mono_linux_x86_64" >&2
  exit 1
}
python3 -c "import PIL" 2>/dev/null || {
  echo "capture: python3 needs Pillow (pip install pillow)" >&2; exit 1
}

TMP="$(mktemp -d /tmp/luna-captures.XXXXXX)"
trap 'rm -rf "$TMP"' EXIT

echo "capture: building the C# (Debug — the assembly an editor run loads)"
dotnet build "$PROJECT/LunaQuest.Godot.csproj" -c Debug

# A fresh checkout has no .godot/ yet; without the import pass every frame would open on
# missing assets. The import may warn, it must not need the editor.
if [ ! -d "$PROJECT/.godot" ]; then
  echo "capture: first run — importing the project"
  "$GODOT_BIN" --headless --path "$PROJECT" --import > "$TMP/import.log" 2>&1 || true
  grep -Ei "parse error|script error|failed to load script" "$TMP/import.log" && {
    echo "capture: import reported script errors" >&2; exit 1; }
fi

mkdir -p "$OUT"

# The engine must NOT run --headless here: a headless window has no renderer and therefore
# no frame to save. xvfb-run gives a fixed 1600x1000 virtual screen instead, which keeps the
# game window at its full 1280x720 (a smaller real screen would shrink it).
shot() { # shot <frame> <delay-seconds> [engine flags...]
  local frame="$1" delay="$2"
  shift 2
  echo "capture: $frame"
  xvfb-run -a -s "-screen 0 1600x1000x24" "$GODOT_BIN" --path "$PROJECT" -- \
    --autostart --no-intro "$@" \
    --screenshot="$TMP/$frame.png" --shot-delay="$delay" \
    > "$TMP/$frame.log" 2>&1 || {
      echo "capture: $frame exited non-zero:" >&2; tail -20 "$TMP/$frame.log" >&2; exit 1; }
  grep -q "Screenshot saved" "$TMP/$frame.log" || {
    echo "capture: $frame saved no frame:" >&2; tail -20 "$TMP/$frame.log" >&2; exit 1; }
  # A silent renderer failure still writes a PNG sometimes; a script error never may.
  if grep -qE "SCRIPT ERROR|Parse Error|Unhandled exception" "$TMP/$frame.log"; then
    grep -E "SCRIPT ERROR|Parse Error|Unhandled exception" "$TMP/$frame.log" >&2
    exit 1
  fi
  # QA/forensics: keep the raw Godot console when a log directory is requested.
  if [ -n "${CAPTURE_LOG_DIR:-}" ]; then
    mkdir -p "$CAPTURE_LOG_DIR"
    cp "$TMP/$frame.log" "$CAPTURE_LOG_DIR/$frame.log"
  fi
}

# Frames to capture: every argument is a substring filter (like the workflow's `only`
# input); with no arguments all four are captured.
REQUESTED=("$@")
wanted() {
  [ "${#REQUESTED[@]}" -eq 0 ] && return 0
  local r
  for r in "${REQUESTED[@]}"; do
    case "$1" in *"$r"*) return 0 ;; esac
  done
  return 1
}

# The frames, with the flags each one needs:
#
#   colony          default spawn, third person — the shift task board over the colony
#   portrait        front close-up; --no-board keeps the board across the visor (it sits
#                   dead centre of the frame) out of the picture
#   astronaut-detail the suit's back and backpack: --portrait with --look applied after
#                   the portrait camera orbits the 2.5 m arm behind the commander
#   rover-driving   mid-drive from the garage apron at --look=3.54,-0.3, t=4.5 s — the
#                   rover is between the garage and the plaza, before it noses into the
#                   curb, with the garage milestone ticked on the board
if wanted colony;        then shot colony 6; fi
if wanted portrait;      then shot portrait 4.5 --portrait --no-board; fi
if wanted astronaut-detail; then shot astronaut-detail 9 --portrait --look=0,-0.04 --no-board; fi
if wanted rover-driving; then shot rover-driving 4.5 --spawn=-11,-25 --drive --look=3.54,-0.3; fi

# Move the frames into place; the detail is a crop of its raw frame (440² around the suit).
[ -f "$TMP/colony.png" ] && mv "$TMP/colony.png" "$OUT/colony.png"
[ -f "$TMP/portrait.png" ] && mv "$TMP/portrait.png" "$OUT/portrait.png"
[ -f "$TMP/rover-driving.png" ] && mv "$TMP/rover-driving.png" "$OUT/rover-driving.png"
if [ -f "$TMP/astronaut-detail.png" ]; then
  python3 - "$TMP/astronaut-detail.png" "$OUT/astronaut-detail.png" <<'PY'
import sys
from PIL import Image
raw, out = sys.argv[1], sys.argv[2]
Image.open(raw).crop((425, 240, 865, 680)).save(out)
PY
fi

# Every frame has to be the size the docs expect — a wrong size means the window was
# shrunk or the renderer gave up.
python3 - "$OUT" <<'PY'
import sys
from pathlib import Path
from PIL import Image

out = Path(sys.argv[1])
expect = {"colony.png": (1280, 720), "portrait.png": (1280, 720),
          "rover-driving.png": (1280, 720)}
bad = []
for name, size in expect.items():
    path = out / name
    if not path.exists():
        continue                      # not requested this run
    got = Image.open(path).size
    if got != size:
        bad.append(f"{name}: {got}, expected {size}")
detail = out / "astronaut-detail.png"
if detail.exists():
    w, h = Image.open(detail).size
    if w != h:
        bad.append(f"astronaut-detail.png: {w}x{h}, expected a square crop")
if bad:
    sys.exit("capture: wrong frame sizes:\n  " + "\n  ".join(bad))
print("capture: frames in", out)
for path in sorted(out.glob("*.png")):
    print(f"  {path.name:24s} {Image.open(path).size[0]}x{Image.open(path).size[1]}"
          f"  {path.stat().st_size // 1024} kB")
PY
