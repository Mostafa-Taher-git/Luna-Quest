# 🛰️🌕 Moon Toolbox: Which NASA APIs Help a Moon Colony
*The Moon-relevant parts of NASA Open APIs (api.nasa.gov)*

> **Important note.** The page you gave (`api.nasa.gov`) only shows a **sign-up form for a free API key**. Its list of APIs loads with JavaScript, and I got nothing more from it. So I built this guide from public descriptions of the NASA Open APIs. I filtered the whole list down to what helps with the Moon, and I did **not** call any live API from here. The code is untested.

---
the api=https://api.nasa.gov/
## 1. Quick filter: every NASA API and its Moon value

| NASA API | Moon value | What it gives a Moon project |
|---|---|---|
| 🗺️ **Moon Trek (WMTS map tiles)** | 🔴 **Very high** | Real maps of the Moon: global mosaics, plus north and south polar views |
| 🖼️ **NASA Image & Video Library** | 🔴 **Very high** | Apollo photos, Artemis images, videos, audio, searchable by keyword |
| ☀️ **DONKI (space weather)** | 🔴 **Very high** | Solar flares, particle storms, CMEs (see the space weather guide) |
| 🧬 **GeneLab / OSDR** | 🟠 High | Life-science data, such as plants grown in Apollo soil (see the life-science guide) |
| 🔧 **TechPort** | 🟠 Medium | Catalog of NASA technology projects (life support, power, mining) |
| 📡 **Satellite Situation Center (SSC)** | 🟡 Medium | Lists of spacecraft and ground stations. I did not verify which lunar spacecraft it includes |
| 🌠 **APOD** | 🟡 Low | Daily astronomy picture, sometimes the Moon |
| ☄️ **SSD/CNEOS, NeoWs** | ⚪ Low | Asteroids and comets, not the Moon itself |
| 🛰️ **TLE** | ⚪ None | Orbits of **Earth-orbiting** satellites only |
| 🌍 **EPIC, EONET, Earth** | ⚪ None | Earth views and Earth events |
| 🔴 **Mars Rover Photos, InSight** | ⚪ None | Mars only |
| 🪐 **Exoplanet Archive, TechTransfer** | ⚪ None | Other planets and patents |

*Ratings are my own judgment.*

---

## 2. The most useful one: Moon Trek maps

NASA's Trek services serve **Moon, Mars and Vesta** maps as map tiles (WMTS). The service is maintained by NASA's SSERVI and JPL's Trek team. Public descriptions say **no API key is needed**.

**URL pattern:**
```
https://trek.nasa.gov/tiles/Moon/EQ/{Layer}/1.0.0/{Style}/{TileMatrixSet}/{TileMatrix}/{TileRow}/{TileCol}.jpg
```

**Example (whole-Moon mosaic from the Lunar Reconnaissance Orbiter):**
```
https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02/1.0.0/default/default028mm/0/0/0.jpg
```

**Layer details file:**
```
https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02/1.0.0/WMTSCapabilities.xml
```

**How zoom works (equirectangular maps):** every zoom level doubles the tiles each way.

| Zoom | Columns × Rows | Tiles |
|---|---|---|
| 0 | 2 × 1 | 2 |
| 1 | 4 × 2 | 8 |
| 2 | 8 × 4 | 32 |
| 3 | 16 × 8 | 128 |
| 4 | 32 × 16 | 512 |
| 5 | 64 × 32 | 2,048 |

- **Polar maps:** Moon Trek also has stereographic views centered on the **north and south poles**. The south pole is where Artemis is aiming, so those are the maps for base planning.
- **Missing tile = no data** for that area, not necessarily an error.
- Full Moon layer list: `https://trek.nasa.gov/tiles/apidoc/trekAPI.html?body=moon`

---

## 3. Image and Video Library (Apollo, Artemis, base ideas)

No key is needed according to public descriptions. Base address: `https://images-api.nasa.gov/`

```
https://images-api.nasa.gov/search?q=lunar%20base&media_type=image
https://images-api.nasa.gov/search?q=apollo%2017&media_type=image
https://images-api.nasa.gov/asset/as11-40-5874      (example asset ID: Apollo 11 image)
```

Good searches for this project: `lunar base`, `moon regolith`, `south pole`, `Artemis`, `lunar greenhouse`, `Apollo 17`.

---

## 4. How each API helps a colony crew (my interpretation)

| Crew role | API they would use | For what |
|---|---|---|
| 🧑‍✈️ Commander | DONKI | Daily storm forecast, go / no-go for outdoor work |
| 🔬 Geologist | Moon Trek | Pick landing sites, ice crater routes, base location |
| 🚙 Rover lead | Moon Trek | Plan safe routes with polar maps |
| 🌿 Farmer | GeneLab / OSDR | Read soil and plant experiment data |
| 🏗️ Builder / Engineer | TechPort | Find NASA tech projects for building, power, life support |
| 🎓 Teacher / Kids game | Image Library | Real photos and videos for lessons |
| 📡 Comms engineer | DONKI + SSC | Space weather alerts and spacecraft contact info |

---

## 5. Ready-to-run code (untested)

```python
import requests

KEY = "DEMO_KEY"   # get a free key at api.nasa.gov (DEMO_KEY is limited)

# 1) Search NASA images for a lunar base
r = requests.get("https://images-api.nasa.gov/search",
                 params={"q": "lunar base", "media_type": "image"})
for item in r.json()["collection"]["items"][:5]:
    d = item["data"][0]
    print(d["title"], "|", d.get("date_created", "")[:10])

# 2) Download the whole-Moon map tile (zoom 0, left half)
tile = ("https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02/"
        "1.0.0/default/default028mm/0/0/0.jpg")
open("moon_tile_0_0.jpg", "wb").write(requests.get(tile).content)

# 3) Recent solar particle events (space weather)
s = requests.get("https://api.nasa.gov/DONKI/SEP",
                 params={"startDate": "2026-10-14", "endDate": "2026-11-13",
                         "api_key": KEY})
print(s.status_code, len(s.json()) if s.ok else "no data")
```

---

## 6. Idea for the 3D project: use the real Moon map
The 3D Moon in your handbook uses a drawn texture. You could replace it with the **real LRO map tiles** from Moon Trek (zoom 0 gives the two halves of the Moon). I have not tested whether the tile server allows loading from a web page, so check that first. If it does not, download the tiles and bundle them with the page.

---

## 7. Limits and cautions
- No live API calls were made, so nothing here shows current data.
- Some API status notes from a third-party wrapper say a few services were down or removed (Earth imagery backend, TechTransfer, TLE, and TechPort returning errors). Check each one before building on it.
- Key limits: `DEMO_KEY` is about 30 requests per hour and 50 per day. A free personal key allows more.
- Respect NASA media rules when reusing images.

---

## 8. Sources
- NASA Open APIs portal: api.nasa.gov (key sign-up)
- Public API directory listing NASA APIs, including the Vesta/Moon/Mars Trek WMTS services
- NASA Trek WMTS documentation: trek.nasa.gov/tiles/apidoc/trekAPI.html?body=moon
- Third-party Python wrapper listing which services need keys (pypi: nasa-apis-wrapper)
- NASA Image and Video Library API (images-api.nasa.gov)

*The crew, base and roles in this project are fictional concepts. Always check the official NASA API pages before using any endpoint in a real product.*
