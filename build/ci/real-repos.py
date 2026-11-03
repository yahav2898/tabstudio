#!/usr/bin/env python3
"""Clones the repositories in build/ci/real-repos.json at their pinned commits.

Usage: build/ci/real-repos.py <destination>
       build/ci/real-repos.py --migrate <destination>

Each repository lands in <destination>/<name>. Only the pinned commit is fetched, so the job sees exactly the
sources its baseline was measured on.

With --migrate, every repository whose serve entry names a migrate project gets its database ready for the serve
check: its EF Core migrations are applied with the .NET SDK's dotnet-ef (the repository's own global.json picks the
SDK), to the connection strings its connections entry reads from the environment. This prepares test data only; the
build and the serve check themselves happen in TabStudio.

TABSTUDIO_REAL_REPOS_ONLY (comma-separated names) limits both to some of the repositories, as it does the test, and
TABSTUDIO_REAL_REPOS_JOB to the repositories of one job (their "job"; unset, those that name none).

What goes over the network (git fetch, installing dotnet-ef) is tried again after a failure, after each of
RETRY_DELAYS, so a blip on GitHub's or nuget.org's side does not fail the job: a pinned commit that really is gone
still fails, after the last try.
"""
import json
import os
import pathlib
import subprocess
import sys
import time

# Seconds to wait before each new try of a network step: four tries in all.
RETRY_DELAYS = (5, 15, 30)


def run(*args: str, cwd: pathlib.Path, env: dict[str, str] | None = None) -> None:
    subprocess.run(args, cwd=cwd, check=True, env=env)


def run_with_retries(*args: str, cwd: pathlib.Path, env: dict[str, str] | None = None, delays=None, sleep=None) -> None:
    """Runs a command that uses the network, again after each of the delays while it fails."""
    delays = RETRY_DELAYS if delays is None else delays
    sleep = time.sleep if sleep is None else sleep
    for attempt in range(len(delays) + 1):
        try:
            run(*args, cwd=cwd, env=env)
            return
        except subprocess.CalledProcessError as error:
            if attempt == len(delays):
                raise
            print(f"{' '.join(args[:2])} failed with exit code {error.returncode}; trying again in {delays[attempt]} s "
                  f"(attempt {attempt + 2} of {len(delays) + 1}).", file=sys.stderr, flush=True)
            sleep(delays[attempt])


def clone(destination: pathlib.Path, repositories: list[dict], delays=None, sleep=None) -> None:
    for repository in repositories:
        folder = destination / repository["name"]
        folder.mkdir(parents=True, exist_ok=True)
        run("git", "init", "-q", cwd=folder)
        run_with_retries("git", "fetch", "-q", "--depth", "1", repository["url"], repository["sha"], cwd=folder, delays=delays, sleep=sleep)
        run("git", "-c", "advice.detachedHead=false", "checkout", "-q", "FETCH_HEAD", cwd=folder)
        print(f"{repository['name']}: {repository['sha']}")


def migrate(destination: pathlib.Path, repositories: list[dict]) -> None:
    tools = destination / ".tools"
    for repository in repositories:
        serve = repository.get("serve") or {}
        if "migrate" not in serve:
            continue

        env = dict(os.environ)
        for name, variable in serve.get("connections", {}).items():
            value = os.environ.get(variable)
            if not value:
                raise SystemExit(f"{repository['name']}: {variable} is not set, so its database cannot be prepared.")
            env["ConnectionStrings__" + name] = value

        folder = destination / repository["name"]
        if not (tools / "dotnet-ef").exists():
            run_with_retries("dotnet", "tool", "install", "dotnet-ef", "--version", "8.0.11", "--tool-path", str(tools), cwd=folder)
        run(str(tools / "dotnet-ef"), "database", "update", "--project", serve["migrate"], cwd=folder, env=env)
        print(f"{repository['name']}: migrated {serve['migrate']}")


def main() -> int:
    arguments = sys.argv[1:]
    migrating = arguments[:1] == ["--migrate"]
    if migrating:
        arguments = arguments[1:]
    if len(arguments) != 1:
        print(__doc__, file=sys.stderr)
        return 2

    destination = pathlib.Path(arguments[0])
    destination.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((pathlib.Path(__file__).parent / "real-repos.json").read_text(encoding="utf-8"))
    repositories = manifest["repositories"]
    # TABSTUDIO_REAL_REPOS_JOB names the job's repositories: those whose "job" it is, or, unset, those with none.
    job = os.environ.get("TABSTUDIO_REAL_REPOS_JOB") or None
    repositories = [r for r in repositories if r.get("job") == job]
    only = [n.strip().lower() for n in os.environ.get("TABSTUDIO_REAL_REPOS_ONLY", "").split(",") if n.strip()]
    if only:
        repositories = [r for r in repositories if r["name"].lower() in only]
    if migrating:
        migrate(destination, repositories)
    else:
        clone(destination, repositories)

    return 0


if __name__ == "__main__":
    sys.exit(main())
