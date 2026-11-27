# Theory Test Audit: `set-01`, `set-02`, and `set-03`

**Audited against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Scope:** All 150 questions across `content/quizzes/gb/tests/set-01.json`, `set-02.json`, and `set-03.json` (50 questions per set, drawn from `content/quizzes/gb/bank/GB.*.json`).

---

## 1. Summary of Findings

- **Factual accuracy & single best answer:** All 150 questions across `set-01`–`set-03` have one unambiguously correct answer matching the cited Highway Code rule (`0` wrong answers found).
- **Highway Code rule numbers (`hc_rule`):** Verified all `hc_rule` references against `content/countries/gb/rules.yaml` and GOV.UK (`103`, `124`, `126`, `133`, `160`, `163`, `170`, `171`, `172`, `174`, `175`, `177`, `178`, `182`, `185`, `186`, `191`, `195`, `196`, `208`, `210`, `219`, `238`, `H1`, `H2`): `0` rule-number mismatches.
- **Plain B1 wording & formatting fixes applied:**
  - Normalised non-glossary all-caps emphasis (`MUST`, `MUST NOT`, `STOP`, `RED`, `AMBER`, `RED AND AMBER`, `GREEN`) to standard lowercase (`must`, `must not`, `stop`, `red`, `amber`, `red and amber`, `green`) across the quiz bank files (e.g. `qb_jct_gw_3`, `qb_jct_h2_4`, `qb_jct_stop_1`, `qb_tl_red_1`, `qb_tl_amber_1`, `qb_tl_ra_1`, `qb_xng_zebra_2`, `qb_sch_slow_3`) so screen readers and B1 readability checks treat them cleanly.
  - Rebuilt all 10 theory test sets (`python -m ltl_pipeline.quiz_bank`) so `set-01`–`set-10` reflect the normalised bank text.
- **Note for future polish:** ~153 explanations/questions across the wider 24-rule bank still use 16–23-word single sentences (grammatically clear, e.g. `"Highway Code Rule 174 states you must not enter a box junction unless your exit is clear."`), which can be split into two <= 15-word sentences in a follow-up pass if strict 15-word sentence caps are enforced on the quiz bank too.
