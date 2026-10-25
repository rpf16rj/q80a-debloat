# WLED Ambilight on Samsung Q80A — tutorial

Real-time screen-capture ambilight on a QN55Q80A (Tizen 6.0). ~45fps, works on HDMI + all apps. No camera, no HDMI splitter, no Android box.

## Requirements

- Samsung Q80A (NikeM2, Tizen 6.0, firmware T-NKM2UABC — may apply to other 2021 Samsungs)
- ESP32 running WLED on the same LAN, strips wired
- Windows PC on the same LAN (only needed for the 1-click arming)

## How it works

The TV's `libvideo-capture` API exposes the live framebuffer (NV16 720x540) through the TDM socket. A small .NET daemon reads it, averages edge colors, and pushes DDP packets to the WLED at ~45fps.

The capture gate checks the caller's SMACK label — the daemon must run as `User` (or `System::Privileged`). `/usr/bin/dotnet` already has SMACK64EXEC=`User`, so `dotnet WledAmbiPpi.exe` runs in the right context — the trick is just spawning it there.

## Setup

1. **WLED**: flash WLED on the ESP32, note its IP (e.g. 192.168.3.176), enable DDP (default port 4048).

2. **Repo files** (`github.com/rpf16rj/q80a-debloat`):
   - `tools/sdpmemcs/WledAmbiPpi.exe` — the capture→DDP daemon
   - `tools/sdpmemcs/DropPriv.exe` — runs a command as uid 5001
   - `kit/` — stage2 + support stack

3. **PC side** (one-time): run `kit/install-startup.bat` as admin — registers the listener stack (`tv-stack.py`) at logon and re-arms the `tvrdp` password payload every boot.

4. **Push the binaries** to the TV (once): `WledAmbiPpi.exe`, `DropPriv.exe`, `PpiCap.exe` into `/home/owner/share/` (via curl from the PC's :8888 server).

5. **Each session**: TV → Remote PC → profile `tvrdp` → **Shared Folder**. The mount evaluates the payload (`r` = stage2), which opens the root shell and launches the daemon as `User`. Strip lights up.

## Ritual per TV boot

The mount eval is volatile — after each TV reboot: **Remote PC → tvrdp → Shared Folder** once. ~5 seconds. The daemon survives the PC disconnecting.

## Files

- `tools/sdpmemcs/WledAmbiPpi.*` — daemon (capture + DDP)
- `tools/sdpmemcs/DropPriv.*` — uid 5001 launcher
- `kit/stage2.sh` — the `r` payload that runs on mount
- `kit/tv-stack.py` — PC listener stack
