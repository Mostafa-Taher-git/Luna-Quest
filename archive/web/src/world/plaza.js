/**
 * The plaza: the paved spine of the colony.
 *
 * A real lunar base does not have astronauts wandering loose regolith between modules — the
 * ground is graded, sintered and marked, and the walk between the airlock and the habitat is a
 * road. This lays that road down, with bollard lamps, painted edges and a dust apron, which
 * also gives the player an unmistakable axis from the spawn to the airlock.
 */
import * as THREE from 'three';
import { mulberry } from './parts.js';

export function createPlaza(materials, terrain) {
  const group = new THREE.Group();
  group.name = 'Plaza';

  const paving = new THREE.MeshStandardMaterial({
    color: 0x8d8b85,
    roughness: 0.92,
    metalness: 0.04,
  });
  const paint = new THREE.MeshStandardMaterial({ color: 0xe6e2d6, roughness: 0.8, metalness: 0.02 });
  const hazard = materials.hazardStripe;

  const addSlab = (cx, cz, width, depth, material, y = 0.16) => {
    const slab = new THREE.Mesh(new THREE.BoxGeometry(width, 0.14, depth), material);
    slab.position.set(cx, y, cz);
    slab.receiveShadow = true;
    group.add(slab);
    return slab;
  };

  // Main spine: airlock to the north apron, in front of the habitat.
  addSlab(0, 21, 7.2, 20, paving);
  // Cross road along the front of the habitat.
  addSlab(0, 10.5, 52, 5.4, paving);
  // Spurs to the workshop, the lab, the greenhouse and life support.
  addSlab(-24, 12.6, 20, 3.6, paving);
  addSlab(24, 12.6, 20, 3.6, paving);
  addSlab(0, 34, 5.4, 8, paving);

  // Painted edges: thin white lines with hazard chevrons at the crossings.
  for (const x of [-3.5, 3.5]) {
    const line = addSlab(x, 21, 0.28, 19.4, paint, 0.24);
    line.castShadow = false;
  }
  for (const z of [13.0, 8.0]) {
    const line = addSlab(0, z, 6.8, 0.26, paint, 0.24);
    line.castShadow = false;
  }
  for (const x of [-4.4, 4.4]) {
    addSlab(x, 31.6, 1.1, 3.2, hazard, 0.2).castShadow = false;
  }

  // Bollard lamps along both sides of the spine.
  const lampMat = materials.lamp;
  const postMat = materials.panelDark;
  for (let z = 13.6; z <= 33; z += 4.2) {
    for (const x of [-4.6, 4.6]) {
      const post = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.11, 1.05, 10), postMat);
      post.position.set(x, 0.52, z);
      post.castShadow = true;
      group.add(post);
      const head = new THREE.Mesh(new THREE.BoxGeometry(0.26, 0.2, 0.26), lampMat);
      head.position.set(x, 1.12, z);
      group.add(head);
      const cap = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.06, 0.34), postMat);
      cap.position.set(x, 1.24, z);
      group.add(cap);
    }
  }

  // Regolith apron: a graded dust skirt so the paving does not float on a flat plane.
  const apron = new THREE.Mesh(new THREE.RingGeometry(0.1, 1, 32), materials.regolithClay);
  apron.visible = false;
  group.add(apron);

  // Tracks: two ruts where the rovers run, plus scattered stones pressed into the surface.
  const rutMat = new THREE.MeshStandardMaterial({ color: 0x6b6862, roughness: 1 });
  for (const x of [-1.5, 1.5]) {
    const rut = addSlab(x, 26, 0.5, 16, rutMat, 0.235);
    rut.castShadow = false;
  }
  const rand = mulberry(9182);
  const stoneGeo = new THREE.DodecahedronGeometry(0.12, 0);
  const stoneMat = new THREE.MeshStandardMaterial({ color: 0x8f8a80, roughness: 0.98 });
  const stones = new THREE.InstancedMesh(stoneGeo, stoneMat, 60);
  const matrix = new THREE.Matrix4();
  const quat = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  const pos = new THREE.Vector3();
  for (let i = 0; i < 60; i += 1) {
    const x = (rand() - 0.5) * 34;
    const z = 6 + rand() * 30;
    pos.set(x, 0.05 + rand() * 0.04, z);
    quat.setFromEuler(new THREE.Euler(rand() * 3, rand() * 3, rand() * 3));
    const s = 0.4 + rand() * 1.1;
    scale.set(s, s * 0.7, s);
    matrix.compose(pos, quat, scale);
    stones.setMatrixAt(i, matrix);
  }
  stones.castShadow = false;
  stones.receiveShadow = true;
  group.add(stones);

  return group;
}
