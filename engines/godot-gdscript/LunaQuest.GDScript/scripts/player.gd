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

	move_and_slide()

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
