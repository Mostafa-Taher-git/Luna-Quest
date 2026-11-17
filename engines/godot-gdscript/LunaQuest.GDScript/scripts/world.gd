class_name ColonyWorld
extends Node3D
## Builds the colony from `content/buildings.json`: a graded pad of regolith, sixteen modules with
## walkable interiors, the paved plaza, consoles, rovers, rocks and the landing pad.
##
## Everything is procedural on purpose. The kit models in `assets/models` are the same ones the
## other builds use, and they are loaded when present, but the colony itself is geometry generated
## from the same content the simulation reads — so the building you walk into is the building the
## resource model knows about.

const TERRAIN_SIZE := 420.0
const TERRAIN_CELLS := 160
const WALL_T := 0.28

var materials: Dictionary = {}
var interactables: Array = []
var building_nodes: Dictionary = {}
var sun: DirectionalLight3D
var earth_mat: ShaderMaterial = null
var star_mat: StandardMaterial3D = null
var _attached_prop_count: Dictionary = {}
var _bilingual: Array = []   # {label, en, ar} every world Label3D produced by _label_both
var arabic_on := false

func _label_both(group: Node3D, en: String, ar: String, position: Vector3, rotation_y: float,
		size := 64, pixel := 0.006, color := Color(1, 1, 1)) -> Label3D:
	var label := _label(group, ar if arabic_on else en, position, rotation_y, size, pixel, color)
	_bilingual.append({"label": label, "en": en, "ar": ar})
	return label

## Both languages exist in the content; only the player's selected one is ever shown.
func refresh_language(arabic_on: bool) -> void:
	for pair in _bilingual:
		var label: Label3D = pair["label"]
		if label != null and is_instance_valid(label):
			label.text = str(pair["ar"]) if arabic_on else str(pair["en"])
var terrain := LQTerrain.new(1337)


func build() -> void:
	_make_materials()
	_make_environment()
	_make_terrain()
	_make_sky()
	_make_plaza()
	for building in Content.buildings:
		_make_building(building)
	_make_perimeter()
	_make_rocks()
	_make_rovers()
	_make_landing_pad()
	_make_power_farm()
	_make_kit_dressing()
	print("Luna-Quest (GDScript): colony built — %d buildings, %d consoles" % [
		Content.buildings.size(), interactables.size()])


# ---- materials -------------------------------------------------------------

func _mat(key: String, color: Color, roughness := 0.7, metallic := 0.05) -> StandardMaterial3D:
	if materials.has(key):
		return materials[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	m.metallic = metallic
	materials[key] = m
	return m


func _glow(key: String, color: Color, emission: Color, energy := 1.4) -> StandardMaterial3D:
	if materials.has(key):
		return materials[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = 0.5
	m.emission_enabled = true
	m.emission = emission
	m.emission_energy_multiplier = energy
	materials[key] = m
	return m


func _make_materials() -> void:
	_mat("hull", Color(0.83, 0.86, 0.89), 0.62, 0.08)
	_mat("panel", Color(0.70, 0.74, 0.78), 0.7, 0.06)
	_mat("dark", Color(0.16, 0.18, 0.21), 0.75, 0.15)
	_mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55)
	_mat("accent", Color(0.85, 0.52, 0.19), 0.55, 0.15)
	_mat("paving", Color(0.46, 0.46, 0.45), 0.9, 0.02)
	_mat("paving_light", Color(0.60, 0.60, 0.58), 0.9, 0.02)
	_mat("rock", Color(0.34, 0.32, 0.30), 1.0, 0.0)
	_mat("solar", Color(0.07, 0.11, 0.22), 0.25, 0.7)
	_mat("rubber", Color(0.10, 0.10, 0.11), 0.95, 0.0)
	_glow("lamp", Color(1.0, 0.96, 0.88), Color(1.0, 0.93, 0.78), 2.2)
	_glow("screen", Color(0.06, 0.12, 0.16), Color(0.32, 0.78, 0.95), 1.2)
	_glow("window", Color(0.55, 0.66, 0.75), Color(0.55, 0.66, 0.75), 0.55)
	var interior := _mat("interior", Color(0.72, 0.75, 0.78), 0.85, 0.02)
	interior.emission_enabled = true
	interior.emission = Color(0.42, 0.46, 0.52)
	interior.emission_energy_multiplier = 0.55
	var floor_mat := _mat("interior_floor", Color(0.40, 0.42, 0.45), 0.9, 0.02)
	floor_mat.emission_enabled = true
	floor_mat.emission = Color(0.24, 0.26, 0.30)
	floor_mat.emission_energy_multiplier = 0.5
	_mat("regolith", Color(0.40, 0.36, 0.31), 1.0, 0.0)


# ---- sky, sun, terrain -----------------------------------------------------

func _make_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.0, 0.0, 0.0)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	# Near-zero lunar ambient: a very low warm regolith bounce, not sky fill.
	env.ambient_light_color = Color(0.36, 0.30, 0.24)
	env.ambient_light_energy = 0.32
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.glow_enabled = true
	env.glow_intensity = 0.35
	env.fog_enabled = false
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	sun = DirectionalLight3D.new()
	sun.name = "Sun"
	sun.light_energy = 2.1
	sun.light_color = Color(1.0, 0.97, 0.92)
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 180.0
	add_child(sun)

	# Faint cool earthshine: Earth reads as a small blue disc; it throws almost no light, but the
	# shadow side never goes dead black.
	var earthshine := DirectionalLight3D.new()
	earthshine.name = "EarthShine"
	earthshine.light_color = Color(0.38, 0.47, 0.72)
	earthshine.light_energy = 0.13
	earthshine.shadow_enabled = false
	earthshine.rotation_degrees = Vector3(-38, 150, 0)
	add_child(earthshine)


## Sun elevation 11° and the azimuth from the mission clock.
func point_sun(clock: LQClock) -> void:
	if sun == null:
		return
	var elevation: float = deg_to_rad(clock.sun_elevation_deg())
	var azimuth: float = deg_to_rad(clock.sun_azimuth_deg() + 232.0)
	var dir := Vector3(sin(azimuth) * cos(elevation), sin(elevation), cos(azimuth) * cos(elevation))
	sun.look_at_from_position(dir * 200.0, Vector3.ZERO, Vector3.UP)
	sun.light_energy = 1.6 + 0.6 * clock.solar_irradiance_fraction()
	# The Earth is lit from the same sun as the colony: the phase and terminator stay in step.
	if earth_mat != null:
		earth_mat.set_shader_parameter("sun_dir", dir)
	# The horizon sun washes the stars out.
	if star_mat != null:
		star_mat.albedo_color = Color(1, 1, 1,
			clampf(1.15 - (0.5 + sin(deg_to_rad(clock.sun_elevation_deg())) * 0.6) * 0.9, 0.18, 1.0))


func height_at(x: float, z: float) -> float:
	# The shared deterministic heightfield (shared/LunaQuest.Core/Terrain/LunarTerrain.cs),
	# ported exactly in terrain.gd, so GDScript, Godot .NET and Unity draw the same Moon.
	return terrain.height(x, z)


func _make_terrain() -> void:
	var half := TERRAIN_SIZE * 0.5
	var step := TERRAIN_SIZE / float(TERRAIN_CELLS)
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()

	# Sample the shared heightfield once per grid vertex; normals come from the same grid,
	# so the drawn surface and the analytic height_at agree to within a grid cell.
	var n := TERRAIN_CELLS + 1
	var heights := PackedFloat32Array()
	heights.resize(n * n)
	for iz in range(n):
		for ix in range(n):
			heights[iz * n + ix] = height_at(-half + ix * step, -half + iz * step)
	for iz in range(n):
		for ix in range(n):
			var x := -half + ix * step
			var z := -half + iz * step
			verts.append(Vector3(x, heights[iz * n + ix], z))
			var hl := heights[iz * n + maxi(ix - 1, 0)]
			var hr := heights[iz * n + mini(ix + 1, TERRAIN_CELLS)]
			var hd := heights[maxi(iz - 1, 0) * n + ix]
			var hu := heights[mini(iz + 1, TERRAIN_CELLS) * n + ix]
			normals.append(Vector3(hl - hr, 2.0 * step, hd - hu).normalized())
			uvs.append(Vector2(float(ix) / float(TERRAIN_CELLS) * 40.0, float(iz) / float(TERRAIN_CELLS) * 40.0))

	for iz in range(TERRAIN_CELLS):
		for ix in range(TERRAIN_CELLS):
			var a := iz * (TERRAIN_CELLS + 1) + ix
			var b := a + 1
			var c := a + TERRAIN_CELLS + 1
			var d := c + 1
			# Clockwise seen from above, so the surface faces up for the renderer *and* for
			# the physics trimesh baked from it. The old a-c-b / b-c-d order wound every
			# triangle face-down: the ground was drawn inside-out and a ray cast downwards
			# passed straight through it (nothing could stand on the Moon).
			indices.append(a)
			indices.append(b)
			indices.append(c)
			indices.append(b)
			indices.append(d)
			indices.append(c)

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	var node := MeshInstance3D.new()
	node.name = "Regolith"
	node.mesh = mesh
	node.material_override = _regolith_material()
	add_child(node)

	# Ground collision is baked from the very mesh that is drawn, so physics and visuals are
	# the same surface everywhere — the graded pad, crater bowls and highland swells alike.
	# (The old flat slab left the commander walking through hills beyond the pad.)
	var body := StaticBody3D.new()
	body.name = "Ground"
	var shape := CollisionShape3D.new()
	shape.shape = mesh.create_trimesh_shape()
	body.add_child(shape)
	add_child(body)


## The scanned regolith: Poly Haven "moon_dusted_03" (CC0), the same PBR set the Godot .NET
## and browser builds wear. Albedo + OpenGL normal + roughness + AO over the graded heightfield,
## tiled so a tile spans a few metres of basaltic dust. Falls back to the flat clay colour when
## the textures have not been synced in, so a fresh clone still boots.
func _regolith_material() -> StandardMaterial3D:
	var m := _mat("regolith", Color(0.40, 0.36, 0.31), 1.0, 0.0)
	# Prefer the moon_02 set named in the design table; fall back to moon_dusted_03, then clay.
	var dir := "res://assets/real/regolith/"
	var stem := "moon_dusted_03"
	if ResourceLoader.exists(dir + "moon_02/moon_02_albedo.jpg"):
		dir += "moon_02/"
		stem = "moon_02"
	var albedo_path := dir + stem + "_albedo.jpg"
	if not ResourceLoader.exists(albedo_path):
		return m
	m.albedo_texture = load(albedo_path) as Texture2D
	m.albedo_color = Color(1.0, 1.0, 1.0)
	m.roughness = 1.0
	m.metallic = 0.0
	# One tile per ~3.5 m (420 m span, x ~120 repeats) instead of 3 repeats over the whole site.
	m.uv1_scale = Vector3(120.0, 120.0, 120.0)
	var normal_path := dir + stem + "_normal.jpg"
	if ResourceLoader.exists(normal_path):
		m.normal_enabled = true
		m.normal_texture = load(normal_path) as Texture2D
		m.normal_scale = 0.6
	var rough_path := dir + stem + "_rough.jpg"
	if ResourceLoader.exists(rough_path):
		m.roughness_texture = load(rough_path) as Texture2D
	var ao_path := dir + stem + "_ao.jpg"
	if ResourceLoader.exists(ao_path):
		m.ao_enabled = true
		m.ao_texture = load(ao_path) as Texture2D
		m.ao_light_affect = 0.6
	return m


func _make_sky() -> void:
	# Stars are points of light, not squares the size of the Moon. Magnitude distribution:
	# many faint points, few bright ones, colour spread, no twinkle. A faint Milky Way band
	# is drawn from a rotated great circle of haze points.
	var verts := PackedVector3Array()
	var colors := PackedColorArray()
	var rng := RandomNumberGenerator.new()
	rng.seed = 4242
	var band_normal := Vector3(0.42, 0.9, 0.25).normalized()
	var bin1 := band_normal.cross(Vector3.RIGHT)
	if bin1.length_squared() < 1e-4:
		bin1 = band_normal.cross(Vector3.FORWARD)
	bin1 = bin1.normalized()
	var bin2 := band_normal.cross(bin1)
	for i in range(2200):
		var is_band := i >= 1700
		var dir: Vector3
		var size: float
		var color: Color
		if is_band:
			var t := rng.randf() * TAU
			var spread := (rng.randf() + rng.randf() + rng.randf() - 1.5) * 0.34
			dir = (bin1 * cos(t) + bin2 * sin(t) + band_normal * spread).normalized()
			if dir.y < 0.03:
				dir.y = 0.03
				dir = dir.normalized()
			size = 0.15 + pow(rng.randf(), 3.0) * 0.15
			var b := 0.16 + rng.randf() * 0.18
			color = Color(b * 0.75, b * 0.82, b)
		else:
			var az := rng.randf() * TAU
			var el := asin(0.02 + rng.randf() * 0.95)
			dir = Vector3(cos(el) * cos(az), sin(el), cos(el) * sin(az))
			var m := pow(rng.randf(), 2.6)
			size = 0.14 + m * 0.46
			var b2 := 0.35 + pow(rng.randf(), 2.2) * 0.75
			var w := rng.randf()
			if w < 0.78:
				color = Color(b2, b2, b2)
			elif w < 0.89:
				color = Color(b2, b2 * 0.82, b2 * 0.65)
			else:
				color = Color(b2 * 0.78, b2 * 0.88, b2)
		var distance := 1500.0
		var center: Vector3 = dir * distance
		var up := Vector3.UP if absf(dir.y) < 0.9 else Vector3.RIGHT
		var right: Vector3 = dir.cross(up).normalized()
		var up2: Vector3 = right.cross(dir).normalized()
		var p0 := center + right * size + up2 * size
		var p1 := center - right * size + up2 * size
		var p2 := center - right * size - up2 * size
		var p3 := center + right * size - up2 * size
		for p in [p0, p1, p2, p0, p2, p3]:
			verts.append(p)
			colors.append(color)

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_COLOR] = colors
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	star_mat = StandardMaterial3D.new()
	star_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	star_mat.vertex_color_use_as_albedo = true
	star_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	star_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	var stars := MeshInstance3D.new()
	stars.name = "Stars"
	stars.mesh = mesh
	stars.material_override = star_mat
	stars.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(stars)

	# Earth, low over the ridge the way it hangs over the Shackleton rim in the intro film.
	# The real Blue Marble surface, lit from the same sun as the colony (see point_sun): the
	# phase and the terminator match the ground, and a thin atmosphere hugs the lit limb.
	var earth := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 19.0
	sphere.height = 38.0
	sphere.radial_segments = 64
	sphere.rings = 32
	earth.mesh = sphere
	var earth_texture: Texture2D = load("res://assets/real/earth/blue_marble_2048.jpg")
	if ResourceLoader.exists("res://shaders/earth.gdshader"):
		var shader := load("res://shaders/earth.gdshader") as Shader
		earth_mat = ShaderMaterial.new()
		earth_mat.shader = shader
		earth_mat.set_shader_parameter("surface", earth_texture)
		earth_mat.set_shader_parameter("sun_dir", Vector3(0.35, 0.2, 0.8))
		earth.material_override = earth_mat
	else:
		var fallback := StandardMaterial3D.new()
		fallback.albedo_texture = earth_texture
		fallback.roughness = 1.0
		earth.material_override = fallback
	earth.position = Vector3(-620, 210, -900)
	earth.rotation = Vector3(0.35, -1.9, 0.41)
	earth.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	earth.name = "Earth"
	add_child(earth)


const SHELL_ASSET := {
	"habitat": "rigid-cylinder-habitat-9b2fdbb4.glb",
	"mission_control": "rigid-cylinder-habitat-2dec4448.glb",
	"greenhouse": "greenhouse-module-b35be615.glb",
	"workshop": "workshop-module-7729780d.glb",
	"life_support": "oxygen-extraction-plan-002fb0ae.glb",
	"lab": "medical-module-6d266b02.glb",
	"medical": "medical-module-6d266b02.glb",
	"garage": "inflatable-dome-habita-f86f5d7b.glb",
	"storage": "battery-container-9755ed67.glb",
	"comms_tower": "comms-relay-mast-4cdfe54f.glb",
	"regolith_plant": "sintering-print-gantry-64cc192a.glb",
	"storm_shelter": "inflatable-dome-habita-514822a4.glb",
	"power_farm": "solar-array-row-e52454d9.glb",
	"ice_site": "regolith-excavator-64b4560f.glb",
	"airlock": "airlock-with-dust-lock-fe27e26c.glb",
}

func _aabb_of(node: Node3D, xform: Transform3D) -> AABB:
	var aabb := AABB()
	var found := false
	var mine := xform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		aabb = mine * node.mesh.get_aabb()
		found = true
	for child in node.get_children():
		if child is Node3D:
			var sub := _aabb_of(child, mine)
			if sub.size.length() < 1e-6:
				continue
			if not found:
				aabb = sub
				found = true
			else:
				aabb = aabb.merge(sub)
	return aabb

## The exterior shell: match the building with a CC0 kit module scaled to its footprint.
## The plain box wall colliders stay exactly where they are; only their meshes are hidden.
func _make_shell(group: Node3D, id: String, w: float, d: float, h: float) -> void:
	if not SHELL_ASSET.has(id):
		return
	var asset: String = str(SHELL_ASSET[id])
	if asset == "":
		return
	var path := "res://assets/models/lunar_colony/" + asset
	if not ResourceLoader.exists(path):
		return
	var scene := load(path) as PackedScene
	if scene == null:
		return
	var node := scene.instantiate() as Node3D
	if node == null:
		return
	node.name = "Shell_%s" % id
	# Measure before scaling: the kit modules are not uniformly sized.
	var aabb := _aabb_of(node, Transform3D(Basis(), Vector3.ZERO))
	if aabb.size.length() > 1e-6:
		var sx := clampf(w / maxf(aabb.size.x, 1e-3), 0.5, 2.5)
		var sy := clampf(h / maxf(aabb.size.y, 1e-3), 0.5, 2.5)
		var sz := clampf(d / maxf(aabb.size.z, 1e-3), 0.5, 2.5)
		node.scale = Vector3(sx, sy, sz)
		# Feet at y = 0, footprint centred on the building origin.
		var min_after := Vector3(aabb.position.x * sx, aabb.position.y * sy, aabb.position.z * sz)
		var centre_after := min_after + Vector3(aabb.size.x * sx * 0.5, 0.0, aabb.size.z * sz * 0.5)
		node.position = Vector3(-centre_after.x, -min_after.y, -centre_after.z)
	else:
		node.scale = Vector3.ONE
	group.add_child(node)

## The porch: the airlock-style entrance every walkable module gets. Gold MLI collar, gold
## foil panels, a hazard-stripe sill and handrails, all pointing at the door azimuth.
func _add_porch(group: Node3D, door: int, w: float, d: float) -> void:
	var off := Vector3.ZERO
	var rot := 0.0
	match door:
		0: off = Vector3(0, 0, d * 0.5); rot = 0.0
		1: off = Vector3(0, 0, -d * 0.5); rot = PI
		2: off = Vector3(w * 0.5, 0, 0); rot = PI * 0.5
		3: off = Vector3(-w * 0.5, 0, 0); rot = -PI * 0.5
	var porch := Node3D.new()
	porch.name = "Porch"
	porch.position = off
	porch.rotation.y = rot
	group.add_child(porch)
	var gold := _mat("mli_gold", Color(0.78, 0.55, 0.14), 0.35, 0.6)
	var gold_glow := _glow("mli_gold_glow", Color(0.92, 0.66, 0.18), Color(0.98, 0.72, 0.22), 0.35)
	var alloy := _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55)
	# Gold foil collar around the doorway.
	_box(porch, Vector3(3.4, 0.6, 0.55), Vector3(0, 0.3, 0.35), gold_glow)
	# Gold side panels, ribbed.
	for side in [-1.0, 1.0]:
		_box(porch, Vector3(0.5, 2.7, 0.62), Vector3(side * 1.35, 1.6, 0.15), gold)
		_box(porch, Vector3(0.55, 0.09, 0.66), Vector3(side * 1.35, 1.05, 0.16), alloy)
	# Handrails at both sides of the sill.
	for side in [-1.0, 1.0]:
		_cylinder(porch, 0.055, 1.15, Vector3(side * 1.05, 0.58, 0.62), alloy)
	# Hazard-stripe sill: yellow between black blocks.
	_box(porch, Vector3(2.4, 0.06, 0.34), Vector3(0, 0.03, 0.52),
		_mat("hazard_yellow", Color(0.82, 0.66, 0.12), 0.7, 0.02))
	_box(porch, Vector3(0.35, 0.07, 0.35), Vector3(-1.05, 0.035, 0.52),
		_mat("hazard_black", Color(0.12, 0.11, 0.10), 0.8, 0.02))
	_box(porch, Vector3(0.35, 0.07, 0.35), Vector3(1.05, 0.035, 0.52),
		_mat("hazard_black", Color(0.12, 0.11, 0.10), 0.8, 0.02))

## The extra dressing around the shell: rib rings, a gold MLI panel, a radiator pair on the
## non-door side and a cable tray.
func _dress_shell(group: Node3D, w: float, d: float, h: float, door: int, id: String) -> void:
	var rib := _mat("rib", Color(0.42, 0.45, 0.5), 0.55, 0.35)
	var gold := _mat("mli_gold", Color(0.78, 0.55, 0.14), 0.35, 0.6)
	var radiator := _mat("radiator", Color(0.28, 0.30, 0.34), 0.7, 0.05)
	# MLI gold foil patch on the side facing the plaza.
	_box(group, Vector3(1.6, 1.6, 0.12), Vector3(0, h * 0.62, -d * 0.5 - 0.05) if door != 0 else Vector3(w * 0.5 + 0.05, h * 0.62, 0), gold, 0.0)
	# Rib rings around the body at one third and two thirds of the height.
	for frac in [0.33, 0.67]:
		var ring := MeshInstance3D.new()
		var torus := TorusMesh.new()
		torus.inner_radius = minf(w, d) * 0.40
		torus.outer_radius = torus.inner_radius + 0.18
		torus.rings = 32
		torus.ring_segments = 8
		ring.mesh = torus
		ring.material_override = rib
		ring.rotation = Vector3(PI * 0.5, 0.0, 0.0)
		ring.position = Vector3(0, h * frac, 0)
		group.add_child(ring)
	# A radiator pair on the side opposite the door (dark panel).
	if door == 3:
		_box(group, Vector3(0.14, 1.9, d * 0.7), Vector3(w * 0.5 + 0.07, h * 0.5, 0), radiator)
	elif door == 2:
		_box(group, Vector3(0.14, 1.9, d * 0.7), Vector3(-w * 0.5 - 0.07, h * 0.5, 0), radiator)
	elif door == 0:
		_box(group, Vector3(w * 0.7, 1.9, 0.14), Vector3(0, h * 0.5, -d * 0.5 - 0.07), radiator)
	else:
		_box(group, Vector3(w * 0.7, 1.9, 0.14), Vector3(0, h * 0.5, d * 0.5 + 0.07), radiator)
	# A cable tray running the length of the building, just below the roof line.
	_box(group, Vector3(0.30, 0.16, d * 0.8), Vector3(-w * 0.5 - 0.15, h * 0.78, 0), rib)

## Furnish the interior per building id. Everything inside sidereal bounds; props carry no
## gameplay collisions so walking through them never blocks an egress route.
func _furnish_interior(group: Node3D, id: String, w: float, d: float, h: float) -> void:
	var count := 0
	var steel := _mat("steel", Color(0.72, 0.74, 0.78), 0.4, 0.35)
	var wood := _mat("worktop", Color(0.45, 0.38, 0.30), 0.7, 0.05)
	var dark := _mat("panel_dark", Color(0.32, 0.34, 0.38), 0.6, 0.05)
	var fabric := _mat("fabric", Color(0.55, 0.52, 0.47), 0.85, 0.02)

	var _mk := func(prop_name: String, size: Vector3, pos: Vector3, mat: Material, rot_y := 0.0):
		_box(group, size, pos, mat, rot_y)

	if id == "habitat":
		# Bunk beds in the quarters end, kitchen counter along the back wall, a table in the common room.
		_box(group, Vector3(1.9, 0.55, 0.85), Vector3(-5.0, 0.55, -3.5), fabric)              # bunk 1 lower
		_box(group, Vector3(1.9, 0.22, 0.85), Vector3(-5.0, 1.65, -3.5), wood)                 # bunk 1 upper
		_cylinder(group, 0.07, 1.7, Vector3(-5.9, 0.85, -3.0), steel)                          # posts
		_cylinder(group, 0.07, 1.7, Vector3(-4.1, 0.85, -4.0), steel)
		_box(group, Vector3(1.9, 0.55, 0.85), Vector3(-5.0, 0.55, 3.5), fabric)                # bunk 2 lower
		_box(group, Vector3(1.9, 0.22, 0.85), Vector3(-5.0, 1.65, 3.5), wood)                  # bunk 2 upper
		_cylinder(group, 0.07, 1.7, Vector3(-5.9, 0.85, 3.0), steel)
		_cylinder(group, 0.07, 1.7, Vector3(-4.1, 0.85, 4.0), steel)
		_box(group, Vector3(4.5, 0.95, 0.7), Vector3(3.0, 0.5, -5.2), dark)                    # kitchen counter
		_box(group, Vector3(4.5, 0.15, 0.75), Vector3(3.0, 1.0, -5.2), steel)
		_cylinder(group, 0.9, 0.12, Vector3(0.5, 0.45, 1.0), steel, 20)                        # table top
		_cylinder(group, 0.15, 0.45, Vector3(0.5, 0.22, 1.0), steel)
		_box(group, Vector3(2.2, 0.5, 1.0), Vector3(0.5, 0.28, 2.6), fabric)                     # bench
	elif id == "mission_control":
		# Console desks ringing the operations floor + a map wall panel.
		for x in [-3.0, 0.0, 3.0]:
			_box(group, Vector3(2.4, 0.95, 0.8), Vector3(x, 0.5, -2.2), dark)
			_box(group, Vector3(2.4, 0.4, 0.14), Vector3(x, 1.15, -2.35), _glow("screen_%d" % int(x + 4.0), Color(0.10, 0.22, 0.35), Color(0.45, 0.75, 1.0), 0.5))
			_box(group, Vector3(2.4, 0.35, 0.8), Vector3(x, 0.18, -1.35), wood)
		_box(group, Vector3(5.6, 2.4, 0.18), Vector3(0.0, 1.6, -5.1), steel)                   # map wall
		_box(group, Vector3(5.0, 1.8, 0.05), Vector3(0.0, 1.65, -5.0), _glow("map", Color(0.08, 0.14, 0.24), Color(0.25, 0.45, 0.75), 0.35))
		_box(group, Vector3(1.8, 2.2, 0.7), Vector3(-5.3, 1.1, 0.5), steel)                    # comms rack
	elif id == "greenhouse":
		# Grow racks with grow lights + water bar.
		for x in [-4.5, -1.5, 1.5, 4.5]:
			_box(group, Vector3(2.0, 1.9, 1.0), Vector3(x, 0.95, -2.0), steel)                  # rack
			_box(group, Vector3(1.9, 0.10, 0.9), Vector3(x, 1.95, -2.0), _glow("growlight", Color(0.85, 0.95, 0.55), Color(0.75, 0.95, 0.45), 0.7))
			_box(group, Vector3(2.1, 0.35, 1.1), Vector3(x, 0.2, -2.0), wood)                  # tray
		_box(group, Vector3(1.2, 1.7, 1.2), Vector3(-5.6, 0.85, 1.6), steel)                    # water tank
	elif id == "workshop":
		_box(group, Vector3(3.6, 1.05, 1.15), Vector3(-3.4, 0.55, -3.6), dark)                  # workbench
		_box(group, Vector3(3.6, 0.12, 1.25), Vector3(-3.4, 1.12, -3.6), wood)
		_box(group, Vector3(3.2, 1.05, 1.15), Vector3(3.6, 0.55, -3.6), dark)
		_box(group, Vector3(3.2, 0.12, 1.25), Vector3(3.6, 1.12, -3.6), wood)
		_box(group, Vector3(3.6, 1.9, 0.16), Vector3(-3.5, 1.15, 4.0), dark)                    # tool board
		_box(group, Vector3(1.0, 1.1, 1.0), Vector3(4.6, 0.55, 3.6), steel)                     # parts crate
	elif id == "life_support":
		_cylinder(group, 0.7, 2.3, Vector3(-2.6, 1.15, -1.6), steel, 20)                        # scrubber tank
		_cylinder(group, 0.7, 2.3, Vector3(0.0, 1.15, -1.6), _mat("tank_yellow", Color(0.85, 0.75, 0.15), 0.55, 0.15), 20) # o2 tank
		_cylinder(group, 0.7, 2.3, Vector3(2.6, 1.15, -1.6), steel, 20)                          # water tank
		_box(group, Vector3(4.2, 1.0, 0.6), Vector3(0, 0.5, 0.4), dark)                         # control bank
		_box(group, Vector3(4.2, 0.14, 0.1), Vector3(0, 1.05, 0.35), _glow("led", Color(0.05, 0.2, 0.08), Color(0.3, 1.0, 0.4), 0.6))
		_box(group, Vector3(2.0, 2.0, 0.35), Vector3(5.0, 1.0, 1.5), steel)                     # filter rack
	elif id == "lab":
		_box(group, Vector3(3.2, 1.05, 1.1), Vector3(-2.6, 0.55, -2.8), dark)                   # sample bench
		_box(group, Vector3(3.2, 0.12, 1.2), Vector3(-2.6, 1.12, -2.8), wood)
		_box(group, Vector3(3.2, 1.05, 1.1), Vector3(2.7, 0.55, -2.8), dark)
		_box(group, Vector3(3.2, 0.12, 1.2), Vector3(2.7, 1.12, -2.8), wood)
		_cylinder(group, 0.09, 0.6, Vector3(-2.6, 1.4, -2.8), dark, 12)                         # microscope column
		_box(group, Vector3(0.6, 0.28, 0.4), Vector3(-2.6, 1.72, -2.8), _glow("scope", Color(0.1, 0.18, 0.3), Color(0.4, 0.65, 1.0), 0.45))
		_box(group, Vector3(2.4, 0.9, 0.5), Vector3(0.0, 0.45, 1.4), fabric)                    # sample crate
	elif id == "medical":
		_box(group, Vector3(1.9, 0.7, 0.85), Vector3(-2.0, 0.4, -2.5), dark)                    # bed base
		_box(group, Vector3(1.9, 0.22, 0.95), Vector3(-2.0, 0.82, -2.5), fabric)                # mattress
		_box(group, Vector3(0.4, 0.16, 0.5), Vector3(-2.75, 0.9, -2.5), fabric)                 # pillow
		_box(group, Vector3(1.6, 1.7, 0.3), Vector3(2.0, 0.85, -5.2), steel)                    # cabinet
		_box(group, Vector3(1.1, 0.7, 0.12), Vector3(2.0, 1.85, -5.1), _glow("monitor", Color(0.1, 0.16, 0.22), Color(0.35, 0.72, 0.95), 0.5))
	elif id == "garage":
		_box(group, Vector3(5.6, 0.08, 3.0), Vector3(0, 0.06, 1.2), _mat("bay_floor", Color(0.33, 0.34, 0.36), 0.7, 0.15)) # parking bay
		_box(group, Vector3(0.35, 2.0, 0.35), Vector3(-1.9, 1.0, -2.4), steel)                  # charge post
		_cylinder(group, 0.09, 2.1, Vector3(1.9, 1.05, -2.4), steel, 12)                         # charge cable
		_box(group, Vector3(1.2, 1.05, 1.1), Vector3(4.4, 0.55, -2.6), dark)                     # tool crate
		_box(group, Vector3(1.0, 0.8, 0.9), Vector3(-4.3, 0.4, -2.6), fabric)                    # suit crate
	elif id == "storage":
		for i in [0, 1, 2]:
			_box(group, Vector3(1.4, 1.4, 1.4), Vector3(-3.4 + i * 2.1, 0.75, -3.3), wood)        # stack
			_box(group, Vector3(1.4, 1.4, 1.4), Vector3(-3.4 + i * 2.1, 0.75, 3.3), steel)        # stack
		_box(group, Vector3(6.8, 1.8, 0.4), Vector3(0.0, 0.95, 0.0), dark)                       # shelf
		_box(group, Vector3(6.0, 0.14, 0.25), Vector3(0.0, 1.95, 0.0), steel)
	elif id == "comms_tower":
		_box(group, Vector3(2.6, 0.9, 0.7), Vector3(-2.2, 0.45, -2.6), dark)                     # monitor desk
		_box(group, Vector3(2.6, 0.42, 0.12), Vector3(-2.2, 1.12, -2.6), _glow("screen_t", Color(0.09, 0.16, 0.3), Color(0.35, 0.75, 1.0), 0.5))
		_cylinder(group, 0.09, 3.2, Vector3(1.6, 1.6, -1.8), steel, 12)                          # mast
		_box(group, Vector3(4.5, 0.7, 0.7), Vector3(-2.0, 0.35, -3.5), dark)                     # equipment bench
	elif id == "regolith_plant":
		_cylinder(group, 1.1, 1.6, Vector3(-3.0, 0.8, -1.6), steel, 14)                          # hopper
		_box(group, Vector3(3.6, 0.7, 0.8), Vector3(0.5, 0.35, -1.6), steel)                     # conveyor body
		_box(group, Vector3(3.6, 0.08, 0.85), Vector3(0.5, 0.74, -1.6), dark)                    # conveyor belt
		_box(group, Vector3(1.4, 1.5, 0.6), Vector3(3.6, 0.75, 0.4), dark)                       # control box
	elif id == "storm_shelter":
		_box(group, Vector3(1.9, 0.55, 0.85), Vector3(-2.4, 0.55, -2.6), fabric)                 # bunk
		_box(group, Vector3(1.9, 0.22, 0.85), Vector3(-2.4, 1.55, -2.6), wood)
		_box(group, Vector3(1.9, 0.55, 0.85), Vector3(2.4, 0.55, -2.6), fabric)
		_box(group, Vector3(1.9, 0.22, 0.85), Vector3(2.4, 1.55, -2.6), wood)
		_box(group, Vector3(2.4, 1.4, 0.5), Vector3(0.0, 0.7, 3.4), steel)                       # emergency box
	elif id == "power_farm":
		for x in [-4.2, -1.4, 1.4, 4.2]:
			_box(group, Vector3(2.2, 1.5, 1.2), Vector3(x, 0.75, -3.0), dark)                     # battery bank
			_box(group, Vector3(2.2, 0.28, 0.25), Vector3(x, 1.45, -3.0), _glow("b_led_%d" % int(x+6.0), Color(0.05, 0.2, 0.08), Color(0.3, 1.0, 0.4), 0.55))
	elif id == "ice_site":
		_cylinder(group, 0.32, 1.9, Vector3(-2.6, 0.95, -2.0), steel, 14)                        # drill mast
		_cylinder(group, 0.16, 1.2, Vector3(-2.6, 2.2, -2.0), _mat("amber", Color(0.85, 0.6, 0.12), 0.5, 0.2), 12)
		_box(group, Vector3(1.2, 1.0, 1.0), Vector3(2.0, 0.5, -2.4), dark)                       # sample crate
		_box(group, Vector3(2.4, 0.9, 0.65), Vector3(0.0, 0.45, 2.4), wood)                      # console desk
	elif id == "airlock":
		_box(group, Vector3(1.1, 1.6, 0.15), Vector3(-1.8, 0.8, 0.0), steel)                     # suit rack
		_box(group, Vector3(0.9, 0.35, 1.0), Vector3(1.9, 0.18, 0.2), dark)                      # tool box
	_attached_prop_count[id] = group.get_child_count()

func _box(parent: Node3D, size: Vector3, position: Vector3, material: Material,
		rotation_y := 0.0) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.material_override = material
	node.position = position
	node.rotation.y = rotation_y
	parent.add_child(node)
	return node


func _blocker(parent: Node3D, size: Vector3, position: Vector3, rotation_y := 0.0) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.position = position
	body.rotation.y = rotation_y
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	body.add_child(shape)
	parent.add_child(body)
	return body


func _cylinder(parent: Node3D, radius: float, height: float, position: Vector3,
		material: Material, radial_segments := 20) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius
	mesh.bottom_radius = radius
	mesh.height = height
	mesh.radial_segments = radial_segments
	node.mesh = mesh
	node.material_override = material
	node.position = position
	parent.add_child(node)
	return node


func _label(parent: Node3D, text: String, position: Vector3, rotation_y: float,
		size := 64, pixel := 0.006, color := Color(1, 1, 1)) -> Label3D:
	var label := Label3D.new()
	label.text = text
	label.font_size = size
	label.pixel_size = pixel
	label.modulate = color
	label.outline_size = 12
	label.outline_modulate = Color(0, 0, 0, 0.85)
	label.position = position
	label.rotation.y = rotation_y
	label.billboard = BaseMaterial3D.BILLBOARD_DISABLED
	label.no_depth_test = false
	parent.add_child(label)
	return label


# ---- the plaza -------------------------------------------------------------

func _make_plaza() -> void:
	var plaza := Node3D.new()
	plaza.name = "Plaza"
	add_child(plaza)

	# A paved spine between the airlock and the habitat, with painted edges.
	var spine := 56.0
	var centre := 8.0
	_box(plaza, Vector3(26, 0.14, spine), Vector3(0, 0.07, centre), _mat("paving", Color(0.46, 0.46, 0.45), 0.9))
	_box(plaza, Vector3(0.5, 0.16, spine), Vector3(-12.6, 0.09, centre), _mat("paving_light", Color(0.6, 0.6, 0.58), 0.9))
	_box(plaza, Vector3(0.5, 0.16, spine), Vector3(12.6, 0.09, centre), _mat("paving_light", Color(0.6, 0.6, 0.58), 0.9))
	_box(plaza, Vector3(22, 0.05, spine), Vector3(0, 0.15, centre), _glow("accent_line", Color(0.85, 0.52, 0.19), Color(0.85, 0.52, 0.19), 0.35))

	# Bollards with lit tops, and a couple of bare regolith strips either side.
	for i in range(6):
		var z := -14.0 + i * 8.0
		for side in [-1.0, 1.0]:
			var x: float = side * 13.4
			_cylinder(plaza, 0.16, 1.15, Vector3(x, 0.6, z), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
			var cap := _box(plaza, Vector3(0.42, 0.16, 0.42), Vector3(x, 1.24, z), _glow("lamp", Color(1, 0.96, 0.88), Color(1, 0.93, 0.78), 1.6))
			cap.name = "bollard-light"


# ---- buildings -------------------------------------------------------------

func _door_direction(building: Dictionary) -> int:
	# 0 north (+z), 1 south (-z), 2 east (+x), 3 west (-x). The door faces the plaza.
	var pos: Array = building.get("position", [0, 0])
	var v := Vector2(-float(pos[0]), -float(pos[1]))
	if absf(v.x) > absf(v.y):
		return 2 if v.x > 0 else 3
	return 0 if v.y > 0 else 1


func _make_building(building: Dictionary) -> void:
	var id: String = building.get("id", "")
	var footprint: Array = building.get("footprint", [10, 8])
	var w := float(footprint[0])
	var d := float(footprint[1])
	var h := float(building.get("height", 5.6))
	var pos: Array = building.get("position", [0, 0])
	var group := Node3D.new()
	group.name = "Building_%s" % id
	group.position = Vector3(float(pos[0]), 0.0, float(pos[1]))
	add_child(group)
	building_nodes[id] = group

	var tint := Color(str(building.get("color", "#d8dde2")))
	var wall_mat := _mat("hull", Color(0.83, 0.86, 0.89), 0.62, 0.08)
	var trim_mat := _mat("trim_%s" % id, tint.lerp(Color(0.85, 0.52, 0.19), 0.45), 0.6, 0.1)

	# Pad. It is sunk so its surface sits 6 cm proud of the regolith: the capsule walks over that
	# without a step, and the doorway stays walkable.
	_box(group, Vector3(w + 3.2, 0.3, d + 3.2), Vector3(0, -0.09, 0), _mat("paving", Color(0.46, 0.46, 0.45), 0.9))
	_blocker(group, Vector3(w + 3.2, 0.3, d + 3.2), Vector3(0, -0.09, 0))

	var door := _door_direction(building)
	var door_width := 2.4
	var door_height := 3.0
	var wall_meshes: Array = []

	# Walls. The door wall is built in two segments with a lintel above the opening.
	var walls := [
		{"axis": "z", "sign": 1.0, "length": w},
		{"axis": "z", "sign": -1.0, "length": w},
		{"axis": "x", "sign": 1.0, "length": d},
		{"axis": "x", "sign": -1.0, "length": d},
	]
	for index in range(walls.size()):
		var wall: Dictionary = walls[index]
		var along: float = float(wall["length"])
		var sign: float = float(wall["sign"])
		var thickness := WALL_T
		var size: Vector3
		var offset: Vector3
		if wall["axis"] == "z":
			size = Vector3(along, h, thickness)
			offset = Vector3(0, h * 0.5, sign * (d * 0.5 - thickness * 0.5))
		else:
			size = Vector3(thickness, h, along)
			offset = Vector3(sign * (w * 0.5 - thickness * 0.5), h * 0.5, 0)

		if index == door:
			# Two jambs plus a lintel: a real opening, not a painted door.
			var jamb := (along - door_width) * 0.5
			var jamb_size := size
			var left_pos := offset
			var right_pos := offset
			if wall["axis"] == "z":
				jamb_size = Vector3(jamb, h, thickness)
				left_pos = offset + Vector3(-(door_width * 0.5 + jamb * 0.5), 0, 0)
				right_pos = offset + Vector3(door_width * 0.5 + jamb * 0.5, 0, 0)
			else:
				jamb_size = Vector3(thickness, h, jamb)
				left_pos = offset + Vector3(0, 0, -(door_width * 0.5 + jamb * 0.5))
				right_pos = offset + Vector3(0, 0, door_width * 0.5 + jamb * 0.5)
			if jamb > 0.05:
				var jamb_mesh_a := _box(group, jamb_size, left_pos, wall_mat)
				wall_meshes.append(jamb_mesh_a)
				_blocker(group, jamb_size, left_pos)
				var jamb_mesh_b := _box(group, jamb_size, right_pos, wall_mat)
				wall_meshes.append(jamb_mesh_b)
				_blocker(group, jamb_size, right_pos)
			var lintel_h: float = h - door_height
			if lintel_h > 0.05:
				var lintel_size := size
				var lintel_pos := offset
				if wall["axis"] == "z":
					lintel_size = Vector3(door_width, lintel_h, thickness)
				else:
					lintel_size = Vector3(thickness, lintel_h, door_width)
				lintel_pos.y = door_height + lintel_h * 0.5
				var lintel_mesh := _box(group, lintel_size, lintel_pos, wall_mat)
				wall_meshes.append(lintel_mesh)
				_blocker(group, lintel_size, lintel_pos)
		else:
			wall_meshes.append(_box(group, size, offset, wall_mat))
			_blocker(group, size, offset)

	# Shell + dressing + interior. The colliders for the plain box walls stay untouched.
	_make_shell(group, id, w, d, h)
	_add_porch(group, door, w, d)
	_dress_shell(group, w, d, h, door, id)
	_furnish_interior(group, id, w, d, h)
	# Hide only the plain wall MESHES; their blockers stay in place.
	for m in wall_meshes:
		if m != null:
			m.visible = false

	# Interior floor and ceiling.
	_box(group, Vector3(w - WALL_T * 2.0, 0.08, d - WALL_T * 2.0), Vector3(0, 0.1, 0),
		_mat("interior_floor", Color(0.40, 0.42, 0.45), 0.9, 0.02))
	_box(group, Vector3(w - WALL_T * 2.0, 0.14, d - WALL_T * 2.0), Vector3(0, h - 0.09, 0),
		_mat("ceiling", Color(0.72, 0.75, 0.78), 0.85, 0.02))

	_make_roof(group, building, w, d, h, wall_mat)
	_make_trim(group, w, d, h, trim_mat)
	_make_sign(group, building, w, d, h, door)
	_make_interactions(group, building, w, d, h, door)
	_make_interior_light(group, w, d, h)


func _make_roof(group: Node3D, building: Dictionary, w: float, d: float, h: float,
		wall_mat: Material) -> void:
	var id: String = building.get("id", "")
	var vault := id in ["habitat", "lab", "medical", "storage", "storm_shelter"]
	var glass := id == "greenhouse"
	if glass:
		var glass_mat := _mat("glass_roof", Color(0.42, 0.55, 0.62), 0.2, 0.3)
		glass_mat.emission_enabled = true
		glass_mat.emission = Color(0.30, 0.42, 0.48)
		glass_mat.emission_energy_multiplier = 0.5
		_box(group, Vector3(w + 0.4, 0.16, d + 0.4), Vector3(0, h + 0.08, 0), glass_mat)
		for i in range(6):
			var x := -w * 0.5 + (w / 6.0) * (i + 0.5)
			_box(group, Vector3(0.12, 0.5, d + 0.5), Vector3(x, h + 0.3, 0), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
	elif vault:
		var tube := MeshInstance3D.new()
		var mesh := CylinderMesh.new()
		mesh.top_radius = w * 0.52
		mesh.bottom_radius = w * 0.52
		mesh.height = d + 0.6
		mesh.radial_segments = 28
		mesh.rings = 1
		mesh.cap_top = false
		mesh.cap_bottom = false
		tube.mesh = mesh
		tube.material_override = wall_mat
		tube.rotation.x = PI * 0.5
		tube.position = Vector3(0, h - w * 0.10, 0)
		tube.scale = Vector3(1.0, 1.0, 0.55)
		group.add_child(tube)
	else:
		_box(group, Vector3(w + 0.6, 0.22, d + 0.6), Vector3(0, h + 0.1, 0),
			_mat("panel", Color(0.70, 0.74, 0.78), 0.7, 0.06))

	# A trim band at eaves height, and roof-mounted equipment.
	_box(group, Vector3(w + 0.3, 0.18, 0.14), Vector3(0, h - 0.35, d * 0.5), _mat("accent", Color(0.85, 0.52, 0.19), 0.55, 0.15))
	_box(group, Vector3(w + 0.3, 0.18, 0.14), Vector3(0, h - 0.35, -d * 0.5), _mat("accent", Color(0.85, 0.52, 0.19), 0.55, 0.15))
	_box(group, Vector3(1.1, 0.5, 1.1), Vector3(w * 0.22, h + 0.5, -d * 0.18), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))


func _make_trim(group: Node3D, w: float, d: float, height: float, trim_mat: Material) -> void:
	# Vertical pilasters on the corners: cheap, and they break up the box.
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(group, Vector3(0.26, height, 0.26),
				Vector3(sx * (w * 0.5 - 0.1), height * 0.5, sz * (d * 0.5 - 0.1)), trim_mat)


func _make_sign(group: Node3D, building: Dictionary, w: float, d: float, h: float,
		door: int) -> void:
	var text_en := str(building.get("nameEn", ""))
	var text_ar := str(building.get("nameAr", ""))
	var offset := Vector3.ZERO
	var yaw := 0.0
	match door:
		0:
			offset = Vector3(0, h - 0.75, d * 0.5 + 0.16)
			yaw = 0.0
		1:
			offset = Vector3(0, h - 0.75, -d * 0.5 - 0.16)
			yaw = PI
		2:
			offset = Vector3(w * 0.5 + 0.16, h - 0.75, 0)
			yaw = PI * 0.5
		_:
			offset = Vector3(-w * 0.5 - 0.16, h - 0.75, 0)
			yaw = -PI * 0.5
	var label := _label_both(group, text_en, text_ar, offset, yaw, 48, 0.0075)
	label.name = "Sign_%s" % building.get("id", "")
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


func _make_interactions(group: Node3D, building: Dictionary, w: float, d: float, h: float,
		door: int) -> void:
	var interactions: Array = building.get("interactions", [])
	var count := interactions.size()
	for i in range(count):
		var point: Dictionary = interactions[i]
		# Consoles line the wall opposite the door, spaced across the room.
		var room := w if door < 2 else d
		var limit: float = maxf(0.0, room * 0.5 - 1.8)
		var spread: float = clampf((float(i) - (count - 1) * 0.5) * 2.6, -limit, limit)
		var local := Vector3.ZERO
		var yaw := 0.0
		match door:
			0:
				local = Vector3(spread, 0, -d * 0.5 + 1.2)
				yaw = 0.0
			1:
				local = Vector3(spread, 0, d * 0.5 - 1.2)
				yaw = PI
			2:
				local = Vector3(-w * 0.5 + 1.2, 0, spread)
				yaw = PI * 0.5
			_:
				local = Vector3(w * 0.5 - 1.2, 0, spread)
				yaw = -PI * 0.5

		var desk := Node3D.new()
		desk.position = local
		desk.rotation.y = yaw
		group.add_child(desk)

		_box(desk, Vector3(1.15, 1.0, 0.62), Vector3(0, 0.6, 0), _mat("panel", Color(0.70, 0.74, 0.78), 0.7, 0.06))
		_box(desk, Vector3(0.9, 0.5, 0.06), Vector3(0, 1.16, -0.26), _glow("screen", Color(0.06, 0.12, 0.16), Color(0.32, 0.78, 0.95), 1.2))
		var en_text := str(point.get("nameEn", ""))
		var ar_text := str(point.get("nameAr", ""))
		var label := _label_both(desk, en_text, ar_text, Vector3(0, 1.62, 0), 0.0, 36, 0.004)
		label.name = "ConsoleLabel"

		var body := StaticBody3D.new()
		body.position = Vector3(0, 0.875, 0)
		var shape := CollisionShape3D.new()
		var box := BoxShape3D.new()
		# Full height: the player looks from 1.66 m, so the panel has to reach the eye line or the
		# crosshair sails over it into the wall.
		box.size = Vector3(1.15, 1.75, 0.62)
		shape.shape = box
		body.add_child(shape)
		body.set_meta("interaction_id", str(point.get("id", "")))
		body.set_meta("building_id", str(building.get("id", "")))
		body.set_meta("label_en", str(point.get("nameEn", "")))
		body.set_meta("label_ar", str(point.get("nameAr", "")))
		desk.add_child(body)
		interactables.append({
			"node": body,
			"interaction_id": str(point.get("id", "")),
			"building_id": str(building.get("id", "")),
			"label_en": str(point.get("nameEn", "")),
			"label_ar": str(point.get("nameAr", "")),
		})


func _make_interior_light(group: Node3D, w: float, d: float, h: float) -> void:
	var panel := _box(group, Vector3(minf(w * 0.5, 3.4), 0.08, 0.7), Vector3(0, h - 0.2, 0),
		_glow("lamp", Color(1, 0.96, 0.88), Color(1.0, 0.94, 0.82), 2.0))
	panel.name = "CeilingLight"


# ---- the traverse limit ----------------------------------------------------

## The colony is graded flat inside 110 m; beyond that the regolith rolls. This ring is the survey
## line the crew are cleared to, and it keeps the player on ground the collision actually models.
func _make_perimeter() -> void:
	var group := Node3D.new()
	group.name = "TraverseLimit"
	add_child(group)
	var radius := 118.0
	var segments := 28
	for i in range(segments):
		var angle := TAU * float(i) / float(segments)
		var x := cos(angle) * radius
		var z := sin(angle) * radius
		var gy := height_at(x, z)
		var post := _cylinder(group, 0.12, 1.3, Vector3(x, gy + 0.65, z),
			_mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
		post.name = "Marker_%d" % i
		var cap := _box(group, Vector3(0.26, 0.26, 0.26), Vector3(x, gy + 1.36, z),
			_glow("lamp", Color(1, 0.96, 0.88), Color(1.0, 0.93, 0.78), 1.8))
		cap.name = "MarkerLight_%d" % i
		# One flat barrier per segment: cheap, and it follows the circle well enough at 26 m.
		var width := 2.0 * radius * sin(PI / float(segments)) + 1.0
		var blocker := _blocker(group, Vector3(width, 2.4, 0.6), Vector3(x, gy + 1.2, z))
		blocker.rotation.y = -angle + PI * 0.5


# ---- set dressing ----------------------------------------------------------

func _make_rocks() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 20261114
	var rocks := Node3D.new()
	rocks.name = "Rocks"
	add_child(rocks)
	for i in range(34):
		var angle := rng.randf() * TAU
		var radius := rng.randf_range(30.0, 135.0)
		var x := cos(angle) * radius
		var z := sin(angle) * radius
		var scale := rng.randf_range(0.4, 2.6)
		var rock := _scanned_rock(i)
		if rock != null:
			rock.position = Vector3(x, height_at(x, z) - scale * 0.12, z)
			rock.rotation = Vector3(rng.randf() * 0.3, rng.randf() * TAU, rng.randf() * 0.3)
			rock.scale = Vector3(scale, scale * rng.randf_range(0.7, 1.0), scale * rng.randf_range(0.8, 1.2))
			rocks.add_child(rock)
		else:
			var mesh := MeshInstance3D.new()
			var sphere := SphereMesh.new()
			sphere.radius = scale
			sphere.height = scale * rng.randf_range(1.1, 1.8)
			sphere.radial_segments = 9
			sphere.rings = 6
			mesh.mesh = sphere
			mesh.material_override = _mat("rock", Color(0.34, 0.32, 0.30), 1.0, 0.0)
			mesh.position = Vector3(x, height_at(x, z) + scale * 0.32, z)
			mesh.rotation = Vector3(rng.randf() * 0.6, rng.randf() * TAU, rng.randf() * 0.6)
			mesh.scale = Vector3(1.0, rng.randf_range(0.6, 1.0), rng.randf_range(0.7, 1.2))
			rocks.add_child(mesh)


## One of the seven Poly Haven photoscanned Moon rocks (CC0), glTF at 1k. Returns null when the
## scans have not been synced, and the caller falls back to a rough boulder sphere.
func _scanned_rock(index: int) -> Node3D:
	var id := "moon_rock_%02d" % (1 + (index % 7))
	var path := "res://assets/real/rocks/%s/%s.gltf" % [id, id]
	if not ResourceLoader.exists(path):
		return null
	var scene := load(path) as PackedScene
	if scene == null:
		return null
	var node := scene.instantiate() as Node3D
	for child in node.find_children("*", "MeshInstance3D", true, false):
		var mi := child as MeshInstance3D
		if mi != null:
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	return node


func _make_rovers() -> void:
	var garage: Dictionary = Content.building("garage")
	var base: Array = garage.get("position", [-16, -40])
	var kit := _kit_module("open-rover-unpressuris-0da93181")
	for i in range(3):
		var px := float(base[0]) + 9.0 + i * 3.4
		var pz := float(base[1]) + 12.0
		var rover: Node3D
		if kit != null:
			rover = kit.duplicate() as Node3D
		else:
			rover = _box_rover()
		rover.position = Vector3(px, 0.0, pz)
		rover.rotation.y = 0.4 + i * 0.3
		add_child(rover)


## The procedural fallback rover, kept so a fresh clone without the kit still shows a vehicle.
func _box_rover() -> Node3D:
	var rover := Node3D.new()
	_box(rover, Vector3(3.0, 0.6, 1.7), Vector3(0, 0.9, 0), _mat("hull", Color(0.83, 0.86, 0.89), 0.62, 0.08))
	_box(rover, Vector3(1.3, 0.5, 1.4), Vector3(-0.4, 1.35, 0), _mat("panel", Color(0.70, 0.74, 0.78), 0.7, 0.06))
	_box(rover, Vector3(0.9, 0.06, 1.45), Vector3(-0.4, 1.62, 0), _mat("solar", Color(0.07, 0.11, 0.22), 0.25, 0.7))
	for sx in [-1.0, 0.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_cylinder(rover, 0.42, 0.34, Vector3(sx * 1.05, 0.42, sz * 0.85), _mat("rubber", Color(0.10, 0.10, 0.11), 0.95, 0.0)).rotation.x = PI * 0.5
	_cylinder(rover, 0.06, 1.4, Vector3(1.1, 1.9, 0.4), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
	_box(rover, Vector3(0.3, 0.22, 0.3), Vector3(1.1, 2.6, 0.4), _mat("dark", Color(0.16, 0.18, 0.21), 0.75, 0.15))
	_box(rover, Vector3(0.18, 0.18, 0.18), Vector3(1.1, 2.74, 0.4), _glow("lamp", Color(1, 0.96, 0.88), Color(1.0, 0.93, 0.78), 1.4))
	return rover


## Loads one CC0 "Moon Base Colony and Lunar Rovers" module, or null when it has not been synced.
func _kit_module(stem: String) -> Node3D:
	var path := "res://assets/models/lunar_colony/%s.glb" % stem
	if not ResourceLoader.exists(path):
		return null
	var scene := load(path) as PackedScene
	if scene == null:
		return null
	return scene.instantiate() as Node3D


## The industrial hardware around the walkable colony: landers, haulers, the power yard, ISRU
## skids, the motor pool and the relay mast. Same CC0 kit and same layout as the browser build,
## placed as visual dressing (no colliders) so the scene audit's spawn / console / crew queries
## are untouched. Skipped entirely when the kit has not been synced in.
func _make_kit_dressing() -> void:
	var layout := [
		["cargo-lander-94227716", 0.0, 52.0, 0.0],
		["approach-light-beacon-20fb9bc6", -11.0, 44.0, 0.0],
		["approach-light-beacon-20fb9bc6", 9.0, 46.0, 0.0],
		["approach-light-beacon-20fb9bc6", -11.0, 62.0, 0.0],
		["approach-light-beacon-20fb9bc6", 11.0, 62.0, 0.0],
		["blast-deflector-wall-51ed7365", -22.0, 52.0, PI * 0.5],
		["blast-deflector-wall-51ed7365", 22.0, 52.0, -PI * 0.5],
		["cargo-hauler-flatbed-76ce4ade", -14.0, 36.0, 0.35],
		["equipment-sled-408c66bc", -10.0, 42.0, 0.5],
		["propellant-tank-farm-3dcbfa21", -30.0, 50.0, PI * 0.5],
		["cryo-transfer-arm-5d350240", -24.0, 40.0, 0.0],
		["cargo-lander-94227716", 26.0, 62.0, 1.1],
		["battery-container-9755ed67", -66.0, -14.0, PI * 0.5],
		["battery-container-9755ed67", -66.0, -20.0, PI * 0.5],
		["radiator-bank-0c1ce93e", -52.0, 16.0, 0.0],
		["nuclear-power-unit-8239903e", -72.0, -32.0, 0.0],
		["vertical-solar-tower-c58cca7d", -68.0, 10.0, 0.0],
		["solar-array-row-tracke-e35a3b96", -84.0, -2.0, 0.5],
		["oxygen-extraction-plan-002fb0ae", -46.0, 19.0, 0.0],
		["regolith-excavator-64b4560f", -56.0, 22.0, 0.6],
		["sintering-print-gantry-64cc192a", -40.0, 30.0, 0.0],
		["water-processing-skid-27dcfe03", 42.0, 26.0, 0.0],
		["greenhouse-module-b35be615", 46.0, -14.0, PI * 0.5],
		["survey-marker-array-99d264a2", 58.0, -44.0, 0.0],
		["regolith-hopper-screen-148a2c37", 63.0, -24.0, -0.5],
		["crane-rover-d5a76830", -34.0, -30.0, 0.45],
		["pressurised-long-range-35466e96", -28.0, -46.0, 0.55],
		["small-survey-rover-f1fa6584", -24.0, -52.0, 0.0],
		["eva-tool-cart-3f077d9d", -30.0, -38.0, 1.0],
		["comms-relay-mast-4cdfe54f", 26.0, -30.0, 0.0],
		["dust-berm-2dec8cfd", 62.0, 12.0, PI * 0.5],
		["dust-berm-2dec8cfd", -62.0, 30.0, -PI * 0.5],
		["blast-berm-segment-6e0ba7b3", 34.0, 44.0, 0.0],
		["crater-rim-segment-fafd0277", -84.0, 44.0, 0.8],
	]
	var dressing := Node3D.new()
	dressing.name = "KitDressing"
	add_child(dressing)
	var placed := 0
	for item in layout:
		var node := _kit_module(str(item[0]))
		if node == null:
			continue
		var x := float(item[1])
		var z := float(item[2])
		node.position = Vector3(x, height_at(x, z), z)
		node.rotation.y = float(item[3])
		dressing.add_child(node)
		placed += 1
	if placed > 0:
		print("Luna-Quest (GDScript): %d kit modules dressed the colony" % placed)


func _make_landing_pad() -> void:
	var pad: Dictionary = Content.building("landing_pad")
	var pos: Array = pad.get("position", [0, 52])
	var group := Node3D.new()
	group.name = "LandingPad"
	group.position = Vector3(float(pos[0]), 0.0, float(pos[1]))
	add_child(group)

	_box(group, Vector3(22, 0.24, 22), Vector3(0, 0.12, 0), _mat("paving", Color(0.46, 0.46, 0.45), 0.9))
	var ring := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius = 6.4
	torus.outer_radius = 6.9
	torus.rings = 32
	torus.ring_segments = 8
	ring.mesh = torus
	ring.material_override = _glow("accent_line", Color(0.85, 0.52, 0.19), Color(0.85, 0.52, 0.19), 0.8)
	ring.position = Vector3(0, 0.26, 0)
	group.add_child(ring)

	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_cylinder(group, 0.18, 1.4, Vector3(sx * 10.2, 0.7, sz * 10.2), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
			_box(group, Vector3(0.3, 0.3, 0.3), Vector3(sx * 10.2, 1.5, sz * 10.2),
				_glow("lamp", Color(1, 0.96, 0.88), Color(1.0, 0.93, 0.78), 2.4))

	# F6: the supply crate. A physical, hold-E-interactable delivery trunk.
	var crate := StaticBody3D.new()
	crate.name = "SupplyCrate"
	crate.set_meta("interaction_id", "cargo_crate")
	crate.set_meta("building_id", "")
	crate.set_meta("label_en", "Supply Crate")
	crate.set_meta("label_ar", "صندوق الإمداد")
	crate.position = Vector3(6.8, 0, 3.0)
	crate.collision_layer = 1
	group.add_child(crate)
	var crate_mesh := MeshInstance3D.new()
	var crate_box := BoxMesh.new()
	crate_box.size = Vector3(1.5, 1.25, 1.15)
	crate_mesh.mesh = crate_box
	crate_mesh.material_override = _mat("crate", Color(0.42, 0.35, 0.27), 0.75, 0.05)
	crate_mesh.position = Vector3(0, 0.62, 0)
	crate.add_child(crate_mesh)
	var stripe := _box(crate, Vector3(1.52, 0.18, 1.17), Vector3(0, 0.42, 0),
		_glow("hazard_strip", Color(0.84, 0.68, 0.14), Color(0.9, 0.74, 0.2), 0.35))
	var crate_shape := CollisionShape3D.new()
	var crate_body := BoxShape3D.new()
	crate_body.size = Vector3(1.5, 1.25, 1.15)
	crate_shape.shape = crate_body
	crate_shape.position = Vector3(0, 0.62, 0)
	crate.add_child(crate_shape)
	_label(crate, "SUPPLY", Vector3(0, 1.42, 0), 0, 30, 0.005, Color(0.92, 0.66, 0.22))


func _make_power_farm() -> void:
	var farm: Dictionary = Content.building("power_farm")
	var pos: Array = farm.get("position", [-62, -8])
	var group := Node3D.new()
	group.name = "SolarFarm"
	group.position = Vector3(float(pos[0]), 0.0, float(pos[1]))
	add_child(group)
	for row in range(3):
		for col in range(4):
			var x := -9.0 + col * 6.0
			var z := -10.0 + row * 7.0
			var panel := _box(group, Vector3(5.2, 0.12, 3.4), Vector3(x, 1.5, z),
				_mat("solar", Color(0.07, 0.11, 0.22), 0.25, 0.7))
			panel.rotation.x = deg_to_rad(-62.0)
			_cylinder(group, 0.1, 1.5, Vector3(x, 0.75, z), _mat("alloy", Color(0.55, 0.58, 0.62), 0.4, 0.55))
