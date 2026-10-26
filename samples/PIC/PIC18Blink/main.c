// PIC18F14K22, internal oscillator at 16 MHz. LED + resistor on RC0.
// This device has a real gpsim model for F5 simulation on the tablet.
#include <xc.h>
#define _XTAL_FREQ 16000000UL
#pragma config FOSC = IRC, WDTEN = OFF, LVP = OFF

void main(void)
{
    OSCCONbits.IRCF = 7;
    ANSEL = 0;
    ANSELH = 0;
    LATCbits.LATC0 = 0;
    TRISCbits.TRISC0 = 0;
    for (;;) {
        LATCbits.LATC0 ^= 1;
        __delay_ms(500);
    }
}
