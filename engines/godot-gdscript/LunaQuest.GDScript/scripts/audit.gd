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


func run(tree: SceneTree, world: ColonyWorld, player: ColonyPlayer, crew: ColonyCrew) -> bool:
	space = world.get_world_3d().direct_space_state
	print("Luna-Quest (GDScript) scene audit")
	_check_spawn(player)
	_check_doors(world)
	_check_consoles(world)
	_check_crew(world, crew)
	_check_perimeter()
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
		var eye: Vector3 = point + toward * 1.7 + Vector3(0, 1.1, 0)
		var hit := ray(eye, point + Vector3(0, 1.1, 0))
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
