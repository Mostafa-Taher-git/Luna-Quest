/**
 * The lunar surface — an exact port of shared/LunaQuest.Core/Terrain/LunarTerrain.cs.
 *
 * The height of any point depends only on its (x, z) metres and the site seed, so the
 * browser, Godot and Unity draw the same ground, a save never stores terrain, and the
 * surface can stream forever in any direction. The model is low-frequency highland relief,
 * three scales of simple impact craters (bowl, raised rim, ejecta) and fine regolith
 * roughness, flattened under the colony so every building keeps its designed foundation.
 */

const SMOOTH = (t) => {
  if (t <= 0) return 0;
  if (t >= 1) return 1;
  return t * t * (3 - 2 * t);
};

export class LunarTerrain {
  constructor(seed = 1337, { flatCenterX = 0, flatCenterZ = 8, flatRadius = 88, fadeWidth = 90, ruggedness = 1 } = {}) {
    this.seed = seed | 0;
    this.flatCenterX = flatCenterX;
    this.flatCenterZ = flatCenterZ;
    this.flatRadius = flatRadius;
    this.fadeWidth = Math.max(1, fadeWidth);
    this.ruggedness = ruggedness;
  }

  /**
   * Height above the datum plane in metres.
   *
   * `detail` is a level-of-detail switch used only for the distant mesh: sampling 5 m craters
   * on a 20 m grid aliases into visible ridges, so the far band drops the finest crater layer
   * and the roughest noise octave. Physics and the player always use the full surface.
   */
  height(x, z, detail = 2) {
    const dx = x - this.flatCenterX;
    const dz = z - this.flatCenterZ;
    const dist = Math.sqrt(dx * dx + dz * dz);
    const blend = SMOOTH((dist - this.flatRadius) / this.fadeWidth);
    if (blend <= 0) return 0;
    const fine = detail >= 2 ? this._relief(x, z) + this._craters(x, z) : this._reliefCoarse(x, z) + this._cratersCoarse(x, z);
    return fine * blend * this.ruggedness;
  }

  _reliefCoarse(x, z) {
    let h = 0;
    h += (this._fbm(x / 420, z / 420, 11, 4) - 0.5) * 2 * 16;
    h += (this._fbm(x / 70, z / 70, 23, 3) - 0.5) * 2 * 1.4;
    return h;
  }

  _cratersCoarse(x, z) {
    return this._craterLayer(x, z, 520, 70, 210, 0.55, 101) + this._craterLayer(x, z, 150, 14, 52, 0.75, 202);
  }

  /** Slope angle in degrees, from central differences over 2 m. */
  slopeDegrees(x, z) {
    const d = 1;
    const gx = (this.height(x + d, z) - this.height(x - d, z)) / (2 * d);
    const gz = (this.height(x, z + d) - this.height(x, z - d)) / (2 * d);
    return (Math.atan(Math.sqrt(gx * gx + gz * gz)) * 180) / Math.PI;
  }

  /** Approximate surface normal, used by the dust shader and prop alignment. */
  normal(x, z, out = { x: 0, y: 1, z: 0 }) {
    const d = 1.2;
    const gx = (this.height(x + d, z) - this.height(x - d, z)) / (2 * d);
    const gz = (this.height(x, z + d) - this.height(x, z - d)) / (2 * d);
    const len = Math.hypot(gx, 1, gz);
    out.x = -gx / len;
    out.y = 1 / len;
    out.z = -gz / len;
    return out;
  }

  _relief(x, z) {
    let h = 0;
    h += (this._fbm(x / 420, z / 420, 11, 4) - 0.5) * 2 * 16;
    h += (this._fbm(x / 70, z / 70, 23, 3) - 0.5) * 2 * 1.4;
    h += (this._fbm(x / 9, z / 9, 37, 2) - 0.5) * 2 * 0.12;
    return h;
  }

  _craters(x, z) {
    return (
      this._craterLayer(x, z, 520, 70, 210, 0.55, 101) +
      this._craterLayer(x, z, 150, 14, 52, 0.75, 202) +
      this._craterLayer(x, z, 42, 2.5, 13, 0.8, 303)
    );
  }

  _craterLayer(x, z, cell, rMin, rMax, presence, salt) {
    const cx = Math.floor(x / cell);
    const cz = Math.floor(z / cell);
    let sum = 0;
    for (let i = -1; i <= 1; i += 1) {
      for (let j = -1; j <= 1; j += 1) {
        const gx = cx + i;
        const gz = cz + j;
        const hsh = hash(gx, gz, this.seed + salt);
        if (unit(hsh) > presence) continue;
        const r = rMin + (rMax - rMin) * Math.pow(unit(next(hsh, 1)), 2.2);
        const px = (gx + 0.15 + 0.7 * unit(next(hsh, 2))) * cell;
        const pz = (gz + 0.15 + 0.7 * unit(next(hsh, 3))) * cell;
        const dx = x - px;
        const dz = z - pz;
        const t = Math.sqrt(dx * dx + dz * dz) / r;
        if (t > 2.6) continue;
        const depth = r * 0.19 * (0.55 + 0.45 * unit(next(hsh, 4)));
        const rim = depth * 0.22;
        const bowl = t < 1 ? depth * (t * t - 1) : 0;
        const ring = rim * Math.exp(-Math.pow((t - 1) / 0.3, 2));
        const ejecta = t > 1 ? rim * 0.35 * Math.exp(-(t - 1) * 2.4) : 0;
        sum += bowl + ring + ejecta;
      }
    }
    return sum;
  }

  _fbm(x, z, salt, octaves) {
    let sum = 0;
    let amp = 0.5;
    let norm = 0;
    let px = x;
    let pz = z;
    for (let o = 0; o < octaves; o += 1) {
      sum += amp * this._valueNoise(px, pz, salt + o * 17);
      norm += amp;
      px *= 2.03;
      pz *= 2.03;
      amp *= 0.5;
    }
    return sum / norm;
  }

  _valueNoise(x, z, salt) {
    const xi = Math.floor(x);
    const zi = Math.floor(z);
    const fx = x - xi;
    const fz = z - zi;
    const u = fx * fx * (3 - 2 * fx);
    const v = fz * fz * (3 - 2 * fz);
    const a = unit(hash(xi, zi, this.seed + salt));
    const b = unit(hash(xi + 1, zi, this.seed + salt));
    const c = unit(hash(xi, zi + 1, this.seed + salt));
    const d = unit(hash(xi + 1, zi + 1, this.seed + salt));
    const top = a + (b - a) * u;
    const bottom = c + (d - c) * u;
    return top + (bottom - top) * v;
  }
}

function hash(x, z, salt) {
  let h = (salt * 374761393 + x * 668265263 + z * 2246822519) >>> 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
  h ^= h >>> 16;
  return h >>> 0;
}

function next(h, n) {
  let v = (h + n * 0x9e3779b9) >>> 0;
  v = Math.imul(v ^ (v >>> 15), 2246822519) >>> 0;
  v = Math.imul(v ^ (v >>> 13), 3266489917) >>> 0;
  return (v ^ (v >>> 16)) >>> 0;
}

const unit = (h) => (h & 0xffffff) / 0x1000000;

/** The site the colony was built on: Shackleton rim, 89.9° S. */
export const SITE_SEED = 1337;
