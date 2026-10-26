/* SPDX-License-Identifier: Apache-2.0
 * Exact-device usbfs open bridge for dynamically linked libc clients.
 * Raw-syscall/static clients require TabStudio's PRoot syscall bridge instead.
 * USB ioctls and descriptor reads run on the kernel's original granted handle.
 */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <pthread.h>
#include <stdarg.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static pthread_once_t init_once = PTHREAD_ONCE_INIT;
static int granted_fd = -1;
static char selected_path[32];
static int (*real_open)(const char *, int, ...);
static int (*real_openat)(int, const char *, int, ...);

static int needs_mode(int flags) {
    return (flags & O_CREAT) || ((flags & O_TMPFILE) == O_TMPFILE);
}

static void initialize(void) {
    real_open = dlsym(RTLD_NEXT, "open");
    real_openat = dlsym(RTLD_NEXT, "openat");
    const char *fd_text = getenv("TABSTUDIO_USB_FD");
    const char *path = getenv("TABSTUDIO_USB_DEVPATH");
    if (!fd_text || !path || strlen(path) != 20 || strncmp(path, "/dev/bus/usb/", 13)) return;
    if (path[16] != '/') return;
    for (int i = 13; i < 20; i++) if (i != 16 && (path[i] < '0' || path[i] > '9')) return;
    char *end = NULL;
    long descriptor = strtol(fd_text, &end, 10);
    if (!end || *end || descriptor < 3 || descriptor > 1048575) return;
    if (fcntl((int)descriptor, F_GETFD) < 0) return;
    granted_fd = (int)descriptor;
    memcpy(selected_path, path, 21);
}

/* Returns -2 to forward a non-USB path. No ungranted USB node is ever opened. */
static int permissioned_open(const char *path, int flags) {
    pthread_once(&init_once, initialize);
    if (!path || strncmp(path, "/dev/bus/usb/", 13)) return -2;
    if (granted_fd < 3 || strcmp(path, selected_path)) { errno = EACCES; return -1; }
    if (flags & (O_CREAT | O_TRUNC | O_EXCL)) { errno = EINVAL; return -1; }
    return fcntl(granted_fd, flags & O_CLOEXEC ? F_DUPFD_CLOEXEC : F_DUPFD, 3);
}

int open(const char *path, int flags, ...) {
    mode_t mode = 0;
    if (needs_mode(flags)) { va_list args; va_start(args, flags); mode = (mode_t)va_arg(args, int); va_end(args); }
    int bridged = permissioned_open(path, flags);
    if (bridged != -2) return bridged;
    if (!real_open) { errno = ENOSYS; return -1; }
    return real_open(path, flags, mode);
}

int open64(const char *path, int flags, ...) {
    mode_t mode = 0;
    if (needs_mode(flags)) { va_list args; va_start(args, flags); mode = (mode_t)va_arg(args, int); va_end(args); }
    return open(path, flags, mode);
}

int openat(int dirfd, const char *path, int flags, ...) {
    mode_t mode = 0;
    if (needs_mode(flags)) { va_list args; va_start(args, flags); mode = (mode_t)va_arg(args, int); va_end(args); }
    int bridged = permissioned_open(path, flags);
    if (bridged != -2) return bridged;
    if (!real_openat) { errno = ENOSYS; return -1; }
    return real_openat(dirfd, path, flags, mode);
}

int openat64(int dirfd, const char *path, int flags, ...) {
    mode_t mode = 0;
    if (needs_mode(flags)) { va_list args; va_start(args, flags); mode = (mode_t)va_arg(args, int); va_end(args); }
    return openat(dirfd, path, flags, mode);
}

int __open_2(const char *path, int flags) { return open(path, flags); }
int __open64_2(const char *path, int flags) { return open(path, flags); }
int __openat_2(int dirfd, const char *path, int flags) { return openat(dirfd, path, flags); }
int __openat64_2(int dirfd, const char *path, int flags) { return openat(dirfd, path, flags); }
