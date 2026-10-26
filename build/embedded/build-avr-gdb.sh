#!/usr/bin/env bash
# Ubuntu's gdb-multiarch excludes AVR; gdb-avr 13 predates DAP. Build the real
# upstream AVR target from Ubuntu's signed, maintained gdb-source package.
set -euo pipefail
work=${1:?usage: build-avr-gdb.sh WORK_DIRECTORY [SOURCE_TAR]}
archive=${2:-/usr/src/gdb.tar.xz}
mkdir -p "$work/source" "$work/build"
if [[ ! -f "$work/source/configure" ]]; then
  tar -xJf "$archive" -C "$work/source" --strip-components=1
fi
cd "$work/build"
if [[ ! -f Makefile ]]; then
  "$work/source/configure" --target=avr --disable-nls --disable-werror \
    --disable-sim --disable-binutils --disable-gas --disable-ld --disable-gold \
    --disable-gprofng --without-guile --without-debuginfod --without-babeltrace \
    --without-intel-pt --with-expat --with-python=python3
fi
make -j"${TABSTUDIO_GDB_BUILD_JOBS:-2}" MAKEINFO=true all-gdb
"$work/build/gdb/gdb" --data-directory="$work/build/gdb/data-directory" --batch --quiet --nx -ex 'set architecture avr' -ex 'show architecture'
if [[ -n "${GITHUB_ENV:-}" ]]; then
  printf 'TABSTUDIO_AVR_GDB=%s\n' "$work/build/gdb/gdb" >> "$GITHUB_ENV"
  printf 'TABSTUDIO_AVR_GDB_DATA_DIRECTORY=%s\n' "$work/build/gdb/data-directory" >> "$GITHUB_ENV"
fi
