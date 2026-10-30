#!/usr/bin/env python3
"""Checks that the accessibility audit saw every view of the IDE, and sums up what it found.

Every screenshot test audits its window (tests/TabStudio.App.Tests/Accessibility/AccessibilityReport.cs) at the text
size it was shown at and at the largest one, and lists the views it showed in accessibility/views.tsv. This script
reads those lists from the screenshot folders given (the dark run's and the light run's), and fails when a view of
src/TabStudio.App/Views was never audited in one of them, unless tests/TabStudio.App.Tests/Accessibility/
unaudited-views.txt names it with the reason. It writes accessibility/summary.md with the counts of each finding.

Usage: build/ci/accessibility-coverage.py <screenshots folder> [<screenshots folder> ...]
"""

import collections
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
VIEWS = ROOT / "src" / "TabStudio.App" / "Views"
EXEMPT = ROOT / "tests" / "TabStudio.App.Tests" / "Accessibility" / "unaudited-views.txt"


def views_of_the_ide():
    """Every user control of the IDE's views, by its full name."""
    found = set()
    for file in VIEWS.rglob("*.axaml"):
        text = file.read_text(encoding="utf-8")
        root = re.search(r"<\s*([A-Za-z:]+)", text)
        name = re.search(r'x:Class="([^"]+)"', text)
        if root and name and root.group(1) == "UserControl":
            found.add(name.group(1))
    return found


def exempt():
    """The views the audit cannot show, each with its reason."""
    if not EXEMPT.exists():
        return {}
    result = {}
    for line in EXEMPT.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            name, _, reason = line.partition(" ")
            result[name] = reason.strip()
    return result


def views_shown(folder):
    """The (screenshot, view) pairs a run's audit saw."""
    path = folder / "accessibility" / "views.tsv"
    if not path.exists():
        return []
    rows = []
    for row in path.read_text(encoding="utf-8").splitlines():
        parts = row.split("\t")
        if len(parts) == 2:
            rows.append((parts[0], parts[1]))
    return rows


def main(folders):
    all_views = views_of_the_ide()
    exemptions = exempt()
    failed = False
    lines = ["# Accessibility audit", ""]
    shown = {pathlib.Path(f): views_shown(pathlib.Path(f)) for f in folders}
    for folder in map(pathlib.Path, folders):
        report = folder / "accessibility"
        # A test can show a view in both themes itself (the panel audit's "-dark" and "-light" screenshots): those
        # count for the run of that theme whichever folder they were saved in.
        theme = "light" if folder.name == "light" else "dark"
        seen = {view for _, view in shown[folder]}
        for other, rows in shown.items():
            if other != folder:
                seen |= {view for shot, view in rows if shot.endswith(f"-{theme}")}
        findings = collections.Counter()
        controls = collections.defaultdict(set)
        if (report / "findings.tsv").exists():
            for row in (report / "findings.tsv").read_text(encoding="utf-8").splitlines():
                parts = row.split("\t")
                if len(parts) == 4:
                    findings[parts[1]] += 1
                    controls[parts[1]].add(parts[2])
        missing = sorted(all_views - seen - set(exemptions))
        lines.append(f"## {folder}")
        lines.append("")
        lines.append(f"Views audited: {len(all_views & seen)} of {len(all_views)} ({len(exemptions)} exempt).")
        lines.append("")
        lines.append("| Rule | Findings | Distinct controls |")
        lines.append("| --- | --- | --- |")
        for kind in ("target", "name", "contrast", "icon-contrast", "clipped"):
            lines.append(f"| {kind} | {findings[kind]} | {len(controls[kind])} |")
        lines.append("")
        if missing:
            failed = True
            lines.append("Views never audited:")
            lines.extend(f"- {name}" for name in missing)
            lines.append("")
        report.mkdir(parents=True, exist_ok=True)
    summary = "\n".join(lines)
    print(summary)
    for folder in map(pathlib.Path, folders):
        (folder / "accessibility").mkdir(parents=True, exist_ok=True)
        (folder / "accessibility" / "summary.md").write_text(summary + "\n", encoding="utf-8")
    if failed:
        print("::error::Some views were never audited for accessibility (see above).")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:] or ["ci-out/screenshots"]))
