/* Follow cloned target threads while another OS thread makes ptrace requests. */
#define _GNU_SOURCE
#include <errno.h>
#include <pthread.h>
#include <signal.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <unistd.h>

enum { OPTIONS = 1, CONTINUE, EVENT, FINISH };
static atomic_int action, target, child_pid;
static long result;
static int error;
static unsigned long cloned;
static const char *executable;

static void require(int condition, const char *message)
{
    if (!condition) {
        perror(message);
        exit(1);
    }
}

static void *target_thread(void *unused)
{
    (void) unused;
    usleep(10000);
    return NULL;
}

static void *tracer(void *unused)
{
    (void) unused;
    pid_t child = fork();
    require(child >= 0, "fork");
    if (child == 0) {
        if (ptrace(PTRACE_TRACEME, 0, NULL, NULL) != 0)
            _exit(99);
        execl(executable, executable, "--target", NULL);
        _exit(98);
    }
    atomic_store(&child_pid, child);
    for (;;) {
        int command;
        while ((command = atomic_load(&action)) == 0) { }
        if (command == FINISH)
            return NULL;
        pid_t pid = atomic_load(&target);
        errno = 0;
        if (command == OPTIONS)
            result = ptrace(PTRACE_SETOPTIONS, pid, NULL, (void *) PTRACE_O_TRACECLONE);
        else if (command == EVENT)
            result = ptrace(PTRACE_GETEVENTMSG, pid, NULL, &cloned);
        else
            result = ptrace(PTRACE_CONT, pid, NULL, NULL);
        error = errno;
        atomic_store(&action, 0);
    }
}

static long request(int command, pid_t pid)
{
    atomic_store(&target, pid);
    atomic_store(&action, command);
    while (atomic_load(&action) != 0) { }
    errno = error;
    return result;
}

static void check(void)
{
    pthread_t owner;
    atomic_store(&child_pid, 0);
    atomic_store(&action, 0);
    require(pthread_create(&owner, NULL, tracer, NULL) == 0, "tracer thread");
    pid_t child;
    while ((child = atomic_load(&child_pid)) == 0) { }
    int status;
    require(waitpid(child, &status, __WALL) == child && WIFSTOPPED(status), "exec stop");
    require(request(OPTIONS, child) == 0, "trace clone events");
    require(request(CONTINUE, child) == 0, "continue target");
    int clones = 0;
    pid_t configured[2] = {0};
    for (;;) {
        pid_t pid = waitpid(-1, &status, __WALL);
        require(pid > 0, "target event");
        if (WIFEXITED(status)) {
            if (pid == child) {
                require(WEXITSTATUS(status) == 37 && clones == 2, "target exit and clone count");
                break;
            }
            continue;
        }
        require(WIFSTOPPED(status), "stopped target thread");
        if ((unsigned int) status >> 16 == PTRACE_EVENT_CLONE) {
            require(request(EVENT, pid) == 0 && cloned > 0, "cloned thread id");
            pid_t thread = (pid_t) cloned;
            require(clones < 2, "expected clone count");
            configured[clones] = thread;
            clones++;
            /* Like Delve, retry ESRCH only after collecting the new thread's stop. */
            if (request(OPTIONS, thread) == -1) {
                require(errno == ESRCH, "new thread is not stopped yet");
                require(waitpid(thread, &status, __WALL) == thread && WIFSTOPPED(status), "initial thread stop");
                require(request(OPTIONS, thread) == 0, "options after initial stop");
            }
            require(request(CONTINUE, thread) == 0, "continue cloned thread");
        }
        else if (pid != child && pid != configured[0] && pid != configured[1]) {
            /* Linux can report the child's SIGSTOP before the parent's clone event. */
            continue;
        }
        require(request(CONTINUE, pid) == 0, "continue stopped thread");
    }
    atomic_store(&action, FINISH);
    require(pthread_join(owner, NULL) == 0, "join tracer");
}

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--target") == 0) {
        pthread_t threads[2];
        for (int i = 0; i < 2; i++)
            require(pthread_create(&threads[i], NULL, target_thread, NULL) == 0, "target thread");
        for (int i = 0; i < 2; i++)
            require(pthread_join(threads[i], NULL) == 0, "join target thread");
        return 37;
    }
    executable = argv[0];
    alarm(25);
    for (int i = 0; i < 5; i++)
        check();
    puts("Cloned target threads resume only after their initial stop.");
    return 0;
}
