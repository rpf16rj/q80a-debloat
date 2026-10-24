import socket, threading, queue, sys

# TV connects on 9998 (root shell). Control connects on 9997.
# Bytes flow TV->print + control<-queue. Commands written to control conn go to TV.
tv_send = queue.Queue()
state = {'tv': None}

def tv_server():
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('0.0.0.0', 9998)); s.listen(1)
    print('[9998] aguardando TV...', flush=True)
    while True:
        conn, addr = s.accept()
        print('\n[9998] ROOT SHELL de %s' % (addr,), flush=True)
        state['tv'] = conn
        conn.settimeout(600)
        def writer():
            while True:
                data = tv_send.get()
                try:
                    conn.sendall(data)
                except OSError:
                    return
        threading.Thread(target=writer, daemon=True).start()
        try:
            while True:
                d = conn.recv(4096)
                if not d:
                    break
                sys.stdout.buffer.write(d)
                sys.stdout.buffer.flush()
        except Exception as e:
            print('\n[9998] shell caiu:', e, flush=True)
        state['tv'] = None
        conn.close()

def control_server():
    s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('127.0.0.1', 9997)); s.listen(1)
    print('[9997] controle pronto', flush=True)
    while True:
        conn, addr = s.accept()
        print('[9997] controlador conectado', flush=True)
        try:
            while True:
                d = conn.recv(4096)
                if not d:
                    break
                tv_send.put(d)
        except OSError:
            pass
        conn.close()

threading.Thread(target=tv_server, daemon=True).start()
threading.Thread(target=control_server, daemon=True).start()
threading.Event().wait()
