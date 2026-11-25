# Tasks: Google Hydrogen, branch `hydrogen/osm-pipeline-rule-pack`

**Goal:** see `docs/08-parallel-work-plan.md` §2. Read `AGENTS.md` first. You own `pipeline/**`, `content/countries/**`, `content/cities/**`, the pipeline CI workflows, and verification edits to `docs/02-rules-catalogue.md`. Don't edit `core/**` or `unity/**`.

Work through these in order. Tick each box when it's done and add a one-line note (commit hash or what changed).

## A. Pipeline foundation
- [ ] A1. `pipeline/pyproject.toml` (Python 3.12+): deps `osmium` (pyosmium), `shapely`, `pyproj`, `jsonschema`, `pyyaml`; dev deps `pytest`, `ruff`. Package name `ltl_pipeline`.
- [ ] A2. `pipeline/ltl_pipeline/schema.py`: validate any road pack against `content/schemas/roadpack.v0.schema.json`. Add a test that `content/samples/roadpack.sample.json` validates.
- [ ] A3. `pipeline/ltl_pipeline/geo.py`: lat/lon → local metres (x = east, z = north) around a pack origin, using a local tangent plane or a pyproj transverse Mercator centred on the origin. Tests: round trip, and known distances within 0.5 m over 1 km.
- [ ] A4. `.github/workflows/pipeline.yml`: on PRs and `main`, run `ruff` and `pytest` in `pipeline/`, plus schema validation of everything in `content/`.

## B. OSM → RoadPack v0 for Wolverhampton
- [ ] B1. `pipeline/ltl_pipeline/fetch.py`: download the Geofabrik `west-midlands-latest.osm.pbf` into `pipeline/cache/` (gitignored), checksum it, and record the extract timestamp. No live Overpass calls in the app; one-off pipeline fetches are fine.
- [ ] B2. `content/cities/wolverhampton/config.yaml`: a **bbox of about 1 km²** covering Wolverhampton city centre inside or along the ring road (A4150), with at least one signalised junction, 2–3 T-junctions, a zebra or light-controlled crossing, and ideally one ring-road roundabout. **Exclude streets with West Midlands Metro tram tracks** (`railway=tram`); trams are out of scope. Record the bbox, the reason for it, and the excluded ways.
- [ ] B3. `pipeline/ltl_pipeline/extract.py`: clip the pbf to the bbox and keep `highway=*` drivable classes (motorway…residential, service optional). Keep these tags:
  - `lanes`, `lanes:forward`, `lanes:backward`, `turn:lanes*`, `oneway`
  - `maxspeed`, `junction=roundabout`
  - `busway`, `bus:lanes`, `cycleway*`
  - `highway=traffic_signals`, `crossing=*`, `crossing_ref=*` (zebra/pelican/puffin/toucan), `highway=crossing`
  - `name`, `ref`
- [ ] B4. `pipeline/ltl_pipeline/build.py`: build the link-level graph:
  - Split ways at junctions, and give nodes `kind` (junction / roundabout / mini_roundabout / crossing / end).
  - Speed limits: use the tagged `maxspeed`, otherwise the GB defaults from `content/countries/gb/speed_defaults.yaml` (create it: built-up/restricted road 30 mph in England, single carriageway 60, dual carriageway 70; source Highway Code rule 124). Set `speedLimitSource`.
  - Crossings (type from tags) and traffic signals.
  - Zones: bus lanes with `:conditional` times when tagged; 20 mph zones.
- [ ] B5. **Confidence** (0–1) per link and per crossing, with a short `confidenceNotes`. For example: explicit `lanes` and `turn:lanes` = high; lanes inferred from road class = low; crossing type missing = low. Document the scoring in `pipeline/README.md`.
- [ ] B6. Output `content/cities/wolverhampton/roadpack.json` (pretty JSON, deterministic ordering) and make sure it validates. Include the `source` block: Geofabrik extract name, timestamp, bbox, licence `ODbL-1.0`, attribution `© OpenStreetMap contributors`.
- [ ] B7. Tests built from tiny hand-made `.osm` XML fixtures in `pipeline/tests/fixtures/`, covering: a T-junction split, maxspeed default vs tagged, zebra detection, roundabout node kind, a tram-street exclusion, and deterministic output.
- [ ] B8. `pipeline/README.md`: how to run, what each stage does, the confidence rules, and the **ODbL obligations**. Derived packs are ODbL, and pack data must be offered on request, per `docs/04` §7.1.

## C. Machine-readable GB rule pack
- [ ] C1. `pipeline/ltl_pipeline/rulepack.py`: parse the tables in `docs/02-rules-catalogue.md` into entries that conform to `content/schemas/rulepack.v0.schema.json`, and write `content/countries/gb/rules.yaml`. It must be **regenerable**: re-running it gives the same file.
- [ ] C2. Fill the `implementation` field for rules that already exist in code. Find them by scanning `core/Rules.Core/*.cs` read-only for `RuleId`/`Rule*` string constants such as `"GB.RBT.GIVE_WAY_RIGHT"`. In CI, fail if code uses a rule id that isn't in `rules.yaml`.
- [ ] C3. Tests: every catalogue row appears in the output; ids are unique and match `^GB\.[A-Z]+\.[A-Z0-9_]+$`; `force` ∈ {MUST, should, info}; each `source.url` is an https gov.uk URL (or marked `UNVERIFIED`).

## D. Verification (web research, GOV.UK sources only)
- [ ] D1. Resolve every item in `docs/02` §5 "Open verification list":
  - typical **stopping distances** (from the GOV.UK stopping-distances PDF);
  - Highway Code **rule 160** (keep left) and **rule 129** (double white line exceptions);
  - **rule 239** (door opening / Dutch reach);
  - the **Traffic signs** page;
  - **smart motorway** policy and naming;
  - **penalty amounts** (red X, mobile phones);
  - **DVSA practical-test fault categories**, from the public GOV.UK page;
  - **rules 222–223** (buses).

  Update the rows (✅/⚠️/❌ and notes) and record the date checked. If something can't be verified, leave it `UNVERIFIED` and say why. **Don't guess.**
- [ ] D2. `.github/workflows/highway-code-watch.yml`: a weekly job that fetches `https://www.gov.uk/guidance/the-highway-code/updates`, compares the newest entry date with `content/countries/gb/highway-code-version.txt`, and opens a GitHub issue if it changed.

## E. Hand-off
- [ ] E1. Update `docs/08` §6 with a dated note of what was delivered and any contract questions.
- [ ] E2. Open a PR to `main` titled "Hydrogen: OSM pipeline, Wolverhampton road pack, GB rule pack".

## Requests from Claude
- The C# loader on `claude/town-centre-junctions` reads `geometry` as `[[x, z], …]` in metres and `speedLimitMph` as an integer. Please keep both exactly as in the schema.
- If you need a schema change, add it here **and** in `docs/tasks/claude-branch.md` → "Requests from Hydrogen", and don't change `content/schemas/` until it's agreed.

## Requests from Hydrogen (to Claude)
*(none yet)*
