# Uses the Store's SHA-256-pinned upstream startup, linker script and MCU headers.
.DEFAULT_GOAL := build
TARGET := firmware
TARGET_MCU := CH32V003
CH32FUN ?= @CH32FUN@
EXTRA_CFLAGS += -fstack-usage
include $(CH32FUN)/ch32fun.mk
