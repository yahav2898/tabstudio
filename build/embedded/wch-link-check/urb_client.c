#define _GNU_SOURCE
/* Synthetic raw USBFS client: static build deliberately bypasses libc preload in the tracee. */
#include <errno.h>
#include <fcntl.h>
#include <linux/usbdevice_fs.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/syscall.h>
#include <unistd.h>

static int usb_ioctl(int fd, unsigned long command, void *argument) {
#ifdef NATIVE_BACKEND_UNIT
    return ioctl(fd, command, argument);
#else
    return (int)syscall(SYS_ioctl, fd, command, argument);
#endif
}
static void require(int condition, const char *message) { if (!condition) { fprintf(stderr, "%s errno=%d\n", message, errno); exit(1); } }
static void denied(int fd, unsigned char *bytes, int length) {
    struct usbdevfs_urb urb = {.type=USBDEVFS_URB_TYPE_BULK, .endpoint=1, .buffer=bytes, .buffer_length=length};
    if (usb_ioctl(fd, USBDEVFS_SUBMITURB, &urb) < 0) { printf("WCH_RAW_UNSAFE_COMMAND_REFUSED errno=%d\n", errno); return; }
    void *completed = NULL;
    require(usb_ioctl(fd, USBDEVFS_REAPURBNDELAY, &completed) == 0 && completed == &urb && urb.status < 0, "unsafe transfer was not rejected");
    printf("WCH_RAW_UNSAFE_COMMAND_REFUSED asynchronous=%d\n", urb.status);
}
int main(int argc, char **argv) {
    require(argc == 2, "choose queue-cancel, fragment, coalesced or unprotect");
#ifdef NATIVE_BACKEND_UNIT
    char pid[32]; snprintf(pid, sizeof pid, "%ld", (long)getpid()); setenv("TABSTUDIO_TEST_TRACER_PID", pid, 1);
    int fd = 3;
#else
    int fd = open("/dev/bus/usb/001/002", O_RDWR | O_CLOEXEC);
    require(fd >= 0, "open permissioned USB node");
#endif
    uint32_t caps = 0x55;
    require(usb_ioctl(fd, USBDEVFS_GET_CAPABILITIES, &caps) == 0 && caps == 0, "real native backend capability routing");
    if (strcmp(argv[1], "fragment") == 0) { unsigned char p[] = {0x81,6}; denied(fd,p,sizeof p); return 0; }
    if (strcmp(argv[1], "coalesced") == 0) { unsigned char p[] = {0x81,6,1,1,0x81,6,1,2}; denied(fd,p,sizeof p); return 0; }
    if (strcmp(argv[1], "unprotect") == 0) { unsigned char p[] = {0x81,6,1,2}; denied(fd,p,sizeof p); return 0; }
    require(strcmp(argv[1], "queue-cancel") == 0, "unknown fixture mode");
    unsigned char command[] = {0x81,0x0d,1,1}, response1[64], response2[64];
    memset(response1,0xa5,sizeof response1); memset(response2,0x5a,sizeof response2);
    struct usbdevfs_urb out = {.type=USBDEVFS_URB_TYPE_BULK,.endpoint=1,.buffer=command,.buffer_length=sizeof command,.usercontext=(void *)(uintptr_t)0x1111};
    struct usbdevfs_urb in1 = {.type=USBDEVFS_URB_TYPE_BULK,.endpoint=0x81,.buffer=response1,.buffer_length=64,.usercontext=(void *)(uintptr_t)0x2222};
    struct usbdevfs_urb in2 = {.type=USBDEVFS_URB_TYPE_BULK,.endpoint=0x81,.buffer=response2,.buffer_length=64,.usercontext=(void *)(uintptr_t)0x3333};
    require(usb_ioctl(fd,USBDEVFS_SUBMITURB,&out)==0,"submit OUT");
    void *out_completed=NULL;
    require(usb_ioctl(fd,USBDEVFS_REAPURBNDELAY,&out_completed)==0&&out_completed==&out&&out.status==0&&out.actual_length==4,"reap completed OUT before response queue");
    require(out.buffer==command&&out.usercontext==(void *)(uintptr_t)0x1111,"OUT identity/context");
    require(usb_ioctl(fd,USBDEVFS_SUBMITURB,&in1)==0,"queue IN1");
    require(usb_ioctl(fd,USBDEVFS_SUBMITURB,&in2)==0,"queue IN2");
    require(usb_ioctl(fd,USBDEVFS_DISCARDURB,&in2)==0,"cancel pending IN2");
    unsigned seen=1;
    for(unsigned i=0;i<2;i++) {
        void *completed=NULL;
        require(usb_ioctl(fd,USBDEVFS_REAPURBNDELAY,&completed)==0,"reap original URB");
        if(completed==&out) { require(!(seen&1)&&out.status==0&&out.actual_length==4&&out.buffer==command&&out.usercontext==(void *)(uintptr_t)0x1111,"OUT identity/status"); seen|=1; }
        else if(completed==&in1) { require(!(seen&2)&&in1.status==0&&in1.actual_length==7&&in1.buffer==response1&&in1.usercontext==(void *)(uintptr_t)0x2222,"IN1 identity/status"); require(response1[0]==0x82&&response1[1]==0x0d&&response1[2]==4,"IN1 response bytes"); seen|=2; }
        else if(completed==&in2) { require(!(seen&4)&&in2.status<0&&in2.actual_length==0&&in2.buffer==response2&&in2.usercontext==(void *)(uintptr_t)0x3333,"cancelled IN2 identity/status"); for(unsigned j=0;j<64;j++) require(response2[j]==0x5a,"cancelled buffer overwritten"); seen|=4; }
        else require(0,"reap returned an unknown/native pointer");
    }
    require(seen==7,"missing completion");
    void *none=NULL; require(usb_ioctl(fd,USBDEVFS_REAPURBNDELAY,&none)<0&&errno==EAGAIN,"empty completion queue");
    puts("WCH_RAW_QUEUED_CANCEL_REAP_PASSED"); return 0;
}
