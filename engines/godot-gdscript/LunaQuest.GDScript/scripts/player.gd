class_name ColonyPlayer
extends CharacterBody3D
## The commander: a first/third-person walker with lunar gravity (1.62 m/s²), a soft step, and a
## short interaction ray. Keyboard-only fallbacks (`W`/`Esc`) keep the script honest even if the
## input map in project.godot is ever replaced.

const WALK_SPEED := 3.2
const RUN_SPEED := 5.6
const JUMP_VELOCITY := 3.4
const GRAVITY := 1.62
const MOUSE_SENSITIVITY := 0.0022
const EYE_HEIGHT := 1.66
const THIRD_PERSON_OFFSET := Vector3(0.42, 0.18, 2.9)

var camera: Camera3D
var ray: RayCast3D
var astronaut: Node3D
var third_person := false
var can_move := true
var mouse_sensitivity := MOUSE_SENSITIVITY
var bob_time := 0.0
var _pitch := 0.0
var _arm_position := Vector3.ZERO
var _head: Node3D
var _foot_puff: CPUParticles3D = null
var _suit_lamp: SpotLight3D = null
var _thud: AudioStreamPlayer3D = null
var _step_timer := 0.0
var _was_airborne := false
var _fall_vel := 0.0
var _current_target: Dictionary = {}
var rover: ColonyRover = null # set while driving the crew rover

## The F0 contract: W moves toward the way the camera looks. Kept as a static helper so the
## input-to-direction mapping can be locked by regression tests without a live keyboard.
static func move_direction(input_dir: Vector2, yaw: float) -> Vector3:
	return (Basis(Vector3.UP, yaw) * Vector3(input_dir.x, 0.0, input_dir.y)).normalized()


func _ready() -> void:
	_head = Node3D.new()
	_head.position = Vector3(0, EYE_HEIGHT, 0)
	add_child(_head)

	camera = Camera3D.new()
	camera.fov = 72.0
	camera.near = 0.05
	camera.far = 2200.0
	_head.add_child(camera)

	ray = RayCast3D.new()
	ray.target_position = Vector3(0, 0, -3.4)
	ray.collide_with_areas = true
	ray.collide_with_bodies = true
	ray.enabled = true
	camera.add_child(ray)

	var shape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.34
	capsule.height = 1.75
	shape.shape = capsule
	shape.position = Vector3(0, 0.9, 0)
	add_child(shape)

	# F8: footsteps kick a whisper of dust, helmet lamp in first person, a thud on landing.
	_foot_puff = CPUParticles3D.new()
	_foot_puff.name = "FootDust"
	_foot_puff.position = Vector3(0, 0.07, 0)
	_foot_puff.one_shot = true
	_foot_puff.amount = 6
	_foot_puff.lifetime = 1.0
	_foot_puff.explosiveness = 0.8
	_foot_puff.direction = Vector3(0, 0.75, 0)
	_foot_puff.spread = 75.0
	_foot_puff.gravity = Vector3(0, -0.65, 0)
	_foot_puff.initial_velocity_min = 0.25
	_foot_puff.initial_velocity_max = 1.0
	_foot_puff.scale_amount_min = 0.07
	_foot_puff.scale_amount_max = 0.24
	_foot_puff.emitting = false
	var dust_mat := StandardMaterial3D.new()
	dust_mat.albedo_color = Color(0.60, 0.56, 0.50, 0.5)
	dust_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	dust_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_foot_puff.material_override = dust_mat
	add_child(_foot_puff)

	_suit_lamp = SpotLight3D.new()
	_suit_lamp.name = "SuitLamp"
	_suit_lamp.position = Vector3(0, 0.05, 0)
	_suit_lamp.light_color = Color(0.86, 0.93, 1.0)
	_suit_lamp.light_energy = 1.5
	_suit_lamp.spot_range = 16.0
	_suit_lamp.spot_angle = 34.0
	_suit_lamp.spot_attenuation = 0.6
	_suit_lamp.visible = not third_person
	camera.add_child(_suit_lamp)

	_thud = AudioStreamPlayer3D.new()
	_thud.name = "LandThud"
	var sample_rate := 16000
	var frames := int(sample_rate * 0.35)
	var tdata := PackedByteArray()
	for i in range(frames):
		var t := float(i) / float(sample_rate)
		var v := sin(t * TAU * 48.0) * exp(-t * 14.0) * 0.9
		tdata.append(int(v * 12000.0) & 0xFF)
		tdata.append((int(v * 12000.0) >> 8) & 0xFF)
	var thud_wav := AudioStreamWAV.new()
	thud_wav.format = AudioStreamWAV.FORMAT_16_BITS
	thud_wav.mix_rate = sample_rate
	thud_wav.stereo = false
	_thud.stream = thud_wav
	_thud.max_distance = 18.0
	add_child(_thud)

	floor_max_angle = deg_to_rad(50.0)
	floor_snap_length = 0.3


func attach_astronaut(model: Node3D) -> void:
	astronaut = model
	astronaut.visible = third_person
	add_child(astronaut)


func set_third_person(on: bool) -> void:
	third_person = on
	if astronaut != null:
		astronaut.visible = on
	if _suit_lamp != null:
		_suit_lamp.visible = not on

## Settings: the saved mouse sensitivity multiplies the default turn rate and the saved
## field-of-view overrides the default lens.
func apply_camera_settings(sensitivity: float, fov: float) -> void:
	mouse_sensitivity = MOUSE_SENSITIVITY * clampf(sensitivity, 0.1, 4.0)
	camera.fov = clampf(fov, 30.0, 110.0)

## The over-the-shoulder boom: the camera pulls back to THIRD_PERSON_OFFSET, but a ray from the
## eyes bounces it forward if a wall or rock is in the way (SpringArm3D-style). The commander
## is never clipped inside geometry, and the suit never swallows the camera.
func update_camera_arm(delta: float) -> void:
	if not third_person:
		camera.position = Vector3.ZERO
		return
	var from := _head.global_position
	var desired_world := _head.global_transform * THIRD_PERSON_OFFSET
	var space := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(from, desired_world)
	query.collide_with_areas = false
	query.collide_with_bodies = true
	query.exclude = [get_rid()]
	var hit := space.intersect_ray(query)
	var desired := THIRD_PERSON_OFFSET
	if not hit.is_empty():
		var hit_dist := from.distance_to(hit.position)
		var full := THIRD_PERSON_OFFSET.length()
		var scale_len := clampf((hit_dist - 0.15) / full, 0.3, 1.0)
		desired = THIRD_PERSON_OFFSET * scale_len
	camera.position = camera.position.lerp(desired, minf(delta * 10.0, 1.0))


func _unhandled_input(event: InputEvent) -> void:
	# Captured pointer on desktop, click-and-drag in a browser tab where pointer lock was refused.
	var locked := Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	var dragging: bool = event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_LEFT) != 0
	if event is InputEventMouseMotion and (locked or dragging) and can_move:
		rotate_y(-event.relative.x * mouse_sensitivity)
		_pitch = clampf(_pitch - event.relative.y * mouse_sensitivity, -1.2, 1.0)
		_head.rotation.x = _pitch


func _physics_process(delta: float) -> void:
	update_camera_arm(delta)
	if not can_move:
		return

	# At the wheel: the rover owns movement and carries the commander this frame.
	if rover != null:
		rover.drive(delta, self)
		return

	if not is_on_floor():
		velocity.y -= GRAVITY * delta

	var input_dir := Vector2.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		input_dir.y -= 1.0
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		input_dir.y += 1.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		input_dir.x -= 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		input_dir.x += 1.0
	input_dir = input_dir.normalized()

	var running := Input.is_key_pressed(KEY_SHIFT)
	var speed := RUN_SPEED if running else WALK_SPEED
	var direction := ColonyPlayer.move_direction(input_dir, rotation.y)
	if direction.length() > 0.01:
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
		bob_time += delta * (9.0 if running else 6.0)
	else:
		velocity.x = move_toward(velocity.x, 0.0, 12.0 * delta)
		velocity.z = move_toward(velocity.z, 0.0, 12.0 * delta)

	# A little lunar hop: 3.4 m/s on the Moon clears about a metre and hangs for a second.
	if is_on_floor() and Input.is_key_pressed(KEY_SPACE) and velocity.y < 0.1:
		velocity.y = JUMP_VELOCITY

	# F8: above 30 degrees of slope the suit starts to slide, doesn't stop dead.
	if is_on_floor():
		var slope := rad_to_deg(get_floor_angle())
		if slope > 30.0:
			var n := get_floor_normal()
			var downhill := -Vector3(n.x, 0.0, n.z).normalized()
			var excess := clampf((slope - 30.0) / 60.0, 0.0, 0.6)
			velocity.x += downhill.x * excess * 6.5
			velocity.z += downhill.z * excess * 6.5

	var horizontal_speed := Vector2(velocity.x, velocity.z).length()
	if is_on_floor() and horizontal_speed > 1.0:
		_step_timer += delta * (9.0 if running else 5.5)
		if _step_timer >= 1.0:
			_step_timer = 0.0
			if _foot_puff != null:
				_foot_puff.restart()

	move_and_slide()

	# Landing thump: a fall that ends harder than a step off a crate.
	if is_on_floor():
		if _was_airborne and _fall_vel < -2.4:
			if _thud != null:
				_thud.play()
			if _foot_puff != null:
				_foot_puff.restart()
		_was_airborne = false
	else:
		_was_airborne = true
		_fall_vel = velocity.y

	if astronaut != null and third_person:
		astronaut.position = Vector3(0, 0, 0)
		astronaut.rotation.y = 0.0
		astronaut.visible = true


## What the crosshair is on, if it is something the player can use.
func target() -> Dictionary:
	if ray == null:
		return {}
	ray.force_raycast_update()
	if not ray.is_colliding():
		return {}
	var collider := ray.get_collider()
	if collider == null:
		return {}
	if collider.has_meta("interaction_id"):
		return {
			"type": "console",
			"body": collider,
			"building_id": str(collider.get_meta("building_id")),
			"interaction_id": str(collider.get_meta("interaction_id")),
			"label_en": str(collider.get_meta("label_en", "")),
			"label_ar": str(collider.get_meta("label_ar", "")),
		}
	if collider.has_meta("rover_id"):
		return {
			"type": "rover",
			"body": collider,
			"label_en": "Crew rover",
			"label_ar": "مركبة الطاقم",
		}
	if collider.has_meta("npc_id"):
		return {
			"type": "npc",
			"body": collider,
			"npc_id": str(collider.get_meta("npc_id")),
			"label_en": str(collider.get_meta("npc_name_en", "")),
			"label_ar": str(collider.get_meta("npc_name_ar", "")),
		}
	return {}
