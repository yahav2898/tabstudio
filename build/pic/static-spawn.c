/* Regression for the raw clone-with-child-stack used by static glibc's
 * posix_spawn. The spawned program must execute and its exit status survive. */
#include <spawn.h>
#include <errno.h>
#include <stdio.h>
#include <string.h>
#include <sys/wait.h>
#include <unistd.h>

extern char **environ;

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--child") == 0)
        return 42;
    pid_t child;
    int status;
    char *arguments[] = {argv[0], "--child", NULL};
    for (int attempt = 0; attempt < 32; ++attempt) {
        int error = posix_spawn(&child, argv[0], NULL, NULL, arguments, environ);
        if (error || waitpid(child, &status, 0) != child ||
            !WIFEXITED(status) || WEXITSTATUS(status) != 42)
            return 1;
    }
    if (posix_spawn(&child, "/tabstudio-no-such-program", NULL, NULL, arguments, environ) != ENOENT)
        return 4;
    puts("static posix_spawn: child exited 42; 32 repetitions and ENOENT checked");
    return 0;
}
