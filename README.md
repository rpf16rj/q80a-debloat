# Q80A Debloat — Samsung QN55Q80AAGXZD (NikeM2, Tizen 6.0)

Debloat/customization work on a Samsung Q80A 55" 2021 (firmware `T-NKM2UABC` 2301).

## Current state

- **Root obtained** via command injection in the Remote PC/CIFS flow (`System::Privileged`, volatile — gone after reboot)
- **12 apps removed** via `pkgcmd -u` (see `docs/DEBLOAT.md`)
- **TizenBrew** installed + visual catalog served from the PC
- **FreeMyTV** (alternative launcher) installed — pkgid `FreeMyTVio`
- **AdGuard Home** with Samsung ads/telemetry blocklists
- Firmware SWU keys captured (`swu_keys/`) — AES wrapped by TrustZone

## Structure

```
apps/          — TizenBrew catalog, WGTs, AdGuard blocklists
docs/          — documentation (root, debloat, HDMI/CEC, research)
inventory/     — full package inventory (355 pkgs)
kit/           — re-root kit (stage2, bridge, listener)
modules/       — TizenBrew local modules (WLED ambilight, HDMI panel)
swu_keys/      — SWU keys extracted from the TV
tools/         — analysis tools (firmware .msd, V4L2 timings helper)
.tools/        — working tools (not versioned)
```

## Warnings

- NikeM2 firmware is encrypted + RSA-signed — **there is no modified-firmware flash**. All work is runtime-only.
- Root is **volatile**: reboot kills the shell. App removals are permanent (done by the package manager).
- `/` is hardware write-protected. Writable: `/opt` and `/mnt/systemrw`.
- Use on your own device only.
