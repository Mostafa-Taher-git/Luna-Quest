/**
 * Luna-Quest, in the browser.
 *
 * Boot order: assets → simulation → world → crew → the film → the colony. The intro film is
 * the existing Luna-Quest film, untouched; it is the game's opening, and skipping it is always
 * one key away. Everything after it is the same colony, the same rules and the same save
 * schema as the Godot and Unity builds.
 */
import * as THREE from 'three';

import { loadContent } from './content.js';
import { Simulation } from './sim/simulation.js';
import { read } from './sim/state.js';
import { Crop } from './sim/models.js';
import { L, setLanguage, t } from './i18n.js';
import { Input, KEYS } from './input.js';
import { Player } from './player.js';
import { Materials } from './world/materials.js';
import { createRenderer, createEnvironment, loadRegolithTextures } from './world/environment.js';
import { createBuilding } from './world/buildings.js';
import { SolidWorld } from './world/parts.js';
import { PropLibrary, dressColony } from './world/props.js';
import { AstronautFactory } from './world/astronaut.js';
import { mergeStaticMeshes } from './world/merge.js';
import { createPlaza } from './world/plaza.js';
import { Crew } from './world/crew.js';
import { Hud } from './ui/hud.js';
import { AudioManager } from './audio.js';

const BASE = import.meta.env.BASE_URL ?? './';
const SAVE_KEY = 'lunaquest-web-save-v2';
const INTRO_KEY = 'lunaquest-web-intro-seen';

class Game {
  constructor() {
    this.canvas = document.getElementById('scene');
    this.bootLine = document.getElementById('boot-line');
    this.bootProgress = document.getElementById('boot-progress');
    this.running = false;
    this.paused = true;
    this.started = false;
    this.overlayOpen = false;
    this.missionEnded = false;
    this.consoles = [];
    this.buildings = new Map();
    this.raycaster = new THREE.Raycaster();
    this.center = new THREE.Vector2(0, 0);
    this.focus = null;
    this.nearCrew = null;
    this.elapsed = 0;
    this.errorShown = false;
  }

  async boot() {
    this.setProgress(0.02, 'Reading the colony file…');

    // ---- renderer and scene ------------------------------------------------
    this.renderer = createRenderer(this.canvas);
    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(62, window.innerWidth / window.innerHeight, 0.08, 4000);
    this.camera.position.set(0, 2.4, 26);

    await this.loadFonts();
    this.setProgress(0.06, 'Loading content…');
    this.content = await loadContent(BASE, (fraction, name) => this.setProgress(0.06 + fraction * 0.08, `Content: ${name}`));

    this.sim = new Simulation(this.content);
    // A new mission starts at 06:00 so the first shift, meal and maintenance round are ahead.
    this.sim.Clock.setTotalMinutes(6 * 60);
    this.sim.PlayerName = 'Mostafa';

    // ---- environment -------------------------------------------------------
    this.setProgress(0.16, 'Scanned regolith and the Earth…');
    this.textures = await loadRegolithTextures(BASE);
    this.env = createEnvironment(this.scene, { textures: this.textures });
    this.env.updateSun(this.sim.Clock);

    // ---- materials and structure -------------------------------------------
    this.setProgress(0.3, 'Building the colony…');
    this.materials = new Materials(this.renderer);
    this.solids = new SolidWorld();
    this.buildGroup = new THREE.Group();
    this.buildGroup.name = 'Colony';
    this.scene.add(this.buildGroup);

    for (const building of this.content.buildings) {
      const { group, consoles } = createBuilding(this.materials, building, { solids: this.solids });
      // Merge before adding: one mesh per material per building instead of one per railing.
      mergeStaticMeshes(group, {
        keep: (mesh) => Boolean(mesh.userData.isConsole),
        castShadowFor: (material) => ![this.materials.accent, this.materials.dark, this.materials.lamp, this.materials.screen].includes(material),
      });
      // Bake the group transform now; freezing matrixAutoUpdate before this would leave
      // every building stacked at the origin.
      group.updateMatrix();
      this.buildGroup.add(group);
      this.buildings.set(building.id, building);
      this.consoles.push(...consoles);
    }
    this.buildGroup.updateMatrixWorld(true);

    // ---- paved plaza -------------------------------------------------------
    const plaza = createPlaza(this.materials, this.env.terrain);
    mergeStaticMeshes(plaza, { castShadowFor: () => false });
    plaza.updateMatrix();
    this.scene.add(plaza);

    // ---- dressing from the CC0 kit ----------------------------------------
    this.setProgress(0.5, 'Placing landers, rovers and masts…');
    this.props = new PropLibrary({ materials: this.materials, baseUrl: BASE });
    this.scene.add(this.props.root);
    const dressing = await dressColony(this.props, this.env.terrain, (fraction) =>
      this.setProgress(0.5 + fraction * 0.2, 'Placing landers, rovers and masts…'),
    );
    // Bake the kit and the rocks down to one mesh per material: 395 meshes become a handful.
    const propMerge = mergeStaticMeshes(this.props.root, {
      castShadowFor: (material) => material.name !== 'rock',
    });
    this.allColliders = [...this.solids.boxes, ...this.props.colliders];
    console.info(`Geometry merged: dressing ${propMerge.merged} materials from ${propMerge.removed} meshes (${propMerge.skipped} kept whole).`);

    // ---- the commander and the crew ---------------------------------------
    this.setProgress(0.74, 'Waking the crew…');
    this.astronauts = new AstronautFactory({ materials: this.materials, baseUrl: BASE });
    const commanderSuit = await this.astronauts.create({ height: 1.78, trim: 0xf2a93b, role: 'commander' });

    this.player = new Player({
      camera: this.camera,
      scene: this.scene,
      terrain: this.env.terrain,
      colliders: this.allColliders,
      spawn: new THREE.Vector3(0, 0, 21),
      astronaut: commanderSuit,
    });
    this.player.sensitivity = 0.0021;
    this.player.yaw = 0; // look at the habitat hatch, not back at the storage shed

    this.crew = new Crew({
      content: this.content,
      factory: this.astronauts,
      scene: this.scene,
      terrain: this.env.terrain,
      buildings: this.buildings,
      sim: this.sim,
      audio: null,
      materials: this.materials,
    });
    await this.crew.build((fraction) => this.setProgress(0.74 + fraction * 0.08, 'Waking the crew…'));

    // ---- interface and audio ----------------------------------------------
    this.setProgress(0.84, 'Wiring the consoles…');
    this.initQuality();

    this.hud = new Hud({
      sim: this.sim,
      content: this.content,
      onLanguageChange: () => this.toggleLanguage(),
      onClose: () => {
        this.overlayOpen = false;
        this.paused = !this.started ? true : this.paused;
      },
    });
    this.hud.panelHandlers = {};

    this.audio = await new AudioManager({
      baseUrl: BASE,
      onSubtitle: (en, ar) => this.hud.subtitle(en, ar, 6.5),
    }).load();
    this.crew.audio = this.audio;

    this.input = new Input(window);
    this.input.attach(this.canvas);
    this.input.handlers.onPointerLock.push((locked) => {
      if (!locked && this.started && !this.overlayOpen && !this.paused) this.pause();
    });
    this.canvas.addEventListener('click', () => {
      if (this.started && !this.overlayOpen && !this.paused) this.input.requestLock();
    });

    this.wireUi();
    window.addEventListener('resize', () => this.resize());
    this.resize();

    this.setProgress(0.94, 'Checking the hatch seals…');
    console.info(
      `Luna-Quest web ready: ${this.content.buildings.length} buildings, ${this.content.npcs.length} crew, ` +
        `${dressing.props} kit props, ${dressing.rocks} rocks, ${this.consoles.length} consoles, ` +
        `${this.sim.Learning.journal.length} journal entries.`,
    );

    this.setProgress(1, 'Ready');
    this.showIntroOrStart();
    this.renderer.setAnimationLoop((time) => this.frame(time));
  }

  async loadFonts() {
    // Canvas signage needs the same faces as the DOM, especially for Arabic.
    try {
      const latin = new FontFace('Noto Latin', `url(${new URL(`${BASE}fonts/noto-sans-latin.woff`, location.href)})`);
      const arabic = new FontFace('Noto Arabic', `url(${new URL(`${BASE}fonts/noto-sans-arabic.woff`, location.href)})`);
      const faces = await Promise.allSettled([latin.load(), arabic.load()]);
      for (const face of faces) {
        if (face.status === 'fulfilled') document.fonts.add(face.value);
      }
    } catch {
      /* the system font is a fine fallback */
    }
  }

  /**
   * Adaptive quality. A colony with sixteen buildings, a kit dressing and a photo-scanned
   * ground is fine on a discrete GPU and painful on an old integrated one, so the renderer
   * steps itself down instead of stuttering: first the shadow resolution, then shadows
   * entirely, then the far terrain band.
   */
  initQuality() {
    this.quality = { level: 2, samples: [], cooldown: 0 };
    this.renderer.shadowMap.autoUpdate = false;
    this.renderer.shadowMap.needsUpdate = true;
    this.shadowTick = 0;
  }

  adaptQuality(dt) {
    const q = this.quality;
    if (q.level === 0) return;
    q.samples.push(dt);
    if (q.samples.length > 90) q.samples.shift();
    q.cooldown -= dt;
    if (q.samples.length < 45 || q.cooldown > 0) return;
    const average = q.samples.reduce((a, b) => a + b, 0) / q.samples.length;
    const fps = average > 0 ? 1 / average : 0;
    if (fps > 26) return;
    q.cooldown = 4;
    q.level -= 1;
    if (q.level === 1) {
      this.renderer.shadowMap.enabled = false;
      this.env.sunLight.castShadow = false;
      this.renderer.setPixelRatio(Math.min(1, window.devicePixelRatio || 1));
      this.resize();
    } else if (q.level === 0) {
      this.env.far.visible = false;
      if (this.env.regolithMat.normalMap) {
        this.env.regolithMat.normalMap = null;
        this.env.regolithMat.needsUpdate = true;
      }
    }
    console.info(`Adaptive quality: level ${q.level} (${fps.toFixed(1)} fps)`);
  }

  setProgress(fraction, label) {
    if (this.bootProgress) this.bootProgress.style.width = `${Math.round(fraction * 100)}%`;
    if (this.bootLine && label) this.bootLine.textContent = label;
  }

  // ---- screens -------------------------------------------------------------

  showIntroOrStart() {
    document.getElementById('boot').classList.add('hidden');
    const seen = localStorage.getItem(INTRO_KEY) === '1';
    const video = document.getElementById('intro-video');
    if (seen || !video) {
      this.showStart();
      return;
    }
    const intro = document.getElementById('intro');
    const skip = document.getElementById('intro-skip');
    intro.classList.remove('hidden');

    let finished = false;
    const finish = () => {
      if (finished) return;
      finished = true;
      localStorage.setItem(INTRO_KEY, '1');
      intro.classList.add('hidden');
      video.pause();
      this.showStart();
    };
    skip.textContent = L.arabic ? 'تخطي المقدمة' : 'Skip intro';
    skip.addEventListener('click', finish, { once: true });
    video.addEventListener('ended', finish, { once: true });
    video.addEventListener('error', finish, { once: true });
    window.addEventListener('keydown', (event) => {
      if (finished) return;
      if (['Escape', 'Space', 'Enter'].includes(event.code)) finish();
    });

    // The film has narration, so sound matters; try it with audio first. Browsers that
    // refuse autoplay get a Play button, and a film that cannot decode at all (an old
    // Chromium without H.264, for example) is replaced by a poster and an honest note.
    video.muted = false;
    video.volume = 0.95;
    const showPlayButton = () => {
      if (document.getElementById('intro-play')) return;
      const play = document.createElement('button');
      play.id = 'intro-play';
      play.className = 'primary-button';
      play.type = 'button';
      play.textContent = L.arabic ? '▶ شاهد المقدمة' : '▶ Watch the intro';
      play.style.position = 'absolute';
      play.style.left = '50%';
      play.style.top = '52%';
      play.style.transform = 'translate(-50%, -50%)';
      play.addEventListener('click', () => {
        video.muted = false;
        video.currentTime = 0;
        video.play().catch(() => {});
        play.remove();
      });
      intro.appendChild(play);
    };

    const attempt = video.play();
    if (attempt?.catch) attempt.catch(() => showPlayButton());

    window.setTimeout(() => {
      if (finished) return;
      if (video.paused || video.currentTime < 0.15) {
        showPlayButton();
        const note = document.createElement('p');
        note.className = 'start-foot';
        note.style.position = 'absolute';
        note.style.left = '50%';
        note.style.bottom = '86px';
        note.style.transform = 'translateX(-50%)';
        note.style.textAlign = 'center';
        note.textContent = L.arabic
          ? 'إن لم تعمل المقدمة على هذا المتصفح، يمكنك تخطيها وبدء المناوبة.'
          : 'If the film cannot play in this browser, skip it — the colony is ready either way.';
        intro.appendChild(note);
        window.setTimeout(() => note.remove(), 9000);
      }
    }, 3200);
  }

  showStart() {
    const start = document.getElementById('start');
    start.classList.remove('hidden');
    this.hud.hide();
    const nameInput = document.getElementById('commander-name');
    nameInput.value = this.sim.PlayerName;
    const begin = document.getElementById('begin');
    begin.addEventListener('click', () => this.beginMission(), { once: false });
    nameInput.addEventListener('keydown', (event) => {
      if (event.code === 'Enter') this.beginMission();
    });
    document.getElementById('lang-toggle').addEventListener('click', () => this.toggleLanguage());
  }

  beginMission() {
    const nameInput = document.getElementById('commander-name');
    const name = (nameInput?.value ?? '').trim().slice(0, 24) || 'Mostafa';
    this.sim.PlayerName = name;
    document.getElementById('start').classList.add('hidden');
    this.hud.show();
    this.hud.refreshStatic();
    this.started = true;
    this.paused = false;
    this.audio.startMusic();
    this.input.requestLock();
    this.hud.toast(
      L.arabic
        ? `${name}، أنت على اللوحة. امشِ في المستعمرة واقرأ الأرقام.`
        : `${name}, you are on console. Walk the colony and read the numbers.`,
      7000,
    );
    this.hud.renderTasks();
  }

  // ---- interface wiring ----------------------------------------------------

  wireUi() {
    this.hud.panelHandlers = {
      'console-action': (interactionId) => this.runConsole(interactionId, true),
      'console-read': (interactionId) => this.runConsole(interactionId, false),
      __save: () => this.save(),
      __load: () => this.load(),
    };
    this.hud.onLanguageChange = () => this.toggleLanguage();
  }

  toggleLanguage() {
    setLanguage(!L.arabic);
    this.hud.refreshStatic();
    this.hud.renderTasks();
    this.hud.toast(L.arabic ? 'تم التبديل إلى العربية.' : 'Switched to English.', 2200);
    if (this.hud.openPanelName === 'console' && this.hud.consoleContext) {
      this.hud.consolePanel(this.hud.consoleContext.buildingId, this.hud.consoleContext.interactionId);
    }
  }

  pause() {
    if (!this.started || this.paused) return;
    this.paused = true;
    this.overlayOpen = true;
    this.input.releaseLock();
    this.hud.pausePanel({ onResume: () => this.resume() });
  }

  resume() {
    this.overlayOpen = false;
    this.paused = false;
    this.input.requestLock();
  }

  // ---- consoles and crew ---------------------------------------------------

  runConsole(interactionId, isAction) {
    const ctx = this.hud.consoleContext;
    if (!ctx) return;
    const result = this.sim.interact(ctx.buildingId, interactionId);
    const message = L.arabic ? result.ar || result.en : result.en || result.ar;
    if (message) {
      this.hud.toast(message, 5200);
      this.hud.pushLog(
        {
          id: `act:${interactionId}:${Math.round(this.sim.Clock.totalMinutes)}`,
          kind: result.ok ? 'info' : 'warning',
          titleEn: isAction ? 'Console action' : 'Observation logged',
          titleAr: isAction ? 'إجراء على اللوحة' : 'سُجلت الملاحظة',
          bodyEn: result.en,
          bodyAr: result.ar,
        },
        { recordOnly: false },
      );
    }
    this.hud.consolePanel(ctx.buildingId, interactionId);
    this.hud.renderTasks();
    this.audio.duck(true);
    setTimeout(() => this.audio.duck(false), 900);
  }

  openConsole(consoleEntry) {
    this.overlayOpen = true;
    this.input.releaseLock();
    this.audio.duck(true);
    this.hud.consolePanel(consoleEntry.buildingId, consoleEntry.interactionId);
    const owner = this.content.incidents.find((i) => i.location === consoleEntry.buildingId)?.owner;
    if (owner) this.audio.greeting(owner);
  }

  talkTo(member) {
    const profile = member.profile;
    const state = this.sim.Npcs.state(profile.id);
    const topics = this.content.topicsFor(profile.id);
    this.overlayOpen = true;
    this.input.releaseLock();
    this.audio.duck(true);
    this.audio.greeting(profile.id);
    this.hud.crewPanel({
      profile,
      state,
      topics,
      onTopic: (topicId) => {
        const topic = topics.find((x) => x.id === topicId);
        if (!topic) return;
        const spoken = this.audio.topic(profile.id, topicId);
        if (!spoken) this.hud.subtitle(topic.lineEn, topic.lineAr, 7);
        this.hud.pushLog(
          {
            id: `talk:${profile.id}:${topicId}:${Math.round(this.sim.Clock.totalMinutes)}`,
            kind: 'info',
            titleEn: `${profile.nameEn} — ${topic.labelEn}`,
            titleAr: `${profile.nameAr} — ${topic.labelAr}`,
            bodyEn: topic.lineEn,
            bodyAr: topic.lineAr,
            speaker: profile.id,
          },
          { recordOnly: false },
        );
      },
    });
  }

  // ---- save / load ---------------------------------------------------------

  save() {
    try {
      const data = this.sim.captureSave({ player: this.player.serialize() });
      localStorage.setItem(SAVE_KEY, JSON.stringify(data));
      this.hud.toast(t('saved'), 3200);
    } catch (err) {
      this.hud.toast(`Save failed: ${err.message}`, 5000);
    }
  }

  load() {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) {
      this.hud.toast(t('noSave'), 3600);
      return;
    }
    try {
      const data = JSON.parse(raw);
      this.sim.applySave(data);
      if (data.extras?.player) this.player.restore(data.extras.player);
      this.hud.bindSim(this.sim);
      this.hud.renderTasks();
      this.hud.toast(t('loaded'), 3200);
      this.env.updateSun(this.sim.Clock);
    } catch (err) {
      this.hud.toast(`Load failed: ${err.message}`, 5200);
    }
  }

  // ---- per-frame -----------------------------------------------------------

  resize() {
    const width = window.innerWidth;
    const height = window.innerHeight;
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
  }

  frame() {
    const now = performance.now();
    const dt = Math.min(0.05, (now - (this.lastTime ?? now)) / 1000);
    this.lastTime = now;
    if (!this.started) {
      this.renderIdle(dt);
      return;
    }
    this.update(dt);
    this.renderer.render(this.scene, this.camera);
  }

  /** Before the player takes control: a slow orbit of the colony behind the menu. */
  renderIdle(dt) {
    this.elapsed += dt;
    const t = this.elapsed * 0.06;
    this.camera.position.set(Math.sin(t) * 46, 18 + Math.sin(t * 0.7) * 3, Math.cos(t) * 46 + 6);
    this.camera.lookAt(0, 3, 10);
    this.camera.fov = 55;
    this.camera.updateProjectionMatrix();
    this.env.updateSun(this.sim.Clock);
    this.env.followShadow(new THREE.Vector3(0, 0, 12));
    this.crew.update(dt, {});
    this.renderer.render(this.scene, this.camera);
  }

  update(dt) {
    const input = this.input;

    // ---- global keys -------------------------------------------------------
    if (input.consume(...KEYS.journal)) {
      this.overlayOpen = true;
      this.input.releaseLock();
      this.hud.journalPanel();
    }
    if (input.consume(...KEYS.math)) {
      this.overlayOpen = true;
      this.input.releaseLock();
      this.hud.mathPanel();
    }
    if (input.consume(...KEYS.help)) {
      this.overlayOpen = true;
      this.input.releaseLock();
      this.hud.helpPanel();
    }
    if (input.consume(...KEYS.save)) this.save();
    if (input.consume(...KEYS.load)) this.load();
    if (input.consume(...KEYS.mute)) this.hud.toast(this.audio.toggleMute() ? 'Muted' : 'Sound on', 1800);
    if (input.consume(...KEYS.language)) this.toggleLanguage();
    if (input.consume(...KEYS.view) && !this.overlayOpen) {
      const mode = this.player.toggleView();
      this.hud.toast(mode === 'first' ? 'Helmet view' : 'Third person', 1400);
    }
    if (input.consume(...KEYS.pause)) {
      if (this.hud.openPanelName) {
        this.hud.closePanel();
        this.overlayOpen = false;
        this.audio.duck(false);
        this.input.requestLock();
      } else if (this.paused) {
        this.resume();
      } else {
        this.pause();
      }
    }

    if (this.paused) {
      this.player.update(dt, input, { allowMove: false });
      this.renderer.render(this.scene, this.camera);
      return;
    }

    // ---- simulation --------------------------------------------------------
    const events = this.sim.step(dt);
    for (const event of events) {
      this.hud.pushLog(event);
      if (['critical', 'warning', 'discovery', 'mission'].includes(event.kind)) {
        this.audio.incident(event.id);
      }
    }
    if (events.length) {
      for (const event of events) {
        if (event.kind === 'critical' || event.kind === 'mission') {
          const message = L.arabic ? event.bodyAr : event.bodyEn;
          if (message) this.hud.toast(message, 5200);
        }
      }
      this.hud.renderTasks();
    }

    // ---- world -------------------------------------------------------------
    this.player.update(dt, input);
    this.env.updateSun(this.sim.Clock);
    this.env.followShadow(this.player.position);
    this.crew.update(dt, { playerPosition: this.player.position, anyIncident: this.sim.Incidents.active.length > 0 });
    this.updateFocus();
    this.hud.update(dt);
    this.adaptQuality(dt);

    // Shadows only need refreshing a few times a second: the Sun moves slowly and the crew
    // are the only shadows that move quickly, so every third frame is imperceptible.
    if (this.renderer.shadowMap.enabled) {
      this.shadowTick = (this.shadowTick + 1) % 3;
      this.renderer.shadowMap.needsUpdate = this.shadowTick === 0;
    }

    if (!this.missionEnded && this.sim.Clock.day > 30) {
      this.missionEnded = true;
      this.overlayOpen = true;
      this.paused = true;
      this.input.releaseLock();
      this.hud.missionEndPanel(this.sim.summary(), { onContinue: () => this.resume() });
    }
  }

  /** What the player is looking at: a console, or a crew member, or nothing. */
  updateFocus() {
    this.focus = null;
    this.nearCrew = null;

    this.raycaster.setFromCamera(this.center, this.camera);
    this.raycaster.far = 3.6;
    const meshes = this.consoles.map((c) => c.mesh);
    const hits = this.raycaster.intersectObjects(meshes, false);
    if (hits.length) {
      const hitMesh = hits[0].object;
      const entry = this.consoles.find((c) => c.mesh === hitMesh);
      if (entry) {
        this.focus = entry;
        this.hud.setPrompt(L.arabic ? `اقرأ ${entry.nameAr}` : `Read ${entry.nameEn}`);
        return;
      }
    }

    const crew = this.crew.nearest(this.player.position, 3.4);
    if (crew) {
      this.nearCrew = crew;
      const name = L.arabic ? crew.profile.nameAr : crew.profile.nameEn;
      this.hud.setPrompt(`${L.arabic ? 'تحدّث مع' : 'Talk to'} ${name}`);
      return;
    }

    this.hud.setPrompt(null);
  }

  /** E: use the console in front of you, or talk to the crew member beside you. */
  interact() {
    if (this.overlayOpen || this.paused) return;
    if (this.focus) {
      this.openConsole(this.focus);
      return;
    }
    if (this.nearCrew) {
      this.talkTo(this.nearCrew);
      return;
    }
    this.hud.toast(L.arabic ? 'لا شيء في متناول اليد. اقترب من لوحة.' : 'Nothing in reach. Walk up to a console.', 2400);
  }

  fatal(message) {
    if (this.errorShown) return;
    this.errorShown = true;
    const screen = document.getElementById('fatal');
    const body = document.getElementById('fatal-message');
    if (body) body.textContent = String(message ?? 'Unknown error');
    screen?.classList.remove('hidden');
    document.getElementById('boot')?.classList.add('hidden');
  }
}

// ---- start -----------------------------------------------------------------

const game = new Game();
window.lunaQuest = game;

window.addEventListener('error', (event) => {
  if (event.error) game.fatal(`${event.error.name}: ${event.error.message}\n${event.error.stack ?? ''}`);
});
window.addEventListener('unhandledrejection', (event) => {
  const reason = event.reason;
  game.fatal(reason?.stack ?? reason?.message ?? String(reason));
});

// E is handled on keydown so it works even when a frame is slow.
window.addEventListener('keydown', (event) => {
  if (event.code === 'KeyE' && !event.repeat) game.interact();
  if (event.code === 'Escape' && game.hud?.openPanelName) {
    game.hud.closePanel();
    game.overlayOpen = false;
    game.audio?.duck(false);
    if (!game.paused) game.input?.requestLock();
  }
});

game.boot().catch((err) => game.fatal(err?.stack ?? err?.message ?? String(err)));
