class_name LunaHud
extends CanvasLayer
## The interface: gauges that never guess, a prompt you can act on, a journal that is proof of
## work done, and the two screens that hold the game together — the start card and the mission
## report.
##
## Everything is built in code so the whole UI can be read in one file, and every string exists
## in English and Arabic because the colony is bilingual by design, not by translation pass.

signal start_requested(player_name: String)
signal resume_requested
signal quit_requested
signal restart_requested
signal topic_requested(npc_id: String, topic_id: String)
signal dialogue_closed
signal language_toggled

const FONT_LATIN := "res://assets/noto-sans-latin.woff"
const FONT_ARABIC := "res://assets/noto-sans-arabic.woff"
const ACCENT := Color(0.91, 0.58, 0.23)
const TEXT := Color(0.90, 0.92, 0.95)
const DIM := Color(0.66, 0.70, 0.76)
const PANEL_BG := Color(0.06, 0.08, 0.11, 0.92)
## Finished rows read as a calm teal rather than a highlight: a done task is settled, not urgent.
const DONE := Color(0.45, 0.80, 0.72)

## The shift task board — 14 rows. Eleven are the learning objectives from content/learning.json
## (done = a journal entry), two are the crew rover's own milestones, and one is the mission line.
## `kind` says how `done` is read, `shift` says where the row leads. Ported from the browser
## edition's web/src/ui/hud.js taskList(); learning.json itself stays at 11 objectives (H9).
const BOARD_ROWS: Array = [
	{"id": "obj_power_budget", "kind": "journal", "shift": "morning",
		"en": "Read the colony board in Mission Control", "ar": "اقرأ لوحة المستعمرة في مركز التحكم",
		"hint": "E at the board · Mission Control"},
	{"id": "obj_solar_dust", "kind": "journal", "shift": "morning",
		"en": "Clean the solar array when dust cuts output", "ar": "نظّف الألواح الشمسية",
		"hint": "Power farm"},
	{"id": "obj_water_filter", "kind": "journal", "shift": "morning",
		"en": "Check the water loop and back-flush the filter", "ar": "افحص الماء والفلتر",
		"hint": "Life support"},
	{"id": "obj_sample", "kind": "journal", "shift": "afternoon",
		"en": "Collect a regolith sample at the ice site", "ar": "اجمع عينة من التربة في موقع الجليد",
		"hint": "Ice site · sampler console"},
	{"id": "obj_rover_take", "kind": "rover_driven", "shift": "afternoon",
		"en": "Take the crew rover from the garage apron", "ar": "خذ مركبة الطاقم من أرضية المرآب",
		"hint": "Garage · E to drive"},
	{"id": "obj_rover_ice", "kind": "rover_ice", "shift": "afternoon",
		"en": "Drive the crew rover out to the ice prospecting site",
		"ar": "قد المركبة إلى موقع استكشاف الجليد", "hint": "Ice site · 52, -34"},
	{"id": "obj_sensor_dust", "kind": "journal", "shift": "afternoon",
		"en": "Brush the sensor heads and the antenna", "ar": "نظّف رؤوس الحساسات والهوائي",
		"hint": "Comms tower"},
	{"id": "obj_plant_care", "kind": "journal", "shift": "afternoon",
		"en": "Harvest the grow pod", "ar": "احصد المحصول", "hint": "Greenhouse"},
	{"id": "obj_airlock_dust", "kind": "journal", "shift": "evening",
		"en": "Sweep the airlock dust before the next EVA", "ar": "نظّف غرفة المعادل قبل الخروج التالي",
		"hint": "Airlock"},
	{"id": "obj_comms_relay", "kind": "journal", "shift": "evening",
		"en": "Realign the comms relay", "ar": "أعد ضبط مرحّل الاتصالات",
		"hint": "Mission Control · comms desk"},
	{"id": "obj_air_scrubber", "kind": "journal", "shift": "evening",
		"en": "Restart electrolysis and log the scrubber",
		"ar": "أعد تشغيل التحليل الكهربائي وسجّل الغسّال", "hint": "Life support"},
	{"id": "obj_crew_rest", "kind": "journal", "shift": "night",
		"en": "Rebalance the crew's rest plan", "ar": "أعد خطة الراحة للطاقم", "hint": "Habitat"},
	{"id": "obj_radiation", "kind": "journal", "shift": "night",
		"en": "Shelter the crew during a particle event", "ar": "احمِ الطاقم عند حدث الجسيمات",
		"hint": "Habitat · shielded section"},
	{"id": "mission", "kind": "mission", "shift": "any",
		"en": "Survive the mission — day %d of 30", "ar": "أكمل المهمة — اليوم %d من 30",
		"hint": ""},
]

var root: Control
var hud_layer: Control
var top_values: Dictionary = {}
var board_box: VBoxContainer
var board_header: Label
var board_row_labels: Array = []
var _board_shift := ""
var crosshair: Control
var prompt_label: Label
var subtitle_label: Label
var toast_box: VBoxContainer
var journal_panel: PanelContainer
var journal_body: VBoxContainer
var help_panel: PanelContainer
var pause_panel: PanelContainer
var dialogue_panel: PanelContainer
var dialogue_body: VBoxContainer
var start_screen: Control
var end_screen: PanelContainer
var end_body: VBoxContainer
var intro_layer: Control
var intro_video: VideoStreamPlayer
var intro_skip: Button
var name_edit: LineEdit
var lang_button: Button
var arabic := false
var ui_theme := Theme.new()
var _dialogue_npc := ""


func _ready() -> void:
	layer = 10
	_build_theme()
	_build_hud()
	_build_panels()
	_build_start_screen()
	show_hud(false)


# ---- theme -----------------------------------------------------------------

func _build_theme() -> void:
	ui_theme.default_font_size = 16
	if ResourceLoader.exists(FONT_LATIN):
		var latin = load(FONT_LATIN)
		if latin is Font:
			if ResourceLoader.exists(FONT_ARABIC):
				var arabic_font = load(FONT_ARABIC)
				if arabic_font is Font:
					latin.set_fallbacks([arabic_font])
			ui_theme.default_font = latin
	ui_theme.set_color("font_color", "Label", TEXT)
	ui_theme.set_color("font_color", "Button", TEXT)
	ui_theme.set_stylebox("panel", "PanelContainer", _panel_style())
	ui_theme.set_stylebox("normal", "Button", _button_style(false))
	ui_theme.set_stylebox("hover", "Button", _button_style(true))
	ui_theme.set_stylebox("pressed", "Button", _button_style(true))
	ui_theme.set_constant("margin_left", "MarginContainer", 18)
	ui_theme.set_constant("margin_right", "MarginContainer", 18)


func _panel_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL_BG
	style.border_color = Color(ACCENT.r, ACCENT.g, ACCENT.b, 0.55)
	style.set_border_width_all(1)
	style.set_corner_radius_all(6)
	style.set_content_margin_all(14)
	return style


func _button_style(hover: bool) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.16, 0.18, 0.22, 0.96) if not hover else Color(0.85, 0.52, 0.19, 0.92)
	style.set_corner_radius_all(4)
	style.set_content_margin_all(10)
	return style


func _label(text: String, size := 16, color := TEXT) -> Label:
	var node := Label.new()
	node.text = text
	node.add_theme_font_size_override("font_size", size)
	node.add_theme_color_override("font_color", color)
	return node


# ---- live HUD --------------------------------------------------------------

func _build_hud() -> void:
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.theme = ui_theme
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	hud_layer = Control.new()
	hud_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hud_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(hud_layer)

	# Gauges across the top.
	var bar := PanelContainer.new()
	bar.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	bar.offset_left = 18
	bar.offset_right = -18
	bar.offset_top = 14
	hud_layer.add_child(bar)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 22)
	bar.add_child(row)
	for key in ["day", "power", "o2", "water", "food", "temp", "co2", "dose", "signal"]:
		var cell := VBoxContainer.new()
		cell.add_theme_constant_override("separation", 0)
		var name_label := _label(_gauge_name(key), 12, DIM)
		var value_label := _label("—", 18, TEXT)
		cell.add_child(name_label)
		cell.add_child(value_label)
		row.add_child(cell)
		top_values[key] = value_label

	# The shift task board under the gauges: a header naming the current shift, then the 14 rows.
	# The nodes are built once in canonical order and only moved when the shift changes, so a
	# per-frame refresh costs a text swap rather than a rebuild.
	board_box = VBoxContainer.new()
	board_box.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	board_box.offset_left = 20
	board_box.offset_top = 92
	board_box.offset_right = 690
	board_box.add_theme_constant_override("separation", 2)
	board_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(board_box)

	board_header = _label("", 15, ACCENT)
	board_box.add_child(board_header)
	board_row_labels.clear()
	for _spec in BOARD_ROWS:
		var line := _label("", 14, TEXT)
		line.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		line.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		board_box.add_child(line)
		board_row_labels.append(line)

	# Crosshair: a dot you can find on a bright module and lose against a black sky, with a thin
	# ring so the eye reads it without hunting.
	var aim := Control.new()
	aim.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	aim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(aim)
	var ring := Panel.new()
	ring.custom_minimum_size = Vector2(22, 22)
	ring.size = Vector2(22, 22)
	ring.position = Vector2(-11, -11)
	ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var ring_style := StyleBoxFlat.new()
	ring_style.bg_color = Color(1, 1, 1, 0.06)
	ring_style.border_color = Color(1, 1, 1, 0.45)
	ring_style.set_border_width_all(1)
	ring_style.set_corner_radius_all(11)
	ring.add_theme_stylebox_override("panel", ring_style)
	aim.add_child(ring)
	var dot := ColorRect.new()
	dot.color = Color(1, 1, 1, 0.9)
	dot.size = Vector2(4, 4)
	dot.position = Vector2(-2, -2)
	dot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	aim.add_child(dot)
	crosshair = aim

	# Prompt at the bottom centre.
	prompt_label = _label("", 19, TEXT)
	prompt_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	prompt_label.offset_left = -420
	prompt_label.offset_right = 420
	prompt_label.offset_top = -96
	prompt_label.offset_bottom = -60
	prompt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hud_layer.add_child(prompt_label)

	# Subtitles: the spoken line, always shown, always attributed.
	subtitle_label = _label("", 18, Color(0.96, 0.94, 0.88))
	subtitle_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	subtitle_label.offset_left = -480
	subtitle_label.offset_right = 480
	subtitle_label.offset_top = -150
	subtitle_label.offset_bottom = -104
	subtitle_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	subtitle_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hud_layer.add_child(subtitle_label)

	# Event toasts, top right.
	toast_box = VBoxContainer.new()
	toast_box.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	toast_box.offset_left = -420
	toast_box.offset_right = -18
	toast_box.offset_top = 110
	toast_box.add_theme_constant_override("separation", 8)
	toast_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_layer.add_child(toast_box)


func _gauge_name(key: String) -> String:
	match key:
		"day": return "MISSION"
		"power": return "BATTERY"
		"o2": return "OXYGEN"
		"water": return "WATER"
		"food": return "FOOD"
		"temp": return "CABIN"
		"co2": return "CO₂"
		"dose": return "DOSE RATE"
		"signal": return "EARTH LINK"
		_: return key.to_upper()


func refresh(sim: LQSim, rover = null) -> void:
	if top_values.is_empty():
		return
	top_values["day"].text = sim.clock.stamp(arabic)
	top_values["power"].text = "%d%%" % int(round(sim.battery_percent() * 100.0))
	top_values["o2"].text = "%d%%" % int(round(sim.o2_percent() * 100.0))
	top_values["water"].text = "%d%%" % int(round(sim.water_percent() * 100.0))
	top_values["food"].text = "%.1f d" % sim.food_days_remaining()
	top_values["temp"].text = "%.0f°C" % sim.state["cabinTempC"]
	top_values["co2"].text = "%d ppm" % int(round(sim.co2_ppm()))
	top_values["dose"].text = "%.0f µGy/h" % sim.state["doseRateMicroGyH"]
	top_values["signal"].text = sim.comms_band().to_upper()

	# Values that are out of family go amber, not red: nothing here is a fail state.
	_colour(top_values["power"], sim.battery_percent() < 0.25)
	_colour(top_values["o2"], sim.o2_percent() < 0.35)
	_colour(top_values["water"], sim.water_percent() < 0.2)
	_colour(top_values["food"], sim.food_days_remaining() < 4.0)
	_colour(top_values["temp"], sim.state["cabinTempC"] < 18.0)
	_colour(top_values["co2"], sim.co2_ppm() > 5000.0)
	_colour(top_values["dose"], sim.state["doseRateMicroGyH"] > 60.0)
	_colour(top_values["signal"], sim.state["signalQuality"] < 0.45)

	_update_board(sim, rover)


## Which shift the clock is in, with its bilingual header. Six to twelve is morning, twelve to
## eighteen is afternoon, eighteen and later is evening, and everything before six is night.
static func current_shift(hour: float) -> Dictionary:
	if hour >= 6.0 and hour < 12.0:
		return {"id": "morning", "en": "Morning shift - power, water, air",
			"ar": "مناوبة الصباح - الطاقة والماء والهواء"}
	if hour >= 12.0 and hour < 18.0:
		return {"id": "afternoon", "en": "Afternoon shift - samples and crops",
			"ar": "مناوبة الظهيرة - العينات والمحاصيل"}
	if hour >= 18.0:
		return {"id": "evening", "en": "Evening shift - dust, comms, oxygen",
			"ar": "مناوبة المساء - الغبار والاتصالات والأكسجين"}
	return {"id": "night", "en": "Night shift - rest and radiation watch",
		"ar": "مناوبة الليل - الراحة ومراقبة الإشعاع"}


## Every row resolved against the live simulation: the label in the requested language, the hint
## that says where to stand, and whether the row is finished. The rover rows read the rover's own
## milestones; `rover` may be null before the vehicle exists, which simply leaves them pending.
static func board_rows(sim: LQSim, rover = null, arabic := false) -> Array:
	var out: Array = []
	var day: int = mini(sim.clock.day(), 30)
	for row in BOARD_ROWS:
		var kind := str(row["kind"])
		var id := str(row["id"])
		var label := str(row["ar"]) if arabic else str(row["en"])
		var hint := str(row["hint"])
		var done := false
		match kind:
			"journal":
				done = sim.has_entry(id)
			"rover_driven":
				done = rover != null and rover.has_driven
			"rover_ice":
				done = rover != null and rover.reached_ice
			"mission":
				done = sim.clock.day() > 30
				if arabic:
					label = "أكمل المهمة — اليوم %d من 30" % day
				else:
					label = "Survive the mission — day %d of 30" % day
				hint = "%d%%" % int(round(sim.clock.mission_progress() * 100.0))
		out.append({"id": id, "kind": kind, "shift": str(row["shift"]),
			"label": label, "hint": hint, "done": done})
	return out


## Paint the board. The nodes keep their canonical identity — `board_row_labels[i]` always
## describes `BOARD_ROWS[i]` — so only their position in the box ever changes with the shift.
func _update_board(sim: LQSim, rover) -> void:
	if board_row_labels.is_empty():
		return
	var shift: Dictionary = current_shift(sim.clock.hour_of_day())
	board_header.text = str(shift["ar"]) if arabic else str(shift["en"])

	var shift_id := str(shift["id"])
	if shift_id != _board_shift:
		_board_shift = shift_id
		# The current shift leads and everything behind it keeps its canonical order, so pulling
		# the leading rows to the front in a single pass leaves the whole board correctly sorted.
		var at := 1 # index 0 is the header
		for i in BOARD_ROWS.size():
			if str(BOARD_ROWS[i]["shift"]) == shift_id:
				board_box.move_child(board_row_labels[i], at)
				at += 1

	var rows := board_rows(sim, rover, arabic)
	for i in BOARD_ROWS.size():
		var row: Dictionary = rows[i]
		var text := str(row["label"])
		var hint := str(row["hint"])
		if hint != "":
			text = "%s  ·  %s" % [text, hint]
		if bool(row["done"]):
			text = "✓ " + text
		var line: Label = board_row_labels[i]
		line.text = text
		line.add_theme_color_override("font_color", DONE if bool(row["done"]) else TEXT)


func _colour(label: Label, warn: bool) -> void:
	label.add_theme_color_override("font_color", ACCENT if warn else TEXT)


func show_hud(on: bool) -> void:
	hud_layer.visible = on


func set_crosshair(on: bool) -> void:
	if crosshair != null:
		crosshair.visible = on


func set_prompt(text: String) -> void:
	prompt_label.text = text


func set_subtitle(text: String) -> void:
	subtitle_label.text = text


func show_event(event: Dictionary, arabic: bool) -> void:
	var title := str(event.get("titleAr" if arabic else "titleEn", ""))
	var body := str(event.get("bodyAr" if arabic else "bodyEn", ""))
	if title == "":
		return
	var card := PanelContainer.new()
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var column := VBoxContainer.new()
	var kind := str(event.get("kind", "info"))
	var colour := ACCENT
	match kind:
		"critical": colour = Color(0.92, 0.35, 0.32)
		"warning": colour = Color(0.95, 0.68, 0.25)
		"discovery": colour = Color(0.45, 0.80, 0.72)
		_: colour = Color(0.72, 0.78, 0.86)
	var heading := _label(title, 16, colour)
	heading.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(heading)
	if body != "":
		var text := _label(body, 13, DIM)
		text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		text.custom_minimum_size = Vector2(360, 0)
		column.add_child(text)
	card.add_child(column)
	toast_box.add_child(card)
	while toast_box.get_child_count() > 4:
		var oldest := toast_box.get_child(0)
		toast_box.remove_child(oldest)
		oldest.queue_free()
	var timer := Timer.new()
	timer.wait_time = 9.0
	timer.one_shot = true
	timer.autostart = true
	timer.timeout.connect(func():
		if is_instance_valid(card):
			card.queue_free())
	card.add_child(timer)


# ---- panels ----------------------------------------------------------------

func _build_panels() -> void:
	var journal := _make_panel("Journal", "دفتر المهام")
	journal_panel = journal["panel"]
	journal_body = journal["body"]
	journal_panel.visible = false
	root.add_child(journal_panel)

	var help := _make_panel("Controls", "التحكم")
	help_panel = help["panel"]
	var help_body: VBoxContainer = help["body"]
	for line in [
		"W A S D — walk · Shift — run · Space — hop",
		"Mouse — look · E — use console / talk",
		"V — first/third person · J — journal · Tab — fast-forward",
		"F5 / F9 — save / load · L — language · Esc — pause",
		"",
		"Nothing here is a fail state. Read the gauge, change one variable, wait.",
	]:
		help_body.add_child(_label(line, 16, TEXT))
	help_panel.visible = false
	root.add_child(help_panel)

	var pause := _make_panel("Paused", "متوقف")
	pause_panel = pause["panel"]
	var pause_body: VBoxContainer = pause["body"]
	var resume := Button.new()
	resume.text = "Resume  ·  متابعة"
	resume.pressed.connect(func(): resume_requested.emit())
	pause_body.add_child(resume)
	lang_button = Button.new()
	lang_button.text = "Language: English"
	lang_button.pressed.connect(func(): language_toggled.emit())
	pause_body.add_child(lang_button)
	var restart := Button.new()
	restart.text = "Restart the mission  ·  إعادة المهمة"
	restart.pressed.connect(func(): restart_requested.emit())
	pause_body.add_child(restart)
	var quit := Button.new()
	quit.text = "Back to the title  ·  إلى البداية"
	quit.pressed.connect(func(): quit_requested.emit())
	pause_body.add_child(quit)
	pause_panel.visible = false
	root.add_child(pause_panel)

	var dialogue := _make_panel("", "")
	dialogue_panel = dialogue["panel"]
	dialogue_body = dialogue["body"]
	dialogue_panel.visible = false
	root.add_child(dialogue_panel)

	var end := _make_panel("Mission report", "تقرير المهمة")
	end_screen = end["panel"]
	end_body = end["body"]
	end_screen.visible = false
	end_screen.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	end_screen.offset_left = -320
	end_screen.offset_right = 320
	end_screen.offset_top = -230
	end_screen.offset_bottom = 230
	root.add_child(end_screen)


func _make_panel(title_en: String, title_ar: String) -> Dictionary:
	var panel := PanelContainer.new()
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -360
	panel.offset_right = 360
	panel.offset_top = -260
	panel.offset_bottom = 260
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(660, 440)
	var body := VBoxContainer.new()
	body.add_theme_constant_override("separation", 8)
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if title_en != "":
		body.add_child(_label(title_en, 22, ACCENT))
		body.add_child(_label(title_ar, 18, DIM))
	scroll.add_child(body)
	panel.add_child(scroll)
	return {"panel": panel, "body": body}


func open_journal(sim: LQSim) -> void:
	for child in journal_body.get_children():
		child.queue_free()
	var total := Content.objectives.size()
	journal_body.add_child(_label("Tasks: %d of %d" % [sim.journal.size(), total], 18, TEXT))
	journal_body.add_child(_label("المهام: %d من %d" % [sim.journal.size(), total], 16, DIM))
	journal_body.add_child(_label("", 8, DIM))
	if sim.journal.is_empty():
		journal_body.add_child(_label("Nothing recorded yet. Read a console, clean an array, "
			+ "harvest a crop — the journal is proof of work.", 15, DIM))
	for entry in sim.journal:
		var title := _label("• " + str(entry.get("titleEn", "")), 17, TEXT)
		title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		journal_body.add_child(title)
		var arabic_title := _label("  " + str(entry.get("titleAr", "")), 15, DIM)
		arabic_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		journal_body.add_child(arabic_title)
		var body := _label("  " + str(entry.get("bodyEn", "")), 14, DIM)
		body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		journal_body.add_child(body)
		journal_body.add_child(_label("  Skill: " + str(entry.get("skill", "")), 13, ACCENT))
	journal_body.add_child(_label("", 8, DIM))
	journal_body.add_child(_label("Close with J or Esc.", 14, DIM))
	journal_panel.visible = true


func open_dialogue(profile: Dictionary, line: String, sim: LQSim) -> void:
	_dialogue_npc = str(profile.get("id", ""))
	for child in dialogue_body.get_children():
		child.queue_free()
	var name_label := _label(str(profile.get("nameEn", "")), 22, ACCENT)
	dialogue_body.add_child(name_label)
	dialogue_body.add_child(_label(str(profile.get("nameAr", "")), 17, DIM))
	dialogue_body.add_child(_label(str(profile.get("roleEn", "")), 14, DIM))
	dialogue_body.add_child(_label("", 6, DIM))
	var greeting := _label(line, 16, TEXT)
	greeting.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	greeting.custom_minimum_size = Vector2(640, 0)
	dialogue_body.add_child(greeting)

	var skills: Array = profile.get("skills", [])
	if not skills.is_empty():
		dialogue_body.add_child(_label("Skills: " + ", ".join(skills), 13, DIM))

	dialogue_body.add_child(_label("", 6, DIM))
	for topic in Content.topics_for(_dialogue_npc):
		var button := Button.new()
		button.text = str(topic.get("labelEn", "")) + "  ·  " + str(topic.get("labelAr", ""))
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var topic_id := str(topic.get("id", ""))
		button.pressed.connect(func(): topic_requested.emit(_dialogue_npc, topic_id))
		dialogue_body.add_child(button)

	var close := Button.new()
	close.text = "Close  ·  إغلاق"
	close.pressed.connect(func():
		close_dialogue()
		dialogue_closed.emit())
	dialogue_body.add_child(close)
	set_crosshair(false)
	dialogue_panel.visible = true


func set_dialogue_line(text: String) -> void:
	set_subtitle(text)


func close_dialogue() -> void:
	dialogue_panel.visible = false
	set_crosshair(true)
	_dialogue_npc = ""


func is_dialogue_open() -> bool:
	return dialogue_panel != null and dialogue_panel.visible


func toggle_journal(sim: LQSim) -> void:
	if journal_panel.visible:
		journal_panel.visible = false
	else:
		open_journal(sim)


func toggle_help() -> void:
	help_panel.visible = not help_panel.visible


func set_paused(on: bool) -> void:
	pause_panel.visible = on
	set_crosshair(not on)


func set_language(arabic_on: bool) -> void:
	arabic = arabic_on
	if lang_button != null:
		lang_button.text = "Language: العربية" if arabic_on else "Language: English"


# ---- start and end screens -------------------------------------------------

func _build_start_screen() -> void:
	start_screen = Control.new()
	start_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	start_screen.mouse_filter = Control.MOUSE_FILTER_STOP
	root.add_child(start_screen)

	var backdrop := ColorRect.new()
	backdrop.color = Color(0.015, 0.02, 0.03, 0.96)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	start_screen.add_child(backdrop)

	var column := VBoxContainer.new()
	column.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	column.offset_left = -420
	column.offset_right = 420
	column.offset_top = -260
	column.offset_bottom = 260
	column.add_theme_constant_override("separation", 12)
	start_screen.add_child(column)

	var title := _label("LUNA-QUEST", 54, ACCENT)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(title)
	var subtitle := _label("The Living Moon Colony  ·  المستعمرة القمرية الحية", 20, TEXT)
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(subtitle)
	var brief := _label("Shackleton rim, 89.9° S. Thirty Earth days. Six crew, one commander and NEXO.\n"
		+ "Read every console, keep nine people alive, and finish the mission with the colony still running.",
		15, DIM)
	brief.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	brief.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(brief)
	column.add_child(_label("", 10, DIM))

	name_edit = LineEdit.new()
	name_edit.text = "Amara Okafor"
	name_edit.placeholder_text = "Commander's name"
	name_edit.custom_minimum_size = Vector2(360, 44)
	name_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(name_edit)

	var begin := Button.new()
	begin.text = "Begin the mission  ·  ابدأ المهمة"
	begin.custom_minimum_size = Vector2(360, 48)
	begin.pressed.connect(func():
		start_requested.emit(name_edit.text.strip_edges()))
	column.add_child(begin)

	var load_button := Button.new()
	load_button.text = "Continue from a save  ·  متابعة من حفظ"
	load_button.custom_minimum_size = Vector2(360, 40)
	load_button.pressed.connect(func(): resume_requested.emit())
	column.add_child(load_button)

	var note := _label("Pure GDScript build · Web, Windows and Linux exports · "
		+ "the same content and save file as the Godot .NET and Unity projects", 13, DIM)
	note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(note)


func hide_start_screen() -> void:
	start_screen.visible = false


func show_start_screen() -> void:
	start_screen.visible = true
	end_screen.visible = false


func show_end_screen(sim: LQSim) -> void:
	for child in end_body.get_children():
		child.queue_free()
	var summary := sim.summary()
	end_body.add_child(_label("Thirty days complete", 24, ACCENT))
	end_body.add_child(_label("أكملت ثلاثين يومًا", 18, DIM))
	end_body.add_child(_label("", 8, DIM))
	end_body.add_child(_label("Tasks recorded: %d of %d" % [summary["journal"], summary["objectives"]], 16, TEXT))
	end_body.add_child(_label("Samples collected: %d" % summary["samples"], 16, TEXT))
	end_body.add_child(_label("Cumulative dose: %.2f mSv" % summary["doseMsv"], 16, TEXT))
	end_body.add_child(_label("Crew morale: %d%%  ·  fatigue %d%%" % [
		int(round(summary["morale"] * 100.0)), int(round(summary["fatigue"] * 100.0))], 16, TEXT))
	end_body.add_child(_label("Food remaining: %.1f days" % summary["foodDays"], 16, TEXT))
	end_body.add_child(_label("", 10, DIM))
	var again := Button.new()
	again.text = "Fly it again  ·  أعد المهمة"
	again.pressed.connect(func(): restart_requested.emit())
	end_body.add_child(again)
	end_screen.visible = true


# ---- intro film ------------------------------------------------------------

## Plays the same intro film the other builds use. Returns false when the stream is missing, in
## which case the caller goes straight to the title card — a browser without Theora, for example.
func play_intro(path: String) -> bool:
	if not ResourceLoader.exists(path):
		return false
	var stream = load(path)
	if stream == null:
		return false
	intro_layer = Control.new()
	intro_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(intro_layer)

	intro_video = VideoStreamPlayer.new()
	intro_video.stream = stream
	intro_video.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	intro_video.expand = true
	intro_layer.add_child(intro_video)

	intro_skip = Button.new()
	intro_skip.text = "Skip  ·  تخطٍ"
	intro_skip.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	intro_skip.offset_left = -160
	intro_skip.offset_top = -70
	intro_skip.offset_right = -24
	intro_skip.offset_bottom = -24
	intro_skip.pressed.connect(func(): stop_intro())
	intro_layer.add_child(intro_skip)

	intro_video.finished.connect(func(): stop_intro())
	intro_video.play()

	# Watchdog: if the platform cannot decode the film (a browser without Theora, for instance) the
	# player must still reach the title card instead of a black rectangle with a Skip button.
	var guard := Timer.new()
	var length: float = intro_video.get_stream_length()
	guard.wait_time = maxf(6.0, length + 1.5)
	guard.one_shot = true
	guard.autostart = true
	guard.timeout.connect(func(): stop_intro())
	add_child(guard)
	return true


func stop_intro() -> void:
	if intro_layer == null:
		return
	intro_layer.visible = false
	if intro_video != null:
		intro_video.stop()
	intro_layer.queue_free()
	intro_layer = null
	intro_video = null
