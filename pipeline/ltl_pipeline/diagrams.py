"""Generate language-free top-down SVG and 1024x1024 PNG learn-card diagrams."""

from __future__ import annotations

import json
import math
import struct
import zlib
from pathlib import Path

from ltl_pipeline.schema import REPO_ROOT

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
DIAGRAMS_DIR = LESSONS_DIR / "diagrams"

# Colours (RGB)
GRASS = (212, 228, 200)
ROAD = (68, 74, 82)
WHITE = (245, 247, 250)
YELLOW = (242, 194, 48)
PLAYER_BLUE = (29, 112, 184)
OTHER_GREY = (146, 154, 164)
ASL_GREEN = (46, 125, 82)
AMBER = (240, 165, 0)
RED = (212, 53, 28)

SIZE = 1024


def _rgb_hex(c: tuple[int, int, int]) -> str:
    return f"#{c[0]:02x}{c[1]:02x}{c[2]:02x}"


class _Canvas:
    """Minimal 1024x1024 raster + SVG dual renderer (zero external deps, no text)."""

    def __init__(self, bg: tuple[int, int, int] = GRASS) -> None:
        self.buf = bytearray([bg[0], bg[1], bg[2]] * (SIZE * SIZE))
        self.svg: list[str] = [
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}">',
            f'  <rect width="{SIZE}" height="{SIZE}" fill="{_rgb_hex(bg)}"/>',
        ]

    def rect(self, x: int, y: int, w: int, h: int, color: tuple[int, int, int]) -> None:
        x0, y0 = max(0, x), max(0, y)
        x1, y1 = min(SIZE, x + w), min(SIZE, y + h)
        r, g, b = color
        row_bytes = bytes([r, g, b]) * max(0, x1 - x0)
        for py in range(y0, y1):
            start = (py * SIZE + x0) * 3
            self.buf[start : start + len(row_bytes)] = row_bytes
        self.svg.append(
            f'  <rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{_rgb_hex(color)}"/>'
        )

    def circle(self, cx: int, cy: int, radius: int, color: tuple[int, int, int]) -> None:
        r2 = radius * radius
        r, g, b = color
        for py in range(max(0, cy - radius), min(SIZE, cy + radius + 1)):
            dy = py - cy
            for px in range(max(0, cx - radius), min(SIZE, cx + radius + 1)):
                dx = px - cx
                if dx * dx + dy * dy <= r2:
                    idx = (py * SIZE + px) * 3
                    self.buf[idx] = r
                    self.buf[idx + 1] = g
                    self.buf[idx + 2] = b
        self.svg.append(
            f'  <circle cx="{cx}" cy="{cy}" r="{radius}" fill="{_rgb_hex(color)}"/>'
        )

    def line(
        self, x0: int, y0: int, x1: int, y1: int, width: int, color: tuple[int, int, int]
    ) -> None:
        dx, dy = x1 - x0, y1 - y0
        steps = max(abs(dx), abs(dy), 1)
        rad = max(1, width // 2)
        r, g, b = color
        for s in range(steps + 1):
            cx = int(round(x0 + dx * s / steps))
            cy = int(round(y0 + dy * s / steps))
            for py in range(max(0, cy - rad), min(SIZE, cy + rad + 1)):
                for px in range(max(0, cx - rad), min(SIZE, cx + rad + 1)):
                    idx = (py * SIZE + px) * 3
                    self.buf[idx] = r
                    self.buf[idx + 1] = g
                    self.buf[idx + 2] = b
        self.svg.append(
            f'  <line x1="{x0}" y1="{y0}" x2="{x1}" y2="{y1}" '
            f'stroke="{_rgb_hex(color)}" stroke-width="{width}" stroke-linecap="round"/>'
        )

    def arrow(
        self, x0: int, y0: int, x1: int, y1: int, color: tuple[int, int, int] = PLAYER_BLUE
    ) -> None:
        self.line(x0, y0, x1, y1, 14, color)
        ang = math.atan2(y1 - y0, x1 - x0)
        head = 34
        for delta in (2.55, -2.55):
            hx = int(round(x1 + head * math.cos(ang + delta)))
            hy = int(round(y1 + head * math.sin(ang + delta)))
            self.line(x1, y1, hx, hy, 14, color)

    def car(self, cx: int, cy: int, color: tuple[int, int, int], horiz: bool = False) -> None:
        w, h = (92, 52) if horiz else (52, 92)
        self.rect(cx - w // 2, cy - h // 2, w, h, color)

    def write(self, svg_path: Path, png_path: Path) -> None:
        self.svg.append("</svg>")
        svg_path.write_text("\n".join(self.svg) + "\n", encoding="utf-8")

        raw = bytearray()
        stride = SIZE * 3
        for y in range(SIZE):
            raw.append(0)
            raw.extend(self.buf[y * stride : (y + 1) * stride])

        def chunk(tag: bytes, data: bytes) -> bytes:
            return (
                struct.pack("!I", len(data))
                + tag
                + data
                + struct.pack("!I", zlib.crc32(tag + data) & 0xFFFFFFFF)
            )

        ihdr = struct.pack("!IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)
        png_bytes = (
            b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(bytes(raw), level=6))
            + chunk(b"IEND", b"")
        )
        png_path.write_bytes(png_bytes)


def _render_lesson_diagram(lesson_id: str) -> _Canvas:
    c = _Canvas()

    if "roundabout" in lesson_id:
        # Four-arm roundabout
        c.rect(412, 0, 200, SIZE, ROAD)
        c.rect(0, 412, SIZE, 200, ROAD)
        c.circle(512, 512, 230, ROAD)
        c.circle(512, 512, 105, GRASS)
        # Broken give-way line on south entry (left lane)
        for gx in range(420, 504, 20):
            c.rect(gx, 742, 12, 10, WHITE)
        # Circulating grey car on offside (right)
        c.car(360, 450, OTHER_GREY, horiz=True)
        # Player blue car approaching in left lane + clockwise arrow
        c.car(456, 860, PLAYER_BLUE)
        c.arrow(456, 800, 456, 680)
        c.arrow(360, 560, 360, 440)
        return c

    if "zebra" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        for y in range(0, SIZE, 60):
            c.rect(508, y, 8, 34, WHITE)
        # Zebra stripes
        for sx in range(372, 652, 40):
            c.rect(sx, 450, 22, 110, WHITE)
        # White zig-zags
        for zy in (280, 620):
            c.line(372, zy, 392, zy + 50, 8, WHITE)
            c.line(392, zy + 50, 372, zy + 100, 8, WHITE)
            c.line(652, zy, 632, zy + 50, 8, WHITE)
            c.line(632, zy + 50, 652, zy + 100, 8, WHITE)
        c.circle(330, 505, 18, AMBER)
        c.circle(694, 505, 18, AMBER)
        c.circle(340, 480, 14, OTHER_GREY)
        c.car(436, 760, PLAYER_BLUE)
        c.arrow(436, 700, 436, 595)
        return c

    if "box-junction" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        c.rect(0, 362, SIZE, 300, ROAD)
        # Yellow box grid
        c.line(362, 362, 662, 362, 8, YELLOW)
        c.line(662, 362, 662, 662, 8, YELLOW)
        c.line(662, 662, 362, 662, 8, YELLOW)
        c.line(362, 662, 362, 362, 8, YELLOW)
        for off in range(60, 300, 60):
            c.line(362 + off, 362, 662, 662 - off, 6, YELLOW)
            c.line(362, 362 + off, 662 - off, 662, 6, YELLOW)
        c.car(436, 220, OTHER_GREY)
        c.car(436, 790, PLAYER_BLUE)
        c.arrow(436, 730, 436, 680)
        return c

    if "traffic-lights" in lesson_id or "advanced-stop" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        c.rect(0, 362, SIZE, 300, ROAD)
        # ASL cycle box
        c.rect(362, 662, 150, 90, ASL_GREEN)
        c.rect(362, 662, 150, 10, WHITE)
        c.rect(362, 752, 150, 10, WHITE)
        c.circle(436, 707, 16, OTHER_GREY)
        c.car(436, 860, PLAYER_BLUE)
        c.arrow(436, 800, 436, 768)
        return c

    if "give-way" in lesson_id or "pedestrians" in lesson_id or "position-after" in lesson_id:
        # T-junction
        c.rect(0, 320, SIZE, 280, ROAD)
        c.rect(382, 600, 260, 424, ROAD)
        for x in range(0, SIZE, 70):
            c.rect(x, 456, 36, 8, WHITE)
        # Double broken give-way lines
        for gx in range(390, 505, 22):
            c.rect(gx, 600, 14, 8, WHITE)
            c.rect(gx, 616, 14, 8, WHITE)
        c.car(740, 390, OTHER_GREY, horiz=True)
        c.car(446, 820, PLAYER_BLUE)
        c.arrow(446, 760, 446, 640)
        return c

    if "dual" in lesson_id or "two-second" in lesson_id:
        # Two-lane dual carriageway (left-hand)
        c.rect(240, 0, 240, SIZE, ROAD)
        c.rect(544, 0, 240, SIZE, ROAD)
        for y in range(0, SIZE, 60):
            c.rect(356, y, 8, 30, WHITE)
            c.rect(660, y, 8, 30, WHITE)
        c.car(300, 320, OTHER_GREY)
        c.car(300, 740, PLAYER_BLUE)
        c.arrow(300, 680, 300, 460)
        return c

    # Default straight urban road (keep-left, MSM, speed, cyclists, following distance)
    c.rect(362, 0, 300, SIZE, ROAD)
    for y in range(0, SIZE, 60):
        c.rect(508, y, 8, 34, WHITE)
    if "cyclist" in lesson_id:
        c.circle(395, 420, 18, OTHER_GREY)
        c.car(465, 720, PLAYER_BLUE)
        c.arrow(465, 660, 540, 440)
    else:
        c.car(586, 300, OTHER_GREY)
        c.car(436, 760, PLAYER_BLUE)
        c.arrow(436, 700, 436, 480)
    return c


def generate_all_diagrams() -> list[Path]:
    """Generate SVG+PNG diagrams for lessons 1-18 and set learnCard.diagram."""
    DIAGRAMS_DIR.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for lf in sorted(LESSONS_DIR.glob("[01][0-9]-*.json")):
        lesson = json.loads(lf.read_text(encoding="utf-8"))
        lid = lesson["id"]
        canvas = _render_lesson_diagram(lid)
        svg_path = DIAGRAMS_DIR / f"{lid}.svg"
        png_path = DIAGRAMS_DIR / f"{lid}.png"
        canvas.write(svg_path, png_path)
        lesson["learnCard"]["diagram"] = f"content/lessons/gb/diagrams/{lid}.png"
        lf.write_text(json.dumps(lesson, indent=2) + "\n", encoding="utf-8")
        written.extend([svg_path, png_path])
    return written


if __name__ == "__main__":
    files = generate_all_diagrams()
    print(f"Generated {len(files)} diagram files.")
