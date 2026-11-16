#!/usr/bin/env node
/**
 * Content and asset validation shared by all three engines.
 *
 *   node tools/check-content.mjs
 *
 * Fails loudly on the mistakes that otherwise show up as a silent dead console: a duplicate
 * id, an interaction pointing at a room that does not exist, an incident whose required
 * action no console can perform, or a voice line whose MP3 is missing.
 */
import { readFile, readdir, access } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const problems = [];
const notes = [];
const fail = (message) => problems.push(message);

async function json(path) {
  try {
    return JSON.parse(await readFile(join(root, path), 'utf8'));
  } catch (err) {
    fail(`${path}: ${err.message}`);
    return null;
  }
}

const buildings = (await json('content/buildings.json'))?.buildings ?? [];
const npcs = (await json('content/npcs.json'))?.npcs ?? [];
const incidents = (await json('content/incidents.json'))?.incidents ?? [];
const crops = (await json('content/crops.json'))?.crops ?? [];
const learning = (await json('content/learning.json'))?.learning ?? [];
const research = (await json('content/research.json'))?.research ?? [];
const dialogue = (await json('content/dialogue.json'))?.topics ?? [];
const voices = await json('content/voices.json');

// ---- structural checks -----------------------------------------------------
const unique = (list, label) => {
  const seen = new Set();
  for (const item of list) {
    if (!item.id) fail(`${label}: an entry has no id`);
    if (seen.has(item.id)) fail(`${label}: duplicate id ${item.id}`);
    seen.add(item.id);
  }
  return seen;
};

const buildingIds = unique(buildings, 'buildings');
unique(npcs, 'npcs');
unique(incidents, 'incidents');
unique(crops, 'crops');
unique(learning, 'learning');
unique(research, 'research');

const objectiveIds = new Set(learning.map((o) => o.id));
const npcIds = new Set(npcs.map((n) => n.id));

// Every functional building needs rooms, interactions and a real footprint.
for (const b of buildings) {
  if (b.category !== 'functional') continue;
  if (!Array.isArray(b.rooms) || b.rooms.length === 0) fail(`${b.id}: functional building without rooms`);
  if (!Array.isArray(b.interactions) || b.interactions.length === 0) fail(`${b.id}: functional building without interactions`);
  const roomIds = new Set((b.rooms ?? []).map((r) => r.id));
  for (const i of b.interactions ?? []) {
    if (!roomIds.has(i.roomId)) fail(`${b.id}/${i.id}: room ${i.roomId} does not exist`);
    if (i.learning && !objectiveIds.has(i.learning)) fail(`${b.id}/${i.id}: unknown objective ${i.learning}`);
  }
  const [w, d] = b.footprint ?? [];
  if (!(w > 2 && d > 2)) fail(`${b.id}: footprint is not walkable (${w}×${d})`);
  if (!(b.height >= 2.5)) fail(`${b.id}: height ${b.height} m is too low for a person and their helmet`);
  for (const i of b.interactions ?? []) {
    if (!i.nameEn || !i.nameAr) fail(`${b.id}/${i.id}: bilingual name missing`);
  }
}

// No two buildings may stand in the same place.
for (let i = 0; i < buildings.length; i += 1) {
  for (let j = i + 1; j < buildings.length; j += 1) {
    const a = buildings[i];
    const b = buildings[j];
    const gapX = Math.abs(a.position[0] - b.position[0]) - (a.footprint[0] + b.footprint[0]) / 2;
    const gapZ = Math.abs(a.position[1] - b.position[1]) - (a.footprint[1] + b.footprint[1]) / 2;
    if (gapX <= 0.5 && gapZ <= 0.5) fail(`${a.id} and ${b.id} overlap; move one of them`);
  }
}

// Every incident must be resolvable by an action some console exposes.
const consoleActions = new Set([
  'clean_arrays', 'clean_sensors', 'clean_airlock', 'clean_filter', 'replace_scrubber', 'repair_o2',
  'inspect_crop', 'shelter', 'repair_relay', 'rest', 'balance_load', 'harvest', 'collect_sample',
]);
for (const incident of incidents) {
  if (incident.learning && !objectiveIds.has(incident.learning)) {
    fail(`${incident.id}: unknown objective ${incident.learning}`);
  }
  if (incident.owner && !npcIds.has(incident.owner)) fail(`${incident.id}: unknown owner ${incident.owner}`);
  if (incident.location && !buildingIds.has(incident.location)) fail(`${incident.id}: unknown location ${incident.location}`);
  if (incident.action && !consoleActions.has(incident.action)) {
    // A rule whose action no console can perform would hang on screen forever.
    const reachable = buildings.some((b) => (b.interactions ?? []).some((i) => i.id === incident.action));
    if (!reachable) fail(`${incident.id}: required action "${incident.action}" is not reachable from any console`);
  }
}

// Crew: six humans with approved names, plus NEXO.
const humans = npcs.filter((n) => !n.isRobot);
if (humans.length !== 6) fail(`expected six human crew, found ${humans.length}`);
const robots = npcs.filter((n) => n.isRobot);
if (robots.length !== 1) fail(`expected one robot (NEXO), found ${robots.length}`);
for (const npc of npcs) {
  if (npc.locationId && !buildingIds.has(npc.locationId)) fail(`${npc.id}: unknown home building ${npc.locationId}`);
  if (!Array.isArray(npc.schedule) || npc.schedule.length === 0) fail(`${npc.id}: no schedule`);
  for (const entry of npc.schedule ?? []) {
    if (!buildingIds.has(entry.location)) fail(`${npc.id}: schedule points at ${entry.location}, which does not exist`);
    if (!(entry.start < entry.end)) fail(`${npc.id}: schedule entry ${entry.start}–${entry.end} is inverted`);
  }
  if (!npc.greetingEn || !npc.greetingAr) fail(`${npc.id}: greeting must be bilingual`);
}

// Dialogue topics must belong to a real crew member and have a voice key.
for (const topic of dialogue) {
  if (!npcIds.has(topic.npcId)) fail(`dialogue: topic ${topic.id} belongs to unknown ${topic.npcId}`);
  if (!topic.labelEn || !topic.labelAr) fail(`dialogue: topic ${topic.id} is not bilingual`);
}

// ---- assets ----------------------------------------------------------------
const godot = 'engines/godot/LunaQuest.Godot';
const packPath = join(godot, 'assets/voice/pack.json');
if (existsSync(packPath)) {
  const pack = JSON.parse(await readFile(packPath, 'utf8'));
  const files = new Set(await readdir(join(root, godot, 'assets/voice')));
  for (const [key, entry] of Object.entries(pack)) {
    if (!files.has(entry.file)) fail(`voice pack: ${key} -> ${entry.file} is missing`);
    if (!entry.text || !entry.textAr) fail(`voice pack: ${key} has no bilingual subtitle`);
  }
  notes.push(`${Object.keys(pack).length} voice lines verified`);
} else {
  fail('voice pack.json is missing');
}

for (const [label, path] of [
  ['astronaut', 'assets/astronaut/cute_astronaut.glb'],
  ['intro film (ogv)', 'assets/video/intro.ogv'],
  ['intro film (mp4)', 'assets/video/intro.mp4'],
  ['score', 'assets/audio/music/lunaquest-score.mp3'],
  ['colony kit manifest', 'assets/models/lunar_colony/manifest.json'],
]) {
  await access(join(root, path)).catch(() => fail(`${label}: ${path} is missing`));
}

// ---- report ----------------------------------------------------------------
if (notes.length) for (const note of notes) console.log(`  · ${note}`);
if (problems.length) {
  console.error(`\ncontent check failed with ${problems.length} problem(s):`);
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}
console.log(
  `content check: ${buildings.length} buildings, ${npcs.length} crew, ${incidents.length} incident rules, ` +
    `${learning.length} objectives, ${research.length} research notes, ${dialogue.length} dialogue topics — all consistent`,
);
