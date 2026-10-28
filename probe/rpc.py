import json, subprocess, sys, threading, queue, socket, time, os

class Stream:
    """Content-Length framed JSON-RPC over a pair of binary streams."""
    def __init__(self, reader, writer):
        self.r, self.w = reader, writer
        self.q = queue.Queue()
        threading.Thread(target=self._read, daemon=True).start()
    def _read(self):
        while True:
            headers = {}
            while True:
                line = self.r.readline()
                if not line: self.q.put(None); return
                line = line.decode('ascii', 'replace').strip()
                if not line: break
                k, _, v = line.partition(':'); headers[k.lower()] = v.strip()
            body = self.r.read(int(headers['content-length']))
            self.q.put(json.loads(body))
    def send(self, msg):
        data = json.dumps(msg).encode()
        self.w.write(b'Content-Length: %d\r\n\r\n' % len(data) + data); self.w.flush()
    def wait(self, pred, timeout):
        end = time.time() + timeout
        while time.time() < end:
            try: m = self.q.get(timeout=max(0.1, end - time.time()))
            except queue.Empty: break
            if m is None: raise SystemExit('stream closed')
            log(m)
            if pred(m): return m
        raise SystemExit('timeout')

def log(m):
    s = json.dumps(m)
    print('<<', s[:600], flush=True)

class Lsp:
    def __init__(self, cmd, cwd=None, env=None):
        self.p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, cwd=cwd, env=env)
        self.s = Stream(self.p.stdout, self.p.stdin); self.id = 0
    def request(self, method, params, timeout=300):
        self.id += 1; i = self.id
        self.s.send({'jsonrpc': '2.0', 'id': i, 'method': method, 'params': params})
        def pred(m):
            if m.get('id') is not None and 'method' in m:  # server request: answer null / config
                self.s.send({'jsonrpc': '2.0', 'id': m['id'], 'result': [None] * len(m.get('params', {}).get('items', [])) if m['method'] == 'workspace/configuration' else None})
                return False
            return m.get('id') == i
        return self.s.wait(pred, timeout)
    def notify(self, method, params):
        self.s.send({'jsonrpc': '2.0', 'method': method, 'params': params})
    def initialize(self, root, options=None):
        r = self.request('initialize', {'processId': os.getpid(), 'rootUri': 'file://' + root, 'workspaceFolders': [{'uri': 'file://' + root, 'name': 'w'}],
            'capabilities': {'textDocument': {'completion': {'completionItem': {'snippetSupport': True}}, 'publishDiagnostics': {}}, 'workspace': {'configuration': True, 'workspaceFolders': True}},
            'initializationOptions': options or {}})
        self.notify('initialized', {})
        return r
    def open(self, path, lang):
        self.notify('textDocument/didOpen', {'textDocument': {'uri': 'file://' + path, 'languageId': lang, 'version': 1, 'text': open(path).read()}})
    def complete(self, path, line, ch, want, attempts=30):
        for _ in range(attempts):
            r = self.request('textDocument/completion', {'textDocument': {'uri': 'file://' + path}, 'position': {'line': line, 'character': ch}}, 120)
            res = r.get('result') or []
            items = res['items'] if isinstance(res, dict) else res
            labels = [i['label'] for i in items]
            print('COMPLETION', len(labels), [l for l in labels if want in l][:5], flush=True)
            if any(want in l for l in labels): return True
            time.sleep(4)
        return False
