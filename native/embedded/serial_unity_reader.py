#!/usr/bin/env python3
"""Read one existing Unity firmware run through the production POSIX serial PTY.

Never transmits, flashes, asserts modem outputs, sends BREAK or requests reset.
The standard UnityEnd summary AND newline-terminated OK/FAIL/FAILED are required.
This is transport framing; the managed Unity parser validates individual counts.
"""
import argparse
import array
import codecs
import fcntl
import os
import re
import select
import sys
import termios
import time

MAX_BYTES = 16 * 1024 * 1024
MAX_LINE = 64 * 1024
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
SUMMARY = re.compile(r"^(\d+) Tests (\d+) Failures (\d+) Ignored$")


def capture(port, baud, timeout, destination=None):
    """Retain complete strict UTF-8 lines and stop only at Unity's final trailer."""
    if not 1 <= baud <= 3500000 or not 1 <= timeout <= 3600:
        raise ValueError("Explicit baud and a timeout from 1 to 3600 seconds are required")
    destination = destination or sys.stdout
    descriptor = os.open(port, os.O_RDONLY | os.O_NOCTTY | os.O_NONBLOCK)
    try:
        # The bridge already selects the requested baud. This client never calls
        # TIOCMSET/TIOCMBIS/TIOCMBIC, flushes buffered input, or enables HUPCL.
        # Linux termios2 retains arbitrary firmware baud rates. Plain tcgetattr
        # can represent only named rates and would turn a custom rate into 38400.
        config = array.array("I", [0] * 11)
        fcntl.ioctl(descriptor, 0x802c542a, config, True)  # TCGETS2, 44-byte Linux ABI.
        if config[10] != baud:
            raise ValueError("The serial bridge did not retain the explicitly selected firmware baud")
        config[2] &= ~termios.HUPCL
        fcntl.ioctl(descriptor, 0x402c542b, config)  # TCSETS2, no input flush or drain.
        print("Serial Unity capture ready; start the board's harness now.", file=sys.stderr, flush=True)
        decoder = codecs.getincrementaldecoder("utf-8")("strict")
        partial = ""
        total = 0
        expected = None
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if not select.select([descriptor], [], [], min(0.1, max(0, deadline - time.monotonic())))[0]:
                continue
            chunk = os.read(descriptor, 4096)
            if not chunk:
                raise OSError("Serial disconnected before a complete Unity run")
            total += len(chunk)
            if total > MAX_BYTES:
                raise ValueError("Serial Unity output exceeds 16 MiB")
            partial += decoder.decode(chunk)
            while "\n" in partial:
                line, partial = partial.split("\n", 1)
                if len(line) > MAX_LINE:
                    raise ValueError("Serial Unity line exceeds 64 KiB")
                destination.write(line + "\n")
                destination.flush()
                clean = ANSI.sub("", line).rstrip()
                summary = SUMMARY.fullmatch(clean)
                if summary:
                    count, failures, ignored = map(int, summary.groups())
                    if count < 1 or count > 100000 or failures + ignored > count:
                        raise ValueError("Unity summary has invalid test counts")
                    expected = "OK" if failures == 0 else "FAIL"
                elif clean in ("OK", "FAIL", "FAILED") and expected:
                    if clean != expected and not (expected == "FAIL" and clean == "FAILED"):
                        raise ValueError("Unity final trailer contradicts its summary")
                    if partial:
                        # A second run or incomplete bytes in the same transfer
                        # cannot silently be dropped after a passing trailer.
                        destination.write(partial)
                        destination.flush()
                        raise ValueError("Unexpected data after Unity's final trailer")
                    decoder.decode(b"", final=True)
                    return
            if len(partial) > MAX_LINE:
                raise ValueError("Serial Unity line exceeds 64 KiB")
        raise TimeoutError("Timed out waiting for Unity's complete summary and final trailer")
    finally:
        os.close(descriptor)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", required=True)
    parser.add_argument("--baud", required=True, type=int)
    parser.add_argument("--timeout", required=True, type=int)
    args = parser.parse_args()
    try:
        capture(args.port, args.baud, args.timeout)
        return 0
    except (OSError, UnicodeError, ValueError) as error:
        print("Serial Unity capture failed: " + str(error), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
