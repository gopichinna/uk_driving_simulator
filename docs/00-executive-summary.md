# L to Local: Phase 0 Executive Summary

**L to Local**: *Keep Left – UK driving practice* (working title; tagline "Keep Left").

*Status: planning only. No game code has been written. Date: 2026-11-25.*

## What we are building
A mobile (Android + iOS) driving game with stylised low-poly graphics. It teaches UK road rules by having you drive through real situations and explaining every mistake straight away. The target: after finishing the curriculum, a newcomer to the UK can correctly apply about **80% of the rules they meet on a daily city and motorway commute**. Version 1 covers cars only, in Great Britain (England, Scotland, Wales), in English.

The product is built around one loop: **Learn → Practise → Drive → Test**. Each step uses a short interactive rule card, a 1–3 minute micro-scenario, a mixed route, and a mock test with faults modelled on the practical driving test. After every drive, a **replay shows exactly why you were wrong**, with the Highway Code rule linked. Spaced repetition brings your weak rules back until you have mastered them.

## Answers you gave (recorded as constraints)
| Topic | Decision |
|---|---|
| Team | **Claude writes the code**; you test on your phone, play-test and make decisions. Low-poly assets are bought, not made. Hardware: MacBook Air (M5) + OnePlus 12. |
| Engine | You asked me to choose. I chose **Unity 6 LTS**; see ADR-001. |
| Business model | **Paid product**. Free download; **the first 3 lessons are free**, then a one-off **"Full Licence" purchase at £14.99** unlocks everything. An optional **rewarded-ad** route may come later. No subscription and no payments in the prototype. |
| Language | English only for v1. The user's *preferred learning language* is captured during onboarding, and the code is ready for translation (see doc 03, section 9). |
| Theory test | Not in v1. It is planned as a later "Theory Prep" mode with original questions. |
| Gearbox and controls | Automatic only for MVP. On-screen wheel and pedals, with tilt steering as an option. |
| Devices | Prototype on your **OnePlus 12** (Android), built from your Mac. Performance is also checked against a mid-range profile (see doc 07, section 4). iOS from the vertical slice. |
| Name | **L to Local**, tagline **"Keep Left"**. Trademark and store-name checks still to be done on the UK IPO register and both stores. |
| Audience | **18+ only** at launch (age gate). |
| Market | **Great Britain only** at launch (England, Scotland, Wales). |
| Prototype city | **Wolverhampton** city centre. |

## Top 5 decisions
1. **Unity 6 LTS (C#) as the client, but the simulation core and the rules engine are plain C# libraries with no Unity dependency.** They are tested with `dotnet test` in CI, run headless, and could move to another engine later. Unity wins for a first-time game developer because of its learning resources, mature mobile in-app purchases and ads, the Asset Store, and how widely it is known.
2. **We write our own fixed-timestep simulation (50 Hz) and our own arcade vehicle model instead of using Unity physics.** Unity renders what the simulation decides. Replays use an input log plus a state snapshot every second, so we do **not** depend on floating-point results being identical across devices. This challenges the brief's "deterministic" assumption: we keep determinism within one device and rely on snapshots across devices.
3. **The rules engine is data-driven.** A country pack contains about 20 *parameterised rule kinds* written in code (e.g. `speed_over_limit`, `fail_to_give_way`) plus a small number of rules written fully in code. Every rule carries a `rule_id`, a GOV.UK source reference, a legal force (MUST vs should), and fixture tests. Rules talk about **nearside/offside** and **near turn/far turn** rather than left and right, so a right-hand-traffic country is largely a matter of configuration.
4. **World content is a mix of two types, with one shared format.** Hand-authored lane-level training scenarios (roundabouts, motorway merges, school zones) and real streets from an **offline Python OSM → lane-graph pipeline** use the same road-pack format. The client builds road meshes procedurally from it. City packs are an ODbL *derivative database*: we attribute OSM and must offer the pack data under ODbL. Our code and art stay closed.
5. **A small managed backend: Firebase** (Auth, Firestore with EU location, Storage/CDN for packs, Remote Config, Crashlytics) **plus RevenueCat** for one-off purchases on both stores. Analytics are privacy-first. Ads, if added, are **rewarded only**, and are personalised only with consent. The app is 18+ at launch.

## Top 5 risks
| # | Risk | Mitigation |
|---|---|---|
| 1 | **Scope versus a solo first-time game developer.** A driving sim with traffic AI is a big build: with Claude writing the code, the limit is the test-and-feedback loop, so the estimate is **~8–10 months to UK launch** (doc 06). | Hard phase gates, the smallest possible prototype, bought assets, and the MVP curriculum built on hand-authored areas only. |
| 2 | **OSM data is not lane-accurate** (turn lanes, bus-lane hours and markings are often missing), so free-drive could teach wrong rules or mark players unfairly. | Each road element carries a data-confidence score. Rules only fire where confidence is high. Cities are curated. Free-drive is labelled as practice. |
| 3 | **Rule correctness and liability.** Teaching a rule wrongly, or the Highway Code changing. | A `rule_id → source` map, a paid **ADI (approved driving instructor) review** before launch, a watcher on the GOV.UK Highway Code updates log, and a disclaimer. |
| 4 | **Touch-screen driving that also models "observation"** (mirrors, blind spots) may feel like a chore. | Test with 5–8 real users in the prototype. Assist levels. Observation actions are optional for beginners. |
| 5 | **Monetisation.** A crowded learner-app market and low conversion from free to paid at £14.99. | 3 free lessons end right after a replay shows its value; soft launch validates the price; 18+ age gate and privacy-by-default settings keep compliance simple. |

## Documents
1. [01-product-spec.md](01-product-spec.md): personas, journeys, features, metrics, scope
2. [02-rules-catalogue.md](02-rules-catalogue.md): 129 catalogue entries (90 in MVP) with sources, detection, severity, plus coverage analysis
3. [03-gameplay-and-learning-design.md](03-gameplay-and-learning-design.md): curriculum, coach, feedback, replay, spaced repetition
4. [04-architecture.md](04-architecture.md): components, Mermaid diagrams, data model, pipeline
5. [05-tech-decision-records.md](05-tech-decision-records.md): ADRs with scoring matrices
6. [06-roadmap-and-risks.md](06-roadmap-and-risks.md): milestones, effort, costs, risk register
7. [07-prototype-plan.md](07-prototype-plan.md): the vertical-slice prototype, task by task

## Decisions
Recorded in [06-roadmap-and-risks.md](06-roadmap-and-risks.md#decisions). Open: the **working title**, and the **budget/ADI reviewer** (deferred to before the vertical slice).
