# Windows Dynamic Island 🏝️

Sebuah aplikasi floating bar interaktif bergaya **Dynamic Island** dan **MacBook Notch** untuk sistem operasi **Windows 10 & 11**, dibangun secara native menggunakan C# dan WPF (.NET 10).

---

## ✨ Fitur Utama

- 🏝️ **Dua Mode Tampilan**:
  - **Dynamic Island**: Terapung elegan di bagian atas layar dengan animasi elastis (*spring easing*).
  - **Notch Mode**: Menempel presisi di tepi atas bezel layar layaknya MacBook notch.
- 🎵 **Media Controller & Live Synced Lyrics**:
  - Terintegrasi langsung dengan Windows Media (Apple Music, Spotify, YouTube, Chrome, Edge).
  - **Lirik Otomatis (LRCLIB)**: Sinkronisasi lirik bergaya karaoke Apple Music Sing dengan kompensasi latensi vokal.
  - **Click-to-Seek**: Klik bait lirik atau geser (*scrub*) garis durasi untuk langsung melompat ke detik yang diinginkan.
- 💬 **Mirroring Notifikasi Toast Windows**:
  - Menangkap notifikasi dari **WhatsApp**, Telegram, Discord, Teams, Instagram, Email/Outlook, dan Sistem.
  - Dilengkapi badge ikon dan warna aksen otomatis.
- 📊 **Notch Control Center / Dashboard**:
  - Pemantau beban CPU, RAM, Status Baterai, dan Kecepatan Jaringan.
  - Pengatur *Brightness* layar & volume audio hardware secara langsung.
- 🛠️ **Tools & Quick Actions**:
  - Jam digital, perkiraan cuaca lokal, pengingat (*reminders*), timer/countdown, hingga fitur Retro Polaroid Camera viewfinder.
- 🖥️ **Dukungan Multi-Monitor**:
  - Dapat ditampilkan di seluruh monitor yang terhubung secara bersamaan (*Show on all monitors*).

---

## 🚀 Cara Menjalankan (Development)

### Persyaratan Sistem:
- **Sistem Operasi**: Windows 10 (versi 1809+ / Build 17763+) atau Windows 11 (x64)
- **Runtime / SDK**: .NET 10 SDK

### Menjalankan Aplikasi:
Buka PowerShell di direktori proyek, lalu jalankan:
```powershell
.\run.ps1
```
Skrip ini akan secara otomatis mengompilasi versi terbaru dan menjalankan aplikasi di sistem Anda.

---

## 📦 Build Standalone Executable (.exe Portable)

Untuk membuat file `.exe` mandiri yang bisa langsung dijalankan di PC lain tanpa perlu menginstal .NET SDK:

```powershell
dotnet publish .\src\WindowsDynamicIsland\WindowsDynamicIsland.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```
File hasil publish akan tersedia di folder `publish\WindowsDynamicIsland.exe`.

---

## 📜 Lisensi
Hak Cipta © 2026. Lisensi MIT atau lisensi kontributor proyek.
