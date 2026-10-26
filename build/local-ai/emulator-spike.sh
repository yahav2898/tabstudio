#!/usr/bin/env bash
set -euo pipefail
mkdir -p ci-out
adb shell mkdir -p /data/local/tmp/tabstudio-ai
adb push artifacts/local-ai/bin/. /data/local/tmp/tabstudio-ai/
adb push artifacts/tiny.gguf /data/local/tmp/tabstudio-ai/tiny.gguf
adb shell 'cd /data/local/tmp/tabstudio-ai && LD_LIBRARY_PATH=. ./tabstudio-ai-bench tiny.gguf' | tee ci-out/local-ai-emulator-native.jsonl
adb forward tcp:18791 tcp:18791
# Keep adb's foreground server process in a managed child. An adb shell background job can keep the shell
# transport open on some emulator images; it must not block the host before the requests can be sent.
python3 - <<'PY'
import json, subprocess, time, urllib.request
with open('ci-out/emulator-server.log', 'w') as log:
    server = subprocess.Popen(['adb', 'shell', 'cd /data/local/tmp/tabstudio-ai && LD_LIBRARY_PATH=. ./llama-server -m tiny.gguf -c 512 -t 2 -ngl 0 --host 127.0.0.1 --port 18791 --no-webui'], stdout=log, stderr=log)
    try:
        ready = False
        for _ in range(120):
            if server.poll() is not None: raise RuntimeError('Android llama-server exited; see emulator-server.log')
            try:
                urllib.request.urlopen('http://127.0.0.1:18791/health', timeout=1).close()
                ready = True
                break
            except Exception: time.sleep(.25)
        if not ready: raise RuntimeError('Android llama-server did not become healthy')
        with open('ci-out/local-ai-emulator-server.jsonl', 'w') as output:
            for repeat in range(3):
                body = json.dumps(dict(prompt='Once upon a time', n_predict=64, temperature=0, seed=1, ignore_eos=True, cache_prompt=False)).encode()
                with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:18791/completion', body, {'Content-Type':'application/json'}), timeout=120) as response:
                    result = json.load(response)
                assert result['tokens_predicted'] == 64
                row = json.dumps(dict(backend='server', run=repeat, tokens=64, tokens_per_second=result['timings']['predicted_per_second']))
                print(row, flush=True)
                output.write(row + '\n')
    finally:
        server.terminate()
        try: server.wait(timeout=10)
        except subprocess.TimeoutExpired: server.kill(); server.wait()
PY
adb forward --remove tcp:18791
