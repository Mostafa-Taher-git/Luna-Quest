#!/usr/bin/env bash
# Build a Debian/Ubuntu .deb for the pure-GDScript Luna-Quest desktop build.
#
#   bash tools/package-desktop.sh <linux-binary-path> <version> <output-deb-path>
#
# Example:
#   bash tools/package-desktop.sh \
#     engines/godot-gdscript/LunaQuest.GDScript/build/linux/LunaQuest.x86_64 \
#     3.3 out/LunaQuest-Linux-amd64.deb
#
# The GDScript export presets set binary_format/embed_pck=true, so the Linux
# export is a single self-contained binary. The package only has to drop it
# into /opt, add a launcher, a menu entry and an icon. Nothing is downloaded
# or installed at runtime: no Godot editor, no .NET/Mono, no Python.
#
# Works as root or as an ordinary user (dpkg-deb --root-owner-group does not
# need fakeroot or sudo). Static assets come from packaging/ so CI and local
# builds produce byte-identical layout.
set -euo pipefail

if [ "$#" -ne 3 ]; then
    echo "Usage: bash tools/package-desktop.sh <linux-binary-path> <version> <output-deb-path>" >&2
    exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$script_dir/.." && pwd)"
packaging="$root/packaging"

binary="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
version="$2"
mkdir -p "$(dirname "$3")"
output="$(cd "$(dirname "$3")" && pwd)/$(basename "$3")"

for asset in luna-quest.desktop control.template postinst prerm luna-quest.png; do
    if [ ! -f "$packaging/$asset" ]; then
        echo "error: missing packaging asset: $packaging/$asset" >&2
        exit 1
    fi
done

if [ ! -s "$binary" ]; then
    echo "error: Linux binary is missing or empty: $binary" >&2
    exit 1
fi
if [ ! -x "$binary" ]; then
    echo "note: Linux binary is not executable; the package will chmod it to 755" >&2
fi

# Debian versions must start with a digit and may contain digits, dots,
# pluses and dashes. Anything else (a stray 'v', a tag suffix, a shell
# metacharacter) is rejected instead of producing a broken package.
if [[ ! "$version" =~ ^[0-9]+([.+-][0-9]+)*$ ]]; then
    echo "error: '$version' is not a valid Debian version (digits, dots, dashes only)" >&2
    exit 2
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
# mktemp creates 0700; dpkg records that mode for the payload's root entry.
chmod 755 "$tmp"

mkdir -p "$tmp/DEBIAN" \
         "$tmp/usr/bin" \
         "$tmp/opt/luna-quest" \
         "$tmp/usr/share/applications" \
         "$tmp/usr/share/icons/hicolor/256x256/apps" \
         "$tmp/usr/share/doc/luna-quest"

# 1. The game itself: one file, PCK embedded.
cp "$binary" "$tmp/opt/luna-quest/luna-quest"
chmod 755 "$tmp/opt/luna-quest/luna-quest"

# 2. Launcher on PATH. Godot writes saves to user:// (the user's XDG data
#    home), never into /opt, so the game runs unprivileged and needs no sudo.
cat > "$tmp/usr/bin/luna-quest" <<'LAUNCHER'
#!/bin/sh
# Luna-Quest — The Living Moon Colony
# Start from a writable, user-owned directory. Godot keeps saves under
# user:// (~/.local/share/godot/app_userdata), which is always in the
# player's home, so nothing ever needs to be written into /opt.
if [ -n "${HOME:-}" ] && [ -d "$HOME" ]; then
    cd "$HOME" || true
fi
exec /opt/luna-quest/luna-quest "$@"
LAUNCHER
chmod 755 "$tmp/usr/bin/luna-quest"

# 3. Menu entry and icon (shared with CI via packaging/).
cp "$packaging/luna-quest.desktop" "$tmp/usr/share/applications/luna-quest.desktop"
cp "$packaging/luna-quest.png" "$tmp/usr/share/icons/hicolor/256x256/apps/luna-quest.png"
chmod 644 "$tmp/usr/share/applications/luna-quest.desktop" \
          "$tmp/usr/share/icons/hicolor/256x256/apps/luna-quest.png"

# 4. Short documentation shipped inside the package.
cat > "$tmp/usr/share/doc/luna-quest/copyright" <<'COPYRIGHT'
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: Luna-Quest
Source: https://github.com/Mostafa-Taher-git/Luna-Quest

Files: *
Copyright: 2026 Luna-Quest contributors
License: MIT
 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:
 .
 The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.
 .
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 DEALINGS IN THE SOFTWARE.

Note: third-party art, fonts, voice recordings, engine code and research
material remain under their own licenses. See CREDITS.md upstream.
COPYRIGHT

cat > "$tmp/usr/share/doc/luna-quest/README" <<'DOC'
Luna-Quest — The Living Moon Colony
===================================

Start "Luna-Quest" from your applications menu, or run: luna-quest

This package installs the pure-GDScript Godot 4 build. It is one
self-contained executable in /opt/luna-quest/ (the game data is embedded
in the binary), plus a launcher, a desktop entry and an icon.

Nothing else is needed: no Godot editor, no .NET/Mono runtime, no Python,
no unpacking, no extra files next to the binary. Godot stores saves in
~/.local/share/godot/app_userdata/ (user://), so the game runs as a
normal user and never needs sudo.

Upstream: https://github.com/Mostafa-Taher-git/Luna-Quest
License:  MIT — see the copyright file in this directory.
DOC
chmod 644 "$tmp/usr/share/doc/luna-quest/copyright" "$tmp/usr/share/doc/luna-quest/README"

# 5. Maintainer scripts (all 755, as dpkg requires).
cp "$packaging/postinst" "$tmp/DEBIAN/postinst"
cp "$packaging/prerm" "$tmp/DEBIAN/prerm"
chmod 755 "$tmp/DEBIAN/postinst" "$tmp/DEBIAN/prerm"

# 6. control, with Installed-Size computed from what we actually install
#    (in KiB; the DEBIAN/ directory is metadata and is excluded).
installed_size="$(du -sk "$tmp/usr" "$tmp/opt" | awk '{ sum += $1 } END { print sum + 1 }')"
sed -e "s|@@VERSION@@|$version|g" \
    -e "s|@@INSTALLED_SIZE@@|$installed_size|g" \
    "$packaging/control.template" > "$tmp/DEBIAN/control"
chmod 644 "$tmp/DEBIAN/control"

# 7. Build. --root-owner-group forces root:root ownership without fakeroot.
#    -Zgzip matters: dpkg-deb >= 1.21.18 (Ubuntu 24.04, the GitHub runner)
#    defaults to zstd. Debian 11 / older dpkg cannot read zstd; Ubuntu 22.04
#    can, but gzip is the maximum-compatibility choice and keeps
#    "sudo apt install ./..." working on every supported distribution.
dpkg-deb --build --root-owner-group -Zgzip "$tmp" "$output"

echo
echo "== Built $output ($(stat -c %s "$output") bytes) =="
echo "--- dpkg-deb --info ---"
dpkg-deb --info "$output"
echo "--- dpkg-deb --contents ---"
dpkg-deb --contents "$output"
