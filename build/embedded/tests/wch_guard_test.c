/* Synthetic protocol contracts, not physical WCH captures. */
#include "wch_guard.h"
#include "wch_loader.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void exchange(struct wch_guard *g, const unsigned char *command, size_t length,
                     const unsigned char *response, size_t count) {
    assert(wch_guard_out(g,1,command,length)==0);
    assert(wch_guard_input_allowed(g,0x81,64)==0);
    assert(wch_guard_in(g,0x81,response,count)==0);
}
static struct wch_guard attached(void) {
    struct wch_guard g;
    assert(wch_guard_init(&g,"00300500")==0);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,1},4,(unsigned char[]){0x82,0x0d,4,2,9,2,0},7);
    exchange(&g,(unsigned char[]){0x81,0x0c,2,9,1},5,(unsigned char[]){0x82,0x0c,1,1},4);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,2},4,(unsigned char[]){0x82,0x0d,5,9,0,0x30,5,0},8);
    return g;
}
static struct wch_guard unprotected(void) {
    struct wch_guard g=attached();
    exchange(&g,(unsigned char[]){0x81,6,1,1},4,(unsigned char[]){0x82,6,1,2},4);
    exchange(&g,(unsigned char[]){0x81,6,1,4},4,(unsigned char[]){0x82,6,1,0},4);
    return g;
}
static struct wch_guard loader(void) {
    struct wch_guard g=unprotected();
    exchange(&g,(unsigned char[]){0x81,1,8,8,0,0,0,0,0,4,2},11,(unsigned char[]){0x82,1,1,0},4);
    exchange(&g,(unsigned char[]){0x81,2,1,5},4,(unsigned char[]){0x82,2,1,5},4);
    return g;
}
int main(void) {
    struct wch_guard g;
    for (const char **p=(const char*[]){"00000000","0030050g","003005000","00300501","FFFFFFFF",NULL};*p;p++)
        assert(wch_guard_init(&g,*p)<0);
    assert(wch_guard_init(&g,"00300510")==0); /* Revision accepted only when explicitly selected. */

    g=loader();
    for (size_t i=0;i<512;i+=64) assert(wch_guard_out(&g,2,wch_loader+i,64)==0);
    exchange(&g,(unsigned char[]){0x81,2,1,7},4,(unsigned char[]){0x82,2,1,7},4);
    exchange(&g,(unsigned char[]){0x81,2,1,2},4,(unsigned char[]){0x82,2,1,2},4);
    unsigned char data[64]; memset(data,0x55,sizeof(data));
    for (unsigned i=0;i<16;i++) assert(wch_guard_out(&g,2,data,64)==0);
    assert(wch_guard_out(&g,2,data,64)<0); /* Next block cannot precede real ACK. */
    g=loader();
    for (size_t i=0;i<512;i+=64) assert(wch_guard_out(&g,2,wch_loader+i,64)==0);
    exchange(&g,(unsigned char[]){0x81,2,1,7},4,(unsigned char[]){0x82,2,1,7},4);
    exchange(&g,(unsigned char[]){0x81,2,1,2},4,(unsigned char[]){0x82,2,1,2},4);
    for (unsigned i=0;i<16;i++) assert(wch_guard_out(&g,2,data,64)==0);
    assert(wch_guard_in(&g,0x82,(unsigned char[]){0x41,1,1,4},4)==0);
    memset(data,0xff,sizeof(data)); data[0]=1; data[1]=2;
    assert(wch_guard_out(&g,2,data,64)==0);
    assert(wch_guard_in(&g,0x82,(unsigned char[]){0x41,1,1,4},4)==0);
    exchange(&g,(unsigned char[]){0x81,2,1,8},4,(unsigned char[]){0x82,2,1,8},4);
    assert(g.phase==WCH_IDLE && g.written==1088 && !g.poisoned);

    for (unsigned sub=0;sub<256;sub++) {
        if (sub==1 || sub==4) continue;
        g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,6,1,(unsigned char)sub},4)<0);
    }
    for (unsigned sub=0;sub<256;sub++) {
        if (sub==1 || sub==2 || sub==0xff) continue;
        g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0d,1,(unsigned char)sub},4)<0);
    }
    for (unsigned sub=0;sub<256;sub++) {
        g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,2,1,(unsigned char)sub},4)<0);
    }
    for (unsigned ep=0;ep<256;ep++) {
        if (ep==1) continue;
        g=unprotected(); assert(wch_guard_out(&g,ep,(unsigned char[]){0x81,6,1,1},4)<0);
    }
    for (unsigned i=0;i<512;i++) {
        g=loader();
        for (unsigned before=0;before<i/64;before++) assert(wch_guard_out(&g,2,wch_loader+before*64,64)==0);
        memcpy(data,wch_loader+(i/64)*64,64); data[i%64]^=1;
        assert(wch_guard_out(&g,2,data,64)<0);
    }
    for (unsigned n=1;n<64;n++) {
        g=loader(); assert(wch_guard_out(&g,2,wch_loader,n)<0);
    }
    g=attached(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,1,8,8,0,0,0,0,0,0,64},11)<0);
    g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,1,8,8,0,0x3f,0xff,0,0,0,1},11)<0); /* Padding crosses flash end. */
    g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,1,8,0x1f,0xff,0xf8,0,0,0,0,64},11)<0); /* Option bytes. */
    for (unsigned n=0;n<4;n++) {
        g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,6,1,2},n)<0);
    }
    g=attached(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,6,1,1},4)==0);
    assert(wch_guard_in(&g,0x81,(unsigned char[]){0x82,6,1,1},4)<0); /* Protected; no auto-unprotect. */
    g=unprotected(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0d,1,0xff},4)==0);
    assert(!g.attached && !g.read_ok && !g.write_ok);
    assert(wch_guard_in(&g,0x81,(unsigned char[]){0x82,0x0d,1,0},4)==0);
    assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0d,1,2},4)==0);
    assert(wch_guard_in(&g,0x81,(unsigned char[]){0x82,0x0d,5,9,0,0x30,5,0x10},8)<0); /* Exact revision changed. */
    for (unsigned bad=0;bad<4;bad++) {
        g=attached(); assert(wch_guard_out(&g,1,(unsigned char[]){0x81,6,1,1},4)==0);
        unsigned char reply[]={0x82,6,1,2}; reply[bad]^=0x80;
        assert(wch_guard_in(&g,0x81,reply,sizeof(reply))<0);
    }
    g=unprotected();
    exchange(&g,(unsigned char[]){0x81,3,8,8,0,0,0,0,0,0,64},11,(unsigned char[]){0x82,3,1,0},4);
    exchange(&g,(unsigned char[]){0x81,2,1,0x0c},4,(unsigned char[]){0x82,2,1,0x0c},4);
    memset(data,0,sizeof(data)); assert(wch_guard_in(&g,0x82,data,64)==0 && g.phase==WCH_IDLE);
    wch_guard_init_read_only(&g);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,1},4,(unsigned char[]){0x82,0x0d,4,2,9,2,0},7);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,2},4,(unsigned char[]){0x82,0x0d,5,9,0,0x30,5,0x10},8);
    assert(g.chip_id==0x00300510 && g.read_only);
    exchange(&g,(unsigned char[]){0x81,6,1,1},4,(unsigned char[]){0x82,6,1,2},4);
    exchange(&g,(unsigned char[]){0x81,6,1,4},4,(unsigned char[]){0x82,6,1,0},4);
    struct wch_guard readonly=g;
    assert(wch_guard_out(&g,1,(unsigned char[]){0x81,1,8,8,0,0,0,0,0,0,64},11)<0);
    g=readonly; assert(wch_guard_out(&g,2,wch_loader,64)<0);
    g=readonly;
    exchange(&g,(unsigned char[]){0x81,3,8,8,0,0,0,0,0,0,64},11,(unsigned char[]){0x82,3,1,0},4);
    exchange(&g,(unsigned char[]){0x81,2,1,0x0c},4,(unsigned char[]){0x82,2,1,0x0c},4);
    assert(wch_guard_in(&g,0x82,data,64)==0);
    g=unprotected();
    assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0b,1,1},4)<0); /* Flash never implicitly runs. */
    g=readonly;
    assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0b,1,1},4)<0); /* Capture/backup cannot reset. */
    assert(wch_guard_init_reset(&g,"00300500")==0);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,1},4,(unsigned char[]){0x82,0x0d,4,2,9,2,0},7);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,2},4,(unsigned char[]){0x82,0x0d,5,9,0,0x30,5,0},8);
    struct wch_guard reset=g;
    exchange(&g,(unsigned char[]){0x81,0x0b,1,1},4,(unsigned char[]){0x82,0x0b,1,1},4);
    assert(!g.attached && g.read_only && g.reset_only);
    for (unsigned sub=0;sub<256;sub++) {
        if (sub==1) continue;
        g=reset;
        assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0b,1,(unsigned char)sub},4)<0);
    }
    for (unsigned op=1;op<=3;op++) {
        g=reset;
        assert(wch_guard_out(&g,1,(unsigned char[]){0x81,(unsigned char)op,8,8,0,0,0,0,0,0,64},11)<0);
    }
    assert(wch_guard_init_read_only_bound(&g,NULL)==0 && !g.chip_id && g.read_only);
    assert(wch_guard_init_read_only_bound(&g,"")<0);
    assert(wch_guard_init_read_only_bound(&g,"00300510")==0 && g.chip_id==0x00300510 && g.read_only);
    exchange(&g,(unsigned char[]){0x81,0x0d,1,1},4,(unsigned char[]){0x82,0x0d,4,2,9,2,0},7);
    assert(wch_guard_out(&g,1,(unsigned char[]){0x81,0x0d,1,2},4)==0);
    assert(wch_guard_in(&g,0x81,(unsigned char[]){0x82,0x0d,5,9,0,0x30,5,0},8)<0); /* Readback cannot relearn another revision. */
    puts("WCH_PROTOCOL_GUARD_PASSED synthetic=true hardware=false cases>1500");
    return 0;
}
