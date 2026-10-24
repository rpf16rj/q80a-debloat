// wled-ambilight service — TizenBrew service module (Node on TV).
// Receives raw RGB frames over local HTTP and forwards as DDP UDP to WLED.
// No external deps: http + dgram builtins only.

const http = require('http');
const dgram = require('dgram');

const WLED_IP = '192.168.3.176';
const DDP_PORT = 4048;
const LISTEN_PORT = 8090;
const LED_COUNT = 226;

const udp = dgram.createSocket('udp4');
let seq = 0;

function sendDDP(rgb) {
    // DDP header: flags(0x41=VER1|PUSH) seq type(0x01=RGB24) dest offset(4) len(2)
    const h = Buffer.alloc(10);
    h[0] = 0x41;
    h[1] = seq & 0x0f;
    h[2] = 0x01;
    h[3] = 0x01;
    h.writeUInt32BE(0, 4);
    h.writeUInt16BE(rgb.length, 8);
    udp.send(Buffer.concat([h, rgb]), DDP_PORT, WLED_IP);
    seq = (seq + 1) & 0x0f;
}

const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, GET, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'content-type');

    if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }

    if (req.method === 'GET' && req.url === '/ping') {
        res.writeHead(200); res.end('ok'); return;
    }

    if (req.method === 'POST' && req.url === '/ddp') {
        const chunks = [];
        req.on('data', c => chunks.push(c));
        req.on('end', () => {
            const rgb = Buffer.concat(chunks);
            if (rgb.length % 3 === 0 && rgb.length > 0) {
                sendDDP(rgb);
                res.writeHead(200); res.end('ok');
            } else {
                res.writeHead(400); res.end('bad rgb');
            }
        });
        return;
    }

    res.writeHead(404); res.end('?');
});

server.listen(LISTEN_PORT, '127.0.0.1', () => {
    console.log('wled-ambilight service: :' + LISTEN_PORT + ' -> DDP ' + WLED_IP + ':' + DDP_PORT);
});
