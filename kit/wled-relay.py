#!/usr/bin/env python3
"""wled-relay.py — HTTP -> DDP relay for WLED ambilight.

The TV (browser JS inside an app/mod) cannot send UDP. It POSTs a compact
binary or JSON frame here; we repack as a DDP packet and fire it at the WLED.

Endpoints:
  POST /ddp   body = raw RGB bytes (led_count*3)          -> DDP packet to WLED
  POST /json  {"colors":[[r,g,b],...]}                    -> DDP packet to WLED
  GET  /ping                                            -> "ok"

DDP: 10-byte header + RGB payload, UDP to <wled>:4048.
226 LEDs = 678 bytes payload -> single packet (fits MTU easily).

Usage:  python wled-relay.py [--wled 192.168.3.176] [--port 8090]
"""

import argparse
import json
import socket
import struct
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DDP_PORT = 4048
TIMEOUT_SECS = 2  # DDP timeout hint unused; WLED defaults are fine

_wled = "192.168.3.176"
_led_count = 226
_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)


def send_ddp(rgb: bytes, offset: int = 0):
    """One DDP packet: flags(0x41=VER1|PUSH) seq type(0x01=RGB24) dest offset len."""
    header = bytes([0x41, 0x01, 0x01, 0x01]) + struct.pack(">I", offset) + struct.pack(">H", len(rgb))
    _sock.sendto(header + rgb, (_wled, DDP_PORT))


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "content-type")

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_GET(self):
        if self.path == "/ping":
            body = b"ok"
            self.send_response(200)
        elif self.path == "/status":
            body = json.dumps({"wled": _wled, "leds": _led_count}).encode()
            self.send_response(200)
        else:
            body = b"?"
            self.send_response(404)
        self._cors()
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0))
        data = self.rfile.read(n)
        try:
            if self.path == "/ddp":
                rgb = data
            elif self.path == "/json":
                cols = json.loads(data)["colors"]
                rgb = bytes(v for c in cols for v in c[:3])
            else:
                raise ValueError("bad path")
            if len(rgb) % 3:
                raise ValueError("rgb length not multiple of 3")
            send_ddp(rgb)
            self.send_response(200)
            body = b"ok"
        except Exception as e:
            self.send_response(400)
            body = str(e).encode()
        self._cors()
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--wled", default=_wled)
    ap.add_argument("--port", type=int, default=8090)
    ap.add_argument("--leds", type=int, default=_led_count)
    a = ap.parse_args()
    _wled, _led_count = a.wled, a.leds
    print(f"relay on :{a.port} -> DDP {_wled}:{DDP_PORT} ({_led_count} leds)")
    ThreadingHTTPServer(("0.0.0.0", a.port), Handler).serve_forever()
