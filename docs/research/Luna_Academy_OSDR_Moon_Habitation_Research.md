# 🌕 LUNA ACADEMY
## NASA OSDR — Moon Habitation & Space-Biology Research Brief

> **Document type:** Curated scientific research brief for game design  
> **Version:** 1.0  
> **Compiled:** 13 November 2026  
> **Project:** Luna Academy — Single-player, open-world lunar-colony simulator  
> **Primary source:** [NASA Open Science Data Repository (OSDR)](https://osdr.nasa.gov/bio/repo/)  
> **Requested filtered catalogue:** [OSDR Study search — GeneLab, ALSDA, ESA](https://osdr.nasa.gov/bio/repo/search?q=&data_source=cgene,alsda,esa&data_type=study)

---
the api= https://osdr.nasa.gov/bio/repo/search?q=&data_source=cgene,alsda,esa&data_type=study
## Executive Summary

This brief filters NASA OSDR study records for biological and operational topics that matter when humans live for extended periods in a lunar habitat. The highest-value findings fall into five areas:

1. **Growing food in lunar material:** Apollo-returned regolith has been tested with plants. Plants can germinate, but raw lunar regolith is stressful and is not a ready-made fertile soil.
2. **Closed-loop crop production:** Antarctic greenhouse and ISS hydroponic/aeroponic demonstrations provide useful analogue data for growing food, recycling nutrient solutions and checking food safety.
3. **Water and habitat hygiene:** ISS studies show why water loops, wet surfaces, filters, pipes and occupied modules need monitoring for biofilms and microbial/chemical changes.
4. **Crew health:** Bed-rest analogues and human spaceflight studies support exercise, health monitoring and long-term medical research as core habitat systems.
5. **Scientific uncertainty:** The Moon is not the ISS, Antarctica or a laboratory. Every analogue must be labelled as an analogue and not presented as a direct lunar measurement.

### What OSDR can and cannot tell Luna Academy

| OSDR is useful for | OSDR is not a complete source for |
|---|---|
| Plant biology, regolith–plant interaction, crop growth research | Lunar temperature maps or local illumination maps |
| Human, animal, plant and microbial biology in space/analogue studies | Surface radiation dose-rate maps by lunar location |
| Closed-habitat microbiome and water-system biological risks | Dust grain-size distributions and electrostatic behaviour |
| Physiology, exercise, tissue chips and health monitoring research | Regolith bearing strength, slope stability and construction loads |
| Experiment methods, samples, metadata and study publications | A complete engineering design for a lunar base |

Use dedicated lunar mission instruments, geospatial products and engineering references for those physical variables. This document deliberately does not invent missing values.

---

## 1. Scope and Filtering Method

### 1.1 Starting query

The user-provided URL filters the OSDR catalogue to `data_type=study` and requests the data-source selectors `cgene` (GeneLab), `alsda` and `esa`.

**Important source terminology:** NASA's public OSDR materials describe GeneLab and ALSDA as the core OSDR data systems and also describe federated search across repositories such as NIH GEO and EBI PRIDE. The reviewed public API reference does not clearly define `esa` as a universal OSDR source code in the same way as `cgene`/`alsda`. Therefore, this brief identifies each selected record by its displayed accession and verified OSDR record; it does not assume every record is ESA-origin. Always confirm the source badge on the live record before making a source-agency claim.

### 1.2 Topic filters used

The blank keyword query was narrowed conceptually using these terms and related concepts:

- `lunar`, `Moon`, `Apollo lunar regolith`, `lunar surface fines`, `simulated lunar soil`
- `plant`, `seed germination`, `greenhouse`, `hydroponic`, `aeroponic`, `food production`
- `water processor`, `biofilm`, `microbiome`, `closed habitat`, `surface swab`
- `spaceflight`, `bed rest`, `muscle`, `exercise`, `blood`, `immune`, `neuro-ocular`
- `radiation` and combined space-stressor models, where the study's actual biological scope is relevant

### 1.3 Selection rules

A study was prioritised when it met at least one of the following:

- Tested actual lunar-returned material.
- Tested a technology or biological process that could support a closed habitat.
- Measured human/biological responses to spaceflight or a relevant Earth analogue.
- Helps create a credible educational system, NPC storyline or maintenance activity.

The result is a **curated shortlist of high-value study records**, not a complete census of every record returned by OSDR and not a re-analysis of every raw FASTQ, image, assay or spreadsheet file. Metadata and published findings were reviewed; exact raw-file re-analysis remains a separate research task.

### 1.4 Evidence tags

- 🟢 **A — Direct lunar-material evidence:** actual Apollo-returned lunar material was part of the study.
- 🔵 **B — Spaceflight/ground analogue:** ISS, Antarctic station, bed-rest or laboratory analogue. Relevant, but not a Moon measurement.
- 🟠 **C — Model-system evidence:** animal, cell, tissue-chip or ex-vivo research. Useful for learning and research mechanics; not a clinical conclusion about lunar crew.

---

## 2. 3D Moon Outpost Reference Map

![Isometric concept map of a lunar research outpost](assets/lunar_outpost_isometric.svg)

*The illustration is a game-design concept, not a surveyed lunar site or NASA engineering plan. Use it to connect research systems to the places where the player interacts with them.*

### Suggested mapping from research to game facilities

| 3D facility | Research-backed reason to include it | Example gameplay |
|---|---|---|
| Greenhouse | Apollo regolith plant studies; EDEN ISS; XROOTS | Run seed trials, adjust nutrient delivery, inspect crop health |
| Water & life-support room | ISS Water Processor Assembly biofilm study | Detect stagnation, inspect filters, collect samples and maintain a loop |
| Hygiene / sample stations | ISS environmental microbiome mapping | Swab a surface, map a hotspot, compare zones and schedule cleaning |
| Medical & exercise module | 70-day bed-rest exercise study; astronaut biomarker research | Build a routine, compare trends, discuss why exercise matters |
| Science laboratory | OSDR study methods across plant, microbial and tissue research | Form a hypothesis, set a control, record observations, interpret evidence |
| Mission-control data wall | Multi-system data integration | Compare trends and decide which issue needs attention first |

---

# 3. Priority Studies — Direct Lunar Material

## 🟢 OSD-476 — Plants grown in Apollo lunar regolith

**Title:** *Plants grown in Apollo lunar regolith present stress-associated transcriptomes that inform prospects for lunar exploration*  
**Source:** GeneLab / OSDR  
**Release date:** 16 May 2022  
**Catalogue update:** 20 October 2026  
**Organism:** *Arabidopsis thaliana* (a model plant)  
**Material:** Returned regolith from Apollo 11, Apollo 12 and Apollo 17  
**Access:** [NASA OSDR OSD-476](https://osdr.nasa.gov/bio/repo/data/studies/OSD-476) · [Data.gov record](https://catalog.data.gov/dataset/plants-grown-in-apollo-lunar-regolith-present-stress-associated-transcriptomes-that-inform) · [GeneLab visualisation](https://visualization.genelab.nasa.gov/data/OSD-476)

### What was found

- The model plant germinated and grew in several Apollo regolith samples.
- Growth was challenging and slow.
- Plant gene-expression patterns indicated ionic stress, and many plants displayed severe stress morphologies.
- The authors concluded that lunar regolith is not a benign ready-to-use substrate; its plant interactions need to be understood and likely mitigated.

### Why it matters for living on the Moon

A lunar greenhouse cannot simply scoop up raw surface soil and expect normal crops. The colony will need controlled growing media, nutrient management, water chemistry, plant-health monitoring and scientific tests before relying on local material.

### Luna Academy implementation

- Add a **Regolith Research Bench** with three sample profiles: Earth growth medium, lunar simulant and Apollo-regolith-inspired profile.
- Let the player run a controlled seed experiment with a comparison group.
- Add measurable plant states: germination, growth, leaf condition, nutrient status and stress indicators.
- NPC botanist Amina explains why a control sample is necessary before deciding that a treatment worked.

**Do not claim:** this proves food crops can be mass-produced directly in untreated lunar soil. It tested a model plant and shows significant stress.

---

## 🟢 OSD-883 — Vegetable seedlings in Apollo 14 lunar surface fines

**Title:** *Analysis of Vegetable Seedlings Grown in Contact with Apollo 14 Lunar Surface Fines*  
**Source:** ALSDA / OSDR  
**Release date:** 15 August 2025  
**Plants:** Cabbage, lettuce, radish and carrot  
**Exposure period described in catalogue:** 14–21 days  
**Methods:** Germination/growth assessment and spectrochemical analyses  
**Access:** [NASA OSDR OSD-883](https://osdr.nasa.gov/bio/repo/data/studies/OSD-883) · [OSDR filtered results](https://osdr.nasa.gov/bio/repo/search?data_source=alsda&data_type=study&q=Apollo%2014)

### What it adds

The study documents a direct chemical interaction between germfree vegetable seedlings and Apollo 14 lunar fines. It is particularly useful because it involves familiar food plants rather than only a small model plant.

### Luna Academy implementation

- Build a **sample-to-lab workflow**: collect a lunar sample → label it → prepare a control → grow seedlings → analyse plant material → write a conclusion.
- Use a simple mineral-analysis interface where the player compares sample readings.
- Teach that detecting an element in a plant is not automatically proof that the plant is nutritious or safe to eat.

**Data caution:** use the complete study files before turning individual element concentrations into gameplay thresholds. The catalogue summary alone is not enough for that.

---

## 🟠 OSD-856 — Lettuce explants and simulated lunar soil

**Title:** *Effect of varying amounts of simulated lunar soil batch #005 upon growth of lettuce explants*  
**Source:** ALSDA / OSDR  
**Release date:** 30 July 2025  
**Organism:** Lettuce (*Lactuca sativa*)  
**Material:** Simulated lunar soil batch #005 — not returned lunar soil  
**Access:** [NASA OSDR OSD-856](https://osdr.nasa.gov/bio/repo/data/studies/OSD-856)

### Use in the game

This is a good research activity for comparing different proportions of simulant and growth medium. It teaches experimental design and iteration: test, observe, compare and revise.

**Do not claim:** this is a validated recipe for growing edible lettuce on the Moon. It is a laboratory assay using tissue/explant material and a simulant.

---

## 🟠 OSD-855 — Apollo 17 seed-germination challenge

**Title:** *Apollo 17 Challenge System — Seed Germination (Cabbage, Brussels Sprouts, Radish and Lettuce)*  
**Source:** ALSDA / OSDR  
**Release date:** 18 June 2025  
**Access:** [NASA OSDR OSD-855](https://osdr.nasa.gov/bio/repo/data/studies/OSD-855)

This record is useful as a historical research activity concerning the effects of Apollo 17 lunar materials on seed germination and plant development. Public catalogue metadata is not enough to justify precise numerical outcomes. For game implementation, use it to teach controls, observation logs and the difference between a research question and a proven result.

---

# 4. Priority Studies — Food Production and Greenhouse Systems

## 🔵 OSD-895 — EDEN ISS Antarctic space greenhouse

**Title:** *Biomass Production of the EDEN ISS Space Greenhouse in Antarctica During the 2018 Experiment Phase*  
**Source:** ALSDA / OSDR  
**Initial OSDR release:** 28 September 2026  
**Environment:** Antarctic analogue facility near Neumayer III Station  
**Operational window:** 7 February–20 November 2018  
**Access:** [NASA OSDR OSD-895](https://osdr.nasa.gov/bio/repo/data/studies/OSD-895) · [Original open-access paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC7264257/)

### Key reported values

| Measure | Published value | Meaning for the game |
|---|---:|---|
| Operational phase | 286 days | A greenhouse must be operated continuously, not just planted once |
| Fresh edible biomass | 268 kg | Food production can be tracked as a real output |
| Cultivation area | About 12.5 m² | Space-efficient agriculture is a design challenge |
| Total productivity | 21.44 kg/m² | Useful historical analogue metric |
| Time-normalised productivity | 0.075 kg/(m²·day) | Useful for a simple yield-per-area-per-day comparison |

### Luna Academy implementation

Build a living greenhouse simulation with:

1. Crop choice and planting schedule.
2. Lighting and light-period management.
3. Water and nutrient-solution supply.
4. Temperature and humidity monitoring.
5. Plant health and pest/microbe inspection.
6. Harvest and food-safety checks.
7. Waste plant material handling and resource recovery.

**Critical limitation:** Antarctica is an Earth environment with normal gravity, atmospheric pressure and Earth logistics. These results are a strong greenhouse operations analogue, not a forecast of lunar crop yields.

---

## 🔵 OSD-773 — XROOTS hydroponic and aeroponic demonstration on the ISS

**Title:** *Exposed Root On-Orbit Test System (XROOTS)*  
**Source:** GeneLab / OSDR  
**Release:** 11 May 2026  
**Catalogue update:** 20 October 2026  
**Environment:** International Space Station, microgravity  
**Access:** [NASA OSDR OSD-773](https://osdr.nasa.gov/bio/repo/data/studies/OSD-773) · [Data.gov record](https://catalog.data.gov/dataset/exposed-root-on-orbit-test-system-xroots-an-aeroponic-and-hydroponic-plant-growth-system-t)

### What was tested

XROOTS demonstrated aeroponic and hydroponic plant-growth methods integrated with Veggie hardware. The system evaluated nutrient delivery with an ebb-and-flow nutrient-solution recovery system. Harvested plants were returned for food-safety, microbiome and chemistry evaluation.

### Luna Academy implementation

- Allow the greenhouse to be configured as hydroponic, aeroponic or substrate-based.
- Model pumps, nutrient tanks, filters, return lines, leak sensors and root-zone health.
- A failed pump should affect nutrient delivery and plant health; the player can diagnose the cause.
- Add a food-safety inspection before harvested crops are placed into the colony kitchen.

**Critical limitation:** microgravity is not lunar gravity (about one-sixth of Earth's). Fluid handling and root behaviour require Moon-specific validation.

---

# 5. Priority Studies — Water, Hygiene and Closed Habitats

## 🔵 OSD-734 — The ISS microbial and chemical environment

**Title:** *The International Space Station Has a Unique and Extreme Microbial and Chemical Environment Driven by Use Patterns*  
**Source:** GeneLab / OSDR  
**Study publication:** Cell, 3 April 2025  
**Environment:** Nine sampled ISS modules during Expedition 64  
**Access:** [NASA OSDR OSD-734](https://osdr.nasa.gov/bio/repo/data/studies/OSD-734) · [Open-access paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC12068931/)

### Important counts and observations

- 803 environmental samples in total: 737 surface swabs and 66 controls.
- Nine ISS modules were sampled.
- Microbial and chemical signatures differed by module and human use patterns.
- The research used 3D spatial mapping, sequencing and metabolomics to understand habitat surfaces.

### Luna Academy implementation

Create a 3D habitat hygiene overlay:

- Different rooms have different occupancy and surface-contact patterns.
- The player or an NPC can take a surface swab and label the location.
- A map shows where samples were taken and whether follow-up cleaning or sampling is needed.
- The kitchen, sleeping quarters, science laboratory, airlock and water plant should have different cleaning schedules.

**Important teaching point:** microbes are not all dangerous. The game teaches sampling, cleanliness, contamination control and evidence-based response—not fear of every microorganism.

---

## 🔵 OSD-729 — Biofilm in an ISS water-processor isolate

**Title:** *Biofilm formation is correlated with low nutrient and simulated microgravity conditions in a Burkholderia isolate from the ISS water processor assembly*  
**Source:** GeneLab / OSDR  
**Release date shown in OSDR:** 4 August 2026  
**Access:** [NASA OSDR OSD-729](https://osdr.nasa.gov/bio/repo/data/studies/OSD-729)

### Why it matters

The record concerns an isolate associated with the ISS Water Processor Assembly and investigates biofilm formation under low-nutrient and simulated-microgravity conditions. Intermittent dormancy in part of a wastewater recycling system makes stagnation and maintenance relevant issues.

### Luna Academy implementation

- Display water-loop flow, filter status, maintenance history and sample alerts.
- Teach why stagnant sections and neglected filters need inspection.
- Give the player choices: inspect, flush using the approved procedure, collect a sample, replace a filter or request a specialist.
- The NPC engineer explains how a biofilm differs from a simple mineral blockage.

**Do not claim:** this one isolate defines a universal human-health threshold or proves that a particular organism will occur in a lunar base.

---

## 🔵 OSD-554 — Biofilms on engineering materials

**Title:** *Characterization of Biofilm Formation, Growth, and Gene Expression on Different Materials and Environmental Conditions in Microgravity*  
**Source:** GeneLab / OSDR  
**Release date:** 24 August 2023  
**Organism:** *Pseudomonas aeruginosa* biofilm model  
**Access:** [NASA OSDR OSD-554](https://osdr.nasa.gov/bio/repo/data/studies/OSD-554)

### Luna Academy implementation

Use this research theme to explain why material selection matters in pipes, tanks, valves, seals and equipment surfaces. Different surfaces may be easier or harder to inspect and clean. The player learns preventive maintenance and why engineers avoid dead zones and inaccessible plumbing.

**Limit:** Do not assign material performance rankings unless the complete study tables have been extracted and checked.

---

# 6. Priority Studies — Human Health and Long-Duration Crew Routines

## 🔵 OSD-942 — Exercise during 70-day bed rest

**Title:** *Exercise Training Attenuates the Muscle Mitochondria Genomic Response to Bed Rest*  
**Source:** GeneLab / OSDR  
**Release date:** 27 June 2024  
**Participants:** 22 healthy adult men  
**Environment:** 70-day, six-degree head-down-tilt bed-rest analogue  
**Access:** [NASA OSDR OSD-942](https://osdr.nasa.gov/bio/repo/data/studies/OSD-942) · [PubMed publication](https://pubmed.ncbi.nlm.nih.gov/38650118/)

### Study design

| Group | Participants | Condition |
|---|---:|---|
| Bed rest only | 7 | No exercise countermeasure |
| Bed rest + standard devices | 7 | Aerobic and resistance training |
| Bed rest + flywheel | 8 | Aerobic and resistance training using a flywheel |

The study protocol included aerobic training six days per week (15–35 minutes per session) and resistance training three days per week (35–60 minutes per session). The study reports that exercise countermeasures attenuated losses in aerobic capacity, muscle mass, strength and endurance.

### Luna Academy implementation

- Make the exercise module a real habitat room with resistance equipment and a schedule board.
- NPCs have routines and can remind one another to exercise.
- Players learn to plan work, rest and exercise rather than treating exercise as a cosmetic mini-game.
- Show a simple fictional trend graph for strength or fitness, with explanations rather than medical diagnoses.

**Critical limitation:** bed rest is an Earth-based analogue, not a perfect model of lunar partial gravity. Do not copy the protocol as a real astronaut prescription.

---

## 🔵 OSD-53 — Changes in astronaut blood gene expression

**Title:** *Spaceflight Modulates Gene Expression in Astronauts*  
**Source:** GeneLab / OSDR  
**Initial release:** 15 October 2013; metadata updated 19 September 2024  
**Participants:** Six astronauts on short-duration missions  
**Samples:** Whole blood, around 10 days before launch and 2–3 hours after landing  
**Access:** [NASA OSDR OSD-53](https://osdr.nasa.gov/bio/repo/data/studies/OSD-53) · [Publication](https://www.nature.com/articles/s41526-016-0039)

### What was measured

A 234-gene stress-response panel was used. The description reports changes involving DNA repair, oxidative stress and protein folding/degradation.

### Luna Academy implementation

- Use it as inspiration for a medical dashboard that compares baseline and follow-up measurements.
- Teach that health monitoring uses multiple indicators over time.
- Make the medical NPC explain that one result does not diagnose a disease.

**Limit:** These were short-duration missions and samples taken soon after return; they are not long-duration lunar-health thresholds.

---

## 🔵 OSD-684 — 3D human muscle tissue chips in space

**Title:** *Human skeletal muscle tissue chip autonomous payload reveals changes in fiber type and metabolic gene expression due to spaceflight*  
**Source:** GeneLab / OSDR  
**Release:** 7 March 2024; metadata update 18 February 2026  
**Environment:** 3D human-derived muscle myobundles flown to the ISS with ground controls  
**Access:** [NASA OSDR OSD-684](https://osdr.nasa.gov/bio/repo/data/studies/OSD-684) · [Publication](https://www.nature.com/articles/s41526-023-00322-y)

### Luna Academy implementation

The science laboratory can contain a tissue-chip demonstration that shows how researchers study cells in a controlled micro-environment. This is ideal for explaining why cells, tissues, animals and whole humans are different levels of biological evidence.

**Limit:** A tissue chip is not a whole astronaut and cannot predict an individual crew member's health outcome.

---

## 🔵 OSD-575 — Inspiration4 blood-serum measurements

**Title:** *SpaceX Inspiration4 Blood Serum Metabolic Panel and Immune/Cardiac Cytokine Arrays*  
**Source:** ALSDA / OSDR  
**Release:** 14 May 2024  
**Participants:** Four private astronauts on a three-day low-Earth-orbit mission in September 2021  
**Samples:** Multiple pre-flight and post-flight serum time points  
**Access:** [NASA OSDR OSD-575](https://osdr.nasa.gov/bio/repo/data/studies/OSD-575)

### Luna Academy implementation

Use this as an example of longitudinal monitoring. A doctor looks at a crew member's baseline, an in-flight reading and follow-up readings; the story teaches that trends and context matter more than a single measurement.

**Limit:** Four participants and a three-day mission cannot represent long-term lunar residence.

---

## 🟠 OSD-817 — Human eye-tissue model for SANS research

**Title:** *The ex vivo human translaminar autonomous system to study spaceflight-associated neuro-ocular syndrome pathogenesis*  
**Source:** ALSDA / OSDR  
**Release:** 23 July 2026  
**Model:** De-identified human donor eye tissue in a 14-day ground-based pressure analogue  
**Access:** [NASA OSDR OSD-817](https://osdr.nasa.gov/bio/repo/data/studies/OSD-817) · [Publication](https://www.nature.com/articles/s41526-022-00232-5)

### Luna Academy implementation

A medical science side activity can introduce eye-health monitoring and show that long-duration spaceflight research includes the visual system. Make the activity educational and non-diagnostic.

**Limit:** This is an ex-vivo model investigating mechanisms; it is not a clinical diagnostic test and does not establish a lunar-specific condition.

---

# 7. The Most Important Game Systems to Build From This Research

| Priority | Game system | Research basis | What it should simulate |
|---:|---|---|---|
| 1 | Greenhouse and crop science | OSD-476, OSD-883, OSD-895, OSD-773 | Crop cycles, nutrients, lighting, root systems, yield and food safety |
| 2 | Water recycling and filter maintenance | OSD-729, OSD-773 | Flow, recovery, filter inspection, stagnant sections and sampling |
| 3 | Habitat hygiene map | OSD-734, OSD-554 | Occupancy zones, sample points, cleaning and material inspection |
| 4 | Exercise and crew schedule | OSD-942 | Routine, resistance exercise, recovery and fitness trends |
| 5 | Medical monitoring | OSD-53, OSD-575, OSD-684, OSD-817 | Baselines, checkups, data trends and limits of evidence |
| 6 | Science journal | All selected records | Hypothesis, control group, sample label, observation and conclusion |

## 7.1 A sample integrated gameplay event

**Incident: Greenhouse output is falling.**

1. The plant NPC notices slower growth and changes in leaves.
2. The player checks the grow-room dashboard.
3. A nutrient-return pump is found to have intermittent flow.
4. The player inspects the plumbing and sees a maintenance alert.
5. A sample is collected from the return line.
6. The water specialist explains biofilms and why stagnation matters.
7. The player repairs or replaces the relevant component, then checks the flow again.
8. The botanist compares crop-health observations over subsequent game days.
9. The science journal records what was observed and what remains uncertain.

**Learning outcomes:** systems thinking, biology, water chemistry, engineering maintenance, observation, responsibility and evidence-based decisions.

---

# 8. Scientific Accuracy Rules for Luna Academy

1. **Never silently convert an analogue into a lunar fact.** Label each datum as Apollo lunar material, ISS microgravity, Antarctic analogue, Earth bed rest, animal model, cell model or ex-vivo tissue model.
2. **Never use a single study to invent safety limits.** A study design or finding is not automatically an operational threshold.
3. **Show uncertainty.** Some effects are still being studied; the game should let NPC scientists say “we do not know yet”.
4. **Keep measurement types separate.** For example, a surface swab is not a crew blood test; plant tissue chemistry is not food-safety approval; a cell model is not a human clinical result.
5. **No unsupported medical treatment advice.** Children learn why monitoring and professional procedures matter, not how to diagnose or treat real people.
6. **Do not confuse Moon gravity with microgravity.** The Moon's surface gravity is approximately one-sixth of Earth's; ISS experiments occur in microgravity.
7. **Treat local resources as variable.** Apollo samples represent a few sites and missions; lunar soil and ice conditions vary by location.
8. **Build fictional gameplay values separately from scientific source values.** If game balance needs simplified numbers, mark them as “gameplay tuning”, not NASA measurements.

---

# 9. What This OSDR Filter Does Not Cover — Add Other NASA Data Sources

OSDR is focused on space biology and space-relevant biological, physiological, behavioral and omics datasets. For a complete Moon environment simulation, Luna Academy needs additional source families:

| Needed for the game | Better source family to add | Why OSDR alone is insufficient |
|---|---|---|
| Lunar radiation dose and particle environment | CRaTER/LRO, RAD instrument archives, space-weather datasets | OSDR is not a full lunar radiation mapping service |
| Surface temperature and illumination | Diviner/LRO, LOLA topography, orbital illumination products | Need location- and time-specific lunar measurements |
| Water-ice evidence and volatile distribution | LRO, LCROSS, neutron spectrometers and lunar prospecting studies | Biological studies do not map ice deposits |
| Dust properties and electrostatic behaviour | Apollo dust studies, lander/rover measurements and tribology studies | Need particle-size, adhesion and abrasion data |
| Terrain, slopes and construction locations | LOLA/SLDEM and landing-site engineering studies | Need elevation and terrain hazard layers |
| Habitat structure and life-support design | NASA ECLSS, Human Research Program and engineering reports | OSDR biological records do not specify a complete habitat design |

The previous Luna Academy RadLab research note should be used alongside this brief for radiation-related research, but RadLab and OSDR are distinct data products.

---

# 10. Data Access and Reproducibility

### Main OSDR search

[Open the user's filtered OSDR study search](https://osdr.nasa.gov/bio/repo/search?q=&data_source=cgene,alsda,esa&data_type=study)

### NASA OSDR Biological Data API

[API documentation](https://visualization.osdr.nasa.gov/biodata/api/) describes REST access to study/sample metadata and query interfaces for filtering data and metadata, with outputs such as CSV/TSV and JSON.

### NASA OSDR FAQ

[OSDR FAQ](https://science.nasa.gov/reference/osdr-faq/) explains the search facets (data source, data type, project type, assay type, organism, tissue and factor) and the access rules. Most OSDR data are public; some astronaut-derived sequence data are controlled access even when processed data are public.

### Recommended follow-up data workflow

1. Open each OSDR accession link above.
2. Download the study metadata package first.
3. Review study description, sample table, factors, assays, protocols and publication.
4. Download only the data files required for the game mechanic being designed.
5. Keep a provenance record: accession, version, download date, source file, unit and evidence class.
6. Before inserting numeric data in the game, independently verify units, sample counts, controls and whether the figure is measured or modelled.

---

# 11. Recommended Research-to-Game Data Schema

Use a structure like the following for every science-backed mechanic:

```json
{
  "mechanic_id": "greenhouse.regolith_trial",
  "title": "Lunar regolith plant trial",
  "source_accessions": ["OSD-476", "OSD-883", "OSD-856"],
  "evidence_class": "direct_lunar_material_and_analogue",
  "measured_variable": "plant_growth_and_stress_response",
  "game_variables": [
    "germination_rate",
    "growth_rate",
    "leaf_health",
    "nutrient_status",
    "sample_provenance"
  ],
  "gameplay_values_are_simplified": true,
  "scientific_thresholds_verified": false,
  "player_learning": [
    "experimental_controls",
    "plant_biology",
    "regolith_is_not_earth_soil",
    "evidence_and_uncertainty"
  ]
}
```

### Data provenance rule

Every numeric constant in the simulation must store:

- `source_accession` or external source URL;
- unit;
- measurement context;
- evidence class;
- confidence/limitation note;
- whether it is a real measured value or a game-balancing parameter.

---

# 12. Final Recommendations

## Build first

1. **Greenhouse research loop** — the strongest direct lunar-material and operational-agriculture evidence in this shortlist.
2. **Water recycler and microbial maintenance** — a high-value colony system with clear NPC stories and real maintenance concepts.
3. **3D habitat hygiene map** — OSD-734 is especially valuable because it already demonstrates spatial microbial mapping across a space habitat.
4. **Crew exercise / health routine** — build a humane, non-punitive system informed by bed-rest and astronaut-health research.
5. **Scientific journal** — let the child record hypotheses, controls, measurements and conclusions.

## Best NPC story hooks from this data

- **Amina (botanist):** argues that raw lunar soil is not ready for crops; asks the player to run a controlled test instead of guessing.
- **Omar (engineer):** investigates a water-loop flow problem and explains filters, stagnant sections and biofilms.
- **Sofia (medical officer):** teaches routine monitoring, exercise, rest and why health trends need context.
- **Leo (robotics/science technician):** operates sampling tools, labels surfaces and builds a 3D contamination map.
- **Maya (commander):** chooses which maintenance activity gets crew time and energy first, asking the player to explain trade-offs.

These are fictional narratives inspired by research topics, not claims that those named characters took part in the real studies.

---

## References — Primary Sources

1. [NASA OSDR — OSD-476: Apollo lunar regolith and plant stress](https://osdr.nasa.gov/bio/repo/data/studies/OSD-476)
2. [Data.gov — OSD-476 full catalogue description](https://catalog.data.gov/dataset/plants-grown-in-apollo-lunar-regolith-present-stress-associated-transcriptomes-that-inform)
3. [NASA OSDR — OSD-883: Apollo 14 vegetable seedlings](https://osdr.nasa.gov/bio/repo/data/studies/OSD-883)
4. [NASA OSDR — OSD-856: simulated lunar soil and lettuce](https://osdr.nasa.gov/bio/repo/data/studies/OSD-856)
5. [NASA OSDR — OSD-855: Apollo 17 seed germination](https://osdr.nasa.gov/bio/repo/data/studies/OSD-855)
6. [NASA OSDR — OSD-895: EDEN ISS greenhouse](https://osdr.nasa.gov/bio/repo/data/studies/OSD-895)
7. [EDEN ISS 2018 biomass-production paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC7264257/)
8. [NASA OSDR — OSD-773: XROOTS](https://osdr.nasa.gov/bio/repo/data/studies/OSD-773)
9. [Data.gov — XROOTS catalogue record](https://catalog.data.gov/dataset/exposed-root-on-orbit-test-system-xroots-an-aeroponic-and-hydroponic-plant-growth-system-t)
10. [NASA OSDR — OSD-734: ISS microbial/chemical environment](https://osdr.nasa.gov/bio/repo/data/studies/OSD-734)
11. [Cell 2025 — ISS microbial and chemical 3D mapping paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC12068931/)
12. [NASA OSDR — OSD-729: water-processor biofilm study](https://osdr.nasa.gov/bio/repo/data/studies/OSD-729)
13. [NASA OSDR — OSD-554: material-surface biofilms](https://osdr.nasa.gov/bio/repo/data/studies/OSD-554)
14. [NASA OSDR — OSD-942: bed-rest exercise study](https://osdr.nasa.gov/bio/repo/data/studies/OSD-942)
15. [NASA OSDR — OSD-53: astronaut gene-expression study](https://osdr.nasa.gov/bio/repo/data/studies/OSD-53)
16. [NASA OSDR — OSD-684: human muscle tissue chip](https://osdr.nasa.gov/bio/repo/data/studies/OSD-684)
17. [NASA OSDR — OSD-575: Inspiration4 serum study](https://osdr.nasa.gov/bio/repo/data/studies/OSD-575)
18. [NASA OSDR — OSD-817: ex-vivo human eye tissue model](https://osdr.nasa.gov/bio/repo/data/studies/OSD-817)
19. [NASA OSDR Biological Data API](https://visualization.osdr.nasa.gov/biodata/api/)
20. [NASA OSDR FAQ](https://science.nasa.gov/reference/osdr-faq/)

---

**Document status:** Research shortlist for game design. It is not a human-health manual, a lunar habitat engineering specification, or an exhaustive meta-analysis of all OSDR files.
