#!/usr/bin/env python3
"""Parse Samsung MSDU11 upgrade.msd: partition table + OUIT headers,
then try known SWU keys against the encrypted header blob."""
import struct, sys, hashlib
from Crypto.Cipher import AES

MSD = r"C:\Users\rpf16\q80a_debloat\T-NKM2UABC\T-NKM2UABC\image\upgrade.msd"

SWU_KEYS = {
    "T-HKM":   "1ac8989ff57db5e75ea67b033050871c",
    "T-HKP":   "cce8a3ef92f3e94895999e928f4dd6c3",
    "T-JZM":   "9b1d077c0d137d406c79ddacb6b159fe",
    "T-HKMFK": "c7097975e8ab994beb5eaae57e0ba77c",
    "T-KTM":   "d0d49d5f36f5c0da50062fbf32168f5b",
    "T-KTSU":  "19e1ba41163f03735e692d9daa2cbb47",
    "T-KTSD":  "39332605ff47a0aea999b10ce9087389",
    "T-KTM2":  "29110e0ce940b3a9b67d3e158f3f1342",
    "T-KTM2L": "46b04f5e794ca4377a20951c9ea00427",
    "T-NKL":   "5bab1098dab48792619ebd63650d929f",
}

MAGIC18 = b"Tizen Software Upgrade Tree Binary Format ver. 1.8"
MAGIC19 = b"Tizen Software Upgrade Tree Binary Format ver. 1.9"

f = open(MSD, 'rb')
f.seek(0)
magic = f.read(6)
print("magic:", magic)
unk0, unk1, unk2, nitems = struct.unpack("<IIII", f.read(16))
print("unk0=%08x unk1=%08x unk2=%08x nparts=%d" % (unk0, unk1, unk2, nitems))

parts = []
for i in range(nitems):
    pid, offs, size = struct.unpack("<IQQ", f.read(20))
    parts.append((pid, offs, size))
    print("part %2d: offs=%012x size=%012x" % (pid, offs, size))

onitems, = struct.unpack("<I", f.read(4))
print("\nouit header items:", onitems)
headers = []
for i in range(onitems):
    offs, size, llen = struct.unpack("<QIB", f.read(13))
    label = f.read(llen).decode('ascii', 'replace')
    headers.append((offs, size, label))
    print("  hdr: label=%s offs=%012x size=%08x" % (label, offs, size))

# dump each header blob, look for Salted__
for offs, size, label in headers:
    blob = f.read(0)
    f.seek(offs)
    blob = f.read(size)
    idx = blob.find(b'Salted__')
    print("\n[%s] blob[0:16]=%s  Salted__ @ %d" % (label, blob[:16].hex(), idx))
    if idx >= 0:
        salt = blob[idx+8:idx+16]
        ct = blob[idx+16:]
        print("  salt=%s ctlen=%d" % (salt.hex(), len(ct)))
        for kname, khex in SWU_KEYS.items():
            key = bytes.fromhex(khex)
            iv = hashlib.md5(salt).digest()
            dec = AES.new(key, AES.MODE_CBC, iv).decrypt(ct)
            for m in (MAGIC18, MAGIC19):
                p = dec.find(m)
                if p >= 0:
                    print("  !!! KEY MATCH %s -> magic @%d" % (kname, p))
                    open("hdr_%s_dec.bin" % label.replace('/','_'), 'wb').write(dec)
        else:
            pass
f.close()
