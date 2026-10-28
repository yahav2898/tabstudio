import json, os, socket, subprocess, sys, time
sys.path.insert(0, os.path.dirname(__file__))
from rpc import Lsp, Stream
root = '/tmp/phpw'; os.makedirs(root, exist_ok=True)
src = root + '/index.php'
open(src, 'w').write('<?php\n\nfunction greet(string $name): string\n{\n    $answer = 42;\n    $message = "Hello, $name! $answer";\n    return $message;\n}\n\necho greet("Ada"), "\\n";\n$s = str_\n')
open(root + '/composer.json', 'w').write('{"name":"t/t","require":{}}')
lsp = Lsp(['php', '/usr/local/bin/phpactor', 'language-server'], cwd=root)
r = lsp.initialize(root)
print('PHPACTOR caps', list((r.get('result') or {}).get('capabilities', {}).keys()), flush=True)
lsp.open(src, 'php')
print('RESULT phpactor completion', lsp.complete(src, 10, 10, 'str_replace', 15), flush=True)
open(src, 'w').write(open(src).read().replace('$s = str_\n', ''))
# Xdebug through vscode-php-debug
p = subprocess.Popen(['node', '/opt/pd/extension/out/phpDebug.js'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
d = Stream(p.stdout, p.stdin); seq = [0]
def req(cmd, args, timeout=60):
    seq[0] += 1; s = seq[0]
    d.send({'seq': s, 'type': 'request', 'command': cmd, 'arguments': args})
    return d.wait(lambda m: m.get('type') == 'response' and m.get('request_seq') == s, timeout)
req('initialize', {'adapterID': 'php', 'linesStartAt1': True, 'columnsStartAt1': True, 'pathFormat': 'path'})
seq[0] += 1
d.send({'seq': seq[0], 'type': 'request', 'command': 'launch', 'arguments': {'program': src, 'cwd': root, 'port': 0, 'runtimeExecutable': 'php', 'externalConsole': False}})
d.wait(lambda m: m.get('type') == 'event' and m.get('event') == 'initialized', 60)
print('BP', req('setBreakpoints', {'source': {'path': src}, 'breakpoints': [{'line': 7}]}).get('body'), flush=True)
req('configurationDone', {})
try:
    st = d.wait(lambda m: m.get('type') == 'event' and m.get('event') in ('stopped', 'terminated'), 60)
    if st['event'] == 'stopped':
        tid = st['body']['threadId']; tr = req('stackTrace', {'threadId': tid})
        print('FRAMES', [(f['name'], f['line']) for f in tr['body']['stackFrames']], flush=True)
        sc = req('scopes', {'frameId': tr['body']['stackFrames'][0]['id']})
        print('LOCALS', [(v['name'], v['value']) for v in req('variables', {'variablesReference': sc['body']['scopes'][0]['variablesReference']})['body']['variables']], flush=True)
        print('RESULT xdebug breakpoint HIT', flush=True)
    else: print('RESULT xdebug terminated without stop', flush=True)
except SystemExit as e: print('RESULT xdebug NOT hit', e, flush=True)
