extends Node
## Autoload `Voice`: plays the spoken lines in `assets/voice`, keyed exactly like every other
## edition (`crew.safaa.greet` → `crew-safaa-greet.mp3`).
##
## The game is fully playable with the sound off — every line that is spoken is also printed as a
## subtitle — so a missing asset degrades the experience instead of breaking it.

const DIRS := ["res://assets/voice/", "res://assets/audio/"]
const EXTENSIONS := [".mp3", ".ogg", ".wav"]

var enabled := true
var player: AudioStreamPlayer
var current_key := ""
var _queue: Array = []
var _cache: Dictionary = {}


func _ready() -> void:
	player = AudioStreamPlayer.new()
	player.bus = "Master"
	player.volume_db = -2.0
	player.finished.connect(_on_finished)
	add_child(player)


func path_for(key: String) -> String:
	if _cache.has(key):
		return _cache[key]
	var stem := key.replace(".", "-")
	for dir in DIRS:
		for extension in EXTENSIONS:
			var candidate: String = dir + stem + extension
			if ResourceLoader.exists(candidate):
				_cache[key] = candidate
				return candidate
	_cache[key] = ""
	return ""


func has_key(key: String) -> bool:
	return path_for(key) != ""


## Plays one line. If something is already speaking the line is queued, so barks never cut each
## other off mid-sentence.
func play_key(key: String) -> bool:
	if not enabled or key == "":
		return false
	var path := path_for(key)
	if path == "":
		return false
	if player.playing:
		if not _queue.has(key):
			_queue.append(key)
		return true
	return _play(path, key)


func _play(path: String, key: String) -> bool:
	var stream = load(path)
	if stream == null:
		return false
	player.stream = stream
	player.play()
	current_key = key
	return true


## F10: the happy chirp NEXO makes when the colony completes a task. Runtime-generated —
## a quick two-note sweep that reads as delight and downloads nothing.
func play_chirp() -> void:
	if not enabled:
		return
	var sample_rate := 22050
	var duration := 0.38
	var frames := int(sample_rate * duration)
	var tdata := PackedByteArray()
	for i in range(frames):
		var t := float(i) / float(sample_rate)
		var p := t / duration
		var f := 1180.0 + 1900.0 * p
		var v := sin(t * TAU * f + sin(t * TAU * 13.0) * 1.1) * 0.72 * sin(p * PI) * (1.0 - p * 0.35)
		tdata.append(int(v * 9200.0) & 0xFF)
		tdata.append((int(v * 9200.0) >> 8) & 0xFF)
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = sample_rate
	wav.stereo = false
	wav.loop_mode = AudioStreamWAV.LOOP_DISABLED
	wav.data = tdata
	var chirp := AudioStreamPlayer.new()
	chirp.bus = "Master"
	chirp.volume_db = -6.0
	chirp.stream = wav
	add_child(chirp)
	chirp.finished.connect(chirp.queue_free)
	chirp.play()


func stop() -> void:
	_queue.clear()
	player.stop()
	current_key = ""


func set_enabled(on: bool) -> void:
	enabled = on
	if not on:
		stop()


func _on_finished() -> void:
	current_key = ""
	while not _queue.is_empty():
		var next: String = _queue.pop_front()
		if play_key(next):
			return
