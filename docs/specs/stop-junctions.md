# STOP Junctions Investigation (`GB.JCT.STOP_SIGN`, Highway Code Rule 171)

**Checked against OSM:** `west-midlands-latest.osm.pbf` (2026-11-26 Geofabrik extract, ODbL © OpenStreetMap contributors).  
**Highway Code:** Rule 171 (`https://www.gov.uk/guidance/the-highway-code/using-the-road-159-to-203`, checked 2026-11-27).

---

## 1. Root Cause in `pipeline/ltl_pipeline/build.py`
In `suburb_practice/roadpack.json`, node `n_4506177992` (`stop: True`) did **not** receive a `junctionPriority` entry with `controlledBy: "stop"` because `build.py` filtered candidate nodes with:
```python
is_junction = node["kind"] in ("junction", "roundabout", "mini_roundabout") or node.get("trafficSignals")
```
In OpenStreetMap, `highway=stop` is almost always tagged on a **degree-2 approach node** a few metres before the intersection stop line (`kind == "end"` or `"plain"`), rather than on the 3+-arm intersection node itself. Because `is_junction` checked `node.get("trafficSignals")` but omitted `node.get("stop")` and `node.get("giveWay")`, degree-2 STOP nodes were skipped before reaching `elif node.get("stop"): controlled_by = "stop"`.

**Fix applied (R34.2):** Updated `is_junction` in `pipeline/ltl_pipeline/build.py` to include `or node.get("stop") or node.get("giveWay")`, and rebuilt `content/cities/suburb_practice/roadpack.json` so `n_4506177992` now emits `controlledBy: "stop"` in `junctionPriority[]`.

---

## 2. Real West Midlands `highway=stop` Junctions in OSM
Scanning the entire West Midlands extract (`west-midlands-latest.osm.pbf`) found **277** nodes tagged `highway=stop`. Most are on private/service exits (`highway=service`), minor residential streets, or signal-failure/cycle crossings, while a smaller subset are genuine UK public-road STOP junctions (verified from OSM `source=survey` / council aerial imagery tags and road hierarchy):

1. **Old Hill, Tettenhall (`suburb_practice` pack — OSM node `999605090` / `4506177992`):**
   - `52.59847, -2.16378` — `unclassified` (`Old Hill`), `highway=stop`, `stop=minor`, `direction=backward`.
2. **Upper Villiers Street, Wolverhampton (OSM node `3605794936`):**
   - `52.56781, -2.13467` — `tertiary` (`Upper Villiers Street`), `highway=stop`, `direction=forward`, `source=survey`.
3. **Pennhouse Avenue, Wolverhampton (OSM node `1516056765`):**
   - `52.56167, -2.15189` — `residential` (`Pennhouse Avenue`), `highway=stop`, `stop=minor`, `direction=backward`.
4. **Titford Road / Trinity Street (`B4170`), Oldbury (OSM nodes `310423570` & `1071401819`):**
   - `52.49368, -2.01421` & `52.49342, -2.01408` — `secondary` (`B4170`), `highway=stop`, `direction=backward`.
5. **Vicarage Road / Creynolds Lane, Solihull / Warwickshire (OSM nodes `3752683867` & `3752683868`):**
   - `52.37595, -1.81467` — `tertiary`, `highway=stop`, `source=Warwickshire CC Aerial Imagery 2013`.
6. **Saint Martins Queensway / Moor Street Queensway (`B4100`), Birmingham (OSM nodes `82665815`, `4231387770`):**
   - `52.47850, -1.89332` — `unclassified` (`B4100`), `highway=stop`, `source=survey`.

---

## 3. Scenarios Added
Extended `suburb_practice` (`content/cities/suburb_practice/roadpack.json` rebuilt with `controlledBy: "stop"`) and added two `scenario.v0` practice scenarios in `content/scenarios/gb/`:
- `s3_suburb_stop_pass.json`: come to a complete standstill behind the solid white stop line on `suburb_practice`, then pull out when clear.
- `s3_suburb_stop_roll.json`: rolling through the STOP line without coming to a full halt (demonstrates `GB.JCT.STOP_SIGN` serious fault).
