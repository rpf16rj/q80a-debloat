# Relatório — sessão de customização Q80A (2026-10-23/24)

## Estado final do sistema

### Apps instalados (via `pkgcmd -i -t wgt`, root)
| App | Pacote | Versão | Origem |
|---|---|---|---|
| TizenTube | `xvvl3S1TT1` | 2.1.1 | `apps/TizenTubeCobalt.wgt` |
| Jellyfin | `AprZAARz4r` | 0.1.0 (web 10.11.z) | jeppevinkel/jellyfin-tizen-builds |
| Moonlight | `MoonLightS` | 1.16.0 | já instalado — upgrade p/ 1.19.0 bloqueado por troca de assinatura (OneLiberty → brightcraft) |

Transferência: `python -m http.server 8888` no PC + `curl` na TV para `/tmp`, instalação com `pkgcmd`.

### Apps removidos anteriormente (12, persistem)
Amazon Alexa, Samsung Promotion, Xbox, Spotify, e-Manual, Security Center, Globoplay (2), SmartOne IPTV, Apple TV (2), Disney+. Telemetria/ads mantidos por segurança (AdGuard cobre rede).

### HyperTizen — inviável na Tizen 6.0
- Requisito oficial: Tizen 6.5+ (2022+)
- Verificado na TV: `/usr/lib/libvideoenhance.so.0.1` existe mas **não contém** `cs_ve_get_rgb_measure_*` nem `ve_get_rgb_measure_*` (grep -a no binário: `measure` = 0 hits)
- HyperTizen usa exatamente essas funções para amostrar RGB da tela → sem API de captura no 6.0

### WLED ambilight (substituto do HyperTizen) — implementado
- **Patch no TizenBrew NG**: suporte a módulos `local/<nome>` servidos de `/opt/usr/apps/xvvl3S1bvH/res/wgt/localmodules/` — sem publicar no jsDelivr
  - `service/index.js`: rota `/local/` estática
  - `utils/moduleLoader.js`: `package.json` local para módulos `local/*`
  - `utils/serviceLauncher.js`: `serviceFile` local
  - `utils/debugger.js`: `mainFile` local
  - Backups: `<arquivo>.orig` ao lado de cada patch
- **Módulo `wled-ambilight`** (`modules/wled-ambilight/`):
  - `package.json` — packageType `mods`, alvo `youtube.com/tv`
  - `service.js` — HTTP `127.0.0.1:8090/ddp` → DDP UDP → `192.168.3.176:4048` (Node `dgram` na TV, sem relay no PC)
  - `main.js` — amostra bordas do `<video>` (canvas 64×36 @15fps) → 226 zonas → POST binário; carrega userscript do TizenTube junto (`LOAD_TIZENTUBE`)
  - Layout: 226 LEDs, canto superior esquerdo, horário — 72 topo / 41 dir / 72 base / 41 esq
- Registrado em `/home/owner/share/tizenbrewConfig.json` → `"local/wled-ambilight"`
- **Status**: módulo aparece mas LEDs não reagiram no primeiro teste; a rota `/local/` respondia código antigo até reiniciar `StandaloneService`. **Próximo debug**: checar se `:8090` sobe e se `main.js` é injetado.
- `kit/wled-relay.py` — alternativa PC-side (HTTP→DDP), testada e funcional (flash verde confirmado)

### HDMI / CEC — recon completo
- **Detecção por porta**: `/sys/devices/virtual/extcon/hdmi_det_info/` — `cable.0..15` nomeados HDMI1-6, H1_c..H6_c, ARC, EARC, MULTI_PBP, HDMIFW_UPDATE; agregado em `state`
- **CEC**: `org.tizen.tv.hdmicec` (busctl) — `GetDeviceCount(b)`, `GetDeviceName(i)`, `GetDevicePortNum(i)`, `GetActiveDeviceName`, `GetDeviceType`, `SendKeyToHdmicec`… — API doc: `/usr/bin/hdmicec-api.xml`. Ao vivo: 1 dispositivo "HTR-3066" (receiver Yamaha) na HDMI 3
- **Sinal de vídeo**: `/dev/video21` = `sdp-hdmi` (V4L2; timing via ioctl — precisa binário ARM). Switch: `video28`. Decoders MFC: video10-14
- **Fonte**: `com.samsung.ISourceControl` no `tvs-daemon` (introspecção XML malformada; métodos a mapear)
- EDID: `card0-LVDS-1/edid` (painel interno)

### TizenBrew / sdb / dev-mode
- `sdb` (26101): CNXN + `sysinfo:` OK (Tizen 6.0, QN55Q80AAGXZD) mas `shell:`/`sync:` recusados — dev-mode host travado em `127.0.0.1`
- UEP: scripts executados via `bash arquivo.sh` são bloqueados (`[uep][bash] the file is NOT signed!!`); `bash < arquivo` (stdin) passa. JS de app em `/opt/usr/apps/` **não** é assinado — patchável (foi assim que o módulo local entrou)
- Arquivos `.orig` preservam originais do TizenBrew

### Root CIFS
- Cadeia confirmada: Remote PC → Shared Folder → `remotepc_uilauncher` → `CallCommand("ps_mount_cifs", …)` via `com.safe.ps_agent` (D-Bus `/com/safe/ps_agent`) → `/usr/apps/privileged-service/bin/mount.smb.sh` → `mount -o …,password=$(…)` avaliado por `/bin/bash -c`
- Arquivo de credenciais `/tmp/fileXXXX`: `username=/password=/smbpath=`
- `busctl call com.safe.ps_agent … CallCommand ss ps_mount_cifs …` → timeout e `mount.smb.sh` não spawnou — **não reproduzido por software** (rota UI confirmada; chamada direta ainda sem prova)
- `mount.smb.sh`/`bash -i` ficam como pais do shell (pid visível no `ps`) — matar o mount mata o shell

### Menu de serviço
- `app_launcher -s org.tizen.factory` abre direto — sem sequência de controle. Inspeção apenas (nada alterado)

### Painel HDMI — módulo pronto (não testado na TV ainda)
- `modules/hdmi-panel/` — app TizenBrew local: `service.js` serve `/api` em `127.0.0.1:8091` lendo `extcon/state` + chamando `busctl` para CEC; `index.html` renderiza painel auto-refresh (2s)
- `patch-tizenbrew.sh` ganhou step `[3b]` — appPath local para módulos `local/*` do tipo `app` (necessário pro painel)
- `tools/hdmi-timings` — binário ARMv7 estático (zig 0.17, musl) que faz `VIDIOC_QUERY_DV_TIMINGS` em `/dev/video21` → resolução/fps/pixclk reais do sinal HDMI. Fonte: `tools/hdmi-timings.c`
- Pendente: copiar `modules/hdmi-panel` pra `localmodules/` na TV + registrar `local/hdmi-panel` no config + testar se o service (user `owner`) consegue ler extcon e chamar `busctl` (SMACK pode bloquear — degradar graceful)

### Limpeza executada
- Listeners do PC parados (bridge, listener, http.server 8888, wled-relay 8090)
- **Pendente (admin no Windows)**: `kit/cleanup-rdp.ps1` — remove share `x$(nc …)`, desativa `tvrdp`, `fDenyTSConnections=1`, firewall RDP off
- **Pendente (usuário)**: restaurar senha do `rpf16` (atual = payload `$(/bin/bash</home/owner/share/r)`)
- Persistem na TV (benigno/necessário): `/home/owner/share/r`, `localmodules/`, patch TizenBrew, config `local/wled-ambilight`
