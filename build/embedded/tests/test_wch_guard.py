"""Compile and execute the production C parser; no hardware transcripts fabricated."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]


class WchGuardTests(unittest.TestCase):
    def test_real_native_guard_parser_refuses_mutations_and_bounds_every_phase(self):
        with tempfile.TemporaryDirectory(prefix="tabstudio-wch-guard-") as folder:
            target = Path(folder) / "guard-test"
            subprocess.run(["cc", "-std=c17", "-O2", "-Wall", "-Wextra", "-Werror", "-I", str(ROOT / "native/embedded"),
                            str(ROOT / "native/embedded/wch_guard.c"), str(ROOT / "build/embedded/tests/wch_guard_test.c"),
                            "-o", str(target)], check=True)
            result = subprocess.run([str(target)], check=True, text=True, capture_output=True, timeout=10)
            self.assertIn("WCH_PROTOCOL_GUARD_PASSED", result.stdout)

    def test_actual_broker_owns_buffers_drains_cancellations_and_rejects_foreign_pointers(self):
        # These minimal PRoot ABI doubles are test-only. The separate stock
        # wlink fixture exercises the real PRoot types, ptrace and nusb path.
        with tempfile.TemporaryDirectory(prefix="tabstudio-wch-broker-") as folder:
            root = Path(folder)
            (root / "tracee").mkdir()
            (root / "syscall").mkdir()
            (root / "tracee/tracee.h").write_text("""#pragma once
#include <stdint.h>
#include <sys/types.h>
typedef uintptr_t word_t;
typedef struct { pid_t pid, tgid; } Tracee;
typedef enum { CURRENT } RegVersion;
""")
            (root / "tracee/mem.h").write_text("""#pragma once
#include "tracee/tracee.h"
int read_data(const Tracee *,void *,word_t,word_t);
int write_data(Tracee *,word_t,const void *,word_t);
""")
            (root / "tracee/reg.h").write_text("""#pragma once
#include "tracee/tracee.h"
typedef enum { SYSARG_1=1, SYSARG_2, SYSARG_3 } Reg;
word_t peek_reg(const Tracee *,RegVersion,Reg);
""")
            (root / "syscall/sysnum.h").write_text("#pragma once\n")
            target = root / "broker-test"
            subprocess.run(["cc", "-std=c17", "-O2", "-Wall", "-Wextra", "-Werror", "-I", str(root),
                            str(ROOT / "build/embedded/tests/wch_usbfs_test.c"), str(ROOT / "native/embedded/wch_guard.c"),
                            "-o", str(target)], check=True)
            result = subprocess.run([str(target)], text=True, capture_output=True, timeout=10)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn("WCH_NATIVE_BROKER_OWNED_BUFFER_CANCEL_DRAIN_PASSED", result.stdout)


if __name__ == "__main__":
    unittest.main()
