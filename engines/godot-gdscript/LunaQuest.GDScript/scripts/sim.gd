class_name LQSim
extends RefCounted
## The colony rules, ported one-for-one from shared/LunaQuest.Core (and from the browser edition
## that already mirrors it). No rendering and no input in here: a fixed one-second step makes the
## result deterministic, and the save file keeps the schema-2 keys, so a save written here opens
## in the .NET build and the other way round.
##
## Every number is tied to a real anchor. Oxygen is 0.84 kg per person per day (NASA BVAD),
## recovery is ISS-class 93%, the surface dose is Chang'e-4's 13.2 µGy/h. The player is meant to
## be able to argue with the model, not just watch a bar move.

const SAVE_SCHEMA := 2
const FIXED_STEP := 1.0
const MINUTES_PER_DAY := 1440

const ACTION_ALIASES := {
	"dust_removal": "clean_airlock",
	"eva_prep": "clean_airlock",
	"clean_arrays": "clean_arrays",
	"clean_sensors": "clean_sensors",
	"clean_airlock": "clean_airlock",
	"harvest": "harvest_crops",
	"crop_monitor": "inspect_crop",
	"inspect_crop": "inspect_crop",
	"grow_lights": "toggle_grow_lights",
	"clean_filter": "clean_filter",
	"replace_scrubber": "replace_scrubber",
	"repair_o2": "repair_o2",
	"repair_relay": "repair_relay",
	"relay": "repair_relay",
	"comms_desk": "repair_relay",
	"repair_array": "repair_array",
	"balance_load": "balance_load",
	"antenna": "clean_sensors",
	"collect_sample": "collect_sample",
	"sample_intake": "collect_sample",
	"shelter": "shelter",
	"rest": "rest_crew",
	"checkup": "medical_checkup",
	"exercise": "exercise",
	"intake": "process_regolith",
}

# ---- anchors ---------------------------------------------------------------
const O2_KG_PER_PERSON_DAY := 0.84
const CO2_KG_PER_PERSON_DAY := 1.0
const CABIN_VOLUME_M3 := 300.0
const CO2_DENSITY_KG_M3 := 1.98
const WATER_L_PER_PERSON_DAY := 11.0
const KCAL_PER_PERSON_DAY := 2800.0
const POD_AREA_M2 := 24.0
const CROP_WATER_L_PER_M2_DAY := 3.0
const CROP_CO2_KG_PER_M2_DAY := 0.03
const CROP_HARVEST_KCAL_PER_M2 := 450.0
const CROP_CYCLE_DAYS := 24.0
const SURFACE_MICRO_GY_H := 13.2
const TARGET_CABIN_C := 21.0

var clock: LQClock
var state: Dictionary = {}
var npcs_state: Dictionary = {}
var incidents_active: Array = []
var journal: Array = []
var skills: Dictionary = {}
var events: Array = []
var player_name := "Mostafa"
var arabic := false
var extras: Dictionary = {}

var _accumulator := 0.0
var _incident_accumulator := 0.0
var _notice_at: Dictionary = {}
var _last_day := 1
var _spe_played := false
var _fired_beats: Dictionary = {}
var _last_raised: Dictionary = {}


func _init() -> void:
	clock = LQClock.new(0.0)
	state = _initial_state()
	_reset_npcs()
	_last_day = clock.day()


func _initial_state() -> Dictionary:
	return {
		# power
		"batteryKwh": 96.0,
		"batteryCapacityKwh": 120.0,
		"arrayPeakKw": 30.0,
		"arrayHealth": 1.0,
		"baseLoadKw": 12.4,
		"labAndGreenhousePowered": true,
		"solarKw": 0.0,
		# water
		"waterL": 900.0,
		"waterCapacityL": 1200.0,
		"recyclerEfficiency": 0.93,
		"filterHealth": 1.0,
		"waterQuality": 0.98,
		"recyclerRunning": true,
		# air
		"o2Kg": 83.7,
		"o2CapacityKg": 110.0,
		"co2Kg": 0.35,
		"cabinPressureKpa": 101.3,
		"scrubberHealth": 1.0,
		"o2GeneratorKw": 2.4,
		"o2GeneratorRunning": true,
		# thermal
		"cabinTempC": 21.0,
		"externalTempC": -55.0,
		"heaterKw": 1.8,
		"insulation": 0.86,
		# dust
		"arrayDust": 0.04,
		"sensorDust": 0.03,
		"airlockDust": 0.05,
		# food
		"foodKcal": 482000.0,
		"foodCapacityKcal": 607000.0,
		"foodShortageDays": 0.0,
		"foodShortage": false,
		"cropWaterStress": 0.0,
		"cropLightHours": 0.0,
		"cropBiomassKg": 0.0,
		"cropHealth": 1.0,
		"growLightsOn": true,
		"growLightsResumeAtMinute": -1.0,
		# radiation
		"doseRateMicroGyH": 13.2,
		"cumulativeDoseMsv": 0.0,
		"solarParticleEvent": false,
		"speIntensity": 0.0,
		"crewSheltered": false,
		# comms
		"signalQuality": 1.0,
		"earthLinkUp": true,
		"relayHealth": 1.0,
		# crew and mission
		"crewCount": 7,
		"averageFatigue": 0.0,
		"averageMorale": 0.8,
		"suitEnergy": 1.0,
		"samplesCollected": 0,
		"dataPoints": 0.0,
		"gameOver": false,
	}


func _reset_npcs() -> void:
	npcs_state.clear()
	for n in Content.npcs:
		npcs_state[n.get("id", "")] = {
			"location": n.get("locationId", "habitat"),
			"activityEn": "on duty",
			"activityAr": "في المناوبة",
			"trust": 0.5,
			"fatigue": 0.2,
			"memory": [],
		}


# ---- derived readings ------------------------------------------------------

func battery_percent() -> float:
	return state["batteryKwh"] / maxf(0.001, state["batteryCapacityKwh"])


func o2_percent() -> float:
	return state["o2Kg"] / maxf(0.001, state["o2CapacityKg"])


func water_percent() -> float:
	return state["waterL"] / maxf(0.001, state["waterCapacityL"])


func food_percent() -> float:
	return state["foodKcal"] / maxf(0.001, state["foodCapacityKcal"])


func co2_ppm() -> float:
	return maxf(400.0, (state["co2Kg"] / CO2_DENSITY_KG_M3 / CABIN_VOLUME_M3) * 1000000.0)


func pressure_percent() -> float:
	return state["cabinPressureKpa"] / 101.3


func food_days_remaining() -> float:
	return state["foodKcal"] / (KCAL_PER_PERSON_DAY * maxf(1.0, state["crewCount"]))


func crop_stage() -> String:
	var biomass: float = state["cropBiomassKg"]
	if biomass < 0.15:
		return "seedling"
	if biomass < 0.55:
		return "vegetative"
	if biomass < 0.95:
		return "maturing"
	return "ready"


func comms_band() -> String:
	var q: float = state["signalQuality"]
	if q > 0.75:
		return "nominal"
	if q > 0.45:
		return "degraded"
	return "weak" if state["earthLinkUp"] else "lost"


func metric(name: String) -> float:
	match name:
		"arrayDust": return state["arrayDust"]
		"sensorDust": return state["sensorDust"]
		"airlockDust": return state["airlockDust"]
		"batteryPercent": return battery_percent()
		"waterPercent": return water_percent()
		"o2Percent": return o2_percent()
		"co2Ppm": return co2_ppm()
		"filterHealth": return state["filterHealth"]
		"scrubberHealth": return state["scrubberHealth"]
		"cropHealth": return state["cropHealth"]
		"signalQuality": return state["signalQuality"]
		"doseRate": return state["doseRateMicroGyH"]
		"cabinTemp": return state["cabinTempC"]
		"foodPercent": return food_percent()
		"relayHealth": return state["relayHealth"]
		"crewFatigue": return state["averageFatigue"]
		"pressure": return state["cabinPressureKpa"]
		"samples": return float(state["samplesCollected"])
		"suitEnergy": return state["suitEnergy"]
		"foodKcal": return state["foodKcal"]
		"cropBiomass": return state["cropBiomassKg"]
		"day": return clock.day()
		_: return 0.0


# ---- the fixed step --------------------------------------------------------

## Advance by real seconds. Returns the events raised during those seconds.
func step(real_seconds: float) -> Array:
	var raised: Array = []
	if real_seconds <= 0.0:
		return raised
	_accumulator += real_seconds
	var guard := 0
	while _accumulator >= FIXED_STEP and guard < 400:
		_accumulator -= FIXED_STEP
		_step_once(raised)
		guard += 1
	_evaluate_notices(raised)
	if events.size() > 160:
		events = events.slice(events.size() - 160, events.size())
	return raised


func _step_once(raised: Array) -> void:
	clock.step(FIXED_STEP)
	_step_power()
	_step_water()
	_step_air()
	_step_thermal()
	_step_dust()
	_step_crop()
	_step_food()
	_step_radiation()
	_step_comms()
	_tick_npcs()
	_update_mood()

	_incident_accumulator += FIXED_STEP
	if _incident_accumulator >= 5.0:
		_incident_accumulator = 0.0
		for def in _evaluate_incidents():
			raised.append(incident_event(def))
			events.append(incident_event(def))

	_maybe_spe(raised)
	_scripted_beats(raised)

	if clock.day() != _last_day:
		_last_day = clock.day()
		var ev := {
			"id": "day_%d" % _last_day,
			"kind": "mission",
			"titleEn": "Mission day %d of 30" % _last_day,
			"titleAr": "اليوم %d من 30" % _last_day,
			"bodyEn": "The Sun moves on. Check power, water and the crew's rest.",
			"bodyAr": "تقدمت الشمس. راجع الطاقة والماء وراحة الطاقم.",
			"at": clock.total_minutes,
			"speaker": "",
			"location": "",
		}
		raised.append(ev)
		events.append(ev)


func _dt_hours(seconds: float) -> float:
	return (seconds * LQClock.MINUTES_PER_SECOND) / 60.0


func _game_days(seconds: float) -> float:
	return (seconds * LQClock.MINUTES_PER_SECOND) / LQClock.MINUTES_PER_DAY


func _step_power() -> void:
	var dt_h := _dt_hours(FIXED_STEP)
	var irradiance := clock.solar_irradiance_fraction()
	var dust_factor := 1.0 - minf(0.85, state["arrayDust"] * 0.9)
	state["solarKw"] = state["arrayPeakKw"] * irradiance * state["arrayHealth"] * dust_factor

	var load: float = state["baseLoadKw"]
	if state["o2GeneratorRunning"]:
		load += state["o2GeneratorKw"]
	if state["growLightsOn"]:
		load += 2.0
	load += state["heaterKw"]

	var net: float = state["solarKw"] - load
	state["labAndGreenhousePowered"] = true
	if state["batteryKwh"] + net * dt_h < state["batteryCapacityKwh"] * 0.2 and state["growLightsOn"]:
		var shed_net: float = state["solarKw"] - (load - 2.0)
		if shed_net > net:
			net = shed_net
			load -= 2.0
			state["labAndGreenhousePowered"] = false

	state["batteryKwh"] = clampf(state["batteryKwh"] + net * dt_h, 0.0, state["batteryCapacityKwh"])

	# Never a hard failure: at an empty pack the colony runs on emergency reserves with reduced
	# service, and the HUD explains exactly that.
	if state["batteryKwh"] <= 0.01:
		state["o2GeneratorRunning"] = false
		state["growLightsOn"] = false
		state["labAndGreenhousePowered"] = false
		if state["growLightsResumeAtMinute"] < 0.0:
			state["growLightsResumeAtMinute"] = clock.total_minutes + 360.0

	if not state["growLightsOn"] and state["growLightsResumeAtMinute"] >= 0.0 \
			and clock.total_minutes >= state["growLightsResumeAtMinute"] \
			and battery_percent() > 0.35:
		state["growLightsOn"] = true
		state["growLightsResumeAtMinute"] = -1.0


func _step_air() -> void:
	var days := _game_days(FIXED_STEP)
	var dt_h := _dt_hours(FIXED_STEP)
	var o2_use: float = O2_KG_PER_PERSON_DAY * state["crewCount"] * days
	var co2_made: float = CO2_KG_PER_PERSON_DAY * state["crewCount"] * days

	state["o2Kg"] = maxf(0.0, state["o2Kg"] - o2_use)
	state["co2Kg"] += co2_made

	# Electrolysis: 1 kg of water yields ~0.889 kg O2. The stack is sized to 90% of demand, so the
	# reserve declines slowly and the greenhouse's oxygen is a real contribution.
	if state["o2GeneratorRunning"] and state["waterL"] > 20.0:
		var o2_made: float = o2_use * 0.9
		state["waterL"] = maxf(0.0, state["waterL"] - o2_made / 0.889)
		state["o2Kg"] = minf(state["o2CapacityKg"], state["o2Kg"] + o2_made)
	elif not state["o2GeneratorRunning"]:
		state["o2Kg"] = maxf(0.0, state["o2Kg"] - o2_use * 0.05)

	var baseline := 0.24
	var excess: float = maxf(0.0, state["co2Kg"] - baseline)
	var scrub_per_hour: float = 0.55 * maxf(0.0, state["scrubberHealth"])
	var removed: float = excess * (1.0 - exp(-scrub_per_hour * dt_h))
	state["co2Kg"] = maxf(0.0, state["co2Kg"] - removed)
	state["co2Kg"] = maxf(0.0, state["co2Kg"] - 0.0002 * FIXED_STEP)

	state["cabinPressureKpa"] = clampf(state["cabinPressureKpa"] - 0.00002 * FIXED_STEP, 80.0, 101.3)
	if state["o2GeneratorRunning"] and state["labAndGreenhousePowered"]:
		state["cabinPressureKpa"] = minf(101.3, state["cabinPressureKpa"] + 0.00003 * FIXED_STEP)

	state["scrubberHealth"] = clampf(state["scrubberHealth"] - 0.00025 * FIXED_STEP, 0.0, 1.0)


func _step_water() -> void:
	var days := _game_days(FIXED_STEP)
	var demand: float = minf(WATER_L_PER_PERSON_DAY * state["crewCount"] * days, state["waterL"])

	var recovered := 0.0
	if state["recyclerRunning"] and state["filterHealth"] > 0.05:
		recovered = demand * state["recyclerEfficiency"] * state["filterHealth"] * state["waterQuality"]
	state["waterL"] = clampf(state["waterL"] - demand + recovered, 0.0, state["waterCapacityL"])

	state["filterHealth"] = clampf(state["filterHealth"] - demand * 0.0004, 0.0, 1.0)
	state["waterQuality"] = clampf(
		state["waterQuality"] + (state["filterHealth"] - 0.5) * 0.00002, 0.4, 1.0)

	if state["waterL"] < 40.0:
		state["cropWaterStress"] = minf(1.0, state["cropWaterStress"] + 0.01)
	else:
		state["cropWaterStress"] = maxf(0.0, state["cropWaterStress"] - 0.004)


func _step_thermal() -> void:
	var illum := clock.solar_irradiance_fraction()
	state["externalTempC"] = -180.0 + 130.0 * illum

	var delta_t: float = TARGET_CABIN_C - state["externalTempC"]
	var loss_kw: float = 0.03 * delta_t * (1.0 - state["insulation"] * 0.75)

	var heat_in_kw := 0.0
	if state["batteryKwh"] > 1.0:
		heat_in_kw = minf(state["heaterKw"], loss_kw)
	else:
		heat_in_kw = loss_kw * 0.92

	var net_kw: float = heat_in_kw - loss_kw
	var dt_h := _dt_hours(FIXED_STEP)
	var thermal_mass := 6.0
	state["cabinTempC"] = clampf(state["cabinTempC"] + (net_kw * dt_h) / thermal_mass, 3.0, 35.0)

	if state["cabinTempC"] < 16.0:
		state["averageFatigue"] = minf(1.0, state["averageFatigue"] + 0.0015)


func _step_dust() -> void:
	state["arrayDust"] = clampf(state["arrayDust"] + 0.0003 * FIXED_STEP, 0.0, 1.0)
	state["sensorDust"] = clampf(state["sensorDust"] + 0.00017 * FIXED_STEP, 0.0, 1.0)
	state["airlockDust"] = clampf(state["airlockDust"] + 0.00025 * FIXED_STEP, 0.0, 1.0)
	state["arrayHealth"] = clampf(
		state["arrayHealth"] - state["arrayDust"] * 0.0004 * FIXED_STEP, 0.2, 1.0)


func _step_crop() -> void:
	var days := _game_days(FIXED_STEP)
	var dt: float = days / CROP_CYCLE_DAYS

	var light_ok: bool = state["growLightsOn"] and state["labAndGreenhousePowered"] and state["batteryKwh"] > 0.5
	var water_ok: bool = state["waterL"] > 30.0 and state["cropWaterStress"] < 0.5
	var air_ok: bool = co2_ppm() > 350.0

	if light_ok and water_ok and air_ok:
		state["cropBiomassKg"] = minf(1.0, state["cropBiomassKg"] + dt * (0.55 + 0.45 * state["cropHealth"]))

	if light_ok:
		state["waterL"] = maxf(0.0, state["waterL"] - POD_AREA_M2 * CROP_WATER_L_PER_M2_DAY * days * 0.1)
		var co2_used: float = minf(state["co2Kg"], POD_AREA_M2 * CROP_CO2_KG_PER_M2_DAY * days)
		state["co2Kg"] = maxf(0.0, state["co2Kg"] - co2_used)
		state["o2Kg"] = minf(state["o2CapacityKg"], state["o2Kg"] + co2_used * (32.0 / 44.0))
		state["cropLightHours"] += (FIXED_STEP * LQClock.MINUTES_PER_SECOND) / 60.0

	if not light_ok or not water_ok or not air_ok:
		state["cropHealth"] = maxf(0.0, state["cropHealth"] - 0.0006 * FIXED_STEP)
		state["cropBiomassKg"] = maxf(0.0, state["cropBiomassKg"] - 0.00002 * FIXED_STEP)
	else:
		state["cropHealth"] = minf(1.0, state["cropHealth"] + 0.00004 * FIXED_STEP)


func _step_food() -> void:
	var days := _game_days(FIXED_STEP)
	var need: float = KCAL_PER_PERSON_DAY * state["crewCount"] * days

	if state["foodKcal"] >= need:
		state["foodKcal"] -= need
		state["foodShortage"] = false
		state["foodShortageDays"] = maxf(0.0, state["foodShortageDays"] - days)
	else:
		state["foodKcal"] = 0.0
		state["foodShortage"] = true
		state["foodShortageDays"] += days
		# A shortfall is not a death screen: the crew slows down and morale falls.
		state["averageFatigue"] = minf(1.0, state["averageFatigue"] + days * 0.1)
		state["averageMorale"] = maxf(0.0, state["averageMorale"] - days * 0.05)


func _step_radiation() -> void:
	var hours := (FIXED_STEP * LQClock.MINUTES_PER_SECOND) / 60.0
	var rate := SURFACE_MICRO_GY_H
	if state["solarParticleEvent"]:
		rate *= 1.0 + 120.0 * state["speIntensity"]
	if state["crewSheltered"]:
		rate *= 0.25
	state["doseRateMicroGyH"] = rate
	state["cumulativeDoseMsv"] += (rate * hours) / 1000.0


func _step_comms() -> void:
	var dust_penalty: float = 1.0 - minf(0.45, state["sensorDust"] * 0.5)
	var sun_noise: float = 1.0 - 0.06 * maxf(0.0, sin(clock.lunar_phase() * 2.0 * PI))
	state["signalQuality"] = clampf(state["relayHealth"] * dust_penalty * sun_noise, 0.0, 1.0)
	state["earthLinkUp"] = state["signalQuality"] > 0.25


# ---- crew ------------------------------------------------------------------

func _tick_npcs() -> void:
	var hour := clock.hour_of_day()
	var dt_hours := float(LQClock.MINUTES_PER_SECOND) / 60.0
	for profile in Content.npcs:
		var id: String = profile.get("id", "")
		var entry := _schedule_entry(profile, hour)
		var st: Dictionary = npcs_state.get(id, {})
		if st.is_empty():
			continue
		if not entry.is_empty():
			st["location"] = entry.get("location", st.get("location", ""))
			st["activityEn"] = entry.get("activityEn", "on duty")
			st["activityAr"] = entry.get("activityAr", "في المناوبة")
		var resting: bool = str(st.get("activityEn", "")).to_lower().contains("rest") \
			or str(st.get("activityEn", "")).to_lower().contains("sleep")
		var rate := -0.035 if resting else 0.03
		if not profile.get("isRobot", false):
			st["fatigue"] = clampf(float(st.get("fatigue", 0.2)) + rate * dt_hours, 0.0, 1.0)
		npcs_state[id] = st


func _schedule_entry(profile: Dictionary, hour: float) -> Dictionary:
	var schedule: Array = profile.get("schedule", [])
	for e in schedule:
		var start := float(e.get("start", 0))
		var end := float(e.get("end", 24))
		var wraps: bool = start > end
		var inside: bool = (hour >= start or hour < end) if wraps else (hour >= start and hour < end)
		if inside:
			return e
	return schedule[0] if schedule.size() > 0 else {}


func _update_mood() -> void:
	var fatigue := 0.0
	var n := 0
	for id in npcs_state.keys():
		var profile := Content.npc(id)
		if profile.get("isRobot", false):
			continue
		fatigue += float(npcs_state[id].get("fatigue", 0.0))
		n += 1
	if n > 0:
		state["averageFatigue"] = fatigue / n

	if state["cabinTempC"] < 18.0:
		state["averageFatigue"] = clampf(state["averageFatigue"] + 0.00012, 0.0, 1.0)
	if co2_ppm() > 5000.0:
		state["averageFatigue"] = clampf(state["averageFatigue"] + 0.00008, 0.0, 1.0)

	var co2_penalty: float = minf(0.35, maxf(0.0, (co2_ppm() - 1200.0) / 12000.0))
	var food_penalty := 0.15 if food_percent() < 0.2 else 0.0
	state["averageMorale"] = clampf(
		0.92 - state["averageFatigue"] * 0.35 - co2_penalty - food_penalty, 0.0, 1.0)


## The line a crew member says when the commander walks up. Fixed, approved content.
func npc_line(npc_id: String, incident_nearby := false) -> String:
	var p := Content.npc(npc_id)
	if p.is_empty():
		return ""
	var st: Dictionary = npcs_state.get(npc_id, {})
	if incident_nearby:
		return "القراءة غير طبيعية. لا تصلحها قبل أن تعرف سببها." if arabic else \
			"That reading is off. Don't fix it before you know why."
	if float(st.get("fatigue", 0.0)) > 0.7 and not p.get("isRobot", false):
		return "أنا متعب. هل يمكننا إعادة ترتيب المناوبة؟" if arabic else \
			"I'm running tired. Can we rebalance the shift?"
	var greeting := Content.label(p, "greeting", arabic)
	if greeting != "":
		return greeting
	if arabic:
		return "%s — %s" % [p.get("nameAr", ""), p.get("roleAr", "")]
	return "%s — %s" % [p.get("nameEn", ""), p.get("roleEn", "")]


# ---- incidents -------------------------------------------------------------

func _evaluate_incidents() -> Array:
	var raised: Array = []
	for def in Content.incidents:
		var value := metric(str(def.get("metric", "")))
		var threshold := float(def.get("threshold", 0.0))
		var hit: bool = value < threshold if str(def.get("op", "above")) == "below" else value > threshold
		if not hit:
			continue
		var r := _try_raise(def)
		if not r.is_empty():
			raised.append(r)
	return raised


func _try_raise(def: Dictionary) -> Dictionary:
	var id: String = def.get("id", "")
	if incidents_active.size() >= 3:
		return {}
	for a in incidents_active:
		if a.get("definitionId", "") == id:
			return {}
	var minute := clock.total_minutes
	if _last_raised.has(id) and minute - float(_last_raised[id]) < float(def.get("cooldownMin", 720)):
		return {}
	incidents_active.append({"definitionId": id, "raisedAtMinute": minute, "hintLevel": 0})
	_last_raised[id] = minute
	return def


func incident_event(def: Dictionary) -> Dictionary:
	var kind := "info"
	match str(def.get("priority", "")):
		"critical": kind = "critical"
		"important": kind = "warning"
		_: kind = "discovery" if str(def.get("kind", "")) == "discovery" else "info"
	return {
		"id": def.get("id", ""),
		"kind": kind,
		"titleEn": def.get("titleEn", ""),
		"titleAr": def.get("titleAr", ""),
		"bodyEn": def.get("bodyEn", ""),
		"bodyAr": def.get("bodyAr", ""),
		"at": clock.total_minutes,
		"speaker": def.get("owner", ""),
		"location": def.get("location", ""),
	}


# ---- scripted pressure beats ----------------------------------------------

func _scripted_beats(raised: Array) -> void:
	_beat("welcome", 1, 8, func():
		_push(raised, "welcome", "mission",
			"Day 1 — the colony is yours", "اليوم الأول — المستعمرة مسؤوليتك",
			"Power, water, air, food, heat. Watch them in that order and nothing here surprises you.",
			"الطاقة، الماء، الهواء، الغذاء، الحرارة. راقبها بهذا الترتيب ولن يفاجئك شيء هنا.",
			"npc_abdelaziz", "habitat"))

	_beat("dust_front", 3, 10, func():
		state["arrayDust"] = minf(1.0, state["arrayDust"] + 0.18)
		_push(raised, "dust_front", "warning",
			"Dust front crossing the farm", "جبهة غبار تعبر المزرعة",
			"Output is already falling. Clean the arrays before the reserve notices.",
			"الإنتاج ينخفض بالفعل. نظّف الألواح قبل أن يلاحظ المخزون.",
			"npc_abdelaziz", "power_farm"))

	_beat("heavy_eva", 9, 6, func():
		for id in npcs_state.keys():
			npcs_state[id]["fatigue"] = minf(1.0, float(npcs_state[id].get("fatigue", 0.2)) + 0.3)
		_push(raised, "heavy_eva", "warning",
			"Heavy EVA day", "يوم سير فضائي طويل",
			"Everyone is running on too little sleep. Rebuild the rest plan before the next shift.",
			"الجميع يعملون بنوم قليل. أعد خطة الراحة قبل المناوبة التالية.",
			"npc_safaa", "medical"))

	_beat("comms_glitch", 15, 12, func():
		state["relayHealth"] = maxf(0.4, state["relayHealth"] - 0.3)
		_push(raised, "comms_glitch", "warning",
			"Earth link is dropping packets", "رابط الأرض يفقد الحزم",
			"The antenna is fine. The relay alignment drifted. Fix it before the next data window.",
			"الهوائي سليم. محاذاة المرحل انحرفت. أصلحها قبل نافذة البيانات التالية.",
			"npc_nexo", "comms_tower"))

	_beat("dust_storm", 18, 5, func():
		state["arrayDust"] = minf(1.0, state["arrayDust"] + 0.25)
		state["sensorDust"] = minf(1.0, state["sensorDust"] + 0.2)
		_push(raised, "dust_storm", "critical",
			"Electrostatic dust event", "حدث غبار كهروستاتيكي",
			"Sharp regolith is coating every exposed surface. Arrays and sensors first.",
			"ثرى حاد يغطي كل سطح مكشوف. الألواح والحساسات أولًا.",
			"npc_abdelaziz", "power_farm"))

	_beat("crop_stress", 21, 9, func():
		state["cropHealth"] = maxf(0.3, state["cropHealth"] - 0.25)
		_push(raised, "crop_stress", "info",
			"A crop batch is stressed", "دفعة محاصيل تحت ضغط",
			"Change one variable, then wait. Do not chase the reading.",
			"غيّر متغيرًا واحدًا ثم انتظر. لا تطارد القراءة.",
			"npc_nexo", "greenhouse"))

	_beat("filter_clog", 24, 7, func():
		state["filterHealth"] = maxf(0.2, state["filterHealth"] - 0.25)
		_push(raised, "filter_clog", "warning",
			"Recycler flow is falling", "تدفق التدوير ينخفض",
			"Recovery is down and the reserve is paying for it. Back-flush the filter.",
			"الاستعادة انخفضت والمخزون يدفع الثمن. اغسل الفلتر عكسيًا.",
			"npc_nour", "life_support"))

	_beat("insulation_fault", 27, 10, func():
		state["insulation"] = maxf(0.4, state["insulation"] - 0.15)
		_push(raised, "insulation_fault", "warning",
			"Habitat shell is losing heat", "هيكل الموئل يفقد الحرارة",
			"A seal is compromised. Heaters can hold it, but they will eat the power budget.",
			"ختم متضرر. السخانات تستطيع الصمود لكنها ستأكل ميزانية الطاقة.",
			"npc_mostafa", "habitat"))

	_beat("mission_end", 30, 20, func():
		_push(raised, "mission_end", "mission",
			"Thirty days. The colony is still alive.", "ثلاثون يومًا. المستعمرة ما زالت حية.",
			"Not because nothing failed, but because someone read the gauge and acted.",
			"ليس لأن شيئًا لم يفشل، بل لأن أحدهم قرأ المؤشر وتصرف.",
			"npc_safaa", "habitat"))


func _beat(id: String, day: int, hour: float, action: Callable) -> void:
	if _fired_beats.has(id):
		return
	if clock.day() < day:
		return
	if clock.day() == day and clock.hour_of_day() < hour:
		return
	_fired_beats[id] = true
	action.call()


func _push(raised: Array, id: String, kind: String, title_en: String, title_ar: String,
		body_en: String, body_ar: String, speaker: String, location: String) -> void:
	var ev := {
		"id": id, "kind": kind,
		"titleEn": title_en, "titleAr": title_ar,
		"bodyEn": body_en, "bodyAr": body_ar,
		"at": clock.total_minutes, "speaker": speaker, "location": location,
	}
	raised.append(ev)
	events.append(ev)


func _maybe_spe(raised: Array) -> void:
	if _spe_played:
		if state["solarParticleEvent"] and (clock.day() > 12 or clock.hour_of_day() >= 15.0):
			state["solarParticleEvent"] = false
			state["speIntensity"] = 0.0
			_push(raised, "spe_end", "info",
				"Solar particle event has passed", "انتهى حدث الجسيمات الشمسية",
				"Background radiation is back to the lunar surface baseline.",
				"عاد الإشعاع الخلفي إلى مستوى سطح القمر الأساسي.", "", "")
		return
	if clock.day() == 12 and clock.hour_of_day() >= 9.0:
		_spe_played = true
		state["solarParticleEvent"] = true
		state["speIntensity"] = 0.8
		_push(raised, "spe_onset", "critical",
			"Solar particle event — seek shelter", "حدث جسيمات شمسية — الاحتماء مطلوب",
			"Dose rate is rising fast. Get the crew into the shielded section until it passes.",
			"معدل الجرعة يرتفع بسرعة. أدخل الطاقم إلى القسم المحمي حتى ينتهي الحدث.",
			"npc_safaa", "habitat")
		if npcs_state.has("npc_safaa"):
			npcs_state["npc_safaa"]["memory"].append("spe_day_12")
	elif state["solarParticleEvent"] and (clock.day() > 12 or clock.hour_of_day() >= 15.0):
		state["solarParticleEvent"] = false
		state["speIntensity"] = 0.0


# ---- threshold notices -----------------------------------------------------

func _evaluate_notices(raised: Array) -> void:
	_notice(raised, "battery_low", battery_percent() < 0.2, 240, "warning",
		"Battery below 20%", "البطارية أقل من 20%",
		"The greenhouse and lab will be shed first. Clean the arrays or wait for sunlight.",
		"سيتم فصل الدفيئة والمختبر أولًا. نظّف الألواح أو انتظر ضوء الشمس.",
		"npc_abdelaziz", "power_farm")
	_notice(raised, "water_low", water_percent() < 0.15, 240, "warning",
		"Water reserve low", "احتياطي الماء منخفض",
		"Recycling recovers most of it, but the filter is the weak point. Inspect it.",
		"التدوير يستعيد معظم الماء، لكن الفلتر هو نقطة الضعف. افحصه.",
		"npc_nour", "life_support")
	_notice(raised, "o2_low", o2_percent() < 0.3, 180, "critical",
		"Oxygen reserve low", "احتياطي الأكسجين منخفض",
		"Check the electrolysis load and the scrubber before doing anything else.",
		"افحص حمل التحليل الكهربائي والمرشح قبل أي شيء آخر.",
		"npc_nour", "life_support")
	_notice(raised, "co2_high", co2_ppm() > 5000.0, 180, "warning",
		"CO₂ is climbing", "ثاني أكسيد الكربون يرتفع",
		"The scrubber is losing efficiency. Replace it or reduce crew exertion.",
		"كفاءة المرشح تنخفض. استبدله أو قلل مجهود الطاقم.",
		"npc_nour", "life_support")
	_notice(raised, "temp_low", state["cabinTempC"] < 17.0, 240, "warning",
		"Cabin temperature falling", "حرارة المقصورة تنخفض",
		"Heaters are competing with the lab for power. Rebalance the load.",
		"السخانات تنافس المختبر على الطاقة. أعد موازنة الأحمال.",
		"npc_abdelaziz", "power_farm")
	_notice(raised, "crop_ready", state["cropBiomassKg"] >= 0.98, 600, "info",
		"Greenhouse crop ready", "المحصول جاهز",
		"Harvest it in the grow pod; fresh food restores morale and kcal.",
		"احصدها في وحدة النمو؛ الغذاء الطازج يعيد الروح المعنوية والسعرات.",
		"npc_nexo", "greenhouse")
	_notice(raised, "signal_weak", state["signalQuality"] < 0.45, 360, "info",
		"Earth link degraded", "رابط الأرض ضعيف",
		"Dust on the antenna or relay wear. Clean the sensors and check the relay.",
		"غبار على الهوائي أو تآكل المرحل. نظّف الحساسات وافحص المرحل.",
		"npc_nexo", "comms_tower")
	_notice(raised, "airlock_dust", state["airlockDust"] > 0.5, 480, "info",
		"Airlock dust is high", "غبار الأيرلوك مرتفع",
		"Dust in the seal is a real hazard. Clean the airlock floor before the next EVA.",
		"الغبار في الختم خطر حقيقي. نظّف أرضية الأيرلوك قبل الخروج التالي.",
		"npc_nour", "airlock")


func _notice(raised: Array, id: String, condition: bool, cooldown: float, kind: String,
		title_en: String, title_ar: String, body_en: String, body_ar: String,
		speaker: String, location: String) -> void:
	if not condition:
		return
	var now := clock.total_minutes
	if _notice_at.has(id) and now - float(_notice_at[id]) < cooldown:
		return
	_notice_at[id] = now
	_push(raised, id, kind, title_en, title_ar, body_en, body_ar, speaker, location)


# ---- player actions --------------------------------------------------------

## Console entry point. Unmapped interaction ids are observation stations: reading one unlocks
## its journal objective, which is exactly how the C# core behaves.
func interact(building_id: String, interaction_id: String) -> Dictionary:
	var mapped: String = ACTION_ALIASES.get(interaction_id, "")
	if mapped == "repair_o2":
		state["o2GeneratorRunning"] = true
		state["growLightsOn"] = false
		_complete("obj_air_scrubber", "repair_o2")
		return _say(true,
			"Electrolysis restarted; grow lights shed so oxygen comes first.",
			"أُعيد تشغيل التحليل الكهربائي؛ فُصلت أضواء النمو ليأتي الأكسجين أولًا.")
	if mapped != "":
		return try_action(mapped)
	return _read_station(building_id, interaction_id)


func _read_station(building_id: String, interaction_id: String) -> Dictionary:
	var building := Content.building(building_id)
	var point := {}
	for i in building.get("interactions", []):
		if i.get("id", "") == interaction_id:
			point = i
			break
	if point.is_empty():
		return _say(false, "This station is for observation. Read the panel.",
			"هذه المحطة للمراقبة. اقرأ اللوحة.")
	var objective_id := str(point.get("learning", ""))
	var objective := Content.objective(objective_id)
	if objective.is_empty():
		return _say(false, "%s. The reading is steady." % point.get("nameEn", ""),
			"%s. القراءة مستقرة." % point.get("nameAr", ""))
	var fresh := _unlock(objective_id)
	state["dataPoints"] = float(state["dataPoints"]) + 0.5
	if not fresh.is_empty():
		return _say(true,
			"%s. %s" % [objective.get("titleEn", ""), objective.get("bodyEn", "")],
			"%s. %s" % [objective.get("titleAr", ""), objective.get("bodyAr", "")])
	return _say(true, "%s: %s." % [point.get("nameEn", ""), objective.get("titleEn", "")],
		"%s: %s." % [point.get("nameAr", ""), objective.get("titleAr", "")])


func try_action(action: String) -> Dictionary:
	match action:
		"clean_arrays":
			state["arrayDust"] = clampf(state["arrayDust"] - 0.6, 0.0, 1.0)
			state["arrayHealth"] = minf(1.0, state["arrayHealth"] + 0.01)
			_complete("obj_solar_dust", "clean_arrays")
			return _say(true, "You brush the regolith off the array. Output recovers.",
				"أزلت الثرى عن الألواح. عاد الإنتاج.")
		"clean_sensors":
			state["sensorDust"] = clampf(state["sensorDust"] - 0.8, 0.0, 1.0)
			_complete("obj_sensor_dust", "clean_sensors")
			return _say(true, "Sensors cleaned. Readings should settle now.",
				"نُظفت الحساسات. يجب أن تستقر القراءات الآن.")
		"clean_airlock":
			state["airlockDust"] = clampf(state["airlockDust"] - 0.9, 0.0, 1.0)
			_complete("obj_airlock_dust", "clean_airlock")
			return _say(true, "Airlock floor and seal cleaned. Dust stays outside where it belongs.",
				"نُظفت أرضية الأيرلوك والختم. بقي الغبار في الخارج.")
		"harvest_crops":
			if state["cropBiomassKg"] < 0.98:
				return _say(false, "Not ready yet. The crop is still maturing.",
					"لم ينضج بعد. المحصول ما زال ينمو.")
			var kcal: float = CROP_HARVEST_KCAL_PER_M2 * POD_AREA_M2 * maxf(0.4, state["cropHealth"])
			state["foodKcal"] = minf(state["foodCapacityKcal"], state["foodKcal"] + kcal)
			state["cropBiomassKg"] = 0.0
			state["cropLightHours"] = 0.0
			state["cropHealth"] = minf(1.0, state["cropHealth"] + 0.05)
			_complete("obj_plant_care", "harvest")
			return _say(true, "Harvested. +%s kcal stored." % _thousands(kcal),
				"تم الحصاد. +%s سعرة مخزنة." % _thousands(kcal))
		"clean_filter":
			state["filterHealth"] = minf(1.0, state["filterHealth"] + 0.45)
			state["waterQuality"] = minf(1.0, state["waterQuality"] + 0.06)
			_complete("obj_water_filter", "clean_filter")
			return _say(true, "Filter back-flushed. Recovery rate is back up.",
				"تم غسل الفلتر عكسيًا. عاد معدل الاستعادة.")
		"replace_scrubber":
			state["scrubberHealth"] = 1.0
			_complete("obj_air_scrubber", "replace_scrubber")
			return _say(true, "New CO₂ scrubber installed. Watch the ppm fall.",
				"تم تركيب مرشح CO₂ جديد. راقب انخفاض القراءة.")
		"repair_relay":
			state["relayHealth"] = minf(1.0, state["relayHealth"] + 0.5)
			_step_comms()
			_complete("obj_comms_relay", "repair_relay")
			return _say(true, "Relay realigned. The next data window should be clean.",
				"أُعيدت محاذاة المرحل. نافذة البيانات التالية يجب أن تكون نظيفة.")
		"collect_sample":
			state["samplesCollected"] = int(state["samplesCollected"]) + 1
			state["dataPoints"] = float(state["dataPoints"]) + 1.0
			_complete("obj_sample", "collect_sample")
			return _say(true, "Sample logged with its position and mass. One reading is not a deposit.",
				"سُجلت العينة مع موقعها وكتلتها. قراءة واحدة ليست راسبًا.")
		"shelter":
			state["crewSheltered"] = true
			_complete("obj_radiation", "shelter")
			return _say(true, "Crew moved into the shielded section. Dose rate falls to a quarter.",
				"انتقل الطاقم إلى القسم المحمي. انخفض معدل الجرعة إلى الربع.")
		"leave_shelter":
			state["crewSheltered"] = false
			return _say(true, "Crew back to normal duties.", "عاد الطاقم إلى العمل الطبيعي.")
		"rest_crew":
			for id in npcs_state.keys():
				npcs_state[id]["fatigue"] = maxf(0.0, float(npcs_state[id].get("fatigue", 0.2)) - 0.25)
			state["averageFatigue"] = maxf(0.0, state["averageFatigue"] - 0.25)
			_complete("obj_crew_rest", "rest")
			return _say(true, "Shift rebalanced. The crew can breathe again.",
				"أُعيد توزيع المناوبة. استطاع الطاقم أن يتنفس.")
		"inspect_crop":
			_complete("obj_plant_care", "inspect_crop")
			return _say(true, "Light, water, CO₂ and nutrients logged. Change one variable, then wait.",
				"سُجل الضوء والماء وثاني أكسيد الكربون والمغذيات. غيّر متغيرًا واحدًا ثم انتظر.")
		"balance_load":
			state["growLightsOn"] = false
			state["growLightsResumeAtMinute"] = clock.total_minutes + 360.0
			_complete("obj_power_budget", "balance_load")
			return _say(true,
				"Grow lights shed for six hours. Heat and life support keep priority until the battery recovers.",
				"فُصلت أضواء النمو ست ساعات. تبقى الحرارة ودعم الحياة لهما الأولوية حتى تتعافى البطارية.")
		"toggle_grow_lights":
			state["growLightsOn"] = not state["growLightsOn"]
			state["growLightsResumeAtMinute"] = -1.0
			_complete("obj_power_budget", "grow_lights")
			if state["growLightsOn"]:
				return _say(true, "Grow lights on. 2.0 kW from the colony bus, no direct Sun.",
					"أضواء النمو تعمل. 2.0 كيلوواط من شبكة المستعمرة، بدون شمس مباشرة.")
			return _say(true, "Grow lights off. The crop slows, but life support keeps its power.",
				"أضواء النمو متوقفة. يتباطأ المحصول، لكن دعم الحياة يحتفظ بطاقته.")
		"medical_checkup":
			state["averageFatigue"] = maxf(0.0, state["averageFatigue"] - 0.1)
			state["averageMorale"] = minf(1.0, state["averageMorale"] + 0.03)
			state["suitEnergy"] = 1.0
			for id in npcs_state.keys():
				npcs_state[id]["fatigue"] = maxf(0.0, float(npcs_state[id].get("fatigue", 0.2)) - 0.1)
			_complete("obj_crew_rest", "checkup")
			return _say(true, "Check-up logged. Resting heart rates, hydration and dose are within limits.",
				"سُجل الفحص. معدلات القلب أثناء الراحة والترطيب والجرعة ضمن الحدود.")
		"exercise":
			state["averageFatigue"] = maxf(0.0, state["averageFatigue"] - 0.05)
			state["averageMorale"] = minf(1.0, state["averageMorale"] + 0.04)
			state["suitEnergy"] = 1.0
			_complete("obj_crew_rest", "exercise")
			return _say(true, "Resistance session done. Bone and muscle load counters the low gravity.",
				"انتهت جلسة المقاومة. الحمل العضلي والعظمي يوازن الجاذبية المنخفضة.")
		"process_regolith":
			state["o2Kg"] = minf(state["o2CapacityKg"], state["o2Kg"] + 0.2)
			state["dataPoints"] = float(state["dataPoints"]) + 1.0
			_complete("obj_air_scrubber", "intake")
			return _say(true, "Regolith feed screened. The oxygen plant extracts a measured 0.2 kg of O2.",
				"فُحصت تغذية الثرى. يستخلص مصنع الأكسجين 0.2 كغ من الأكسجين.")
		"repair_array":
			state["arrayHealth"] = minf(1.0, state["arrayHealth"] + 0.12)
			_complete("obj_solar_dust", "repair_array")
			return _say(true, "Damaged cover glass replaced on two strings. Array health is back up.",
				"استُبدل الزجاج المتضرر في سلسلتين. عادت صحة الألواح.")
		_:
			return _say(false, "Nothing happened.", "لم يحدث شيء.")


func _say(ok: bool, en: String, ar: String) -> Dictionary:
	return {"ok": ok, "en": en, "ar": ar}


func _thousands(value: float) -> String:
	var n := int(round(value))
	var text := str(n)
	var out := ""
	var count := 0
	for i in range(text.length() - 1, -1, -1):
		out = text[i] + out
		count += 1
		if count % 3 == 0 and i > 0:
			out = "," + out
	return out


# ---- dialogue --------------------------------------------------------------

## A conversation is mechanical: the topic deepens a memory, nudges trust, plays its line and can
## unlock the objective the crew member was carrying. Same contract as the C# and JS cores.
func converse(npc_id: String, topic_id: String) -> Dictionary:
	var topic := Content.topic(npc_id, topic_id)
	if topic.is_empty():
		return _say(false, "", "")
	var state_npc: Dictionary = npcs_state.get(npc_id, {})
	var memory: Array = state_npc.get("memory", [])
	memory.append(str(topic.get("id", "")))
	if memory.size() > 12:
		memory = memory.slice(memory.size() - 12, memory.size())
	state_npc["memory"] = memory
	state_npc["trust"] = clampf(float(state_npc.get("trust", 0.5)) + 0.03, 0.0, 1.0)
	npcs_state[npc_id] = state_npc
	var objective_id := str(topic.get("learning", ""))
	if objective_id == "":
		objective_id = str(topic.get("objectiveId", ""))
	_unlock(objective_id)
	return {
		"ok": true,
		"en": str(topic.get("lineEn", "")),
		"ar": str(topic.get("lineAr", "")),
		"voiceKey": str(topic.get("voiceKey", "")),
	}


# ---- journal ---------------------------------------------------------------

func has_entry(objective_id: String) -> bool:
	for entry in journal:
		if entry.get("objectiveId", "") == objective_id:
			return true
	return false


func _unlock(objective_id: String) -> Dictionary:
	if objective_id == "" or has_entry(objective_id):
		return {}
	var obj := Content.objective(objective_id)
	if obj.is_empty():
		return {}
	var entry := {
		"objectiveId": obj.get("id", ""),
		"titleEn": obj.get("titleEn", ""),
		"titleAr": obj.get("titleAr", ""),
		"bodyEn": obj.get("bodyEn", ""),
		"bodyAr": obj.get("bodyAr", ""),
		"skill": obj.get("skill", ""),
		"evidenceClass": obj.get("evidenceClass", ""),
		"source": obj.get("source", ""),
		"unlockedAtMinute": clock.total_minutes,
	}
	journal.append(entry)
	var skill: String = entry["skill"]
	if skill != "":
		skills[skill] = int(skills.get(skill, 0)) + 1
	return entry


## Finishes a task and closes any incident that the same action resolves.
func _complete(objective_id: String, action: String) -> void:
	_unlock(objective_id)
	var remaining: Array = []
	for a in incidents_active:
		var def := Content.incident(str(a.get("definitionId", "")))
		if def.is_empty():
			continue
		var matches: bool = str(def.get("action", "")) == action \
			or str(def.get("learning", "")) == objective_id
		if not matches:
			remaining.append(a)
	incidents_active = remaining


# ---- save contract (schema 2, same keys as the C# core) ---------------------

func capture_save(extra: Dictionary = {}) -> Dictionary:
	var npcs_out := {}
	for id in npcs_state.keys():
		var st: Dictionary = npcs_state[id]
		npcs_out[id] = {
			"trust": st.get("trust", 0.5),
			"fatigue": st.get("fatigue", 0.2),
			"memory": st.get("memory", []),
		}
	var colony := {
		"batteryKwh": state["batteryKwh"],
		"arrayHealth": state["arrayHealth"],
		"arrayDust": state["arrayDust"],
		"sensorDust": state["sensorDust"],
		"airlockDust": state["airlockDust"],
		"waterL": state["waterL"],
		"filterHealth": state["filterHealth"],
		"waterQuality": state["waterQuality"],
		"o2Kg": state["o2Kg"],
		"co2Kg": state["co2Kg"],
		"pressureKpa": state["cabinPressureKpa"],
		"scrubberHealth": state["scrubberHealth"],
		"cabinTempC": state["cabinTempC"],
		"foodKcal": state["foodKcal"],
		"cropBiomass": state["cropBiomassKg"],
		"cropHealth": state["cropHealth"],
		"cropWaterStress": state["cropWaterStress"],
		"doseMsv": state["cumulativeDoseMsv"],
		"signal": state["signalQuality"],
		"relayHealth": state["relayHealth"],
		"fatigue": state["averageFatigue"],
		"suitEnergy": state["suitEnergy"],
		"samples": state["samplesCollected"],
		"sheltered": state["crewSheltered"],
		"growLights": state["growLightsOn"],
		"o2Generator": state["o2GeneratorRunning"],
		"recycler": state["recyclerRunning"],
		"spe": state["solarParticleEvent"],
		"speIntensity": state["speIntensity"],
	}
	var journal_ids: Array = []
	for entry in journal:
		journal_ids.append(entry.get("objectiveId", ""))
	return {
		"schemaVersion": SAVE_SCHEMA,
		"savedAt": Time.get_datetime_string_from_system(true),
		"playerName": player_name,
		"language": "ar" if arabic else "en",
		"clockMinutes": clock.total_minutes,
		"colony": colony,
		"npcs": npcs_out,
		"journal": journal_ids,
		"skills": skills.duplicate(),
		"resolved": [],
		"ignored": [],
		"extras": extra.merged(extras, false),
	}


func apply_save(data: Dictionary) -> bool:
	if data.is_empty():
		return false
	var schema := int(data.get("schemaVersion", 1))
	if schema < 1 or schema > SAVE_SCHEMA:
		push_warning("Unsupported save schema %d; expected <= %d" % [schema, SAVE_SCHEMA])
		return false

	player_name = str(data.get("playerName", player_name))
	arabic = str(data.get("language", "en")) == "ar"
	clock = LQClock.new(float(data.get("clockMinutes", 0.0)))

	var c: Dictionary = data.get("colony", {})
	state["batteryKwh"] = float(c.get("batteryKwh", 96.0))
	state["arrayHealth"] = float(c.get("arrayHealth", 1.0))
	state["arrayDust"] = float(c.get("arrayDust", 0.04))
	state["sensorDust"] = float(c.get("sensorDust", 0.03))
	state["airlockDust"] = float(c.get("airlockDust", 0.05))
	state["waterL"] = float(c.get("waterL", 900.0))
	state["filterHealth"] = float(c.get("filterHealth", 1.0))
	state["waterQuality"] = float(c.get("waterQuality", 0.98))
	state["o2Kg"] = float(c.get("o2Kg", 83.7))
	state["co2Kg"] = float(c.get("co2Kg", 0.35))
	state["cabinPressureKpa"] = float(c.get("pressureKpa", 101.3))
	state["scrubberHealth"] = float(c.get("scrubberHealth", 1.0))
	state["cabinTempC"] = float(c.get("cabinTempC", 21.0))
	state["foodKcal"] = float(c.get("foodKcal", 482000.0))
	state["cropBiomassKg"] = float(c.get("cropBiomass", 0.0))
	state["cropHealth"] = float(c.get("cropHealth", 1.0))
	state["cropWaterStress"] = float(c.get("cropWaterStress", 0.0))
	state["cumulativeDoseMsv"] = float(c.get("doseMsv", 0.0))
	state["signalQuality"] = float(c.get("signal", 1.0))
	state["relayHealth"] = float(c.get("relayHealth", 1.0))
	state["averageFatigue"] = float(c.get("fatigue", 0.0))
	state["suitEnergy"] = float(c.get("suitEnergy", 1.0))
	state["samplesCollected"] = int(c.get("samples", 0))
	state["crewSheltered"] = bool(c.get("sheltered", false))
	state["growLightsOn"] = bool(c.get("growLights", true))
	state["o2GeneratorRunning"] = bool(c.get("o2Generator", true))
	state["recyclerRunning"] = bool(c.get("recycler", true))
	state["solarParticleEvent"] = bool(c.get("spe", false))
	state["speIntensity"] = float(c.get("speIntensity", 0.0))

	var npcs_in: Dictionary = data.get("npcs", {})
	for id in npcs_in.keys():
		if not npcs_state.has(id):
			npcs_state[id] = {"location": "", "activityEn": "", "activityAr": "", "trust": 0.5, "fatigue": 0.2, "memory": []}
		var v: Dictionary = npcs_in[id]
		npcs_state[id]["trust"] = float(v.get("trust", 0.5))
		npcs_state[id]["fatigue"] = float(v.get("fatigue", 0.2))
		var memory = v.get("memory", [])
		npcs_state[id]["memory"] = memory if typeof(memory) == TYPE_ARRAY else []

	journal.clear()
	skills.clear()
	for id in data.get("journal", []):
		_unlock(str(id))
	var skills_in: Dictionary = data.get("skills", {})
	for skill in skills_in.keys():
		skills[skill] = int(skills_in[skill])

	extras = data.get("extras", {})
	_fired_beats.clear()
	for id in extras.get("firedBeats", []):
		_fired_beats[str(id)] = true
	_last_day = clock.day()
	_spe_played = clock.day() > 12 or state["solarParticleEvent"]
	_accumulator = 0.0
	_notice_at.clear()
	return true


## A compact summary for the mission-end card.
func summary() -> Dictionary:
	return {
		"day": mini(clock.day(), LQClock.MISSION_DAYS),
		"journal": journal.size(),
		"objectives": Content.objectives.size(),
		"samples": int(state["samplesCollected"]),
		"doseMsv": state["cumulativeDoseMsv"],
		"morale": state["averageMorale"],
		"fatigue": state["averageFatigue"],
		"foodDays": food_days_remaining(),
		"dataPoints": state["dataPoints"],
	}
