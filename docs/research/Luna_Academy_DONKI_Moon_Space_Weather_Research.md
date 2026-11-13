---
title: "Luna Academy — DONKI Moon Space-Weather Research"
subtitle: "Solar events that matter to a long-duration lunar settlement"
version: "1.0"
research_cutoff_utc: "2026-11-13"
filtered_period_utc: "2020-06-01 to 2026-11-13"
catalog: "M2M_CATALOG"
project: "Luna Academy"
---
the api = https://ccmc.gsfc.nasa.gov/tools/DONKI/
# LUNA ACADEMY
## DONKI • Moon Space-Weather Research

**A research brief for designing the solar-radiation, storm-shelter, mission-control and NPC systems of a realistic Moon-colony game.**

> **Main finding:** For people living on the lunar surface, the most important DONKI event family to monitor is **Solar Energetic Particles (SEP)**. Solar flares and coronal mass ejections (CMEs) help explain and contextualize some SEP events. DONKI observations are generally made by spacecraft near Earth, SOHO or STEREO; they are not direct measurements of radiation dose inside a lunar habitat.

![3D isometric concept: solar particles and a shielded lunar habitat](assets/donki_lunar_radiation_3d.png)

**Research cutoff:** 13 November 2026 (UTC)  
**Date filter:** 1 June 2020 – 13 November 2026 (UTC)  
**Primary catalog:** `M2M_CATALOG`  
**Selection:** Relevance-ranked event examples, not a complete statistical census of every DONKI record.

---

## 1. Executive summary

DONKI is NASA CCMC's space-weather knowledge base. It links observations, analyst interpretations, model outputs and notifications across event families such as solar flares (FLR), solar energetic particles (SEP), coronal mass ejections (CME), interplanetary shocks (IPS), high-speed streams (HSS), geomagnetic storms (GST) and WSA-ENLIL simulations. The active Moon-to-Mars catalog (`M2M_CATALOG`) contains entries from June 2020 onward; earlier entries were handled by its predecessor team. [DONKI overview](https://ccmc.gsfc.nasa.gov/tools/DONKI/) · [DONKI data reference](https://ccmc.gsfc.nasa.gov/DONKI/guide/data-reference/)

### What matters most for a Moon base?

1. **SEP proton flux:** the most direct DONKI event indicator in this review for a solar-particle hazard scenario. A solar particle event can raise radiation exposure for people and hardware beyond Earth's protective environment.
2. **Flare class and timing:** useful as an early event marker and as a way to connect related records. An X-class flare is not itself a measurement of proton dose.
3. **CME speed, width, direction and modeled propagation:** useful for visualizing a large solar eruption and understanding why a shock/particle event may follow. A CME forecast for Earth is not a lunar-arrival forecast.
4. **IPS and HSS:** useful environmental context for changing solar-wind conditions. Treat spacecraft-location observations as local to the observing platform.
5. **Earth-specific indices:** geomagnetic storm Kp, magnetopause crossings and radiation-belt enhancements are not direct lunar-surface hazard values. Do not copy Earth's Kp number into a Moon radiation meter.

NASA explains that the Moon has no substantial atmosphere and no global magnetic field to provide Earth-like protection. Lunar crews therefore need shielding against the general radiation environment and access to shelter during major SEP events. [NASA lunar radiation environment](https://www.nasa.gov/wp-content/uploads/static/history/alsj/WOTM/WOTM-Radiation.html) · [NASA: protecting astronauts from space radiation](https://science.nasa.gov/science-research/heliophysics/how-nasa-will-protect-astronauts-from-space-radiation-at-the-moon/)

---

## 2. Filtering method

### 2.1 Date window

The selected date window is **2020-06-01 through 2026-11-13 UTC**. June 2020 is used because NASA describes that as the start of the primary `M2M_CATALOG`; records before that date belong to the earlier catalog period. This review highlights dated examples that are useful for a lunar-habitation simulation. It does **not** claim to count every event in that six-year period.

### 2.2 Event filters to keep

| DONKI type | Keep? | Why it matters for Luna Academy |
|---|---|---|
| `SEP` | **Primary** | Proton/energetic-particle event detection; most relevant event family for a solar-particle shelter scenario. |
| `FLR` | **Primary context** | Flare class, time and solar active region help connect the event chain. Flare class alone is not dose. |
| `CME` | **Primary context** | Eruption time, speed, width, direction and morphology help describe an associated eruption. |
| `ENLIL` / WSA-ENLIL | **Primary context** | Modelled propagation and impact locations can be used for space-weather visualization; do not assume a modelled Earth/Mars impact is a Moon impact. |
| `IPS` | Secondary | Shock arrival and local solar-wind changes at the listed observation location. |
| `HSS` | Secondary | High-speed solar-wind stream context. |
| `GST`, `MPC`, `RBE` | Usually exclude from lunar surface score | Primarily Earth magnetosphere / radiation-belt context; useful for Earth-orbit lessons, not a direct lunar surface radiation meter. |

DONKI's current event search exposes date-range and catalog filters. Event-specific filters include flare class, SEP observing location, CME speed/latitude/longitude/half-angle and modelled ENLIL impact location. [DONKI Search Events guide](https://ccmc.gsfc.nasa.gov/DONKI/guide/events/)

### 2.3 Why the records are a selected sample

The records below were selected because they contain a useful combination of one or more of these features: X- or M-class flare, recorded SEP threshold crossing, a linked CME or shock, high-energy proton channel, or an interpretable notification. Values from GOES, STEREO A, SOHO and L1 are preserved with their observing platforms and energy channels. They are **not pooled into one false “Moon dose” dataset**.

---

## 3. Important DONKI dates

The dates below are the most useful case studies found for a Moon-colony space-weather system. Times are UTC. **“Observed at” always means the platform stated in the DONKI record, not the lunar surface.**

![DONKI selected event timeline](assets/donki_event_timeline.png)

### Case 1 — 11–12 May 2024: X5.8 flare, O-type CME and SEP

- **11 May 2024 01:23 UTC:** X5.8 flare peak from Active Region 3664.
- **11 May 2024 01:36 UTC:** associated O-type CME first seen.
- **11 May 2024 04:07 UTC:** SEP threshold crossed at STEREO A / IMPACT in the 13–100 MeV proton channel; reported flux exceeded **0.1 pfu/MeV**.
- The DONKI weekly report describes further SEP activity during the May 8–14 interval, including later threshold crossings at STEREO A, GOES and SOHO; the enhanced environment at the reported locations decreased below thresholds on 12 May for the second event described in the notification.

**Why it matters to the game:** a flare/CME/SEP chain can produce a sequence of alerts rather than one instantaneous “storm” button. Use it to design a multi-step event: observe the Sun, receive a particle alert, recall an EVA team, secure exposed work and enter a protected area.

Sources: [DONKI SEP record 30717](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/30717/1) · [DONKI notification 20240511-AL-013](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/30728/1) · [DONKI May 8–14 event report](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/30944/1)

### Case 2 — 14–17 September 2024: X4.5 flare, CME/shock and SEP channel rise

- **14 September 2024 15:29 UTC:** X4.5 flare peak from Active Region 13825.
- **14 September 2024 15:36 UTC:** associated CME first seen.
- **16 September 2024 22:49 UTC:** related shock observed at L1 by ACE/DSCOVR (Earth-upstream observation location).
- **17 September 2024 04:40 UTC:** SOHO/COSTEP 15.8–39.8 MeV proton channel crossed the reported threshold of **0.1 pfu/MeV**. DONKI notes that the channel may have been elevated below threshold from 14 September and that the CME arrival likely contributed to the later crossing.

**Why it matters to the game:** teach the difference between a flare, a CME and an interplanetary shock. The shock time in this record is an L1/Earth measurement; do not reuse it as the time the same shock reaches the Moon.

Sources: [DONKI SEP record 33473, version 2](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/33473/2) · [CCMC Earth CME prediction detail](https://ccmc.gsfc.nasa.gov/CMESB-Earth/prediction/detail/3582)

### Case 3 — 3–10 October 2024: X9 flare and a strong SEP enhancement later in the period

- **3 October 2024 12:18 UTC:** DONKI notification records an **X9.0** flare peak from Active Region 13842.
- **10 October 2024 14:50 UTC:** a DONKI notification about an interplanetary shock says it further enhanced an ongoing SEP event; the GOES **greater-than-10 MeV** proton flux reached **1368 pfu** at that time.

**Important interpretation:** the 3 October flare and 10 October proton-flux observation are both relevant dates in the period, but the notification excerpt does not establish that the X9.0 flare alone caused the 10 October measurement. Preserve the documented event chain rather than inventing causality.

**Why it matters to the game:** a useful high-intensity training scenario for reading flux plots, comparing event timestamps and understanding that particle conditions can persist or be enhanced by a later shock.

Sources: [DONKI X9.0 flare notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/33695/1) · [DONKI IPS/SEP enhancement notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/33908/1)

### Case 4 — 11–12 November 2025: X5.1 flare, large halo CME, L1 shock and high-energy protons

- **11 November 2025 10:04 UTC:** X5.1 flare peak from Active Region 14274.
- **11 November 2025 10:23 UTC:** large halo CME first seen.
- **12 November 2025 18:52 UTC:** CME shock arrival inferred/observed at L1 by ACE/DSCOVR.
- In the DONKI CME detail, total magnetic field rose from about **19 nT to about 40 nT** around the shock arrival; solar-wind speed later rose from about **580 km/s to about 820 km/s**.
- The SEP notification reports: greater-than-100 MeV proton flux exceeded **1 pfu** on 11 November 10:20 UTC; greater-than-10 MeV exceeded **10 pfu** on 11 November 17:20 UTC; after a further rise beginning 12 November 18:40 UTC, the greater-than-100 MeV channel reached a maximum **5.67 pfu so far** at the notification time.

**Why it matters to the game:** this is a strong linked case for the space-weather console: flare → CME → shock/solar-wind disturbance → SEP channel alerts. Keep the “so far” qualifier on the 5.67 pfu value; it was the reported maximum at that notification time, not a final integrated event total.

Sources: [DONKI SEP notification 20251112-AL-008](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/42660/1) · [CCMC CME prediction/detail 5045](https://ccmc.gsfc.nasa.gov/CMEscoreboard/prediction/detail/5045)

### Case 5 — 21 August 2026: M1.6 flare, CME and STEREO A SEP

- **21 August 2026 09:57 UTC:** M1.6 flare peak.
- **21 August 2026 09:36 UTC:** associated CME first seen (CME observation precedes the flare peak in this event record).
- **21 August 2026 17:33 UTC:** STEREO A detected a 13–100 MeV proton-channel threshold crossing above **0.1 pfu/MeV**.
- DONKI's notification says spacecraft located near STEREO A can be impacted.

**Why it matters to the game:** a reminder that moderate flare class does not mean “no particle event.” The player should examine SEP measurements and observation channels, not judge risk from flare class alone.

Source: [DONKI notification 20260821-AL-005](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/48199/1)

### Case 6 — 5 September 2026: M1.0 flare and GOES/SOHO SEP detections

- **5 September 2026 15:18 UTC:** M1.0 flare peak from Active Region 14520.
- **5 September 2026 15:36 UTC:** associated CME first seen.
- **5 September 2026 16:15 UTC:** GOES-P/SEISS greater-than-10 MeV proton flux crossed **10 pfu**.
- DONKI also lists SOHO/COSTEP threshold crossings in nearby 15.8–39.8 MeV and 28.2–50.1 MeV channels around 16:18 UTC.

**Why it matters to the game:** a lower-class flare can still be linked with a recorded SEP threshold crossing. A system driven only by X-class flare labels would miss important situations.

Sources: [DONKI SEP notification 20260905-AL-007](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/48560/1) · [GOES SEP record](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/48558/1) · [SOHO/COSTEP record](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/48564/1)

---

## 4. What each DONKI measurement actually means

| Term | Plain-language meaning | Use in Luna Academy | Do not confuse it with |
|---|---|---|---|
| FLR / flare class | Soft X-ray brightness classification of a solar flare | Story trigger, event marker, timeline | A proton flux or astronaut dose |
| SEP | Energetic particles, commonly protons, detected in an instrument's energy channel | Main solar-particle hazard signal | A direct reading inside a Moon habitat |
| pfu | Particle-flux unit: particles per square centimetre per second per steradian (with channel/energy convention) | Preserve a specific channel's reported value and threshold | Sievert, gray or personal dose |
| CME | Large eruption of magnetized solar plasma | 3D propagation visual, event-chain context | A radiation-dose measurement |
| IPS | Interplanetary shock observed at a stated location | Arrival marker at that observer | Arrival time at the Moon |
| ENLIL / WSA-ENLIL | Modelled heliospheric propagation | Forecast visualization and uncertainty lessons | A guaranteed trajectory or lunar impact |
| HSS | High-speed solar-wind stream | Background solar-wind variability | SEP dose |
| GST / Kp | Geomagnetic disturbance measured for Earth's magnetosphere | Earth-space-weather lesson | Lunar surface radiation index |

### Three rules for correct science

1. **Flux is not dose.** A pfu measurement is a particle-flux value in a defined energy channel and at a particular observing platform. It cannot be converted directly into a crew dose without particle spectra, geometry, shielding, material transport and dosimetry calculations.
2. **Flare class is not particle severity.** X-ray class and proton flux are related solar phenomena but are different measurements. A moderate flare can be associated with an SEP detection; a large flare does not by itself give the crew's dose.
3. **Earth arrival is not lunar arrival.** An L1, GOES, or Earth geomagnetic observation must not be shown as if it were measured at the lunar outpost. A Moon-specific forecast requires propagation to the Moon's position and an appropriate model/data source.

---

## 5. What DONKI does *not* tell us about a lunar settlement

DONKI is a solar/heliospheric space-weather event database, not a complete Moon-base environmental database. The records selected here do not provide:

- A direct radiation-dose time series for a human standing on the lunar South Pole.
- Radiation dose inside a particular habitat, rover or storm shelter.
- A certified thickness of regolith, water or other shielding required for a safe shelter.
- A lunar-specific arrival time for every Earth-observed CME or shock.
- A complete solar-particle spectrum at the Moon for every event.

For lunar base design, combine DONKI with lunar radiation instrument measurements, radiation-transport models, surface/spacecraft dosimeters, NASA lunar-environment references and qualified human-spaceflight radiation-protection engineering. NASA notes that long-duration lunar operations require adequate GCR shielding and access to SEP shelter. [NASA radiation-environment reference](https://www.nasa.gov/wp-content/uploads/static/history/alsj/WOTM/WOTM-Radiation.html)

DONKI itself states that its information and simulations are supplied as-is and should be considered prototyping quality / research context. It also points users to NOAA's Space Weather Prediction Center as the official U.S. government source for operational space-weather forecasts. DONKI is excellent for educational case studies and prototyping, not the sole source for real mission safety decisions. [DONKI disclaimer and rules](https://ccmc.gsfc.nasa.gov/tools/DONKI/)

---

## 6. Translating DONKI into Luna Academy gameplay

### 6.1 Build a Solar Weather Console

The Mission Control screen can show four separate tracks:

1. **Flare track:** class, start/peak/end, active region and source location.
2. **SEP track:** observing instrument, platform, energy channel, reported threshold and event start/end when available.
3. **CME track:** first-seen time, speed, angular width, direction/morphology and linked event IDs.
4. **Propagation/context track:** WSA-ENLIL model outputs, IPS arrival at the stated observer and HSS background state.

Always show the **observer label** (GOES, SOHO, STEREO A, L1) next to a measurement.

### 6.2 Game-state model (fictional gameplay abstraction)

Use a small, readable state machine. These are **game states**, not real NASA/NOAA storm classes and not medical dose bands.

| Game state | Trigger concept | Player-facing world response |
|---|---|---|
| Quiet | No selected active SEP alert in the scenario | Normal EVA and colony routines |
| Watch | Flare/CME activity is recorded; particle status not yet confirmed | Maya asks crew to monitor the console; Leo checks communications |
| Prepare | SEP detection/forecast reaches a scenario threshold | NPCs secure external equipment; player can help return a rover |
| Shelter | Scenario's particle hazard is active | NPCs move to the designated protected room; nonessential EVA is paused |
| Recovery | Scenario alert has ended or fallen below the scenario's recovery condition | Inspect equipment, review data and resume tasks gradually |

Do not expose a real-world medical risk category or claim a game threshold is a safe/unsafe dose. A child can learn to interpret event data without being shown fictional medical advice.

### 6.3 Example optional incident: “Solar Particle Alert”

**Story setup:** The Mission Control console shows a new SEP notification. It is based on a real historical DONKI pattern, but the game event is a fictional reconstruction.

1. The player sees a solar activity update and opens the event timeline.
2. Maya explains the difference between the flare and the SEP measurement.
3. Sofia asks the player to check the crew board and confirm that the outside team has returned (fictional in-game procedure).
4. Omar asks for help securing an external maintenance cart and inspecting a habitat seal.
5. Leo shows which instrument generated the alert and where it was located.
6. The player enters the protected habitat zone and compares the event timeline with the console's shelter status.
7. After the scenario, Amina opens a short science journal entry: “What did we measure? Where was it measured? What does it not tell us?”

**Player choice:** The player may investigate the console, help an NPC, or continue other non-EVA indoor activities. The world continues, but the crew's safety rules remain consistent within the scenario.

### 6.4 What this teaches

- Solar physics: flare, CME, SEP and shock are different phenomena.
- Data literacy: time stamps, graphs, instruments, energy channels and thresholds.
- Measurement literacy: pfu is not dose.
- Risk reasoning: distinguish observed facts from forecasts and uncertainty.
- Teamwork: NPCs have separate responsibilities during an incident.
- Responsibility: prepare, communicate, verify and review rather than panic.

---

## 7. 3D world and presentation requirements

For the Moon-only setting, visualize the solar event as a 3D sequence:

- **Sun:** active region, flare flash and CME launch.
- **Interplanetary space:** particle-stream visualization and CME cloud. This is an explanatory visualization, not a measured particle trajectory for every event.
- **Earth inset:** show Earth's magnetosphere only as a contrast; do not imply it protects the Moon in the same way.
- **Moon:** show exposed surface, low-horizon sunlight, cratered terrain and a base partly protected by a regolith berm or buried/covered shelter concept.
- **Base interior:** show the designated protected room, dosimeter display and crew status board.

The Moon's South Pole has unusual low-angle sunlight, long moving shadows, permanently shadowed regions and extreme thermal differences. These should affect base placement, solar arrays, rover routes and exploration schedules. [NASA lunar South Pole environment](https://www.nasa.gov/reference/moonbase-environment/) · [NASA shadows at the South Pole](https://science.nasa.gov/resource/shadows-near-the-moons-south-pole/)

**Asset usage note:** The included SVG is an original explanatory vector diagram created for this research pack. It is a conceptual illustration, not NASA mission artwork, measured geometry or a certified shelter design.

---

## 8. Recommended data architecture for the game

Store each event as an immutable source record plus a separate gameplay interpretation. This prevents game balancing from overwriting the science.

```ts
export type DonkiEvent = {
  id: string;
  eventType: "FLR" | "SEP" | "CME" | "IPS" | "HSS" | "ENLIL";
  eventTimeUtc: string;
  observer?: string;
  energyChannelMeV?: { min?: number; max?: number };
  reportedFlux?: number;
  fluxUnit?: "pfu" | "pfu/MeV";
  flareClass?: string;
  cmeSpeedKmS?: number;
  linkedEventIds: string[];
  sourceUrl: string;
  lunarSurfaceMeasurement: false;
};

export type GameSolarScenario = {
  scenarioId: string;
  sourceEventIds: string[];
  fictionalSeverity: "watch" | "prepare" | "shelter";
  playerObjectives: string[];
  npcResponses: string[];
  learningObjectives: string[];
  note: "Gameplay abstraction — not an operational radiation forecast";
};
```

### Suggested pipeline

`DONKI record → validation → preserve observer/channel/units → link related events → game scenario adapter → NPC/world responses → learning journal`

Recommended validation checks:

- Reject an SEP record if its observer or energy channel is silently dropped.
- Never compare pfu and pfu/MeV as if the units were identical.
- Keep UTC timestamps, including the date and time zone.
- Store forecast and observation fields separately.
- Store the source URL and the date the record was retrieved.
- Mark game-created values as `fictional` or `simulation`, never as NASA measurements.

---

## 9. Reproducible DONKI filtering

Use the official DONKI event search:

[https://ccmc.gsfc.nasa.gov/DONKI/events/](https://ccmc.gsfc.nasa.gov/DONKI/events/)

Apply:

- **Time range:** 2020-06-01 through 2026-11-13 UTC.
- **Catalog:** `M2M_CATALOG`.
- **Event families:** FLR, SEP, CME, ENLIL; add IPS and HSS for linked context.
- **Flare filter:** inspect M- and X-class events first, but do not filter SEPs out based only on flare class.
- **SEP filter:** retain observer and channel metadata.
- **CME filter:** retain speed, angular width, direction, morphology and links.
- **ENLIL:** record the model's actual impact location and model distance; never relabel Earth/Mars as Moon.

DONKI API reference: [https://ccmc.gsfc.nasa.gov/DONKI/api/](https://ccmc.gsfc.nasa.gov/DONKI/api/)  
DONKI user guide: [https://ccmc.gsfc.nasa.gov/DONKI/guide/](https://ccmc.gsfc.nasa.gov/DONKI/guide/)  
DONKI event filters guide: [https://ccmc.gsfc.nasa.gov/DONKI/guide/events/](https://ccmc.gsfc.nasa.gov/DONKI/guide/events/)

The CCMC announced that DONKI moved to new official URLs on 30 September 2026; endpoint paths under `/DONKI/` remain the same when using the new `ccmc.gsfc.nasa.gov` host. [CCMC migration announcement](https://ccmc.gsfc.nasa.gov/news/major-updates/)

---

## 10. Priorities for Luna Academy

| Priority | Game feature | Research basis |
|---|---|---|
| P0 | SEP event alert and observer-labelled timeline | DONKI SEP event records |
| P0 | Storm-shelter access and EVA pause as a fictional scenario | NASA lunar radiation-protection guidance |
| P0 | Explain pfu versus dose | Instrument channel metadata and radiation science |
| P1 | Flare → CME → shock → SEP event chain visualization | Linked DONKI records |
| P1 | NPC roles during a space-weather incident | Crew operations simulation |
| P1 | Post-event inspection and learning journal | Cause/effect and data review |
| P2 | Historical solar-event replay mode | Selected DONKI historical case studies |
| P2 | Live data panel | Only after robust API, validation, caching and NOAA operational-source separation |

---

## 11. Research limitations and safety note

- This is a relevance-ranked selection of records and notifications located through DONKI's searchable public interface and indexed event pages. It is **not** a downloaded, exhaustive copy of every API record in the selected date period.
- Several source records refer to GOES, SOHO, STEREO A or L1. Those observations are valuable space-weather context, but are not lunar surface dosimetry.
- DONKI's forecasts/simulations are provided as research/prototyping information. Its own page directs users to NOAA SWPC for official U.S. operational space-weather forecasts.
- No dose conversion or shelter thickness is prescribed here. Real lunar radiation-protection engineering requires qualified analysis and validated transport/dosimetry models.
- The game may simplify the science, but it must label simplified or fictional gameplay numbers clearly.

---

## 12. Sources

### DONKI and CCMC

1. [CCMC DONKI System — catalog coverage, disclaimer and rules](https://ccmc.gsfc.nasa.gov/tools/DONKI/)
2. [DONKI Search Events — date and event-specific filters](https://ccmc.gsfc.nasa.gov/DONKI/guide/events/)
3. [DONKI Data Reference — catalogs, CME measurements and SCORE classes](https://ccmc.gsfc.nasa.gov/DONKI/guide/data-reference/)
4. [DONKI API Reference](https://ccmc.gsfc.nasa.gov/DONKI/api/)
5. [CCMC September 2026 migration announcement](https://ccmc.gsfc.nasa.gov/news/major-updates/)

### Selected event records

6. [11 May 2024 SEP notification — X5.8 flare and O-type CME](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/30728/1)
7. [11 May 2024 SEP event — STEREO A, 13–100 MeV](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/30717/1)
8. [May 2024 multi-day space-weather report](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/30944/1)
9. [17 September 2024 SEP event — SOHO/COSTEP](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/33473/2)
10. [3 October 2024 X9.0 flare notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/33695/1)
11. [10 October 2024 SEP/IPS enhancement notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/33908/1)
12. [12 November 2025 SEP notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/42660/1)
13. [November 2025 CME detail](https://ccmc.gsfc.nasa.gov/CMEscoreboard/prediction/detail/5045)
14. [21 August 2026 SEP notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/48199/1)
15. [5 September 2026 SEP notification](https://ccmc.gsfc.nasa.gov/DONKI/view/Alert/48560/1)
16. [5 September 2026 GOES SEP event record](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/48558/1)
17. [5 September 2026 SOHO/COSTEP SEP event record](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/48564/1)

### Lunar environment and radiation protection

18. [NASA — Radiation Environment (Working on the Moon)](https://www.nasa.gov/wp-content/uploads/static/history/alsj/WOTM/WOTM-Radiation.html)
19. [NASA — How NASA Will Protect Astronauts From Space Radiation at the Moon](https://science.nasa.gov/science-research/heliophysics/how-nasa-will-protect-astronauts-from-space-radiation-at-the-moon/)
20. [NASA — The Lunar South Pole Region](https://www.nasa.gov/reference/moonbase-environment/)
21. [NASA — Solar Wind on the Moon](https://science.nasa.gov/moon/solar-wind/)
22. [NASA — Shadows near the Moon's South Pole](https://science.nasa.gov/resource/shadows-near-the-moons-south-pole/)

---

## Appendix A — Selected machine-readable data

The companion file [`donki_selected_events.json`](donki_selected_events.json) stores the selected event dates, reported values, observing platforms, units, notes and source URLs. The JSON is intended for prototyping a historical event browser or a scenario adapter. It must not be treated as a complete DONKI archive.

## Appendix B — Licensing and attribution

The event summaries in this document are paraphrased from public NASA CCMC/DONKI records and linked NASA references. Follow the CCMC DONKI Rules-of-the-Road for research use, validation studies and acknowledgment. The included vector diagrams were created for this Luna Academy research pack and are conceptual illustrations, not official NASA artwork.
