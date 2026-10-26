# On-device inference spike

Revision: llama.cpp b6000, `4762ad7316dcdec20016ab5985fb46a27902204d`.
Test weights: `stories260K.gguf`, 1,185,376 bytes, SHA-256
`047bf46455a544931cff6fef14d7910154c56afbc23ab1c5e56a72e69912c04b`.
Both candidates use CPU inference, two threads, a 512-token context, greedy sampling,
`Once upon a time` and 64 generated tokens, three repetitions. Server prompt caching is off.
The native number includes sampling/callback-loop overhead; the server reports llama.cpp's
prediction timing. These are short diagnostic measurements, not statistically rigorous
speedup estimates, and must not be used to advertise coding-model performance.

## Measurements

The [verified comparison](https://github.com/yahav2898/tabstudio/actions/runs/40256682901)
on `8be3787c0a11218cb20c818023436c81b2129a00` produced these median decode rates:

| Environment | Native library, tokens/s | llama-server, tokens/s |
| --- | ---: | ---: |
| GitHub Ubuntu 24.04 x64 | 4,729.272 | 4,214.408 |
| GitHub Ubuntu 24.04 ARM64 | 3,874.751 | 3,480.720 |
| Android x64 emulator | 69.774 | 46.620 |

The native library is the selected runtime, behind the managed completion interface.
Both candidates generated the requested 64 tokens in all three environments. Android timings
varied substantially between repetitions; these tiny diagnostic model results establish neither
coding-model speed nor physical-tablet throughput. The model manager initially says "not yet
measured" and stores actual inference throughput separately for each model on the device.

The [APK self-test](https://github.com/yahav2898/tabstudio/actions/runs/40256679446), on the same
source revision, also loaded the native library from inside TabStudio's Android app UID and generated
16 tokens at **91.4 tokens/s**. That is a separate diagnostic request on the x64 emulator, rather than
a 64-token benchmark or a measurement of the downloadable Qwen models.

## Reproduction

```bash
bash build/local-ai/build.sh artifacts/local-ai
bash build/local-ai/download-tiny.sh artifacts/tiny.gguf
python3 build/local-ai/benchmark.py artifacts/local-ai/bin artifacts/tiny.gguf
export TABSTUDIO_LOCAL_AI_LIBRARY="$PWD/artifacts/local-ai/bin/libtabstudio-ai.so"
export TABSTUDIO_LOCAL_AI_MODEL="$PWD/artifacts/tiny.gguf"
dotnet test tests/TabStudio.Agent.Tests --filter 'FullyQualifiedName~Local'
```

For the Android comparison, install NDK r27, set `ANDROID_NDK_HOME`, build with
`bash build/local-ai/build.sh artifacts/local-ai x86_64`, download `artifacts/tiny.gguf`,
start an API 29 x64 emulator and run `bash build/local-ai/emulator-spike.sh`.
`local-ai.yml` runs both host architectures and that emulator, retaining JSONL metrics and logs.
The managed real-model tests assert token generation, streaming, cancellation, reuse and
oversized-context rejection. FIM, protocol, license and model-integrity tests do not need weights.

For an APK, `bash build/local-ai/android.sh arm64-v8a` places the statically linked,
16 KB-aligned library under `artifacts/native/arm64-v8a`; the existing Android native-library
packaging includes it. The emulator uses `android.sh x86_64`. ARMv7 builds retain the IDE but
report that local AI requires 64 bits. A regular APK contains no model; only the emulator
self-test build embeds the verified tiny model with `TabStudioIncludeLocalAITestModel=true`.
The in-app self-test must load that resource through the native ABI and generate real tokens.

CPU/NEON is the portable build. `TABSTUDIO_AI_VULKAN=1` enables the optional Vulkan build
when a suitable Vulkan SDK/glslc is installed. An auto-configured native context uses an
available GPU backend and retries CPU if model/context creation fails. The default CI APK
uses CPU/NEON; no GPU speed or physical-tablet throughput is claimed by this spike.

For Linux desktop development, set `TABSTUDIO_LOCAL_AI_LIBRARY` to the built `.so` before
`dotnet run --project src/TabStudio.Desktop`. A build without that native library shows an
explicit unavailable-runtime error; it never forwards local prompts to a cloud provider.
