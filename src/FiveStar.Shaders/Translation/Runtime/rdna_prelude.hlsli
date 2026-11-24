// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later
// Runtime support for translated RDNA programs.
// Bindless model: a descriptor's dword0 written by the D3D12.X shim is a PC descriptor-heap index (see GPU addresses).

cbuffer UserData : register(b0) { uint4 g_user4[32]; };       // initial user SGPRs (root constants)
static const uint GPU_MEM_HEAP = 0;                            // raw window of the loader's 32-bit space, RW view at + 1
static const uint GPU_MAP_HEAP = 7;                            // address map of game memory (raw SRV)
static const uint GPU_MAP_SPANS = 0x8000u;                     // 4 GiB spans of the 47-bit address space
static const uint STATIC_SAMPLER_BASE = 2032;                  // runtime-created samplers for literal S#s
static const uint PC_END = 0xffffffffu;
static const uint PC_YIELD = 0xfffffffeu;                      // mesh shaders: s_barrier yield point
static const uint CODE_TAG = 0xc0de0000u;                       // pointer hi-word for PC-relative (embedded) data

uint user_sgpr(uint i) { return g_user4[i >> 2][i & 3]; }

uint MemLoad(uint ptr_lo, uint off) {
    ByteAddressBuffer m = ResourceDescriptorHeap[GPU_MEM_HEAP];
    return m.Load((ptr_lo + off) & ~3u);
}
void MemStore(uint ptr_lo, uint off, uint v) {
    RWByteAddressBuffer m = ResourceDescriptorHeap[GPU_MEM_HEAP + 1];   // writable view of the GPU-memory window
    m.Store((ptr_lo + off) & ~3u, v);
}

// ---- GPU addresses. A 64-bit pointer or V# base whose high dword is 0 is in the loader's 32-bit space: the window
// for a pointer, a heap index for a V# (the shim's descriptor records). Any other address is a console GPU address in
// game memory, found in the address map the shim keeps: level 1 holds per 4 GiB span (high dword) the byte offset of
// its page table; a page table holds per 64 KiB page the heap index of a raw SRV over the page (its UAV at + 1) and
// the page's byte offset in that view. Unmapped spans and pages name a null SRV/UAV pair.
uint2 gpu_page(uint lo, uint hi) {
    ByteAddressBuffer map = ResourceDescriptorHeap[GPU_MAP_HEAP];
    uint table = map.Load((hi & (GPU_MAP_SPANS - 1u)) * 4u);
    uint2 page = map.Load2(table + (lo >> 16) * 8u);
    return uint2(page.x, page.y + (lo & 0xffffu));
}
uint PtrLoad(uint lo, uint hi, uint off) {   // carries out of the low dword are not propagated
    uint a = lo + off;
    if (hi == 0u) return MemLoad(a, 0u);
    uint2 p = gpu_page(a, hi);
    ByteAddressBuffer b = ResourceDescriptorHeap[p.x];
    return b.Load(p.y & ~3u);
}
void PtrStore(uint lo, uint hi, uint off, uint v) {
    uint a = lo + off;
    if (hi == 0u) { MemStore(a, 0u, v); return; }
    uint2 p = gpu_page(a, hi);
    RWByteAddressBuffer b = ResourceDescriptorHeap[p.x + 1u];
    b.Store(p.y & ~3u, v);
}
uint2 gpu_rw(uint lo, uint hi) {   // (RW heap index, byte address) of a pointer, for atomics
    if (hi == 0u) return uint2(GPU_MEM_HEAP + 1u, lo);
    uint2 p = gpu_page(lo, hi);
    return uint2(p.x + 1u, p.y);
}
// A V#'s view: (heap index, byte offset of the base in it); rw takes the UAV of a game-memory base. Dword1 holds
// base_hi in bits 0-15 and the stride in bits 16-29.
uint2 buf_view(uint d0, uint d1, bool rw) {
    uint hi = d1 & 0xffffu;
    if (hi == 0u) return uint2(d0, 0u);
    uint2 p = gpu_page(d0, hi);
    return uint2(p.x + (rw ? 1u : 0u), p.y);
}
uint buf_stride(uint d1) { return (d1 >> 16) & 0x3fffu; }

// RDNA range checking: an out-of-range buffer load reads 0, and an out-of-range store or atomic is dropped. Programs
// rely on it (append and argument writers run past their allocation, loops read zeros past an end). OOB_SELECT (V#
// dword3 bits 28-29): 2 checks nothing; 3 (raw) checks index * stride + offset against num_records (dword2); 0 and 1
// (structured) check the index (0 also the offset against the stride, left out: the shim's own records leave the field
// at 0). A V# with stride 0 is raw. The offset is the VGPR offset plus the instruction offset; soffset is not checked.
bool buf_ok(uint d1, uint d2, uint d3, uint index, uint offset) {
    uint sel = (d3 >> 28) & 3u;
    uint stride = buf_stride(d1);
    if (sel == 2u) return true;
    if (sel == 3u || stride == 0u) return index * stride + offset < d2;
    return index < d2;
}
// s_buffer_load: the whole offset against num_records, counted in bytes (stride 0) or in strides.
bool sbuf_ok(uint d1, uint d2, uint offset) {
    uint stride = buf_stride(d1);
    return (stride == 0u ? offset : offset / stride) < d2;
}

uint BufLoad(uint heap_index, uint off) {
    ByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    return b.Load(off & ~3u);
}
uint BufLoadRW(uint heap_index, uint off) {   // load through a UAV slot (buffer also written by this shader)
    RWByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    return b.Load(off & ~3u);
}
void BufStore(uint heap_index, uint off, uint v) {
    RWByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    b.Store(off & ~3u, v);
}

// ---- program waves. Translated code numbers lanes as the console does: 0 .. VW_WAVE-1 of a VW_WAVE-lane wave. The vw_*
// operations take and return those numbers. With VW_EMULATE they stay correct on any hardware wave size:
//   hardware wave = program wave: the native operation;
//   wider: each aligned VW_WAVE-lane segment of the hardware wave is one program wave;
//   narrower: the hardware wave holds program lanes [origin, origin + WaveGetLaneCount()); the other lanes are missing
//   and read as 0 (on pixel and vertex waves that is a partly filled console wave).
// WaveGetLaneCount() is a constant once the driver compiles the program, so only one path remains.
// VW_GROUP_LANES (mesh): the program lane comes from SV_GroupIndex (g_vw_lane), set at entry.
// VW_GROUP_WAVE (compute, VW_GROUP_SIZE <= VW_WAVE threads): the thread group is the console wave. Every operation
// exchanges through groupshared memory with a barrier, so it is right on any hardware wave, but every thread of the
// group must reach every operation: the translated control flow is group-uniform because each branch condition is
// one of these operations, and the translator rejects programs that call one under a per-lane condition.
#ifndef VW_WAVE
#define VW_WAVE 64
#endif
#ifndef VW_EMULATE
#define VW_EMULATE 0
#endif
#ifndef VW_GROUP_LANES
#define VW_GROUP_LANES 0
#endif
#ifndef VW_GROUP_WAVE
#define VW_GROUP_WAVE 0
#endif
#if VW_GROUP_LANES || VW_GROUP_WAVE
static uint g_vw_lane;
#endif
#if VW_GROUP_WAVE
// A buffer is used again two operations of its kind later; the barrier of the operation between keeps it safe.
groupshared uint g_vw_x[2][VW_WAVE];   // values: one buffer per read, alternating
groupshared uint g_vw_m[3][2];         // masks: one in use, the next being cleared, the last still being read
static uint g_vw_reads, g_vw_ballots;  // operations so far (equal on every thread)
void vw_begin(uint gi) {
    g_vw_lane = gi;
    g_vw_reads = 0;
    g_vw_ballots = 0;
    if (gi == 0) { [unroll] for (uint i = 0; i < 3; i++) { g_vw_m[i][0] = 0; g_vw_m[i][1] = 0; } }
    GroupMemoryBarrierWithGroupSync();
}
uint vw_group_read(uint x, uint lane) {
    uint b = g_vw_reads & 1u;
    g_vw_reads++;
    g_vw_x[b][g_vw_lane] = x;
    GroupMemoryBarrierWithGroupSync();
    lane &= VW_WAVE - 1u;
    return lane < VW_GROUP_SIZE ? g_vw_x[b][lane] : 0u;
}
uint2 vw_group_ballot(bool c) {
    uint b = g_vw_ballots % 3u;
    g_vw_ballots++;
    if (g_vw_lane == 0) { g_vw_m[(b + 1u) % 3u][0] = 0; g_vw_m[(b + 1u) % 3u][1] = 0; }
    if (c) InterlockedOr(g_vw_m[b][g_vw_lane >> 5], 1u << (g_vw_lane & 31u));
    GroupMemoryBarrierWithGroupSync();
    return uint2(g_vw_m[b][0], g_vw_m[b][1]);
}
#endif

uint vw_lane() {
#if VW_GROUP_LANES || VW_GROUP_WAVE
    return g_vw_lane;
#elif VW_EMULATE
    return WaveGetLaneIndex() & (VW_WAVE - 1u);
#else
    return WaveGetLaneIndex();
#endif
}
// hardware lane of program lane 0 (wraps below 0 when the hardware wave is narrower and starts later in the program wave)
uint vw_origin() { return WaveGetLaneIndex() - vw_lane(); }
// lanes a program-lane loop visits
uint vw_lanes() {
#if VW_EMULATE || VW_GROUP_WAVE
    return VW_WAVE;
#else
    return WaveGetLaneCount();
#endif
}
uint vw_read(uint x, uint lane) {   // x of program lane `lane` (AMD uses the low log2(wave) bits)
#if VW_GROUP_WAVE
    return vw_group_read(x, lane);
#elif VW_EMULATE
    lane &= VW_WAVE - 1u;
    if (!VW_GROUP_LANES && WaveGetLaneCount() == VW_WAVE) return WaveReadLaneAt(x, lane);
    uint hw = vw_origin() + lane, count = WaveGetLaneCount();
    uint v = WaveReadLaneAt(x, min(hw, count - 1u));
    return hw < count ? v : 0u;
#else
    return WaveReadLaneAt(x, lane);
#endif
}
uint vw_read_fill(uint x, uint lane, uint fill) {   // vw_read where a missing lane holds fill (a lane without a pixel)
#if VW_EMULATE && !VW_GROUP_LANES
    lane &= VW_WAVE - 1u;
    uint hw = vw_origin() + lane, count = WaveGetLaneCount();
    uint v = WaveReadLaneAt(x, min(hw, count - 1u));
    return hw < count ? v : fill;
#else
    return vw_read(x, lane);
#endif
}
uint2 vw_ballot(bool c) {   // lane mask in program lanes (y = lanes 32..63)
#if VW_GROUP_WAVE
    return vw_group_ballot(c);
#endif
    uint4 b = WaveActiveBallot(c);
#if VW_EMULATE
    if (!VW_GROUP_LANES && WaveGetLaneCount() <= VW_WAVE) return b.xy;
    uint o = vw_origin();
    uint2 m;
    if (o < WaveGetLaneCount()) {   // wider: the program wave is the segment from hardware lane o
        uint w = o >> 5, s = o & 31u;
        uint3 t = uint3(b[w], b[min(w + 1u, 3u)], b[min(w + 2u, 3u)]);
        m = s == 0u ? t.xy : uint2((t.x >> s) | (t.y << (32u - s)), (t.y >> s) | (t.z << (32u - s)));
    } else {   // narrower: the hardware lanes (all in b.x) are program lanes s ..
        uint s = 0u - o;
        m = s >= 32u ? uint2(0u, b.x << (s - 32u)) : uint2(b.x << s, b.x >> (32u - s));
    }
    if (VW_WAVE < 32) return uint2(m.x & ((1u << (VW_WAVE & 31u)) - 1u), 0u);   // test-sized program waves
    return VW_WAVE == 32 ? uint2(m.x, 0u) : m;
#else
    return b.xy;
#endif
}
bool vw_any(bool c) {
#if VW_GROUP_WAVE
    uint2 g = vw_group_ballot(c); return (g.x | g.y) != 0;
#elif VW_EMULATE
    if (WaveGetLaneCount() > VW_WAVE) { uint2 m = vw_ballot(c); return (m.x | m.y) != 0; }
#endif
    return WaveActiveAnyTrue(c);
}
uint vw_count(bool c) {
#if VW_GROUP_WAVE
    uint2 g = vw_group_ballot(c); return countbits(g.x) + countbits(g.y);
#elif VW_EMULATE
    if (WaveGetLaneCount() > VW_WAVE) { uint2 m = vw_ballot(c); return countbits(m.x) + countbits(m.y); }
#endif
    return WaveActiveCountBits(c);
}

// ---- cross-lane
uint readfirstlane(uint x, bool active) {   // first lane with EXEC set (not just the first HLSL-active lane); lane 0 if none
    uint2 m = vw_ballot(active);
    uint l = m.x != 0 ? firstbitlow(m.x) : m.y != 0 ? 32 + firstbitlow(m.y) : 0u;
    return vw_read(x, l);
}
bool lane_bit(uint lo, uint hi) {   // this lane's bit of an integer lane mask (wave64 layout)
    uint l = vw_lane();
    return ((l < 32 ? (lo >> l) : (hi >> (l - 32))) & 1u) != 0;
}
uint quad_perm(uint x, uint p0, uint p1, uint p2, uint p3) {
    uint q0 = QuadReadLaneAt(x, 0), q1 = QuadReadLaneAt(x, 1), q2 = QuadReadLaneAt(x, 2), q3 = QuadReadLaneAt(x, 3);
    uint l = WaveGetLaneIndex() & 3;
    uint sel = l == 0 ? p0 : l == 1 ? p1 : l == 2 ? p2 : p3;
    return sel == 0 ? q0 : sel == 1 ? q1 : sel == 2 ? q2 : q3;
}

// ---- v_cmp_class_f32: bit0 sNaN, 1 qNaN, 2 -inf, 3 -normal, 4 -denorm, 5 -0, 6 +0, 7 +denorm, 8 +normal, 9 +inf
bool cmp_class(float x, uint m) {
    uint u = asuint(x); bool neg = (u >> 31) != 0; uint e = (u >> 23) & 0xff, f = u & 0x7fffff;
    uint bit;
    if (e == 0xff) bit = f != 0 ? ((f & 0x400000) ? 1 : 0) : (neg ? 2 : 9);
    else if (e == 0) bit = f == 0 ? (neg ? 5 : 6) : (neg ? 4 : 7);
    else bit = neg ? 3 : 8;
    return ((m >> bit) & 1) != 0;
}

// ---- formatted buffer loads. code = layout*8 + numeric type (GCN dfmt/nfmt numbering), from the gfx10 unified format
// in V# dword3 bits 12-18 (buffer_load_format, buf_code) or from the instruction (tbuffer_load_format).
// Branch-free (select only, no switch or loop): FmtLoad is inlined at every call, and a program can hold thousands,
// each of which would otherwise add dozens of blocks and an [unroll] loop that dxc unrolls against the whole function.
// Fields per layout, components x..w: bit offset | width << 8 (width 0 = no component). AMD format names list fields
// from the MSB: 2_10_10_10 = R10G10B10A2 (R in the low bits), 10_11_11 = R11G11B10.
static const uint4 FMT_FIELDS[16] = {
    uint4(0, 0, 0, 0),
    uint4(0x800, 0, 0, 0),                      // 1: 8
    uint4(0x1000, 0, 0, 0),                     // 2: 16
    uint4(0x800, 0x808, 0, 0),                  // 3: 8_8
    uint4(0x2000, 0, 0, 0),                     // 4: 32
    uint4(0x1000, 0x1010, 0, 0),                // 5: 16_16
    uint4(0xb00, 0xb0b, 0xa16, 0),              // 6: 10_11_11
    uint4(0xa00, 0xb0a, 0xb15, 0),              // 7: 11_11_10
    uint4(0x200, 0xa02, 0xa0c, 0xa16),          // 8: 10_10_10_2
    uint4(0xa00, 0xa0a, 0xa14, 0x21e),          // 9: 2_10_10_10
    uint4(0x800, 0x808, 0x810, 0x818),          // 10: 8_8_8_8
    uint4(0x2000, 0x2020, 0, 0),                // 11: 32_32
    uint4(0x1000, 0x1010, 0x1020, 0x1030),      // 12: 16_16_16_16
    uint4(0x2000, 0x2020, 0x2040, 0),           // 13: 32_32_32
    uint4(0x2000, 0x2020, 0x2040, 0x2060),      // 14: 32_32_32_32
    uint4(0, 0, 0, 0),
};
uint fmt_field(uint4 w, uint field) {
    uint off = field & 0xffu, bits = field >> 8, d = off >> 5;
    uint word = select(d == 0u, w.x, select(d == 1u, w.y, select(d == 2u, w.z, w.w)));
    return (word >> (off & 31u)) & select(bits >= 32u, 0xffffffffu, (1u << (bits & 31u)) - 1u);
}
uint fmt_convert(uint v, uint bits, uint num) {
    uint mx = select(bits >= 32u, 0xffffffffu, (1u << (bits & 31u)) - 1u), sh = (32u - bits) & 31u;
    int sv = select(bits >= 32u, asint(v), asint(v << sh) >> sh);
    float un = (float)v / (float)mx;                                         // UNORM
    float sn = max((float)sv / (float)(mx >> 1), -1.0);                      // SNORM
    // FLOAT: 11- and 10-bit fields are unsigned halves with 6 and 5 mantissa bits
    float fp = select(bits == 32u, asfloat(v), f16tof32(v << select(bits == 11u, 4u, select(bits == 10u, 5u, 0u))));
    float f = select(num == 0u, un, select(num == 1u, sn, select(num == 2u, (float)v,   // USCALED
              select(num == 3u, (float)sv, select(num == 7u, fp, 0.0)))));                 // SSCALED
    return select(num == 4u, v, select(num == 5u, asuint(sv), asuint(f)));                 // UINT, SINT
}
uint fmt_component(uint4 w, uint field, uint num, uint missing) {
    return select((field >> 8) != 0u, fmt_convert(fmt_field(w, field), field >> 8, num), missing);
}
uint4 FmtLoad(uint heap_index, uint addr, uint code) {
    ByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    uint base = addr & ~3u, sh = (addr & 3u) * 8u;
    uint4 w = b.Load4(base); uint w4 = b.Load(base + 16);
    uint4 shifted = (w >> sh) | (uint4(w.yzw, w4) << ((32u - sh) & 31u));
    w = select(sh != 0u, shifted, w);
    uint layout = code >> 3, num = code & 7u;
    uint4 fields = select(layout < 16u, FMT_FIELDS[layout & 15u], uint4(0, 0, 0, 0));
    uint one = select(num - 4u <= 1u, 1u, asuint(1.0));
    return uint4(fmt_component(w, fields.x, num, 0u), fmt_component(w, fields.y, num, 0u),
                 fmt_component(w, fields.z, num, 0u), fmt_component(w, fields.w, num, one));
}
// gfx10 unified buffer formats (RDNA 2 ISA, buffer data formats: 1 8_UNORM .. 77 32_32_32_32_FLOAT) as codes; 0 invalid.
static const uint FMT_UNIFIED[80] = {
    0, 8, 9, 10, 11, 12, 13, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 36, 37, 39, 40, 41, 42, 43, 44, 45, 47,
    48, 49, 50, 51, 52, 53, 55, 56, 57, 58, 59, 60, 61, 63, 64, 65, 66, 67, 68, 69, 72, 73, 74, 75, 76, 77, 80, 81, 82, 83,
    84, 85, 92, 93, 95, 96, 97, 98, 99, 100, 101, 103, 108, 109, 111, 116, 117, 119, 0, 0,
};
uint buf_code(uint d3) { uint f = (d3 >> 12) & 0x7fu; return select(f < 80u, FMT_UNIFIED[min(f, 79u)], 0u); }
uint fmt_select(uint4 v, uint sel, uint one) {
    uint c = select((sel & 3u) == 0u, v.x, select((sel & 3u) == 1u, v.y, select((sel & 3u) == 2u, v.z, v.w)));
    return select(sel >= 4u, c, select(sel == 1u, one, 0u));
}
// DST_SEL_X..W (V# dword3 bits 0-11): 0 and 1 the constants, 4-7 the loaded x..w (BGRA views read z, y, x, w). Loads
// only; all four 0 keeps the loaded order.
uint4 fmt_swizzle(uint4 v, uint d3, uint code) {
    uint s = d3 & 0xfffu, one = select((code & 7u) - 4u <= 1u, 1u, asuint(1.0));
    uint4 r = uint4(fmt_select(v, s & 7u, one), fmt_select(v, (s >> 3) & 7u, one),
                    fmt_select(v, (s >> 6) & 7u, one), fmt_select(v, (s >> 9) & 7u, one));
    return s == 0u ? v : r;
}

// ---- cube maps (AMD conventions). face: 0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z
float cubeid(float x, float y, float z) {
    if (abs(z) >= abs(x) && abs(z) >= abs(y)) return z < 0 ? 5.0 : 4.0;
    if (abs(y) >= abs(x)) return y < 0 ? 3.0 : 2.0;
    return x < 0 ? 1.0 : 0.0;
}
float cubesc(float x, float y, float z) {
    if (abs(z) >= abs(x) && abs(z) >= abs(y)) return z < 0 ? -x : x;
    if (abs(y) >= abs(x)) return x;
    return x < 0 ? z : -z;
}
float cubetc(float x, float y, float z) {
    if (abs(z) >= abs(x) && abs(z) >= abs(y)) return -y;
    if (abs(y) >= abs(x)) return y < 0 ? -z : z;
    return -y;
}
float cubema(float x, float y, float z) {
    if (abs(z) >= abs(x) && abs(z) >= abs(y)) return 2.0 * z;
    if (abs(y) >= abs(x)) return 2.0 * y;
    return 2.0 * x;
}
// inverse of the hardware cube addressing: s,t in [1,2] (sc/|ma| + 1.5), face id -> direction
float3 cube_dir(float s, float t, float face) {
    float u = (s - 1.5) * 2.0, v = (t - 1.5) * 2.0;
    uint f = (uint)face % 6;
    switch (f) {
        case 0: return float3(1, -v, -u);
        case 1: return float3(-1, -v, u);
        case 2: return float3(u, 1, v);
        case 3: return float3(u, -1, -v);
        case 4: return float3(u, -v, 1);
        default: return float3(-u, -v, -1);
    }
}

uint enc_num(float f, uint bits, uint num) {
    uint mx = bits >= 32 ? 0xffffffffu : ((1u << bits) - 1);
    switch (num) {
        case 0: return (uint)round(saturate(f) * (float)mx);
        case 1: return asuint((int)round(clamp(f, -1.0, 1.0) * (float)(mx >> 1))) & mx;
        case 2: return (uint)f & mx;
        case 3: return asuint((int)f) & mx;
        case 7: return bits == 32 ? asuint(f) : bits == 16 ? f32tof16(f) : ((uint)f & mx);
    }
    return 0;
}
void FmtStore(uint heap_index, uint addr, uint code, uint4 v, uint n) {
    RWByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    uint layout = code >> 3, num = code & 7;
    bool ints = num == 4 || num == 5;
    uint4 c = v;
    uint a = addr & ~3u;
    switch (layout) {
        case 4:  b.Store(a, ints ? c.x : enc_num(asfloat(v.x), 32, num)); break;
        case 5:  b.Store(a, (ints ? (c.x & 0xffff) | (c.y << 16) : enc_num(asfloat(v.x), 16, num) | (enc_num(asfloat(v.y), 16, num) << 16))); break;
        case 10: b.Store(a, ints ? ((c.x & 0xff) | ((c.y & 0xff) << 8) | ((c.z & 0xff) << 16) | (c.w << 24))
                                 : (enc_num(asfloat(v.x), 8, num) | (enc_num(asfloat(v.y), 8, num) << 8) | (enc_num(asfloat(v.z), 8, num) << 16) | (enc_num(asfloat(v.w), 8, num) << 24))); break;
        case 9:  b.Store(a, enc_num(asfloat(v.x), 10, num) | (enc_num(asfloat(v.y), 10, num) << 10) | (enc_num(asfloat(v.z), 10, num) << 20) | (enc_num(asfloat(v.w), 2, num) << 30)); break;
        case 11: b.Store2(a, ints ? c.xy : uint2(enc_num(asfloat(v.x), 32, num), enc_num(asfloat(v.y), 32, num))); break;
        case 12: b.Store2(a, ints ? uint2((c.x & 0xffff) | (c.y << 16), (c.z & 0xffff) | (c.w << 16))
                                  : uint2(enc_num(asfloat(v.x), 16, num) | (enc_num(asfloat(v.y), 16, num) << 16), enc_num(asfloat(v.z), 16, num) | (enc_num(asfloat(v.w), 16, num) << 16))); break;
        case 13: b.Store3(a, ints ? c.xyz : uint3(enc_num(asfloat(v.x), 32, num), enc_num(asfloat(v.y), 32, num), enc_num(asfloat(v.z), 32, num))); break;
        case 14: b.Store4(a, ints ? c : uint4(enc_num(asfloat(v.x), 32, num), enc_num(asfloat(v.y), 32, num), enc_num(asfloat(v.z), 32, num), enc_num(asfloat(v.w), 32, num))); break;
        default: break;   // 8-bit / 16-bit single-channel and packed float formats: TODO (need byte-granular RMW)
    }
}

float mul_legacy(float a, float b) { return (a == 0.0 || b == 0.0) ? 0.0 : a * b; }
float mad_legacy(float a, float b, float c) { return mul_legacy(a, b) + c; }
float med3(float a, float b, float c) { return max(min(a, b), min(max(a, b), c)); }
uint med3u(uint a, uint b, uint c) { return max(min(a, b), min(max(a, b), c)); }
int med3i(int a, int b, int c) { return max(min(a, b), min(max(a, b), c)); }
float round_ne(float x) { return round(x); }
uint bfe_u(uint v, uint off, uint w) { off &= 31; w &= 31; return w == 0 ? 0 : (v >> off) & ((1u << w) - 1); }
uint bfe_i(uint v, uint off, uint w) { off &= 31; w &= 31; if (w == 0) return 0; if (off + w >= 32) return asuint(asint(v) >> off); int s = asint(v << (32 - off - w)); return asuint(s >> (32 - w)); }

// exact f32 -> f16 conversions (f32tof16 rounding is implementation-defined; AMD default = round to nearest even)
uint f32tof16_rne(float f) {
    uint u = asuint(f); uint s = (u >> 16) & 0x8000u; uint a = u & 0x7fffffffu;
    if (a > 0x7f800000u) return s | 0x7e00u;
    if (a >= 0x477ff000u) return s | 0x7c00u;
    if (a < 0x38800000u) {
        if (a < 0x33000000u) return s;
        uint e = a >> 23; uint m = (a & 0x7fffffu) | 0x800000u; uint sh = 126u - e;
        uint q = m >> sh; uint rem = m & ((1u << sh) - 1u); uint hf = 1u << (sh - 1u);
        return s | (q + ((rem > hf || (rem == hf && (q & 1u))) ? 1u : 0u));
    }
    uint r = a - 0x38000000u; uint q = r >> 13; uint rem = r & 0x1fffu;
    return s | (q + ((rem > 0x1000u || (rem == 0x1000u && (q & 1u))) ? 1u : 0u));
}
uint f32tof16_rtz(float f) {
    uint u = asuint(f); uint s = (u >> 16) & 0x8000u; uint a = u & 0x7fffffffu;
    if (a > 0x7f800000u) return s | 0x7e00u;
    if (a == 0x7f800000u) return s | 0x7c00u;
    if (a >= 0x47800000u) return s | 0x7bffu;
    if (a < 0x38800000u) { if (a < 0x33800000u) return s; uint e = a >> 23; uint m = (a & 0x7fffffu) | 0x800000u; return s | (m >> (126u - e)); }
    return s | ((a - 0x38000000u) >> 13);
}
float ldexp_exact(float x, int e) { e = clamp(e, -378, 381); int a = e / 3; int b = (e - a) / 2; int c = e - a - b;
    precise float r = x * asfloat((uint)(a + 127) << 23); r = r * asfloat((uint)(b + 127) << 23); r = r * asfloat((uint)(c + 127) << 23); return r; }
// fp32 fused multiply-add via fp64 (HLSL has no fp32 fma; exact for fp32 inputs)
float fma32(float a, float b, float c) { return (float)((double)a * (double)b + (double)c); }
int sext24(uint v) { return asint(v << 8) >> 8; }
uint mul_hi_u(uint a, uint b) { uint64_t p = (uint64_t)a * (uint64_t)b; return (uint)(p >> 32); }
uint firstbithigh_amd(uint v) { return v == 0 ? 0xffffffffu : 31 - firstbithigh(v); }
// s_flbit_i32 / v_ffbh_i32: count of leading bits equal to the sign bit (from MSB); -1 if all bits are equal
uint flbit_signed(uint v) { uint x = (v >> 31) != 0 ? ~v : v; return x == 0 ? 0xffffffffu : 31 - firstbithigh(x); }
uint alignbit(uint hi, uint lo, uint s) { uint64_t x = ((uint64_t)hi << 32) | lo; return (uint)(x >> (s & 31)); }
uint perm_b32(uint a, uint b, uint sel) {
    uint64_t x = ((uint64_t)a << 32) | b; uint r = 0;
    for (uint i = 0; i < 4; i++) {
        uint s = (sel >> (8 * i)) & 0xff; uint byte;
        if (s < 8) byte = (uint)(x >> (8 * s)) & 0xff;
        else if (s == 12) byte = 0; else if (s >= 13) byte = 0xff;
        else byte = ((uint)(x >> (8 * ((s - 8) * 2 + 1))) & 0x80) ? 0xff : 0;
        r |= byte << (8 * i);
    }
    return r;
}
