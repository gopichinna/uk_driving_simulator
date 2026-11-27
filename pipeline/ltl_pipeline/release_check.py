"""Release-candidate readiness check (licences, claims, UNVERIFIED, legal text, placeholders)."""

from __future__ import annotations

import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

import yaml

from ltl_pipeline.schema import REPO_ROOT

PLACEHOLDER_RE = re.compile(r"<owner email>|\bTODO\b|\bTBD\b|\blorem\b", re.IGNORECASE)


@dataclass
class ReleaseReport:
    """Structured release-check results."""

    failures: list[str] = field(default_factory=list)
    passed_checks: list[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return len(self.failures) == 0


def check_licences(repo_root: Path = REPO_ROOT) -> list[str]:
    """Verify shipped audio, images, and city packs have licence/attribution entries."""
    errors: list[str] = []
    licences = (repo_root / "content" / "audio" / "LICENCES.md").read_text(encoding="utf-8")
    attributions = (repo_root / "content" / "ATTRIBUTIONS.md").read_text(encoding="utf-8")
    credits = (repo_root / "content" / "legal" / "en" / "credits.md").read_text(encoding="utf-8")

    sfx_manifest = json.loads(
        (repo_root / "content" / "audio" / "sfx" / "manifest.json").read_text(encoding="utf-8")
    )
    for entry in sfx_manifest["entries"]:
        if entry["file"] not in licences:
            errors.append(f"SFX missing licence line in LICENCES.md: {entry['file']}")

    if "en_GB-cori-medium" not in licences:
        errors.append("Voice model licence missing from content/audio/LICENCES.md")
    if "Open Government Licence" not in attributions:
        errors.append("OGL v3.0 attribution missing from content/ATTRIBUTIONS.md")
    if "OpenStreetMap" not in credits:
        errors.append("OSM attribution missing from content/legal/en/credits.md")

    for city_dir in sorted((repo_root / "content" / "cities").iterdir()):
        if city_dir.is_dir() and not (
            (city_dir / "SOURCE.md").is_file() or (city_dir / "QA.md").is_file()
        ):
            errors.append(f"City pack missing SOURCE.md/QA.md attribution: {city_dir.name}")
    return errors


def check_claims_evidence(repo_root: Path = REPO_ROOT) -> list[str]:
    """Verify every claim in claims.yaml points to existing evidence files."""
    errors: list[str] = []
    claims_path = repo_root / "content" / "store" / "en" / "claims.yaml"
    claims_data = yaml.safe_load(claims_path.read_text(encoding="utf-8"))
    for claim in claims_data.get("claims", []):
        ev = claim.get("evidence") or []
        if not ev:
            errors.append(f"Claim {claim['id']} has no evidence paths")
        for rel in ev:
            if not (repo_root / rel).is_file():
                errors.append(f"Claim {claim['id']} missing evidence file: {rel}")
    return errors


def check_unverified_shipped_rules(repo_root: Path = REPO_ROOT) -> list[str]:
    """Verify no UNVERIFIED marker remains in shipped rule text."""
    errors: list[str] = []
    rules_data = yaml.safe_load(
        (repo_root / "content" / "countries" / "gb" / "rules.yaml").read_text(encoding="utf-8")
    )
    impl_ids = {
        r["id"]
        for r in rules_data["rules"]
        if r.get("implementation", {}).get("status") == "implemented"
    }
    for r in rules_data["rules"]:
        if r["id"] in impl_ids and "UNVERIFIED" in json.dumps(r):
            errors.append(f"Shipped rule {r['id']} contains UNVERIFIED in rules.yaml")

    for fname in ("rules.json", "explainers.json"):
        p = repo_root / "content" / "countries" / "gb" / "strings" / "en" / fname
        if p.is_file() and "UNVERIFIED" in p.read_text(encoding="utf-8"):
            errors.append(f"Shipped strings file {fname} contains UNVERIFIED")
    return errors


def check_required_legal_strings(repo_root: Path = REPO_ROOT) -> list[str]:
    """Verify 18+, £14.99, and © OpenStreetMap contributors appear where required."""
    errors: list[str] = []
    store_en = (repo_root / "content" / "store" / "en" / "listing.md").read_text(encoding="utf-8")
    play_en = (repo_root / "content" / "store" / "listing" / "en.md").read_text(encoding="utf-8")
    credits = (repo_root / "content" / "legal" / "en" / "credits.md").read_text(encoding="utf-8")

    for label, text in (("store/en/listing.md", store_en), ("store/listing/en.md", play_en)):
        if "18+" not in text:
            errors.append(f"Missing '18+' age text in {label}")
        if "£14.99" not in text:
            errors.append(f"Missing '£14.99' price text in {label}")

    if "© OpenStreetMap contributors" not in play_en:
        errors.append("Missing '© OpenStreetMap contributors' in store/listing/en.md")
    if "© OpenStreetMap contributors" not in credits:
        errors.append("Missing '© OpenStreetMap contributors' in content/legal/en/credits.md")
    return errors


def check_placeholders(repo_root: Path = REPO_ROOT) -> list[str]:
    """Scan content/** and docs/store/** for unresolved placeholders."""
    errors: list[str] = []
    exts = {".md", ".json", ".yaml", ".yml", ".txt"}
    for base in (repo_root / "content", repo_root / "docs" / "store"):
        if not base.exists():
            continue
        for path in sorted(base.rglob("*")):
            if path.is_file() and path.suffix in exts:
                text = path.read_text(encoding="utf-8", errors="ignore")
                matches = PLACEHOLDER_RE.findall(text)
                if matches:
                    rel = path.relative_to(repo_root)
                    errors.append(f"Placeholder(s) {sorted(set(matches))} in {rel}")
    return errors


def run_release_check(repo_root: Path = REPO_ROOT) -> ReleaseReport:
    """Run all release-candidate checks and return a ReleaseReport."""
    report = ReleaseReport()
    checks = [
        ("Licences & attributions", check_licences),
        ("Claims evidence files", check_claims_evidence),
        ("Shipped rules verified (no UNVERIFIED)", check_unverified_shipped_rules),
        ("Required 18+ / £14.99 / OSM legal text", check_required_legal_strings),
        ("No unresolved placeholders", check_placeholders),
    ]
    for name, fn in checks:
        errs = fn(repo_root)
        if errs:
            report.failures.extend(f"[{name}] {e}" for e in errs)
        else:
            report.passed_checks.append(name)
    return report


def format_report(report: ReleaseReport) -> str:
    """Format a concise one-page release readiness report."""
    lines = ["# L to Local — Release Check Report", ""]
    lines.append(f"Passed groups ({len(report.passed_checks)}):")
    for item in report.passed_checks:
        lines.append(f"  - PASS: {item}")
    lines.append("")
    lines.append(f"Failures ({len(report.failures)}):")
    if not report.failures:
        lines.append("  - None")
    else:
        for f in report.failures:
            lines.append(f"  - FAIL: {f}")
    return "\n".join(lines)


if __name__ == "__main__":
    rep = run_release_check()
    print(format_report(rep))
    sys.exit(0 if rep.ok else 1)
