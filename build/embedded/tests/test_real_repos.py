"""Tests for the pinned-repository build runner, without downloading SDKs or claiming hardware evidence."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[1] / "run-real-repos.py"
SPEC = importlib.util.spec_from_file_location("embedded_real_repos", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class RealRepositoryManifestTests(unittest.TestCase):
    def test_all_seven_workflows_have_real_commit_pins_and_zero_error_baselines(self):
        repositories = RUNNER.entries()
        self.assertEqual({"platformio", "arduino", "stm32", "esp-idf", "pico", "zephyr", "rust"}, {r["kind"] for r in repositories})
        self.assertTrue(all(r["maxErrors"] == 0 for r in repositories))

    def manifest(self, directory, entry):
        path = Path(directory) / "manifest.json"
        path.write_text(json.dumps({"embeddedRepositories": [entry]}), encoding="utf-8")
        return path

    def test_branch_names_are_not_accepted_as_source_pins(self):
        with tempfile.TemporaryDirectory() as directory:
            entry = dict(RUNNER.entries()[0], sha="main")
            with self.assertRaises(ValueError):
                RUNNER.entries(self.manifest(directory, entry))

    def test_project_paths_cannot_escape_the_checkout(self):
        with tempfile.TemporaryDirectory() as directory:
            entry = dict(RUNNER.entries()[0], project="../outside")
            with self.assertRaises(ValueError):
                RUNNER.entries(self.manifest(directory, entry))

    def test_error_baselines_cannot_be_raised_to_hide_a_build_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            entry = dict(RUNNER.entries()[0], maxErrors=1)
            with self.assertRaises(ValueError):
                RUNNER.entries(self.manifest(directory, entry))

    def test_failure_always_writes_an_honest_summary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runner = RUNNER.Runner(RUNNER.entries()[0], root / "work", root / "out")
            with patch.object(runner, "build", side_effect=RuntimeError("compiler missing")):
                self.assertFalse(runner.execute())
            summary = json.loads((runner.output / "summary.json").read_text())
            self.assertFalse(summary["passed"])
            self.assertFalse(summary["hardwareVerified"])
            self.assertFalse(summary["androidVerified"])
            self.assertEqual("compiler missing", summary["error"])

    def test_success_requires_a_nonempty_firmware_artifact(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runner = RUNNER.Runner(RUNNER.entries()[0], root / "work", root / "out")
            missing = root / "firmware.elf"
            with self.assertRaises(RuntimeError):
                runner.require(missing)
            missing.write_bytes(b"")
            with self.assertRaises(RuntimeError):
                runner.require(missing)
            missing.write_bytes(b"\x7fELFfirmware-fixture")
            runner.require(missing)
            self.assertEqual(missing.read_bytes(), (runner.output / "firmware.elf").read_bytes())
            self.assertEqual(64, len(runner.steps[-1]["sha256"]))

    def test_platformio_invocation_cannot_import_the_repositories_platform_py_as_stdlib(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository = root / "source"
            project = repository / "examples/arduino-blink"
            project.mkdir(parents=True)
            (repository / "platform.py").write_text("raise RuntimeError('must not be imported as stdlib')\n")
            (project / "platformio.ini").write_text("[env:uno]\nplatform = atmelavr\nboard = uno\nframework = arduino\n")
            runner = RUNNER.Runner(RUNNER.entries()[0], root / "work", root / "out")
            with patch.object(runner, "clone", return_value=repository), patch.object(runner, "run") as command, patch.object(runner, "require"):
                runner.build()
            argv = command.call_args.args[0]
            self.assertEqual([RUNNER.sys.executable, "-P", "-m", "platformio"], argv[:4])
            self.assertEqual(project, command.call_args.args[1])


if __name__ == "__main__":
    unittest.main()
