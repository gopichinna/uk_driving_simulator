# Content Authoring Guide — L to Local ("Keep Left")

This guide explains how to add new GB content and validate it with the Python pipeline (`pipeline/`).

## 1. Plain-English & Rule Accuracy Rules (Non-Negotiable)
- **Never invent road rules.** Every rule must cite a GOV.UK Highway Code rule number and URL in `docs/02-rules-catalogue.md` and `content/countries/gb/rules.yaml`.
- **Plain English (CEFR B1):** One idea per sentence, ideally `<= 12` words, no driving-school jargon from `content/countries/gb/strings/en/jargon_phrases.txt` (e.g. say *"cars behind you"*, not *"following traffic"*).
- **Country-agnostic code:** Use **nearside/offside** and **near turn/far turn** in schemas and code.

## 2. Adding a Lesson (`content/lessons/gb/`)
1. Create `content/lessons/gb/<NN>-<slug>.json` matching `content/schemas/lesson.v0.schema.json` (example: `content/lessons/gb/11-roundabout-give-way.json`).
2. Add its diagram generator in `pipeline/ltl_pipeline/diagrams.py` (producing `<lessonId>.svg` + `1024x1024` `.png` in `content/lessons/gb/diagrams/`).
3. Add its circular badge in `pipeline/ltl_pipeline/art_assets.py` (`content/lessons/gb/badges/<lessonId>.svg|png`).
4. Update `content/lessons/gb/progression.json`, then regenerate `rule_index.json` and `IMPLEMENTATION.md`:
   ```bash
   cd pipeline && uv run python -m ltl_pipeline.rule_index && uv run python -m ltl_pipeline.lesson_report
   ```

## 3. Adding a Scenario (`content/scenarios/gb/`)
1. Create `content/scenarios/gb/<scenarioId>.json` matching `content/schemas/scenario.v0.schema.json` (example: `content/scenarios/gb/s3_11_single_rbt_give_way.json`).
2. Use only supported `course`, `anchor`, and `events` types (`traffic_density`, `parked_car`, `cyclist_ahead`, `pedestrian_waits`, `pedestrian_steps_out`, `car_emerges`, `conditions`, `take_exit`, `finish_at`, `finish_after_s`, `independent_driving`, `red_x`, `variable_limit`, `slow_vehicle_ahead`, `breakdown`).
3. Every **implemented** rule in `rules.yaml` must have `>= 2` non-mock-test practice scenarios (enforced by `pipeline/tests/test_scenarios.py`).

## 4. Adding a Quiz Question or Theory Test Set (`content/quizzes/gb/`)
- **Stage quiz:** `content/quizzes/gb/<NN>-<slug>.json` matching `content/schemas/quiz.v0.schema.json`.
- **Theory bank (`content/quizzes/gb/bank/<rule-id>.json`):** Schema `{"id","rule","question","options":[4],"answer":0..3,"explain","hc_rule","difficulty":1..3}`. Keep `question <= 25` words, quote `hc_rule` in `explain`, and avoid banned jargon.
- Rebuild `INDEX.md` and `content/quizzes/gb/tests/set-01.json`–`set-10.json`:
  ```bash
  cd pipeline && uv run python -m ltl_pipeline.quiz_bank
  ```

## 5. Adding a Voice Line (`content/countries/gb/strings/en/voice.json`)
1. Add `voice.<ruleId>.fault` / `.good` plus `.v2` and `.v3` variants (`<= 12` words each, plain English).
2. Synthesise 16-bit PCM mono 44.1 kHz WAVs with Piper `en_GB-cori-medium`, update `manifest.json`, and regenerate `en-XA`:
   ```bash
   cd pipeline && uv run python -c "from ltl_pipeline.audio import generate_voice_audio; generate_voice_audio()" && uv run python -m ltl_pipeline.pseudolocale
   ```
3. Record any new audio sources/licences (CC0 / CC BY 4.0 only) in `content/audio/LICENCES.md`.

## 6. Adding an OSM Road Pack (`content/cities/<pack_id>/`)
1. Create `config.yaml` with `pack_id`, `origin`, and `bbox` inside the Geofabrik extract (example: `content/cities/suburb_practice/config.yaml`).
2. Run the OSM pipeline (`build_roadpack`, `build_buildings`, `build_pois`, `write_direction_signs_and_street_names`, `write_street_furniture`, `build_pedestrian_routes`) — see `pipeline/ltl_pipeline/practice_areas.py`.
3. Add `SOURCE.md` with bbox, OSM extract timestamp, `© OpenStreetMap contributors (ODbL-1.0)`, and any hand-fixed segments (`confidence: 0.4`).

## 7. Full Validation Commands (Run Before Every PR)
```bash
cd pipeline
uv run ruff check .
uv run pytest -q
uv run python -c "from ltl_pipeline.schema import validate_all_content; validate_all_content()"
uv run python -m ltl_pipeline.readability
uv run python -m ltl_pipeline.health
```
