class_name LQSelfTest
extends RefCounted
## The GDScript edition's own harness. Runs headlessly with
##
##   godot --headless --path engines/godot-gdscript/LunaQuest.GDScript -- --selftest
##
## and exits non-zero if anything is wrong, so CI can gate on it. It flies the whole thirty days,
## opens every console, tries every action, talks to every crew member about every topic, verifies
## the voice pack resolves and round-trips a save through JSON. No rendering, no GPU, no display.

const EXPECTED_BUILDINGS := 16
const EXPECTED_CREW := 7
const EXPECTED_INCIDENTS := 15
const EXPECTED_OBJECTIVES := 11
const EXPECTED_RESEARCH := 17
const EXPECTED_TOPICS := 44

const BEATS := ["welcome", "dust_front", "heavy_eva", "spe", "comms_glitch", "dust_storm",
	"crop_stress", "filter_clog", "insulation_fault", "mission_end"]

const ACTIONS := ["clean_arrays", "clean_sensors", "clean_airlock", "harvest_crops",
	"inspect_crop", "toggle_grow_lights", "clean_filter", "replace_scrubber", "repair_o2",
	"repair_relay", "repair_array", "balance_load", "collect_sample", "shelter", "rest_crew",
	"medical_checkup", "exercise", "process_regolith"]

var checks := 0
var failures: Array = []


func expect(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures.append(label)


func run() -> bool:
	print("Luna-Quest (GDScript) selftest — Godot %s" % Engine.get_version_info()["string"])
	_test_content()
	_test_clock()
	var flown := _test_thirty_days()
	_test_actions()
	_test_objectives()
	_test_dialogue()
	_test_voice_pack()
	_test_save()
	return _report(flown)


func _report(flown: Dictionary) -> bool:
	print("  flew %d simulated days, %d events raised, %d beats fired" % [
		int(flown.get("days", 0)), int(flown.get("events", 0)), int(flown.get("beats", 0))])
	if failures.is_empty():
		print("SELFTEST OK — %d checks passed" % checks)
		return true
	print("SELFTEST FAILED — %d of %d checks failed:" % [failures.size(), checks])
	for failure in failures:
		print("  · %s" % failure)
	return false


# ---- content ---------------------------------------------------------------

func _test_content() -> void:
	expect(Content.buildings.size() == EXPECTED_BUILDINGS,
		"content: expected %d buildings, found %d" % [EXPECTED_BUILDINGS, Content.buildings.size()])
	expect(Content.npcs.size() == EXPECTED_CREW,
		"content: expected %d crew, found %d" % [EXPECTED_CREW, Content.npcs.size()])
	expect(Content.incidents.size() == EXPECTED_INCIDENTS,
		"content: expected %d incidents, found %d" % [EXPECTED_INCIDENTS, Content.incidents.size()])
	expect(Content.objectives.size() == EXPECTED_OBJECTIVES,
		"content: expected %d objectives, found %d" % [EXPECTED_OBJECTIVES, Content.objectives.size()])
	expect(Content.research.size() == EXPECTED_RESEARCH,
		"content: expected %d research notes, found %d" % [EXPECTED_RESEARCH, Content.research.size()])
	expect(Content.topics.size() == EXPECTED_TOPICS,
		"content: expected %d dialogue topics, found %d" % [EXPECTED_TOPICS, Content.topics.size()])

	for building in Content.buildings:
		var id := str(building.get("id", ""))
		expect(id != "", "content: a building has no id")
		expect(str(building.get("nameEn", "")) != "" and str(building.get("nameAr", "")) != "",
			"content: %s is missing a bilingual name" % id)
		expect(building.get("position", []) is Array and building.get("footprint", []) is Array,
			"content: %s has no position or footprint" % id)
		for point in building.get("interactions", []):
			expect(str(point.get("id", "")) != "", "content: %s has a console without an id" % id)
			expect(str(point.get("nameEn", "")) != "" and str(point.get("nameAr", "")) != "",
				"content: %s/%s is missing a bilingual label" % [id, point.get("id", "")])

	for npc in Content.npcs:
		expect(str(npc.get("nameEn", "")) != "" and str(npc.get("nameAr", "")) != "",
			"content: a crew member has no bilingual name")
		expect(not npc.get("schedule", []).is_empty(),
			"content: %s has no schedule" % npc.get("id", ""))
		expect(not Content.topics_for(str(npc.get("id", ""))).is_empty(),
			"content: %s has no dialogue topics" % npc.get("id", ""))

	for incident in Content.incidents:
		var action := str(incident.get("action", ""))
		expect(ACTIONS.has(action) or LQSim.ACTION_ALIASES.has(action),
			"content: incident %s names an unknown action '%s'" % [incident.get("id", ""), action])

	# Every objective must be reachable from a console, or it is not playable.
	for objective in Content.objectives:
		var found := false
		for building in Content.buildings:
			for point in building.get("interactions", []):
				if str(point.get("learning", "")) == str(objective.get("id", "")):
					found = true
		expect(found, "content: objective %s has no console" % objective.get("id", ""))


# ---- clock -----------------------------------------------------------------

func _test_clock() -> void:
	var clock := LQClock.new(0.0)
	expect(clock.day() == 1, "clock: a new clock should be day 1")
	expect(clock.hour_of_day() < 0.01, "clock: a new clock should start at 00:00")
	clock.step(60.0)
	expect(absf(clock.hour_of_day() - 8.0) < 0.01, "clock: one real minute is eight mission hours")
	var elevations: Array = []
	for i in range(48):
		clock.step(7.0)
		elevations.append(clock.sun_elevation_deg())
		expect(clock.solar_irradiance_fraction() >= 0.6 and clock.solar_irradiance_fraction() <= 1.0,
			"clock: irradiance outside its anchored band")
	var low: float = elevations.min()
	var high: float = elevations.max()
	expect(low >= 8.0 and high <= 14.0, "clock: sun elevation outside 11° ± 2.4° (%f..%f)" % [low, high])
	expect(clock.stamp().begins_with("Day"), "clock: the English stamp should start with Day")
	expect(clock.stamp(true).contains("اليوم"), "clock: the Arabic stamp should be in Arabic")


# ---- the whole mission -----------------------------------------------------

func _test_thirty_days() -> Dictionary:
	var sim := LQSim.new()
	sim.player_name = "Selftest"
	var seen_beats := {}
	var event_count := 0
	var seconds := 0.0
	var min_battery := 999.0
	var min_o2 := 999.0
	var min_water := 999.0
	var min_temp := 999.0
	var max_temp := -999.0
	var worst: Array = []

	while not sim.clock.is_mission_complete() and seconds < 30.0 * 24.0 * 60.0 * 60.0 / 12.0:
		var raised: Array = sim.step(300.0)
		seconds += 300.0
		for event in raised:
			event_count += 1
			seen_beats[str(event.get("id", ""))] = true
		min_battery = minf(min_battery, sim.battery_percent())
		min_o2 = minf(min_o2, sim.o2_percent())
		min_water = minf(min_water, sim.water_percent())
		min_temp = minf(min_temp, float(sim.state["cabinTempC"]))
		max_temp = maxf(max_temp, float(sim.state["cabinTempC"]))
		if is_nan(float(sim.state["batteryKwh"])) or is_nan(float(sim.state["o2Kg"])):
			worst.append("non-finite resource at day %d" % sim.clock.day())
			break

	expect(sim.clock.is_mission_complete(), "flight: thirty days did not complete")
	expect(sim.clock.day() > LQClock.MISSION_DAYS, "flight: the clock stopped short of day 31")
	expect(event_count >= 10, "flight: only %d events in a whole mission" % event_count)
	for beat in BEATS:
		expect(seen_beats.has(beat), "flight: the '%s' beat never fired" % beat)
	expect(worst.is_empty(), "flight: %s" % str(worst))

	# Physical quantities stay finite and inside their tanks for the entire mission.
	expect(min_battery >= 0.0 and sim.battery_percent() <= 1.0, "flight: battery left 0..100%%")
	expect(min_o2 >= 0.0, "flight: oxygen went negative")
	expect(min_water >= 0.0, "flight: water went negative")
	expect(float(sim.state["foodKcal"]) >= 0.0, "flight: food went negative")
	expect(min_temp > -60.0 and max_temp < 80.0,
		"flight: cabin temperature left a survivable band (%f..%f)" % [min_temp, max_temp])
	var summary := sim.summary()
	expect(int(summary["day"]) == LQClock.MISSION_DAYS, "flight: the summary should report day 30")
	expect(float(summary["doseMsv"]) > 0.0, "flight: no radiation dose accumulated in thirty days")
	print("  resource floors: battery %.0f%%, oxygen %.0f%%, water %.0f%%, cabin %.1f..%.1f °C" % [
		min_battery * 100.0, min_o2 * 100.0, min_water * 100.0, min_temp, max_temp])

	return {"days": sim.clock.day() - 1, "events": event_count, "beats": seen_beats.size()}


# ---- player actions --------------------------------------------------------

func _test_actions() -> void:
	var sim := LQSim.new()
	for action in ACTIONS:
		var result: Dictionary = sim.try_action(action)
		expect(result.has("ok") and result.has("en") and result.has("ar"),
			"actions: %s did not return an ok/en/ar result" % action)
		expect(str(result.get("en", "")) != "", "actions: %s returned an empty English line" % action)

	for building in Content.buildings:
		for point in building.get("interactions", []):
			var result: Dictionary = sim.interact(str(building.get("id", "")), str(point.get("id", "")))
			expect(str(result.get("en", "")) != "",
				"consoles: %s/%s said nothing" % [building.get("id", ""), point.get("id", "")])
			expect(str(result.get("ar", "")) != "",
				"consoles: %s/%s has no Arabic line" % [building.get("id", ""), point.get("id", "")])

	var unknown: Dictionary = sim.interact("habitat", "there_is_no_such_console")
	expect(unknown.get("ok", true) == false, "consoles: an unknown console should decline politely")


func _test_objectives() -> void:
	var sim := LQSim.new()
	for building in Content.buildings:
		for point in building.get("interactions", []):
			if str(point.get("learning", "")) != "":
				sim.interact(str(building.get("id", "")), str(point.get("id", "")))
	expect(sim.journal.size() == Content.objectives.size(),
		"journal: reading every console unlocked %d of %d objectives" % [
			sim.journal.size(), Content.objectives.size()])
	for entry in sim.journal:
		expect(str(entry.get("titleEn", "")) != "" and str(entry.get("titleAr", "")) != "",
			"journal: an entry is missing a bilingual title")
		expect(str(entry.get("bodyEn", "")) != "", "journal: an entry has no body")
		expect(str(entry.get("skill", "")) != "", "journal: an entry has no skill")
	expect(not sim.skills.is_empty(), "journal: no skills were credited")


# ---- conversation and the voice pack ---------------------------------------

func _test_dialogue() -> void:
	var sim := LQSim.new()
	for npc in Content.npcs:
		var npc_id := str(npc.get("id", ""))
		for topic in Content.topics_for(npc_id):
			var result: Dictionary = sim.converse(npc_id, str(topic.get("id", "")))
			expect(result.get("ok", false), "dialogue: %s/%s refused to talk" % [npc_id, topic.get("id", "")])
			expect(str(result.get("en", "")) != "" and str(result.get("ar", "")) != "",
				"dialogue: %s/%s has no bilingual line" % [npc_id, topic.get("id", "")])
		var state: Dictionary = sim.npcs_state.get(npc_id, {})
		expect(not state.get("memory", []).is_empty(), "dialogue: %s remembered nothing" % npc_id)
		expect(float(state.get("trust", 0.0)) > 0.5, "dialogue: talking did not earn %s's trust" % npc_id)


func _test_voice_pack() -> bool:
	var path := "res://data/voices.json"
	if not FileAccess.file_exists(path):
		expect(false, "voice: data/voices.json is missing")
		return false
	var file := FileAccess.open(path, FileAccess.READ)
	var text := file.get_as_text()
	file.close()
	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		expect(false, "voice: voices.json did not parse")
		return false
	var lines: Array = parsed.get("lines", [])
	expect(lines.size() >= 40, "voice: only %d lines in the pack" % lines.size())
	var missing: Array = []
	for line in lines:
		var key := str(line.get("key", ""))
		if key == "" or not Voice.has_key(key):
			missing.append(key)
	expect(missing.is_empty(), "voice: %d lines have no audio file (%s)" % [
		missing.size(), str(missing.slice(0, 4))])
	return true


# ---- save contract ---------------------------------------------------------

func _test_save() -> void:
	var sim := LQSim.new()
	sim.player_name = "Mostafa"
	sim.step(600.0)
	sim.try_action("clean_arrays")
	sim.converse("npc_safaa", str(Content.topics_for("npc_safaa")[0].get("id", "")))

	var data: Dictionary = sim.capture_save({"playerName": sim.player_name, "arabic": true})
	expect(int(data.get("schemaVersion", 0)) == LQSim.SAVE_SCHEMA,
		"save: schema version should be %d" % LQSim.SAVE_SCHEMA)
	expect(str(data.get("playerName", "")) == "Mostafa", "save: the commander's name was lost")
	expect(data.get("extras", {}).get("arabic", false) == true, "save: the language choice was lost")

	var json := JSON.stringify(data)
	var parsed = JSON.parse_string(json)
	expect(typeof(parsed) == TYPE_DICTIONARY, "save: the file did not survive a JSON round trip")

	var restored := LQSim.new()
	expect(restored.apply_save(parsed), "save: a fresh simulation refused the save")
	expect(absf(restored.clock.total_minutes - sim.clock.total_minutes) < 0.01,
		"save: the clock did not restore (%.2f vs %.2f)" % [
			restored.clock.total_minutes, sim.clock.total_minutes])
	expect(absf(float(restored.state["batteryKwh"]) - float(sim.state["batteryKwh"])) < 0.01,
		"save: the battery did not restore")
	expect(restored.journal.size() == sim.journal.size(), "save: the journal did not restore")
	expect(str(restored.player_name) == "Mostafa", "save: the name did not restore")
	var before: Dictionary = sim.npcs_state.get("npc_safaa", {})
	var after: Dictionary = restored.npcs_state.get("npc_safaa", {})
	expect(absf(float(before.get("trust", 0.0)) - float(after.get("trust", 0.0))) < 0.001,
		"save: crew trust did not restore")

	# A save at the end of the mission must still load: the report screen depends on it.
	sim.step(400.0)
	var late: Dictionary = sim.capture_save()
	var late_sim := LQSim.new()
	expect(late_sim.apply_save(late), "save: a late-mission save did not load")
