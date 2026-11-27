# Localisation Extraction Plan (Hard-Coded English)

**Context:** `python -m ltl_pipeline.l10n` reports **245** hard-coded English fields inside `content/**` outside `content/countries/gb/strings/`. Do **not** move them yet; this plan groups them and sets the extraction order before a second language ships.

## 1. Inventory by File Type (245 fields)

| Group | Source files | Typical fields | Must move before L2? | Est. effort |
|---|---|---|---|---|
| **A. Lesson UI text** | `content/lessons/gb/[0-9][0-9]-*.json` | `title`, `learnCard.blocks[]`, `scenarios[].goal`, `recap[]` | **Yes** (player-facing) | 1.5 days |
| **B. Lesson & Bank quizzes** | `content/lessons/gb/quiz/*.json`, `content/quizzes/gb/bank/*.json`, `tests/set-*.json` | `question`, `options[]`, `explanation` / `explain` | **Yes** (player-facing) | 2.0 days |
| **C. Scenario briefings** | `content/scenarios/gb/*.json` | `events[].instruction` (`independent_driving`) | **Yes** (player-facing) | 0.5 day |
| **D. Store & legal docs** | `content/store/**`, `content/legal/**` | Listing copy, captions, privacy/credits | **Store-only** (Store console, not runtime JSON) | 0.5 day |
| **E. Author-only metadata** | `content/countries/gb/signs.yaml`, `cities/*/QA.md` | Internal notes / comments | **No** (can stay English) | 0 days |

## 2. Recommended Key Scheme (`content/countries/<cc>/strings/<lang>/`)
- `lessons.json` (`kind: "lessons"`):
  - `lesson.<id>.title`
  - `lesson.<id>.learn.<idx>`
  - `lesson.<id>.recap.<idx>`
  - `scenario.<scenarioId>.goal`
  - `scenario.<scenarioId>.instruction.<idx>`
- `quizzes.json` (`kind: "quizzes"`):
  - `quiz.<questionId>.question`
  - `quiz.<questionId>.option.<idx>`
  - `quiz.<questionId>.explain`

## 3. Recommended Migration Order
1. **Step 1 (Schema + Fallback):** Add optional `titleKey` / `goalKey` / `questionKey` fields (or lookup-by-ID in Unity) so existing English JSON keeps working with zero breakage.
2. **Step 2 (Lessons + Scenarios — Groups A & C):** Extract lesson titles, learn cards, recaps, and scenario goals/instructions into `strings/en/lessons.json` + `en-XA`.
3. **Step 3 (Quizzes — Group B):** Extract lesson quizzes and theory bank questions into `strings/en/quizzes.json` + `en-XA` (note: non-GB countries will also swap Highway Code rule references).
4. **Step 4 (CI Gate):** Switch `python -m ltl_pipeline.l10n` hard-coded English check from warning to error once Groups A–C are extracted.
