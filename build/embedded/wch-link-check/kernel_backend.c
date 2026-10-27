#define _GNU_SOURCE
/* Test-only synthetic USB kernel backend. Never compiled into the APK or loaded by product code. */
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <sys/ioctl.h>
#include <linux/usbdevice_fs.h>
#include <linux/usb/ch9.h>
#include <pthread.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#define MAX_LIVE 64
#define MAX_PACKETS 512
struct packet { size_t length; unsigned char bytes[64]; };
struct packet_queue { struct packet items[MAX_PACKETS]; unsigned head, count; };
struct live { struct usbdevfs_urb *urb; unsigned long sequence; int state; };
static struct live live[MAX_LIVE];
static struct usbdevfs_urb *done[MAX_LIVE];
static unsigned done_head, done_count;
static unsigned long sequence;
static struct packet_queue replies[2];
static pthread_mutex_t gate = PTHREAD_MUTEX_INITIALIZER;
static bool initialized;
static FILE *log_file;
static unsigned char flash[16384], expected[16384], expected_loader[512];
static size_t expected_loader_length;
static size_t expected_length, loader_length, program_length;
static uint32_t region_start, region_length, chip_id;
static bool writing_loader, writing_program;
static unsigned violations, wire_out;
static const char *scenario;
static int (*real_ioctl)(int, unsigned long, ...);

static bool active(int fd) {
    const char *pid = getenv("TABSTUDIO_TEST_TRACER_PID");
    return fd == 3 && pid && strtol(pid, NULL, 10) == (long)getpid();
}
static void event(const char *kind, unsigned endpoint, const unsigned char *bytes, size_t length) {
    if (!log_file) return;
    fprintf(log_file, "%s endpoint=%02x length=%zu data=", kind, endpoint, length);
    for (size_t i = 0; i < length; i++) fprintf(log_file, "%02x", bytes[i]);
    fputc('\n', log_file); fflush(log_file);
}
static void initialize(void) {
    if (initialized) return;
    initialized = true;
    scenario = getenv("TABSTUDIO_TEST_WCH_SCENARIO");
    if (!scenario) scenario = "normal";
    chip_id = strcmp(scenario, "wrong-chip") == 0 ? 0x00310500u : 0x00300500u;
    memset(flash, 0xff, sizeof flash);
    const char *path = getenv("TABSTUDIO_TEST_WCH_LOG");
    if (path) log_file = fopen(path, "a");
    path = getenv("TABSTUDIO_TEST_WCH_FIRMWARE");
    if (path) { FILE *f = fopen(path, "rb"); if (f) { expected_length = fread(expected, 1, sizeof expected, f); fclose(f); } }
    path = getenv("TABSTUDIO_TEST_WCH_LOADER");
    if (path) { FILE *f = fopen(path, "rb"); if (f) { expected_loader_length = fread(expected_loader, 1, sizeof expected_loader, f); fclose(f); } }
    path = getenv("TABSTUDIO_TEST_WCH_FLASH");
    if (path) { FILE *f = fopen(path, "rb"); if (f) { (void)fread(flash, 1, sizeof flash, f); fclose(f); } }
    if (log_file) { fprintf(log_file, "BACKEND pid=%ld scenario=%s synthetic=true\n", (long)getpid(), scenario); fflush(log_file); }
}
static void violation(const char *reason) {
    violations++;
    if (log_file) { fprintf(log_file, "VIOLATION %s\n", reason); fflush(log_file); }
}
static bool push(unsigned ep, const unsigned char *data, size_t length) {
    struct packet_queue *q = &replies[ep == 0x81 ? 0 : 1];
    if (length > 64 || q->count == MAX_PACKETS) { violation("reply queue overflow"); return false; }
    struct packet *p = &q->items[(q->head + q->count++) % MAX_PACKETS];
    p->length = length; memcpy(p->bytes, data, length); return true;
}
static size_t pop(unsigned ep, unsigned char *data, size_t capacity) {
    struct packet_queue *q = &replies[ep == 0x81 ? 0 : 1];
    if (!q->count) return 0;
    struct packet *p = &q->items[q->head]; size_t length = p->length < capacity ? p->length : capacity;
    memcpy(data, p->bytes, length);
    if (length == p->length) { q->head = (q->head + 1) % MAX_PACKETS; q->count--; }
    else { memmove(p->bytes, p->bytes + length, p->length - length); p->length -= length; }
    event("IN", ep, data, length); return length;
}
static uint32_t be32(const unsigned char *p) { return (uint32_t)p[0] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[2] << 8 | p[3]; }
static void ack(unsigned char command, unsigned char value) {
    unsigned char p[] = {0x82, command, 1, value}; (void)push(0x81, p, sizeof p);
}
static int command(const unsigned char *p, size_t n) {
    if (n < 4 || p[0] != 0x81 || n != 3u + p[2]) { violation("malformed command reached native backend"); return -EPROTO; }
    unsigned char op = p[1], sub = p[3];
    if (op == 0x06) {
        if (p[2] != 1 || (sub != 1 && sub != 4)) { violation("protection write reached native backend"); return -EPERM; }
        unsigned char value = sub == 1 ? 2 : 0;
        if (sub == 1 && strcmp(scenario, "protected") == 0) value = 1;
        if (sub == 4 && strcmp(scenario, "write-protected") == 0) value = 0x11;
        if ((sub == 1 && strcmp(scenario, "unknown-read") == 0) || (sub == 4 && strcmp(scenario, "unknown-write") == 0)) value = 0x7f;
        ack(op, value); return 0;
    }
    if (op == 0x0d && sub == 1 && n == 4) {
        unsigned char response[] = {0x82, 0x0d, 4, 2, 22, 2, 0};
        (void)push(0x81, response, sizeof response); return 0;
    }
    if (op == 0x0d && sub == 2 && n == 4) {
        unsigned char response[] = {0x82, 0x0d, 5, 9, (unsigned char)(chip_id >> 24), (unsigned char)(chip_id >> 16), (unsigned char)(chip_id >> 8), (unsigned char)chip_id};
        if (strcmp(scenario, "malformed-attach") == 0) response[2] = 4;
        (void)push(0x81, response, strcmp(scenario, "short-attach") == 0 ? 4 : sizeof response); return 0;
    }
    if (op == 0x0d && sub == 0xff && n == 4) { ack(op, 0); return 0; }
    if (op == 0x0b && sub == 1 && n == 4) { ack(op, 0); return 0; }
    if (op == 0x0c && n == 5) { ack(op, 1); return 0; }
    if (op == 0x11 && n == 4) {
        unsigned char response[] = {0xff,0xff,0,16,0xaa,0xbb,0xcc,0xdd,0x12,0x34,0x56,0x78,0xff,0xff,0xff,0xff,0,0x30,5,0};
        (void)push(0x81, response, sizeof response); return 0;
    }
    if (op == 0x01 && n == 11) {
        region_start = be32(p + 3); region_length = be32(p + 7);
        if (region_start < 0x08000000u || region_start - 0x08000000u > sizeof flash || region_length > sizeof flash - (region_start - 0x08000000u)) { violation("out of bounds region reached backend"); return -EACCES; }
        program_length = 0; loader_length = 0; ack(op, 0); return 0;
    }
    if (op == 0x03 && n == 11) {
        region_start = be32(p + 3); region_length = be32(p + 7);
        if (region_start < 0x08000000u || region_start - 0x08000000u > sizeof flash || region_length > sizeof flash - (region_start - 0x08000000u) || region_length % 4) { violation("out of bounds read reached backend"); return -EACCES; }
        ack(op, 0); return 0;
    }
    if (op == 0x02 && n == 4) {
        if (sub == 5) { writing_loader = true; writing_program = false; loader_length = 0; ack(op, 5); return 0; }
        if (sub == 7) { if (loader_length != 512) { violation("incomplete loader"); return -EPROTO; } writing_loader = false; ack(op, 7); return 0; }
        if (sub == 2) { writing_program = true; program_length = 0; ack(op, 2); return 0; }
        if (sub == 8) {
            if (program_length != ((region_length + 63u) & ~63u)) { violation("incomplete firmware"); return -EPROTO; }
            const char *path = getenv("TABSTUDIO_TEST_WCH_FLASH");
            if (path) { FILE *f = fopen(path, "wb"); if (!f || fwrite(flash, 1, sizeof flash, f) != sizeof flash) { if (f) fclose(f); return -EIO; } fclose(f); }
            writing_program = false; ack(op, 8); return 0;
        }
        if (sub == 0x0c) {
            ack(op, sub);
            for (size_t offset = 0; offset < region_length; offset += 64) {
                unsigned char data[64]; size_t length = region_length - offset < 64 ? region_length - offset : 64;
                memcpy(data, flash + (region_start - 0x08000000u) + offset, length);
                for (size_t i = 0; i < length; i += 4) { unsigned char a = data[i], b = data[i+1]; data[i] = data[i+3]; data[i+1] = data[i+2]; data[i+2] = b; data[i+3] = a; }
                (void)push(0x82, data, length);
            }
            return 0;
        }
        violation("unexpected program operation"); return -EPERM;
    }
    violation("unknown command reached backend"); return -EPERM;
}
static int out(unsigned ep, const unsigned char *data, size_t length) {
    wire_out++; event("OUT", ep, data, length);
    if (ep == 1) return command(data, length);
    if (ep != 2 || length != 64) { violation("unexpected data endpoint/packet size"); return -EPROTO; }
    if (writing_loader) { if (loader_length + length > 512 || expected_loader_length != 512 || memcmp(data, expected_loader + loader_length, length)) { violation("noncanonical loader reached backend"); return -EPROTO; } loader_length += length; return 0; }
    if (!writing_program || program_length + length > ((region_length + 63u) & ~63u)) { violation("unexpected program data"); return -EPROTO; }
    for (size_t i = 0; i < length; i++) {
        size_t offset = program_length + i;
        if (offset < region_length && (offset >= expected_length || data[i] != expected[offset])) { violation("firmware differs from fixture input"); return -EILSEQ; }
        if (offset >= region_length && data[i] != 0xff) { violation("non-FF program padding"); return -EILSEQ; }
        flash[region_start - 0x08000000u + offset] = data[i];
    }
    program_length += length;
    if (program_length % 1024 == 0 || program_length == ((region_length + 63u) & ~63u)) {
        unsigned char response[] = {0x41, 1, 1, 4}; (void)push(0x82, response, sizeof response);
    }
    return 0;
}
static void complete(struct live *entry, int status, int length) {
    if (done_count == MAX_LIVE) abort();
    entry->urb->status = status; entry->urb->actual_length = length; entry->state = 2;
    done[(done_head + done_count++) % MAX_LIVE] = entry->urb;
}
static void fulfill(void) {
    for (;;) {
        struct live *chosen = NULL;
        for (unsigned i = 0; i < MAX_LIVE; i++) {
            struct live *entry = &live[i];
            if (entry->state != 1 || !(entry->urb->endpoint & 0x80)) continue;
            struct packet_queue *q = &replies[entry->urb->endpoint == 0x81 ? 0 : 1];
            if (q->count && (!chosen || entry->sequence < chosen->sequence)) chosen = entry;
        }
        if (!chosen) return;
        size_t length = pop(chosen->urb->endpoint, chosen->urb->buffer, (size_t)chosen->urb->buffer_length);
        complete(chosen, 0, (int)length);
    }
}
static int backend(unsigned long request, void *arg) {
    initialize();
    if (request == USBDEVFS_CONTROL) {
        static const unsigned char descriptors[] = {
            18,1,0,2,0,0,0,64,0x86,0x1a,0x10,0x80,0,1,0,0,0,1,
            9,2,46,0,1,1,0,0x80,50, 9,4,0,0,4,0xff,0,0,0,
            7,5,1,2,64,0,0, 7,5,0x81,2,64,0,0,
            7,5,2,2,64,0,0, 7,5,0x82,2,64,0,0
        };
        struct usbdevfs_ctrltransfer *t = arg;
        if (!t || t->bRequestType != 0x80 || !t->data) { errno = EINVAL; return -1; }
        if (t->bRequest == USB_REQ_GET_CONFIGURATION && t->wLength == 1) { *(unsigned char *)t->data = 1; return 1; }
        if (t->bRequest == USB_REQ_GET_DESCRIPTOR && (t->wValue == 0x0100 || t->wValue == 0x0200)) {
            size_t offset = t->wValue == 0x0100 ? 0 : 18;
            size_t count = t->wValue == 0x0100 ? 18 : 46;
            if (count > t->wLength) count = t->wLength;
            memcpy(t->data, descriptors + offset, count); return (int)count;
        }
        errno = EPIPE; return -1;
    }
    if (request == USBDEVFS_GET_CAPABILITIES) { *(uint32_t *)arg = 0; return 0; }
    if (request == USBDEVFS_GET_SPEED) return USB_SPEED_FULL;
    if (request == USBDEVFS_CLAIMINTERFACE || request == USBDEVFS_RELEASEINTERFACE) return *(unsigned *)arg == 0 ? 0 : (errno = EINVAL, -1);
    if (request == USBDEVFS_SUBMITURB) {
        struct usbdevfs_urb *urb = arg;
        if (!urb || urb->type != USBDEVFS_URB_TYPE_BULK || urb->buffer_length < 1 || urb->buffer_length > 64 || !urb->buffer || (urb->endpoint != 1 && urb->endpoint != 0x81 && urb->endpoint != 2 && urb->endpoint != 0x82)) { errno = EINVAL; return -1; }
        struct live *entry = NULL;
        for (unsigned i = 0; i < MAX_LIVE; i++) { if (live[i].state && live[i].urb == urb) { errno = EBUSY; return -1; } if (!live[i].state && !entry) entry = &live[i]; }
        if (!entry) { errno = ENOSPC; return -1; }
        *entry = (struct live){urb, ++sequence, 1};
        if (!(urb->endpoint & 0x80)) { int status = out(urb->endpoint, urb->buffer, (size_t)urb->buffer_length); complete(entry, status, status ? 0 : urb->buffer_length); }
        fulfill(); return 0;
    }
    if (request == USBDEVFS_DISCARDURB) {
        for (unsigned i = 0; i < MAX_LIVE; i++) if (live[i].state == 1 && live[i].urb == arg) { complete(&live[i], -ECONNRESET, 0); return 0; }
        errno = EINVAL; return -1;
    }
    if (request == USBDEVFS_REAPURBNDELAY || request == USBDEVFS_REAPURB) {
        fulfill(); if (!done_count) { errno = EAGAIN; return -1; }
        struct usbdevfs_urb *urb = done[done_head]; done_head = (done_head + 1) % MAX_LIVE; done_count--;
        for (unsigned i = 0; i < MAX_LIVE; i++) if (live[i].state == 2 && live[i].urb == urb) { live[i].state = 0; break; }
        *(void **)arg = urb; return 0;
    }
    if (request == USBDEVFS_BULK) {
        struct usbdevfs_bulktransfer *transfer = arg;
        if (!transfer || transfer->len > 64 || !transfer->data) { errno = EINVAL; return -1; }
        if (transfer->ep & 0x80) { size_t n = pop(transfer->ep, transfer->data, transfer->len); if (!n) { errno = EAGAIN; return -1; } return (int)n; }
        int status = out(transfer->ep, transfer->data, transfer->len); fulfill(); if (status) { errno = -status; return -1; } return (int)transfer->len;
    }
    errno = ENOTTY; if (log_file) { fprintf(log_file, "UNSUPPORTED_IOCTL %lx\n", request); fflush(log_file); } return -1;
}
int ioctl(int fd, unsigned long request, ...) {
    va_list ap; va_start(ap, request); void *arg = va_arg(ap, void *); va_end(ap);
    if (!real_ioctl) *(void **)(&real_ioctl) = dlsym(RTLD_NEXT, "ioctl");
    if (!active(fd)) return real_ioctl(fd, request, arg);
    pthread_mutex_lock(&gate); int result = backend(request, arg); pthread_mutex_unlock(&gate); return result;
}
__attribute__((destructor)) static void finish(void) {
    if (initialized && log_file) { fprintf(log_file, "SUMMARY wireOut=%u violations=%u loaderBytes=%zu programBytes=%zu\n", wire_out, violations, loader_length, program_length); fclose(log_file); }
}
