set -x
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq && apt-get install -y -qq binutils git gnupg2 libc6-dev libcurl4-openssl-dev libedit2 libgcc-13-dev libncurses-dev libpython3-dev libsqlite3-0 libstdc++-13-dev libxml2-dev libz3-dev pkg-config tzdata unzip zlib1g-dev python3 curl file > /tmp/apt.log 2>&1 || tail -5 /tmp/apt.log
arch=$(uname -m); suffix=$([ "$arch" = aarch64 ] && echo -aarch64)
url=https://download.swift.org/swift-6.4.1-release/ubuntu2404$suffix/swift-6.4.1-RELEASE/swift-6.4.1-RELEASE-ubuntu24.04$suffix.tar.gz
curl -fsSL "$url" -o /tmp/swift.tgz
mkdir -p /opt/swift && tar -xzf /tmp/swift.tgz -C /opt/swift --strip-components=1
export PATH=/opt/swift/usr/bin:$PATH
swift build --help 2>&1 | grep -i -A2 "build-system\|debug-info" | head -20
mkdir -p /tmp/t && cd /tmp/t
cat > Package.swift <<'P'
// swift-tools-version:6.0
import PackageDescription
let package = Package(
    name: "Greeter",
    targets: [
        .target(name: "GreeterCore"),
        .executableTarget(name: "Greeter", dependencies: ["GreeterCore"]),
        .testTarget(name: "GreeterCoreTests", dependencies: ["GreeterCore"]),
        .testTarget(name: "XCTests", dependencies: ["GreeterCore"]),
    ]
)
P
mkdir -p Sources/Greeter Sources/GreeterCore Tests/GreeterCoreTests Tests/XCTests
cat > Sources/GreeterCore/Greeting.swift <<'P'
public func greet(_ name: String) -> String {
    let answer = 42
    let message = "Hello, \(name)! The answer is \(answer)."
    return message
}
P
cat > Sources/Greeter/main.swift <<'P'
import GreeterCore

let name = "Ada"
print(greet(name))
P
cat > Tests/GreeterCoreTests/GreetingTests.swift <<'P'
import Testing
@testable import GreeterCore

@Test func greetsByName() {
    #expect(greet("Ada").hasPrefix("Hello, Ada!"))
}

struct Arithmetic {
    @Test("Adds") func adds() { #expect(1 + 1 == 2) }
    @Test func fails() { #expect(1 + 1 == 3) }
}
P
cat > Tests/XCTests/XTests.swift <<'P'
import XCTest
@testable import GreeterCore

final class XTests: XCTestCase {
    func testGreets() { XCTAssertTrue(greet("Ada").hasPrefix("Hello")) }
    func testFails() { XCTAssertEqual(1, 2) }
}
P
dbg() {
  echo "=== $1"; exe=$2
  ls -la "$exe"; file -L "$exe"
  readelf -S -W "$exe" | grep -E "debug|gnu_debuglink" || echo "no debug sections"
  lldb --batch -o "target create $exe" -o "b main.swift:4" -o "b Greeting.swift:4" -o run -o "frame variable" -o bt -o quit 2>&1 | tail -25
}
swift build 2>&1 | tail -3
dbg default .build/debug/Greeter
ls -la .build; readlink -f .build/debug
find .build -name "*.o" -path "*Greeting*" | head -3 | xargs -r readelf -S -W | grep debug_info
rm -rf .build
swift build --build-system native 2>&1 | tail -3
dbg native .build/debug/Greeter
rm -rf .build
swift build -debug-info-format dwarf 2>&1 | tail -3
dbg dwarf-flag .build/debug/Greeter
rm -rf .build
swift build -Xswiftc -g -Xlinker --no-strip-debug 2>&1 | tail -3 ; dbg xswiftc .build/debug/Greeter
mkdir -p /tmp/l && cd /tmp/l && printf 'func g() -> Int {\n    let a = 42\n    return a\n}\nprint(g())\n' > hello.swift && swiftc -g -Onone hello.swift -o hello && dbg swiftc ./hello
cd /tmp/t && rm -rf .build
echo "=== tests"
swift test --parallel --xunit-output /tmp/r/swift.xml 2>&1 | tail -15
ls -la /tmp/r; for f in /tmp/r/*.xml; do echo "--- $f"; cat "$f"; done
swift test --parallel --xunit-output /tmp/r2/swift.xml --filter 'GreeterCoreTests\.Arithmetic[./]adds(?:\(|$)|XTests\.XTests[./]testGreets(?:\(|$)' 2>&1 | tail -8
for f in /tmp/r2/*.xml; do echo "--- $f"; cat "$f"; done
python3 /probe/swift_dap.py /tmp/t
