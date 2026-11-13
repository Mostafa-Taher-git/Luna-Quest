#!/usr/bin/env bash
# Copy the single source of truth in content/ and assets/ into both engine projects.
#
#   ./tools/sync-content.sh
#
# The shared core is compiled directly by the Godot project and by the tests. Unity
# cannot import C# outside its Assets folder, so the core sources are mirrored into
# Assets/Scripts/Core. Edit shared/LunaQuest.Core and re-run this script; never edit
# the Unity mirror by hand.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="$ROOT/engines/godot/LunaQuest.Godot"
UNITY="$ROOT/engines/unity/LunaQuest.Unity"

if [[ ! -d "$ROOT/content" || ! -d "$GODOT" || ! -d "$UNITY" ]]; then
  echo "error: run this from inside the Luna-Quest repository" >&2
  exit 1
fi

echo "== content =="
mkdir -p "$GODOT/data" "$UNITY/Assets/StreamingAssets/content"
cp -f "$ROOT/content"/*.json "$GODOT/data/"
cp -f "$ROOT/content"/*.json "$UNITY/Assets/StreamingAssets/content/"

echo "== shared core -> Unity mirror =="
mkdir -p "$UNITY/Assets/Scripts/Core"
rm -f "$UNITY/Assets/Scripts/Core"/*.cs
find "$ROOT/shared/LunaQuest.Core" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \
  -exec cp -f {} "$UNITY/Assets/Scripts/Core/" \;

echo "== assets -> Godot =="
mkdir -p "$GODOT/assets/astronaut" "$GODOT/assets/textures" "$GODOT/assets/voice"
cp -f "$ROOT/assets/astronaut/cute_astronaut.glb" "$GODOT/assets/astronaut/"
cp -f "$ROOT/assets/textures/earth.jpg" "$GODOT/assets/textures/"
cp -f "$ROOT/assets/noto-sans-latin.woff" "$GODOT/assets/"
cp -f "$ROOT/assets/noto-sans-arabic.woff" "$GODOT/assets/"
find "$ROOT/assets/audio/voice" -name '*.mp3' -exec cp -f {} "$GODOT/assets/voice/" \;
cp -f "$ROOT/assets/audio/voice/pack.json" "$GODOT/assets/voice/"

echo "== assets -> Unity =="
mkdir -p "$UNITY/Assets/Resources/Textures" "$UNITY/Assets/Resources/Fonts" "$UNITY/Assets/Resources/Voice"
cp -f "$ROOT/assets/textures/earth.jpg" "$UNITY/Assets/Resources/Textures/"
cp -f "$ROOT/assets/fonts/DejaVuSans.ttf" "$UNITY/Assets/Resources/Fonts/"
cp -f "$ROOT/assets/fonts/NotoNaskhArabic.ttf" "$UNITY/Assets/Resources/Fonts/"
find "$ROOT/assets/audio/voice" -name '*.mp3' -exec cp -f {} "$UNITY/Assets/Resources/Voice/" \;
cp -f "$ROOT/assets/audio/voice/pack.json" "$UNITY/Assets/Resources/Voice/"
if [[ -f "$ROOT/tools/bake_astronaut_obj.py" ]]; then
  echo "   (astronaut OBJ is baked separately by tools/bake_astronaut_obj.py)"
fi

echo "done: content, core, textures, fonts and voice are in sync"
