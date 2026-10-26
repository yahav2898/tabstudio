#!/usr/bin/env python3
"""Run actual signed-package embedded compilers and pinned front ends in Alpine (no target hardware)."""
import hashlib
import json
import os
import pathlib
import platform
import shutil
import struct
import subprocess
import tarfile
import tempfile
import urllib.request


def run(*args):
    print('>', *args, flush=True)
    subprocess.run(args, check=True)


def main():
    root = pathlib.Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix='tabstudio-embedded-') as temporary:
        work = pathlib.Path(temporary)
        source = work / 'firmware.c'
        source.write_text('volatile unsigned counter; int main(void) { for (;;) ++counter; }\n')
        targets = [
            ('arm-none-eabi-gcc', ['-mcpu=cortex-m3', '-mthumb', '-nostdlib', '-Wl,-e,main', '-Wl,-Ttext=0x08000000'], 40),
            ('riscv-none-elf-gcc', ['-march=rv32imac', '-mabi=ilp32', '-nostdlib', '-Wl,-e,main', '-Wl,-Ttext=0x80000000'], 243),
            ('avr-gcc', ['-mmcu=atmega328p'], 83),
        ]
        for compiler, flags, machine in targets:
            elf = work / (compiler + '.elf')
            run(compiler, *flags, '-g', '-Os', '-fstack-usage', str(source), '-o', str(elf))
            header = elf.read_bytes()[:64]
            assert header[:4] == b'\x7fELF' and header[5] in (1, 2)
            assert struct.unpack_from('<H' if header[5] == 1 else '>H', header, 18)[0] == machine
            print(json.dumps({'compiler': compiler, 'elfMachine': machine, 'sha256': hashlib.sha256(elf.read_bytes()).hexdigest(), 'hardwareVerified': False}))

        for name, module in [('platformio', 'platformio'), ('mpremote', 'mpremote')]:
            venv = work / name
            run('python3', '-m', 'venv', str(venv))
            python = str(venv / 'bin/python')
            run(python, '-m', 'pip', 'install', '--only-binary=:all:', '--require-hashes', '-r', str(root / 'build/embedded' / (name + '.requirements.txt')))
            run(python, '-m', module, '--version' if name == 'platformio' else '--help')

        catalog = json.loads((root / 'src/TabStudio.Toolchains/Catalog/toolchains.json').read_text())
        arduino = next(item for item in catalog['toolchains'] if item['id'] == 'arduino')
        architecture = {'arm64': 'aarch64', 'AMD64': 'x86_64'}.get(platform.machine(), platform.machine())
        archive = next(step['archive'] for step in arduino['setup'] if architecture in step.get('architectures', []))
        archive_path = work / 'arduino.tar.gz'
        with urllib.request.urlopen(archive['url'], timeout=60) as response, archive_path.open('wb') as output:
            shutil.copyfileobj(response, output)
        assert hashlib.sha256(archive_path.read_bytes()).hexdigest() == archive['sha256']
        destination = work / 'arduino'
        destination.mkdir()
        with tarfile.open(archive_path) as package:
            package.extractall(destination, filter='data')
        run(str(destination / 'arduino-cli'), 'version')
        run('gdb-multiarch', '--batch', '--quiet', '--nx', '-ex', "python import gdb,importlib.util; assert importlib.util.find_spec('gdb.dap'); print(gdb.architecture_names())")
        run('python3', '-m', 'unittest', 'discover', '-s', str(root / 'build/embedded/tests'), '-p', 'test_bridges.py', '-v')
        print('EMBEDDED_ALPINE_PASSED', architecture)


if __name__ == '__main__':
    main()
