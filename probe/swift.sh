set -x
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq && apt-get install -y -qq binutils git gnupg2 libc6-dev libcurl4-openssl-dev libedit2 libgcc-13-dev libncurses-dev libpython3-dev libsqlite3-0 libstdc++-13-dev libxml2-dev libz3-dev pkg-config tzdata unzip zlib1g-dev python3 curl > /tmp/apt.log 2>&1 || tail -5 /tmp/apt.log
arch=$(uname -m); suffix=$([ "$arch" = aarch64 ] && echo -aarch64)
url=https://download.swift.org/swift-6.4.1-release/ubuntu2404$suffix/swift-6.4.1-RELEASE/swift-6.4.1-RELEASE-ubuntu24.04$suffix.tar.gz
curl -fsSL "$url" -o /tmp/swift.tgz && sha256sum /tmp/swift.tgz
mkdir -p /opt/swift && tar -xzf /tmp/swift.tgz -C /opt/swift --strip-components=1
du -sh /opt/swift; ls /opt/swift/usr/bin | tr '\n' ' '; echo
export PATH=/opt/swift/usr/bin:$PATH
swift --version
mkdir -p /tmp/sw && cd /tmp/sw && swift package init --type executable --name Greeter > /dev/null
ls -R Sources
time swift build 2>&1 | tail -5
swift run Greeter 2>&1 | tail -3
mkdir -p /tmp/swl && cd /tmp/swl && swift package init --type library --name Calc > /dev/null && time swift test 2>&1 | tail -15
python3 /probe/swift_tools.py
