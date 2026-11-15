/**
 * The driveable crew rover — a real, rideable LTV.
 *
 * The mesh is the CC0 "Moon Base Colony and Lunar Rovers" open unpressurised rover, the same
 * module the colony dresses its motor pool with. It ships with cleanly separated sub-meshes
 * (`wheel-front-left`, `steer-rear-right`, `hand-controller`, `antenna-dish`, ...), so the
 * wheels really spin, the four corners really steer, and the commander really sits at the
 * hand controller instead of being glued to a box.
 *
 * Physics is lunar: 1.62 m/s², low traction, long float over bumps, gentle speed-sensitive
 * steering and a handbrake that locks the wheels. The rover follows the same deterministic
 * heightfield the commander walks on, so a save never stores terrain and the browser, Godot
 * and Unity agree about where the ground is. The battery is engine-local (it lives in the save
 * extras, not the shared colony core) and recharges on the garage apron.
 *
 * The driver is the one Cute Astronaut, posed by placement and recline — the GLB is frozen and
 * its meshes are not skinned, so the seat pose is a transform, never a reshaped character.
 * See docs/design/Luna-Quest_Immutable_Assets_EN.md.
 */
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const GRAVITY = 1.62;
const MAX_FORWARD = 8.0; // m/s (~29 km/h): a brisk, fun LTV pace
const MAX_REVERSE = 2.2;
const ACCEL = 3.4; // m/s² at the wheels, limited by regolith traction
const BRAKE = 5.0;
const DRAG = 0.55; // rolling + drivetrain drag
const STEER_MAX = 0.52; // rad at the front corners
const WHEEL_RADIUS = 0.44;
const CLEARANCE = 0.42; // chassis ride height above the wheel contact plane
const BATTERY_KWH = 14; // a small LTV pack
const DRAW_KW_PER_MS = 0.9; // kW drawn per m/s of road speed
const CHARGE_KW = 6.0; // garage fast charge
const GARAGE = { x: -16, z: -40, radius: 12 };
const ICE_SITE = { x: 52, z: -34, radius: 12 };

export class CrewRover {
  constructor({ scene, terrain, colliders, materials, baseUrl = './', spec }) {
    this.scene = scene;
    this.terrain = terrain;
    this.colliders = colliders;
    this.materials = materials;
    this.baseUrl = baseUrl;
    this.spec = spec;
    this.root = new THREE.Group();
    this.root.name = 'crew-rover';
    this.position = new THREE.Vector3(spec.x, 0, spec.z);
    this.yaw = spec.yaw ?? 0;
    this.speed = 0;
    this.steer = 0;
    this.spin = 0;
    this.batteryKwh = BATTERY_KWH;
    this.occupied = false;
    this.hasDriven = false;
    this.reachedIce = false;
    this.odometerM = 0;
    this._wheels = [];
    this._steerNodes = [];
    this._seat = new THREE.Vector3(0, 0.34, -0.18);
    this._seatRecline = 0.32;
    this._dust = null;
    this._dustVel = [];
    this._dustLife = [];
    this._tmp = new THREE.Vector3();
    this._loaded = false;
  }

  async load() {
    const url = new URL(`${this.baseUrl}models/kit/${this.spec.stem}.glb`, location.href).href;
    const gltf = await new Promise((resolve) => {
      new GLTFLoader().load(url, resolve, undefined, () => resolve(null));
    });
    if (!gltf) {
      // The rover is dressing-grade hardware; a missing module is reported, not substituted
      // with a box. The game still runs on foot.
      console.warn(`Crew rover module could not be loaded from ${url}; driving disabled.`);
      return false;
    }
    const model = gltf.scene;
    model.traverse((child) => {
      if (!child.isMesh) return;
      child.castShadow = true;
      child.receiveShadow = true;
      child.material = this._remap(child.material);
    });
    this.root.add(model);
    this._rigWheels(model);
    this._buildDust();
    this.scene.add(this.root);
    this._loaded = true;
    this._placeOnGround();
    return true;
  }

  _remap(material) {
    const name = (material?.name ?? '').toLowerCase();
    const m = this.materials;
    if (name.includes('glass')) return m.glass;
    if (name.includes('solar')) return m.solar;
    if (name.includes('accent') || name.includes('orange')) return m.accent;
    if (name.includes('dark')) return m.dark;
    if (name.includes('alloy') || name.includes('metal')) return m.alloy;
    if (name.includes('panel')) return m.panel;
    if (name.includes('hull')) return m.hull;
    return material;
  }

  /** Collect the named wheel / steer sub-meshes so they can be driven independently. */
  _rigWheels(model) {
    const corners = ['front-left', 'front-right', 'rear-left', 'rear-right'];
    for (const corner of corners) {
      const wheels = [];
      const steers = [];
      model.traverse((child) => {
        const n = (child.name ?? '').toLowerCase();
        if (n === `wheel-${corner}`) wheels.push(child);
        else if (n === `steer-${corner}`) steers.push(child);
      });
      const front = corner.startsWith('front');
      for (const w of wheels) {
        w.rotation.order = 'YXZ'; // steer (Y) then spin (X) about the steered axle
        this._wheels.push({ node: w, front });
      }
      for (const s of steers) this._steerNodes.push({ node: s, front });
    }
  }

  _buildDust() {
    const COUNT = 90;
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(COUNT * 3);
    for (let i = 0; i < COUNT; i += 1) {
      pos[i * 3 + 1] = -100; // parked below the world until kicked up
      this._dustVel.push(new THREE.Vector3());
      this._dustLife.push(0);
    }
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const mat = new THREE.PointsMaterial({
      color: 0x9a8f7d, size: 0.22, transparent: true, opacity: 0.5, depthWrite: false,
    });
    this._dust = new THREE.Points(geo, mat);
    this._dust.frustumCulled = false;
    this.scene.add(this._dust);
    this._dustCursor = 0;
  }

  get available() {
    return this._loaded;
  }

  forward(out = new THREE.Vector3()) {
    return out.set(Math.sin(this.yaw), 0, Math.cos(this.yaw)); // model faces +Z
  }

  seatWorld(out = new THREE.Vector3()) {
    out.copy(this._seat).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw).add(this.position);
    return out;
  }

  distanceTo(p) {
    return Math.hypot(p.x - this.position.x, p.z - this.position.z);
  }

  enter(player) {
    if (!this._loaded || this.occupied) return false;
    this.occupied = true;
    this.hasDriven = true;
    player.vehicle = this;
    if (player.astronaut?.root) player.astronaut.root.visible = player.mode === 'third';
    return true;
  }

  exit(player) {
    if (!this.occupied) return false;
    this.occupied = false;
    this.speed = 0;
    player.vehicle = null;
    // Step out beside the rover, onto the ground, never into a wall.
    const side = this._tmp.set(Math.cos(this.yaw), 0, -Math.sin(this.yaw));
    const spot = player.position.copy(this.position).addScaledVector(side, 1.7);
    spot.y = this.terrain ? this.terrain.height(spot.x, spot.z) : 0;
    player.velocity.set(0, 0, 0);
    if (player.astronaut?.root) {
      player.astronaut.root.visible = player.mode === 'third';
      player.astronaut.root.rotation.set(0, player.yaw, 0);
    }
    return true;
  }

  /** Called by Player.update while the commander is at the wheel. */
  update(dt, input, player) {
    if (dt > 0.1) dt = 0.1;
    const throttle = (input.down('KeyW', 'ArrowUp') ? 1 : 0) - (input.down('KeyS', 'ArrowDown') ? 1 : 0);
    const steerIn = (input.down('KeyA', 'ArrowLeft') ? 1 : 0) - (input.down('KeyD', 'ArrowRight') ? 1 : 0);
    const handbrake = input.down('Space');
    const boost = input.down('ShiftLeft', 'ShiftRight');

    const flat = this.batteryKwh <= 0.05;
    const maxF = flat ? 0 : MAX_FORWARD * (boost ? 1.0 : 0.72);

    // Longitudinal dynamics.
    if (throttle > 0 && !flat) this.speed += ACCEL * dt;
    else if (throttle < 0) this.speed -= (this.speed > 0 ? BRAKE : ACCEL * 0.6) * dt;
    else this.speed -= Math.sign(this.speed) * DRAG * dt;
    if (handbrake) this.speed -= Math.sign(this.speed) * BRAKE * 1.4 * dt;
    if (Math.abs(this.speed) < 0.03 && throttle === 0) this.speed = 0;
    this.speed = THREE.MathUtils.clamp(this.speed, -MAX_REVERSE, maxF);

    // Speed-sensitive steering: sharp at a crawl, settled at speed. Rear corners counter-steer
    // a little so the long wheelbase can still turn inside the colony plaza.
    const steerTarget = steerIn * STEER_MAX * (0.55 + 0.45 / (1 + Math.abs(this.speed) * 0.35));
    this.steer += (steerTarget - this.steer) * Math.min(1, dt * 6);
    const turnRate = (this.speed / 2.3) * Math.tan(this.steer);
    this.yaw += turnRate * dt;

    // Move, sweeping the chassis box against the static colony.
    const fwd = this.forward(this._tmp);
    const step = this.speed * dt;
    this._sweep(fwd.x * step, fwd.z * step);
    this.odometerM += Math.abs(step);

    // Follow the heightfield: sample the four contact patches, ride their average, and tilt
    // the chassis to the local slope so the rover leans into crater rims instead of clipping.
    this._placeOnGround();

    // Spin and steer the actual sub-meshes.
    this.spin += (this.speed / WHEEL_RADIUS) * dt;
    for (const w of this._wheels) {
      w.node.rotation.x = this.spin;
      w.node.rotation.y = w.front ? this.steer : -this.steer * 0.4;
    }
    for (const s of this._steerNodes) s.node.rotation.y = s.front ? this.steer : -this.steer * 0.4;

    // Battery: draw with speed, charge on the garage apron.
    const nearGarage = Math.hypot(this.position.x - GARAGE.x, this.position.z - GARAGE.z) < GARAGE.radius;
    if (nearGarage && Math.abs(this.speed) < 0.2) this.batteryKwh = Math.min(BATTERY_KWH, this.batteryKwh + CHARGE_KW * dt / 3600 * 60);
    else this.batteryKwh = Math.max(0, this.batteryKwh - (Math.abs(this.speed) * DRAW_KW_PER_MS) * dt / 3600 * 60);

    if (!this.reachedIce && Math.hypot(this.position.x - ICE_SITE.x, this.position.z - ICE_SITE.z) < ICE_SITE.radius) {
      this.reachedIce = true;
    }

    this._kickDust(dt);
    this._carryPlayer(player);
    this._updateCamera(player);
  }

  _sweep(dx, dz) {
    const half = 1.35;
    const tryAxis = (axis, delta) => {
      if (delta === 0) return;
      this.position[axis] += delta;
      const minX = this.position.x - half, maxX = this.position.x + half;
      const minZ = this.position.z - half, maxZ = this.position.z + half;
      const feet = this.position.y, head = feet + 1.8;
      for (const c of this.colliders) {
        if (c.tag === 'rover') continue;
        if (c.maxY <= feet + 0.35 || c.minY >= head) continue;
        if (maxX <= c.minX || minX >= c.maxX || maxZ <= c.minZ || minZ >= c.maxZ) continue;
        if (axis === 'x') this.position.x = delta > 0 ? c.minX - half - 0.01 : c.maxX + half + 0.01;
        else this.position.z = delta > 0 ? c.minZ - half - 0.01 : c.maxZ + half + 0.01;
        this.speed *= 0.25; // bump: bleed most of the speed
        break;
      }
    };
    tryAxis('x', dx);
    tryAxis('z', dz);
  }

  _placeOnGround() {
    const h = (x, z) => (this.terrain ? this.terrain.height(x, z) : 0);
    const f = this.forward(this._tmp);
    const rx = Math.cos(this.yaw), rz = -Math.sin(this.yaw);
    const L = 1.16, W = 0.96;
    const fl = h(this.position.x + f.x * L + rx * W, this.position.z + f.z * L + rz * W);
    const fr = h(this.position.x + f.x * L - rx * W, this.position.z + f.z * L - rz * W);
    const rl = h(this.position.x - f.x * L + rx * W, this.position.z - f.z * L + rz * W);
    const rr = h(this.position.x - f.x * L - rx * W, this.position.z - f.z * L - rz * W);
    const avg = (fl + fr + rl + rr) / 4;
    this.position.y += (avg - this.position.y) * 0.5; // soft suspension
    const pitch = Math.atan2((rl + rr) / 2 - (fl + fr) / 2, 2 * L);
    const roll = Math.atan2((fr + rr) / 2 - (fl + rl) / 2, 2 * W);
    this.root.position.copy(this.position);
    this.root.rotation.set(pitch * 0.6, this.yaw, roll * 0.6, 'YXZ');
  }

  _carryPlayer(player) {
    const seat = this.seatWorld(this._tmp);
    player.position.copy(seat);
    player.velocity.set(0, 0, 0);
    player.grounded = true;
    if (player.astronaut?.root) {
      const root = player.astronaut.root;
      root.position.copy(seat);
      root.rotation.set(-this._seatRecline, this.yaw, 0, 'YXZ');
      root.visible = player.mode === 'third';
    }
  }

  _updateCamera(player) {
    const cam = player.camera;
    const fwd = this.forward(this._tmp);
    if (player.mode === 'first') {
      const eye = this.seatWorld(new THREE.Vector3()).add(new THREE.Vector3(0, 1.18, 0));
      cam.position.copy(eye);
      const dir = new THREE.Vector3(
        Math.sin(player.yaw) * Math.cos(player.pitch),
        Math.sin(player.pitch),
        Math.cos(player.yaw) * Math.cos(player.pitch),
      );
      cam.lookAt(eye.clone().add(dir));
      cam.fov = 74;
    } else {
      const pivot = this.position.clone().add(new THREE.Vector3(0, 1.5, 0));
      const dist = 7.5;
      const dir = new THREE.Vector3(
        Math.sin(player.yaw) * Math.cos(player.pitch),
        -Math.sin(player.pitch) + 0.28,
        Math.cos(player.yaw) * Math.cos(player.pitch),
      ).normalize();
      cam.position.copy(pivot).addScaledVector(dir, dist);
      const groundY = this.terrain ? this.terrain.height(cam.position.x, cam.position.z) : 0;
      if (cam.position.y < groundY + 0.6) cam.position.y = groundY + 0.6;
      cam.lookAt(pivot);
      cam.fov = 62;
    }
    cam.updateProjectionMatrix();
  }

  _kickDust(dt) {
    if (!this._dust) return;
    const pos = this._dust.geometry.attributes.position;
    const moving = Math.abs(this.speed) > 0.6;
    if (moving) {
      const f = this.forward(this._tmp);
      for (let k = 0; k < 2; k += 1) {
        const i = this._dustCursor;
        this._dustCursor = (this._dustCursor + 1) % this._dustLife.length;
        const rear = this.position.clone().addScaledVector(f, -1.3);
        rear.x += (Math.random() - 0.5) * 1.6;
        rear.z += (Math.random() - 0.5) * 1.6;
        rear.y = (this.terrain ? this.terrain.height(rear.x, rear.z) : 0) + 0.1;
        pos.setXYZ(i, rear.x, rear.y, rear.z);
        // Lunar dust: no air, so it flies in clean ballistic arcs and falls fast.
        this._dustVel[i].set(-f.x * this.speed * 0.3 + (Math.random() - 0.5), 1.2 + Math.random() * 1.4, -f.z * this.speed * 0.3 + (Math.random() - 0.5));
        this._dustLife[i] = 1.4;
      }
    }
    for (let i = 0; i < this._dustLife.length; i += 1) {
      if (this._dustLife[i] <= 0) continue;
      this._dustLife[i] -= dt;
      const v = this._dustVel[i];
      v.y -= GRAVITY * dt;
      pos.setXYZ(i, pos.getX(i) + v.x * dt, pos.getY(i) + v.y * dt, pos.getZ(i) + v.z * dt);
      if (this._dustLife[i] <= 0) pos.setY(i, -100);
    }
    pos.needsUpdate = true;
  }

  speedKmh() {
    return Math.abs(this.speed) * 3.6;
  }

  batteryPercent() {
    return this.batteryKwh / BATTERY_KWH;
  }

  serialize() {
    return {
      position: [r(this.position.x), r(this.position.y), r(this.position.z)],
      yaw: r(this.yaw),
      battery: r(this.batteryKwh),
      odometer: r(this.odometerM),
      hasDriven: this.hasDriven,
      reachedIce: this.reachedIce,
      occupied: this.occupied,
    };
  }

  restore(data, player) {
    if (!data) return;
    if (Array.isArray(data.position) && data.position.length === 3) {
      this.position.set(Number(data.position[0]) || this.spec.x, Number(data.position[1]) || 0, Number(data.position[2]) || this.spec.z);
    }
    if (Number.isFinite(data.yaw)) this.yaw = data.yaw;
    if (Number.isFinite(data.battery)) this.batteryKwh = THREE.MathUtils.clamp(data.battery, 0, BATTERY_KWH);
    if (Number.isFinite(data.odometer)) this.odometerM = data.odometer;
    this.hasDriven = Boolean(data.hasDriven);
    this.reachedIce = Boolean(data.reachedIce);
    this._placeOnGround();
    if (data.occupied && player) this.enter(player);
  }
}

const r = (v) => Math.round(v * 1000) / 1000;
