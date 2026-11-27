"""Generate Round 18 visual assets: lesson badges, fault icons, and app icon concepts."""

from __future__ import annotations

import json
import re
import struct
import zlib
from pathlib import Path
from typing import Any

import numpy as np

from ltl_pipeline.schema import REPO_ROOT

SIGNS_DIR = REPO_ROOT / "content" / "countries" / "gb" / "signs"
LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
BADGES_DIR = LESSONS_DIR / "badges"
ICONS_DIR = REPO_ROOT / "content" / "ui" / "icons"
ICON_CONCEPTS_DIR = REPO_ROOT / "content" / "store" / "icon-concepts"

GREEN_RING = (11, 110, 79, 255)  # #0B6E4F
GREEN_LIGHT = (19, 138, 99, 255)  # #138A63
YELLOW = (242, 194, 48, 255)  # #F2C230
WHITE = (255, 255, 255, 255)
RED = (212, 53, 28, 255)  # #D4351C
BLUE = (0, 121, 193, 255)  # #0079C1
AMBER = (245, 166, 35, 255)
CHARCOAL = (24, 28, 34, 255)
ROAD_GREY = (68, 74, 82, 255)

LESSON_SIGN_MAP: dict[str, tuple[str, str]] = {
    "gb-s0-01-keep-left-controls": ("ahead_only", "arrow_up"),
    "gb-s0-02-moving-off-msm": ("turn_right_only", "arrow_right"),
    "gb-s0-03-speed-limits": ("speed_30", "speed_30"),
    "gb-s1-04-give-way-tjunctions": ("give_way", "give_way"),
    "gb-s1-05-pedestrians-h2": ("pedestrian_crossing_ahead", "pedestrian"),
    "gb-s1-06-position-after-turning": ("turn_left_only", "arrow_left"),
    "gb-s2-07-traffic-lights-amber": ("traffic_signals_ahead", "signals"),
    "gb-s2-08-advanced-stop-lines": ("cycle_lane", "cycle"),
    "gb-s2-09-zebra-zigzags": ("pedestrian_crossing_ahead", "zebra"),
    "gb-s2-10-box-junctions": ("stop", "box_junction"),
    "gb-s3-11-roundabout-give-way": ("give_way", "give_way"),
    "gb-s3-12-roundabout-lanes-signals": ("roundabout_ahead", "roundabout"),
    "gb-s3-13-roundabout-stay-in-lane": ("roundabout_ahead", "roundabout"),
    "gb-s3-14-roundabout-cyclists": ("cycle_lane", "cycle"),
    "gb-s4-15-overtaking-cyclists": ("cycle_lane", "cycle"),
    "gb-s4-16-following-distance-town": ("speed_20", "speed_20"),
    "gb-s5-17-dual-carriageway-lanes": ("national_speed_limit", "nsl"),
    "gb-s5-18-two-second-rule-speed": ("national_speed_limit", "nsl"),
    "gb-s8-19-mock-driving-test": ("one_way", "l_plate"),
    "gb-s8-20-roads-near-test-centre": ("zone_20", "zone_20"),
    "gb-s7-21-night-dipped-headlights": ("ahead_only", "headlight"),
    "gb-s7-22-rain-stopping-distances": ("speed_30", "rain"),
    "gb-s7-23-fog-low-visibility": ("traffic_signals_ahead", "fog"),
    "gb-s6-24-motorway-joining-leaving": ("ahead_only", "motorway"),
    "gb-s6-25-motorway-lane-discipline": ("national_speed_limit", "nsl"),
    "gb-s6-26-motorway-red-x-limits": ("no_entry", "red_x"),
    "gb-s6-27-motorway-breakdowns": ("stop", "sos"),
    "gb-s4-28-school-zones": ("school_children", "pedestrian"),
    "gb-s1-29-turning-left-right": ("ahead_only", "arrow_up"),
}

RULE_GROUP_SPECS: list[dict[str, str]] = [
    {
        "id": "signals",
        "sign": "traffic_signals_ahead",
        "motif": "signals",
        "label": "Signals & lights",
    },
    {"id": "junctions", "sign": "give_way", "motif": "give_way", "label": "Junctions & priority"},
    {
        "id": "crossings",
        "sign": "pedestrian_crossing_ahead",
        "motif": "pedestrian",
        "label": "Crossings & VRUs",
    },
    {"id": "speed", "sign": "speed_30", "motif": "speed_30", "label": "Speed limits"},
    {"id": "gaps", "sign": "stop", "motif": "gaps", "label": "Following gaps"},
    {"id": "position", "sign": "ahead_only", "motif": "arrow_up", "label": "Lane position"},
    {
        "id": "roundabouts",
        "sign": "roundabout_ahead",
        "motif": "roundabout",
        "label": "Roundabouts",
    },
    {"id": "motorways", "sign": "national_speed_limit", "motif": "motorway", "label": "Motorways"},
    {"id": "conditions", "sign": "school_children", "motif": "fog", "label": "Weather & lighting"},
]


def _write_rgba_png(path: Path, rgba: np.ndarray) -> None:
    """Write an HxWx4 uint8 numpy array as an RGBA PNG file."""
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))

    def chunk(tag: bytes, payload: bytes) -> bytes:
        crc = zlib.crc32(tag + payload) & 0xFFFFFFFF
        return struct.pack(">I", len(payload)) + tag + payload + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, level=6))
        + chunk(b"IEND", b"")
    )
    path.write_bytes(png)


def _blend_mask(canvas: np.ndarray, mask: np.ndarray, color: tuple[int, int, int, int]) -> None:
    """Alpha-blend a float [0, 1] coverage mask onto an HxWx4 uint8 RGBA canvas."""
    a = np.clip(mask, 0.0, 1.0) * (color[3] / 255.0)
    inv = 1.0 - a
    for c in range(3):
        canvas[:, :, c] = np.clip(
            canvas[:, :, c].astype(np.float32) * inv + color[c] * a,
            0,
            255,
        ).astype(np.uint8)
    canvas_a = canvas[:, :, 3].astype(np.float32) / 255.0
    out_a = a + canvas_a * inv
    canvas[:, :, 3] = np.clip(out_a * 255.0, 0, 255).astype(np.uint8)


def _sdf_circle(xx: np.ndarray, yy: np.ndarray, cx: float, cy: float, r: float) -> np.ndarray:
    dist = np.hypot(xx - cx, yy - cy) - r
    return np.clip(0.5 - dist, 0.0, 1.0)


def _sdf_ring(
    xx: np.ndarray, yy: np.ndarray, cx: float, cy: float, r_outer: float, r_inner: float
) -> np.ndarray:
    return np.clip(
        _sdf_circle(xx, yy, cx, cy, r_outer) - _sdf_circle(xx, yy, cx, cy, r_inner), 0.0, 1.0
    )


def _sdf_rounded_rect(
    xx: np.ndarray, yy: np.ndarray, x0: float, y0: float, x1: float, y1: float, radius: float
) -> np.ndarray:
    cx = (x0 + x1) * 0.5
    cy = (y0 + y1) * 0.5
    hx = (x1 - x0) * 0.5 - radius
    hy = (y1 - y0) * 0.5 - radius
    dx = np.maximum(np.abs(xx - cx) - hx, 0.0)
    dy = np.maximum(np.abs(yy - cy) - hy, 0.0)
    dist = np.hypot(dx, dy) - radius
    return np.clip(0.5 - dist, 0.0, 1.0)


def _sdf_segment(
    xx: np.ndarray, yy: np.ndarray, x0: float, y0: float, x1: float, y1: float, half_w: float
) -> np.ndarray:
    vx, vy = x1 - x0, y1 - y0
    denom = vx * vx + vy * vy
    if denom < 1e-6:
        return _sdf_circle(xx, yy, x0, y0, half_w)
    t = np.clip(((xx - x0) * vx + (yy - y0) * vy) / denom, 0.0, 1.0)
    px = x0 + t * vx
    py = y0 + t * vy
    return np.clip(0.5 - (np.hypot(xx - px, yy - py) - half_w), 0.0, 1.0)


def _sdf_triangle(
    xx: np.ndarray,
    yy: np.ndarray,
    p0: tuple[float, float],
    p1: tuple[float, float],
    p2: tuple[float, float],
) -> np.ndarray:
    def edge(a: tuple[float, float], b: tuple[float, float]) -> np.ndarray:
        return (xx - a[0]) * (b[1] - a[1]) - (yy - a[1]) * (b[0] - a[0])

    s0 = edge(p0, p1)
    s1 = edge(p1, p2)
    s2 = edge(p2, p0)
    inside = ((s0 >= 0) & (s1 >= 0) & (s2 >= 0)) | ((s0 <= 0) & (s1 <= 0) & (s2 <= 0))
    return inside.astype(np.float32)


def _draw_motif(canvas: np.ndarray, xx: np.ndarray, yy: np.ndarray, motif: str, size: int) -> None:
    """Draw a crisp centred UK road-sign motif onto the badge/icon canvas."""
    s = float(size)
    c = s * 0.5
    if motif in ("give_way", "roundabout", "signals", "pedestrian"):
        if motif == "give_way":
            p0, p1, p2 = (
                (c - s * 0.24, c - s * 0.16),
                (c + s * 0.24, c - s * 0.16),
                (c, c + s * 0.26),
            )
            _blend_mask(canvas, _sdf_triangle(xx, yy, p0, p1, p2), RED)
            q0, q1, q2 = (
                (c - s * 0.16, c - s * 0.11),
                (c + s * 0.16, c - s * 0.11),
                (c, c + s * 0.17),
            )
            _blend_mask(canvas, _sdf_triangle(xx, yy, q0, q1, q2), WHITE)
        else:
            p0, p1, p2 = (
                (c, c - s * 0.26),
                (c - s * 0.25, c + s * 0.19),
                (c + s * 0.25, c + s * 0.19),
            )
            _blend_mask(canvas, _sdf_triangle(xx, yy, p0, p1, p2), RED)
            q0, q1, q2 = (
                (c, c - s * 0.16),
                (c - s * 0.17, c + s * 0.14),
                (c + s * 0.17, c + s * 0.14),
            )
            _blend_mask(canvas, _sdf_triangle(xx, yy, q0, q1, q2), WHITE)
            if motif == "signals":
                _blend_mask(
                    canvas,
                    _sdf_rounded_rect(
                        xx, yy, c - s * 0.03, c - s * 0.07, c + s * 0.03, c + s * 0.11, s * 0.015
                    ),
                    CHARCOAL,
                )
                _blend_mask(canvas, _sdf_circle(xx, yy, c, c - s * 0.035, s * 0.018), RED)
                _blend_mask(canvas, _sdf_circle(xx, yy, c, c + s * 0.015, s * 0.018), AMBER)
                _blend_mask(canvas, _sdf_circle(xx, yy, c, c + s * 0.065, s * 0.018), GREEN_RING)
            elif motif == "roundabout":
                _blend_mask(
                    canvas, _sdf_ring(xx, yy, c, c + s * 0.03, s * 0.075, s * 0.048), CHARCOAL
                )
            elif motif == "pedestrian":
                _blend_mask(canvas, _sdf_circle(xx, yy, c, c - s * 0.03, s * 0.022), CHARCOAL)
                _blend_mask(
                    canvas,
                    _sdf_segment(xx, yy, c, c - s * 0.01, c, c + s * 0.08, s * 0.016),
                    CHARCOAL,
                )
    elif motif in ("speed_30", "speed_20", "zone_20"):
        _blend_mask(canvas, _sdf_ring(xx, yy, c, c, s * 0.24, s * 0.17), RED)
        _blend_mask(canvas, _sdf_circle(xx, yy, c - s * 0.055, c, s * 0.05), CHARCOAL)
        _blend_mask(canvas, _sdf_circle(xx, yy, c - s * 0.055, c, s * 0.025), WHITE)
        _blend_mask(canvas, _sdf_circle(xx, yy, c + s * 0.055, c, s * 0.05), CHARCOAL)
        _blend_mask(canvas, _sdf_circle(xx, yy, c + s * 0.055, c, s * 0.025), WHITE)
    elif motif in ("arrow_up", "arrow_left", "arrow_right"):
        _blend_mask(canvas, _sdf_circle(xx, yy, c, c, s * 0.24), BLUE)
        if motif == "arrow_up":
            _blend_mask(
                canvas, _sdf_segment(xx, yy, c, c + s * 0.12, c, c - s * 0.10, s * 0.028), WHITE
            )
            _blend_mask(
                canvas,
                _sdf_triangle(
                    xx,
                    yy,
                    (c, c - s * 0.15),
                    (c - s * 0.07, c - s * 0.05),
                    (c + s * 0.07, c - s * 0.05),
                ),
                WHITE,
            )
        elif motif == "arrow_left":
            _blend_mask(
                canvas, _sdf_segment(xx, yy, c + s * 0.12, c, c - s * 0.08, c, s * 0.028), WHITE
            )
            _blend_mask(
                canvas,
                _sdf_triangle(
                    xx,
                    yy,
                    (c - s * 0.15, c),
                    (c - s * 0.05, c - s * 0.07),
                    (c - s * 0.05, c + s * 0.07),
                ),
                WHITE,
            )
        else:
            _blend_mask(
                canvas, _sdf_segment(xx, yy, c - s * 0.12, c, c + s * 0.08, c, s * 0.028), WHITE
            )
            _blend_mask(
                canvas,
                _sdf_triangle(
                    xx,
                    yy,
                    (c + s * 0.15, c),
                    (c + s * 0.05, c - s * 0.07),
                    (c + s * 0.05, c + s * 0.07),
                ),
                WHITE,
            )
    elif motif == "nsl":
        _blend_mask(canvas, _sdf_ring(xx, yy, c, c, s * 0.24, s * 0.21), CHARCOAL)
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.14, c + s * 0.14, c + s * 0.14, c - s * 0.14, s * 0.042),
            CHARCOAL,
        )
    elif motif == "motorway":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.22, c - s * 0.18, c + s * 0.22, c + s * 0.18, s * 0.04
            ),
            BLUE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.12, c + s * 0.11, c - s * 0.05, c - s * 0.10, s * 0.022),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c + s * 0.12, c + s * 0.11, c + s * 0.05, c - s * 0.10, s * 0.022),
            WHITE,
        )
        _blend_mask(
            canvas, _sdf_segment(xx, yy, c - s * 0.14, c, c + s * 0.14, c, s * 0.016), WHITE
        )
    elif motif == "red_x":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.20, c - s * 0.20, c + s * 0.20, c + s * 0.20, s * 0.04
            ),
            CHARCOAL,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.11, c - s * 0.11, c + s * 0.11, c + s * 0.11, s * 0.028),
            RED,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.11, c + s * 0.11, c + s * 0.11, c - s * 0.11, s * 0.028),
            RED,
        )
    elif motif == "sos":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.22, c - s * 0.16, c + s * 0.22, c + s * 0.16, s * 0.04
            ),
            BLUE,
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.14, c - s * 0.08, c + s * 0.14, c + s * 0.08, s * 0.03
            ),
            AMBER,
        )
    elif motif == "cycle":
        _blend_mask(canvas, _sdf_circle(xx, yy, c, c, s * 0.24), BLUE)
        _blend_mask(
            canvas, _sdf_ring(xx, yy, c - s * 0.09, c + s * 0.04, s * 0.065, s * 0.045), WHITE
        )
        _blend_mask(
            canvas, _sdf_ring(xx, yy, c + s * 0.09, c + s * 0.04, s * 0.065, s * 0.045), WHITE
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.09, c + s * 0.04, c, c - s * 0.05, s * 0.015),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c, c - s * 0.05, c + s * 0.09, c + s * 0.04, s * 0.015),
            WHITE,
        )
    elif motif == "zebra":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.20, c - s * 0.16, c + s * 0.20, c + s * 0.16, s * 0.03
            ),
            CHARCOAL,
        )
        for dx in (-0.11, -0.04, 0.04, 0.11):
            _blend_mask(
                canvas,
                _sdf_rounded_rect(
                    xx,
                    yy,
                    c + s * (dx - 0.02),
                    c - s * 0.12,
                    c + s * (dx + 0.02),
                    c + s * 0.12,
                    s * 0.01,
                ),
                WHITE,
            )
    elif motif == "box_junction":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.18, c - s * 0.18, c + s * 0.18, c + s * 0.18, s * 0.02
            ),
            ROAD_GREY,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.14, c - s * 0.14, c + s * 0.14, c + s * 0.14, s * 0.018),
            YELLOW,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.14, c + s * 0.14, c + s * 0.14, c - s * 0.14, s * 0.018),
            YELLOW,
        )
    elif motif == "gaps":
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.20, c - s * 0.06, c - s * 0.05, c + s * 0.06, s * 0.025
            ),
            BLUE,
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c + s * 0.05, c - s * 0.06, c + s * 0.20, c + s * 0.06, s * 0.025
            ),
            GREEN_RING,
        )
    elif motif in ("headlight", "rain", "fog"):
        _blend_mask(canvas, _sdf_circle(xx, yy, c, c, s * 0.22), CHARCOAL)
        color = YELLOW if motif == "headlight" else (BLUE if motif == "rain" else WHITE)
        for dy in (-0.07, 0.0, 0.07):
            _blend_mask(
                canvas,
                _sdf_segment(xx, yy, c - s * 0.10, c + s * dy, c + s * 0.10, c + s * dy, s * 0.018),
                color,
            )
    else:  # l_plate
        _blend_mask(
            canvas,
            _sdf_rounded_rect(
                xx, yy, c - s * 0.16, c - s * 0.18, c + s * 0.16, c + s * 0.18, s * 0.03
            ),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.06, c - s * 0.10, c - s * 0.06, c + s * 0.10, s * 0.032),
            RED,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, c - s * 0.06, c + s * 0.10, c + s * 0.08, c + s * 0.10, s * 0.032),
            RED,
        )


def _extract_svg_inner(sign_name: str) -> str:
    raw = (SIGNS_DIR / f"{sign_name}.svg").read_text(encoding="utf-8")
    inner = re.sub(r"^<svg[^>]*>", "", raw.strip())
    inner = re.sub(r"</svg>\s*$", "", inner)
    return inner.strip()


def _render_circular_badge_svg(sign_name: str, size: int) -> str:
    inner = _extract_svg_inner(sign_name)
    half = size / 2
    ring_r = size * 0.46
    stroke_w = size * 0.055
    sign_scale = (size * 0.56) / 100.0
    sign_offset = (size - 100.0 * sign_scale) / 2.0
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}"'
        f' width="{size}" height="{size}">\n'
        f'  <circle cx="{half}" cy="{half}" r="{ring_r:.1f}"'
        f' fill="#FFFFFF" stroke="#0B6E4F" stroke-width="{stroke_w:.1f}"/>\n'
        f'  <g transform="translate({sign_offset:.2f},{sign_offset:.2f})'
        f' scale({sign_scale:.4f})">\n'
        f"    {inner}\n"
        f"  </g>\n"
        f"</svg>\n"
    )


def _render_circular_badge_png(motif: str, size: int) -> np.ndarray:
    canvas = np.zeros((size, size, 4), dtype=np.uint8)
    coords = np.arange(size, dtype=np.float32) + 0.5
    yy, xx = np.meshgrid(coords, coords, indexing="ij")
    c = size * 0.5
    _blend_mask(canvas, _sdf_circle(xx, yy, c, c, size * 0.48), GREEN_RING)
    _blend_mask(canvas, _sdf_circle(xx, yy, c, c, size * 0.42), WHITE)
    _draw_motif(canvas, xx, yy, motif, size)
    return canvas


def _render_app_icon_concept(variant: int, size: int = 1024) -> np.ndarray:
    """Render one of 3 'L to Local' 1024x1024 app icon concepts (no DVSA/GOV.UK logos)."""
    canvas = np.zeros((size, size, 4), dtype=np.uint8)
    coords = np.arange(size, dtype=np.float32) + 0.5
    yy, xx = np.meshgrid(coords, coords, indexing="ij")
    s = float(size)
    c = s * 0.5

    bg_color = GREEN_RING if variant in (1, 3) else YELLOW
    _blend_mask(canvas, _sdf_rounded_rect(xx, yy, 0, 0, s, s, s * 0.22), bg_color)

    if variant == 1:
        # Concept 1: Deep green tile, white L-plate with bold green L and yellow corner stripe.
        _blend_mask(
            canvas,
            _sdf_rounded_rect(xx, yy, s * 0.16, s * 0.16, s * 0.84, s * 0.84, s * 0.10),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(xx, yy, s * 0.22, s * 0.22, s * 0.78, s * 0.78, s * 0.06),
            GREEN_RING,
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(xx, yy, s * 0.25, s * 0.25, s * 0.75, s * 0.75, s * 0.04),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.38, s * 0.33, s * 0.38, s * 0.67, s * 0.065),
            GREEN_RING,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.38, s * 0.67, s * 0.64, s * 0.67, s * 0.065),
            GREEN_RING,
        )
        _blend_mask(canvas, _sdf_circle(xx, yy, s * 0.68, s * 0.34, s * 0.065), YELLOW)
    elif variant == 2:
        # Concept 2: Warm yellow tile with green road-sign ring, white centre, bold L.
        _blend_mask(canvas, _sdf_circle(xx, yy, c, c, s * 0.39), GREEN_RING)
        _blend_mask(canvas, _sdf_circle(xx, yy, c, c, s * 0.32), WHITE)
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.40, s * 0.33, s * 0.40, s * 0.65, s * 0.060),
            GREEN_RING,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.40, s * 0.65, s * 0.63, s * 0.65, s * 0.060),
            GREEN_RING,
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(xx, yy, s * 0.28, s * 0.78, s * 0.72, s * 0.84, s * 0.03),
            GREEN_LIGHT,
        )
    else:
        # Concept 3: Keep-left road sweep under a crisp white L-plate badge.
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.24, s * 0.90, s * 0.50, s * 0.52, s * 0.13),
            GREEN_LIGHT,
        )
        _blend_mask(
            canvas, _sdf_segment(xx, yy, s * 0.24, s * 0.90, s * 0.50, s * 0.52, s * 0.018), YELLOW
        )
        _blend_mask(
            canvas,
            _sdf_rounded_rect(xx, yy, s * 0.26, s * 0.14, s * 0.74, s * 0.62, s * 0.08),
            WHITE,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.41, s * 0.24, s * 0.41, s * 0.51, s * 0.055),
            GREEN_RING,
        )
        _blend_mask(
            canvas,
            _sdf_segment(xx, yy, s * 0.41, s * 0.51, s * 0.61, s * 0.51, s * 0.055),
            GREEN_RING,
        )

    return canvas


def generate_all_art_assets() -> dict[str, Any]:
    """Generate R18 lesson badges, fault icons, and 1024px app icon concepts."""
    BADGES_DIR.mkdir(parents=True, exist_ok=True)
    ICONS_DIR.mkdir(parents=True, exist_ok=True)
    ICON_CONCEPTS_DIR.mkdir(parents=True, exist_ok=True)

    badge_count = 0
    for lf in sorted(LESSONS_DIR.glob("[0-9][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        lid = lesson["id"]
        sign_name, motif = LESSON_SIGN_MAP[lid]
        (BADGES_DIR / f"{lid}.svg").write_text(
            _render_circular_badge_svg(sign_name, 256), encoding="utf-8"
        )
        _write_rgba_png(BADGES_DIR / f"{lid}.png", _render_circular_badge_png(motif, 256))
        badge_count += 1

    rule_groups_map: dict[str, dict[str, str]] = {}
    for spec in RULE_GROUP_SPECS:
        gid = spec["id"]
        svg_path = ICONS_DIR / f"{gid}.svg"
        png_path = ICONS_DIR / f"{gid}.png"
        svg_path.write_text(_render_circular_badge_svg(spec["sign"], 96), encoding="utf-8")
        _write_rgba_png(png_path, _render_circular_badge_png(spec["motif"], 96))
        rule_groups_map[gid] = {
            "label": spec["label"],
            "svg": f"content/ui/icons/{gid}.svg",
            "png": f"content/ui/icons/{gid}.png",
            "sourceSign": f"content/countries/gb/signs/{spec['sign']}.svg",
        }

    (ICONS_DIR / "rule_groups.json").write_text(
        json.dumps({"version": "0.1", "groups": rule_groups_map}, indent=2) + "\n",
        encoding="utf-8",
    )

    concept_names = [
        "concept-1-green-l-plate.png",
        "concept-2-yellow-badge-l.png",
        "concept-3-keep-left-l-road.png",
    ]
    for idx, fname in enumerate(concept_names, start=1):
        _write_rgba_png(ICON_CONCEPTS_DIR / fname, _render_app_icon_concept(idx, 1024))

    return {
        "badges": badge_count,
        "faultIcons": len(RULE_GROUP_SPECS),
        "appIconConcepts": len(concept_names),
    }


if __name__ == "__main__":
    summary = generate_all_art_assets()
    print(f"Generated R18 assets: {summary}")
