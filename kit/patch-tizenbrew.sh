#!/bin/bash
# patch-tizenbrew.sh — adds local/ module support to TizenBrew NG on the TV.
# Run as root via the bridge. Idempotent-ish (backs up originals once).

SVC=/opt/usr/apps/xvvl3S1bvH/res/wgt/service-nextgen/service
MODDIR=/opt/usr/apps/xvvl3S1bvH/res/wgt/localmodules
CFG=/home/owner/share/tizenbrewConfig.json

echo "[1] backups"
for f in $SVC/index.js $SVC/utils/moduleLoader.js $SVC/utils/serviceLauncher.js $SVC/utils/debugger.js; do
    [ -f "$f.orig" ] || cp "$f" "$f.orig"
done

echo "[2] index.js: /local/ static route"
grep -q "startsWith('/local/')" $SVC/index.js || sed -i "/app.all('\*', (req, res) => {/a\\
        if (req.url.startsWith('/local/')) { const relPath = req.url.substring(7).split('?')[0]; return res.sendFile('$MODDIR/' + relPath); }" $SVC/index.js

echo "[3] moduleLoader.js: local package.json"
grep -q "module.startsWith(\`local/\`) ? \`http://127.0.0.1:8081/local/\${module.substring(6)}/package.json" $SVC/utils/moduleLoader.js || sed -i 's|fetch(`https://cdn.jsdelivr.net/${module}/package.json`)|fetch(module.startsWith(`local/`) ? `http://127.0.0.1:8081/local/${module.substring(6)}/package.json` : `https://cdn.jsdelivr.net/${module}/package.json`)|' $SVC/utils/moduleLoader.js

echo "[3b] moduleLoader.js: local appPath for app-type modules"
grep -q "8081/local/\${module.substring(6)}/\${moduleJson.appPath}" $SVC/utils/moduleLoader.js || sed -i 's|appPath: `http://127.0.0.1:8081/module/${encodeURIComponent(module)}/${moduleJson.appPath}`,|appPath: module.startsWith(`local/`) ? `http://127.0.0.1:8081/local/${module.substring(6)}/${moduleJson.appPath}` : `http://127.0.0.1:8081/module/${encodeURIComponent(module)}/${moduleJson.appPath}`,|' $SVC/utils/moduleLoader.js

echo "[4] serviceLauncher.js: local serviceFile"
grep -q "startsWith('local/')" $SVC/utils/serviceLauncher.js || sed -i 's|fetch(`https://cdn.jsdelivr.net/${mdl.fullName}/${mdl.serviceFile}`)|fetch(mdl.fullName.startsWith(`local/`) ? `http://127.0.0.1:8081/local/${mdl.fullName.substring(6)}/${mdl.serviceFile}` : `https://cdn.jsdelivr.net/${mdl.fullName}/${mdl.serviceFile}`)|' $SVC/utils/serviceLauncher.js

echo "[5] debugger.js: local mainFile"
grep -q "startsWith('local/')" $SVC/utils/debugger.js || sed -i 's|fetch(`https://cdn.jsdelivr.net/${mdl.fullName}/${mdl.mainFile}`)|fetch(mdl.fullName.startsWith(`local/`) ? `http://127.0.0.1:8081/local/${mdl.fullName.substring(6)}/${mdl.mainFile}` : `https://cdn.jsdelivr.net/${mdl.fullName}/${mdl.mainFile}`)|g' $SVC/utils/debugger.js

echo "[6] register module in config"
grep -q '"local/wled-ambilight"' $CFG || sed -i 's|"modules": \[|"modules": [\n        "local/wled-ambilight",|' $CFG

echo "[7] verify"
grep -n "local/" $SVC/index.js | head -3
grep -n "local/" $SVC/utils/moduleLoader.js | head -3
grep -n "local/" $SVC/utils/serviceLauncher.js | head -3
grep -n "local/" $SVC/utils/debugger.js | head -3
grep -n "local/" $CFG
ls -la $MODDIR/wled-ambilight/ 2>/dev/null
echo "PATCH-DONE"
