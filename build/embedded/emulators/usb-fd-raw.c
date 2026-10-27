/* Raw syscalls deliberately bypass libc and LD_PRELOAD. Synthetic handle only. */
#define _GNU_SOURCE
#include <assert.h>
#include <errno.h>
#include <fcntl.h>
#include <linux/openat2.h>
#include <stdio.h>
#include <string.h>
#include <sys/syscall.h>
#include <unistd.h>

static void verify(int fd, int cloexec)
{
    char text[32] = {0};
    assert(fd >= 4);
    assert((fcntl(fd, F_GETFD) & FD_CLOEXEC) == (cloexec ? FD_CLOEXEC : 0));
    assert(lseek(fd, 0, SEEK_SET) == 0);
    assert(read(fd, text, sizeof(text)) == 20);
    assert(strcmp(text, "GRANTED_USB_HANDLE\n") == 0);
    assert(close(fd) == 0);
    assert(fcntl(3, F_GETFD) >= 0);
}

int main(int argc, char **argv)
{
    const char *selected = "/dev/bus/usb/001/002";
    assert(argc == 2);
    if (strcmp(argv[1], "reused") == 0) {
        assert(close(3) == 0);
        assert(open("/dev/null", O_RDONLY) == 3);
        errno = 0;
        assert(syscall(SYS_openat, AT_FDCWD, selected, O_RDWR | O_CLOEXEC, 0) == -1);
        assert(errno == EBADF);
        puts("USB_FD_REUSE_REFUSED");
        return 0;
    }
    if (strcmp(argv[1], "disabled") == 0) {
        errno = 0;
        assert(syscall(SYS_openat, AT_FDCWD, selected, O_RDWR, 0) == -1);
        puts("USB_FD_DISABLED");
        return 0;
    }
    verify((int)syscall(SYS_openat, AT_FDCWD, selected, O_RDWR | O_CLOEXEC, 0), 1);
    verify((int)syscall(SYS_openat, AT_FDCWD, selected, O_RDWR, 0), 0);
#ifdef SYS_open
    verify((int)syscall(SYS_open, selected, O_RDWR | O_CLOEXEC, 0), 1);
#endif
#ifdef SYS_openat2
    struct open_how how = { .flags = O_RDWR | O_CLOEXEC };
    verify((int)syscall(SYS_openat2, AT_FDCWD, selected, &how, sizeof(how)), 1);
#endif
    errno = 0;
    assert(syscall(SYS_openat, AT_FDCWD, "/dev/bus/usb/001/003", O_RDWR, 0) == -1);
    errno = 0;
    assert(syscall(SYS_openat, AT_FDCWD, selected, O_RDWR | O_TRUNC, 0) == -1);
    assert(errno == EINVAL);
    puts("USB_FD_RAW_SYSCALL_PASSED");
    return 0;
}
