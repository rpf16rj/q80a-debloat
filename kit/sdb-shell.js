// sdb-shell.js <tvIp> <command...>
// Runs a shell command on the TV via sdb (adb protocol, port 26101).
// Usage: node sdb-shell.js 192.168.3.191 "id; uname -a"

const adbhost = require('adbhost');

const tvIp = process.argv[2];
const cmd = process.argv.slice(3).join(' ');
if (!tvIp || !cmd) {
    console.log('usage: node sdb-shell.js <tvIp> "<command>"');
    process.exit(1);
}

function runShell(adb, command) {
    return new Promise((resolve) => {
        let s;
        try {
            s = adb.createStream(`shell:0 ${command}`);
        } catch (e) {
            return resolve('[createStream error] ' + e.message);
        }
        let out = '';
        s.on('data', c => { out += c.toString(); });
        s.on('end', () => resolve(out));
        s.on('close', () => resolve(out));
        s.on('error', e => resolve('[stream error] ' + e.message));
        setTimeout(() => resolve(out + '\n[30s timeout]'), 30000);
    });
}

async function main() {
    const adb = adbhost.createConnection({ host: tvIp, port: 26101 });
    await new Promise((res, rej) => {
        adb.once('connect', res);
        adb._stream.on('error', rej);
        setTimeout(() => rej(new Error('connect timeout')), 15000);
    });
    console.error('[connected]');
    const out = await runShell(adb, cmd);
    console.log(out);
    process.exit(0);
}

main().catch(e => { console.error('ERR:', e.message || e); process.exit(1); });
