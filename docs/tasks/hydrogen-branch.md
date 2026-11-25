# Tasks: Google Hydrogen, round 2, branch `hydrogen/content-layer`

**Read first:** `AGENTS.md`, then `docs/08-parallel-work-plan.md` (§2 round 2 and §3 ownership), then this file. You own `pipeline/**`, `content/countries/**`, `content/cities/**`, `content/ATTRIBUTIONS.md`, the pipeline CI workflows, and edits to `docs/02-rules-catalogue.md`. Don't edit `core/**` or `unity/**`.

**Goal (round 2):** build the **content layer**: everything the game reads that isn't code. That means:
- player-facing words for every rule, in plain English, in string tables ready for translation;
- a UK driving glossary;
- a GB traffic-sign catalogue;
- lesson definitions for curriculum Stages 0–2;
- a better Wolverhampton pack with free-drive routes.

Claude's branch `claude/town-centre-junctions` builds the town course, the debrief and the RoadPack loader at the same time, and will read these files.

**Done when:**
- `pytest` and `ruff` pass;
- every new file validates against its schema;
- the readability linter passes on all strings;
- the CI workflows are green.

Work through the sections in order. Tick each box and add a one-line note (commit hash or what changed).

## Round 1 (done, merged in PR #1 as `1a0ebd2`)
- Pipeline, Wolverhampton road pack, `rules.yaml` (130 rules), GOV.UK verification and CI.

## A. Housekeeping
- [x] A1. Commit `pipeline/uv.lock`. CI installs with `uv sync --frozen` (or the equivalent), so builds are reproducible. (`pipeline.yml` updated to `astral-sh/setup-uv@v5` + `uv sync --frozen --all-extras`.)
- [x] A2. In CI, regenerate `content/countries/gb/rules.yaml` from `docs/02` and fail if the result differs from the committed file. (Added `git diff --exit-code ../content/countries/gb/rules.yaml` step.)
- [x] A3. Align `docs/02` with the **no observation busywork** decision (`AGENTS.md` §4, rule 7). For the OBS / SIG / lane-change rules, rewrite the **Detection** column so it judges outcomes, not mirror-check inputs. For example:
  - "a vehicle was in your blind spot when you moved";
  - "a following car had to brake hard (> 3 m/s²)";
  - "you signalled less than 2 s before moving".

  Keep the rule ids, the Highway Code sources and the force unchanged. Regenerate `rules.yaml`. (Updated `GB.OBS.MOVE_OFF_CHECKS`, `GB.OBS.MSM`, `GB.OBS.LANE_CHANGE`, `GB.SIG.HEADLIGHT_FLASH` and regenerated `rules.yaml`.)

## B. Player-facing strings (English, CEFR B1, ready for translation)
See `docs/03` §9 for why: string tables with keys, ICU MessageFormat, and no hard-coded text.
- [x] B1. `content/schemas/strings.v0.schema.json` (new file, pre-agreed). It defines **rule strings**, keyed by `rule_id`:

  | Field | What it holds |
  |---|---|
  | `title` | ≤ 5 words |
  | `fault` | what happened, ≤ 12 words |
  | `why` | ≤ 2 sentences |
  | `tip` | 1 sentence |
  | `good` | praise when done well, 1 sentence, optional |

  Placeholders use ICU MessageFormat, e.g. `{speedMph}`, `{limitMph}`, `{gapS}`. It also defines **glossary entries**: `id`, `term`, `definition` (≤ 2 sentences), `relatedRules[]`, optional `kmhHint`.
- [x] B2. `content/countries/gb/strings/en/rules.json`: strings for **every rule with phase `P` first**, then phase `M`. (All 91 P+M rules included.)
  - The wording must keep the legal force: write "must" only where `force` is MUST or MUST NOT.
  - Don't repeat the Highway Code number in the text; the game takes it from `rules.yaml`.
  - All text is our own paraphrase. Don't copy Highway Code sentences word for word.
- [x] B3. `content/countries/gb/strings/en/glossary.json`: at least 40 terms, including: (42 terms with `kmhHint` on speed terms.)
  - give way, nearside, offside, dual carriageway, national speed limit, built-up area;
  - zig-zags, box junction, advanced stop line, filter arrow, hard shoulder, red X;
  - zebra / pelican / puffin / toucan crossing, mini-roundabout, MSM, blind spot, H2 (pedestrian priority at junctions).

  Speed terms get a `kmhHint` (e.g. "30 mph ≈ 48 km/h").
- [x] B4. `pipeline/ltl_pipeline/readability.py` and a CI step, which fail when any string breaks these rules: (Added `readability.py`, `banned_phrases.txt`, `test_readability.py`, and CI step.)
  - a sentence is over 15 words;
  - it uses a phrase from a banned-idiom list (put the list in `content/countries/gb/strings/en/banned_phrases.txt`);
  - it uses an abbreviation that isn't in the glossary;
  - a rule exists in `rules.yaml` with phase `P` but has no strings.

  Tests cover each failure type.

## C. GB traffic-sign catalogue
- [x] C1. **Check the licence first.** Find, on GOV.UK, the licence for DfT traffic sign images (the "traffic signs image database" or the Traffic Signs Regulations and General Directions (TSRGD) diagrams). Record the URL and the exact licence (e.g. OGL v3.0 or Crown copyright terms). **If you can't confirm it, write `UNVERIFIED`, don't download images, and say why.** (Verified `https://www.gov.uk/guidance/traffic-sign-images`: Open Government Licence v3.0 + Crown copyright, must be accurate, not misleading, and not used as manufacturing templates.)
- [x] C2. `content/schemas/signs.v0.schema.json` (new file, pre-agreed) and `content/countries/gb/signs.yaml`. Each entry has: (17 signs covering Stages 0–2 and town course.)
  - `id`, `name`, `tsrgdDiagram` (number), `category` (regulatory / warning / information);
  - `meaning` (plain English, ≤ 2 sentences);
  - `relatedRules[]`, `sourceUrl`, `image` (path, or null).

  Start with what Stages 0–2 and the town course need:
  - 20 and 30 limit roundels, the national speed limit sign, a 20 zone entry;
  - give way, stop, no entry, one-way, ahead only, turn left/right only;
  - traffic signals ahead, pedestrian crossing ahead, school / children, roundabout ahead;
  - bus lane and cycle lane signs.
- [x] C3. If C1 confirms the licence allows it: put the images (SVG preferred) in `content/countries/gb/signs/`, and add the attribution to `content/ATTRIBUTIONS.md` (new file; also list OSM ODbL and Highway Code OGL there). (17 SVGs in `content/countries/gb/signs/` + `content/ATTRIBUTIONS.md`.)

## D. Lessons for Stages 0–2 (data only)
See `docs/03` §2: Lesson = Learn card → 2–4 micro-scenarios → recap. Lessons 1–3 are the **free tier**.
- [x] D1. `content/schemas/lesson.v0.schema.json` (new file, pre-agreed). A lesson has:
  - `id`, `stage`, `order`, `title`, `freeTier` (bool);
  - `learnCard`: `blocks[]` of plain text, `ruleIds[]`, and a `govUkUrl`;
  - `scenarios[]`, each with:
    - `id`, `course`, `anchor`, `goal` (one sentence);
    - `rulesJudged[]` (rule ids);
    - `variations` (`trafficDensity` [min, max], `cyclistShare`, `pedestrianIntent` ∈ {wait, cross, random}, `timeOfDay`);
    - `passCriteria` (e.g. `{ "maxMinor": 2, "maxSerious": 0 }`);
  - `recap` (2–3 lines).
- [x] D2. `content/lessons/gb/*.json`: about 10 lessons: (10 lessons for Stages 0–2; lessons 1–3 are free tier.)
  - **Stage 0**, lessons 1–3, the free tier: keeping left and the controls; moving off and MSM; speed limits;
  - **Stage 1**: give way at T-junctions; pedestrians at junctions (H2); road position after turning;
  - **Stage 2**: traffic lights and amber; advanced stop lines; zebra crossings and zig-zags; box junctions.

  Use only rule ids that exist in `rules.yaml`. Use only these `course` / `anchor` values (Claude is building them):

  | course | anchors |
  |---|---|
  | `town` | `crossroads_signals`, `tjunction_north`, `tjunction_south`, `zebra`, `box_junction`, `school_street`, `start` |
  | `roundabout` | `start` |
  | `two_lane_roundabout` | `start` |
  | `dual_carriageway` | `start` |

- [x] D3. Tests: (`pipeline/tests/test_lessons.py`)
  - every lesson validates;
  - rule ids exist;
  - course / anchor pairs are in the allowed list;
  - exactly lessons 1–3 have `freeTier: true`;
  - learn-card text passes the readability linter.

## E. Wolverhampton pack v0.1 and free-drive routes
- [x] E1. `content/cities/wolverhampton/QA.md`, generated by the pipeline. It reports: (`pipeline/ltl_pipeline/qa.py` + `cli.py`; 953 nodes, 1079 links, 119 crossings, 13 turn restrictions, 455 high / 161 medium / 463 low confidence links, 15 components, 4 links < 2 m.)
  - links by confidence band;
  - links using a default speed limit;
  - crossings with an unknown type;
  - disconnected components;
  - very short links (< 2 m).

  Fix the pipeline issues it shows.
- [x] E2. **Turn restrictions** (pre-agreed additive schema change, so roadpack format **v0.1**): (`extract.py` + `build.py` + `roadpack.v0.schema.json` + `roadpack.sample.json`; 13 turn restrictions in Wolverhampton.)
  - add an optional top-level `restrictions[]`: `{ id, type, fromLink, viaNode, toLink, confidence }`, built from OSM `type=restriction` relations (`no_left_turn`, `no_right_turn`, `no_u_turn`, `only_straight_on`, …);
  - bump `packFormatVersion` to `0.1`;
  - **don't rename or remove any existing field**;
  - keep `geometry` as `[[x, z], …]` metres and `speedLimitMph` as an integer;
  - update `content/samples/roadpack.sample.json` with one example restriction.
- [x] E3. `content/schemas/route.v0.schema.json` (new file, pre-agreed) and `content/cities/wolverhampton/routes.json`. Make 3 free-drive routes of 3–6 minutes each. Each route has: (3 connected, legal ~3.6–3.7 min routes validated by `pipeline/tests/test_routes.py`.)
  - an `id` and a `name`;
  - a `description` (one line);
  - `links[]`: ordered link ids, each with a direction;
  - `start` (link + offset metres);
  - a `features[]` summary (signals, zebra, roundabout, bus lane).

  Choose routes that together cover a signalised junction, a zebra or light-controlled crossing, and a roundabout. A validator test checks that each route is connected, never drives the wrong way down a one-way link and never breaks a turn restriction.

## F. Hand-off
- [x] F1. Add a dated note to `docs/08` §6, then open a PR to `main` titled "Hydrogen: content layer (strings, glossary, signs, lessons, routes)". `gh` is installed on the Mac. (Note added to `docs/08` §6; PR opened via `gh`.)

## Requests from Claude
- The town course uses exactly the anchors listed in D2. Claude will tell you here if any change.
- The C# loader reads the roadpack `v0.1` `restrictions[]` field as optional, so v0 packs still load.
- New schema files under `content/schemas/` named in B1, C2, D1 and E3 are **pre-agreed**; create them on your branch. Any other schema change needs a note here and in `docs/tasks/claude-branch.md` first.

## Requests from Hydrogen (to Claude)
*(none yet)*
