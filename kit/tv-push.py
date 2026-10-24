#!/usr/bin/env python3
"""tv-push.py — push a local file to the TV through the root bridge (port 9997).
Sends base64 chunks, decodes on the TV side.
Usage: python tv-push.py <local_path> <remote_path>
"""
import base64
import socket
import sys
import time

def send_cmd(sock, cmd, wait=0.3):
    sock.sendall(cmd.encode() + b"\n")
    time.sleep(wait)

def main():
    local, remote = sys.argv[1], sys.argv[2]
    data = open(local, "rb").read()
    b64 = base64.b64encode(data).decode()
    c = socket.create_connection(("127.0.0.1", 9997))
    time.sleep(0.5)
    send_cmd(c, f"rm -f /tmp/_push.b64")
    for i in range(0, len(b64), 3000):
        send_cmd(c, f"echo -n {b64[i:i+3000]} >> /tmp/_push.b64", 0.15)
    send_cmd(c, f"base64 -d /tmp/_push.b64 > {remote} && echo PUSH-OK {remote} $(wc -c < {remote})", 1.5)
    c.close()

if __name__ == "__main__":
    main()
