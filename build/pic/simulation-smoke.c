/* CI simulation fixture for gpsim's real PIC18F14K22 model. */
#include <xc.h>
#pragma config WDTEN = OFF
volatile unsigned char counter;
void main(void)
{
    for (;;)
    {
        counter++;
    }
}
