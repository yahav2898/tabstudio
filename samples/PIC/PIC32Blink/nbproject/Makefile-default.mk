# Firmware flags for this template. TabStudio invokes this file directly.
MP_CC ?= xc32-gcc
MP_BIN2HEX ?= xc32-bin2hex
DFP_DIR ?= /opt/tabstudio/microchip/packs/Microchip/PIC32MX_DFP/1.7.380
CND_CONF ?= default
OUT := dist/$(CND_CONF)/production
.PHONY: .build-conf .clean-conf
.build-conf: $(OUT)/firmware.hex
.clean-conf:
	$(RM) $(OUT)/firmware.elf $(OUT)/firmware.hex
$(OUT)/firmware.elf: main.c
	mkdir -p "$(OUT)"
	$(MP_CC) -mprocessor=32MX250F128B -mdfp="$(DFP_DIR)" -O0 -g -Wl,--defsym=_min_heap_size=0,--report-mem -o "$@" "$<"
$(OUT)/firmware.hex: $(OUT)/firmware.elf
	$(MP_BIN2HEX) "$<"
