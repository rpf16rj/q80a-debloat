#!/usr/bin/env python3
"""send-cmd.py — envia comando pro root shell via bridge (porta 9997).

Uso:
  python send-cmd.py "id; uname -a"
  python send-cmd.py "pkgcmd -l | head -20" --wait 15

A saida do shell aparece no terminal do bridge.py (nao neste).
Este script so injeta o comando; --wait define quanto tempo esperar antes de sair.
"""
import socket, sys, time

cmd = sys.argv[1] if len(sys.argv) > 1 else "id"
wait = 8
for i, a in enumerate(sys.argv):
    if a == "--wait" and i + 1 < len(sys.argv):
        wait = int(sys.argv[i + 1])

c = socket.create_connection(("127.0.0.1", 9997))
c.sendall((cmd + "\n").encode())
time.sleep(wait)
c.close()
