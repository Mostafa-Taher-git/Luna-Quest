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

var camera: Camera3D
var ray: RayCast3D
var astronaut: Node3D
var third_person := false
var can_move := true
var bob_time := 0.0
var _pitch := 0.0
var _head: Node3D
var _current_target: Dictionary = {}


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
	ray.collide_with_areas = false
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
	camera.position = Vector3(0, 0, 2.9) if on else Vector3.ZERO


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED and can_move:
		rotate_y(-event.relative.x * MOUSE_SENSITIVITY)
		_pitch = clampf(_pitch - event.relative.y * MOUSE_SENSITIVITY, -1.2, 1.0)
		_head.rotation.x = _pitch


func _physics_process(delta: float) -> void:
	if not can_move:
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
	var direction := (transform.basis * Vector3(input_dir.x, 0.0, input_dir.y)).normalized()
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
	if collider.has_meta("npc_id"):
		return {
			"type": "npc",
			"body": collider,
			"npc_id": str(collider.get_meta("npc_id")),
			"label_en": str(collider.get_meta("npc_name_en", "")),
			"label_ar": str(collider.get_meta("npc_name_ar", "")),
		}
	return {}
