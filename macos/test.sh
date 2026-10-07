#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$MAC_ROOT"
MAC_CACHE="$MAC_ROOT/.build/module-cache"
MAC_OBJ="$MAC_ROOT/.build/native"
MAC_TARGET="$(uname -m)-apple-macosx15.0"
mkdir -p "$MAC_CACHE" "$MAC_OBJ"
xcrun swiftc -swift-version 5 -target "$MAC_TARGET" -module-cache-path "$MAC_CACHE" \
    -O -whole-module-optimization -parse-as-library -module-name MomoCore \
    -enable-testing -emit-module -emit-module-path "$MAC_OBJ/MomoCore.swiftmodule" \
    -emit-object Sources/MomoCore/*.swift -o "$MAC_OBJ/Core.o"
xcrun swiftc -swift-version 5 -target "$MAC_TARGET" -module-cache-path "$MAC_CACHE" \
    -parse-as-library -I "$MAC_OBJ" \
    Tests/MomoCoreTests/*.swift Tools/TestMain.swift "$MAC_OBJ/Core.o" -o "$MAC_OBJ/CoreTests"
"$MAC_OBJ/CoreTests"
