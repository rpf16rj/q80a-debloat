// WLED Ambilight service — spawns the capture daemon as a child process.
// Runs under the TizenBrew StandaloneService (app uid) — ppicapture needs no root.
const { spawn } = require('child_process');

const EXE = '/home/owner/share/WledAmbiPpi.exe';
const WLED_IP = '192.168.3.176';
const WLED_PORT = '4048';

let child = null;

function start() {
  if (child) return;
  child = spawn('/usr/bin/dotnet', [EXE, WLED_IP, WLED_PORT, '-1'], {
    detached: true, stdio: 'ignore'
  });
  child.unref();
  console.log('[wled-ambi] started pid=' + child.pid);
}

function stop() {
  if (child) { try { process.kill(child.pid); } catch (e) {} child = null; }
  try { require('child_process').execSync('pkill -f WledAmbiPpi'); } catch (e) {}
}

start();
process.on('exit', () => { /* keep daemon running after module unload */ });
