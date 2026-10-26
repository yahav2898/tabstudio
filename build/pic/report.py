#!/usr/bin/env python3
"""Combine the four runner measurements; incomplete/failed spikes are never a pass."""
import argparse
import json
from pathlib import Path
import statistics


def report(folder):
    results = {}
    for file in folder.rglob("result.json"):
        result = json.loads(file.read_text())
        key = (result["compiler"], result["hostArchitecture"])
        if key in results:
            raise ValueError(f"Duplicate measurement: {key}")
        results[key] = result
    lines = ["# PIC compiler spike", "",
             "Wall time includes PRoot startup, compilation, assembly and linking. The first build is followed by two warm builds.",
             "XC32 HEX conversion is verified separately and excluded from the compile/link wall time. Installer timing excludes the separate archive fallback when needed.",
             "The ARM64 and x86-64 runners use different CPUs; the ratio is an end-to-end runner comparison, not an isolated translation overhead measurement.",
             "", "| Compiler | Host | First (s) | Warm mean (s) | Installer attempt (s) | Result |",
             "| --- | --- | ---: | ---: | ---: | --- |"]
    success = True
    comparisons = []
    for compiler in ["xc8", "xc32"]:
        for arch in ["x86_64", "aarch64"]:
            result = results.get((compiler, arch))
            if not result or not result["success"] or len(result["compileSeconds"]) != 3:
                lines.append(f"| {compiler} | {arch} | — | — | — | FAILED / missing |")
                success = False
                continue
            times = result["compileSeconds"]
            lines.append(f'| {compiler} | {arch} | {times[0]:.3f} | {statistics.mean(times[1:]):.3f} | {result["installSeconds"]:.3f} | Passed |')
        native, arm = results.get((compiler, "x86_64")), results.get((compiler, "aarch64"))
        if native and arm and native["success"] and arm["success"]:
            ratio = statistics.mean(arm["compileSeconds"][1:]) / statistics.mean(native["compileSeconds"][1:])
            comparisons.extend(["", f"{compiler}: ARM64/x86-64 warm-time ratio **{ratio:.2f}×**.",
                          f'Firmware HEX hashes match: **{native["hexSha256"] == arm["hexSha256"]}**.', ""])
    lines.extend(comparisons)
    lines.extend(["", "## Native fallback evaluation", "",
                  "These checks distinguish target-object generation from firmware linking. A linked fallback blink does not establish compatibility with arbitrary XC projects.",
                  "", "| Compiler | Native host | Source build (s) | Compile mean (s) | Linked firmware | Result |",
                  "| --- | --- | ---: | ---: | --- | --- |"])
    for compiler in ["xc32-native", "sdcc-native"]:
        result = results.get((compiler, "aarch64"))
        if not result:
            lines.append(f"| {compiler} | — | — | — | No | Missing |")
            continue
        build = sum(result.get("componentSeconds", {}).values())
        times = result.get("compileSeconds", [])
        mean = f"{statistics.mean(times):.3f}" if times else "—"
        status = ("Firmware linked" if result.get("linkedFirmware") else "Object emitted") if result["success"] else "Failed"
        lines.append(f'| {compiler} | {result.get("nativeHost", "unknown")} | {build:.1f} | {mean} | {result.get("linkedFirmware", False)} | {status} |')
    for (compiler, arch), result in sorted(results.items()):
        lines.extend(["", f"## {compiler} / {arch}", "", "```text", result["cpu"].strip(), "```"])
        if result.get("error"):
            lines.extend(["", result["error"]])
        if result.get("scope"):
            lines.extend(["", result["scope"]])
    return "\n".join(lines) + "\n", success


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    text, success = report(args.folder)
    args.output.write_text(text)
    print(text)
    raise SystemExit(0 if success else 1)
