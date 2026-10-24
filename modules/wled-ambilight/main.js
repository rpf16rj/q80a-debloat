// wled-ambilight main.js — injected by TizenBrew into youtube.com/tv.
// Samples the <video> element edges -> 226 RGB zones -> POST to local service.
// LED layout: 226 total, start top-left corner, clockwise.
//   top 0..71 (L->R), right 72..112 (T->B), bottom 113..184 (R->L), left 185..225 (B->T)

(function () {
    'use strict';

    var SERVICE = 'http://127.0.0.1:8090/ddp';
    var LOAD_TIZENTUBE = true;   // also load TizenTube userscript (adblock etc.)
    var FPS = 15;
    var CW = 64, CH = 36;        // capture canvas size
    var TOP = 72, SIDE = 41, BOT = 72; // LED counts per edge
    var LEDS = TOP + SIDE + BOT + SIDE; // 226

    // Optionally pull in the TizenTube userscript so this mod = TizenTube + ambilight
    if (LOAD_TIZENTUBE) {
        try {
            var s = document.createElement('script');
            s.src = 'https://cdn.jsdelivr.net/npm/tizentube@latest/dist/userScript.js';
            s.onerror = function () { console.log('[wled] tizentube load failed, ambilight continues'); };
            document.head.appendChild(s);
        } catch (e) {}
    }

    var cv = document.createElement('canvas');
    cv.width = CW; cv.height = CH;
    var cx = cv.getContext('2d', { willReadFrequently: true });

    // Precompute LED sample rectangles in canvas pixel space
    var zones = [];
    function edgeZones(count, x0, y0, dx, dy, w, h) {
        for (var i = 0; i < count; i++) {
            var t = (i + 0.5) / count;
            zones.push({
                x: Math.max(0, Math.min(CW - w, Math.round((x0 + dx * t) * CW - w / 2))),
                y: Math.max(0, Math.min(CH - h, Math.round((y0 + dy * t) * CH - h / 2))),
                w: w, h: h
            });
        }
    }
    // sample patch ~2px thick, edge band width
    edgeZones(TOP,  0, 0,    1, 0,    Math.max(1, Math.round(CW / TOP)), 3);   // top L->R
    edgeZones(SIDE, 1, 0,    0, 1,    3, Math.max(1, Math.round(CH / SIDE))); // right T->B
    edgeZones(BOT,  1, 1,   -1, 0,    Math.max(1, Math.round(CW / BOT)), 3);  // bottom R->L
    edgeZones(SIDE, 0, 1,    0, -1,   3, Math.max(1, Math.round(CH / SIDE))); // left B->T

    function findVideo() { return document.querySelector('video'); }

    var video = null;
    var timer = setInterval(function () {
        if (!video || video.readyState < 2 || video.paused || video.ended) {
            video = findVideo();
            if (!video) return;
        }
        try {
            cx.drawImage(video, 0, 0, CW, CH);
        } catch (e) { return; }  // DRM/cross-origin -> frame not readable, skip
        var img;
        try {
            img = cx.getImageData(0, 0, CW, CH).data;
        } catch (e) { return; }
        var rgb = new Uint8Array(LEDS * 3);
        for (var z = 0; z < LEDS; z++) {
            var zn = zones[z], r = 0, g = 0, b = 0, n = 0;
            for (var y = zn.y; y < zn.y + zn.h; y++) {
                for (var x = zn.x; x < zn.x + zn.w; x++) {
                    var p = (y * CW + x) * 4;
                    r += img[p]; g += img[p + 1]; b += img[p + 2]; n++;
                }
            }
            rgb[z * 3] = r / n; rgb[z * 3 + 1] = g / n; rgb[z * 3 + 2] = b / n;
        }
        try {
            fetch(SERVICE, { method: 'POST', body: rgb.buffer, mode: 'no-cors' }).catch(function () {});
        } catch (e) {}
    }, Math.round(1000 / FPS));

    console.log('[wled] ambilight capture running, ' + LEDS + ' zones');
})();
