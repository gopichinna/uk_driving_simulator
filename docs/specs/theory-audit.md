# Theory Test Audit: `set-01` through `set-10`

**Audited against GOV.UK:** 2026-11-27 (`https://www.gov.uk/guidance/the-highway-code`, OGL v3.0).  
**Scope:** All 500 questions across `content/quizzes/gb/tests/set-01.json`–`set-10.json` (50 questions per set, drawn from all 24 `content/quizzes/gb/bank/GB.*.json` files).

---

## 1. Summary of Findings (`set-01`–`set-10`)

- **Factual accuracy & single best answer:** All questions across `set-01`–`set-03` (R35.5) and `set-04`–`set-10` (R36.3) have one unambiguously correct answer matching the cited Highway Code rule (`0` wrong answers found).
- **Highway Code rule numbers (`hc_rule`):** Verified every `hc_rule` reference against `content/countries/gb/rules.yaml` and GOV.UK (`103`, `124`, `126`, `133`, `160`, `163`, `170`, `171`, `172`, `174`, `175`, `177`, `178`, `180`, `182`, `185`, `186`, `191`, `195`, `196`, `208`, `210`, `219`, `238`, `H1`, `H2`): `0` rule-number mismatches.
- **Plain B1 wording & sentence-length fixes (R35.5 + R36.4):**
  - Normalised all-caps emphasis (`MUST`, `MUST NOT`, `STOP`, `RED`, `AMBER`, `RED AND AMBER`, `GREEN`, `NOT`, `NEVER`, `BOTH`, `SCHOOL KEEP CLEAR`) to standard case across the bank.
  - Split and shortened all ~153 long (`16–26` word) questions and explanations across the bank so **every sentence in the entire quiz bank is ≤ 15 words** and passes `check_text_readability()` with **0 errors**.
  - Rebuilt `content/quizzes/gb/bank/INDEX.md` and all 10 theory test sets (`set-01`–`set-10`) via `python -m ltl_pipeline.quiz_bank`.
