#include "ch32fun.h"
#include <stdio.h>

int main(void) {
    SystemInit();
    for (;;) {
        // Upstream UART printf uses PD5 TX at 115200 baud (3.3 V signalling).
        printf("Hello from TabStudio\r\n");
        Delay_Ms(1000);
    }
}
