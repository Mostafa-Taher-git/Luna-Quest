class_name LQClock
extends RefCounted
## Deterministic mission clock, ported from shared/LunaQuest.Core/Time/GameClock.cs.
##
## One real second is twelve simulated minutes, so a thirty-day mission is one hour of play.
## The colony sits on the Shackleton rim (89.9° S): the Sun crawls along the horizon, which is
## why every shadow on this site is a hundred metres long.

const MINUTES_PER_SECOND := 12
const MINUTES_PER_DAY := 1440
const SYNODIC_DAYS := 29.53059
const MISSION_DAYS := 30

var total_minutes := 0.0


func _init(minutes := 0.0) -> void:
	total_minutes = maxf(0.0, minutes)


func step(real_seconds: float) -> void:
	total_minutes += real_seconds * MINUTES_PER_SECOND


## 1-based mission day (1..30).
func day() -> int:
	return int(floor(total_minutes / MINUTES_PER_DAY)) + 1


func day_fraction() -> float:
	return fmod(total_minutes, MINUTES_PER_DAY) / MINUTES_PER_DAY


func hour_of_day() -> float:
	return day_fraction() * 24.0


## 0..1 through the synodic month; drives the Sun's slow circle of the sky.
func lunar_phase() -> float:
	return fmod(total_minutes / MINUTES_PER_DAY, SYNODIC_DAYS) / SYNODIC_DAYS


## A near-polar site: the Sun circles between 8.6° and 13.4° — never overhead, never far below.
func sun_elevation_deg() -> float:
	var tilt := 2.4 * sin(2.0 * PI * lunar_phase() - PI / 2.0)
	return 11.0 + tilt


func sun_azimuth_deg() -> float:
	return fmod(lunar_phase() * 360.0 + 180.0, 360.0)


## Fraction of full irradiance reaching the farm; full noon power never happens here.
func solar_irradiance_fraction() -> float:
	var seasonal := 0.8 + 0.16 * sin(2.0 * PI * (lunar_phase() - 0.15))
	return clampf(seasonal, 0.0, 1.0)


func is_daylight() -> bool:
	return sun_elevation_deg() > 0.0


func mission_progress() -> float:
	return clampf(total_minutes / (MISSION_DAYS * MINUTES_PER_DAY), 0.0, 1.0)


func is_mission_complete() -> bool:
	return day() > MISSION_DAYS


func stamp(arabic := false) -> String:
	var hours := int(hour_of_day())
	var minutes := int((hour_of_day() - hours) * 60.0)
	if arabic:
		return "اليوم %d · %02d:%02d" % [day(), hours, minutes]
	return "Day %d · %02d:%02d" % [day(), hours, minutes]
