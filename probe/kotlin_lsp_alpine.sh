# kotlin-lsp with Alpine's OpenJDK 25 (musl) instead of its bundled glibc runtime.
apk add --no-cache openjdk25-jdk python3 bash gcompat > /tmp/apk.log 2>&1 || tail /tmp/apk.log
cd /kls/extension/server
python3 - <<'PY'
import json; p=json.load(open('product-info.json')); l=p['launch'][0]
print(json.dumps({k: l[k] for k in l if k in ('os','arch','launcherPath','javaExecutablePath','vmOptionsFilePath','bootClassPathJarNames','additionalJvmArguments','mainClass')}, indent=1)[:4000])
PY
ls bin; cat bin/*.vmoptions 2>/dev/null | head -30
find lib -name '*.so' -o -name '*musl*' | head -20; unzip -l lib/rocksdbjni/*.jar 2>/dev/null | grep -i '\.so' | head; ls lib/rocksdbjni lib/jna lib/pty4j | head -30
