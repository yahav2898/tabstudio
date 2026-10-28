arch=$1   # aarch64 or amd64
v=263.7084.0; n=kotlin-server-0.0.27-linux-$arch.vsix
curl -fsSL "https://download.jetbrains.com/language-server/kotlin-server/$v/$n" -o /tmp/$n
echo "HASH $(sha256sum /tmp/$n) SIZE $(stat -c %s /tmp/$n)"
rm -rf /tmp/kls && mkdir /tmp/kls && cd /tmp/kls && unzip -q /tmp/$n
echo "== LICENSE.txt"; head -c 2500 extension/LICENSE.txt; echo
echo "== server/license"; ls extension/server/license | head -30
cat extension/server/build.txt; head -c 1500 extension/server/product-info.json; echo
ls extension/server/bin; file extension/server/lib/rocksdbjni/* 2>/dev/null | head -5; find extension/server/lib -name '*.so' | head -10
