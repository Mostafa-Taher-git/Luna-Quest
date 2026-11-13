# 🌕 Living on the Moon: Life-Science Data Guide
*Moon-relevant findings from NASA's Open Science Data Repository (OSDR) and the studies around it*

> **Important note.** The link you gave (`osdr.nasa.gov/bio/repo/search?...data_type=study`) is a search page that builds its list with JavaScript. When I opened it, it returned **no study list**, so I could not filter it directly. Instead I searched for OSDR records and the papers behind them and kept only what relates to the Moon. Only the studies marked **[OSDR]** are confirmed OSDR records. Others are published papers or preprints that give context. To browse OSDR yourself, see Section 7.

---
the api = https://osdr.nasa.gov/bio/repo/search?q=&data_source=cgene,alsda,esa&data_type=study
## 1. The big picture in 6 lines

| Topic | What the research shows |
|---|---|
| 🌱 Growing food in Moon soil | Plants **do** grow in real lunar soil, but slowly and under stress |
| 🧪 Moon soil problems | Shards of volcanic glass, **no organic matter**, metals and salts that stress plants |
| 🥔 Fixes | Mixing in compost (vermicompost) greatly improved growth in simulant soil (early preprint) |
| 🦴 Gravity (1/6 of Earth) | Ground simulations show muscle and bone loss, so daily exercise is required |
| ☢️ Radiation | Surface dose is about 2× the ISS (see the earlier radiation guide) |
| ❓ Biggest gap | Nobody has yet grown a plant or lived long-term on the lunar surface |

---

## 2. Growing crops in real Moon soil

### ⭐ Key study: Plants grown in Apollo lunar regolith **[OSDR: OSD-476]**
Paul, Elardo & Ferl, *Communications Biology*, May 2022 (University of Florida).

| Detail | Finding |
|---|---|
| Plant | *Arabidopsis thaliana* (a small mustard-family plant) |
| Moon soil used | Real samples from **Apollo 11, 12 and 17** |
| Amount | About **1 gram of soil per plant** (900 mg per well) |
| Control | JSC-1A, a volcanic-ash Moon-soil simulant made on Earth |
| Germination | **Every seed sprouted**, and all plants looked the same until about **day 6** |
| After day 6 | Lunar-soil plants grew **more slowly**, had **stunted roots**, and some had stunted leaves with **reddish pigment** |
| Which soil was worst | **Apollo 11** samples (the most weathered soil) gave the least robust plants |
| What genes showed | Stress responses like those to **salt, metal and reactive oxygen** |

**Why it matters:** the authors say lunar soil can help grow plants in lunar habitats, but it is **not a harmless soil**. The stress will need to be understood and reduced.

### Why Moon soil is hard for plants
- It contains **shards of volcanic glass** and **no organic material**.
- It is constantly hit by radiation.
- Samples from different places behave differently, so the base site matters.

### Possible fix: compost (early preprint, not yet peer reviewed)
A 2026 bioRxiv study on **potatoes** tested five lunar soil simulants. It found that adding **vermicompost** at a 70:30 mix (simulant to compost) made growth virtually indistinguishable from the control, and the authors call soil-based farming "viable". Heavy metals were a keyword to watch. Treat it as promising, not proven.

### Related OSDR study (not Moon, but useful method)
**OSD-670:** CI asteroid soil simulant tested with lettuce, radishes and peppers mixed with peat moss. It crusts and compacts and causes drought stress. A reminder that "space soil" needs mixing and watering care.

---

## 3. Living in 1/6 gravity (0.16 g)

No human has lived for months at lunar gravity, so this evidence comes from **Earth-based simulations**:

| Study type | Result |
|---|---|
| Mouse partial-weight-bearing model (first built 2010) | About **20% lower calf muscle mass** and about **24% lower thigh-bone volume** |
| Rat hindlimb simulator (Moon 1/6 g, Mars 3/8 g, weightlessness) | Muscle mass fell in **all three** groups, most in weightlessness. "Partial gravity is insufficient to preserve normal muscle" |
| Bone-cell culture in a Random Positioning Machine (Moon 0.16 g, Mars 0.38 g) | Short-term (6 days) bone-cell growth showed a **dose-dependent reduction** at lower gravity |
| Related OSDR study **OSD-616** (Mars-level 0.4 g, rats) | Examines how sex hormones affect muscle loss in partial gravity |

**Caution:** these use animals or cells and *simulate* reduced gravity. Results suggest the Moon is **better than weightlessness but not safe enough** to skip exercise.

---

## 4. What this means for the crew (my interpretation)

| Crew role | Takeaway from the data |
|---|---|
| 🌿 Farmer / Food lead | Test soil from the exact site. Mix in compost. Start with hydroponics as a backup. Watch for metal and salt stress |
| ⚕️ Medic | Plan daily resistance exercise, bone and muscle checks, and dose tracking |
| 🏗️ Builder | Bury habitats and greenhouses, because radiation also hits plants |
| 🔬 Geologist | Soil type matters: Apollo 11-type old soil was the worst. Map soil before farming |
| 🧑‍🚀 Commander | Schedule exercise like a safety task |

### 3D-style view of a lunar farm bay (cross-section)
```
          ☀️  ☢️ radiation from above
   ═══════════════════════════════  ← 2-3 m regolith shield
  ┌───────────────────────────────┐
  │  💡 LED lights (red + blue)   │  ← top layer
  │  🥬🥔🌿 crop trays            │  ← middle layer
  │  🧪 regolith + compost mix    │  ← soil layer (tested for metals)
  │  💧 water + nutrient pipes    │  ← bottom layer
  └───────────────────────────────┘
  gravity here: 1/6 of Earth
```

---

## 5. What we still don't know
- No plant has grown on the **actual lunar surface** (all tests are on Earth).
- Real lunar soil is rare, so tests are tiny (about 1 g per plant) and many use simulants.
- Gravity data comes from rodents and cells, not astronauts.
- South pole soil (where Artemis is aiming) has not been grown in.

---

## 6. Sources
- Paul A.-L., Elardo S.M., Ferl R. (2022), *Plants grown in Apollo lunar regolith present stress-associated transcriptomes…*, Communications Biology. Data: OSDR **OSD-476**, GEO GSE188852, BioProject PRJNA780518
- NASA feature "Scientists Grow Plants in Lunar Soil" (nasa.gov)
- bioRxiv 2026, *Growth and molecular responses of potato to lunar regolith simulants* (preprint)
- OSDR **OSD-670**: CI asteroid regolith as a plant growth medium
- OSDR **OSD-616**: gonadal hormones and muscle in partial gravity
- Braveboy-Wagner & Lelkes, simulated partial gravity and 7F2 osteoblasts (PMC9174291)
- Frontiers in Cell and Developmental Biology (2022), new partial-gravity apparatus for rat hindlimbs
- PMC7599661, review of rodent partial weight-bearing models

## 7. How to explore OSDR yourself
1. Open `https://osdr.nasa.gov/bio/repo/search` in a browser (needs JavaScript).
2. Search words: **lunar**, **regolith**, **partial gravity**, **Moon**, **plant**.
3. Open a study by ID, e.g. `https://osdr.nasa.gov/bio/repo/data/studies/OSD-476`.
4. The `data_source=cgene,alsda,esa` filter in your link appears to select OSDR data sources. I could not check what each one means.

*Crew names and habitat ideas elsewhere in this project are fictional. Study results are real but limited. Check the original papers before using numbers in school or research work.*
