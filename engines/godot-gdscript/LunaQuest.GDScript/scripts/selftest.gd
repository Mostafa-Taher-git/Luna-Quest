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

## One Moon, numerically. LQTerrain (scripts/terrain.gd) is a hand port of the shared
## LunaQuest.Core/Terrain/LunarTerrain.cs; these are the heights the shared algorithm produces for
## seed 1337, and the self-test refuses to pass if the port drifts. tools/check-engine-parity.py
## recomputes the same table from the C# algorithm on every push, so the numbers cannot be
## "adjusted" to match a broken port: the table is the contract, and the engine must meet it.
## (0, 23) and (52, -34) are inside the 88 m flat pad: exactly zero.
const TERRAIN_PARITY := [
	[0.0, 23.0, 0.0],
	[52.0, -34.0, 0.0],
	[95.0, 8.0, -0.066552338049],
	[200.0, 200.0, -7.724299839182],
	[-350.0, 120.0, 3.014551430629],
	[300.0, -300.0, 0.332602820384],
	[1000.0, 0.0, 4.653101754171],
]

const BEATS := ["welcome", "dust_front", "heavy_eva", "spe_onset", "comms_glitch", "dust_storm",
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
	_test_terrain()
	_test_clock()
	var flown := _test_thirty_days()
	_test_actions()
	_test_objectives()
	_test_board()
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


# ---- terrain ---------------------------------------------------------------

## The GDScript port must reproduce the shared heightfield exactly (to float precision).
func _test_terrain() -> void:
	var terrain := LQTerrain.new(1337)
	for row in TERRAIN_PARITY:
		var x: float = row[0]
		var z: float = row[1]
		var expected: float = row[2]
		var actual := terrain.height(x, z)
		var delta := absf(actual - expected)
		expect(delta < 0.001, "terrain: (%.0f, %.0f) is %.6f in the engine, %.6f in the shared C#" % [
			x, z, actual, expected])
		if absf(expected) == 0.0:
			expect(actual == 0.0, "terrain: (%.0f, %.0f) is inside the flat pad but the port says %.6f" % [
				x, z, actual])
	# The pad is flat by construction: a ring of points inside it must all read zero.
	for i in range(8):
		var angle := TAU * float(i) / 8.0
		var x := 60.0 * cos(angle)
		var z := 8.0 + 60.0 * sin(angle)
		expect(terrain.height(x, z) == 0.0, "terrain: the flat pad is not flat at (%.1f, %.1f)" % [x, z])


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

	# The two frozen assets must be present. A clean checkout needs
	# tools/sync-gdscript-assets.sh once; CI runs it before this harness. A missing model must
	# fail here instead of silently turning every astronaut into a different shape at runtime.
	expect(ResourceLoader.exists(ColonyCrew.ASTRONAUT_SCENE),
		"assets: the Cute Astronaut GLB is missing; run tools/sync-gdscript-assets.sh")
	expect(ResourceLoader.exists("res://assets/video/intro.ogv"),
		"assets: the intro stream is missing; run tools/sync-gdscript-assets.sh")
	expect(ColonyCrew.CREW_HEIGHT_M == 1.75,
		"assets: the crew height drifted from the shared 1.75 m")


# ---- clock -----------------------------------------------------------------

func _test_clock() -> void:
	var clock := LQClock.new(0.0)
	expect(clock.day() == 1, "clock: a new clock should be day 1")
	expect(clock.hour_of_day() < 0.01, "clock: a new clock should start at 00:00")
	clock.step(60.0)
	expect(int(clock.hour_of_day()) == 12,
		"clock: one real minute should be twelve mission hours, got %f" % clock.hour_of_day())
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


## The shift task board, as data: fourteen rows, every learning objective on it, both languages
## distinct, the rover tasks following the rover, and the shift boundaries where they belong.
func _test_board() -> void:
	var sim := LQSim.new()
	var rows: Array = LunaHud.board_rows(sim, null, false)
	expect(rows.size() == 14, "board: expected 14 rows, found %d" % rows.size())

	var ids := {}
	var journals := 0
	var rovers := 0
	var missions := 0
	for row in rows:
		var id := str(row["id"])
		expect(not ids.has(id), "board: duplicate row id %s" % id)
		ids[id] = true
		expect(str(row["label"]) != "", "board: %s has no label" % id)
		expect(str(row["hint"]) != "", "board: %s has no hint" % id)
		match str(row["kind"]):
			"journal":
				journals += 1
				expect(str(row["shift"]) in ["morning", "afternoon", "evening", "night"],
					"board: %s carries shift '%s'" % [id, str(row["shift"])])
			"rover_driven", "rover_ice":
				rovers += 1
			"mission":
				missions += 1
	expect(journals == EXPECTED_OBJECTIVES,
		"board: %d journal rows, expected %d" % [journals, EXPECTED_OBJECTIVES])
	expect(rovers == 2, "board: %d rover rows, expected 2" % rovers)
	expect(missions == 1, "board: %d mission rows, expected 1" % missions)

	# Every objective in content/learning.json is on the board, and the board invents nothing
	# that is not an objective, a rover milestone or the mission line itself.
	for objective in Content.objectives:
		expect(ids.has(str(objective.get("id", ""))),
			"board: objective %s is missing from the board" % str(objective.get("id", "")))

	# Both languages: the Arabic pass carries its own text for every single row.
	var ar_rows: Array = LunaHud.board_rows(sim, null, true)
	expect(ar_rows.size() == rows.size(), "board: the Arabic pass changed the row count")
	for i in rows.size():
		expect(str(ar_rows[i]["label"]) != "" and str(ar_rows[i]["label"]) != str(rows[i]["label"]),
			"board: row %d has no distinct Arabic label" % i)

	# The rover tasks read the rover's own milestones: pending without one, then flipped by
	# driving it and again by reaching the ice site.
	expect(not bool(rows[4]["done"]) and not bool(rows[5]["done"]),
		"board: a rover task is already done before the rover has moved")
	var rover_ref := ColonyRover.new()
	rover_ref.has_driven = true
	var driven: Array = LunaHud.board_rows(sim, rover_ref, false)
	expect(bool(driven[4]["done"]), "board: taking the rover did not mark its task")
	expect(not bool(driven[5]["done"]), "board: the ice task completed without reaching the site")
	rover_ref.reached_ice = true
	var arrived: Array = LunaHud.board_rows(sim, rover_ref, false)
	expect(bool(arrived[5]["done"]), "board: reaching the ice site did not mark its task")
	rover_ref.free()

	# Shift boundaries: 06:00 opens the morning shift, 12:00 the afternoon, 18:00 the evening,
	# and everything before six is night.
	expect(str(LunaHud.current_shift(0.0)["id"]) == "night", "board: 00:00 is not the night shift")
	expect(str(LunaHud.current_shift(5.99)["id"]) == "night", "board: 05:59 is not the night shift")
	expect(str(LunaHud.current_shift(6.0)["id"]) == "morning", "board: 06:00 is not morning")
	expect(str(LunaHud.current_shift(11.99)["id"]) == "morning", "board: 11:59 is not morning")
	expect(str(LunaHud.current_shift(12.0)["id"]) == "afternoon", "board: 12:00 is not afternoon")
	expect(str(LunaHud.current_shift(17.99)["id"]) == "afternoon", "board: 17:59 is not afternoon")
	expect(str(LunaHud.current_shift(18.0)["id"]) == "evening", "board: 18:00 is not evening")
	expect(str(LunaHud.current_shift(23.99)["id"]) == "evening", "board: 23:59 is not evening")


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
	# The character lines — greetings, barks and every dialogue topic — must have audio. The fifteen
	# incident headlines are narration: the game prints them as events and survives without a file,
	# which is why they are counted rather than required.
	var missing: Array = []
	var narration := 0
	for line in lines:
		var key := str(line.get("key", ""))
		if key == "":
			missing.append("(blank key)")
			continue
		if not Voice.has_key(key):
			if key.begins_with("inc."):
				narration += 1
			else:
				missing.append(key)
	expect(missing.is_empty(), "voice: %d spoken lines have no audio file (%s)" % [
		missing.size(), str(missing.slice(0, 4))])
	expect(narration <= 15, "voice: %d incident lines are narration-only, expected at most 15" % narration)
	return true


# ---- save contract ---------------------------------------------------------

func _test_save() -> void:
	var sim := LQSim.new()
	sim.player_name = "Amara Okafor"
	sim.step(600.0)
	sim.try_action("clean_arrays")
	sim.converse("npc_safaa", str(Content.topics_for("npc_safaa")[0].get("id", "")))

	var data: Dictionary = sim.capture_save({"playerName": sim.player_name, "arabic": true})
	expect(int(data.get("schemaVersion", 0)) == LQSim.SAVE_SCHEMA,
		"save: schema version should be %d" % LQSim.SAVE_SCHEMA)
	expect(str(data.get("playerName", "")) == "Amara Okafor", "save: the commander's name was lost")
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
	expect(str(restored.player_name) == "Amara Okafor", "save: the name did not restore")
	var before: Dictionary = sim.npcs_state.get("npc_safaa", {})
	var after: Dictionary = restored.npcs_state.get("npc_safaa", {})
	expect(absf(float(before.get("trust", 0.0)) - float(after.get("trust", 0.0))) < 0.001,
		"save: crew trust did not restore")

	# A save at the end of the mission must still load: the report screen depends on it.
	sim.step(400.0)
	var late: Dictionary = sim.capture_save()
	var late_sim := LQSim.new()
	expect(late_sim.apply_save(late), "save: a late-mission save did not load")
