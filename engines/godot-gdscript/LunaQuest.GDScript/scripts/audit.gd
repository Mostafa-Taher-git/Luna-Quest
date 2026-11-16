class_name LQAudit
extends RefCounted
## Walks the built colony with physics queries instead of eyes.
##
##   godot --headless --path engines/godot-gdscript/LunaQuest.GDScript -- --audit
##
## The self-test proves the rules; this proves the *place*. For every module it fires a ray through
## the doorway to show the commander can get in, fires a ray at every console from where they would
## stand to show it can be used, checks the spawn point is clear, that the floor is under the
## commander's feet, and that the traverse markers close the colony off before the collision model
## stops matching the terrain.

var checks := 0
var failures: Array = []
var space: PhysicsDirectSpaceState3D


func expect(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures.append(label)


func ray(from: Vector3, to: Vector3, exclude: Array = []) -> Dictionary:
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collide_with_areas = false
	query.collide_with_bodies = true
	query.exclude = exclude
	return space.intersect_ray(query)


func run(tree: SceneTree, world: ColonyWorld, player: ColonyPlayer, crew: ColonyCrew,
		hud: LunaHud, sim: LQSim) -> bool:
	space = world.get_world_3d().direct_space_state
	var root: Node = player.get_parent()
	var rover := root.get_node_or_null("CrewRover") as ColonyRover
	print("Luna-Quest (GDScript) scene audit")
	_check_spawn(player)
	_check_view(player, world)
	_check_interface(hud, sim, rover)
	_check_doors(world)
	_check_consoles(world)
	_check_crew(world, crew)
	_check_perimeter()
	_check_ground_faces()
	_check_rover(root, world, player, crew, sim)
	_check_f0_controls(root, world, player)
	_check_buildings(root, world)
	if failures.is_empty():
		print("AUDIT OK — %d checks passed" % checks)
		return true
	print("AUDIT FAILED — %d of %d checks failed:" % [failures.size(), checks])
	for failure in failures:
		print("  · %s" % failure)
	return false


func _check_spawn(player: ColonyPlayer) -> void:
	var spawn: Vector3 = player.global_position
	var ground := ray(spawn + Vector3(0, 2.0, 0), spawn - Vector3(0, 1.5, 0), [player.get_rid()])
	expect(not ground.is_empty(), "spawn: there is no floor under the commander")
	if not ground.is_empty():
		var height: float = ground["position"].y
		expect(height > -0.2 and height < 0.35,
			"spawn: the floor under the commander sits at %.2f m" % height)

	var shape := CapsuleShape3D.new()
	shape.radius = 0.34
	shape.height = 1.75
	var params := PhysicsShapeQueryParameters3D.new()
	params.shape = shape
	params.transform = Transform3D(Basis(), spawn + Vector3(0, 0.9, 0))
	params.collide_with_areas = false
	params.exclude = [player.get_rid()]
	var overlap := space.intersect_shape(params, 4)
	expect(overlap.is_empty(), "spawn: the commander starts inside %d colliders" % overlap.size())


## The opening frame: standing at the spawn, looking where the commander faces, there has to be
## colony in front of you rather than a wall half a metre away.
func _check_view(player: ColonyPlayer, world: ColonyWorld) -> void:
	var eye: Vector3 = player.global_position + Vector3(0, 1.66, 0)
	var forward: Vector3 = -player.global_transform.basis.z.normalized()
	var ahead := ray(eye, eye + forward * 14.0, [player.get_rid()])
	var distance := 14.0
	if not ahead.is_empty():
		distance = eye.distance_to(ahead["position"])
	expect(distance > 8.0, "view: the spawn looks into something %.1f m away" % distance)

	# And the light is where the clock says it should be: a Sun above the horizon, not below it.
	if world.sun != null:
		var travel: Vector3 = -world.sun.global_transform.basis.z.normalized()
		expect(travel.y < -0.05, "light: the Sun is not shining down onto the colony")
		expect(world.sun.light_energy > 0.5, "light: the Sun is too dim to see by")


## The interface has to exist and survive the two screens a whole session passes through.
func _check_interface(hud: LunaHud, sim: LQSim, rover) -> void:
	expect(hud != null, "hud: no interface")
	if hud == null:
		return
	expect(hud.top_values.size() == 9, "hud: expected 9 gauges, found %d" % hud.top_values.size())
	expect(hud.crosshair != null, "hud: no crosshair")
	expect(hud.prompt_label != null and hud.subtitle_label != null, "hud: no prompt or subtitle line")
	hud.refresh(sim, rover)
	expect(hud.top_values["day"].text.begins_with("Day"),
		"hud: the mission stamp reads '%s'" % hud.top_values["day"].text)
	expect(hud.top_values["power"].text.ends_with("%"), "hud: the battery gauge has no value")

	hud.set_language(true)
	expect(hud.arabic, "hud: the language toggle did not stick")
	hud.refresh(sim, rover)
	expect(hud.top_values["day"].text.begins_with("اليوم"), "hud: the stamp did not change language")
	_check_board(hud, sim, true)
	hud.set_language(false)
	hud.refresh(sim, rover)
	_check_board(hud, sim, false)

	# Done styling: finishing an objective marks its own row and recolours it, so "done" is
	# legible at a glance rather than only countable in the journal.
	var unlocked := false
	for building in Content.buildings:
		if unlocked:
			break
		for point in building.get("interactions", []):
			if str(point.get("learning", "")) == "obj_power_budget":
				sim.interact(str(building.get("id", "")), str(point.get("id", "")))
				unlocked = true
				break
	hud.refresh(sim, rover)
	if unlocked:
		expect(str(hud.board_row_labels[0].text).begins_with("✓"),
			"board: a finished row is not marked: '%s'" % hud.board_row_labels[0].text)
		expect(hud.board_row_labels[0].get_theme_color("font_color") == LunaHud.DONE,
			"board: a finished row is not recoloured")

	hud.open_journal(sim)
	expect(hud.journal_panel.visible and hud.journal_body.get_child_count() > 0,
		"hud: the journal did not open")
	hud.toggle_journal(sim)
	expect(not hud.journal_panel.visible, "hud: the journal did not close")

	hud.show_event({"kind": "warning", "titleEn": "Audit", "titleAr": "تدقيق",
		"bodyEn": "Event card smoke test.", "bodyAr": "اختبار."}, false)
	expect(hud.toast_box.get_child_count() == 1, "hud: the event card did not appear")

	# The mission report: the screen a player sees after thirty days.
	var finished := LQSim.new()
	finished.clock.total_minutes = LQClock.MISSION_DAYS * LQClock.MINUTES_PER_DAY + 60.0
	hud.show_end_screen(finished)
	expect(hud.end_screen.visible, "hud: the mission report did not open")
	expect(hud.end_body.get_child_count() >= 8,
		"hud: the mission report has only %d lines" % hud.end_body.get_child_count())
	hud.end_screen.visible = false


## The board has to be complete, readable in the language the rest of the HUD is in, and led by
## the row for the shift the clock actually says it is.
func _check_board(hud: LunaHud, sim: LQSim, arabic: bool) -> void:
	expect(hud.board_box != null, "board: there is no task board")
	if hud.board_box == null:
		return
	expect(hud.board_row_labels.size() == 14,
		"board: expected 14 rows, found %d" % hud.board_row_labels.size())
	expect(hud.board_box.get_child_count() == 15,
		"board: the board holds %d children, expected a header plus 14 rows" %
		hud.board_box.get_child_count())

	var shift: Dictionary = LunaHud.current_shift(sim.clock.hour_of_day())
	var expected_header := str(shift["ar"]) if arabic else str(shift["en"])
	expect(str(hud.board_header.text) == expected_header,
		"board: the header reads '%s', expected the %s shift" %
		[str(hud.board_header.text), str(shift["id"])])

	var blanks := 0
	for line in hud.board_row_labels:
		if str(line.text).strip_edges() == "":
			blanks += 1
	expect(blanks == 0, "board: %d rows are blank" % blanks)

	expect(str(hud.board_row_labels[13].text).contains("30"),
		"board: the mission line does not name the 30 days: '%s'" % hud.board_row_labels[13].text)
	if arabic:
		expect(str(hud.board_row_labels[0].text).contains("لوحة"),
			"board: the rows did not change to Arabic: '%s'" % hud.board_row_labels[0].text)

	# The row that visually leads is the first child after the header; it must belong to the
	# shift the clock says it is, which is what "current shift leads" means.
	var leader := hud.board_box.get_child(1) as Label
	var leading := ""
	if leader != null:
		for i in hud.board_row_labels.size():
			if hud.board_row_labels[i] == leader:
				leading = str(LunaHud.BOARD_ROWS[i]["shift"])
	expect(leading == str(shift["id"]),
		"board: the %s row leads but the clock says %s" % [leading, str(shift["id"])])


func _check_doors(world: ColonyWorld) -> void:
	for building in Content.buildings:
		var id := str(building.get("id", ""))
		var footprint: Array = building.get("footprint", [10, 8])
		var position: Array = building.get("position", [0, 0])
		var centre := Vector3(float(position[0]), 0.0, float(position[1]))
		var door: int = world._door_direction(building)
		var normal := Vector3.ZERO
		var extent := 0.0
		match door:
			0:
				normal = Vector3(0, 0, 1)
				extent = float(footprint[1]) * 0.5
			1:
				normal = Vector3(0, 0, -1)
				extent = float(footprint[1]) * 0.5
			2:
				normal = Vector3(1, 0, 0)
				extent = float(footprint[0]) * 0.5
			_:
				normal = Vector3(-1, 0, 0)
				extent = float(footprint[0]) * 0.5

		# The doorway itself: a 2.4 m opening, 3 m tall, centred on the wall.
		var opening: Vector3 = centre + normal * extent + Vector3(0, 1.4, 0)
		var outside := opening + normal * 3.0
		var inside := opening - normal * 2.2
		var blocking := ray(outside, inside)
		expect(blocking.is_empty(),
			"doors: %s cannot be entered — the doorway is blocked" % id)

		# The room behind it: floor under the middle, headroom above it.
		var middle: Vector3 = centre + Vector3(0, 0.2, 0)
		var down := ray(middle + Vector3(0, 1.5, 0), middle - Vector3(0, 1.0, 0))
		expect(not down.is_empty(), "interiors: %s has no floor" % id)
		if not down.is_empty():
			var floor_y: float = down["position"].y
			expect(floor_y > -0.1 and floor_y < 0.45,
				"interiors: the floor in %s is at %.2f m" % [id, floor_y])
		var headroom := ray(outside - normal * 3.2 + Vector3(0, 0.3, 0), outside - normal * 3.2 + Vector3(0, 8.0, 0))
		expect(headroom.is_empty() or headroom["position"].y > 2.6,
			"interiors: the doorway into %s has no headroom" % id)


func _check_consoles(world: ColonyWorld) -> void:
	expect(world.interactables.size() == 45,
		"consoles: expected 45, the colony built %d" % world.interactables.size())
	for entry in world.interactables:
		var body: StaticBody3D = entry["node"]
		var building := Content.building(str(entry["building_id"]))
		var footprint: Array = building.get("footprint", [10, 8])
		var position: Array = building.get("position", [0, 0])
		var centre := Vector3(float(position[0]), 0.0, float(position[1]))
		var point: Vector3 = body.global_position

		# Inside the walls, not through them.
		expect(absf(point.x - centre.x) < float(footprint[0]) * 0.5 - 0.2
			and absf(point.z - centre.z) < float(footprint[1]) * 0.5 - 0.2,
			"consoles: %s/%s stands outside its walls" % [entry["building_id"], entry["interaction_id"]])

		# A commander standing in the room can put the crosshair on it from eye height.
		var toward := (centre - point)
		toward.y = 0.0
		if toward.length() < 0.1:
			toward = Vector3(0, 0, 1)
		toward = toward.normalized()
		# Eye height 1.6 m, standing 1.7 m off the panel, aiming at the screen — exactly the shot a
		# player takes when they walk up to a console.
		var eye := Vector3(point.x, 1.6, point.z) + toward * 1.7
		var aim := Vector3(point.x, 1.15, point.z)
		var hit := ray(eye, aim)
		var reachable := false
		if not hit.is_empty():
			var collider = hit["collider"]
			reachable = collider != null and collider.has_meta("interaction_id") \
				and str(collider.get_meta("interaction_id")) == str(entry["interaction_id"])
		expect(reachable, "consoles: %s/%s cannot be reached from inside the room" % [
			entry["building_id"], entry["interaction_id"]])


func _check_crew(world: ColonyWorld, crew: ColonyCrew) -> void:
	expect(crew.members.size() == 7, "crew: expected 7, built %d" % crew.members.size())
	for member in crew.members:
		var root: Node3D = member["root"]
		var spot: Vector3 = root.global_position
		var inside_a_building := ""
		for building in Content.buildings:
			var footprint: Array = building.get("footprint", [10, 8])
			var position: Array = building.get("position", [0, 0])
			var dx: float = absf(spot.x - float(position[0]))
			var dz: float = absf(spot.z - float(position[1]))
			if dx < float(footprint[0]) * 0.5 and dz < float(footprint[1]) * 0.5:
				inside_a_building = str(building.get("id", ""))
		expect(inside_a_building == "", "crew: %s stands inside %s" % [
			member["id"], inside_a_building])
		var ground := ray(spot + Vector3(0, 1.2, 0), spot - Vector3(0, 1.2, 0))
		expect(not ground.is_empty(), "crew: %s is standing over a hole" % member["id"])


func _check_perimeter() -> void:
	var blocked := 0
	for i in range(12):
		var angle := TAU * float(i) / 12.0
		var direction := Vector3(cos(angle), 0, sin(angle))
		var hit := ray(direction * 108.0 + Vector3(0, 1.2, 0), direction * 132.0 + Vector3(0, 1.2, 0))
		if not hit.is_empty():
			blocked += 1
	expect(blocked >= 10, "perimeter: only %d of 12 headings are closed off" % blocked)


## The ground trimesh must face up. A triangle wound the other way is invisible from above and
## unfindable by a downward ray, so the commander falls through the Moon — which is exactly what
## happened before the winding was fixed. This guards the winding itself, not just a ray: one
## probe per ring of radius 40, 90 and 150 m, where the first is inside the flat pad, the second
## is on the fade and the third is the open highlands.
func _check_ground_faces() -> void:
	var facing := 0
	var probes := 0
	for radius in [40.0, 90.0, 150.0]:
		for i in range(8):
			var angle := TAU * float(i) / 8.0
			var spot := Vector3(cos(angle) * radius, 0.0, sin(angle) * radius + 8.0)
			var down := ray(spot + Vector3(0, 30.0, 0), spot - Vector3(0, 30.0, 0))
			if down.is_empty() or not _is_ground(down["collider"]):
				# A roof, a wall or a kit module is in the way: not a terrain probe.
				continue
			probes += 1
			# The same segment from below must not find the surface: a face-down triangle is
			# invisible from above and solid from underneath.
			var up := ray(spot - Vector3(0, 30.0, 0), spot + Vector3(0, 30.0, 0))
			if up.is_empty() or not _is_ground(up["collider"]):
				facing += 1
	expect(probes >= 12, "ground: only %d terrain probes found, the ground body is missing" % probes)
	expect(facing == probes,
		"ground: only %d of %d terrain probes are face-up (triangles wound the wrong way)" % [
			facing, probes])


func _is_ground(collider: Object) -> bool:
	return collider != null and collider is Node and (collider as Node).name == "Ground"


## The T5 rover checklist, driven instead of described: enter, drive, steer, boost, brake, the
## battery rule, the garage charger, the ice-site milestone, exiting on the ground and putting the
## F0 — the universal control contract, proven by driving, not by reasoning.
##
## W (or Up) drives the rover forward; S (or Down) reverses; A and D turn toward screen-left and
## screen-right, always measured against the camera basis; and walking keeps the same meaning
## after any driving episode.
func _check_f0_controls(root: Node, world: ColonyWorld, player: ColonyPlayer) -> void:
	var rover := root.get_node_or_null("CrewRover") as ColonyRover
	expect(rover != null, "f0: the crew rover is not in the scene")
	if rover == null:
		return
	# Deterministic pose: the apron, the usual yaw, full charge.
	rover.global_position = Vector3(-11.0, 0.05, -28.0)
	rover.rotation.y = 0.4
	rover.speed = 0.0
	rover.steer = 0.0
	rover.battery_kwh = ColonyRover.BATTERY_KWH
	rover.settle()
	rover.enter(player)
	# The audit entered the vehicle by hand earlier; point the commander at the same heading
	# before any camera basis is read, so the checks measure the vehicle, not a stale angle.
	player.rotation.y = rover.rotation.y

	var veh_fwd := -rover.global_transform.basis.z
	var cam_fwd := -player.camera.global_transform.basis.z
	expect(veh_fwd.dot(cam_fwd) > 0.9,
		"f0: the rover's forward is not aligned with the camera (%.2f)" % veh_fwd.dot(cam_fwd))

	# W under throttle: displacement goes toward the view, not away from it.
	var before := rover.global_position
	for i in range(120):
		rover._step(1.0 / 60.0, 1.0, 0.0, false, false)
	var moved := rover.global_position - before
	expect(moved.dot(veh_fwd) > 0.5,
		"f0: W does not drive toward the view (%.2f m)" % moved.dot(veh_fwd))

	# S from rest: displacement away from the view.
	rover.speed = 0.0
	before = rover.global_position
	for i in range(120):
		rover._step(1.0 / 60.0, -1.0, 0.0, false, false)
	moved = rover.global_position - before
	expect(moved.dot(veh_fwd) < 0.0,
		"f0: S does not reverse (%.2f m)" % moved.dot(veh_fwd))

	# Get rolling, then A turns toward the camera's screen-left, D toward screen-right.
	rover.speed = 0.0
	for i in range(90):
		rover._step(1.0 / 60.0, 1.0, 0.0, false, false)
	var cam_right := player.camera.global_transform.basis.x
	var left := -cam_right
	var yaw0 := rover.rotation.y
	for i in range(60):
		rover._step(1.0 / 60.0, 1.0, 1.0, false, false)
	var h0 := Basis(Vector3.UP, yaw0) * Vector3(0, 0, -1)
	var h1 := Basis(Vector3.UP, rover.rotation.y) * Vector3(0, 0, -1)
	expect(h1.dot(left) > h0.dot(left),
		"f0: A did not move the heading toward screen-left (delta %.2f)" % (h1.dot(left) - h0.dot(left)))
	rover.speed = 0.0
	yaw0 = rover.rotation.y
	for i in range(60):
		rover._step(1.0 / 60.0, 1.0, -1.0, false, false)
	h0 = Basis(Vector3.UP, yaw0) * Vector3(0, 0, -1)
	h1 = Basis(Vector3.UP, rover.rotation.y) * Vector3(0, 0, -1)
	expect(h1.dot(cam_right) > h0.dot(cam_right),
		"f0: D did not move the heading toward screen-right (delta %.2f)" % (h1.dot(cam_right) - h0.dot(cam_right)))

	# Exiting cannot drift walking: W still leads to the camera, A/D still map to screen-left/right.
	rover.exit(player)
	expect(not player.rover, "f0: exit() left the rover glued to the commander")
	expect(ColonyPlayer.move_direction(Vector2(0, -1), 0.0).dot(Vector3(0, 0, -1)) > 0.9,
		"f0: W no longer maps to the camera forward after exiting the rover")
	expect(ColonyPlayer.move_direction(Vector2(-1, 0), 0.0).dot(Vector3(-1, 0, 0)) > 0.9,
		"f0: A no longer maps to screen-left after exiting the rover")
	expect(ColonyPlayer.move_direction(Vector2(1, 0), 0.0).dot(Vector3(1, 0, 0)) > 0.9,
		"f0: D no longer maps to screen-right after exiting the rover")
	expect(ColonyPlayer.move_direction(Vector2(0, 1), 0.0).dot(Vector3(0, 0, 1)) > 0.9,
		"f0: S no longer maps to reverse after exiting the rover")
	expect(ColonyPlayer.move_direction(Vector2(0, -1), PI / 2.0).dot(Vector3(-1, 0, 0)) > 0.9,
		"f0: walking at yaw 90 degrees is not camera-relative")

	# Back to the apron for anything that runs after this check.
	rover.global_position = Vector3(-11.0, 0.05, -28.0)
	rover.rotation.y = 0.4
	rover.settle()


## F2 — every walkable building wears a matching CC0 kit shell and reads as furnished.
## The procedural box-wall meshes are hidden; the colliders (doors and consoles) are untouched.
func _check_buildings(root: Node, world: ColonyWorld) -> void:
	var shell_ids := [
		"habitat", "mission_control", "greenhouse", "workshop", "life_support", "lab", "medical",
		"storage", "comms_tower", "regolith_plant", "storm_shelter", "power_farm", "ice_site",
		"garage", "airlock",
	]
	for id in shell_ids:
		var group := world.building_nodes.get(id) as Node3D
		expect(group != null, "buildings: %s group is missing" % id)
		if group == null:
			continue
		var shell := group.find_child("Shell_%s" % id, true, false)
		expect(shell != null, "buildings: %s has no CC0 kit shell" % id)
		var props := int(world._attached_prop_count.get(id, 0))
		expect(props >= 5, "buildings: %s reads unfurnished (%d children)" % [id, props])
		var porch := group.find_child("Porch", true, false)
		expect(porch != null, "buildings: %s has no airlock porch at the door" % id)
		var hidden := 0
		for child in group.get_children():
			if child is MeshInstance3D and not child.visible:
				hidden += 1
		expect(hidden >= 2, "buildings: %s wall meshes were not hidden (%d)" % [id, hidden])


## rover back with the mission restart (including the reset that BUG-4 was about).
func _check_rover(root: Node, world: ColonyWorld, player: ColonyPlayer, crew: ColonyCrew,
		sim: LQSim) -> void:
	var rover := root.get_node_or_null("CrewRover") as ColonyRover
	expect(rover != null, "rover: the crew rover is not in the scene")
	if rover == null:
		return
	# Walking to it from the spawn: the apron is 40 m across, so this is a walk, not a step. What
	# matters is that the rover's own box really is where the prompt measures from, and that the
	# ground around it is solid to stand on while walking over.
	var parked := rover.global_position
	expect(absf(parked.y - 0.05) < 0.2, "rover: it is parked at y %.2f, not on the apron" % parked.y)
	var around_ok := true
	for offset in [Vector3(2.6, 0, 0), Vector3(-2.6, 0, 0), Vector3(0, 0, 3.2), Vector3(0, 0, -3.2)]:
		var beside: Vector3 = parked + offset
		var ground := ray(beside + Vector3(0, 6.0, 0), beside - Vector3(0, 6.0, 0))
		# Anything the commander can stand on counts: the apron collider is an unnamed
		# StaticBody3D laid over the terrain, and both are the same surface here.
		var hit_ground: bool = not ground.is_empty()
		if not hit_ground or absf(ground["position"].y) > 0.3:
			around_ok = false
	expect(around_ok, "rover: the ground around the parked rover is not the flat apron")

	# Enter: the suit is carried, and stops colliding with the rover it sits inside.
	rover.enter(player)
	expect(rover.occupied and player.rover == rover, "rover: enter() did not take the seat")
	expect(player.collision_layer == 0 and player.collision_mask == 0,
		"rover: the driven suit still collides (layer %d, mask %d)" % [
			player.collision_layer, player.collision_mask])

	# Drive: throttle, steering, boost, handbrake and the wheel animation, stepped by hand so the
	# test never depends on which keys a headless run happens to think are held.
	var start := rover.global_position
	var start_yaw := rover.rotation.y
	var drained := rover.battery_kwh
	for i in range(120):
		rover._step(1.0 / 60.0, 1.0, 1.0, false, false)
	expect(rover.speed > 4.0, "rover: 2 s of throttle only reached %.2f m/s" % rover.speed)
	expect(rover.global_position.distance_to(start) > 3.0,
		"rover: it did not move (%.2f m)" % rover.global_position.distance_to(start))
	expect(absf(rover.rotation.y - start_yaw) > 0.05, "rover: steering did not turn it")
	expect(rover.battery_kwh < drained, "rover: driving did not drain the battery")
	expect(rover.odometer > 3.0, "rover: the odometer did not count (%.2f m)" % rover.odometer)
	expect(rover.steer > 0.01 and not rover._steers.is_empty(), "rover: the front wheels did not steer")
	expect(absf(rover.spin) > 1.0, "rover: the wheels did not spin (%.2f)" % rover.spin)

	# Boost raises the cap, the handbrake stops it, reverse is slower than forward.
	rover.speed = 0.0
	for i in range(240):
		rover._step(1.0 / 60.0, 1.0, 0.0, true, false)
	expect(rover.speed > ColonyRover.MAX_FORWARD * 0.72 + 0.2,
		"rover: Shift did not raise the speed cap (%.2f m/s)" % rover.speed)
	expect(rover.speed <= ColonyRover.MAX_FORWARD + 0.001,
		"rover: it exceeded its own top speed (%.2f m/s)" % rover.speed)
	for i in range(120):
		rover._step(1.0 / 60.0, 0.0, 0.0, false, true)
	expect(absf(rover.speed) < 0.05, "rover: the handbrake left it rolling at %.2f m/s" % rover.speed)

	# The battery: a flat pack refuses to drive, the garage apron recharges a stopped rover.
	rover.battery_kwh = 0.0
	rover.speed = 0.0
	for i in range(60):
		rover._step(1.0 / 60.0, 1.0, 0.0, false, false)
	expect(absf(rover.speed) < 0.01, "rover: a flat battery still drove (%.2f m/s)" % rover.speed)
	rover.global_position = Vector3(ColonyRover.GARAGE.x, 0.05, ColonyRover.GARAGE.y)
	rover.battery_kwh = 1.0
	for i in range(60):
		rover._step(1.0 / 60.0, 0.0, 0.0, false, false)
	expect(rover.battery_kwh > 1.0, "rover: it did not recharge on the garage apron")
	rover.global_position = start

	# The ice-site milestone is a place, not a state.
	rover.reached_ice = false
	rover.global_position = Vector3(ColonyRover.ICE_SITE.x, 0.05, ColonyRover.ICE_SITE.y)
	for i in range(2):
		rover._step(1.0 / 60.0, 0.0, 0.0, false, false)
	expect(rover.reached_ice, "rover: driving onto the ice site did not set the milestone")
	rover.global_position = parked
	rover.rotation.y = 0.4
	rover.settle()

	# Exit: the suit is left standing on the ground beside it, solid again and upright.
	rover.exit(player)
	expect(not rover.occupied and player.rover == null, "rover: exit() kept the seat")
	expect(player.collision_layer == 1 and player.collision_mask == 1,
		"rover: the suit is still non-solid after stepping out")
	expect(absf(player.astronaut.rotation.x) < 0.001 if player.astronaut != null else true,
		"rover: the suit stayed reclined after stepping out")
	var under := ray(player.global_position + Vector3(0, 2.0, 0),
		player.global_position - Vector3(0, 2.0, 0), [player.get_rid()])
	expect(not under.is_empty(), "rover: there is no ground beside the rover to stand on")
	var overlap := _capsule_overlap(player.global_position, [player.get_rid()])
	var names: Array = []
	for hit in overlap:
		var c = hit.get("collider")
		names.append(c.name if c != null else "?")
	expect(overlap.is_empty(), "rover: the suit steps out inside %d colliders %s" % [overlap.size(), str(names)])

	# Save round trip: the rover state survives JSON, and an old save without a rover still loads.
	var captured := rover.capture()
	rover.position = Vector3.ZERO
	rover.battery_kwh = 0.5
	rover.restore(captured, player)
	expect(rover.position.distance_to(parked) < 0.2,
		"rover: restore() lost the parked position")
	expect(absf(rover.battery_kwh - captured["battery"]) < 0.001, "rover: restore() lost the battery")
	var pristine := ColonyRover.new()
	pristine.restore({}, null)
	expect(pristine.battery_kwh == ColonyRover.BATTERY_KWH,
		"rover: an old save without rover keys did not load")
	pristine.free()

	# Restart: back on the apron with a full battery and no milestones (BUG-4).
	rover.has_driven = true
	rover.reached_ice = true
	rover.odometer = 99.0
	root._restart_mission()
	expect(rover.position.distance_to(parked) < 0.2,
		"rover: a new mission did not return it to the apron")
	expect(rover.battery_kwh == ColonyRover.BATTERY_KWH, "rover: a new mission did not refill it")
	expect(not rover.reached_ice and not rover.has_driven and rover.odometer == 0.0,
		"rover: a new mission kept the old milestones")


func _capsule_overlap(spot: Vector3, exclude: Array) -> Array:
	var shape := CapsuleShape3D.new()
	shape.radius = 0.34
	shape.height = 1.75
	var params := PhysicsShapeQueryParameters3D.new()
	params.shape = shape
	params.transform = Transform3D(Basis(), spot + Vector3(0, 0.9, 0))
	params.collide_with_areas = false
	params.exclude = exclude
	return space.intersect_shape(params, 4)
