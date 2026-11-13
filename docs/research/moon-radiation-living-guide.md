# 🌕 Living on the Moon: Radiation Data Guide
*Filtered from NASA OSDR RadLab and the published papers behind its Moon datasets*

> **Important note about the data.** The page you gave me (`radlab/gui/data-api/`) is the **API manual**, not a data table. I could not run live queries against the RadLab server from this environment. So the numbers below come from RadLab's release notes and the peer-reviewed papers for the two Moon instruments in RadLab. Section 5 has ready-to-run queries so you can pull the raw rows yourself. Those queries are built from the documented syntax and have **not** been tested.

---
the api = [https://visualization.osdr.nasa.gov/radlab/gui/data-api/](https://visualization.osdr.nasa.gov/radlab/gui/data-api/)
## 1. Moon data inside RadLab

RadLab holds mostly ISS data. Only two instrument datasets relate to the Moon:

| Dataset (instrument ID) | Where it is | What it gives in RadLab |
|---|---|---|
| **LND** (Lunar Lander Neutron & Dosimetry) | On the surface, Chang'e 4 lander, far side, von Kármán crater (45.4° S, 177.6° E) | Absorbed dose rate, median cadence about 1 hour |
| **CRaTER-D1D2** (Cosmic Ray Telescope for the Effects of Radiation) | In orbit, aboard NASA's Lunar Reconnaissance Orbiter (LRO) | Radiation readings in lunar orbit |

Fields in the API that matter for the Moon: `celestial_body=Moon`, `trajectory` (Surface or Orbit), `spacecraft`, `instrument`, `instrument_id`, `timestamp`, `absorbed_dose_rate` (µGy/hour), `dose_equivalent_rate` (µSv/hour), `flux` (cm⁻² sr⁻¹ s⁻¹).
Fields `latitude`, `longitude`, `altitude`, `B` and `L` are tracked **only for the ISS**, so they are blank (`NA`) for Moon data.

---

## 2. The most important numbers

### On the surface (measured, Chang'e 4 LND, first ever)
| Quantity | Value |
|---|---|
| Total absorbed dose rate (in silicon) | **13.2 ± 1 µGy/hour** |
| Dose rate from neutral particles (neutrons + gamma) | **3.1 ± 0.5 µGy/hour** |
| Compared with Mars surface (deep solar minimum) | about **15% higher** on the Moon |
| Compared with the ISS | about **2× higher** than ISS |
| Lander's own radioactive power source (subtracted from the data) | 5.2 ± 0.6 µGy/hour total, 1.7 ± 0.4 neutral |

### In lunar orbit (measured)
| Mission | Dose rate |
|---|---|
| Chandrayaan-1 RADOM, 100 km orbit | 0.227 mGy/day |
| Chandrayaan-1 RADOM, 200 km orbit | 0.257 mGy/day |
| LRO CRaTER, 50 km orbit | 0.22 to 0.27 mGy/day |

### My own conversions (simple arithmetic, so check before citing)
| Time spent on the surface | Absorbed dose (silicon) |
|---|---|
| 1 day | about 0.32 mGy |
| 30 days | about 9.5 mGy |
| 1 year | about 116 mGy |

⚠️ These are doses in **silicon**, not the biologically weighted dose equivalent for human tissue. Dose equivalent (in sieverts) is higher, because some particles, especially neutrons, damage cells more. Do not read these numbers as astronaut limits.

---

## 3. What creates the radiation

```
        ☀️ Solar particle events  (sudden, can be dangerous)
              │
  ✨ Galactic cosmic rays (constant, from deep space)
              │
              ▼
   ┌───────────────────────────┐   ← no atmosphere, no magnetic field
   │      MOON SURFACE         │
   │  hits soil (regolith)     │
   └───────────┬───────────────┘
               ▼
   Secondary radiation: neutrons + gamma rays
               ▼
   ┌───────────────────────────┐
   │  ⛺ Habitat under soil     │   ← shielding helps, but must be designed
   └───────────────────────────┘
```

- **Constant:** galactic cosmic rays.
- **Sudden:** solar particle events.
- **Made on the Moon itself:** neutrons and gamma rays, produced when cosmic rays hit the soil.
- **Not important on the Moon:** Earth-style trapped radiation belts.
- Neutrons matter for health because they can damage DNA more than their dose number suggests.

---

## 4. What this means for living on the Moon (interpretation)

These points are my interpretation of the data, not NASA requirements.

1. **Plan for about twice ISS radiation.** Long stays need active monitoring, not guesses.
2. **Shield the base.** Burying the habitat under soil helps, but the soil also creates neutrons, so shielding design has to be tested, not assumed.
3. **Build a storm shelter.** Solar particle events are sudden, so every crew member needs a nearby, better-shielded place and clear rules for when to go there.
4. **Track every person's dose.** Each astronaut wears a dosimeter, and spacewalk time is limited when radiation is high.
5. **Use dose as a schedule.** Heavy outdoor work goes in low-radiation periods when forecasts allow.
6. **Neutrons can also help.** The same LND instrument measures thermal neutrons, which are a sign of **subsurface water**. That matters for finding ice for drinking water and crops (the South Pole-Aitken basin has shadowed regions where ice may exist).

**Open questions the data cannot answer yet:** only one instrument has measured the lunar surface directly, on the far side, at one location. Before it, surface knowledge came from computer models. No data in RadLab covers the south pole, a crater floor, or the inside of a buried habitat.

---

## 5. Pull the raw Moon data yourself (RadLab API)

Base address: `https://visualization.osdr.nasa.gov/radlab/api/` (GET requests). Add `format=csv`, `tsv`, `json` or `html`. Spaces must be written as `%20`.

**All Moon readings (dose rate, dose equivalent, flux):**
```
https://visualization.osdr.nasa.gov/radlab/api/?celestial_body=Moon&instrument_id&timestamp&absorbed_dose_rate&dose_equivalent_rate&flux&format=csv
```

**Only the surface instrument (LND):**
```
https://visualization.osdr.nasa.gov/radlab/api/?instrument=LND&timestamp&absorbed_dose_rate&format=json
```

**Boolean style (2022 only):**
```
https://visualization.osdr.nasa.gov/radlab/api/?query=(celestial_body=Moon)AND(timestamp>=2022-01-01)AND(timestamp<2023-01-01)&fields+=absorbed_dose_rate&format=csv
```

**Python:**
```python
import pandas as pd
url = ("https://visualization.osdr.nasa.gov/radlab/api/"
       "?celestial_body=Moon&instrument_id&timestamp&absorbed_dose_rate&format=csv")
df = pd.read_csv(url)
print(df.groupby("instrument_id")["absorbed_dose_rate"].describe())
```

Rules from the manual: don't mix key-value and `query=` syntax; `AND`/`OR` must be uppercase; `instrument_id` and ISO timestamps are case-sensitive.

---

## 6. Sources
- NASA OSDR RadLab Data API manual: visualization.osdr.nasa.gov/radlab/gui/data-api/
- RadLab release history (Moon datasets): visualization.osdr.nasa.gov/radlab/kb/Release_history
- Zhang et al. 2020, *First measurements of the radiation dose on the lunar surface*, Science Advances 6, eaaz1334
- Wimmer-Schweingruber et al., *The Lunar Lander Neutron and Dosimetry (LND) Experiment on Chang'E 4* (arXiv:2001.11028)
- Xu et al., LND proton measurements (arXiv:2209.05831)
- Cite RadLab as: "Data and visualizations are courtesy of the RadLab Portal, https://visualization.osdr.nasa.gov/radlab/gui/overview/"

*Crew roles and habitat ideas in this project are fictional concepts. Radiation numbers are real measurements, but limited. Always check the latest NASA guidance.*
