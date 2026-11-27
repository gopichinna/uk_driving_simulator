"""Tests for learn-card diagrams: island avoidance, left-hand lane, and contact sheet."""

from __future__ import annotations

import json
import math

from ltl_pipeline.diagrams import LESSONS_DIR, _render_lesson_diagram


def test_diagrams_avoid_islands_and_stay_left() -> None:
    lesson_files = sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json"))
    assert len(lesson_files) == 31

    for lf in lesson_files:
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        lid = lesson["id"]
        canvas = _render_lesson_diagram(lid)
        assert canvas.geom.path_points, f"{lid}: no path points recorded"

        # Path points must never fall inside any roundabout central island
        for px, py in canvas.geom.path_points:
            for cx, cy, r in canvas.geom.islands:
                assert math.hypot(px - cx, py - cy) > r, (
                    f"{lid}: path point ({px}, {py}) inside island ({cx}, {cy}, r={r})"
                )

        # For straight/approach roads with a directed centreline (going up, y decreasing),
        # left-hand traffic means x <= centreline_x (2D cross product check)
        for x0, y0, x1, y1 in canvas.geom.centrelines:
            vx, vy = x1 - x0, y1 - y0
            for px, py in canvas.geom.path_points:
                # Cross product (v x (p - p0)): in screen coords (x right, y down),
                # left of v has vx*(py-y0) - vy*(px-x0) < 0
                cross = vx * (py - y0) - vy * (px - x0)
                if "position-after" not in lid:
                    assert cross <= 0.0, (
                        f"{lid}: path point ({px}, {py}) crossed to right of centreline"
                    )


def test_contact_sheet_exists() -> None:
    from ltl_pipeline.schema import REPO_ROOT

    assert (REPO_ROOT / "docs" / "images" / "diagrams-contact-sheet.png").is_file()
