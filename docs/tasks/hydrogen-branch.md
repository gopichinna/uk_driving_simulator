# Tasks: Google Hydrogen, round 7, branch `hydrogen/city-life-and-progression`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 7 and §3 ownership), then this file. You own:
- `pipeline/**`;
- `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/scenarios/**`, `content/legal/**`, `content/store/**`;
- `content/ATTRIBUTIONS.md`;
- the pipeline CI workflows;
- edits to `docs/02-rules-catalogue.md`.

Don't edit `core/**` or `unity/**`.

**Goal (round 7):**
- a cleaner Wolverhampton network;
- people walking its streets and using its crossings;
- crossing lights that give pedestrians a turn;
- the progression data (unlocks, stars, review schedule) that turns 18 lessons into a course.

**Context from play-testing:** Wolverhampton (beta) is on the phone. Roads are now drawn from the lane graph, but the pack has **113 links shorter than 10 m**, mostly where OSM splits a way at every tagged node. They make junctions fiddly and turn paths kinked.

**Done when:**
- `pytest`, `ruff` and the readability linter pass;
- every new file validates;
- CI is green.

Tick each box with a one-line note.

## Done before
- Rounds 1–6 (PRs #1–#6, last merge `551fb08`).

## A. Simplify the Wolverhampton network
- [ ] A1. In `build.py`, merge chains of links joined at **plain degree-2 nodes**: same direction rules, same lanes, same speed limit, same name/ref, and a node that is not a crossing, signal, give-way or restriction via-node. A merged link keeps the first link's id. Its `geometry` is the joined polyline, and `confidence` is the minimum of the parts.
- [ ] A2. Rewrite every reference so it stays consistent: `crossings[].linkId`, `restrictions`, `junctionPriority.majorLinks`, `signalPlans.stages`, `laneConnections`, `spawnPoints` and `routes.json`. Add tests for:
  - no dangling references;
  - every route still connected and legal;
  - link count reduced, with before and after numbers in `QA.md`.
- [ ] A3. Report the remaining links under 10 m in `QA.md`, and why each stays (for example, a real short link between two signal junctions).

## B. Pedestrians in Wolverhampton
- [ ] B1. `content/schemas/pedroutes.v0.schema.json` (new, pre-agreed) and `content/cities/wolverhampton/pedestrians.json`: 20–40 walking routes. Each route has:
  - `id`;
  - `points` (`[[x, z], …]`, the same origin as the pack, along footways at about 1.5 m outside the carriageway edge; use link `widthM` and `sidewalk`);
  - `crossings[]` = `{ crossingId, fromIndex, toIndex }` (the route points that walk across the road at a crossing from the pack).
- [ ] B2. Routes cross roads **only at mapped crossings** (zebra, pelican, puffin, toucan, or uncontrolled with confidence ≥ 0.7). Also include some routes that walk across **side-road mouths** (rule H2): add `mouthCrossings[]` = `{ nodeId, fromIndex, toIndex }` where a route crosses the end of a minor road at a give-way junction.
- [ ] B3. Tests:
  - points are within 6 m of a footway edge, except while crossing;
  - every crossing reference exists;
  - routes avoid building footprints (`buildings.json`).

## C. Pedestrian stages at signal crossings (roadpack v0.6, additive, pre-agreed)
- [ ] C1. For `signalPlans` at nodes that are signal-controlled pedestrian crossings (pelican, puffin or toucan crossings, or a crossing node with `traffic_signals`), add `pedestrianStageS` (e.g. 8), and `crossingIds[]` for the crossings it serves.
  - Keep confidence ≤ 0.5 (inferred).
  - Bump `packFormatVersion` to `0.6`. **Don't rename or remove fields.**

## D. Progression: from lessons to a course
- [x] D1. `content/schemas/progression.v0.schema.json` (new, pre-agreed) and `content/lessons/gb/progression.json`: (`progression.v0.schema.json` + `progression.json`: Stages 0–5, 1–3 stars, 1/3/7/14-day review cap 3/day, freeTier lessons 1–3.)
  - `stages[]` = `{ stage, title, lessonIds[], unlockAfter: { stage, minStars } }`;
  - `stars`: rules for 1–3 stars from a lesson attempt, e.g. 1 = finished, 2 = test-style pass (from `test_marking.yaml`), 3 = no faults and the quiz all correct;
  - `review`: a spaced-repetition schedule for rules the player got wrong (e.g. review after 1, 3, 7 and 14 days), with a cap of 3 review drives per day;
  - `freeTier`: lesson ids 1–3 (must match `lesson.freeTier`).
- [x] D2. Tests: (`pipeline/tests/test_progression.py`)
  - every lesson appears exactly once;
  - unlocks reference earlier stages only (no cycles);
  - the free lessons match the lesson files;
  - star rules reference `test_marking.yaml` fields that exist.
- [x] D3. Plain-English UI strings for the progression screens in `ui.json` (with en-XA regenerated): (Added all 11 `progress.*` and `lesson.*` keys to `ui.json`, `en-XA`, and `test_readability.py`.)
  - `progress.title`
  - `progress.stage_locked`, `progress.unlock_hint` ("Get {stars} stars in Stage {stage} to unlock")
  - `progress.stars`
  - `progress.review_due` ("{count, plural, one {# review drive} other {# review drives}} ready")
  - `lesson.start`, `lesson.next`, `lesson.retry`, `lesson.quiz_title`, `lesson.correct`, `lesson.incorrect`

## E. Hand-off
- [ ] E1. Add a dated note to `docs/08` §6, then open a PR on `gopichinna/uk_driving_simulator` titled "Hydrogen: simpler Wolverhampton, pedestrians, crossing stages, progression". Do D first (Claude builds the lesson flow next) and open a draft PR early. Double-check that the PR link in your summary uses `gopichinna`.

## Requests from Claude
- Pre-agreed for round 7:
  - new schemas `pedroutes.v0` and `progression.v0`;
  - roadpack **v0.6** `pedestrianStageS` and `crossingIds[]` on signal plans;
  - link merging in A, as long as every reference is rewritten consistently.

  Claude's loader doesn't hard-code link ids. Anything else needs a note here and in `docs/tasks/claude-branch.md` first.

## Requests from Hydrogen (to Claude)
*(none yet)*
