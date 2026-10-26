#!/usr/bin/env bash
set -euo pipefail
mkdir -p ci-out
adb shell mkdir -p /data/local/tmp/tabstudio-ai
adb push artifacts/local-ai/bin/. /data/local/tmp/tabstudio-ai/
adb push artifacts/tiny.gguf /data/local/tmp/tabstudio-ai/tiny.gguf
adb shell 'cd /data/local/tmp/tabstudio-ai && LD_LIBRARY_PATH=. ./tabstudio-ai-bench tiny.gguf' | tee ci-out/local-ai-emulator-native.jsonl
adb shell 'cd /data/local/tmp/tabstudio-ai && LD_LIBRARY_PATH=. ./llama-server -m tiny.gguf -c 512 -t 2 -ngl 0 --host 127.0.0.1 --port 18791 --no-webui >/data/local/tmp/tabstudio-ai/server.log 2>&1 &' </dev/null
adb forward tcp:18791 tcp:18791
python3 - <<'PY'
import json, time, urllib.request
for _ in range(120):
    try:
        urllib.request.urlopen('http://127.0.0.1:18791/health', timeout=1).close()
        break
    except Exception: time.sleep(.25)
with open('ci-out/local-ai-emulator-server.jsonl', 'w') as output:
    for repeat in range(3):
        body = json.dumps(dict(prompt='Once upon a time', n_predict=64, temperature=0, seed=1, ignore_eos=True, cache_prompt=False)).encode()
        with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:18791/completion', body, {'Content-Type':'application/json'}), timeout=120) as response:
            result = json.load(response)
        assert result['tokens_predicted'] == 64
        row = json.dumps(dict(backend='server', run=repeat, tokens=64, tokens_per_second=result['timings']['predicted_per_second']))
        print(row, flush=True)
        output.write(row + '\n')
PY
adb pull /data/local/tmp/tabstudio-ai/server.log ci-out/emulator-server.log
