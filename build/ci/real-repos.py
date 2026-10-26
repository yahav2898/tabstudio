#!/usr/bin/env python3
"""Clones the repositories in build/ci/real-repos.json at their pinned commits.

Usage: build/ci/real-repos.py <destination>

Each repository lands in <destination>/<name>. Only the pinned commit is fetched, so the job sees exactly the
sources its baseline was measured on.
"""
import json
import pathlib
import subprocess
import sys


def run(*args: str, cwd: pathlib.Path) -> None:
    subprocess.run(args, cwd=cwd, check=True)


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2

    destination = pathlib.Path(sys.argv[1])
    destination.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((pathlib.Path(__file__).parent / "real-repos.json").read_text(encoding="utf-8"))
    for repository in manifest["repositories"]:
        folder = destination / repository["name"]
        folder.mkdir(parents=True, exist_ok=True)
        run("git", "init", "-q", cwd=folder)
        run("git", "fetch", "-q", "--depth", "1", repository["url"], repository["sha"], cwd=folder)
        run("git", "-c", "advice.detachedHead=false", "checkout", "-q", "FETCH_HEAD", cwd=folder)
        print(f"{repository['name']}: {repository['sha']}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
