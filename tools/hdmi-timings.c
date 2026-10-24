/* hdmi-timings.c — query live HDMI input timings on Samsung sdp-hdmi (/dev/video21)
 *
 * Cross-compile for armv7l (Tizen 6.0 / Q80A):
 *   arm-linux-gnueabi-gcc -static -O2 -o hdmi-timings hdmi-timings.c
 * or with zig:
 *   zig cc -target arm-linux-musleabi -static -O2 -o hdmi-timings hdmi-timings.c
 *
 * Run on TV:  ./hdmi-timings [device]   (default /dev/video21)
 * Prints: resolution, fps, interlaced, colorspace if the driver reports it.
 */
#include <stdio.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <linux/videodev2.h>

int main(int argc, char **argv)
{
    const char *dev = argc > 1 ? argv[1] : "/dev/video21";
    int fd = open(dev, O_RDONLY | O_NONBLOCK);
    if (fd < 0) { perror(dev); return 1; }

    struct v4l2_dv_timings t;
    memset(&t, 0, sizeof t);

    if (ioctl(fd, VIDIOC_QUERY_DV_TIMINGS, &t) < 0) {
        perror("VIDIOC_QUERY_DV_TIMINGS");
        /* try detection of what the device supports */
        struct v4l2_dv_timings_cap cap;
        memset(&cap, 0, sizeof cap);
        if (ioctl(fd, VIDIOC_DV_TIMINGS_CAP, &cap) == 0)
            printf("device supports dv-timings caps (type %u)\n", cap.type);
        close(fd);
        return 2;
    }

    const struct v4l2_bt_timings *b = &t.bt;
    unsigned long long pixclk = b->pixelclock ? b->pixelclock : 1;
    unsigned int ht = b->width + b->hfrontporch + b->hsync + b->hbackporch;
    unsigned int vt = b->height + b->vfrontporch + b->vsync + b->vbackporch;
    double fps = (double)pixclk / ((double)ht * (double)(vt ? vt : 1));

    printf("res      %ux%u %s\n", b->width, b->height,
           (b->interlaced == V4L2_DV_INTERLACED) ? "interlaced" : "progressive");
    printf("pixclk   %llu Hz\n", pixclk);
    printf("htotal   %u  vtotal %u\n", ht, vt);
    printf("fps      %.3f\n", fps);
    printf("vsync    %u+%u+%u  hsync %u+%u+%u\n",
           b->vfrontporch, b->vsync, b->vbackporch,
           b->hfrontporch, b->hsync, b->hbackporch);
    printf("polar    hsync=%u vsync=%u\n", b->polarities & V4L2_DV_HSYNC_POS_POL ? 1 : 0,
           b->polarities & V4L2_DV_VSYNC_POS_POL ? 1 : 0);
    printf("standards 0x%llx  flags 0x%x\n",
           (unsigned long long)b->standards, b->flags);
    close(fd);
    return 0;
}
