/* SPDX-License-Identifier: MIT
 * Deliberately linked to the distro's actual hidapi-hidraw backend. The same
 * unchanged executable must enumerate/open/read/write the permissioned device
 * only after the real hidapi-libusb backend and TabStudio FD bridge preload.
 */
#define _GNU_SOURCE
#include <hidapi/hidapi.h>
#include <assert.h>
#include <dlfcn.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(int argc,char **argv) {
    (void)argv;
    const char *symbols[]={"hid_init","hid_exit","hid_enumerate","hid_free_enumeration",
        "hid_open_path","hid_close","hid_read_timeout","hid_write","hid_version_str"};
    for(unsigned i=0;i<sizeof(symbols)/sizeof(symbols[0]);i++) {
        Dl_info info={0};void *symbol=dlsym(RTLD_DEFAULT,symbols[i]);
        assert(symbol && dladdr(symbol,&info) && info.dli_fname);
        assert(strstr(info.dli_fname,argc>1?"hidapi-hidraw":"hidapi-libusb"));
    }
    if(argc>1) {printf("ORIGINAL_CLIENT_LINKED_HIDRAW version=%s\n",hid_version_str());return 0;}
    assert(getenv("TABSTUDIO_EXPECT_HID_VERSION") && strcmp(getenv("TABSTUDIO_EXPECT_HID_VERSION"),hid_version_str())==0);
    assert(hid_init()==0);
    struct hid_device_info *devices=hid_enumerate(0xcafe,0xd00d);
    assert(devices && !devices->next && devices->interface_number==0);
    assert(devices->vendor_id==0xcafe && devices->product_id==0xd00d);
    hid_device *handle=hid_open_path(devices->path);assert(handle);
    unsigned char output[65]={0};output[1]=2; /* report ID 0, synthetic DAP_Connect byte */
    assert(hid_write(handle,output,sizeof(output))==(int)sizeof(output));
    unsigned char input[64]={0};
    assert(hid_read_timeout(handle,input,sizeof(input),1000)==64 && input[0]==0x42);
    int (*writes)(void)=dlsym(RTLD_DEFAULT,"test_usb_report_writes");
    assert(writes && writes()==1);
    hid_close(handle);hid_free_enumeration(devices);assert(hid_exit()==0);
    assert(fcntl(3,F_GETFD)>=0); /* The Android parent still owns the borrowed fd. */
    printf("HIDAPI_LIBUSB_GRANTED_FD_PASSED version=%s hardware=false\n",hid_version_str());
    return 0;
}
