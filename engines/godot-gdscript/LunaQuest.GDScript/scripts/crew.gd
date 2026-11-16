class_name ColonyCrew
extends Node3D
## The six crew and NEXO, standing where their schedule says they should be.
##
## Every human uses the one Cute Astronaut GLB, copied in by tools/sync-gdscript-assets.sh. A
## missing model is a broken checkout, not a reason to draw a different astronaut: the build
## reports the error and leaves the character out. Each suit carries a trim band in its own
## colour, which is how you tell Dr. Safaa from Noor at thirty metres.

const TRIM := {
	"npc_safaa": Color(0.83, 0.37, 0.42),
	"npc_nour": Color(0.25, 0.65, 0.84),
	"npc_alaa": Color(0.39, 0.72, 0.42),
	"npc_abdelaziz": Color(0.88, 0.65, 0.16),
	"npc_ahmed": Color(0.56, 0.48, 0.84),
	"npc_mostafa": Color(0.79, 0.76, 0.70),
	"npc_nexo": Color(0.35, 0.78, 0.91),
}

const ASTRONAUT_SCENE := "res://assets/astronaut/cute_astronaut.glb"

## A single correct bounds helper. The holder is measured before it joins the tree: each
## mesh's own local transform is composed with its ancestors back up to the holder, so
## nested bone/armature transforms are honoured. A flat mesh_node.transform * mesh.get_aabb()
## (local only) is the bug this exists to fix.
static func bounds_of(holder: Node3D) -> AABB:
	var total := AABB()
	var first := true
	for child in holder.find_children("*", "MeshInstance3D", true, false):
		var mesh_node := child as MeshInstance3D
		if mesh_node == null or mesh_node.mesh == null:
			continue
		# Bind-pose meshes (SkinCount > 0) never match the posed silhouette; skip them
		# exactly as the C# audit does. The suit fit measurement matches the visual suit.
		if mesh_node.mesh.get_skin_count() > 0:
			continue
		# Compose the holder-relative transform by walking from the mesh up to the holder.
		var xform := Transform3D()
		var chain: Array = []
		var n: Node = mesh_node
		while n != holder:
			if n is Node3D:
				chain.push_front(n)
			n = n.get_parent()
		for item in chain:
			xform = xform * item.transform
		var local: AABB = xform * mesh_node.mesh.get_aabb()
		if first:
			total = local
			first = false
		else:
			total = total.merge(local)
	return total## Standing height shared by the player and every human crew member (metres). The retired
## web runtime read the same value from archive/web/src/world/crew-metrics.js.
const CREW_HEIGHT_M := 1.75

var members: Array = []
var world: ColonyWorld
var sim: LQSim
var _astronaut_template: PackedScene
var _eyes: Array = []
var _obstacles: Array = []  # building block AABBs as [x, z, radius], for steering avoidance


func _build_obstacles() -> void:
	_obstacles.clear()
	for b in Content.buildings:
		var pos: Array = b.get("position", [0, 0])
		var footprint: Array = b.get("footprint", [10, 8])
		var w := float(footprint[0])
		var d := float(footprint[1])
		var radius := sqrt(w * w + d * d) * 0.5 + 1.5
		_obstacles.append(Vector3(float(pos[0]), float(pos[1]), radius))


## Steer around building footprints: how crew reach their workplace without walking through walls.
## A genuinely planned route would need a navmesh — this is the steering the brief allows.
func steer_direction(pos: Vector3, target: Vector3) -> Vector3:
	var desired := target - pos
	desired.y = 0.0
	if desired.length() < 0.25:
		return Vector3.ZERO
	desired = desired.normalized()
	for o in _obstacles:
		var to_obs := Vector3(o.x - pos.x, 0.0, o.y - pos.z)
		var dist := to_obs.length()
		var rr := float(o.z) + 1.3
		if dist < rr and desired.dot(to_obs.normalized()) > 0.0:
			# Push the heading away perpendicular to the obstacle centre.
			var push := desired.dot(to_obs.normalized())
			var perp := Vector3(-desired.z, 0.0, desired.x)
			var side := 1.0 if perp.dot(to_obs) > 0.0 else -1.0
			desired = (desired + perp * side * (1.0 - push) * 1.6).normalized()
	return desired


func build(colony: ColonyWorld, simulation: LQSim) -> void:
	world = colony
	sim = simulation
	_build_obstacles()
	if ResourceLoader.exists(ASTRONAUT_SCENE):
		_astronaut_template = load(ASTRONAUT_SCENE) as PackedScene
	if _astronaut_template == null:
		push_error("Luna-Quest: the Cute Astronaut GLB is missing. Run tools/sync-gdscript-assets.sh before launching. No substitute astronaut will be drawn.")
	for profile in Content.npcs:
		_spawn(profile)
	print("Luna-Quest (GDScript): %d crew placed" % members.size())


func _spawn(profile: Dictionary) -> void:
	var id: String = profile.get("id", "")
	var is_robot: bool = profile.get("isRobot", false)
	var home: Dictionary = Content.building(str(profile.get("locationId", "habitat")))
	var spot := _spot_for(home, id)

	var root := Node3D.new()
	root.name = "Crew_%s" % id
	root.position = spot
	add_child(root)

	var trim: Color = TRIM.get(id, Color(0.85, 0.52, 0.19))
	var animator := SuitAnimator.new() if not is_robot else null
	if is_robot:
		_build_nexo(root, trim)
	else:
		_build_astronaut(root, trim, animator)

	var area := Area3D.new()
	var shape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.62
	capsule.height = 1.9
	shape.shape = capsule
	shape.position = Vector3(0, 0.95, 0)
	area.add_child(shape)
	area.set_meta("npc_id", id)
	area.set_meta("npc_name_en", str(profile.get("nameEn", "")))
	area.set_meta("npc_name_ar", str(profile.get("nameAr", "")))
	root.add_child(area)

	var label := Label3D.new()
	label.text = "%s\n%s" % [profile.get("nameEn", ""), profile.get("nameAr", "")]
	label.font_size = 44
	label.pixel_size = 0.0038
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.outline_size = 10
	label.outline_modulate = Color(0, 0, 0, 0.8)
	label.position = Vector3(0, 2.3, 0)
	label.no_depth_test = false
	label.visible = false
	root.add_child(label)

	members.append({
		"id": id,
		"profile": profile,
		"root": root,
		"area": area,
		"label": label,
		"home": home,
		"target": spot,
		"is_robot": is_robot,
		"phase": randf() * TAU,
		"trim": trim,
		"animator": animator,
	})


func _spot_for(building: Dictionary, npc_id: String) -> Vector3:
	if building.is_empty():
		return Vector3(0, 0, 16)
	var pos: Array = building.get("position", [0, 0])
	var footprint: Array = building.get("footprint", [10, 8])
	var centre := Vector3(float(pos[0]), 0.0, float(pos[1]))

	# Face the plaza: pick the wall whose outward normal points most directly at the origin, which
	# is the same door the world builder cut.
	var to_plaza := Vector2(-centre.x, -centre.z)
	var normal := Vector3(0, 0, -1)
	var extent: float = float(footprint[1]) * 0.5
	if absf(to_plaza.x) > absf(to_plaza.y):
		normal = Vector3(signf(to_plaza.x), 0, 0)
		extent = float(footprint[0]) * 0.5
	elif to_plaza.length() > 0.01:
		normal = Vector3(0, 0, signf(to_plaza.y))

	var tangent := Vector3(-normal.z, 0.0, normal.x)
	var hash := 0
	for i in range(npc_id.length()):
		hash += npc_id.unicode_at(i)
	var spread: float = float((hash % 5) - 2) * 1.7
	var stand := centre + normal * (extent + 2.4) + tangent * spread
	stand.y = 0.06
	return stand


func _build_astronaut(root: Node3D, trim: Color, animator: SuitAnimator) -> void:
	if _astronaut_template == null:
		# There is exactly one human character model in Luna-Quest. A missing GLB is reported
		# by build(); it must never be answered with a differently shaped astronaut.
		return
	var body := _astronaut_template.instantiate() as Node3D
	if body == null:
		return
	root.add_child(body)
	_fit_height(body, CREW_HEIGHT_M)

	# The GLB ships one long action; the idle/walk sections become looping clips here.
	animator.name = "SuitAnimator"
	root.add_child(animator)
	var an := body.find_child("AnimationPlayer", true, false) as AnimationPlayer
	if an != null:
		animator.setup(an)

	var band := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 0.24
	torus.outer_radius = 0.3
	torus.rings = 20
	torus.ring_segments = 8
	band.mesh = torus
	var band_mat := StandardMaterial3D.new()
	band_mat.albedo_color = trim
	band_mat.emission_enabled = true
	band_mat.emission = trim
	band_mat.emission_energy_multiplier = 0.35
	band.material_override = band_mat
	band.position = Vector3(0, 1.32, 0)
	band.rotation.x = PI * 0.5
	root.add_child(band)


func _build_nexo(root: Node3D, trim: Color) -> void:
	var shell := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.42
	sphere.height = 0.84
	sphere.radial_segments = 24
	sphere.rings = 12
	shell.mesh = sphere
	var shell_mat := StandardMaterial3D.new()
	shell_mat.albedo_color = Color(0.88, 0.90, 0.92)
	shell_mat.roughness = 0.35
	shell_mat.metallic = 0.2
	shell.material_override = shell_mat
	shell.position = Vector3(0, 1.05, 0)
	root.add_child(shell)

	var band := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 0.43
	torus.outer_radius = 0.5
	torus.rings = 28
	torus.ring_segments = 8
	band.mesh = torus
	var band_mat := StandardMaterial3D.new()
	band_mat.albedo_color = trim
	band_mat.emission_enabled = true
	band_mat.emission = trim
	band_mat.emission_energy_multiplier = 1.2
	band.material_override = band_mat
	band.position = Vector3(0, 1.05, 0)
	band.rotation.x = PI * 0.5
	root.add_child(band)

	# Screen face: two eyes that brighten when the colony has an active warning.
	for side in [-1.0, 1.0]:
		var eye := MeshInstance3D.new()
		var eye_mesh := SphereMesh.new()
		eye_mesh.radius = 0.07
		eye_mesh.height = 0.14
		eye_mesh.radial_segments = 10
		eye_mesh.rings = 6
		eye.mesh = eye_mesh
		var eye_mat := StandardMaterial3D.new()
		eye_mat.albedo_color = Color(0.08, 0.55, 0.75)
		eye_mat.emission_enabled = true
		eye_mat.emission = Color(0.25, 0.85, 1.0)
		eye_mat.emission_energy_multiplier = 1.6
		eye.material_override = eye_mat
		eye.position = Vector3(side * 0.14, 1.12, 0.36)
		root.add_child(eye)
		_eyes.append(eye_mat)

	var skirt := MeshInstance3D.new()
	var cone := CylinderMesh.new()
	cone.top_radius = 0.34
	cone.bottom_radius = 0.12
	cone.height = 0.5
	cone.radial_segments = 18
	skirt.mesh = cone
	skirt.material_override = shell_mat
	skirt.position = Vector3(0, 0.55, 0)
	root.add_child(skirt)


func _fit_height(node: Node3D, target_height: float) -> void:
	# All three editions share the same visor-to-sole pose height (2.311 m), so every human
	# suit stands exactly 1.75 m in every engine. A pose-bound fit is the honest measure for
	# this asset: the raw GLB carries no usable bind pose to read it from.
	const POSED_HEIGHT := 2.311
	var factor: float = target_height / POSED_HEIGHT
	node.scale = Vector3(factor, factor, factor)


func update(delta: float, player_position: Vector3) -> void:
	var warning := not sim.incidents_active.is_empty()
	for eye in _eyes:
		eye.emission = Color(1.0, 0.55, 0.18) if warning else Color(0.25, 0.85, 1.0)
		eye.emission_energy_multiplier = 2.2 if warning else 1.4

	for member in members:
		var root: Node3D = member["root"]
		var target: Vector3 = member["target"]
		var distance: float = root.position.distance_to(target)
		var moving := distance > 0.25
		if moving:
			# Face where they walk, and follow the Moon, not the sky.
			var dir := steer_direction(root.position, target)
			if dir.length() > 0.001:
				root.rotation.y = lerp_angle(root.rotation.y,
					atan2(dir.x, dir.z), min(delta * 4.0, 1.0))
			var to_go: float = min(delta * 1.55, distance)
			root.position += dir * to_go
			root.position.y = world.height_at(root.position.x, root.position.z) + 0.02
			if member["animator"] != null:
				member["animator"].set_locomotion(true, 1.55)
		else:
			if member["animator"] != null:
				member["animator"].set_locomotion(false, 0.0)
			var to_player: Vector3 = player_position - root.position
			to_player.y = 0.0
			if to_player.length() < 6.0:
				root.rotation.y = lerp_angle(root.rotation.y,
					atan2(to_player.x, to_player.z), min(delta * 2.4, 1.0))
		# The hover robot floats; humans stay on the surface.
		if member["is_robot"]:
			member["phase"] = float(member["phase"]) + delta * 1.6
			root.position.y = 0.35 + sin(float(member["phase"])) * 0.06
		var label: Label3D = member["label"]
		label.visible = distance_to_player(member, player_position) < 9.0


func distance_to_player(member: Dictionary, player_position: Vector3) -> float:
	var root: Node3D = member["root"]
	return Vector2(root.position.x - player_position.x, root.position.z - player_position.z).length()


## Moves a crew member to the building their schedule names, when the shift changes.
func apply_schedule() -> void:
	for member in members:
		var st: Dictionary = sim.npcs_state.get(member["id"], {})
		var location: String = str(st.get("location", ""))
		var building: Dictionary = Content.building(location)
		if building.is_empty():
			continue
		if str(member["home"].get("id", "")) == location:
			continue
		member["home"] = building
		member["target"] = _spot_for(building, member["id"])


func member_by_id(id: String) -> Dictionary:
	for member in members:
		if member["id"] == id:
			return member
	return {}
