#!/usr/bin/env python3
"""Unmodified dynamic x64 C client through the production PTY/control bridge.

Runs a native client as an independent control baseline, then the same source
compiled for x64 through native execution/Box64. Exact RPC sequences must match.
No synthetic test here is a physical USB capture.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

sys.path.insert(0, "/opt/tabstudio/embedded")
import box64_runtime


def launch(arguments, shim):
    command, environment = box64_runtime.command(arguments[0], arguments[1:], serial_shim=shim)
    return subprocess.call(command, env=environment)


def check(args):
    versions = subprocess.run(["readelf", "--version-info", str(args.x64_shim)], check=True,
                              text=True, capture_output=True).stdout
    args.client.with_name("serial-x64-symbol-versions.txt").write_text(versions)
    native = args.client.with_name("serial-client-native")
    subprocess.run(["cc", "-O2", "-Wall", "-Wextra", "-Werror", str(args.source), "-o", str(native)], check=True)
    evidence = {}
    for name, target in [("native", [str(native)]),
                         ("x64", [sys.executable, "-P", str(Path(__file__).resolve()), "launch", "--shim", str(args.x64_shim), "--", str(args.client)])]:
        audit = args.client.with_name("serial-" + name + "-audit.jsonl")
        audit.unlink(missing_ok=True)
        alias = args.client.with_name("serial-" + name + "-tty")
        command = [sys.executable, str(args.tools_dir / "serial_bridge.py"), "--virtual",
                   "--shim", str(args.native_shim), "--port", str(alias), "--audit", str(audit),
                   "--", *target, "{port}"]
        result = subprocess.run(command, text=True, capture_output=True, timeout=90)
        print(result.stdout, end="", flush=True)
        print(result.stderr, end="", file=sys.stderr, flush=True)
        if result.returncode or "SERIAL_POSIX_PASSED" not in result.stdout:
            raise RuntimeError(name + " serial client failed with code " + str(result.returncode))
        if alias.exists() or alias.is_symlink():
            raise RuntimeError("Serial alias was not cleaned up")
        evidence[name] = [json.loads(line) for line in audit.read_text().splitlines()]
    if evidence["native"] != evidence["x64"]:
        raise ValueError("Guest controls differ from native baseline (missing or duplicate RPCs): " + json.dumps(evidence))
    if not any(item == {"operation": 1, "values": [123456, 8, 2, 2]} for item in evidence["x64"]):
        raise ValueError("Custom baud/parity/stop coding did not reach the worker")
    args.client.with_name("serial-x64-evidence.json").write_text(json.dumps(evidence, indent=2))
    print("SERIAL_X64_ABI_PASSED controlEvents=" + str(len(evidence["x64"])) + " hardware=false", flush=True)


def main():
    if sys.argv[1:2] == ["launch"]:
        parser = argparse.ArgumentParser()
        parser.add_argument("--shim", required=True)
        parser.add_argument("arguments", nargs=argparse.REMAINDER)
        args = parser.parse_args(sys.argv[2:])
        arguments = args.arguments[1:] if args.arguments[:1] == ["--"] else args.arguments
        return launch(arguments, args.shim)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--native-shim", type=Path, required=True)
    parser.add_argument("--x64-shim", type=Path, required=True)
    parser.add_argument("--tools-dir", type=Path, default=Path("/opt/tabstudio/embedded"))
    check(parser.parse_args())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
