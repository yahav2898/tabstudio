"""publish-release.sh with a stand-in for `gh release` that keeps the releases in a file."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest

SCRIPT = Path(__file__).parents[1] / "publish-release.sh"

FAKE_GH = textwrap.dedent(r'''
    #!/usr/bin/env python3
    import json, os, sys
    state_file = os.environ["FAKE_RELEASES"]
    state = json.load(open(state_file))
    # gh release <command> <arguments>
    command, args = sys.argv[2], sys.argv[3:]
    with open(os.environ["FAKE_CALLS"], "a") as calls:
        calls.write(" ".join(sys.argv[1:]) + "\n")
    if command == "list":
        print("\n".join(state["tags"]))
    elif command == "view":
        sys.exit(0 if args[0] in state["tags"] else 1)
    elif command == "create":
        state["tags"].append(args[0])
        json.dump(state, open(state_file, "w"))
        if state.get("fail_after_create", 0) > 0:
            state["fail_after_create"] -= 1
            json.dump(state, open(state_file, "w"))
            print("HTTP 500 (https://api.github.com/repos/o/r/releases)", file=sys.stderr)
            sys.exit(1)
    elif command not in ("edit", "upload"):
        sys.exit(2)
''').lstrip()


class PublishReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = Path(tempfile.mkdtemp())
        bin_dir = self.temp / "bin"
        bin_dir.mkdir()
        (bin_dir / "gh").write_text(FAKE_GH)
        (bin_dir / "gh").chmod(0o755)
        (bin_dir / "sleep").write_text("#!/bin/sh\n")
        (bin_dir / "sleep").chmod(0o755)
        self.repo = self.temp / "repo"
        (self.repo / "docs" / "releases").mkdir(parents=True)
        self.releases = self.temp / "releases.json"
        self.calls = self.temp / "calls"
        self.apk = self.temp / "TabStudio.apk"
        self.apk.write_text("apk")
        self.env = dict(os.environ, PATH=f"{bin_dir}{os.pathsep}{os.environ['PATH']}", FAKE_RELEASES=str(self.releases),
                        FAKE_CALLS=str(self.calls), GITHUB_SHA="abc", GH_TOKEN="t", GH_REPO="o/r")

    def publish(self, version, published, fail_after_create=0):
        (self.repo / "Directory.Build.props").write_text(f"<Project><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>\n")
        (self.repo / "docs" / "releases" / f"{version}.md").write_text("notes")
        self.releases.write_text(json.dumps({"tags": published, "fail_after_create": fail_after_create}))
        self.calls.write_text("")
        run = subprocess.run(["bash", str(SCRIPT), "auto", str(self.apk)], cwd=self.repo, env=self.env, capture_output=True, text=True)
        return run, [line.split() for line in self.calls.read_text().splitlines()]

    def test_the_newest_version_is_marked_latest(self):
        run, calls = self.publish("0.21.6", ["v0.21.4", "v0.21.5", "v0.9.0"])
        self.assertEqual(0, run.returncode, run.stderr)
        create = next(c for c in calls if c[1] == "create")
        self.assertIn("--latest", create)

    def test_an_older_version_finishing_after_a_newer_one_is_not_marked_latest(self):
        run, calls = self.publish("0.21.5", ["v0.21.4", "v0.21.6"])
        self.assertEqual(0, run.returncode, run.stderr)
        create = next(c for c in calls if c[1] == "create")
        self.assertIn("--latest=false", create)
        self.assertNotIn("--latest", create)

    def test_an_error_after_the_release_was_created_completes_it_instead_of_creating_it_again(self):
        run, calls = self.publish("0.21.5", ["v0.21.4"], fail_after_create=1)
        self.assertEqual(0, run.returncode, run.stderr)
        self.assertEqual(1, sum(c[1] == "create" for c in calls))
        self.assertTrue(any(c[1] == "upload" and "--clobber" in c for c in calls))
        self.assertEqual(["v0.21.4", "v0.21.5"], json.loads(self.releases.read_text())["tags"])


if __name__ == "__main__":
    unittest.main()
