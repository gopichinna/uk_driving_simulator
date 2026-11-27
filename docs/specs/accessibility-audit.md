# Accessibility Text & Diagram Audit

**Checked:** 2026-11-27 across `content/countries/gb/strings/en/*.json`, `content/lessons/gb/*.json`, `content/lessons/gb/quiz/*.json`, `content/quizzes/gb/bank/*.json`, `content/lessons/gb/diagrams/*.svg`, `content/lessons/gb/badges/*.svg`, and `content/ui/icons/`.

---

## 1. Plain-English & Readability Check
- **Sentences over 15 words:** `0` across all `en/*.json` string tables, 29 lessons, 29 lesson quizzes, 64 scenarios, routes, and all 25 theory quiz bank files (`python -m ltl_pipeline.readability` = 0 errors).
- **Jargon not in glossary:** `0` unglossed terms or non-glossary abbreviations.

## 2. Colour as the Only Signal
- **Learn-card diagrams (`diagrams.py`):**
  - Player vehicle (`PLAYER_BLUE`) and other vehicles (`OTHER_GREY`) are differentiated by both colour and directional arrow/path (`polyline_arrow`) showing intended movement.
  - Pedestrian crossings use high-contrast black/white zebra stripes and zig-zag lines, not colour alone.
  - Traffic-light diagrams use vertical 3-lens position (top red, middle amber, bottom green) in addition to lens colour.
- **Fault icons & lesson badges (`art_assets.py`):**
  - Each of the 9 rule-group icons (`signals`, `junctions`, `crossings`, `speed`, `gaps`, `position`, `roundabouts`, `motorways`, `conditions`) and 29 lesson badges pairs a distinct geometric UK road-sign shape/motif (triangle, octagon, circle, arrow, zebra, motorway) with colour, so colour-blind users can distinguish every icon by shape.
- **Debrief & HUD (`ui.json`):**
  - Fault counts use explicit text labels (`driving fault`, `serious fault`, `dangerous fault`) alongside colour coding.

## 3. Screen-Reader (`a11y.*`) Coverage
- **Added in R37.4 to `content/countries/gb/strings/en/ui.json` (and `en-XA`):**
  - All 9 rule-group fault icons: `a11y.icon.signals`, `a11y.icon.junctions`, `a11y.icon.crossings`, `a11y.icon.speed`, `a11y.icon.gaps`, `a11y.icon.position`, `a11y.icon.roundabouts`, `a11y.icon.motorways`, `a11y.icon.conditions`.
  - Parametric screen-reader labels for all 29 lesson diagrams and 29 badges: `a11y.diagram.lesson` (`"Top-down road diagram for {lesson}"`) and `a11y.badge.lesson` (`"Completion badge for {lesson}"`).
  - Existing HUD and screen labels (`a11y.hud.*`, `a11y.screen.*`) remain intact.
