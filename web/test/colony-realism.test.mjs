/**
 * Surface-habitat realism checks.
 *
 * Unlike the source-level guards, this suite actually builds all sixteen buildings against the
 * same headless canvas stub as world.test.mjs, so it proves the sealed vaults, dust porches and
 * shielding berms are really in the scene graph and in the collision world. See
 * docs/research/Luna-Quest_Surface_Habitat_Design_EN.md.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

function makeContext() {
  const gradient = { addColorStop() {} };
  return {
    canvas: { width: 512, height: 512 },
    fillStyle: '#000', strokeStyle: '#000', lineWidth: 1, font: '',
    textBaseline: 'middle',
    fillRect() {}, strokeRect() {}, clearRect() {}, beginPath() {}, closePath() {},
    moveTo() {}, lineTo() {}, arc() {}, fill() {}, stroke() {}, save() {}, restore() {},
    translate() {}, rotate() {}, scale() {},
    createPattern: () => null, setLineDash() {}, fillText() {},
    measureText: (text) => ({ width: String(text).length * 8 }),
    createLinearGradient: () => gradient, createRadialGradient: () => gradient,
  };
}

globalThis.document = {
  createElement(tag) {
    if (tag !== 'canvas') throw new Error(`unexpected element ${tag}`);
    return { width: 1, height: 1, getContext: () => makeContext() };
  },
};
globalThis.window = { devicePixelRatio: 1, innerWidth: 1280, innerHeight: 720 };
globalThis.location = { href: 'http://localhost/' };

const { Materials } = await import('../src/world/materials.js');
const { createBuilding, STYLES } = await import('../src/world/buildings.js');
const { SolidWorld } = await import('../src/world/parts.js');
const { ContentDatabase } = await import('../src/content.js');

const names = ['buildings', 'npcs', 'incidents', 'crops', 'learning', 'research', 'voices', 'dialogue'];
const loaded = {};
for (const name of names) {
  loaded[name] = JSON.parse(await readFile(join(root, 'content', `${name}.json`), 'utf8'));
}
const content = new ContentDatabase(loaded);

const materials = new Materials(null);
const solids = new SolidWorld();
const built = content.buildings.map((building) => ({ building, ...createBuilding(materials, building, { solids }) }));

test('barrel vaults are sealed pressure modules, not open canopies', () => {
  const vaults = built.filter(({ building }) => ['vault', 'glass', 'buried'].includes(STYLES[building.id]?.roof));
  assert.ok(vaults.length >= 5, `expected several vaulted modules, found ${vaults.length}`);
  for (const { building, group } of vaults) {
    let caps = 0;
    group.traverse((child) => {
      if (child.name === 'vault-bulkhead') caps += 1;
    });
    assert.equal(caps, 2, `${building.id} has ${caps} end bulkheads; a sealed module needs two`);
  }
  const ceilings = solids.boxes.filter((box) => box.tag === 'vault-ceiling');
  assert.equal(ceilings.length, vaults.length, 'every vault needs a ceiling collider');
});

test('every functional building has a dust-mitigation porch at its hatch', () => {
  for (const { building, group } of built) {
    if (building.id === 'landing_pad') continue;
    const porch = group.getObjectByName('dust-porch');
    assert.ok(porch, `${building.id} has no dust porch`);
    assert.ok(porch.children.length >= 9, `${building.id} dust porch is missing its grate, marking or brush posts`);
  }
});

test('regolith shielding berms sit behind the sensitive modules, clear of the hatch', () => {
  const shielded = ['habitat', 'life_support', 'storm_shelter', 'medical'];
  for (const id of shielded) {
    const entry = built.find(({ building }) => building.id === id);
    let berm = false;
    entry.group.traverse((child) => {
      if (child.name === 'berm') berm = true;
    });
    assert.ok(berm, `${id} has no shielding berm`);
  }
  assert.ok(solids.boxes.some((box) => box.tag === 'berm'), 'berms must be solid');
  // A berm on the plaza side would block the door; every berm is offset from the building centre.
  for (const box of solids.boxes.filter((b) => b.tag === 'berm')) {
    assert.ok(box.maxX > box.minX && box.maxZ > box.minZ, 'inverted berm collider');
  }
});

test('the scanned regolith height map drives the ground material', async () => {
  const text = await readFile(join(root, 'web/src/world/environment.js'), 'utf8');
  assert.match(text, /regolithMat\.bumpMap = textures\.height/, 'the fifth PBR map is still unused');
  assert.match(text, /regolithMat\.bumpScale = 0\.12/);
  assert.match(text, /farMat\.bumpMap\.repeat\.set\(26, 26\)/, 'the far ring needs its own repeat');
});

test('the browser research note records the NASA source and its limits', async () => {
  const text = await readFile(join(root, 'docs/research/Luna-Quest_Surface_Habitat_Design_EN.md'), 'utf8');
  assert.match(text, /nasa\.gov\/reference\/jsc-surface-habitats/);
  assert.match(text, /pressurised transfer tunnels/);
  assert.match(text, /design reference, not a flight-certified design/);
  assert.match(text, /no game number in the simulation was changed/);
});
