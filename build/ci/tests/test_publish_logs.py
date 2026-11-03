"""publish-logs.sh against a local bare repository, with a stand-in for `gh api` that does what GitHub's Git Data API does."""
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest

SCRIPT = Path(__file__).parents[1] / "publish-logs.sh"

FAKE_GH = textwrap.dedent(r'''
    #!/usr/bin/env python3
    import json, os, subprocess, sys
    bare = os.environ["FAKE_BARE"]
    def git(*args, stdin=None):
        return subprocess.run(["git", "--git-dir", bare, *args], capture_output=True, text=True, input=stdin, check=True).stdout.strip()
    args = sys.argv[2:]
    method = "GET"
    if args[0] == "-X":
        method, args = args[1], args[2:]
    path, rest = args[0], args[1:]
    opts = dict(zip(rest[::2], rest[1::2]))
    with open(os.environ["FAKE_CALLS"], "a") as calls:
        calls.write(f"{method} {path}\n")
    if path.endswith("git/ref/heads/ci-logs"):
        print(git("rev-parse", "refs/heads/ci-logs"))
    elif "/git/commits/" in path:
        print(git("rev-parse", path.rsplit("/", 1)[1] + "^{tree}"))
    elif "/git/trees/" in path:  # <commit>:<folder>?recursive=1, printed as its blobs' hashes
        listing = subprocess.run(["git", "--git-dir", bare, "ls-tree", "-r", path.split("/git/trees/", 1)[1].split("?")[0]],
                                 capture_output=True, text=True)
        if listing.returncode:
            sys.exit(1)
        print("\n".join(line.split()[2] for line in listing.stdout.splitlines()))
    elif path.endswith("git/trees"):
        # Like GitHub: every entry goes in at its path over base_tree, and git mktree refuses objects the repository lacks.
        body = json.load(open(opts["--input"]))
        def put(tree, parts, entry):
            lines = git("ls-tree", tree).splitlines() if tree else []
            sub = next((l.split()[2] for l in lines if l.split("\t")[1] == parts[0]), None)
            kept = [l for l in lines if l.split("\t")[1] != parts[0]]
            if len(parts) == 1:
                kept.append(f"{entry['mode']} {entry['type']} {entry['sha']}\t{parts[0]}")
            else:
                kept.append(f"040000 tree {put(sub, parts[1:], entry)}\t{parts[0]}")
            return git("mktree", stdin="\n".join(kept) + "\n")
        tree = body.get("base_tree")
        for entry in body["tree"]:
            tree = put(tree, entry["path"].split("/"), entry)
        print(tree)
    elif path.endswith("git/commits"):
        body = json.load(open(opts["--input"]))
        print(git("-c", "user.name=t", "-c", "user.email=t@t", "commit-tree", body["tree"], "-p", body["parents"][0], "-m", body["message"]))
    elif path.endswith("git/refs/heads/ci-logs") and method == "PATCH":
        new, current = opts["-f"].split("=", 1)[1], git("rev-parse", "refs/heads/ci-logs")
        if subprocess.run(["git", "--git-dir", bare, "merge-base", "--is-ancestor", current, new]).returncode:
            sys.exit(1)  # not a fast-forward: another job's commit came first
        git("update-ref", "refs/heads/ci-logs", new, current)
        print("{}")
    else:
        sys.exit(2)
''').lstrip()


class PublishLogsTests(unittest.TestCase):
    def setUp(self):
        self.temp = Path(tempfile.mkdtemp())
        self.bare = self.temp / "remote.git"
        subprocess.run(["git", "init", "-q", "--bare", str(self.bare)], check=True)
        seed = self.temp / "seed"
        for name, text in {"app/old.log": "old", "branches/agent-x/app/gone.log": "gone", "branches/agent-y/libraries/l.log": "kept"}.items():
            (seed / name).parent.mkdir(parents=True, exist_ok=True)
            (seed / name).write_text(text)
        git = ["git", "-C", str(seed), "-c", "user.name=t", "-c", "user.email=t@t"]
        subprocess.run(git[:3] + ["init", "-q"], check=True)
        subprocess.run(git + ["add", "-A"], check=True)
        subprocess.run(git + ["commit", "-qm", "seed"], check=True)
        subprocess.run(git + ["push", "-q", str(self.bare), "HEAD:refs/heads/ci-logs"], check=True)
        # Records what each push of a temporary branch carried.
        self.pushed = self.temp / "pushed"
        hook = self.bare / "hooks" / "pre-receive"
        hook.write_text(textwrap.dedent(f'''\
            #!/bin/sh
            while read old new ref; do
              case "$ref:$new" in refs/heads/ci-logs-staging/*:*[1-9a-f]*) git ls-tree --name-only "$new" >> "{self.pushed}";; esac
            done
        '''))
        hook.chmod(0o755)
        bin_dir = self.temp / "bin"
        bin_dir.mkdir()
        (bin_dir / "gh").write_text(FAKE_GH)
        (bin_dir / "gh").chmod(0o755)
        # The script's remote, pointed at the bare repository; everything else is the script as CI runs it.
        script = SCRIPT.read_text().replace('remote="https://', f'remote="{self.bare}" #', 1)
        self.script = self.temp / "publish-logs.sh"
        self.script.write_text(script)
        self.work = self.temp / "work"
        (self.work / "ci-out" / "screenshots").mkdir(parents=True)
        (self.work / "ci-out" / "test.log").write_text("new")
        (self.work / "ci-out" / "screenshots" / "s.png").write_text("png")
        self.calls = self.temp / "calls"
        self.env = dict(os.environ, PATH=f"{bin_dir}{os.pathsep}{os.environ['PATH']}", FAKE_BARE=str(self.bare), FAKE_CALLS=str(self.calls),
                        GITHUB_TOKEN="t", GITHUB_REPOSITORY="o/r", GITHUB_SHA="abcdef1234", GITHUB_RUN_ID="7", GITHUB_RUN_ATTEMPT="1")

    def publish(self, job, branch):
        return subprocess.Popen(["bash", str(self.script), job, "success"], cwd=self.work, env=dict(self.env, GITHUB_REF_NAME=branch),
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)

    def files(self):
        out = subprocess.run(["git", "--git-dir", str(self.bare), "ls-tree", "-r", "--name-only", "ci-logs"], capture_output=True, text=True, check=True)
        return set(out.stdout.split())

    def refs(self):
        out = subprocess.run(["git", "--git-dir", str(self.bare), "for-each-ref", "--format=%(refname)"], capture_output=True, text=True, check=True)
        return out.stdout.split()

    def test_a_job_replaces_only_its_own_folder_without_fetching_the_branch(self):
        for branch in ("main", "agent/x"):
            run = self.publish("app", branch)
            output = run.communicate(timeout=120)[0]
            self.assertEqual(0, run.returncode, output)
            self.assertNotIn("fetching ci-logs instead", output)
        self.assertEqual({"app/STATUS", "app/test.log", "app/screenshots/s.png",
                          "branches/agent-x/app/STATUS", "branches/agent-x/app/test.log", "branches/agent-x/app/screenshots/s.png",
                          "branches/agent-y/libraries/l.log"}, self.files())
        self.assertEqual(["refs/heads/ci-logs"], self.refs())  # the temporary branches are gone

    def test_files_github_has_already_are_not_pushed_again(self):
        def blob(path):
            return subprocess.run(["git", "hash-object", str(path)], capture_output=True, text=True, check=True).stdout.strip()
        for text in ("first", "second"):
            (self.work / "ci-out" / "test.log").write_text(text)
            self.pushed.write_text("")
            run = self.publish("app", "main")
            output = run.communicate(timeout=120)[0]
            self.assertEqual(0, run.returncode, output)
            self.assertNotIn("fetching ci-logs instead", output)
        pushed = set(self.pushed.read_text().split())
        self.assertIn(blob(self.work / "ci-out" / "test.log"), pushed)
        self.assertNotIn(blob(self.work / "ci-out" / "screenshots" / "s.png"), pushed)
        self.assertEqual("second", subprocess.run(["git", "--git-dir", str(self.bare), "show", "ci-logs:app/test.log"],
                                                  capture_output=True, text=True, check=True).stdout)
        self.assertIn("app/screenshots/s.png", self.files())

    def test_jobs_publishing_at_once_keep_each_other(self):
        runs = [self.publish(job, "main") for job in ("j1", "j2", "j3", "j4")]
        for run in runs:
            output = run.communicate(timeout=180)[0]
            self.assertEqual(0, run.returncode, output)
        self.assertTrue({f"{job}/test.log" for job in ("j1", "j2", "j3", "j4")} <= self.files())
        self.assertEqual(["refs/heads/ci-logs"], self.refs())


if __name__ == "__main__":
    unittest.main()
