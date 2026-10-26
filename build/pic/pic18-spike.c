// PIC18F45K22, internal oscillator at 16 MHz. LED + resistor on RD0.
#include <xc.h>
#define _XTAL_FREQ 16000000UL
#pragma config FOSC = INTIO67, WDTEN = OFF, LVP = OFF
#pragma config PWRTEN = OFF, BOREN = SBORDIS, BORV = 190

void main(void)
{
    OSCCONbits.IRCF = 7;
    ANSELD = 0;
    LATDbits.LATD0 = 0;
    TRISDbits.TRISD0 = 0;
    for (;;) {
        LATDbits.LATD0 ^= 1;
        __delay_ms(500);
    }
}
