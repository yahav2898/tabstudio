/* SPDX-License-Identifier: MIT
 * Independent synthetic libusb transport for the real distro HIDAPI backend.
 * No real USB nodes are enumerated or opened. Only explicitly borrowed fd 3
 * is accepted. Descriptor/report bytes below are fixtures, not captures.
 */
#define _GNU_SOURCE
#include <libusb-1.0/libusb.h>
#include <assert.h>
#include <fcntl.h>
#include <pthread.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

struct libusb_context { int handles; };
struct libusb_device { libusb_context *context; int references; };
struct libusb_device_handle { libusb_device *device; };
static pthread_mutex_t gate = PTHREAD_MUTEX_INITIALIZER;
static struct libusb_transfer *pending;
static int cancelled, emitted, output_reports;
static const unsigned char report_descriptor[] = {
    0x06,0x00,0xff, 0x09,0x01, 0xa1,0x01, 0x15,0x00, 0x26,0xff,0x00,
    0x75,0x08, 0x95,0x40, 0x09,0x01, 0x81,0x02,
    0x95,0x40, 0x09,0x01, 0x91,0x02, 0xc0
};
static const unsigned char hid_extra[] = {9,0x21,0x11,0x01,0,1,0x22,sizeof(report_descriptor),0};
static const struct libusb_endpoint_descriptor endpoints[] = {
    {.bLength=7,.bDescriptorType=5,.bEndpointAddress=0x81,.bmAttributes=3,.wMaxPacketSize=64,.bInterval=1},
    {.bLength=7,.bDescriptorType=5,.bEndpointAddress=0x01,.bmAttributes=3,.wMaxPacketSize=64,.bInterval=1}
};
static const struct libusb_interface_descriptor alternate = {
    .bLength=9,.bDescriptorType=4,.bInterfaceNumber=0,.bAlternateSetting=0,.bNumEndpoints=2,
    .bInterfaceClass=3,.bInterfaceSubClass=0,.bInterfaceProtocol=0,.endpoint=endpoints,
    .extra=hid_extra,.extra_length=sizeof(hid_extra)
};
static const struct libusb_interface interface = {.altsetting=&alternate,.num_altsetting=1};
static struct libusb_interface_descriptor control_alternate;
static struct libusb_interface control_interface;
static struct libusb_config_descriptor control_configuration;
static const struct libusb_config_descriptor configuration = {
    .bLength=9,.bDescriptorType=2,.wTotalLength=41,.bNumInterfaces=1,.bConfigurationValue=1,
    .bmAttributes=0x80,.MaxPower=50,.interface=&interface
};

int libusb_init_context(libusb_context **context, const struct libusb_init_option options[], int count) {
    int discovery_disabled=0;
    for(int i=0;i<count;i++) if(options[i].option==LIBUSB_OPTION_NO_DEVICE_DISCOVERY) discovery_disabled=1;
    assert(context && discovery_disabled);
    *context=calloc(1,sizeof(**context));
    return *context ? 0 : LIBUSB_ERROR_NO_MEM;
}
int libusb_init(libusb_context **context) {(void)context;abort();}
void libusb_exit(libusb_context *context) {assert(context && context->handles==0);free(context);}
int libusb_wrap_sys_device(libusb_context *context,intptr_t fd,libusb_device_handle **output) {
    assert(context && fd==3 && fcntl(3,F_GETFD)>=0);
    *output=calloc(1,sizeof(**output));assert(*output);
    (*output)->device=calloc(1,sizeof(*(*output)->device));assert((*output)->device);
    (*output)->device->context=context;(*output)->device->references=1;context->handles++;
    return 0;
}
libusb_device *libusb_get_device(libusb_device_handle *handle) {return handle->device;}
libusb_device *libusb_ref_device(libusb_device *device) {device->references++;return device;}
void libusb_unref_device(libusb_device *device) {if(--device->references==0)free(device);}
void libusb_close(libusb_device_handle *handle) {handle->device->context->handles--;libusb_unref_device(handle->device);free(handle);}
void libusb_free_device_list(libusb_device **devices,int unref) {
    if(unref)for(size_t i=0;devices[i];i++)libusb_unref_device(devices[i]);
    free(devices);
}
ssize_t libusb_get_device_list(libusb_context *context,libusb_device ***devices) {(void)context;(void)devices;abort();}
int libusb_open(libusb_device *device,libusb_device_handle **handle) {(void)device;(void)handle;abort();}
int libusb_get_device_descriptor(libusb_device *device,struct libusb_device_descriptor *descriptor) {
    assert(device);*descriptor=(struct libusb_device_descriptor){.bLength=18,.bDescriptorType=1,.bcdUSB=0x0200,
        .bMaxPacketSize0=64,.idVendor=0xcafe,.idProduct=0xd00d,.bcdDevice=0x0100,
        .iManufacturer=1,.iProduct=2,.iSerialNumber=3,.bNumConfigurations=1};return 0;
}
int libusb_get_config_descriptor(libusb_device *device,uint8_t index,struct libusb_config_descriptor **result) {
    assert(device && index==0);
    if(getenv("TABSTUDIO_TEST_HID_CONTROL_ONLY")) {
        control_alternate=alternate;control_alternate.bNumEndpoints=1;
        control_interface=(struct libusb_interface){.altsetting=&control_alternate,.num_altsetting=1};
        control_configuration=configuration;control_configuration.interface=&control_interface;
        *result=&control_configuration;
    } else *result=(struct libusb_config_descriptor *)&configuration;
    return 0;
}
int libusb_get_active_config_descriptor(libusb_device *device,struct libusb_config_descriptor **result) {return libusb_get_config_descriptor(device,0,result);}
void libusb_free_config_descriptor(struct libusb_config_descriptor *value) {assert(value==&configuration || value==&control_configuration);}
uint8_t libusb_get_bus_number(libusb_device *device) {assert(device);return 1;}
uint8_t libusb_get_device_address(libusb_device *device) {assert(device);return 2;}
int libusb_get_port_numbers(libusb_device *device,uint8_t *ports,int length) {assert(device && length>0);ports[0]=2;return 1;}
int libusb_kernel_driver_active(libusb_device_handle *handle,int number) {assert(handle && number==0);return 0;}
int libusb_detach_kernel_driver(libusb_device_handle *handle,int number) {assert(handle && number==0);return 0;}
int libusb_attach_kernel_driver(libusb_device_handle *handle,int number) {assert(handle && number==0);return 0;}
int libusb_claim_interface(libusb_device_handle *handle,int number) {assert(handle && number==0);return 0;}
int libusb_release_interface(libusb_device_handle *handle,int number) {assert(handle && number==0);return 0;}
int libusb_set_interface_alt_setting(libusb_device_handle *handle,int number,int alternate_setting) {assert(handle && number==0 && alternate_setting==0);return 0;}
int libusb_set_auto_detach_kernel_driver(libusb_device_handle *handle,int enabled) {assert(handle);(void)enabled;return 0;}
const char *libusb_error_name(int code) {(void)code;return "synthetic USB transport";}
int libusb_control_transfer(libusb_device_handle *handle,uint8_t kind,uint8_t request,uint16_t value,
    uint16_t index,unsigned char *data,uint16_t length,unsigned int timeout) {
    assert(handle);(void)index;(void)timeout;
    if((kind&0x80) && request==6 && (value>>8)==3) {
        if((value&255)==0) {assert(length>=4);memcpy(data,(unsigned char[]){4,3,9,4},4);return 4;}
        const char *text=(value&255)==1?"TabStudio synthetic":(value&255)==2?"CMSIS-DAP synthetic":"FD3ONLY";
        unsigned n=(unsigned)strlen(text);unsigned size=2+2*n;if(size>length)size=length;
        if(size<2)return LIBUSB_ERROR_INVALID_PARAM;
        data[0]=(unsigned char)(2+2*n);data[1]=3;
        for(unsigned i=2;i<size;i++)data[i]=(i&1)?0:(unsigned char)text[(i-2)/2];
        return (int)size;
    }
    if((kind&0x80) && request==6 && (value>>8)==0x22) {
        unsigned size=sizeof(report_descriptor);if(size>length)size=length;
        memcpy(data,report_descriptor,size);return (int)size;
    }
    if(kind==0x21 && request==9 && (value>>8)==2) {
        assert(length==64 && data[0]==2);output_reports++;return length;
    }
    return LIBUSB_ERROR_NOT_SUPPORTED;
}
int libusb_interrupt_transfer(libusb_device_handle *handle,unsigned char endpoint,unsigned char *data,
    int length,int *actual,unsigned timeout) {
    assert(handle && endpoint==1 && length==64 && data[0]==2);(void)timeout;
    output_reports++;*actual=length;return 0;
}
struct libusb_transfer *libusb_alloc_transfer(int packets) {assert(packets==0);return calloc(1,sizeof(struct libusb_transfer));}
void libusb_free_transfer(struct libusb_transfer *transfer) {free(transfer);}
int libusb_submit_transfer(struct libusb_transfer *transfer) {
    pthread_mutex_lock(&gate);pending=transfer;pthread_mutex_unlock(&gate);return 0;
}
int libusb_cancel_transfer(struct libusb_transfer *transfer) {
    pthread_mutex_lock(&gate);pending=transfer;cancelled=1;pthread_mutex_unlock(&gate);return 0;
}
int libusb_handle_events(libusb_context *context) {
    assert(context);struct libusb_transfer *deliver=NULL;
    pthread_mutex_lock(&gate);
    if(pending && (cancelled || !emitted)) {
        deliver=pending;pending=NULL;
        if(cancelled) {deliver->status=LIBUSB_TRANSFER_CANCELLED;deliver->actual_length=0;}
        else {emitted=1;deliver->status=LIBUSB_TRANSFER_COMPLETED;deliver->actual_length=64;memset(deliver->buffer,0x42,64);}
    }
    pthread_mutex_unlock(&gate);
    if(deliver)deliver->callback(deliver);
    else usleep(1000);
    return 0;
}
int libusb_handle_events_completed(libusb_context *context,int *completed) {
    if(!*completed)return libusb_handle_events(context);
    return 0;
}
int test_usb_report_writes(void) {return output_reports;}
