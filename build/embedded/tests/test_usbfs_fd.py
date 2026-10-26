"""Synthetic file-descriptor contracts, not physical USB captures or flashing proofs."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]

CLIENT = r'''
#define _GNU_SOURCE
#include <assert.h>
#include <errno.h>
#include <fcntl.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

int main(int argc, char **argv) {
    assert(argc == 3);
    const char *selected = "/dev/bus/usb/001/002";
    int handles[4] = {
        open(selected, O_RDWR | O_CLOEXEC),
        open64(selected, O_RDWR | O_CLOEXEC),
        openat(AT_FDCWD, selected, O_RDWR | O_CLOEXEC),
        openat64(AT_FDCWD, selected, O_RDWR | O_CLOEXEC)
    };
    for (int i = 0; i < 4; i++) {
        assert(handles[i] >= 3);
        assert(fcntl(handles[i], F_GETFD) & FD_CLOEXEC);
        char value[11] = {0};
        assert(pread(handles[i], value, 10, 0) == 10);
        assert(!strcmp(value, "permission"));
        close(handles[i]);
    }
    errno = 0;
    assert(open("/dev/bus/usb/001/003", O_RDWR) == -1 && errno == EACCES);
    errno = 0;
    assert(open(selected, O_RDWR | O_TRUNC) == -1 && errno == EINVAL);
    errno = 0;
    assert(openat(AT_FDCWD, selected, O_RDWR | O_CREAT, 0600) == -1 && errno == EINVAL);
    int ordinary = open(argv[1], O_RDONLY);
    assert(ordinary >= 0);
    close(ordinary);
    int written = open(argv[2], O_CREAT | O_RDWR | O_TRUNC, 0600);
    assert(written >= 0 && write(written, "ordinary", 8) == 8);
    close(written);
    return 0;
}
'''


@unittest.skipUnless(shutil.which("cc"), "A C compiler is required for native bridge contracts")
class UsbfsFdContracts(unittest.TestCase):
    def test_exact_permissioned_path_duplication_and_unrelated_file_operations(self):
        with tempfile.TemporaryDirectory() as folder:
            work = Path(folder)
            source = work / "client.c"
            source.write_text(CLIENT)
            shim = work / "usbfs-fd.so"
            client = work / "client"
            subprocess.run(["cc", "-std=gnu17", "-Wall", "-Wextra", "-Werror", "-O2", "-shared", "-fPIC",
                            str(ROOT / "native/embedded/usbfs_fd.c"), "-ldl", "-pthread", "-o", str(shim)], check=True)
            subprocess.run(["cc", "-std=gnu17", "-Wall", "-Wextra", "-Werror", "-O2", str(source), "-o", str(client)], check=True)
            device = work / "granted-fixture"
            device.write_bytes(b"permission")
            fd = os.open(device, os.O_RDWR)
            try:
                environment = {**os.environ, "LD_PRELOAD": str(shim), "TABSTUDIO_USB_FD": str(fd),
                               "TABSTUDIO_USB_DEVPATH": "/dev/bus/usb/001/002"}
                subprocess.run([str(client), str(source), str(work / "ordinary-output")], env=environment,
                               pass_fds=(fd,), check=True, timeout=10)
                self.assertEqual((work / "ordinary-output").read_bytes(), b"ordinary")
            finally:
                os.close(fd)


if __name__ == "__main__":
    unittest.main()
