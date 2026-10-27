/* SPDX-License-Identifier: MIT */
#ifndef TABSTUDIO_WCH_GUARD_H
#define TABSTUDIO_WCH_GUARD_H
#include <stddef.h>
#include <stdint.h>

/* Narrow protocol policy for stock wlink 0.1.3 and ordinary CH32V003 code flash.
 * This validates traffic, not the electrical probe or physical flash contents. */
enum wch_phase { WCH_IDLE, WCH_REGION, WCH_LOADER, WCH_LOADER_READY,
    WCH_COMMITTED, WCH_PROGRAM, WCH_ACK, WCH_PROGRAM_DONE, WCH_READ_REGION, WCH_READ };
enum wch_response { WCH_NONE, WCH_PROBE, WCH_SPEED, WCH_ATTACH, WCH_DETACH,
    WCH_READ_PROTECTION, WCH_WRITE_PROTECTION, WCH_SIGNATURE, WCH_WRITE_REGION_ACK,
    WCH_LOADER_ACK, WCH_COMMIT_ACK, WCH_PROGRAM_ACK, WCH_END_ACK,
    WCH_READ_REGION_ACK, WCH_READ_ACK, WCH_RESET_ACK };
struct wch_guard {
    uint32_t chip_id, region, source_length, padded_length, written, read_remaining;
    size_t loader_offset;
    enum wch_phase phase;
    enum wch_response response;
    unsigned char response_opcode;
    unsigned char probe_ok, attached, read_ok, write_ok, poisoned, read_only, reset_only;
};
int wch_guard_init(struct wch_guard *guard, const char *chip_id);
int wch_guard_init_reset(struct wch_guard *guard, const char *chip_id);
void wch_guard_init_read_only(struct wch_guard *guard);
int wch_guard_init_read_only_bound(struct wch_guard *guard, const char *chip_id);
int wch_guard_out(struct wch_guard *guard, unsigned endpoint, const unsigned char *data, size_t length);
int wch_guard_in(struct wch_guard *guard, unsigned endpoint, const unsigned char *data, size_t length);
int wch_guard_input_allowed(const struct wch_guard *guard, unsigned endpoint, size_t length);
void wch_guard_poison(struct wch_guard *guard);
#endif
