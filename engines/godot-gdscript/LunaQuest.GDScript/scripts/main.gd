extends Node3D
## Luna-Quest — the GDScript edition.
##
## One script owns the flow: title card, intro film, the fixed-step simulation, the player, the
## crew, the interface, saving and the mission report at the end. Everything else is a component
## that this script assembles — world, player, crew, HUD, voice — so there is exactly one place to
## read if you want to know how a session runs.

const INTRO_STREAM := "res://assets/video/intro.ogv"
const SAVE_PATH := "user://lunaquest-save.json"
const OPENING_MINUTE := 8 * 60
const SPAWN := Vector3(0.0, 0.3, -14.0)

var sim: LQSim
var world: ColonyWorld
var player: ColonyPlayer
var crew: ColonyCrew
var hud: LunaHud
var rover: ColonyRover

var commander_name := "Amara Okafor"
var playing := false
var paused := false
var arabic := false
var dialogue_open := false
var mission_complete := false

var _hud_clock := 0.0
var _schedule_clock := 0.0
var _subtitle_until := 0


func _ready() -> void:
	randomize()

	# The browser build gets the same scene at 4× MSAA? No: software GPUs and integrated laptop
	# chips both feel that, so the web export renders without it and lets the picture carry itself.
	if OS.has_feature("web"):
		get_viewport().msaa_3d = Viewport.MSAA_DISABLED
		Engine.max_fps = 60

	# `godot --headless --path . -- --selftest` flies the whole mission and exits non-zero on any
	# broken rule. CI runs it before every build, and so can anyone with Godot.
	if OS.get_cmdline_user_args().has("--selftest"):
		var passed := LQSelfTest.new().run()
		get_tree().quit(0 if passed else 1)
		return

	hud = LunaHud.new()
	hud.name = "Hud"
	add_child(hud)
	hud.start_requested.connect(_on_start_requested)
	hud.resume_requested.connect(_on_resume_requested)
	hud.quit_requested.connect(_on_quit_requested)
	hud.restart_requested.connect(_on_restart_requested)
	hud.topic_requested.connect(_on_topic_requested)
	hud.dialogue_closed.connect(_on_dialogue_closed)
	hud.language_toggled.connect(_toggle_language)

	world = ColonyWorld.new()
	world.name = "Colony"
	add_child(world)
	world.build()

	# The rideable crew rover, parked on the garage apron beside the static kit rovers.
	rover = ColonyRover.new()
	rover.name = "CrewRover"
	add_child(rover)
	rover.setup(world)
	rover.position = Vector3(-11.0, 0.05, -28.0)
	rover.rotation.y = 0.4
	rover.settle()

	sim = LQSim.new()
	sim.clock.total_minutes = OPENING_MINUTE
	sim.arabic = arabic

	player = ColonyPlayer.new()
	player.name = "Commander"
	player.position = SPAWN
	player.rotation.y = PI
	player.can_move = false
	add_child(player)
	var suit := crew_astronaut()
	if suit != null:
		player.attach_astronaut(suit)

	crew = ColonyCrew.new()
	crew.name = "Crew"
	add_child(crew)
	crew.build(world, sim)

	world.point_sun(sim.clock)

	# `--audit` walks the finished colony with physics queries: doorways, consoles, spawn, crew.
	if OS.get_cmdline_user_args().has("--audit"):
		await get_tree().physics_frame
		await get_tree().physics_frame
		var passed := LQAudit.new().run(get_tree(), world, player, crew, hud, sim)
		get_tree().quit(0 if passed else 1)
		return

	hud.show_hud(false)
	hud.set_language(arabic)
	hud.refresh(sim)

	if DisplayServer.get_name() != "headless":
		if not hud.play_intro(INTRO_STREAM):
			print("Luna-Quest (GDScript): intro stream unavailable; starting at the title card.")

	print("Luna-Quest (GDScript): ready — Godot %s, %d buildings, %d crew, %d tasks" % [
		Engine.get_version_info()["string"], Content.buildings.size(), crew.members.size(),
		Content.objectives.size()])


## The commander's third-person body: the same Cute Astronaut the colony walks around in. A
## missing model is reported, never answered with a differently shaped stand-in.
func crew_astronaut() -> Node3D:
	var holder := Node3D.new()
	holder.name = "Suit"
	if ResourceLoader.exists(ColonyCrew.ASTRONAUT_SCENE):
		var scene := load(ColonyCrew.ASTRONAUT_SCENE) as PackedScene
		if scene != null:
			var model := scene.instantiate() as Node3D
			holder.add_child(model)
			crew_fit(holder, ColonyCrew.CREW_HEIGHT_M)
			return holder
	push_error("Luna-Quest: the Cute Astronaut GLB is missing. Run tools/sync-gdscript-assets.sh before launching. No substitute astronaut will be drawn.")
	return holder


func crew_fit(node: Node3D, target_height: float) -> void:
	var bounds := AABB()
	var first := true
	for child in node.find_children("*", "MeshInstance3D", true, false):
		var mesh_node := child as MeshInstance3D
		if mesh_node == null or mesh_node.mesh == null:
			continue
		# The suit is measured before it joins the tree, so only the local transform is valid.
		var box: AABB = mesh_node.transform * mesh_node.get_aabb()
		if first:
			bounds = box
			first = false
		else:
			bounds = bounds.merge(box)
	if first or bounds.size.y < 0.001:
		return
	var factor := target_height / bounds.size.y
	node.scale = Vector3(factor, factor, factor)


# ---- session flow ----------------------------------------------------------

func _on_start_requested(player_name: String) -> void:
	var cleaned := player_name.strip_edges()
	if cleaned != "":
		commander_name = cleaned
	# "Begin" always means a fresh thirty days; "Continue from a save" is the other button.
	_restart_mission()


func _begin_session() -> void:
	playing = true
	paused = false
	mission_complete = false
	player.can_move = true
	player.position = SPAWN
	player.rotation.y = PI
	player.set_third_person(false)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	hud.hide_start_screen()
	hud.show_hud(true)
	hud.set_paused(false)
	hud.refresh(sim)
	hud.set_subtitle("")
	Voice.set_enabled(true)
	Voice.play_key("crew.safaa.greet")


func _on_resume_requested() -> void:
	if _load_game():
		_begin_session()
	else:
		hud.set_subtitle("No save file yet — begin a mission and press F5.")


func _on_quit_requested() -> void:
	playing = false
	paused = false
	player.can_move = false
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	hud.show_hud(false)
	hud.set_paused(false)
	hud.show_start_screen()


func _on_restart_requested() -> void:
	_restart_mission()


func _restart_mission() -> void:
	sim = LQSim.new()
	sim.clock.total_minutes = OPENING_MINUTE
	sim.arabic = arabic
	sim.player_name = commander_name
	mission_complete = false
	crew.queue_free()
	crew = ColonyCrew.new()
	add_child(crew)
	crew.build(world, sim)
	if rover != null:
		if rover.occupied:
			rover.exit(player)
		rover.position = Vector3(-11.0, 0.05, -28.0)
		rover.rotation.y = 0.4
		rover.battery_kwh = ColonyRover.BATTERY_KWH
		rover.speed = 0.0
		# A new mission starts with a clean rover: no milestones carried over.
		rover.has_driven = false
		rover.reached_ice = false
		rover.odometer = 0.0
		rover.steer = 0.0
		rover.spin = 0.0
		rover.settle()
	for child in hud.toast_box.get_children():
		child.queue_free()
	_begin_session()


func _on_mission_complete() -> void:
	mission_complete = true
	playing = false
	player.can_move = false
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	hud.show_end_screen(sim)
	Voice.play_key("crew.safaa.farewell")


# ---- the loop --------------------------------------------------------------

func _process(delta: float) -> void:
	# --selftest and a failed startup quit before the colony exists; the frames that follow must
	# not touch the simulation or the world.
	if sim == null or world == null:
		return
	if playing and not paused:
		var fast := Input.is_key_pressed(KEY_TAB) or Input.is_key_pressed(KEY_T)
		var speed := 6.0 if fast else 1.0
		var raised: Array = sim.step(delta * speed)
		for event in raised:
			_on_event(event)

		crew.update(delta, player.global_position)
		_schedule_clock += delta
		if _schedule_clock >= 1.0:
			_schedule_clock = 0.0
			crew.apply_schedule()

		_update_prompt()
		_hud_clock += delta
		if _hud_clock >= 0.2:
			_hud_clock = 0.0
			hud.refresh(sim)

		if not mission_complete and sim.clock.is_mission_complete():
			_on_mission_complete()

	world.point_sun(sim.clock)

	if _subtitle_until > 0 and Time.get_ticks_msec() > _subtitle_until:
		_subtitle_until = 0
		hud.set_subtitle("")


func _update_prompt() -> void:
	if not playing or paused or dialogue_open:
		hud.set_prompt("")
		return
	if rover != null and rover.occupied:
		var kmh := int(rover.speed_kmh())
		var batt := int(rover.battery_percent() * 100.0)
		var out := "اخرج" if arabic else "step out"
		hud.set_prompt("[E] %s · %d km/h · %d%%" % [out, kmh, batt])
		return
	var target := player.target()
	if target.is_empty():
		hud.set_prompt("")
		return
	if target["type"] == "rover":
		hud.set_prompt("[E]  %s" % ("قد مركبة الطاقم" if arabic else "drive the crew rover"))
		return
	var action := "استخدام" if arabic else "use"
	if target["type"] == "npc":
		action = "تحدث" if arabic else "talk"
	hud.set_prompt("[E]  %s  ·  %s" % [
		tr_label(target["label_en"], target["label_ar"], arabic), action])


func tr_label(english: String, arabic_text: String, use_arabic: bool) -> String:
	return arabic_text if use_arabic else english


func _on_event(event: Dictionary) -> void:
	hud.show_event(event, arabic)
	var title := str(event.get("titleAr", "")) if arabic else str(event.get("titleEn", ""))
	var body := str(event.get("bodyAr", "")) if arabic else str(event.get("bodyEn", ""))
	if title != "":
		hud.set_subtitle(body if body != "" else title)
		_subtitle_until = Time.get_ticks_msec() + 9000
	var key := str(event.get("voiceKey", ""))
	if key != "":
		Voice.play_key(key)


# ---- input -----------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and playing and not paused and not dialogue_open:
		if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
			return
	if not (event is InputEventKey) or not event.pressed or event.echo:
		return
	var key_event := event as InputEventKey
	match key_event.keycode:
		KEY_ESCAPE:
			if hud.journal_panel.visible:
				hud.journal_panel.visible = false
			elif hud.help_panel.visible:
				hud.help_panel.visible = false
			elif dialogue_open:
				_close_dialogue()
			elif playing:
				_set_paused(not paused)
			else:
				_on_quit_requested()
		KEY_E:
			if playing and not paused and not dialogue_open:
				_interact()
		KEY_J:
			if playing and not paused and not dialogue_open:
				hud.toggle_journal(sim)
		KEY_H:
			if playing and not paused and not dialogue_open:
				hud.toggle_help()
		KEY_V:
			if playing and not paused:
				player.set_third_person(not player.third_person)
		KEY_L:
			_toggle_language()
		KEY_F5:
			if playing:
				_save_game()
		KEY_F9:
			if _load_game() and playing:
				hud.set_subtitle("Save loaded.")
		KEY_F11:
			var window := get_window()
			window.mode = Window.MODE_WINDOWED if window.mode == Window.MODE_FULLSCREEN else Window.MODE_FULLSCREEN


func _set_paused(on: bool) -> void:
	paused = on
	player.can_move = not on
	hud.set_paused(on)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if on else Input.MOUSE_MODE_CAPTURED


func _toggle_language() -> void:
	arabic = not arabic
	sim.arabic = arabic
	hud.set_language(arabic)
	hud.refresh(sim)
	hud.set_subtitle("اللغة العربية" if arabic else "English")


# ---- interaction -----------------------------------------------------------

func _interact() -> void:
	if rover != null and rover.occupied:
		rover.exit(player)
		var msg := "خرجت من المركبة. هي تُشحن على أرضية المرآب." if arabic else "Out of the rover. It recharges on the garage apron."
		hud.set_subtitle(msg)
		_subtitle_until = Time.get_ticks_msec() + 5000
		return
	var target := player.target()
	if target.is_empty():
		return
	if target["type"] == "rover":
		rover.enter(player)
		var msg := "أنت على المقعد. W للسائق، A/D للتوجيه، Space للفرملة، E للخروج." if arabic else "At the wheel. W drive, A/D steer, Space brake, E to step out."
		hud.set_subtitle(msg)
		_subtitle_until = Time.get_ticks_msec() + 7000
		return
	if target["type"] == "console":
		var result: Dictionary = sim.interact(str(target["building_id"]), str(target["interaction_id"]))
		var text := str(result.get("ar", "")) if arabic else str(result.get("en", ""))
		hud.set_subtitle(text)
		_subtitle_until = Time.get_ticks_msec() + 9000
		Voice.play_key("ui.console")
		hud.refresh(sim)
		if result.get("ok", false):
			Voice.play_key("ui.confirm")
	else:
		var npc_id := str(target["npc_id"])
		var profile := Content.npc(npc_id)
		if profile.is_empty():
			return
		dialogue_open = true
		player.can_move = false
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		hud.open_dialogue(profile, sim.npc_line(npc_id), sim)
		Voice.play_key("crew.%s.greet" % npc_id.replace("npc_", ""))


func _on_topic_requested(npc_id: String, topic_id: String) -> void:
	var result := sim.converse(npc_id, topic_id)
	var text := str(result.get("ar", "")) if arabic else str(result.get("en", ""))
	hud.set_dialogue_line(text)
	hud.set_subtitle(text)
	_subtitle_until = Time.get_ticks_msec() + 12000
	hud.refresh(sim)


func _on_dialogue_closed() -> void:
	_close_dialogue()


func _close_dialogue() -> void:
	dialogue_open = false
	player.can_move = not paused
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	hud.close_dialogue()


# ---- saving ----------------------------------------------------------------

func _save_game() -> void:
	var data: Dictionary = sim.capture_save({
		"playerName": sim.player_name,
		"arabic": arabic,
		"savedAt": Time.get_datetime_string_from_system(true),
		"rover": rover.capture() if rover != null else {},
	})
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file == null:
		hud.set_subtitle("Could not write the save file.")
		return
	file.store_string(JSON.stringify(data, "\t"))
	file.close()
	hud.set_subtitle("Saved at %s — day %d." % [
		Time.get_datetime_string_from_system(false, true).substr(11, 5), sim.clock.day()])
	_subtitle_until = Time.get_ticks_msec() + 5000


func _load_game() -> bool:
	if not FileAccess.file_exists(SAVE_PATH):
		return false
	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if file == null:
		return false
	var text := file.get_as_text()
	file.close()
	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		return false
	var data: Dictionary = parsed
	if not sim.apply_save(data):
		return false
	var extras: Dictionary = data.get("extras", {})
	arabic = bool(extras.get("arabic", false))
	sim.arabic = arabic
	hud.set_language(arabic)
	hud.refresh(sim)
	if rover != null:
		rover.restore(extras.get("rover", {}), player)
	return true
