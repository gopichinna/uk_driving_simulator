# 08: Parallel Work Plan (Claude + Google Hydrogen)

*Created 2026-11-25. Two AI agents work on separate branches at the same time, and the owner merges both into `main`.*

## 1. Approach
The work is split **by layer, not by feature**, so each agent mostly touches different directories and merges are close to conflict-free. A small set of **contracts** (data schemas + a sample file) is committed to `main` *before* branching. Both agents build against them, so they can work independently and still fit together.

```mermaid
flowchart LR
  subgraph H["hydrogen/osm-pipeline-rule-pack"]
    OSM[Wolverhampton OSM extract] --> PIPE[pipeline/ Python] --> PACK[content/cities/wolverhampton/roadpack.json]
    CAT[docs/02 rules catalogue] --> RY[content/countries/gb/rules.yaml]
  end
  subgraph C["claude/town-centre-junctions"]
    CORE[core/ sim + rules] --> UNITY[unity/ courses, debrief, settings]
    LOADER[RoadPack loader in core/] --> UNITY
  end
  SCHEMA[(content/schemas/*.json<br/>contracts on main)] --> PIPE
  SCHEMA --> LOADER
  RY -. rule ids checked against .-> CORE
  PACK -. loaded by .-> LOADER
```

## 2. Branch goals

### Claude, branch `claude/town-centre-junctions`
**Goal:** a playable **town-centre course** that teaches the everyday junction rules, plus the **debrief and replay** loop that makes feedback stick. In detail:
- a hand-authored town centre with traffic lights, a zebra crossing, give-way T-junctions and pedestrians;
- the core prototype rules for those junctions (red light, amber, zebra, give way, pedestrians at junctions, H2);
- a post-drive **debrief with "why you were wrong" replays**;
- a **Settings** screen (steering sensitivity);
- a C# **RoadPack loader** that reads Hydrogen's Wolverhampton pack, so the merge can produce a real-streets drive.

**Done when:** all of the above runs on the OnePlus 12 with green tests, and the loader passes on `content/samples/roadpack.sample.json`.

### Google Hydrogen, branch `hydrogen/osm-pipeline-rule-pack`
**Goal:** the **data side**:
1. a tested Python **OSM → RoadPack v0** pipeline that produces `content/cities/wolverhampton/roadpack.json` for a ~1 km² area of Wolverhampton city centre, with ODbL attribution and per-element confidence;
2. a machine-readable **GB rule pack** (`content/countries/gb/rules.yaml`) generated from `docs/02`, validated against its schema;
3. a **verification pass** on every `UNVERIFIED` item in `docs/02`, using GOV.UK sources;
4. **CI** for the pipeline, plus a weekly **Highway Code update watcher**.

**Done when:** `pytest` passes, the generated pack validates against `content/schemas/roadpack.v0.schema.json`, `rules.yaml` validates and covers every catalogue row, and the CI workflows are green.

### Round 2 (from 2026-11-25, after PR #1)
- **Hydrogen, branch `hydrogen/content-layer`:** the **content layer**:
  - rule strings (plain English, keyed for translation) and a glossary;
  - a GB traffic-sign catalogue (licence checked first);
  - lessons for Stages 0–2 (lessons 1–3 are the free tier);
  - Wolverhampton pack v0.1 (turn restrictions, QA report) and 3 free-drive routes.

  Tasks: `docs/tasks/hydrogen-branch.md`.
- **Claude, branch `claude/town-centre-junctions`:** unchanged (§2 above); the loader can now test on the real Wolverhampton pack.
- Each agent works in its **own folder**:
  - Hydrogen: `coding_space/uk_driving_simulator`;
  - Claude: the git worktree `coding_space/uk_driving_simulator-claude`.

  This keeps the two checkouts from changing under each other.

## 3. File ownership (who may edit what)

| Path | Owner | The other agent may |
|---|---|---|
| `core/**`, `unity/**` | Claude | read only |
| `.github/workflows/core-tests.yml` | Claude | read only |
| `pipeline/**` | Hydrogen | read only |
| `content/countries/**`, `content/cities/**`, `content/lessons/**`, `content/ATTRIBUTIONS.md` | Hydrogen | read only |
| `.github/workflows/pipeline*.yml`, `.github/workflows/highway-code-watch.yml` | Hydrogen | read only |
| `docs/02-rules-catalogue.md` | Hydrogen (verification edits) | read only |
| `docs/tasks/claude-branch.md` | Claude | read; add requests under "Requests from Hydrogen" |
| `docs/tasks/hydrogen-branch.md` | Hydrogen | read; add requests under "Requests from Claude" |
| `content/schemas/**`, `content/samples/**` | **Shared contract**: change only by agreement, recorded in both task files | propose changes |
| `AGENTS.md`, `GEMINI.md`, `docs/00`–`07`, `docs/08` | Owner (user). Agents may append dated notes to the end of `docs/08` | |
| `graphify-out/**` | Regenerated after merges on `main` only | don't edit |

## 4. Contracts (on `main` before branching)
- `content/schemas/roadpack.v0.schema.json`: the road-pack format: link-level road graph, lane counts, speed limits, crossings, signals, zones, confidence, ODbL source block. Local metres: x = east, z = north, from an origin lat/lon.
- `content/samples/roadpack.sample.json`: a tiny valid pack (a T-junction with traffic lights and a zebra). Claude's loader tests use it; Hydrogen's validator tests use it too.
- `content/schemas/rulepack.v0.schema.json`: the machine-readable rule entries (id, plain English, source, force, verified, phase, severity, detection, implementation reference).

## 5. Merge order and checkpoints
1. Hydrogen merges first (independent of Unity), after its CI is green.
2. Claude rebases on `main`, switches the loader test to the real Wolverhampton pack, then merges.
3. Integration step (Claude, after both merges): a "Wolverhampton (beta)" free-drive course built from the pack, with only high-confidence rules firing.
4. After each merge: `dotnet test`, `pytest`, an APK build, and a graphify `--update`.

## 6. Notes (append below, newest last)
- 2026-11-25: plan created; contracts v0 committed to `main`.
- 2026-11-25 (Hydrogen): delivered `ltl_pipeline` (`pipeline/`), `content/cities/wolverhampton/roadpack.json` (953 nodes, 1079 links, 119 crossings, 3 tram-street ways excluded), `content/countries/gb/speed_defaults.yaml`, `content/countries/gb/rules.yaml` (130 rules, all `core/Rules.Core` IDs linked including `GB.RBT.WRONG_WAY` added to `docs/02`), GOV.UK verification of `docs/02` §5, and CI workflows (`pipeline.yml`, `highway-code-watch.yml`). All 18 pytest tests and `ruff check` pass; all content packs validate against v0 schemas. No schema changes needed.
- 2026-11-25 (Claude): reviewed and merged PR #1 (`1a0ebd2`); CI green (dotnet + python). Round 2 tasks were written for Hydrogen. New schemas `strings.v0`, `signs.v0`, `lesson.v0` and `route.v0`, plus the additive roadpack v0.1 `restrictions[]`, are pre-agreed (see both task files).
