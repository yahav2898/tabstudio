# Stock wlink through the permissioned USBFS guard

This is a **synthetic protocol fixture**, not a recorded WCH-Link transcript or hardware flash test.
It runs the unchanged official wlink 0.1.3 Linux x64 binary, including its normal nusb backend, through the
production patched PRoot and the production C# `UsbfsDeviceInventory` builder. Artifact and canonical
flash-loader hashes come from the immutable upstream release/source commit.

The test-only `kernel_backend.c` is loaded into the **PRoot tracer**, scoped to that exact PID and retained
FD3. It supplies native USBFS ioctl completions against a socketpair. It is never compiled into the APK,
loaded into the wlink process or selected by product code. The socket carries valid USB descriptors;
no `/dev` device, USB kernel module or physical probe is required.

The fixture checks:

- The real CLI's complete unprotected flash protocol, the exact 512-byte loader, 1,037 actual firmware
  bytes and only FF padding, preceded by read-only full-ID discovery, followed by a separate real CLI
  dump and byte comparison, and only then a separate reset-only guarded CLI worker.
- Refusal of read protection, write protection, wrong exact package ID, unknown protection states and
  malformed/short attach responses before any loader or image data reaches the native backend.
- Raw static-syscall USBFS clients with multiple queued IN URBs, cancellation, reaping the original
  URB/context/buffer identities, empty-queue EAGAIN, and preservation of cancelled buffers.
- Rejection of fragmented/coalesced command frames and protection writes before any prefix reaches
  the native backend.

Every full proof runs with PRoot's seccomp accelerator enabled and disabled. Wire logs and JSON summaries
mark hardware/Android verification false. The native backend separately tests its completion queue and
unsafe-wire oracle, so a silently permissive test backend does not make the guard test green.

```sh
python3 build/embedded/wch-link-check/run.py \
  --proot "$TABSTUDIO_TEST_PROOT" \
  --work "$RUNNER_TEMP/wch-link-check" \
  --output ci-out/wch-link-check
```

Prerequisites are a native Linux x64 host, .NET from `global.json`, a C compiler with static libc support,
and the **actual guarded PRoot build** plus its loader (`PROOT_LOADER`). An unguarded or unbrokered PRoot
must fail the positive proof. `--backend-unit-only` executes only the backend's native contracts and does
not establish that PRoot or wlink works.
