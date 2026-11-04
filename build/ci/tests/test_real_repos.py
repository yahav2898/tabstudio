"""Cloning the pinned repositories survives a network blip, against a real local git repository."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("real_repos", Path(__file__).parents[1] / "real-repos.py")
real_repos = importlib.util.module_from_spec(spec)
spec.loader.exec_module(real_repos)


def git(*args, cwd):
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


class CloneTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        self.upstream = root / "upstream"
        self.upstream.mkdir()
        git("init", "-q", cwd=self.upstream)
        git("config", "uploadpack.allowReachableSHA1InWant", "true", cwd=self.upstream)
        (self.upstream / "README.md").write_text("pinned\n")
        git("add", "README.md", cwd=self.upstream)
        git("-c", "user.name=CI", "-c", "user.email=ci@example.invalid", "commit", "-q", "-m", "pinned", cwd=self.upstream)
        self.sha = git("rev-parse", "HEAD", cwd=self.upstream)
        self.destination = root / "clones"
        self.repository = {"name": "sample", "url": self.upstream.as_uri(), "sha": self.sha}
        self.waits = []
        self.real_run = real_repos.run

    def tearDown(self):
        real_repos.run = self.real_run
        self.temp.cleanup()

    def fail_fetch(self, times):
        """Makes the next `times` git fetches fail as a dropped connection does, then lets them through."""
        failures = {"left": times, "fetches": 0}

        def run(*args, cwd, env=None):
            if args[:2] == ("git", "fetch"):
                failures["fetches"] += 1
                if failures["left"] > 0:
                    failures["left"] -= 1
                    raise subprocess.CalledProcessError(128, list(args), "fatal: unable to access: Connection reset by peer")
            self.real_run(*args, cwd=cwd, env=env)

        real_repos.run = run
        return failures

    def test_a_fetch_that_fails_once_is_tried_again_and_checks_out_the_pinned_commit(self):
        failures = self.fail_fetch(1)

        real_repos.clone(self.destination, [self.repository], delays=(7, 8, 9), sleep=self.waits.append)

        self.assertEqual(2, failures["fetches"])
        self.assertEqual([7], self.waits)
        self.assertEqual(self.sha, git("rev-parse", "HEAD", cwd=self.destination / "sample"))
        self.assertEqual("pinned\n", (self.destination / "sample" / "README.md").read_text())

    def test_a_fetch_that_keeps_failing_fails_after_the_last_try(self):
        failures = self.fail_fetch(10)

        with self.assertRaises(subprocess.CalledProcessError):
            real_repos.clone(self.destination, [self.repository], delays=(1, 2), sleep=self.waits.append)

        self.assertEqual(3, failures["fetches"])
        self.assertEqual([1, 2], self.waits)

    def test_a_clean_fetch_does_not_wait(self):
        real_repos.clone(self.destination, [self.repository], delays=(1, 2), sleep=self.waits.append)

        self.assertEqual([], self.waits)
        self.assertEqual(self.sha, git("rev-parse", "HEAD", cwd=self.destination / "sample"))


if __name__ == "__main__":
    unittest.main()
