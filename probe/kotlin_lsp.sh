set -x
arch=$1   # aarch64 or amd64
v=263.7084.0; n=kotlin-server-0.0.27-linux-$arch.vsix
curl -fsSL "https://download.jetbrains.com/language-server/kotlin-server/$v/$n" -o /tmp/$n
curl -fsSL "https://download.jetbrains.com/language-server/kotlin-server/$v/$n.sha256"; echo
echo "HASH $(sha256sum /tmp/$n) SIZE $(stat -c %s /tmp/$n)"
rm -rf /tmp/kls && mkdir /tmp/kls && cd /tmp/kls && unzip -q /tmp/$n
find . -maxdepth 4 -type d | head -40
find . -maxdepth 5 \( -name '*.sh' -o -name 'kotlin-lsp*' -o -name 'LICENSE*' -o -name 'NOTICE*' -o -name 'release' \) | head -20
for f in $(find . -maxdepth 5 -name 'kotlin-lsp.sh' -o -maxdepth 5 -name 'kotlin-lsp' -type f | head -2); do echo "== $f"; head -c 3000 $f; echo; done
cat extension/package.json | head -c 1500; echo
du -sh /tmp/kls; ls extension/server 2>/dev/null | head; ls extension/server/jre* 2>/dev/null | head
