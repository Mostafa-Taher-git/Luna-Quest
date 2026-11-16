/**
 * Cross-engine astronaut consistency.
 *
 * The same Cute Astronaut, at the same 1.75 m, in all four runtimes. These are source-level
 * checks so they run in CI without Godot, Unity or a GPU. The policy is in
 * docs/design/Luna-Quest_Immutable_Assets_EN.md.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = (path) => readFile(join(root, path), 'utf8');

const GDS = 'engines/godot-gdscript/LunaQuest.GDScript/scripts/';
const NET = 'engines/godot/LunaQuest.Godot/scripts/';
const UNITY = 'engines/unity/LunaQuest.Unity/Assets/Scripts/';

const ENGINE_FILES = [
  `${GDS}crew.gd`,
  `${GDS}main.gd`,
  `${GDS}player.gd`,
  `${GDS}audit.gd`,
  `${GDS}selftest.gd`,
  `${NET}WorldBuilder.cs`,
  `${NET}Main.cs`,
  `${NET}NpcAgent.cs`,
  `${NET}PlayerController.cs`,
  'engines/godot/LunaQuest.Godot/tools/model_check.gd',
  `${UNITY}ColonyWorld.cs`,
  `${UNITY}ColonyPlayer.cs`,
];

test('no engine still builds a substitute human suit', async () => {
  const forbidden = ['fallbackSuit', '_fallback_suit', 'FallbackSuit', 'BuildFallbackSuit'];
  for (const file of ENGINE_FILES) {
    const text = await read(file);
    for (const name of forbidden) {
      assert.ok(!text.includes(name), `${file} still contains ${name}`);
    }
  }
});

test('a missing model is reported, never silently replaced', async () => {
  const guards = [
    `${GDS}crew.gd`,
    `${GDS}main.gd`,
    `${NET}WorldBuilder.cs`,
    `${UNITY}ColonyWorld.cs`,
  ];
  for (const file of guards) {
    const text = await read(file);
    assert.match(text, /No substitute astronaut will be drawn/, `${file} has no explicit failure message`);
    assert.match(text, /push_error|GD\.PushError|Debug\.LogError/, `${file} does not log the missing model`);
  }
});

test('every engine creates astronauts at the shared 1.75 m', async () => {
  const sites = [
    [`${GDS}crew.gd`, /const CREW_HEIGHT_M := 1\.75/],
    [`${GDS}crew.gd`, /_fit_height\(body, CREW_HEIGHT_M\)/],
    [`${GDS}main.gd`, /crew_fit\(holder, ColonyCrew\.CREW_HEIGHT_M\)/],
    [`${GDS}player.gd`, /capsule\.height = 1\.75/],
    [`${GDS}audit.gd`, /shape\.height = 1\.75/],
    [`${NET}WorldBuilder.cs`, /CreateAstronaut\(float targetHeight = 1\.75f\)/],
    [`${NET}Main.cs`, /CreateAstronaut\(1\.75f\)/],
    [`${NET}NpcAgent.cs`, /CreateAstronaut\(1\.75f\)/],
    [`${NET}PlayerController.cs`, /Height = 1\.75f/],
    ['engines/godot/LunaQuest.Godot/tools/model_check.gd', /const HEIGHT := 1\.75/],
    [`${UNITY}ColonyPlayer.cs`, /CreateAstronaut\(1\.75f/],
    [`${UNITY}ColonyPlayer.cs`, /height = 1\.75f/],
    [`${UNITY}ColonyWorld.cs`, /CreateAstronaut\(1\.75f/],
  ];
  for (const [file, pattern] of sites) {
    assert.match(await read(file), pattern, `${file} drifted from the shared crew height`);
  }

  // Catch any future hard-coded height that is not 1.75.
  for (const file of ENGINE_FILES) {
    const text = await read(file);
    const stale = text.match(/CreateAstronaut\([^)]*1\.(?:72|76|78)/g) ?? [];
    assert.deepEqual(stale, [], `${file} still passes an old astronaut height`);
  }
});

test('all four runtimes load the same model, with one posed-height baseline', async () => {
  const sites = [
    ['web/src/world/crew-metrics.js', /models\/astronaut\/cute_astronaut\.glb/],
    [`${GDS}crew.gd`, /res:\/\/assets\/astronaut\/cute_astronaut\.glb/],
    [`${NET}WorldBuilder.cs`, /res:\/\/assets\/astronaut\/cute_astronaut\.glb/],
    [`${UNITY}ColonyWorld.cs`, /Art\/Astronaut\/cute_astronaut_baked/],
  ];
  for (const [file, pattern] of sites) {
    assert.match(await read(file), pattern, `${file} does not load the Cute Astronaut`);
  }

  const net = await read(`${NET}WorldBuilder.cs`);
  const unity = await read(`${UNITY}ColonyWorld.cs`);
  assert.match(net, /const float posedHeight = 2\.311f/);
  assert.match(unity, /targetHeight \/ 2\.311f/, 'the Unity bake must use the same scale baseline');
});

test('the GDScript selftest gates on the two frozen assets', async () => {
  const text = await read(`${GDS}selftest.gd`);
  assert.match(text, /ResourceLoader\.exists\(ColonyCrew\.ASTRONAUT_SCENE\)/);
  assert.match(text, /ResourceLoader\.exists\("res:\/\/assets\/video\/intro\.ogv"\)/);
  assert.match(text, /ColonyCrew\.CREW_HEIGHT_M == 1\.75/);
});
