#!/usr/bin/env node
/**
 * Capture the README frames from the browser edition.
 *
 * The sandbox this project is developed in has no GPU and no browser, so the honest way to
 * photograph the game is to do it in CI, on a runner that has a real Chrome. This script is
 * self-contained: it serves `web/dist` on a local port, drives the game through the
 * `window.lunaQuest` API, and writes one PNG per shot into `--out` (default `shots/`).
 *
 *   node tools/capture-readme.mjs [--out shots] [--port 4173] [--only hero]
 *
 * It does not press keys or move the mouse. Every frame is a deliberate pose: the render loop is
 * stopped, the commander is placed, the production camera rig is run once, and the frame is read
 * back. That makes the pictures reproducible instead of luck.
 */
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { createReadStream, promises as fs } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

// ---- arguments --------------------------------------------------------------

const argv = process.argv.slice(2);
const arg = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};
const OUT_DIR = resolve(arg('out', 'shots'));
const PORT = Number(arg('port', '4173'));
const ONLY = arg('only', '');
const ROOT = resolve('web/dist');
const WIDTH = 1600;
const HEIGHT = 900;

// ---- a very small static server --------------------------------------------

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.mp4': 'video/mp4',
  '.webm': 'video/webm',
  '.glb': 'model/gltf-binary',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.wasm': 'application/wasm',
};

function serve(root, port) {
  const server = createServer(async (req, res) => {
    const url = decodeURIComponent((req.url ?? '/').split('?')[0]);
    let file = normalize(join(root, url === '/' ? 'index.html' : url));
    if (!file.startsWith(root)) {
      res.writeHead(403).end('forbidden');
      return;
    }
    try {
      const stat = await fs.stat(file);
      if (stat.isDirectory()) file = join(file, 'index.html');
      const body = createReadStream(file);
      body.on('error', () => res.writeHead(404).end('not found'));
      res.writeHead(200, {
        'content-type': MIME[extname(file).toLowerCase()] ?? 'application/octet-stream',
        'cache-control': 'no-store',
      });
      body.pipe(res);
    } catch {
      res.writeHead(404).end('not found');
    }
  });
  return new Promise((ok) => server.listen(port, '127.0.0.1', () => ok(server)));
}

// ---- the pose rig, injected into the page ----------------------------------

/* eslint-disable no-undef */
function installRig() {
  const LQ = window.lunaQuest;

  const vec = () => new LQ.player.position.constructor();
  const quat = () => new LQ.camera.quaternion.constructor();

  const api = {
    /** Freeze the game: no render loop, no simulation ticking under the camera. */
    stop() {
      LQ.renderer.setAnimationLoop(null);
      LQ.paused = true;
      LQ.overlayOpen = false;
      return true;
    },

    render() {
      LQ.camera.updateMatrixWorld(true);
      LQ.scene.updateMatrixWorld(true);
      LQ.renderer.render(LQ.scene, LQ.camera);
      return true;
    },

    /** Stop the suit lagging behind the camera: the third-person rig lerps the body. */
    settle(mode) {
      const p = LQ.player;
      if (mode === 'first' && p.mode !== 'first') {
        p.mode = 'first';
        if (p.astronaut?.root) p.astronaut.root.visible = false;
      }
      if (mode === 'third' && p.mode !== 'third') {
        p.mode = 'third';
        if (p.astronaut?.root) p.astronaut.root.visible = true;
      }
      p._targetYaw = p.yaw;
      if (p.astronaut?.root) {
        p.root.position.copy(p.position);
        p.root.rotation.y = p.yaw;
      }
      p.velocity.set(0, 0, 0);
      p.bob = 0;
      p.headBob = 0;
      p.update(0.016, LQ.input, { allowMove: false });
      api.render();
      return { x: +p.position.x.toFixed(2), y: +p.position.y.toFixed(2), z: +p.position.z.toFixed(2) };
    },

    /** Put the commander somewhere and aim the camera rig. */
    pose({ x, y, z, yaw, pitch = -0.05, mode = 'third' }) {
      const p = LQ.player;
      if (mode) api.settle(mode);
      p.position.set(x, y ?? p.position.y, z);
      p.yaw = yaw;
      p.pitch = pitch;
      return api.settle(mode);
    },

    /** Run the game's own focus pass so the HUD prompt names what is in front of you. */
    focus() {
      try {
        LQ.updateFocus();
      } catch (error) {
        return String(error);
      }
      return true;
    },

    /** Where a console actually is, and the side of it a person stands on. */
    consoleFront({ buildingId, interactionId, distance = 1.7 }) {
      const entry = LQ.consoles.find(
        (c) => c.buildingId === buildingId && c.interactionId === interactionId,
      );
      if (!entry) return null;
      const at = entry.mesh.getWorldPosition(vec());
      const q = entry.mesh.getWorldQuaternion(quat());
      const front = vec().set(0, 0, -1).applyQuaternion(q);
      front.y = 0;
      if (front.lengthSq() < 1e-6) front.set(0, 0, 1);
      front.normalize();
      // The screen sits 1.28 m above the station's own floor.
      const floorY = at.y - 1.28;
      const px = at.x + front.x * distance;
      const pz = at.z + front.z * distance;
      const yaw = Math.atan2(at.x - px, at.z - pz) + Math.PI;
      return {
        at: { x: at.x, y: at.y, z: at.z },
        front: { x: front.x, z: front.z },
        stand: { x: px, y: floorY, z: pz },
        yaw,
        entry,
      };
    },

    stand_at_console({ buildingId, interactionId, distance = 1.7, pitch = -0.16, mode = 'first' }) {
      const spot = api.consoleFront({ buildingId, interactionId, distance });
      if (!spot) return null;
      api.pose({ x: spot.stand.x, y: spot.stand.y, z: spot.stand.z, yaw: spot.yaw, pitch, mode });
      api.focus();
      api.render();
      return spot.stand;
    },

    open_console({ buildingId, interactionId }) {
      const entry = LQ.consoles.find(
        (c) => c.buildingId === buildingId && c.interactionId === interactionId,
      );
      if (!entry) return null;
      LQ.openConsole(entry);
      return { name: entry.nameEn, buildingId, interactionId };
    },

    /** A crew member, where they are standing, and a spot a few metres away facing them. */
    crewSpot({ role, distance = 3.2 }) {
      const member = LQ.crew.members.find((m) => m.profile.id === role);
      if (!member) return null;
      const at = member.root.position.clone();
      // Stand between the member and the middle of the colony, so the camera looks outward.
      const towards = vec().set(0, 0, 10).sub(at);
      towards.y = 0;
      if (towards.lengthSq() < 1) towards.set(0, 0, 1);
      towards.normalize();
      const px = at.x + towards.x * distance;
      const pz = at.z + towards.z * distance;
      const yaw = Math.atan2(at.x - px, at.z - pz) + Math.PI;
      return { at: { x: at.x, y: at.y, z: at.z }, stand: { x: px, y: at.y, z: pz }, yaw, id: member.profile.id, name: member.profile.nameEn };
    },

    stand_near_crew({ role, distance = 3.2, pitch = -0.02, mode = 'third' }) {
      const spot = api.crewSpot({ role, distance });
      if (!spot) return null;
      api.pose({ x: spot.stand.x, y: spot.stand.y, z: spot.stand.z, yaw: spot.yaw, pitch, mode });
      api.focus();
      api.render();
      return spot;
    },

    talk({ role }) {
      const member = LQ.crew.members.find((m) => m.profile.id === role);
      if (!member) return null;
      LQ.talkTo(member);
      return member.profile.nameEn;
    },

    journal() {
      LQ.overlayOpen = true;
      LQ.hud.journalPanel();
      return true;
    },

    arabic(on = true) {
      const already = window.__ar === true;
      if (on !== already) LQ.toggleLanguage();
      window.__ar = on;
      return document.documentElement.lang;
    },

    close() {
      try {
        LQ.hud.closePanel();
      } catch {
        /* no panel open */
      }
      LQ.overlayOpen = false;
      return true;
    },

    /** A free camera, for the aerial. Nothing in the game is moved. */
    free({ eye, look, fov = 50 }) {
      LQ.camera.position.set(eye[0], eye[1], eye[2]);
      LQ.camera.lookAt(look[0], look[1], look[2]);
      LQ.camera.fov = fov;
      LQ.camera.updateProjectionMatrix();
      api.render();
      return true;
    },

    where() {
      const p = LQ.player.position;
      return { x: +p.x.toFixed(2), y: +p.y.toFixed(2), z: +p.z.toFixed(2), yaw: +LQ.player.yaw.toFixed(2) };
    },
  };

  window.__rig = api;
  return Object.keys(api);
}
/* eslint-enable no-undef */

// ---- the shot list ---------------------------------------------------------

/**
 * Every entry is one frame. `run` is a list of `[rigMethod, args]` calls made in order, so a shot
 * is data, not code inside the page.
 */
const SHOTS = [
  // ---- hero: the colony, wide, HUD live -----------------------------------
  {
    name: 'hero-A-plaza-spawn',
    note: 'Spawn on the plaza, third person, full HUD',
    run: [['pose', { x: -6, y: 0, z: 20, yaw: 0.6, pitch: -0.04, mode: 'third' }]],
  },
  {
    name: 'hero-B-plaza-mirror',
    note: 'The other side of the plaza walk, looking down the base',
    run: [['pose', { x: 6, y: 0, z: 20, yaw: -0.6, pitch: -0.04, mode: 'third' }]],
  },
  {
    name: 'hero-C-colony-wide',
    note: 'Further back: the whole line of modules',
    run: [['pose', { x: -14, y: 0, z: 34, yaw: 0.42, pitch: -0.02, mode: 'third' }]],
  },
  {
    name: 'hero-D-aerial',
    note: 'Aerial of the base from the rim side',
    run: [['free', { eye: [-30, 26, 42], look: [0, 2, 6], fov: 48 }]],
  },
  {
    name: 'hero-E-life-support',
    note: 'Three-quarter on Life Support with the facade sign readable',
    run: [['stand_near_crew', { role: 'npc_nour', distance: 6.5, pitch: -0.02, mode: 'third' }]],
  },

  // ---- console: the Water Filter, over a lit interior ---------------------
  {
    name: 'console-A-water-filter',
    note: 'Water Filter panel, first person, inside Life Support',
    run: [
      ['stand_at_console', { buildingId: 'life_support', interactionId: 'clean_filter', distance: 1.6, pitch: -0.12, mode: 'first' }],
      ['open_console', { buildingId: 'life_support', interactionId: 'clean_filter' }],
    ],
  },
  {
    name: 'console-B-power',
    note: 'Load budget panel at the Power Farm',
    run: [
      ['stand_at_console', { buildingId: 'power_farm', interactionId: 'balance_load', distance: 1.6, pitch: -0.12, mode: 'first' }],
      ['open_console', { buildingId: 'power_farm', interactionId: 'balance_load' }],
    ],
  },
  {
    name: 'console-C-greenhouse',
    note: 'Harvest panel in the greenhouse',
    run: [
      ['stand_at_console', { buildingId: 'greenhouse', interactionId: 'harvest', distance: 1.6, pitch: -0.12, mode: 'first' }],
      ['open_console', { buildingId: 'greenhouse', interactionId: 'harvest' }],
    ],
  },

  // ---- crew: Noor outside Life Support -----------------------------------
  {
    name: 'crew-A-dialogue',
    note: 'Noor dialogue, six voiced topics',
    run: [['talk', { role: 'npc_nour' }]],
  },
  {
    name: 'crew-B-noor-third',
    note: 'Noor outside the Life Support hatch, third person',
    run: [['stand_near_crew', { role: 'npc_nour', distance: 3.4, pitch: -0.02, mode: 'third' }]],
  },
  {
    name: 'crew-C-noor-close',
    note: 'Closer on Noor, hatch and sign in frame',
    run: [['stand_near_crew', { role: 'npc_nour', distance: 2.2, pitch: 0.02, mode: 'third' }]],
  },
  {
    name: 'crew-D-arabic',
    note: 'Third person near Noor with the interface in Arabic',
    run: [
      ['arabic', { on: true }],
      ['stand_near_crew', { role: 'npc_nour', distance: 3.4, pitch: -0.02, mode: 'third' }],
    ],
  },
  {
    name: 'crew-E-abdelaziz-power',
    note: 'Abdulaziz outside the Power Farm',
    run: [['stand_near_crew', { role: 'npc_abdelaziz', distance: 3.4, pitch: -0.02, mode: 'third' }]],
  },
];

// ---- drive -----------------------------------------------------------------

const log = (...args) => console.info('[shots]', ...args);

async function main() {
  await fs.mkdir(OUT_DIR, { recursive: true });
  const shots = ONLY ? SHOTS.filter((s) => s.name.includes(ONLY)) : SHOTS;
  log(`${shots.length} shots, serving ${ROOT} on 127.0.0.1:${PORT}`);

  const server = await serve(ROOT, PORT);
  const browser = await chromium.launch({
    args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--disable-lcd-text'],
  });
  const context = await browser.newContext({
    viewport: { width: WIDTH, height: HEIGHT },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();

  const errors = [];
  page.on('pageerror', (error) => errors.push(`pageerror: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });

  const started = Date.now();
  await page.goto(`http://127.0.0.1:${PORT}/`, { waitUntil: 'load', timeout: 120_000 });
  // The build ends by showing the start card; that is the readiness signal.
  await page.waitForFunction(
    () => window.lunaQuest && !document.getElementById('start').classList.contains('hidden'),
    null,
    { timeout: 300_000 },
  );
  log(`ready in ${((Date.now() - started) / 1000).toFixed(1)}s`);

  // Skip the film if it is on screen, then take the shift.
  await page.evaluate(() => {
    const skip = document.getElementById('intro-skip');
    const intro = document.getElementById('intro');
    if (skip && intro && !intro.classList.contains('hidden')) skip.click();
  });
  await page.waitForFunction(() => !document.getElementById('start').classList.contains('hidden'), null, {
    timeout: 60_000,
  });
  await page.evaluate(() => {
    const begin = document.getElementById('begin');
    if (begin) begin.click();
  });
  await page.waitForFunction(() => window.lunaQuest.started === true, null, { timeout: 60_000 });
  await page.waitForTimeout(2500); // let the suit materialise and the sky settle

  await page.evaluate(`(${installRig.toString()})()`);
  await page.evaluate(() => window.__rig.stop());
  await page.waitForTimeout(400);

  const report = [];
  for (const shot of shots) {
    const facts = [];
    try {
      await page.evaluate(() => window.__rig.close());
      for (const [method, args] of shot.run) {
        const result = await page.evaluate(
          ([m, a]) => {
            const fn = window.__rig[m];
            if (!fn) throw new Error(`no rig method ${m}`);
            return fn(a ?? {});
          },
          [method, args ?? {}],
        );
        if (result !== null && result !== undefined) facts.push({ [method]: result });
      }
      await page.waitForTimeout(350);
      const file = join(OUT_DIR, `${shot.name}.png`);
      await page.screenshot({ path: file, type: 'png' });
      const { size } = await fs.stat(file);
      report.push({ name: shot.name, note: shot.note, file, kb: Math.round(size / 1024), facts });
      log(`${shot.name} — ${Math.round(size / 1024)} kB`);
    } catch (error) {
      report.push({ name: shot.name, note: shot.note, error: String(error) });
      log(`${shot.name} FAILED: ${error}`);
    }
  }

  await browser.close();
  server.close();

  await fs.writeFile(
    join(OUT_DIR, 'report.json'),
    JSON.stringify({ when: new Date().toISOString(), viewport: [WIDTH, HEIGHT], errors, report }, null, 2),
  );
  log(`errors: ${errors.length}`);
  for (const error of errors.slice(0, 8)) log(`  ${error}`);
}

main().catch((error) => {
  console.error('[shots] fatal:', error);
  process.exit(1);
});
