import json, subprocess, sys, os, time, threading
root = sys.argv[1]
subprocess.run(["swift", "build", "--build-system", "native"], cwd=root)
exe = os.path.join(root, ".build/debug/Greeter")
p = subprocess.Popen(["lldb-dap"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
seq = 0
def send(cmd, args):
    global seq; seq += 1
    body = json.dumps({"seq": seq, "type": "request", "command": cmd, "arguments": args}).encode()
    p.stdin.write(b"Content-Length: %d\r\n\r\n" % len(body) + body); p.stdin.flush(); return seq
msgs = []
def reader():
    while True:
        line = p.stdout.readline()
        if not line: return
        if line.startswith(b"Content-Length:"):
            n = int(line.split(b":")[1]); p.stdout.readline(); m = json.loads(p.stdout.read(n)); msgs.append(m)
            if m.get("event") != "module": print("<<", json.dumps(m)[:400], flush=True)
threading.Thread(target=reader, daemon=True).start()
def wait(pred, t=60):
    end = time.time() + t
    while time.time() < end:
        for m in list(msgs):
            if pred(m): return m
        time.sleep(0.1)
send("initialize", {"adapterID": "lldb-dap", "linesStartAt1": True, "columnsStartAt1": True, "pathFormat": "path"})
send("launch", {"program": exe, "cwd": root, "stopOnEntry": False})
wait(lambda m: m.get("event") == "initialized")
s = send("setBreakpoints", {"source": {"path": os.path.join(root, "Sources/GreeterCore/Greeting.swift")}, "breakpoints": [{"line": 4}]})
print("BP", wait(lambda m: m.get("request_seq") == s))
send("configurationDone", {})
st = wait(lambda m: m.get("event") in ("stopped", "exited", "terminated"), 60)
print("STOP", st)
if st and st.get("event") == "stopped":
    t = send("stackTrace", {"threadId": st["body"]["threadId"]})
    frames = wait(lambda m: m.get("request_seq") == t)["body"]["stackFrames"]
    print("FRAMES", [f["name"] + ":" + str(f["line"]) for f in frames[:4]])
    sc = send("scopes", {"frameId": frames[0]["id"]})
    scopes = wait(lambda m: m.get("request_seq") == sc)["body"]["scopes"]
    v = send("variables", {"variablesReference": scopes[0]["variablesReference"]})
    print("LOCALS", [(x["name"], x["value"]) for x in wait(lambda m: m.get("request_seq") == v)["body"]["variables"]])
    print("RESULT swift breakpoint OK")
send("disconnect", {"terminateDebuggee": True})
time.sleep(1)
