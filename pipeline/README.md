# L to Local Content Pipeline (`ltl_pipeline`)

Offline Python pipeline that turns OpenStreetMap (OSM) extracts and the markdown rules catalogue (`docs/02-rules-catalogue.md`) into validated JSON/YAML packs for the C# simulation core.

## Setup

Requires Python 3.12+.

```bash
cd pipeline
python -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"
```

## Running

### 1. Build the Wolverhampton RoadPack

```bash
python -m ltl_pipeline.cli build-city wolverhampton
```

Stages:
1. **`fetch.py`**: Downloads Geofabrik `west-midlands-latest.osm.pbf` into `pipeline/cache/` (gitignored), verifies its `.md5` checksum, and records `extractTimestamp` in `pipeline/cache/*.meta.json`.
2. **`extract.py`**: Clips to the bounding box in `content/cities/wolverhampton/config.yaml`, keeps drivable `highway=*` classes and relevant traffic tags, and excludes streets carrying West Midlands Metro tramways (`railway=tram`).
3. **`build.py`**: Splits ways at junctions, crossings, signals, and roundabouts; projects coordinates to local metres (`x = east, z = north`) via `geo.py` (`LocalProjection`); resolves untagged speed limits from `content/countries/gb/speed_defaults.yaml`; scores confidence; and validates against `content/schemas/roadpack.v0.schema.json`.

### 2. Generate the GB RulePack

```bash
python -m ltl_pipeline.rulepack
```

Parses `docs/02-rules-catalogue.md`, scans `core/Rules.Core/*.cs` (read-only) for implemented `GB.*` rule IDs, writes `content/countries/gb/rules.yaml`, and validates it against `content/schemas/rulepack.v0.schema.json`.

### 3. Tests & Linting

```bash
ruff check .
pytest
```

## Confidence Scoring

Every link and crossing has a `confidence` in `[0.0, 1.0]` and (for links) `confidenceNotes`:

- **Links** start at `0.45`:
  - `+0.25` if `lanes` or `lanes:forward`/`lanes:backward` is explicitly tagged in OSM.
  - `+0.15` if `turn:lanes*` is explicitly tagged.
  - `+0.15` if `maxspeed` is explicitly tagged (otherwise `default` from `speed_defaults.yaml`).
- **Crossings**:
  - `0.95` when `crossing_ref` is explicitly tagged (e.g. `zebra`, `pelican`, `puffin`, `toucan`).
  - `0.85` when `crossing=zebra`, `traffic_signals`, or `parallel`.
  - `0.65` when `crossing=uncontrolled` or `marked`.
  - `0.40` when `highway=crossing` has no crossing type tag (`signals_unknown` / `uncontrolled`).

## ODbL Licensing Obligations

Per `docs/04-architecture.md` §7.1:

- City `roadpack.json` files derived from OpenStreetMap are **Derivative Databases** under the **ODbL-1.0** licence.
- Every OSM-derived pack includes `"licence": "ODbL-1.0"` and `"attribution": "© OpenStreetMap contributors"` in its `source` block.
- Any manual fixes applied to OSM-derived city packs are also ODbL-1.0.
- The ODbL-derived pack data (or the pipeline and config to reproduce it) must be offered on request under ODbL-1.0, and attribution must appear on the free-drive screen, in credits, and in store listings.
