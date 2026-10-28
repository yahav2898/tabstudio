import os, sys, time
sys.path.insert(0, os.path.dirname(__file__))
from rpc import Lsp
root = '/tmp/klsp'; os.makedirs(root + '/src/main/kotlin', exist_ok=True)
open(root + '/settings.gradle.kts', 'w').write('rootProject.name = "greeter"\n')
open(root + '/build.gradle.kts', 'w').write('plugins {\n    kotlin("jvm") version "2.4.21"\n    application\n}\nrepositories { mavenCentral() }\napplication { mainClass.set("MainKt") }\n')
src = root + '/src/main/kotlin/Main.kt'
open(src, 'w').write('fun main() {\n    val message = "Hello"\n    println(message.leng)\n}\n')
cmd = sys.argv[1:]
t = time.time()
lsp = Lsp(cmd, cwd=root)
r = lsp.initialize(root)
print('KOTLIN-LSP initialized in %.0fs' % (time.time() - t), list((r.get('result') or {}).get('capabilities', {}).keys()), flush=True)
lsp.open(src, 'kotlin')
ok = lsp.complete(src, 2, 24, 'length', 60)
print('RESULT kotlin-lsp completion', ok, 'after %.0fs' % (time.time() - t), flush=True)
