/**
 * Colony dressing from real GLB assets.
 *
 * - The CC0 "Moon Base Colony and Lunar Rovers" kit (54 modules) provides the industrial
 *   hardware around the walkable buildings: landers, haulers, rovers, skids, tanks, masts.
 * - Seven photo-scanned Moon rocks from Poly Haven break up the horizon.
 *
 * The kit's own PBR materials are remapped onto the colony palette so a lander sitting next
 * to a hand-built habitat does not look like it came from a different game.
 */
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { mulberry } from './parts.js';

export class PropLibrary {
  constructor({ materials, baseUrl = './', onProgress = () => {} }) {
    this.materials = materials;
    this.baseUrl = baseUrl;
    this.loader = new GLTFLoader();
    this.cache = new Map();
    this.onProgress = onProgress;
    this.root = new THREE.Group();
    this.root.name = 'Dressing';
    this.colliders = [];
    this.loaded = 0;
    this.failed = [];
  }

  _url(path) {
    return new URL(`${this.baseUrl}${path}`, location.href).href;
  }

  async load(path) {
    if (this.cache.has(path)) return this.cache.get(path);
    const promise = new Promise((resolve) => {
      this.loader.load(
        this._url(path),
        (gltf) => {
          const scene = gltf.scene;
          scene.traverse((child) => {
            if (!child.isMesh) return;
            child.castShadow = true;
            child.receiveShadow = true;
            child.material = this._remap(child.material);
          });
          this.cache.set(path, scene);
          this.loaded += 1;
          this.onProgress(this.loaded);
          resolve(scene);
        },
        undefined,
        () => {
          this.failed.push(path);
          this.cache.set(path, null);
          resolve(null);
        },
      );
    });
    this.cache.set(path, promise);
    const result = await promise;
    this.cache.set(path, result);
    return result;
  }

  _remap(material) {
    const name = (material?.name ?? '').toLowerCase();
    const mats = this.materials;
    if (name.includes('glass')) return mats.glass;
    if (name.includes('solar')) return mats.solar;
    if (name.includes('regolith') || name.includes('dust') || name.includes('soil')) return mats.regolithClay;
    if (name.includes('accent') || name.includes('orange') || name.includes('stripe')) return mats.accent;
    if (name.includes('dark')) return mats.dark;
    if (name.includes('alloy') || name.includes('metal')) return mats.alloy;
    if (name.includes('panel')) return mats.panel;
    if (name.includes('hull')) return mats.hull;
    return material;
  }

  /** Clones a kit module, places it, and registers one box collider for it. */
  async place(stem, { x = 0, z = 0, yaw = 0, scale = 1, terrain, tag = 'prop', blocks = true } = {}) {
    const source = await this.load(`models/kit/${stem}.glb`);
    if (!source) return null;
    const node = source.clone(true);
    node.position.set(x, heightOf(terrain, x, z), z);
    node.rotation.y = yaw;
    node.scale.setScalar(scale);
    node.name = stem;
    this.root.add(node);

    if (blocks) {
      const box = new THREE.Box3().setFromObject(node);
      const size = box.getSize(new THREE.Vector3());
      const center = box.getCenter(new THREE.Vector3());
      if (size.x > 0.6 && size.z > 0.6 && size.y > 0.4) {
        this.colliders.push({
          minX: center.x - (size.x * 0.94) / 2,
          maxX: center.x + (size.x * 0.94) / 2,
          minY: box.min.y,
          maxY: Math.max(box.max.y, box.min.y + 0.6),
          minZ: center.z - (size.z * 0.94) / 2,
          maxZ: center.z + (size.z * 0.94) / 2,
          tag,
        });
      }
    }
    return node;
  }

  async placeRocks(terrain, { count = 26, seed = 4711 } = {}) {
    const rand = mulberry(seed);
    const models = await Promise.all(
      Array.from({ length: 7 }, (_, i) => this.load(`models/rocks/moon_rock_0${i + 1}/moon_rock_0${i + 1}.gltf`)),
    );
    const available = models.filter(Boolean);
    if (!available.length) return 0;

    let placed = 0;
    for (let i = 0; i < count; i += 1) {
      // Scatter outside the plaza and behind the colony buildings.
      const angle = rand() * Math.PI * 2;
      const distance = 62 + Math.pow(rand(), 0.7) * 190;
      const x = Math.cos(angle) * distance;
      const z = Math.sin(angle) * distance + 8;
      const model = available[Math.floor(rand() * available.length)];
      const rock = model.clone(true);
      const s = 0.5 + Math.pow(rand(), 2.2) * 3.4;
      rock.position.set(x, terrain.height(x, z) - 0.25 * s, z);
      rock.rotation.set(0, rand() * Math.PI * 2, (rand() - 0.5) * 0.16);
      rock.scale.setScalar(s);
      rock.traverse((child) => {
        if (!child.isMesh) return;
        child.castShadow = true;
        child.receiveShadow = true;
        const mats = Array.isArray(child.material) ? child.material : [child.material];
        for (const m of mats) {
          if (!m) continue;
          m.color?.multiplyScalar(1.05 + rand() * 0.3);
          m.roughness = 0.95;
          m.metalness = 0.0;
        }
      });
      this.root.add(rock);
      if (s > 1.6) {
        const box = new THREE.Box3().setFromObject(rock);
        const size = box.getSize(new THREE.Vector3());
        const center = box.getCenter(new THREE.Vector3());
        this.colliders.push({
          minX: center.x - size.x * 0.34,
          maxX: center.x + size.x * 0.34,
          minY: box.min.y,
          maxY: box.min.y + size.y * 0.9,
          minZ: center.z - size.z * 0.34,
          maxZ: center.z + size.z * 0.34,
          tag: 'rock',
        });
      }
      placed += 1;
    }
    return placed;
  }
}

const heightOf = (terrain, x, z) => (terrain ? terrain.height(x, z) : 0);

/**
 * The exterior layout, in metres, yaw in radians. It mirrors WorldBuilder.Kit.cs so the
 * browser and the Godot build describe the same working base.
 */
export const KIT_LAYOUT = [
  // Landing apron
  { stem: 'cargo-lander-94227716', x: 0, z: 52, yaw: 0 },
  { stem: 'approach-light-beacon-20fb9bc6', x: -11, z: 44, yaw: 0 },
  { stem: 'approach-light-beacon-20fb9bc6', x: 9, z: 46, yaw: 0 },
  { stem: 'approach-light-beacon-20fb9bc6', x: -11, z: 62, yaw: 0 },
  { stem: 'approach-light-beacon-20fb9bc6', x: 11, z: 62, yaw: 0 },
  { stem: 'blast-deflector-wall-51ed7365', x: -22, z: 52, yaw: Math.PI / 2 },
  { stem: 'blast-deflector-wall-51ed7365', x: 22, z: 52, yaw: -Math.PI / 2 },
  { stem: 'cargo-hauler-flatbed-76ce4ade', x: -14, z: 36, yaw: 0.35 },
  { stem: 'equipment-sled-408c66bc', x: -10, z: 42, yaw: 0.5 },
  { stem: 'propellant-tank-farm-3dcbfa21', x: -30, z: 50, yaw: Math.PI / 2 },
  { stem: 'cryo-transfer-arm-5d350240', x: -24, z: 40, yaw: 0 },
  { stem: 'cargo-lander-94227716', x: 26, z: 62, yaw: 1.1 },
  // Power yard
  { stem: 'battery-container-9755ed67', x: -66, z: -14, yaw: Math.PI / 2 },
  { stem: 'battery-container-9755ed67', x: -66, z: -20, yaw: Math.PI / 2 },
  { stem: 'radiator-bank-0c1ce93e', x: -52, z: 16, yaw: 0 },
  { stem: 'nuclear-power-unit-8239903e', x: -72, z: -32, yaw: 0 },
  { stem: 'vertical-solar-tower-c58cca7d', x: -68, z: 10, yaw: 0 },
  { stem: 'solar-array-row-tracke-e35a3b96', x: -84, z: -2, yaw: 0.5 },
  // Resource processing
  { stem: 'oxygen-extraction-plan-002fb0ae', x: -46, z: 19, yaw: 0 },
  { stem: 'regolith-excavator-64b4560f', x: -56, z: 22, yaw: 0.6 },
  { stem: 'sintering-print-gantry-64cc192a', x: -40, z: 30, yaw: 0 },
  { stem: 'water-processing-skid-27dcfe03', x: 42, z: 26, yaw: 0 },
  { stem: 'greenhouse-module-b35be615', x: 46, z: -14, yaw: Math.PI / 2 },
  { stem: 'survey-marker-array-99d264a2', x: 58, z: -44, yaw: 0 },
  { stem: 'regolith-hopper-screen-148a2c37', x: 63, z: -24, yaw: -0.5 },
  // Motor pool and relay
  { stem: 'crane-rover-d5a76830', x: -34, z: -30, yaw: 0.45 },
  { stem: 'pressurised-long-range-35466e96', x: -28, z: -46, yaw: 0.55 },
  { stem: 'small-survey-rover-f1fa6584', x: -24, z: -52, yaw: 0 },
  { stem: 'eva-tool-cart-3f077d9d', x: -30, z: -38, yaw: 1.0 },
  { stem: 'comms-relay-mast-4cdfe54f', x: 26, z: -30, yaw: 0 },
  { stem: 'open-rover-unpressuris-0da93181', x: -12, z: 22, yaw: 2.8 },
  { stem: 'open-rover-unpressuris-3741288d', x: 9, z: 30, yaw: 0.4 },
  // Berms and edges
  { stem: 'dust-berm-2dec8cfd', x: 62, z: 12, yaw: Math.PI / 2 },
  { stem: 'dust-berm-2dec8cfd', x: -62, z: 30, yaw: -Math.PI / 2 },
  { stem: 'blast-berm-segment-6e0ba7b3', x: 34, z: 44, yaw: 0 },
  { stem: 'crater-rim-segment-fafd0277', x: -84, z: 44, yaw: 0.8 },
];

export async function dressColony(library, terrain, onProgress = () => {}) {
  let done = 0;
  const total = KIT_LAYOUT.length;
  for (const item of KIT_LAYOUT) {
    await library.place(item.stem, { ...item, terrain, tag: item.stem });
    done += 1;
    onProgress(done / (total + 1));
  }
  const rocks = await library.placeRocks(terrain);
  onProgress(1);
  return { props: done, rocks, failed: library.failed };
}
