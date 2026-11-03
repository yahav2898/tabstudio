"""publish-logs.sh: logs reach the ci-logs branch, and a stalled push never holds a job past its time."""

import os
import shutil
import subprocess
import tempfile
import time
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), "..", "publish-logs.sh")
REAL_GIT = shutil.which("git")


def git(*args, cwd=None):
    return subprocess.run([REAL_GIT, *args], cwd=cwd, check=True, capture_output=True, text=True).stdout


class PublishLogsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.mkdtemp()
        self.remote = os.path.join(self.temp, "remote.git")
        git("init", "-q", "--bare", self.remote)
        self.job = os.path.join(self.temp, "job")
        os.makedirs(os.path.join(self.job, "ci-out"))
        with open(os.path.join(self.job, "ci-out", "test.log"), "w") as log:
            log.write("all passed\n")

    def tearDown(self):
        shutil.rmtree(self.temp, ignore_errors=True)

    def run_script(self, path=None, **extra):
        env = dict(os.environ, GITHUB_TOKEN="x", GITHUB_REPOSITORY="owner/repo", GITHUB_REF_NAME="agent/test",
                   GITHUB_SHA="0123456789abcdef", GITHUB_RUN_ID="1", TABSTUDIO_PUBLISH_REMOTE=self.remote, **extra)
        if path:
            env["PATH"] = path + os.pathsep + env["PATH"]
        start = time.monotonic()
        result = subprocess.run(["bash", os.path.abspath(SCRIPT), "libraries", "success"], cwd=self.job, env=env,
                                capture_output=True, text=True, timeout=120)
        return result, time.monotonic() - start

    def test_logs_are_published_to_the_branch_folder(self):
        result, _ = self.run_script()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("all passed\n", git("--git-dir", self.remote, "show", "ci-logs:branches/agent-test/libraries/test.log"))

    def test_a_push_that_stalls_gives_up_by_the_deadline_without_failing_the_job(self):
        # The SSH job's tests passed in four minutes, then its publish stalled until the job's 20 minutes ran out.
        bin_dir = os.path.join(self.temp, "bin")
        os.makedirs(bin_dir)
        wrapper = os.path.join(bin_dir, "git")
        with open(wrapper, "w") as script:
            script.write(f'#!/usr/bin/env bash\nfor a in "$@"; do [ "$a" = push ] && exec sleep 600; done\nexec "{REAL_GIT}" "$@"\n')
        os.chmod(wrapper, 0o755)

        result, elapsed = self.run_script(bin_dir, TABSTUDIO_PUBLISH_GIT_TIMEOUT="2", TABSTUDIO_PUBLISH_DEADLINE="5")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertLess(elapsed, 60)
        self.assertIn("Could not publish the logs of libraries", result.stderr)


    def test_a_git_call_outside_the_limits_that_hangs_is_stopped_with_everything_it_started(self):
        # With every fetch and push under a limit, publishing still held the SSH job for 17 minutes: another Git call,
        # and what it started, kept going. The watchdog stops the whole publish.
        bin_dir = os.path.join(self.temp, "bin")
        os.makedirs(bin_dir)
        wrapper = os.path.join(bin_dir, "git")
        with open(wrapper, "w") as script:
            script.write(f'#!/usr/bin/env bash\nfor a in "$@"; do [ "$a" = write-tree ] && {{ sleep 600 & sleep 600; }}; done\nexec "{REAL_GIT}" "$@"\n')
        os.chmod(wrapper, 0o755)

        result, elapsed = self.run_script(bin_dir, TABSTUDIO_PUBLISH_DEADLINE="3", TABSTUDIO_PUBLISH_GRACE="3")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertLess(elapsed, 30)
        self.assertIn("took too long and was stopped", result.stderr)


if __name__ == "__main__":
    unittest.main()
