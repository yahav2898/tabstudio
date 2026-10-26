/* Regression for Delve's separate ptrace and wait OS threads. */
#define _GNU_SOURCE
#include <errno.h>
#include <pthread.h>
#include <signal.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <unistd.h>

static atomic_int child_pid, resume_child;

static void require(int condition, const char *message)
{
    if (!condition) {
        perror(message);
        exit(1);
    }
}

static void *trace(void *unused)
{
    (void) unused;
    pid_t child = fork();
    require(child >= 0, "fork");
    if (child == 0) {
        /* Let the sibling start waiting before this thread becomes a tracer. */
        usleep(100000);
        if (ptrace(PTRACE_TRACEME, 0, NULL, NULL) != 0)
            _exit(99);
        raise(SIGSTOP);
        _exit(37);
    }

    atomic_store(&child_pid, child);
    while (!atomic_load(&resume_child))
        usleep(1000);
    require(ptrace(PTRACE_CONT, child, NULL, NULL) == 0, "owner resumes its ptracee");
    return NULL;
}

static void check(int nonblocking)
{
    pthread_t tracer;
    atomic_store(&child_pid, 0);
    atomic_store(&resume_child, 0);
    require(pthread_create(&tracer, NULL, trace, NULL) == 0, "pthread_create");
    pid_t child;
    while ((child = atomic_load(&child_pid)) == 0)
        usleep(1000);

    int status;
    errno = 0;
    require(waitpid(child, &status, __WALL | __WNOTHREAD | WNOHANG) == -1 && errno == ECHILD,
        "__WNOTHREAD excludes a sibling's child");

    pid_t result;
    do {
        result = waitpid(child, &status, __WALL | (nonblocking ? WNOHANG : 0));
        if (result == 0)
            usleep(1000);
    } while (result == 0);
    require(result == child && WIFSTOPPED(status) && WSTOPSIG(status) == SIGSTOP,
        "sibling receives the debugger stop");

    errno = 0;
    require(ptrace(PTRACE_CONT, child, NULL, NULL) == -1 && errno == ESRCH,
        "a waiting sibling cannot control the ptracee");
    atomic_store(&resume_child, 1);
    require(waitpid(child, &status, __WALL) == child && WIFEXITED(status) && WEXITSTATUS(status) == 37,
        "sibling receives the exit status");
    require(pthread_join(tracer, NULL) == 0, "pthread_join");
}

int main(void)
{
    alarm(20);
    check(0);
    check(1);
    puts("Cross-thread ptrace waits, ownership and __WNOTHREAD passed.");
    return 0;
}
