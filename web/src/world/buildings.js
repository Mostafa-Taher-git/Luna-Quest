/**
 * The sixteen colony buildings, built from content/buildings.json.
 *
 * Each functional building is a real walkable structure: a foundation, pressure walls with a
 * door on the plaza side, a roof that matches its function (barrel vault, glass vault, gable,
 * flat equipment deck), partitions for its rooms, furniture suited to each room, and one
 * console per interaction point. That is the GDD rule the Godot build follows: no gameplay
 * building may be exterior-only.
 *
 * Style comes from the building id, dimensions and colour from the content file, so the
 * world stays data-driven.
 */
import * as THREE from 'three';
import {
  SolidWorld,
  box,
  solidBox,
  cylinder,
  wallWithDoor,
  windowBand,
  portico,
  hatch,
  radiator,
  pipeRun,
  ladder,
  tank,
  mast,
  solarArrayRow,
  bermSegment,
  crates,
  desk,
  bench,
  planter,
  bunk,
  consoleStation,
  ceilingLight,
  rover,
  beacon,
  mulberry,
} from './parts.js';

const WALL_T = 0.3;
const WALL_H = 3.2;

/** A canvas text plane, used for building signage and console names. */
export function makeSignTexture(mats, { title, subtitle = '', accent = '#f2a93b', width = 512, height = 128 }) {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = 'rgba(9, 14, 21, 0.92)';
  ctx.fillRect(0, 0, width, height);
  ctx.strokeStyle = accent;
  ctx.lineWidth = 3;
  ctx.strokeRect(1.5, 1.5, width - 3, height - 3);
  ctx.fillStyle = accent;
  ctx.fillRect(0, 0, 10, height);
  ctx.textBaseline = 'middle';
  ctx.fillStyle = '#e9f1f9';
  ctx.font = '600 40px "Noto Latin", system-ui, sans-serif';
  ctx.fillText(title, 26, subtitle ? height * 0.38 : height * 0.52, width - 40);
  if (subtitle) {
    ctx.fillStyle = '#9db2c8';
    ctx.font = '500 30px "Noto Arabic", "Noto Latin", system-ui, sans-serif';
    ctx.fillText(subtitle, 26, height * 0.74, width - 40);
  }
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 4;
  return tex;
}

/** Which side of the building faces the plaza and the main airlock walkway. */
const PLAZA = { x: 0, z: 14 };
function doorSide(building) {
  const [x, z] = building.position;
  const dx = PLAZA.x - x;
  const dz = PLAZA.z - z;
  if (Math.abs(dz) >= Math.abs(dx)) return dz >= 0 ? 'north' : 'south';
  return dx >= 0 ? 'east' : 'west';
}

const STYLES = {
  habitat: { roof: 'vault', ribbed: true, cladding: 'panels', trim: 'accent', lights: 3 },
  mission_control: { roof: 'flat', ribbed: true, cladding: 'panels', trim: 'accentBlue', dish: true, lights: 2 },
  greenhouse: { roof: 'glass', ribbed: true, cladding: 'glass', trim: 'green', lights: 2 },
  workshop: { roof: 'gable', ribbed: false, cladding: 'panels', trim: 'accent', crane: true, lights: 2 },
  life_support: { roof: 'flat', ribbed: true, cladding: 'panels', trim: 'cyan', tanks: true, lights: 2 },
  lab: { roof: 'vault', ribbed: true, cladding: 'panels', trim: 'violet', cupola: true, lights: 2 },
  medical: { roof: 'vault', ribbed: true, cladding: 'panels', trim: 'red', cross: true, lights: 2 },
  garage: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'accent', rollDoors: 2, lights: 3 },
  storage: { roof: 'vault', ribbed: false, cladding: 'panels', trim: 'accent', containers: true, lights: 1 },
  comms_tower: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'cyan', mast: true, lights: 1 },
  regolith_plant: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'accent', silo: true, gantry: true, lights: 1 },
  storm_shelter: { roof: 'buried', ribbed: false, cladding: 'regolith', trim: 'accent', lights: 1 },
  power_farm: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'cyan', arrays: true, lights: 1 },
  ice_site: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'cyan', derrick: true, lights: 1 },
  landing_pad: { roof: 'none', pad: true },
  airlock: { roof: 'flat', ribbed: false, cladding: 'panels', trim: 'accent', lights: 1 },
};

class BuildingContext {
  constructor(materials, solids, origin) {
    this.mats = materials;
    this.solids = solids;
    this.origin = origin ? { x: origin.x, z: origin.z } : { x: 0, z: 0 };
    this.group = new THREE.Group();
    this.group.position.set(this.origin.x, 0, this.origin.z);
  }
}

/**
 * Builds one building. Returns { group, consoles, lights } where consoles carry the
 * interaction ids the player can use and the meshes the raycast looks for.
 */
export function createBuilding(materials, building, { solids = new SolidWorld() } = {}) {
  const style = STYLES[building.id] ?? { roof: 'flat', lights: 1 };
  const [ox, oz] = building.position;
  const ctx = new BuildingContext(materials, solids, { x: ox, z: oz });
  const [w, d] = building.footprint;
  const h = building.height;
  const consoles = [];

  // Foundation: every building stands on a sintered pad, 10 cm proud of the regolith.
  const pad = box(ctx.group, w + 2.4, 0.2, d + 2.4, materials.regolithClay, { y: 0.1, cast: false });
  pad.name = 'foundation';
  box(ctx.group, w + 2.8, 0.06, d + 2.8, materials.panelDark, { y: 0.02, cast: false });
  // The pad is a step you walk up, not a hole you fall into.
  solids.addBox(ctx.origin.x, 0.1, ctx.origin.z, w + 2.4, 0.2, d + 2.4, { tag: 'pad' });

  if (building.id === 'landing_pad') {
    buildLandingPad(ctx, building, materials, solids);
    return { group: ctx.group, consoles, solids };
  }

  // ---- shell -----------------------------------------------------------------
  const door = doorSide(building);
  const front = {
    north: { x: 0, z: d / 2 - WALL_T / 2, yaw: 0, length: w },
    south: { x: 0, z: -d / 2 + WALL_T / 2, yaw: 0, length: w },
    east: { x: w / 2 - WALL_T / 2, z: 0, yaw: Math.PI / 2, length: d },
    west: { x: -w / 2 + WALL_T / 2, z: 0, yaw: Math.PI / 2, length: d },
  };
  const doorWall = front[door];
  const wallMat = style.cladding === 'glass' ? materials.interiorWall : materials.hull;

  // Front wall with the entrance.
  wallWithDoor(ctx, {
    x: doorWall.x,
    z: doorWall.z,
    yaw: doorWall.yaw,
    length: doorWall.length,
    height: WALL_H,
    doorWidth: 2.4,
    material: wallMat,
  });

  // The other three walls, in two lengths each so the shell reads as panels.
  for (const [name, wall] of Object.entries(front)) {
    if (name === door) continue;
    solidBox(ctx, wall.length, WALL_H, WALL_T, wallMat, {
      x: wall.x,
      y: WALL_H / 2,
      z: wall.z,
      ry: wall.yaw,
      tag: 'wall',
    });
  }

  // Interior floor and ceiling.
  const floor = box(ctx.group, w - WALL_T, 0.06, d - WALL_T, materials.interiorFloor, { y: 0.22, cast: false });
  floor.name = 'floor';
  solids.addBox(ctx.origin.x, 0.22, ctx.origin.z, w - WALL_T, 0.12, d - WALL_T, { tag: 'floor' });
  box(ctx.group, w - WALL_T, 0.12, d - WALL_T, materials.ceiling, { y: WALL_H - 0.06, cast: false });

  // ---- roof ------------------------------------------------------------------
  buildRoof(ctx, building, style, { w, d, h });

  // ---- cladding and architectural detail -------------------------------------
  cladding(ctx, building, style, { w, d, h, door });

  // ---- entrance ---------------------------------------------------------------
  const porticoPos = {
    north: { x: 0, z: d / 2 + 1.1, yaw: 0 },
    south: { x: 0, z: -d / 2 - 1.1, yaw: Math.PI },
    east: { x: w / 2 + 1.1, z: 0, yaw: Math.PI / 2 },
    west: { x: -w / 2 - 1.1, z: 0, yaw: -Math.PI / 2 },
  }[door];
  portico(ctx, { x: porticoPos.x, z: porticoPos.z, yaw: porticoPos.yaw, width: 3.6, depth: 2.4, height: 3.1 });

  // Dust-mitigation porch. Lunar dust is fine and abrasive and attacks seals, joints and
  // instruments, so a real surface habitat gives the crew a grated sill and a marked cleaning
  // zone before the pressure hatch. See the surface habitat design note in docs/research/.
  const dustPorch = new THREE.Group();
  dustPorch.name = 'dust-porch';
  dustPorch.position.set(porticoPos.x, 0, porticoPos.z);
  dustPorch.rotation.y = porticoPos.yaw;
  ctx.group.add(dustPorch);
  for (let i = 0; i < 7; i += 1) {
    box(dustPorch, 3.2, 0.04, 0.16, materials.alloy, { x: 0, y: 0.15, z: -0.9 + i * 0.3, cast: false });
  }
  box(dustPorch, 3.4, 0.05, 0.12, materials.hazardStripe, { x: 0, y: 0.17, z: 1.06, cast: false });
  for (const sx of [-1, 1]) {
    box(dustPorch, 0.12, 0.5, 0.12, materials.alloy, { x: sx * 1.62, y: 0.4, z: -0.85, cast: false });
  }

  // The hatch hangs open against the wall beside the opening. A door panel across the
  // doorway would look tidy and make the building impossible to enter.
  const side = door === 'north' || door === 'south' ? [-2.75, 0.05] : [0.05, -2.75];
  hatch(ctx, ctx.group, {
    x: doorWall.x + side[0],
    y: 1.1,
    z: doorWall.z + side[1],
    w: 2.1,
    h: 2.1,
    yaw: doorWall.yaw,
    material: materials.panel,
  });

  // Bilingual signage, hung from the front of the portico. It used to sit on the wall over the
  // door, where the canopy slab (3.02 m) cut straight through the top of the letters from every
  // angle a player could stand at. Hanging it under the canopy's leading edge keeps it legible.
  const signTex = makeSignTexture(materials, { title: building.nameEn, subtitle: building.nameAr, accent: '#f2a93b' });
  const signMat = new THREE.MeshBasicMaterial({ map: signTex, transparent: true });
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(4.0, 0.82), signMat);
  const outward = {
    north: { x: 0, z: 1 },
    south: { x: 0, z: -1 },
    east: { x: 1, z: 0 },
    west: { x: -1, z: 0 },
  }[door];
  sign.position.set(porticoPos.x + outward.x * 1.04, 2.6, porticoPos.z + outward.z * 1.04);
  sign.rotation.y = porticoPos.yaw;
  ctx.group.add(sign);

  // ---- interior ---------------------------------------------------------------
  const interior = interiorRooms(ctx, building, style, { w, d });
  consoles.push(...interior.consoles);

  // ---- regolith shielding ------------------------------------------------------
  // Piled regolith on the lee side of the modules that need the most shielding. NASA surface
  // habitat studies treat in-situ material as radiation and thermal cover; the berm is kept
  // clear of the hatch and the plaza so it never blocks an approach route.
  if (['habitat', 'life_support', 'storm_shelter', 'medical'].includes(building.id)) {
    const away = {
      north: { x: 0, z: -d / 2 - 2.9, yaw: 0 },
      south: { x: 0, z: d / 2 + 2.9, yaw: 0 },
      east: { x: -w / 2 - 2.9, z: 0, yaw: Math.PI / 2 },
      west: { x: w / 2 + 2.9, z: 0, yaw: Math.PI / 2 },
    }[door];
    bermSegment(ctx, {
      x: away.x,
      z: away.z,
      yaw: away.yaw,
      length: Math.min(w, d) * 0.82,
      height: 1.15,
    });
  }

  // ---- exterior kit ------------------------------------------------------------
  exteriorProps(ctx, building, style, { w, d, h, door });

  return { group: ctx.group, consoles, solids };
}

/**
 * Barrel vault over a rectangular plan: an upper-half parametric arch extruded along Z.
 * Built by hand so the arc, the springing line and the normals are exactly where they should
 * be; a rotated half-cylinder is easy to get wrong and impossible to debug by eye.
 */
function barrelVaultGeometry(radius, length, rise, segments = 30, slices = 10) {
  const positions = [];
  const normals = [];
  const uvs = [];
  const indices = [];
  for (let s = 0; s <= slices; s += 1) {
    const z = -length / 2 + (length * s) / slices;
    for (let i = 0; i <= segments; i += 1) {
      const a = (i / segments) * Math.PI;
      const x = Math.cos(a) * radius;
      const y = Math.sin(a) * rise;
      positions.push(x, y, z);
      const n = new THREE.Vector3(Math.cos(a) / radius, Math.sin(a) / Math.max(rise, 0.001), 0).normalize();
      normals.push(n.x, n.y, n.z);
      uvs.push(i / segments, s / slices);
    }
  }
  const stride = segments + 1;
  for (let s = 0; s < slices; s += 1) {
    for (let i = 0; i < segments; i += 1) {
      const a = s * stride + i;
      const b = a + 1;
      const c = a + stride;
      const d2 = c + 1;
      indices.push(a, c, b, b, c, d2);
    }
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  return geo;
}

function buildRoof(ctx, building, style, { w, d, h }) {
  const mats = ctx.mats;
  const rise = Math.max(1.2, Math.min(2.6, (h - WALL_H) * 0.9));
  if (style.roof === 'none') return;

  if (style.roof === 'vault' || style.roof === 'glass' || style.roof === 'buried') {
    const radius = w / 2;
    const arc = new THREE.Mesh(
      barrelVaultGeometry(radius, d, rise, 30, 8),
      style.roof === 'glass' ? mats.glass : style.roof === 'buried' ? mats.regolithClay : mats.hull,
    );
    arc.position.y = WALL_H;
    arc.castShadow = true;
    arc.receiveShadow = true;
    ctx.group.add(arc);

    // End caps. A real surface module is a sealed horizontal cylinder, not an open canopy:
    // both ends get a half-ellipse bulkhead at the springing line. The cap material is cloned
    // so forcing DoubleSide here cannot change the winding of the rest of the shell.
    const capMat = (style.roof === 'glass' ? mats.glass : style.roof === 'buried' ? mats.regolithClay : mats.hull).clone();
    capMat.side = THREE.DoubleSide;
    const capGeo = new THREE.CircleGeometry(1, 32, 0, Math.PI);
    for (const z of [-d / 2, d / 2]) {
      const cap = new THREE.Mesh(capGeo, capMat);
      cap.scale.set(radius, rise, 1);
      cap.position.set(0, WALL_H, z);
      cap.castShadow = true;
      cap.receiveShadow = true;
      cap.name = 'vault-bulkhead';
      ctx.group.add(cap);
    }
    // The vault is now a closed pressure shell: keep the commander from leaping through it.
    ctx.solids.addBox(ctx.origin.x, WALL_H + rise * 0.5, ctx.origin.z, w, rise, d, { tag: 'vault-ceiling' });

    // Arch ribs: the strongest visual cue that this is a pressure structure.
    if (style.ribbed) {
      const ribs = Math.max(3, Math.round(d / 1.6));
      for (let i = 0; i <= ribs; i += 1) {
        const z = -d / 2 + (d * i) / ribs;
        // A torus with a half arc already lies across the module, so it only needs a
        // vertical squash to follow the vault's rise.
        const rib = new THREE.Mesh(new THREE.TorusGeometry(radius * 1.005, 0.05, 6, 22, Math.PI), mats.alloy);
        rib.position.set(0, WALL_H, z);
        rib.scale.set(1, Math.min(1, rise / radius), 1);
        rib.castShadow = true;
        ctx.group.add(rib);
      }
    }
    // Regolith cap on shielded roofs: a thicker ridge on top.
    if (style.roof === 'buried') {
      box(ctx.group, w + 1.6, 0.7, d + 1.2, mats.regolithClay, { y: WALL_H + rise * 0.55, cast: false });
    }
    return;
  }

  if (style.roof === 'gable') {
    const shape = new THREE.Shape();
    const halfW = w / 2 + 0.3;
    shape.moveTo(-halfW, 0);
    shape.lineTo(0, rise + 0.6);
    shape.lineTo(halfW, 0);
    shape.lineTo(-halfW, 0);
    const geo = new THREE.ExtrudeGeometry(shape, { depth: d + 0.6, bevelEnabled: false });
    const roof = new THREE.Mesh(geo, mats.panel);
    roof.rotation.y = 0;
    roof.position.set(0, WALL_H, -(d + 0.6) / 2);
    roof.castShadow = true;
    roof.receiveShadow = true;
    ctx.group.add(roof);
    for (let i = 0; i <= 4; i += 1) {
      box(ctx.group, 0.16, 0.16, d + 0.7, mats.alloy, { x: -w / 2 + (w * i) / 4, y: WALL_H + 0.08 });
    }
    return;
  }

  // Flat equipment deck.
  box(ctx.group, w + 0.7, 0.34, d + 0.7, mats.panel, { y: WALL_H + 0.17 });
  box(ctx.group, w * 0.3, 0.5, d * 0.3, mats.panelDark, { x: w * 0.2, y: WALL_H + 0.6 });
  box(ctx.group, 1.6, 0.2, 1.2, mats.alloy, { x: -w * 0.22, y: WALL_H + 0.45 });
  for (let i = 0; i < 4; i += 1) {
    box(ctx.group, 0.1, 0.9, 0.1, mats.alloy, { x: -w / 2 + 0.5 + i * (w - 1) / 3, y: WALL_H + 0.75, z: d * 0.36, cast: false });
  }
}

function cladding(ctx, building, style, { w, d, h, door }) {
  const mats = ctx.mats;
  const trim = {
    accent: mats.accent,
    accentBlue: mats.accentBlue,
    cyan: mats.accentBlue,
    green: mats.plant,
    violet: mats.accentBlue,
    red: mats.accent,
  }[style.trim] ?? mats.accent;

  // Painted band at waist height, like the kit's stripes.
  const bandY = 1.55;
  const bandMat = trim;
  for (const [sx, sz, len, yaw] of [
    [0, d / 2 + 0.01, w, 0],
    [0, -d / 2 - 0.01, w, 0],
    [w / 2 + 0.01, 0, d, Math.PI / 2],
    [-w / 2 - 0.01, 0, d, Math.PI / 2],
  ]) {
    // Skip the doorway so the hatch stays clean.
    if (Math.abs(sx) < 0.01 && Math.abs(sz) > 0.01 && ((sz > 0 && door === 'north') || (sz < 0 && door === 'south'))) {
      box(ctx.group, len * 0.42, 0.22, 0.06, bandMat, { x: -len * 0.27, y: bandY, z: sz, yaw, cast: false });
      box(ctx.group, len * 0.42, 0.22, 0.06, bandMat, { x: len * 0.27, y: bandY, z: sz, yaw, cast: false });
    } else {
      box(ctx.group, len * 0.92, 0.22, 0.06, bandMat, { x: sx, y: bandY, z: sz, yaw, cast: false });
    }
  }

  // Vertical panel seams.
  const seams = Math.max(2, Math.round(w / 2.4));
  for (let i = 1; i < seams; i += 1) {
    const x = -w / 2 + (w * i) / seams;
    box(ctx.group, 0.05, WALL_H - 0.3, 0.05, mats.panelDark, { x, y: WALL_H / 2, z: d / 2 + 0.02, cast: false });
    box(ctx.group, 0.05, WALL_H - 0.3, 0.05, mats.panelDark, { x, y: WALL_H / 2, z: -d / 2 - 0.02, cast: false });
  }

  // Window band on the plaza side for habitable rooms.
  if (['habitat', 'mission_control', 'lab', 'medical', 'greenhouse'].includes(building.id)) {
    const side = door === 'north' ? 1 : door === 'south' ? -1 : 0;
    if (side !== 0) {
      windowBand(ctx, ctx.group, { x: -w * 0.28, y: 2.15, z: side * (d / 2 + 0.04), width: w * 0.3, height: 0.62 });
      windowBand(ctx, ctx.group, { x: w * 0.28, y: 2.15, z: side * (d / 2 + 0.04), width: w * 0.3, height: 0.62 });
    }
  }

  // MLI (gold) blanket over one corner: insulation is a real lunar building material.
  if (['habitat', 'life_support', 'lab', 'medical'].includes(building.id)) {
    box(ctx.group, w * 0.22, 1.1, 0.08, mats.mli, { x: w * 0.32, y: 2.6, z: d / 2 + 0.03, cast: false });
  }

  // Utility spine: power and coolant run along the back wall.
  box(ctx.group, w * 0.9, 0.16, 0.2, mats.alloy, { y: 0.45, z: -d / 2 - 0.14 });
  for (let i = 0; i < 3; i += 1) {
    box(ctx.group, 0.12, 0.5, 0.12, mats.panelDark, {
      x: -w * 0.35 + i * w * 0.35,
      y: 0.25,
      z: -d / 2 - 0.14,
      cast: false,
    });
  }

  // Ladder to the roof deck on the outside, and a small antenna.
  ladder(ctx, { x: -w / 2 - 0.2, z: -d * 0.32, height: WALL_H + 0.6, yaw: Math.PI / 2 });
  box(ctx.group, 0.06, 1.1, 0.06, mats.alloy, { x: w * 0.4, y: WALL_H + 1.0, z: -d * 0.3 });
  box(ctx.group, 0.22, 0.22, 0.22, mats.panelDark, { x: w * 0.4, y: WALL_H + 1.55, z: -d * 0.3, cast: false });
}

function interiorRooms(ctx, building, style, { w, d }) {
  const mats = ctx.mats;
  const consoles = [];
  const inset = WALL_T / 2 + 0.15;
  const usePartitions = building.rooms.length > 1;

  for (const room of building.rooms) {
    const [rx, rz] = room.offset;
    const [rw, rd] = room.size;

    if (usePartitions) {
      // Partitions on the room's outer edges; a 1.4 m gap keeps every room reachable.
      for (const [px, pz, len, yaw] of [
        [rx - rw / 2, rz, rd, Math.PI / 2],
        [rx + rw / 2, rz, rd, Math.PI / 2],
        [rx, rz + rd / 2, rw, 0],
      ]) {
        if (px < -w / 2 + inset || px > w / 2 - inset) continue;
        if (pz > d / 2 - inset) continue;
        const clipped = Math.min(len * 0.82, len);
        const isX = Math.abs(yaw) > 0.01;
        const sideX = Math.max(-w / 2 + inset, Math.min(w / 2 - inset, px + (isX ? 0 : 0)));
        const sideZ = Math.max(-d / 2 + inset, Math.min(d / 2 - inset, pz));
        box(ctx.group, isX ? 0.16 : clipped, WALL_H * 0.82, isX ? clipped : 0.16, mats.interiorWall, {
          x: sideX,
          y: WALL_H * 0.41,
          z: sideZ,
          cast: false,
        });
      }
    }

    // Room furniture, chosen by what the room is for.
    const purpose = `${room.id} ${room.purpose ?? ''}`.toLowerCase();
    const cx = Math.max(-w / 2 + 1, Math.min(w / 2 - 1, rx));
    const cz = Math.max(-d / 2 + 1, Math.min(d / 2 - 1, rz));

    if (/common|meeting|ops|map|control|network/.test(purpose)) {
      desk(ctx, ctx.group, { x: cx, y: 0.25, z: cz + 0.6, yaw: 0, width: Math.min(2.4, rw * 0.6) });
      bench(ctx, ctx.group, { x: cx - 0.9, y: 0.25, z: cz - 1.1, yaw: 0 });
    } else if (/rest|sleep|quarter/.test(purpose)) {
      bunk(ctx, ctx.group, { x: cx + rw * 0.22, y: 0.25, z: cz, yaw: 0 });
      bunk(ctx, ctx.group, { x: cx + rw * 0.22, y: 0.25, z: cz - 1.0, yaw: 0 });
    } else if (/grow|harvest|seed|kitchen|food/.test(purpose)) {
      planter(ctx, ctx.group, { x: cx, y: 0.25, z: cz + rd * 0.24, yaw: 0, width: Math.min(2.6, rw * 0.7) });
      planter(ctx, ctx.group, { x: cx, y: 0.25, z: cz - rd * 0.24, yaw: 0, width: Math.min(2.6, rw * 0.7) });
    } else if (/repair|parts|fabrication|vehicle|charging|bench|sample/.test(purpose)) {
      desk(ctx, ctx.group, { x: cx, y: 0.25, z: cz - rd * 0.28, yaw: Math.PI, width: Math.min(2.2, rw * 0.6) });
      crates(ctx, { x: ctx.origin.x + cx + rw * 0.24, z: ctx.origin.z + cz + rd * 0.12, yaw: 0.4, seed: 21 });
    } else if (/medical|check|exercise|dose/.test(purpose)) {
      bench(ctx, ctx.group, { x: cx, y: 0.25, z: cz + rd * 0.2, yaw: 0 });
      box(ctx.group, 0.8, 0.12, 1.9, mats.hullWarm, { x: cx - rw * 0.24, y: 0.7, z: cz, cast: false });
    } else {
      desk(ctx, ctx.group, { x: cx, y: 0.25, z: cz - rd * 0.26, yaw: Math.PI, width: Math.min(1.8, rw * 0.55) });
    }

    ceilingLight(ctx, ctx.group, { x: cx, y: WALL_H - 0.22, z: cz, w: 1.1, d: 0.5 });

    // Every interaction gets a console in its own room.
    for (const point of building.interactions.filter((i) => i.roomId === room.id)) {
      const accent = {
        read: 0x58c6e8,
        use: 0x63d08f,
        repair: 0xf2a93b,
        clean: 0xb295f2,
        harvest: 0x8fd06b,
        inspect: 0x58c6e8,
      }[point.kind] ?? 0x58c6e8;
      // Sit the console against the far wall of the room, facing the doorway.
      const px = Math.max(-w / 2 + 0.9, Math.min(w / 2 - 0.9, cx + (consoles.length % 2 ? 0.9 : -0.9)));
      const pz = Math.max(-d / 2 + 0.9, Math.min(d / 2 - 0.9, cz + rd * 0.3));
      const station = consoleStation(ctx, {
        x: px,
        z: pz,
        yaw: Math.PI,
        label: point.nameEn,
        accent,
      });
      station.screen.userData.buildingId = building.id;
      station.screen.userData.interactionId = point.id;
      station.group.userData.buildingId = building.id;
      station.group.userData.interactionId = point.id;

      station.group.position.y = 0.25; // stand on the deck, not in it
      const tex = makeSignTexture(mats, { title: point.nameEn, subtitle: point.nameAr, accent: '#58c6e8', width: 512, height: 128 });
      const plate = new THREE.Mesh(
        new THREE.PlaneGeometry(1.5, 0.38),
        new THREE.MeshBasicMaterial({ map: tex, transparent: true }),
      );
      plate.position.set(0, 1.78, 0.02);
      plate.rotation.y = Math.PI;
      station.group.add(plate);

      consoles.push({
        buildingId: building.id,
        interactionId: point.id,
        nameEn: point.nameEn,
        nameAr: point.nameAr,
        kind: point.kind,
        learning: point.learning,
        roomId: room.id,
        mesh: station.screen,
        group: station.group,
        world: new THREE.Vector3(ctx.origin.x + px, 1.3, ctx.origin.z + pz),
      });
    }
  }
  return { consoles };
}

function exteriorProps(ctx, building, style, { w, d, h, door }) {
  const mats = ctx.mats;
  const back = door === 'north' ? -1 : 1;

  if (style.dish) {
    mast(ctx, { x: w * 0.36, z: back * (d / 2 + 3.4), height: 5.4, dishR: 1.2 });
  }
  if (style.mast) {
    mast(ctx, { x: w * 0.3, z: back * (d / 2 + 2.2), height: 11.5, dishR: 1.5 });
    box(ctx.group, 2.4, 1.2, 1.6, mats.panelDark, { x: w * 0.3, y: 0.6, z: back * (d / 2 + 2.2) });
  }
  if (style.tanks) {
    tank(ctx, { x: -w / 2 - 2.2, z: back * d * 0.36, radius: 1.1, height: 3.4 });
    tank(ctx, { x: -w / 2 - 2.2, z: back * d * 0.36 - 2.6, radius: 1.1, height: 3.4, horizontal: true });
    radiator(ctx, { x: w / 2 + 2.4, z: 0, yaw: -Math.PI / 2, width: 4.4, height: 2.2 });
    pipeRun(ctx, { from: [-w / 2 - 1.4, back * d * 0.36], to: [-w / 2 - 0.2, back * d * 0.36 - 4] });
  }
  if (style.tanks === undefined && ['habitat', 'lab', 'medical', 'workshop', 'power_farm'].includes(building.id)) {
    radiator(ctx, { x: w / 2 + 2.2, z: back * d * 0.2, yaw: -Math.PI / 2, width: 3.6, height: 2.0 });
  }
  if (style.silo) {
    tank(ctx, { x: -w / 2 - 2.6, z: back * d * 0.3, radius: 1.5, height: 6.2 });
    box(ctx.group, 2.2, 2.4, 2.2, mats.panelDark, { x: w / 2 + 2.4, y: 1.2, z: back * d * 0.3 });
  }
  if (style.gantry) {
    for (const sx of [-1, 1]) {
      for (const sz of [-1, 1]) {
        box(ctx.group, 0.16, 4.4, 0.16, mats.alloy, { x: sx * 2.2 + w / 2 + 1.6, y: 2.2, z: sz * 1.8 + back * d * 0.3 - 2.2 });
      }
    }
    box(ctx.group, 4.6, 0.24, 4.2, mats.panel, { x: w / 2 + 1.6, y: 4.4, z: back * d * 0.3 - 2.2 });
  }
  if (style.derrick) {
    const bx = w / 2 + 3.2;
    const bz = back * d * 0.2;
    for (let i = 0; i < 4; i += 1) {
      const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
      box(ctx.group, 0.14, 7.4, 0.14, mats.alloy, { x: bx + Math.cos(a) * 0.9, y: 3.7, z: bz + Math.sin(a) * 0.9 });
    }
    for (let y = 1.2; y < 7.4; y += 1.4) {
      box(ctx.group, 2.0, 0.08, 0.08, mats.alloy, { x: bx, y, z: bz + 0.9 });
      box(ctx.group, 2.0, 0.08, 0.08, mats.alloy, { x: bx, y, z: bz - 0.9 });
    }
    box(ctx.group, 0.5, 0.5, 0.5, mats.accent, { x: bx, y: 7.6, z: bz });
    ctx.solids.addBox(ctx.origin.x + bx, 3.7, ctx.origin.z + bz, 2.2, 7.4, 2.2, { tag: 'derrick' });
  }
  if (style.cross) {
    box(ctx.group, 0.9, 0.28, 0.06, mats.accent, { x: 0, y: 2.7, z: back * -0.01 + d / 2 + 0.03, cast: false });
    box(ctx.group, 0.28, 0.9, 0.06, mats.accent, { x: 0, y: 2.7, z: d / 2 + 0.03, cast: false });
  }
  if (style.containers) {
    crates(ctx, { x: w / 2 + 2.4, z: back * d * 0.3, yaw: 0.2, seed: 7 });
    crates(ctx, { x: -w / 2 - 2.4, z: back * d * 0.35, yaw: -0.4, seed: 9 });
  }
  if (style.rollDoors) {
    for (let i = 0; i < style.rollDoors; i += 1) {
      box(ctx.group, 3.2, 2.8, 0.14, i % 2 ? mats.panel : mats.panelDark, {
        x: -w * 0.24 + i * w * 0.48,
        y: 1.4,
        z: back * (d / 2 + 0.02),
      });
      for (let s = 0; s < 6; s += 1) {
        box(ctx.group, 3.1, 0.05, 0.18, mats.alloy, { x: -w * 0.24 + i * w * 0.48, y: 0.4 + s * 0.46, z: back * (d / 2 + 0.04), cast: false });
      }
    }
    rover(ctx, { x: w * 0.5 + 2.2, z: back * d * 0.4, yaw: 0.6 });
  }
  if (style.crane) {
    box(ctx.group, 0.24, 0.24, d * 0.8, mats.alloy, { x: w * 0.24, y: WALL_H - 0.4, z: 0, cast: false });
    box(ctx.group, 0.4, 0.3, 0.5, mats.accent, { x: w * 0.24, y: WALL_H - 0.7, z: d * 0.14 });
  }
  if (style.arrays) {
    // The array farm sits behind the inverter hall: two rows of vertical trackers.
    for (let i = 0; i < 5; i += 1) {
      solarArrayRow(ctx, { x: w / 2 + 4.5 + i * 4.2, z: back * (d * 0.1), yaw: -Math.PI / 2, width: 4.6, height: 3.0 });
    }
    tank(ctx, { x: -w / 2 - 2.6, z: back * d * 0.35, radius: 1.2, height: 2.4, horizontal: true });
    for (let i = 0; i < 3; i += 1) {
      box(ctx.group, 1.6, 1.1, 1.1, mats.panelDark, { x: -w / 2 - 1.4, y: 0.55, z: back * d * 0.3 + i * 1.6 });
    }
  }
  if (building.id === 'greenhouse') {
    // Glass barrel vault gets a condenser skid and a water line out to life support.
    box(ctx.group, 2.0, 1.2, 1.2, mats.panel, { x: -w / 2 - 2.0, y: 0.6, z: back * d * 0.3 });
    pipeRun(ctx, { from: [-w / 2 - 2.0, back * d * 0.3], to: [-w / 2 - 2.0, back * d * 0.3 - 6], insulated: true });
  }
  if (building.id === 'storm_shelter') {
    bermSegment(ctx, { x: 0, z: back * (d / 2 + 2.6), yaw: 0, length: w + 3, height: 1.4 });
    for (const sx of [-1, 1]) {
      bermSegment(ctx, { x: sx * (w / 2 + 1.8), z: 0, yaw: Math.PI / 2, length: d + 1.5, height: 1.3 });
    }
  }
  if (building.id === 'airlock') {
    // The airlock is the seam between inside and outside: grating, hazard paint, EVA rack.
    box(ctx.group, 2.6, 0.06, 3.0, mats.hazardStripe, { y: 0.24, z: back * (d / 2 + 1.4), cast: false });
    for (let i = 0; i < 3; i += 1) {
      box(ctx.group, 0.9, 1.5, 0.5, mats.panelDark, { x: -2.4 + i * 0.9, y: 0.9, z: back * (d / 2 + 2.0) });
    }
  }
  if (building.id === 'storage') {
    tank(ctx, { x: w / 2 + 2.4, z: back * d * 0.4, radius: 1.2, height: 3.0, horizontal: true });
  }
  if (building.id === 'ice_site') {
    crates(ctx, { x: -w / 2 - 2.2, z: back * d * 0.4, yaw: 0.3, seed: 31 });
  }
  if (building.id === 'workshop' || building.id === 'garage') {
    beacon(ctx, { x: w / 2 + 1.6, z: -back * (d / 2 - 0.4) });
  }
}

function buildLandingPad(ctx, building, mats, solids) {
  const [w, d] = building.footprint;
  const radius = Math.min(w, d) / 2;
  const pad = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius + 0.6, 0.28, 40), mats.panelDark);
  pad.position.y = 0.14;
  pad.receiveShadow = true;
  ctx.group.add(pad);

  const ring = new THREE.Mesh(new THREE.RingGeometry(radius * 0.62, radius * 0.72, 48), new THREE.MeshStandardMaterial({
    color: 0xf2a93b,
    roughness: 0.7,
    metalness: 0.1,
  }));
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.29;
  ctx.group.add(ring);

  // Touchdown cross and the four corner markings.
  for (const yaw of [0, Math.PI / 2]) {
    const bar = box(ctx.group, radius * 1.3, 0.02, 0.5, mats.hull, { y: 0.3, cast: false });
    bar.rotation.y = yaw;
  }
  for (let i = 0; i < 4; i += 1) {
    const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
    box(ctx.group, 1.4, 0.02, 0.14, mats.accent, {
      x: Math.cos(a) * radius * 0.86,
      y: 0.3,
      z: Math.sin(a) * radius * 0.86,
      ry: -a,
      cast: false,
    });
  }

  // Approach beacons around the apron.
  for (let i = 0; i < 4; i += 1) {
    const a = (i / 4) * Math.PI * 2;
    beacon(ctx, { x: Math.cos(a) * (radius + 3.2), z: Math.sin(a) * (radius + 3.2) });
  }

  // The pad edge is walkable, so only a low lip collides.
  solids.addBox(ctx.origin.x, 0.15, ctx.origin.z, radius * 2, 0.3, radius * 2, { tag: 'pad' });
}

export { STYLES, doorSide };
