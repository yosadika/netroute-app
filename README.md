# NetRoute Manager

Aplikasi desktop Windows untuk mengikat aplikasi tertentu ke network adapter pilihan secara otomatis — tanpa perlu menjalankan perintah `route add` secara manual setiap kali.

---

## Masalah yang Diselesaikan

Windows tidak menyediakan cara bawaan untuk mengontrol network adapter mana yang digunakan oleh aplikasi tertentu. Jika Anda memiliki beberapa koneksi aktif sekaligus — misalnya Wi-Fi, LAN, dan VPN — semua aplikasi akan menggunakan adapter dengan routing metric terbaik secara default.

NetRoute Manager memungkinkan Anda menentukan: *"aplikasi A harus selalu menggunakan adapter B"*, dan merestorasi konfigurasi jaringan secara otomatis setelah aplikasi tersebut ditutup.

**Contoh penggunaan:**
- Aplikasi kantor (Teams, Outlook) → VPN
- Browser → ISP langsung
- Game → interface LAN dengan latensi terendah

---

## Fitur

- **Konfigurasi per-aplikasi** — simpan daftar aplikasi beserta adapter yang diinginkan
- **Launch otomatis** — sekali klik, adapter langsung dikunci dan aplikasi diluncurkan
- **Monitor aktif** — lihat semua aplikasi yang sedang berjalan dengan binding adapter-nya
- **Restore otomatis** — metric jaringan dikembalikan ke semula saat aplikasi ditutup
- **Release manual** — lepas binding kapan saja tanpa menutup aplikasi
- **Installed apps picker** — pilih dari daftar aplikasi terinstall lengkap dengan icon
- **Multi-select** — edit atau hapus beberapa konfigurasi sekaligus via checkbox

---

## Screenshot

> *(tambahkan screenshot di sini)*

---

## Download & Install

> **Tidak perlu install .NET** — runtime sudah termasuk dalam file exe.

1. Buka halaman [**Releases**](../../releases/latest)
2. Download file `NetRouteManager-vX.X.X-win-x64.zip`
3. Extract `NetRouteManager.exe` ke folder mana saja
4. Klik kanan → **Run as administrator**

### Kebutuhan Sistem

| | |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Hak akses | Administrator (UAC prompt otomatis muncul) |
| .NET Runtime | Tidak diperlukan |

---

## Cara Kerja

NetRoute Manager bekerja dengan memanipulasi **interface metric** pada Windows:

1. Saat aplikasi diluncurkan, adapter target di-set ke metric **1** (prioritas tertinggi)
2. Semua adapter lain di-set ke metric **9999** (prioritas terendah)
3. Windows akan otomatis mengarahkan traffic aplikasi ke adapter dengan metric terendah
4. Saat aplikasi ditutup, semua metric dikembalikan ke nilai semula

Teknik ini menggunakan `netsh interface ipv4 set interface` — tidak ada driver atau modifikasi kernel yang diperlukan.

> **Catatan:** Perubahan metric bersifat sementara (`store=active`) dan hanya berlaku selama sesi Windows berjalan. Tidak ada perubahan permanen pada konfigurasi sistem.

---

## Build dari Source

**Requirements:** .NET 10 SDK, Windows

```bash
git clone https://github.com/yosadika/netroute-app.git
cd netroute-app
dotnet run
```

**Build self-contained exe:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```

---

## Struktur Project

```
├── Program.cs              # Entry point, UAC elevation
├── MainForm.cs             # UI utama
├── LauncherDialog.cs       # Dialog tambah / edit konfigurasi
├── LauncherManager.cs      # Business logic: launch, monitor, restore
├── NetworkUtils.cs         # Wrapper netsh
├── InstalledAppsProvider.cs # Registry scanner untuk daftar app terinstall
├── AppLauncher.cs          # Model data konfigurasi (persisten)
├── ActiveBinding.cs        # State runtime binding aktif
└── InstalledApp.cs         # Model data app terinstall
```

---

## Keterbatasan

- Routing bekerja di level **metric interface**, bukan per-proses atau per-paket — efektif selama tidak ada traffic besar dari aplikasi lain di waktu bersamaan
- Hanya memantau **PID utama** — child process yang dibuat aplikasi tidak dipantau
- Tidak mendukung **IPv6**

---

## Lisensi

MIT
