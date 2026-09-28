// pixel drawing kit: hard pixels only, palette colours, ordered dithering for fades
const png = require("./png");

const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];

// the game's palette (from the user's sprites) plus a few in-between violets for electro
const P = {
  K: hex("#000000"), W: hex("#ffffff"),
  R0: hex("#3e0812"), R1: hex("#7e1426"), R2: hex("#d02838"), R3: hex("#ff6a5a"),
  G0: hex("#6e3e14"), G1: hex("#c88830"), G2: hex("#f8d068"), G3: hex("#fff0b0"),
  V0: hex("#1e0c3a"), V1: hex("#3a1a6e"), V2: hex("#5e2a9e"), V3: hex("#8446cc"),
  V4: hex("#a45ce8"), V5: hex("#c98cf6"), V6: hex("#ebc4ff"),
};

const BAYER = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
const bayer = (x, y) => (BAYER[(y & 3) * 4 + (x & 3)] + 0.5) / 16;

function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const image = (w, h) => png.make(w, h);

// an image may carry an origin (ox, oy): drawn with a margin round it, so a glow or an outline
// can spill past where its drawing began without being cut off. every other image has none
function put(im, x, y, c) {
  x = Math.round(x) + (im.ox | 0); y = Math.round(y) + (im.oy | 0);
  if (x < 0 || y < 0 || x >= im.w || y >= im.h || !c) return;
  const i = (y * im.w + x) * 4;
  im.data[i] = c[0]; im.data[i + 1] = c[1]; im.data[i + 2] = c[2]; im.data[i + 3] = c[3] === undefined ? 255 : c[3];
}
function get(im, x, y) {
  x = Math.round(x) + (im.ox | 0); y = Math.round(y) + (im.oy | 0);
  if (x < 0 || y < 0 || x >= im.w || y >= im.h) return null;
  const i = (y * im.w + x) * 4;
  return im.data[i + 3] ? [im.data[i], im.data[i + 1], im.data[i + 2], im.data[i + 3]] : null;
}
const alpha = (im, x, y) => get(im, x, y) ? get(im, x, y)[3] : 0;

// draws b over a (both same size), keeping a where b is empty
function over(a, b) {
  for (let i = 0; i < a.w * a.h; i++) if (b.data[i * 4 + 3]) for (let c = 0; c < 4; c++) a.data[i * 4 + c] = b.data[i * 4 + c];
  return a;
}
function blit(dst, src, ox, oy) {
  const sx = src.ox | 0, sy = src.oy | 0;
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
    const i = (y * src.w + x) * 4;
    if (src.data[i + 3]) put(dst, ox + x - sx, oy + y - sy, [src.data[i], src.data[i + 1], src.data[i + 2], src.data[i + 3]]);
  }
  return dst;
}

// ---- masks (Uint8Array w*h) ----
// a mask may carry an origin (ox, oy) as an image may; mset and mget take drawing coordinates,
// everything else here works on its raw cells
const mask = (w, h, ox = 0, oy = 0) => ({ w, h, m: new Uint8Array(w * h), ox, oy });
function mset(M, x, y) { x = Math.round(x) + (M.ox | 0); y = Math.round(y) + (M.oy | 0); if (x >= 0 && y >= 0 && x < M.w && y < M.h) M.m[y * M.w + x] = 1; }
const mget = (M, x, y) => { x = Math.round(x) + (M.ox | 0); y = Math.round(y) + (M.oy | 0); return x >= 0 && y >= 0 && x < M.w && y < M.h ? M.m[y * M.w + x] : 0; };
const mraw = (M, x, y) => x >= 0 && y >= 0 && x < M.w && y < M.h ? M.m[y * M.w + x] : 0;
function dilate(M, r = 1, diag = true) {
  let cur = M;
  for (let k = 0; k < r; k++) {
    const n = mask(M.w, M.h, M.ox, M.oy);
    for (let y = 0; y < M.h; y++) for (let x = 0; x < M.w; x++) {
      if (mraw(cur, x, y) || mraw(cur, x - 1, y) || mraw(cur, x + 1, y) || mraw(cur, x, y - 1) || mraw(cur, x, y + 1) ||
        (diag && (mraw(cur, x - 1, y - 1) || mraw(cur, x + 1, y - 1) || mraw(cur, x - 1, y + 1) || mraw(cur, x + 1, y + 1))))
        n.m[y * M.w + x] = 1;
    }
    cur = n;
  }
  return cur;
}
function minus(A, B) { const n = mask(A.w, A.h, A.ox, A.oy); for (let i = 0; i < A.m.length; i++) n.m[i] = A.m[i] && !B.m[i] ? 1 : 0; return n; }
function paint(im, M, c, density = 1, phase = 0) {
  for (let y = 0; y < M.h; y++) for (let x = 0; x < M.w; x++)
    if (M.m[y * M.w + x] && (density >= 1 || bayer(x + phase, y) < density)) put(im, x - (M.ox | 0), y - (M.oy | 0), c);
}
function line(M, x0, y0, x1, y1) {
  x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1);
  const dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
  let err = dx + dy;
  for (;;) {
    mset(M, x0, y0);
    if (x0 === x1 && y0 === y1) break;
    const e2 = 2 * err;
    if (e2 >= dy) { err += dy; x0 += sx; }
    if (e2 <= dx) { err += dx; y0 += sy; }
  }
  return M;
}
function polyline(M, pts) { for (let i = 1; i < pts.length; i++) line(M, pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1]); return M; }

// jagged lightning between two points by midpoint displacement
function bolt(r, x0, y0, x1, y1, disp, levels) {
  let pts = [[x0, y0], [x1, y1]];
  for (let l = 0; l < levels; l++) {
    const next = [pts[0]];
    for (let i = 1; i < pts.length; i++) {
      const [ax, ay] = pts[i - 1], [bx, by] = pts[i];
      const len = Math.hypot(bx - ax, by - ay) || 1, nx = -(by - ay) / len, ny = (bx - ax) / len;
      const o = (r() * 2 - 1) * disp;
      next.push([(ax + bx) / 2 + nx * o, (ay + by) / 2 + ny * o], pts[i]);
    }
    pts = next;
    disp *= 0.55;
  }
  return pts;
}

// ---- fields (Float32Array) posterised onto a palette ramp ----
const field = (w, h) => ({ w, h, f: new Float32Array(w * h) });
function fmax(F, x, y, v) { if (x >= 0 && y >= 0 && x < F.w && y < F.h) { const i = y * F.w + x; if (v > F.f[i]) F.f[i] = v; } }
function each(F, fn) { for (let y = 0; y < F.h; y++) for (let x = 0; x < F.w; x++) { const v = fn(x + 0.5, y + 0.5); if (v > 0) fmax(F, x, y, v); } }

// ramp: [[threshold, colour], ...] ascending. values between two steps dither between their
// colours; below the first step the colour dithers out to nothing
function shade(im, F, ramp, opt = {}) {
  const fadeBand = opt.fadeBand === undefined ? 0.5 : opt.fadeBand, bands = opt.bands !== false;
  for (let y = 0; y < F.h; y++) for (let x = 0; x < F.w; x++) {
    const v = F.f[y * F.w + x];
    if (v <= 0) continue;
    const th = bayer(x, y);
    if (v < ramp[0][0]) {
      const lo = ramp[0][0] * (1 - fadeBand);
      if (v > lo && (v - lo) / (ramp[0][0] - lo) > th) put(im, x, y, ramp[0][1]);
      continue;
    }
    let k = 0;
    while (k + 1 < ramp.length && v >= ramp[k + 1][0]) k++;
    let c = ramp[k][1];
    if (bands && k + 1 < ramp.length) {
      const t = (v - ramp[k][0]) / (ramp[k + 1][0] - ramp[k][0]);
      if (t > 0.72 && (t - 0.72) / 0.28 > th) c = ramp[k + 1][1];
    }
    put(im, x, y, c);
  }
  return im;
}

// the electro ramp: deep violet up to white hot
const ELECTRO = [[0.1, P.V1], [0.18, P.V2], [0.3, P.V3], [0.44, P.V4], [0.6, P.V5], [0.76, P.V6], [0.9, P.W]];
const SMOKE = [[0.05, P.V0], [0.09, P.V1], [0.13, P.V2]];

// a tapered ray: full width at the root, a point at the tip
function ray(F, cx, cy, ang, len, width, v0 = 1, v1 = 0.45) {
  const dx = Math.cos(ang), dy = Math.sin(ang);
  const r = Math.ceil(len + width);
  for (let y = Math.floor(cy - r); y <= cy + r; y++) for (let x = Math.floor(cx - r); x <= cx + r; x++) {
    const px = x + 0.5 - cx, py = y + 0.5 - cy, u = px * dx + py * dy, v = Math.abs(-px * dy + py * dx);
    if (u < 0 || u > len) continue;
    const k = u / len, half = Math.max(0.5, (width / 2) * Math.pow(1 - k, 1.1));
    if (v <= half) fmax(F, x, y, v0 + (v1 - v0) * k);
  }
}
// ✦ flare: four long arms plus optional short diagonals
function flare(F, cx, cy, len, width, diagLen = 0, diagWidth = 1, v0 = 1, v1 = 0.45, rot = 0) {
  for (let k = 0; k < 4; k++) ray(F, cx, cy, rot + k * Math.PI / 2, len, width, v0, v1);
  if (diagLen > 0) for (let k = 0; k < 4; k++) ray(F, cx, cy, rot + Math.PI / 4 + k * Math.PI / 2, diagLen, diagWidth, v0 * 0.85, v1 * 0.8);
}

module.exports = { P, hex, bayer, rng, image, put, get, alpha, over, blit, mask, mset, mget, dilate, minus, paint, line, polyline, bolt,
  field, fmax, each, shade, ELECTRO, SMOKE, ray, flare };
