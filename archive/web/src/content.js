/**
 * Content loader. The seven JSON files in content/ are the single source of truth for the
 * Godot build, the Unity build and this one. Nothing is generated here: the same ids, the
 * same footprints, the same learning objectives.
 */

const FILES = [
  'buildings',
  'npcs',
  'incidents',
  'crops',
  'learning',
  'research',
  'voices',
  'dialogue',
];

export async function loadContent(baseUrl = './', onProgress = () => {}) {
  const root = new URL(`${baseUrl}content/`, location.href);
  let done = 0;
  const load = async (name) => {
    const url = new URL(`${name}.json`, root);
    const res = await fetch(url, { cache: 'no-cache' });
    if (!res.ok) throw new Error(`content/${name}.json — HTTP ${res.status}`);
    const json = await res.json();
    done += 1;
    onProgress(done / FILES.length, name);
    return json;
  };

  const loaded = {};
  for (const name of FILES) {
    try {
      loaded[name] = await load(name);
    } catch (err) {
      // dialogue.json is optional in older exports; everything else is required.
      if (name === 'dialogue') loaded[name] = { topics: [] };
      else throw err;
    }
  }

  return new ContentDatabase(loaded, baseUrl);
}

export class ContentDatabase {
  constructor(loaded, baseUrl = './') {
    this.baseUrl = baseUrl;
    this.buildings = (loaded.buildings?.buildings ?? []).map(normaliseBuilding);
    this.npcs = (loaded.npcs?.npcs ?? []).map(normaliseNpc);
    this.incidents = loaded.incidents?.incidents ?? [];
    this.crops = loaded.crops?.crops ?? [];
    this.learning = loaded.learning?.learning ?? [];
    this.research = loaded.research?.research ?? [];
    this.voiceCast = loaded.voices?.cast ?? {};
    this.voiceLines = loaded.voices?.lines ?? [];
    this.dialogueTopics = loaded.dialogue?.topics ?? [];

    this._buildingById = new Map(this.buildings.map((b) => [b.id, b]));
    this._npcById = new Map(this.npcs.map((n) => [n.id, n]));
    this._objectiveById = new Map(this.learning.map((o) => [o.id, o]));
    this._incidentById = new Map(this.incidents.map((i) => [i.id, i]));
  }

  building(id) {
    return this._buildingById.get(id) ?? null;
  }

  npc(id) {
    return this._npcById.get(id) ?? null;
  }

  objective(id) {
    return this._objectiveById.get(id) ?? null;
  }

  incident(id) {
    return this._incidentById.get(id) ?? null;
  }

  /** Every interaction in the world, flattened, with its building attached. */
  get interactions() {
    if (this._interactions) return this._interactions;
    const list = [];
    for (const b of this.buildings) {
      for (const i of b.interactions) list.push({ ...i, buildingId: b.id, building: b });
    }
    this._interactions = list;
    return list;
  }

  interactionsOf(buildingId) {
    return this.interactions.filter((i) => i.buildingId === buildingId);
  }

  topicFor(npcId, id) {
    return this.dialogueTopics.find((t) => t.npcId === npcId && t.id === id) ?? null;
  }

  topicsFor(npcId) {
    return this.dialogueTopics.filter((t) => t.npcId === npcId);
  }
}

function normaliseBuilding(raw) {
  const footprint = normalisePair(raw.footprint, [10, 8]);
  const position = normalisePair(raw.position, [0, 0]);
  return {
    ...raw,
    id: String(raw.id ?? ''),
    nameEn: raw.nameEn ?? raw.id ?? '',
    nameAr: raw.nameAr ?? raw.nameEn ?? raw.id ?? '',
    category: raw.category ?? 'functional',
    position,
    footprint,
    height: Number(raw.height ?? 4),
    color: raw.color ?? '#cfd6dd',
    rooms: (raw.rooms ?? []).map((r) => ({
      ...r,
      nameEn: r.nameEn ?? r.id,
      nameAr: r.nameAr ?? r.nameEn ?? r.id,
      size: normalisePair(r.size, [footprint[0] * 0.6, footprint[1] * 0.6]),
      offset: normalisePair(r.offset, [0, 0]),
    })),
    interactions: (raw.interactions ?? []).map((i) => ({
      ...i,
      nameEn: i.nameEn ?? i.id,
      nameAr: i.nameAr ?? i.nameEn ?? i.id,
      kind: i.kind ?? 'read',
      learning: i.learning ?? null,
    })),
  };
}

function normaliseNpc(raw) {
  return {
    ...raw,
    id: String(raw.id ?? ''),
    nameEn: raw.nameEn ?? raw.id ?? '',
    nameAr: raw.nameAr ?? raw.nameEn ?? raw.id ?? '',
    isRobot: Boolean(raw.isRobot),
    schedule: (raw.schedule ?? []).map((e) => ({
      start: Number(e.start ?? 0),
      end: Number(e.end ?? 24),
      location: e.location ?? raw.locationId ?? '',
      activityEn: e.activityEn ?? '',
      activityAr: e.activityAr ?? e.activityEn ?? '',
    })),
  };
}

function normalisePair(value, fallback) {
  if (Array.isArray(value) && value.length >= 2) {
    const a = Number(value[0]);
    const b = Number(value[1]);
    return [Number.isFinite(a) ? a : fallback[0], Number.isFinite(b) ? b : fallback[1]];
  }
  return [...fallback];
}
