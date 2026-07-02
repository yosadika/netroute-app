# Product Requirements Document — NetRoute Manager

## Overview

NetRoute Manager adalah aplikasi desktop Windows yang memungkinkan pengguna mengikat aplikasi tertentu ke network adapter pilihan saat diluncurkan. Aplikasi ini menghilangkan kebutuhan untuk menjalankan perintah `route add` secara manual di Command Prompt setiap kali ingin menggunakan jaringan tertentu untuk aplikasi spesifik.

---

## Problem Statement

Windows tidak menyediakan mekanisme bawaan untuk mengarahkan traffic satu aplikasi ke network adapter tertentu tanpa intervensi tingkat kernel. Pengguna yang memiliki beberapa koneksi jaringan aktif secara bersamaan (misalnya Wi-Fi dan VPN, atau dua ISP berbeda) tidak dapat mengontrol adapter mana yang digunakan oleh aplikasi tertentu tanpa manipulasi routing manual yang kompleks dan tidak persisten.

---

## Goals

- Memudahkan pengguna mengikat aplikasi ke network adapter tanpa pengetahuan teknis routing
- Menjaga ikatan adapter selama proses aplikasi berjalan dan merestorasi konfigurasi saat aplikasi ditutup
- Menyediakan UI yang bersih, intuitif, dan profesional untuk manajemen konfigurasi multi-aplikasi
- Tidak meninggalkan perubahan permanen pada konfigurasi sistem setelah aplikasi atau binding ditutup

## Non-Goals

- Routing per-paket atau per-koneksi tingkat kernel (membutuhkan WFP driver)
- Mendukung sistem operasi selain Windows
- Menyediakan fitur firewall atau filtering traffic
- Routing untuk proses anak (child process) dari aplikasi yang diluncurkan

---

## User Personas

**Pengguna Utama:** IT professional, network engineer, atau power user yang mengoperasikan beberapa koneksi jaringan secara bersamaan dan perlu mengarahkan aplikasi tertentu ke jalur jaringan spesifik (misalnya: aplikasi kantor via VPN, browser via ISP langsung).

---

## Features

### F-01 — Configured Apps Management

Pengguna dapat mendaftarkan daftar aplikasi beserta konfigurasi adapter yang diinginkan.

**Requirements:**
- Setiap konfigurasi menyimpan: App Name, Executable Path, Network Adapter, Arguments (opsional)
- Konfigurasi disimpan secara persisten ke file `launchers.json` di direktori aplikasi
- Pengguna dapat menambah, mengedit, dan menghapus konfigurasi
- Mendukung seleksi multi-item via checkbox untuk operasi edit dan hapus massal
- Operasi edit massal membatasi field yang dapat diubah menjadi adapter saja (nama/path tidak dapat di-bulk-edit)

### F-02 — App Launcher

Pengguna dapat meluncurkan aplikasi yang sudah terdaftar dengan adapter yang terikat.

**Requirements:**
- Sebelum meluncurkan, adapter target di-set ke metric=1 (prioritas tertinggi)
- Semua adapter lain di-set ke metric=9999 (prioritas terendah) untuk memaksa OS menggunakan adapter target
- Aplikasi diluncurkan dengan `UseShellExecute = true` agar UAC dan asosiasi file berjalan normal
- Proses dipantau secara async; saat proses keluar, metric semua adapter direstorasi ke nilai semula
- Snapshot metric diambil sebelum manipulasi dilakukan

### F-03 — Active Bindings Monitor

Pengguna dapat melihat aplikasi yang sedang berjalan dalam status terikat adapter.

**Requirements:**
- Tabel Active Bindings diperbarui setiap 1 detik via timer
- Menampilkan: App Name, PID, Adapter, Running Since
- Pengguna dapat merestorasi adapter secara manual (Release Adapter) tanpa menunggu proses keluar
- Tinggi panel Active Bindings dapat diatur secara fleksibel via splitter (SplitContainer)

### F-04 — Installed Apps Picker

Saat menambah konfigurasi baru, pengguna dapat memilih dari daftar aplikasi yang terinstall.

**Requirements:**
- Daftar diambil dari Windows Registry (HKLM dan HKCU Uninstall keys)
- Menampilkan icon, nama, dan path executable setiap aplikasi
- Mendukung filter/search real-time
- Icon dimuat di background thread untuk menjaga responsivitas UI
- Alternatif: pengguna dapat browse .exe secara manual via OpenFileDialog

### F-05 — Administrator Privilege

Manipulasi metric adapter membutuhkan hak administrator.

**Requirements:**
- Aplikasi mendeteksi apakah berjalan sebagai administrator saat startup
- Jika tidak, aplikasi melakukan re-launch otomatis dengan UAC elevation via `runas` verb
- Aplikasi berjalan tanpa console window (OutputType: WinExe)

---

## Technical Requirements

| Requirement | Detail |
|---|---|
| Platform | Windows 10 / 11 |
| Runtime | .NET 10, WinForms |
| Privileges | Administrator (UAC elevation otomatis) |
| Persistence | `launchers.json` di direktori binary |
| Network API | `netsh interface ipv4` via subprocess |
| Registry API | `Microsoft.Win32.Registry` untuk installed app discovery |

---

## UI Requirements

- Light theme dengan palet warna Teal/Blue (#DBF7D2 → #8FDBC5 → #64A0C4 → #4887B7 → #367096)
- Tombol berbentuk rounded rectangle (border-radius 6px) dengan hover dan pressed state
- Tidak ada console window yang muncul
- Dialog "Add App" menampilkan installed apps picker secara inline
- Dialog "Edit App" menampilkan form sederhana tanpa picker (data sudah ada)
- Tombol Edit dan Delete menampilkan jumlah item yang dipilih: `Edit (3)`, `Delete (3)`
- Column header tabel tidak berubah warna saat diklik
- Baris pertama tidak otomatis ter-select saat aplikasi pertama dibuka
- Tinggi panel Configured Apps dan Active Bindings dapat diatur via splitter (SplitContainer)

---

## Out of Scope

- Multiple adapter binding per aplikasi
- Scheduling (jalankan otomatis pada jam tertentu)
- Import/export konfigurasi
- Notifikasi sistem (system tray, toast)
- Dukungan IPv6 routing metric
