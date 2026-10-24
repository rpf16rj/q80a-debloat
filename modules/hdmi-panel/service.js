// hdmi-panel service — TizenBrew local module (Node on TV).
// GET /api -> JSON with per-port cable state + CEC device list.
// Sources: /sys extcon (world-readable) + busctl to org.tizen.tv.hdmicec.

const http = require('http');
const fs = require('fs');
const { exec } = require('child_process');

const PORT = 8091;
const EXTCON = '/sys/devices/virtual/extcon/hdmi_det_info/state';
const BUS = 'org.tizen.tv.hdmicec';
const OBJ = '/org/tizen/tv/hdmicec';
const IF = 'org.tizen.tv.HdmiCec';

function busctl(args) {
    return new Promise((resolve) => {
        exec(`busctl call ${BUS} ${OBJ} ${IF} ${args} 2>&1`,
            { timeout: 3000 },
            (e, out) => resolve((out || '').trim()));
    });
}

// busctl prints e.g. `ii 0 1`, `is 0 "name"` — grab trailing values
const val = (out, re) => (out.match(re) || [])[1];

async function collect() {
    const data = { ports: [], cec: [], errors: [] };

    try {
        const raw = fs.readFileSync(EXTCON, 'utf8');
        for (const line of raw.split('\n')) {
            const m = line.match(/^([A-Z0-9_]+)=(\d+)/);
            if (m) data[m[1]] = +m[2];
        }
        for (let i = 1; i <= 6; i++) {
            data.ports.push({
                port: `HDMI${i}`,
                cable: data[`HDMI${i}`] === 1,
                connected: data[`H${i}_c`] === 1
            });
        }
        data.arc = data.ARC === 1;
        data.earc = data.EARC === 1;
    } catch (e) { data.errors.push('extcon: ' + e.message); }

    try {
        const count = +(val(await busctl('GetDeviceCount b true'), /ii \d+ (\d+)/) || 0);
        const jobs = [];
        for (let i = 0; i < count; i++) {
            jobs.push((async () => ({
                name: val(await busctl(`GetDeviceName i ${i}`), /is \d+ "(.*)"/),
                port: val(await busctl(`GetDevicePortNum i ${i}`), /ii \d+ (\d+)/),
                type: val(await busctl(`GetDeviceType i ${i}`), /ii \d+ (\d+)/)
            }))());
        }
        data.cec = await Promise.all(jobs);
        data.activeSource = val(await busctl('GetActiveDeviceName'), /is \d+ "(.*)"/);
    } catch (e) { data.errors.push('cec: ' + e.message); }

    return data;
}

http.createServer(async (req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    if (req.url === '/api') {
        try {
            const d = await collect();
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(d));
        } catch (e) {
            res.writeHead(500); res.end(JSON.stringify({ errors: [e.message] }));
        }
    } else {
        res.writeHead(200); res.end('ok');
    }
}).listen(PORT, '127.0.0.1', () => console.log('hdmi-panel api on :' + PORT));
