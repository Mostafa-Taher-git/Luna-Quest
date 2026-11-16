class_name GameSettings
extends RefCounted

## Persisted game settings (the game's own user:// file, never the save file).
## Easy to inspect and easy to delete — nothing here is game content or a player save.

const PATH := "user://settings.json"

const DEFAULTS := {
	"third_person": false,
	"language": "en",
	"mouse_sensitivity": 1.0,
	"fov": 72.0,
	"master_volume": 0.8,
	"voice": true,
	"subtitles": true,
	"quality": "medium",
	"fullscreen": false,
}


static func defaults() -> Dictionary:
	return DEFAULTS.duplicate(true)


static func load() -> Dictionary:
	var data := defaults()
	if not FileAccess.file_exists(PATH):
		return data
	var f := FileAccess.open(PATH, FileAccess.READ)
	if f == null:
		return data
	var text := f.get_as_text()
	f.close()
	var parsed = JSON.parse_string(text)
	if parsed is Dictionary:
		for key in parsed.keys():
			data[key] = parsed[key]
	return data
	return data


static func save(data: Dictionary) -> Error:
	var merged := defaults()
	for key in data.keys():
		merged[key] = data[key]
	var tmp := PATH + ".tmp"
	var f := FileAccess.open(tmp, FileAccess.WRITE)
	if f == null:
		return FileAccess.get_open_error()
	f.store_string(JSON.stringify(merged, "\t"))
	f.close()
	if FileAccess.file_exists(PATH):
		DirAccess.rename_absolute(PATH, PATH + ".bak")
	return DirAccess.rename_absolute(tmp, PATH)


## Apply the persisted settings to engine globals and the active player.
## This is the launch-time path; the settings panel calls it on every change.
static func apply_to(data: Dictionary, player: Node, voice_player: Node = null) -> void:
	if player != null:
		if player.has_method("set_third_person"):
			player.set_third_person(bool(data["third_person"]))
		if player.has_method("apply_camera_settings"):
			player.apply_camera_settings(float(data["mouse_sensitivity"]), float(data["fov"]))
	if voice_player != null and voice_player.has_method("set_enabled"):
		voice_player.set_enabled(bool(data["voice"]))
	_apply_global(data)


static func _apply_global(data: Dictionary) -> void:
	# Master volume and mute.
	var bus_idx := AudioServer.get_bus_index("Master")
	if bus_idx != -1:
		var v := float(data["master_volume"])
		AudioServer.set_bus_volume_db(bus_idx, linear_to_db(v))
		AudioServer.set_bus_mute(bus_idx, v <= 0.001)
	# Quality preset: window size.
	var quality := str(data["quality"])
	var size := Vector2i(1280, 720)
	if quality == "low":
		size = Vector2i(960, 540)
	elif quality == "high":
		size = Vector2i(1600, 900)
	DisplayServer.window_set_size(size)
	# Fullscreen toggle.
	if bool(data["fullscreen"]):
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	else:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)


static func linear_to_db(linear: float) -> float:
	return 20.0 * log(maxf(linear, 1e-6)) / log(10.0)
