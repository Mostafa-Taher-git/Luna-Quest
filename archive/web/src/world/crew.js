/**
 * The six crew and NEXO, walking their schedule.
 *
 * Each crew member moves to whatever building their schedule says they are in, stands outside
 * the hatch they are working behind, and turns to face the commander when you come close.
 * NEXO floats: a white shell, a blue band, a screen face whose eyes go orange when the colony
 * has an active warning. No dialogue is generated — every line is content.
 */
import * as THREE from 'three';
import { CREW_HEIGHT_M } from './crew-metrics.js';

const TRIM = {
  npc_safaa: 0xd45f6a,
  npc_nour: 0x3fa7d6,
  npc_alaa: 0x63b86b,
  npc_abdelaziz: 0xe0a52a,
  npc_ahmed: 0x8f7ad6,
  npc_mostafa: 0xc9c2b4,
  npc_nexo: 0x58c6e8,
};

export class Crew {
  constructor({ content, factory, scene, terrain, buildings, sim, audio, materials }) {
    this.content = content;
    this.factory = factory;
    this.scene = scene;
    this.terrain = terrain;
    this.buildings = buildings;
    this.sim = sim;
    this.audio = audio;
    this.materials = materials;
    this.members = [];
    this.root = new THREE.Group();
    this.root.name = 'Crew';
    scene.add(this.root);
    this.nexoEyes = [];
    this._barks = 0;
  }

  async build(onProgress = () => {}) {
    const npcs = this.content.npcs;
    let done = 0;
    for (const profile of npcs) {
      const home = this.buildings.get(profile.locationId) ?? this.buildings.get('habitat');
      const spawn = this._spotFor(home, profile.id);
      if (profile.isRobot) {
        const member = buildNexo(this.materials, TRIM[profile.id]);
        member.root.position.copy(spawn);
        this.root.add(member.root);
        this.members.push({ profile, ...member, target: spawn.clone(), position: spawn.clone(), bob: Math.random() * 6 });
        this.nexoEyes.push(...member.eyes);
      } else {
        const astronaut = await this.factory.create({ height: CREW_HEIGHT_M, trim: TRIM[profile.id], role: profile.id });
        astronaut.root.position.copy(spawn);
        astronaut.root.rotation.y = Math.random() * Math.PI * 2;
        this.root.add(astronaut.root);
        this.members.push({
          profile,
          root: astronaut.root,
          astronaut,
          target: spawn.clone(),
          position: spawn.clone(),
          speed: 0.9 + Math.random() * 0.3,
          idlePhase: Math.random() * 6.28,
        });
      }
      done += 1;
      onProgress(done / npcs.length);
    }
    return this.members;
  }

  /** A standing spot just outside a building, spread around the door. */
  _spotFor(building, npcId) {
    if (!building) return new THREE.Vector3(0, 0, 16);
    const [x, z] = building.position;
    const [w, d] = building.footprint;
    const hash = [...npcId].reduce((a, c) => a + c.charCodeAt(0), 0);
    const side = npcId.length % 2 === 0 ? 1 : -1;
    const offset = side * (2.2 + (hash % 5) * 0.9);
    const px = x + offset;
    const pz = z + Math.sign(z || 1) * (d / 2 + 4.2);
    return new THREE.Vector3(px, this.terrain ? this.terrain.height(px, pz) : 0, pz);
  }

  update(dt, { playerPosition = null, anyIncident = false } = {}) {
    const hour = this.sim.Clock.hourOfDay;
    for (const member of this.members) {
      const state = this.sim.Npcs.state(member.profile.id);
      const building = this.buildings.get(state.locationId);
      if (building) {
        const spot = this._spotFor(building, member.profile.id);
        member.target.copy(spot);
      }

      const distance = member.position.distanceTo(member.target);
      const isRobot = member.profile.isRobot;
      if (distance > 0.6) {
        const speed = isRobot ? 2.4 : member.speed;
        const dir = member.target.clone().sub(member.position);
        dir.y = 0;
        dir.normalize();
        member.position.addScaledVector(dir, Math.min(distance, speed * dt));
        member.position.y = this.terrain ? this.terrain.height(member.position.x, member.position.z) : 0;
        const yaw = Math.atan2(dir.x, dir.z);
        const current = member.root.rotation.y;
        let delta = yaw - current;
        while (delta > Math.PI) delta -= Math.PI * 2;
        while (delta < -Math.PI) delta += Math.PI * 2;
        member.root.rotation.y = current + delta * Math.min(1, dt * 6);
        member.walking = true;
      } else {
        member.walking = false;
        // Turn to look at the commander when they are close.
        if (playerPosition && member.position.distanceTo(playerPosition) < 7) {
          const dir = playerPosition.clone().sub(member.position);
          const yaw = Math.atan2(dir.x, dir.z);
          const current = member.root.rotation.y;
          let delta = yaw - current;
          while (delta > Math.PI) delta -= Math.PI * 2;
          while (delta < -Math.PI) delta += Math.PI * 2;
          member.root.rotation.y = current + delta * Math.min(1, dt * 2.4);
        }
      }

      if (isRobot) {
        member.bob += dt * 1.6;
        member.root.position.set(member.position.x, member.position.y + 0.55 + Math.sin(member.bob) * 0.08, member.position.z);
        const color = anyIncident ? 0xff8a2b : 0x58c6e8;
        for (const eye of member.eyes) eye.material.color.setHex(color);
        for (const eye of member.eyes) eye.material.emissive?.setHex(color);
        if (member.thruster) member.thruster.material.opacity = 0.35 + 0.12 * Math.sin(member.bob * 3);
      } else {
        member.root.position.copy(member.position);
        if (member.astronaut?.mixer) member.astronaut.mixer.update(dt);
      }
    }

    // Occasional ambient crew line, when the commander is near and nothing is blocking it.
    this._barks += dt;
    if (this._barks > 40 && playerPosition) {
      const near = this.members.filter((m) => !m.profile.isRobot && m.position.distanceTo(playerPosition) < 6);
      if (near.length) {
        const pick = near[Math.floor(Math.random() * near.length)];
        this.audio?.bark(pick.profile.id);
      }
      this._barks = 0;
    }
  }

  /** Closest crew member within reach, for the interaction prompt. */
  nearest(playerPosition, maxDistance = 3.2) {
    let best = null;
    let bestDistance = maxDistance;
    for (const member of this.members) {
      const d = member.position.distanceTo(playerPosition);
      if (d < bestDistance) {
        bestDistance = d;
        best = member;
      }
    }
    return best;
  }

  setMood(anyIncident) {
    for (const member of this.members) {
      if (!member.profile.isRobot) continue;
      const color = anyIncident ? 0xff8a2b : 0x58c6e8;
      for (const eye of member.eyes) {
        eye.material.color.setHex(color);
        eye.material.emissive?.setHex(color);
      }
    }
  }
}

/** NEXO: the colony robot from the intro film, built from primitives. */
function buildNexo(materials, bandColor) {
  const root = new THREE.Group();
  root.name = 'NEXO';

  const shellMat = new THREE.MeshStandardMaterial({ color: 0xf1f4f7, roughness: 0.32, metalness: 0.42 });
  const bandMat = new THREE.MeshStandardMaterial({ color: bandColor, roughness: 0.35, metalness: 0.5, emissive: bandColor, emissiveIntensity: 0.35 });
  const darkMat = new THREE.MeshStandardMaterial({ color: 0x0c131c, roughness: 0.4, metalness: 0.5 });

  const body = new THREE.Mesh(new THREE.SphereGeometry(0.34, 26, 20), shellMat);
  body.scale.set(1, 1.12, 0.92);
  body.castShadow = true;
  root.add(body);

  const band = new THREE.Mesh(new THREE.TorusGeometry(0.335, 0.035, 10, 30), bandMat);
  band.rotation.x = Math.PI / 2;
  band.position.y = 0.02;
  root.add(band);

  // Screen face with two eyes.
  const face = new THREE.Mesh(new THREE.SphereGeometry(0.245, 22, 16, 0, Math.PI * 2, 0, Math.PI * 0.62), darkMat);
  face.rotation.x = Math.PI / 2;
  face.position.set(0, 0.06, 0.2);
  root.add(face);

  const eyes = [];
  for (const side of [-1, 1]) {
    const eye = new THREE.Mesh(
      new THREE.CircleGeometry(0.055, 18),
      new THREE.MeshStandardMaterial({ color: bandColor, emissive: bandColor, emissiveIntensity: 2.4, roughness: 0.3 }),
    );
    eye.position.set(side * 0.09, 0.1, 0.395);
    root.add(eye);
    eyes.push(eye);
  }

  // Arms and a small sensor cluster.
  for (const side of [-1, 1]) {
    const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.055, 0.24, 4, 10), shellMat);
    arm.position.set(side * 0.36, -0.04, 0);
    arm.rotation.z = side * 0.9;
    arm.castShadow = true;
    root.add(arm);
  }
  const sensor = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.12, 8), darkMat);
  sensor.position.set(0, 0.42, 0);
  root.add(sensor);

  // Thruster glow underneath: it floats, so give it a reason.
  const thruster = new THREE.Mesh(
    new THREE.ConeGeometry(0.16, 0.22, 16, 1, true),
    new THREE.MeshBasicMaterial({ color: 0x3fb0e8, transparent: true, opacity: 0.4, side: THREE.DoubleSide, depthWrite: false }),
  );
  thruster.rotation.x = Math.PI;
  thruster.position.y = -0.42;
  root.add(thruster);

  const light = new THREE.PointLight(0x58c6e8, 1.6, 4.5, 2);
  light.position.set(0, 0.15, 0.3);
  root.add(light);

  return { root, eyes, thruster };
}
