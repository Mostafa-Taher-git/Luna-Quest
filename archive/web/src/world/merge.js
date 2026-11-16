/**
 * Static geometry merging.
 *
 * The colony is built from thousands of small parts, which is lovely to author and ruinous to
 * draw: one mesh per handrail means one draw call per handrail. After a building (or the whole
 * dressing set) is finished, everything that shares a material is baked into one buffer and
 * the parts are thrown away. Consoles keep their screens separate because the crosshair
 * raycasts against them.
 */
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const KEEP_ATTRIBUTES = ['position', 'normal', 'uv'];

/**
 * Merges every eligible mesh under `root` into one mesh per material.
 * Returns { merged, removed } so the caller can log the saving.
 */
export function mergeStaticMeshes(root, { keep = () => false, castShadowFor = () => true } = {}) {
  root.updateMatrixWorld(true);
  const inverseRoot = new THREE.Matrix4().copy(root.matrixWorld).invert();
  const buckets = new Map();
  const removals = [];
  let skipped = 0;

  root.traverse((object) => {
    if (!object.isMesh || object.isSkinnedMesh || object.isInstancedMesh) return;
    if (Array.isArray(object.material)) return;
    if (!object.geometry?.attributes?.position) return;
    // Morph targets cannot be baked into a single static buffer, and a mesh without a
    // position attribute is not a mesh worth having.
    if (object.geometry.morphAttributes?.position?.length) {
      skipped += 1;
      return;
    }
    if (keep(object)) return;

    // Rebuild the geometry attribute by attribute. glTF models arrive interleaved and with
    // assorted index and attribute types; reading through the accessors is the only way to
    // be sure the baked copy is the same shape as the original.
    const source = object.geometry;
    const geometry = new THREE.BufferGeometry();
    for (const name of KEEP_ATTRIBUTES) {
      const attribute = source.attributes[name];
      if (!attribute) continue;
      const itemSize = attribute.itemSize;
      const values = new Float32Array(attribute.count * itemSize);
      if (typeof attribute.getComponent === 'function') {
        for (let i = 0; i < attribute.count; i += 1) {
          for (let c = 0; c < itemSize; c += 1) values[i * itemSize + c] = attribute.getComponent(i, c);
        }
      } else {
        values.set(attribute.array.subarray(0, values.length));
      }
      const rebuilt = new THREE.BufferAttribute(values, itemSize);
      rebuilt.gpuType = null;
      geometry.setAttribute(name, rebuilt);
    }
    if (source.index) {
      const index = source.index;
      const values = new Uint32Array(index.count);
      for (let i = 0; i < index.count; i += 1) values[i] = index.getX(i);
      geometry.setIndex(new THREE.BufferAttribute(values, 1));
    } else {
      const count = source.attributes.position.count;
      const values = new Uint32Array(count);
      for (let i = 0; i < count; i += 1) values[i] = i;
      geometry.setIndex(new THREE.BufferAttribute(values, 1));
    }
    geometry.applyMatrix4(new THREE.Matrix4().multiplyMatrices(inverseRoot, object.matrixWorld));

    const material = object.material;
    if (!buckets.has(material)) buckets.set(material, []);
    buckets.get(material).push(geometry);
    removals.push(object);
  });

  for (const object of removals) object.removeFromParent();

  let merged = 0;
  for (const [material, geometries] of buckets) {
    if (geometries.length === 0) continue;
    const geometry = geometries.length === 1 ? geometries[0] : mergeGeometries(geometries, false);
    if (!geometry) {
      // If a bucket refuses to merge, put its parts back rather than losing geometry.
      for (const geo of geometries) {
        const mesh = new THREE.Mesh(geo, material);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        root.add(mesh);
      }
      continue;
    }
    if (geometries.length > 1) for (const geo of geometries) geo.dispose();
    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = castShadowFor(material);
    mesh.receiveShadow = true;
    mesh.name = `merged:${material.name || material.type}`;
    mesh.updateMatrix();
    mesh.matrixAutoUpdate = false;
    root.add(mesh);
    merged += 1;
  }

  return { merged, removed: removals.length, skipped };
}
