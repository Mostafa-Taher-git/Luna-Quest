using System;
using System.Collections.Generic;
using LunaQuest.Core.Content;
using LunaQuest.Core.Domain;
using LunaQuest.Core.Time;

namespace LunaQuest.Core.Sim
{
    public enum PlayerAction
    {
        CleanArrays,
        CleanSensors,
        CleanAirlock,
        HarvestCrops,
        CleanFilter,
        ReplaceScrubber,
        RepairRelay,
        CollectSample,
        ShelterCrew,
        LeaveShelter,
        RestCrew,
        RepairArray,
        InspectCrop,
        BalanceLoad,
        ToggleGrowLights,
        MedicalCheckup,
        Exercise,
        ProcessRegolith
    }

    /// <summary>
    /// The whole game rules engine. It has no engine types: Unity and Godot feed it real seconds
    /// and read back state and events. Fixed 1-second steps make it deterministic and testable.
    /// </summary>
    public sealed class ColonySimulation
    {
        public const double FixedStepSeconds = 1.0;

        public GameClock Clock { get; } = new GameClock();
        public ColonyState State { get; } = new ColonyState();
        public ContentDatabase Content { get; }
        public NpcDirector Npcs { get; }
        public IncidentDirector Incidents { get; }
        public LearningDirector Learning { get; }

        public string PlayerName = "Amara Okafor";
        public string Language = "en";
        public bool Arabic => Language == "ar";
        /// <summary>Every event id ever emitted, for the journal and the mission summary.</summary>
        public readonly HashSet<string> SeenEventIds = new HashSet<string>();

        private double _accumulator;
        private double _incidentAccumulator;
        private readonly Dictionary<string, double> _noticeAt = new Dictionary<string, double>();
        private bool _spePlayed;
        private int _lastDay = 1;
        private readonly HashSet<string> _firedBeats = new HashSet<string>();

        public ColonySimulation(ContentDatabase content)
        {
            Content = content;
            Npcs = new NpcDirector(content);
            Incidents = new IncidentDirector(content.Incidents);
            Learning = new LearningDirector(content);
        }

        // ---- main loop -------------------------------------------------------

        public List<SimEvent> Step(double realSeconds)
        {
            var events = new List<SimEvent>();
            if (realSeconds <= 0) return events;

            _accumulator += realSeconds;
            int guard = 0;
            while (_accumulator >= FixedStepSeconds && guard++ < 600)
            {
                _accumulator -= FixedStepSeconds;
                StepOnce(events);
            }

            // UI-level notices are evaluated every frame so they feel immediate.
            EvaluateNotices(events);
            foreach (var e in events) SeenEventIds.Add(e.Id);
            return events;
        }

        private void StepOnce(List<SimEvent> events)
        {
            Clock.Step(FixedStepSeconds);
            PowerModel.Step(State, Clock, FixedStepSeconds);
            WaterModel.Step(State, Clock, FixedStepSeconds);
            AirModel.Step(State, Clock, FixedStepSeconds);
            ThermalModel.Step(State, Clock, FixedStepSeconds);
            DustModel.Step(State, Clock, FixedStepSeconds);
            CropModel.Step(State, Clock, FixedStepSeconds);
            FoodModel.Step(State, Clock, FixedStepSeconds);
            RadiationModel.Step(State, Clock, FixedStepSeconds);
            CommsModel.Step(State, Clock, FixedStepSeconds);

            Npcs.Tick(Clock.HourOfDay);
            UpdateCrewMood();

            _incidentAccumulator += FixedStepSeconds;
            if (_incidentAccumulator >= 5.0)
            {
                _incidentAccumulator = 0;
                foreach (var def in Incidents.Evaluate(State, Clock.TotalMinutes))
                    events.Add(IncidentToEvent(def));
            }

            MaybeSolarParticleEvent(events);
            ScriptedBeats(events);

            if (Clock.Day != _lastDay)
            {
                _lastDay = Clock.Day;
                events.Add(SimEvent.Make(
                    $"day_{_lastDay}", SimEventKind.Mission,
                    $"Mission day {_lastDay} of 30",
                    $"اليوم {_lastDay} من 30",
                    "The Sun moves on. Check power, water and the crew's rest.",
                    "تقدمت الشمس. راجع الطاقة والماء وراحة الطاقم.",
                    Clock.TotalMinutes));
            }
        }

        private void UpdateCrewMood()
        {
            double fatigue = 0.0;
            int n = 0;
            foreach (var st in Npcs.States.Values) { fatigue += st.Fatigue; n++; }
            if (n > 0) State.AverageFatigue = fatigue / n;

            // Cold and stale air wear the crew down; this is how a failing system becomes a
            // human problem instead of just a gauge.
            if (State.CabinTempC < 18.0) State.AverageFatigue = Clamp(State.AverageFatigue + 0.00012, 0.0, 1.0);
            if (State.Co2Ppm > 5000.0) State.AverageFatigue = Clamp(State.AverageFatigue + 0.00008, 0.0, 1.0);

            // Morale follows rest, air quality and food. No punishing spiral.
            double co2Penalty = Math.Min(0.35, Math.Max(0.0, (State.Co2Ppm - 1200.0) / 12000.0));
            double foodPenalty = State.FoodPercent < 0.2 ? 0.15 : 0.0;
            State.AverageMorale = Clamp(0.92 - State.AverageFatigue * 0.35 - co2Penalty - foodPenalty, 0.0, 1.0);
        }

        /// <summary>
        /// Authored pressure beats from the mission timeline. Each one is a real operational
        /// problem with a known first response, not random punishment.
        /// </summary>
        private void ScriptedBeats(List<SimEvent> events)
        {
            Beat("welcome", 1, 8.0, events, () =>
            {
                events.Add(SimEvent.Make("welcome", SimEventKind.Mission,
                    "Day 1 — the colony is yours",
                    "اليوم الأول — المستعمرة مسؤوليتك",
                    "Power, water, air, food, heat. Watch them in that order and nothing here surprises you.",
                    "الطاقة، الماء، الهواء، الغذاء، الحرارة. راقبها بهذا الترتيب ولن يفاجئك شيء هنا.",
                    Clock.TotalMinutes, "npc_abdelaziz", "habitat"));
            });

            Beat("dust_front", 3, 10.0, events, () =>
            {
                State.ArrayDust = Math.Min(1.0, State.ArrayDust + 0.18);
                events.Add(SimEvent.Make("dust_front", SimEventKind.Warning,
                    "Dust front crossing the farm",
                    "جبهة غبار تعبر المزرعة",
                    "Output is already falling. Clean the arrays before the reserve notices.",
                    "الإنتاج ينخفض بالفعل. نظّف الألواح قبل أن يلاحظ المخزون.",
                    Clock.TotalMinutes, "npc_abdelaziz", "power_farm"));
            });

            Beat("heavy_eva", 9, 6.0, events, () =>
            {
                foreach (var st in Npcs.States.Values) st.Fatigue = Math.Min(1.0, st.Fatigue + 0.30);
                events.Add(SimEvent.Make("heavy_eva", SimEventKind.Warning,
                    "Heavy EVA day",
                    "يوم سير فضائي طويل",
                    "Everyone is running on too little sleep. Rebuild the rest plan before the next shift.",
                    "الجميع يعملون بنوم قليل. أعد خطة الراحة قبل المناوبة التالية.",
                    Clock.TotalMinutes, "npc_safaa", "medical"));
            });

            Beat("comms_glitch", 15, 12.0, events, () =>
            {
                State.RelayHealth = Math.Max(0.4, State.RelayHealth - 0.30);
                events.Add(SimEvent.Make("comms_glitch", SimEventKind.Warning,
                    "Earth link is dropping packets",
                    "رابط الأرض يفقد الحزم",
                    "The antenna is fine. The relay alignment drifted. Fix it before the next data window.",
                    "الهوائي سليم. محاذاة المرحل انحرفت. أصلحها قبل نافذة البيانات التالية.",
                    Clock.TotalMinutes, "npc_comms_software_engineer", "comms_tower"));
            });

            Beat("dust_storm", 18, 5.0, events, () =>
            {
                State.ArrayDust = Math.Min(1.0, State.ArrayDust + 0.25);
                State.SensorDust = Math.Min(1.0, State.SensorDust + 0.20);
                events.Add(SimEvent.Make("dust_storm", SimEventKind.Critical,
                    "Electrostatic dust event",
                    "حدث غبار كهروستاتيكي",
                    "Sharp regolith is coating every exposed surface. Arrays and sensors first.",
                    "ثرى حاد يغطي كل سطح مكشوف. الألواح والحساسات أولًا.",
                    Clock.TotalMinutes, "npc_abdelaziz", "power_farm"));
            });

            Beat("crop_stress", 21, 9.0, events, () =>
            {
                State.CropHealth = Math.Max(0.3, State.CropHealth - 0.25);
                events.Add(SimEvent.Make("crop_stress", SimEventKind.Info,
                    "A crop batch is stressed",
                    "دفعة محاصيل تحت ضغط",
                    "Change one variable, then wait. Do not chase the reading.",
                    "غيّر متغيرًا واحدًا ثم انتظر. لا تطارد القراءة.",
                    Clock.TotalMinutes, "npc_agriculture_engineer", "greenhouse"));
            });

            Beat("filter_clog", 24, 7.0, events, () =>
            {
                State.FilterHealth = Math.Max(0.2, State.FilterHealth - 0.25);
                events.Add(SimEvent.Make("filter_clog", SimEventKind.Warning,
                    "Recycler flow is falling",
                    "تدفق التدوير ينخفض",
                    "Recovery is down and the reserve is paying for it. Back-flush the filter.",
                    "الاستعادة انخفضت والمخزون يدفع الثمن. اغسل الفلتر عكسيًا.",
                    Clock.TotalMinutes, "npc_nour", "life_support"));
            });

            Beat("insulation_fault", 27, 10.0, events, () =>
            {
                State.Insulation = Math.Max(0.4, State.Insulation - 0.15);
                events.Add(SimEvent.Make("insulation_fault", SimEventKind.Warning,
                    "Habitat shell is losing heat",
                    "هيكل الموئل يفقد الحرارة",
                    "A seal is compromised. Heaters can hold it, but they will eat the power budget.",
                    "ختم متضرر. السخانات تستطيع الصمود لكنها ستأكل ميزانية الطاقة.",
                    Clock.TotalMinutes, "npc_mostafa", "habitat"));
            });

            Beat("mission_end", 30, 20.0, events, () =>
            {
                events.Add(SimEvent.Make("mission_end", SimEventKind.Mission,
                    "Thirty days. The colony is still alive.",
                    "ثلاثون يومًا. المستعمرة ما زالت حية.",
                    "Not because nothing failed, but because someone read the gauge and acted.",
                    "ليس لأن شيئًا لم يفشل، بل لأن أحدهم قرأ المؤشر وتصرف.",
                    Clock.TotalMinutes, "npc_safaa", "habitat"));
            });
        }

        private void Beat(string id, int day, double hour, List<SimEvent> events, Action action)
        {
            if (_firedBeats.Contains(id)) return;
            if (Clock.Day < day) return;
            if (Clock.Day == day && Clock.HourOfDay < hour) return;
            _firedBeats.Add(id);
            action();
        }

        private void MaybeSolarParticleEvent(List<SimEvent> events)
        {
            // One authored event window: day 12, ~6 hours. Real SPEs are rare and serious.
            if (_spePlayed) return;
            if (Clock.Day == 12 && Clock.HourOfDay >= 9.0)
            {
                _spePlayed = true;
                State.SolarParticleEvent = true;
                State.SpeIntensity = 0.8;
                events.Add(SimEvent.Make(
                    "spe_onset", SimEventKind.Critical,
                    "Solar particle event — seek shelter",
                    "حدث جسيمات شمسية — الاحتماء مطلوب",
                    "Dose rate is rising fast. Get the crew into the shielded section until it passes.",
                    "معدل الجرعة يرتفع بسرعة. أدخل الطاقم إلى القسم المحمي حتى ينتهي الحدث.",
                    Clock.TotalMinutes, "npc_safaa", "habitat"));
                Npcs.Remember("npc_safaa", "spe_day_12");
            }
            else if (State.SolarParticleEvent && (Clock.Day > 12 || Clock.HourOfDay >= 15.0))
            {
                State.SolarParticleEvent = false;
                State.SpeIntensity = 0;
                events.Add(SimEvent.Make(
                    "spe_end", SimEventKind.Info,
                    "Solar particle event has passed",
                    "انتهى حدث الجسيمات الشمسية",
                    "Background radiation is back to the lunar surface baseline.",
                    "عاد الإشعاع الخلفي إلى مستوى سطح القمر الأساسي.",
                    Clock.TotalMinutes));
            }
        }

        private SimEvent IncidentToEvent(IncidentDefinition def)
        {
            var kind = def.Priority == "critical" ? SimEventKind.Critical
                     : def.Priority == "important" ? SimEventKind.Warning
                     : def.Kind == "discovery" ? SimEventKind.Discovery
                     : SimEventKind.Info;
            return SimEvent.Make(def.Id, kind, def.TitleEn, def.TitleAr, def.BodyEn, def.BodyAr,
                Clock.TotalMinutes, def.OwnerNpcId, def.LocationId);
        }

        private void EvaluateNotices(List<SimEvent> events)
        {
            Notice(events, "battery_low", State.BatteryPercent < 0.20, 240, SimEventKind.Warning,
                "Battery below 20%", "البطارية أقل من 20%",
                "The greenhouse and lab will be shed first. Clean the arrays or wait for sunlight.",
                "سيتم فصل الدفيئة والمختبر أولًا. نظّف الألواح أو انتظر ضوء الشمس.");

            Notice(events, "water_low", State.WaterPercent < 0.15, 240, SimEventKind.Warning,
                "Water reserve low", "احتياطي الماء منخفض",
                "Recycling recovers most of it, but the filter is the weak point. Inspect it.",
                "التدوير يستعيد معظم الماء، لكن الفلتر هو نقطة الضعف. افحصه.",
                "npc_nour", "life_support");

            Notice(events, "o2_low", State.O2Percent < 0.30, 180, SimEventKind.Critical,
                "Oxygen reserve low", "احتياطي الأكسجين منخفض",
                "Check the electrolysis load and the scrubber before doing anything else.",
                "افحص حمل التحليل الكهربائي والمرشح قبل أي شيء آخر.",
                "npc_nour", "life_support");

            Notice(events, "co2_high", State.Co2Ppm > 5000, 180, SimEventKind.Warning,
                "CO₂ is climbing", "ثاني أكسيد الكربون يرتفع",
                "The scrubber is losing efficiency. Replace it or reduce crew exertion.",
                "كفاءة المرشح تنخفض. استبدله أو قلل مجهود الطاقم.",
                "npc_nour", "life_support");

            Notice(events, "temp_low", State.CabinTempC < 17.0, 240, SimEventKind.Warning,
                "Cabin temperature falling", "حرارة المقصورة تنخفض",
                "Heaters are competing with the lab for power. Rebalance the load.",
                "السخانات تنافس المختبر على الطاقة. أعد موازنة الأحمال.",
                "npc_abdelaziz", "power_farm");

            Notice(events, "crop_ready", CropModel.ReadyToHarvest(State), 600, SimEventKind.Info,
                "Greenhouse crop ready", "المحصول جاهز",
                "Harvest it in the grow pod; fresh food restores morale and kcal.",
                "احصدها في وحدة النمو؛ الغذاء الطازج يعيد الروح المعنوية والسعرات.",
                "npc_agriculture_engineer", "greenhouse");

            Notice(events, "signal_weak", State.SignalQuality < 0.45, 360, SimEventKind.Info,
                "Earth link degraded", "رابط الأرض ضعيف",
                "Dust on the antenna or relay wear. Clean the sensors and check the relay.",
                "غبار على الهوائي أو تآكل المرحل. نظّف الحساسات وافحص المرحل.",
                "npc_comms_software_engineer", "communications_tower");

            Notice(events, "airlock_dust", State.AirlockDust > 0.5, 480, SimEventKind.Info,
                "Airlock dust is high", "غبار الأيرلوك مرتفع",
                "Dust in the seal is a real hazard. Clean the airlock floor before the next EVA.",
                "الغبار في الختم خطر حقيقي. نظّف أرضية الأيرلوك قبل الخروج التالي.",
                "npc_nour", "airlock");
        }

        private void Notice(List<SimEvent> events, string id, bool condition, double cooldown,
            SimEventKind kind, string titleEn, string titleAr, string bodyEn, string bodyAr,
            string speaker = "", string location = "")
        {
            if (!condition) return;
            double now = Clock.TotalMinutes;
            if (_noticeAt.TryGetValue(id, out double last) && now - last < cooldown) return;
            _noticeAt[id] = now;
            events.Add(SimEvent.Make(id, kind, titleEn, titleAr, bodyEn, bodyAr, now, speaker, location));
        }

        // ---- player actions --------------------------------------------------

        public bool TryAction(PlayerAction action, out string messageEn, out string messageAr)
        {
            messageEn = ""; messageAr = "";
            switch (action)
            {
                case PlayerAction.CleanArrays:
                    DustModel.CleanArrays(State);
                    State.ArrayHealth = Math.Min(1.0, State.ArrayHealth + 0.01);
                    Complete("obj_solar_dust", "clean_arrays", "cleaning");
                    messageEn = "You brush the regolith off the array. Output recovers.";
                    messageAr = "أزلت الثرى عن الألواح. عاد الإنتاج.";
                    return true;

                case PlayerAction.CleanSensors:
                    DustModel.CleanSensors(State);
                    Complete("obj_sensor_dust", "clean_sensors", "cleaning");
                    messageEn = "Sensors cleaned. Readings should settle now.";
                    messageAr = "نُظفت الحساسات. يجب أن تستقر القراءات الآن.";
                    return true;

                case PlayerAction.CleanAirlock:
                    DustModel.CleanAirlock(State);
                    Complete("obj_airlock_dust", "clean_airlock", "cleaning");
                    messageEn = "Airlock floor and seal cleaned. Dust stays outside where it belongs.";
                    messageAr = "نُظفت أرضية الأيرلوك والختم. بقي الغبار في الخارج.";
                    return true;

                case PlayerAction.HarvestCrops:
                    double kcal = CropModel.Harvest(State);
                    if (kcal <= 0) { messageEn = "Not ready yet."; messageAr = "لم ينضج بعد."; return false; }
                    Complete("obj_plant_care", "harvest", "botany");
                    messageEn = $"Harvested. +{kcal:0} kcal stored.";
                    messageAr = $"تم الحصاد. +{kcal:0} سعرة مخزنة.";
                    return true;

                case PlayerAction.CleanFilter:
                    State.FilterHealth = Math.Min(1.0, State.FilterHealth + 0.45);
                    State.WaterQuality = Math.Min(1.0, State.WaterQuality + 0.06);
                    Complete("obj_water_filter", "clean_filter", "chemistry");
                    messageEn = "Filter back-flushed. Recovery rate is back up.";
                    messageAr = "تم غسل الفلتر عكسيًا. عاد معدل الاستعادة.";
                    return true;

                case PlayerAction.ReplaceScrubber:
                    State.ScrubberHealth = 1.0;
                    Complete("obj_air_scrubber", "replace_scrubber", "chemistry");
                    messageEn = "New CO₂ scrubber installed. Watch the ppm fall.";
                    messageAr = "تم تركيب مرشح CO₂ جديد. راقب انخفاض القراءة.";
                    return true;

                case PlayerAction.RepairRelay:
                    State.RelayHealth = Math.Min(1.0, State.RelayHealth + 0.4);
                    Complete("obj_comms_relay", "repair_relay", "engineering");
                    messageEn = "Relay aligned. Signal quality is recovering.";
                    messageAr = "تمت محاذاة المرحل. جودة الإشارة تتحسن.";
                    return true;

                case PlayerAction.RepairArray:
                    State.ArrayHealth = Math.Min(1.0, State.ArrayHealth + 0.15);
                    Complete("obj_solar_dust", "repair_array", "engineering");
                    messageEn = "Array connector re-seated. Health improved.";
                    messageAr = "أُعيد تثبيت موصل الألواح. تحسنت الحالة.";
                    return true;

                case PlayerAction.CollectSample:
                    State.SamplesCollected++;
                    State.DataPoints += 10;
                    Complete("obj_sample", "collect_sample", "geology");
                    messageEn = "Sample bagged, photographed and logged with its coordinates.";
                    messageAr = "تم توثيق العينة وتصويرها وتسجيل إحداثياتها.";
                    return true;

                case PlayerAction.ShelterCrew:
                    State.CrewSheltered = true;
                    Npcs.Remember("npc_safaa", "sheltered_crew");
                    Complete("obj_radiation", "shelter", "health");
                    messageEn = "Crew moved to the shielded section. Dose rate is falling.";
                    messageAr = "انتقل الطاقم إلى القسم المحمي. معدل الجرعة ينخفض.";
                    return true;

                case PlayerAction.LeaveShelter:
                    State.CrewSheltered = false;
                    messageEn = "Crew back to normal duty.";
                    messageAr = "عاد الطاقم إلى العمل الطبيعي.";
                    return true;

                case PlayerAction.RestCrew:
                    foreach (var st in Npcs.States.Values) st.Fatigue = Math.Max(0.0, st.Fatigue - 0.25);
                    State.AverageFatigue = Math.Max(0.0, State.AverageFatigue - 0.25);
                    Complete("obj_crew_rest", "rest", "crew");
                    messageEn = "Shift rebalanced. The crew can breathe again.";
                    messageAr = "أُعيد توزيع المناوبة. استطاع الطاقم أن يتنفس.";
                    return true;

                case PlayerAction.InspectCrop:
                    Complete("obj_plant_care", "inspect_crop", "botany");
                    messageEn = "Light, water, CO₂ and nutrients logged. Change one variable, then wait.";
                    messageAr = "سُجل الضوء والماء وثاني أكسيد الكربون والمغذيات. غيّر متغيرًا واحدًا ثم انتظر.";
                    return true;

                case PlayerAction.BalanceLoad:
                    State.GrowLightsOn = false;
                    State.GrowLightsResumeAtMinute = Clock.TotalMinutes + 360.0;
                    Complete("obj_power_budget", "balance_load", "engineering");
                    messageEn = "Grow lights shed for six hours. Heat and life support keep priority until the battery recovers.";
                    messageAr = "فُصلت أضواء النمو ست ساعات. تبقى الحرارة ودعم الحياة لهما الأولوية حتى تتعافى البطارية.";
                    return true;

                case PlayerAction.ToggleGrowLights:
                    State.GrowLightsOn = !State.GrowLightsOn;
                    State.GrowLightsResumeAtMinute = -1;
                    Complete("obj_power_budget", "grow_lights", "engineering");
                    if (State.GrowLightsOn)
                    {
                        messageEn = "Grow lights on. 2.0 kW from the colony bus, no direct Sun.";
                        messageAr = "أضواء النمو تعمل. 2.0 كيلوواط من شبكة المستعمرة، بدون شمس مباشرة.";
                    }
                    else
                    {
                        messageEn = "Grow lights off. The crop slows, but life support keeps its power.";
                        messageAr = "أضواء النمو متوقفة. يتباطأ المحصول، لكن دعم الحياة يحتفظ بطاقته.";
                    }
                    return true;

                case PlayerAction.MedicalCheckup:
                    State.AverageFatigue = Math.Max(0.0, State.AverageFatigue - 0.10);
                    State.AverageMorale = Math.Min(1.0, State.AverageMorale + 0.03);
                    State.SuitEnergy = 1.0;
                    foreach (var st in Npcs.States.Values)
                        st.Fatigue = Math.Max(0.0, st.Fatigue - 0.10);
                    Complete("obj_crew_rest", "checkup", "health");
                    messageEn = "Check-up logged. Resting heart rates, hydration and dose are within limits.";
                    messageAr = "سُجل الفحص. معدلات القلب أثناء الراحة والترطيب والجرعة ضمن الحدود.";
                    return true;

                case PlayerAction.Exercise:
                    State.AverageFatigue = Math.Max(0.0, State.AverageFatigue - 0.05);
                    State.AverageMorale = Math.Min(1.0, State.AverageMorale + 0.04);
                    State.SuitEnergy = 1.0;
                    Complete("obj_crew_rest", "exercise", "health");
                    messageEn = "Resistance session done. Bone and muscle load counters the low gravity.";
                    messageAr = "انتهت جلسة المقاومة. الحمل العضلي والعظمي يوازن الجاذبية المنخفضة.";
                    return true;

                case PlayerAction.ProcessRegolith:
                    State.O2Kg = Math.Min(State.O2CapacityKg, State.O2Kg + 0.20);
                    State.DataPoints += 1.0;
                    Complete("obj_air_scrubber", "intake", "chemistry");
                    messageEn = "Regolith feed screened. The oxygen plant extracts a measured 0.2 kg of O2.";
                    messageAr = "فُحصت تغذية الثرى. يستخلص مصنع الأكسجين 0.2 كغ من الأكسجين.";
                    return true;
            }
            return false;
        }

        /// <summary>Attempts to resolve the matching active incident and records the learning.</summary>
        private void Complete(string objectiveId, string action, string skillTag)
        {
            Learning.Unlock(objectiveId, Clock.TotalMinutes);
            foreach (var a in Incidents.Active)
            {
                if (a.Resolved || a.Ignored) continue;
                var def = Incidents.Definition(a.DefinitionId);
                if (def != null && def.RequiredAction == action)
                {
                    Incidents.Resolve(def.Id, action);
                }
                else if (def != null && ObjectivesMatch(def.LearningObjectiveId, objectiveId))
                {
                    Incidents.Resolve(def.Id, def.RequiredAction);
                }
            }
            Incidents.DismissResolved();
        }

        private static bool ObjectivesMatch(string a, string b)
            => !string.IsNullOrEmpty(a) && a == b;

        public bool Interact(string buildingId, string interactionId, out string messageEn, out string messageAr)
        {
            messageEn = ""; messageAr = "";
            switch (interactionId)
            {
                case "clean_arrays": return TryAction(PlayerAction.CleanArrays, out messageEn, out messageAr);
                case "clean_sensors": return TryAction(PlayerAction.CleanSensors, out messageEn, out messageAr);
                case "clean_airlock": return TryAction(PlayerAction.CleanAirlock, out messageEn, out messageAr);
                case "dust_removal":
                case "eva_prep": return TryAction(PlayerAction.CleanAirlock, out messageEn, out messageAr);
                case "harvest": return TryAction(PlayerAction.HarvestCrops, out messageEn, out messageAr);
                case "clean_filter": return TryAction(PlayerAction.CleanFilter, out messageEn, out messageAr);
                case "replace_scrubber": return TryAction(PlayerAction.ReplaceScrubber, out messageEn, out messageAr);
                case "repair_relay":
                case "comms_desk": return TryAction(PlayerAction.RepairRelay, out messageEn, out messageAr);
                case "repair_array": return TryAction(PlayerAction.RepairArray, out messageEn, out messageAr);
                case "antenna": return TryAction(PlayerAction.CleanSensors, out messageEn, out messageAr);
                case "collect_sample":
                case "sample_intake": return TryAction(PlayerAction.CollectSample, out messageEn, out messageAr);
                case "shelter": return TryAction(PlayerAction.ShelterCrew, out messageEn, out messageAr);
                case "rest": return TryAction(PlayerAction.RestCrew, out messageEn, out messageAr);
                case "checkup": return TryAction(PlayerAction.MedicalCheckup, out messageEn, out messageAr);
                case "exercise": return TryAction(PlayerAction.Exercise, out messageEn, out messageAr);
                case "intake": return TryAction(PlayerAction.ProcessRegolith, out messageEn, out messageAr);
                case "crop_monitor":
                case "inspect_crop": return TryAction(PlayerAction.InspectCrop, out messageEn, out messageAr);
                case "grow_lights": return TryAction(PlayerAction.ToggleGrowLights, out messageEn, out messageAr);
                case "balance_load": return TryAction(PlayerAction.BalanceLoad, out messageEn, out messageAr);
                case "repair_o2":
                    State.O2GeneratorRunning = true;
                    State.GrowLightsOn = false;
                    Complete("obj_air_scrubber", "repair_o2", "chemistry");
                    messageEn = "Electrolysis restarted; grow lights shed so oxygen comes first.";
                    messageAr = "أُعيد تشغيل التحليل الكهربائي؛ فُصلت أضواء النمو ليأتي الأكسجين أولًا.";
                    return true;
                default:
                    return ReadStation(buildingId, interactionId, out messageEn, out messageAr);
            }
        }

        /// <summary>
        /// Observation stations are not decoration. Reading one unlocks its journal objective
        /// and returns the one-line observation the crew would say out loud. This keeps every
        /// console in buildings.json useful without inventing a separate quiz system.
        /// </summary>
        private bool ReadStation(string buildingId, string interactionId, out string messageEn, out string messageAr)
        {
            messageEn = "This station is for observation. Read the panel.";
            messageAr = "هذه المحطة للمراقبة. اقرأ اللوحة.";

            var building = Content.Building(buildingId);
            if (building == null) return false;
            InteractionPoint? point = null;
            foreach (var i in building.Interactions)
                if (i.Id == interactionId) { point = i; break; }
            if (point == null) return false;

            var objective = Content.Objective(point.LearningObjectiveId);
            if (objective == null)
            {
                messageEn = $"{point.NameEn}. The reading is steady.";
                messageAr = $"{point.NameAr}. القراءة مستقرة.";
                return false;
            }

            bool fresh = Learning.Unlock(objective.Id, Clock.TotalMinutes) != null;
            State.DataPoints += 0.5;
            messageEn = $"{objective.TitleEn}. {objective.BodyEn}";
            messageAr = $"{objective.TitleAr}. {objective.BodyAr}";
            if (!fresh)
            {
                messageEn = $"{point.NameEn}: {objective.TitleEn}.";
                messageAr = $"{point.NameAr}: {objective.TitleAr}.";
            }
            return true;
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
