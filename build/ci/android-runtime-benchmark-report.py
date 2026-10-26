#!/usr/bin/env python3
"""Report paired measurements; only the deliberately bad baseline may fail."""
from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import statistics


def load_variant(root: Path, name: str) -> dict:
    folder = root / name
    source = folder / "runtime-performance.json"
    try:
        report = json.loads(source.read_text()) if source.exists() else {}
    except (OSError, ValueError) as error:
        report = {"success": False, "error": f"Could not read partial measurements: {error}"}
    code_file = folder / "exit-code.txt"
    try:
        code = int(code_file.read_text().strip()) if code_file.exists() else None
    except (OSError, ValueError):
        code = None
    verdict = (folder / "verdict.txt").read_text().strip() if (folder / "verdict.txt").exists() else "No device verdict."
    return {"exit_code": code, "verdict": verdict, "measurements": report}


def metric(report: dict, field: str, warm: bool) -> str:
    values = report.get(field) or []
    chosen = values[1:] if warm else values[:1]
    if not chosen:
        return "not measured"
    value = statistics.median(chosen)
    return f"{value:.1f}"


def production_errors(variant: dict, source_sha: str) -> list[str]:
    report = variant["measurements"]
    errors = []
    if variant["exit_code"] != 0 or report.get("success") is not True:
        errors.append("Production JIT benchmark did not complete successfully.")
    if source_sha not in report.get("informationalVersion", ""):
        errors.append("Production APK did not report the requested source SHA.")
    if report.get("interpreterOptions"):
        errors.append("Production APK unexpectedly enabled interpreter options.")
    for field in ("f5PromptMilliseconds", "f5ExitMilliseconds", "completionPopupMilliseconds"):
        values = report.get(field) or []
        if len(values) != 6 or any(not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0 for v in values):
            errors.append(f"Production {field} needs one first and five valid warm samples.")
    if not isinstance(report.get("workspaceOpenMilliseconds"), (int, float)):
        errors.append("Production workspace-open timing is missing.")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()
    baseline = load_variant(args.directory, "baseline")
    jit = load_variant(args.directory, "jit")
    errors = production_errors(jit, args.source_sha)
    baseline_ok = baseline["exit_code"] == 0 and baseline["measurements"].get("success") is True
    baseline_mode_confirmed = baseline["measurements"].get("interpreterOptions") == "-all" and args.source_sha in baseline["measurements"].get("informationalVersion", "")
    rows = [
        ("F5 to first input prompt, first", "f5PromptMilliseconds", False),
        ("F5 to first input prompt, five warm median", "f5PromptMilliseconds", True),
        ("F5 through successful exit, first", "f5ExitMilliseconds", False),
        ("F5 through successful exit, five warm median", "f5ExitMilliseconds", True),
        ("C# completion popup, first request after F5", "completionPopupMilliseconds", False),
        ("C# completion popup, five warm median", "completionPopupMilliseconds", True),
    ]
    text = [
        "# Android runtime latency",
        "",
        f"Source SHA: {args.source_sha}",
        "",
        "Both APKs ran sequentially on the same API 35 x64 emulator (3 cores, 4 GB RAM), with pm clear before each variant.",
        "The first completion request follows the F5 trials; it is not a cold compiler measurement.",
        "Warm medians exclude the first sample. All raw samples and runtime metadata are retained in the JSON report.",
        "",
        "| Measurement (ms) | Global interpreter baseline | Production JIT |",
        "| --- | ---: | ---: |",
    ]
    for label, field, warm in rows:
        text.append(f"| {label} | {metric(baseline['measurements'], field, warm)} | {metric(jit['measurements'], field, warm)} |")
    for label, variant in (("Baseline", baseline), ("Production JIT", jit)):
        report = variant["measurements"]
        value = report.get("workspaceOpenMilliseconds")
        opening = f"{value:.1f} ms" if isinstance(value, (int, float)) else "not measured"
        text += ["", f"{label}: workspace open {opening}; phase {report.get('phase', 'not started')}; exit code {variant['exit_code']}.",
                 f"Runtime: {report.get('framework', 'unknown')}; architecture: {report.get('architecture', 'unknown')}; interpreter options: {report.get('interpreterOptions') or '(unset)'}.",
                 f"MetadataUpdater.IsSupported: {report.get('metadataUpdatesSupported', 'unknown')}; DOTNET_MODIFIABLE_ASSEMBLIES: {report.get('modifiableAssemblies') or '(unset)'}.",
                 f"RuntimeFeature.IsDynamicCodeSupported: {report.get('dynamicCodeSupported', 'unknown')}; IsDynamicCodeCompiled: {report.get('dynamicCodeCompiled', 'unknown')}; visible processor count: {report.get('processorCount', 'unknown')}.",
                 f"Device verdict: {variant['verdict'].replace(chr(10), ' | ')}"]
    if not baseline_mode_confirmed:
        text += ["", "**The baseline interpreter configuration/source was not verified; no valid before/after comparison is claimed.**"]
    if not baseline_ok:
        text += ["", "**The global-interpreter baseline failed or timed out. Partial timings are diagnostic only; this is not a functional pass.**"]
    if errors:
        text += ["", "**Production measurement failed validation:**", "", *[f"- {error}" for error in errors]]
    else:
        text += ["", "Production JIT verified all six F5 greetings/exits and all six visible Greet completion lists."]
    markdown = "\n".join(text) + "\n"
    args.directory.mkdir(parents=True, exist_ok=True)
    (args.directory / "runtime-benchmark.md").write_text(markdown)
    (args.directory / "runtime-benchmark.json").write_text(json.dumps(
        {"source_sha": args.source_sha, "baseline_functional_pass": baseline_ok, "baseline_mode_confirmed": baseline_mode_confirmed,
         "production_valid": not errors, "validation_errors": errors, "baseline": baseline, "jit": jit}, indent=2) + "\n")
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a") as output:
            output.write(markdown)
    print(markdown)
    return int(bool(errors))


if __name__ == "__main__":
    raise SystemExit(main())
