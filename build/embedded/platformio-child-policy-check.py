#!/usr/bin/env python3
"""Real Core/SCons child-policy regression with an unchanged x64 AVR compiler.

The generated test-only interpreter reports ARM64 to each Core/SCons Python
process. Compiler launchers retain the real native Python interpreter, so an x64
host executes its unchanged x64 compiler natively. Native ARM64 CI still uses the
real Box64 path; this simulation does not claim ARM64 translation qualification.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def digest(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def interpreter(path, audit, suppress_child):
    """Emulate Python's script entry point without changing sys.executable."""
    path.write_text("#!" + sys.executable + "\n" + "\n".join([
        "import json, os, platform, runpy, sys",
        "from pathlib import Path",
        "platform.machine = lambda: 'aarch64'",  # Test-only, never a production environment override.
        "args = sys.argv[1:]",
        "while args and args[0] in ('-P', '-u', '-B'): args = args[1:]",
        "if not args: raise SystemExit('missing Python script')",
        "child = len(args) > 1 and args[1] == '--scons'",
        "with open(" + repr(str(audit)) + ", 'a') as output:",
        "    output.write(json.dumps({'pid':os.getpid(),'parentPid':os.getppid(),'sconsBootstrap':child,'suppressed':child and " + repr(suppress_child) + ",'argv':args,'machine':platform.machine(),'realPython':sys.executable}) + '\\n')",
        "if child and " + repr(suppress_child) + ": args = args[2:]",
        "sys.argv = args",
        "sys.path.insert(0, str(Path(args[0]).resolve().parent))",
        "runpy.run_path(args[0], run_name='__main__')", "",
    ]))
    path.chmod(0o755)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", required=True, type=Path, help="Original unchanged x64 toolchain-atmelavr package")
    parser.add_argument("--platform", required=True, type=Path, help="Pinned atmelavr 5.3.0 package")
    parser.add_argument("--scons", required=True, type=Path, help="Core's original tool-scons package")
    parser.add_argument("--helper", required=True, type=Path, help="Production platformio_box64.py source")
    parser.add_argument("--work", required=True, type=Path)
    args = parser.parse_args()
    import platformio
    if platformio.__version__ != "6.1.18":
        raise ValueError("This proof requires the production pinned Core 6.1.18")
    compiler = args.compiler.resolve()
    platform = args.platform.resolve()
    scons = args.scons.resolve()
    helper = args.helper.resolve()
    manifest = json.loads((compiler / "package.json").read_text())
    if manifest["name"] != "toolchain-atmelavr" or manifest["version"] != "1.70300.191015":
        raise ValueError("The regression requires the unchanged pinned GCC7 AVR package")
    if json.loads((platform / "platform.json").read_text())["version"] != "5.3.0":
        raise ValueError("The regression requires pinned atmelavr 5.3.0")
    binary = compiler / "bin/avr-gcc"
    header = binary.read_bytes()[:20]
    if header[:7] != b"\x7fELF\x02\x01\x01" or header[18:20] != b"\x3e\0":
        raise ValueError("The compiler package must contain its original x64 GCC executable")
    work = args.work.resolve()
    project = work / "project"
    core = project / ".tabstudio/platformio-boxed"
    if (core / "packages").exists():
        raise ValueError("Use a fresh private work directory; never reuse mutated package state for this proof")
    (project / "src").mkdir(parents=True)
    (core / "packages").mkdir(parents=True)
    (core / "platforms").mkdir()
    for source, destination in [(compiler, core / "packages/toolchain-atmelavr"),
                                (scons, core / "packages/tool-scons"),
                                (platform, core / "platforms/atmelavr")]:
        shutil.copytree(source, destination, symlinks=True)
    (project / "platformio.ini").write_text("[env:proof]\nplatform=atmelavr@5.3.0\nboard=uno\nextra_scripts=pre:audit.py\n")
    audit = work / "python-processes.jsonl"
    actual = work / "scons-policy.jsonl"
    (project / "audit.py").write_text("\n".join([
        "Import('env')",
        "import json, os, platform",
        "from platformio.package.manager.tool import ToolPackageManager",
        "with open(" + repr(str(actual)) + ", 'a') as output:",
        "    output.write(json.dumps({'pid':os.getpid(),'machine':platform.machine(),'x64PackageVisible':bool(ToolPackageManager.is_system_compatible(['linux_x86_64']))}) + '\\n')", "",
    ]))
    source = project / "src/main.c"
    source.write_text("#include <avr/io.h>\nint main(void){DDRB|=_BV(5);for(;;){PORTB^=_BV(5);}}\n")
    native = work / "simulated-arm-python"
    missing = work / "missing-child-policy-python"
    interpreter(native, audit, False)
    interpreter(missing, audit, True)
    environment = dict(os.environ)
    environment.pop("TABSTUDIO_USB_FD", None)  # This proof has no USB transfer; FD lifetime is qualified separately.
    environment.update(TABSTUDIO_PLATFORMIO_BOX64="1", PLATFORMIO_SETTING_ENABLE_TELEMETRY="no",
        PLATFORMIO_SETTING_CHECK_PLATFORMIO_INTERVAL="7")
    # A source-tree proof has no installed /opt entry; native CI uses that actual
    # installed location. This import path is local to these test subprocesses.
    if not Path("/opt/tabstudio/embedded/box64_runtime.py").is_file():
        environment["PYTHONPATH"] = str(helper.parent)
    results = {}

    def run(name, python, target=None):
        environment["PYTHONEXEPATH"] = str(python)
        argv = [str(python), "-P", str(helper), "--", "run", "--jobs", "2", "--verbose"]
        if target:
            argv.extend(["--target", target])
        result = subprocess.run(argv, cwd=project, env=environment, text=True,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
        (work / (name + ".log")).write_text(result.stdout)
        results[name] = {"exitCode": result.returncode, "argv": argv}
        print(result.stdout, end="", flush=True)
        return result

    firmware = project / ".pio/build/proof/firmware.elf"
    negative = run("missing-child-policy", missing)
    if negative.returncode == 0 or firmware.exists():
        raise AssertionError("Removing only the SCons policy did not fail before producing firmware")
    positive = run("complete-child-policy", native)
    if positive.returncode or not firmware.is_file():
        raise AssertionError("The production policy did not reach real GCC/linking in fresh SCons")
    data = firmware.read_bytes()
    if data[:4] != b"\x7fELF" or data[18:20] != b"\x53\0":
        raise AssertionError("The actual compiler did not link AVR firmware")
    firmware_hash = hashlib.sha256(data).hexdigest()
    database = run("compilation-database", native, "compiledb")
    if database.returncode or not (project / "compile_commands.json").is_file():
        raise AssertionError("The fresh SCons compilation database child lost the policy")
    source.write_text("#error DELIBERATE_CHILD_POLICY_BUILD_FAILURE\n")
    failure = run("compiler-failure", native)
    if failure.returncode == 0 or "DELIBERATE_CHILD_POLICY_BUILD_FAILURE" not in failure.stdout:
        raise AssertionError("Core's real compiler failure was swallowed by the wrapper")
    policy_records = [json.loads(line) for line in actual.read_text().splitlines()]
    process_records = [json.loads(line) for line in audit.read_text().splitlines()]
    if not any(not item["x64PackageVisible"] for item in policy_records):
        raise AssertionError("The negative fresh SCons child was not observed without the policy")
    positive_records = [item for item in policy_records if item["x64PackageVisible"]]
    if len(positive_records) < 3 or len({item["pid"] for item in positive_records}) != len(positive_records):
        raise AssertionError("Every actual build/compiledb/failure must reach a distinct fresh SCons process")
    overlay = json.loads((core / "packages/toolchain-atmelavr/.tabstudio-box64-overlay.json").read_text())
    original = Path(overlay["original"]) / "bin/avr-gcc"
    if digest(binary) != digest(original):
        raise AssertionError("The x64 compiler bytes changed")
    evidence = {"passed":True,"coreVersion":platformio.__version__,"hostArchitecture":os.uname().machine,
        "pythonReportedArchitecture":"aarch64","simulationOnly":True,"hardwareVerified":False,
        "compilerSha256":digest(binary),"firmwareSha256":firmware_hash,"results":results,
        "sconsRecords":policy_records,"processRecords":process_records}
    (work / "evidence.json").write_text(json.dumps(evidence, indent=2))
    print("PLATFORMIO_FRESH_SCONS_POLICY_PASSED original_compiler=true missing_policy_refused=true compiler_failure_propagated=true hardware=false", flush=True)


if __name__ == "__main__":
    main()
