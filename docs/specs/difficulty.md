# Scenario Difficulty Rating Method

**Applies to:** All lesson scenarios in `content/scenarios/gb/*.json` (`scenario.v0.schema.json` optional `difficulty` integer `1..3`).

## 1. Scoring Factors (0–8 points)
Each scenario is scored deterministically from four observable properties:

1. **Traffic density (`trafficDensity`):**
   - `0` pts: `<= 0.35`
   - `1` pt: `0.36` to `0.55`
   - `2` pts: `> 0.55`
2. **Speed & road class (`course`):**
   - `0` pts: `town`, `suburb_practice`, `wolverhampton-test-area`
   - `1` pt: `roundabout`, `two_lane_roundabout`, `wolverhampton`, `rural_practice`, `ring_road_practice`, `coventry`
   - `2` pts: `dual_carriageway`, `motorway`
3. **Junction & hazard complexity (`events` & `pedestrianIntent`):**
   - `0` pts: straight flow or single basic cue
   - `1` pt: junction, roundabout, cyclist, or `cross` / `random` pedestrian intent
   - `2` pts: `conditions` (night/rain/fog), `independent_driving`, `red_x` / `breakdown`, or `>= 3` hazard events
4. **Rules judged (`len(rulesJudged)`):**
   - `0` pts: `1–2` rules
   - `1` pt: `3` rules
   - `2` pts: `>= 4` rules

## 2. Lesson Ladder Normalisation (`1` = Easy, `2` = Medium, `3` = Hard)
Within each lesson, scenarios form a progressive ladder:
- The first scenario (`scenarios[0]`) is always rated `1` (entry level for that lesson's topic).
- Subsequent scenarios are rated from `delta = score - first_score`:
  - `delta <= 0` → `1`
  - `1 <= delta <= 2` → `2`
  - `delta >= 3` → `3`
- Step-change guard: `abs(difficulty[i] - difficulty[i - 1]) <= 1` (clamped if a raw jump exceeds `1`).
