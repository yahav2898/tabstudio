/* TabStudio's MIT-licensed compatibility shim for musl JVMs on Android.
 *
 * musl can merge a new anonymous RW mapping with an adjacent pthread stack. Android's SELinux then classifies
 * mprotect of that separate allocation as execstack rather than execmem. HotSpot's startup probe encounters this.
 * Marking just the requested allocation MADV_DONTDUMP splits the VMA before retrying the real protection change.
 * SELinux still decides whether to permit it. A request that includes our actual stack pointer is never retried.
 *
 * This library is freestanding: musl supplies syscall and its errno accessor when LD_PRELOAD loads it. Linking
 * against Android's bionic would give the Alpine loader an incompatible libc dependency.
 */
#include <errno.h>
#include <stdint.h>
#include <sys/mman.h>
#include <sys/syscall.h>
#include <unistd.h>

extern int *__errno_location(void);

int mprotect(void *address, size_t length, int protection)
{
    int result = (int) syscall(SYS_mprotect, address, length, protection);
    if (result != -1 || !(protection & PROT_EXEC) || length == 0)
        return result;

    int error = *__errno_location();
    uintptr_t start = (uintptr_t) address;
    uintptr_t stack = (uintptr_t) &result;
    if (error != EACCES || start > UINTPTR_MAX - length || (stack >= start && stack - start < length))
        return result;

    if (syscall(SYS_madvise, address, length, MADV_DONTDUMP) != 0) {
        *__errno_location() = error;
        return result;
    }

    return (int) syscall(SYS_mprotect, address, length, protection);
}
