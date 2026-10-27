/* SPDX-License-Identifier: MIT
 * Stock wlink 0.1.3 wire contract, source 7ed4b99321fb8e88eb05f7553211b39d41196a6c.
 * No generic option/configuration writes, DMI, power or erase opcodes. Reset is
 * available only in a separate exact-chip-bound session after host readback.
 */
#include "wch_guard.h"
#include "wch_loader.h"
#include <errno.h>
#include <string.h>

#define FLASH_START 0x08000000U
#define FLASH_SIZE 16384U
static uint32_t be32(const unsigned char *p) { return (uint32_t)p[0]<<24 | (uint32_t)p[1]<<16 | (uint32_t)p[2]<<8 | p[3]; }
void wch_guard_poison(struct wch_guard *g) { g->poisoned=1; g->attached=g->read_ok=g->write_ok=0; }
static int refuse(struct wch_guard *g) { wch_guard_poison(g); return -EPERM; }
static int ordinary_region(uint32_t address, uint32_t length) {
    return length && length<=FLASH_SIZE && address>=FLASH_START && address-FLASH_START<=FLASH_SIZE-length;
}
static int allowed_part(uint32_t id) {
    uint32_t part=id&0xffffff0fU;
    return part==0x00300500U || part==0x00310500U || part==0x00320500U || part==0x00330500U;
}
void wch_guard_init_read_only(struct wch_guard *g) { memset(g,0,sizeof(*g)); g->read_only=1; }
int wch_guard_init(struct wch_guard *g, const char *text) {
    uint32_t id=0;
    memset(g,0,sizeof(*g));
    if (!text || strlen(text)!=8) return -EINVAL;
    for (unsigned n=0;n<8;n++) {
        unsigned char c=(unsigned char)text[n]; unsigned v;
        if (c>='0' && c<='9') v=c-'0';
        else if (c>='a' && c<='f') v=c-'a'+10;
        else if (c>='A' && c<='F') v=c-'A'+10;
        else return -EINVAL;
        id=(id<<4)|v;
    }
    if (!allowed_part(id)) return -EINVAL;
    g->chip_id=id; return 0;
}
int wch_guard_init_reset(struct wch_guard *g, const char *text) {
    int result=wch_guard_init(g,text);
    if (!result) g->read_only=g->reset_only=1;
    return result;
}
int wch_guard_init_read_only_bound(struct wch_guard *g, const char *text) {
    if (!text) { wch_guard_init_read_only(g); return 0; }
    int result=wch_guard_init(g,text);
    if (!result) g->read_only=1;
    return result;
}
static int expect(struct wch_guard *g, enum wch_response response, unsigned char opcode) {
    g->response=response; g->response_opcode=opcode; return 0;
}
int wch_guard_out(struct wch_guard *g, unsigned ep, const unsigned char *p, size_t n) {
    if (g->poisoned || !p || !n || n>64 || g->response!=WCH_NONE) return refuse(g);
    if (ep==2) {
        if (g->read_only || n!=64 || !g->attached || !g->read_ok || !g->write_ok) return refuse(g);
        if (g->phase==WCH_LOADER) {
            if (g->loader_offset>sizeof(wch_loader)-64 || memcmp(p,wch_loader+g->loader_offset,64)) return refuse(g);
            g->loader_offset+=64;
            if (g->loader_offset==sizeof(wch_loader)) g->phase=WCH_LOADER_READY;
            return 0;
        }
        if (g->phase!=WCH_PROGRAM || g->written>=g->padded_length) return refuse(g);
        size_t actual=g->source_length>g->written?g->source_length-g->written:0;
        if (actual>64) actual=64;
        for (size_t i=actual;i<64;i++) if (p[i]!=0xff) return refuse(g);
        g->written+=64;
        if (g->written%1024==0 || g->written==g->padded_length) g->phase=WCH_ACK;
        return 0;
    }
    if (ep!=1 || n<4 || p[0]!=0x81 || p[2]!=n-3) return refuse(g);
    unsigned op=p[1], sub=p[3];
    if (g->reset_only && (op==1 || op==2 || op==3)) return refuse(g);
    if (g->read_only && (op==1 || (op==2 && sub!=0x0c))) return refuse(g);
    if (op==0x0d && n==4) {
        if (sub==1 && g->phase==WCH_IDLE) return expect(g,WCH_PROBE,op);
        if (sub==2 && g->probe_ok && g->phase==WCH_IDLE) {
            g->attached=g->read_ok=g->write_ok=0; return expect(g,WCH_ATTACH,op);
        }
        if (sub==0xff && g->phase==WCH_IDLE) {
            g->attached=g->read_ok=g->write_ok=0; return expect(g,WCH_DETACH,op);
        }
        return refuse(g);
    }
    if (op==0x0c && n==5 && sub==9 && p[4]>=1 && p[4]<=3 && g->probe_ok && g->phase==WCH_IDLE)
        return expect(g,WCH_SPEED,op);
    if (!g->attached) return refuse(g);
    if (op==0x0b && n==4 && sub==1 && g->reset_only && g->phase==WCH_IDLE)
        return expect(g,WCH_RESET_ACK,op);
    if (op==6 && n==4 && g->phase==WCH_IDLE) {
        if (sub==1) { g->read_ok=0; return expect(g,WCH_READ_PROTECTION,op); }
        if (sub==4) { g->write_ok=0; return expect(g,WCH_WRITE_PROTECTION,op); }
        return refuse(g); /* Includes every option/protection/configuration mutation. */
    }
    if (op==0x11 && n==4 && (sub==6 || sub==9) && g->phase==WCH_IDLE) return expect(g,WCH_SIGNATURE,op);
    if ((op==1 || op==3) && n==11 && g->phase==WCH_IDLE && g->read_ok) {
        uint32_t address=be32(p+3), length=be32(p+7);
        if (!ordinary_region(address,length)) return refuse(g);
        if (op==1) {
            uint32_t padded=(length+63U)&~63U;
            if (!g->write_ok || !ordinary_region(address,padded)) return refuse(g);
            g->region=address; g->source_length=length; g->padded_length=padded; g->written=0; g->loader_offset=0;
            return expect(g,WCH_WRITE_REGION_ACK,op);
        }
        if (length%4) return refuse(g);
        g->region=address; g->read_remaining=length; return expect(g,WCH_READ_REGION_ACK,op);
    }
    if (op==2 && n==4) {
        if (sub==5 && g->phase==WCH_REGION && g->read_ok && g->write_ok) return expect(g,WCH_LOADER_ACK,op);
        if (sub==7 && g->phase==WCH_LOADER_READY) return expect(g,WCH_COMMIT_ACK,op);
        if (sub==2 && g->phase==WCH_COMMITTED) return expect(g,WCH_PROGRAM_ACK,op);
        if (sub==8 && g->phase==WCH_PROGRAM_DONE) return expect(g,WCH_END_ACK,op);
        if (sub==0x0c && g->phase==WCH_READ_REGION) return expect(g,WCH_READ_ACK,op);
    }
    return refuse(g);
}
int wch_guard_input_allowed(const struct wch_guard *g, unsigned ep, size_t n) {
    if (g->poisoned || n!=64) return -EPERM;
    if (ep==0x81 && g->response!=WCH_NONE) return 0;
    if (ep==0x82 && (g->phase==WCH_ACK || g->phase==WCH_READ)) return 0;
    return -EPERM;
}
int wch_guard_in(struct wch_guard *g, unsigned ep, const unsigned char *p, size_t n) {
    if (g->poisoned || !p || !n || n>64) return refuse(g);
    if (ep==0x82) {
        if (g->phase==WCH_ACK && n==4 && !memcmp(p,"\x41\x01\x01\x04",4)) {
            g->phase=g->written==g->padded_length?WCH_PROGRAM_DONE:WCH_PROGRAM; return 0;
        }
        if (g->phase==WCH_READ && n<=g->read_remaining && n%4==0) {
            g->read_remaining-=(uint32_t)n; if (!g->read_remaining) g->phase=WCH_IDLE; return 0;
        }
        return refuse(g);
    }
    if (ep!=0x81 || g->response==WCH_NONE) return refuse(g);
    enum wch_response expected=g->response;
    if (expected==WCH_SIGNATURE) {
        /* Upstream's documented raw form is 20 bytes; legacy parsers also
         * consume the 12-byte flash-size/UID prefix. Never accept arbitrary
         * extra bytes, a family-sized fallback, or a contradictory chip ID. */
        if ((n!=12 && n!=20) || p[2]!=0 || p[3]!=16 || (n==20 && be32(p+16)!=g->chip_id)) return refuse(g);
        g->response=WCH_NONE; return 0;
    }
    if (n<3 || p[0]!=0x82 || p[1]!=g->response_opcode || p[2]!=n-3) return refuse(g);
    const unsigned char *data=p+3; size_t count=n-3;
    switch (expected) {
    case WCH_PROBE:
        if (count!=4 || !((data[2]==2)||(data[2]==12)||(data[2]==3)||(data[2]==5)||(data[2]==85))) return refuse(g);
        g->probe_ok=1; break;
    case WCH_SPEED: if (count!=1 || data[0]!=1) return refuse(g); break;
    case WCH_ATTACH:
        if (count!=5 || data[0]!=9 || !allowed_part(be32(data+1))) return refuse(g);
        if (!g->chip_id && g->read_only) g->chip_id=be32(data+1);
        if (be32(data+1)!=g->chip_id) return refuse(g);
        g->attached=1; break;
    case WCH_READ_PROTECTION:
        if (count!=1 || data[0]!=2) return refuse(g);
        g->read_ok=1; break;
    case WCH_WRITE_PROTECTION:
        if (count!=1 || data[0]!=0) return refuse(g);
        g->write_ok=1; break;
    case WCH_COMMIT_ACK:
        if (count!=1 || data[0]!=7) return refuse(g);
        g->phase=WCH_COMMITTED; break;
    case WCH_LOADER_ACK: if (count!=1) return refuse(g); g->phase=WCH_LOADER; break;
    case WCH_PROGRAM_ACK: if (count!=1) return refuse(g); g->phase=WCH_PROGRAM; break;
    case WCH_END_ACK: if (count!=1) return refuse(g); g->phase=WCH_IDLE; break;
    case WCH_READ_ACK: if (count!=1) return refuse(g); g->phase=WCH_READ; break;
    case WCH_WRITE_REGION_ACK: if (count>1) return refuse(g); g->phase=WCH_REGION; break;
    case WCH_READ_REGION_ACK: if (count>1) return refuse(g); g->phase=WCH_READ_REGION; break;
    case WCH_DETACH: if (count>1) return refuse(g); break;
    case WCH_RESET_ACK:
        if (count>1) return refuse(g);
        g->attached=g->read_ok=g->write_ok=0; break;
    default: return refuse(g);
    }
    g->response=WCH_NONE; return 0;
}
