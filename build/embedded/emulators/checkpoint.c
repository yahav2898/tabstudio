/* Deterministic debugger fixture, not a hardware peripheral simulation. */
typedef unsigned long uint32;
volatile uint32 counter;

__attribute__((noinline)) void checkpoint(uint32 value)
{
    volatile uint32 local = value + 7;
    counter = local; /* TABSTUDIO_BREAKPOINT */
    counter += 1;
}

int main(void)
{
    checkpoint(35);
    for (;;) {
        __asm__ volatile ("nop");
    }
}
