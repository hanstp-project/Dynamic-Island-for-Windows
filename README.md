# Windows Dynamic Island & Notch for Windows 🏝️

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%20Windows%2011-0078D6?logo=windows&logoColor=white)](https://github.com/hanstp-project/Dynamic-Island-for-Windows)
[![Framework](https://img.shields.io/badge/.NET-10.0%20WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-x64-blue)](#)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Aplikasi floating bar interaktif nan elegan bergaya **Dynamic Island (iPhone)** dan **MacBook Notch** untuk sistem operasi **Windows 10 & Windows 11**. Dibangun secara native menggunakan C# dan WPF (.NET 10) dengan animasi elastis (*spring easing*), konsumsi sumber daya ringan, dan integrasi penuh ke sistem Windows.

---

## 📑 Daftar Isi
- [✨ Fitur Utama](#-fitur-utama)
- [📥 Panduan Download & Instalasi (Pengguna Umum)](#-panduan-download--instalasi-pengguna-umum)
- [🎮 Panduan Lengkap Cara Menggunakan](#-panduan-lengkap-cara-menggunakan)
- [🛠️ Panduan Untuk Pengembang (Build from Source)](#️-panduan-untuk-pengembang-build-from-source)
- [❓ FAQ & Troubleshooting](#-faq--troubleshooting)
- [📜 Lisensi](#-lisensi)

---

## ✨ Fitur Utama

- 🏝️ **Dua Mode Tampilan Premium**:
  - **Dynamic Island**: Terapung bebas dan elegan di bagian atas layar dengan animasi transisi elastis.
  - **MacBook Notch**: Menempel presisi pada bezel atas layar, menyatu sempurna dengan layar laptop/monitor.
- 🎵 **Media Controller & Karaoke Synced Lyrics**:
  - Otomatis sinkron dengan Spotify, Apple Music, YouTube Music (Chrome/Edge), Tidal, dll.
  - **Lirik Otomatis (LRCLIB)**: Lirik tersinkronisasi presisi kata-per-kata secara otomatis.
  - **Click-to-Seek Lirik**: Klik baris lirik mana pun untuk langsung melompat memutar lagu ke detik tersebut.
  - **Interactive Scrubber**: Geser garis durasi lagu dengan presisi tinggi.
- 💬 **Mirroring Notifikasi Windows (WhatsApp, dll.)**:
  - Menangkap notifikasi masuk dari **WhatsApp Desktop/Web**, Telegram, Discord, Teams, Instagram, Email/Outlook, dan Sistem.
  - Dilengkapi badge aplikasi dan warna aksen dinamis.
- 📊 **Notch Control Center / Dashboard**:
  - **Hardware Control**: Atur *Brightness* (kecerahan layar) dan Volume suara secara langsung lewat slider.
  - **Live Hardware Monitor**: Pantau CPU, RAM, Status Baterai, dan Kecepatan Jaringan secara *real-time*.
- 🛠️ **Mini Tools Bawaan**:
  - Jam & kalender digital, prakiraan cuaca, pengingat (*reminders*), countdown timer, dan Retro Polaroid Camera viewfinder.
- 🖥️ **Multi-Monitor Support**:
  - Mendukung tampilan di seluruh layar monitor yang terhubung (*Show on all monitors*).
- 🚀 **Ringan & Portable**:
  - Berjalan sebagai file executable mandiri tanpa perlu instalasi rumit.

---

## 📥 Panduan Download & Instalasi (Pengguna Umum)

Bagi Anda yang hanya ingin langsung memakai aplikasi tanpa perlu menginstal SDK atau koding:

### Langkah 1: Download File Aplikasi
1. Buka halaman rilis resmi di GitHub: 👉 **[Download Windows Dynamic Island (Releases)](https://github.com/hanstp-project/Dynamic-Island-for-Windows/releases)**
2. Pada rilis versi terbaru (misalnya `v1.0.0`), unduh file:
   - **`WindowsDynamicIsland-Portable.zip`** (Disarankan) atau file executable **`WindowsDynamicIsland.exe`**.

### Langkah 2: Ekstrak & Jalankan
1. Ekstrak file `.zip` yang sudah diunduh ke folder yang Anda inginkan (contoh: `C:\Program Files\WindowsDynamicIsland` atau di folder `Documents`).
2. Klik 2x pada file **`WindowsDynamicIsland.exe`**.

### Langkah 3: Penanganan Windows SmartScreen (Penting)
Karena aplikasi ini open source mandiri dan baru dirilis, Windows Defender / SmartScreen mungkin akan menampilkan peringatan:
> *"Windows protected your PC"* atau *"Windows melindungi PC Anda"*

**Cara mengatasinya:**
1. Klik teks **"More info"** (*Info selengkapnya*).
2. Klik tombol **"Run anyway"** (*Tetap jalankan*).
3. Aplikasi akan langsung terbuka dan pulau Dynamic Island akan muncul di bagian atas layar Anda! 🎉

---

## 🎮 Panduan Lengkap Cara Menggunakan

### 1. Berpindah Antara Mode Dynamic Island dan Notch
- Arahkan kursor (*hover*) atau klik pada pulau untuk memunculkan panel kontrol.
- Klik ikon toggle mode di pojok atas (atau klik kanan pada ikon tray di taskbar -> pilih **Toggle Notch / Island Mode**).
- Mode **Island** akan melayang bebas di layar, sedangkan mode **Notch** akan menempel di bibir bezel atas monitor layaknya MacBook.

### 2. Mengontrol Musik & Sinkronisasi Lirik
- Putar lagu kesukaan Anda di **Spotify**, **Apple Music**, atau browser (**YouTube Music** di Chrome/Edge/Firefox).
- Dynamic Island akan otomatis menampilkan judul lagu, nama artis, dan cover album.
- **Melihat Lirik**: Lirik akan otomatis terunduh dan bergerak selaras dengan alunan vokal penyanyi.
- **Click-to-Seek**: Ingin mengulang bagian reff atau bait tertentu? Cukup **klik pada bait lirik tersebut**, pemutaran musik akan langsung melompat ke detik bait itu dinyanyikan!
- **Durasi Scrubber**: Geser titik/garis durasi di bawah judul lagu untuk memajukan atau memundurkan lagu.

### 3. Notifikasi WhatsApp & Media Sosial
- Pastikan notifikasi Windows untuk WhatsApp/Telegram dalam status aktif di Windows Settings.
- Saat ada pesan masuk, pulau akan melebar (*expand*) secara halus menampilkan foto pengirim, nama, dan ringkasan isi pesan.
- Klik pada notifikasi untuk membukanya, atau biarkan beberapa detik hingga pulau kembali ke ukuran minimalis.

### 4. Notch Control Center & Pengatur Brightness/Volume
- Buka Control Center dengan mengeklik pulau saat tidak memutar media atau lewat ikon Dashboard.
- **Slider Kecerahan**: Geser slider matahari untuk mengatur terang-gelap layar monitor Anda.
- **Slider Volume**: Geser slider speaker untuk mengatur volume suara laptop/PC.
- **Status Hardware**: Pantau beban kerja prosesor (CPU), memori (RAM), baterai laptop, dan aktivitas download/upload internet Anda.

### 5. Pengaturan di System Tray (Pojok Kanan Bawah)
Cari ikon pulau di area notifikasi Taskbar (dekat jam Windows):
- **Klik Kanan Ikon**:
  - **Run on Windows Startup**: Centang opsi ini agar Dynamic Island otomatis aktif setiap kali laptop/PC dinyalakan.
  - **Show on all monitors**: Centang jika Anda menggunakan lebih dari satu monitor dan ingin pulau muncul di semua layar.
  - **Settings**: Buka jendela pengaturan posisi dan preferensi.
  - **Exit**: Menutup aplikasi sepenuhnya.

---

## 🛠️ Panduan Untuk Pengembang (Build from Source)

Jika Anda seorang programmer yang ingin memodifikasi kode atau berkontribusi pada proyek ini:

### Prasyarat:
- **Sistem Operasi**: Windows 10 (Build 17763+ / versi 1809+) atau Windows 11 (x64)
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Editor**: Visual Studio 2022+ / Visual Studio Code / JetBrains Rider

### Langkah Menjalankan:
```powershell
# 1. Clone repository
git clone https://github.com/hanstp-project/Dynamic-Island-for-Windows.git

# 2. Masuk ke direktori proyek
cd "Dynamic Island for Windows"

# 3. Jalankan aplikasi secara langsung
.\run.ps1
```

### Mem-build Versi Standalone Portable (.exe):
Jalankan skrip otomasi rilis berikut:
```powershell
.\publish.ps1
```
Skrip akan secara otomatis mengompilasi file single-file executable mandiri dan menghasilkan `WindowsDynamicIsland-Portable.zip` di dalam folder `publish/`.

---

## ❓ FAQ & Troubleshooting

<details>
<summary><b>1. Mengapa notifikasi WhatsApp tidak muncul di Dynamic Island?</b></summary>
Pastikan fitur notifikasi toast Windows diaktifkan:
1. Buka <i>Windows Settings</i> -> <i>System</i> -> <i>Notifications</i>.
2. Pastikan notifikasi dalam keadaan <b>On</b>.
3. Pastikan aplikasi WhatsApp Desktop diizinkan mengirim notifikasi banner/toast.
</details>

<details>
<summary><b>2. Mengapa lirik lagu tidak muncul?</b></summary>
Aplikasi mengambil lirik secara otomatis dari database publik LRCLIB berdasarkan judul lagu dan artis yang dilaporkan oleh pemutar media. Pastikan koneksi internet Anda aktif dan metadata lagu di pemutar media memiliki judul serta nama artis yang jelas.
</details>

<details>
<summary><b>3. Bagaimana cara memindahkan posisi pulau jika terhalang webcam?</b></summary>
Anda dapat mengatur pergeseran vertikal (*vertical offset*) melalui menu <i>Settings</i> atau klik kanan ikon Tray untuk menyesuaikan jarak pulau dari tepi atas layar.
</details>

<details>
<summary><b>4. Apakah aplikasi ini memakan banyak baterai atau RAM?</b></summary>
Tidak. Aplikasi ini dibangun secara native dengan C# WPF dan menggunakan timer adaptif yang menurunkan konsumsi daya saat idle, sehingga sangat efisien dan ramah baterai laptop.
</details>

---

## 🤝 Kontribusi

Kontribusi selalu diterima dengan senang hati!
1. Fork repository ini.
2. Buat branch fitur baru (`git checkout -b feature/FiturKeren`).
3. Commit perubahan Anda (`git commit -m 'feat: Menambahkan fitur keren'`).
4. Push ke branch (`git push origin feature/FiturKeren`).
5. Buat **Pull Request**.

---

## 📜 Lisensi
Hak Cipta © 2026 **hanstp project** (`hanstp_projects`).


