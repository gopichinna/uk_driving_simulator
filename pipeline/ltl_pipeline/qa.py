"""Generate a Markdown QA report for a RoadPack v0/v0.1 file."""

from __future__ import annotations

import math
from collections import defaultdict
from pathlib import Path
from typing import Any


def _polyline_length_m(geom: list[list[float]]) -> float:
    return sum(
        math.hypot(geom[i + 1][0] - geom[i][0], geom[i + 1][1] - geom[i][1])
        for i in range(len(geom) - 1)
    )


def _connected_components(pack: dict[str, Any]) -> list[int]:
    adj: dict[str, set[str]] = defaultdict(set)
    for lk in pack.get("links", []):
        adj[lk["from"]].add(lk["to"])
        adj[lk["to"]].add(lk["from"])

    seen: set[str] = set()
    sizes: list[int] = []
    for n in pack.get("nodes", []):
        nid = n["id"]
        if nid in seen:
            continue
        stack = [nid]
        seen.add(nid)
        size = 0
        while stack:
            cur = stack.pop()
            size += 1
            for nxt in adj[cur]:
                if nxt not in seen:
                    seen.add(nxt)
                    stack.append(nxt)
        sizes.append(size)
    return sorted(sizes, reverse=True)


def generate_qa_markdown(pack: dict[str, Any]) -> str:
    """Build a deterministic Markdown QA report for a RoadPack dict."""
    links = pack.get("links", [])
    nodes = pack.get("nodes", [])
    crossings = pack.get("crossings", [])
    restrictions = pack.get("restrictions", [])

    high = sum(1 for lk in links if lk["confidence"] >= 0.8)
    med = sum(1 for lk in links if 0.5 <= lk["confidence"] < 0.8)
    low = sum(1 for lk in links if lk["confidence"] < 0.5)

    default_speed = sum(1 for lk in links if lk.get("speedLimitSource") == "default")
    unknown_crossings = [
        c for c in crossings if c.get("type") in ("signals_unknown", "uncontrolled")
    ]
    short_links = [
        (lk["id"], round(_polyline_length_m(lk["geometry"]), 2))
        for lk in links
        if _polyline_length_m(lk["geometry"]) < 2.0
    ]
    components = _connected_components(pack)

    lines = [
        f"# RoadPack QA Report: {pack.get('displayName', pack['packId'])}",
        "",
        f"- **Pack ID:** `{pack['packId']}` (format `{pack['packFormatVersion']}`)",
        f"- **Extract:** `{pack['source'].get('extract', 'n/a')}` "
        f"(`{pack['source'].get('extractTimestamp', 'n/a')}`)",
        f"- **Totals:** {len(nodes)} nodes, {len(links)} links, "
        f"{len(crossings)} crossings, {len(restrictions)} turn restrictions",
        "",
        "## Links by confidence band",
        "| Band | Count |",
        "|---|---|",
        f"| High (>= 0.80) | {high} |",
        f"| Medium (0.50–0.79) | {med} |",
        f"| Low (< 0.50) | {low} |",
        "",
        "## Speed limits & crossings",
        f"- **Links using default speed limit:** {default_speed} / {len(links)}",
        f"- **Crossings with unknown/uncontrolled type:** {len(unknown_crossings)} / "
        f"{len(crossings)}",
        "",
        "## Graph connectivity",
        f"- **Connected components:** {len(components)}",
        f"- **Largest component:** {components[0] if components else 0} / {len(nodes)} nodes",
        "",
        "## Very short links (< 2.0 m)",
        f"- **Count:** {len(short_links)}",
    ]
    for lid, dist in short_links:
        lines.append(f"  - `{lid}`: {dist} m")
    lines.append("")
    return "\n".join(lines)


def write_qa_report(pack: dict[str, Any], out_path: Path) -> Path:
    """Write QA.md to disk."""
    out_path.write_text(generate_qa_markdown(pack), encoding="utf-8")
    return out_path
