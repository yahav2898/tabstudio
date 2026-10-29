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
            self.assertEqual("no", command.call_args.args[2]["PLATFORMIO_SETTING_ENABLE_TELEMETRY"])
            self.assertNotIn("PYTHONSAFEPATH", command.call_args.args[2])

    def test_cloned_repositories_register_authoritative_origin_before_submodules(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entry = RUNNER.entries()[0]
            runner = RUNNER.Runner(entry, root / "work", root / "out")
            with patch.object(runner, "run") as command, patch.object(RUNNER.subprocess, "check_output", return_value=entry["sha"] + "\n"):
                runner.clone(entry, "source", submodules=True)
            args = [call.args[0] for call in command.call_args_list]
            self.assertEqual(["git", "remote", "add", "origin", entry["url"]], args[1])
            self.assertEqual("origin", args[2][-2])
            self.assertEqual("submodule", args[-1][3])

    def test_zephyr_cross_prefix_is_absolute_even_when_compiler_was_found_on_path(self):
        with patch.object(RUNNER.shutil, "which", return_value="/private SDK/bin/arm-none-eabi-gcc"):
            self.assertEqual("/private SDK/bin/arm-none-eabi-", RUNNER.arm_cross_prefix({"PATH": "/private SDK/bin"}))
        with patch.object(RUNNER.shutil, "which", return_value=None):
            with self.assertRaisesRegex(RuntimeError, "Arm GNU"):
                RUNNER.arm_cross_prefix({})

    def test_stm32_preserves_the_vendor_preset_but_never_uses_a_checked_in_build_cache(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            entry = next(e for e in RUNNER.entries() if e["kind"] == "stm32")
            runner = RUNNER.Runner(entry, root / "work", root / "out")
            repository = root / "source"
            repository.mkdir()
            with patch.object(runner, "clone", return_value=repository), patch.object(runner, "run") as command, patch.object(runner, "require"):
                runner.build()
            configure = command.call_args_list[0].args[0]
            build = command.call_args_list[1].args[0]
            private_build = str(runner.work / "build")
            self.assertEqual(["cmake", "--preset", "Debug", "-B", private_build], configure[:5])
            self.assertEqual(["cmake", "--build", private_build], build[:3])

    def test_vendor_sdk_scripts_keep_their_required_sibling_module_imports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository = root / "source"
            repository.mkdir()
            entry = next(e for e in RUNNER.entries() if e["kind"] == "esp-idf")
            # The example's CMakeLists.txt names the firmware.
            (repository / entry["project"]).mkdir(parents=True)
            (repository / entry["project"] / "CMakeLists.txt").write_text("project(hello_world)\n", encoding="utf-8")
            runner = RUNNER.Runner(entry, root / "work", root / "out")
            with patch.object(runner, "clone", return_value=repository), patch.object(runner, "run") as command, patch.object(runner, "require") as require, patch.dict(RUNNER.os.environ, {"PYTHONSAFEPATH": "1"}):
                runner.build()
            self.assertEqual(2, command.call_count)
            for call in command.call_args_list:
                self.assertNotIn("PYTHONSAFEPATH", call.args[2])
            self.assertEqual("hello_world.elf", require.call_args.args[0].name)

    def test_every_child_receives_import_compatible_environment_without_telemetry(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runner = RUNNER.Runner(RUNNER.entries()[0], root / "work", root / "out")
            with patch.dict(RUNNER.os.environ, {"PYTHONSAFEPATH": "1", "PLATFORMIO_SETTING_ENABLE_TELEMETRY": "yes"}):
                runner.run([RUNNER.sys.executable, "-c", "import os; assert 'PYTHONSAFEPATH' not in os.environ; assert os.environ['PLATFORMIO_SETTING_ENABLE_TELEMETRY'] == 'no'"], root)
            self.assertEqual(0, runner.steps[-1]["exitCode"])


if __name__ == "__main__":
    unittest.main()


class FirmwareNameTests(unittest.TestCase):
    """The firmware a build must produce is named after the project, never after one known example."""

    def test_esp_idf_firmware_is_named_after_the_cmake_project(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            for text, name in [
                ("cmake_minimum_required(VERSION 3.16)\ninclude($ENV{IDF_PATH}/tools/cmake/project.cmake)\nproject(blink)\n", "blink"),
                ("project(hello_world)\n", "hello_world"),
                ("  PROJECT( led_strip_demo LANGUAGES C)\n", "led_strip_demo"),
            ]:
                (folder / "CMakeLists.txt").write_text(text, encoding="utf-8")
                self.assertEqual(name, RUNNER.cmake_project_name(folder))

    def test_a_cmake_file_without_a_project_is_an_error(self):
        with tempfile.TemporaryDirectory() as directory:
            (Path(directory) / "CMakeLists.txt").write_text("idf_component_register(SRCS main.c)\n", encoding="utf-8")
            with self.assertRaises(RuntimeError):
                RUNNER.cmake_project_name(Path(directory))
