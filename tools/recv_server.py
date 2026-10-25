import http.server, os, sys
class H(http.server.BaseHTTPRequestHandler):
    def do_PUT(self):
        n = int(self.headers.get('Content-Length', 0))
        data = self.rfile.read(n)
        path = os.path.join('C:/Users/rpf16/q80a_debloat/tools/tvbin', self.path.strip('/').split('/')[-1])
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'wb') as f: f.write(data)
        self.send_response(200); self.end_headers()
        print('recv', path, n, flush=True)
    def log_message(self, *a): pass
http.server.HTTPServer(('0.0.0.0', 8899), H).serve_forever()
