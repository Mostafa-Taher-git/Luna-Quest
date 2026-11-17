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
var _seat := Vector3(0.0, 0.34, 0.18)
var _hum: AudioStreamPlayer3D = null
var _dust_left: CPUParticles3D = null
var _dust_right: CPUParticles3D = null
## Last place the rover's own body sat, used by exit(): transform changes only reach the physics
## server on the next physics tick, and that tick fires before the frame in which the exit is
## handled. See exit().
var _settled_position := Vector3(-11.0, 0.05, -28.0)
var _settled_rotation := 0.4


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
	_build_hum()
	_build_dust()
	floor_max_angle = deg_to_rad(50.0)
	floor_snap_length = 0.4


## F7: a two-stroke hum generated at runtime — nothing is downloaded. The pitch scales with
## speed, so the drive communicates through the ear before the eye.
func _build_hum() -> void:
	_hum = AudioStreamPlayer3D.new()
	_hum.name = "EngineHum"
	_hum.max_distance = 26.0
	_hum.attenuation_model = AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
	_hum.unit_size = 4.0
	var sample_rate := 22050
	var frames := sample_rate        # 1 s loop, seamless sawtooth
	var data := PackedByteArray()
	var period := sample_rate / 55.0  # 55 Hz fundamental, a deep two-stroke idle
	for i in range(frames):
		var t := float(i)
		var phase := fmod(t, period) / period
		var saw := phase * 2.0 - 1.0
		var sub := sin(t * TAU * 27.5 / float(sample_rate))
		var v := clampf(saw * 0.55 + sub * 0.35, -1.0, 1.0) * 9000.0
		data.append(int(v) & 0xFF)
		data.append((int(v) >> 8) & 0xFF)
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = sample_rate
	wav.stereo = false
	wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
	wav.data = data
	_hum.stream = wav
	_hum.volume_db = -9.0
	add_child(_hum)


## F7: regolith puffs kicked from the rear wheels while moving. Real behaviour, approximate look.
func _build_dust() -> void:
	for side in [-1.0, 1.0]:
		var p := CPUParticles3D.new()
		p.name = "DustLeft" if side < 0.0 else "DustRight"
		p.position = Vector3(side * 0.96, 0.2, 1.15)
		p.emitting = false
		p.amount = 18
		p.lifetime = 1.5
		p.preprocess = 0.0
		p.one_shot = false
		p.explosiveness = 0.0
		p.randomness = 0.6
		p.direction = Vector3(0, 0.55, 1.0)
		p.spread = 24.0
		p.gravity = Vector3(0, -1.1, 0)
		p.initial_velocity_min = 1.2
		p.initial_velocity_max = 3.2
		p.scale_amount_min = 0.12
		p.scale_amount_max = 0.4
		var mat := StandardMaterial3D.new()
		mat.albedo_color = Color(0.62, 0.58, 0.53)
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		mat.albedo_color.a = 0.55
		p.material_override = mat
		add_child(p)
		if side < 0.0:
			_dust_left = p
		else:
			_dust_right = p


func _load_model() -> Node3D:
	var path := "res://assets/models/lunar_colony/open-rover-unpressuris-0da93181.glb"
	if ResourceLoader.exists(path):
		var scene := load(path) as PackedScene
		if scene != null:
			var instance := scene.instantiate() as Node3D
			# The kit GLB's nose is +Z (front wheels at z = +1.16); the rover's forward
			# is -Z (Godot camera / F0 contract), so the visual nose is swung 180 degrees.
			instance.rotation.y = PI
			return instance
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
	for corner: String in ["front-left", "front-right", "rear-left", "rear-right"]:
		# Typed explicitly: `corner` comes out of an untyped Array, and parse-time inference
		# of `var front := corner.begins_with(...)` fails without it.
		var front: bool = corner.begins_with("front")
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
	# The commander rides inside the rover's own box collider, so the suit capsule must stop
	# colliding while it is carried at the hand controller — otherwise move_and_slide pushes
	# the two bodies apart every physics frame (jitter, and the rover shoves itself backwards).
	player.collision_layer = 0
	player.collision_mask = 0


func exit(player: ColonyPlayer) -> void:
	occupied = false
	speed = 0.0
	velocity = Vector3.ZERO
	player.rover = null
	# Walking again: the suit is solid once more.
	player.collision_layer = 1
	player.collision_mask = 1
	var side := global_transform.basis.x.normalized()
	# Read the pose from the last physics frame, not from the node: a rover that moved this frame
	# has already been ticked by the physics server at its old transform, so global_position here
	# can be a frame ahead of where its collider actually is — and the commander would be dropped
	# straight into the rover's own box.
	var origin := Vector3(_settled_position.x, global_position.y, _settled_position.z)
	var basis := Basis(Vector3.UP, _settled_rotation)
	side = basis.x.normalized()
	var spot := origin + side * 1.8
	spot.y = world.height_at(spot.x, spot.z) if world != null else 0.0
	player.global_position = spot
	player.velocity = Vector3.ZERO
	if player.astronaut != null:
		player.astronaut.rotation.x = 0.0


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
	_step(delta, throttle, steer_in, Input.is_key_pressed(KEY_SHIFT),
		Input.is_key_pressed(KEY_SPACE), player)


## One physics step with the controls passed in (keyboard here, the audit when it drives the
## rover itself). Kept separate so the vehicle can be tested without a keyboard.
func _step(delta: float, throttle: float, steer_in: float, boost: bool, handbrake: bool,
		driver: ColonyPlayer = null) -> void:
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

	var forward := -global_transform.basis.z.normalized()
	velocity = forward * speed
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0
	move_and_slide()
	_settled_position = global_position
	_settled_rotation = rotation.y
	odometer += absf(speed) * delta

	spin -= (speed / WHEEL_RADIUS) * delta
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

	# F7: the ear leads the eye — pitch the hum with speed; kick dust when carrying speed.
	if _hum != null:
		var ratio: float = clampf(absf(speed) / MAX_FORWARD, 0.0, 1.0)
		_hum.pitch_scale = 0.55 + ratio * 1.55
		_hum.volume_db = -16.0 + ratio * 8.0
		var driving := absf(speed) > 0.4
		# The 3D positional hum is audible inside and outside the suit.
		if driving == (_hum.playing == false):
			if driving:
				_hum.play()
			else:
				_hum.stop()
	for p in [_dust_left, _dust_right]:
		if p != null:
			p.emitting = absf(speed) > 1.6 and is_on_floor()

	# F7: suspension tilt from the four contact patches — the body rolls and pitches with the terrain.
	if world != null and model != null:
		# right and forward in world space, measured from the chassis yaw
		var right := Vector3(cos(rotation.y), 0.0, sin(rotation.y))
		var fwd := Vector3(sin(rotation.y), 0.0, -cos(rotation.y))
		var h_fl := _wheel_ground(right, fwd, -0.96, 1.16)
		var h_fr := _wheel_ground(right, fwd, 0.96, 1.16)
		var h_rl := _wheel_ground(right, fwd, -0.96, -1.16)
		var h_rr := _wheel_ground(right, fwd, 0.96, -1.16)
		var front := (h_fl + h_fr) * 0.5
		var rear := (h_rl + h_rr) * 0.5
		var left := (h_fl + h_rl) * 0.5
		var rightside := (h_fr + h_rr) * 0.5
		var pitch := clampf(atan((rear - front) / 2.32), -0.38, 0.38)
		var roll := clampf(atan((rightside - left) / 1.92), -0.38, 0.38)
		model.rotation.x = lerp_angle(model.rotation.x, pitch, clampf(delta * 6.0, 0.0, 1.0))
		model.rotation.z = lerp_angle(model.rotation.z, roll, clampf(delta * 6.0, 0.0, 1.0))

	if driver == null:
		return
	# Carry the commander at the hand controller, facing the way the rover points.
	driver.global_position = seat_global()
	driver.velocity = Vector3.ZERO
	driver.rotation.y = rotation.y
	if driver.astronaut != null:
		driver.astronaut.rotation.x = -0.32


func _wheel_ground(right: Vector3, fwd: Vector3, right_off: float, fwd_off: float) -> float:
	var p := global_position + right * right_off + fwd * fwd_off
	return world.height_at(p.x, p.z)


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


## Keep the exit reference in step with a rover that is placed (spawn, save load, restart) rather
## than driven, so exit() never reads a stale pose.
func settle() -> void:
	_settled_position = global_position
	_settled_rotation = rotation.y


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

	# The saved pose is where the rover now is; exit() measures from here.
	settle()
