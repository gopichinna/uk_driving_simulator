# Graph Report - uk_driving_simulator  (2026-11-25)

## Corpus Check
- 59 files · ~53,789 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 220 file(s) not represented in the graph (top: .meta 113, .asset 56, .mat 35)
- WARNING: 220 of 279 files seen (79%) have no supported extractor and are missing from this graph entirely. It may look complete while representing only part of the codebase.

## Summary
- 1228 nodes · 2708 edges · 86 communities (49 shown, 37 thin omitted)
- Extraction: 83% EXTRACTED · 17% INFERRED · 0% AMBIGUOUS · INFERRED: 460 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Roundabout Rule Tests
- Roundabout Geometry & Views
- Lane Change & Observation
- Courses & Scene Setup
- Rules Engine Files
- Unity Package Dependencies 5
- Unity Package Dependencies 6
- Unity Package Dependencies 7
- Unity Package Dependencies 8
- Rules Engine Design Decisions
- Rule Events & Driver Frames
- Unity Package Dependencies 11
- Core .NET Solution
- Unity Package Dependencies 13
- Roundabout Rule Logic
- Cyclist Passing Rule
- Backend & Monetisation Decisions
- Exec Summary & Top Risks
- Unity Package Dependencies 18
- Prototype Rule IDs
- Roundabout View Interface
- 2D vs 3D Mock-up
- Unity Package Dependencies 22
- Unity Package Dependencies 23
- Ring Vehicles
- Unity Package Dependencies 25
- Unity Package Dependencies 26
- User Journeys & Personas
- Curriculum & Learning Design
- Core Tests CI
- Fixed-Step Clock
- Lane Change Rule Logic
- Following Distance Rule
- Mirror Rig
- Unity Package Dependencies 34
- Unity Package Dependencies 35
- DriveGame Controller
- Touch Controls
- Unity Package Dependencies 38
- Unity Package Dependencies 39
- Unity Package Dependencies 40
- Rules.Core Package
- Car Parameters
- Course Interface
- Feedback HUD
- Sim.Core Package
- Disclaimer & Ground Rules
- Unity Package Dependencies 48
- Roundabout Phases
- Unity Package Dependencies 50
- Unity Package Dependencies 51
- Unity Package Dependencies 52
- Fixed-Step Sim Decision
- Preview Renderer
- Unity Module: ide.rider
- Unity Module: ide.visualstudio
- Unity Module: accessibility
- Unity Module: adaptiveperformance
- Unity Module: ai
- Unity Module: androidjni
- Unity Module: cloth
- Unity Module: screencapture
- Unity Module: terrainphysics
- Unity Module: tetgen
- Unity Module: unityanalytics
- Unity Module: unitywebrequesttexture
- Unity Module: unitywebrequestwww
- Unity Module: vectorgraphics
- Unity Module: vehicles
- Unity Module: video
- Unity Module: wind
- Unity Module: xr
- Unity Module: assetbundle
- Unity Module: audio
- Unity Module: imgui
- Unity Module: jsonserialize
- Unity Module: physics
- Unity Module: physics2d
- Unity Module: physicscore2d
- Unity Module: terrain
- Unity Module: ui
- Unity Module: uielements
- Unity Module: unitywebrequest
- Unity Module: unitywebrequestaudio
- Unity Module: test-framework

## God Nodes (most connected - your core abstractions)
1. `Vec2` - 77 edges
2. `CarState` - 71 edges
3. `RuleEvent` - 38 edges
4. `TwoLaneRoundaboutGeometry` - 37 edges
5. `TwoLaneRoundaboutTraffic` - 36 edges
6. `RoundaboutTraffic` - 35 edges
7. `DriveGame` - 35 edges
8. `DriverFrame` - 33 edges
9. `TrafficSim` - 32 edges
10. `LToLocal.Sim` - 30 edges

## Surprising Connections (you probably didn't know these)
- `StartState` --calls--> `CarState`  [INFERRED]
  unity/Assets/Scripts/DualCarriagewayCourse.cs → core/Sim.Core/Vehicle.cs
- `StartState` --calls--> `CarState`  [INFERRED]
  unity/Assets/Scripts/RoundaboutCourse.cs → core/Sim.Core/Vehicle.cs
- `Educational practice tool disclaimer` --semantically_similar_to--> `In-app disclaimer (OGL + ODbL attribution)`  [INFERRED] [semantically similar]
  README.md → docs/01-product-spec.md
- `Ground rules (no invented rules, no DVSA clips, OSM ODbL, disclaimer)` --references--> `GOV.UK Highway Code (OGL v3.0)`  [INFERRED]
  uk-driving-sim-PROMPT.md → docs/02-rules-catalogue.md
- `Geometry` --calls--> `TwoLaneRoundaboutGeometry`  [INFERRED]
  unity/Assets/Scripts/TwoLaneRoundaboutCourse.cs → core/Sim.Core/TwoLaneRoundabout.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Simulation-to-feedback rules evaluation flow** — docs_04_architecture_simulation_core, docs_04_architecture_driverframe, docs_04_architecture_rules_engine, docs_04_architecture_ruleevent, docs_04_architecture_coach_system, docs_04_architecture_recorder_replay [EXTRACTED 1.00]
- **8 prototype rules exercised in the vertical slice** — docs_02_rules_catalogue_gb_spd_over_limit, docs_02_rules_catalogue_gb_obs_msm, docs_02_rules_catalogue_gb_rbt_give_way_right, docs_02_rules_catalogue_gb_rbt_approach_lane, docs_02_rules_catalogue_gb_rbt_signal_approach_exit, docs_02_rules_catalogue_gb_tl_red, docs_02_rules_catalogue_gb_xng_zebra, docs_02_rules_catalogue_gb_jct_give_way [EXTRACTED 1.00]
- **Post-drive learning loop (feedback, replay, SRS, mastery)** — docs_03_gameplay_and_learning_design_instant_feedback_by_severity, docs_03_gameplay_and_learning_design_why_you_were_wrong_replay, docs_03_gameplay_and_learning_design_fsrs_spaced_repetition, docs_01_product_spec_weighted_rule_mastery_metric, docs_00_executive_summary_learn_practise_drive_test_loop [INFERRED 0.85]
- **Rendering style decision: 2D vs 3D for the same roundabout moment** — docs_images_2d_vs_3d_2d_top_down_style, docs_images_2d_vs_3d_3d_low_poly_chase_camera, docs_images_2d_vs_3d_roundabout_give_way_scenario, docs_images_2d_vs_3d_top_down_replay_view [EXTRACTED 1.00]
- **Visual style decision: 2D top-down vs 3D chase cam with replay fallback** — docs_images_2d_vs_3d_2d_top_down_style, docs_images_2d_vs_3d_3d_low_poly_chase_camera, docs_images_2d_vs_3d_top_down_replay_view, docs_images_2d_vs_3d_unity_6, docs_images_2d_vs_3d_flutter_flame [EXTRACTED 1.00]

## Communities (86 total, 37 thin omitted)

### Community 0 - "Roundabout Rule Tests"
Cohesion: 0.05
Nodes (33): RoundaboutRuleTests, DeterministicRandom, State, PathPolyline, Closed, Length, Points, RoundaboutCar (+25 more)

### Community 1 - "Roundabout Geometry & Views"
Cohesion: 0.06
Nodes (28): TwoLaneRoundaboutView, ApproachLanes, ArmCount, TwoLaneRoundaboutRuleTests, Vec2, Heading, Length, X (+20 more)

### Community 2 - "Lane Change & Observation"
Cohesion: 0.06
Nodes (31): ObservationEvent, Kind, Time, ObservationKind, MirrorInterior, MirrorNearside, MirrorOffside, ShoulderNearside (+23 more)

### Community 3 - "Courses & Scene Setup"
Cohesion: 0.07
Nodes (11): ProjectSetup, CarParts, Palette, DualCarriagewayCourse, DisplayName, StartState, RoundaboutCourse, DisplayName (+3 more)

### Community 4 - "Rules Engine Files"
Cohesion: 0.06
Nodes (21): CarSide, Nearside, Offside, DrivingSide, Left, Right, RuleOutcome, GoodPractice (+13 more)

### Community 5 - "Unity Package Dependencies 5"
Cohesion: 0.07
Nodes (35): com.unity.graph-authoring, com.unity.render-pipelines.core, com.unity.render-pipelines.universal-config, com.unity.searcher, com.unity.shadergraph, dependencies, depth, source (+27 more)

### Community 6 - "Unity Package Dependencies 6"
Cohesion: 0.06
Nodes (32): com.unity.modules.subsystems, dependencies, depth, source, version, dependencies, depth, source (+24 more)

### Community 7 - "Unity Package Dependencies 7"
Cohesion: 0.08
Nodes (29): dependencies, depth, source, version, dependencies, depth, source, version (+21 more)

### Community 8 - "Unity Package Dependencies 8"
Cohesion: 0.08
Nodes (27): com.unity.burst, com.unity.collections, com.unity.nuget.mono-cecil, com.unity.profiling.core, dependencies, depth, source, version (+19 more)

### Community 9 - "Rules Engine Design Decisions"
Cohesion: 0.12
Nodes (22): DriverFrame detection signals, Rule-pack YAML representation, Coach with four intervention levels (L1-L4), FSRS-style spaced repetition of weak rules, 'Why you were wrong' replay with ghost car, Architecture, Coach system (RuleEvent -> presentation), Country / locale pack (+14 more)

### Community 10 - "Rule Events & Driver Frames"
Cohesion: 0.12
Nodes (13): RuleEvent, Message, Outcome, RuleId, Severity, Source, Time, Severity (+5 more)

### Community 11 - "Unity Package Dependencies 11"
Cohesion: 0.08
Nodes (24): com.unity.ext.nunit, com.unity.test-framework.performance, dependencies, depth, source, version, dependencies, depth (+16 more)

### Community 12 - "Core .NET Solution"
Cohesion: 0.10
Nodes (18): netstandard2.1, Microsoft.NET.Sdk, net11.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.0), NUnit (4.6.1), NUnit3TestAdapter (6.2.0), NUnit.Analyzers (4.14.0) (+10 more)

### Community 13 - "Unity Package Dependencies 13"
Cohesion: 0.09
Nodes (23): com.unity.modules.hierarchycore, dependencies, depth, source, version, dependencies, depth, source (+15 more)

### Community 14 - "Roundabout Rule Logic"
Cohesion: 0.19
Nodes (6): RoundaboutRule, DriverFrame, Ego, Indicator, Time, LeftNormal

### Community 15 - "Cyclist Passing Rule"
Cohesion: 0.16
Nodes (10): CyclistPassingRule, PassState, RoadUser, Heading, Id, IsCyclist, LengthM, Position (+2 more)

### Community 16 - "Backend & Monetisation Decisions"
Cohesion: 0.10
Nodes (7): Build, test, CI/CD pipeline, Content pipeline (offline Python OSM -> RoadPack), Firebase backend (Auth, Firestore EU, Storage, Remote Config, Crashlytics), OSM ODbL licensing impact, Privacy (UK GDPR, 18+, consent-based analytics), RevenueCat, Technical Decision Records

### Community 17 - "Exec Summary & Top Risks"
Cohesion: 0.11
Nodes (17): Phase 0 Executive Summary, Top 5 risks (scope, OSM quality, rule correctness, touch observation, monetisation), Wolverhampton prototype city, Dispute button (correctness feedback loop), Mobile performance budget (30 fps, sim<=4 ms, rules<=1 ms), Budget to launch (~£2k-5k), Decisions table (2026-11-25), Phased milestones (Prototype -> Vertical slice -> MVP -> Soft launch -> UK launch -> V1 -> US) (+9 more)

### Community 18 - "Unity Package Dependencies 18"
Cohesion: 0.10
Nodes (21): dependencies, depth, source, version, dependencies, depth, source, version (+13 more)

### Community 19 - "Prototype Rule IDs"
Cohesion: 0.21
Nodes (18): GB.BRK.GO_LEFT, GB.JCT.GIVE_WAY, GB.MWY.RED_X, GB.OBS.MSM, GB.RBT.APPROACH_LANE, GB.RBT.GIVE_WAY_RIGHT, GB.RBT.SIGNAL_APPROACH / SIGNAL_EXIT, GB.SPD.BUILT_UP (Wales 20 mph) (+10 more)

### Community 20 - "Roundabout View Interface"
Cohesion: 0.15
Nodes (6): IRoundaboutView, ApproachLanes, ArmCount, SingleLaneRoundaboutView, ApproachLanes, ArmCount

### Community 21 - "2D vs 3D Mock-up"
Cohesion: 0.18
Nodes (17): 2D Top-Down Style, 3D Low-Poly Chase Camera Style (chosen plan), AI Traffic Car, 2D vs 3D Comparison Mock-up (Roundabout Approach), Driving HUD (speedometer, speed limit sign, mirror, indicators, steering wheel, brake/go pedals), Flutter + Flame, Highway Code Rule 185, 2D vs 3D Roundabout Comparison Mock-up (+9 more)

### Community 22 - "Unity Package Dependencies 22"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 23 - "Unity Package Dependencies 23"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 24 - "Ring Vehicles"
Cohesion: 0.21
Nodes (7): RingVehicle, Bearing, LastAccelMps2, Radius, SpeedMps, TwoLaneRoundaboutCourse, DisplayName

### Community 25 - "Unity Package Dependencies 25"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, version, dependencies, depth, source, version (+8 more)

### Community 26 - "Unity Package Dependencies 26"
Cohesion: 0.12
Nodes (16): dependencies, depth, source, version, dependencies, depth, source, version (+8 more)

### Community 27 - "User Journeys & Personas"
Cohesion: 0.16
Nodes (14): J1 First launch + diagnostic drive, J3 Mock test journey, J5 Purchase journey, Explicitly out of scope (DVSA content, theory test, NI, under-18s), Persona P1: left-hand-traffic newcomer, Persona P2: right-hand-traffic newcomer, Persona P3: UK learner, Persona P4: licence switcher or returner (+6 more)

### Community 28 - "Curriculum & Learning Design"
Cohesion: 0.14
Nodes (12): J4 Free drive in a real city, Accessibility support, Curriculum Stages 0-8 (L-plate to Full Licence), Engagement without gimmicks, Free-drive mode (real streets, confidence-gated), Gameplay and Learning Design, Instant feedback by severity (toast/freeze), Lesson unit: learn card -> micro-scenarios -> recap (+4 more)

### Community 29 - "Core Tests CI"
Cohesion: 0.13
Nodes (14): runs-on, steps, jobs, dotnet, name, on, pull_request, push (+6 more)

### Community 30 - "Fixed-Step Clock"
Cohesion: 0.26
Nodes (6): FixedStepClock, Alpha, SimTime, StepSeconds, TickCount, FixedStepClockTests

### Community 31 - "Lane Change Rule Logic"
Cohesion: 0.26
Nodes (3): LaneChangeParams, LaneChangeRule, Pending

### Community 34 - "Unity Package Dependencies 34"
Cohesion: 0.17
Nodes (11): com.ltolocal.rules.core, com.unity.modules.particlesystem, com.unity.modules.tilemap, com.unity.modules.umbra, com.unity.modules.imageconversion@1.0.0, com.unity.modules.particlesystem@1.0.0, com.unity.modules.tilemap@1.0.0, com.unity.modules.umbra@1.0.0 (+3 more)

### Community 35 - "Unity Package Dependencies 35"
Cohesion: 0.17
Nodes (12): com.unity.modules.director, com.unity.modules.timelinefoundation, com.unity.render-pipelines.universal, dependencies, com.ltolocal.rules.core@file:../../core/Rules.Core, com.ltolocal.sim.core@file:../../core/Sim.Core, com.unity.modules.animation@1.0.0, com.unity.modules.director@1.0.0 (+4 more)

### Community 36 - "DriveGame Controller"
Cohesion: 0.25
Nodes (4): DriveGame, View, Chase, Cockpit

### Community 37 - "Touch Controls"
Cohesion: 0.18
Nodes (8): TouchControls, BrakeRect, GoRect, Look, LookLeftRect, LookRightRect, MirrorBottom, SteeringSensitivity

### Community 38 - "Unity Package Dependencies 38"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 39 - "Unity Package Dependencies 39"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 40 - "Unity Package Dependencies 40"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 42 - "Rules.Core Package"
Cohesion: 0.25
Nodes (8): dependencies, com.ltolocal.sim.core@0.1.0, description, displayName, com.ltolocal.sim.core, name, unity, version

### Community 43 - "Car Parameters"
Cohesion: 0.25
Nodes (8): CarParams, CoastDecelMps2, MaxAccelMps2, MaxBrakeMps2, MaxSteerDeg, SteerAtTopSpeedFraction, TopSpeedMps, WheelbaseM

### Community 44 - "Course Interface"
Cohesion: 0.25
Nodes (3): ICourse, DisplayName, StartState

### Community 46 - "Sim.Core Package"
Cohesion: 0.33
Nodes (5): description, displayName, name, unity, version

### Community 47 - "Disclaimer & Ground Rules"
Cohesion: 0.33
Nodes (5): In-app disclaimer (OGL + ODbL attribution), Educational practice tool disclaimer, L to Local (Keep Left) README, Deliverables 01-07 docs, UK Driving Rules Simulator Phase 0 brief

### Community 48 - "Unity Package Dependencies 48"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.ide.rider

### Community 49 - "Roundabout Phases"
Cohesion: 0.40
Nodes (5): Phase, Approaching, Away, Exiting, OnRing

### Community 50 - "Unity Package Dependencies 50"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

### Community 51 - "Unity Package Dependencies 51"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.unityanalytics

### Community 52 - "Unity Package Dependencies 52"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

## Knowledge Gaps
- **167 isolated node(s):** `net11.0`, `coverlet.collector (6.0.4)`, `Microsoft.NET.Test.Sdk (17.14.0)`, `NUnit (4.6.1)`, `NUnit.Analyzers (4.14.0)` (+162 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 415 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **37 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Vec2` connect `Roundabout Geometry & Views` to `Following Distance Rule`, `Roundabout Rule Tests`, `Lane Change & Observation`, `Courses & Scene Setup`, `Rules Engine Files`, `Roundabout Rule Logic`, `Cyclist Passing Rule`, `Ring Vehicles`?**
  _High betweenness centrality (0.048) - this node is a cross-community bridge._
- **Are the 19 inferred relationships involving `Vec2` (e.g. with `CyclistPassingRule.OnFrame()` and `FollowingDistanceRule.HeadwayTo()`) actually correct?**
  _`Vec2` has 19 INFERRED edges - model-reasoned connections that need verification._
- **What connects `net11.0`, `coverlet.collector (6.0.4)`, `Microsoft.NET.Test.Sdk (17.14.0)` to the rest of the system?**
  _167 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Roundabout Rule Tests` be split into smaller, more focused modules?**
  _Cohesion score 0.05106382978723404 - nodes in this community are weakly interconnected._
- **Why does `dependencies` connect `Unity Package Dependencies 18` to `Unity Package Dependencies 5`, `Unity Package Dependencies 38`, `Unity Package Dependencies 6`, `Unity Package Dependencies 8`, `Unity Package Dependencies 7`, `Unity Package Dependencies 39`, `Unity Package Dependencies 11`, `Unity Package Dependencies 40`, `Unity Package Dependencies 13`, `Unity Package Dependencies 48`, `Unity Package Dependencies 50`, `Unity Package Dependencies 51`, `Unity Package Dependencies 52`, `Unity Package Dependencies 22`, `Unity Package Dependencies 23`, `Unity Package Dependencies 25`, `Unity Package Dependencies 26`?**
  _High betweenness centrality (0.048) - this node is a cross-community bridge._
- **Are the 26 inferred relationships involving `CarState` (e.g. with `FollowingDistanceRuleTests.Follow()` and `LaneChangeRuleTests.Drive()`) actually correct?**
  _`CarState` has 26 INFERRED edges - model-reasoned connections that need verification._
- **Should `Roundabout Geometry & Views` be split into smaller, more focused modules?**
  _Cohesion score 0.05616605616605617 - nodes in this community are weakly interconnected._