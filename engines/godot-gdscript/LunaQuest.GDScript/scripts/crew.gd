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
	var nexo_extras: Array = []
	if is_robot:
		nexo_extras = _build_nexo(root, trim)
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
	label.text = str(profile.get("nameAr", "")) if sim.arabic else str(profile.get("nameEn", ""))
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
		"thruster": nexo_extras[0] if nexo_extras.size() > 0 else null,
		"pointer": nexo_extras[1] if nexo_extras.size() > 1 else null,
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


const NEXO_SCENE := "res://assets/models/nexo_robot/nexo_robot.glb"

## The ice-line for the next objective: where NEXO points. Mirrors the six crew id -> building id
## table used everywhere; the pointer reads the sim's journal for the first unfinished objective.
const OBJ_TARGET := {
	"obj_solar_dust": "power_farm",
	"obj_sensor_dust": "comms_tower",
	"obj_airlock_dust": "airlock",
	"obj_water_filter": "life_support",
	"obj_air_scrubber": "life_support",
	"obj_plant_care": "greenhouse",
	"obj_radiation": "storm_shelter",
	"obj_comms_relay": "comms_tower",
	"obj_crew_rest": "habitat",
	"obj_sample": "ice_site",
	"obj_power_budget": "power_farm",
	"obj_rover_take": "garage",
	"obj_rover_ice": "ice_site",
}


func _build_nexo(root: Node3D, trim: Color) -> Array:
	"""CC0 Quaternius Animated LowPoly Robot body, with the procedural body as fallback.

	The GLB is a 66 mm bind pose (FBX2glTF kept FBX units); we measure once and scale to 1.05 m,
	so the file can be swapped in place without guessing at the rig's units.
	"""
	var want_model := ResourceLoader.exists(NEXO_SCENE)
	var model: Node3D = null
	if want_model:
		var scene := load(NEXO_SCENE) as PackedScene
		model = scene.instantiate() as Node3D if scene != null else null
	var glb_used := false
	if model != null:
		var raw := _nexo_raw_bounds(model)
		# The FBX2glTF rig measures ~26 mm from sole to crown. Fit it to 1.05 m.
		if raw.size.y > 0.001:
			var s := 1.05 / raw.size.y
			model.scale = Vector3(s, s, s)
			root.add_child(model)
			var after := _nexo_raw_bounds(model)
			model.position.y = -after.position.y
			glb_used = true
			var anim := model.find_child("AnimationPlayer", true, false) as AnimationPlayer
			if anim != null:
				for n in anim.get_animation_list():
					var a := anim.get_animation(n)
					if a != null and n.to_lower().contains("idle"):
						a.loop_mode = Animation.LOOP_LINEAR
						anim.play(n)
						break
		else:
			root.add_child(model)
			glb_used = true
	if not glb_used:
		_build_nexo_primitive(root, trim)

	# Hover thruster — doubles as the warning light: cyan normally, orange pulse during incidents.
	var thruster := OmniLight3D.new()
	thruster.name = "ThrusterGlow"
	thruster.light_color = Color(0.35, 0.78, 0.91)
	thruster.light_energy = 1.6
	thruster.omni_range = 2.0
	thruster.shadow_enabled = false
	thruster.position = Vector3(0, 0.16, 0)
	root.add_child(thruster)

	# Objective pointer: a chevron that yaws to point at the next objective's building.
	var pointer := Node3D.new()
	pointer.name = "ObjectivePointer"
	pointer.position = Vector3(0, 0.28, 0)
	var pen := MeshInstance3D.new()
	var tip := CylinderMesh.new()
	tip.top_radius = 0.0
	tip.bottom_radius = 0.065
	tip.height = 0.19
	tip.radial_segments = 8
	pen.mesh = tip
	var pen_mat := StandardMaterial3D.new()
	pen_mat.albedo_color = Color(0.18, 0.55, 0.62)
	pen_mat.emission_enabled = true
	pen_mat.emission = Color(0.25, 0.85, 1.0)
	pen_mat.emission_energy_multiplier = 1.5
	pen_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	pen.material_override = pen_mat
	pen.position = Vector3(0, 0.1, 0.0)
	pen.rotation = Vector3(PI * 0.4, 0.0, 0.0)
	pointer.add_child(pen)
	for side in [-1.0, 1.0]:
		var blade := MeshInstance3D.new()
		var bm := BoxMesh.new()
		bm.size = Vector3(0.11, 0.04, 0.05)
		var blade_mat := StandardMaterial3D.new()
		blade_mat.albedo_color = Color(0.18, 0.55, 0.62)
		blade_mat.emission_enabled = true
		blade_mat.emission = Color(0.25, 0.85, 1.0)
		blade_mat.emission_energy_multiplier = 1.3
		blade_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		blade.mesh = bm
		blade.material_override = blade_mat
		blade.position = Vector3(side * 0.115, 0.16, 0.07)
		blade.rotation.y = side * 0.72
		pointer.add_child(blade)
	root.add_child(pointer)

	return [thruster, pointer]


## F10: the upgraded procedural body — kept as the fresh-clone fallback. Rounded shell, dark
## curved visor, two glowing eyes, cyan accent ring, antenna with tip, solar fins, thruster bell.
func _build_nexo_primitive(root: Node3D, trim: Color) -> void:
	var shell := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.42
	sphere.height = 0.84
	sphere.radial_segments = 32
	sphere.rings = 16
	shell.mesh = sphere
	var shell_mat := StandardMaterial3D.new()
	shell_mat.albedo_color = Color(0.88, 0.90, 0.92)
	shell_mat.roughness = 0.3
	shell_mat.metallic = 0.15
	shell.material_override = shell_mat
	shell.position = Vector3(0, 1.12, 0)
	root.add_child(shell)

	# Panel seams: two thin dark rings.
	for frac in [-0.18, 0.18]:
		var seam := MeshInstance3D.new()
		var ring := CylinderMesh.new()
		ring.top_radius = 0.422
		ring.bottom_radius = 0.422
		ring.height = 0.018
		ring.radial_segments = 32
		seam.mesh = ring
		var seam_mat := StandardMaterial3D.new()
		seam_mat.albedo_color = Color(0.28, 0.30, 0.33)
		seam_mat.roughness = 0.55
		seam.mesh = ring
		seam.material_override = seam_mat
		seam.position = Vector3(0, 1.12 + frac, 0)
		root.add_child(seam)

	# Dark curved visor across the face.
	var visor := MeshInstance3D.new()
	var visor_mesh := SphereMesh.new()
	visor_mesh.radius = 0.34
	visor_mesh.height = 0.55
	visor_mesh.radial_segments = 28
	visor_mesh.rings = 12
	visor.mesh = visor_mesh
	var visor_mat := StandardMaterial3D.new()
	visor_mat.albedo_color = Color(0.02, 0.025, 0.035)
	visor_mat.roughness = 0.08
	visor_mat.metallic = 0.4
	visor_mat.emission_enabled = true
	visor_mat.emission = Color(0.03, 0.04, 0.06)
	visor_mat.emission_energy_multiplier = 0.4
	visor.material_override = visor_mat
	visor.scale = Vector3(1.0, 0.62, 0.32)
	visor.position = Vector3(0, 1.14, 0.30)
	root.add_child(visor)

	# Cyan accent ring around the waist.
	var ring := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 0.42
	torus.outer_radius = 0.445
	torus.rings = 36
	torus.ring_segments = 10
	ring.mesh = torus
	var ring_mat := StandardMaterial3D.new()
	ring_mat.albedo_color = Color(0.10, 0.55, 0.70)
	ring_mat.emission_enabled = true
	ring_mat.emission = Color(0.25, 0.85, 1.0)
	ring_mat.emission_energy_multiplier = 1.5
	ring.material_override = ring_mat
	ring.position = Vector3(0, 1.02, 0)
	ring.rotation.x = PI * 0.5
	root.add_child(ring)

	# Eyes: the glowing, expressive pair — the pulse target for warnings.
	for side in [-1.0, 1.0]:
		var eye := MeshInstance3D.new()
		var emesh := SphereMesh.new()
		emesh.radius = 0.055
		emesh.height = 0.11
		emesh.radial_segments = 12
		emesh.rings = 8
		eye.mesh = emesh
		var emm := StandardMaterial3D.new()
		emm.albedo_color = Color(0.05, 0.30, 0.38)
		emm.emission_enabled = true
		emm.emission = Color(0.30, 0.85, 1.0)
		emm.emission_energy_multiplier = 2.2
		eye.material_override = emm
		eye.position = Vector3(side * 0.13, 1.18, 0.42)
		root.add_child(eye)
		_eyes.append(emm)

	# Antenna with an orange tip.
	var stalk := MeshInstance3D.new()
	var cons := CylinderMesh.new()
	cons.top_radius = 0.012
	cons.bottom_radius = 0.018
	cons.height = 0.24
	cons.radial_segments = 8
	stalk.mesh = cons
	var stalk_mat := StandardMaterial3D.new()
	stalk_mat.albedo_color = shell_mat.albedo_color
	stalk_mat.roughness = 0.4
	stalk.material_override = stalk_mat
	stalk.position = Vector3(0, 1.68, 0)
	root.add_child(stalk)
	var tip := MeshInstance3D.new()
	var tipmesh := SphereMesh.new()
	tipmesh.radius = 0.032
	tipmesh.height = 0.064
	tipmesh.radial_segments = 10
	tipmesh.rings = 6
	tip.mesh = tipmesh
	var tip_mat := StandardMaterial3D.new()
	tip_mat.albedo_color = Color(0.9, 0.45, 0.08)
	tip_mat.emission_enabled = true
	tip_mat.emission = Color(1.0, 0.6, 0.2)
	tip_mat.emission_energy_multiplier = 1.8
	tip.material_override = tip_mat
	tip.position = Vector3(0, 1.82, 0)
	root.add_child(tip)

	# Solar fins at the two shoulders.
	for side in [-1.0, 1.0]:
		var fin := MeshInstance3D.new()
		var fb := BoxMesh.new()
		fb.size = Vector3(0.02, 0.22, 0.34)
		var fin_mat := StandardMaterial3D.new()
		fin_mat.albedo_color = Color(0.10, 0.16, 0.30)
		fin_mat.metallic = 0.55
		fin_mat.roughness = 0.25
		fin_mat.emission_enabled = true
		fin_mat.emission = Color(0.07, 0.16, 0.38)
		fin_mat.emission_energy_multiplier = 0.5
		fin.mesh = fb
		fin.material_override = fin_mat
		fin.position = Vector3(side * 0.50, 1.36, 0)
		fin.rotation.z = side * 0.22
		root.add_child(fin)

	# Hover skirt.
	var skirt := MeshInstance3D.new()
	var cone := CylinderMesh.new()
	cone.top_radius = 0.34
	cone.bottom_radius = 0.12
	cone.height = 0.42
	cone.radial_segments = 20
	skirt.mesh = cone
	skirt.material_override = shell_mat
	skirt.position = Vector3(0, 0.72, 0)
	root.add_child(skirt)


## Skinned meshes cannot be filtered for the NEXO fit (F3 for the human AABB stays filtered).
## This walk includes skinned meshes on purpose — it is only used on this one asset.
func _nexo_raw_bounds(node: Node3D) -> AABB:
	var total := AABB()
	var first := true
	for child in node.find_children("*", "MeshInstance3D", true, false):
		var mesh_node := child as MeshInstance3D
		if mesh_node == null or mesh_node.mesh == null:
			continue
		var chain: Array = []
		var n: Node = mesh_node
		while n != node:
			if n is Node3D:
				chain.push_front(n)
			n = n.get_parent()
		var xform := Transform3D()
		for item in chain:
			xform = xform * item.transform
		var box: AABB = xform * mesh_node.mesh.get_aabb()
		if first:
			total = box
			first = false
		else:
			total = total.merge(box)
	return total


func _fit_height(node: Node3D, target_height: float) -> void:
	# All three editions share the same visor-to-sole pose height (2.311 m), so every human
	# suit stands exactly 1.75 m in every engine. A pose-bound fit is the honest measure for
	# this asset: the raw GLB carries no usable bind pose to read it from.
	const POSED_HEIGHT := 2.311
	var factor: float = target_height / POSED_HEIGHT
	node.scale = Vector3(factor, factor, factor)


func update(delta: float, player_position: Vector3) -> void:
	var warning := not sim.incidents_active.is_empty()
	var eye_pulse := 1.0
	if warning:
		eye_pulse = 1.0 + 0.9 * (0.5 + 0.5 * sin(Time.get_ticks_msec() * 0.0075))
	for eye in _eyes:
		if warning:
			eye.emission = Color(1.0, 0.55, 0.15)
			eye.emission_energy_multiplier = 1.8 * eye_pulse
		else:
			eye.emission = Color(0.25, 0.85, 1.0)
			eye.emission_energy_multiplier = 1.4

	var clock_ticks := Time.get_ticks_msec() * 0.001
	for member in members:
		var root: Node3D = member["root"]
		var target: Vector3 = member["target"]
		if member["is_robot"]:
			# F10: polite follow — hover a few metres behind-and-beside the commander.
			var to_me := player_position - root.position
			to_me.y = 0.0
			var d_me := to_me.length()
			if d_me > 4.6:
				target = player_position - to_me.normalized() * 3.4
			else:
				target = root.position
			target.y = 0.0
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
		# F10: the hover robot floats, points at the next objective, and glows its warning light.
		if member["is_robot"]:
			member["phase"] = float(member["phase"]) + delta * 1.6
			root.position.y = 0.35 + sin(float(member["phase"])) * 0.06
			var pointer: Node3D = member.get("pointer")
			var thruster: OmniLight3D = member.get("thruster")
			var goal_pos := _objective_position()
			if pointer != null:
				var to_o := goal_pos - root.position
				to_o.y = 0.0
				if to_o.length() > 0.01:
					var target_yaw := atan2(to_o.x, to_o.z) - root.rotation.y
					pointer.rotation.y = lerp_angle(pointer.rotation.y, target_yaw, minf(delta * 3.0, 1.0))
				var pen_pulse := 1.05 + 0.35 * (0.5 + 0.5 * sin(clock_ticks * 3.1))
				for pen in pointer.get_children():
					if pen is MeshInstance3D:
						var mat: StandardMaterial3D = pen.material_override
						if mat != null:
							mat.emission_energy_multiplier = pen_pulse
			if thruster != null:
				if warning:
					thruster.light_color = Color(1.0, 0.55, 0.15)
					thruster.light_energy = 1.1 + 1.2 * (0.5 + 0.5 * sin(clock_ticks * 7.5))
				else:
					thruster.light_color = Color(0.35, 0.78, 0.91)
					thruster.light_energy = 1.25 + 0.5 * (0.5 + 0.5 * sin(clock_ticks * 2.4))
		var label: Label3D = member["label"]
		label.visible = distance_to_player(member, player_position) < 9.0


## F10: where NEXO points. The first objective in list order whose journal entry does not exist
## yet maps to its building; when all are complete, the pointer rests on Mission Control.
func _objective_position() -> Vector3:
	for obj in Content.objectives:
		var jid := str(obj.get("id", ""))
		var done := false
		for entry in sim.journal:
			if str(entry.get("objectiveId", "")) == jid:
				done = true
				break
		if not done:
			var b: Dictionary = Content.building(str(OBJ_TARGET.get(jid, "habitat")))
			return Vector3(float(b.get("position", [0, 0])[0]), 0.0, float(b.get("position", [0, 0])[1]))
	var mc: Dictionary = Content.building("mission_control")
	return Vector3(float(mc.get("position", [0, 0])[0]), 0.0, float(mc.get("position", [0, 0])[1]))


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

## Every world-visible crew label shows exactly one language, the selected one.
func refresh_language(arabic_on: bool) -> void:
	for member in members:
		var profile: Dictionary = member["profile"]
		var label: Label3D = member["label"]
		label.text = str(profile.get("nameAr", "")) if arabic_on else str(profile.get("nameEn", ""))
