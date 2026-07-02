# NetRoute Manager

A Windows desktop app to bind specific applications to a chosen network adapter — automatically, without running `route add` commands every time.

---

## The Problem

Windows has no built-in way to control which network adapter a specific application uses. If you have multiple active connections — Wi-Fi, LAN, VPN — all apps share the same default route.

NetRoute Manager lets you say: *"this app should always use this adapter"*, and automatically restores your network configuration when the app closes.

**Common use cases:**
- Work apps (Teams, Outlook) → VPN
- Browser → direct ISP connection
- Games → low-latency LAN interface

---

## Features

- **Per-app configuration** — store a list of apps with their preferred adapter
- **One-click launch** — adapter is locked and the app starts in a single click
- **Active bindings monitor** — see all running apps with their current adapter binding
- **Auto-restore** — network metrics are restored automatically when the app exits
- **Manual release** — unlock the adapter at any time without closing the app
- **Installed apps picker** — browse installed apps with icons from the Add dialog
- **Multi-select** — edit or delete multiple configurations at once via checkboxes

---

## Screenshot

> *(add screenshot here)*

---

## Download & Install

> **No .NET installation required** — the runtime is bundled inside the exe.

1. Go to the [**Releases**](../../releases/latest) page
2. Download `NetRouteManager-vX.X.X-win-x64.zip`
3. Extract `NetRouteManager.exe` anywhere
4. Right-click → **Run as administrator**

### System Requirements

| | |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Privileges | Administrator (UAC prompt appears automatically) |
| .NET Runtime | Not required |

---

## How It Works

NetRoute Manager manipulates **interface metrics** on Windows:

1. When you launch an app, the target adapter is set to metric **1** (highest priority)
2. All other adapters are set to metric **9999** (lowest priority)
3. Windows routes traffic through the adapter with the lowest metric
4. When the app exits, all metrics are restored to their original values

This uses `netsh interface ipv4 set interface` — no drivers or kernel modifications required.

> **Note:** Metric changes are temporary (`store=active`) and only apply to the current Windows session. No permanent changes are made to your system configuration.

---

## Build from Source

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

## Project Structure

```
├── Program.cs               # Entry point, UAC elevation
├── MainForm.cs              # Main window UI
├── LauncherDialog.cs        # Add / Edit configuration dialog
├── LauncherManager.cs       # Core logic: launch, monitor, restore
├── NetworkUtils.cs          # netsh wrappers
├── InstalledAppsProvider.cs # Registry scanner for installed apps
├── AppLauncher.cs           # Configuration data model (persisted)
├── ActiveBinding.cs         # Runtime binding state
└── InstalledApp.cs          # Installed app data model
```

---

## Limitations

- Routing works at the **interface metric** level, not per-process or per-packet — effective as long as other apps are not generating significant traffic simultaneously
- Only the **main process PID** is monitored — child processes spawned by the app are not tracked
- **IPv6** is not supported

---

## License

MIT
