#!/usr/bin/env node
/**
 * Copies the single source of truth (content/, assets/, engine art) into web/public.
 *
 * The repository keeps one copy of every asset; the web build gets a generated mirror so
 * the browser can fetch it with plain relative URLs. Run automatically by `npm run dev`
 * and `npm run build`, and by the release workflow.
 *
 *   node scripts/sync-assets.mjs [--quiet]
 */
import { cp, mkdir, readdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const web = resolve(here, '..');
const root = resolve(web, '..');
const out = join(web, 'public');
const quiet = process.argv.includes('--quiet');

// The mirror is generated: wipe it so a removed asset cannot linger in a release.
await rm(out, { recursive: true, force: true });

const REQUIRED = [];
const OPTIONAL = [];
let copied = 0;
let bytes = 0;

async function sizeOf(path) {
  try {
    const s = await stat(path);
    return s.isDirectory() ? 0 : s.size;
  } catch {
    return 0;
  }
}

async function copy(from, to, { required = false } = {}) {
  const src = join(root, from);
  const dst = join(out, to);
  if (!existsSync(src)) {
    (required ? REQUIRED : OPTIONAL).push(from);
    return false;
  }
  await mkdir(dirname(dst), { recursive: true });
  await cp(src, dst, { recursive: true, force: true });
  if ((await stat(src)).isDirectory()) {
    for (const entry of await readdir(src, { withFileTypes: true })) {
      if (entry.isFile()) {
        bytes += await sizeOf(join(src, entry.name));
        copied += 1;
      }
    }
  } else {
    bytes += await sizeOf(src);
    copied += 1;
  }
  return true;
}

// ---- files owned by the web app itself (icon, poster) ----------------------
const staticDir = join(web, 'static');
if (existsSync(staticDir)) {
  await mkdir(out, { recursive: true });
  for (const entry of await readdir(staticDir, { withFileTypes: true })) {
    await copy(join('web/static', entry.name), entry.name, { required: true });
  }
}

// ---- content: the seven JSON files both engines read ------------------------
const contentDir = join(root, 'content');
if (!existsSync(contentDir)) {
  console.error('error: content/ not found — run this from inside the repository');
  process.exit(1);
}
await mkdir(join(out, 'content'), { recursive: true });
for (const file of await readdir(contentDir)) {
  if (file.endsWith('.json')) await copy(join('content', file), join('content', file), { required: true });
}

// ---- hero assets -----------------------------------------------------------
await copy('assets/astronaut/cute_astronaut.glb', 'models/astronaut/cute_astronaut.glb', { required: true });
await copy('assets/video/intro.mp4', 'media/intro.mp4', { required: true });
await copy('assets/video/intro.webm', 'media/intro.webm');
await copy('assets/audio/music/lunaquest-score.mp3', 'audio/lunaquest-score.mp3', { required: true });
await copy('assets/noto-sans-latin.woff', 'fonts/noto-sans-latin.woff');
await copy('assets/noto-sans-arabic.woff', 'fonts/noto-sans-arabic.woff');

// ---- colony kit (CC0 Moon Base Colony and Lunar Rovers) --------------------
// Only the modules the layout actually places are shipped, so the download stays honest.
const propsSource = join(web, 'src/world/props.js');
const usedStems = [...(await readFile(propsSource, 'utf8')).matchAll(/stem: '([^']+)'/g)].map((m) => m[1]);
await mkdir(join(out, 'models/kit'), { recursive: true });
for (const stem of new Set(usedStems)) {
  await copy(`assets/models/lunar_colony/${stem}.glb`, `models/kit/${stem}.glb`, { required: true });
}
await copy('assets/models/lunar_colony/manifest.json', 'models/kit/manifest.json');

// ---- real scanned art from the Godot project ------------------------------
const godotArt = 'engines/godot/LunaQuest.Godot/assets/real';
await copy(`${godotArt}/regolith`, 'textures/regolith', { required: true });
await copy(`${godotArt}/earth/blue_marble_2048.jpg`, 'textures/earth_day.jpg', { required: true });
await copy(`${godotArt}/rocks`, 'models/rocks', { required: true });

// ---- voice pack ------------------------------------------------------------
const voiceDir = join(root, 'engines/godot/LunaQuest.Godot/assets/voice');
if (existsSync(voiceDir)) {
  await mkdir(join(out, 'audio/voice'), { recursive: true });
  for (const file of await readdir(voiceDir)) {
    if (file.endsWith('.mp3')) await copy(join('engines/godot/LunaQuest.Godot/assets/voice', file), join('audio/voice', file));
  }
}

// ---- a manifest so the app can degrade gracefully if art is missing --------
const manifest = {
  generatedBy: 'web/scripts/sync-assets.mjs',
  video: existsSync(join(out, 'media/intro.mp4')) ? 'media/intro.mp4' : null,
  videoWebm: existsSync(join(out, 'media/intro.webm')) ? 'media/intro.webm' : null,
  astronaut: existsSync(join(out, 'models/astronaut/cute_astronaut.glb')) ? 'models/astronaut/cute_astronaut.glb' : null,
  kit: existsSync(join(out, 'models/kit')) ? 'models/kit' : null,
  regolith: existsSync(join(out, 'textures/regolith')) ? 'textures/regolith' : null,
  rocks: existsSync(join(out, 'models/rocks')) ? 'models/rocks' : null,
  voice: existsSync(join(out, 'audio/voice')) ? 'audio/voice' : null,
};
await writeFile(join(out, 'asset-manifest.json'), JSON.stringify(manifest, null, 2));

if (!quiet) {
  console.log(`web assets: ${copied} files, ${(bytes / 1024 / 1024).toFixed(1)} MB -> web/public`);
  if (OPTIONAL.length) console.log(`optional, not found: ${OPTIONAL.join(', ')}`);
}
if (REQUIRED.length) {
  console.error(`error: required assets missing:\n  ${REQUIRED.join('\n  ')}`);
  process.exit(1);
}
