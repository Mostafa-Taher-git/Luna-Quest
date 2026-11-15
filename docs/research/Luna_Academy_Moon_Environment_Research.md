---
title: "Luna Academy — Moon Environment & Living Data Research"
subtitle: "Filtered NASA RadLab / LRO-CRaTER data + lunar surface and habitation references"
version: "1.0"
updated: "2026-11-13"
language: "English"
project: "Luna Academy — The Living Moon Colony"
---
the api =https://visualization.osdr.nasa.gov/radlab/gui/data-api/
# 🌕 LUNA ACADEMY
## Moon Environment & Living Data Research

> **Purpose:** Turn real lunar and space-environment evidence into practical specifications for a 3D Moon-colony sandbox game. This is a research and game-design reference, not a human spaceflight safety manual.

![3D-inspired Moon colony concept](assets/moon-base-concept.png)

| Document field | Value |
|---|---|
| Game | Luna Academy — The Living Moon Colony |
| Player model | One human-controlled player; human NPC crew |
| World | Open-world lunar South Pole-inspired colony and surrounding terrain |
| Audience | Ages 7–13; learning depth adapts to observed play, not an age-selection screen |
| Research snapshot | 13 November 2026 |
| Main source supplied | NASA OSDR RadLab Data API |

---

# 1. Executive findings

## 1.1 What the supplied RadLab source actually contains

NASA's RadLab portal is a radiation-dosimetry data portal. Its API supports filters for celestial body, trajectory, spacecraft, instrument, timestamp, absorbed dose rate, dose-equivalent rate, and particle flux. It can return CSV, TSV, JSON, or HTML. It is **not** a general database of lunar temperature, oxygen, water, food, or habitat telemetry. [RadLab Data API](https://visualization.osdr.nasa.gov/radlab/gui/data-api/) · [RadLab overview](https://visualization.osdr.nasa.gov/radlab/gui/overview/)

The Moon-related RadLab path identified in its Knowledgebase is:

- **Celestial body:** Moon
- **Trajectory:** Lunar orbit (also listed as Moon orbit)
- **Spacecraft:** Lunar Reconnaissance Orbiter (LRO)
- **Instrument:** CRaTER — Cosmic Ray Telescope for the Effects of Radiation
- **Current RadLab cadence described for CRaTER:** 1 hour

The RadLab Knowledgebase lists Moon-associated trajectories as **Lunar orbit** and **Lunar flyby**. The CRaTER entry specifically identifies LRO and a lunar-orbit trajectory. This means these RadLab records are primarily orbital radiation observations—not readings from a permanent surface habitat. [Moon entry](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/moon) · [CRaTER entry](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/crater) · [Moon-orbit entry](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/moon_orbit)

## 1.2 Date window used for the lunar CRaTER archive

The NASA Planetary Data System's LRO CRaTER Data Bundle, version 2.4, lists this coverage:

| Field | Date/time (UTC) |
|---|---|
| Start | 2009-06-29 12:00:00 |
| Stop | 2026-03-31 11:59:59.010 |
| Filter used in the API link | `timestamp >= 2009-06-29` and `timestamp < 2026-04-01` |

**Important scope note:** these are the date boundaries of the cited PDS CRaTER bundle. The static RadLab Knowledgebase page states an hourly cadence but does not display its own exact first/last indexed timestamp. Therefore, the RadLab query below requests this full PDS date window; the API can only return records that are actually indexed in RadLab. [NASA PDS CRaTER Bundle v2.4](https://pds.nasa.gov/ds-view/pds/viewBundle.jsp?identifier=urn%3Anasa%3Apds%3Alro-crater&version=2.4) · [RadLab CRaTER entry](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/crater)

## 1.3 A direct lunar-surface radiation benchmark

RadLab's CRaTER record is orbital. To avoid pretending that orbital data are surface measurements, this document adds a separate, direct surface reference from the **Lunar Lander Neutrons and Dosimetry (LND)** experiment on the Chang'e 4 lander.

The paper shows time-resolved measurements during **3–12 January 2019** and **31 January–10 February 2019**. For its reported averages, the authors excluded readings affected by Yutu-2 still being piggybacked on the lander and the interval when the lander thermal-control system (TCS) was active (**11 January 2019 14:41 to 3 February 2019 07:53**). The reported values also correct for the known RTG/RHU background. Its final table reports:

| Surface measurement | Final reported value | Unit / interpretation |
|---|---:|---|
| Total absorbed dose rate | 13.2 ± 0.7 | µGy/hour in silicon |
| Neutral-particle component | 3.1 ± 0.5 | µGy/hour in silicon |
| Charged-particle component | 10.2 ± 0.9 | µGy/hour in silicon |
| Dose-equivalent rate from charged particles | 57.1 ± 10.6 | µSv/hour; charged particles only |

These are **detector measurements**, with the detector/lander shielding and detector material affecting the reading. They are not a direct personal effective-dose measurement for a human crew member. Do not compare µGy/hour and µSv/hour as if they were the same quantity. [Zhang et al., *Science Advances* (2020), “First measurements of the radiation dose on the lunar surface”](https://pubmed.ncbi.nlm.nih.gov/32978156/) · [Full-text search result](https://pmc.ncbi.nlm.nih.gov/articles/PMC7518862/)

---

# 2. Filtered RadLab data specification

## 2.1 Recommended filter

Use the following filter to isolate the Moon / LRO / CRaTER / lunar-orbit subset and constrain it to the PDS CRaTER coverage window.

**CSV export:**

[Open filtered RadLab CSV query](https://visualization.osdr.nasa.gov/radlab/api/?celestial_body=Moon&trajectory=Lunar%20orbit&spacecraft=LRO&instrument=CRaTER&timestamp%3E=2009-06-29&timestamp%3C2026-04-01&celestial_body&trajectory&spacecraft&instrument&timestamp&absorbed_dose_rate&dose_equivalent_rate&flux&format=csv)

**JSON export:**

[Open filtered RadLab JSON query](https://visualization.osdr.nasa.gov/radlab/api/?celestial_body=Moon&trajectory=Lunar%20orbit&spacecraft=LRO&instrument=CRaTER&timestamp%3E=2009-06-29&timestamp%3C2026-04-01&celestial_body&trajectory&spacecraft&instrument&timestamp&absorbed_dose_rate&dose_equivalent_rate&flux&format=json)

The query uses the RadLab key-value syntax. The API documentation states that `timestamp` accepts inclusive/exclusive comparison operators; `absorbed_dose_rate` is in µGy/hour, `dose_equivalent_rate` is in µSv/hour, and `flux` is in particles·cm⁻²·sr⁻¹·s⁻¹. A field may be unavailable for some instrument records; missing values must not be treated as zero. [RadLab API syntax and units](https://visualization.osdr.nasa.gov/radlab/gui/data-api/)

## 2.2 What to retain from the export

| Field | Meaning | Unit | Game use |
|---|---|---|---|
| `timestamp` | Measurement time | ISO timestamp | Time-series playback and event chronology |
| `celestial_body` | Associated body | Text | Confirm Moon context |
| `trajectory` | Orbital path/type | Text | Separate lunar orbit from lunar surface |
| `spacecraft` | Measuring spacecraft | Text | Identify LRO |
| `instrument` / `instrument_id` | Detector identity | Text | Provenance and detector comparison |
| `absorbed_dose_rate` | Energy deposited per mass and time | µGy/hour | Radiation sensor visualization |
| `dose_equivalent_rate` | Radiation-weighted dose rate, where available | µSv/hour | Advanced radiation-education panel; not automatically whole-body effective dose |
| `flux` | Particle flux | particles·cm⁻²·sr⁻¹·s⁻¹ | Particle-environment visualization |

## 2.3 Filtering and aggregation rules

1. Keep only rows that match Moon + LRO + CRaTER + lunar orbit for the orbital dataset.
2. Retain the timestamp and detector ID with every measurement.
3. Do not merge different instruments into one average before checking their units, calibration, geometry, and detector response.
4. Preserve missing values as `null` / blank.
5. For charts, aggregate to hourly or daily summaries only after retaining the original samples.
6. Mark solar-particle-event spikes separately from the quieter galactic-cosmic-ray background when reliable event labels are available.
7. Keep orbital readings and surface readings in separate tables and separate visual layers.

### Example local analysis after exporting CSV

```python
import pandas as pd

# Save the CSV export from the filtered RadLab link as radlab_moon_crater.csv.
df = pd.read_csv("radlab_moon_crater.csv")
df["timestamp"] = pd.to_datetime(df["timestamp"], utc=True, errors="coerce")
df = df.dropna(subset=["timestamp"]).sort_values("timestamp")

# Keep the selected archive window. End date is exclusive.
df = df[(df["timestamp"] >= "2009-06-29") &
        (df["timestamp"] <  "2026-04-01")]

# Preserve instrument identity. Build daily medians as a visualization summary,
# not as a replacement for the original scientific records.
summary = (df.groupby(["instrument_id", pd.Grouper(key="timestamp", freq="1D")])
             [["absorbed_dose_rate", "dose_equivalent_rate", "flux"]]
             .median(numeric_only=True)
             .reset_index())

summary.to_csv("moon_crater_daily_summary.csv", index=False)
print(df.shape, summary.head())
```

---

# 3. What the data means for living on the Moon

The following topics combine the RadLab radiation dataset with other NASA / peer-reviewed lunar-environment sources. This broader context is necessary because RadLab does not measure every habitat system.

## 3.1 Radiation — a permanent environmental system

**What is real:** Lunar surface crews are exposed to galactic cosmic rays (GCRs), occasional solar particle events (SPEs), and secondary particles produced when radiation interacts with the lunar ground or nearby structures. The Moon does not provide the global magnetic and atmospheric protection experienced at Earth's surface.

**Useful evidence:**

- CRaTER on LRO provides radiation measurements in lunar orbit and has an hourly cadence in the RadLab Knowledgebase entry.
- LND on Chang'e 4 provides a direct lunar-surface measurement for January–February 2019.
- A 2024 *Space Weather* analysis compares measurements and shielding models. It explains why detector readings, material-specific absorbed dose, dose equivalent, and whole-body estimates must be distinguished.

**3D game implementation:**

- Give the player a radiation-monitor screen with a time graph and units.
- Make exposure depend on location, duration, shielding, and event conditions.
- Model habitat shielding as a material/thickness property that changes exposure; do not present one universal “safe number”.
- Add solar-particle-event alerts as rare, serious incidents. During an alert, NPCs follow a shelter protocol and the player can help secure external activity.
- Show the distinction between an external detector reading and an estimated crew exposure.

Sources: [RadLab CRaTER](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/crater) · [LND surface measurement](https://pubmed.ncbi.nlm.nih.gov/32978156/) · [2024 shielding analysis](https://agupubs.onlinelibrary.wiley.com/doi/10.1029/2024SW004095)

## 3.2 Gravity and human movement

| Property | Reference value | Game interpretation |
|---|---:|---|
| Lunar surface gravity | ~1.62 m/s² | ~0.165 of Earth's gravity |
| Practical effect | Lower weight, not zero gravity | Longer jumps, slower falls, altered lifting and vehicle handling |

Do not make the player float like in zero gravity. Objects still fall, people still have traction and inertia, and heavy equipment still has mass. The player's movement can be stylized, but the physics should teach that the Moon has weaker gravity—not no gravity. [NASA Moon facts](https://science.nasa.gov/moon/facts/) · [NASA Glenn: Moon](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/moon/)

## 3.3 Vacuum and pressure habitat

The Moon has no appreciable breathable atmosphere. A person cannot walk outside without a functioning spacesuit, and open liquid water is not stable on the exposed surface.

**Required colony systems:**

- Pressurized habitat shell and pressure monitoring.
- Airlocks with controlled depressurization / repressurization.
- Oxygen supply and distribution.
- Carbon dioxide removal and trace-contaminant control.
- Ventilation, humidity and temperature control.
- Fire detection and suppression.
- Leak detection and emergency reserve procedures.

For an educational reference, NASA's ISS Environmental Control and Life Support System (ECLSS) describes water recovery, air revitalization, oxygen generation, CO₂ removal and waste management. NASA reports the ISS Water Recovery System can recover and recycle about 90% of station water. **Use 90% as an ISS technology reference, not as a guaranteed performance figure for a future lunar base.** [NASA ECLSS](https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/)

## 3.4 Temperature, sunlight and shadow — especially at the South Pole

The lunar South Pole is not a simple repeating sunrise/sunset landscape. The Sun stays very low above the horizon; ridges and crater rims create long moving shadows. Some elevated locations can receive extended sunlight, while permanently shadowed regions (PSRs) may never receive direct sunlight.

NASA reports that some South Pole permanently shadowed regions can reach about **−203°C**. Conditions vary with latitude, elevation, terrain and illumination. NASA has also documented evidence of water ice in some polar shadowed areas, but ice is not evenly distributed and not every shadowed region contains the same amount. [NASA South Pole environment](https://www.nasa.gov/reference/moonbase-environment/) · [NASA shadows visualization](https://science.nasa.gov/resource/shadows-near-the-moons-south-pole/)

**3D game implementation:**

- Use a low-angle Sun near the horizon for the South Pole setting.
- Shadows should be long and should move according to a selected simulation date/time.
- Build a lighting map for ridges, high ground, crater rims and shadowed floors.
- Solar farms should be placed where sunlight access and cable routes justify them.
- PSRs should have deep cold, poor solar access and a special exploration requirement.
- Do not put a green “ice guaranteed” icon on every shadowed crater. Make ice probability and confidence depend on exploration data.

## 3.5 Water ice and local resources (ISRU)

Water ice is scientifically important because, if accessible and extractable, water could support drinking, hygiene, plant growth, oxygen production and potentially propellant production. NASA is developing technologies to collect and process lunar resources, but large-scale routine mining and supply of a permanent crewed Moon base should be treated as a future capability—not an already operational service.

**3D game implementation:**

- Exploration rover scans a candidate PSR site.
- Player collects samples and records location, depth and confidence.
- NPC scientist compares results and proposes a drilling location.
- Excavation system collects icy regolith.
- Processing separates water from soil; purification tests the output.
- A separate electrolysis plant can split water into oxygen and hydrogen in an advanced project.
- Resource extraction consumes power and produces waste heat and tailings.

Sources: [NASA lunar South Pole water ice](https://www.nasa.gov/reference/moonbase-environment/) · [NASA ISRU overview](https://www.nasa.gov/overview-in-situ-resource-utilization/) · [NASA Lunar Surface Technology](https://www.nasa.gov/lunar-surface-technology/)

## 3.6 Lunar dust / regolith

Lunar regolith is made of broken rock, mineral fragments, glass and fine dust. Without wind and water weathering, many grains remain irregular and abrasive. NASA notes that dust can cling to suits, get into seals and mechanisms, obscure optical surfaces and reduce the performance of radiators and solar arrays.

**3D game implementation:**

- Dust accumulates on solar panels, rover joints, radiators, suit connectors and airlock floors.
- Rovers can kick up dust during movement.
- The airlock has a suit-cleaning and dust-capture process.
- Use electrostatic dust-removal equipment as a technology project.
- Avoid a simplistic “sweep with a broom” solution; brushing can make contamination worse in some situations.

Sources: [NASA Lunar South Pole environment](https://www.nasa.gov/reference/moonbase-environment/) · [NASA hazards caused by regolith](https://science.nasa.gov/biological-physical/what-hazards-are-caused-by-lunar-regolith/) · [NASA Electrodynamic Dust Shield](https://www.nasa.gov/centers-and-facilities/kennedy/nasa-technology-helps-guard-against-lunar-dust/)

## 3.7 Meteoroids and impact ejecta

The Moon has little or no atmosphere to stop incoming meteoroids. Impacts can excavate craters and throw ejecta across the surface. NASA's Meteoroid Environment Office studies the impact and ejecta environment for extended lunar surface operations.

**3D game implementation:**

- Habitat and equipment need impact-resistant design and protected placement.
- Add impact-monitoring sensors and inspection tasks.
- A nearby impact may trigger a dust/ejecta inspection or damage report.
- Keep catastrophic impacts rare; more frequent gameplay can focus on small debris, sensor alerts and inspection procedures.

Sources: [NASA Lunar Meteoroid Environment](https://www.nasa.gov/meteoroid-environment-office/lunar-meteoroid-environment/) · [NASA Lunar Impact Monitoring](https://www.nasa.gov/meteoroid-environment-office/about-lunar-impact-monitoring/)

## 3.8 Power and energy storage

Solar power is a key candidate for lunar surface infrastructure, but South Pole shadows make siting and energy storage important. NASA is working on power generation, power management, distribution and storage for long-duration lunar operations. Vertical solar arrays and other concepts are being matured for difficult terrain and long-duration use.

**3D game implementation:**

- Solar arrays have orientation, illumination, dust, temperature and health variables.
- Batteries store energy for shadow periods and peak loads.
- Power distribution uses cables, junction boxes and protected routes.
- Noncritical systems can be load-shed during shortages.
- A future advanced colony may compare solar plus storage with other power sources; don't assume every crater settlement has continuous sunlight.

Source: [NASA Lunar Surface Technology — Surface Power](https://www.nasa.gov/lunar-surface-technology/)

## 3.9 Communications and navigation

Radio signals between Earth and the Moon have a one-way light-time of roughly 1.3 seconds, depending on geometry. Terrain or the Moon itself can block line-of-sight communications; far-side operations therefore need relay infrastructure.

**3D game implementation:**

- Earth communication is not instantaneous; use a short command/reply delay for realism.
- Terrain-aware signal coverage should create dead zones behind crater rims.
- Add relay towers or relay spacecraft as infrastructure projects.
- Local NPC decisions and safety systems must continue without a live Earth link.

Sources: [ESA: communications beyond LEO](https://nebula.esa.int/4000136202) · [NASA Moon Base communications needs](https://www.nasa.gov/reference/moonbase-environment/)

---

# 4. Research-to-game system map

| Real-world evidence / constraint | 3D simulation variable | Colony system | Example player activity | Learning objective |
|---|---|---|---|---|
| CRaTER hourly orbital radiation data | `radiationRate`, `particleFlux` | Radiation monitoring | Compare readings and move to shelter | Radiation, units, data literacy |
| LND lunar surface radiation sample | `surfaceDoseReference` | Suit / habitat shielding | Compare detector and shielded readings | Measurement, shielding, scientific uncertainty |
| Lunar gravity ~0.165 g | `gravity` | Player & vehicle physics | Move cargo and drive rover on a slope | Physics, force, motion |
| South Pole long shadows | `sunDirection`, `illuminationFraction` | Power and navigation | Find a well-lit solar-array site | Geometry, solar energy, mapping |
| PSRs can be extremely cold | `surfaceTemperature` | Thermal control | Inspect insulated equipment | Heat transfer, materials |
| Ice is unevenly distributed | `iceProbability`, `sampleConfidence` | Exploration / ISRU | Survey and sample a crater | Research, probability, geology |
| Abrasive charged dust | `dustLoad`, `sealHealth` | Maintenance / airlock | Remove dust using approved equipment | Materials, safety, engineering |
| No useful atmosphere | `cabinPressure`, `oxygenLevel` | Life support | Find and isolate a simulated leak | Systems thinking, safety |
| Water recovery on ISS ~90% | `waterRecoveryEfficiency` | Water processing | Diagnose filter and water-quality issue | Chemistry, conservation, measurement |
| Terrain blocks radio line of sight | `signalStrength`, `relayCoverage` | Communications | Reposition relay / rover | Networks, geometry, problem-solving |
| Meteoroid impacts and ejecta | `impactAlert`, `surfaceDamage` | Inspection / protection | Inspect external equipment | Risk awareness, observation |

---

# 5. Suggested 3D world variables (starting point)

These are **game architecture fields**, not claims that NASA has published exact operating setpoints for a future habitat. Store real measurements, modeled values and gameplay abstractions separately.

```json
{
  "world": {
    "targetRegion": "lunar-south-pole-inspired",
    "gravity_m_s2": 1.62,
    "gravity_earth_fraction": 0.165,
    "atmosphere": "near-vacuum",
    "sunModel": "low-angle-polar-illumination",
    "terrainLayers": ["elevation", "slope", "roughness", "shadow", "regolith"]
  },
  "radiation": {
    "orbitalReference": "LRO-CRaTER",
    "surfaceReference": "Chang-e-4-LND-2019",
    "storeAbsorbedDoseSeparately": true,
    "storeDoseEquivalentSeparately": true,
    "storeFluxSeparately": true,
    "unknownValuesAreNull": true,
    "neverTreatOrbitalReadingsAsSurfaceMeasurements": true
  },
  "habitat": {
    "pressureMonitoring": true,
    "oxygenMonitoring": true,
    "co2Removal": true,
    "waterRecovery": true,
    "temperatureControl": true,
    "fireDetection": true,
    "leakDetection": true,
    "dustControlAtAirlock": true
  },
  "colony": {
    "solarGeneration": true,
    "batteryStorage": true,
    "powerLoadShed": true,
    "waterStorageAndTreatment": true,
    "greenhouse": true,
    "communicationsRelays": true,
    "maintenanceAndInspection": true
  }
}
```

## 5.1 3D site-planning attributes

Every candidate building tile / location should be able to store:

- `elevation_m`
- `slope_degrees`
- `terrain_roughness`
- `sunlight_fraction_by_time`
- `shadow_duration_hours`
- `distance_to_ridge_m`
- `distance_to_crater_edge_m`
- `radio_line_of_sight`
- `ice_evidence_confidence`
- `regolith_depth_estimate_m`
- `distance_to_habitat_m`
- `cable_and_rover_access_cost`

The game can use these values for realistic building choices: a solar farm wants illumination and service access; an ice-extraction site wants evidence of ice and a safe route; a habitat wants a stable foundation, protected location, power and life-support connections.

---

# 6. High-value incidents for Luna Academy

| Incident | Trigger | Player's options | Main skills |
|---|---|---|---|
| Radiation alert | Elevated radiation-event feed | Return to shelter, check monitoring and secure EVA | Risk assessment, reading data |
| Solar array output drop | Dust / shadow / equipment fault | Inspect panel, clean with dust-control system, reroute power | Energy, diagnosis |
| Habitat pressure warning | Simulated seal leak | Locate module, isolate compartment, notify NPC engineer | Safety, logic |
| CO₂ filter issue | Air-revitalization sensor | Compare readings, swap or service filter | Chemistry, health systems |
| Water-quality anomaly | Conductivity / filter warning | Quarantine tank, test sample, reprocess water | Chemistry, hygiene |
| Cold equipment zone | Long shadow / heater fault | Inspect insulation, power and thermal hardware | Heat transfer, planning |
| Dust in airlock | Suit / seal inspection | Use suit-cleaning and capture procedure | Materials, responsibility |
| Rover radio dead zone | Crater-rim obstruction | Move rover or route through relay coverage | Communication, geometry |
| Suspected ice deposit | Rover / drill sample | Survey, collect, label and compare samples | Scientific method, geology |
| Impact inspection | Sensor reports a nearby impact | Inspect route and external assets safely | Observation, teamwork |

Incidents should not all become emergencies. Mix routine maintenance, scientific discoveries, long projects and occasional high-priority alerts. The player remains free to explore and choose what to do.

---

# 7. Accuracy rules for the game team

1. **Do not label CRaTER orbital measurements as surface measurements.** Use the Chang'e 4 LND record as the direct surface reference in this document.
2. **Do not treat absorbed dose (Gy) and dose equivalent (Sv) as interchangeable.** Keep the detector material and radiation weighting context attached to the value.
3. **Do not treat a detector measurement as a crew member's whole-body effective dose.** Human body geometry, shielding, organs and exposure conditions matter.
4. **Do not claim every permanently shadowed crater contains accessible water ice.** NASA says the distribution is complex and variable.
5. **Do not use an Earth-like sky, wind, rain or open breathable air on the lunar surface.** Weather and sound effects outside a pressurized habitat should be designed accordingly.
6. **Do not model the South Pole with a simple 14-day bright / 14-day dark switch.** Use local terrain, solar azimuth and date-dependent lighting.
7. **Do not assume solar panels always have direct sunlight.** Model shadowing, storage, power routing and load shedding.
8. **Keep research-backed values separate from game balancing values.** Mark every simplified number as `GAMEPLAY_ABSTRACTION`.
9. **Keep future technologies labeled as prototypes, concepts or planned capabilities** until they are demonstrated as operational systems.
10. **Cite data inside the game's science journal** so children can distinguish evidence, interpretation and a fictional scenario.

---

# 8. Recommended in-game science journal entries

### Entry 01 — Why does the Moon need a habitat?
The Moon has no breathable atmosphere. A habitat must keep its inside pressurized and provide oxygen, remove carbon dioxide, control temperature and manage water.

### Entry 02 — Why do we monitor radiation?
Space radiation includes energetic particles from the Sun and from outside the solar system. A radiation detector measures a quantity with a unit and a location; the number does not automatically equal the dose to a person's entire body.

### Entry 03 — Why build near some ridges?
Some high locations near the lunar poles can receive long periods of sunlight. Nearby craters may remain in shadow and preserve very cold conditions. A good base location must balance power, access to resources, terrain safety and communications.

### Entry 04 — Why is lunar dust dangerous to machines?
Lunar dust is irregular and abrasive. It can cling to suits and surfaces, enter seals and mechanisms, and reduce solar-panel or radiator performance.

### Entry 05 — Can we use local water?
Some permanently shadowed regions contain evidence of water ice. Future missions must determine where it is, how much is available and whether it can be safely extracted and processed.

### Entry 06 — Why recycle water?
A colony cannot depend on unlimited deliveries from Earth. Water recovery reduces resupply needs. NASA's ISS system is an engineering reference, but a lunar base would still need systems designed and tested for its own environment.

---

# 9. Source register

The sources below were checked for this research snapshot. Prefer the original NASA / PDS / peer-reviewed pages when updating the numbers.

| ID | Source | Why it matters |
|---|---|---|
| S1 | [NASA RadLab Data API](https://visualization.osdr.nasa.gov/radlab/gui/data-api/) | Filter syntax, fields, units and exports |
| S2 | [RadLab Overview](https://visualization.osdr.nasa.gov/radlab/gui/overview/) | Purpose and scope of the radiation portal |
| S3 | [RadLab Moon Knowledgebase](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/moon) | Moon-related trajectories available in RadLab |
| S4 | [RadLab CRaTER Knowledgebase](https://visualization.osdr.nasa.gov/radlab/gui/knowledgebase/crater) | CRaTER, LRO, lunar orbit, hourly cadence |
| S5 | [NASA PDS — LRO CRaTER Bundle v2.4](https://pds.nasa.gov/ds-view/pds/viewBundle.jsp?identifier=urn%3Anasa%3Apds%3Alro-crater&version=2.4) | Exact archive start/stop dates and collections |
| S6 | [Zhang et al. (2020), First measurements of the radiation dose on the lunar surface](https://pubmed.ncbi.nlm.nih.gov/32978156/) | Direct Chang'e 4 LND surface radiation measurements |
| S7 | [Matthiä & Berger (2024), Radiation Exposure and Shielding Effects on the Lunar Surface](https://agupubs.onlinelibrary.wiley.com/doi/10.1029/2024SW004095) | Measurement/model comparison and shielding interpretation |
| S8 | [NASA — The Lunar South Pole Region](https://www.nasa.gov/reference/moonbase-environment/) | South Pole illumination, temperature, ice, terrain and dust |
| S9 | [NASA — Moon Facts](https://science.nasa.gov/moon/facts/) | Gravity and general lunar facts |
| S10 | [NASA — ECLSS](https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/) | Pressure, oxygen, water recovery, CO₂ and life support |
| S11 | [NASA — Lunar Surface Technology](https://www.nasa.gov/lunar-surface-technology/) | Power, storage, dust mitigation, excavation, ISRU and comms |
| S12 | [NASA — ISRU overview](https://www.nasa.gov/overview-in-situ-resource-utilization/) | Using local resources for water, oxygen, fuel and construction |
| S13 | [NASA — Hazards caused by lunar regolith](https://science.nasa.gov/biological-physical/what-hazards-are-caused-by-lunar-regolith/) | Dust and equipment / crew hazards |
| S14 | [NASA — Lunar Meteoroid Environment](https://www.nasa.gov/meteoroid-environment-office/lunar-meteoroid-environment/) | Meteoroids and ejecta during extended surface operations |
| S15 | [ESA — Communication beyond LEO](https://nebula.esa.int/4000136202) | One-way Earth–Moon communication latency estimate |
| S16 | [NASA — Shadows near the Moon's South Pole](https://science.nasa.gov/resource/shadows-near-the-moons-south-pole/) | Polar Sun angle and moving shadow behavior |

---

# 10. Final recommendation for Luna Academy

Use the RadLab CRaTER subset to power a **radiation-monitoring learning system** and historical orbital radiation visualization. Use the Chang'e 4 LND surface dataset as a separate surface calibration reference. Use NASA's South Pole environment, ECLSS and lunar technology sources to build the habitat, power, water, dust, thermal, navigation and construction systems.

The colony should not just display scientific facts. It should make those facts matter: sunlight determines power, terrain affects radio and rover routes, water evidence shapes exploration, dust creates maintenance, life support makes the habitat livable, and radiation influences when and where NPCs work outside.

**Design target:** a believable, evidence-informed Moon world where children learn by exploring, building, asking human NPCs questions, making decisions and seeing how connected systems respond.

---

*Prepared for Luna Academy. Scientific values are linked to their source and date. Game values must be labeled separately from measured or modeled scientific values.*
