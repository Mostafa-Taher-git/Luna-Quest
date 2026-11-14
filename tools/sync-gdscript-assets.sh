#!/usr/bin/env bash
# Populate the GDScript project's assets folder from the .NET Godot project.
#
# The GDScript edition is a separate Godot project, but it ships the same astronaut, the same
# kit models, the same intro film and the same voice pack. Copying 79 MB into the repository a
# second time would be silly, so this script is the single source of those files: run it once
# after cloning (or before exporting) and the project is complete.
#
#   bash tools/sync-gdscript-assets.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
src="$root/engines/godot/LunaQuest.Godot"
dst="$root/engines/godot-gdscript/LunaQuest.GDScript"

if [ ! -d "$src/assets" ]; then
  echo "No assets in $src — the .NET Godot project is the source of the shared files." >&2
  exit 1
fi

mkdir -p "$dst/assets" "$dst/data"

copy_tree() {
  local name="$1"
  if [ -d "$src/assets/$name" ]; then
    rm -rf "$dst/assets/$name"
    cp -R "$src/assets/$name" "$dst/assets/$name"
    echo "  assets/$name"
  fi
}

echo "Syncing assets into engines/godot-gdscript/LunaQuest.GDScript:"
copy_tree astronaut
copy_tree models
copy_tree video
copy_tree voice
copy_tree textures
copy_tree real
for f in "$src/assets"/*.woff; do
  [ -e "$f" ] || continue
  cp "$f" "$dst/assets/"
done

# Content JSON is authored in content/ and committed into every engine's data folder.
for name in buildings npcs incidents crops learning research dialogue voices; do
  cp "$root/content/$name.json" "$dst/data/$name.json"
done
echo "  data/*.json"

echo "Done. Open the folder with Godot 4.7.2 (standard build, no .NET) and press F5."
