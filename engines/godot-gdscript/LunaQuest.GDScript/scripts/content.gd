extends Node
## Content database (autoload `Content`).
##
## The seven JSON files in `res://data/` are the source of truth for every build of this game —
## the C# core, the Unity project, the browser edition and this one. Nothing in here is dialogue
## generated at runtime: the crew, their topics, the incidents and the learning entries are all
## authored content, and this loader only reads it.

const FILES := {
	"buildings": "res://data/buildings.json",
	"npcs": "res://data/npcs.json",
	"incidents": "res://data/incidents.json",
	"crops": "res://data/crops.json",
	"learning": "res://data/learning.json",
	"research": "res://data/research.json",
	"dialogue": "res://data/dialogue.json",
}

var buildings: Array = []
var npcs: Array = []
var incidents: Array = []
var crops: Array = []
var objectives: Array = []
var research: Array = []
var topics: Array = []

var _building_index := {}
var _npc_index := {}
var _objective_index := {}
var _topic_index := {}
var _loading_error := ""


func _ready() -> void:
	load_all()


func load_all() -> void:
	var buildings_raw := _read("buildings")
	buildings = buildings_raw.get("buildings", [])
	var npcs_raw := _read("npcs")
	npcs = npcs_raw.get("npcs", [])
	var incidents_raw := _read("incidents")
	incidents = incidents_raw.get("incidents", [])
	var crops_raw := _read("crops")
	crops = crops_raw.get("crops", [])
	var learning_raw := _read("learning")
	objectives = learning_raw.get("learning", [])
	var research_raw := _read("research")
	research = research_raw.get("research", [])
	var dialogue_raw := _read("dialogue")
	topics = dialogue_raw.get("topics", [])

	_building_index.clear()
	for b in buildings:
		_building_index[b.get("id", "")] = b
	_npc_index.clear()
	for n in npcs:
		_npc_index[n.get("id", "")] = n
	_objective_index.clear()
	for o in objectives:
		_objective_index[o.get("id", "")] = o
	_topic_index.clear()
	for t in topics:
		var key := "%s/%s" % [t.get("npcId", ""), t.get("id", "")]
		_topic_index[key] = t

	if buildings.is_empty():
		push_error("Luna-Quest content failed to load: %s" % _loading_error)


func _read(name: String) -> Dictionary:
	var path: String = FILES.get(name, "")
	if path == "":
		return {}
	if not FileAccess.file_exists(path):
		_loading_error = "missing %s" % path
		return {}
	var text := FileAccess.get_file_as_string(path)
	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		_loading_error = "could not parse %s" % path
		push_error("Luna-Quest: could not parse %s" % path)
		return {}
	return parsed


# ---- lookups ---------------------------------------------------------------

func building(id: String) -> Dictionary:
	return _building_index.get(id, {})


func npc(id: String) -> Dictionary:
	return _npc_index.get(id, {})


func objective(id: String) -> Dictionary:
	return _objective_index.get(id, {})


func topics_for(npc_id: String) -> Array:
	var out: Array = []
	for t in topics:
		if t.get("npcId", "") == npc_id:
			out.append(t)
	return out


func topic(npc_id: String, id: String) -> Dictionary:
	return _topic_index.get("%s/%s" % [npc_id, id], {})


func crew() -> Array:
	var out: Array = []
	for n in npcs:
		if not n.get("isRobot", false):
			out.append(n)
	return out


func incident(id: String) -> Dictionary:
	for i in incidents:
		if i.get("id", "") == id:
			return i
	return {}


# ---- bilingual helpers -----------------------------------------------------

func loc_en(obj: Dictionary, field: String) -> String:
	return str(obj.get(field + "En", ""))


func loc_ar(obj: Dictionary, field: String) -> String:
	return str(obj.get(field + "Ar", ""))


func label(obj: Dictionary, field: String, arabic: bool) -> String:
	return str(obj.get(field + ("Ar" if arabic else "En"), ""))
