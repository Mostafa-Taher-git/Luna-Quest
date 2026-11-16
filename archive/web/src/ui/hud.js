/**
 * The in-world interface: status gauges, colony log, shift board, interaction prompt,
 * subtitles, and every panel (consoles, journal, resource math, help, pause, mission end).
 *
 * It is deliberately the same layout as the Godot HUD: nine gauges on the left, the log on
 * the right, the board bottom-left, one line of subtitle at the bottom. Bilingual throughout.
 */
import { L, t, fmt } from '../i18n.js';
import { read } from '../sim/state.js';
import { Crop, Food } from '../sim/models.js';
import { actionForInteraction, PlayerAction } from '../sim/simulation.js';

const GAUGE_ORDER = ['pwr', 'wtr', 'o2', 'co2', 'temp', 'food', 'dose', 'link', 'crew'];

export class Hud {
  constructor({ sim, content, onLanguageChange = () => {}, onClose = () => {} }) {
    this.sim = sim;
    this.content = content;
    this.onLanguageChange = onLanguageChange;
    this.onClose = onClose;
    this.el = {
      hud: document.getElementById('hud'),
      clockDay: document.getElementById('clock-day'),
      clockTime: document.getElementById('clock-time'),
      missionProgress: document.getElementById('mission-progress'),
      missionProgressLabel: document.getElementById('mission-progress-label'),
      gauges: document.getElementById('gauges'),
      logList: document.getElementById('log-list'),
      tasks: document.getElementById('task-list'),
      prompt: document.getElementById('prompt'),
      promptText: document.getElementById('prompt-text'),
      toast: document.getElementById('toast'),
      subtitle: document.getElementById('subtitle'),
      speed: document.getElementById('speed'),
      speedLabel: document.getElementById('speed-label'),
      overlay: document.getElementById('overlay'),
      overlayBody: document.getElementById('overlay-body'),
      overlayClose: document.getElementById('overlay-close'),
      langBtn: document.getElementById('lang-toggle-hud'),
    };
    this.openPanelName = null;
    this._gaugeEls = new Map();
    this._toastTimer = 0;
    this._subtitleTimer = 0;
    this._logIds = new Set();
    this._buildGauges();

    this.el.overlayClose.addEventListener('click', () => this.closePanel());
    this.el.overlay.addEventListener('click', (e) => {
      if (e.target === this.el.overlay) this.closePanel();
    });
    this.el.langBtn.addEventListener('click', () => this.onLanguageChange());
  }

  bindSim(sim) {
    this.sim = sim;
    this._logIds.clear();
    this.el.logList.innerHTML = '';
    for (const ev of sim.events.slice(-8)) this.pushLog(ev, { recordOnly: true });
  }

  show() {
    this.el.hud.classList.remove('hidden');
  }

  hide() {
    this.el.hud.classList.add('hidden');
  }

  _buildGauges() {
    const labels = {
      pwr: t('power'),
      wtr: t('water'),
      o2: t('oxygen'),
      co2: t('co2'),
      temp: t('temp'),
      food: t('food'),
      dose: t('dose'),
      link: t('link'),
      crew: t('fatigue'),
    };
    this.el.gauges.innerHTML = '';
    for (const key of GAUGE_ORDER) {
      const row = document.createElement('div');
      row.className = 'gauge';
      row.innerHTML = `<span class="k">${labels[key]}</span><span class="bar"><i></i></span><span class="v">—</span>`;
      this.el.gauges.appendChild(row);
      this._gaugeEls.set(key, { row, bar: row.querySelector('i'), value: row.querySelector('.v') });
    }
  }

  setGauge(key, fraction, text, level = 'ok') {
    const g = this._gaugeEls.get(key);
    if (!g) return;
    const pct = Math.max(0, Math.min(1, fraction));
    g.bar.style.width = `${pct * 100}%`;
    g.bar.style.background = level === 'crit' ? '#ef6a5a' : level === 'warn' ? '#f2a93b' : gaugeColor(key);
    g.value.textContent = text;
    g.row.classList.toggle('warn', level === 'warn');
    g.row.classList.toggle('crit', level === 'crit');
  }

  refreshStatic() {
    this.el.langBtn.textContent = L.arabic ? 'English' : 'العربية';
    document.querySelectorAll('[data-i18n]').forEach((node) => {
      node.textContent = t(node.dataset.i18n);
    });
    this._buildGauges();
  }

  /** Called every frame (cheap: DOM text only changes when values do). */
  update(dt) {
    const sim = this.sim;
    if (!sim) return;
    const s = sim.State;
    const clock = sim.Clock;

    this.el.clockDay.textContent = `Day ${clock.day}`;
    this.el.clockTime.textContent = fmt.time(clock.hourOfDay);
    const progress = Math.min(1, clock.missionProgress);
    this.el.missionProgress.style.width = `${Math.max(3, progress * 100)}%`;
    this.el.missionProgressLabel.textContent = `Day ${Math.min(clock.day, 30)} / 30`;

    const battery = read.batteryPercent(s);
    this.setGauge('pwr', battery, `${fmt.pct(battery)} · ${s.solarKw.toFixed(1)} kW`, battery < 0.2 ? 'crit' : battery < 0.35 ? 'warn' : 'ok');
    const water = read.waterPercent(s);
    this.setGauge('wtr', water, `${Math.round(s.waterL)} / ${Math.round(s.waterCapacityL)} L`, water < 0.15 ? 'crit' : water < 0.3 ? 'warn' : 'ok');
    const o2 = read.o2Percent(s);
    this.setGauge('o2', o2, `${Math.round(s.o2Kg)} kg`, o2 < 0.3 ? 'crit' : o2 < 0.45 ? 'warn' : 'ok');
    const ppm = read.co2Ppm(s);
    this.setGauge('co2', Math.min(1, ppm / 12000), fmt.ppm(ppm), ppm > 5000 ? 'crit' : ppm > 3000 ? 'warn' : 'ok');
    const tempOk = 1 - Math.min(1, Math.abs(s.cabinTempC - 21) / 12);
    this.setGauge('temp', tempOk, fmt.c(s.cabinTempC), s.cabinTempC < 17 ? 'crit' : s.cabinTempC < 19 ? 'warn' : 'ok');
    const food = read.foodPercent(s);
    this.setGauge('food', food, fmt.pct(food), food < 0.15 ? 'crit' : food < 0.3 ? 'warn' : 'ok');
    this.setGauge('dose', Math.min(1, s.doseRateMicroGyH / 200), fmt.dose(s.doseRateMicroGyH), s.doseRateMicroGyH > 80 ? 'crit' : s.doseRateMicroGyH > 40 ? 'warn' : 'ok');
    this.setGauge('link', s.signalQuality, `${Math.round(s.signalQuality * 100)}%`, s.signalQuality < 0.45 ? 'crit' : s.signalQuality < 0.7 ? 'warn' : 'ok');
    this.setGauge('crew', 1 - s.averageFatigue, `${Math.round(s.averageMorale * 100)}%`, s.averageFatigue > 0.65 ? 'crit' : s.averageFatigue > 0.5 ? 'warn' : 'ok');

    if (this._toastTimer > 0) {
      this._toastTimer -= dt;
      if (this._toastTimer <= 0) this.el.toast.classList.add('hidden');
    }
    if (this._subtitleTimer > 0) {
      this._subtitleTimer -= dt;
      if (this._subtitleTimer <= 0) this.el.subtitle.classList.add('hidden');
    }
    if (this.openPanelName === 'console') this._refreshConsoleLive();
  }

  // ---- log / tasks ---------------------------------------------------------

  pushLog(event, { recordOnly = false } = {}) {
    const key = `${event.id}:${Math.round(event.at ?? 0)}`;
    if (this._logIds.has(key)) return;
    this._logIds.add(key);
    if (this._logIds.size > 80) {
      const first = this._logIds.values().next().value;
      this._logIds.delete(first);
    }

    const item = document.createElement('div');
    item.className = `log-item ${event.kind ?? 'info'}`;
    const title = L.arabic ? event.titleAr || event.titleEn : event.titleEn;
    const body = L.arabic ? event.bodyAr || event.bodyEn : event.bodyEn;
    const who = event.speaker ? this.content.npc(event.speaker) : null;
    item.innerHTML = `
      <span class="t">${escapeHtml(title ?? '')}</span>
      <span class="b">${escapeHtml(body ?? '')}</span>
      ${who ? `<span class="who">${escapeHtml(L.arabic ? who.nameAr : who.nameEn)}</span>` : ''}
    `;
    this.el.logList.prepend(item);
    while (this.el.logList.children.length > 14) this.el.logList.lastElementChild.remove();
    if (!recordOnly) {
      item.animate?.([{ opacity: 0, transform: 'translateY(-6px)' }, { opacity: 1, transform: 'none' }], {
        duration: 220,
        easing: 'ease-out',
      });
    }
  }

  /** The current shift. The board is a checklist, not a queue: no shift is mandatory. */
  currentShift() {
    const hour = this.sim.Clock.hourOfDay;
    if (hour >= 6 && hour < 12) {
      return { id: 'morning', en: 'Morning shift - power, water, air', ar: 'مناوبة الصباح - الطاقة والماء والهواء' };
    }
    if (hour >= 12 && hour < 18) {
      return { id: 'afternoon', en: 'Afternoon shift - samples and crops', ar: 'مناوبة الظهيرة - العينات والمحاصيل' };
    }
    if (hour >= 18) {
      return { id: 'evening', en: 'Evening shift - dust, comms, oxygen', ar: 'مناوبة المساء - الغبار والاتصالات والأكسجين' };
    }
    return { id: 'night', en: 'Night shift - rest and radiation watch', ar: 'مناوبة الليل - الراحة ومراقبة الإشعاع' };
  }

  /**
   * What the commander can actually do this shift. Every learning objective in
   * content/learning.json has a station task here, so the board is a real to-do list rather
   * than a handful of milestones. Tasks for the current hour lead; the rest stay reachable.
   */
  taskList() {
    const sim = this.sim;
    const clock = sim.Clock;
    const day = clock.day;
    const unlock = (id) => sim.Learning.hasEntry(id);
    const state = sim.State;
    const shift = this.currentShift().id;
    const all = [
      { id: 'obj_power_budget', shift: 'morning',
        label: L.arabic ? 'اقرأ لوحة المستعمرة في مركز التحكم' : 'Read the colony board in Mission Control',
        done: unlock('obj_power_budget'), hint: 'E at the board · Mission Control' },
      { id: 'obj_solar_dust', shift: 'morning',
        label: L.arabic ? 'نظّف الألواح الشمسية' : 'Clean the solar array when dust cuts output',
        done: unlock('obj_solar_dust'), hint: 'Power farm' },
      { id: 'obj_water_filter', shift: 'morning',
        label: L.arabic ? 'افحص الماء والفلتر' : 'Check the water loop and back-flush the filter',
        done: unlock('obj_water_filter'), hint: 'Life support' },
      { id: 'obj_sample', shift: 'afternoon',
        label: L.arabic ? 'اجمع عينة من التربة في موقع الجليد' : 'Collect a regolith sample at the ice site',
        done: unlock('obj_sample'), hint: 'Ice site · sampler console' },
      { id: 'obj_rover_take', shift: 'afternoon',
        label: L.arabic ? 'خذ مركبة الطاقم من أرضية المرآب' : 'Take the crew rover from the garage apron',
        done: Boolean(this.rover?.hasDriven), hint: 'Garage · E to drive' },
      { id: 'obj_rover_ice', shift: 'afternoon',
        label: L.arabic ? 'قد المركبة إلى موقع استكشاف الجليد' : 'Drive the crew rover out to the ice prospecting site',
        done: Boolean(this.rover?.reachedIce), hint: 'Ice site · 52, -34' },
      { id: 'obj_sensor_dust', shift: 'afternoon',
        label: L.arabic ? 'نظّف رؤوس الحساسات والهوائي' : 'Brush the sensor heads and the antenna',
        done: unlock('obj_sensor_dust'), hint: 'Comms tower' },
      { id: 'obj_plant_care', shift: 'afternoon',
        label: L.arabic ? 'احصد المحصول' : 'Harvest the grow pod',
        done: unlock('obj_plant_care') && Crop.readyToHarvest(state) === false, hint: 'Greenhouse' },
      { id: 'obj_airlock_dust', shift: 'evening',
        label: L.arabic ? 'نظّف غرفة المعادل قبل الخروج التالي' : 'Sweep the airlock dust before the next EVA',
        done: unlock('obj_airlock_dust'), hint: 'Airlock' },
      { id: 'obj_comms_relay', shift: 'evening',
        label: L.arabic ? 'أعد ضبط مرحّل الاتصالات' : 'Realign the comms relay',
        done: unlock('obj_comms_relay'), hint: 'Mission Control · comms desk' },
      { id: 'obj_air_scrubber', shift: 'evening',
        label: L.arabic ? 'أعد تشغيل التحليل الكهربائي وسجّل الغسّال' : 'Restart electrolysis and log the scrubber',
        done: unlock('obj_air_scrubber'), hint: 'Life support' },
      { id: 'obj_crew_rest', shift: 'night',
        label: L.arabic ? 'أعد خطة الراحة للطاقم' : "Rebalance the crew's rest plan",
        done: unlock('obj_crew_rest'), hint: 'Habitat' },
      { id: 'obj_radiation', shift: 'night',
        label: L.arabic ? 'احمِ الطاقم عند حدث الجسيمات' : 'Shelter the crew during a particle event',
        done: unlock('obj_radiation'), hint: 'Habitat · shielded section' },
      { id: 'mission', shift: 'any',
        label: L.arabic ? `أكمل المهمة — اليوم ${Math.min(day, 30)} من 30` : `Survive the mission — day ${Math.min(day, 30)} of 30`,
        done: clock.day > 30, hint: `${Math.round(clock.missionProgress * 100)}%` },
    ];
    return [...all].sort((a, b) => (a.shift === shift ? 0 : 1) - (b.shift === shift ? 0 : 1));
  }

  renderTasks() {
    const tasks = this.taskList();
    const shift = this.currentShift();
    this.el.tasks.innerHTML = '';
    const head = document.createElement('li');
    head.className = 'shift-head';
    head.textContent = L.arabic ? shift.ar : shift.en;
    this.el.tasks.appendChild(head);
    for (const task of tasks.slice(0, 5)) {
      const li = document.createElement('li');
      li.className = task.done ? 'done' : '';
      li.textContent = task.label;
      this.el.tasks.appendChild(li);
    }
  }

  // ---- prompt / toast / subtitles -----------------------------------------

  setPrompt(text) {
    if (!text) {
      this.el.prompt.classList.add('hidden');
      return;
    }
    this.el.promptText.textContent = text;
    this.el.prompt.classList.remove('hidden');
  }

  toast(text, ms = 3600) {
    this.el.toast.innerHTML = escapeHtml(text);
    this.el.toast.classList.remove('hidden');
    this._toastTimer = ms / 1000;
  }

  subtitle(en, ar, seconds = 6) {
    const main = L.arabic ? ar || en : en;
    const second = L.arabic ? en : ar;
    this.el.subtitle.innerHTML = `${escapeHtml(main)}${second ? `<span class="ar">${escapeHtml(second)}</span>` : ''}`;
    this.el.subtitle.classList.remove('hidden');
    this._subtitleTimer = seconds;
  }

  setSpeed(text) {
    if (!text) {
      this.el.speed.classList.add('hidden');
      return;
    }
    this.el.speedLabel.textContent = text;
    this.el.speed.classList.remove('hidden');
  }

  // ---- panels --------------------------------------------------------------

  showPanel(name, html, { wide = false } = {}) {
    this.openPanelName = name;
    this.el.overlayBody.innerHTML = html;
    this.el.overlay.classList.remove('hidden');
    this.el.overlay.querySelector('.overlay-card').style.width = wide ? 'min(980px, 94vw)' : 'min(820px, 92vw)';
    this.el.overlayBody.querySelectorAll('[data-action]').forEach((btn) => {
      btn.addEventListener('click', () => this._onPanelAction(btn.dataset.action, btn.dataset.arg));
    });
  }

  closePanel() {
    this.el.overlay.classList.add('hidden');
    this.el.overlayBody.innerHTML = '';
    this.openPanelName = null;
    this.onClose();
  }

  _onPanelAction(action, arg) {
    if (this.panelHandlers?.[action]) this.panelHandlers[action](arg);
  }

  /** Console panel for one interaction point. */
  consolePanel(buildingId, interactionId) {
    const sim = this.sim;
    const building = this.content.building(buildingId);
    const point = building?.interactions?.find((i) => i.id === interactionId);
    if (!building || !point) return;

    this.consoleContext = { buildingId, interactionId };
    const action = actionForInteraction(interactionId);
    const objective = point.learning ? this.content.objective(point.learning) : null;
    const done = point.learning ? sim.Learning.hasEntry(point.learning) : false;
    const station = this.content.interactions.find((i) => i.buildingId === buildingId && i.id === interactionId);
    const room = building.rooms.find((r) => r.id === point.roomId);

    const title = L.arabic ? point.nameAr : point.nameEn;
    const buildingName = L.arabic ? building.nameAr : building.nameEn;
    const roomName = room ? (L.arabic ? room.nameAr : room.nameEn) : '';

    const readouts = this._readoutsFor(buildingId, interactionId);
    const actionLabel = action && action !== 'repair_o2' ? actionLabelFor(action) : null;

    const html = `
      <div class="kicker">${escapeHtml(buildingName)}${roomName ? ` · ${escapeHtml(roomName)}` : ''}${station ? ` · ${escapeHtml(station.kind)}` : ''}</div>
      <h2>${escapeHtml(title)}</h2>
      <div class="readouts">
        ${readouts
          .map(
            (r) => `<div class="readout ${r.level ?? ''}"><div class="k">${escapeHtml(r.k)}</div><div class="v">${r.v}<br><small>${escapeHtml(r.sub ?? '')}</small></div></div>`,
          )
          .join('')}
      </div>
      ${
        objective
          ? `<h3>${done ? (L.arabic ? 'مسجّل في المفكرة' : 'In the field journal') : L.arabic ? 'الهدف' : 'Objective'}</h3>
             <p>${escapeHtml(L.arabic ? objective.titleAr : objective.titleEn)}</p>
             <p class="ar" style="color:#93a7bd">${escapeHtml(L.arabic ? objective.bodyEn : objective.bodyAr)}</p>`
          : ''
      }
      ${
        actionLabel
          ? `<div class="actions"><button class="action-button" data-action="console-action" data-arg="${escapeHtml(interactionId)}">${escapeHtml(actionLabel.en)}<small>${escapeHtml(actionLabel.hint)}</small></button></div>`
          : `<div class="actions"><button class="action-button" data-action="console-read" data-arg="${escapeHtml(interactionId)}">${L.arabic ? 'اقرأ القراءات' : 'Log the reading'}<small>${L.arabic ? 'يُضاف إلى المفكرة' : 'Adds it to the field journal'}</small></button></div>`
      }
      <p class="start-foot" style="margin-top:16px">${L.arabic ? 'ESC للإغلاق · الأرقام حقيقية، والهدف أن تفهمها' : 'ESC closes the panel. Every number here is a real anchor, not a score.'}</p>
    `;
    this.showPanel('console', html);
  }

  _refreshConsoleLive() {
    if (!this.consoleContext) return;
    const readouts = this._readoutsFor(this.consoleContext.buildingId, this.consoleContext.interactionId);
    const nodes = this.el.overlayBody.querySelectorAll('.readout');
    if (nodes.length !== readouts.length) return;
    readouts.forEach((r, i) => {
      const node = nodes[i];
      if (!node) return;
      node.querySelector('.v').innerHTML = `${r.v}<br><small>${escapeHtml(r.sub ?? '')}</small>`;
      node.className = `readout ${r.level ?? ''}`;
    });
  }

  /** Live numbers, chosen per station. This is what makes a console worth walking to. */
  _readoutsFor(buildingId, interactionId) {
    const s = this.sim.State;
    const clock = this.sim.Clock;
    const out = [];
    const push = (k, v, sub = '', level = '') => out.push({ k, v, sub, level });

    switch (interactionId) {
      case 'board':
      case 'colony_board':
      case 'inventory':
      case 'spares':
      case 'parts':
      case 'network':
        push('Battery', `${Math.round(read.batteryPercent(s) * 100)}%`, `${s.batteryKwh.toFixed(0)} of ${s.batteryCapacityKwh} kWh`, read.batteryPercent(s) < 0.2 ? 'crit' : '');
        push('Solar now', `${s.solarKw.toFixed(1)} kW`, `array health ${Math.round(s.arrayHealth * 100)}%`);
        push('Water', `${Math.round(s.waterL)} L`, `filter ${Math.round(s.filterHealth * 100)}%`);
        push('Food', `${Math.round(Food.daysRemaining(s))} days`, `${Math.round(s.foodKcal).toLocaleString('en-US')} kcal`);
        break;
      case 'solar_weather':
        push('Dose rate', `${s.doseRateMicroGyH.toFixed(1)} µGy/h`, s.solarParticleEvent ? 'SPE in progress' : 'surface baseline', s.solarParticleEvent ? 'crit' : '');
        push('Sun elevation', `${clock.sunElevationDeg.toFixed(2)}°`, `azimuth ${Math.round(clock.sunAzimuthDeg)}°`);
        push('Irradiance', `${Math.round(clock.solarIrradianceFraction * 100)}%`, 'of full solar constant');
        push('Cumulative', `${s.cumulativeDoseMsv.toFixed(2)} mSv`, 'mission to date');
        break;
      case 'comms_desk':
      case 'relay':
        push('Earth link', `${Math.round(s.signalQuality * 100)}%`, s.earthLinkUp ? 'link up' : 'link lost', s.signalQuality < 0.45 ? 'crit' : '');
        push('Relay health', `${Math.round(s.relayHealth * 100)}%`, 'alignment and wear');
        push('Antenna dust', `${Math.round(s.sensorDust * 100)}%`, 'sensor and antenna surfaces');
        push('Light time', '1.3 s', 'one way, Earth–Moon');
        break;
      case 'crop_monitor':
      case 'harvest':
        push('Stage', Crop.stage(s), `${Math.round(s.cropBiomassKg * 100)}% of cycle`);
        push('Crop health', `${Math.round(s.cropHealth * 100)}%`, s.cropWaterStress > 0.4 ? 'water stress' : 'no stress', s.cropHealth < 0.7 ? 'crit' : '');
        push('Light', s.growLightsOn ? '2.0 kW on' : 'off', `${Math.round(s.cropLightHours)} light-hours`);
        push('Harvest value', `${Math.round(450 * 24 * Math.max(0.4, s.cropHealth)).toLocaleString('en-US')} kcal`, '24 m² at 450 kcal/m²');
        break;
      case 'clean_filter':
        push('Filter health', `${Math.round(s.filterHealth * 100)}%`, 'back-flush restores 45%', s.filterHealth < 0.6 ? 'crit' : '');
        push('Recovery', `${Math.round(s.recyclerEfficiency * s.filterHealth * s.waterQuality * 100)}%`, 'of crew water');
        push('Water', `${Math.round(s.waterL)} L`, `net loss ${(11 * s.crewCount * (1 - s.recyclerEfficiency * s.filterHealth * s.waterQuality)).toFixed(1)} L/day`);
        break;
      case 'replace_scrubber':
      case 'repair_o2':
        push('CO₂', `${Math.round(read.co2Ppm(s)).toLocaleString('en-US')} ppm`, read.co2Ppm(s) > 5000 ? 'above warning' : 'within limits', read.co2Ppm(s) > 5000 ? 'crit' : '');
        push('Scrubber', `${Math.round(s.scrubberHealth * 100)}%`, 'life remaining', s.scrubberHealth < 0.55 ? 'crit' : '');
        push('O₂ reserve', `${Math.round(s.o2Kg)} kg`, `${Math.round(read.o2Percent(s) * 100)}% of tank`, read.o2Percent(s) < 0.35 ? 'crit' : '');
        push('Electrolysis', s.o2GeneratorRunning ? 'running' : 'stopped', '2.4 kW when running');
        break;
      case 'clean_arrays':
      case 'repair_array':
        push('Array dust', `${Math.round(s.arrayDust * 100)}%`, 'covers the cover glass', s.arrayDust > 0.35 ? 'crit' : '');
        push('Array health', `${Math.round(s.arrayHealth * 100)}%`, 'physical condition');
        push('Output', `${s.solarKw.toFixed(1)} kW`, `clean would give ${(s.arrayPeakKw * clock.solarIrradianceFraction * s.arrayHealth).toFixed(1)} kW`);
        push('Battery', `${Math.round(read.batteryPercent(s) * 100)}%`, s.labAndGreenhousePowered ? 'lab + greenhouse powered' : 'load shed');
        break;
      case 'clean_sensors':
      case 'antenna':
        push('Sensor dust', `${Math.round(s.sensorDust * 100)}%`, 'corrupts readings', s.sensorDust > 0.4 ? 'crit' : '');
        push('Signal', `${Math.round(s.signalQuality * 100)}%`, 'Earth link');
        push('Airlock dust', `${Math.round(s.airlockDust * 100)}%`, 'seal hazard', s.airlockDust > 0.5 ? 'crit' : '');
        break;
      case 'dust_removal':
      case 'eva_prep':
        push('Airlock dust', `${Math.round(s.airlockDust * 100)}%`, 'sharp regolith in the seal', s.airlockDust > 0.45 ? 'crit' : '');
        push('Suit energy', `${Math.round(s.suitEnergy * 100)}%`, 'commander PLSS');
        push('Air pressure', `${s.cabinPressureKpa.toFixed(1)} kPa`, `${Math.round((s.cabinPressureKpa / 101.3) * 100)}% of nominal`);
        break;
      case 'rest':
        push('Crew fatigue', `${Math.round(s.averageFatigue * 100)}%`, 'across six crew', s.averageFatigue > 0.6 ? 'crit' : '');
        push('Morale', `${Math.round(s.averageMorale * 100)}%`, 'rest, air, food');
        push('Shift', `${Math.round(clock.hourOfDay)}:00`, 'rest plan rotates every 7 hours');
        break;
      case 'shelter':
      case 'status':
        push('Dose rate', `${s.doseRateMicroGyH.toFixed(1)} µGy/h`, s.crewSheltered ? 'crew sheltered' : 'crew exposed', s.doseRateMicroGyH > 80 ? 'crit' : '');
        push('Shelter factor', '×0.25', 'regolith berms plus the shielded section');
        push('Cumulative', `${s.cumulativeDoseMsv.toFixed(2)} mSv`, 'mission to date');
        break;
      case 'checkup':
      case 'dose_log':
        push('Commander', `${Math.round(s.suitEnergy * 100)}%`, 'hydration and rest');
        push('Crew fatigue', `${Math.round(s.averageFatigue * 100)}%`, 'mean across the crew');
        push('Cumulative dose', `${s.cumulativeDoseMsv.toFixed(2)} mSv`, 'limit is a guideline, not a score');
        break;
      case 'grow_lights':
      case 'balance_load':
        push('Battery', `${Math.round(read.batteryPercent(s) * 100)}%`, `${(s.solarKw - 12.4 - (s.o2GeneratorRunning ? 2.4 : 0) - (s.growLightsOn ? 2 : 0) - s.heaterKw).toFixed(1)} kW net`);
        push('Grow lights', s.growLightsOn ? 'on · 2.0 kW' : 'off', 'first load to be shed');
        push('Heater', `${s.heaterKw} kW`, `cabin ${s.cabinTempC.toFixed(1)} °C`);
        push('Shed state', s.labAndGreenhousePowered ? 'nothing shed' : 'lab shed', 'life support always first');
        break;
      case 'collect_sample':
      case 'sample_intake':
      case 'microscope':
      case 'ice_probe':
      case 'output':
      case 'cache_log':
      case 'clean_bench':
        push('Samples', `${s.samplesCollected}`, 'each one measured, not guessed');
        push('Data points', `${s.dataPoints.toFixed(1)}`, 'observation credit');
        push('Water table', `${Math.round(s.waterL)} L`, 'ice becomes drinking water and O₂');
        break;
      case 'intake':
        push('O₂ reserve', `${Math.round(s.o2Kg)} kg`, `+0.2 kg per screened batch`);
        push('Regolith feed', 'screened', 'ilmenite reduction needs heat and power');
        push('Pressure', `${s.cabinPressureKpa.toFixed(1)} kPa`, 'cabin seal holds');
        break;
      case 'repair_array':
        push('Array health', `${Math.round(s.arrayHealth * 100)}%`, 'cover glass and strings');
        break;
      default:
        push('Mission time', `Day ${clock.day}`, fmt.time(clock.hourOfDay));
        push('Battery', `${Math.round(read.batteryPercent(s) * 100)}%`, '');
        push('Water', `${Math.round(s.waterL)} L`, '');
        push('CO₂', `${Math.round(read.co2Ppm(s))} ppm`, '');
    }
    return out.slice(0, 4);
  }

  // ---- full-screen reference panels ---------------------------------------

  journalPanel() {
    const sim = this.sim;
    const entries = sim.Learning.journal;
    const objectives = this.content.learning;
    const tasks = this.taskList();

    const html = `
      <div class="kicker">${L.arabic ? 'المفكرة الميدانية' : 'FIELD JOURNAL'}</div>
      <h2>${L.arabic ? 'ما فعلته، وما تعلّمته' : 'What you did, and what it taught'}</h2>
      <p>${L.arabic
        ? 'كل هدف يُسجّل بمجرد أن تنفّذ العمل الفعلي على المحطة. لا اختبارات، ولا أسئلة.'
        : 'An objective is recorded the moment you do the real work at a station. No quizzes, no multiple choice — the evidence is the action.'}</p>
      <h3>${L.arabic ? 'التحقيق' : 'Progress'} · ${entries.length} / ${objectives.length}</h3>
      <ul class="objective-list">
        ${objectives
          .map((o) => {
            const entry = entries.find((e) => e.objectiveId === o.id);
            const title = L.arabic ? o.titleAr : o.titleEn;
            const body = L.arabic ? o.bodyAr : o.bodyEn;
            return `<li class="objective ${entry ? 'done' : ''}">
              <div class="t">${escapeHtml(title)}</div>
              ${entry ? `<div class="b">${escapeHtml(body)}</div>` : `<div class="b">${L.arabic ? 'لم يُسجّل بعد.' : 'Not recorded yet.'}</div>`}
              <div class="meta">${escapeHtml(o.skill ?? '')} · ${escapeHtml(o.evidenceClass ?? '')} ${entry ? `· <b>${L.arabic ? 'مسجّل' : 'recorded'}</b>` : ''}</div>
            </li>`;
          })
          .join('')}
      </ul>
      <h3>${L.arabic ? 'لوحة المناوبة' : 'Shift board'}</h3>
      <ul class="objective-list">
        ${tasks
          .map(
            (task) => `<li class="objective ${task.done ? 'done' : ''}">
              <div class="t">${escapeHtml(task.label)}</div>
              <div class="meta">${escapeHtml(task.hint ?? '')}</div>
            </li>`,
          )
          .join('')}
      </ul>
      <p class="start-foot">J ${L.arabic ? 'يفتح هذه المفكرة · ESC للإغلاق' : 'opens this journal · ESC closes it'}</p>
    `;
    this.showPanel('journal', html, { wide: true });
  }

  mathPanel() {
    const s = this.sim.State;
    const clock = this.sim.Clock;
    const rows = [
      ['Time', '1 real second = 12 simulated minutes; one mission day = 120 real seconds; 30 days = 60 minutes.'],
      ['Power', `${s.arrayPeakKw} kW array peak · base load ${s.baseLoadKw} kW · heater ${s.heaterKw} kW · electrolysis ${s.o2GeneratorKw} kW · grow lights 2.0 kW`],
      ['Solar now', `${s.solarKw.toFixed(2)} kW from ${Math.round(clock.solarIrradianceFraction * 100)}% irradiance, ${Math.round(s.arrayHealth * 100)}% health, ${Math.round(s.arrayDust * 100)}% dust`],
      ['Battery', `${s.batteryKwh.toFixed(1)} / ${s.batteryCapacityKwh} kWh (${Math.round(read.batteryPercent(s) * 100)}%)`],
      ['Air', `${Math.round(s.o2Kg)} kg O₂ (${Math.round(read.o2Percent(s) * 100)}%) · ${Math.round(read.co2Ppm(s))} ppm CO₂ · ${s.cabinPressureKpa.toFixed(1)} kPa`],
      ['O₂ demand', `${(0.84 * s.crewCount).toFixed(2)} kg/day for ${s.crewCount} people; the stack makes 90% of that, plants add a little`],
      ['Water', `${Math.round(s.waterL)} L · recovery ${Math.round(s.recyclerEfficiency * 100)}% × filter ${Math.round(s.filterHealth * 100)}% × quality ${Math.round(s.waterQuality * 100)}%`],
      ['Food', `${Math.round(s.foodKcal).toLocaleString('en-US')} kcal · ${Food.daysRemaining(s).toFixed(1)} days at ${2800 * s.crewCount} kcal/day`],
      ['Crops', `${Math.round(s.cropBiomassKg * 100)}% of a 24-day lettuce cycle · harvest ≈ 10,800 kcal`],
      ['Thermal', `cabin ${s.cabinTempC.toFixed(1)} °C · outside ${s.externalTempC.toFixed(0)} °C · insulation ${Math.round(s.insulation * 100)}%`],
      ['Radiation', `${s.doseRateMicroGyH.toFixed(1)} µGy/h · cumulative ${s.cumulativeDoseMsv.toFixed(2)} mSv`],
      ['Crew', `${s.crewCount} people + NEXO · fatigue ${Math.round(s.averageFatigue * 100)}% · morale ${Math.round(s.averageMorale * 100)}%`],
    ];
    const html = `
      <div class="kicker">${L.arabic ? 'حساب الموارد' : 'RESOURCE MATH'}</div>
      <h2>${L.arabic ? 'من أين تأتي كل الأرقام' : 'Where every number comes from'}</h2>
      <p>${L.arabic
        ? 'هذه الحسابات نفسها تعمل في نسخة Godot ونسخة Unity. الحساب محسوم كل ثانية.'
        : 'The same arithmetic runs in the Godot and Unity builds. The step is fixed at one simulated second, so the colony behaves identically in all three.'}</p>
      <div class="readouts" style="grid-template-columns:1fr">
        ${rows.map(([k, v]) => `<div class="readout"><div class="k">${escapeHtml(k)}</div><div class="v" style="font-family:var(--font);font-size:12.5px;line-height:1.55">${escapeHtml(v)}</div></div>`).join('')}
      </div>
      <p class="start-foot">${L.arabic ? 'G يفتح هذه الصفحة · ESC للإغلاق' : 'G opens this page · ESC closes it'}</p>
    `;
    this.showPanel('math', html, { wide: true });
  }

  /** Crew conversation: greeting, activity and the approved topic list for that person. */
  crewPanel({ profile, state, topics, onTopic }) {
    const name = L.arabic ? profile.nameAr : profile.nameEn;
    const role = L.arabic ? profile.roleAr : profile.roleEn;
    const activity = state ? (L.arabic ? state.activityAr : state.activityEn) : '';
    const greeting = L.arabic ? profile.greetingAr : profile.greetingEn;
    const boundary = L.arabic ? profile.boundaryAr : profile.boundaryEn;
    const story = L.arabic ? profile.storySeedAr : profile.storySeedEn;

    this.panelHandlers = {
      ...(this.panelHandlers ?? {}),
      'crew-topic': (id) => onTopic?.(id),
    };

    const html = `
      <div class="kicker">${escapeHtml(role)}${activity ? ` · ${escapeHtml(activity)}` : ''}</div>
      <h2>${escapeHtml(name)}</h2>
      <p>${escapeHtml(greeting || '')}</p>
      ${story ? `<p style="color:#9db2c8">${escapeHtml(story)}</p>` : ''}
      <h3>${L.arabic ? 'اسأل عن' : 'Ask about'}</h3>
      <div class="actions">
        ${topics
          .map(
            (topic) =>
              `<button class="action-button" data-action="crew-topic" data-arg="${escapeHtml(topic.id)}">${escapeHtml(L.arabic ? topic.labelAr : topic.labelEn)}<small>${L.arabic ? 'اسمع ثم اقرأ الترجمة' : 'Spoken line, with a subtitle'}</small></button>`,
          )
          .join('')}
      </div>
      ${boundary ? `<p class="start-foot" style="margin-top:16px">${escapeHtml(boundary)}</p>` : ''}
    `;
    this.showPanel('crew', html);
  }

  helpPanel() {
    const controls = [
      ['W A S D', L.arabic ? 'المشي' : 'Walk'],
      ['Shift', L.arabic ? 'الجري' : 'Run'],
      ['Space', L.arabic ? 'قفزة قمرية' : 'Jump (lunar, 1.62 m/s²)'],
      ['Mouse', L.arabic ? 'النظر' : 'Look'],
      ['E', L.arabic ? 'استخدام المحطة' : 'Use the console you face'],
      ['V', L.arabic ? 'منظور أول / ثالث' : 'First / third person'],
      ['J', L.arabic ? 'المفكرة' : 'Field journal'],
      ['G', L.arabic ? 'حساب الموارد' : 'Resource math'],
      ['H', L.arabic ? 'المساعدة' : 'This help'],
      ['F5 / F9', L.arabic ? 'حفظ / تحميل' : 'Save / load (this browser)'],
      ['L', L.arabic ? 'العربية / English' : 'English / العربية'],
      ['M', L.arabic ? 'كتم الصوت' : 'Mute'],
      ['Esc', L.arabic ? 'إغلاق / إيقاف' : 'Close a panel / pause'],
    ];
    const html = `
      <div class="kicker">${L.arabic ? 'كيف تلعب' : 'HOW TO PLAY'}</div>
      <h2>${L.arabic ? 'اقرأ، ثم تصرّف' : 'Read the number, then act'}</h2>
      <p>${L.arabic
        ? 'لا قتال ولا فشل دائم. الضغط يأتي من الهواء والماء والطاقة والغذاء والحرارة والغبار والوقت. كل محطة تخبرك بالرقم وتشرح سببه، والقرار لك.'
        : 'No combat, no permanent failure. The pressure comes from air, water, power, food, heat, dust and time. Every console shows the number and explains its cause; the decision is yours.'}</p>
      <div class="help-grid">
        ${controls.map(([k, v]) => `<b>${escapeHtml(k)}</b><span>${escapeHtml(v)}</span>`).join('')}
      </div>
      <h3>${L.arabic ? 'الطاقم' : 'The crew'}</h3>
      <p>${this.content.npcs
        .map((n) => `${escapeHtml(L.arabic ? n.nameAr : n.nameEn)} — ${escapeHtml(L.arabic ? n.roleAr : n.roleEn)}`)
        .join('<br>')}</p>
      <p class="start-foot">${L.arabic
        ? 'هذه محاكاة تعليمية بأرقام حقيقية، وليست خطة مهمة حقيقية.'
        : 'This is a teaching model built on real numbers. It is not a real mission plan.'}</p>
    `;
    this.showPanel('help', html);
  }

  pausePanel({ onResume }) {
    this.panelHandlers = {
      ...(this.panelHandlers ?? {}),
      resume: () => {
        this.closePanel();
        onResume?.();
      },
      save: () => this.panelHandlers.__save?.(),
      load: () => this.panelHandlers.__load?.(),
      journal: () => this.journalPanel(),
      help: () => this.helpPanel(),
    };
    const html = `
      <div class="kicker">${L.arabic ? 'متوقف مؤقتًا' : 'PAUSED'}</div>
      <h2>${L.arabic ? 'المستعمرة تتوقف معك' : 'The colony waits with you'}</h2>
      <p>${L.arabic
        ? 'الساعة والمحاكاة متوقفتين تمامًا. لا يستهلك الطاقم شيئًا أثناء الإيقاف.'
        : 'The clock and every resource step are stopped. Nothing is consumed while you are here.'}</p>
      <div class="actions">
        <button class="action-button" data-action="resume">${L.arabic ? 'متابعة' : 'Resume'} <small>Esc</small></button>
        <button class="action-button" data-action="save">${L.arabic ? 'حفظ' : 'Save'} <small>F5</small></button>
        <button class="action-button" data-action="load">${L.arabic ? 'تحميل' : 'Load'} <small>F9</small></button>
        <button class="action-button" data-action="journal">${L.arabic ? 'المفكرة' : 'Journal'} <small>J</small></button>
        <button class="action-button" data-action="help">${L.arabic ? 'المساعدة' : 'Help'} <small>H</small></button>
      </div>
    `;
    this.showPanel('pause', html);
  }

  missionEndPanel(summary, { onContinue }) {
    this.panelHandlers = {
      ...(this.panelHandlers ?? {}),
      resume: () => {
        this.closePanel();
        onContinue?.();
      },
    };
    const html = `
      <div class="kicker">${L.arabic ? 'نهاية المهمة' : 'MISSION COMPLETE'}</div>
      <h2>${L.arabic ? 'ثلاثون يومًا، والمستعمرة حية' : 'Thirty days, and the colony is alive'}</h2>
      <p>${L.arabic
        ? 'ليس لأن شيئًا لم يفشل، بل لأن أحدًا قرأ المؤشر وتصرّف.'
        : 'Not because nothing failed — because someone read the gauge and acted.'}</p>
      <div class="readouts">
        <div class="readout"><div class="k">${L.arabic ? 'الأهداف' : 'Objectives'}</div><div class="v">${summary.journal} / ${summary.objectives}</div></div>
        <div class="readout"><div class="k">${L.arabic ? 'العينات' : 'Samples'}</div><div class="v">${summary.samples}</div></div>
        <div class="readout"><div class="k">${L.arabic ? 'الجرعة' : 'Dose'}</div><div class="v">${summary.doseMsv.toFixed(2)} <small>mSv</small></div></div>
        <div class="readout"><div class="k">${L.arabic ? 'الروح المعنوية' : 'Morale'}</div><div class="v">${Math.round(summary.morale * 100)}%</div></div>
        <div class="readout"><div class="k">${L.arabic ? 'الغذاء' : 'Food'}</div><div class="v">${summary.foodDays.toFixed(1)} <small>days</small></div></div>
        <div class="readout"><div class="k">${L.arabic ? 'نقاط البيانات' : 'Data points'}</div><div class="v">${summary.dataPoints.toFixed(1)}</div></div>
      </div>
      <div class="actions"><button class="action-button" data-action="resume">${L.arabic ? 'أكمل الاستكشاف' : 'Keep exploring'} <small>Esc</small></button></div>
    `;
    this.showPanel('mission', html, { wide: true });
  }
}

function gaugeColor(key) {
  switch (key) {
    case 'pwr':
    case 'food':
      return 'linear-gradient(90deg,#f2a93b,#ffcf86)';
    case 'wtr':
      return 'linear-gradient(90deg,#3aa7d8,#7fd7f5)';
    case 'o2':
      return 'linear-gradient(90deg,#3fd0c2,#8ef0e4)';
    case 'co2':
      return 'linear-gradient(90deg,#b073e0,#e0a7f5)';
    case 'temp':
      return 'linear-gradient(90deg,#e07a5a,#ffb28c)';
    case 'dose':
      return 'linear-gradient(90deg,#e05a7a,#ff9ab0)';
    case 'link':
      return 'linear-gradient(90deg,#63d08f,#a8f2c4)';
    default:
      return 'linear-gradient(90deg,#58c6e8,#9fe4f7)';
  }
}

function actionLabelFor(action) {
  const labels = {
    [PlayerAction.CleanArrays]: { en: 'Clean the array', hint: 'Brushes the regolith off; output recovers at once' },
    [PlayerAction.CleanSensors]: { en: 'Clean the sensors', hint: 'Settles the readings and the antenna' },
    [PlayerAction.CleanAirlock]: { en: 'Clean the airlock', hint: 'Removes dust from the seal' },
    [PlayerAction.CleanFilter]: { en: 'Back-flush the filter', hint: 'Restores most of the water recovery' },
    [PlayerAction.ReplaceScrubber]: { en: 'Replace the CO₂ scrubber', hint: 'Full life, and the ppm starts falling' },
    [PlayerAction.RepairRelay]: { en: 'Realign the relay', hint: 'Recovers the Earth link' },
    [PlayerAction.RepairArray]: { en: 'Repair the array', hint: 'Replaces damaged cover glass' },
    [PlayerAction.HarvestCrops]: { en: 'Harvest the pod', hint: 'Adds about 10,800 kcal when ready' },
    [PlayerAction.CollectSample]: { en: 'Take a measured sample', hint: 'Position, mass and a repeat reading' },
    [PlayerAction.ShelterCrew]: { en: 'Shelter the crew', hint: 'Cuts the dose rate to a quarter' },
    [PlayerAction.RestCrew]: { en: 'Rebalance the rest plan', hint: 'Cuts crew fatigue by a quarter' },
    [PlayerAction.MedicalCheckup]: { en: 'Run a check-up', hint: 'Rest, hydration and dose logged' },
    [PlayerAction.Exercise]: { en: 'Resistance session', hint: 'Bone and muscle load against 1.62 m/s²' },
    [PlayerAction.BalanceLoad]: { en: 'Shed the grow lights', hint: 'Six hours; heat and life support keep priority' },
    [PlayerAction.ToggleGrowLights]: { en: 'Toggle the grow lights', hint: '2.0 kW either way' },
    [PlayerAction.ProcessRegolith]: { en: 'Screen a regolith batch', hint: '0.2 kg of oxygen per batch' },
    [PlayerAction.InspectCrop]: { en: 'Log the crop readings', hint: 'Light, water, CO₂, nutrients' },
  };
  return labels[action] ?? null;
}

export function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
