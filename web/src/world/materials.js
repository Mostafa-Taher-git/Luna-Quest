/**
 * Shared materials and small procedural textures.
 *
 * The colony palette is fixed by the kit models (white hull, grey panels, alloy, solar blue,
 * orange accent). Everything else is generated here at boot on a canvas, so the browser build
 * stays small and every surface can be tuned without re-exporting art.
 */
import * as THREE from 'three';

export const PALETTE = {
  hull: 0xc8d0d8,
  hullWarm: 0xe8e4da,
  panel: 0xb8c0c8,
  panelDark: 0x7c848c,
  alloy: 0x9aa4ad,
  dark: 0x2b3037,
  accent: 0xe8933a,
  accentBlue: 0x4a9ad4,
  solar: 0x1b2a4a,
  glass: 0x9fc4dd,
  regolith: 0x8d8b86,
  mli: 0xc9a227, // gold multi-layer insulation
  hazard: 0xe0b23a,
  interior: 0xd5d9dc,
  floor: 0x8e9298,
  plant: 0x4f8f52,
  lamp: 0xfff0cf,
};

function canvas(size = 512) {
  const c = document.createElement('canvas');
  c.width = size;
  c.height = size;
  return { canvas: c, ctx: c.getContext('2d') };
}

function toTexture(canvas, { repeat = [1, 1], aniso = 4 } = {}) {
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.repeat.set(repeat[0], repeat[1]);
  tex.anisotropy = aniso;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

/**
 * The visor, painted rather than reflected.
 *
 * There is no environment probe in this scene, so a physically mirrored visor renders as a
 * black hole in the middle of a white suit. A painted reflection — deep navy glass, a warm
 * horizon at the bottom, one hard specular streak and a soft sky wash — reads as glass at any
 * angle and costs one 256 px texture.
 */
export function makeVisorGlass(size = 256) {
  const { canvas: c, ctx } = canvas(size);
  const base = ctx.createLinearGradient(0, 0, size * 0.4, size);
  base.addColorStop(0, '#2b3a52');
  base.addColorStop(0.4, '#111a26');
  base.addColorStop(1, '#05080d');
  ctx.fillStyle = base;
  ctx.fillRect(0, 0, size, size);

  // Warm dust light bouncing off the regolith along the lower third.
  const bounce = ctx.createLinearGradient(0, size * 0.5, 0, size);
  bounce.addColorStop(0, 'rgba(168, 132, 88, 0)');
  bounce.addColorStop(1, 'rgba(196, 152, 96, 0.55)');
  ctx.fillStyle = bounce;
  ctx.fillRect(0, 0, size, size);

  // Sky wash in the upper left, and the specular streak that sells the curve.
  const sky = ctx.createRadialGradient(size * 0.3, size * 0.2, size * 0.02, size * 0.3, size * 0.2, size * 0.75);
  sky.addColorStop(0, 'rgba(196, 214, 238, 0.62)');
  sky.addColorStop(0.55, 'rgba(140, 166, 198, 0.2)');
  sky.addColorStop(1, 'rgba(150, 176, 208, 0)');
  ctx.fillStyle = sky;
  ctx.fillRect(0, 0, size, size);

  ctx.save();
  ctx.translate(size * 0.5, size * 0.44);
  ctx.rotate(-0.6);
  const streak = ctx.createLinearGradient(0, -size * 0.14, 0, size * 0.14);
  streak.addColorStop(0, 'rgba(255, 255, 255, 0)');
  streak.addColorStop(0.5, 'rgba(255, 255, 255, 0.85)');
  streak.addColorStop(1, 'rgba(255, 255, 255, 0)');
  ctx.fillStyle = streak;
  ctx.fillRect(-size * 0.46, -size * 0.05, size * 0.46, size * 0.1);
  ctx.restore();

  const texture = new THREE.CanvasTexture(c);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = THREE.ClampToEdgeWrapping;
  texture.wrapT = THREE.ClampToEdgeWrapping;
  return texture;
}

/** A noisy, slightly dirty white fabric — the suit and the soft goods of the colony. */
export function makeFabricTexture(size = 512) {
  const { canvas: c, ctx } = canvas(size);
  ctx.fillStyle = '#dde1e6';
  ctx.fillRect(0, 0, size, size);

  // Quilting: soft diagonal channels like the outside of a pressure garment.
  const step = size / 16;
  ctx.strokeStyle = 'rgba(140, 148, 156, 0.30)';
  ctx.lineWidth = 1.4;
  for (let i = -size; i < size * 2; i += step) {
    ctx.beginPath();
    ctx.moveTo(i, 0);
    ctx.lineTo(i + size, size);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(i, size);
    ctx.lineTo(i + size, 0);
    ctx.stroke();
  }

  // Panel seams and stitching.
  ctx.strokeStyle = 'rgba(120, 128, 136, 0.55)';
  ctx.lineWidth = 2;
  for (const y of [size * 0.28, size * 0.62, size * 0.86]) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(size, y);
    ctx.stroke();
    ctx.setLineDash([5, 6]);
    ctx.strokeStyle = 'rgba(150, 158, 166, 0.5)';
    ctx.beginPath();
    ctx.moveTo(0, y + 6);
    ctx.lineTo(size, y + 6);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.strokeStyle = 'rgba(120, 128, 136, 0.55)';
  }

  // Wear: fine speckle plus a few darker scuffs.
  for (let i = 0; i < 2600; i += 1) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const v = Math.random();
    ctx.fillStyle = v > 0.5 ? 'rgba(255,255,255,0.16)' : 'rgba(96,102,110,0.14)';
    ctx.fillRect(x, y, 1.4, 1.4);
  }
  for (let i = 0; i < 16; i += 1) {
    const x = Math.random() * size;
    const y = Math.random() * size;
    const r = 6 + Math.random() * 26;
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, 'rgba(110, 116, 124, 0.20)');
    g.addColorStop(1, 'rgba(110, 116, 124, 0)');
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }
  return toTexture(c, { repeat: [2, 2], aniso: 8 });
}

/** Hazard chevrons for the airlock sill and the pad edge. */
export function makeHazardTexture(size = 256) {
  const { canvas: c, ctx } = canvas(size);
  ctx.fillStyle = '#20242a';
  ctx.fillRect(0, 0, size, size);
  ctx.fillStyle = '#e0b23a';
  const w = size / 6;
  ctx.save();
  ctx.translate(0, 0);
  for (let i = -size; i < size * 2; i += w * 2) {
    ctx.beginPath();
    ctx.moveTo(i, 0);
    ctx.lineTo(i + w, 0);
    ctx.lineTo(i + w + size, size);
    ctx.lineTo(i + size, size);
    ctx.closePath();
    ctx.fill();
  }
  ctx.restore();
  return toTexture(c, { repeat: [4, 1] });
}

/** Solar cell grid: dark blue cells with fine fingers and a dusty edge. */
export function makeSolarTexture(size = 512) {
  const { canvas: c, ctx } = canvas(size);
  ctx.fillStyle = '#16233c';
  ctx.fillRect(0, 0, size, size);
  const cells = 6;
  const gap = size / cells;
  for (let x = 0; x < cells; x += 1) {
    for (let y = 0; y < cells; y += 1) {
      const g = ctx.createLinearGradient(x * gap, y * gap, (x + 1) * gap, (y + 1) * gap);
      g.addColorStop(0, '#22375c');
      g.addColorStop(0.5, '#1a2a49');
      g.addColorStop(1, '#111b30');
      ctx.fillStyle = g;
      ctx.fillRect(x * gap + 1.5, y * gap + 1.5, gap - 3, gap - 3);
      ctx.strokeStyle = 'rgba(140, 170, 210, 0.22)';
      ctx.lineWidth = 0.8;
      for (let f = 1; f < 6; f += 1) {
        ctx.beginPath();
        ctx.moveTo(x * gap + 2, y * gap + (f * gap) / 6);
        ctx.lineTo((x + 1) * gap - 2, y * gap + (f * gap) / 6);
        ctx.stroke();
      }
    }
  }
  // Dust film, heaviest at the bottom of the array.
  const d = ctx.createLinearGradient(0, 0, 0, size);
  d.addColorStop(0, 'rgba(180, 176, 168, 0.05)');
  d.addColorStop(1, 'rgba(180, 176, 168, 0.30)');
  ctx.fillStyle = d;
  ctx.fillRect(0, 0, size, size);
  return toTexture(c, { repeat: [1, 1] });
}

/** Brushed metal with rivets — the alloy that holds the colony together. */
export function makeMetalTexture(size = 512) {
  const { canvas: c, ctx } = canvas(size);
  const g = ctx.createLinearGradient(0, 0, size, size);
  g.addColorStop(0, '#b6bec6');
  g.addColorStop(0.5, '#a4acb4');
  g.addColorStop(1, '#b9c1c8');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  for (let i = 0; i < 1400; i += 1) {
    ctx.fillStyle = Math.random() > 0.5 ? 'rgba(255,255,255,0.10)' : 'rgba(90,96,102,0.10)';
    ctx.fillRect(Math.random() * size, Math.random() * size, 8 + Math.random() * 30, 1);
  }
  ctx.fillStyle = 'rgba(90, 96, 102, 0.55)';
  for (let x = 16; x < size; x += 64) {
    for (let y = 16; y < size; y += 64) {
      ctx.beginPath();
      ctx.arc(x, y, 1.9, 0, Math.PI * 2);
      ctx.fill();
    }
  }
  return toTexture(c, { repeat: [3, 3] });
}

/** Interior wall panels: quiet, light, with a faint grid so rooms read as built. */
export function makeInteriorTexture(size = 512) {
  const { canvas: c, ctx } = canvas(size);
  ctx.fillStyle = '#dee2e6';
  ctx.fillRect(0, 0, size, size);
  ctx.strokeStyle = 'rgba(150, 158, 166, 0.35)';
  ctx.lineWidth = 1;
  for (let i = 0; i <= size; i += size / 8) {
    ctx.beginPath();
    ctx.moveTo(i, 0);
    ctx.lineTo(i, size);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, i);
    ctx.lineTo(size, i);
    ctx.stroke();
  }
  for (let i = 0; i < 900; i += 1) {
    ctx.fillStyle = 'rgba(120,128,136,0.05)';
    ctx.fillRect(Math.random() * size, Math.random() * size, 2, 2);
  }
  return toTexture(c, { repeat: [2, 2] });
}

export class Materials {
  constructor(renderer) {
    const aniso = Math.min(8, renderer?.capabilities?.getMaxAnisotropy?.() ?? 4);
    this.fabric = makeFabricTexture();
    this.visorGlass = makeVisorGlass();
    this.fabric.anisotropy = aniso;
    this.hazard = makeHazardTexture();
    this.solarCells = makeSolarTexture();
    this.metal = makeMetalTexture();
    this.interior = makeInteriorTexture();

    this.hull = new THREE.MeshStandardMaterial({ color: PALETTE.hull, roughness: 0.7, metalness: 0.05 });
    this.hullWarm = new THREE.MeshStandardMaterial({ color: PALETTE.hullWarm, roughness: 0.66, metalness: 0.05 });
    this.panel = new THREE.MeshStandardMaterial({ color: PALETTE.panel, roughness: 0.55, metalness: 0.22, map: this.metal });
    this.panelDark = new THREE.MeshStandardMaterial({ color: PALETTE.panelDark, roughness: 0.6, metalness: 0.3 });
    this.alloy = new THREE.MeshStandardMaterial({ color: PALETTE.alloy, roughness: 0.38, metalness: 0.72, map: this.metal });
    this.dark = new THREE.MeshStandardMaterial({ color: PALETTE.dark, roughness: 0.75, metalness: 0.25 });
    this.rubber = new THREE.MeshStandardMaterial({ color: 0x1b1e22, roughness: 0.9, metalness: 0.0 });
    this.accent = new THREE.MeshStandardMaterial({ color: PALETTE.accent, roughness: 0.5, metalness: 0.12, emissive: 0x1a0d02, emissiveIntensity: 0.25 });
    this.accentBlue = new THREE.MeshStandardMaterial({ color: PALETTE.accentBlue, roughness: 0.45, metalness: 0.25 });
    this.mli = new THREE.MeshStandardMaterial({
      color: PALETTE.mli,
      roughness: 0.32,
      metalness: 0.85,
      emissive: 0x1a1204,
      emissiveIntensity: 0.4,
    });
    this.hazardStripe = new THREE.MeshStandardMaterial({ map: this.hazard, roughness: 0.7, metalness: 0.1 });
    // Real transmission would force three to render the whole scene into a second target
    // every frame. A tinted, transparent standard material reads the same on the colony's
    // small windows and costs nothing.
    this.glass = new THREE.MeshStandardMaterial({
      color: PALETTE.glass,
      roughness: 0.12,
      metalness: 0.25,
      transparent: true,
      opacity: 0.34,
      envMapIntensity: 0.8,
    });
    this.visor = new THREE.MeshPhysicalMaterial({
      map: this.visorGlass,
      color: 0xffffff,
      roughness: 0.18,
      metalness: 0.2,
      clearcoat: 1,
      clearcoatRoughness: 0.08,
      emissive: 0xffffff,
      emissiveMap: this.visorGlass,
      emissiveIntensity: 0.4,
      envMapIntensity: 0.6,
    });
    this.solar = new THREE.MeshStandardMaterial({
      map: this.solarCells,
      roughness: 0.28,
      metalness: 0.55,
      color: 0x9fb4d0,
    });
    this.regolith = new THREE.MeshStandardMaterial({ color: PALETTE.regolith, roughness: 0.97, metalness: 0.0 });
    this.regolithClay = new THREE.MeshStandardMaterial({ color: 0x9a958c, roughness: 0.98, metalness: 0.0 });
    this.suit = new THREE.MeshStandardMaterial({
      map: this.fabric,
      color: 0xf2f4f7,
      roughness: 0.56,
      metalness: 0.04,
    });
    this.suitVisor = this.visor;
    // Interiors carry a faint self-illumination so a sealed module is readable without
    // burning a point light per room. It reads as the cabin lighting, and it is free.
    this.interiorWall = new THREE.MeshStandardMaterial({
      map: this.interior,
      roughness: 0.82,
      metalness: 0.03,
      emissive: 0x2c3038,
      emissiveIntensity: 0.55,
    });
    this.interiorFloor = new THREE.MeshStandardMaterial({
      color: PALETTE.floor,
      roughness: 0.86,
      metalness: 0.05,
      emissive: 0x1e2126,
      emissiveIntensity: 0.5,
    });
    this.ceiling = new THREE.MeshStandardMaterial({
      color: 0xc3c9cf,
      roughness: 0.9,
      metalness: 0.02,
      emissive: 0x22262c,
      emissiveIntensity: 0.5,
    });
    this.plant = new THREE.MeshStandardMaterial({ color: PALETTE.plant, roughness: 0.85, metalness: 0 });
    this.soil = new THREE.MeshStandardMaterial({ color: 0x3a332c, roughness: 1 });
    this.lamp = new THREE.MeshStandardMaterial({
      color: 0xfff4dd,
      emissive: PALETTE.lamp,
      emissiveIntensity: 2.2,
      roughness: 0.5,
    });
    this.screen = new THREE.MeshStandardMaterial({
      color: 0x0b1a26,
      emissive: 0x4fc3e8,
      emissiveIntensity: 0.85,
      roughness: 0.25,
      metalness: 0.2,
    });
    this.warning = new THREE.MeshStandardMaterial({
      color: 0x2a1408,
      emissive: 0xff7b28,
      emissiveIntensity: 1.6,
      roughness: 0.5,
    });
    this.astronautTrim = new THREE.MeshStandardMaterial({ color: PALETTE.accent, roughness: 0.5, metalness: 0.1 });
  }

  /**
   * Console bodies get a per-station accent colour so the colony reads at a glance. They are
   * cached by colour: 45 consoles with 45 unique materials would each cost a draw call.
   */
  consoleSoft(color) {
    this._consoleCache ??= new Map();
    const key = color >>> 0;
    let material = this._consoleCache.get(key);
    if (!material) {
      material = new THREE.MeshStandardMaterial({
        color: 0x1b2530,
        roughness: 0.5,
        metalness: 0.35,
        emissive: color,
        emissiveIntensity: 0.4,
        name: `console-${key.toString(16)}`,
      });
      this._consoleCache.set(key, material);
    }
    return material;
  }

  dispose() {
    for (const value of Object.values(this)) {
      if (value && value.isMaterial) value.dispose();
      else if (value && value.isTexture) value.dispose();
    }
  }
}
