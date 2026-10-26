#!/usr/bin/env python3
"""Keep a bound working directory usable when another PRoot instance exits."""

import os
from pathlib import Path
import selectors
import signal
import subprocess
import sys
import tempfile


def ready(process):
    with selectors.DefaultSelector() as selector:
        selector.register(process.stdout, selectors.EVENT_READ)
        if not selector.select(10):
            raise TimeoutError("PRoot did not report its working directory")
    line = process.stdout.readline().strip()
    if line != "/shared/project":
        raise AssertionError(f"PRoot did not enter the bound directory: {line}")


def run(proot, private_paths):
    with tempfile.TemporaryDirectory(prefix="tabstudio-proot-cwd-") as folder:
        root = Path(folder) / "rootfs"
        project = Path(folder) / "project"
        root.mkdir()
        project.mkdir()
        bindings = []
        for name in ("usr", "bin", "lib", "lib64", "proc"):
            if Path("/" + name).exists():
                (root / name).mkdir()
                bindings.extend(["-b", "/" + name])

        environment = os.environ.copy()
        environment.pop("PROOT_DONT_POLLUTE_ROOTFS", None)
        if private_paths:
            environment["PROOT_DONT_POLLUTE_ROOTFS"] = "1"
        arguments = [
            proot, "--kill-on-exit", "-r", str(root), *bindings,
            "-b", f"{project}:/shared/project", "-w", "/shared/project",
            "/usr/bin/python3", "-u", "-c",
            "import os,sys;print(os.getcwd(),flush=True);"
            "sys.stdin.readline();print(os.getcwd(),flush=True)",
        ]
        processes = []
        try:
            for _ in range(2):
                process = subprocess.Popen(
                    arguments, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE, text=True, env=environment,
                    start_new_session=True,
                )
                processes.append(process)
                ready(process)

            first, second = processes
            first_output, first_error = first.communicate("exit\n", timeout=10)
            assert first.returncode == 0, (first_output, first_error)
            output, error = second.communicate("continue\n", timeout=10)
            assert project.is_dir(), "The host project must still exist"
            if private_paths:
                assert second.returncode == 0, error
                assert output.strip() == "/shared/project", output
            print(f"PRoot private bind paths={private_paths}: exit={second.returncode}; "
                  f"{output.strip() or error.strip()}", flush=True)
        finally:
            for process in processes:
                if process.poll() is None:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.communicate(timeout=10)


if __name__ == "__main__":
    binary = str(Path(sys.argv[1]).resolve())
    # The pinned upstream build's control demonstrates the shared-placeholder failure; the guarded run must pass.
    run(binary, private_paths=False)
    run(binary, private_paths=True)
