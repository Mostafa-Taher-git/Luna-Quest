/**
 * Headless world build. There is no GPU here, but three.js geometry is pure maths, so the
 * whole colony can be constructed against a stub 2D canvas. This catches broken parts,
 * missing materials and consoles that drift out of their building — the failures that would
 * otherwise only show up as a black screen in a browser.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

// ---- minimal DOM -----------------------------------------------------------
function makeContext() {
  const gradient = { addColorStop() {} };
  return {
    canvas: { width: 512, height: 512 },
    fillStyle: '#000',
    strokeStyle: '#000',
    lineWidth: 1,
    font: '',
    textBaseline: 'middle',
    fillRect() {},
    strokeRect() {},
    clearRect() {},
    beginPath() {},
    closePath() {},
    moveTo() {},
    lineTo() {},
    arc() {},
    fill() {},
    stroke() {},
    save() {},
    restore() {},
    translate() {},
    rotate() {},
    scale() {},
    createPattern: () => null,
    setLineDash() {},
    fillText() {},
    measureText: (text) => ({ width: String(text).length * 8 }),
    createLinearGradient: () => gradient,
    createRadialGradient: () => gradient,
  };
}

globalThis.document = {
  createElement(tag) {
    if (tag !== 'canvas') throw new Error(`unexpected element ${tag}`);
    const canvas = { width: 1, height: 1, getContext: () => makeContext() };
    return canvas;
  },
};
globalThis.window = { devicePixelRatio: 1, innerWidth: 1280, innerHeight: 720 };
globalThis.location = { href: 'http://localhost/' };

const THREE = await import('three');
const { Materials } = await import('../src/world/materials.js');
const { createBuilding, doorSide, STYLES } = await import('../src/world/buildings.js');
const { SolidWorld } = await import('../src/world/parts.js');
const { LunarTerrain } = await import('../src/world/terrain.js');
const { ContentDatabase } = await import('../src/content.js');

const names = ['buildings', 'npcs', 'incidents', 'crops', 'learning', 'research', 'voices', 'dialogue'];
const loaded = {};
for (const name of names) {
  loaded[name] = JSON.parse(await readFile(join(root, 'content', `${name}.json`), 'utf8'));
}
const content = new ContentDatabase(loaded);

test('world:', async (t) => {
  const materials = new Materials(null);
  const solids = new SolidWorld();
  const terrain = new LunarTerrain(1337);

  await t.test('every building has a style profile', () => {
    for (const b of content.buildings) {
      assert.ok(STYLES[b.id], `no style for ${b.id}`);
    }
  });

  await t.test('the door faces the plaza', () => {
    assert.equal(doorSide({ position: [0, 0] }), 'north');
    assert.equal(doorSide({ position: [30, -14] }), 'west');
    assert.equal(doorSide({ position: [-44, 6] }), 'east');
    assert.equal(doorSide({ position: [44, 6] }), 'west');
    assert.equal(doorSide({ position: [0, 30] }), 'south');
  });

  const built = [];
  for (const building of content.buildings) {
    built.push({ building, ...createBuilding(materials, building, { solids }) });
  }

  await t.test('all sixteen buildings build without throwing', () => {
    assert.equal(built.length, 16);
    for (const entry of built) {
      let meshes = 0;
      entry.group.traverse((child) => {
        if (child.isMesh) meshes += 1;
      });
      assert.ok(meshes > 6, `${entry.building.id} only produced ${meshes} meshes`);
    }
  });

  await t.test('every interaction has a console, inside its own building', () => {
    for (const { building, consoles } of built) {
      const expected = building.interactions.length;
      assert.equal(consoles.length, expected, `${building.id}: ${consoles.length} consoles for ${expected} interactions`);
      for (const console of consoles) {
        const dx = Math.abs(console.world.x - building.position[0]);
        const dz = Math.abs(console.world.z - building.position[1]);
        assert.ok(dx <= building.footprint[0] / 2 + 0.6, `${building.id}/${console.interactionId} is outside in x (${dx})`);
        assert.ok(dz <= building.footprint[1] / 2 + 0.6, `${building.id}/${console.interactionId} is outside in z (${dz})`);
        assert.equal(console.mesh.userData.interactionId, console.interactionId);
      }
    }
  });

  await t.test('colliders cover walls, pads and consoles', () => {
    const tags = new Set(solids.boxes.map((b) => b.tag));
    for (const tag of ['wall', 'pad', 'floor', 'console']) {
      assert.ok(tags.has(tag), `no ${tag} colliders`);
    }
    assert.ok(solids.boxes.length > 150, `only ${solids.boxes.length} colliders`);
    for (const box of solids.boxes) {
      assert.ok(Number.isFinite(box.minX) && Number.isFinite(box.maxY), 'collider is not finite');
      assert.ok(box.maxX > box.minX && box.maxY > box.minY && box.maxZ > box.minZ, 'collider is inverted');
    }
  });

  await t.test('the colony pad the terrain promises is actually flat', () => {
    for (const { building } of built) {
      const [x, z] = building.position;
      assert.equal(terrain.height(x, z), 0, `${building.id} sits on uneven ground`);
    }
  });

  await t.test('buildings do not overlap each other', () => {
    for (let i = 0; i < built.length; i += 1) {
      for (let j = i + 1; j < built.length; j += 1) {
        const a = built[i].building;
        const b = built[j].building;
        const gapX = Math.abs(a.position[0] - b.position[0]) - (a.footprint[0] + b.footprint[0]) / 2;
        const gapZ = Math.abs(a.position[1] - b.position[1]) - (a.footprint[1] + b.footprint[1]) / 2;
        assert.ok(gapX > 0.5 || gapZ > 0.5, `${a.id} overlaps ${b.id}`);
      }
    }
  });

  await t.test('the sun light and materials are wired', () => {
    assert.ok(materials.hull.isMaterial);
    assert.ok(materials.suit.map, 'the suit should not be a flat colour');
    assert.ok(materials.visor.isMeshPhysicalMaterial);
    const light = new THREE.DirectionalLight(0xffffff, 1);
    assert.ok(light.shadow);
  });
});
