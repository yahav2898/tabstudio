#!/usr/bin/env bash
# Names the functions in the native backtraces of Android tombstones (the "F DEBUG ... #NN pc <offset> <library>"
# lines logcat prints when the app aborts) for the .NET runtime's own libraries, such as Mono's garbage collector in
# libmonosgen-2.0.so. Release builds carry stripped libraries, so a backtrace has only offsets; this takes the
# libraries out of the APK, downloads their symbol files from Microsoft's symbol server by build id
# (dotnet-symbol), and appends each frame's function, file and line.
#
# Usage: build/ci/android-symbolize.sh <apk> <abi> <output-folder> <file-with-backtraces>...
# Writes <output-folder>/<name>.symbolized.txt for every input file that has a backtrace; prints them too.
set -uo pipefail

apk="${1:?apk}"
abi="${2:?abi, such as x86_64}"
out="${3:?output folder}"
shift 3

work="$(mktemp -d)"
mkdir -p "$out" "$work/lib" "$work/symbols"
unzip -o -q "$apk" "lib/$abi/libmonosgen-2.0.so" "lib/$abi/libmonodroid.so" "lib/$abi/libSystem.Native.so" -d "$work" 2> /dev/null || true

if ! command -v dotnet-symbol > /dev/null; then
  dotnet tool install --global dotnet-symbol > /dev/null 2>&1 || true
  export PATH="$PATH:$HOME/.dotnet/tools"
fi
for library in "$work/lib/$abi"/*.so; do
  [ -f "$library" ] || continue
  build_id="$(readelf -n "$library" | sed -n 's/.*Build ID: //p')"
  echo "$(basename "$library"): build id $build_id"
  dotnet-symbol --symbols --output "$work/symbols" "$library" > /dev/null 2>&1 || echo "  no symbols found for it"
done

for input in "$@"; do
  [ -f "$input" ] && grep -q ' #[0-9][0-9] pc ' "$input" || continue
  name="$(echo "${input#./}" | tr '/' '-')"
  result="$out/${name%.txt}.symbolized.txt"
  frame='#[0-9]+ pc ([0-9a-f]+) +[^ ]*/(lib[A-Za-z0-9._-]+\.so)'
  while IFS= read -r line; do
    echo "$line"
    if [[ "$line" =~ $frame ]]; then
      offset="${BASH_REMATCH[1]}"
      library="${BASH_REMATCH[2]}"
      debug="$(find "$work/symbols" -name "$library.dbg" -o -name "${library%.so}.so.dbg" | head -n 1)"
      if [ -n "$debug" ]; then
        addr2line -f -i -C -e "$debug" "0x$offset" | paste - - | sed 's/^/        -> /'
      fi
    fi
  done < <(grep -E ' #[0-9]+ pc |Abort message|signal [0-9]+|pid: ' "$input") > "$result"
  echo "== $input"
  cat "$result"
done
rm -rf "$work"
