#!/usr/bin/env python3
"""Run the unchanged official legacy Nordic CLI through the production serial bridge.

The DFU peer is an explicitly synthetic Nordic Secure DFU SLIP/object/CRC model.
This proves the real executable/PTY/control ABI, not a physical USB board or its trust policy.
"""
import argparse
import binascii
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import struct
import subprocess
import sys
import tempfile
import zipfile


def load_bridge(path):
    spec = importlib.util.spec_from_file_location("tabstudio_production_serial", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", required=True, type=Path)
    parser.add_argument("--shim", required=True, type=Path, help="Shim matching the unchanged x64 executable")
    parser.add_argument("--bridge", type=Path)
    routes = parser.add_mutually_exclusive_group()
    routes.add_argument("--box64-runtime", type=Path)
    routes.add_argument("--launcher", type=Path, help="Installed product launcher; qualifies its selected native/Box64 route")
    routes.add_argument("--translator-test-only", type=Path, help="Portable interpreter on x64; never qualifies ARM64 or the product launcher")
    parser.add_argument("--native-shim", type=Path, help="Host-ABI serial shim retained by the product Python parent")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not args.binary.is_file() or not args.shim.is_file():
        raise ValueError("The actual official executable and ABI-matching compiled shim are required")
    if args.launcher and not args.launcher.is_file():
        raise ValueError("The installed product launcher must exist")
    if args.native_shim and not args.native_shim.is_file():
        raise ValueError("The native parent shim must exist")
    if args.launcher and platform.machine() == "aarch64" and not args.native_shim:
        raise ValueError("Qualifying the ARM64 product parent requires its actual native serial shim")
    if args.translator_test_only and (not args.translator_test_only.is_file() or platform.machine() != "x86_64"):
        raise ValueError("The portable interpreter test is exclusively an actual x64 executable on an x64 host")
    bridge_path = args.bridge or Path(__file__).resolve().parents[2] / "native/embedded/serial_bridge.py"
    module = load_bridge(bridge_path)
    args.output.mkdir(parents=True, exist_ok=True)
    firmware = bytes(range(256)) * 24  # Includes all SLIP escape values and crosses object boundaries.

    class SecureDfuPeer(module.VirtualSerial):
        def __init__(self, corrupt_crc=False):
            super().__init__()
            self.frame = bytearray()
            self.escaped = False
            self.current = 1
            self.objects = {1: bytearray(), 2: bytearray()}
            self.limit = None
            self.create_sizes = []
            self.executions = []
            self.ops = []
            self.corrupt_crc = corrupt_crc

        def respond(self, operation, payload=b""):
            packet = bytes([0x60, operation, 1]) + payload
            encoded = packet.replace(b"\xdb", b"\xdb\xdd").replace(b"\xc0", b"\xdb\xdc") + b"\xc0"
            self.data.put(encoded)

        def packet(self, packet):
            operation, payload = packet[0], packet[1:]
            self.ops.append(operation)
            if operation == 9:
                self.respond(operation, payload)
            elif operation == 2:
                assert payload == b"\0\0", "Fixture disables unsolicited PRN replies explicitly"
                self.respond(operation)
            elif operation == 7:
                self.respond(operation, struct.pack("<H", 512))
            elif operation == 6:
                assert len(payload) == 1 and payload[0] in (1, 2)
                self.current = payload[0]
                contents = self.objects[self.current]
                self.respond(operation, struct.pack("<III", 4096, len(contents), binascii.crc32(contents) & 0xffffffff))
            elif operation == 1:
                self.current, size = struct.unpack("<BI", payload)
                assert self.current in (1, 2) and 0 < size <= 4096
                self.limit = len(self.objects[self.current]) + size
                self.create_sizes.append((self.current, size))
                self.respond(operation)
            elif operation == 8:
                contents = self.objects[self.current]
                assert self.limit is not None and len(contents) + len(payload) <= self.limit
                contents.extend(payload)
            elif operation == 3:
                contents = self.objects[self.current]
                crc = binascii.crc32(contents) & 0xffffffff
                self.respond(operation, struct.pack("<II", len(contents), crc ^ int(self.corrupt_crc)))
            elif operation == 4:
                assert self.limit == len(self.objects[self.current])
                self.executions.append((self.current, self.limit))
                self.respond(operation)
            else:
                raise OSError("Unexpected synthetic Secure DFU opcode: " + str(operation))

        def write(self, payload):
            try:
                for value in payload:
                    if value == 0xc0:
                        if self.frame:
                            self.packet(bytes(self.frame))
                            self.frame.clear()
                        if self.escaped:
                            raise ValueError("Truncated SLIP escape")
                    elif self.escaped:
                        if value not in (0xdc, 0xdd):
                            raise ValueError("Invalid SLIP escape")
                        self.frame.append(0xc0 if value == 0xdc else 0xdb)
                        self.escaped = False
                    elif value == 0xdb:
                        self.escaped = True
                    else:
                        self.frame.append(value)
                    if len(self.frame) > 4096:
                        raise ValueError("Unbounded SLIP frame")
            except (AssertionError, ValueError, struct.error) as error:
                raise OSError("Synthetic DFU contract failed: " + str(error)) from error
            return len(payload)

    def invocation(arguments):
        if args.launcher:
            return [str(args.launcher), *arguments], dict(os.environ)
        if args.translator_test_only:
            environment = dict(os.environ)
            environment.pop("LD_PRELOAD", None)
            environment.update(BOX64_LD_PRELOAD=str(args.shim), BOX64_DYNAREC="0",
                               BOX64_LOG="0", BOX64_NOBANNER="1")
            return [str(args.translator_test_only), str(args.binary), *arguments], environment
        if args.box64_runtime:
            sys.path.insert(0, str(args.box64_runtime.parent))
            spec = importlib.util.spec_from_file_location("nordic_box64_runtime", args.box64_runtime)
            runtime = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(runtime)
            command, environment = runtime.command(str(args.binary), arguments, serial_shim=str(args.shim))
            return command, environment
        if platform.machine() != "x86_64":
            raise ValueError("Native x64 execution cannot qualify ARM64; select the real Box64 runtime explicitly")
        return [str(args.binary), *arguments], dict(os.environ)

    def execute(arguments, name, environment=None, failure_expected=False):
        command, baseline = invocation(arguments)
        if environment:
            baseline.update(environment)
        result = subprocess.run(command, env=baseline, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=90)
        (args.output / name).write_text(result.stdout)
        print(result.stdout, end="", flush=True)
        if failure_expected:
            if not result.returncode or "Failed CRC validation" not in result.stdout or "Device programmed." in result.stdout:
                raise RuntimeError("The actual Nordic CLI did not explicitly refuse the corrupt target CRC")
        elif result.returncode:
            raise RuntimeError("Actual Nordic CLI failed: " + name)
        return result.stdout

    with tempfile.TemporaryDirectory(prefix="tabstudio-nordic-proof-") as scratch:
        scratch = Path(scratch)
        binary_file, package = scratch / "application.bin", scratch / "application.zip"
        binary_file.write_bytes(firmware)
        execute(["version"], "version.log")
        for command in ("serial", "usb-serial"):
            execute(["dfu", command, "--help"], command + "-help.log")
        execute(["pkg", "generate", "--application", str(binary_file), "--application-version", "1",
                 "--hw-version", "52", "--sd-req", "0", str(package)], "package.log")
        with zipfile.ZipFile(package) as archive:
            manifest = json.loads(archive.read("manifest.json"))["manifest"]["application"]
            expected_command = archive.read(manifest["dat_file"])
            expected_firmware = archive.read(manifest["bin_file"])
        for transport, corrupt_crc in (("serial", False), ("usb-serial", False), ("serial", True)):
            peer = SecureDfuPeer(corrupt_crc)
            label = transport + ("-bad-crc" if corrupt_crc else "")
            audit = args.output / (label + "-controls.jsonl")
            bridge = module.SerialBridge(peer, str(audit))
            bridge.start()
            environment = {"TABSTUDIO_SERIAL_DEVICE": bridge.slave_name,
                           "TABSTUDIO_SERIAL_CONTROL_DIR": bridge.directory.name}
            if args.launcher:
                environment["LD_PRELOAD"] = str(args.native_shim or args.shim)
            elif not (args.box64_runtime or args.translator_test_only):
                environment["LD_PRELOAD"] = str(args.shim)
            try:
                output = execute(["dfu", transport, "--package", str(package), "--port", bridge.slave_name,
                                  "--baud-rate", "230400", "--flow-control", "0", "--packet-receipt-notification", "0",
                                  "--timeout", "1", "--connect-delay", "0"], label + "-dfu.log", environment, failure_expected=corrupt_crc)
                assert bridge.error is None, bridge.error
                if corrupt_crc:
                    assert 3 in peer.ops
                    continue
                assert "Device programmed." in output
                assert bytes(peer.objects[1]) == expected_command
                assert bytes(peer.objects[2]) == expected_firmware == firmware
                assert len([item for item in peer.create_sizes if item[0] == 2]) >= 2
                events = [json.loads(line) for line in audit.read_text().splitlines()]
                assert any(event["operation"] == module.CONFIG and event["values"][0] == 230400 for event in events)
                assert any(event["operation"] == module.LINES and event["values"][1] & 2 for event in events), "Actual DTR ioctl was not bridged"
                assert any(event["operation"] == module.LINES and event["values"][1] & 4 for event in events), "Actual RTS ioctl was not bridged"
            finally:
                bridge.close()
    evidence = {"hardwareVerified": False, "hostArchitecture": platform.machine(),
                "translated": platform.machine() == "aarch64" and bool(args.launcher or args.box64_runtime),
                "productLauncher": bool(args.launcher),
                "portableInterpreter": bool(args.translator_test_only),
                "armExecution": platform.machine() == "aarch64",
                "binarySha256": hashlib.sha256(args.binary.read_bytes()).hexdigest(),
                "firmwareSha256": hashlib.sha256(firmware).hexdigest(), "firmwareBytes": len(firmware),
                "transports": ["serial", "usb-serial"], "baud": 230400, "dtrRtsForwarded": True,
                "corruptCrcRefused": True}
    (args.output / "summary.json").write_text(json.dumps(evidence, indent=2))
    print("NORDIC_SERIAL_BRIDGE_PASSED " + json.dumps(evidence), flush=True)


if __name__ == "__main__":
    main()
