# WLED Ambilight on a Samsung Q80A (QN55Q80AAGXZD) — full screen capture ~45fps, no root app needed

**TL;DR:** I got real-time ambilight working on a Samsung Q80A (Tizen 6.0) by talking directly to the TV's internal screen-capture API (`libvideo-capture` / `ppicapture`) and pushing DDP packets to a WLED strip. ~45 fps, works on every input — HDMI, YouTube, native apps. No Android box, no camera, no HDMI splitter.

## Hardware

- Samsung QN55Q80AAGXZD ("NikeM2", Tizen 6.0, firmware T-NKM2UABC 2301)
- WS2812B / SK6812 strip + ESP32 running WLED (192.168.3.176:4048, DDP)

## The capture path nobody talks about

Tizen has an internal `IVideoCapture` interface (`libvideo-capture.so` → `libvideo-capture-impl-sec.so` → `libtdm-client` → `tdm-socket` served by **enlightenment**). `ppicapture` returns a real NV16 framebuffer — 720x540 @ ~13ms/frame.

## The gate — SMACK label, not uid

The compositor's `e_security_privilege_check` reads the peer's SMACK label via `smack_new_label_from_process` and compares it. What I measured empirically:

- `User` label → **captures real frames**
- `System::Privileged` → captures
- `User::Pkg::*` (per-app labels) → **rejected** (daemon runs but gets zeroed/black frames)
- uid/groups/manifest privileges → irrelevant

So the daemon must run as uid 5001 with label `User` or better. `/usr/bin/dotnet` already has SMACK64EXEC=`User` — any dotnet process runs as `User`.

## How I run it

1. **CIFS-root** (the mount.smb.sh eval trick — the `r` stage2) gives a root shell via Remote PC → Shared Folder.
2. stage2 drops the daemon as `User`: `DropPriv → /bin/sh → /usr/bin/dotnet WledAmbiPpi.exe` — all `User`, ~45fps.

`WledAmbiPpi.exe` is a small .NET binary that opens the capture, downsamples the Y/CbCr planes into per-edge colors, and pushes DDP at the strip.

## What doesn't work (and why)

- **TizenBrew module**: the StandaloneService runs as `User`, but the app sandbox blocks exec of system binaries (`spawnSync /bin/sh EACCES`), and the module vm only has webpack `require` — no `child_process`/`fs`.
- **Custom WGT**: same label/sandbox gate.
- **sdbd injection (SVE-2025-50109-style)**: this sdbd doesn't eval package names (`;`, backticks refused at the protocol layer), `shell:`/`exec:` refused. `sync:` push does give file-write as uid5001, though.
- **SMACK64EXEC on a copied binary**: UEP blocks unsigned copies; rootfs is read-only anyway.

## Open questions

- `vd_appinstall` postinst might still give exec — needs a properly signed TPK to test.
- A kernel module could NOP `e_security_privilege_check` (insmod unsigned works) — I decided against it for now since the 1-click ritual is stable.

Repo: github.com/rpf16rj/q80a-debloat — `tools/` has WledAmbiPpi, DropPriv, the sdb wire client and the stage2 payloads.
