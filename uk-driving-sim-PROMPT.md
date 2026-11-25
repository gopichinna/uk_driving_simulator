# PROMPT: UK Driving Rules Simulator — Phase 0 (Planning & Architecture only)

## Your role
Act as my **principal architect and senior game engineer** (think lead systems/gameplay engineer on a large open-world driving title). You care about **simulation correctness, systems design and gameplay feedback loops over graphical fidelity**. You are pragmatic: you pick boring, proven tech where it reduces risk, and you challenge my assumptions when they are wrong.

## Product vision
A fun, mobile-first (Android + iOS) driving **simulation game that teaches UK road rules by doing**, so that a newcomer to the UK can sign up and, after completing the curriculum, understand and correctly apply roughly **80% of the rules they will meet on a daily city + motorway commute**.

- First audience: **people new to the UK** (many are experienced drivers from right-hand-drive countries or from India) and UK learner drivers. Cars only for v1; design so that motorcycles/vans/LGV can be added later.
- Learning happens by **driving in realistic situations and getting instant, explained feedback**, not by reading a book.
- Graphics are deliberately **low-poly / stylised**. Budget goes into traffic behaviour, junction logic, rule evaluation and feedback quality.
- **UK first, global later.** Architecture must treat "country" as a data/config pack (driving side, speed limits, signs, rule set, language, map region), not hard-coded logic. US and others should be addable mostly by content, not rewrites.

## Rules/situations that must be covered (starting taxonomy — you will refine and prioritise it)
- Driving on the left, lane discipline, position and mirrors/signal routine
- **Roundabouts**: standard, multi-lane, spiral, mini-roundabouts, signalling, lane choice, giving way to the right
- **Motorways**: joining/leaving, lane discipline, overtaking, stopping distances, smart motorways, **hard shoulder / emergency areas / red X**, breakdown procedure, roadworks, average-speed cameras
- **Bus lanes** (operating hours/signs), **cycle lanes** (mandatory/advisory), advanced stop lines, overtaking cyclists, hierarchy of road users, cyclists/pedestrians priority
- Speed limits (national limits, 20/30/40 zones, repeater signs, school and hospital zones)
- **School areas**: zig-zag lines, crossing patrols, 20 mph, children behaviour
- **Hospital/emergency**: ambulances/blue lights, giving way safely, clearways
- Pedestrian crossings (zebra, pelican, puffin, toucan, pegasus), traffic lights, box junctions, give way, priority on narrow roads, filter lanes, level crossings
- Parking and waiting rules (single/double yellow, red routes, bays), clearways, loading
- Following distance/2-second rule, stopping distances, weather/night/fog lights, dual carriageways, country lanes/single-track passing places
- Mobile phone, seatbelt, drink/drug awareness, fines/points concepts (informational)
- Payment/zone awareness (congestion charge, ULEZ, tolls) — informational, lower priority

## Ground rules
1. **This phase is planning only. Do NOT write game code yet.** Deliver documents and (at the end) a runnable-prototype *plan*. Stop for my approval before building anything.
2. **Do not invent road rules.** Source every rule from the current official **Highway Code (GOV.UK, Open Government Licence v3.0)** and other official sources; keep a `rule_id → source reference` mapping. Flag anything you could not verify. Research the web for current versions (e.g. recent Highway Code changes such as the hierarchy of road users, and current smart-motorway guidance).
3. **Do not use DVSA-licensed hazard-perception clips or question banks** (they are licensed). Create original scenarios and original questions.
4. Map data will come from **OpenStreetMap (ODbL — attribution/licence obligations apply)**. Evaluate and document this licensing impact.
5. Add a clear in-app disclaimer: educational tool, not a replacement for lessons or the official tests.
6. Ask me **at most 8 clarifying questions, in one batch, before you start the deliverables** (use sensible defaults if I don't answer and list your assumptions).

## Deliverables (write as Markdown files under `/docs`)
1. **`01-product-spec.md`** — personas, user journeys, core loop (Learn → Practise → Drive → Test), feature list split into **MVP / v1 / later**, success metrics (e.g. rule-mastery %, retention), and what is explicitly out of scope.
2. **`02-rules-catalogue.md`** — a structured catalogue of UK rules grouped by scenario, each with: `rule_id`, plain-English explanation, source link, how it is **detected in simulation**, severity (minor/serious/dangerous, modelled on the real practical-test fault categories), and the teaching moment. Include a **coverage analysis** estimating how we reach ~80% of daily-commute situations, and what the remaining 20% is.
3. **`03-gameplay-and-learning-design.md`** — curriculum/level progression (L-plate → full licence), instructor/coach system, instant feedback + post-drive **replay with "why you were wrong"**, scoring, spaced repetition on weak rules, mock-test mode, free-drive mode in real streets, accessibility, onboarding for non-native English speakers, and engagement mechanics that are fun without being gimmicky.
4. **`04-architecture.md`** — the core of this phase (see below).
5. **`05-tech-decision-records.md`** — ADRs for each major choice with options considered, scoring matrix, and a firm recommendation.
6. **`06-roadmap-and-risks.md`** — phased milestones (Prototype → Vertical slice → MVP → UK launch → second country), team/effort estimates for a very small team, top risks and mitigations.
7. **`07-prototype-plan.md`** — the smallest vertical slice that proves the idea: one hand-authored roundabout + one real-OSM street area, rule engine firing 5–8 rules, feedback UI, running on a mid-range Android phone. List exact tasks, in order.

## Architecture guidance (challenge it if you disagree)
**Principles**
- **Simulation core separate from presentation**: deterministic, fixed-timestep simulation; rendering is just a view. This enables headless automated tests and replays.
- **Rules engine is engine-agnostic and data-driven**: pure library (no game-engine dependencies) consuming a stream of simulation state/events and emitting rule events (`RuleViolation`, `RuleSatisfied`, `NearMiss`, etc.). Rules defined as data/config packs per country plus code for complex ones. Every rule has unit tests built from scenario fixtures.
- **Country/locale packs**: driving side, speed-limit defaults by road class, sign set, rule pack, language strings, scoring parameters.
- **Hybrid world content**: (a) **hand-authored training scenarios** for complex situations (roundabouts, motorway merges, school zones) because raw OSM data is too messy to teach from, and (b) **real-city free-drive** generated offline from OSM into compact, game-ready road-graph/tile packs (**no live Overpass calls from the app**).
- **Traffic & pedestrian AI**: road-graph-based agents that follow rules and priority correctly, plus scripted "teachable" events (cyclist undertaking, child steps out, ambulance approaching, car cuts in).
- **Low-poly, mobile-friendly**: target 30 fps on mid-range Android, small app size, offline-first with downloadable city packs.
- Privacy/GDPR-aware analytics; thin backend (auth, progress sync, content/pack delivery, leaderboards later).

**Components to design and diagram (Mermaid)**
Client app/UI, simulation core, vehicle physics (arcade-realistic, not racing-sim), road graph + junction/priority model, signage/signal system, traffic/pedestrian AI, rules engine, feedback/coach system, replay system, progress/curriculum service, content pipeline (OSM → packs; scenario authoring tools), backend/API, analytics, build/CI.

**Tech stack — evaluate and recommend (my current leaning in brackets, but decide on evidence):**
- Client/game engine: Unity (C#) vs Godot 4 vs Flutter + custom sim vs native. [Unity + C#; rules engine as a pure C# library]
- Content pipeline/tooling: Python (OSM extraction, graph building, pack generation, rule-catalogue validation). [Python]
- Backend: managed BaaS (e.g. Supabase/Firebase) vs small custom API (e.g. FastAPI + Postgres). [Keep minimal and managed]
- Data formats for rule packs and road packs (JSON/FlatBuffers/etc.), testing strategy, CI/CD for Android + iOS builds.
Explicitly cover: licensing/cost of each option, mobile performance, team hiring pool, iOS/Android parity, and how easily a second country (e.g. US, right-hand driving) can be added.

## Output format
- Start with a **one-page executive summary** and your **top 5 decisions + top 5 risks**.
- Use tables for comparisons, Mermaid for diagrams, concise prose elsewhere.
- Mark every assumption as `ASSUMPTION:` and every unverified fact as `UNVERIFIED:`.
- End with a **"Decisions I need from you"** list and then **stop and wait**.

Begin by asking your clarifying questions (max 8, one batch).
