/**
 * Architectural parts for the colony.
 *
 * Everything here is built from a handful of primitives so the buildings read as engineered
 * structures rather than coloured boxes: ribbed pressure hulls on legs, barrel-vault glass,
 * blast berms, cable trays, radiators, hazard sills, airlock porticos. Each part registers an
 * axis-aligned collider so the commander can walk, stand and bump into the colony.
 */
import * as THREE from 'three';
import { PALETTE } from './materials.js';

const geomCache = new Map();
function boxGeo(w, h, d) {
  const key = `b${w.toFixed(3)}|${h.toFixed(3)}|${d.toFixed(3)}`;
  let g = geomCache.get(key);
  if (!g) {
    g = new THREE.BoxGeometry(w, h, d);
    geomCache.set(key, g);
  }
  return g;
}
function cylGeo(rt, rb, h, seg = 24) {
  const key = `c${rt.toFixed(3)}|${rb.toFixed(3)}|${h.toFixed(3)}|${seg}`;
  let g = geomCache.get(key);
  if (!g) {
    g = new THREE.CylinderGeometry(rt, rb, h, seg);
    geomCache.set(key, g);
  }
  return g;
}

/** Axis-aligned collider store. Rotated parts get a conservative world-space box. */
export class SolidWorld {
  constructor() {
    this.boxes = [];
  }

  addBox(cx, cy, cz, sx, sy, sz, { tag = '', blocks = true } = {}) {
    if (!blocks) return;
    this.boxes.push({
      minX: cx - sx / 2,
      maxX: cx + sx / 2,
      minY: cy - sy / 2,
      maxY: cy + sy / 2,
      minZ: cz - sz / 2,
      maxZ: cz + sz / 2,
      tag,
    });
  }

  /** Registers a yawed box as the AABB that contains it. */
  addRotatedBox(cx, cy, cz, sx, sy, sz, yaw, opts) {
    const c = Math.abs(Math.cos(yaw));
    const s = Math.abs(Math.sin(yaw));
    this.addBox(cx, cy, cz, sx * c + sz * s, sy, sx * s + sz * c, opts);
  }
}

function mesh(parent, geometry, material, { x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0, cast = true, receive = true } = {}) {
  const m = new THREE.Mesh(geometry, material);
  m.position.set(x, y, z);
  m.rotation.set(rx, ry, rz);
  m.castShadow = cast;
  m.receiveShadow = receive;
  parent.add(m);
  return m;
}

export function box(parent, w, h, d, material, opts = {}) {
  return mesh(parent, boxGeo(w, h, d), material, opts);
}

/** A box that also exists in the collision world. Local x/z are rotated into world space. */
export function solidBox(ctx, w, h, d, material, { x = 0, y = 0, z = 0, ry = 0, tag = '', blocks = true, cast = true } = {}) {
  const m = box(ctx.group, w, h, d, material, { x, y, z, ry, cast });
  const cos = Math.cos(ry);
  const sin = Math.sin(ry);
  const wx = ctx.origin.x + x * cos + z * sin;
  const wz = ctx.origin.z - x * sin + z * cos;
  if (ry === 0) ctx.solids.addBox(wx, y, wz, w, h, d, { tag, blocks });
  else ctx.solids.addRotatedBox(wx, y, wz, w, h, d, ry, { tag, blocks });
  return m;
}

export function cylinder(parent, rTop, rBottom, h, material, opts = {}) {
  return mesh(parent, cylGeo(rTop, rBottom, h, opts.seg ?? 24), material, opts);
}

/** A pressure hull: horizontal cylinder with domed ends, ribs and landing legs. */
export function pressureHull(ctx, { x = 0, z = 0, yaw = 0, radius = 3, length = 12, lift = 1.6, ribs = 5, material = null, trim = null } = {}) {
  const mat = material ?? ctx.mats.hull;
  const trimMat = trim ?? ctx.mats.accent;
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  const bodyY = lift + radius;
  // Main tube laid along Z.
  const tube = mesh(g, cylGeo(radius, radius, length, 32), mat, { x: 0, y: bodyY, z: 0, rx: Math.PI / 2 });
  tube.name = 'hull-tube';

  // Domed ends.
  const endGeo = new THREE.SphereGeometry(radius, 28, 18, 0, Math.PI * 2, 0, Math.PI / 2);
  mesh(g, endGeo, mat, { x: 0, y: bodyY, z: length / 2, rx: Math.PI / 2 });
  mesh(g, endGeo, mat, { x: 0, y: bodyY, z: -length / 2, rx: -Math.PI / 2 });

  // Rib rings read as structural frames and catch the low Sun.
  for (let i = 0; i < ribs; i += 1) {
    const t = ribs === 1 ? 0.5 : i / (ribs - 1);
    const z = -length / 2 + length * (0.08 + 0.84 * t);
    mesh(g, new THREE.TorusGeometry(radius + 0.035, 0.055, 8, 40), ctx.mats.alloy, { x: 0, y: bodyY, z, rx: 0 });
  }

  // Accent band along the side, like the kit's painted stripes.
  mesh(g, cylGeo(radius + 0.012, radius + 0.012, 0.34, 32), trimMat, {
    x: 0,
    y: bodyY + radius * 0.55,
    z: 0,
    rx: Math.PI / 2,
  });

  // Landing legs with foot pads.
  const legCount = Math.max(4, Math.round(length / 4));
  for (let i = 0; i < legCount; i += 1) {
    const t = i / (legCount - 1);
    const z = -length / 2 + 1.2 + (length - 2.4) * t;
    for (const side of [-1, 1]) {
      const lx = side * radius * 0.62;
      const leg = mesh(g, cylGeo(0.11, 0.13, lift + 0.6, 10), ctx.mats.alloy, { x: lx, y: (lift + 0.6) / 2, z, rz: side * 0.16 });
      leg.name = 'leg';
      mesh(g, cylGeo(0.42, 0.46, 0.09, 16), ctx.mats.dark, { x: lx + side * 0.12, y: 0.05, z });
    }
  }

  // Sintered-regolith plinth: a low apron the crew walks on, and radiation shielding
  // banked at both ends of the module.
  box(g, radius * 2 + 3.4, 0.5, length + 3.2, ctx.mats.regolithClay, { y: 0.25, cast: false });
  for (const sz of [-1, 1]) {
    box(g, radius * 2 + 2.4, 1.3, 1.6, ctx.mats.regolithClay, { y: 0.65, z: sz * (length / 2 + 1.6) });
  }

  ctx.solids.addRotatedBox(ctx.origin.x + x, bodyY, ctx.origin.z + z, radius * 2, radius * 2, length + radius * 1.1, yaw, { tag: 'hull' });
  // The plinth is low enough to step onto; the shield banks are solid.
  ctx.solids.addRotatedBox(ctx.origin.x + x, 0.25, ctx.origin.z + z, radius * 2 + 3.4, 0.5, length + 3.2, yaw, { tag: 'plinth' });
  for (const sz of [-1, 1]) {
    ctx.solids.addRotatedBox(ctx.origin.x + x, 0.65, ctx.origin.z + z + sz * (length / 2 + 1.6), radius * 2 + 2.4, 1.3, 1.6, yaw, { tag: 'berm' });
  }
  return g;
}

/** A straight wall with a doorway gap, so interiors are reachable without a door system. */
export function wallWithDoor(ctx, { x = 0, z = 0, length = 10, height = 3, yaw = 0, doorWidth = 2.2, doorHeight = 2.4, material = null, sill = true } = {}) {
  const mat = material ?? ctx.mats.interiorWall;
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  const side = (length - doorWidth) / 2;
  const thick = 0.24;
  for (const sign of [-1, 1]) {
    const cx = sign * (doorWidth / 2 + side / 2);
    solidBox({ ...ctx, group: g, origin: { x: ctx.origin.x + x, z: ctx.origin.z + z } }, side, height, thick, mat, {
      x: cx,
      y: height / 2,
      z: 0,
      ry: yaw,
      tag: 'wall',
    });
  }
  // Lintel above the door.
  solidBox({ ...ctx, group: g, origin: { x: ctx.origin.x + x, z: ctx.origin.z + z } }, doorWidth, height - doorHeight, thick, mat, {
    x: 0,
    y: doorHeight + (height - doorHeight) / 2,
    z: 0,
    ry: yaw,
    tag: 'wall',
  });
  if (sill) {
    const sillMesh = box(g, doorWidth, 0.08, 0.5, ctx.mats.hazardStripe, { x: 0, y: 0.04, z: 0, cast: false });
    sillMesh.name = 'sill';
  }
  return g;
}

/** Window band: dark recess, glass and a heavy frame, on every habitable module. */
export function windowBand(ctx, parent, { x = 0, y = 2, z = 0, width = 6, height = 0.7, yaw = 0, glass = true, frame = null } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, width, height + 0.2, 0.16, frame ?? ctx.mats.panelDark, { cast: false });
  if (glass !== false) box(g, width - 0.2, height, 0.05, ctx.mats.glass, { z: 0.06, cast: false });
  box(g, width + 0.16, 0.08, 0.2, ctx.mats.alloy, { y: (height + 0.2) / 2, cast: false });
  box(g, width + 0.16, 0.08, 0.2, ctx.mats.alloy, { y: -(height + 0.2) / 2, cast: false });
  return g;
}

/** A hatch or roll door: ribbed panel with a wheel handle and a hazard sill. */
export function hatch(ctx, parent, { x = 0, y = 1.2, z = 0, w = 1.6, h = 2.3, yaw = 0, material = null } = {}) {
  const mat = material ?? ctx.mats.panel;
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, w, h, 0.16, mat, { z: 0 });
  box(g, w * 0.16, h * 0.86, 0.2, ctx.mats.panelDark, { x: 0, z: 0.02 });
  for (let i = -2; i <= 2; i += 1) box(g, w * 0.9, 0.06, 0.19, ctx.mats.accent, { y: i * (h / 6), z: 0.03 });
  const wheel = mesh(g, new THREE.TorusGeometry(0.16, 0.03, 8, 20), ctx.mats.alloy, { z: 0.14 });
  wheel.name = 'hatch-wheel';
  box(g, w * 0.7, 0.1, 0.3, ctx.mats.hazardStripe, { y: -h / 2 + 0.05, z: 0.1 });
  return g;
}

/** Airlock portico: canopy, two stanchions, deck grating and a lamp. */
export function portico(ctx, { x = 0, z = 0, yaw = 0, width = 3.4, depth = 2.6, height = 3.2 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  box(g, width, 0.12, depth, ctx.mats.alloy, { y: 0.06 });
  box(g, width + 0.5, 0.16, depth + 0.3, ctx.mats.panel, { y: height });
  for (const sx of [-1, 1]) {
    for (const sz of [-1, 1]) {
      box(g, 0.14, height, 0.14, ctx.mats.alloy, { x: sx * width * 0.46, y: height / 2, z: sz * depth * 0.42 });
    }
  }
  // Lit canopy: an emissive panel, not a real light. Twenty porticos with point lights
  // would cost every material in the colony a shader slot and gain almost nothing.
  const lamp = box(g, 0.5, 0.06, 0.5, ctx.mats.lamp, { y: height - 0.12, cast: false });
  lamp.name = 'portico-lamp';

  ctx.solids.addRotatedBox(ctx.origin.x + x, height / 2, ctx.origin.z + z, width + 0.4, height, depth + 0.3, yaw, { tag: 'portico' });
  return g;
}

/** Railing: posts plus two rails, for observation decks and ramps. */
export function railing(ctx, parent, { x = 0, y = 0, z = 0, length = 6, yaw = 0, height = 1.05 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  const posts = Math.max(2, Math.round(length / 1.4));
  for (let i = 0; i <= posts; i += 1) {
    const px = -length / 2 + (length * i) / posts;
    box(g, 0.06, height, 0.06, ctx.mats.alloy, { x: px, y: height / 2 });
  }
  for (const ry of [height, height * 0.55]) {
    box(g, length, 0.045, 0.045, ctx.mats.alloy, { y: ry });
  }
  return g;
}

/** Radiator bank: white panels on a frame, angled to dump heat to space. */
export function radiator(ctx, { x = 0, z = 0, yaw = 0, width = 6, height = 2.6 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  for (let i = 0; i < 4; i += 1) {
    const panel = box(g, width / 4 - 0.12, height, 0.08, ctx.mats.hull, { x: -width / 2 + (width / 8) + (i * width) / 4, y: height / 2 + 0.9, z: 0 });
    panel.rotation.x = 0.12;
  }
  for (const sx of [-1, 1]) {
    box(g, 0.12, height + 1.1, 0.12, ctx.mats.alloy, { x: sx * width * 0.5, y: (height + 1.1) / 2 });
  }
  box(g, width + 0.3, 0.12, 0.5, ctx.mats.panelDark, { y: 0.06 });
  ctx.solids.addRotatedBox(ctx.origin.x + x, (height + 1.1) / 2, ctx.origin.z + z, width + 0.4, height + 1.1, 0.8, yaw, { tag: 'radiator' });
  return g;
}

/** Cable tray and pipe run along the ground between buildings. */
export function pipeRun(ctx, { from = [0, 0], to = [10, 0], height = 0.35, radius = 0.16, insulated = false } = {}) {
  const dx = to[0] - from[0];
  const dz = to[1] - from[1];
  const len = Math.hypot(dx, dz);
  if (len < 0.2) return null;
  const yaw = Math.atan2(dx, dz);
  const g = new THREE.Group();
  g.position.set((from[0] + to[0]) / 2, height, (from[1] + to[1]) / 2);
  g.rotation.y = yaw;
  ctx.group.add(g);
  const mat = insulated ? ctx.mats.mli : ctx.mats.alloy;
  const pipe = mesh(g, cylGeo(radius, radius, len, 12), mat, { rx: Math.PI / 2 });
  pipe.name = 'pipe';
  mesh(g, cylGeo(radius * 0.7, radius * 0.7, len, 10), ctx.mats.panelDark, { y: radius * 2.1, z: 0, rx: Math.PI / 2 });
  for (let i = 1; i < Math.round(len / 4); i += 1) {
    const z = -len / 2 + (len * i) / Math.round(len / 4);
    box(g, radius * 4, 0.12, 0.18, ctx.mats.panelDark, { y: 0.05, z });
  }
  ctx.solids.addRotatedBox((from[0] + to[0]) / 2, height / 2, (from[1] + to[1]) / 2, radius * 4, height, len, yaw, { tag: 'pipe' });
  return g;
}

/** Ladder up a module, with a safety cage. */
export function ladder(ctx, { x = 0, z = 0, height = 4, yaw = 0 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  for (const sx of [-0.22, 0.22]) box(g, 0.06, height, 0.06, ctx.mats.alloy, { x: sx, y: height / 2 });
  for (let y = 0.3; y < height; y += 0.32) box(g, 0.46, 0.04, 0.04, ctx.mats.alloy, { y });
  ctx.solids.addBox(ctx.origin.x + x, height / 2, ctx.origin.z + z, 0.6, height, 0.4, { tag: 'ladder', blocks: false });
  return g;
}

/** A tank farm: capsule tanks on saddles with a manifold. */
export function tank(ctx, { x = 0, z = 0, radius = 1.6, height = 5, yaw = 0, horizontal = false } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  if (horizontal) {
    const body = mesh(g, cylGeo(radius, radius, height, 24), ctx.mats.hull, { y: radius + 0.5, rx: Math.PI / 2 });
    body.name = 'tank';
    mesh(g, new THREE.SphereGeometry(radius, 24, 14, 0, Math.PI * 2, 0, Math.PI / 2), ctx.mats.hull, { y: radius + 0.5, z: height / 2, rx: Math.PI / 2 });
    mesh(g, new THREE.SphereGeometry(radius, 24, 14, 0, Math.PI * 2, 0, Math.PI / 2), ctx.mats.hull, { y: radius + 0.5, z: -height / 2, rx: -Math.PI / 2 });
    for (const sz of [-0.3, 0.3]) box(g, radius * 1.7, 0.5, 0.4, ctx.mats.panelDark, { y: 0.25, z: height * sz });
    ctx.solids.addRotatedBox(ctx.origin.x + x, radius + 0.5, ctx.origin.z + z, radius * 2, radius * 2 + 1, height + radius, yaw, { tag: 'tank' });
  } else {
    const body = mesh(g, cylGeo(radius, radius, height, 24), ctx.mats.hull, { y: height / 2 + 0.3 });
    body.name = 'tank';
    mesh(g, new THREE.SphereGeometry(radius, 24, 14, 0, Math.PI * 2, 0, Math.PI / 2), ctx.mats.hull, { y: height + 0.3 });
    mesh(g, new THREE.TorusGeometry(radius + 0.04, 0.06, 6, 28), ctx.mats.accent, { y: height * 0.72 });
    ctx.solids.addBox(ctx.origin.x + x, height / 2 + 0.3, ctx.origin.z + z, radius * 2, height + radius, radius * 2, { tag: 'tank' });
  }
  return g;
}

/** Lattice comms mast with a parabolic dish and an obstruction light. */
export function mast(ctx, { x = 0, z = 0, height = 12, dishR = 1.6, yaw = 0 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  const legs = 3;
  for (let i = 0; i < legs; i += 1) {
    const a = (i / legs) * Math.PI * 2;
    box(g, 0.1, height, 0.1, ctx.mats.alloy, { x: Math.cos(a) * 0.55, y: height / 2, z: Math.sin(a) * 0.55 });
  }
  for (let y = 1; y < height; y += 1.6) {
    const r = 0.55;
    for (let i = 0; i < legs; i += 1) {
      const a = (i / legs) * Math.PI * 2;
      const a2 = ((i + 1) / legs) * Math.PI * 2;
      const mx = (Math.cos(a) + Math.cos(a2)) * r * 0.5;
      const mz = (Math.sin(a) + Math.sin(a2)) * r * 0.5;
      const len = Math.hypot((Math.cos(a) - Math.cos(a2)) * r, (Math.sin(a) - Math.sin(a2)) * r);
      const bar = box(g, len, 0.05, 0.05, ctx.mats.alloy, { x: mx, z: mz, y });
      bar.rotation.y = -Math.atan2((Math.sin(a2) - Math.sin(a)), Math.cos(a2) - Math.cos(a));
    }
  }

  // Dish: a lathe-like bowl from a cone plus a ring.
  const dish = new THREE.Group();
  dish.position.set(0.9, height - 1.1, 0);
  dish.rotation.set(-0.5, 0.4, 0);
  g.add(dish);
  mesh(dish, new THREE.SphereGeometry(dishR, 26, 16, 0, Math.PI * 2, 0, Math.PI / 3.4), ctx.mats.hull, { rx: Math.PI });
  mesh(dish, new THREE.TorusGeometry(dishR * 0.97, 0.045, 6, 30), ctx.mats.alloy);
  box(dish, 0.06, dishR * 1.2, 0.06, ctx.mats.alloy, { y: dishR * 0.5 });

  const beacon = box(g, 0.18, 0.18, 0.18, ctx.mats.warning, { y: height + 0.1, cast: false });
  beacon.name = 'mast-beacon';

  ctx.solids.addBox(ctx.origin.x + x, height / 2, ctx.origin.z + z, 1.6, height, 1.6, { tag: 'mast' });
  return g;
}

/** One vertical, azimuth-tracking solar array row: the shape that makes the farm read. */
export function solarArrayRow(ctx, { x = 0, z = 0, yaw = 0, width = 6, height = 3.2, tracker = true } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  box(g, 0.5, 0.12, 0.5, ctx.mats.panelDark, { y: 0.06 });
  box(g, 0.16, 1.5, 0.16, ctx.mats.alloy, { y: 0.75 });
  const spin = new THREE.Group();
  spin.position.y = 1.5;
  g.add(spin);
  // Tilted panel bank, always facing the low Sun.
  const panel = new THREE.Group();
  panel.rotation.z = 0.16;
  spin.add(panel);
  const cells = mesh(panel, boxGeo(width, height, 0.07), ctx.mats.solar, { y: height / 2 + 0.2 });
  cells.name = 'solar-panel';
  box(panel, width + 0.12, 0.1, 0.16, ctx.mats.alloy, { y: 0.2 });
  box(panel, width + 0.12, 0.1, 0.16, ctx.mats.alloy, { y: height + 0.2 });
  for (const sx of [-1, 1]) box(panel, 0.1, height, 0.16, ctx.mats.alloy, { x: (sx * width) / 2, y: height / 2 + 0.2 });
  if (tracker) {
    box(spin, 0.3, 0.3, 0.7, ctx.mats.panel, { z: 0.3 });
    box(spin, 0.22, 0.22, 0.22, ctx.mats.accentBlue, { z: 0.6 });
  }
  ctx.solids.addRotatedBox(ctx.origin.x + x, height / 2 + 0.7, ctx.origin.z + z, width, height + 1.2, 0.7, yaw, { tag: 'array' });
  return { group: g, panel, spin };
}

/** Blast berm segment: a low ridge of sintered regolith. */
export function bermSegment(ctx, { x = 0, z = 0, yaw = 0, length = 8, height = 1.6 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  const ridge = mesh(g, new THREE.CylinderGeometry(height, height * 1.8, length, 12, 1, false, 0, Math.PI), ctx.mats.regolithClay, {
    y: 0,
    rz: Math.PI / 2,
    ry: 0,
  });
  ridge.rotation.set(0, Math.PI / 2, -Math.PI / 2);
  ridge.name = 'berm';
  ctx.solids.addRotatedBox(ctx.origin.x + x, height * 0.4, ctx.origin.z + z, length, height * 0.8, height * 2.6, yaw, { tag: 'berm' });
  return g;
}

/** Storage crates and container stacks — the clutter that makes a base look inhabited. */
export function crates(ctx, { x = 0, z = 0, yaw = 0, seed = 1 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  const rand = mulberry(seed);
  const count = 3 + Math.floor(rand() * 3);
  let top = 0;
  for (let i = 0; i < count; i += 1) {
    const w = 0.7 + rand() * 0.5;
    const h = 0.6 + rand() * 0.5;
    const d = 0.7 + rand() * 0.5;
    const px = (rand() - 0.5) * 2.4;
    const pz = (rand() - 0.5) * 2.4;
    const mat = rand() > 0.55 ? ctx.mats.panel : ctx.mats.hullWarm;
    box(g, w, h, d, mat, { x: px, y: top + h / 2, z: pz });
    box(g, w * 0.55, 0.05, d * 0.55, ctx.mats.accent, { x: px, y: top + h + 0.01, z: pz, cast: false });
    top += h * (rand() > 0.7 ? 1 : 0);
    if (top > 1.4) top = 0;
  }
  ctx.solids.addRotatedBox(ctx.origin.x + x, 0.8, ctx.origin.z + z, 3.2, 1.6, 3.2, yaw, { tag: 'crates' });
  return g;
}

/** Interior furniture: a desk with a screen, a bench, a planter, a bunk. */
export function desk(ctx, parent, { x = 0, y = 0, z = 0, yaw = 0, width = 1.8 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, width, 0.06, 0.7, ctx.mats.panel, { y: 0.75 });
  for (const sx of [-1, 1]) box(g, 0.06, 0.75, 0.06, ctx.mats.alloy, { x: sx * width * 0.44, y: 0.37, z: 0.28 });
  for (const sx of [-1, 1]) box(g, 0.06, 0.75, 0.06, ctx.mats.alloy, { x: sx * width * 0.44, y: 0.37, z: -0.28 });
  box(g, width * 0.44, 0.32, 0.05, ctx.mats.screen, { x: 0, y: 0.93, z: -0.28, cast: false });
  box(g, width * 0.2, 0.03, 0.18, ctx.mats.dark, { x: -width * 0.2, y: 0.79, z: 0.1 });
  return g;
}

export function bench(ctx, parent, { x = 0, y = 0, z = 0, yaw = 0, length = 2.2 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, length, 0.08, 0.5, ctx.mats.hullWarm, { y: 0.45 });
  for (const sx of [-1, 1]) box(g, 0.08, 0.45, 0.45, ctx.mats.alloy, { x: sx * length * 0.42, y: 0.22 });
  return g;
}

export function planter(ctx, parent, { x = 0, y = 0, z = 0, yaw = 0, width = 1.6, depth = 0.6 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, width, 0.4, depth, ctx.mats.panel, { y: 0.2 });
  box(g, width - 0.12, 0.06, depth - 0.12, ctx.mats.soil, { y: 0.4, cast: false });
  // Leaves: small cones are cheap and read as a crop row at distance.
  const leaves = Math.max(3, Math.round(width / 0.28));
  for (let i = 0; i < leaves; i += 1) {
    const px = -width / 2 + 0.18 + (i * (width - 0.36)) / Math.max(1, leaves - 1);
    mesh(g, new THREE.ConeGeometry(0.11, 0.28, 6), ctx.mats.plant, { x: px, y: 0.56, z: (i % 2 ? 0.1 : -0.1) });
  }
  return g;
}

export function bunk(ctx, parent, { x = 0, y = 0, z = 0, yaw = 0 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  g.rotation.y = yaw;
  parent.add(g);
  box(g, 2, 0.16, 0.9, ctx.mats.alloy, { y: 0.55 });
  box(g, 1.9, 0.18, 0.85, ctx.mats.hullWarm, { y: 0.68 });
  box(g, 0.5, 0.12, 0.7, ctx.mats.suit, { x: -0.6, y: 0.79, cast: false });
  for (const sx of [-1, 1]) box(g, 0.1, 0.55, 0.85, ctx.mats.alloy, { x: sx * 0.92, y: 0.28 });
  return g;
}

/** The interactive console. One per interaction point, with a glowing screen. */
export function consoleStation(ctx, { x = 0, z = 0, yaw = 0, label = '', accent = 0x4fc3e8 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);

  box(g, 0.9, 1.05, 0.55, ctx.mats.panelDark, { y: 0.52 });
  const slope = box(g, 0.86, 0.06, 0.5, ctx.mats.panel, { y: 1.06, z: 0.02 });
  slope.rotation.x = -0.28;
  const screen = box(g, 0.74, 0.5, 0.04, ctx.mats.consoleSoft(accent), { y: 1.28, z: -0.12, cast: false });
  screen.rotation.x = -0.2;
  screen.name = 'console-screen';
  box(g, 0.2, 0.03, 0.12, ctx.mats.screen, { y: 1.1, z: 0.22, cast: false });

  // The screen is the raycast target: the player aims at the display, not the whole box.
  screen.userData.isConsole = true;
  g.userData.label = label;

  ctx.solids.addRotatedBox(ctx.origin.x + x, 0.6, ctx.origin.z + z, 0.95, 1.2, 0.7, yaw, { tag: 'console' });
  return { group: g, screen };
}

/** Ceiling light panel for interiors. */
export function ceilingLight(ctx, parent, { x = 0, y = 2.7, z = 0, w = 1.4, d = 0.6 } = {}) {
  const panel = box(parent, w, 0.06, d, ctx.mats.lamp, { x, y, z, cast: false });
  panel.name = 'ceiling-light';
  return panel;
}

/** A small expedition rover, procedural but shaped like the real thing. */
export function rover(ctx, { x = 0, z = 0, yaw = 0, scale = 1 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  g.rotation.y = yaw;
  ctx.group.add(g);
  const body = box(g, 2.6, 0.55, 1.5, ctx.mats.hull, { y: 0.85 });
  body.name = 'rover-body';
  box(g, 1.1, 0.4, 1.2, ctx.mats.panelDark, { x: -0.3, y: 1.25 });
  box(g, 0.8, 0.06, 1.26, ctx.mats.solar, { x: -0.3, y: 1.47, cast: false });
  box(g, 0.5, 0.35, 0.9, ctx.mats.accent, { x: 1.0, y: 1.15 });
  // Wheels: six, with treads.
  for (const sx of [-0.9, 0, 0.9]) {
    for (const sz of [-1, 1]) {
      const wheel = mesh(g, cylGeo(0.36, 0.36, 0.26, 16), ctx.mats.rubber, { x: sx, y: 0.36, z: sz * 0.78, rx: Math.PI / 2 });
      wheel.name = 'wheel';
      mesh(g, cylGeo(0.14, 0.14, 0.3, 10), ctx.mats.alloy, { x: sx, y: 0.36, z: sz * 0.78, rx: Math.PI / 2 });
    }
  }
  // Mast with camera.
  box(g, 0.06, 1.1, 0.06, ctx.mats.alloy, { x: 0.8, y: 1.6, z: 0.4 });
  box(g, 0.3, 0.2, 0.2, ctx.mats.dark, { x: 0.8, y: 2.15, z: 0.4 });
  g.scale.setScalar(scale);
  ctx.solids.addRotatedBox(ctx.origin.x + x, 0.9 * scale, ctx.origin.z + z, 2.8 * scale, 1.8 * scale, 1.8 * scale, yaw, { tag: 'rover' });
  return g;
}

/** A tall approach beacon, like the ones that mark the landing apron. */
export function beacon(ctx, { x = 0, z = 0 } = {}) {
  const g = new THREE.Group();
  g.position.set(x, 0, z);
  ctx.group.add(g);
  box(g, 0.16, 3.2, 0.16, ctx.mats.panelDark, { y: 1.6 });
  box(g, 0.9, 0.9, 0.06, ctx.mats.panel, { y: 2.6 });
  const lamp = box(g, 0.5, 0.5, 0.5, ctx.mats.lamp, { y: 3.1, cast: false });
  lamp.name = 'beacon-lamp';
  ctx.solids.addBox(ctx.origin.x + x, 1.6, ctx.origin.z + z, 0.4, 3.2, 0.4, { tag: 'beacon' });
  return g;
}

export function mulberry(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
