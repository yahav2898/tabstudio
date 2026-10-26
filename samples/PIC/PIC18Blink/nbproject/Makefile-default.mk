# Firmware flags for this template. TabStudio invokes this file directly.
MP_CC ?= xc8-cc
MP_BIN2HEX ?= xc32-bin2hex
DFP_DIR ?= /opt/tabstudio/microchip/packs/Microchip/PIC18F-K_DFP/1.17.312/xc8
CND_CONF ?= default
OUT := dist/$(CND_CONF)/production
.PHONY: .build-conf .clean-conf
.build-conf: $(OUT)/firmware.hex
.clean-conf:
	$(RM) $(OUT)/firmware.elf $(OUT)/firmware.hex
$(OUT)/firmware.elf: main.c
	mkdir -p "$(OUT)"
	$(MP_CC) -mcpu=18F14K22 -mdfp="$(DFP_DIR)" -O0 -o "$@" "$<"
$(OUT)/firmware.hex: $(OUT)/firmware.elf
	@test -s "$@"
