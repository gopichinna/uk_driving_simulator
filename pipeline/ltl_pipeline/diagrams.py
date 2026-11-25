"""Generate language-free top-down SVG, 1024x1024 PNG diagrams, and a contact sheet."""

from __future__ import annotations

import json
import math
import struct
import zlib
from dataclasses import dataclass, field
from pathlib import Path

from ltl_pipeline.schema import REPO_ROOT

LESSONS_DIR = REPO_ROOT / "content" / "lessons" / "gb"
DIAGRAMS_DIR = LESSONS_DIR / "diagrams"

GRASS = (212, 228, 200)
ROAD = (68, 74, 82)
WHITE = (245, 247, 250)
YELLOW = (242, 194, 48)
PLAYER_BLUE = (29, 112, 184)
OTHER_GREY = (146, 154, 164)
ASL_GREEN = (46, 125, 82)
AMBER = (240, 165, 0)
DARK_GREY = (40, 44, 50)

SIZE = 1024


def _rgb_hex(c: tuple[int, int, int]) -> str:
    return f"#{c[0]:02x}{c[1]:02x}{c[2]:02x}"


@dataclass
class DiagramGeometry:
    """Geometry metadata used by automated tests to verify left-hand & island rules."""

    path_points: list[tuple[float, float]] = field(default_factory=list)
    islands: list[tuple[float, float, float]] = field(default_factory=list)  # (cx, cy, r)
    # List of (x0, y0, x1, y1) directed road centrelines in travel direction
    centrelines: list[tuple[float, float, float, float]] = field(default_factory=list)


class _Canvas:
    """Minimal 1024x1024 raster + SVG dual renderer (no text)."""

    def __init__(self, bg: tuple[int, int, int] = GRASS) -> None:
        self.buf = bytearray([bg[0], bg[1], bg[2]] * (SIZE * SIZE))
        self.svg: list[str] = [
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}">',
            f'  <rect width="{SIZE}" height="{SIZE}" fill="{_rgb_hex(bg)}"/>',
        ]
        self.geom = DiagramGeometry()

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

    def ring(
        self,
        cx: int,
        cy: int,
        radius: int,
        width: int,
        color: tuple[int, int, int],
        dashed: bool = True,
    ) -> None:
        steps = 48 if dashed else 96
        for i in range(steps):
            if dashed and i % 2 == 1:
                continue
            a0 = 2 * math.pi * i / steps
            a1 = 2 * math.pi * (i + 0.8) / steps
            self.line(
                int(round(cx + radius * math.cos(a0))),
                int(round(cy + radius * math.sin(a0))),
                int(round(cx + radius * math.cos(a1))),
                int(round(cy + radius * math.sin(a1))),
                width,
                color,
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

    def polyline_arrow(
        self,
        pts: list[tuple[int, int]],
        color: tuple[int, int, int] = PLAYER_BLUE,
        width: int = 14,
        record_path: bool = True,
    ) -> None:
        if record_path:
            self.geom.path_points.extend((float(x), float(y)) for x, y in pts)
        for i in range(len(pts) - 1):
            self.line(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], width, color)
        x0, y0 = pts[-2]
        x1, y1 = pts[-1]
        ang = math.atan2(y1 - y0, x1 - x0)
        head = 32
        for delta in (2.55, -2.55):
            hx = int(round(x1 + head * math.cos(ang + delta)))
            hy = int(round(y1 + head * math.sin(ang + delta)))
            self.line(x1, y1, hx, hy, width, color)

    def give_way_triangle(self, cx: int, cy: int) -> None:
        self.line(cx - 22, cy - 16, cx + 22, cy - 16, 6, WHITE)
        self.line(cx + 22, cy - 16, cx, cy + 20, 6, WHITE)
        self.line(cx, cy + 20, cx - 22, cy - 16, 6, WHITE)

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


def _clockwise_arc(
    cx: float, cy: float, r: float, start_deg: float, end_deg: float, steps: int = 16
) -> list[tuple[int, int]]:
    """Generate points going clockwise on screen (increasing angle in y-down coords)."""
    if end_deg <= start_deg:
        end_deg += 360.0
    pts: list[tuple[int, int]] = []
    for i in range(steps + 1):
        ang = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        pts.append((int(round(cx + r * math.cos(ang))), int(round(cy + r * math.sin(ang)))))
    return pts


def _render_lesson_diagram(lesson_id: str) -> _Canvas:
    c = _Canvas()

    if "12-roundabout-lanes" in lesson_id:
        # 2-lane approach roundabout (south arm 280 wide, centre x=512), turning right (3 o'clock)
        c.rect(372, 0, 280, SIZE, ROAD)
        c.rect(0, 372, SIZE, 280, ROAD)
        c.circle(512, 512, 265, ROAD)
        c.circle(512, 512, 105, GRASS)
        c.geom.islands.append((512.0, 512.0, 105.0))
        c.ring(512, 512, 185, 6, WHITE, dashed=True)
        # South approach lane divider (left lane: 372..442, right lane: 442..512)
        for y in range(770, SIZE, 50):
            c.rect(440, y, 6, 28, WHITE)
        c.rect(509, 770, 6, 254, WHITE)
        # Painted lane arrows on south approach (left=ahead, right=right)
        c.polyline_arrow([(408, 940), (408, 860)], WHITE, width=6, record_path=False)
        c.polyline_arrow([(476, 940), (476, 890), (504, 890)], WHITE, width=6, record_path=False)
        # Broken give-way line
        for gx in range(380, 506, 20):
            c.rect(gx, 766, 12, 8, WHITE)
        # Player car in right approach lane -> clockwise inner lane -> east exit left lane (y=440)
        c.car(476, 850, PLAYER_BLUE)
        arc = _clockwise_arc(512, 512, 150, 112, 342)
        c.polyline_arrow([(476, 790), (430, 690)] + arc + [(720, 440), (880, 440)])
        return c

    if "roundabout" in lesson_id:
        # Four-arm roundabout (centre 512, 512; island r=105; carriageway r=105..235)
        c.rect(402, 0, 220, SIZE, ROAD)
        c.rect(0, 402, SIZE, 220, ROAD)
        c.circle(512, 512, 240, ROAD)
        c.circle(512, 512, 105, GRASS)
        c.geom.islands.append((512.0, 512.0, 105.0))
        if "13-roundabout" in lesson_id:
            c.ring(512, 512, 172, 6, WHITE, dashed=True)
        # Broken give-way line on south entry (left half)
        for gx in range(410, 506, 20):
            c.rect(gx, 746, 12, 10, WHITE)
        c.rect(509, 756, 6, 268, WHITE)
        # Circulating grey car/cyclist on offside/outer
        if "14-roundabout-cyclists" in lesson_id:
            c.circle(345, 512, 18, OTHER_GREY)
        else:
            c.car(345, 450, OTHER_GREY, horiz=True)
        c.car(456, 860, PLAYER_BLUE)
        arc = _clockwise_arc(512, 512, 175, 110, 162)
        c.polyline_arrow([(456, 800)] + arc + [(180, 568)])
        return c

    if "zebra" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        c.geom.centrelines.append((512.0, 960.0, 512.0, 60.0))
        # Zebra stripes (y=450..570) with NO centre line through them
        for sx in range(372, 652, 40):
            c.rect(sx, 450, 22, 120, WHITE)
        # Zig-zag lines on kerbs and centre on both approaches (y=230..410 and y=610..790)
        for y0 in (230, 610):
            for step in range(3):
                ya = y0 + step * 60
                yb = ya + 30
                yc = ya + 60
                c.line(372, ya, 394, yb, 8, WHITE)
                c.line(394, yb, 372, yc, 8, WHITE)
                c.line(512, ya, 526, yb, 8, WHITE)
                c.line(526, yb, 512, yc, 8, WHITE)
                c.line(652, ya, 630, yb, 8, WHITE)
                c.line(630, yb, 652, yc, 8, WHITE)
        c.circle(330, 510, 18, AMBER)
        c.circle(694, 510, 18, AMBER)
        c.circle(340, 480, 14, OTHER_GREY)
        c.car(436, 760, PLAYER_BLUE)
        c.polyline_arrow([(436, 700), (436, 595)])
        return c

    if "box-junction" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        c.rect(0, 362, SIZE, 300, ROAD)
        c.geom.centrelines.append((512.0, 960.0, 512.0, 60.0))
        c.line(362, 362, 662, 362, 8, YELLOW)
        c.line(662, 362, 662, 662, 8, YELLOW)
        c.line(662, 662, 362, 662, 8, YELLOW)
        c.line(362, 662, 362, 362, 8, YELLOW)
        for off in range(60, 300, 60):
            c.line(362 + off, 362, 662, 662 - off, 6, YELLOW)
            c.line(362, 362 + off, 662 - off, 662, 6, YELLOW)
        c.car(436, 220, OTHER_GREY)
        c.car(436, 790, PLAYER_BLUE)
        c.polyline_arrow([(436, 730), (436, 680)])
        return c

    if "traffic-lights" in lesson_id or "advanced-stop" in lesson_id:
        c.rect(362, 0, 300, SIZE, ROAD)
        c.rect(0, 362, SIZE, 300, ROAD)
        c.geom.centrelines.append((512.0, 960.0, 512.0, 60.0))
        c.rect(362, 662, 150, 90, ASL_GREEN)
        c.rect(362, 662, 150, 10, WHITE)
        c.rect(362, 752, 150, 10, WHITE)
        c.rect(318, 620, 26, 72, DARK_GREY)
        c.circle(331, 656, 9, AMBER)
        c.circle(436, 707, 16, OTHER_GREY)
        c.car(436, 860, PLAYER_BLUE)
        c.polyline_arrow([(436, 800), (436, 768)])
        return c

    if "give-way" in lesson_id or "pedestrians" in lesson_id or "position-after" in lesson_id:
        c.rect(0, 320, SIZE, 280, ROAD)
        c.rect(382, 600, 260, 424, ROAD)
        c.geom.centrelines.append((512.0, 980.0, 512.0, 460.0))
        for x in range(0, SIZE, 70):
            c.rect(x, 456, 36, 8, WHITE)
        for y in range(640, SIZE, 60):
            c.rect(509, y, 6, 32, WHITE)
        for gx in range(390, 505, 22):
            c.rect(gx, 600, 14, 8, WHITE)
            c.rect(gx, 616, 14, 8, WHITE)
        c.give_way_triangle(446, 668)
        c.car(740, 390, OTHER_GREY, horiz=True)
        c.car(446, 840, PLAYER_BLUE)
        if "position-after" in lesson_id:
            # Right turn into left lane of major road (y=530)
            c.polyline_arrow([(446, 780), (455, 620), (520, 540), (760, 540)])
        elif "pedestrians" in lesson_id:
            c.circle(350, 595, 16, OTHER_GREY)
            c.polyline_arrow([(446, 780), (446, 645)])
        else:
            c.polyline_arrow([(446, 780), (446, 645)])
        return c

    if "dual" in lesson_id or "two-second" in lesson_id:
        c.rect(240, 0, 240, SIZE, ROAD)
        c.rect(544, 0, 240, SIZE, ROAD)
        c.geom.centrelines.append((512.0, 960.0, 512.0, 60.0))
        for y in range(0, SIZE, 60):
            c.rect(356, y, 8, 30, WHITE)
            c.rect(660, y, 8, 30, WHITE)
        c.car(300, 320, OTHER_GREY)
        c.car(300, 740, PLAYER_BLUE)
        if "dual-carriageway-lanes" in lesson_id:
            c.polyline_arrow([(300, 680), (420, 520), (420, 320), (300, 180)])
        else:
            c.polyline_arrow([(300, 680), (300, 480)])
        return c

    # Urban straight road
    c.rect(362, 0, 300, SIZE, ROAD)
    c.geom.centrelines.append((512.0, 960.0, 512.0, 60.0))
    for y in range(0, SIZE, 60):
        c.rect(508, y, 8, 34, WHITE)
    if "cyclist" in lesson_id:
        c.circle(395, 450, 18, OTHER_GREY)
        c.car(436, 740, PLAYER_BLUE)
        # Pass wide right (still left of oncoming/clear) and back to left lane
        c.polyline_arrow([(436, 680), (500, 540), (500, 360), (436, 220)])
    else:
        c.car(586, 300, OTHER_GREY)
        c.car(436, 760, PLAYER_BLUE)
        c.polyline_arrow([(436, 700), (436, 480)])
    return c


def generate_contact_sheet(png_paths: list[Path], out_path: Path) -> Path:
    """Tile all 18 1024x1024 diagrams into a 6x3 contact sheet PNG (1536x768)."""
    cols, rows = 6, 3
    thumb = 256
    step = SIZE // thumb
    sheet_w, sheet_h = cols * thumb, rows * thumb
    buf = bytearray([240, 240, 240] * (sheet_w * sheet_h))

    for idx, p in enumerate(png_paths[: cols * rows]):
        r_idx, c_idx = divmod(idx, cols)
        # Re-render or decode? Use _render_lesson_diagram directly for crisp 4x4 downsampling!
        lesson_id = p.stem
        canvas = _render_lesson_diagram(lesson_id)
        for ty in range(thumb):
            sy = ty * step
            dst_y = r_idx * thumb + ty
            for tx in range(thumb):
                sx = tx * step
                src_i = (sy * SIZE + sx) * 3
                dst_i = (dst_y * sheet_w + c_idx * thumb + tx) * 3
                buf[dst_i : dst_i + 3] = canvas.buf[src_i : src_i + 3]

    raw = bytearray()
    stride = sheet_w * 3
    for y in range(sheet_h):
        raw.append(0)
        raw.extend(buf[y * stride : (y + 1) * stride])

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack("!I", len(data))
            + tag
            + data
            + struct.pack("!I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack("!IIBBBBB", sheet_w, sheet_h, 8, 2, 0, 0, 0)
    png_bytes = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(bytes(raw), level=6))
        + chunk(b"IEND", b"")
    )
    out_path.write_bytes(png_bytes)
    return out_path


def generate_all_diagrams() -> list[Path]:
    """Generate SVG+PNG diagrams for lessons 1-18, contact sheet, and set learnCard.diagram."""
    DIAGRAMS_DIR.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    pngs: list[Path] = []
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
        pngs.append(png_path)
    contact_path = generate_contact_sheet(pngs, DIAGRAMS_DIR / "_contact_sheet.png")
    written.append(contact_path)
    return written


if __name__ == "__main__":
    files = generate_all_diagrams()
    print(f"Generated {len(files)} diagram files (including contact sheet).")
