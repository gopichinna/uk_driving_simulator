# Localisation Guide (L to Local)

L to Local ships **English only (`en`) for v1** (Great Britain). All UI, coach voice, rules, and glossary strings are structured under `content/countries/<cc>/strings/<locale>/` so additional languages can be added cleanly later without code changes.

---

## 1. String Keying Conventions

Each locale folder (`en/`, pseudo-locale `en-XA/`, future `ur/`, `pl/`, `ar/`, etc.) contains 4 JSON files (`strings.v0.schema.json`):
- `ui.json` (`kind: "ui"`): screen titles, buttons, onboarding, paywall, settings, and accessibility labels (`btn.*`, `onboarding.*`, `settings.*`, `a11y.*`, `debrief.*`).
- `rules.json` (`kind: "rules"`): per-rule player-facing text keyed as `rule.<RULE_ID>.(title|fault|why|tip|good)`.
- `voice.json` (`kind: "voice"`): coach voice lines keyed as `voice.<RULE_ID>.(fault|good)(|.v2|.v3)` and `voice.lesson.*`.
- `glossary.json` (`kind: "glossary"`): plain-English driving terms (`glossary.<term>`).

**Button length budget:** Keep `ui.*` button labels (`*.btn.*`, `btn.*`, `*.cta`, `*.confirm`, `*.skip`) at **<= 40 characters** so mobile UI layouts never clip in longer languages (e.g. German/Polish +35% expansion).

---

## 2. Placeholders & Plurals

- Use named or indexed curly-brace placeholders: `{n}`, `{count}`, `{price}`, `{rule}`, `{mph}`.
- Every translation **must** preserve the exact set of `{...}` placeholder tokens from `en` (checked automatically by `python -m ltl_pipeline.l10n`).
- For counts that vary by plural form, use explicit keys (e.g. `*.one` / `*.other`) rather than concatenating `"s"` in code.

---

## 3. Do-Not-Translate-Literally Glossary

These UK Highway Code terms must be adapted to the official UK meaning rather than translated word-for-word:
- **Give way** (UK priority rule — do not translate as "yield" in regions where another legal sign term applies without explanation).
- **Keep left** (left-hand traffic discipline).
- **Box junction** (yellow criss-cross box where you must not stop unless turning right).
- **Advanced stop line (ASL)** / **cycle box** (two white stop lines at traffic lights).
- **Zebra crossing** / **Belisha beacon** / **Pelican crossing** / **Puffin crossing** / **Toucan crossing** (distinct UK crossing types).
- **Hard shoulder** / **Emergency area** / **Red X** (UK motorway lane terms).
- **Driving fault** / **Serious fault** / **Dangerous fault** (official DVSA practical test marking categories).

---

## 4. How to Add a New Locale & Review

1. Copy `content/countries/gb/strings/en/` to `content/countries/gb/strings/<bcp47>/` and set `"language": "<bcp47>"` in each file.
2. Translate `entries` while keeping all `{...}` placeholders intact and button labels `<= 40` characters.
3. Run `cd pipeline && uv run python -m ltl_pipeline.l10n` to check for missing/extra keys, placeholder mismatches, and button length overflows.
4. Have a native speaker **and** a UK driving-test-familiar reviewer verify driving terminology before shipping.
5. Verify pseudo-locale (`en-XA`) stays up to date via `python -m ltl_pipeline.pseudolocale`.
