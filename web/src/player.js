/**
 * The commander's body and cameras.
 *
 * Lunar gravity is 1.62 m/s², so a jump leaves the ground for over a second and a hop covers
 * several metres — the movement has to be tuned for that or it feels like a bug. Ground
 * acceleration, air control, a coyote window and a jump buffer keep it responsive, and every
 * solid in the world is an axis-aligned box the capsule is swept against.
 */
import * as THREE from 'three';
import { KEYS } from './input.js';
import { BODY_RADIUS_M, CREW_HEIGHT_M, EYE_HEIGHT_M } from './world/crew-metrics.js';

const GRAVITY = 1.62; // m/s², real lunar surface gravity
const WALK_SPEED = 3.1;
const RUN_SPEED = 5.4;
const GROUND_ACCEL = 24;
const AIR_ACCEL = 7.5;
const JUMP_SPEED = 2.95; // ≈ 2.7 m apex under lunar gravity
const COYOTE = 0.14;
const JUMP_BUFFER = 0.16;
const STEP_HEIGHT = 0.42;
const RADIUS = BODY_RADIUS_M;
const HEIGHT = CREW_HEIGHT_M;
const EYE = EYE_HEIGHT_M;

export class Player {
  constructor({ camera, scene, terrain, colliders, spawn = new THREE.Vector3(0, 0, 23), astronaut = null }) {
    this.camera = camera;
    this.scene = scene;
    this.terrain = terrain;
    this.colliders = colliders;
    this.position = spawn.clone();
    this.velocity = new THREE.Vector3();
    this.yaw = 0; // facing the colony: the hatch and the plaza are to the north (-Z)
    this.pitch = -0.05;
    this.grounded = true;
    this.coyote = 0;
    this.jumpBuffer = 0;
    this.mode = 'third';
    this.bob = 0;
    this.headBob = 0;
    this.astronaut = astronaut;
    this.root = astronaut?.root ?? new THREE.Group();
    this.root.position.copy(this.position);
    scene.add(this.root);
    this.eyeHeight = EYE;
    this.sensitivity = 0.0022;
    this._targetYaw = this.yaw;
    this._walkTime = 0;
    this._verticalSpeed = 0;
    this.vehicle = null; // set when the commander is driving the crew rover
  }

  setAstronaut(astronaut) {
    if (!astronaut) return;
    if (this.astronaut?.root) this.scene.remove(this.astronaut.root);
    this.astronaut = astronaut;
    this.root = astronaut.root;
    this.root.position.copy(this.position);
    this.scene.add(this.root);
  }

  get isFirstPerson() {
    return this.mode === 'first';
  }

  toggleView() {
    this.mode = this.mode === 'third' ? 'first' : 'third';
    if (this.astronaut?.root) this.astronaut.root.visible = this.mode === 'third';
    return this.mode;
  }

  look(dx, dy) {
    this.yaw -= dx * this.sensitivity;
    this.pitch = THREE.MathUtils.clamp(this.pitch - dy * this.sensitivity, -1.35, 1.05);
  }

  /** Forward vector on the ground plane. */
  forwardVector(out = new THREE.Vector3()) {
    return out.set(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
  }

  eyePosition(out = new THREE.Vector3()) {
    return out.set(this.position.x, this.position.y + this.eyeHeight, this.position.z);
  }

  update(dt, input, { allowMove = true } = {}) {
    if (dt > 0.1) dt = 0.1;
    const { dx, dy } = input.takeMouse();
    if (input.locked) this.look(dx, dy);

    // At the wheel: the rover owns movement, the seat pose and the camera this frame.
    if (this.vehicle) {
      this.vehicle.update(dt, input, this);
      return;
    }

    const wish = new THREE.Vector3();
    if (allowMove) {
      if (input.down(...KEYS.forward)) wish.z -= 1;
      if (input.down(...KEYS.back)) wish.z += 1;
      if (input.down(...KEYS.left)) wish.x -= 1;
      if (input.down(...KEYS.right)) wish.x += 1;
    }
    const running = input.down(...KEYS.run);
    const speed = running ? RUN_SPEED : WALK_SPEED;

    if (wish.lengthSq() > 0) {
      wish.normalize().applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
      this._targetYaw = Math.atan2(-wish.x, -wish.z);
    }

    const accel = this.grounded ? GROUND_ACCEL : AIR_ACCEL;
    const targetX = wish.x * speed;
    const targetZ = wish.z * speed;
    this.velocity.x = approach(this.velocity.x, targetX, accel * dt);
    this.velocity.z = approach(this.velocity.z, targetZ, accel * dt);

    // Jump with a buffer and a coyote window.
    if (input.consume(...KEYS.jump)) this.jumpBuffer = JUMP_BUFFER;
    this.jumpBuffer = Math.max(0, this.jumpBuffer - dt);
    this.coyote = this.grounded ? COYOTE : Math.max(0, this.coyote - dt);
    if (this.jumpBuffer > 0 && this.coyote > 0) {
      this.velocity.y = JUMP_SPEED;
      this.grounded = false;
      this.coyote = 0;
      this.jumpBuffer = 0;
    }

    this.velocity.y -= GRAVITY * dt;

    this._moveAxis('x', this.velocity.x * dt);
    this._moveAxis('z', this.velocity.z * dt);
    this._moveVertical(this.velocity.y * dt);

    // Ground against the terrain heightfield.
    const groundY = this.terrain ? this.terrain.height(this.position.x, this.position.z) : 0;
    if (this.position.y <= groundY + 0.001) {
      this.position.y = groundY;
      if (this.velocity.y < 0) this.velocity.y = 0;
      this.grounded = true;
    }

    // Falling off the world is not a thing here, but a deep crater can still drop you.
    if (this.position.y < groundY - 4) {
      this.position.y = groundY;
      this.velocity.set(0, 0, 0);
    }

    const horizontalSpeed = Math.hypot(this.velocity.x, this.velocity.z);
    this._walkTime += dt * (0.6 + horizontalSpeed * 0.55);
    this.bob = Math.sin(this._walkTime * 4.2) * Math.min(0.05, horizontalSpeed * 0.012);
    this.headBob = Math.sin(this._walkTime * 8.4) * Math.min(0.03, horizontalSpeed * 0.006);

    this._updateRoot(dt, horizontalSpeed);
    this._updateCamera(dt, horizontalSpeed);
  }

  _updateRoot(dt, speed) {
    if (!this.astronaut?.root) return;
    this.root.position.copy(this.position);
    // Turn the suit toward travel, slowly enough to look like mass.
    let delta = this._targetYaw - this.root.rotation.y;
    while (delta > Math.PI) delta -= Math.PI * 2;
    while (delta < -Math.PI) delta += Math.PI * 2;
    this.root.rotation.y += delta * Math.min(1, dt * 9);

    if (this.astronaut.mixer) this.astronaut.mixer.update(dt);
    // The single baked clip is an idle; keep it running so the suit is never a statue.
    if (this.astronaut.play) this.astronaut.play(null);
  }

  _updateCamera(dt, speed) {
    const cam = this.camera;
    if (this.mode === 'first') {
      cam.position.set(this.position.x, this.position.y + this.eyeHeight + this.headBob * 0.5, this.position.z);
      const dir = new THREE.Vector3(
        -Math.sin(this.yaw) * Math.cos(this.pitch),
        Math.sin(this.pitch),
        -Math.cos(this.yaw) * Math.cos(this.pitch),
      );
      cam.lookAt(cam.position.clone().add(dir));
      cam.fov = 78;
    } else {
      const boom = 3.6;
      const pivot = new THREE.Vector3(this.position.x, this.position.y + 1.5 + this.bob, this.position.z);
      const dir = new THREE.Vector3(
        Math.sin(this.yaw) * Math.cos(this.pitch),
        -Math.sin(this.pitch),
        Math.cos(this.yaw) * Math.cos(this.pitch),
      ).normalize();
      const distance = this._clearBoom(pivot, dir, boom);
      cam.position.copy(pivot).addScaledVector(dir, distance);
      cam.lookAt(pivot.x - dir.x * 2, pivot.y - dir.y * 2 + 0.1, pivot.z - dir.z * 2);
      cam.fov = 62;
    }
    cam.updateProjectionMatrix();
  }

  /** Shortens the third-person boom when something solid is in the way. */
  _clearBoom(pivot, dir, maxDistance) {
    let distance = maxDistance;
    const steps = 12;
    for (let i = 1; i <= steps; i += 1) {
      const t = (i / steps) * maxDistance;
      const p = pivot.clone().addScaledVector(dir, t);
      if (this._pointInside(p, 0.28)) {
        distance = Math.max(0.5, t - 0.35);
        break;
      }
      const groundY = this.terrain ? this.terrain.height(p.x, p.z) : 0;
      if (p.y < groundY + 0.2) {
        distance = Math.max(0.5, t - 0.2);
        break;
      }
    }
    return distance;
  }

  _pointInside(point, padding = 0) {
    const feet = point.y;
    for (const c of this.colliders) {
      if (feet < c.minY - padding || feet > c.maxY + padding) continue;
      if (point.x > c.minX - padding && point.x < c.maxX + padding && point.z > c.minZ - padding && point.z < c.maxZ + padding) {
        // Ignore anything the player is standing on.
        if (c.maxY <= this.position.y + STEP_HEIGHT) continue;
        return true;
      }
    }
    return false;
  }

  _moveAxis(axis, delta) {
    if (delta === 0) return;
    this.position[axis] += delta;
    const minX = this.position.x - RADIUS;
    const maxX = this.position.x + RADIUS;
    const minZ = this.position.z - RADIUS;
    const maxZ = this.position.z + RADIUS;
    const feet = this.position.y;
    const headY = feet + HEIGHT;

    for (const c of this.colliders) {
      if (maxY(c) <= feet + STEP_HEIGHT) continue; // steppable
      if (c.minY >= headY) continue;
      if (maxX <= c.minX || minX >= c.maxX || maxZ <= c.minZ || minZ >= c.maxZ) continue;

      if (axis === 'x') {
        if (delta > 0) this.position.x = c.minX - RADIUS - 0.001;
        else this.position.x = c.maxX + RADIUS + 0.001;
      } else if (delta > 0) this.position.z = c.minZ - RADIUS - 0.001;
      else this.position.z = c.maxZ + RADIUS + 0.001;
      this.velocity[axis] = 0;
      break;
    }
  }

  _moveVertical(delta) {
    if (delta === 0) return;
    this.position.y += delta;
    const minX = this.position.x - RADIUS;
    const maxX = this.position.x + RADIUS;
    const minZ = this.position.z - RADIUS;
    const maxZ = this.position.z + RADIUS;
    let feet = this.position.y;
    const headY = feet + HEIGHT;

    for (const c of this.colliders) {
      if (maxX <= c.minX || minX >= c.maxX || maxZ <= c.minZ || minZ >= c.maxZ) continue;

      if (delta < 0 && feet < c.maxY && feet + HEIGHT > c.minY) {
        // Landing on top of a crate, a pad lip or a module.
        if (c.maxY - feet < 0.9 || c.maxY <= feet + STEP_HEIGHT * 2) {
          this.position.y = c.maxY;
          this.velocity.y = 0;
          this.grounded = true;
          feet = this.position.y;
        }
      } else if (delta > 0 && headY > c.minY && feet < c.minY) {
        this.position.y = c.minY - HEIGHT;
        this.velocity.y = 0;
      }
    }
  }

  serialize() {
    return {
      position: [round(this.position.x), round(this.position.y), round(this.position.z)],
      yaw: round(this.yaw),
      pitch: round(this.pitch),
      mode: this.mode,
    };
  }

  restore(data) {
    if (!data) return;
    if (Array.isArray(data.position) && data.position.length === 3) {
      this.position.set(Number(data.position[0]) || 0, Number(data.position[1]) || 0, Number(data.position[2]) || 0);
    }
    if (Number.isFinite(data.yaw)) this.yaw = data.yaw;
    if (Number.isFinite(data.pitch)) this.pitch = data.pitch;
    if (data.mode === 'first' || data.mode === 'third') {
      this.mode = data.mode;
      if (this.astronaut?.root) this.astronaut.root.visible = this.mode === 'third';
    }
  }
}

const round = (v) => Math.round(v * 1000) / 1000;
const maxY = (c) => c.maxY;
function approach(value, target, maxDelta) {
  if (value < target) return Math.min(target, value + maxDelta);
  if (value > target) return Math.max(target, value - maxDelta);
  return value;
}
