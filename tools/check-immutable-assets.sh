#!/usr/bin/env bash
# Guard the two frozen assets: the existing intro film and the existing Cute Astronaut.
#
#   ./tools/check-immutable-assets.sh
#
# The policy is in docs/design/Luna-Quest_Immutable_Assets_EN.md and the pinned hashes are in
# docs/design/asset-lock.json. This script never writes anything. It fails loudly if a locked
# file changed, if a new intro video or astronaut mesh appeared, if a generated copy drifted
# from its source, or if the decorative EVA suit prop entered a runtime loading path.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

python3 - <<'PY'
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path('.')
LOCK_FILE = ROOT / 'docs/design/asset-lock.json'
GUARD = 'tools/check-immutable-assets.sh'
PROP = 'eva-suit-23f03707'

problems = []
checks = 0

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def tracked_files():
    for path in ROOT.rglob('*'):
        if not path.is_file():
            continue
        if '.git' in path.parts or 'node_modules' in path.parts:
            continue
        yield path

try:
    lock = json.loads(LOCK_FILE.read_text())
except Exception as err:
    print(f'immutable asset check: cannot read {LOCK_FILE.as_posix()}: {err}', file=sys.stderr)
    sys.exit(1)

locked = lock.get('locked', [])
derived = lock.get('derived', [])
known_paths = {entry['path'] for entry in locked} | {entry['path'] for entry in derived}

# 1. Every tracked copy is present, with the exact recorded size and hash.
for entry in locked:
    rel = entry['path']
    path = ROOT / rel
    checks += 1
    if not path.is_file():
        problems.append(f'locked asset missing: {rel}')
        continue
    size = path.stat().st_size
    if size != entry.get('bytes'):
        problems.append(f'locked asset size changed: {rel} is {size} B, expected {entry.get("bytes")} B')
    digest = sha256(path)
    if digest != entry.get('sha256'):
        problems.append(f'locked asset bytes changed: {rel} sha256 is {digest}')

# 2. Generated copies are build artefacts: absent is fine, present must be byte-identical.
for entry in derived:
    rel = entry['path']
    path = ROOT / rel
    if not path.is_file():
        continue
    checks += 1
    digest = sha256(path)
    if digest != entry.get('sha256'):
        problems.append(f'generated copy drifted from its source: {rel} sha256 is {digest}')

# 3. No additional intro video may exist anywhere in the tree.
VIDEO_EXT = {'.mp4', '.webm', '.ogv', '.mov', '.mkv', '.avi', '.m4v', '.mpg'}
intro_allowed = {
    entry['path'] for entry in locked + derived
    if Path(entry['path']).name.startswith('intro') and Path(entry['path']).suffix.lower() in VIDEO_EXT
}
for path in tracked_files():
    rel = path.as_posix()
    if path.name.startswith('intro') and path.suffix.lower() in VIDEO_EXT:
        if rel not in intro_allowed:
            problems.append(f'unlisted intro video (a new film is forbidden): {rel}')

# 4. No new astronaut / suit mesh file may be introduced.
MESH_EXT = {'.glb', '.gltf', '.obj', '.fbx', '.dae'}
MESH_HINTS = ('astronaut', 'eva-suit', 'eva_suit', 'spacesuit', 'space-suit', 'suit')
allowed_meshes = {'cute_astronaut.glb', 'cute_astronaut_baked.obj', f'{PROP}.glb'}
for path in tracked_files():
    rel = path.as_posix()
    if path.suffix.lower() in MESH_EXT and any(h in path.name.lower() for h in MESH_HINTS):
        if path.name not in allowed_meshes:
            problems.append(f'unlisted astronaut mesh (a second model is forbidden): {rel}')

# 5. Every copy of the astronaut GLB in the tree is the same bytes.
ASTRONAUT_HASH = '86ae33158e9e7656410cde7c19db14ba8640ef808476afad4f50b4227a6841a2'
for path in tracked_files():
    if path.name != 'cute_astronaut.glb':
        continue
    checks += 1
    digest = sha256(path)
    if digest != ASTRONAUT_HASH:
        problems.append(f'astronaut GLB copy differs from the authoritative model: {path.as_posix()}')

# 6. The decorative EVA suit prop must stay out of every runtime loading path.
CODE_EXT = {'.js', '.mjs', '.cjs', '.ts', '.tsx', '.gd', '.cs', '.py', '.sh', '.html', '.json'}
for path in tracked_files():
    rel = path.as_posix()
    if rel.startswith('docs/') or rel == GUARD:
        continue
    # Test code may name the prop in an assertion; it is not a runtime loading path.
    if 'test' in path.parts or 'tests' in path.parts:
        continue
    if path.suffix.lower() not in CODE_EXT:
        continue
    if PROP not in path.read_bytes().decode('utf-8', errors='ignore'):
        continue
    if path.name == 'manifest.json':
        continue  # asset library index: decorative, not a character load
    problems.append(f'decorative EVA suit prop referenced by runtime code: {rel}')

# 7. No runtime may still contain a substitute-suit builder. NEXO is a robot, not a human,
#    so its procedural body is allowed; a second human-shaped suit is not.
SUIT_BUILDERS = ('fallbackSuit', '_fallback_suit', 'FallbackSuit', 'BuildFallbackSuit')
for path in tracked_files():
    rel = path.as_posix()
    if rel.startswith('docs/') or rel == GUARD:
        continue
    if 'test' in path.parts or 'tests' in path.parts:
        continue
    if path.suffix.lower() not in {'.js', '.mjs', '.gd', '.cs', '.py', '.sh'}:
        continue
    text = path.read_bytes().decode('utf-8', errors='ignore')
    for name in SUIT_BUILDERS:
        if name in text:
            problems.append(f'substitute astronaut suit builder "{name}" still exists in {rel}')

# ---- report ----------------------------------------------------------------
if problems:
    print(f'immutable asset check FAILED with {len(problems)} problem(s):', file=sys.stderr)
    for problem in problems:
        print(f'  - {problem}', file=sys.stderr)
    print('See docs/design/Luna-Quest_Immutable_Assets_EN.md', file=sys.stderr)
    sys.exit(1)

print(
    f'immutable asset check: {len(locked)} locked files, {checks} hash checks, '
    f'1 intro film, 1 astronaut model - all pinned and unchanged'
)
PY
