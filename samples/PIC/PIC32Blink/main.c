// PIC32MX250F128B, 8 MHz FRC; LED + resistor on RB0.
#include <xc.h>
#pragma config FNOSC = FRC, POSCMOD = OFF, FWDTEN = OFF, JTAGEN = OFF

int main(void)
{
    ANSELBCLR = 1;
    LATBCLR = 1;
    TRISBCLR = 1;
    for (;;) {
        LATBINV = 1;
        for (volatile unsigned long ticks = 0; ticks < 400000UL; ++ticks) { }
    }
}
