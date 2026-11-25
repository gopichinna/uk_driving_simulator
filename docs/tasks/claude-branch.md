# Tasks: Claude, branch `claude/town-centre-junctions`

**Goal:** see `docs/08-parallel-work-plan.md` §2. Claude owns `core/**`, `unity/**` and `core-tests.yml`. Claude doesn't edit `pipeline/**`, `content/countries/**`, `content/cities/**` or `docs/02`.

## A. Town-centre course (hand-authored)
- [ ] A1. Core: `TownGeometry`: a small grid of two-way streets (one lane each way, 30 mph, with a 20 mph school street). It has:
  - a **signalised crossroads** (with an advanced stop line for cyclists);
  - two **give-way T-junctions**;
  - a **zebra crossing** with zig-zags;
  - a **box junction**.

  Lane paths, conflict zones and stop/give-way lines all come from one geometry.
- [ ] A2. Core: `TrafficSignals`: a GB light sequence (red → red+amber → green → amber) per signal group, with configurable timings; deterministic.
- [ ] A3. Core: town traffic AI:
  - IDM along lane paths;
  - stops for red and amber;
  - gives way at give-way lines and to pedestrians at junctions (H2);
  - doesn't block box junctions;
  - 10% cyclists;
  - 3-second gaps.
- [ ] A4. Core: pedestrians: walk footways, wait at the zebra and cross; some cross at junction mouths (to test H2). Deterministic and seeded.
- [ ] A5. Rules (Rules.Core), each with pass/minor/serious fixtures:

  | Rule | Highway Code |
  |---|---|
  | `GB.TL.RED` (stop behind the line on red) | 175–176 |
  | `GB.TL.AMBER` (stop on amber unless unsafe to stop) | 175 |
  | `GB.TL.ASL` | 178 |
  | `GB.XNG.ZEBRA` (give way to people on or waiting at a zebra) | 195, H2 |
  | `GB.XNG.ZIGZAG_OVERTAKE` | 191 |
  | `GB.JCT.GIVE_WAY` | 172 |
  | `GB.JCT.H2_PEDESTRIANS` | H2, 170 |
  | `GB.JCT.BOX` | 174 |

- [ ] A6. Unity: generated `Town` scene with road markings:
  - stop lines and ASL boxes;
  - give-way lines and triangles;
  - zebra stripes, Belisha beacons and zig-zags;
  - the yellow box-junction hatch;
  - working traffic lights;
  - pedestrians;
  - "30" and "20" signs.

  Add it to the Course list.

## B. Debrief and replay (prototype P5)
- [ ] B1. Core: `DriveRecorder`: an input log plus a state snapshot every second, and a 60 s ring buffer; restore the nearest snapshot and re-simulate. Test: replaying matches the original run.
- [ ] B2. Unity: end-of-drive **Debrief** screen with the fault list grouped by rule. Tapping a fault replays from 6 s before to 3 s after, from a bird's-eye camera, with the explanation and the Highway Code rule.
- [ ] B3. Unity: "Finish drive" button and a short summary (good / minor / serious / dangerous, and pass/fail in test style).

## C. Settings
- [ ] C1. Unity: **Settings** screen: steering sensitivity slider (30–150%, default 60%, saved), units hint (mph), and reset progress. Reachable from the HUD.

## D. Real streets (prepare for the merge)
- [ ] D1. Core: `RoadPackLoader`: parse RoadPack v0 JSON (`content/schemas/roadpack.v0.schema.json`) into a link graph with lane paths.
  - Tests use `content/samples/roadpack.sample.json` **and** the merged `content/cities/wolverhampton/roadpack.json`.
  - Read v0.1 `restrictions[]` as optional.
- [ ] D2. After Hydrogen's merge: a "Wolverhampton (beta)" course from `content/cities/wolverhampton/roadpack.json`. Roads are meshed from links, rules fire only on elements with confidence ≥ 0.7, and the ODbL attribution is shown.

## D+. Use Hydrogen's round-2 content as it lands
- [ ] D3. Take the debrief and toast text from `content/countries/gb/strings/en/rules.json` when it exists; otherwise use the built-in text.
- [ ] D4. The town course exposes the anchors promised to Hydrogen:
  - `start`;
  - `crossroads_signals`;
  - `tjunction_north`, `tjunction_south`;
  - `zebra`;
  - `box_junction`;
  - `school_street`.

## E. Hand-off
- [ ] E1. Keep `dotnet test` green and the APK building. Add a dated note to `docs/08` §6, then open a PR to `main`.

## Requests from Hydrogen
- Pre-agreed on 2026-11-25:
  - new schemas `strings.v0`, `signs.v0`, `lesson.v0` and `route.v0`;
  - an additive roadpack **v0.1**: an optional top-level `restrictions[]`, with no renamed or removed fields.

## Requests to Hydrogen
- Keep `geometry` as `[[x, z], …]` metres and `speedLimitMph` as an integer (as in the schema).
