import glob, os, subprocess, sys, time
sys.path.insert(0, os.path.dirname(__file__))
from rpc import Lsp, Stream
root = '/tmp/sw'
src = glob.glob(root + '/Sources/**/*.swift', recursive=True)[0]
open(src, 'w').write('func greet(_ name: String) -> String {\n    let answer = 42\n    let message = "Hello, \\(name)! \\(answer)"\n    return message\n}\n\nprint(greet("Ada"))\nlet s = "x"\nprint(s.coun)\n')
subprocess.run(['swift', 'build'], cwd=root)
lsp = Lsp(['sourcekit-lsp'], cwd=root)
lsp.initialize(root)
lsp.open(src, 'swift')
print('RESULT sourcekit-lsp completion', lsp.complete(src, 8, 12, 'count', 30), flush=True)
open(src, 'w').write(open(src).read().replace('print(s.coun)\n', ''))
subprocess.run(['swift', 'build'], cwd=root)
subprocess.run(['bash', '-c', 'readelf -S .build/debug/Greeter | grep -c debug_; ls -la .build/debug/ | head; lldb --batch -o "b Greeter.swift:4" -o run -o "frame variable" -o "bt" .build/debug/Greeter 2>&1 | tail -30'], cwd=root)
exe = root + '/.build/debug/Greeter'
p = subprocess.Popen(['lldb-dap'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
d = Stream(p.stdout, p.stdin); seq = [0]
def req(cmd, args, timeout=90):
    seq[0] += 1; s = seq[0]
    d.send({'seq': s, 'type': 'request', 'command': cmd, 'arguments': args})
    return d.wait(lambda m: m.get('type') == 'response' and m.get('request_seq') == s, timeout)
req('initialize', {'adapterID': 'lldb-dap', 'linesStartAt1': True, 'columnsStartAt1': True, 'pathFormat': 'path'})
seq[0] += 1
d.send({'seq': seq[0], 'type': 'request', 'command': 'launch', 'arguments': {'program': exe, 'cwd': root, 'initCommands': ['settings set target.inherit-env true']}})
d.wait(lambda m: m.get('type') == 'event' and m.get('event') == 'initialized', 90)
print('BP', req('setBreakpoints', {'source': {'path': src}, 'breakpoints': [{'line': 4}]}).get('body'), flush=True)
req('configurationDone', {})
try:
    st = d.wait(lambda m: m.get('type') == 'event' and m.get('event') in ('stopped', 'terminated', 'exited'), 90)
    if st['event'] == 'stopped':
        tid = st['body']['threadId']; tr = req('stackTrace', {'threadId': tid})
        print('FRAMES', [(f['name'], f['line']) for f in tr['body']['stackFrames'][:4]], flush=True)
        sc = req('scopes', {'frameId': tr['body']['stackFrames'][0]['id']})
        print('LOCALS', [(v['name'], v['value']) for v in req('variables', {'variablesReference': sc['body']['scopes'][0]['variablesReference']})['body']['variables']], flush=True)
        print('RESULT lldb breakpoint HIT', flush=True)
    else: print('RESULT lldb', st['event'], flush=True)
except SystemExit as e: print('RESULT lldb NOT hit', e, flush=True)
