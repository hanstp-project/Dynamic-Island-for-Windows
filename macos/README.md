# Mac Dynamic Island & Notch for macOS 🏝️

[![Platform](https://img.shields.io/badge/Platform-macOS%2013%2B%20%7C%20Ventura%20%7C%20Sonoma%20%7C%20Sequoia-000000?logo=apple&logoColor=white)](https://github.com/hanstp-project/Dynamic-Island-for-Windows)
[![Language](https://img.shields.io/badge/Swift-5.9%2B-F05138?logo=swift&logoColor=white)](https://swift.org)
[![UI](https://img.shields.io/badge/UI-SwiftUI%20%2B%20AppKit-blue)](#)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)

Versi native **macOS** dari Dynamic Island & Notch bar interaktif oleh **hanstp project** (`hanstp_projects`). Dibangun secara native menggunakan **Swift, SwiftUI, dan AppKit** untuk performa super ringan (< 25 MB RAM), hemat baterai, dan animasi elastis (*spring easing*) kelas Apple.

---

## ✨ Fitur Khusus macOS

- 🏝️ **Dukungan Notch Fisik MacBook**:
  - Otomatis mendeteksi keberadaan notch fisik kamera MacBook Pro / MacBook Air (M1, M2, M3, M4) melalui `safeAreaInsets`.
  - Dua mode: **MacBook Notch** (menempel presisi membungkus bezel kamera) dan **Dynamic Island** (melayang anggun di bawah kamera).
- 🎵 **Media Controller & Synced Karaoke Lyrics**:
  - Integrasi native dengan **Spotify** dan **Apple Music**.
  - Sinkronisasi lirik kata-per-kata otomatis (LRCLIB API).
  - **Click-to-Seek**: Klik bait lirik mana pun untuk langsung melompat memutar lagu ke detik tersebut!
  - Garis durasi interaktif (*interactive scrubber*).
- 💬 **Mirroring Notifikasi Toast**:
  - Menangkap notifikasi masuk dari **WhatsApp Desktop**, Telegram, Discord, dan Sistem dengan badge logo dan warna aksen.
- 📊 **Notch Control Center**:
  - Slider pengatur **Kecerahan Layar (*Brightness*)** dan **Volume Audio** secara langsung.
  - Pemantau beban CPU, RAM, baterai, dan kecepatan jaringan secara real-time.
- ⚙️ **Menu Bar Integration**:
  - Berjalan sebagai *accessory overlay* di latar belakang tanpa mengotori Dock Mac.
  - Ikon Menu Bar di pojok kanan atas untuk akses cepat ganti mode dan pengaturan startup.

---

## 🚀 Cara Menjalankan di Mac

### Prasyarat:
- Mac dengan macOS 13.0 (Ventura), macOS 14 (Sonoma), atau macOS 15 (Sequoia)+
- Xcode 15+ atau Xcode Command Line Tools (`xcode-select --install`)

### Opsi 1: Lewat Terminal (Paling Cepat)
```bash
# 1. Buka folder macos
cd macos

# 2. Jalankan skrip build
chmod +x build.sh
./build.sh
```

### Opsi 2: Buka di Xcode
1. Buka folder `macos` di Finder.
2. Klik 2x pada file `Package.swift` (Xcode akan otomatis terbuka).
3. Klik tombol **Run (▶️)** di pojok kiri atas Xcode.

---

## 📜 Lisensi
Hak Cipta © 2026 **hanstp project** (`hanstp_projects`).
Didistribusikan di bawah lisensi MIT License.
