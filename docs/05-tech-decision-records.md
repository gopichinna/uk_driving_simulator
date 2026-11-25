# 05: Technical Decision Records (ADRs)

Format: Context → Options → Scoring (1–5, higher is better; weights sum to 100) → **Decision** → Consequences.
Status of all ADRs: **Proposed**, waiting for your approval.

---

## ADR-001: Client game engine
**Context.** Solo developer, no game-dev experience, working with AI. Mobile 3D (low-poly), Android + iOS, paid app with in-app purchases and optional rewarded ads. The sim and rules core will be plain C# either way (ADR-002/004).

**Options.** Unity 6 LTS (C#) · Godot 4.x (GDScript/C#) · Flutter + custom 3D sim · Native (Kotlin + Swift, two renderers).

| Criterion | Weight | Unity 6 | Godot 4 | Flutter + custom | Native ×2 |
|---|---|---|---|---|---|
| Beginner learning resources + AI assistance quality | 20 | **5** | 4 | 2 | 2 |
| Mobile 3D performance & tooling (profiler, URP, IL2CPP) | 15 | **5** | 3 | 2 | 4 |
| IAP + ads + Firebase SDK maturity | 15 | **5** | 3 (community plugins) | 4 | 5 |
| iOS/Android parity, one codebase | 10 | **5** | 4 | 4 | 1 |
| Licence cost & risk | 10 | 3 | **5** | 5 | 5 |
| Asset ecosystem (low-poly kits, road tools) | 10 | **5** | 3 | 1 | 1 |
| Hiring pool / freelancers | 10 | **5** | 3 | 3 | 4 |
| Fit with a pure-C# core | 5 | **5** | 4 (C# mobile export newer) | 1 | 2 |
| App size / startup | 5 | 3 | **5** | 4 | 5 |
| **Weighted total** | 100 | **4.70** | 3.65 | 2.85 | 3.20 |

**Licensing / cost evidence.**
- **Unity**: the Personal plan is free up to **US$200k revenue + funding** in the last 12 months. The Runtime Fee was cancelled (Sept 2024). Unity Pro is required above that threshold (~US$2,310/seat/yr after a 5% rise in Jan 2026; `UNVERIFIED` exact price on Unity's own page). The "Made with Unity" splash is optional from Unity 6. Sources: [unity.com/products/pricing-updates](https://unity.com/products/pricing-updates).
- **Godot**: MIT licence, no fees. Ads and IAP on mobile depend on **community plugins** (e.g. Poing Studios AdMob, Godot IAP/OpenIAP). They exist and are active, but versions must be pinned and checked per Godot release.

**Decision: Unity 6**, URP, IL2CPP, C#. *Update 2026-11-25:* the prototype uses **Unity 6.6 (6000.6.10f1)**, the supported release that Unity Hub installed. We move to the next LTS (6.7 LTS, announced) before the vertical slice. Because the core is engine-free, this upgrade only touches the Unity view layer.

**Consequences.**
- The pricing risk Unity created in 2023 is real. We contain it by keeping **sim, rules, learning and packs in engine-free C#**: moving to Godot (C#) later would mean rewriting the view/input/platform layer, not the product's core.
- You will need ~2 weeks of guided Unity learning before productive work (doc 07, task 0).

---

## ADR-002: Simulation & vehicle physics
**Options.** (A) Unity PhysX + WheelCollider · (B) Asset Store vehicle physics package · (C) **Own fixed-step sim + arcade single-track vehicle model in pure C#**.

| Criterion | W | A | B | C |
|---|---|---|---|---|
| Headless testability / CI | 30 | 1 | 1 | **5** |
| Replay reliability | 20 | 2 | 2 | **5** |
| Feel "good enough" for teaching | 20 | 3 | **5** | 4 |
| Effort for a beginner | 20 | **4** | 4 | 3 |
| Engine independence | 10 | 1 | 1 | **5** |
| **Total** | | 2.20 | 2.60 | **4.40** |

**Decision: C.** The vehicle model is small (~300–500 lines). The real complexity is in junction logic and rules, which we have to build anyway. Determinism is per-build only; replays use snapshots (doc 04, section 1).

---

## ADR-003: Rules engine approach
**Options.** (A) Hard-coded rules in game scripts · (B) General rule DSL / Rete engine (e.g. NRules) · (C) **Parameterised rule kinds in C# + YAML rule definitions + a few code rules**.

| Criterion | W | A | B | C |
|---|---|---|---|---|
| Correctness & testability per rule | 30 | 3 | 3 | **5** |
| New country mostly by content | 25 | 1 | **5** | 4 |
| Simplicity for a solo dev | 25 | 4 | 1 | **4** |
| Runtime performance on mobile | 10 | **5** | 3 | 5 |
| Explainability (evidence for replays) | 10 | 3 | 3 | **5** |
| **Total** | | 2.95 | 3.00 | **4.50** |

**Decision: C.** About 20 kinds cover about 85% of catalogue rules. Procedures (roundabout signalling sequence, breakdown) are coded rule classes. YAML is validated with a JSON Schema in CI; each rule needs `source`, `force`, `severity`, `fixtures`, `strings`.

---

## ADR-004: Core language, test framework, packaging
**Decision.** Use C# targeting **.NET Standard 2.1**, the profile Unity supports, and test with NUnit through `dotnet test`. Ship the core to Unity either as prebuilt DLLs or as a local UPM package with assembly definitions. The UPM package is simpler while the project is small, so start with that. Turn nullable reference types on and keep analyzers strict. The core cannot reference `UnityEngine`; an assembly-definition constraint enforces this, and CI fails if anything sneaks in.

---

## ADR-005: Content pipeline & scenario authoring
**Options for OSM processing.** pyosmium · OSMnx · osm2pgsql + PostGIS · Java/osmosis.
**Decision: Python 3.12 + pyosmium + shapely + networkx**, with optional PostGIS for debugging. OSMnx is good for street-level graphs but doesn't model lanes, and its dependencies are heavy. We need our own lane inference anyway. Python is the best language for an AI-assisted solo dev doing GIS work.

**Scenario authoring options.** (A) Our own visual road editor in Unity · (B) YAML + a Unity preview window · (C) An Asset Store road tool (e.g. a spline road kit) exported to YAML.
**Decision: B for the prototype and MVP, then evaluate C at the vertical slice.** A custom editor (A) is the single biggest scope trap.

---

## ADR-006: Data formats
| Data | Format | Why |
|---|---|---|
| Rule packs, curriculum, scenarios, country config (authored) | **YAML** in git → compiled to JSON | Human-diffable, comment-friendly; schema-validated |
| Road packs (runtime) | **FlatBuffers** + gzip | Zero-copy load on mobile, schema evolution, C# + Python codegen |
| Replays | Custom binary: header + input stream + snapshot blocks (MessagePack) | Compact, versioned |
| Strings | JSON per locale (ICU MessageFormat) | Translator tooling support |
| Local progress | SQLite | Robust, queryable, small |

**Prototype exception:** road packs may start as gzipped JSON. A `packFormatVersion` field keeps the move to FlatBuffers in the vertical slice cheap.

---

## ADR-007: Backend
**Options.** Firebase · Supabase · Custom (FastAPI + Postgres) · No backend.

| Criterion | W | Firebase | Supabase | Custom | None |
|---|---|---|---|---|---|
| Official Unity SDKs | 20 | **5** | 2 (community C#) | 2 | 5 |
| Ops burden for solo dev | 20 | **5** | 4 | 1 | 5 |
| Cross-device progress & entitlements | 20 | **5** | 4 | 5 | 1 |
| Cost at small scale | 10 | 4 | 4 | 3 | **5** |
| Offline sync | 10 | **4** (Firestore offline) | 2 | 2 | 1 |
| Data residency / GDPR (EU region) | 10 | 4 | **5** | 5 | 5 |
| Lock-in | 10 | 2 | 4 | **5** | 5 |
| **Total** | 100 | **4.40** | 3.50 | 3.10 | 3.80 |

"None" is fine for the prototype, but it can't give a paid product cross-device progress and restored purchases.
**Decision: Firebase** (Auth, Firestore EU, Storage/CDN, Remote Config, Crashlytics). Data access stays behind our own `IProgressStore` / `IContentStore` interfaces, so a switch to Supabase later is contained. `UNVERIFIED:` the current Firebase free-tier quotas; check them at setup.

---

## ADR-008: Monetisation plumbing
**Decision.**
- **RevenueCat** handles one non-consumable "Full Licence" product. It gives cross-store entitlements, receipt validation and restore without our own server. `UNVERIFIED:` the RevenueCat free tier threshold; it's revenue-based and free at small scale.
- **Price £14.99**; free tier = first 3 lessons (decided 2026-11-25).
- **AdMob rewarded ads** via the official Unity plugin, **optional, V1 at the earliest**, behind a Remote Config flag. App is 18+, so ads may be personalised with consent via Google's UMP / Apple ATT (`UNVERIFIED:` UMP setup for the UK).
- **Rejected:** subscriptions in v1, interstitial and banner ads, and paid-upfront store pricing. Paid-upfront kills discovery and the chance to try before buying. Free download + one-off unlock is the "paid app" model learners understand.

---

## ADR-009: Analytics & privacy
**Decision.** Firebase Analytics, with:
- consent mode;
- ad-ID collection **disabled**;
- a minimal event schema (doc 04, section 12);
- no location upload;
- BigQuery export for analysis.

**Alternative considered:** PostHog EU. It's more privacy-friendly, but it adds a second vendor; revisit at V1.

---

## ADR-010: CI/CD
**Decision.**
- **GitHub Actions** runs core tests on Linux and pipeline tests in Python.
- **Local builds first**: your MacBook Air (Apple silicon) builds both Android and iOS. **GameCI** in CI produces Android builds on Linux; iOS CI on a macOS runner can wait until builds are routine.
- **fastlane** uploads to TestFlight and the Play internal track. Firebase App Distribution handles quick Android tests.
- Weekly scheduled jobs: the Highway Code update diff and an OSM pack rebuild with a validation report.
- **Alternative:** Unity Build Automation (paid, simpler). Fall back to it if GameCI licence activation is painful.

---

## ADR-011: Map data source & licence
**Options.** OpenStreetMap (ODbL) · Ordnance Survey OS Open Roads (OGL, no lane detail) · commercial (HERE/TomTom; costly, restrictive).
**Decision.** Use **OSM** for city packs and honour ODbL (doc 04, section 7.1). OS Open data is a possible cross-check for road classification. The national speed limit is resolved from the country pack, not the map.

---

## ADR-012: Art direction & assets
**Decision.**
- Flat-shaded low-poly style with **procedural roads**, markings drawn as decals from lane data, and **extruded OSM footprints** for buildings.
- Buy one coherent low-poly city kit and one vehicle and character kit from the Asset Store, roughly £100–300 (`ASSUMPTION`).
- **Signs** use the OGL Crown-copyright traffic-sign images, credited.
- A consistent palette, plus a colour-blind check.

---

## ADR-013: Localisation
**Decision.**
- **Unity Localization package** with string tables keyed per `rule_id`/screen, and ICU MessageFormat for plurals.
- English (en-GB) only in v1.
- `preferred_learning_locale` captured at onboarding.
- Noto fonts loaded per locale as addressable assets.
- An RTL layout check in V1 design reviews (doc 03, section 9).
