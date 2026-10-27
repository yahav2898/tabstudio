#!/usr/bin/env python3
"""Independently read C#-written synthetic FAT files using GNU mtools, never a hardware disk.

Set TABSTUDIO_FAT_PROOF to a scratch folder when running StorageTests, then invoke
this script on that folder. MTYPE may select a locally extracted signed-package binary.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    args = parser.parse_args()
    metadata = sorted(args.folder.glob("fat*.json"))
    if len(metadata) < 6:
        raise RuntimeError("All FAT12/16/32 superfloppy and MBR proof images must be present")
    for path in metadata:
        item = json.loads(path.read_text())
        original = Path(item["image"])
        image = original.resolve()
        if original.is_symlink() or not image.is_file():
            raise RuntimeError("Independent proof reads regular synthetic image files only")
        result = subprocess.run([os.environ.get("MTYPE", "mtype"), "-i",
                                 str(image) + "@@" + str(item["offset"]), "::" + item["file"]],
                                check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
        expected = Path(item["expected"]).read_bytes()
        if result.stdout != expected:
            raise RuntimeError("GNU mtools readback differs for " + path.name)
        print(path.stem + ": independent mtools readback matches " + hashlib.sha256(expected).hexdigest())


if __name__ == "__main__":
    main()
