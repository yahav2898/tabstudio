#!/usr/bin/env python3
"""Compile pinned public embedded repositories with their real toolchains, with no target attached.

This is host-side compiler verification, distinct from Android runtime/USB and hardware verification.
Each command has a hard timeout. Logs and a machine-readable summary survive failures. No compiler
errors are allowed. Upstream dependency locks are retained or generated locks archived honestly.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tarfile
import time
import urllib.request


ROOT = Path(__file__).resolve().parents[2]
SHA = re.compile(r"[0-9a-f]{40}\Z")
URL = re.compile(r"https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\.git)?\Z")
ERROR = re.compile(r"(?:^|\s)(?:fatal\s+)?error(?:\[[A-Z0-9]+\])?:\s", re.I)
ARDUINO_URL = "https://github.com/arduino/arduino-cli/releases/download/v1.3.1/arduino-cli_1.3.1_Linux_64bit.tar.gz"
ARDUINO_SHA256 = "376428d7d45be640c00812a71612e1742edc2f5f9ee3742a2d6da7870e079588"


def arm_cross_prefix(environment: dict) -> str:
    """Zephyr's NO_DEFAULT_PATH compiler search requires a complete cross-compiler prefix."""
    compiler = shutil.which("arm-none-eabi-gcc", path=environment.get("PATH"))
    if compiler is None:
        raise RuntimeError("Install the Arm GNU cross compiler before building this Zephyr sample")
    return str(Path(compiler).absolute()).removesuffix("gcc")


def cmake_project_name(folder: Path) -> str:
    """The name in a CMakeLists.txt's project() call, which ESP-IDF gives the firmware it builds."""
    match = re.search(r"(?im)^\s*project\(\s*([A-Za-z0-9_.+-]+)", (folder / "CMakeLists.txt").read_text(encoding="utf-8"))
    if match is None:
        raise RuntimeError(f"No project() in {folder / 'CMakeLists.txt'}")
    return match.group(1)


def pin_platform(configuration: str, repository: Path) -> str:
    """A PlatformIO configuration whose platform is the checked-out platform repository instead of the registry's.

    A platform repository (platform-atmelavr, platform-espressif32, platform-ststm32, ...) names itself in its
    platform.json; its examples name that platform unversioned, which would install whatever the registry has now.
    """
    manifest = repository / "platform.json"
    name = json.loads(manifest.read_text(encoding="utf-8"))["name"] if manifest.is_file() else "atmelavr"
    return re.sub(r"(?m)^platform[ \t]*=[ \t]*" + re.escape(name) + r"[ \t]*$", "platform = file://" + repository.as_posix(), configuration)


def entries(manifest: Path | None = None) -> list[dict]:
    """Read the shared Real repositories manifest, rejecting unpinned or unsafe entries."""
    path = manifest or ROOT / "build/ci/real-repos.json"
    result = json.loads(path.read_text(encoding="utf-8"))["embeddedRepositories"]
    names: set[str] = set()
    for entry in result:
        if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,63}", entry["name"]) or entry["name"] in names:
            raise ValueError("Embedded repository names must be unique safe directory names")
        names.add(entry["name"])
        for source in [entry] + ([entry["sdk"]] if "sdk" in entry else []):
            if not SHA.fullmatch(source["sha"]) or not URL.fullmatch(source["url"]):
                raise ValueError(f"Unpinned/unsupported repository: {source}")
        if entry.get("maxErrors") != 0:
            raise ValueError("Embedded baselines must remain at zero compiler errors")
        project = Path(entry["project"])
        if project.is_absolute() or ".." in project.parts:
            raise ValueError("Project paths must stay inside the checkout")
    return result


class Runner:
    """Run one isolated repository and retain every command's result."""

    def __init__(self, entry: dict, work: Path, output: Path):
        self.entry = entry
        self.work = work / entry["name"]
        self.output = output / entry["name"]
        self.work.mkdir(parents=True, exist_ok=True)
        self.output.mkdir(parents=True, exist_ok=True)
        self.steps: list[dict] = []
        self.errors = 0

    def run(self, args: list[str], cwd: Path, env: dict | None = None, timeout: int = 1200, count_errors: bool = False) -> None:
        env = dict(os.environ if env is None else env)
        env.pop("PYTHONSAFEPATH", None)
        env["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "no"
        label = f"{len(self.steps) + 1:02d}-{Path(args[0]).name}"
        log = self.output / (label + ".log")
        started = time.monotonic()
        step = {"argv": args, "cwd": str(cwd), "log": log.name}
        self.steps.append(step)
        print("+ " + subprocess.list2cmdline(args), flush=True)
        with log.open("w", encoding="utf-8") as stream:
            process = subprocess.Popen(args, cwd=cwd, env=env, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                code = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
                step["timeout"] = timeout
                raise RuntimeError(f"Command exceeded {timeout}s; see {log}") from None
            finally:
                step["seconds"] = round(time.monotonic() - started, 3)
        step["exitCode"] = code
        text = log.read_text(encoding="utf-8", errors="replace")
        # Full logs are artifacts. Keep console tails useful without truncating the saved evidence.
        print(text[-12000:], flush=True)
        if count_errors:
            found = sum(bool(ERROR.search(line)) for line in text.splitlines())
            self.errors += found
            step["compilerErrors"] = found
        if code:
            raise RuntimeError(f"Command exited {code}: {args[0]}; see {log}")

    def clone(self, source: dict, name: str, submodules: bool = False) -> Path:
        destination = self.work / name
        if destination.exists() and any(destination.iterdir()):
            raise ValueError(f"Refusing to overwrite a previous checkout: {destination}")
        destination.mkdir(exist_ok=True)
        self.run(["git", "init", "-q"], destination)
        # Relative .gitmodules URLs are resolved against the authoritative origin. Fetching a URL
        # without registering it makes Git resolve ESP-IDF's ../../owner/repo paths against /tmp.
        self.run(["git", "remote", "add", "origin", source["url"]], destination)
        self.run(["git", "fetch", "--depth", "1", "--no-tags", "origin", source["sha"]], destination)
        self.run(["git", "checkout", "--detach", "FETCH_HEAD"], destination)
        actual = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=destination, text=True).strip()
        if actual != source["sha"]:
            raise RuntimeError(f"Checkout SHA mismatch: {actual}")
        if submodules:
            self.run(["git", "-c", "protocol.file.allow=never", "submodule", "update", "--init", "--recursive", "--depth", "1"], destination)
        return destination

    def arduino_cli(self) -> str:
        archive = self.work / "arduino-cli.tar.gz"
        with urllib.request.urlopen(ARDUINO_URL, timeout=90) as response, archive.open("wb") as target:
            while block := response.read(1024 * 1024):
                target.write(block)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != ARDUINO_SHA256:
            raise RuntimeError("Arduino CLI download SHA-256 mismatch")
        executable = self.work / "arduino-cli"
        # Extract only the named executable: no tar paths, symlinks or device nodes are accepted.
        with tarfile.open(archive, "r:gz") as package:
            member = package.getmember("arduino-cli")
            if not member.isfile() or member.size > 100 * 1024 * 1024:
                raise RuntimeError("Unexpected Arduino CLI archive member")
            with package.extractfile(member) as data, executable.open("wb") as target:
                target.write(data.read())
        executable.chmod(0o755)
        return str(executable)

    def build(self) -> None:
        e = self.entry
        kind = e["kind"]
        repo = self.clone(e, "source", submodules=kind == "esp-idf")
        project = repo / e["project"]
        build = self.work / "build"
        env = dict(os.environ)
        # Builds must not send PlatformIO analytics, including a first run before settings exist.
        env["PLATFORMIO_SETTING_ENABLE_TELEMETRY"] = "no"
        # Vendor SDK scripts import sibling modules from their script directories. Python safe-path
        # mode is scoped to PlatformIO; applying it to ESP-IDF/Zephyr breaks their official scripts.
        env.pop("PYTHONSAFEPATH", None)
        if kind == "platformio":
            # The upstream example names an unversioned registry platform. Use this exact checked-out
            # platform implementation instead; archive the explicit override and leave its sources intact.
            original = (project / "platformio.ini").read_text(encoding="utf-8")
            pinned = pin_platform(original, repo)
            configuration = project / "tabstudio-ci.ini"
            configuration.write_text(pinned, encoding="utf-8")
            (self.output / "platformio-effective.ini").write_text(pinned, encoding="utf-8")
            # platform-atmelavr has a top-level platform.py; starting Python there would shadow its
            # standard-library platform module before PlatformIO can import it.
            self.run([sys.executable, "-P", "-m", "platformio", "run", "--project-dir", str(project), "--project-conf", str(configuration), "--environment", e["environment"]], project, env, count_errors=True)
            self.require(project / ".pio/build" / e["environment"] / "firmware.elf")
        elif kind == "arduino":
            cli = self.arduino_cli()
            env["ARDUINO_DIRECTORIES_DATA"] = str(self.work / "arduino-data")
            env["ARDUINO_DIRECTORIES_USER"] = str(self.work / "arduino-user")
            self.run([cli, "core", "update-index"], repo, env)
            self.run([cli, "core", "install", e["core"]], repo, env)
            self.run([cli, "compile", "--fqbn", e["fqbn"], "--library", str(repo), "--build-path", str(build), str(project)], repo, env, count_errors=True)
            # arduino-cli names the firmware after the sketch folder.
            self.require(build / f"{project.name}.ino.elf")
        elif kind == "stm32":
            # This real project committed a Windows CMakeCache inside its preset's binaryDir.
            # Retain the vendor preset/toolchain while overriding only the disposable build folder.
            self.run(["cmake", "--preset", e["preset"], "-B", str(build), "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], project, count_errors=True)
            self.run(["cmake", "--build", str(build), "--parallel", "2"], project, count_errors=True)
            self.require(build / "blink.elf")
        elif kind == "pico":
            sdk = self.clone(e["sdk"], "pico-sdk", submodules=True)
            self.run(["cmake", "-S", str(project), "-B", str(build), "-G", "Ninja", "-DPICO_SDK_PATH=" + str(sdk), "-DPICO_BOARD=pico", "-DPICO_NO_PICOTOOL=1", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], repo, count_errors=True)
            self.run(["cmake", "--build", str(build), "--target", e["target"], "--parallel", "2"], repo, count_errors=True)
            self.require(build / "blink/blink.elf")
        elif kind == "esp-idf":
            env.update(IDF_PATH=str(repo), IDF_TOOLS_PATH=str(self.work / "idf-tools"))
            self.run(["bash", str(repo / "install.sh"), e["target"]], repo, env)
            # export.sh computes the vendor Python/toolchain environment. Arguments stay positional;
            # neither repository paths nor project settings are interpolated into shell source.
            self.run(["bash", "-c", 'set -e; . "$IDF_PATH/export.sh"; exec python "$IDF_PATH/tools/idf.py" -C "$1" -B "$2" "-DIDF_TARGET=$3" build', "tabstudio-idf", str(project), str(build), e["target"]], repo, env, count_errors=True)
            # ESP-IDF names the firmware after the CMake project.
            self.require(build / f"{cmake_project_name(project)}.elf")
        elif kind == "zephyr":
            locks = ROOT / "src/TabStudio.Toolchains/Embedded/Installation"
            self.run([sys.executable, "-m", "pip", "install", "--require-hashes", "-r", str(locks / "zephyr-bootstrap.requirements.txt")], repo, env)
            self.run([sys.executable, "-m", "pip", "install", "--require-hashes", "--no-build-isolation", "-r", str(locks / "zephyr.requirements.txt")], repo, env)
            self.run(["west", "init", "-l", str(repo)], self.work)
            self.run(["west", "update", "--narrow", "-o=--depth=1", "cmsis", "cmsis_6"], self.work)
            env.update(ZEPHYR_BASE=str(repo), ZEPHYR_TOOLCHAIN_VARIANT="cross-compile", CROSS_COMPILE=arm_cross_prefix(env))
            self.run(["west", "build", "--build-opt=-j2", "-b", e["board"], "-d", str(build), str(project), "--", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], self.work, env, count_errors=True)
            self.require(build / "zephyr/zephyr.elf")
        elif kind == "rust":
            self.run(["rustup", "toolchain", "install", e["rust"], "--profile", "minimal", "--component", "rust-src", "--target", e["target"]], repo)
            env.update(RUSTUP_TOOLCHAIN=e["rust"], CARGO_TARGET_DIR=str(build))
            # A firmware crate builds a binary; a HAL crate's firmware is one of its examples, for the chip its
            # features name.
            target = ["--example", e["example"]] if "example" in e else ["--bin", e["binary"]]
            features = ["--features", e["features"]] if "features" in e else []
            self.run(["cargo", "build", "--manifest-path", str(project / "Cargo.toml"), "--target", e["target"], *target, *features, "--release"], project, env, count_errors=True)
            self.require(build / e["target"] / "release" / ("examples/" + e["example"] if "example" in e else e["binary"]))
            for lock in [project / "Cargo.lock", repo / "Cargo.lock"]:
                if lock.exists():
                    (self.output / "Cargo.lock").write_bytes(lock.read_bytes())
        else:
            raise ValueError("Unknown embedded builder: " + kind)
        if self.errors:
            raise RuntimeError(f"Compiler error baseline is zero, observed {self.errors}")

    def require(self, artifact: Path) -> None:
        if not artifact.is_file() or artifact.stat().st_size == 0:
            raise RuntimeError(f"Successful build did not produce the expected firmware: {artifact}")
        digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
        self.steps.append({"firmware": str(artifact), "bytes": artifact.stat().st_size, "sha256": digest})
        # Firmware only, never toolchains or vendor distributions.
        (self.output / artifact.name).write_bytes(artifact.read_bytes())

    def execute(self) -> bool:
        started = time.monotonic()
        error = None
        try:
            self.build()
        except Exception as failure:
            error = str(failure)
            print(error, file=sys.stderr, flush=True)
        finally:
            summary = {"repository": self.entry, "seconds": round(time.monotonic() - started, 3), "passed": error is None,
                       "error": error, "compilerErrors": self.errors, "hardwareVerified": False, "androidVerified": False, "steps": self.steps}
            (self.output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        return error is None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list", action="store_true", help="Print the workflow's matrix names")
    parser.add_argument("--only", help="Build exactly one named repository")
    parser.add_argument("--work", type=Path, default=ROOT / ".embedded-ci")
    parser.add_argument("--output", type=Path, default=ROOT / "ci-out/embedded")
    parser.add_argument("--manifest", type=Path)
    options = parser.parse_args()
    repositories = entries(options.manifest)
    if options.list:
        print(json.dumps([e["name"] for e in repositories]))
        return 0
    selected = [e for e in repositories if options.only is None or e["name"] == options.only]
    if not selected:
        parser.error("Unknown embedded repository: " + str(options.only))
    passed = [Runner(e, options.work.resolve(), options.output.resolve()).execute() for e in selected]
    return 0 if all(passed) else 1


if __name__ == "__main__":
    raise SystemExit(main())
