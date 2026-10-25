#!/bin/bash
set -e

echo "=== [1/5] Building Swift Release Executable ==="
swift build -c release

echo "=== [2/5] Locating Binary ==="
BIN_DIR=$(swift build -c release --show-bin-path)
echo "SPM Bin Directory: $BIN_DIR"
EXEC_FILE="$BIN_DIR/MacDynamicIsland"

if [ ! -f "$EXEC_FILE" ]; then
    echo "Warning: Not found at $EXEC_FILE, searching .build directory..."
    for f in $(find .build -type f -name "MacDynamicIsland"); do
        if [ -x "$f" ]; then
            EXEC_FILE="$f"
            break
        fi
    done
fi

echo "Selected Executable: $EXEC_FILE"
if [ ! -f "$EXEC_FILE" ]; then
    echo "ERROR: Could not locate MacDynamicIsland binary!"
    find .build -name "*MacDynamicIsland*"
    exit 1
fi

echo "=== [3/5] Assembling MacDynamicIsland.app ==="
rm -rf MacDynamicIsland.app MacDynamicIsland-macOS.zip
mkdir -p MacDynamicIsland.app/Contents/MacOS
mkdir -p MacDynamicIsland.app/Contents/Resources

cp "$EXEC_FILE" MacDynamicIsland.app/Contents/MacOS/MacDynamicIsland
chmod +x MacDynamicIsland.app/Contents/MacOS/MacDynamicIsland

if [ -f "Info.plist" ]; then
    cp Info.plist MacDynamicIsland.app/Contents/Info.plist
else
    echo "ERROR: Info.plist not found in current directory!"
    exit 1
fi

echo "=== [4/5] Ad-hoc Codesigning App Bundle ==="
codesign --force --deep --sign - MacDynamicIsland.app || echo "Notice: Ad-hoc codesign skipped or non-fatal."

echo "=== [5/5] Creating MacDynamicIsland-macOS.zip ==="
zip -r -y MacDynamicIsland-macOS.zip MacDynamicIsland.app

echo "=== Build and Package Succeeded! ==="
ls -lh MacDynamicIsland-macOS.zip
