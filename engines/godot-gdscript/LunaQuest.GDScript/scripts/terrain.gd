class_name LQTerrain
extends RefCounted
## Exact GDScript port of shared/LunaQuest.Core/Terrain/LunarTerrain.cs.
##
## One Moon for every engine: the height of any point depends only on (x, z) metres and the
## site seed, so GDScript, Godot .NET and Unity draw the same ground and a save never stores
## terrain. Model: low-frequency highland relief + three scales of simple impact craters
## (bowl, raised rim, ejecta) + fine regolith roughness, blended flat around the colony pad.
##
## The C# code hashes with unchecked uint32 arithmetic. GDScript ints are 64-bit signed, so
## every step is masked to 32 bits and multiplies use a 16-bit split (_mul32) to stay inside
## int64 — verified to reproduce the C# hash bit-for-bit. Do not "simplify" the masking.

const M := 0xFFFFFFFF
const SITE_SEED := 1337

var seed: int
var flat_center_x: float
var flat_center_z: float
var flat_radius: float
var fade_width: float
var ruggedness: float


func _init(p_seed: int = SITE_SEED, p_fcx: float = 0.0, p_fcz: float = 8.0,
		p_flat: float = 88.0, p_fade: float = 90.0, p_rugged: float = 1.0) -> void:
	seed = p_seed
	flat_center_x = p_fcx
	flat_center_z = p_fcz
	flat_radius = p_flat
	fade_width = maxf(1.0, p_fade)
	ruggedness = p_rugged


## Height above the datum plane in metres.
func height(x: float, z: float) -> float:
	var dx := x - flat_center_x
	var dz := z - flat_center_z
	var dist := sqrt(dx * dx + dz * dz)
	var blend := _smooth((dist - flat_radius) / fade_width)
	if blend <= 0.0:
		return 0.0
	return (_relief(x, z) + _craters(x, z)) * blend * ruggedness


## Slope angle in degrees, from central differences over 2 m.
func slope_degrees(x: float, z: float) -> float:
	var gx := (height(x + 1.0, z) - height(x - 1.0, z)) / 2.0
	var gz := (height(x, z + 1.0) - height(x, z - 1.0)) / 2.0
	return atan(sqrt(gx * gx + gz * gz)) * 180.0 / PI


func _relief(x: float, z: float) -> float:
	var h := 0.0
	h += (_fbm(x / 420.0, z / 420.0, 11, 4) - 0.5) * 2.0 * 16.0
	h += (_fbm(x / 70.0, z / 70.0, 23, 3) - 0.5) * 2.0 * 1.4
	h += (_fbm(x / 9.0, z / 9.0, 37, 2) - 0.5) * 2.0 * 0.12
	return h


func _craters(x: float, z: float) -> float:
	return _crater_layer(x, z, 520.0, 70.0, 210.0, 0.55, 101) \
		+ _crater_layer(x, z, 150.0, 14.0, 52.0, 0.75, 202) \
		+ _crater_layer(x, z, 42.0, 2.5, 13.0, 0.80, 303)


func _crater_layer(x: float, z: float, cell: float, r_min: float, r_max: float,
		presence: float, salt: int) -> float:
	var cx := int(floor(x / cell))
	var cz := int(floor(z / cell))
	var sum := 0.0
	for i in range(-1, 2):
		for j in range(-1, 2):
			var gx := cx + i
			var gz := cz + j
			var hsh := _hash32(gx, gz, seed + salt)
			if _unit(hsh) > presence:
				continue
			var r := r_min + (r_max - r_min) * pow(_unit(_next32(hsh, 1)), 2.2)
			var px := (gx + 0.15 + 0.7 * _unit(_next32(hsh, 2))) * cell
			var pz := (gz + 0.15 + 0.7 * _unit(_next32(hsh, 3))) * cell
			var dx := x - px
			var dz := z - pz
			var t := sqrt(dx * dx + dz * dz) / r
			if t > 2.6:
				continue
			var depth := r * 0.19 * (0.55 + 0.45 * _unit(_next32(hsh, 4)))
			var rim := depth * 0.22
			var bowl := depth * (t * t - 1.0) if t < 1.0 else 0.0
			var ring := rim * exp(-pow((t - 1.0) / 0.30, 2.0))
			var ejecta := rim * 0.35 * exp(-(t - 1.0) * 2.4) if t > 1.0 else 0.0
			sum += bowl + ring + ejecta
	return sum


func _fbm(x: float, z: float, salt: int, octaves: int) -> float:
	var sum := 0.0
	var amp := 0.5
	var norm := 0.0
	for o in range(octaves):
		sum += amp * _value_noise(x, z, salt + o * 17)
		norm += amp
		x *= 2.03
		z *= 2.03
		amp *= 0.5
	return sum / norm


func _value_noise(x: float, z: float, salt: int) -> float:
	var xi := int(floor(x))
	var zi := int(floor(z))
	var fx := x - xi
	var fz := z - zi
	var u := fx * fx * (3.0 - 2.0 * fx)
	var v := fz * fz * (3.0 - 2.0 * fz)
	var a := _unit(_hash32(xi, zi, seed + salt))
	var b := _unit(_hash32(xi + 1, zi, seed + salt))
	var c := _unit(_hash32(xi, zi + 1, seed + salt))
	var d := _unit(_hash32(xi + 1, zi + 1, seed + salt))
	var top := a + (b - a) * u
	var bottom := c + (d - c) * u
	return top + (bottom - top) * v


static func _mul32(a: int, b: int) -> int:
	a &= M
	b &= M
	return ((a & 0xFFFF) * b + ((((a >> 16) * b) & 0xFFFF) << 16)) & M


static func _hash32(x: int, z: int, salt: int) -> int:
	var h := (_mul32(salt, 374761393) + _mul32(x, 668265263) + _mul32(z, 2246822519)) & M
	h = _mul32(h ^ (h >> 13), 1274126177)
	return (h ^ (h >> 16)) & M


static func _next32(h: int, n: int) -> int:
	h = (h + _mul32(n, 0x9E3779B9)) & M
	h = _mul32(h ^ (h >> 15), 2246822519)
	h = _mul32(h ^ (h >> 13), 3266489917)
	return (h ^ (h >> 16)) & M


static func _unit(h: int) -> float:
	return float(h & 0xFFFFFF) / 16777216.0


static func _smooth(t: float) -> float:
	if t <= 0.0:
		return 0.0
	if t >= 1.0:
		return 1.0
	return t * t * (3.0 - 2.0 * t)
