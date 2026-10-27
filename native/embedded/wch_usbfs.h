/* SPDX-License-Identifier: GPL-2.0-or-later; PRoot integration. */
#ifndef TABSTUDIO_WCH_USBFS_H
#define TABSTUDIO_WCH_USBFS_H
#include "tracee/tracee.h"
/* Return 1 if handled (result is the real result or a refusal), 0 if unrelated. */
int tabstudio_wch_ioctl(Tracee *tracee, long *result);
#endif
