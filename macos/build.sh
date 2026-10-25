#!/bin/bash
# Script untuk menjalankan Mac Dynamic Island secara langsung menggunakan Swift Package Manager

echo "========================================================"
echo "    🏝️ Mac Dynamic Island - Build & Run Script"
echo "========================================================"

cd "$(dirname "$0")"

echo "[1/2] Memeriksa Xcode Command Line Tools..."
if ! command -v swift &> /dev/null; then
    echo "[Error] Swift compiler tidak ditemukan!"
    echo "Silakan install Xcode dari App Store atau jalankan: xcode-select --install"
    exit 1
fi

echo "[2/2] Membangun dan menjalankan aplikasi..."
swift run
