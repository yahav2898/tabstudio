/* SPDX-License-Identifier: MIT
 * Permissioned USB serial control for unmodified dynamically-linked POSIX tools.
 * Bytes use a real raw PTY. Only matching PTY descriptors have controls forwarded
 * to the private FIFO control channel. No device nodes, root, TCP listener, or syscall
 * interception. Static binaries/direct syscalls require separate integration.
 */
#define _GNU_SOURCE
#include <arpa/inet.h>
#include <dirent.h>
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <sys/ioctl.h>
#include <sys/file.h>
#include <poll.h>
#include <time.h>
#include <stdio.h>
#include <sys/stat.h>
#include <sys/time.h>
#include <termios.h>
#include <unistd.h>

#ifdef __GLIBC__
typedef unsigned long request_t;
#else
typedef int request_t;
#endif
#ifndef CBAUD
#define CBAUD 0010017
#endif
#ifndef CMSPAR
#define CMSPAR 010000000000
#endif
#define TS_BOTHER 0010000
#define TS_MAGIC 0x54534231U
enum { TS_CONFIG=1, TS_LINES, TS_MODEM, TS_BREAK, TS_FLUSH, TS_DRAIN, TS_GET_CONFIG };

/* Linux's ioctl ABI has 19 control chars, independent of libc's NCCS. */
struct ts_termios { uint32_t iflag, oflag, cflag, lflag; uint8_t line, cc[19]; };
struct ts_termios2 { struct ts_termios base; uint32_t ispeed, ospeed; };
#define TS_TCGETS2 _IOR('T', 0x2a, struct ts_termios2)
#define TS_TCSETS2 _IOW('T', 0x2b, struct ts_termios2)
#define TS_TCSETSW2 _IOW('T', 0x2c, struct ts_termios2)
#define TS_TCSETSF2 _IOW('T', 0x2d, struct ts_termios2)

static int native_close(int fd) {
    int (*fn)(int) = dlsym(RTLD_NEXT, "close");
    if (!fn) { errno = ENOSYS; return -1; }
    return fn(fd);
}

static int native_ioctl(int fd, request_t request, uintptr_t argument) {
    int (*fn)(int, request_t, ...) = dlsym(RTLD_NEXT, "ioctl");
    if (!fn) { errno = ENOSYS; return -1; }
    return fn(fd, request, argument);
}

static int serial_fd(int fd) {
    const char *path = getenv("TABSTUDIO_SERIAL_DEVICE");
    struct stat selected, actual;
    int saved = errno;
    int result = path && *path && stat(path, &selected) == 0 && fstat(fd, &actual) == 0
        && S_ISCHR(actual.st_mode) && selected.st_dev == actual.st_dev && selected.st_ino == actual.st_ino;
    errno = saved;
    return result;
}

static int exchange(int fd, unsigned char *bytes, size_t size, int writing) {
    while (size) {
        struct pollfd waiter = { .fd = fd, .events = writing ? POLLOUT : POLLIN };
        int ready = poll(&waiter, 1, 70000);
        if (ready < 0 && errno == EINTR) continue;
        if (ready <= 0) { if (!ready) errno = ETIMEDOUT; return -1; }
        ssize_t count = writing ? write(fd, bytes, size) : read(fd, bytes, size);
        if (count < 0 && errno == EINTR) continue;
        if (count <= 0) { if (!count) errno = EPIPE; return -1; }
        bytes += count;
        size -= (size_t)count;
    }
    return 0;
}

static int rpc(unsigned operation, uint32_t a, uint32_t b, uint32_t c, uint32_t d, uint32_t result[4]) {
    const char *directory = getenv("TABSTUDIO_SERIAL_CONTROL_DIR");
    char path[4096];
    if (!directory || !*directory || strlen(directory) > sizeof(path) - 16) { errno = ENODEV; return -1; }
    (void)snprintf(path, sizeof(path), "%s/lock", directory);
    int lock = open(path, O_RDWR | O_CLOEXEC);
    if (lock < 0) return -1;
    int ok;
    do { ok = flock(lock, LOCK_EX); } while (ok < 0 && errno == EINTR);
    if (ok < 0) { int saved = errno; native_close(lock); errno = saved; return -1; }
    (void)snprintf(path, sizeof(path), "%s/request", directory);
    int request_fd = open(path, O_WRONLY | O_NONBLOCK | O_CLOEXEC);
    (void)snprintf(path, sizeof(path), "%s/response", directory);
    int response_fd = open(path, O_RDONLY | O_NONBLOCK | O_CLOEXEC);
    if (request_fd < 0 || response_fd < 0) {
        int saved = errno;
        if (request_fd >= 0) native_close(request_fd);
        if (response_fd >= 0) native_close(response_fd);
        native_close(lock); errno = saved; return -1;
    }
    struct timespec stamp;
    (void)clock_gettime(CLOCK_MONOTONIC, &stamp);
    uint32_t token = (uint32_t)stamp.tv_nsec ^ (uint32_t)getpid() ^ (uint32_t)stamp.tv_sec;
    uint32_t request[] = { htonl(TS_MAGIC), htonl(token), htonl(operation), htonl(a), htonl(b), htonl(c), htonl(d) };
    uint32_t response[6];
    ok = exchange(request_fd, (unsigned char *)request, sizeof(request), 1);
    /* A previous caller may have been killed after submitting a request. Ignore
     * its eventual response instead of attributing it to this operation. */
    unsigned stale = 0;
    while (ok == 0) {
        ok = exchange(response_fd, (unsigned char *)response, sizeof(response), 0);
        if (ok < 0 || ntohl(response[1]) == token) break;
        if (++stale > 32) { errno = EPROTO; ok = -1; }
    }
    int saved = errno;
    native_close(request_fd); native_close(response_fd); native_close(lock);
    if (ok < 0) { errno = saved; return -1; }
    unsigned status = ntohl(response[0]);
    if (status) { errno = status <= 4095 ? (int)status : EIO; return -1; }
    if (result) for (unsigned i = 0; i < 4; ++i) result[i] = ntohl(response[i + 2]);
    return 0;
}

struct baud_pair { speed_t code; uint32_t value; };
static const struct baud_pair rates[] = {
    {B0,0}, {B50,50}, {B75,75}, {B110,110}, {B134,134}, {B150,150}, {B200,200},
    {B300,300}, {B600,600}, {B1200,1200}, {B1800,1800}, {B2400,2400},
    {B4800,4800}, {B9600,9600}, {B19200,19200}, {B38400,38400},
    {B57600,57600}, {B115200,115200}, {B230400,230400}, {B460800,460800},
    {B500000,500000}, {B576000,576000}, {B921600,921600}, {B1000000,1000000},
    {B1152000,1152000}, {B1500000,1500000}, {B2000000,2000000},
    {B2500000,2500000}, {B3000000,3000000}, {B3500000,3500000}, {B4000000,4000000}
};

static uint32_t baud_value(speed_t speed) {
    for (unsigned i=0; i<sizeof(rates)/sizeof(rates[0]); ++i) if (rates[i].code == speed) return rates[i].value;
    return UINT32_MAX;
}
static speed_t baud_code(uint32_t baud) {
    for (unsigned i=0; i<sizeof(rates)/sizeof(rates[0]); ++i) if (rates[i].value == baud) return rates[i].code;
    return (speed_t)TS_BOTHER;
}

static tcflag_t coding(tcflag_t flags, uint32_t bits, uint32_t stops, uint32_t parity) {
    flags &= ~(tcflag_t)(CSIZE | CSTOPB | PARENB | PARODD | CMSPAR);
    flags |= bits == 5 ? CS5 : bits == 6 ? CS6 : bits == 7 ? CS7 : CS8;
    if (stops == 2) flags |= CSTOPB;
    if (parity) flags |= PARENB;
    if (parity == 1 || parity == 3) flags |= PARODD;
    if (parity == 3 || parity == 4) flags |= CMSPAR;
    return flags;
}

static int configure(tcflag_t flags, uint32_t baud) {
    if (flags & CRTSCTS) { errno = ENOTSUP; return -1; }
    if (!baud) return rpc(TS_LINES, 0, TIOCM_DTR | TIOCM_RTS, 0, 0, NULL);
    if (baud == UINT32_MAX) { errno = EINVAL; return -1; }
    uint32_t bits = (flags & CSIZE) == CS5 ? 5 : (flags & CSIZE) == CS6 ? 6 : (flags & CSIZE) == CS7 ? 7 : 8;
    uint32_t parity = !(flags & PARENB) ? 0 : (flags & CMSPAR) ? ((flags & PARODD) ? 3 : 4) : ((flags & PARODD) ? 1 : 2);
    return rpc(TS_CONFIG, baud, bits, (flags & CSTOPB) ? 2 : 1, parity, NULL);
}

int tcgetattr(int fd, struct termios *attributes) {
    int (*fn)(int, struct termios *) = dlsym(RTLD_NEXT, "tcgetattr");
    if (!fn) { errno = ENOSYS; return -1; }
    int status = fn(fd, attributes);
    if (status < 0 || !serial_fd(fd)) return status;
    uint32_t config[4];
    if (rpc(TS_GET_CONFIG, 0, 0, 0, 0, config) < 0) return -1;
    attributes->c_cflag = coding(attributes->c_cflag, config[1], config[2], config[3]);
    speed_t code = baud_code(config[0]);
    /* BOTHER belongs to termios2; regular termios can report only named rates. */
    if (code == TS_BOTHER) code = B38400;
    if (cfsetispeed(attributes, code) < 0 || cfsetospeed(attributes, code) < 0) return -1;
    return 0;
}

int tcsetattr(int fd, int action, const struct termios *attributes) {
    int (*fn)(int, int, const struct termios *) = dlsym(RTLD_NEXT, "tcsetattr");
    if (!fn) { errno = ENOSYS; return -1; }
    if (!serial_fd(fd)) return fn(fd, action, attributes);
    if (!attributes) { errno = EFAULT; return -1; }
    if (action != TCSANOW && action != TCSADRAIN && action != TCSAFLUSH) { errno = EINVAL; return -1; }
    speed_t input = cfgetispeed(attributes), output = cfgetospeed(attributes);
    if (input != B0 && input != output) { errno = ENOTSUP; return -1; }
    if (action != TCSANOW && rpc(TS_DRAIN, 0, 0, 0, 0, NULL) < 0) return -1;
    if (configure(attributes->c_cflag, baud_value(output)) < 0) return -1;
    struct termios local = *attributes;
    local.c_cflag = coding(local.c_cflag, 8, 1, 0) | CREAD | CLOCAL;
    if (cfsetispeed(&local, B38400) < 0 || cfsetospeed(&local, B38400) < 0) return -1;
    return fn(fd, action, &local);
}

int tcdrain(int fd) {
    if (serial_fd(fd)) return rpc(TS_DRAIN, 0, 0, 0, 0, NULL);
    int (*fn)(int) = dlsym(RTLD_NEXT, "tcdrain");
    if (!fn) { errno = ENOSYS; return -1; }
    return fn(fd);
}

int tcflush(int fd, int queue) {
    if (!serial_fd(fd)) {
        int (*fn)(int,int) = dlsym(RTLD_NEXT, "tcflush");
        if (!fn) { errno = ENOSYS; return -1; }
        return fn(fd, queue);
    }
    return rpc(TS_FLUSH, (uint32_t)queue, 0, 0, 0, NULL);
}

int tcsendbreak(int fd, int duration) {
    if (serial_fd(fd)) {
        if (duration < 0 || duration > 600) { errno = EINVAL; return -1; }
        return rpc(TS_BREAK, 1, duration ? (uint32_t)duration * 100 : 250, 0, 0, NULL);
    }
    int (*fn)(int,int) = dlsym(RTLD_NEXT, "tcsendbreak");
    if (!fn) { errno = ENOSYS; return -1; }
    return fn(fd, duration);
}

int ioctl(int fd, request_t request, ...) {
    va_list args;
    va_start(args, request);
    uintptr_t argument = 0;
    /* These ioctl operations have no third argument. */
    if (request != TIOCSBRK && request != TIOCCBRK && request != TIOCEXCL && request != TIOCNXCL)
        argument = va_arg(args, uintptr_t);
    va_end(args);
    if (!serial_fd(fd)) return native_ioctl(fd, request, argument);
    uint32_t values[4];
    if (request == TIOCMGET) {
        if (!argument) { errno = EFAULT; return -1; }
        if (rpc(TS_MODEM, 0, 0, 0, 0, values) < 0) return -1;
        *(int *)argument = (int)values[0];
        return 0;
    }
    if (request == TIOCMSET || request == TIOCMBIS || request == TIOCMBIC) {
        if (!argument) { errno = EFAULT; return -1; }
        unsigned bits = (unsigned)*(int *)argument;
        return rpc(TS_LINES, request == TIOCMBIC ? 0 : bits, request == TIOCMSET ? 6 : (bits & 6), 0, 0, NULL);
    }
    if (request == TIOCSBRK || request == TIOCCBRK) return rpc(TS_BREAK, request == TIOCSBRK, 0, 0, 0, NULL);
    if (request == TCSBRK || request == TCSBRKP) {
        if (rpc(TS_DRAIN, 0, 0, 0, 0, NULL) < 0) return -1;
        if (request == TCSBRK && argument) return 0;
        if (argument > 600) { errno = EINVAL; return -1; }
        return rpc(TS_BREAK, 1, argument ? (uint32_t)argument * 100 : 250, 0, 0, NULL);
    }
    if (request == TCFLSH) return rpc(TS_FLUSH, (uint32_t)argument, 0, 0, 0, NULL);
    if (request == TCGETS || (unsigned long)request == TS_TCGETS2) {
        if (!argument) { errno = EFAULT; return -1; }
        if (native_ioctl(fd, TCGETS, argument) < 0 || rpc(TS_GET_CONFIG, 0, 0, 0, 0, values) < 0) return -1;
        struct ts_termios *term = (void *)argument;
        term->cflag = (uint32_t)coding(term->cflag, values[1], values[2], values[3]);
        term->cflag = (term->cflag & ~CBAUD) | baud_code(values[0]);
        if ((unsigned long)request == TS_TCGETS2) {
            struct ts_termios2 *term2 = (void *)argument;
            term2->ispeed = term2->ospeed = values[0];
        }
        return 0;
    }
    if (request == TCSETS || request == TCSETSW || request == TCSETSF ||
        (unsigned long)request == TS_TCSETS2 || (unsigned long)request == TS_TCSETSW2 || (unsigned long)request == TS_TCSETSF2) {
        if (!argument) { errno = EFAULT; return -1; }
        const struct ts_termios *term = (void *)argument;
        uint32_t baud = baud_value(term->cflag & CBAUD);
        if ((term->cflag & CBAUD) == TS_BOTHER) {
            if ((unsigned long)request != TS_TCSETS2 && (unsigned long)request != TS_TCSETSW2 && (unsigned long)request != TS_TCSETSF2) { errno = EINVAL; return -1; }
            const struct ts_termios2 *term2 = (void *)argument;
            baud = term2->ospeed;
            if (term2->ispeed && term2->ispeed != baud) { errno = ENOTSUP; return -1; }
        }
        if (request != TCSETS && (unsigned long)request != TS_TCSETS2 && rpc(TS_DRAIN, 0, 0, 0, 0, NULL) < 0) return -1;
        if (configure(term->cflag, baud) < 0) return -1;
        struct ts_termios local = *term;
        local.cflag = ((uint32_t)coding(local.cflag, 8, 1, 0) & ~CBAUD) | B38400 | CLOCAL | CREAD;
        request_t local_request = (request == TCSETSF || (unsigned long)request == TS_TCSETSF2) ? TCSETSF : TCSETS;
        return native_ioctl(fd, local_request, (uintptr_t)&local);
    }
    return native_ioctl(fd, request, argument);
}

int close(int fd) {
    if (serial_fd(fd)) {
        struct termios attributes;
        int (*get)(int, struct termios *) = dlsym(RTLD_NEXT, "tcgetattr");
        int last = 1;
        DIR *directory = opendir("/proc/self/fd");
        if (directory) {
            struct dirent *entry;
            while ((entry = readdir(directory))) {
                char *end;
                long candidate = strtol(entry->d_name, &end, 10);
                if (!*end && candidate >= 0 && candidate != fd && serial_fd((int)candidate)) { last = 0; break; }
            }
            closedir(directory);
        }
        if (last && get && get(fd, &attributes) == 0 && (attributes.c_cflag & HUPCL))
            (void)rpc(TS_LINES, 0, TIOCM_DTR | TIOCM_RTS, 0, 0, NULL);
    }
    return native_close(fd);
}
