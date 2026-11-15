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
	print("Luna-Quest (GDScript) scene audit")
	_check_spawn(player)
	_check_view(player, world)
	_check_interface(hud, sim)
	_check_doors(world)
	_check_consoles(world)
	_check_crew(world, crew)
	_check_perimeter()
	_check_ground_faces()
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
func _check_interface(hud: LunaHud, sim: LQSim) -> void:
	expect(hud != null, "hud: no interface")
	if hud == null:
		return
	expect(hud.top_values.size() == 9, "hud: expected 9 gauges, found %d" % hud.top_values.size())
	expect(hud.crosshair != null, "hud: no crosshair")
	expect(hud.prompt_label != null and hud.subtitle_label != null, "hud: no prompt or subtitle line")
	hud.refresh(sim)
	expect(hud.top_values["day"].text.begins_with("Day"),
		"hud: the mission stamp reads '%s'" % hud.top_values["day"].text)
	expect(hud.top_values["power"].text.ends_with("%"), "hud: the battery gauge has no value")

	hud.set_language(true)
	expect(hud.arabic, "hud: the language toggle did not stick")
	hud.refresh(sim)
	expect(hud.top_values["day"].text.begins_with("اليوم"), "hud: the stamp did not change language")
	hud.set_language(false)

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
