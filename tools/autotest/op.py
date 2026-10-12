"""Minimal stdio MCP client for the Meta XR Operator proxy.

Usage: python -I -X utf8 op.py <proxy.exe> <script.py | -list | -schema:tool1,tool2>
proxy.exe: xr-racing-app/Library/PackageCache/com.meta.xr.sdk.core@*/Editor/MetaXROperator/Tools~/Windows/meta-xr-operator-mcp-proxy.exe
The app must be in Play mode (Operator's SSE server on 127.0.0.1:8720 only exists while the XR session runs).
A script is Python run with `call(name, **args)` -> result dict, `save_images(result, prefix)`,
`sleep(s)` and `log(msg)` in scope, so a whole interaction runs in one proxy session.
"""
import base64, json, os, subprocess, sys, threading, time, queue

proxy, target = sys.argv[1], sys.argv[2]
# Screenshots go to <repo>/test-captures/<OUT or "latest"> (gitignored).
out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "test-captures", os.environ.get("OUT", "latest"))
os.makedirs(out_dir, exist_ok=True)

p = subprocess.Popen([proxy, "http://127.0.0.1:8720"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                     stderr=subprocess.DEVNULL, text=True, encoding="utf-8", bufsize=1)
q = queue.Queue()
threading.Thread(target=lambda: [q.put(l) for l in p.stdout], daemon=True).start()
_id = 0

def rpc(method, params=None, notify=False, timeout=60):
    global _id
    msg = {"jsonrpc": "2.0", "method": method}
    if params is not None: msg["params"] = params
    if not notify:
        _id += 1; msg["id"] = _id
    p.stdin.write(json.dumps(msg) + "\n"); p.stdin.flush()
    if notify: return None
    end = time.time() + timeout
    while time.time() < end:
        try: line = q.get(timeout=end - time.time())
        except queue.Empty: break
        try: r = json.loads(line)
        except ValueError: continue
        if r.get("id") == _id: return r
    raise TimeoutError(method)

rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "claude-op", "version": "1"}})
rpc("notifications/initialized", notify=True)

def call(name, **args):
    r = rpc("tools/call", {"name": name, "arguments": args})
    return r.get("result", r.get("error"))

def text(result):
    return "\n".join(c.get("text", "") for c in (result or {}).get("content", []) if c.get("type") == "text")

def save_images(result, prefix):
    paths = []
    for i, c in enumerate((result or {}).get("content", [])):
        if c.get("type") == "image":
            path = os.path.join(out_dir, f"{prefix}{'' if i == 0 else '_' + str(i)}.png")
            open(path, "wb").write(base64.b64decode(c["data"])); paths.append(path)
    return paths

def log(*a): print(*a, flush=True)
sleep = time.sleep

try:
    if target.startswith("-schema:"):
        want = target.split(":",1)[1].split(",")
        for t in rpc("tools/list")["result"]["tools"]:
            if t["name"] in want: log(json.dumps(t, indent=1))
    elif target == "-list":
        for t in rpc("tools/list")["result"]["tools"]:
            props = t.get("inputSchema", {}).get("properties", {})
            log(f"- {t['name']}({', '.join(props)}): {t.get('description', '')[:160]}")
    else:
        exec(open(target, encoding="utf-8").read(), {"__file__": os.path.abspath(target), "os": os,"call": call, "text": text, "save_images": save_images,
                                                     "log": log, "sleep": sleep, "json": json})
finally:
    p.stdin.close(); p.terminate()
