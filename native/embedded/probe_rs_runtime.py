#!/usr/bin/python3
"""Keep managed probe-rs 0.32 commands independent of automatic CLI presets.

Upstream main.rs at 48f5e4d53c690a1d40c2454033c6f785b4f4f95c loads
.probe-rs.{toml,json,yaml,yml} from cwd, executable directory and HOME, and
automatically applies a preset named 'default'. Clearing its environment switch
is insufficient. A fresh private HOME supplies an unpredictable empty preset,
selected explicitly without changing cwd, the upstream binary or project files.
"""
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import sys
import tempfile

MARKER = "TABSTUDIO_PROBE_RS_NO_PRESETS"
COMMANDS = frozenset(("read", "info", "download", "run", "attach"))


def launch(executable, arguments, environment=None):
    environment = dict(os.environ if environment is None else environment)
    managed = environment.pop(MARKER, None)
    if managed != "1":
        os.execve(executable, [executable, *arguments], environment)
    if not arguments or arguments[0] not in COMMANDS:
        raise ValueError("The managed probe-rs preset policy requires a supported read/info/download/run/attach command")
    if any(argument == "--preset" or argument.startswith("--preset=") for argument in arguments):
        raise ValueError("Managed probe-rs commands cannot select a project or user configuration preset")
    descriptors = ()
    if environment.get("TABSTUDIO_USB_FD") == "3":
        os.fstat(3)  # A missing permissioned FD is an error, never silently lost.
        descriptors = (3,)
    environment.pop("PROBE_RS_CONFIG_PRESET", None)
    # The typed caller also clears these. Keep the launch boundary conservative
    # if another managed entry point accidentally inherits remote credentials.
    environment.pop("PROBE_RS_REMOTE_HOST", None)
    environment.pop("PROBE_RS_REMOTE_TOKEN", None)
    child = None
    received = None
    previous = {}

    def forward(number, _frame):
        nonlocal received
        received = number
        if child is not None and child.poll() is None:
            try:
                child.send_signal(number)
            except ProcessLookupError:
                pass

    with tempfile.TemporaryDirectory(prefix="tabstudio-probe-preset-") as directory:
        preset = "tabstudio-" + secrets.token_hex(16)
        configuration = Path(directory) / ".probe-rs.json"
        descriptor = os.open(configuration, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump({"presets": {preset: {}}}, stream)
            stream.write("\n")
        environment["HOME"] = directory
        try:
            for number in (signal.SIGINT, signal.SIGTERM):
                previous[number] = signal.signal(number, forward)
            child = subprocess.Popen([executable, "--preset", preset, *arguments], env=environment,
                                     close_fds=True, pass_fds=descriptors)
            if received is not None:
                forward(received, None)
            code = child.wait()
            return code if code >= 0 else 128 - code
        finally:
            if child is not None and child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.wait()
            for number, handler in previous.items():
                signal.signal(number, handler)


if __name__ == "__main__":
    try:
        if len(sys.argv) < 2:
            raise ValueError("Supply the installed upstream probe-rs executable")
        raise SystemExit(launch(sys.argv[1], sys.argv[2:]))
    except (OSError, ValueError) as error:
        print("Managed probe-rs launch refused: " + str(error), file=sys.stderr)
        raise SystemExit(78)
