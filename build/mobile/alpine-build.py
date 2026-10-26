"""Install the catalog's exact pinned Android SDK into Alpine and execute the IDE's template build plan."""
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import urllib.request

catalog = json.loads(Path('src/TabStudio.Toolchains/Catalog/toolchains.json').read_text())
tool = next(t for t in catalog['toolchains'] if t['id'] == 'android')
subprocess.run(['apk', 'add', '--no-cache', *tool['packages']], check=True)
architecture = {'aarch64': 'aarch64', 'armv7l': 'armv7', 'x86_64': 'x86_64'}[platform.machine()]
environment = os.environ | tool['run']['environment']
for step in tool['setup']:
    if step.get('architectures') and architecture not in step['architectures']:
        continue
    print('Setting up', step['name'], flush=True)
    if archive := step.get('archive'):
        target = Path('/tmp') / Path(archive['url']).name
        urllib.request.urlretrieve(archive['url'], target)
        digest = hashlib.file_digest(target.open('rb'), 'sha256').hexdigest()
        if digest != archive['sha256']:
            raise SystemExit(f"SHA-256 mismatch: {step['name']}: {digest}")
        destination = Path(archive['destination'])
        shutil.rmtree(destination, ignore_errors=True)
        destination.mkdir(parents=True)
        subprocess.run(['unzip', '-q', '-o', str(target), '-d', str(destination)], check=True)
        target.unlink()
    if file := step.get('file'):
        path = Path(file['path'])
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(file['content'])
        if file.get('executable'):
            path.chmod(0o755)
    if command := step.get('command'):
        subprocess.run(command, env=environment, check=True)

plan = json.loads(Path(sys.argv[1], 'build-plan.json').read_text())
subprocess.run([plan['Program'], *plan['Arguments']], cwd=plan['WorkingDirectory'], env=os.environ | plan['Environment'], check=True)
apks = list(Path(sys.argv[1]).glob('*/build/outputs/apk/debug/*.apk'))
if not apks:
    raise SystemExit('Gradle did not produce the Android template APK')
for apk in apks:
    print('Built APK:', apk, apk.stat().st_size, 'bytes')
