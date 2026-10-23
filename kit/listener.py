import socket, sys, threading

def serve(port):
    s = socket.socket()
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('0.0.0.0', port))
    s.listen(5)
    print('port %d listening' % port, flush=True)
    while True:
        conn, addr = s.accept()
        print('\n[%d] CONEXAO de %s' % (port, addr), flush=True)
        def pump(c=conn, a=addr, p=port):
            try:
                while True:
                    d = c.recv(4096)
                    if not d:
                        break
                    sys.stdout.write(d.decode('utf-8', 'replace'))
                    sys.stdout.flush()
            except Exception as e:
                print('[%d] erro %s' % (p, e), flush=True)
        threading.Thread(target=pump, daemon=True).start()

for p in (9998, 9999):
    threading.Thread(target=serve, args=(p,), daemon=True).start()
threading.Event().wait()
