#!/usr/bin/env python3
# tv-stack.py — stack completo pra CIFS-root + WLED ambilight.
#   9998: shell root da TV (stage2 disca aqui)
#   9997: ponte de controle (comandos pro shell)
#   9999: beacon STAGE2-OK
#   8888: HTTP GET — serve arquivos do repo pra TV (curl http://PC:8888/<path>)
#   8899: HTTP PUT — TV faz upload de arquivos pro PC (tools/tvbin/)
import socket, threading, queue, sys, os, http.server

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools')
UPLOAD_DIR = os.path.join(ROOT, 'tvbin')

tv_send = queue.Queue()
state = {'tv': None}

def tv_server():
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('0.0.0.0', 9998)); s.listen(5)
    print('[9998] aguardando TV...', flush=True)
    while True:
        conn, addr = s.accept()
        print('\n[9998] ROOT SHELL de %s' % (addr,), flush=True)
        state['tv'] = conn
        conn.settimeout(600)
        def writer(c=conn):
            while True:
                data = tv_send.get()
                if state['tv'] is not c:
                    # socket remplacé (TV reconnectée): remettre la commande en file
                    # pour que le writer de la connexion courante la récupère.
                    tv_send.put(data)
                    return
                try: c.sendall(data)
                except Exception: return
        threading.Thread(target=writer, daemon=True).start()
        try:
            while True:
                d = conn.recv(4096)
                if not d: break
                sys.stdout.write(d.decode('utf-8', 'replace')); sys.stdout.flush()
        except Exception: pass
        state['tv'] = None
        print('\n[9998] TV desconectou', flush=True)

def ctl_server():
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('127.0.0.1', 9997)); s.listen(5)
    print('[9997] ponte de controle ok', flush=True)
    while True:
        conn, addr = s.accept()
        print('\n[9997] controlador conectado', flush=True)
        def pump(c=conn):
            try:
                while True:
                    d = c.recv(4096)
                    if not d: break
                    tv_send.put(d)
            except Exception: pass
        threading.Thread(target=pump, daemon=True).start()

def beacon():
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('0.0.0.0', 9999)); s.listen(5)
    print('[9999] beacon ok', flush=True)
    while True:
        conn, addr = s.accept()
        def pump(c=conn):
            try:
                while True:
                    d = c.recv(4096)
                    if not d: break
                    sys.stdout.write(d.decode('utf-8', 'replace')); sys.stdout.flush()
            except Exception: pass
        threading.Thread(target=pump, daemon=True).start()

class GetH(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=ROOT, **kw)
    def log_message(self, *a): pass

class PutH(http.server.BaseHTTPRequestHandler):
    def do_PUT(self):
        n = int(self.headers.get('Content-Length', 0))
        data = self.rfile.read(n)
        os.makedirs(UPLOAD_DIR, exist_ok=True)
        path = os.path.join(UPLOAD_DIR, self.path.strip('/').split('/')[-1])
        with open(path, 'wb') as f: f.write(data)
        self.send_response(200); self.end_headers()
        print('[8899] recv %s (%d)' % (path, n), flush=True)
    def log_message(self, *a): pass

def http_get():
    http.server.ThreadingHTTPServer(('0.0.0.0', 8888), GetH).serve_forever()

def http_put():
    http.server.ThreadingHTTPServer(('0.0.0.0', 8899), PutH).serve_forever()

for fn in (tv_server, ctl_server, beacon, http_get, http_put):
    threading.Thread(target=fn, daemon=True).start()
print('tv-stack rodando. TV -> Remote PC -> Shared Folder.', flush=True)
import time
while True: time.sleep(3600)
