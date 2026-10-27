/* Unit-test-only kernel/memory doubles exercise the actual production broker.
 * This does not establish PRoot, Android, WCH-Link or physical flash behavior. */
#define _GNU_SOURCE 1
#include <assert.h>
#include <stdarg.h>
#include <sys/wait.h>
#define ioctl unit_ioctl
#include "../../../native/embedded/wch_usbfs.c"
#undef ioctl

static struct usbdevfs_urb *queue[64];
static unsigned queued, ioctl_count, discard_count, writes, unknown_once;
static word_t registers[4];
int read_data(const Tracee *tracee, void *destination, word_t source, word_t count) {
    (void)tracee; memcpy(destination,(const void *)source,count); return 0;
}
int write_data(Tracee *tracee, word_t destination, const void *source, word_t count) {
    (void)tracee; writes++; memcpy((void *)destination,source,count); return 0;
}
word_t peek_reg(const Tracee *tracee, RegVersion version, Reg reg) {
    (void)tracee; (void)version; return registers[reg];
}
int unit_ioctl(int fd, unsigned long request, ...) {
    assert(fd==3); ioctl_count++;
    va_list arguments; va_start(arguments,request); void *argument=va_arg(arguments,void *); va_end(arguments);
    if (request==USBDEVFS_SUBMITURB) {
        assert(queued<64); struct usbdevfs_urb *urb=argument;
        assert(urb->buffer_length>0 && urb->buffer_length<=64);
        urb->status=0; urb->actual_length=(urb->endpoint&0x80)?0:urb->buffer_length;
        queue[queued++]=urb; return 0;
    }
    if (request==USBDEVFS_DISCARDURB) {
        discard_count++;
        for (unsigned i=0;i<queued;i++) if (queue[i]==argument) {
            queue[i]->status=-ENOENT; queue[i]->actual_length=0; return 0;
        }
        errno=EINVAL; return -1;
    }
    if (request==USBDEVFS_REAPURBNDELAY) {
        if (unknown_once) { unknown_once=0; *(void **)argument=(void *)(uintptr_t)0x1234; return 0; }
        if (!queued) { errno=EAGAIN; return -1; }
        *(void **)argument=queue[0]; queued--;
        memmove(queue,queue+1,queued*sizeof(queue[0])); return 0;
    }
    assert(!"Unknown operation reached native test backend"); return -1;
}
static void begin(void) {
    assert(!queued);
    memset(transfers,0,sizeof(transfers));
    assert(wch_guard_init(&policy,"00300500")==0);
    writes=unknown_once=0;
}
static struct usbdevfs_urb *queued_at(unsigned index) { assert(index<queued); return queue[index]; }
int main(void) {
    Tracee tracee={.pid=getpid(),.tgid=getpid()};
    unsigned char command[]={0x81,0x0d,1,1};
    struct usbdevfs_urb output={.type=USBDEVFS_URB_TYPE_BULK,.endpoint=1,.buffer=command,.buffer_length=4,
        .usercontext=(void *)(uintptr_t)0x5678};
    void *completion=NULL;
    begin();
    assert(submit(&tracee,(uintptr_t)&output)==0);
    assert(queued_at(0)!=&output && queued_at(0)->buffer!=command);
    command[3]=0xff; /* An adversarial tracee cannot change already-approved bytes. */
    assert(!memcmp(queued_at(0)->buffer,"\x81\x0d\x01\x01",4));
    assert(reap(&tracee,(uintptr_t)&completion)==0 && completion==&output);
    assert(output.buffer==command && output.usercontext==(void *)(uintptr_t)0x5678 && output.actual_length==4);
    assert(policy.response==WCH_PROBE && !policy.poisoned);

    unsigned char first[64], second[64]; memset(first,0xa5,64); memset(second,0x5a,64);
    struct usbdevfs_urb in1={.type=USBDEVFS_URB_TYPE_BULK,.endpoint=0x81,.buffer=first,.buffer_length=64};
    struct usbdevfs_urb in2={.type=USBDEVFS_URB_TYPE_BULK,.endpoint=0x81,.buffer=second,.buffer_length=64};
    assert(submit(&tracee,(uintptr_t)&in1)==0 && submit(&tracee,(uintptr_t)&in2)==0);
    const unsigned char response[]={0x82,0x0d,4,2,9,2,0};
    memcpy(queued_at(0)->buffer,response,sizeof(response)); queued_at(0)->actual_length=sizeof(response);
    assert(discard(&tracee,(uintptr_t)&in2)==0);
    assert(reap(&tracee,(uintptr_t)&completion)==0 && completion==&in1 && in1.actual_length==7);
    assert(!memcmp(first,response,sizeof(response)));
    assert(reap(&tracee,(uintptr_t)&completion)==0 && completion==&in2 && in2.status==-ENOENT && !in2.actual_length);
    for (unsigned i=0;i<64;i++) assert(second[i]==0x5a);
    assert(!policy.poisoned);

    begin(); policy.response=WCH_PROBE;
    assert(submit(&tracee,(uintptr_t)&in1)==0 && submit(&tracee,(uintptr_t)&in2)==0);
    unsigned before_writes=writes, before_discards=discard_count;
    cleanup();
    assert(!queued && discard_count==before_discards+2 && writes==before_writes);
    for (unsigned i=0;i<MAX_URBS;i++) assert(!transfers[i].active);

    begin(); policy.response=WCH_PROBE;
    assert(submit(&tracee,(uintptr_t)&in1)==0);
    unknown_once=1; before_writes=writes;
    assert(reap(&tracee,(uintptr_t)&completion)==-EIO && policy.poisoned && writes==before_writes);
    cleanup(); assert(!queued);

    begin(); policy.response=WCH_PROBE;
    assert(submit(&tracee,(uintptr_t)&in1)==0);
    pid_t child=fork(); assert(child>=0);
    if (!child) { unknown_once=1; cleanup(); _exit(1); }
    int status=0; assert(waitpid(child,&status,0)==child && WIFEXITED(status) && WEXITSTATUS(status)==79);
    cleanup();

    begin();
    setenv("TABSTUDIO_USB_WCH_GUARD","ch32v003-v1",1);
    setenv("TABSTUDIO_USB_FD","3",1); setenv("TABSTUDIO_WCH_CHIP_ID","00300500",1);
    registers[SYSARG_1]=(word_t)-1; registers[SYSARG_2]=USBDEVFS_RESET;
    long result=0; unsigned before_ioctl=ioctl_count;
    assert(tabstudio_wch_ioctl(&tracee,&result)==1 && result==-EPERM && ioctl_count==before_ioctl);
    assert(policy.poisoned); /* Failed fd identity checks never reach raw USBFS. */
    puts("WCH_NATIVE_BROKER_OWNED_BUFFER_CANCEL_DRAIN_PASSED synthetic=true hardware=false");
    return 0;
}
