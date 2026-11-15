/**
 * Guard the "one model, one scale" rule for the web runtime.
 *
 * These checks are deliberately source-level: they do not need a GPU or a browser, so they
 * catch a reintroduced substitute suit or a drifting height constant in CI before it ever
 * reaches a player. The policy lives in docs/design/Luna-Quest_Immutable_Assets_EN.md.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import {
  ASTRONAUT_SHA256,
  ASTRONAUT_SOURCE_PATH,
  BODY_RADIUS_M,
  CREW_HEIGHT_M,
  EYE_HEIGHT_M,
  PLAYER_HEIGHT_M,
} from '../src/world/crew-metrics.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const webRoot = join(root, 'web');
const src = (path) => readFile(join(webRoot, 'src', path), 'utf8');

const sha256 = (buffer) => createHash('sha256').update(buffer).digest('hex');

test('the commander and the crew share one height and one eye line', () => {
  assert.equal(CREW_HEIGHT_M, 1.75);
  assert.equal(PLAYER_HEIGHT_M, CREW_HEIGHT_M);
  assert.equal(EYE_HEIGHT_M, 1.62);
  assert.equal(BODY_RADIUS_M, 0.34);
  assert.ok(EYE_HEIGHT_M < CREW_HEIGHT_M, 'the eye must sit below the top of the helmet');
});

test('no runtime file hardcodes a different astronaut height', async () => {
  for (const file of ['main.js', 'player.js', 'world/crew.js', 'world/astronaut.js']) {
    const text = await src(file);
    const hardcoded = text.match(/height\s*:\s*1\.[0-9]+/g) ?? [];
    assert.deepEqual(
      hardcoded,
      [],
      `${file} still passes a literal height; import crew-metrics.js instead`,
    );
    assert.ok(
      !/const\s+HEIGHT\s*=\s*1\.[0-9]+/.test(text),
      `${file} still defines a literal body height`,
    );
  }
});

test('a missing GLB is a hard failure, never a substitute astronaut', async () => {
  const text = await src('world/astronaut.js');
  assert.ok(!/fallbackSuit/.test(text), 'a fallback suit was reintroduced');
  assert.ok(!/CapsuleGeometry/.test(text), 'a second procedural human body was reintroduced');
  assert.ok(!/emergency suit/i.test(text), 'an emergency suit comment was reintroduced');
  assert.ok(/throw new Error\(/.test(text), 'a missing model must throw instead of drawing something else');
  assert.ok(/will not draw a substitute/.test(text), 'the error must explain the one-model rule');
  assert.ok(/ASTRONAUT_MODEL_PATH/.test(text), 'the model path must come from crew-metrics.js');
  assert.ok(
    !/['"`]models\/astronaut\/cute_astronaut\.glb['"`]/.test(text),
    'the model path must not be duplicated as a literal',
  );
});

test('every copy of the Cute Astronaut GLB is the pinned model', async () => {
  const lock = JSON.parse(await readFile(join(root, 'docs/design/asset-lock.json'), 'utf8'));
  const pinned = lock.locked.find((entry) => entry.path === ASTRONAUT_SOURCE_PATH);
  assert.ok(pinned, 'the authoritative GLB is not in the asset lock');
  assert.equal(pinned.sha256, ASTRONAUT_SHA256);

  const authoritative = await readFile(join(root, ASTRONAUT_SOURCE_PATH));
  assert.equal(sha256(authoritative), ASTRONAUT_SHA256);

  // All engine copies that are committed to the repository must be byte-identical.
  const copies = [
    'engines/godot/LunaQuest.Godot/assets/astronaut/cute_astronaut.glb',
    'web/public/models/astronaut/cute_astronaut.glb',
    'web/dist/models/astronaut/cute_astronaut.glb',
  ];
  for (const copy of copies) {
    try {
      const bytes = await readFile(join(root, copy));
      assert.equal(sha256(bytes), ASTRONAUT_SHA256, `${copy} differs from the authoritative model`);
    } catch (err) {
      if (err.code !== 'ENOENT') throw err; // generated copies may be absent in a fresh clone
    }
  }
});

test('the asset lock still pins one intro film and one astronaut model', async () => {
  const lock = JSON.parse(await readFile(join(root, 'docs/design/asset-lock.json'), 'utf8'));
  const intro = lock.locked.filter((entry) => /\/intro\.(mp4|webm|ogv)$/.test(entry.path));
  assert.equal(intro.length, 4, 'three authoritative containers plus the Godot project copy');
  const authoritative = intro.filter((entry) => entry.path.startsWith('assets/video/'));
  assert.equal(authoritative.length, 3, 'there must be exactly three film containers, no fourth film');
  assert.deepEqual(
    authoritative.map((entry) => entry.path.split('.').pop()).sort(),
    ['mp4', 'ogv', 'webm'],
  );
  for (const entry of intro) {
    const bytes = await readFile(join(root, entry.path));
    assert.equal(sha256(bytes), entry.sha256, `${entry.path} changed - the film is frozen`);
  }
  assert.ok(lock.rules.intro.includes('Never re-render'));
  assert.ok(lock.rules.astronaut.includes('only human character model'));
});

test('no web source references the decorative EVA suit prop', async () => {
  const walk = async (dir) => {
    const entries = await readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const path = join(dir, entry.name);
      if (entry.isDirectory()) {
        await walk(path);
      } else if (/\.(js|mjs|json|html)$/.test(entry.name)) {
        const text = await readFile(path, 'utf8');
        assert.ok(!/eva-suit-23f03707/.test(text), `${path} treats the decorative prop as a character`);
      }
    }
  };
  await walk(join(webRoot, 'src'));
});
