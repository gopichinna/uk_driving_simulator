# 07: Prototype Plan (smallest slice that proves the idea)

## 1. What the prototype must prove
| # | Hypothesis | Measure | Pass bar |
|---|---|---|---|
| H1 | Instant feedback + replay makes the rule understood | After a fault + replay, the tester explains the rule in their own words | ≥ 80% of faults explained correctly |
| H2 | A phone touch screen is controllable enough to drive a roundabout | Completion without restart; a 1–5 "felt in control" rating | ≤ 2 restarts per attempt; median ≥ 4 |
| H3 | The observation gesture (mirror check) is acceptable, not a chore | 1–5 "helpful vs annoying"; check rate | **Answered early (2026-11-25): it felt like a chore.** Redesigned: observation is judged by outcome (blind-spot / alongside / forced braking) instead of explicit taps |
| H4 | Rules can be fired *fairly* on real OSM streets | False faults found by the reviewer in 10-min drives | < 1 per 10 minutes |
| H5 | Performance on a mid-range Android phone | p95 frame time; sim + rules time per tick | ≥ 30 fps p95; ≤ 5 ms |

A failure is a **result**, not a disaster: each "no" changes the design before we spend months on the vertical slice.

## 2. Scope
**In:**
- One **hand-authored 4-arm roundabout**: 2-lane entries with lane arrows, a zebra crossing on one exit arm, a 30 mph approach and a 40 mph road.
- One **real-OSM street area in Wolverhampton city centre** (~1 km², inside or along the ring road: signalised junctions, 2–3 T-junctions, a zebra or light-controlled crossing, 20/30 limits, ideally one ring-road roundabout). The exact boundary is picked in task 26 after checking OSM coverage. Two things to note: streets with **West Midlands Metro tram** tracks are kept out of the prototype area, since trams are out of scope; and locals call roundabouts **"islands"**, which goes into the glossary.
- Automatic car with on-screen wheel and pedals, indicators, and a mirror-check gesture.
- AI cars following IDM with gap acceptance; pedestrians using the zebra; traffic lights.
- **Rules engine firing 8 rule kinds** (below).
- Feedback: in-drive toasts, post-drive fault list, **replay** of each fault from a bird's-eye view with a short explanation and Highway Code link.
- Android build on your **OnePlus 12**, built from your MacBook Air.

**Out:** accounts, payments, curriculum and SRS, iOS, menus beyond a scene picker, cyclists, motorways, audio polish, art polish (flat colours are fine), ghost-path overlay (that's the vertical slice).

## 3. The 8 prototype rules
| # | rule_id (doc 02) | Rule kind | Where it fires | Why it's in |
|---|---|---|---|---|
| 1 | GB.SPD.OVER_LIMIT | `speed_over_limit` | Both areas | Simplest end-to-end test of zones + severity escalation |
| 2 | GB.OBS.MSM | `observation_missing_before` | Both | Tests the observation mechanic (H3) |
| 3 | GB.RBT.GIVE_WAY_RIGHT | `fail_to_give_way` | Roundabout | Core priority/conflict model; shared with AI |
| 4 | GB.RBT.APPROACH_LANE | `lane_choice_for_exit` | Roundabout | Lane-level graph + exit index |
| 5 | GB.RBT.SIGNAL_APPROACH + GB.RBT.SIGNAL_EXIT | `roundabout_signal_sequence` (code rule) | Roundabout | Proves multi-step code rules |
| 6 | GB.TL.RED | `stop_line_crossed_on_aspect` | OSM area | Signal state machines |
| 7 | GB.XNG.ZEBRA | `fail_to_give_way` (pedestrian variant) | Both | Pedestrian intents; MUST vs should severity |
| 8 | GB.JCT.GIVE_WAY | `fail_to_give_way` | OSM area | Priority model on *real, messy* data (H4) |

## 4. Test devices
- **Daily testing: your OnePlus 12** (Snapdragon 8 Gen 3, a flagship). It's great for feel and controls, but **too fast to prove mid-range performance**.
- **Performance rule during the prototype:** on the OnePlus 12, the frame must stay ≤ 11 ms (≈ 3× headroom against the 33 ms mid-range budget), and sim + rules ≤ 2 ms. The app runs locked at 30 fps in the low-quality tier for testing.
- **Before the vertical slice:** buy a used 2021–22 mid-range Android (~£80–150) as the real reference device, or use a few runs on Firebase Test Lab devices (`UNVERIFIED:` current free quota).
- **iOS:** your Mac can build it. A physical iPhone or TestFlight testers are needed from the vertical slice.

## 5. Task list (in order)
I write the code; you run builds on the phone, test and decide. Milestone durations are **calendar time**, assuming you can give ~1–2 hours on most days to testing and feedback.

### Milestone P0: Setup (≈ 1 week)
1. Install Unity Hub + **Unity 6 LTS** with Android Build Support; install .NET SDK 8, Python 3.12, Git LFS.
2. Guided setup: I walk you through Unity Hub, Android SDK/NDK and USB debugging on the OnePlus 12; build an empty scene to the phone. A Unity crash course is optional, since I'll explain each step as we go.
3. Create the repo structure from doc 04 (`/core`, `/unity`, `/pipeline`, `/content`, `/docs`), `.gitignore` for Unity, Git LFS for binary assets.
4. GitHub Actions: `dotnet test` for `/core`, `pytest` for `/pipeline`.

### Milestone P1: Core skeleton (≈ 1 week)
5. `Sim.Core`: fixed 50 Hz loop, seeded RNG, world state, snapshot/restore + unit tests.
6. `Sim.Vehicle`: single-track model, automatic gearbox, brakes, µ by surface; tests for stopping distance and turning radius.
7. Unity adapter: input (wheel, pedals, indicator buttons, mirror gesture) → `InputFrame`; render interpolation; chase camera.
8. **Checkpoint:** drive a box-car on a flat plane at 30 fps on your phone.

### Milestone P2: Roads and the roundabout (≈ 1–1.5 weeks)
9. `Packs.Schema`: RoadPack v0 (JSON) for nodes, links, lanes, connectors, conflict zones, stop lines, zones, signs.
10. Pipeline: scenario YAML → RoadPack compiler, with schema validation and tests.
11. Author the **roundabout scenario YAML** (4 arms, 2-lane entries, lane arrows, a zebra on one exit, speed zones).
12. Unity: procedural road meshes, lane markings and give-way lines from RoadPack; a few low-poly props.
13. `Sim.Roads`: localise the ego on the lane graph (lane id, offset, connector, zones).

### Milestone P3: Traffic, signals, pedestrians (≈ 1–1.5 weeks)
14. Conflict zones + priority resolution (offside priority on the roundabout; give way at T-junctions).
15. AI cars: IDM following + gap acceptance at conflicts; spawner and despawner; deadlock watchdog.
16. Traffic-light `SignalGroup` with the GB sequence from country-pack config.
17. Pedestrians: footway path, `wait_to_cross` → `cross` at the zebra.
18. Headless soak test: 1,000 seeded runs per scenario, 0 deadlocks.

### Milestone P4: Rules engine (≈ 1 week)
19. `Rules.Core`: `DriverFrame` builder, `RuleEvent`, rule-kind registry, YAML loader, severity escalation, cool-downs.
20. Implement the 5 rule kinds + 1 code rule for the 8 rules above.
21. **Fixtures**: 3 per rule (pass / minor / serious-or-dangerous), all green in CI.
22. Country pack `gb` v0: speed defaults, signals, the 8 rule YAMLs with sources, en-GB strings.

### Milestone P5: Feedback and replay (≈ 4–5 days)
23. In-drive toasts by severity; a dangerous fault freezes the drive with a 4 s mini-replay.
24. Recorder: input log + 1 s snapshots; 60 s ring buffer.
25. Debrief screen: fault list → replay from 6 s before to 3 s after, bird's-eye camera, explanation text, MUST/should label, GOV.UK link.

### Milestone P6: Wolverhampton OSM street area (≈ 1–1.5 weeks)
26. Pipeline: Geofabrik West Midlands extract → pick and clip the Wolverhampton city-centre bounding box (osmium) → pyosmium parse → tag normalisation.
27. Lane-graph inference v0: lanes/oneway/turn:lanes; connectors at junctions; simple roundabout and junction handling; speed zones via country defaults.
28. Confidence scoring + `min_confidence` gating in the rules engine; manual `fixes.yaml` overlay.
29. Load the city pack in the client; buildings as extruded footprints; OSM attribution on screen.
30. Drive it yourself: log false faults, and compare against the real signs and lines you know (or a quick walk or drive past). Fix the data or the rules (H4).

### Milestone P7: Performance and play-test (≈ 1.5 weeks; mostly recruiting and sessions)
31. Profile on the reference phone; fix to budget (batching, agent counts, GC).
32. Play-test with **5–8 adults (18+)**: at least 2 right-hand-traffic drivers, 2 left-hand-traffic newcomers (e.g. from India) and 1–2 learners. Script: roundabout ×3, OSM drive ×1, short interview.
33. Write the **prototype report**: H1–H5 results, videos, the decision to go, change course or stop; update docs 02–07.

**Total: about 6–8 weeks after setup.** If that's too long, the first cut is P6 (the OSM area). H4 then moves to the vertical slice, saving ~1.5 weeks.

## 6. Definition of done (prototype)
- [ ] All 8 rules: ≥ 3 fixtures each, green in CI.
- [ ] Roundabout + OSM area both playable on the reference Android device at ≥ 30 fps p95.
- [ ] Every fault opens a replay with a correct explanation and source link.
- [ ] 0 deadlocks in the 1,000-run soak.
- [ ] Play-test report with H1–H5 verdicts.
- [ ] OSM attribution visible; disclaimer shown on launch.

## 7. How we'll work
- I write the code in small PR-sized steps with tests. You run each build on your phone and tell me what feels wrong.
- Every rule change goes through doc 02 first (source, force, severity), then fixtures, then code.
- Nothing in this plan starts until you approve it.
