/**
 * The crew's suit: the Cute Astronaut GLB, dressed for the colony.
 *
 * The model is two meshes — a white pressure garment and a dark visor dome — with an oversized
 * helmet, stubby limbs and one baked 10 s clip. It also ships a full skeleton that drives
 * *nothing*: the meshes are not skinned, so the bones are scenery. Everything here is therefore
 * fitted against the meshes themselves, in metres, and hangs off the astronaut's root so it
 * cannot drift away from the body.
 *
 * Dressing: the fabric map, a dark mirror visor, an orange visor frame, a chest control unit,
 * a life-support pack, mission patches and a helmet lamp. The trim colour is what tells the six
 * crew and the commander apart at twenty metres.
 */
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { clone as cloneSkinned } from 'three/addons/utils/SkeletonUtils.js';

const SUIT_MESH = 'Object_9';
const VISOR_MESH = 'Object_10';

export class AstronautFactory {
  constructor({ materials, baseUrl = './' }) {
    this.materials = materials;
    this.baseUrl = baseUrl;
    this.loader = new GLTFLoader();
    this.template = null;
    this.animations = [];
    this.failed = false;
  }

  async load() {
    if (this.template || this.failed) return this.template;
    const url = new URL(`${this.baseUrl}models/astronaut/cute_astronaut.glb`, location.href).href;
    const gltf = await new Promise((resolve) => {
      this.loader.load(
        url,
        resolve,
        undefined,
        () => {
          this.failed = true;
          resolve(null);
        },
      );
    });
    if (!gltf) return null;
    this.template = gltf.scene;
    this.animations = gltf.animations ?? [];
    this.template.traverse((child) => {
      if (!child.isMesh) return;
      child.castShadow = true;
      child.receiveShadow = true;
      // The GLB's own visor is stray geometry: its node sits three metres behind and below the
      // suit, so it renders as nothing at all and leaves the helmet with a black hole for a face.
      // It is hidden, and `create` builds a proper glass dome into the aperture instead.
      const glass = child.name === VISOR_MESH;
      if (glass) child.visible = false;
      child.material = glass ? this.materials.visor : this.materials.suit;
      // The garment is authored with reversed winding: its glTF material is double-sided for a
      // reason. Forcing FrontSide here made every crew member render its own interior — dark
      // blue and faceted — which is exactly what it looked like before this line was corrected.
      child.material.side = THREE.DoubleSide;
    });
    return this.template;
  }

  /**
   * Builds one astronaut, standing `height` metres tall with the soles on y = 0.
   * `trim` recolours the frame, patches and chest keys so each crew member is recognisable.
   */
  async create({ height = 1.78, trim = 0xe08a2a, role = 'crew' } = {}) {
    const template = await this.load();
    const root = new THREE.Group();
    root.name = `astronaut-${role}`;

    if (!template) {
      root.add(fallbackSuit(this.materials));
      return { root, mixer: null, play: () => {}, actions: {} };
    }

    // A skinned mesh must be cloned with SkeletonUtils, or the clones share bones. Here it also
    // keeps the bone scenery from being shared, which matters if anything is ever parented to it.
    const model = cloneSkinned(template);

    // Fit by the rendered meshes. `Box3.setFromObject` is trustworthy for these two: neither is
    // skinned, so the box is the silhouette you actually see.
    model.updateMatrixWorld(true);
    const raw = new THREE.Box3().setFromObject(model);
    const rawHeight = Math.max(0.2, raw.max.y - raw.min.y);
    model.scale.multiplyScalar(height / rawHeight);
    model.updateMatrixWorld(true);
    let box = new THREE.Box3().setFromObject(model);
    model.position.y -= box.min.y;
    // Centre the *body* over the root. The GLB's origin is not under its own astronaut: it is
    // under the stray geometry, so a bone centroid is the only honest centreline here.
    const centre = centreFromRig(model);
    if (centre) {
      model.position.x -= centre.x;
      model.position.z -= centre.z;
    }
    model.updateMatrixWorld(true);
    box = new THREE.Box3().setFromObject(model);
    root.add(model);

    const h = Math.max(0.6, box.max.y - box.min.y);
    // The model stands on its own origin, so the centreline is x = 0, z = 0. Its bounding box is
    // not usable for that (see the visor note below), and a torso is about 0.15 of body height
    // deep on a suit this chunky.
    const midX = 0;
    const midZ = 0;
    const halfDepth = h * 0.15;

    // Which way does it face? The rig answers that even though it drives nothing: the toe pivot
    // of a foot is always in front of the heel pivot.
    const front = frontFromRig(model) ?? 1;
    // Proportions, not measurements. The GLB carries a stray copy of its own visor several
    // metres behind the suit, so any bounding box over its vertices includes junk and lands in
    // the wrong place. The Cute Astronaut's helmet is a fixed fraction of its own height — this
    // suit is all helmet — so the fittings are placed by ratio.
    const head = {
      centre: new THREE.Vector3(midX, box.min.y + h * 0.75, midZ + front * h * 0.01),
      radius: h * 0.245,
    };

    const trimMat = new THREE.MeshStandardMaterial({ color: trim, roughness: 0.42, metalness: 0.2 });
    const packMat = new THREE.MeshStandardMaterial({ color: 0xe4e7ea, roughness: 0.6, metalness: 0.06 });
    const darkMat = this.materials.dark;
    const fittings = new THREE.Group();
    fittings.name = 'fittings';
    root.add(fittings);

    // Chest control unit: three keys and a small screen, sized to the torso rather than to a
    // human adult, because this suit is a good head and a half shorter in the body.
    const chestY = box.min.y + h * 0.47;
    const chest = new THREE.Group();
    chest.position.set(midX, chestY, midZ + front * halfDepth * 0.52);
    const body = new THREE.Mesh(new THREE.BoxGeometry(h * 0.1, h * 0.062, h * 0.024), packMat);
    body.castShadow = true;
    chest.add(body);
    for (let i = 0; i < 3; i += 1) {
      const key = new THREE.Mesh(new THREE.BoxGeometry(h * 0.018, h * 0.01, h * 0.008), trimMat);
      key.position.set((i - 1) * h * 0.028, h * 0.012, front * h * 0.014);
      chest.add(key);
    }
    const screen = new THREE.Mesh(new THREE.BoxGeometry(h * 0.06, h * 0.018, h * 0.006), this.materials.screen);
    screen.position.set(0, -h * 0.012, front * h * 0.014);
    chest.add(screen);
    fittings.add(chest);

    // Life-support pack on the back, with a trim-coloured lid so it reads from behind.
    const pack = new THREE.Mesh(new THREE.BoxGeometry(h * 0.13, h * 0.15, h * 0.05), packMat);
    pack.castShadow = true;
    pack.position.set(midX, box.min.y + h * 0.5, midZ - front * halfDepth * 0.62);
    fittings.add(pack);
    const lid = new THREE.Mesh(new THREE.BoxGeometry(h * 0.13, h * 0.012, h * 0.05), trimMat);
    lid.position.set(pack.position.x, pack.position.y + h * 0.075, pack.position.z);
    fittings.add(lid);

    // Glass dome in the helmet aperture: the suit shell is open at the face, so this is what
    // closes it. Slightly inside the shell so the shell still reads as a helmet.
    const glass = new THREE.Mesh(new THREE.SphereGeometry(head.radius * 0.66, 26, 18), this.materials.visor);
    glass.position.copy(head.centre).setZ(head.centre.z + front * head.radius * 0.42);
    glass.scale.set(1, 0.96, 0.9);
    fittings.add(glass);

    // Visor frame: a ring around the aperture, and the one thing that survives at long range.
    const frame = new THREE.Mesh(new THREE.TorusGeometry(head.radius * 0.58, Math.max(0.005, h * 0.006), 8, 30), trimMat);
    frame.position.copy(head.centre).setZ(head.centre.z + front * head.radius * 0.82);
    frame.rotation.y = front > 0 ? 0 : Math.PI;
    frame.castShadow = true;
    fittings.add(frame);

    // Helmet lamp on the crew member's right.
    const lampBody = new THREE.Mesh(new THREE.BoxGeometry(h * 0.025, h * 0.022, h * 0.045), darkMat);
    lampBody.position.set(head.centre.x + head.radius * 0.8, head.centre.y + head.radius * 0.42, head.centre.z + front * head.radius * 0.3);
    fittings.add(lampBody);
    const lamp = new THREE.Mesh(new THREE.CircleGeometry(h * 0.01, 12), this.materials.lamp);
    lamp.position.set(lampBody.position.x, lampBody.position.y, lampBody.position.z + front * h * 0.024);
    lamp.rotation.y = front > 0 ? 0 : Math.PI;
    fittings.add(lamp);

    // Mission patch on each shoulder: a small trim disc where the arms meet the torso.
    for (const side of [-1, 1]) {
      const patch = new THREE.Mesh(new THREE.CylinderGeometry(h * 0.016, h * 0.016, h * 0.004, 14), trimMat);
      patch.rotation.z = Math.PI / 2;
      patch.rotation.y = Math.PI / 2;
      patch.position.set(midX + side * h * 0.1, box.min.y + h * 0.58, midZ + front * halfDepth * 0.1);
      fittings.add(patch);
    }

    // The clip animates bones that drive no skinned mesh, so it is not worth a mixer per crew
    // member. `play` stays on the object as a no-op so callers do not need to care.
    return {
      root,
      model,
      mixer: null,
      actions: {},
      play: () => {},
      stopAll: () => {},
    };
  }
}

/** Horizontal centreline of the figure, averaged over its own bones. */
function centreFromRig(model) {
  const point = new THREE.Vector3();
  const sum = new THREE.Vector3();
  let count = 0;
  model.traverse((child) => {
    if (!child.isBone) return;
    sum.add(point.setFromMatrixPosition(child.matrixWorld));
    count += 1;
  });
  if (count < 4) return null;
  sum.divideScalar(count);
  return sum;
}

/** The toe pivot of either foot, relative to the heel: the model's forward axis. */
function frontFromRig(model) {
  const bones = [];
  model.traverse((child) => {
    if (child.isBone) bones.push(child);
  });
  if (bones.length === 0) return null;
  const toe = bones.find((b) => /^pivotzeh/i.test(b.name));
  const heel = bones.find((b) => /^pivotferse/i.test(b.name));
  if (toe && heel) {
    const delta = toe.position.z - heel.position.z;
    if (Math.abs(delta) > 1e-4) return Math.sign(delta);
  }
  return null;
}

/** A blocky emergency suit, used only if the GLB cannot be fetched. */
function fallbackSuit(mats) {
  const g = new THREE.Group();
  const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.28, 0.7, 8, 16), mats.suit);
  body.position.y = 1.0;
  body.castShadow = true;
  g.add(body);
  const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.22, 20, 16), mats.suit);
  helmet.position.y = 1.62;
  helmet.castShadow = true;
  g.add(helmet);
  const visor = new THREE.Mesh(new THREE.SphereGeometry(0.19, 20, 16, 0, Math.PI, 0, Math.PI * 0.6), mats.visor);
  visor.position.set(0, 1.63, 0.06);
  visor.rotation.y = Math.PI;
  g.add(visor);
  for (const side of [-1, 1]) {
    const leg = new THREE.Mesh(new THREE.CapsuleGeometry(0.11, 0.4, 4, 10), mats.suit);
    leg.position.set(side * 0.12, 0.35, 0);
    leg.castShadow = true;
    g.add(leg);
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.09, 0.34, 4, 10), mats.suit);
    arm.position.set(side * 0.34, 1.05, 0);
    arm.rotation.z = side * 0.22;
    arm.castShadow = true;
    g.add(arm);
  }
  return g;
}
