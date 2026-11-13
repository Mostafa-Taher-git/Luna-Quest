# Luna Academy — NASA API Quickstart

## 1. Search PDS for lunar terrain

```bash
curl -G 'https://pds.nasa.gov/api/search/1/products' \
  --data-urlencode 'keywords=Moon LRO LOLA elevation DEM' \
  --data-urlencode 'limit=20'
```

## 2. Search NASA lunar images

```text
https://images-api.nasa.gov/search?q=Moon%20South%20Pole%20LRO&media_type=image
```

## 3. Read Moon Trek WMTS map tiles

Use the tile URL template and product-specific `WMTSCapabilities.xml`:

```text
https://{WMTS endpoint}/1.0.0/{Style}/{TileMatrixSet}/{TileMatrix}/{TileRow}/{TileCol}.{extension}
```

Example documented global WAC tile:

```text
https://trek.nasa.gov/tiles/Moon/EQ/LRO_WAC_Mosaic_Global_303ppd_v02/1.0.0/default/default028mm/0/0/0.jpg
```

## 4. Calculate Sun geometry with Horizons

Endpoint: `https://ssd.jpl.nasa.gov/api/horizons.api`

Important request settings:

```text
COMMAND='10'                 # Sun
EPHEM_TYPE='OBSERVER'
CENTER='coord@301'            # coordinate site relative to Moon body 301; validate syntax
COORD_TYPE='GEODETIC'
SITE_COORD='EAST_LONGITUDE,LATITUDE,ALTITUDE_KM'
START_TIME='2026-11-13'
STOP_TIME='2026-11-14'
STEP_SIZE='30m'
```

Check the full parameter reference before production integration:
https://ssd-api.jpl.nasa.gov/doc/horizons.html

## 5. Diviner archive

Start at:
https://pds-geosciences.wustl.edu/missions/lro/diviner.htm

The archive's current listed EDR/RDR coverage ends 2026-06-15 for the latest bundle shown in this research snapshot. Check current PDS labels before importing updated records.
