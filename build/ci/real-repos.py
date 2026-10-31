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
"""
import json
import os
import pathlib
import subprocess
import sys


def run(*args: str, cwd: pathlib.Path, env: dict[str, str] | None = None) -> None:
    subprocess.run(args, cwd=cwd, check=True, env=env)


def clone(destination: pathlib.Path, repositories: list[dict]) -> None:
    for repository in repositories:
        folder = destination / repository["name"]
        folder.mkdir(parents=True, exist_ok=True)
        run("git", "init", "-q", cwd=folder)
        run("git", "fetch", "-q", "--depth", "1", repository["url"], repository["sha"], cwd=folder)
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
            run("dotnet", "tool", "install", "dotnet-ef", "--version", "8.0.11", "--tool-path", str(tools), cwd=folder)
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
