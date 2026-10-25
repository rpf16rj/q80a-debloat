// WLED Ambilight service — spawns the capture daemon.
// Runs under wrt-service (node) with the app's SMACK label.
module.exports.onStart = function () {
    console.log('[wled-ambi] service start');
    var child_process;
    try { child_process = require('child_process'); } catch (e) {
        console.log('[wled-ambi] no child_process: ' + e);
        return;
    }
    var EXE = '/home/owner/share/WledAmbiPpi.exe';
    var cmd = 'env HOME=/opt/usr/home/owner /usr/bin/dotnet ' + EXE +
              ' 192.168.3.176 4048 -1 > /opt/usr/home/owner/ppi.log 2>&1';
    try {
        var child = child_process.spawn('/bin/sh', ['-c', cmd], { detached: true, stdio: 'ignore' });
        child.unref();
        console.log('[wled-ambi] spawned pid=' + child.pid);
    } catch (e) {
        console.log('[wled-ambi] spawn failed: ' + e);
    }
};

module.exports.onRequest = function () {
    console.log('[wled-ambi] onRequest');
};

module.exports.onExit = function () {
    console.log('[wled-ambi] service exit');
};
