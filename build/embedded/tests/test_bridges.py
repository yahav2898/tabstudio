"""Synthetic bridge/driver replay tests. No fixture here is a hardware capture."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
MICROCHIP = ROOT / "src/TabStudio.Toolchains/Microchip"
NATIVE = ROOT / "native/embedded"
sys.path.insert(0, str(MICROCHIP))
from usb_serial import UsbSerial


class FakeUsb:
    def __init__(self, vid, pid):
        self.device = {"vendorId": vid, "productId": pid, "interfaces": [{"class": 2, "subclass": 2, "number": 0}]}
        self.calls = []

    def endpoints(self, _kind, _interface):
        return {"number": 1, "class": 10}, {"packetSize": 64}, {}

    def claim(self, _number):
        pass

    def control(self, kind, request, value=0, index=0, data=b"", length=0):
        self.calls.append((kind, request, value & 0xffff, index, data, length))
        return b"\xff" * length if kind & 0x80 else len(data)


class Drivers(unittest.TestCase):
    def test_cdc_runtime_configure_reset_and_break(self):
        usb = FakeUsb(0x1234, 1)
        serial = UsbSerial(usb)
        usb.calls.clear()
        serial.configure(1200, 8, 2, 2)
        serial.set_lines(True, False)
        serial.set_break(True)
        serial.set_break(False)
        self.assertEqual(usb.calls, [
            (0x21, 0x20, 0, 0, b"\xb0\x04\0\0\x02\x02\x08", 0),
            (0x21, 0x22, 1, 0, b"", 0),
            (0x21, 0x23, 0xffff, 0, b"", 0),
            (0x21, 0x23, 0, 0, b"", 0)])

    def test_all_adapters_runtime_controls(self):
        for vid, pid, kind in [(0x1a86, 0x7523, "ch340"), (0x10c4, 0xea60, "cp210x"), (0x0403, 0x6010, "ftdi")]:
            with self.subTest(kind=kind):
                serial = UsbSerial(FakeUsb(vid, pid))
                serial.configure(57600, 8, 2, 2)
                serial.set_lines(True, True)
                self.assertEqual(serial.modem_bits() & 6, 6)
                serial.set_break(True)
                self.assertTrue(serial.break_enabled)
                serial.set_break(False)
                self.assertFalse(serial.break_enabled)
                self.assertEqual((serial.baud, serial.parity, serial.stop_bits), (57600, 2, 2))
                with self.assertRaises(ValueError):
                    serial.configure(0)

    def test_control_failures_are_not_silently_accepted(self):
        usb = FakeUsb(0x1234, 1)
        serial = UsbSerial(usb)
        def disconnected(*_args, **_kwargs):
            raise OSError("Disconnected")
        usb.control = disconnected
        with self.assertRaises(OSError):
            serial.configure(1200)
        self.assertEqual(serial.baud, 115200)


CLIENT = r'''
#define _GNU_SOURCE
#include <assert.h>
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <sys/ioctl.h>
#include <termios.h>
#include <unistd.h>
struct kernel_termios2 { unsigned iflag,oflag,cflag,lflag; unsigned char line,cc[19]; unsigned ispeed,ospeed; };
#define GET2 _IOR('T',0x2a,struct kernel_termios2)
#define SET2 _IOW('T',0x2b,struct kernel_termios2)
int main(int argc,char **argv) {
    assert(argc == 2);
    int fd=open(argv[1],O_RDWR|O_NOCTTY);
    assert(fd>=0);
    struct termios t;
    assert(tcgetattr(fd,&t)==0);
    cfmakeraw(&t); t.c_cflag |= HUPCL;
    assert(cfsetispeed(&t,B1200)==0 && cfsetospeed(&t,B1200)==0);
    assert(tcsetattr(fd,TCSANOW,&t)==0);
    int bits=TIOCM_DTR|TIOCM_RTS;
    assert(ioctl(fd,TIOCMSET,&bits)==0);
    bits=0; assert(ioctl(fd,TIOCMGET,&bits)==0 && (bits&6)==6);
    bits=TIOCM_RTS; assert(ioctl(fd,TIOCMBIC,&bits)==0);
    assert(ioctl(fd,TIOCMGET,&bits)==0 && (bits&6)==2);
    assert(ioctl(fd,TIOCSBRK)==0); assert(ioctl(fd,TIOCCBRK)==0);
    assert(tcsendbreak(fd,0)==0);
    t.c_cflag |= PARENB|CSTOPB;
    assert(tcsetattr(fd,TCSANOW,&t)==0);
    assert(tcgetattr(fd,&t)==0 && (t.c_cflag & (PARENB|CSTOPB))==(PARENB|CSTOPB));
    assert(cfgetospeed(&t)==B1200);
    t.c_cflag |= CRTSCTS; errno=0;
    assert(tcsetattr(fd,TCSANOW,&t)==-1 && errno==ENOTSUP);
    struct kernel_termios2 t2;
    assert(ioctl(fd,GET2,&t2)==0);
    t2.cflag=(t2.cflag & ~0010017)|0010000;
    t2.ispeed=t2.ospeed=123456;
    assert(ioctl(fd,SET2,&t2)==0);
    memset(&t2,0,sizeof(t2)); assert(ioctl(fd,GET2,&t2)==0);
    assert(t2.ospeed==123456 && t2.ispeed==123456);
    const char out[]={0,1,'\r','\n',(char)255,'A'};
    char in[sizeof(out)];
    assert(write(fd,out,sizeof(out))==sizeof(out)); assert(tcdrain(fd)==0);
    size_t got=0;
    while(got<sizeof(in)){ssize_t n=read(fd,in+got,sizeof(in)-got); assert(n>0);got+=(size_t)n;}
    assert(memcmp(in,out,sizeof(out))==0);
    assert(tcflush(fd,TCIOFLUSH)==0);
    assert(close(fd)==0);
    puts("SERIAL_POSIX_PASSED");
    return 0;
}
'''


class NativeSerial(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not shutil.which("cc"):
            raise unittest.SkipTest("C compiler unavailable")
        cls.temp = tempfile.TemporaryDirectory(prefix="ts-bridge-test-")
        cls.folder = Path(cls.temp.name)
        cls.shim = cls.folder / "serial-posix.so"
        subprocess.run(["cc", "-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-shared", "-fPIC", str(NATIVE / "serial_posix.c"), "-ldl", "-o", str(cls.shim)], check=True)
        (cls.folder / "client.c").write_text(CLIENT)
        subprocess.run(["cc", "-Wall", "-Wextra", "-Werror", str(cls.folder / "client.c"), "-o", str(cls.folder / "client")], check=True)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def test_unmodified_native_termios_and_ioctl_over_real_pty(self):
        alias, audit = self.folder / "ttyUSB0", self.folder / "events.jsonl"
        result = subprocess.run([sys.executable, str(NATIVE / "serial_bridge.py"), "--virtual", "--shim", str(self.shim), "--port", str(alias), "--audit", str(audit), "--", str(self.folder / "client"), "{port}"], capture_output=True, text=True, timeout=15)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("SERIAL_POSIX_PASSED", result.stdout)
        self.assertFalse(os.path.lexists(alias))
        operations = [json.loads(line) for line in audit.read_text().splitlines()]
        self.assertIn({"operation": 1, "values": [1200, 8, 2, 2]}, operations)
        self.assertIn({"operation": 1, "values": [123456, 8, 2, 2]}, operations)
        self.assertIn({"operation": 2, "values": [0, 6, 0, 0]}, operations)  # HUPCL/1200-baud touch
        self.assertIn({"operation": 4, "values": [1, 250, 0, 0]}, operations)

    def test_existing_file_never_overwritten(self):
        alias = self.folder / "owned-file"
        alias.write_text("keep")
        result = subprocess.run([sys.executable, str(NATIVE / "serial_bridge.py"), "--virtual", "--shim", str(self.shim), "--port", str(alias), "--", "true"], capture_output=True, text=True, timeout=5)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(alias.read_text(), "keep")

    def test_unrelated_posix_descriptors_are_untouched(self):
        code = "import os,pty,termios; a,b=pty.openpty(); termios.tcgetattr(b); os.close(a); os.close(b); print('OK')"
        result = subprocess.run([sys.executable, "-c", code], env={**os.environ, "LD_PRELOAD": str(self.shim), "TABSTUDIO_SERIAL_DEVICE": "/definitely/not/a/device"}, capture_output=True, text=True, timeout=5)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("OK", result.stdout)

# A deliberately small mock of the upstream ABI, not copied vendor code. It
# makes the preload isolation/context/refcount contract executable without USB.
LIBUSB_HEADER = r'''
#ifndef TS_FAKE_LIBUSB_H
#define TS_FAKE_LIBUSB_H
#include <stdint.h>
#include <sys/types.h>
typedef struct libusb_context libusb_context;
typedef struct libusb_device libusb_device;
typedef struct libusb_device_handle libusb_device_handle;
struct libusb_init_option { int option; union { int ival; void *pval; } value; };
struct libusb_device_descriptor { uint16_t idVendor,idProduct; };
#define LIBUSB_OPTION_NO_DEVICE_DISCOVERY 2
#define LIBUSB_ERROR_ACCESS -3
#define LIBUSB_ERROR_INVALID_PARAM -2
#define LIBUSB_ERROR_NOT_SUPPORTED -12
#define LIBUSB_ERROR_NO_MEM -11
#define LIBUSB_ERROR_NO_DEVICE -4
int libusb_init_context(libusb_context **,const struct libusb_init_option[],int);
int libusb_init(libusb_context **);
void libusb_exit(libusb_context *);
int libusb_wrap_sys_device(libusb_context *,intptr_t,libusb_device_handle **);
void libusb_close(libusb_device_handle *);
libusb_device *libusb_get_device(libusb_device_handle *);
libusb_device *libusb_ref_device(libusb_device *);
void libusb_unref_device(libusb_device *);
void libusb_free_device_list(libusb_device **,int);
ssize_t libusb_get_device_list(libusb_context *,libusb_device ***);
int libusb_open(libusb_device *,libusb_device_handle **);
libusb_device_handle *libusb_open_device_with_vid_pid(libusb_context *,uint16_t,uint16_t);
int libusb_get_device_descriptor(libusb_device *,struct libusb_device_descriptor *);
libusb_context *test_handle_context(libusb_device_handle *);
#endif
'''
LIBUSB_FAKE = r'''
#include <assert.h>
#include <stdlib.h>
#include <fcntl.h>
#include "libusb-1.0/libusb.h"
struct libusb_context { int handles; };
struct libusb_device { libusb_context *ctx; int refs; };
struct libusb_device_handle { libusb_device *dev; };
int libusb_init_context(libusb_context **ctx,const struct libusb_init_option opts[],int n) {
    int found=0;for(int i=0;i<n;i++) if(opts[i].option==2)found=1;assert(found);
    *ctx=calloc(1,sizeof(**ctx));return 0;
}
int libusb_init(libusb_context **ctx){(void)ctx;abort();}
libusb_device_handle *libusb_open_device_with_vid_pid(libusb_context *ctx,uint16_t v,uint16_t p){(void)ctx;(void)v;(void)p;abort();}
void libusb_exit(libusb_context *ctx) {assert(ctx->handles==0);free(ctx);}
int libusb_wrap_sys_device(libusb_context *ctx,intptr_t fd,libusb_device_handle **out) {
    assert(fd==3 && fcntl((int)fd,F_GETFD)>=0);
    *out=calloc(1,sizeof(**out)); (*out)->dev=calloc(1,sizeof(*(*out)->dev));
    (*out)->dev->ctx=ctx;(*out)->dev->refs=1;ctx->handles++;return 0;
}
void libusb_unref_device(libusb_device *d){if(--d->refs==0)free(d);}
void libusb_close(libusb_device_handle *h){h->dev->ctx->handles--;libusb_unref_device(h->dev);free(h);}
libusb_device *libusb_get_device(libusb_device_handle *h){return h->dev;}
libusb_device *libusb_ref_device(libusb_device *d){d->refs++;return d;}
void libusb_free_device_list(libusb_device **list,int unref){if(unref)for(int i=0;list[i];i++)libusb_unref_device(list[i]);free(list);}
int libusb_get_device_descriptor(libusb_device *d,struct libusb_device_descriptor *desc){(void)d;desc->idVendor=0x0483;desc->idProduct=0x3748;return 0;}
libusb_context *test_handle_context(libusb_device_handle *h){return h->dev->ctx;}
ssize_t libusb_get_device_list(libusb_context *ctx,libusb_device ***l){(void)ctx;(void)l;abort();}
int libusb_open(libusb_device *d,libusb_device_handle **h){(void)d;(void)h;abort();}
'''
LIBUSB_CLIENT = r'''
#include <assert.h>
#include <stdlib.h>
#include <stdio.h>
#include <fcntl.h>
#include "libusb-1.0/libusb.h"
int main(int argc,char **argv){
    (void)argv;
    libusb_context *a,*b;
    if(argc>1){assert(libusb_init(&a)==LIBUSB_ERROR_ACCESS);puts("DENIED");return 0;}
    assert(libusb_init(&a)==0 && libusb_init(&b)==0);
    libusb_device **la,**lb,**again;libusb_device_handle *h;
    assert(libusb_get_device_list(a,&la)==1 && libusb_get_device_list(b,&lb)==1);
    assert(libusb_open(la[0],&h)==0 && test_handle_context(h)==a);libusb_close(h);
    assert(libusb_open(lb[0],&h)==0 && test_handle_context(h)==b);libusb_close(h);
    assert(libusb_get_device_list(a,&again)==1 && again[0]==la[0]);
    libusb_free_device_list(again,1);
    assert(libusb_open_device_with_vid_pid(a,1,2)==NULL);
    h=libusb_open_device_with_vid_pid(a,0x0483,0x3748);assert(h && test_handle_context(h)==a);libusb_close(h);
    libusb_free_device_list(la,1);libusb_free_device_list(lb,1);
    libusb_exit(a);libusb_exit(b);
    assert(fcntl(3,F_GETFD)>=0);puts("LIBUSB_CONTEXT_PASSED");return 0;
}
'''


class NativeLibUsb(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not shutil.which("cc"):
            raise unittest.SkipTest("C compiler unavailable")
        cls.temp = tempfile.TemporaryDirectory(prefix="ts-libusb-test-")
        cls.folder = Path(cls.temp.name)
        header = cls.folder / "libusb-1.0/libusb.h"
        header.parent.mkdir()
        header.write_text(LIBUSB_HEADER)
        (cls.folder / "fake.c").write_text(LIBUSB_FAKE)
        (cls.folder / "client.c").write_text(LIBUSB_CLIENT)
        def compile(*args):
            subprocess.run(["cc", "-std=gnu17", "-Wall", "-Wextra", "-Werror", "-I", str(cls.folder), *map(str, args)], check=True)
        compile("-fPIC", "-shared", cls.folder / "fake.c", "-o", cls.folder / "libfakeusb.so")
        compile("-fPIC", "-shared", MICROCHIP / "usb_fd.c", "-o", cls.folder / "usb-fd.so", "-L", cls.folder, "-lfakeusb", "-ldl", "-pthread", "-Wl,-rpath," + str(cls.folder))
        compile(cls.folder / "client.c", "-L", cls.folder, "-lfakeusb", "-o", cls.folder / "client", "-Wl,-rpath," + str(cls.folder))

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def test_libusb_contexts_isolated_and_fd_borrowed(self):
        descriptor = os.open(os.devnull, os.O_RDWR)
        try:
            result = subprocess.run([str(self.folder / "client")], pass_fds=(descriptor,), preexec_fn=lambda: os.dup2(descriptor, 3), env={**os.environ, "LD_PRELOAD": str(self.folder / "usb-fd.so"), "TABSTUDIO_USB_FD": "3"}, capture_output=True, text=True, timeout=5)
        finally:
            os.close(descriptor)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("LIBUSB_CONTEXT_PASSED", result.stdout)

    def test_no_ungranted_enumeration(self):
        result = subprocess.run([str(self.folder / "client"), "denied"], env={**os.environ, "LD_PRELOAD": str(self.folder / "usb-fd.so"), "TABSTUDIO_USB_FD": "-1"}, capture_output=True, text=True, timeout=5)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("DENIED", result.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
