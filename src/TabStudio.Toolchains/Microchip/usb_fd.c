/* Permissioned libusb view for existing programmer CLIs. No USB enumeration.
 * Built natively for Alpine; LD_PRELOAD is scoped to the programmer process.
 * The Android parent owns fd 3 until that process exits. */
#include <libusb-1.0/libusb.h>
#include <stdlib.h>
#include <stdint.h>
#include <errno.h>
#include <dlfcn.h>

static libusb_context *permission_context;

static int permitted_fd(void) {
    const char *text = getenv("TABSTUDIO_USB_FD");
    char *end;
    if (!text || !*text) return -1;
    errno = 0;
    long fd = strtol(text, &end, 10);
    return errno || *end || fd != 3 ? -1 : (int)fd;
}

int libusb_init_context(libusb_context **context, const struct libusb_init_option original[], int count) {
    if (permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    if (count < 0 || count > 16) return LIBUSB_ERROR_INVALID_PARAM;
    struct libusb_init_option options[17];
    int selected = 0;
    for (int i = 0; i < count; i++) if (original[i].option != LIBUSB_OPTION_NO_DEVICE_DISCOVERY) options[selected++] = original[i];
    options[selected++] = (struct libusb_init_option){ .option = LIBUSB_OPTION_NO_DEVICE_DISCOVERY };
    int (*initialize)(libusb_context **, const struct libusb_init_option *, int) = dlsym(RTLD_NEXT, "libusb_init_context");
    if (!initialize) return LIBUSB_ERROR_NOT_SUPPORTED;
    int result = initialize(context, options, selected);
    if (result == 0) permission_context = context ? *context : NULL;
    return result;
}

int libusb_init(libusb_context **context) { return libusb_init_context(context, NULL, 0); }

ssize_t libusb_get_device_list(libusb_context *context, libusb_device ***list) {
    if (!list || permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    *list = NULL;
    libusb_device_handle *handle = NULL;
    int result = libusb_wrap_sys_device(context, (intptr_t)permitted_fd(), &handle);
    if (result < 0) return result;
    libusb_device **selected = calloc(2, sizeof(*selected));
    if (!selected) { libusb_close(handle); return LIBUSB_ERROR_NO_MEM; }
    selected[0] = libusb_ref_device(libusb_get_device(handle));
    libusb_close(handle);
    permission_context = context;
    *list = selected;
    /* libusb_free_device_list releases each ref and frees this allocation. */
    return 1;
}

int libusb_open(libusb_device *device, libusb_device_handle **handle) {
    if (!device || !handle || permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    return libusb_wrap_sys_device(permission_context, (intptr_t)permitted_fd(), handle);
}

libusb_device_handle *libusb_open_device_with_vid_pid(libusb_context *context, uint16_t vid, uint16_t pid) {
    if (permitted_fd() < 0) return NULL;
    libusb_device_handle *handle = NULL;
    if (libusb_wrap_sys_device(context, (intptr_t)permitted_fd(), &handle) < 0) return NULL;
    struct libusb_device_descriptor descriptor;
    if (libusb_get_device_descriptor(libusb_get_device(handle), &descriptor) < 0 || descriptor.idVendor != vid || descriptor.idProduct != pid) {
        libusb_close(handle);
        return NULL;
    }
    return handle;
}
