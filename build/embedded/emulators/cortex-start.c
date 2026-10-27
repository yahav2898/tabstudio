extern int main(void);
extern unsigned long _sbss, _ebss;
void reset_handler(void)
{
    for (unsigned long *p = &_sbss; p < &_ebss; ++p) *p = 0;
    (void)main();
    for (;;) {}
}
__attribute__((section(".vectors"), used))
const unsigned long vectors[] = { 0x20010000, (unsigned long)reset_handler };
