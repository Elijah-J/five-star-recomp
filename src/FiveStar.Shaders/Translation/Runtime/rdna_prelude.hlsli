// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later
// Runtime support for translated RDNA programs (prototype).
// Bindless model: descriptor dword0 written by the D3D12.X shim = PC descriptor-heap index.

cbuffer UserData : register(b0) { uint4 g_user4[32]; };       // initial user SGPRs (root constants)
static const uint GPU_MEM_HEAP = 0;                            // raw GPU-memory window (prototype)
static const uint STATIC_SAMPLER_BASE = 2032;                  // runtime-created samplers for literal S#s
static const uint PC_END = 0xffffffffu;
static const uint PC_YIELD = 0xfffffffeu;                      // mesh shaders: s_barrier yield point
static const uint CODE_TAG = 0xc0de0000u;                       // pointer hi-word for PC-relative (embedded) data

uint user_sgpr(uint i) { return g_user4[i >> 2][i & 3]; }

uint MemLoad(uint ptr_lo, uint off) {
    ByteAddressBuffer m = ResourceDescriptorHeap[GPU_MEM_HEAP];
    return m.Load((ptr_lo + off) & ~3u);
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

// ---- cross-lane
uint readfirstlane(uint x, bool active) {   // first lane with EXEC set (not just the first HLSL-active lane)
    uint4 b = WaveActiveBallot(active);
    uint l = b.x != 0 ? firstbitlow(b.x) : b.y != 0 ? 32 + firstbitlow(b.y) : b.z != 0 ? 64 + firstbitlow(b.z) : 96 + firstbitlow(b.w);
    return WaveReadLaneAt(x, l);
}
bool lane_bit(uint lo, uint hi) {   // this lane's bit of an integer lane mask (wave64 layout)
    uint l = WaveGetLaneIndex();
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

// ---- formatted buffer loads. code = layout*8 + numeric type (GCN dfmt/nfmt numbering), written into
// V# dword3 by the D3D12.X shim (buffer_load_format) or taken from the instruction (tbuffer_load_format).
float cvt_num(uint v, uint bits, uint num) {
    uint mx = bits >= 32 ? 0xffffffffu : ((1u << bits) - 1);
    int sv = bits >= 32 ? asint(v) : (asint(v << (32 - bits)) >> (32 - bits));
    switch (num) {
        case 0: return (float)v / (float)mx;                                  // UNORM
        case 1: return max((float)sv / (float)(mx >> 1), -1.0);               // SNORM
        case 2: return (float)v;                                              // USCALED
        case 3: return (float)sv;                                             // SSCALED
        case 7: return bits == 32 ? asfloat(v) : bits == 16 ? f16tof32(v) : (float)v; // FLOAT (11/10-bit handled by caller)
    }
    return 0;
}
uint4 FmtLoad(uint heap_index, uint addr, uint code) {
    ByteAddressBuffer b = ResourceDescriptorHeap[heap_index];
    uint base = addr & ~3u, sh = (addr & 3u) * 8;
    uint4 w = b.Load4(base); uint w4 = b.Load(base + 16);
    if (sh != 0) w = uint4((w.x >> sh) | (w.y << (32 - sh)), (w.y >> sh) | (w.z << (32 - sh)), (w.z >> sh) | (w.w << (32 - sh)), (w.w >> sh) | (w4 << (32 - sh)));
    uint layout = code >> 3, num = code & 7;
    uint4 raw = uint4(0, 0, 0, 0); uint4 bits = uint4(0, 0, 0, 0); uint n = 0;
    switch (layout) {
        case 1:  raw.x = w.x & 0xff; bits = uint4(8, 0, 0, 0); n = 1; break;
        case 2:  raw.x = w.x & 0xffff; bits = uint4(16, 0, 0, 0); n = 1; break;
        case 3:  raw.xy = uint2(w.x & 0xff, (w.x >> 8) & 0xff); bits = uint4(8, 8, 0, 0); n = 2; break;
        case 4:  raw.x = w.x; bits = uint4(32, 0, 0, 0); n = 1; break;
        case 5:  raw.xy = uint2(w.x & 0xffff, w.x >> 16); bits = uint4(16, 16, 0, 0); n = 2; break;
        // AMD format names list fields from the MSB: 2_10_10_10 = R10G10B10A2 (R in the low bits)
        case 9:  raw = uint4(w.x & 0x3ff, (w.x >> 10) & 0x3ff, (w.x >> 20) & 0x3ff, w.x >> 30); bits = uint4(10, 10, 10, 2); n = 4; break;
        case 8:  raw = uint4(w.x & 0x3, (w.x >> 2) & 0x3ff, (w.x >> 12) & 0x3ff, w.x >> 22); bits = uint4(2, 10, 10, 10); n = 4; break;
        case 10: raw = uint4(w.x & 0xff, (w.x >> 8) & 0xff, (w.x >> 16) & 0xff, w.x >> 24); bits = uint4(8, 8, 8, 8); n = 4; break;
        case 11: raw.xy = w.xy; bits = uint4(32, 32, 0, 0); n = 2; break;
        case 12: raw = uint4(w.x & 0xffff, w.x >> 16, w.y & 0xffff, w.y >> 16); bits = uint4(16, 16, 16, 16); n = 4; break;
        case 13: raw.xyz = w.xyz; bits = uint4(32, 32, 32, 0); n = 3; break;
        case 14: raw = w; bits = uint4(32, 32, 32, 32); n = 4; break;
        default: break;   // 10_11_11 / 11_11_10: TODO
    }
    uint4 r = uint4(0, 0, 0, (num == 4 || num == 5) ? 1u : asuint(1.0));
    [unroll] for (uint i = 0; i < 4; i++) {
        if (i < n) {
            if (num == 4) r[i] = raw[i];
            else if (num == 5) r[i] = bits[i] >= 32 ? raw[i] : asuint(asint(raw[i] << (32 - bits[i])) >> (32 - bits[i]));
            else r[i] = asuint(cvt_num(raw[i], bits[i], num));
        }
    }
    return r;
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

void MemStore(uint ptr_lo, uint off, uint v) {
    RWByteAddressBuffer m = ResourceDescriptorHeap[GPU_MEM_HEAP + 1];   // writable view of the GPU-memory window
    m.Store((ptr_lo + off) & ~3u, v);
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
    uint4 c = ints ? v : uint4(0, 0, 0, 0);
    if (!ints) [unroll] for (uint i = 0; i < 4; i++) c[i] = asuint(asfloat(v[i]));   // keep bits, encode below
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
