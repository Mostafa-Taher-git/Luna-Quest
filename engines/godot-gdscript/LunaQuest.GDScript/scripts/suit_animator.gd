class_name SuitAnimator
extends Node
## The GDScript edition's crew/player animation driver. The imported Sketchfab GLB ships a
## single long action: 0.75s-1.75s breathing idle, 2.50s-5.00s one complete walk cycle.
## These sections are copied into looping clips, and the suit walks in place while the
## root node moves through the world. Mirrors AstronautAnimator.cs — do not change the
## section timings without updating the C# edition.
##
## skills: godot/godot-animation (AnimationPlayer section playback + loop modes)

const IDLE_START := 0.75
const IDLE_END := 1.75
const WALK_START := 2.50
const WALK_END := 5.00
const IDLE_CLIP := "LunaQuestIdle"
const WALK_CLIP := "LunaQuestWalk"

var _player: AnimationPlayer
var has_rig := false
var _walking := false


func setup(player: AnimationPlayer) -> void:
	_player = player
	var names := player.get_animation_list()
	if names.is_empty():
		return
	var source := player.get_animation(names[0])
	if source == null:
		return
	var library: AnimationLibrary = player.get_animation_library("")
	if library == null:
		library = AnimationLibrary.new()
		player.add_animation_library("", library)
	_add_looping(library, source, IDLE_CLIP)
	_add_looping(library, source, WALK_CLIP)
	has_rig = true
	player.play_section(IDLE_CLIP, IDLE_START, IDLE_END)


## Safe to call every frame; switches between clips only when the locomotion state changes.
func set_locomotion(moving: bool, speed: float) -> void:
	if not has_rig or _player == null:
		return
	if moving:
		if not _walking:
			_player.play_section(WALK_CLIP, WALK_START, WALK_END, 0.18)
			_walking = true
		var target := clampf(0.9 + speed * 0.16, 1.15, 2.4)
		if not is_equal_approx(_player.speed_scale, target):
			_player.speed_scale = target
	elif _walking:
		_player.play_section(IDLE_CLIP, IDLE_START, IDLE_END, 0.25)
		_player.speed_scale = 1.0
		_walking = false


func _add_looping(library: AnimationLibrary, source: Animation, name: String) -> void:
	if library.has_animation(name):
		library.remove_animation(name)
	var copy := source.duplicate()
	copy.loop_mode = Animation.LOOP_LINEAR
	library.add_animation(name, copy)
