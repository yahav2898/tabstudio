#!/usr/bin/env python3
"""Small stdio DAP adapter for gpsim 0.32.1's actual PIC18 models.

gpsim loads HEX; pyelftools reads XC8's ELF/DWARF independently for source
breakpoints. Registers and SFRs come from gpsim, never from guessed addresses.
No on-chip PICkit protocol is claimed. That uses MPLAB through the Bridge.
"""
import bisect
import json
import os
from pathlib import Path
import re
import select
import signal
import subprocess
import sys
import threading
import time

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS / "python"))


def read_message(stream):
    length = None
    while True:
        line = stream.readline(8193)
        if not line:
            return None
        if len(line) > 8192:
            raise ValueError("DAP header is too long")
        if line in (b"\r\n", b"\n"):
            break
        name, separator, value = line.partition(b":")
        if not separator:
            raise ValueError("Invalid DAP header")
        if name.lower() == b"content-length":
            if length is not None:
                raise ValueError("Duplicate DAP Content-Length")
            length = int(value)
    if length is None or not 0 < length <= 4_000_000:
        raise ValueError("Invalid DAP Content-Length")
    data = stream.read(length)
    if len(data) != length:
        raise EOFError("Truncated DAP message")
    return json.loads(data)


class SourceMap:
    def __init__(self, elf=None, root=None):
        self.lines = []
        self.symbols = []
        self.addresses = []
        if elf is None:
            return
        from elftools.elf.elffile import ELFFile
        root = Path(root or Path(elf).parent)
        with open(elf, "rb") as stream:
            image = ELFFile(stream)
            symbols = image.get_section_by_name(".symtab")
            if symbols:
                self.symbols = sorted((s["st_value"], s.name) for s in symbols.iter_symbols() if s["st_info"]["type"] == "STT_FUNC")
            if not image.has_dwarf_info():
                return
            dwarf = image.get_dwarf_info()
            for unit in dwarf.iter_CUs():
                program = dwarf.line_program_for_CU(unit)
                if program is None:
                    continue
                for entry in program.get_entries():
                    state = entry.state
                    if state is None or state.end_sequence or not state.line:
                        continue
                    file = program.header["file_entry"][state.file - 1]
                    name = file.name.decode(errors="replace")
                    parent = program.header["include_directory"][file.dir_index - 1].decode(errors="replace") if file.dir_index else ""
                    path = Path(parent) / name
                    if not path.is_absolute(): path = root / path
                    if not path.is_file():
                        candidates = list(root.rglob(Path(name).name))
                        if len(candidates) == 1: path = candidates[0]
                    self.lines.append((state.address, str(path.resolve()), state.line))
        self.lines = sorted(set(self.lines))
        self.addresses = [line[0] for line in self.lines]

    def location(self, address):
        index = bisect.bisect_right(self.addresses, address) - 1
        return self.lines[index] if index >= 0 else None

    def breakpoint(self, file, line):
        file = str(Path(file).resolve())
        candidates = [item for item in self.lines if item[1] == file and item[2] >= line]
        return min(candidates, key=lambda item: (item[2], item[0])) if candidates else None

    def function(self, address):
        candidates = [item for item in self.symbols if item[0] <= address]
        return candidates[-1][1] if candidates else "PIC18 instruction"


class Gpsim:
    def __init__(self, executable, model, firmware):
        if any(c in str(firmware) for c in ('"', "\n", "\r")):
            raise ValueError("gpsim firmware paths cannot contain quotes or newlines")
        self.lock = threading.Lock()
        environment = {**os.environ, "LD_LIBRARY_PATH": str(Path(executable).resolve().parent.parent / "lib") + ":" + os.environ.get("LD_LIBRARY_PATH", "")}
        self.process = subprocess.Popen([str(executable), "-i"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=subprocess.STDOUT, start_new_session=True, env=environment)
        self.pending = bytearray()
        self.read_prompt(10)
        output = self.command(f'load {model} "{firmware}"')
        if re.search(r"(?i)(error|failed|not found|invalid)", output):
            self.close()
            raise ValueError("gpsim could not load firmware: " + output[-2000:])

    def read_prompt(self, timeout):
        deadline = time.monotonic() + timeout if timeout is not None else None
        prompt = b"**gpsim> "
        while prompt not in self.pending:
            if deadline is not None and time.monotonic() >= deadline:
                raise TimeoutError("gpsim did not return to its prompt")
            if select.select([self.process.stdout], [], [], 0.2)[0]:
                data = os.read(self.process.stdout.fileno(), 8192)
                if not data:
                    raise EOFError("gpsim exited: " + self.pending.decode(errors="replace")[-2000:])
                self.pending.extend(data)
                if len(self.pending) > 1_000_000:
                    del self.pending[:len(self.pending) - 100_000]
        index = self.pending.index(prompt)
        output = bytes(self.pending[:index]).decode(errors="replace")
        del self.pending[:index + len(prompt)]
        return output

    def command(self, command, timeout=10):
        with self.lock:
            # gpsim can emit a second prompt after run/reset/CTRL-C. A unique
            # echo response synchronizes the command stream instead of treating
            # a leftover prompt as the next register's response.
            marker = "TABSTUDIO_" + str(time.monotonic_ns())
            self.process.stdin.write((command + '\necho "' + marker + '"\n').encode())
            self.process.stdin.flush()
            ending = b'\n"' + marker.encode() + b'"\n'
            deadline = time.monotonic() + timeout if timeout is not None else None
            while True:
                position = self.pending.find(ending)
                prompt = self.pending.find(b"**gpsim> ", position + len(ending)) if position >= 0 else -1
                if prompt >= 0:
                    output = bytes(self.pending[:position]).decode(errors="replace")
                    del self.pending[:prompt + len(b"**gpsim> ")]
                    return re.sub(r'(?m)^.*echo "' + marker + r'"\s*$', '', output)
                if deadline is not None and time.monotonic() >= deadline: raise TimeoutError("gpsim command timed out")
                if select.select([self.process.stdout], [], [], 0.2)[0]:
                    data = os.read(self.process.stdout.fileno(), 8192)
                    if not data: raise EOFError("gpsim exited during " + command)
                    self.pending.extend(data)
                    if len(self.pending) > 1_000_000: del self.pending[:len(self.pending) - 100_000]

    def pause(self):
        if self.process.poll() is None:
            os.kill(self.process.pid, signal.SIGINT)

    def close(self):
        if self.process.poll() is not None:
            return
        try:
            self.process.stdin.write(b"quit\n")
            self.process.stdin.flush()
            self.process.wait(timeout=2)
        except (OSError, subprocess.TimeoutExpired):
            os.killpg(self.process.pid, signal.SIGKILL)
            self.process.wait()
        finally:
            self.process.stdin.close()
            self.process.stdout.close()


class Adapter:
    def __init__(self, output=None):
        self.output = output or sys.stdout.buffer
        self.write_lock = threading.Lock()
        self.seq = 0
        self.sim = None
        self.sources = SourceMap()
        self.breakpoints = {}
        self.instruction_breakpoints = []
        self.break_ids = []
        self.running = False
        self.worker = None
        self.pause_reason = None
        self.registers = []
        self.stop_on_entry = True

    def send(self, message):
        with self.write_lock:
            self.seq += 1
            message["seq"] = self.seq
            data = json.dumps(message, separators=(",", ":")).encode()
            self.output.write(f"Content-Length: {len(data)}\r\n\r\n".encode() + data)
            self.output.flush()

    def event(self, name, body=None):
        self.send({"type": "event", "event": name, "body": body or {}})

    def response(self, request, body=None, error=None):
        self.send({"type": "response", "request_seq": request["seq"], "command": request["command"], "success": error is None,
                   **({"message": str(error)} if error else {"body": body or {}})})

    def pc(self):
        text = self.sim.command("pc")
        match = re.search(r"\bpc\s*=\s*(0x[0-9a-fA-F]+)", text)
        if not match: raise ValueError("gpsim did not report the PC: " + text)
        return int(match[1], 16)

    def stopped(self, reason):
        self.running = False
        self.event("stopped", {"reason": reason, "threadId": 1, "allThreadsStopped": True})

    def continue_run(self, return_address=None, temporary_ids=()):
        if self.running: raise ValueError("gpsim is already running")
        self.running = True
        self.pause_reason = None
        def run():
            try:
                output = self.sim.command("run", timeout=None)
                for identity in temporary_ids: self.sim.command("clear " + str(identity))
                if "WDT" in output or "INVALID" in output:
                    self.event("output", {"category": "stderr", "output": output})
                    self.stopped(self.pause_reason or "exception")
                else:
                    self.stopped(self.pause_reason or ("step" if return_address is not None and self.pc() == return_address else "breakpoint"))
            except (OSError, ValueError, EOFError) as error:
                self.running = False
                self.event("output", {"category": "stderr", "output": str(error) + "\n"})
                self.event("terminated")
        self.worker = threading.Thread(target=run, daemon=True)
        self.worker.start()

    def install_breakpoints(self):
        if self.running: raise ValueError("Pause before changing breakpoints")
        for identity in self.break_ids: self.sim.command("clear " + str(identity))
        addresses = set(self.instruction_breakpoints)
        for items in self.breakpoints.values(): addresses.update(items)
        for address in sorted(addresses):
            output = self.sim.command("break e " + hex(address))
            if "ERROR" in output.upper(): raise ValueError("gpsim rejected breakpoint: " + output)
        self.break_ids = [int(value) for value in re.findall(r"(?m)^(\d+):", self.sim.command("break"))]

    def launch(self, args):
        device = args["device"].upper()
        supported = json.loads((TOOLS / "gpsim.devices.json").read_text())["devices"]
        if device not in supported:
            raise ValueError(f"gpsim 0.32.1 has no {device} model. Select a supported PIC18 device, or use MPLAB on-chip debugging through the Windows Bridge.")
        program = Path(args["program"]).resolve()
        firmware = Path(args.get("hex", program.with_suffix(".hex"))).resolve()
        if not firmware.is_file(): raise ValueError("Build the production HEX image before simulation")
        self.sources = SourceMap(program if program.suffix == ".elf" else None, args.get("cwd", program.parent))
        executable = args.get("gpsim", str(TOOLS / "native/gpsim/bin/gpsim"))
        self.sim = Gpsim(executable, "p" + device.removeprefix("PIC").lower(), firmware)
        entry = next((address for address, name in self.sources.symbols if name in ("main", "_main")), None)
        if entry is not None:
            self.sim.command("break e " + hex(entry))
            self.sim.command("run", timeout=30)
            for identity in re.findall(r"(?m)^(\d+):", self.sim.command("break")):
                self.sim.command("clear " + identity)
        symbols = self.sim.command("symbol")
        # Register subclasses have many names. Exclude pins, configuration words
        # and simulator attributes; values are queried from the actual model.
        excluded = ("IO_", "CPU_", "Config", "CONFIG", "Boolean", "Float", "String", "Attribute", "Collection", "WDT")
        self.registers = sorted(set(name for name, kind in re.findall(r"(?m)^p18\w+\.([A-Za-z_][A-Za-z_0-9]*)\s+Type:\s+(\w+)", symbols)
                                    if not any(part in kind for part in excluded)))
        self.stop_on_entry = args.get("stopOnEntry", True)

    def handle(self, request):
        command, args = request["command"], request.get("arguments") or {}
        if command == "initialize":
            self.response(request, {"supportsConfigurationDoneRequest": True, "supportsInstructionBreakpoints": True,
                                    "supportsEvaluateForHovers": True, "supportsSteppingGranularity": True})
        elif command == "launch":
            self.launch(args)
            self.response(request)
            self.event("initialized")
        elif command == "setExceptionBreakpoints": self.response(request, {"breakpoints": []})
        elif command == "setBreakpoints":
            path = args["source"]["path"]
            items, addresses = [], []
            for point in args.get("breakpoints", []):
                found = self.sources.breakpoint(path, point["line"])
                if found:
                    addresses.append(found[0]); items.append({"verified": True, "line": found[2], "instructionReference": hex(found[0])})
                else: items.append({"verified": False, "line": point["line"], "message": "No XC8 DWARF executable line at this location; rebuild the ELF image."})
            self.breakpoints[path] = addresses
            self.install_breakpoints()
            self.response(request, {"breakpoints": items})
        elif command == "setInstructionBreakpoints":
            addresses = [int(item["instructionReference"], 0) + item.get("offset", 0) for item in args.get("breakpoints", [])]
            if any(address < 0 or address > 0x1fffff or address % 2 for address in addresses): raise ValueError("PIC18 instruction addresses must be aligned byte addresses")
            self.instruction_breakpoints = addresses
            self.install_breakpoints()
            self.response(request, {"breakpoints": [{"verified": True, "instructionReference": hex(a)} for a in addresses]})
        elif command == "configurationDone":
            self.response(request)
            if self.stop_on_entry: self.stopped("entry")
            else: self.continue_run()
        elif command == "threads": self.response(request, {"threads": [{"id": 1, "name": "PIC18 simulator"}]})
        elif command == "continue":
            if self.running: raise ValueError("gpsim is already running")
            self.response(request, {"allThreadsContinued": True})
            self.continue_run()
        elif command == "pause":
            self.pause_reason = "pause"
            if self.running: self.sim.pause()
            self.response(request)
        elif command in ("next", "stepIn", "stepOut"):
            if self.running: raise ValueError("Pause before stepping")
            depth = int(self.value("stkptr"), 0) & 31
            if command == "stepOut":
                if depth == 0: raise ValueError("The PIC18 hardware return stack has no caller")
                address = sum(int(self.value(name), 0) << shift for name, shift in (("tosl", 0), ("tosh", 8), ("tosu", 16)))
                before = set(re.findall(r"(?m)^(\d+):", self.sim.command("break")))
                self.sim.command("break e " + hex(address))
                after = set(re.findall(r"(?m)^(\d+):", self.sim.command("break")))
                self.response(request)
                self.continue_run(address, after - before)
                return True
            original = self.sources.location(self.pc())
            for _ in range(2048 if original and args.get("granularity") != "instruction" else 1):
                self.sim.command("step")
                if self.sources.location(self.pc()) != original and (command != "next" or (int(self.value("stkptr"), 0) & 31) <= depth): break
            self.response(request)
            self.stopped("step")
        elif command == "stackTrace":
            pc = self.pc(); location = self.sources.location(pc)
            frame = {"id": 1, "name": self.sources.function(pc), "line": location[2] if location else 1, "column": 1, "instructionPointerReference": hex(pc)}
            if location: frame["source"] = {"name": Path(location[1]).name, "path": location[1]}
            self.response(request, {"stackFrames": [frame], "totalFrames": 1})
        elif command == "scopes": self.response(request, {"scopes": [{"name": "CPU registers", "variablesReference": 1, "expensive": False}, {"name": "SFRs", "variablesReference": 2, "expensive": True}]})
        elif command == "variables":
            names = ["pc", "W", "status", "bsr", "stkptr", "fsr0l", "fsr0h", "fsr1l", "fsr1h", "fsr2l", "fsr2h"] if args["variablesReference"] == 1 else self.registers
            start, count = max(0, args.get("start", 0)), args.get("count", len(names)) or len(names)
            variables = []
            for name in names[start:start + min(count, 256)]:
                variables.append({"name": name, "value": self.value(name), "type": "register", "variablesReference": 0, "evaluateName": name})
            self.response(request, {"variables": variables})
        elif command == "evaluate":
            expression = args["expression"].strip()
            expression = next((name for name in self.registers if name.lower() == expression.lower()), expression)
            if expression.lower() == "wreg": expression = "W"
            if expression.lower() == "pc": expression = "pc"
            if expression not in self.registers and expression != "pc" and not re.fullmatch(r"reg\(0x[0-9a-fA-F]{1,3}\)", expression):
                raise ValueError("Evaluate a register/SFR name or reg(0xADDR); arbitrary gpsim commands are not evaluated")
            self.response(request, {"result": self.value(expression), "type": "register", "variablesReference": 0})
        elif command in ("disconnect", "terminate"):
            self.close(); self.response(request); self.event("terminated"); return False
        else: raise ValueError("Unsupported gpsim DAP request: " + command)
        return True

    def value(self, name):
        if self.running: raise ValueError("Pause to inspect registers")
        output = self.sim.command(name)
        match = re.search(r"=\s*(0x[0-9a-fA-F]+|[0-9]+)", output)
        return match[1] if match else "unavailable"

    def close(self):
        if self.sim:
            if self.running:
                self.pause_reason = "pause"; self.sim.pause()
                if self.worker: self.worker.join(timeout=2)
            self.sim.close()
            self.sim = None


def main():
    adapter = Adapter()
    try:
        while request := read_message(sys.stdin.buffer):
            try:
                if not adapter.handle(request): break
            except (OSError, ValueError, KeyError, ImportError, EOFError, TypeError) as error:
                adapter.response(request, error=error)
    finally:
        adapter.close()


if __name__ == "__main__":
    main()
