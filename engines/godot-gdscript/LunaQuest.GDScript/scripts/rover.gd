class_name ColonyRover
extends CharacterBody3D
## The rideable crew rover — the GDScript edition's real LTV.
##
## The mesh is the CC0 "Moon Base Colony and Lunar Rovers" open unpressurised rover, the same
## module the colony dresses its motor pool with, with cleanly separated wheel / steer sub-meshes
## so the wheels spin and the four corners steer. It is a CharacterBody3D, so move_and_slide does
## the collision work against the colony's static bodies under lunar gravity (1.62 m/s²).
##
## The driver is the one Cute Astronaut, carried at the hand controller by transform — the GLB is
## frozen and its meshes are not skinned, so the seat pose is a placement, never a reshaped suit.
## See docs/design/Luna-Quest_Immutable_Assets_EN.md.

const GRAVITY := 1.62
const MAX_FORWARD := 8.0
const MAX_REVERSE := 2.2
const ACCEL := 3.4
const BRAKE := 5.0
const DRAG := 0.55
const STEER_MAX := 0.52
const WHEEL_RADIUS := 0.44
const BATTERY_KWH := 14.0
const GARAGE := Vector2(-16.0, -40.0)
const ICE_SITE := Vector2(52.0, -34.0)

var world: ColonyWorld
var model: Node3D
var occupied := false
var has_driven := false
var reached_ice := false
var speed := 0.0
var steer := 0.0
var spin := 0.0
var battery_kwh := BATTERY_KWH
var odometer := 0.0
var _wheels: Array = []
var _steers: Array = []
var _seat := Vector3(0.0, 0.34, -0.18)


func setup(colony_world: ColonyWorld) -> void:
	world = colony_world
	set_meta("rover_id", "crew_rover")
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(2.3, 1.4, 3.0)
	shape.shape = box
	shape.position = Vector3(0, 0.7, 0)
	add_child(shape)
	model = _load_model()
	add_child(model)
	_rig_wheels()
	floor_max_angle = deg_to_rad(50.0)
	floor_snap_length = 0.4


func _load_model() -> Node3D:
	var path := "res://assets/models/lunar_colony/open-rover-unpressuris-0da93181.glb"
	if ResourceLoader.exists(path):
		var scene := load(path) as PackedScene
		if scene != null:
			return scene.instantiate() as Node3D
	# Fallback for a fresh clone without the kit: a simple box LTV, never a different character.
	var holder := Node3D.new()
	var body := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = Vector3(2.4, 0.6, 3.0)
	body.mesh = bm
	body.position = Vector3(0, 0.8, 0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.83, 0.86, 0.89)
	mat.roughness = 0.6
	body.material_override = mat
	holder.add_child(body)
	return holder


func _rig_wheels() -> void:
	if model == null:
		return
	for corner in ["front-left", "front-right", "rear-left", "rear-right"]:
		var front := corner.begins_with("front")
		for child in model.find_children("*", "Node3D", true, false):
			var n := child.name.to_lower()
			if n == "wheel-" + corner:
				_wheels.append([child, front])
			elif n == "steer-" + corner:
				_steers.append([child, front])


func seat_global() -> Vector3:
	return global_transform * _seat


func distance_to(p: Vector3) -> float:
	return Vector2(global_position.x - p.x, global_position.z - p.z).length()


func enter(player: ColonyPlayer) -> void:
	occupied = true
	has_driven = true
	player.rover = self


func exit(player: ColonyPlayer) -> void:
	occupied = false
	speed = 0.0
	velocity = Vector3.ZERO
	player.rover = null
	var side := global_transform.basis.x.normalized()
	var spot := global_position + side * 1.8
	spot.y = world.height_at(spot.x, spot.z) if world != null else 0.0
	player.global_position = spot
	player.velocity = Vector3.ZERO


## Called by ColonyPlayer._physics_process while the commander is at the wheel.
func drive(delta: float, player: ColonyPlayer) -> void:
	var throttle := 0.0
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		throttle += 1.0
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		throttle -= 1.0
	var steer_in := 0.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		steer_in += 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		steer_in -= 1.0
	var handbrake := Input.is_key_pressed(KEY_SPACE)
	var boost := Input.is_key_pressed(KEY_SHIFT)
	var flat := battery_kwh <= 0.05
	var max_f := 0.0 if flat else MAX_FORWARD * (1.0 if boost else 0.72)

	if throttle > 0.0 and not flat:
		speed += ACCEL * delta
	elif throttle < 0.0:
		speed -= (BRAKE if speed > 0.0 else ACCEL * 0.6) * delta
	else:
		speed = move_toward(speed, 0.0, DRAG * delta)
	if handbrake:
		speed = move_toward(speed, 0.0, BRAKE * 1.4 * delta)
	speed = clampf(speed, -MAX_REVERSE, max_f)

	var steer_target := steer_in * STEER_MAX * (0.55 + 0.45 / (1.0 + absf(speed) * 0.35))
	steer = lerpf(steer, steer_target, clampf(delta * 6.0, 0.0, 1.0))
	rotation.y += (speed / 2.3) * tan(steer) * delta

	var forward := global_transform.basis.z.normalized()
	velocity = forward * speed
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0
	move_and_slide()
	odometer += absf(speed) * delta

	spin += (speed / WHEEL_RADIUS) * delta
	for entry in _wheels:
		var node: Node3D = entry[0]
		var front: bool = entry[1]
		node.rotation.x = spin
		node.rotation.y = steer if front else -steer * 0.4
	for entry in _steers:
		var node: Node3D = entry[0]
		var front: bool = entry[1]
		node.rotation.y = steer if front else -steer * 0.4

	var pos2 := Vector2(global_position.x, global_position.z)
	if pos2.distance_to(GARAGE) < 12.0 and absf(speed) < 0.2:
		battery_kwh = minf(BATTERY_KWH, battery_kwh + 6.0 * delta / 60.0)
	else:
		battery_kwh = maxf(0.0, battery_kwh - absf(speed) * 0.9 * delta / 60.0)
	if not reached_ice and pos2.distance_to(ICE_SITE) < 12.0:
		reached_ice = true

	# Carry the commander at the hand controller, facing the way the rover points.
	player.global_position = seat_global()
	player.velocity = Vector3.ZERO
	player.rotation.y = rotation.y
	if player.astronaut != null:
		player.astronaut.rotation.x = -0.32


func speed_kmh() -> float:
	return absf(speed) * 3.6


func battery_percent() -> float:
	return battery_kwh / BATTERY_KWH


func capture() -> Dictionary:
	return {
		"position": [global_position.x, global_position.y, global_position.z],
		"yaw": rotation.y,
		"battery": battery_kwh,
		"odometer": odometer,
		"hasDriven": has_driven,
		"reachedIce": reached_ice,
		"occupied": occupied,
	}


func restore(data: Dictionary, player: ColonyPlayer) -> void:
	if data.is_empty():
		return
	var p: Array = data.get("position", [])
	if p.size() == 3:
		global_position = Vector3(float(p[0]), float(p[1]), float(p[2]))
	rotation.y = float(data.get("yaw", rotation.y))
	battery_kwh = clampf(float(data.get("battery", BATTERY_KWH)), 0.0, BATTERY_KWH)
	odometer = float(data.get("odometer", 0.0))
	has_driven = bool(data.get("hasDriven", false))
	reached_ice = bool(data.get("reachedIce", false))
	if bool(data.get("occupied", false)) and player != null:
		enter(player)
