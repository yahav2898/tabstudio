/* SPDX-License-Identifier: MIT
 * Copyright (c) 2026 TabStudio contributors.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
/* Permissioned libusb view for existing programmer CLIs. No USB enumeration.
 * Built natively for Alpine; LD_PRELOAD is scoped to the programmer process.
 * The Android parent owns fd 3 until that process exits. */
#include <libusb-1.0/libusb.h>
#include <stdlib.h>
#include <stdint.h>
#include <errno.h>
#include <dlfcn.h>
#include <pthread.h>
#include <fcntl.h>

/* Keep one wrapped view per libusb context, so concurrent contexts do not
 * accidentally attach transfers to another context's event loop. A view owns
 * its base handle until libusb_exit; lists retain ordinary device references. */
struct permission_view {
    libusb_context *context;
    libusb_device_handle *base;
    struct permission_view *next;
};
static struct permission_view *views;
static pthread_mutex_t view_lock = PTHREAD_MUTEX_INITIALIZER;

static int permitted_fd(void) {
    const char *text = getenv("TABSTUDIO_USB_FD");
    char *end;
    if (!text || !*text) return -1;
    errno = 0;
    long fd = strtol(text, &end, 10);
    return errno || *end || fd != 3 || fcntl((int)fd, F_GETFD) < 0 ? -1 : (int)fd;
}

int libusb_init_context(libusb_context **context, const struct libusb_init_option original[], int count) {
    if (permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    if (count < 0 || count > 16 || (count && !original)) return LIBUSB_ERROR_INVALID_PARAM;
    struct libusb_init_option options[17];
    int selected = 0;
    for (int i = 0; i < count; i++) if (original[i].option != LIBUSB_OPTION_NO_DEVICE_DISCOVERY) options[selected++] = original[i];
    options[selected++] = (struct libusb_init_option){ .option = LIBUSB_OPTION_NO_DEVICE_DISCOVERY };
    int (*initialize)(libusb_context **, const struct libusb_init_option *, int) = dlsym(RTLD_NEXT, "libusb_init_context");
    if (!initialize) return LIBUSB_ERROR_NOT_SUPPORTED;
    int result = initialize(context, options, selected);
    return result;
}

int libusb_init(libusb_context **context) { return libusb_init_context(context, NULL, 0); }

/* Must be called with view_lock held. */
static int get_view(libusb_context *context, struct permission_view **output) {
    for (struct permission_view *view = views; view; view = view->next) {
        if (view->context == context) { *output = view; return 0; }
    }
    struct permission_view *view = calloc(1, sizeof(*view));
    if (!view) return LIBUSB_ERROR_NO_MEM;
    int result = libusb_wrap_sys_device(context, (intptr_t)permitted_fd(), &view->base);
    if (result < 0) { free(view); return result; }
    view->context = context;
    view->next = views;
    views = view;
    *output = view;
    return 0;
}

ssize_t libusb_get_device_list(libusb_context *context, libusb_device ***list) {
    if (!list || permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    *list = NULL;
    libusb_device **selected = calloc(2, sizeof(*selected));
    if (!selected) return LIBUSB_ERROR_NO_MEM;
    pthread_mutex_lock(&view_lock);
    struct permission_view *view = NULL;
    int result = get_view(context, &view);
    if (result == 0) selected[0] = libusb_ref_device(libusb_get_device(view->base));
    pthread_mutex_unlock(&view_lock);
    if (result < 0) { free(selected); return result; }
    *list = selected;
    /* libusb_free_device_list releases each ref and frees this allocation. */
    return 1;
}

int libusb_open(libusb_device *device, libusb_device_handle **handle) {
    if (!device || !handle || permitted_fd() < 0) return LIBUSB_ERROR_ACCESS;
    *handle = NULL;
    pthread_mutex_lock(&view_lock);
    int result = LIBUSB_ERROR_NO_DEVICE;
    for (struct permission_view *view = views; view; view = view->next) {
        if (libusb_get_device(view->base) == device) {
            result = libusb_wrap_sys_device(view->context, (intptr_t)permitted_fd(), handle);
            break;
        }
    }
    pthread_mutex_unlock(&view_lock);
    return result;
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

void libusb_exit(libusb_context *context) {
    pthread_mutex_lock(&view_lock);
    struct permission_view **link = &views;
    while (*link) {
        struct permission_view *view = *link;
        if (view->context == context) {
            *link = view->next;
            libusb_close(view->base);
            free(view);
            break;
        }
        link = &view->next;
    }
    pthread_mutex_unlock(&view_lock);
    void (*finish)(libusb_context *) = dlsym(RTLD_NEXT, "libusb_exit");
    if (finish) finish(context);
}
