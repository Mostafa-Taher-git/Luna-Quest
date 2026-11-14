/**
 * Sky, ground and light for the Shackleton rim.
 *
 * There is no atmosphere here, so: a hard black sky with sharp stars, a low Sun that throws
 * very long shadows, a bright Earth that never moves far, and regolith lit almost entirely by
 * direct sunlight. The ground is generated from the shared terrain function, so the browser
 * walks on exactly the same surface as the engine builds.
 */
import * as THREE from 'three';
import { LunarTerrain, SITE_SEED } from './terrain.js';

export function createRenderer(canvas) {
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    powerPreference: 'high-performance',
    alpha: false,
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setSize(window.innerWidth, window.innerHeight, false);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.12;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFShadowMap;
  return renderer;
}

/** Loads the scanned regolith maps. Missing files degrade to a plain grey, never a crash. */
export function loadRegolithTextures(baseUrl = './') {
  const loader = new THREE.TextureLoader();
  const root = new URL(`${baseUrl}textures/regolith/`, location.href);
  const load = (file, colorSpace = THREE.NoColorSpace) =>
    new Promise((resolve) => {
      loader.load(
        new URL(file, root).href,
        (tex) => {
          tex.wrapS = THREE.RepeatWrapping;
          tex.wrapT = THREE.RepeatWrapping;
          tex.colorSpace = colorSpace;
          tex.anisotropy = 8;
          resolve(tex);
        },
        undefined,
        () => resolve(null),
      );
    });

  return Promise.all([
    load('moon_dusted_03_albedo.jpg', THREE.SRGBColorSpace),
    load('moon_dusted_03_normal.jpg'),
    load('moon_dusted_03_rough.jpg'),
    load('moon_dusted_03_ao.jpg'),
    load('moon_dusted_03_height.jpg'),
    load('../../textures/earth_day.jpg', THREE.SRGBColorSpace),
  ]).then(([albedo, normal, rough, ao, height, earth]) => ({ albedo, normal, rough, ao, height, earth }));
}

/** Heightfield mesh for one radius band. */
function terrainRing(terrain, { inner, outer, radial, theta, material }) {
  // A fan of quads from the inner radius to the horizon, sampling the coarse surface.
  const positions = [];
  const uvs = [];
  const indices = [];
  for (let r = 0; r <= radial; r += 1) {
    const t = r / radial;
    const radius = inner + (outer - inner) * Math.pow(t, 1.7);
    for (let a = 0; a <= theta; a += 1) {
      const angle = (a / theta) * Math.PI * 2;
      const x = Math.cos(angle) * radius;
      const z = Math.sin(angle) * radius;
      positions.push(x, terrain.height(x, z, 1) - 0.05, z);
      uvs.push(a / theta, t);
    }
  }
  const stride = theta + 1;
  for (let r = 0; r < radial; r += 1) {
    for (let a = 0; a < theta; a += 1) {
      const i0 = r * stride + a;
      const i1 = i0 + 1;
      const i2 = i0 + stride;
      const i3 = i2 + 1;
      indices.push(i0, i2, i1, i1, i2, i3);
    }
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  const mesh = new THREE.Mesh(geo, material);
  mesh.name = 'regolith-far';
  return mesh;
}

function terrainMesh(terrain, { size, cells, material, yOffset = 0 }) {
  const geo = new THREE.PlaneGeometry(size, size, cells, cells);
  geo.rotateX(-Math.PI / 2);
  const pos = geo.attributes.position;
  for (let i = 0; i < pos.count; i += 1) {
    const x = pos.getX(i);
    const z = pos.getZ(i);
    pos.setY(i, terrain.height(x, z) + yOffset);
  }
  geo.computeVertexNormals();
  const mesh = new THREE.Mesh(geo, material);
  mesh.receiveShadow = true;
  mesh.name = 'regolith';
  return mesh;
}

export function createEnvironment(scene, { textures = {} } = {}) {
  const terrain = new LunarTerrain(SITE_SEED);

  // ---- ground material -------------------------------------------------------
  const regolithMat = new THREE.MeshStandardMaterial({
    color: 0xcfc8bb,
    roughness: 0.97,
    metalness: 0.0,
    map: textures.albedo ?? null,
    normalMap: textures.normal ?? null,
    roughnessMap: textures.rough ?? null,
    aoMap: textures.ao ?? null,
  });
  if (regolithMat.map) {
    for (const key of ['map', 'normalMap', 'roughnessMap', 'aoMap']) {
      if (regolithMat[key]) {
        regolithMat[key].repeat.set(140, 140);
        regolithMat[key].wrapS = THREE.RepeatWrapping;
        regolithMat[key].wrapT = THREE.RepeatWrapping;
      }
    }
  }
  if (regolithMat.normalMap) regolithMat.normalScale = new THREE.Vector2(0.35, 0.35);

  const near = terrainMesh(terrain, { size: 320, cells: 128, material: regolithMat, yOffset: 0.0 });
  // The far band uses its own copy of the maps at a much lower repeat: tiling a 1 m scan
  // across a kilometre turns into a visible grid, which is exactly what a plain does not have.
  const farMat = regolithMat.clone();
  for (const key of ['map', 'normalMap', 'roughnessMap', 'aoMap']) {
    if (farMat[key]) {
      farMat[key] = regolithMat[key].clone();
      farMat[key].repeat.set(26, 26);
      farMat[key].needsUpdate = true;
    }
  }
  // The far band is a ring, not a bigger plane: overlapping planes fight for the depth
  // buffer and show up as shimmering bands across the plain.
  const far = terrainRing(terrain, { inner: 150, outer: 1500, radial: 44, theta: 168, material: farMat });
  far.receiveShadow = false;
  far.renderOrder = -1;
  scene.add(near, far);

  // ---- sky -------------------------------------------------------------------
  scene.background = new THREE.Color(0x020306);

  const starCount = 3600;
  const starGeo = new THREE.BufferGeometry();
  const starPos = new Float32Array(starCount * 3);
  const starCol = new Float32Array(starCount * 3);
  const starSize = new Float32Array(starCount);
  const rand = (() => {
    let s = 12345;
    return () => {
      s = (s * 1103515245 + 12345) & 0x7fffffff;
      return s / 0x7fffffff;
    };
  })();
  for (let i = 0; i < starCount; i += 1) {
    // Uniform on a sphere, radius far outside the terrain.
    const u = rand() * 2 - 1;
    const theta = rand() * Math.PI * 2;
    const r = Math.sqrt(1 - u * u);
    const R = 2400;
    starPos[i * 3] = Math.cos(theta) * r * R;
    starPos[i * 3 + 1] = Math.abs(u) * R; // nothing below the horizon needs drawing
    starPos[i * 3 + 2] = Math.sin(theta) * r * R;
    const mag = Math.pow(rand(), 2.4);
    const c = 0.55 + mag * 0.45;
    starCol[i * 3] = c * (0.94 + rand() * 0.06);
    starCol[i * 3 + 1] = c * (0.96 + rand() * 0.04);
    starCol[i * 3 + 2] = c;
    starSize[i] = 1.2 + mag * 3.4;
  }
  starGeo.setAttribute('position', new THREE.BufferAttribute(starPos, 3));
  starGeo.setAttribute('color', new THREE.BufferAttribute(starCol, 3));
  starGeo.setAttribute('aSize', new THREE.BufferAttribute(starSize, 1));

  const starMat = new THREE.PointsMaterial({
    size: 2.6,
    sizeAttenuation: false,
    vertexColors: true,
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  const stars = new THREE.Points(starGeo, starMat);
  stars.frustumCulled = false;
  scene.add(stars);

  // ---- Earth -----------------------------------------------------------------
  const earthGroup = new THREE.Group();
  const earthGeo = new THREE.SphereGeometry(22, 48, 32);
  // A world two light-seconds away shows its own lit face, so it is drawn unlit rather than
  // waiting for a Sun the camera is standing on.
  const earthMat = textures.earth
    ? new THREE.MeshBasicMaterial({ map: textures.earth, color: 0xd8e6ff })
    : new THREE.MeshBasicMaterial({ color: 0x2f5f9f });
  const earth = new THREE.Mesh(earthGeo, earthMat);
  earth.position.set(0, 0, 0);
  earthGroup.add(earth);

  // Thin blue limb: a slightly larger back-facing sphere with additive glow.
  const halo = new THREE.Mesh(
    new THREE.SphereGeometry(23.4, 40, 26),
    new THREE.MeshBasicMaterial({ color: 0x4f9be0, transparent: true, opacity: 0.16, side: THREE.BackSide, blending: THREE.AdditiveBlending }),
  );
  earthGroup.add(halo);
  earthGroup.position.set(-380, 210, -1500);
  earthGroup.rotation.y = 1.2;
  scene.add(earthGroup);

  // ---- Sun -------------------------------------------------------------------
  // A 0.53° disc at the same place as the directional light, plus its glare.
  const sunGroup = new THREE.Group();
  const sunDisc = new THREE.Mesh(
    new THREE.CircleGeometry(11, 32),
    new THREE.MeshBasicMaterial({ color: 0xfff8ec }),
  );
  sunGroup.add(sunDisc);
  const glareTex = radialGlowTexture();
  const glare = new THREE.Sprite(
    new THREE.SpriteMaterial({ map: glareTex, color: 0xfff2d8, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.9 }),
  );
  glare.scale.setScalar(230);
  sunGroup.add(glare);
  sunGroup.position.set(0, 0, -2200);
  scene.add(sunGroup);

  const sunLight = new THREE.DirectionalLight(0xfff8ee, 4.2);
  sunLight.castShadow = true;
  sunLight.shadow.mapSize.set(1024, 1024);
  sunLight.shadow.camera.near = 1;
  sunLight.shadow.camera.far = 420;
  const shadowSpan = 74;
  sunLight.shadow.camera.left = -shadowSpan;
  sunLight.shadow.camera.right = shadowSpan;
  sunLight.shadow.camera.top = shadowSpan;
  sunLight.shadow.camera.bottom = -shadowSpan;
  sunLight.shadow.bias = -0.0006;
  sunLight.shadow.normalBias = 0.035;
  scene.add(sunLight);
  scene.add(sunLight.target);

  // Earthshine and the ground's own bounce, both weak but not zero.
  // Earthshine from a big blue world, the regolith bouncing light up, and a whisper of
  // ambient: enough to read a shadowed wall without pretending there is an atmosphere.
  const hemi = new THREE.HemisphereLight(0x9fb6d8, 0x9a8f7d, 0.62);
  scene.add(hemi);
  const fill = new THREE.DirectionalLight(0xa8c2e8, 0.55);
  fill.position.set(-180, 120, -220);
  scene.add(fill);
  const ambient = new THREE.AmbientLight(0x46536a, 0.42);
  scene.add(ambient);

  const sunDir = new THREE.Vector3();
  const state = { sunElevationDeg: 11, sunAzimuthDeg: 180 };

  // The colony is not aligned with the map axes: the site faces its doors into a shallow
  // bowl, and the polar Sun sweeps past those doors. This offset is the site's orientation,
  // not a change to the simulation.
  const SITE_AZIMUTH_OFFSET_DEG = 232;

  function updateSun(clock) {
    const elevation = THREE.MathUtils.degToRad(clock.sunElevationDeg);
    const azimuth = THREE.MathUtils.degToRad(clock.sunAzimuthDeg + SITE_AZIMUTH_OFFSET_DEG);
    // The Sun crawls around the horizon, so its horizontal component dominates.
    sunDir.set(Math.sin(azimuth) * Math.cos(elevation), Math.sin(elevation), Math.cos(azimuth) * Math.cos(elevation)).normalize();
    sunGroup.position.copy(sunDir).multiplyScalar(2200);
    sunGroup.lookAt(0, 0, 0);
    sunLight.position.copy(sunDir).multiplyScalar(180);
    sunLight.target.position.set(0, 0, 0);
    sunLight.target.updateMatrixWorld();
    state.sunElevationDeg = clock.sunElevationDeg;
    state.sunAzimuthDeg = clock.sunAzimuthDeg;
    // A grazing Sun is dimmer than a high one on a tilted array, but the disc stays bright.
    sunDisc.material.color.setRGB(1, 0.97, 0.9);
    glare.material.opacity = 0.55 + 0.35 * Math.max(0, Math.min(1, clock.solarIrradianceFraction));
  }

  /** Keeps the shadow frustum around the player so the whole colony stays crisp. */
  function followShadow(target) {
    sunLight.position.copy(target).addScaledVector(sunDir, 160);
    sunLight.target.position.copy(target);
    sunLight.target.updateMatrixWorld();
  }

  return {
    terrain,
    regolithMat,
    farMat,
    near,
    far,
    stars,
    earth,
    earthGroup,
    sunGroup,
    sunLight,
    updateSun,
    followShadow,
    state,
    dispose() {
      scene.remove(near, far, stars, earthGroup, sunGroup, sunLight, hemi, fill, ambient);
      near.geometry.dispose();
      far.geometry.dispose();
      regolithMat.dispose();
    },
  };
}

let glowTexture = null;
function radialGlowTexture() {
  if (glowTexture) return glowTexture;
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');
  const g = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  g.addColorStop(0, 'rgba(255,255,255,1)');
  g.addColorStop(0.12, 'rgba(255,248,232,0.85)');
  g.addColorStop(0.32, 'rgba(255,236,200,0.22)');
  g.addColorStop(0.6, 'rgba(255,230,190,0.05)');
  g.addColorStop(1, 'rgba(255,230,190,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  glowTexture = new THREE.CanvasTexture(canvas);
  glowTexture.colorSpace = THREE.SRGBColorSpace;
  return glowTexture;
}
