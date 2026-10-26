# Mac Dynamic Island & Notch for macOS 🏝️

[![Platform](https://img.shields.io/badge/Platform-macOS%2013%2B%20%7C%20Ventura%20%7C%20Sonoma%20%7C%20Sequoia-000000?logo=apple&logoColor=white)](https://github.com/hanstp-project/Dynamic-Island-for-Windows)
[![Language](https://img.shields.io/badge/Swift-5.9%2B-F05138?logo=swift&logoColor=white)](https://swift.org)
[![UI](https://img.shields.io/badge/UI-SwiftUI%20%2B%20AppKit-blue)](#)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)

Versi native **macOS** dari Dynamic Island & Notch bar interaktif oleh **hanstp project** (`hanstp_projects`). Dibangun secara native menggunakan **Swift, SwiftUI, dan AppKit** untuk performa super ringan (< 25 MB RAM), hemat baterai, dan animasi elastis (*spring easing*) kelas Apple.

---

## ✨ 7 Fitur Lengkap Sesuai Versi Windows

- 🏝️ **Dukungan Notch Fisik MacBook**:
  - Otomatis mendeteksi keberadaan notch fisik kamera MacBook Pro / MacBook Air (M1, M2, M3, M4) melalui `safeAreaInsets`.
  - Dua mode: **MacBook Notch** (menempel presisi membungkus bezel kamera) dan **Dynamic Island** (melayang anggun di bawah kamera).
- 🎵 **Media Controller & Synced Karaoke Lyrics**:
  - Integrasi native dengan **Spotify** dan **Apple Music**.
  - Sinkronisasi lirik kata-per-kata otomatis (LRCLIB API).
  - **Click-to-Seek**: Klik bait lirik mana pun untuk langsung melompat memutar lagu ke detik tersebut!
  - Garis durasi interaktif (*interactive scrubber*).
- 📸 **Retro Instant Camera (Polaroid)**:
  - Live webcam viewfinder kamera Mac dengan 5 filter retro (*Warm '90s, Cyberpunk, B&W, Sepia, Vibrant*).
  - Tombol shutter foto instant dengan efek kilat (*flash*) dan cetak foto Polaroid otomatis tersimpan di `~/Pictures/PolaroidPhotos/`.
- 📊 **Notch Control Center**:
  - Slider pengatur **Kecerahan Layar (*Brightness*)** dan **Volume Audio** secara langsung.
  - Pemantau beban CPU, RAM, baterai, dan kecepatan jaringan secara real-time.
- 💻 **System Health Monitor**:
  - Indikator CPU multi-core, pemakaian RAM Unified Memory, status baterai laptop, dan sisa kapasitas penyimpanan SSD Macintosh HD.
- 🕒 **Time & Date (Waktu & Kalender)**:
  - Jam digital format besar dengan detik, nama hari, minggu tahunan, zona waktu, dan kalender mini bulanan.
- ⏱️ **Tools (Stopwatch, Countdown Timer, Quick Notes)**:
  - Stopwatch dengan pencatat waktu lap (*lap times*).
  - Countdown timer dengan preset cepat (1m, 5m, 15m, 25m Pomodoro) dan nada lonceng selesai.
  - Quick Notes / scratchpad multi-line yang otomatis tersimpan (*auto-save*).
- ☀️ **Live Weather (Cuaca Real-time)**:
  - Suhu real-time dalam °C, deskripsi cuaca, kelembapan udara, kecepatan angin, dan prakiraan cuaca 4 hari ke depan.
- 💬 **Mirroring Notifikasi Toast**:
  - Menangkap notifikasi masuk dari **WhatsApp Desktop**, Telegram, Discord, dan Sistem dengan badge logo dan warna aksen.
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
