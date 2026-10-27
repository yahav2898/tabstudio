ENTRY(_start)
MEMORY { RAM (rwx) : ORIGIN = 0x80000000, LENGTH = 128K }
SECTIONS {
  .text : { KEEP(*(.text.init)) *(.text*) *(.rodata*) } > RAM
  .data : { *(.data*) } > RAM
  .bss (NOLOAD) : { *(.bss*) *(COMMON); } > RAM
  _stack_top = ORIGIN(RAM) + LENGTH(RAM);
}
