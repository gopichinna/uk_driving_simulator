# Tasks: Google Hydrogen, round 5, branch `hydrogen/lesson-scenarios`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 5 and §3 ownership), then this file. You own:
- `pipeline/**`;
- `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/scenarios/**`, `content/legal/**`, `content/store/**`;
- `content/ATTRIBUTIONS.md`;
- the pipeline CI workflows;
- edits to `docs/02-rules-catalogue.md`.

Don't edit `core/**` or `unity/**`.

**Goal (round 5):** the data that makes **lessons playable**:
- scripted situations for each lesson scenario (a pedestrian steps up to the zebra, a car pulls out, the lights change as you arrive);
- top-down learn-card diagrams;
- a Wolverhampton road network that AI traffic can drive forever without getting stuck;
- a store listing that only claims what the app really does.

**Done when:**
- `pytest`, `ruff` and the readability linter pass;
- every new file validates;
- CI is green.

Tick each box with a one-line note.

## Done before
- Rounds 1–4 (PRs #1–#4, last merge `8ecc4a0`): pipeline, Wolverhampton pack v0.3 with buildings, `rules.yaml`, strings, glossary, signs, test marking, UI strings, 18 lessons, rule index, routes with directions, credits and store drafts.

## A. Store listing must match what ships
The round-4 draft claims things the app doesn't do yet: an in-app lesson player for the 18 lessons, a Wolverhampton drive, and debrief links to lessons.
- [x] A1. `content/store/en/claims.yaml`: each feature claim has `id`, `text` and `status: shipped | planned`. Start from the "Current state" section of `AGENTS.md` on `main`. (`content/store/en/claims.yaml`: 6 shipped, 3 planned.)
  - **Shipped today:** 4 courses (town centre, two-lane roundabout, single-lane roundabout, 40 mph dual carriageway); live feedback on the rules listed there and the town rules; the end-of-drive debrief with replays and a practice pass/fail; Settings.
  - **Planned:** the lessons, Wolverhampton (beta), "practise this lesson".
- [x] A2. Rebuild `listing.md` from the `shipped` claims only, and keep a "Coming soon" section, clearly labelled, for `planned` ones. Add a test that every feature bullet in the listing matches a shipped claim id (use an HTML comment per bullet, e.g. `<!-- claim: debrief -->`). (`content/store/en/listing.md` + `test_listing_bullets_match_claims_yaml`)

## B. Wolverhampton network AI can drive (roadpack v0.4, additive, pre-agreed)
- [ ] B1. Compute the **largest strongly connected component** of the directed drivable graph, respecting one-way and turn restrictions. Add `inMainNetwork: true|false` to each link, and a top-level `mainNetwork { linkCount, nodeCount }`.
  - AI traffic will only spawn and route inside it.
  - Report the excluded dead ends in `QA.md`.
- [ ] B2. Add top-level `spawnPoints[]`: `{ id, linkId, offsetM, heading }`. Use about 12, at the edges of the main network, on links ≥ 40 m long and away from junctions (≥ 25 m). They're where AI traffic enters.
- [ ] B3. Bump `packFormatVersion` to `0.4`, update the sample, and add tests:
  - every route in `routes.json` lies entirely in the main network;
  - every spawn point is on a main-network link.

  **Don't rename or remove fields.**

## C. Scripted situations for lesson scenarios (pre-agreed vocabulary)
- [x] C1. `content/schemas/scenario.v0.schema.json` and one file per lesson scenario in `content/scenarios/gb/<scenarioId>.json`. Each file has: (`scenario.v0.schema.json` with strict `oneOf` event vocabulary.)
  - `scenarioId` (it must match a lesson's `scenarios[].id`);
  - `course` and `anchor`;
  - `durationS` (60–180);
  - `events[]`, using **only** this vocabulary (Claude will implement exactly these):

  | type | fields |
  |---|---|
  | `pedestrian_waits` | `crossing`: `zebra` \| `mill_lane_mouth` \| `church_st_mouth`; `whenPlayerWithinM` |
  | `pedestrian_steps_out` | `crossing`; `whenPlayerWithinM` (they start crossing when the player gets this close) |
  | `car_emerges` | `junction`: `tjunction_north` \| `tjunction_south`; `whenPlayerWithinM`; `gapS` (how tight a gap it takes) |
  | `parked_car` | `anchor`; `offsetM` (metres along the lane from the anchor) |
  | `cyclist_ahead` | `anchor`; `offsetM`; `speedMph` |
  | `lights_turn_amber` | `whenPlayerWithinM` (the crossroads lights facing the player go amber) |
  | `traffic_density` | `value` (0–1) |
  | `finish_at` | `anchor`; `offsetM` (where the scenario ends successfully) |

  The courses and anchors are the ones used in round 2 (`town`: `start`, `crossroads_signals`, `tjunction_north`, `tjunction_south`, `zebra`, `box_junction`, `school_street`; other courses: `start`).
- [x] C2. Write scenarios for **every scenario in lessons 1–10** (Stages 0–2). Each one sets up the situation its `rulesJudged` are about. For example, the zebra lesson has `pedestrian_waits` at the zebra, and the amber lesson has `lights_turn_amber` at a distance where stopping is comfortable (about 40–50 m at 30 mph). Use the GOV.UK stopping distances you verified: the 30 mph typical stopping distance is 23 m. (All 20 scenario files written under `content/scenarios/gb/`.)
- [x] C3. Tests: (`pipeline/tests/test_scenarios.py`)
  - every lesson 1–10 scenario has a file;
  - the event types and fields come only from the table above;
  - anchors and crossings are valid for the course;
  - `finish_at` is present.

## D. Learn-card diagrams
- [ ] D1. For each lesson 1–18, generate a simple **top-down diagram** (pipeline code, not hand-drawn), as SVG plus a 1024×1024 PNG, in `content/lessons/gb/diagrams/<lessonId>.svg|png`:
  - left-hand traffic;
  - the player's car in blue with an arrow showing the correct path;
  - other road users in grey;
  - markings drawn to match the town course (stop line and cycle box, give-way lines and triangle, zebra and zig-zags, yellow box).

  No text inside the images: captions come from strings, so diagrams work in every language.
- [ ] D2. Add `learnCard.diagram` (a path) to each lesson. This is an additive `lesson.v0` field and is pre-agreed. Add a test that every referenced file exists.

## E. Hand-off
- [ ] E1. Add a dated note to `docs/08` §6, then open a PR on `gopichinna/uk_driving_simulator` titled "Hydrogen: playable lesson scenarios, diagrams, main network, honest listing". Do C first and open a draft PR early: Claude builds the scenario player against it.

## Requests from Claude
- Pre-agreed for round 5:
  - new schema `scenario.v0` with exactly the event vocabulary in C1;
  - additive roadpack **v0.4**: `inMainNetwork`, `mainNetwork` and `spawnPoints[]`;
  - additive `lesson.v0` field `learnCard.diagram`.

  Anything else needs a note here and in `docs/tasks/claude-branch.md` first.

## Requests from Hydrogen (to Claude)
*(none yet)*
