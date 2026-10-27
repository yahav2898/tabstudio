"""Managed launcher contracts. No target, erase operation or physical USB peer is used."""
from contextlib import suppress
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "native/embedded/probe_rs_runtime.py"
MARKER = "TABSTUDIO_PROBE_RS_NO_PRESETS"
FAKE = """#!/usr/bin/env python3
import json, os, pathlib, sys, time
home = pathlib.Path(os.environ['HOME'])
result = {'arguments': sys.argv[1:], 'home': str(home), 'cwd': os.getcwd(), 'pid': os.getpid(),
          'marker': os.environ.get('TABSTUDIO_PROBE_RS_NO_PRESETS'), 'inheritedPreset': os.environ.get('PROBE_RS_CONFIG_PRESET'),
          'remoteHost': os.environ.get('PROBE_RS_REMOTE_HOST'), 'remoteToken': os.environ.get('PROBE_RS_REMOTE_TOKEN')}
if (home / '.probe-rs.json').exists(): result['config'] = json.loads((home / '.probe-rs.json').read_text())
if os.environ.get('TABSTUDIO_USB_FD') == '3': result['fd3'] = os.read(3, 4).decode('ascii')
if os.environ.get('TABSTUDIO_TEST_READY'):
    pathlib.Path(os.environ['TABSTUDIO_TEST_READY']).write_text(json.dumps(result))
    time.sleep(60)
else:
    print(json.dumps(result))
raise SystemExit(int(os.environ.get('TABSTUDIO_TEST_EXIT', '0')))
"""


class ProbePresetTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="tabstudio-probe-preset-test-")
        self.root = Path(self.temporary.name)
        self.project = self.root / "project with spaces"
        self.project.mkdir()
        self.home = self.root / "original-home"
        self.home.mkdir()
        self.executable = self.root / "fake-probe"
        self.executable.write_text(FAKE)
        self.executable.chmod(0o755)
        self.environment = {**os.environ, "HOME": str(self.home), MARKER: "1", "PROBE_RS_CONFIG_PRESET": "unsafe",
                            "PROBE_RS_REMOTE_HOST": "remote-must-not-be-used", "PROBE_RS_REMOTE_TOKEN": "must-not-be-logged"}

    def tearDown(self):
        self.temporary.cleanup()

    def run_command(self, arguments, environment=None, **extra):
        return subprocess.run([sys.executable, str(SOURCE), str(self.executable), *arguments],
                              cwd=self.project, env=environment or self.environment,
                              capture_output=True, text=True, timeout=10, **extra)

    def test_each_typed_command_gets_a_distinct_empty_nonce_preset_without_changing_project_or_parent_home(self):
        presets = set()
        for command in ("read", "info", "download", "run", "attach"):
            result = self.run_command([command, "firmware with spaces.elf"])
            self.assertEqual(0, result.returncode, result.stderr)
            data = json.loads(result.stdout)
            self.assertEqual("--preset", data['arguments'][0])
            preset = data['arguments'][1]
            self.assertRegex(preset, r"^tabstudio-[0-9a-f]{32}$")
            presets.add(preset)
            self.assertEqual([command, "firmware with spaces.elf"], data['arguments'][2:])
            self.assertEqual({'presets': {preset: {}}}, data['config'])
            self.assertEqual(str(self.project), data['cwd'])
            self.assertNotEqual(str(self.home), data['home'])
            self.assertFalse(Path(data['home']).exists())
            self.assertIsNone(data['marker'])
            self.assertIsNone(data['inheritedPreset'])
            self.assertIsNone(data['remoteHost'])
            self.assertIsNone(data['remoteToken'])
        self.assertEqual(5, len(presets))
        self.assertFalse((self.home / '.probe-rs.json').exists())
        self.assertFalse((self.project / '.probe-rs.json').exists())

    def test_normal_terminal_version_and_dap_calls_keep_upstream_arguments_home_and_config(self):
        environment = {key: value for key, value in self.environment.items() if key != MARKER}
        for arguments in (['--version'], ['dap-server', '--ip', '127.0.0.1'], ['read', 'b8', '0', '1']):
            result = self.run_command(arguments, environment)
            self.assertEqual(0, result.returncode, result.stderr)
            data = json.loads(result.stdout)
            self.assertEqual(arguments, data['arguments'])
            self.assertEqual(str(self.home), data['home'])
            self.assertEqual('unsafe', data['inheritedPreset'])

    def test_marked_unknown_operations_and_explicit_presets_are_refused_before_start(self):
        for arguments in (['erase'], ['write', 'b32', '0', '1'], ['read', '--preset', 'unsafe'], ['info', '--preset=unsafe']):
            result = self.run_command(arguments)
            self.assertEqual(78, result.returncode)
            self.assertNotIn('"pid"', result.stdout)
            self.assertNotIn('must-not-be-logged', result.stderr)

    def test_real_child_receives_only_explicit_usb_fd3_and_nonzero_exit_propagates(self):
        saved = None
        with suppress(OSError):
            saved = os.dup(3)
        read, write = os.pipe()
        try:
            os.write(write, b'USB!')
            os.close(write)
            write = -1
            os.dup2(read, 3, inheritable=True)
            result = self.run_command(['read'], {**self.environment, 'TABSTUDIO_USB_FD': '3', 'TABSTUDIO_TEST_EXIT': '23'}, pass_fds=(3,))
            self.assertEqual(23, result.returncode, result.stderr)
            data = json.loads(result.stdout)
            self.assertEqual('USB!', data['fd3'])
            self.assertFalse(Path(data['home']).exists())
        finally:
            for descriptor in {read, write, 3}:
                if descriptor >= 0:
                    with suppress(OSError):
                        os.close(descriptor)
            if saved is not None:
                os.dup2(saved, 3)
                os.close(saved)

    def test_termination_is_forwarded_child_is_reaped_and_private_home_is_cleaned(self):
        ready = self.root / 'ready.json'
        process = subprocess.Popen([sys.executable, str(SOURCE), str(self.executable), 'read'],
                                   cwd=self.project, env={**self.environment, 'TABSTUDIO_TEST_READY': str(ready)},
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 5
            while not ready.exists() and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertTrue(ready.exists(), 'The actual child did not start')
            data = json.loads(ready.read_text())
            process.send_signal(signal.SIGTERM)
            output, error = process.communicate(timeout=5)
            self.assertEqual(143, process.returncode, output + error)
            self.assertFalse(Path(data['home']).exists())
            with self.assertRaises(ProcessLookupError):
                os.kill(data['pid'], 0)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()

    @unittest.skipUnless(os.environ.get('TABSTUDIO_PROBE_RS_REAL'), 'Actual official probe-rs binary is exercised separately by native installer CI')
    def test_unchanged_official_cli_ignores_hostile_automatic_preset_in_project_and_original_home(self):
        binary = Path(os.environ['TABSTUDIO_PROBE_RS_REAL']).resolve(strict=True)
        digest = hashlib.sha256(binary.read_bytes()).hexdigest()
        sentinel = 'TABSTUDIO_PRESET_MUST_NOT_BE_USED.yaml'
        configuration = {'presets': {'default': {'chip-description-path': str(self.project / sentinel),
                                               'allow-erase-all': True, 'cycle-power': True}}}
        for folder in (self.project, self.home):
            (folder / '.probe-rs.json').write_text(json.dumps(configuration))
        environment = {key: value for key, value in self.environment.items() if key not in (MARKER, 'PROBE_RS_CONFIG_PRESET', 'PROBE_RS_REMOTE_HOST', 'PROBE_RS_REMOTE_TOKEN')}
        arguments = ['read', 'b8', '0x08000000', '1', '--chip', 'STM32F401CCUx', '--probe', '0000:0000', '--non-interactive']
        unsafe = subprocess.run([str(binary), *arguments], cwd=self.project, env=environment, text=True, capture_output=True, timeout=20)
        self.assertNotEqual(0, unsafe.returncode)
        self.assertIn(sentinel, unsafe.stdout + unsafe.stderr)
        safe = subprocess.run([sys.executable, str(SOURCE), str(binary), *arguments], cwd=self.project,
                              env={**environment, MARKER: '1'}, text=True, capture_output=True, timeout=20)
        self.assertNotEqual(0, safe.returncode)  # The impossible 0000:0000 probe is not a connected target.
        self.assertNotIn(sentinel, safe.stdout + safe.stderr)
        self.assertIn('probe', (safe.stdout + safe.stderr).lower())
        self.assertNotIn('Config preset', safe.stdout + safe.stderr)
        self.assertEqual(digest, hashlib.sha256(binary.read_bytes()).hexdigest())


if __name__ == '__main__':
    unittest.main()
