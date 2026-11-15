extends Node3D

# Developer tool: inspect the corrected Sketchfab astronaut in isolation.
# Loads the same GLB the game uses, freezes the Sketchfab action at a chosen
# frame and renders front/side views so scale, facing and visor can be checked.

const MODEL := "res://assets/astronaut/cute_astronaut.glb"
const HEIGHT := 1.75

var inst: Node3D
var cam: Camera3D
var anim: AnimationPlayer

func _ready() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.025, 0.03, 0.045)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.55, 0.65, 0.8)
	env.ambient_light_energy = 0.6
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	# Axis markers: red is +Z, green is -Z. The camera is on +Z for the front shots.
	var marker_z := MeshInstance3D.new()
	marker_z.mesh = SphereMesh.new()
	marker_z.mesh.radius = 0.08
	marker_z.mesh.height = 0.16
	marker_z.material_override = StandardMaterial3D.new()
	marker_z.material_override.albedo_color = Color(1, 0, 0)
	marker_z.position = Vector3(0, 1.3, 1.15)
	add_child(marker_z)
	var marker_nz := MeshInstance3D.new()
	marker_nz.mesh = SphereMesh.new()
	marker_nz.mesh.radius = 0.08
	marker_nz.mesh.height = 0.16
	marker_nz.material_override = StandardMaterial3D.new()
	marker_nz.material_override.albedo_color = Color(0, 1, 0)
	marker_nz.position = Vector3(0, 1.3, -1.15)
	add_child(marker_nz)

	var packed: PackedScene = load(MODEL)
	inst = packed.instantiate()
	add_child(inst)
	anim = _find(inst, "AnimationPlayer") as AnimationPlayer
	print("animations=", anim.get_animation_list() if anim else "none")

	# Recolour the visor. The Sketchfab GLB lost its textures; this restores the
	# black glass look from the model thumbnail without touching the suit.
	for mi in _all_meshes(inst):
		for s in mi.mesh.get_surface_count():
			var m: Material = mi.mesh.surface_get_material(s)
			if m is BaseMaterial3D and String(m.resource_name).to_lower().contains("visier"):
				var dark := StandardMaterial3D.new()
				dark.albedo_color = Color(0.012, 0.018, 0.028)
				dark.metallic = 0.4
				dark.roughness = 0.06
				mi.set_surface_override_material(s, dark)
				print("visor recoloured on ", mi.name)

	# The source model stands along +Y with its face toward +Z once the action is
	# posed. Put the camera on +Z so the visor is visible.
	cam = Camera3D.new()
	cam.position = Vector3(0.0, 1.15, 4.1)
	add_child(cam)
	cam.look_at(Vector3(0.0, 1.05, 0.0))
	cam.current = true

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-28, 18, 0)
	sun.light_energy = 2.2
	add_child(sun)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-12, 195, 0)
	fill.light_energy = 0.45
	add_child(fill)

	for t in [0.0, 0.4, 0.8, 1.2, 1.6]:
		_pose(t)
		await get_tree().process_frame
		await get_tree().process_frame
		_shot("pose_%03d" % int(t * 1000))

	# Side view at the selected idle frame.
	_pose(0.4)
	cam.position = Vector3(4.1, 1.15, 0.0)
	cam.look_at(Vector3(0.0, 1.05, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("side")

	# Opposite side: if the visor is on -Z it will show from here.
	cam.position = Vector3(0.0, 1.15, -4.1)
	cam.look_at(Vector3(0.0, 1.05, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("opposite")

	# Three-quarter view: the visor edge should be visible if it is on +Z.
	cam.position = Vector3(2.7, 1.55, 3.2)
	cam.look_at(Vector3(0.0, 1.2, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("three_quarter")

	# Show only the visor mesh, from both sides, on a white background.
	env.background_color = Color(0.9, 0.9, 0.9)
	for mi in _all_meshes(inst):
		if mi.name != "Object_10":
			mi.visible = false
	cam.position = Vector3(0.0, 1.15, 2.0)
	cam.look_at(Vector3(0.0, 1.15, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("visor_front_only")
	cam.position = Vector3(0.0, 1.15, -2.0)
	cam.look_at(Vector3(0.0, 1.15, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("visor_back_only")

	# Show only the suit mesh from the front.
	for mi in _all_meshes(inst):
		mi.visible = mi.name != "Object_10"
	cam.position = Vector3(0.0, 1.15, 4.1)
	cam.look_at(Vector3(0.0, 1.05, 0.0))
	await get_tree().process_frame
	await get_tree().process_frame
	_shot("suit_front_only")
	get_tree().quit()

func _pose(t: float) -> void:
	if anim == null:
		return
	var names := anim.get_animation_list()
	if names.is_empty():
		return
	anim.play(names[0])
	anim.seek(t, true)
	anim.pause()

func _shot(tag: String) -> void:
	var img := get_viewport().get_texture().get_image()
	img.save_png("user://model_check_%s.png" % tag)
	print("saved ", tag)

func _all_meshes(n: Node, out: Array = []) -> Array:
	if n is MeshInstance3D:
		out.append(n)
	for c in n.get_children():
		_all_meshes(c, out)
	return out

func _find(n: Node, cls: String) -> Node:
	if n.get_class() == cls:
		return n
	for c in n.get_children():
		var r := _find(c, cls)
		if r:
			return r
	return null
