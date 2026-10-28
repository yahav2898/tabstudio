# Does java-debug (loaded into JDT LS) bind and hit a breakpoint in a Kotlin source file?
import json, os, socket, subprocess, sys, time
sys.path.insert(0, os.path.dirname(__file__))
from rpc import Lsp, Stream, log
root = '/tmp/kt'; os.makedirs(root + '/src/demo', exist_ok=True)
src = root + '/src/demo/Main.kt'
open(src, 'w').write('''package demo

class Greeter(private val name: String) {
    fun greet(): String {
        val answer = 42
        val message = "Hello, $name! $answer"
        return message
    }
}

fun main() {
    val g = Greeter("Ada")
    println(g.greet())
}
''')
kotlinc = os.environ['KOTLINC']
subprocess.check_call([kotlinc + '/bin/kotlinc', src, '-d', root + '/out'])
stdlib = kotlinc + '/lib/kotlin-stdlib.jar'
lsp = Lsp(['jdtls', '-data', '/tmp/jdtws'], cwd=root)
lsp.initialize(root, {'bundles': [os.environ['TABSTUDIO_JAVA_DEBUG_BUNDLE']]})
time.sleep(20)
port = lsp.request('workspace/executeCommand', {'command': 'vscode.java.startDebugSession', 'arguments': []}, 180)['result']
print('PORT', port, flush=True)
sock = socket.create_connection(('127.0.0.1', port))
f = sock.makefile('rwb')
d = Stream(f, f); seq = [0]
def req(cmd, args, timeout=120):
    seq[0] += 1; s = seq[0]
    d.send({'seq': s, 'type': 'request', 'command': cmd, 'arguments': args})
    return d.wait(lambda m: m.get('type') == 'response' and m.get('request_seq') == s, timeout)
req('initialize', {'adapterID': 'java', 'linesStartAt1': True, 'columnsStartAt1': True, 'pathFormat': 'path'})
seq[0] += 1; launch_seq = seq[0]
d.send({'seq': launch_seq, 'type': 'request', 'command': 'launch', 'arguments': {'mainClass': 'demo.MainKt', 'classPaths': [root + '/out', stdlib], 'cwd': root, 'console': 'internalConsole', 'projectName': ''}})
d.wait(lambda m: m.get('type') == 'event' and m.get('event') == 'initialized', 120)
for mode in ['kotlin']:
    r = req('setBreakpoints', {'source': {'path': src, 'name': 'Main.kt'}, 'breakpoints': [{'line': 6}], 'lines': [6]})
    print('BREAKPOINTS', json.dumps(r.get('body')), flush=True)
req('configurationDone', {})
try:
    st = d.wait(lambda m: m.get('type') == 'event' and m.get('event') in ('stopped', 'terminated', 'exited'), 90)
    print('EVENT', st.get('event'), flush=True)
    if st.get('event') == 'stopped':
        tid = st['body']['threadId']
        trace = req('stackTrace', {'threadId': tid})
        print('FRAMES', [(f['name'], f.get('source', {}).get('path'), f['line']) for f in trace['body']['stackFrames'][:4]], flush=True)
        fid = trace['body']['stackFrames'][0]['id']
        scopes = req('scopes', {'frameId': fid})
        vref = scopes['body']['scopes'][0]['variablesReference']
        print('LOCALS', [(v['name'], v['value']) for v in req('variables', {'variablesReference': vref})['body']['variables']], flush=True)
        print('RESULT kotlin breakpoint HIT', flush=True)
except SystemExit as e:
    print('RESULT kotlin breakpoint NOT hit', e, flush=True)
