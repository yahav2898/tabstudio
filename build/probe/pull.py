import hashlib, json, os, sys, tarfile, urllib.request, urllib.parse, time, io, shutil, stat
def req(url, headers={}):
    r = urllib.request.Request(url, headers=headers)
    return urllib.request.urlopen(r, timeout=120)
def token(realm, service, scope):
    u = f"{realm}?service={service}&scope={scope}"
    return json.load(req(u))["token"]
def pull(ref, out, arch):
    repo, tag = ref.split(":")
    if "/" not in repo: repo = "library/" + repo
    reg = "https://registry-1.docker.io"
    t = token("https://auth.docker.io/token", "registry.docker.io", f"repository:{repo}:pull")
    h = {"Authorization": "Bearer " + t, "Accept": ",".join([
        "application/vnd.oci.image.index.v1+json","application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.manifest.v1+json","application/vnd.docker.distribution.manifest.v2+json"])}
    m = json.load(req(f"{reg}/v2/{repo}/manifests/{tag}", h))
    if "manifests" in m:
        d = next(x["digest"] for x in m["manifests"] if x.get("platform",{}).get("architecture")==arch and x["platform"].get("os")=="linux")
        m = json.load(req(f"{reg}/v2/{repo}/manifests/{d}", h))
    cfg = json.load(req(f"{reg}/v2/{repo}/blobs/{m['config']['digest']}", h))
    os.makedirs(out, exist_ok=True)
    for layer in m["layers"]:
        data = req(f"{reg}/v2/{repo}/blobs/{layer['digest']}", h).read()
        assert "sha256:" + hashlib.sha256(data).hexdigest() == layer["digest"]
        with tarfile.open(fileobj=io.BytesIO(data)) as tf:
            for mem in tf.getmembers():
                name = os.path.normpath(mem.name).lstrip("/")
                base = os.path.basename(name); parent = os.path.dirname(name)
                if base == ".wh..wh..opq":
                    p = os.path.join(out, parent)
                    for e in os.listdir(p) if os.path.isdir(p) else []:
                        q = os.path.join(p, e); shutil.rmtree(q) if os.path.isdir(q) and not os.path.islink(q) else os.remove(q)
                    continue
                if base.startswith(".wh."):
                    q = os.path.join(out, parent, base[4:])
                    if os.path.lexists(q): shutil.rmtree(q) if os.path.isdir(q) and not os.path.islink(q) else os.remove(q)
                    continue
                dest = os.path.join(out, name)
                if os.path.lexists(dest) and not (mem.isdir() and os.path.isdir(dest)):
                    shutil.rmtree(dest) if os.path.isdir(dest) and not os.path.islink(dest) else os.remove(dest)
                if mem.islnk():
                    os.makedirs(os.path.dirname(dest), exist_ok=True)
                    shutil.copy2(os.path.join(out, os.path.normpath(mem.linkname).lstrip("/")), dest, follow_symlinks=False); continue
                mem.name = name; mem.uid = mem.gid = os.getuid(); mem.uname = mem.gname = ""
                tf.extract(mem, out, set_attrs=False, numeric_owner=True, filter="fully_trusted")
                if mem.isfile() or mem.isdir(): os.chmod(dest, (mem.mode & 0o7777) | (0o700 if mem.isdir() else 0o600))
    json.dump(cfg["config"], open(out + ".json", "w"))
    print(ref, "layers", len(m["layers"]))
t=time.time(); pull(sys.argv[1], sys.argv[2], sys.argv[3]); print("pull seconds", round(time.time()-t,1))
