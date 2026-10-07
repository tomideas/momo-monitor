#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$MAC_ROOT"
MAC_CACHE="$MAC_ROOT/.build/module-cache"
MAC_OBJ="$MAC_ROOT/.build/native"
mkdir -p "$MAC_CACHE" "$MAC_OBJ"
python3 Tools/prepare-resources.py
MAC_TARGET="$(uname -m)-apple-macosx15.0"
xcrun clang -target "$MAC_TARGET" -I Sources/CMomoSMC/include -c Sources/CMomoSMC/MomoSMC.c -o "$MAC_OBJ/SMC.o"
xcrun swiftc -swift-version 5 -target "$MAC_TARGET" -module-cache-path "$MAC_CACHE" \
    -O -whole-module-optimization -parse-as-library -module-name MomoCore \
    -enable-testing -emit-module -emit-module-path "$MAC_OBJ/MomoCore.swiftmodule" \
    -emit-object Sources/MomoCore/*.swift -o "$MAC_OBJ/Core.o"
xcrun swiftc -swift-version 5 -target "$MAC_TARGET" -module-cache-path "$MAC_CACHE" \
    -O -parse-as-library -I "$MAC_OBJ" -I Sources/CMomoSMC/include \
    Sources/MomoMac/*.swift "$MAC_OBJ/Core.o" "$MAC_OBJ/SMC.o" -framework IOKit -o "$MAC_OBJ/MomoMac"
MAC_APP="$MAC_ROOT/build/Momo Monitor.app"
mkdir -p "$MAC_APP/Contents/MacOS" "$MAC_APP/Contents/Resources"
cp "$MAC_OBJ/MomoMac" "$MAC_APP/Contents/MacOS/MomoMac"
cp Info.plist "$MAC_APP/Contents/Info.plist"
cp -R Sources/MomoMac/Resources/. "$MAC_APP/Contents/Resources/"
# External/exFAT volumes create AppleDouble sidecars which codesign treats as
# unsigned nested code. Remove only sidecars in this generated application.
python3 - "$MAC_APP" <<'PY'
from pathlib import Path
import sys
for path in Path(sys.argv[1]).rglob('._*'):
    if path.is_file(): path.unlink()
PY
codesign --force --sign - "$MAC_APP"
printf 'Built: %s\n' "$MAC_APP"
if [[ "${1:-}" == "--open" ]]; then open "$MAC_APP"; fi
