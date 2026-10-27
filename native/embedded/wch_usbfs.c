/* SPDX-License-Identifier: GPL-2.0-or-later
 * PRoot's selected-device WCH broker. The tracer, not tracee memory, owns every
 * submitted USB buffer until actual kernel completion. This prevents another
 * client thread mutating an allowed command after validation (TOCTOU).
 * There is no production virtual USB implementation or successful-I/O fallback.
 */
#ifndef _GNU_SOURCE
#define _GNU_SOURCE 1
#endif
#include "wch_usbfs.h"
#include "wch_guard.h"
#include "tracee/mem.h"
#include "tracee/reg.h"
#include "syscall/sysnum.h"
#include <linux/usbdevice_fs.h>
#include <sys/ioctl.h>
#include <sys/stat.h>
#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <time.h>

#define MAX_URBS 32
struct owned_urb {
    struct usbdevfs_urb urb, original;
    unsigned char bytes[64];
    uintptr_t address;
    pid_t tgid;
    int active, cancelled;
    struct wch_guard after;
};
static struct owned_urb transfers[MAX_URBS];
static struct wch_guard policy;
static int initialized, initialization_error;
static pid_t owner;

__attribute__((constructor)) static void guard_version(void) {
    const char *check=getenv("TABSTUDIO_WCH_GUARD_CHECK");
    if (check && !strcmp(check,"1")) {
        if (sizeof(void*)!=8) _exit(78);
        static const char marker[]="TABSTUDIO_WCH_GUARD_V1\n";
        ssize_t count=write(STDOUT_FILENO,marker,sizeof(marker)-1);
        _exit(count==(ssize_t)sizeof(marker)-1?0:1);
    }
}

static long real_ioctl(unsigned long request, void *argument) {
    int result=ioctl(3,request,argument);
    return result<0?-errno:result;
}
static struct owned_urb *completed_slot(const struct usbdevfs_urb *completed) {
    for (unsigned i=0;i<MAX_URBS;i++)
        if (transfers[i].active && completed==&transfers[i].urb) return &transfers[i];
    return NULL;
}
static void cleanup(void) {
    unsigned pending=0;
    for (unsigned i=0;i<MAX_URBS;i++)
        if (transfers[i].active) {
            pending++;
            (void)ioctl(3,USBDEVFS_DISCARDURB,&transfers[i].urb);
        }
    /* FD3 is a duplicate of the Android app's still-open grant. Closing only
     * our duplicate does NOT cancel that open-file-description's URBs. Drain
     * every owned completion before these tracer-owned buffers disappear.
     * The app always closes the exclusive grant when this worker ends, and
     * must never reuse it (including SIGKILL, which cannot run this hook). */
    struct timespec start, now, pause={.tv_nsec=1000000};
    if (clock_gettime(CLOCK_MONOTONIC,&start)<0 && pending) _exit(79);
    while (pending) {
        struct usbdevfs_urb *completed=NULL;
        int result=ioctl(3,USBDEVFS_REAPURBNDELAY,&completed);
        if (result==0) {
            struct owned_urb *slot=completed_slot(completed);
            if (!slot) { wch_guard_poison(&policy); _exit(79); }
            slot->active=0; pending--; continue;
        }
        if (errno!=EAGAIN && errno!=EINTR) { wch_guard_poison(&policy); _exit(79); }
        if (clock_gettime(CLOCK_MONOTONIC,&now)<0 || now.tv_sec-start.tv_sec>=3) {
            wch_guard_poison(&policy); _exit(79);
        }
        (void)nanosleep(&pause,NULL);
    }
}
static int selected_fd(Tracee *t, int fd) {
    struct stat expected, actual;
    char path[80];
    if (fd<0 || fstat(3,&expected)!=0) return 0;
    int size=snprintf(path,sizeof(path),"/proc/%d/fd/%d",t->pid,fd);
    if (size<0 || size>=(int)sizeof(path) || stat(path,&actual)!=0) return 0;
    return expected.st_dev==actual.st_dev && expected.st_ino==actual.st_ino
        && expected.st_rdev==actual.st_rdev && (expected.st_mode&S_IFMT)==(actual.st_mode&S_IFMT);
}
static struct owned_urb *find(uintptr_t address, pid_t tgid) {
    for (unsigned i=0;i<MAX_URBS;i++)
        if (transfers[i].active && transfers[i].address==address && transfers[i].tgid==tgid) return &transfers[i];
    return NULL;
}
static int out_pending(void) {
    for (unsigned i=0;i<MAX_URBS;i++)
        if (transfers[i].active && !(transfers[i].urb.endpoint&0x80)) return 1;
    return 0;
}
static long bad(int error) { wch_guard_poison(&policy); return -error; }

static long ensure_readable(unsigned ep, const unsigned char *bytes, size_t length) {
    if (policy.read_ok || ep!=1 || length!=11 || bytes[0]!=0x81 || bytes[1]!=3 || bytes[2]!=8) return 0;
    /* Stock wlink dump does not query protection. Validate its complete bounded
     * read first, then make actual read-only protocol transactions ourselves.
     * No invented USB completion or response is exposed to the client. */
    struct wch_guard checked=policy;
    checked.read_ok=1;
    if (wch_guard_out(&checked,ep,bytes,length)<0) return bad(EPERM);
    for (unsigned i=0;i<MAX_URBS;i++) if (transfers[i].active) return bad(EBUSY);
    unsigned char command[]={0x81,6,1,1}, response[64];
    struct wch_guard next=policy;
    if (wch_guard_out(&next,1,command,sizeof(command))<0) return bad(EPERM);
    struct usbdevfs_bulktransfer output={.ep=1,.len=sizeof(command),.timeout=3000,.data=command};
    long result=real_ioctl(USBDEVFS_BULK,&output);
    if (result!=(long)sizeof(command)) return bad(result<0?(int)-result:EIO);
    policy=next;
    struct usbdevfs_bulktransfer input={.ep=0x81,.len=sizeof(response),.timeout=3000,.data=response};
    result=real_ioctl(USBDEVFS_BULK,&input);
    if (result<=0 || result>(long)sizeof(response)) return bad(result<0?(int)-result:EIO);
    return wch_guard_in(&policy,0x81,response,(size_t)result)<0?bad(EPERM):0;
}

static long submit(Tracee *t, uintptr_t address) {
    struct owned_urb *slot=NULL;
    pid_t tgid=t->tgid?t->tgid:t->pid;
    if (!address || find(address,tgid)) return bad(EINVAL);
    for (unsigned i=0;i<MAX_URBS;i++) if (!transfers[i].active) { slot=&transfers[i]; break; }
    if (!slot) return bad(ENOSPC);
    memset(slot,0,sizeof(*slot));
    if (read_data(t,&slot->original,address,sizeof(slot->original))<0) return bad(EFAULT);
    const struct usbdevfs_urb *u=&slot->original;
    if (u->type!=USBDEVFS_URB_TYPE_BULK || u->flags!=0 || u->buffer_length<=0 || u->buffer_length>64
        || !u->buffer || u->number_of_packets || u->signr || u->start_frame) return bad(EPERM);
    slot->after=policy;
    int incoming=(u->endpoint&0x80)!=0;
    if (incoming) {
        if (wch_guard_input_allowed(&policy,u->endpoint,(size_t)u->buffer_length)<0) return bad(EPERM);
    } else {
        if (out_pending()) return bad(EBUSY);
        if (read_data(t,slot->bytes,(uintptr_t)u->buffer,(size_t)u->buffer_length)<0) return bad(EFAULT);
        long preflight=ensure_readable(u->endpoint,slot->bytes,(size_t)u->buffer_length);
        if (preflight<0) return preflight;
        slot->after=policy;
        if (wch_guard_out(&slot->after,u->endpoint,slot->bytes,(size_t)u->buffer_length)<0) return bad(EPERM);
    }
    slot->urb=*u; slot->urb.buffer=slot->bytes;
    slot->urb.status=0; slot->urb.actual_length=0; slot->urb.error_count=0;
    slot->address=address; slot->tgid=tgid;
    long result=real_ioctl(USBDEVFS_SUBMITURB,&slot->urb);
    if (result<0) return bad((int)-result);
    slot->active=1;
    return result;
}
static long discard(Tracee *t, uintptr_t address) {
    struct owned_urb *slot=find(address,t->tgid?t->tgid:t->pid);
    if (!slot) return bad(EINVAL);
    if (!(slot->urb.endpoint&0x80)) wch_guard_poison(&policy);
    long result=real_ioctl(USBDEVFS_DISCARDURB,&slot->urb);
    if (result>=0) slot->cancelled=1;
    return result;
}
static long reap(Tracee *t, uintptr_t address) {
    if (!address) return bad(EFAULT);
    struct usbdevfs_urb *completed=NULL;
    long result=real_ioctl(USBDEVFS_REAPURBNDELAY,&completed);
    if (result<0) return result;
    struct owned_urb *slot=completed_slot(completed);
    if (!slot || slot->tgid!=(t->tgid?t->tgid:t->pid)) return bad(EIO);
    int incoming=(slot->urb.endpoint&0x80)!=0;
    int valid=slot->urb.actual_length>=0 && slot->urb.actual_length<=slot->original.buffer_length;
    if (!valid) {
        slot->urb.status=-EIO; slot->urb.actual_length=0; wch_guard_poison(&policy);
    } else if (slot->cancelled && incoming && slot->urb.actual_length==0) {
        /* Cancelled read-ahead receives no fabricated response or state update. */
    } else if (slot->urb.status!=0) {
        wch_guard_poison(&policy);
    } else if (incoming) {
        if (wch_guard_in(&policy,slot->urb.endpoint,slot->bytes,(size_t)slot->urb.actual_length)<0) {
            slot->urb.status=-EPERM; slot->urb.actual_length=0;
        }
    } else if (slot->urb.actual_length!=slot->original.buffer_length || policy.poisoned) {
        slot->urb.status=-EIO; wch_guard_poison(&policy);
    } else {
        policy=slot->after;
    }
    struct usbdevfs_urb answer=slot->original;
    answer.status=slot->urb.status; answer.actual_length=slot->urb.actual_length; answer.error_count=slot->urb.error_count;
    uintptr_t original=slot->address;
    if (incoming && answer.actual_length && write_data(t,(uintptr_t)answer.buffer,slot->bytes,(size_t)answer.actual_length)<0)
        result=bad(EFAULT);
    if (write_data(t,original,&answer,sizeof(answer))<0 || write_data(t,address,&original,sizeof(original))<0)
        result=bad(EFAULT);
    slot->active=0;
    return result;
}
static long bulk(Tracee *t, uintptr_t address) {
    struct usbdevfs_bulktransfer request;
    unsigned char bytes[64];
    if (read_data(t,&request,address,sizeof(request))<0) return bad(EFAULT);
    if (!request.data || !request.len || request.len>64 || !request.timeout || request.timeout>5000 || out_pending()) return bad(EPERM);
    int incoming=(request.ep&0x80)!=0;
    struct wch_guard after=policy;
    if (incoming) {
        if (wch_guard_input_allowed(&policy,request.ep,request.len)<0) return bad(EPERM);
    } else {
        if (read_data(t,bytes,(uintptr_t)request.data,request.len)<0) return bad(EFAULT);
        long preflight=ensure_readable(request.ep,bytes,request.len);
        if (preflight<0) return preflight;
        after=policy;
        if (wch_guard_out(&after,request.ep,bytes,request.len)<0) return bad(EPERM);
    }
    void *destination=request.data; request.data=bytes;
    long result=real_ioctl(USBDEVFS_BULK,&request);
    if (result<0 || (unsigned long)result>request.len) return bad(result<0?(int)-result:EIO);
    if (incoming) {
        if (wch_guard_in(&policy,request.ep,bytes,(size_t)result)<0) return -EPERM;
        if (write_data(t,(uintptr_t)destination,bytes,(size_t)result)<0) return bad(EFAULT);
    } else {
        if ((unsigned long)result!=request.len) return bad(EIO);
        policy=after;
    }
    return result;
}
static long control(Tracee *t, uintptr_t address) {
    struct usbdevfs_ctrltransfer request;
    unsigned char data[4096];
    if (read_data(t,&request,address,sizeof(request))<0) return bad(EFAULT);
    if (request.bRequestType!=0x80 || !request.data || !request.wLength || request.wLength>sizeof(data)
        || !request.timeout || request.timeout>5000) return bad(EPERM);
    if (request.bRequest==8) {
        if (request.wValue || request.wIndex || request.wLength!=1) return bad(EPERM);
    } else if (request.bRequest==6) {
        unsigned kind=request.wValue>>8;
        if (kind<1 || kind>3) return bad(EPERM);
    } else return bad(EPERM);
    void *destination=request.data; request.data=data;
    long result=real_ioctl(USBDEVFS_CONTROL,&request);
    if (result<0) return result;
    if ((unsigned long)result>request.wLength) return bad(EIO);
    if (write_data(t,(uintptr_t)destination,data,(size_t)result)<0) return bad(EFAULT);
    return result;
}
int tabstudio_wch_ioctl(Tracee *t, long *result) {
    const char *mode=getenv("TABSTUDIO_USB_WCH_GUARD");
    if (!mode) return 0;
    int fd=(int)peek_reg(t,CURRENT,SYSARG_1);
    uint32_t request=(uint32_t)peek_reg(t,CURRENT,SYSARG_2);
    int selected=selected_fd(t,fd);
    /* A procfs/stat failure must never fall through to unguarded USB I/O.
     * This worker has exactly one granted USB OFD; all USBFS requests on any
     * other/unidentifiable descriptor are refused, including inherited FD3.
     * Unrelated terminal/socket ioctls retain their ordinary PRoot handling. */
    if (!selected && _IOC_TYPE(request)!='U') return 0;
    if (!initialized) {
        initialized=1;
        if (!getenv("TABSTUDIO_USB_FD") || strcmp(getenv("TABSTUDIO_USB_FD"),"3")) initialization_error=-EINVAL;
        else if (!strcmp(mode,"ch32v003-v1")) initialization_error=wch_guard_init(&policy,getenv("TABSTUDIO_WCH_CHIP_ID"));
        else if (!strcmp(mode,"ch32v003-read-only-v1")) initialization_error=wch_guard_init_read_only_bound(&policy,getenv("TABSTUDIO_WCH_CHIP_ID"));
        else if (!strcmp(mode,"ch32v003-reset-v1")) initialization_error=wch_guard_init_reset(&policy,getenv("TABSTUDIO_WCH_CHIP_ID"));
        else initialization_error=-EINVAL;
        atexit(cleanup);
    }
    *result=initialization_error;
    if (*result<0) return 1;
#if defined(ARCH_ARM64) || defined(ARCH_X86_64)
    if (is_32on64_mode(t)) { *result=bad(ENOTSUP); return 1; }
#endif
    if (sizeof(void*)!=8) { *result=bad(ENOTSUP); return 1; }
    pid_t group=t->tgid?t->tgid:t->pid;
    if (!owner) owner=group;
    if (owner!=group) { *result=bad(EPERM); return 1; }
    if (!selected) { *result=bad(EPERM); return 1; }
    uintptr_t argument=peek_reg(t,CURRENT,SYSARG_3);
    if (request==(uint32_t)USBDEVFS_SUBMITURB) *result=submit(t,argument);
    else if (request==(uint32_t)USBDEVFS_DISCARDURB) *result=discard(t,argument);
    else if (request==(uint32_t)USBDEVFS_REAPURBNDELAY) *result=reap(t,argument);
    else if (request==(uint32_t)USBDEVFS_BULK) *result=bulk(t,argument);
    else if (request==(uint32_t)USBDEVFS_CONTROL) *result=control(t,argument);
    else if (request==(uint32_t)USBDEVFS_CLAIMINTERFACE || request==(uint32_t)USBDEVFS_RELEASEINTERFACE) {
        unsigned int interface=1;
        if (read_data(t,&interface,argument,sizeof(interface))<0) *result=bad(EFAULT);
        else *result=interface?bad(EPERM):real_ioctl(request,&interface);
    } else if (request==(uint32_t)USBDEVFS_GET_CAPABILITIES) {
        unsigned int capabilities=0;
        *result=real_ioctl(request,&capabilities);
        if (*result>=0 && write_data(t,argument,&capabilities,sizeof(capabilities))<0) *result=bad(EFAULT);
    } else if (request==(uint32_t)USBDEVFS_GET_SPEED) *result=real_ioctl(request,NULL);
    else *result=bad(EPERM);
    return 1;
}
