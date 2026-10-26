/* Box64 regression: raw vfork-style clone, followed by a native libc callback
 * in the parent after the child's guest stack has been unmapped. */
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/wait.h>
#include <unistd.h>

extern char **environ;

/* Use the raw x86-64 syscall rather than libc's wrapped posix_spawn/clone.
 * The child gets a separate guest stack, exactly as static glibc requires. */
__attribute__((naked)) static long spawn_raw(void *stack, const char *file,
                                           char *const args[], char *const env[])
{
    __asm__ volatile(
        "sub $32, %rdi\n"
        "mov %rsi, 0(%rdi)\n"
        "mov %rdx, 8(%rdi)\n"
        "mov %rcx, 16(%rdi)\n"
        "mov %rdi, %rsi\n"
        "mov $0x4111, %edi\n" /* CLONE_VM | CLONE_VFORK | SIGCHLD */
        "xor %edx, %edx\n"
        "xor %r10d, %r10d\n"
        "xor %r8d, %r8d\n"
        "mov $56, %eax\n"
        "syscall\n"
        "test %rax, %rax\n"
        "jnz 1f\n"
        "mov 0(%rsp), %rdi\n"
        "mov 8(%rsp), %rsi\n"
        "mov 16(%rsp), %rdx\n"
        "mov $59, %eax\n"
        "syscall\n"
        "mov $127, %edi\n"
        "mov $60, %eax\n"
        "syscall\n"
        "1: ret\n");
}

static int compare(const void *left, const void *right)
{
    int a = *(const int *)left, b = *(const int *)right;
    return (a > b) - (a < b);
}

int main(int argc, char **argv)
{
    if (argc == 2 && !strcmp(argv[1], "--child")) return 42;
    for (int attempt = 0; attempt < 8; ++attempt) {
        size_t size = 1024 * 1024;
        char *stack = mmap(NULL, size, PROT_READ | PROT_WRITE,
                           MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
        if (stack == MAP_FAILED) return 1;
        char *args[] = {argv[0], "--child", NULL};
        long child = spawn_raw(stack + size, argv[0], args, environ);
        int status;
        if (child < 0 || waitpid(child, &status, 0) != child ||
            !WIFEXITED(status) || WEXITSTATUS(status) != 42) return 2;
        if (munmap(stack, size)) return 3;
        int values[] = {4, 1, 8, 6, 2, 5, 7, 3};
        qsort(values, 8, sizeof(values[0]), compare);
        for (int index = 0; index < 8; ++index)
            if (values[index] != index + 1) return 4;
    }
    puts("raw clone: parent libc callbacks use the parent stack (8 children)");
    return 0;
}
