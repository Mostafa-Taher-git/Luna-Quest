# ☀️🌕 Living on the Moon: Space Weather Guide
*Moon-relevant data from NASA's DONKI (Space Weather Database Of Notifications, Knowledge, Information)*

> **Important note.** The link you gave is the DONKI **home page**, not a list of events. I could not run live DONKI queries from here, so this guide has **no live event list**. It filters DONKI's event types down to the ones that matter on the Moon, adds verified facts about Moon radiation storms, and gives you a ready-to-run script (untested) to pull the latest events yourself. For official forecasts, use NOAA's Space Weather Prediction Center, which DONKI itself names as the official U.S. source.

---
the api=https://ccmc.gsfc.nasa.gov/tools/DONKI/ 
## 1. What DONKI is

- A database run mainly by NASA's **Moon-to-Mars Space Weather Analysis Office (M2M-SWAO)** at Goddard Space Flight Center.
- It logs near-real-time space weather events, model results and notifications, and links causes and effects.
- DONKI's data is "prototyping quality" and "as is". Don't use it as a safety system.
- **New address:** the web app and API moved on **30 Sept 2026** to `ccmc.gsfc.nasa.gov` (old `kauai.ccmc.gsfc.nasa.gov` links should be switched).

**Event types stored in DONKI's main catalog (M2M_CATALOG):**
Solar Flare (FLR) · Solar Energetic Particle (SEP) · Coronal Mass Ejection (CME) · Interplanetary Shock (IPS) · Magnetopause Crossing (MPC) · Geomagnetic Storm (GST) · Radiation Belt Enhancement (RBE) · High Speed Stream (HSS)

---

## 2. Which DONKI events matter on the Moon

| Event | What it is | Moon importance | Why |
|---|---|---|---|
| **SEP** ☢️ | Burst of fast particles from the Sun | 🔴 **Highest** | Main radiation danger for astronauts, can raise dose in hours |
| **FLR** ⚡ | Solar flare | 🟠 High | A strong flare is sometimes a precursor to a particle storm |
| **CME** 💥 | Cloud of solar plasma | 🟠 High | Can trigger SEPs and disturb technology |
| **IPS** 🌊 | Shock wave in space | 🟡 Medium | Marks when a CME reaches the Earth-Moon region |
| **GST / MPC / RBE** 🌍 | Events inside Earth's magnetic field | ⚪ Low on the surface | They are about Earth's magnetosphere (my interpretation) |
| **HSS** 💨 | Fast solar wind stream | ⚪ Low | Mostly affects Earth's field |

*Importance ratings are my own judgment. DONKI has no "Moon" label that I could verify, so look for events aimed at Earth or seen near Earth, since the Moon is at nearly the same distance from the Sun (my inference).*

---

## 3. Key facts about space weather on the Moon

| Fact | Detail |
|---|---|
| No magnetic shield | Earth's field blocks high-energy particles, but the Moon has none, so its surface is exposed |
| Biggest danger | During a large SEP event, an astronaut in a suit could receive **over 1,000× the dose of someone on Earth**, and about **10× the recommended lifetime limit** (University of Michigan CLEAR center estimate) |
| Warning is short | After a flare or CME is detected, particles usually reach Earth in **under an hour**, and moon-walkers would need more time than that to reach shelter |
| Solar cycle | The Sun is at its most active period of its 11-year cycle, with as many as about **20 large SEP events per year** near maximum |
| Forecasting | A University of Michigan model forecasts solar particle storms up to **24 hours ahead** using images of the Sun |
| Artemis II (April 2026) | NASA and NOAA watched the Sun around the clock. Astronauts wore personal dose trackers and Orion sounded an alarm if radiation rose |
| Cabin protection | Crews are trained to move stored gear around the cabin to add mass between themselves and particles |
| Far-side view | NASA used the Perseverance rover on Mars to spot sunspots on the Sun's far side that Earth cannot see |
| Surface adds more | When deep-space radiation hits the Moon's soil it makes secondary particles, which complicates the local radiation environment |

For everyday (non-storm) radiation on the surface, see the earlier **radiation guide**: about **13.2 µGy/hour**, roughly 2× the ISS.

---

## 4. A solar storm day on the Moon (3D-style timeline)

```
   ☀️ T-24h   Forecast: "elevated chance of particle storm"
      │         → Commander postpones outdoor work
      ▼
   💥 T-0     Flare + CME seen (DONKI logs FLR / CME)
      │
      ▼
   ⏱️ T+~min  Particles can arrive within about an hour
      │         → Alarm: ALL crew move to storm shelter
      ▼
   ┌─────────────────────────────┐   ← 2-3 m soil above
   │ 🛡️ STORM SHELTER            │     supplies, water,
   │ 👥 headcount, dosimeters on │     toilet, comms
   └─────────────────────────────┘
      ▼
   ✅ All clear when particle levels fall (hours to days)
```

---

## 5. What this means for the crew (my interpretation)

| Role | Space-weather job |
|---|---|
| 🧑‍✈️ Commander | Decides when to shelter and when to resume work |
| ⚕️ Medic | Checks personal dosimeters and records each person's dose |
| 📡 Comms engineer | Watches forecasts and alerts from Earth and relays them |
| 🏗️ Builder | Builds a shelter reachable within minutes from every work site |
| 🚙 Rover lead | Plans routes with a quick return path to the shelter |
| 🌿 Farmer | Knows greenhouse crops can be affected too |

**Base rules to design in:** storm shelter close to every work area, a daily forecast briefing, outdoor-work "go / no-go" checks, personal dose tracking, and drills.

---

## 6. Pull the latest events yourself (DONKI API)

Base address: `https://api.nasa.gov/DONKI/` with `startDate`, `endDate` (YYYY-MM-DD) and `api_key`. The built-in `DEMO_KEY` allows about 30 requests an hour. A free key from api.nasa.gov gives about 1,000.

Endpoints named in public API descriptions: `CME`, `CMEAnalysis`, `FLR`, `SEP`, `GST`, `MPC`, `IPS`, `RBE`, `notifications` (type = all, FLR, SEP, CME, IPS, MPC, GST, RBE, report).

```python
# Untested sketch: lists recent events that matter most for the Moon
import requests, datetime as dt
KEY = "DEMO_KEY"            # replace with your own free key
end = dt.date.today()
start = end - dt.timedelta(days=30)
for kind in ["SEP", "FLR", "CME"]:
    url = f"https://api.nasa.gov/DONKI/{kind}"
    r = requests.get(url, params={"startDate": start, "endDate": end, "api_key": KEY})
    data = r.json() if r.ok else []
    print(f"\n{kind}: {len(data)} events in the last 30 days")
    for e in data[:5]:
        t = e.get("eventTime") or e.get("startTime") or e.get("beginTime")
        print(" ", t, e.get("classType") or e.get("sourceLocation") or "")
```

Quick browser checks (replace the dates):
```
https://api.nasa.gov/DONKI/SEP?startDate=2026-10-14&endDate=2026-11-13&api_key=DEMO_KEY
https://api.nasa.gov/DONKI/notifications?startDate=2026-10-14&endDate=2026-11-13&type=SEP&api_key=DEMO_KEY
```

---

## 7. Limits of this guide
- No live DONKI events were retrieved, so nothing here describes today's space weather.
- DONKI is research-grade. For real decisions, use NOAA SWPC.
- Numbers like "1,000×" are estimates for a large event and are not typical.
- Moon-specific forecasting is still being developed.

---

## 8. Sources
- NASA CCMC DONKI home page (page last updated 29 Oct 2026): ccmc.gsfc.nasa.gov/tools/DONKI/
- NASA Science: "To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun"
- University of Michigan Engineering (April 2026): solar radiation forecasts for Artemis
- Astronomy.com / Space.com expert piece by Lulu Zhao, University of Michigan CLEAR center
- NASA NTRS paper on radiation dose tools for Artemis (secondary particles near surfaces)
- Public DONKI API descriptions (api.nasa.gov/DONKI endpoints, notification types)

*Crew roles and the base are fictional concepts in this project. Real space weather decisions must follow official NASA and NOAA guidance.*
