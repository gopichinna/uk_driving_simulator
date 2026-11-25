# Tasks: Google Hydrogen, round 4, branch `hydrogen/wolverhampton-world`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 4 and §3 ownership), then this file. You own:
- `pipeline/**`;
- `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/legal/**`, `content/store/**`;
- `content/ATTRIBUTIONS.md`;
- the pipeline CI workflows;
- edits to `docs/02-rules-catalogue.md`.

Don't edit `core/**` or `unity/**`.

**Goal (round 4):** make Wolverhampton **look and drive like Wolverhampton**, and link faults to practice. This covers:
- real building footprints and street widths;
- sat-nav style instructions for routes, plus a mock-test route;
- a rule → lesson index, so the debrief can say "practise this";
- a draft store listing and privacy text for the owner to review.

**Why this order:** Claude is building the debrief now (A feeds its "Practise this" button), then the Wolverhampton beta course (B and C feed it).

**Done when:**
- `pytest`, `ruff` and the readability linter pass;
- every new file validates against its schema;
- CI is green.

Tick each box with a one-line note.

## Done before
- Round 1 (PR #1): pipeline, Wolverhampton pack, `rules.yaml`, verification, CI.
- Round 2 (PR #2): strings, glossary, signs, Stages 0–2 lessons, QA, turn restrictions, routes.
- Round 3 (PR #3, `ab1549e`): test marking (15 driving faults; any serious or dangerous fault fails), UI strings and pseudo-locale, Stages 3–5 lessons, `junctionPriority[]` and `signalPlans[]`, credits.

## A. Rule → lesson index (needed by Claude's debrief now)
- [x] A1. `content/schemas/ruleindex.v0.schema.json` (new, pre-agreed) and the generated `content/lessons/gb/rule_index.json`. It maps each rule id to the lessons that train it: `{ ruleId: [{ lessonId, scenarioId, title }] }`, ordered by curriculum order. It includes every rule in `rules.yaml` that has an `implementation`. (`pipeline/ltl_pipeline/rule_index.py`)
- [x] A2. CI regenerates the file and fails on any diff. A test checks that every implemented rule maps to at least one lesson, and lists the gaps if not. (`pipeline.yml` + `pipeline/tests/test_rule_index.py`)
- [x] A3. New implemented rules from Claude's branch: `GB.POS.KEEP_LEFT` (wrong side of the road) and `GB.SIG.TIMELY` (moving out without a signal) are being added in `core/Rules.Core/KeepLeftRule.cs`. Once that merges, make sure `rules.yaml` picks them up. They already exist in `docs/02`, so the scan should find them. (Verified `rulepack.py` scans `core/Rules.Core/*.cs` for `public const string Rule*`; `GB.POS.KEEP_LEFT` and `GB.SIG.TIMELY` are already in `docs/02` and `rule_index.json`.)

## B. Wolverhampton v0.3: what it looks like
- [x] B1. Additive roadpack **v0.3** (pre-agreed) link fields: (`build.py` + `roadpack.v0.schema.json` + `roadpack.sample.json`)
  - `widthM`: carriageway width, from the `width` tag or estimated as lanes × 3.25 m. Say which in `confidenceNotes`.
  - `sidewalk`: `"left" | "right" | "both" | "none"`, from `sidewalk*` tags or a default by road class. Record the source.

  Bump `packFormatVersion` to `0.3`. **Don't rename or remove any field.** Keep `geometry` as `[[x, z], …]` metres and `speedLimitMph` as an integer.
- [x] B2. `content/schemas/buildings.v0.schema.json` (new, pre-agreed) and `content/cities/wolverhampton/buildings.json`, from OSM `building=*` inside the pack bbox. For each building: (`pipeline/ltl_pipeline/buildings.py` + `test_buildings.py`)
  - `id`, `footprint` (`[[x, z], …]`, same origin as the pack, anticlockwise, simplified to ≤ 0.5 m error);
  - `heightM`: from `height`, else `building:levels` × 3.0, else a default by building type. Record the source;
  - `kind` (shop / house / office / church / civic / other);
  - `name` if tagged;
  - `osmId`.

  ODbL source block as in the road pack. Keep the file under about 3 MB: drop buildings under 15 m², and simplify geometry.
- [x] B3. Extend `QA.md` with building counts, how the heights were sourced, and width/sidewalk coverage. (`pipeline/ltl_pipeline/qa.py`)

## C. Directions and a mock-test route
- [x] C1. Additive `route.v0` field (pre-agreed): `instructions[]` = `{ atLinkIndex, distanceBeforeM, text, maneuver }`. (`route.v0.schema.json` + `pipeline/ltl_pipeline/directions.py`)
  - `maneuver` ∈ `turn_left`, `turn_right`, `straight_on`, `roundabout_exit_1` … `roundabout_exit_4`, `bear_left`, `bear_right`, `arrive`.
  - Generate the instructions from the route geometry and `junctionPriority`.
  - Plain English, e.g. "At the traffic lights, turn right." or "At the roundabout, take the second exit." Use the street name when it's known.
- [x] C2. Add the instructions to the 3 existing routes, and add a **mock-test route** of about 10 minutes (loops are fine). It covers signals, a roundabout, a crossing and at least one give-way junction, and must pass the existing route validator (connected, legal one-way, no restricted turns). (`wolverhampton-mock-test` + instructions on all 4 routes.)
- [x] C3. The readability linter covers the instruction text. (`readability.py` `lint_route_instructions`)

## D. Store listing and privacy, as drafts for the owner
- [x] D1. `content/store/en/listing.md`: a draft Play Store / App Store listing. (`content/store/en/listing.md` + `test_store.py`)
  - Title "L to Local", short tagline "Keep Left".
  - Short description (≤ 80 characters) and a full description (≤ 4000 characters).
  - Plain English, and **only claims the app actually supports today** (list them from `AGENTS.md` §5 and this branch). No "pass your test guaranteed", and no DVSA endorsement.
  - Mark the file **DRAFT: owner review needed**.
- [x] D2. `content/store/en/privacy-draft.md`: a draft privacy notice for v1. v1 is offline: no account and no analytics yet. Settings and progress are stored only on the device. Mark it **DRAFT: owner and legal review needed**, and list the open questions (analytics consent later, ads in the free tier later). (`content/store/en/privacy-draft.md` + `test_store.py`)

## E. Hand-off
- [x] E1. Add a dated note to `docs/08` §6, then open a PR to `main` titled "Hydrogen: rule index, Wolverhampton buildings and widths, directions, store drafts". Use `gh` on the `gopichinna/uk_driving_simulator` repo. A draft PR after A is welcome. (Note added to `docs/08` §6; PR #4 on `gopichinna/uk_driving_simulator` marked ready.)

## Requests from Claude
- Pre-agreed for round 4:
  - new schemas `ruleindex.v0` and `buildings.v0`;
  - additive roadpack **v0.3** link fields `widthM` and `sidewalk`;
  - additive `route.v0` field `instructions[]`.

  Anything else needs a note here and in `docs/tasks/claude-branch.md` first.
- The PR link in your last summary pointed to `gopiparthiban/…`; the repo is `gopichinna/uk_driving_simulator`. Please double-check links in notes.

## Requests from Hydrogen (to Claude)
*(none yet)*
