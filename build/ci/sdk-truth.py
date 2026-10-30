#!/usr/bin/env python3
"""Trial only: builds each solution of one area of build/ci/trial-sweep.json with the real .NET SDK, on a copy of its
clone, and writes the errors the SDK reports (deduplicated across target frameworks) to <out>/sdk-<name>.txt and
<out>/sdk-results.json. The ground truth TabStudio's Error List is compared against.

Usage: sdk-truth.py <clones> <area> <out>
"""
import json, os, re, shutil, subprocess, sys, time

clones, area, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
plan = json.load(open(os.path.join(os.path.dirname(__file__), "trial-sweep.json")))
pattern = re.compile(r"^\s*(?P<file>[^\s].*?)(\((?P<line>\d+),(?P<col>\d+)\))?\s*:\s*error\s+(?P<code>[A-Za-z]+\d+)\s*:\s*(?P<msg>.*?)(\s\[(?P<project>[^\]]+)\])?$")
results = {}
for entry in plan["repositories"]:
    if entry["area"] != area:
        continue
    name = entry["name"]
    source = os.path.join(clones, name)
    copy = os.path.join(clones, "..", "sdk-copies", name)
    shutil.rmtree(copy, ignore_errors=True)
    shutil.copytree(source, copy, symlinks=True)
    # The SDK installed here (.NET 10) builds it, whatever feature band global.json names; msbuild-sdks stay.
    for folder, _, files in os.walk(copy):
        if "global.json" in files:
            path = os.path.join(folder, "global.json")
            try:
                data = json.load(open(path, encoding="utf-8-sig"))
                data.pop("sdk", None)
                json.dump(data, open(path, "w"))
            except Exception as ex:  # noqa: BLE001
                print(f"{name}: could not rewrite {path}: {ex}")
    target = os.path.join(copy, entry["open"])
    start = time.time()
    # Visual Studio builds Windows-targeting projects; a project that needs a workload this runner lacks (NETSDK1147)
    # or a newer SDK (NETSDK1045) fails its restore and stops the whole build, so it is set aside and reported, and the
    # rest is built again, as Visual Studio builds the other projects of the solution.
    targets = [target] if os.path.isfile(target) else sorted(
        os.path.join(folder, f) for folder, _, files in os.walk(target) for f in files if f.endswith((".csproj", ".vbproj", ".fsproj")))
    removed, text, code = [], "", 0
    for build_target in targets:
        for attempt in range(4):
            try:
                proc = subprocess.run(
                    ["dotnet", "build", build_target, "-c", "Debug", "-nologo", "-p:TreatWarningsAsErrors=false", "-p:NuGetAudit=false",
                     "-p:EnableWindowsTargeting=true", "-clp:NoSummary;ForceNoAlign", "-v:m"],
                    cwd=os.path.dirname(build_target), capture_output=True, text=True, timeout=40 * 60)
                output, code = proc.stdout + proc.stderr, proc.returncode
            except subprocess.TimeoutExpired as ex:
                output, code = (ex.stdout.decode() if isinstance(ex.stdout, bytes) else (ex.stdout or "")), "timeout"
            blocked = sorted({m.group(1) for m in re.finditer(r"error (?:NETSDK1147|NETSDK1045|NETSDK1100)[^\[]*\[([^\]:]+?\.(?:cs|vb|fs)proj)", output)})
            if attempt == 3 or not blocked or not build_target.endswith((".sln", ".slnx")):
                text += output
                break
            text += "\n".join(l for l in output.splitlines() if re.search(r"error (NETSDK1147|NETSDK1045|NETSDK1100)", l)) + "\n"
            for project in blocked:
                removed.append(os.path.relpath(project, copy))
                subprocess.run(["dotnet", "sln", build_target, "remove", project], capture_output=True, text=True)
    errors = {}
    for line in text.splitlines():
        m = pattern.match(line)
        if not m:
            continue
        file = m.group("file").replace(copy, "").lstrip("/")
        project = os.path.basename(m.group("project") or "").rsplit(".", 1)[0]
        key = (project, os.path.basename(file), m.group("line"), m.group("code"))
        errors.setdefault(key, m.group("msg").strip())
    by_code = {}
    for (_, _, _, c) in errors:
        by_code[c] = by_code.get(c, 0) + 1
    results[name] = {"exit": code, "errors": len(errors), "codes": by_code, "seconds": round(time.time() - start), "setAside": removed}
    with open(os.path.join(out, f"sdk-{name}.txt"), "w") as f:
        f.write(f"{name}: dotnet build {entry['open']} exited {code} in {results[name]['seconds']} s, {len(errors)} distinct errors\n")
        for project in removed:
            f.write(f"  set aside (needs a workload or a newer SDK): {project}\n")
        for (project, file, line, c), msg in sorted(errors.items()):
            f.write(f"  [{project}] {file}({line}): {c}: {msg}\n")
        f.write("\nLast 300 lines of the build:\n" + "\n".join(text.splitlines()[-300:]))
    print(f"{name}: {len(errors)} errors, exit {code}, {by_code}")
    shutil.rmtree(copy, ignore_errors=True)
json.dump(results, open(os.path.join(out, "sdk-results.json"), "w"), indent=1)
