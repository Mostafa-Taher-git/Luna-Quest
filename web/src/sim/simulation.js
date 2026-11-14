/**
 * The colony rules engine for the browser — a faithful port of
 * shared/LunaQuest.Core/Sim/ColonySimulation.cs.
 *
 * It has no rendering or input code. The fixed one-second step makes it deterministic and
 * comparable with the C# core; the save file uses the identical schema-2 keys, so a save
 * written here opens in the Godot build and the other way round.
 */
import { GameClock, MINUTES_PER_SECOND, MINUTES_PER_DAY, MISSION_DAYS } from './clock.js';
import { createState, read, clamp } from './state.js';
import { Power, Air, Water, Food, Crop, Dust, Radiation, Thermal, Comms } from './models.js';
import { IncidentDirector } from './incidents.js';
import { LearningDirector } from './learning.js';
import { NpcDirector } from './npcs.js';

export const FIXED_STEP_SECONDS = 1;

export const PlayerAction = Object.freeze({
  CleanArrays: 'clean_arrays',
  CleanSensors: 'clean_sensors',
  CleanAirlock: 'clean_airlock',
  HarvestCrops: 'harvest_crops',
  CleanFilter: 'clean_filter',
  ReplaceScrubber: 'replace_scrubber',
  RepairRelay: 'repair_relay',
  CollectSample: 'collect_sample',
  ShelterCrew: 'shelter_crew',
  LeaveShelter: 'leave_shelter',
  RestCrew: 'rest_crew',
  RepairArray: 'repair_array',
  InspectCrop: 'inspect_crop',
  BalanceLoad: 'balance_load',
  ToggleGrowLights: 'toggle_grow_lights',
  MedicalCheckup: 'medical_checkup',
  Exercise: 'exercise',
  ProcessRegolith: 'process_regolith',
});

const SAVE_SCHEMA = 2;

/**
 * Interaction id → player action. Ids that are not listed here are observation stations:
 * reading them unlocks their journal objective, which is exactly how the C# core behaves.
 */
export const ACTION_ALIASES = Object.freeze({
  dust_removal: PlayerAction.CleanAirlock,
  eva_prep: PlayerAction.CleanAirlock,
  harvest: PlayerAction.HarvestCrops,
  clean_arrays: PlayerAction.CleanArrays,
  clean_sensors: PlayerAction.CleanSensors,
  clean_airlock: PlayerAction.CleanAirlock,
  clean_filter: PlayerAction.CleanFilter,
  replace_scrubber: PlayerAction.ReplaceScrubber,
  repair_relay: PlayerAction.RepairRelay,
  comms_desk: PlayerAction.RepairRelay,
  relay: PlayerAction.RepairRelay,
  repair_array: PlayerAction.RepairArray,
  antenna: PlayerAction.CleanSensors,
  collect_sample: PlayerAction.CollectSample,
  sample_intake: PlayerAction.CollectSample,
  shelter: PlayerAction.ShelterCrew,
  rest: PlayerAction.RestCrew,
  checkup: PlayerAction.MedicalCheckup,
  exercise: PlayerAction.Exercise,
  intake: PlayerAction.ProcessRegolith,
  crop_monitor: PlayerAction.InspectCrop,
  inspect_crop: PlayerAction.InspectCrop,
  grow_lights: PlayerAction.ToggleGrowLights,
  balance_load: PlayerAction.BalanceLoad,
  repair_o2: 'repair_o2',
});

/** Which action a console can run, if any. Used by the HUD to draw the right button. */
export function actionForInteraction(interactionId) {
  return ACTION_ALIASES[interactionId] ?? null;
}

export class Simulation {
  constructor(content) {
    this.Content = content;
    this.Clock = new GameClock(0);
    this.State = createState();
    this.Npcs = new NpcDirector(content);
    this.Incidents = new IncidentDirector(content.incidents);
    this.Learning = new LearningDirector(content);

    const humans = content.npcs.filter((n) => !n.isRobot).length;
    if (content.npcs.length > 0) this.State.crewCount = 1 + humans;

    this.PlayerName = 'Mostafa';
    this.Language = 'en';
    this.seenEventIds = new Set();
    this.events = [];

    this._accumulator = 0;
    this._incidentAccumulator = 0;
    this._noticeAt = new Map();
    this._spePlayed = false;
    this._lastDay = 1;
    this._firedBeats = new Set();
    /** Extra state the engine wants to persist (player position, camera, speed). */
    this.extras = {};
  }

  get arabic() {
    return this.Language === 'ar';
  }

  /** Advance by real seconds. Returns the events raised during those seconds. */
  step(realSeconds) {
    const events = [];
    if (realSeconds <= 0) return events;

    this._accumulator += realSeconds;
    let guard = 0;
    while (this._accumulator >= FIXED_STEP_SECONDS && guard++ < 600) {
      this._accumulator -= FIXED_STEP_SECONDS;
      this._stepOnce(events);
    }

    this._evaluateNotices(events);
    for (const e of events) this.seenEventIds.add(e.id);
    this.events.push(...events);
    if (this.events.length > 140) this.events.splice(0, this.events.length - 140);
    return events;
  }

  _stepOnce(events) {
    const s = this.State;
    const clock = this.Clock;
    clock.step(FIXED_STEP_SECONDS);
    Power.step(s, clock, FIXED_STEP_SECONDS);
    Water.step(s, clock, FIXED_STEP_SECONDS);
    Air.step(s, clock, FIXED_STEP_SECONDS);
    Thermal.step(s, clock, FIXED_STEP_SECONDS);
    Dust.step(s, clock, FIXED_STEP_SECONDS);
    Crop.step(s, clock, FIXED_STEP_SECONDS);
    Food.step(s, clock, FIXED_STEP_SECONDS);
    Radiation.step(s, clock, FIXED_STEP_SECONDS);
    Comms.step(s, clock, FIXED_STEP_SECONDS);

    this.Npcs.tick(clock.hourOfDay);
    this._updateCrewMood();

    this._incidentAccumulator += FIXED_STEP_SECONDS;
    if (this._incidentAccumulator >= 5) {
      this._incidentAccumulator = 0;
      for (const def of this.Incidents.evaluate(s, clock.totalMinutes)) {
        events.push(incidentToEvent(def, clock.totalMinutes));
      }
    }

    this._maybeSolarParticleEvent(events);
    this._scriptedBeats(events);

    if (clock.day !== this._lastDay) {
      this._lastDay = clock.day;
      events.push({
        id: `day_${this._lastDay}`,
        kind: 'mission',
        titleEn: `Mission day ${this._lastDay} of 30`,
        titleAr: `اليوم ${this._lastDay} من 30`,
        bodyEn: "The Sun moves on. Check power, water and the crew's rest.",
        bodyAr: 'تقدمت الشمس. راجع الطاقة والماء وراحة الطاقم.',
        at: clock.totalMinutes,
      });
    }
  }

  _updateCrewMood() {
    const s = this.State;
    let fatigue = 0;
    let n = 0;
    for (const [id, st] of this.Npcs.states) {
      const prof = this.Content.npc(id);
      if (prof?.isRobot) continue;
      fatigue += st.fatigue;
      n += 1;
    }
    if (n > 0) s.averageFatigue = fatigue / n;

    if (s.cabinTempC < 18) s.averageFatigue = clamp(s.averageFatigue + 0.00012, 0, 1);
    if (read.co2Ppm(s) > 5000) s.averageFatigue = clamp(s.averageFatigue + 0.00008, 0, 1);

    const co2Penalty = Math.min(0.35, Math.max(0, (read.co2Ppm(s) - 1200) / 12000));
    const foodPenalty = read.foodPercent(s) < 0.2 ? 0.15 : 0;
    s.averageMorale = clamp(0.92 - s.averageFatigue * 0.35 - co2Penalty - foodPenalty, 0, 1);
  }

  /**
   * Authored pressure beats. Each is a real operational problem with a known first response,
   * so the mission has a shape without punishing the player at random.
   */
  _scriptedBeats(events) {
    const beat = (id, day, hour, action) => {
      if (this._firedBeats.has(id)) return;
      if (this.Clock.day < day) return;
      if (this.Clock.day === day && this.Clock.hourOfDay < hour) return;
      this._firedBeats.add(id);
      action();
    };
    const clock = this.Clock;
    const s = this.State;

    beat('welcome', 1, 8, () => {
      events.push({
        id: 'welcome',
        kind: 'mission',
        titleEn: 'Day 1 — the colony is yours',
        titleAr: 'اليوم الأول — المستعمرة مسؤوليتك',
        bodyEn: 'Power, water, air, food, heat. Watch them in that order and nothing here surprises you.',
        bodyAr: 'الطاقة، الماء، الهواء، الغذاء، الحرارة. راقبها بهذا الترتيب ولن يفاجئك شيء هنا.',
        at: clock.totalMinutes,
        speaker: 'npc_abdelaziz',
        location: 'habitat',
      });
    });

    beat('dust_front', 3, 10, () => {
      s.arrayDust = Math.min(1, s.arrayDust + 0.18);
      events.push({
        id: 'dust_front',
        kind: 'warning',
        titleEn: 'Dust front crossing the farm',
        titleAr: 'جبهة غبار تعبر المزرعة',
        bodyEn: 'Output is already falling. Clean the arrays before the reserve notices.',
        bodyAr: 'الإنتاج ينخفض بالفعل. نظّف الألواح قبل أن يلاحظ المخزون.',
        at: clock.totalMinutes,
        speaker: 'npc_abdelaziz',
        location: 'power_farm',
      });
    });

    beat('heavy_eva', 9, 6, () => {
      for (const st of this.Npcs.states.values()) st.fatigue = Math.min(1, st.fatigue + 0.3);
      events.push({
        id: 'heavy_eva',
        kind: 'warning',
        titleEn: 'Heavy EVA day',
        titleAr: 'يوم سير فضائي طويل',
        bodyEn: 'Everyone is running on too little sleep. Rebuild the rest plan before the next shift.',
        bodyAr: 'الجميع يعملون بنوم قليل. أعد خطة الراحة قبل المناوبة التالية.',
        at: clock.totalMinutes,
        speaker: 'npc_safaa',
        location: 'medical',
      });
    });

    beat('comms_glitch', 15, 12, () => {
      s.relayHealth = Math.max(0.4, s.relayHealth - 0.3);
      events.push({
        id: 'comms_glitch',
        kind: 'warning',
        titleEn: 'Earth link is dropping packets',
        titleAr: 'رابط الأرض يفقد الحزم',
        bodyEn: 'The antenna is fine. The relay alignment drifted. Fix it before the next data window.',
        bodyAr: 'الهوائي سليم. محاذاة المرحل انحرفت. أصلحها قبل نافذة البيانات التالية.',
        at: clock.totalMinutes,
        speaker: 'npc_nexo',
        location: 'comms_tower',
      });
    });

    beat('dust_storm', 18, 5, () => {
      s.arrayDust = Math.min(1, s.arrayDust + 0.25);
      s.sensorDust = Math.min(1, s.sensorDust + 0.2);
      events.push({
        id: 'dust_storm',
        kind: 'critical',
        titleEn: 'Electrostatic dust event',
        titleAr: 'حدث غبار كهروستاتيكي',
        bodyEn: 'Sharp regolith is coating every exposed surface. Arrays and sensors first.',
        bodyAr: 'ثرى حاد يغطي كل سطح مكشوف. الألواح والحساسات أولًا.',
        at: clock.totalMinutes,
        speaker: 'npc_abdelaziz',
        location: 'power_farm',
      });
    });

    beat('crop_stress', 21, 9, () => {
      s.cropHealth = Math.max(0.3, s.cropHealth - 0.25);
      events.push({
        id: 'crop_stress',
        kind: 'info',
        titleEn: 'A crop batch is stressed',
        titleAr: 'دفعة محاصيل تحت ضغط',
        bodyEn: 'Change one variable, then wait. Do not chase the reading.',
        bodyAr: 'غيّر متغيرًا واحدًا ثم انتظر. لا تطارد القراءة.',
        at: clock.totalMinutes,
        speaker: 'npc_nexo',
        location: 'greenhouse',
      });
    });

    beat('filter_clog', 24, 7, () => {
      s.filterHealth = Math.max(0.2, s.filterHealth - 0.25);
      events.push({
        id: 'filter_clog',
        kind: 'warning',
        titleEn: 'Recycler flow is falling',
        titleAr: 'تدفق التدوير ينخفض',
        bodyEn: 'Recovery is down and the reserve is paying for it. Back-flush the filter.',
        bodyAr: 'الاستعادة انخفضت والمخزون يدفع الثمن. اغسل الفلتر عكسيًا.',
        at: clock.totalMinutes,
        speaker: 'npc_nour',
        location: 'life_support',
      });
    });

    beat('insulation_fault', 27, 10, () => {
      s.insulation = Math.max(0.4, s.insulation - 0.15);
      events.push({
        id: 'insulation_fault',
        kind: 'warning',
        titleEn: 'Habitat shell is losing heat',
        titleAr: 'هيكل الموئل يفقد الحرارة',
        bodyEn: 'A seal is compromised. Heaters can hold it, but they will eat the power budget.',
        bodyAr: 'ختم متضرر. السخانات تستطيع الصمود لكنها ستأكل ميزانية الطاقة.',
        at: clock.totalMinutes,
        speaker: 'npc_mostafa',
        location: 'habitat',
      });
    });

    beat('mission_end', 30, 20, () => {
      events.push({
        id: 'mission_end',
        kind: 'mission',
        titleEn: 'Thirty days. The colony is still alive.',
        titleAr: 'ثلاثون يومًا. المستعمرة ما زالت حية.',
        bodyEn: 'Not because nothing failed, but because someone read the gauge and acted.',
        bodyAr: 'ليس لأن شيئًا لم يفشل، بل لأن أحدهم قرأ المؤشر وتصرف.',
        at: clock.totalMinutes,
        speaker: 'npc_safaa',
        location: 'habitat',
      });
    });
  }

  _maybeSolarParticleEvent(events) {
    const s = this.State;
    const clock = this.Clock;
    if (this._spePlayed) {
      if (s.solarParticleEvent && (clock.day > 12 || clock.hourOfDay >= 15)) {
        s.solarParticleEvent = false;
        s.speIntensity = 0;
        events.push({
          id: 'spe_end',
          kind: 'info',
          titleEn: 'Solar particle event has passed',
          titleAr: 'انتهى حدث الجسيمات الشمسية',
          bodyEn: 'Background radiation is back to the lunar surface baseline.',
          bodyAr: 'عاد الإشعاع الخلفي إلى مستوى سطح القمر الأساسي.',
          at: clock.totalMinutes,
        });
      }
      return;
    }
    if (clock.day === 12 && clock.hourOfDay >= 9) {
      this._spePlayed = true;
      s.solarParticleEvent = true;
      s.speIntensity = 0.8;
      events.push({
        id: 'spe_onset',
        kind: 'critical',
        titleEn: 'Solar particle event — seek shelter',
        titleAr: 'حدث جسيمات شمسية — الاحتماء مطلوب',
        bodyEn: 'Dose rate is rising fast. Get the crew into the shielded section until it passes.',
        bodyAr: 'معدل الجرعة يرتفع بسرعة. أدخل الطاقم إلى القسم المحمي حتى ينتهي الحدث.',
        at: clock.totalMinutes,
        speaker: 'npc_safaa',
        location: 'habitat',
      });
      this.Npcs.state('npc_safaa').memoryTags.push('spe_day_12');
    } else if (s.solarParticleEvent && (clock.day > 12 || clock.hourOfDay >= 15)) {
      s.solarParticleEvent = false;
      s.speIntensity = 0;
    }
  }

  _evaluateNotices(events) {
    const s = this.State;
    const notice = (id, condition, cooldown, kind, titleEn, titleAr, bodyEn, bodyAr, speaker = '', location = '') => {
      if (!condition) return;
      const now = this.Clock.totalMinutes;
      const last = this._noticeAt.get(id);
      if (last !== undefined && now - last < cooldown) return;
      this._noticeAt.set(id, now);
      events.push({ id, kind, titleEn, titleAr, bodyEn, bodyAr, at: now, speaker, location });
    };

    notice(
      'battery_low',
      read.batteryPercent(s) < 0.2,
      240,
      'warning',
      'Battery below 20%',
      'البطارية أقل من 20%',
      'The greenhouse and lab will be shed first. Clean the arrays or wait for sunlight.',
      'سيتم فصل الدفيئة والمختبر أولًا. نظّف الألواح أو انتظر ضوء الشمس.',
      'npc_abdelaziz',
      'power_farm',
    );
    notice(
      'water_low',
      read.waterPercent(s) < 0.15,
      240,
      'warning',
      'Water reserve low',
      'احتياطي الماء منخفض',
      'Recycling recovers most of it, but the filter is the weak point. Inspect it.',
      'التدوير يستعيد معظم الماء، لكن الفلتر هو نقطة الضعف. افحصه.',
      'npc_nour',
      'life_support',
    );
    notice(
      'o2_low',
      read.o2Percent(s) < 0.3,
      180,
      'critical',
      'Oxygen reserve low',
      'احتياطي الأكسجين منخفض',
      'Check the electrolysis load and the scrubber before doing anything else.',
      'افحص حمل التحليل الكهربائي والمرشح قبل أي شيء آخر.',
      'npc_nour',
      'life_support',
    );
    notice(
      'co2_high',
      read.co2Ppm(s) > 5000,
      180,
      'warning',
      'CO₂ is climbing',
      'ثاني أكسيد الكربون يرتفع',
      'The scrubber is losing efficiency. Replace it or reduce crew exertion.',
      'كفاءة المرشح تنخفض. استبدله أو قلل مجهود الطاقم.',
      'npc_nour',
      'life_support',
    );
    notice(
      'temp_low',
      s.cabinTempC < 17,
      240,
      'warning',
      'Cabin temperature falling',
      'حرارة المقصورة تنخفض',
      'Heaters are competing with the lab for power. Rebalance the load.',
      'السخانات تنافس المختبر على الطاقة. أعد موازنة الأحمال.',
      'npc_abdelaziz',
      'power_farm',
    );
    notice(
      'crop_ready',
      Crop.readyToHarvest(s),
      600,
      'info',
      'Greenhouse crop ready',
      'المحصول جاهز',
      'Harvest it in the grow pod; fresh food restores morale and kcal.',
      'احصدها في وحدة النمو؛ الغذاء الطازج يعيد الروح المعنوية والسعرات.',
      'npc_nexo',
      'greenhouse',
    );
    notice(
      'signal_weak',
      s.signalQuality < 0.45,
      360,
      'info',
      'Earth link degraded',
      'رابط الأرض ضعيف',
      'Dust on the antenna or relay wear. Clean the sensors and check the relay.',
      'غبار على الهوائي أو تآكل المرحل. نظّف الحساسات وافحص المرحل.',
      'npc_nexo',
      'comms_tower',
    );
    notice(
      'airlock_dust',
      s.airlockDust > 0.5,
      480,
      'info',
      'Airlock dust is high',
      'غبار الأيرلوك مرتفع',
      'Dust in the seal is a real hazard. Clean the airlock floor before the next EVA.',
      'الغبار في الختم خطر حقيقي. نظّف أرضية الأيرلوك قبل الخروج التالي.',
      'npc_nour',
      'airlock',
    );
  }

  // ---- player actions ----------------------------------------------------

  /**
   * Runs one console action. Returns { ok, en, ar, incident } so the UI can show the result
   * and the log can record what happened.
   */
  tryAction(action) {
    const s = this.State;
    const say = (ok, en, ar) => ({ ok, en, ar });

    switch (action) {
      case PlayerAction.CleanArrays:
        Dust.cleanArrays(s);
        s.arrayHealth = Math.min(1, s.arrayHealth + 0.01);
        this._complete('obj_solar_dust', 'clean_arrays');
        return say(true, 'You brush the regolith off the array. Output recovers.', 'أزلت الثرى عن الألواح. عاد الإنتاج.');

      case PlayerAction.CleanSensors:
        Dust.cleanSensors(s);
        this._complete('obj_sensor_dust', 'clean_sensors');
        return say(true, 'Sensors cleaned. Readings should settle now.', 'نُظفت الحساسات. يجب أن تستقر القراءات الآن.');

      case PlayerAction.CleanAirlock:
        Dust.cleanAirlock(s);
        this._complete('obj_airlock_dust', 'clean_airlock');
        return say(true, 'Airlock floor and seal cleaned. Dust stays outside where it belongs.', 'نُظفت أرضية الأيرلوك والختم. بقي الغبار في الخارج.');

      case PlayerAction.HarvestCrops: {
        const kcal = Crop.harvest(s);
        if (kcal <= 0) return say(false, 'Not ready yet. The crop is still maturing.', 'لم ينضج بعد. المحصول ما زال ينمو.');
        this._complete('obj_plant_care', 'harvest');
        return say(true, `Harvested. +${Math.round(kcal).toLocaleString('en-US')} kcal stored.`, `تم الحصاد. +${Math.round(kcal).toLocaleString('en-US')} سعرة مخزنة.`);
      }

      case PlayerAction.CleanFilter:
        s.filterHealth = Math.min(1, s.filterHealth + 0.45);
        s.waterQuality = Math.min(1, s.waterQuality + 0.06);
        this._complete('obj_water_filter', 'clean_filter');
        return say(true, 'Filter back-flushed. Recovery rate is back up.', 'تم غسل الفلتر عكسيًا. عاد معدل الاستعادة.');

      case PlayerAction.ReplaceScrubber:
        s.scrubberHealth = 1;
        this._complete('obj_air_scrubber', 'replace_scrubber');
        return say(true, 'New CO₂ scrubber installed. Watch the ppm fall.', 'تم تركيب مرشح CO₂ جديد. راقب انخفاض القراءة.');

      case PlayerAction.RepairRelay:
        s.relayHealth = Math.min(1, s.relayHealth + 0.5);
        Comms.step(s, this.Clock, 0);
        this._complete('obj_comms_relay', 'repair_relay');
        return say(true, 'Relay realigned. The next data window should be clean.', 'أُعيدت محاذاة المرحل. نافذة البيانات التالية يجب أن تكون نظيفة.');

      case PlayerAction.CollectSample:
        s.samplesCollected += 1;
        s.dataPoints += 1;
        this._complete('obj_sample', 'collect_sample');
        return say(true, 'Sample logged with its position and mass. One reading is not a deposit.', 'سُجلت العينة مع موقعها وكتلتها. قراءة واحدة ليست راسبًا.');

      case PlayerAction.ShelterCrew:
        s.crewSheltered = true;
        this._complete('obj_radiation', 'shelter');
        return say(true, 'Crew moved into the shielded section. Dose rate falls to a quarter.', 'انتقل الطاقم إلى القسم المحمي. انخفض معدل الجرعة إلى الربع.');

      case PlayerAction.LeaveShelter:
        s.crewSheltered = false;
        return say(true, 'Crew back to normal duties.', 'عاد الطاقم إلى العمل الطبيعي.');

      case PlayerAction.RestCrew:
        for (const st of this.Npcs.states.values()) st.fatigue = Math.max(0, st.fatigue - 0.25);
        s.averageFatigue = Math.max(0, s.averageFatigue - 0.25);
        this._complete('obj_crew_rest', 'rest');
        return say(true, 'Shift rebalanced. The crew can breathe again.', 'أُعيد توزيع المناوبة. استطاع الطاقم أن يتنفس.');

      case PlayerAction.InspectCrop:
        this._complete('obj_plant_care', 'inspect_crop');
        return say(true, 'Light, water, CO₂ and nutrients logged. Change one variable, then wait.', 'سُجل الضوء والماء وثاني أكسيد الكربون والمغذيات. غيّر متغيرًا واحدًا ثم انتظر.');

      case PlayerAction.BalanceLoad:
        s.growLightsOn = false;
        s.growLightsResumeAtMinute = this.Clock.totalMinutes + 360;
        this._complete('obj_power_budget', 'balance_load');
        return say(true, 'Grow lights shed for six hours. Heat and life support keep priority until the battery recovers.', 'فُصلت أضواء النمو ست ساعات. تبقى الحرارة ودعم الحياة لهما الأولوية حتى تتعافى البطارية.');

      case PlayerAction.ToggleGrowLights:
        s.growLightsOn = !s.growLightsOn;
        s.growLightsResumeAtMinute = -1;
        this._complete('obj_power_budget', 'grow_lights');
        return s.growLightsOn
          ? say(true, 'Grow lights on. 2.0 kW from the colony bus, no direct Sun.', 'أضواء النمو تعمل. 2.0 كيلوواط من شبكة المستعمرة، بدون شمس مباشرة.')
          : say(true, 'Grow lights off. The crop slows, but life support keeps its power.', 'أضواء النمو متوقفة. يتباطأ المحصول، لكن دعم الحياة يحتفظ بطاقته.');

      case PlayerAction.MedicalCheckup:
        s.averageFatigue = Math.max(0, s.averageFatigue - 0.1);
        s.averageMorale = Math.min(1, s.averageMorale + 0.03);
        s.suitEnergy = 1;
        for (const st of this.Npcs.states.values()) st.fatigue = Math.max(0, st.fatigue - 0.1);
        this._complete('obj_crew_rest', 'checkup');
        return say(true, 'Check-up logged. Resting heart rates, hydration and dose are within limits.', 'سُجل الفحص. معدلات القلب أثناء الراحة والترطيب والجرعة ضمن الحدود.');

      case PlayerAction.Exercise:
        s.averageFatigue = Math.max(0, s.averageFatigue - 0.05);
        s.averageMorale = Math.min(1, s.averageMorale + 0.04);
        s.suitEnergy = 1;
        this._complete('obj_crew_rest', 'exercise');
        return say(true, 'Resistance session done. Bone and muscle load counters the low gravity.', 'انتهت جلسة المقاومة. الحمل العضلي والعظمي يوازن الجاذبية المنخفضة.');

      case PlayerAction.ProcessRegolith:
        s.o2Kg = Math.min(s.o2CapacityKg, s.o2Kg + 0.2);
        s.dataPoints += 1;
        this._complete('obj_air_scrubber', 'intake');
        return say(true, 'Regolith feed screened. The oxygen plant extracts a measured 0.2 kg of O2.', 'فُحصت تغذية الثرى. يستخلص مصنع الأكسجين 0.2 كغ من الأكسجين.');

      case PlayerAction.RepairArray:
        s.arrayHealth = Math.min(1, s.arrayHealth + 0.12);
        this._complete('obj_solar_dust', 'repair_array');
        return say(true, 'Damaged cover glass replaced on two strings. Array health is back up.', 'استُبدل الزجاج المتضرر في سلسلتين. عادت صحة الألواح.');

      default:
        return say(false, 'Nothing happened.', 'لم يحدث شيء.');
    }
  }

  /** Attempts to resolve any incident whose required action just happened. */
  _complete(objectiveId, action) {
    const entry = this.Learning.unlock(objectiveId, this.Clock.totalMinutes);
    for (const a of this.Incidents.active) {
      if (a.resolved || a.ignored) continue;
      const def = this.Incidents.definition(a.definitionId);
      if (!def) continue;
      if (def.action === action) this.Incidents.resolve(def.id, action);
      else if (def.learning && def.learning === objectiveId) this.Incidents.resolve(def.id, def.action);
    }
    this.Incidents.dismissResolved();
    return entry;
  }

  /**
   * Console entry point, mirroring ColonySimulation.Interact. Unmapped interaction ids are
   * observation stations: reading one unlocks its journal objective.
   */
  interact(buildingId, interactionId) {
    const mapped = ACTION_ALIASES[interactionId];
    if (mapped === 'repair_o2') {
      this.State.o2GeneratorRunning = true;
      this.State.growLightsOn = false;
      this._complete('obj_air_scrubber', 'repair_o2');
      return { ok: true, en: 'Electrolysis restarted; grow lights shed so oxygen comes first.', ar: 'أُعيد تشغيل التحليل الكهربائي؛ فُصلت أضواء النمو ليأتي الأكسجين أولًا.' };
    }
    if (mapped) return this.tryAction(mapped);
    return this._readStation(buildingId, interactionId);
  }

  _readStation(buildingId, interactionId) {
    const building = this.Content.building(buildingId);
    const point = building?.interactions?.find((i) => i.id === interactionId);
    if (!point) return { ok: false, en: 'This station is for observation. Read the panel.', ar: 'هذه المحطة للمراقبة. اقرأ اللوحة.' };

    const objective = point.learning ? this.Content.objective(point.learning) : null;
    if (!objective) {
      return { ok: false, en: `${point.nameEn}. The reading is steady.`, ar: `${point.nameAr}. القراءة مستقرة.` };
    }
    const fresh = this.Learning.unlock(objective.id, this.Clock.totalMinutes);
    this.State.dataPoints += 0.5;
    if (fresh) return { ok: true, en: `${objective.titleEn}. ${objective.bodyEn}`, ar: `${objective.titleAr}. ${objective.bodyAr}` };
    return { ok: true, en: `${point.nameEn}: ${objective.titleEn}.`, ar: `${point.nameAr}: ${objective.titleAr}.` };
  }

  // ---- save contract (schema 2, same keys as the C# core) ------------------

  captureSave(extras = {}) {
    const s = this.State;
    const npcs = {};
    for (const [id, st] of this.Npcs.states) {
      npcs[id] = { trust: st.trust, fatigue: st.fatigue, memory: [...st.memoryTags] };
    }
    const skills = {};
    for (const [k, v] of this.Learning.skillEvidence) skills[k] = v;

    return {
      schemaVersion: SAVE_SCHEMA,
      savedAt: new Date().toISOString(),
      playerName: this.PlayerName,
      language: this.Language,
      clockMinutes: this.Clock.totalMinutes,
      colony: {
        batteryKwh: s.batteryKwh,
        arrayHealth: s.arrayHealth,
        arrayDust: s.arrayDust,
        sensorDust: s.sensorDust,
        airlockDust: s.airlockDust,
        waterL: s.waterL,
        filterHealth: s.filterHealth,
        waterQuality: s.waterQuality,
        o2Kg: s.o2Kg,
        co2Kg: s.co2Kg,
        pressureKpa: s.cabinPressureKpa,
        scrubberHealth: s.scrubberHealth,
        cabinTempC: s.cabinTempC,
        foodKcal: s.foodKcal,
        cropBiomass: s.cropBiomassKg,
        cropHealth: s.cropHealth,
        cropWaterStress: s.cropWaterStress,
        doseMsv: s.cumulativeDoseMsv,
        signal: s.signalQuality,
        relayHealth: s.relayHealth,
        fatigue: s.averageFatigue,
        suitEnergy: s.suitEnergy,
        samples: s.samplesCollected,
        sheltered: s.crewSheltered,
        growLights: s.growLightsOn,
        o2Generator: s.o2GeneratorRunning,
        recycler: s.recyclerRunning,
        spe: s.solarParticleEvent,
        speIntensity: s.speIntensity,
      },
      npcs,
      journal: this.Learning.journal.map((e) => e.objectiveId),
      skills,
      resolved: [],
      ignored: this.Incidents.active.filter((a) => a.ignored).map((a) => a.definitionId),
      extras: { ...this.extras, ...extras, seenEvents: [...this.seenEventIds].slice(-120), firedBeats: [...this._firedBeats] },
    };
  }

  applySave(data) {
    if (!data || typeof data !== 'object') throw new Error('save is not an object');
    const schema = Number(data.schemaVersion ?? 1);
    if (!Number.isFinite(schema) || schema < 1 || schema > SAVE_SCHEMA) {
      throw new Error(`Unsupported save schema ${data.schemaVersion ?? '?'}; expected <= ${SAVE_SCHEMA}.`);
    }
    const num = (v, fallback) => (typeof v === 'number' && Number.isFinite(v) ? v : fallback);
    const bool = (v, fallback) => (typeof v === 'boolean' ? v : fallback);
    const c = data.colony ?? {};
    const s = this.State;

    this.PlayerName = typeof data.playerName === 'string' && data.playerName.trim() ? data.playerName.slice(0, 24) : this.PlayerName;
    this.Language = data.language === 'ar' ? 'ar' : 'en';
    this.Clock.setTotalMinutes(num(data.clockMinutes, 0));

    s.batteryKwh = num(c.batteryKwh, 96);
    s.arrayHealth = num(c.arrayHealth, 1);
    s.arrayDust = num(c.arrayDust, 0.04);
    s.sensorDust = num(c.sensorDust, 0.03);
    s.airlockDust = num(c.airlockDust, 0.05);
    s.waterL = num(c.waterL, 900);
    s.filterHealth = num(c.filterHealth, 1);
    s.waterQuality = num(c.waterQuality, 0.98);
    s.o2Kg = num(c.o2Kg, 83.7);
    s.co2Kg = num(c.co2Kg, 0.35);
    s.cabinPressureKpa = num(c.pressureKpa, 101.3);
    s.scrubberHealth = num(c.scrubberHealth, 1);
    s.cabinTempC = num(c.cabinTempC, 21);
    s.foodKcal = num(c.foodKcal, 482000);
    s.cropBiomassKg = num(c.cropBiomass, 0);
    s.cropHealth = num(c.cropHealth, 1);
    s.cropWaterStress = num(c.cropWaterStress, 0);
    s.cumulativeDoseMsv = num(c.doseMsv, 0);
    s.signalQuality = num(c.signal, 1);
    s.relayHealth = num(c.relayHealth, 1);
    s.averageFatigue = num(c.fatigue, 0);
    s.suitEnergy = num(c.suitEnergy, 1);
    s.samplesCollected = Math.round(num(c.samples, 0));
    s.crewSheltered = bool(c.sheltered, false);
    s.growLightsOn = bool(c.growLights, true);
    s.o2GeneratorRunning = bool(c.o2Generator, true);
    s.recyclerRunning = bool(c.recycler, true);
    s.solarParticleEvent = bool(c.spe, false);
    s.speIntensity = num(c.speIntensity, 0);

    for (const [id, v] of Object.entries(data.npcs ?? {})) {
      const st = this.Npcs.state(id);
      st.trust = num(v?.trust, 0.5);
      st.fatigue = num(v?.fatigue, 0.2);
      st.memoryTags = Array.isArray(v?.memory) ? [...v.memory] : [];
    }

    this.Learning.journal = [];
    this.Learning.skillEvidence = new Map();
    for (const id of data.journal ?? []) this.Learning.unlock(id, this.Clock.totalMinutes);
    for (const [skill, count] of Object.entries(data.skills ?? {})) this.Learning.skillEvidence.set(skill, Math.round(num(count, 0)));

    for (const id of data.resolved ?? []) this.Incidents.markResolved(id, this.Clock.totalMinutes);
    for (const id of data.ignored ?? []) this.Incidents.markResolved(id, this.Clock.totalMinutes);

    this.extras = data.extras ?? {};
    this.seenEventIds = new Set(Array.isArray(this.extras.seenEvents) ? this.extras.seenEvents : []);
    this._firedBeats = new Set(Array.isArray(this.extras.firedBeats) ? this.extras.firedBeats : []);
    this._lastDay = this.Clock.day;
    this._spePlayed = this.Clock.day > 12 || s.solarParticleEvent;
    this._accumulator = 0;
    this._noticeAt = new Map();
    return this;
  }

  /** A compact human-readable summary used by the mission-end card. */
  summary() {
    const s = this.State;
    return {
      day: Math.min(this.Clock.day, Math.ceil(MISSION_DAYS)),
      minutes: this.Clock.totalMinutes,
      journal: this.Learning.journal.length,
      objectives: this.Content.learning.length,
      samples: s.samplesCollected,
      doseMsv: s.cumulativeDoseMsv,
      morale: s.averageMorale,
      fatigue: s.averageFatigue,
      foodDays: Food.daysRemaining(s),
      dataPoints: s.dataPoints,
      simMinutesPerSecond: MINUTES_PER_SECOND,
      missionDays: MISSION_DAYS,
      minutesPerDay: MINUTES_PER_DAY,
    };
  }
}

function incidentToEvent(def, minute) {
  const kind =
    def.priority === 'critical' ? 'critical'
      : def.priority === 'important' ? 'warning'
        : def.kind === 'discovery' ? 'discovery'
          : 'info';
  return {
    id: def.id,
    kind,
    titleEn: def.titleEn,
    titleAr: def.titleAr,
    bodyEn: def.bodyEn,
    bodyAr: def.bodyAr,
    at: minute,
    speaker: def.owner ?? '',
    location: def.location ?? '',
  };
}
