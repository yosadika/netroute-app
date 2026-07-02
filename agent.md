# Agent Guide — NetRoute Manager

Dokumen ini menjelaskan arsitektur, komponen, alur data, dan konvensi koding untuk AI agent yang bekerja di codebase ini.

---

## Stack

- **Language:** C# 13, .NET 10
- **UI:** Windows Forms (WinForms)
- **Target:** `net10.0-windows`, `OutputType: WinExe` (tanpa console window)
- **Entry point:** `Program.cs → Main()`
- **Serialization:** `System.Text.Json`
- **Registry access:** `Microsoft.Win32`
- **Drawing:** `System.Drawing.Drawing2D` (untuk rounded buttons)

---

## File Structure

```
app-netroute/
├── Program.cs              # Entry point, UAC elevation check
├── MainForm.cs             # Main window
├── LauncherDialog.cs       # Add/Edit app dialog
├── LauncherManager.cs      # Business logic: launch, monitor, restore
├── NetworkUtils.cs         # netsh wrappers, adapter listing
├── AppLauncher.cs          # Data model (persisted)
├── ActiveBinding.cs        # Runtime state (not persisted)
├── InstalledApp.cs         # Record: installed app name + exe path
├── InstalledAppsProvider.cs # Registry scanner for installed apps
├── launchers.json          # Runtime-generated; gitignore candidate
└── NetRouteManager.csproj
```

---

## Component Responsibilities

### `Program.cs`

- `IsAdmin()`: cek `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
- `Elevate()`: re-launch diri sendiri dengan `ProcessStartInfo { Verb = "runas" }`
- Jika sudah admin: `Application.Run(new MainForm())`

### `NetworkUtils.cs`

Semua interaksi dengan `netsh` ada di sini.

```csharp
// Parse output dari: netsh interface show interface
static List<NetworkAdapter> GetAdapters()

// Parse output dari: netsh interface ipv4 show interfaces
// Kolom: Idx  Met  MTU  State  Name
static Dictionary<string, int> GetInterfaceMetrics()

// netsh interface ipv4 set interface "{name}" metric={n} store=active
static (bool Ok, string Error) SetInterfaceMetric(string name, int metric)
```

**Penting:**
- Static constructor memanggil `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` agar encoding CP850 tersedia di .NET Core (diperlukan untuk output CMD)
- Semua subprocess menggunakan `CreateNoWindow = true` dan `Encoding.GetEncoding(850)`
- `RunShell()` menggunakan `cmd.exe /c` sebagai shell wrapper

### `LauncherManager.cs`

Business logic utama. Tidak boleh ada UI code di sini.

**State:**
```csharp
List<AppLauncher> _launchers          // konfigurasi persisten
ConcurrentDictionary<int, ActiveBinding> _active  // binding yang sedang berjalan
```

**`LaunchAsync(launcher, onStatus)`:**
1. Snapshot semua metric adapter saat ini → `originalMetrics`
2. Set adapter target ke metric=1
3. Set semua adapter lain ke metric=9999
4. `Process.Start(launcher.ExePath)` dengan `UseShellExecute = true`
5. Daftarkan ke `_active` dengan PID
6. `await proc.WaitForExitAsync()` di background
7. Saat proses keluar → `RestoreMetrics(originalMetrics)`

**`Release(pid, onStatus)`:**
- Hapus dari `_active`
- Panggil `RestoreMetrics` tanpa menunggu proses keluar

**Persistence:**
- `launchers.json` di `AppDomain.CurrentDomain.BaseDirectory`
- Serialized dengan `System.Text.Json` (default options)

### `MainForm.cs`

**Layout (nested SplitContainer):**
```
Form
├── Panel header (DockStyle.Top, 48px)
│     ├── Panel accent bar (DockStyle.Left, 4px, warna GREEN)
│     ├── Panel separator (DockStyle.Bottom, 1px, warna BORDER)
│     ├── Label "NetRoute Manager"
│     └── Label badge "Run as Administrator"
└── SplitContainer _splitMain (DockStyle.Fill, Horizontal)
      ├── Panel1: TableLayoutPanel
      │     ├── Row 0 (Fill): Panel BuildAppGrid()
      │     └── Row 1 (54px): Panel BuildActionBar()
      └── Panel2: SplitContainer _splitBot (Horizontal)
            ├── Panel1: Panel BuildBindings()
            └── Panel2: Panel BuildLog()
```

**PENTING — urutan Controls.Add:**
WinForms memproses docking dari index Controls tertinggi ke terendah. Untuk `DockStyle.Fill` (splitMain) dan `DockStyle.Top` (header) bekerja benar:
```csharp
Controls.Add(_splitMain);  // index 0 → Fill → diproses KEDUA
Controls.Add(header);      // index 1 → Top  → diproses PERTAMA, klaim 48px atas
```
Jika urutan terbalik, header akan overlap di atas splitMain dan menyembunyikan 48px konten.

**Key behaviors:**
- Timer 1000ms memanggil `RefreshBindings()` untuk update Active Bindings
- Semua callback dari `LauncherManager` menggunakan `BeginInvoke()` karena dipanggil dari background thread
- `RefreshAppList()` selalu diakhiri dengan `ClearSelection()` + `CurrentCell = null` untuk mencegah auto-select baris pertama
- `Shown` event juga melakukan `ClearSelection()` untuk mengatasi auto-focus Windows

**Checkbox multi-select:**
- Kolom 0 di `_gridApps` adalah `DataGridViewCheckBoxColumn`
- Grid bersifat `ReadOnly = true`; toggle checkbox dilakukan programmatically via `CellClick`
- `GetCheckedLaunchers()` membaca nilai checkbox dari setiap baris
- Tombol Edit dan Delete hanya aktif jika ada baris yang di-check
- Label tombol berubah dinamis: `"Edit (3)"`, `"Delete (3)"`

**Column indices `_gridApps`:**
```
Col 0: Checkbox (DataGridViewCheckBoxColumn)
Col 1: App Name
Col 2: Adapter
Col 3: Executable
Col 4: Arguments
```

**Column indices `_gridBinding`:**
```
Col 0: App Name
Col 1: PID (int sebagai string)
Col 2: Adapter
Col 3: Running Since
```

**Theme constants (Teal/Blue palette):**
```csharp
BG      = (245, 252, 249)  // near-white mint — main background
BG2     = (255, 255, 255)  // white — panels, grids
BG3     = (225, 245, 238)  // light teal — headers, inputs, outline button fill
FG      = (18,  40,  55)   // dark navy — primary text
FG2     = (80,  120, 135)  // medium teal-slate — secondary/label text
ACCENT  = (72,  135, 183)  // #4887B7 — Add button, badge text
GREEN   = (54,  112, 150)  // #367096 — Launch button, success log, active bindings text
RED     = (210, 55,  55)   // danger — Delete, Release buttons
BORDER  = (143, 219, 197)  // #8FDBC5 — grid lines, splitters, outline button border
SEL_BG  = (219, 247, 210)  // #DBF7D2 — selected row background
SEL_FG  = (40,  100, 130)  // dark teal — selected row text
```

**Rounded buttons (`MakeButton`):**
- `FlatStyle = FlatStyle.Flat`, `BorderSize = 0`
- Custom `Paint` event menggambar `GraphicsPath` dengan sudut bulat radius 6px via `AddArc`
- State: normal → `back`; hover → `Shift(back, -15)`; pressed → `Shift(back, -25)`
- Outline buttons: fill = BG3, border = BORDER (1.5px)
- Sudut button dibersihkan dengan warna parent background agar tidak ada artefak kotak
- Helper: `RoundedPath(Rectangle, int radius)` dan `Shift(Color, int delta)`

### `LauncherDialog.cs`

Dialog modal untuk Add dan Edit konfigurasi. Perilaku berbeda tergantung mode:

**Add mode** (`existing == null`):
- Dialog besar (720×580), resizable
- Menampilkan installed apps picker (search + DataGridView dengan icon)
- Apps dimuat via `Task.Run(InstalledAppsProvider.GetAll)` di `Shown` event
- Icon dimuat via `Icon.ExtractAssociatedIcon()` di background thread terpisah
- `_iconSeq` counter digunakan untuk cancel load lama saat filter berubah
- Memilih baris langsung mengisi field App Name dan Executable

**Edit mode** (`existing != null`):
- Dialog kecil (560×280), FixedDialog
- Form sederhana: Name, Executable + Browse button, Arguments, Adapter
- Tidak ada installed apps picker

**Bulk edit** (multiple checked):
- Inline Form sederhana (420×170) hanya dengan dropdown Adapter
- Menampilkan jumlah app yang diedit

**Color constants dan `MakeButton`/`RoundedPath`/`Shift`** identik dengan MainForm.cs — jika mengubah salah satu, update keduanya.

### `InstalledAppsProvider.cs`

Scan Windows Registry untuk menemukan aplikasi terinstall.

**Sumber (diurutkan dari prioritas):**
```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
HKCU\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
```

**Filter per entry:**
1. Skip jika tidak ada `DisplayName`
2. Skip jika ada `ParentKeyName` (sub-komponen installer)
3. Skip jika tidak ada `UninstallString` (entry orphaned)

**Resolusi path executable:**
1. Coba `DisplayIcon` → split pada `,` → strip quotes → validasi `.exe` + `File.Exists()`
2. Fallback: `InstallLocation` → scan `*.exe` di root folder → pilih yang namanya paling mirip `DisplayName`

**`TryResolve()`** menggunakan `Path.GetFullPath()` untuk menangani Windows 8.3 short paths (contoh: `C:\PROGRA~1\`).

**Diagnostik:** `LastDiagnostic` property menyimpan log teks dari run terakhir. Juga ditulis ke `%TEMP%\netroute_apps.txt`.

---

## Data Flow: Launching an App

```
User double-clicks row / clicks Launch
    │
    ▼
MainForm.LaunchSelected()
    │  reads SelectedLauncher() from _gridApps.SelectedRows[0].Index
    ▼
LauncherManager.LaunchAsync(launcher, onStatus)
    │
    ├─ NetworkUtils.GetInterfaceMetrics()   → snapshot
    ├─ NetworkUtils.SetInterfaceMetric(target, 1)
    ├─ NetworkUtils.SetInterfaceMetric(others, 9999)
    ├─ Process.Start(launcher.ExePath)
    ├─ _active[pid] = new ActiveBinding(...)
    │
    └─ Background Task: proc.WaitForExitAsync()
           │
           └─ NetworkUtils.SetInterfaceMetric(name, original) × N
              _active.TryRemove(pid)
              onStatus("Metrics restored")
                  │
                  └─ BeginInvoke → MainForm.AppendLog(msg, GREEN)
```

---

## Common Pitfalls

| Masalah | Penyebab | Solusi |
|---|---|---|
| Build gagal "file in use" | NetRouteManager.exe berjalan sebagai Admin, tidak bisa di-overwrite dari non-elevated context | Tutup app dulu, baru `dotnet build` |
| Encoding error di netsh output | .NET Core tidak include CP850 by default | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` di static constructor NetworkUtils |
| DataGridView header hilang saat diklik | Default selection style override text color | Set `SelectionBackColor` dan `SelectionForeColor` sama dengan non-selected di `ColumnHeadersDefaultCellStyle` |
| `DataGridViewImageColumn` error format | Nilai `null` tidak valid untuk image cell | Gunakan `new Bitmap(16, 16, PixelFormat.Format32bppArgb)` sebagai placeholder |
| Baris pertama auto-selected | DataGridView auto-focus saat form shown | `ClearSelection()` + `CurrentCell = null` di `RefreshAppList()` dan di event `Shown` |
| Checkbox tidak toggle (ReadOnly grid) | `ReadOnly = true` memblok user input ke semua cell | Toggle nilai checkbox secara programmatic di handler `CellClick` |
| Header terpotong setelah SplitContainer | WinForms docking order: index tertinggi diproses lebih dulu | Tambah `_splitMain` (Fill) sebelum `header` (Top) ke `Controls` — lihat catatan di atas |
| Tombol action bar terpotong | Row height kurang untuk button height + padding + separator | Row height action bar = 54px (button 34px + padding atas 9px + padding bawah 5px) |
| Rounded button corners kotak | WinForms Button tidak support rounded natively | Custom Paint event + `GraphicsPath.AddArc` + clear corners dengan warna parent |
| Rounded button state tidak update | BackColor change tidak trigger repaint saat FlatStyle.Flat | Track state lewat `MouseEnter/Leave/Down/Up` dan panggil `btn.Invalidate()` secara manual |
| Metric tidak terestore jika app crash | `WaitForExitAsync()` menunggu exit normal | Gunakan `proc.EnableRaisingEvents = true` dan handle `Exited` event sebagai backup |

---

## Extension Points

- **Per-adapter routing rules:** Saat ini semua adapter non-target di-set ke 9999. Untuk routing lebih granular (hanya satu route spesifik), perlu integrasi dengan `netsh interface ipv4 add route` atau Windows Filtering Platform (WFP) API.
- **Start Menu scan:** `InstalledAppsProvider` bisa diperluas dengan scan `.lnk` files di `%APPDATA%\Microsoft\Windows\Start Menu` sebagai sumber tambahan untuk app yang tidak terdaftar di Uninstall registry.
- **Process tree monitoring:** Saat ini hanya PID utama yang dipantau. Jika app spawn child process yang membuat koneksi, metric akan terestore sebelum child selesai.
- **Persistence location:** `launchers.json` berada di direktori binary. Untuk instalasi multi-user, pertimbangkan `%APPDATA%\NetRouteManager\`.
- **Theme extension:** Konstanta warna identik di `MainForm.cs` dan `LauncherDialog.cs`. Jika ingin shared theme, pindahkan ke static class terpisah (misalnya `AppTheme.cs`).
