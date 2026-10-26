"""Measure the same greedy 64-token request over native C ABI and llama-server."""
import json, os, pathlib, subprocess, sys, time, urllib.request
binary, model = pathlib.Path(sys.argv[1]).resolve(), str(pathlib.Path(sys.argv[2]).resolve())
subprocess.run([str(binary / 'tabstudio-ai-bench'), model], check=True)
with open(binary / 'server.log', 'w') as log:
    server = subprocess.Popen([str(binary / 'llama-server'), '-m', model, '-c', '512', '-t', '2', '-ngl', '0', '--host', '127.0.0.1', '--port', '18791', '--no-webui'], stdout=log, stderr=log)
    try:
        for _ in range(120):
            if server.poll() is not None: raise RuntimeError('llama-server exited; see server.log')
            try:
                urllib.request.urlopen('http://127.0.0.1:18791/health', timeout=1).close()
                break
            except Exception: time.sleep(.25)
        for repeat in range(3):
            body = json.dumps(dict(prompt='Once upon a time', n_predict=64, temperature=0, seed=1, ignore_eos=True, cache_prompt=False)).encode()
            with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:18791/completion', body, {'Content-Type': 'application/json'}), timeout=120) as response:
                result = json.load(response)
            assert result['tokens_predicted'] == 64, result
            print(json.dumps(dict(backend='server', run=repeat, tokens=result['tokens_predicted'], tokens_per_second=result['timings']['predicted_per_second'])), flush=True)
    finally:
        server.terminate()
        server.wait(timeout=15)
