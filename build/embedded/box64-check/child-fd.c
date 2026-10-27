/* Extends the exact PIC clone regression with an inherited descriptor check. */
#define main pic_spawn_regression_main
#include "../../pic/static-spawn.c"
#undef main
#include <fcntl.h>

int main(int argc, char **argv)
{
    if (fcntl(3, F_GETFD) < 0)
        return 65;
    if (argc == 2 && strcmp(argv[1], "--child") == 0)
        return 42;
    int result = pic_spawn_regression_main(argc, argv);
    if (result)
        return result;
    puts("BOX64_CHILD_FD_PASSED 32 children retained descriptor 3");
    return 0;
}
