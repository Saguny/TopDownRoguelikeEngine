// the weapons' VFX, drawn frame by frame in the Command Token's style: the same palette and
// ordered dithering, plus fire, azure, peach and ash ramps. every element becomes one layered
// .aseprite file (see make-lua.js). sizes are in world pixels: 28.4615 px per unit, the enemies'
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const { hex, put } = D;

const P = Object.assign({}, D.P, {
  O1: hex("#f0602c"), O2: hex("#ffa040"),
  A0: hex("#0e1830"), A1: hex("#1d3a6b"), A2: hex("#2f6fb0"), A3: hex("#5ac8f0"), A4: hex("#a8ecff"), A5: hex("#e0fbff"),
  K1: hex("#b0305a"), K2: hex("#f07898"), K3: hex("#ffc0d0"),
  S0: hex("#17111d"), S1: hex("#2b2232"), S2: hex("#463a4b"), S3: hex("#6b5b6c"),
});

const FIRE = [[0.08, P.R0], [0.16, P.R1], [0.28, P.R2], [0.42, P.O1], [0.56, P.O2], [0.7, P.G2], [0.84, P.G3], [0.95, P.W]];
const GOLD = [[0.1, P.G0], [0.25, P.G1], [0.45, P.G2], [0.7, P.G3], [0.9, P.W]];
const AZURE = [[0.1, P.A1], [0.2, P.A2], [0.34, P.A3], [0.52, P.A4], [0.72, P.A5], [0.9, P.W]];
const PEACH = [[0.1, P.K1], [0.3, P.K2], [0.6, P.K3], [0.9, P.W]];
const ELECTRO = D.ELECTRO, SMOKE = D.SMOKE;
const DUST = [[0.06, P.V1], [0.1, P.V2], [0.14, P.V3]];

const TAU = Math.PI * 2;
const easeOut = t => 1 - (1 - t) * (1 - t);
const easeInOut = t => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
const lerp = (a, b, t) => a + (b - a) * t;
const img = (w, h) => D.image(w, h);

// ---------------------------------------------------------------- helpers
function hash2(x, y, s) {
  let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(s, 982451653)) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}
function vnoise(x, y, s) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const a = hash2(xi, yi, s), b = hash2(xi + 1, yi, s), c = hash2(xi, yi + 1, s), d = hash2(xi + 1, yi + 1, s);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
function fbm(x, y, s, oct = 3) {
  let t = 0, amp = 0.5, f = 1, norm = 0;
  for (let i = 0; i < oct; i++) { t += amp * vnoise(x * f, y * f, s + i * 17); norm += amp; amp *= 0.5; f *= 2; }
  return t / norm;
}
// noise scrolling by `step` a frame that comes back to where it started after n frames
function loopNoise(x, y, f, n, step, seed) {
  const w = f / n;
  return (1 - w) * fbm(x + f * step, y, seed) + w * fbm(x + (f - n) * step, y, seed);
}

function maskOf(im) {
  const M = D.mask(im.w, im.h);
  for (let i = 0; i < im.w * im.h; i++) if (im.data[i * 4 + 3]) M.m[i] = 1;
  return M;
}
// a solid object gets a dark plum outline so it reads over any floor, like the enemies do
function withOutline(im, col = P.V0, diag = false) {
  const M = maskOf(im), out = img(im.w, im.h);
  D.paint(out, D.minus(D.dilate(M, 1, diag), M), col);
  out.ox = im.ox; out.oy = im.oy;
  return D.over(out, im);
}
// a dithered glow just outside a shape
function halo(im, col, density, phase = 0, r = 1) {
  const M = maskOf(im), out = img(im.w, im.h);
  D.paint(out, D.minus(D.dilate(M, r, false), M), col, density, phase);
  out.ox = im.ox; out.oy = im.oy;
  return out;
}
function ringField(F, cx, cy, R, th, peak, gate) {
  D.each(F, (x, y) => {
    const d = Math.hypot(x - cx, y - cy);
    if (d > R + 0.5 || d <= R - th) return 0;
    let v = peak * (0.55 + 0.45 * Math.pow((d - (R - th)) / th, 1.5));
    if (gate && !gate(Math.atan2(y - cy, x - cx), d)) return 0;
    return v;
  });
}
// breaks a ring into irregular pieces: noise along the arc, so the gaps come in uneven lengths
const breakup = (R, seed, keep) => (a => vnoise((a + Math.PI) * R / 6, seed * 7.3, seed) < keep);
function twinkle(im, x, y, size, cols) {
  x = Math.round(x); y = Math.round(y);
  put(im, x, y, cols[0]);
  if (size >= 1) [[1, 0], [-1, 0], [0, 1], [0, -1]].forEach(([u, v]) => put(im, x + u, y + v, cols[1] || cols[0]));
  if (size >= 2) [[2, 0], [-2, 0], [0, 2], [0, -2]].forEach(([u, v]) => put(im, x + u, y + v, cols[2] || cols[1] || cols[0]));
}
function el(name, group, w, h, layers, durations, tags, extra = {}) {
  return Object.assign({ name, group, w, h, layers, durations, tags }, extra);
}

// ================================================================ BOW
// a spirit arrow: gold broadhead with a travelling glint, red fletching flicking in the wind and a
// streak of qi behind. points right, 32x11
function makeArrow() {
  const W = 32, H = 11, N = 4, arrow = [], trail = [], glow = [];
  for (let f = 0; f < N; f++) {
    const body = img(W, H);
    for (let x = 7; x <= 25; x++) put(body, x, 5, P.G1);
    put(body, 6, 5, P.G2);
    [[27, 3, P.G1], [26, 4, P.G1], [27, 4, P.G2], [28, 4, P.G3], [25, 5, P.G1], [26, 5, P.G2], [27, 5, P.G3], [28, 5, P.G3],
      [29, 5, P.G3], [30, 5, P.W], [26, 6, P.G1], [27, 6, P.G2], [28, 6, P.G2], [27, 7, P.G0]].forEach(([x, y, c]) => put(body, x, y, c));
    const glint = [[28, 4], [29, 5], [28, 5], null][f];
    if (glint) put(body, glint[0], glint[1], P.W);
    const flick = f % 2;
    for (const s of [-1, 1])
      [[9, 1, P.R2], [10, 1, P.R2], [11, 1, P.R3], [12, 1, P.R3], [8, 2, P.R1], [9, 2, P.R2], [10, 2, P.R2], [11, 2, P.R3],
        [7 + flick, 3, P.R1], [8 + flick, 3, P.R1], [9 + flick, 3, P.R2]].forEach(([x, d, c]) => put(body, x, 5 + s * d, c));
    arrow.push(withOutline(body));

    // streaks of qi peeling off behind, dashes running backwards
    const T = img(W, H);
    [[3, 0], [5, 3], [7, 1]].forEach(([y, k]) => {
      for (let x = 0; x <= 6; x++) {
        if (((x + f * 2 + k) % 6) >= 3) continue;
        if (y !== 5 && x > 4) continue;
        if (x < 2 && D.bayer(x, y) > 0.5) continue;
        put(T, x, y, x >= 4 ? P.G3 : x >= 2 ? P.G2 : P.G1);
      }
    });
    trail.push(T);

    const G = img(W, H);
    if (f % 2 === 0) for (let y = 1; y < 10; y++) for (let x = 23; x < 32; x++)
      if (Math.hypot(x - 28, (y - 5) * 1.4) < 4.2 && D.bayer(x + f, y) < 0.4) put(G, x, y, P.G2);
    glow.push(G);
  }
  return el("bow_arrow", "Bow", W, H, [{ name: "trail", frames: trail }, { name: "glow", frames: glow }, { name: "arrow", frames: arrow }],
    Array(N).fill(60), [["fly", 0, N - 1]]);
}

// the loose: the string snapping forward as a bright crescent, speed lines and a puff. faces right
function makeLoose() {
  const S = 24, c = 12, N = 5, arcL = [], lineL = [];
  for (let f = 0; f < N; f++) {
    const A = img(S, S);
    if (f <= 2) {
      const R = [7, 8, 9][f], ox = c + 3 - R + f * 2, span = [1.0, 0.9, 0.75][f];
      for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
        const d = Math.hypot(x + 0.5 - ox, y + 0.5 - c), a = Math.atan2(y + 0.5 - c, x + 0.5 - ox);
        if (Math.abs(a) > span) continue;
        const th = f === 0 ? 2 : 1.3;
        if (d > R + 0.5 || d < R - th) continue;
        if (f === 2 && D.bayer(x, y) > 0.6) continue;
        put(A, x, y, f === 0 ? (d > R - 1 ? P.W : P.G3) : f === 1 ? P.G3 : P.G2);
      }
      if (f === 0) twinkle(A, c + 3, c, 1, [P.W, P.G3]);
      if (f === 1) { twinkle(A, c + 6, c - 6, 0, [P.G3]); twinkle(A, c + 7, c + 5, 0, [P.G3]); }
    }
    arcL.push(A);

    const L = img(S, S);
    const seg = [null, [13, 17], [15, 21], [18, 23], [21, 23]][f];
    if (seg) [-4, 0, 4].forEach((dy, k) => {
      const x0 = seg[0] + (k === 1 ? 1 : 0), x1 = seg[1] + (k === 1 ? 1 : 0);
      for (let x = x0; x <= Math.min(S - 1, x1); x++) {
        const head = x === x1, col = f === 1 ? (head ? P.W : P.G3) : f === 2 ? (head ? P.G3 : P.G2) : f === 3 ? P.G2 : P.G1;
        if (f === 4 && D.bayer(x, c + dy) > 0.5) continue;
        put(L, x, c + dy, col);
      }
    });
    lineL.push(L);
  }
  return el("bow_loose", "Bow", S, S, [{ name: "string", frames: arcL }, { name: "lines", frames: lineL }], Array(N).fill(36), [["loose", 0, N - 1]]);
}

// an arrow striking: a gold flare, splinters and feather bits thrown back, sparks going on. the
// arrow was flying right, into the centre
function makeArrowHit() {
  const S = 32, c = 16, N = 6, r = D.rng(77);
  const splinters = Array.from({ length: 5 }, (_, i) => ({ a: Math.PI + (i - 2) * 0.55 + (r() - 0.5) * 0.3, s: 0.7 + r() * 0.5, len: 2 + (i % 2), spin: r() * TAU }));
  const feathers = Array.from({ length: 3 }, (_, i) => ({ a: Math.PI + (i - 1) * 0.9 + (r() - 0.5) * 0.4, s: 0.5 + r() * 0.4 }));
  const sparks = Array.from({ length: 5 }, (_, i) => ({ a: (i - 2) * 0.5 + (r() - 0.5) * 0.3, s: 0.8 + r() * 0.4 }));
  const flareL = [], bitsL = [], sparkL = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    const fl = [[10, 3, 5, 1, 1], [6, 1, 3, 1, 0.8]][f];
    if (fl) D.flare(F, c, c, fl[0], fl[1], fl[2], fl[3], fl[4], 0.4, f === 1 ? Math.PI / 4 : 0);
    if (f >= 2 && f <= 4) { const R = [8, 10, 11][f - 2]; ringField(F, c, c, R, 1.3, [0.55, 0.4, 0.28][f - 2], breakup(R, f, f === 2 ? 0.7 : 0.45)); }
    flareL.push(D.shade(img(S, S), F, GOLD));

    const B = img(S, S);
    if (f >= 1) {
      const t = f / (N - 1);
      for (const s of splinters) {
        const d = 3 + 11 * s.s * easeOut(t), x = c + Math.cos(s.a) * d, y = c + Math.sin(s.a) * d + t * t * 3;
        const ang = s.spin + f * 0.9;
        D.paint(B, D.line(D.mask(S, S), x, y, x + Math.cos(ang) * s.len, y + Math.sin(ang) * s.len), f >= 4 ? P.G0 : P.G1);
        put(B, x, y, f >= 4 ? P.G0 : P.G2);
      }
      for (const fe of feathers) {
        const d = 2 + 8 * fe.s * easeOut(t), x = Math.round(c + Math.cos(fe.a) * d), y = Math.round(c + Math.sin(fe.a) * d + t * 4);
        put(B, x, y, f >= 4 ? P.R1 : P.R2);
        put(B, x + (f % 2 ? 1 : 0), y + (f % 2 ? 0 : 1), f >= 3 ? P.R1 : P.R3);
      }
    }
    bitsL.push(B);

    const Sp = img(S, S);
    if (f >= 1 && f <= 4) for (const s of sparks) {
      const d = 5 + 10 * s.s * easeOut(f / 4);
      twinkle(Sp, c + Math.cos(s.a) * d, c + Math.sin(s.a) * d, f === 1 ? 1 : 0, f === 1 ? [P.W, P.G3] : f === 2 ? [P.G3] : [P.G2]);
    }
    sparkL.push(Sp);
  }
  return el("bow_hit", "Bow", S, S, [{ name: "flare", frames: flareL }, { name: "bits", frames: bitsL }, { name: "sparks", frames: sparkL }],
    Array(N).fill(40), [["hit", 0, N - 1]]);
}

// ================================================================ METEORITE
// a rock wrapped in fire: bow shock in front, a tail of flame tongues, smoke and embers streaming
// off. flies right; the head sits 14px right of the canvas centre (the prefab offsets for it)
const METEOR_HEAD = [46, 14];
function makeMeteor() {
  const W = 64, H = 28, [hx, hy] = METEOR_HEAD, N = 6, r = D.rng(31);
  const rockShape = [];
  for (let y = -5; y <= 5; y++) for (let x = -5; x <= 5; x++) {
    const d = Math.hypot(x, y), a = Math.atan2(y, x);
    if (d < 3.6 + 0.9 * Math.sin(a * 3 + 1) + 0.5 * Math.sin(a * 5)) rockShape.push([x, y, a, d]);
  }
  const embers = Array.from({ length: 7 }, (_, i) => ({ off: i * 6 + r() * 3, y: (r() - 0.5) * 10, ph: r() * TAU }));
  const tailL = [], smokeL = [], rockL = [], shockL = [], emberL = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(W, H);
    D.each(F, (x, y) => {
      const u = hx - x, v = y - hy;
      if (u < -9 || u > 46) return 0;
      const k = Math.max(0, u) / 46, n = loopNoise(x / 4, y / 2.6, f, N, 1.5, 11);
      const half = 6.8 * Math.pow(1 - k, 0.75) + 0.6;
      const edge = 1 - Math.abs(v) / (half * (0.7 + 0.6 * n));
      if (edge <= 0) return 0;
      if (u < 0) { const d = Math.hypot(u, v); return d < 8 ? 0.98 - (d / 8) * 0.35 : 0; }
      return (1 - k * 0.92) * (0.5 + 0.5 * edge) * (0.62 + 0.7 * n);
    });
    D.each(F, (x, y) => { const d = Math.hypot(x - hx, y - hy); return d < 8.5 ? 1.02 - (d / 8.5) * 0.4 : 0; });
    tailL.push(D.shade(img(W, H), F, FIRE));

    const Sm = D.field(W, H);
    D.each(Sm, (x, y) => {
      const u = hx - x, v = y - hy;
      if (u < 16 || u > 66) return 0;
      const n = loopNoise(x / 5, y / 3.5, f, N, 1.7, 23), spread = 5 + u * 0.14;
      if (Math.abs(v) > spread) return 0;
      return n > 0.52 ? 0.05 + (n - 0.52) * 0.35 * (1 - (u - 16) / 60) : 0;
    });
    smokeL.push(D.shade(img(W, H), Sm, SMOKE, { bands: false }));

    const R = img(W, H);
    for (const [x, y, a] of rockShape) {
      const front = Math.cos(a - 0.15);
      let col = front > 0.55 ? P.O2 : front > 0.1 ? P.S3 : front > -0.5 ? P.S2 : P.S1;
      if (front > 0.8) col = P.G3;
      put(R, hx + 1 + x, hy + y, col);
    }
    [[0, 1], [-1, -1], [1, 2]].forEach(([x, y], i) => { if ((i + f) % 3 !== 0) put(R, hx + 1 + x, hy + y, P.O1); });
    rockL.push(withOutline(R, P.R0));

    const Sh = img(W, H);
    for (let y = 0; y < H; y++) for (let x = hx; x < W; x++) {
      const d = Math.hypot(x + 0.5 - (hx + 1), y + 0.5 - hy), a = Math.atan2(y + 0.5 - hy, x + 0.5 - (hx + 1));
      if (Math.abs(a) > 1.15 || d < 6.6 || d > 7.8) continue;
      if ((Math.round(a * 9) + f) % 4 === 0) continue;
      put(Sh, x, y, Math.abs(a) < 0.55 ? P.W : P.G3);
    }
    shockL.push(Sh);

    const E = img(W, H);
    embers.forEach((e, i) => {
      const u = 12 + ((e.off + f * 7) % 44), x = hx - u, y = hy + e.y + Math.sin(e.ph + f) * 1.5;
      const col = u < 22 ? P.G3 : u < 34 ? P.O2 : u < 46 ? P.R2 : P.R1;
      if ((i + f) % 4 !== 0) put(E, x, y, col);
    });
    emberL.push(E);
  }
  return el("met_meteor", "Meteorite", W, H,
    [{ name: "smoke", frames: smokeL }, { name: "tail", frames: tailL }, { name: "embers", frames: emberL }, { name: "rock", frames: rockL }, { name: "shock", frames: shockL }],
    Array(N).fill(50), [["fall", 0, N - 1]], { head: METEOR_HEAD });
}

// the target: a cinnabar seal on the ground where the meteor will land. played over the flight,
// frame by frame: the chevrons close in and the rim starts to blink just before it hits
const MARK_R = 57;
function makeMark() {
  const S = 128, c = 64, R = MARK_R, N = 10;
  const frames = [];
  for (let f = 0; f < N; f++) {
    const p = f / (N - 1), blink = f >= 6 && f % 2 === 1, im = img(S, S);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const dx = x + 0.5 - c, dy = y + 0.5 - c, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
      if (d < R - 1.5) {
        const dens = 0.035 + 0.2 * Math.pow(d / R, 4) + 0.05 * p;
        if (D.bayer(x, y) < dens) put(im, x, y, d > R - 7 ? P.R1 : P.R0);
      }
      if (d >= R - 1.5 && d <= R + 0.5) {
        const seg = Math.floor(((a + p * 0.7 + TAU * 2) % TAU) / TAU * 24);
        if (seg % 2 === 0 || p > 0.7) put(im, x, y, blink ? P.R3 : d > R - 0.5 ? P.R2 : P.R1);
      }
      const r2 = R * 0.55;
      if (Math.abs(d - r2) < 0.55 && Math.floor(((a + TAU) % TAU) / TAU * 60) % 3 === 0) put(im, x, y, P.R1);
    }
    for (let k = 0; k < 8; k++) {
      const a = k * TAU / 8 + p * 0.7;
      D.paint(im, D.line(D.mask(S, S), c + Math.cos(a) * (R - 7), c + Math.sin(a) * (R - 7), c + Math.cos(a) * (R - 3), c + Math.sin(a) * (R - 3)), blink ? P.G3 : P.G1);
    }
    // four chevrons homing in on the centre
    const dist = lerp(R - 11, 5, easeInOut(p)), chev = D.mask(S, S);
    for (let k = 0; k < 4; k++) {
      const a = k * TAU / 4 + TAU / 8, ux = Math.cos(a), uy = Math.sin(a), vx = -uy, vy = ux;
      const tx = c + ux * dist, ty = c + uy * dist;
      D.line(chev, tx, ty, tx + ux * 5 + vx * 4, ty + uy * 5 + vy * 4);
      D.line(chev, tx, ty, tx + ux * 5 - vx * 4, ty + uy * 5 - vy * 4);
    }
    D.paint(im, D.minus(D.dilate(chev, 1, false), chev), P.R0);
    D.paint(im, chev, f === N - 1 ? P.W : blink ? P.G3 : P.G2);
    // the crosshair, flaring at the end
    for (let i = 2; i <= 4; i++) [[i, 0], [-i, 0], [0, i], [0, -i]].forEach(([u, v]) => put(im, c + u, c + v, p > 0.8 ? P.G3 : P.R2));
    if (f === N - 1) { twinkle(im, c, c, 2, [P.W, P.G3, P.G2]); ringPx(im, c, c, 6, P.G3); }
    frames.push(im);
  }
  return el("met_mark", "Meteorite", S, S, [{ name: "seal", frames }], Array(N).fill(100), [["incoming", 0, N - 1]], { radius: R });
}
function ringPx(im, cx, cy, R, col) {
  for (let y = cy - R - 1; y <= cy + R + 1; y++) for (let x = cx - R - 1; x <= cx + R + 1; x++)
    if (Math.abs(Math.hypot(x + 0.5 - cx, y + 0.5 - cy) - R) < 0.5) put(im, x, y, col);
}

// the blast: white flash, a rolling fireball, a shock ring out to the damage radius (57px, 2
// units, what the Area stat scales from), rocks and embers thrown out and violet smoke rolling off
function makeImpact() {
  const S = 160, c = 80, N = 12, r = D.rng(2024);
  const debris = Array.from({ length: 9 }, () => ({ a: r() * TAU, s: 0.45 + r() * 0.75, big: r() < 0.45, delay: r() < 0.3 ? 1 : 0 }));
  const embers = Array.from({ length: 22 }, () => ({ a: r() * TAU, d: 12 + r() * 44, rise: 1 + r() * 1.4, ph: Math.floor(r() * 3) }));
  const puffs = Array.from({ length: 10 }, () => ({ a: r() * TAU, s: 0.3 + r() * 0.8, size: 0.5 + r() * 0.6, rise: 0.8 + r() * 1.2 }));
  const thin = Array.from({ length: 12 }, () => ({ a: r() * TAU, len: 38 + r() * 30 }));
  const fireL = [], ringL = [], debrisL = [], sparkL = [], smokeL = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    if (f === 0) {
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 20 ? 1 : d < 28 ? 0.92 - (d - 20) / 8 * 0.3 : 0; });
      D.flare(F, c, c, 60, 7, 30, 4, 1, 0.5);
      for (const t of thin) D.ray(F, c, c, t.a, t.len, 1.5, 0.66, 0.3);
    } else if (f <= 8) {
      const rb = [0, 26, 33, 37, 38, 37, 34, 29, 22][f], inten = [0, 1, 0.9, 0.78, 0.64, 0.5, 0.38, 0.27, 0.18][f];
      D.each(F, (x, y) => {
        const dx = x - c, dy = y - c, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
        const n = fbm(x / 6, y / 6 - f * 0.8, 5 + f), lobes = 0.75 + 0.5 * vnoise((a + Math.PI) * 2.2, f * 0.7, 13);
        const edge = rb * lobes * (0.85 + 0.3 * n);
        if (d > edge) return 0;
        let v = inten * (1.05 - 0.6 * d / edge) + (n - 0.5) * 0.45 * inten;
        if (f >= 5 && n < 0.3 + (f - 5) * 0.08) v *= 0.35;
        return v;
      });
    }
    fireL.push(D.shade(img(S, S), F, FIRE));

    const Rg = D.field(S, S);
    if (f >= 1 && f <= 6) {
      const R = [0, 38, 48, 53, 56, 58, 60][f], th = [0, 5, 4, 3, 3, 2, 2][f], pk = [0, 1, 0.88, 0.74, 0.58, 0.44, 0.3][f];
      ringField(Rg, c, c, R, th, pk, f >= 3 ? breakup(R, f, [0, 0, 0, 0.85, 0.7, 0.55, 0.4][f]) : null);
    }
    ringL.push(D.shade(img(S, S), Rg, FIRE));

    // rocks thrown out from the fireball's edge, glowing on the way out and cooling
    const Db = img(S, S);
    if (f >= 2) for (const d of debris) {
      const t = (f - 2 - d.delay) / 9;
      if (t < 0 || (t > 0.75 && !d.big)) continue;
      const dist = 34 + 46 * d.s * easeOut(t), x = Math.round(c + Math.cos(d.a) * dist), y = Math.round(c + Math.sin(d.a) * dist);
      const hot = t < 0.45, s = d.big ? 2 : 1;
      if (hot) for (let k = 2; k <= 4; k++) put(Db, x - Math.round(Math.cos(d.a) * k), y - Math.round(Math.sin(d.a) * k), k === 2 ? P.O2 : k === 3 ? P.O1 : P.R2);
      for (let v = 0; v < s; v++) for (let u = 0; u < s; u++) put(Db, x + u, y + v, hot ? P.O2 : (u + v === 0 ? P.S3 : P.S2));
      if (hot) put(Db, x, y, P.G3);
    }
    debrisL.push(Db);

    const Sk = img(S, S);
    if (f >= 2) embers.forEach((e, i) => {
      const k = f - 2, x = c + Math.cos(e.a) * e.d * (1 + 0.25 * k / 9), y = c + Math.sin(e.a) * e.d * (1 + 0.25 * k / 9) - k * e.rise * 1.6;
      if ((f + e.ph) % 3 === 0 && f > 3) return;
      const col = f < 5 ? P.G3 : f < 7 ? P.O2 : f < 9 ? P.R2 : P.R1;
      if (f === 2 && i % 2 === 0) twinkle(Sk, x, y, 1, [P.W, P.G3]); else put(Sk, x, y, col);
    });
    sparkL.push(Sk);

    const Sm = D.field(S, S);
    if (f >= 3) {
      const i = f - 3, pr = 24 + i * 4, rad = 6 + i * 1.1, val = [0.13, 0.125, 0.115, 0.105, 0.095, 0.085, 0.075, 0.068, 0.062][i];
      for (const p of puffs) {
        const px = c + Math.cos(p.a) * pr * p.s, py = c + Math.sin(p.a) * pr * p.s - i * 1.6 * p.rise, pr2 = rad * p.size;
        D.each(Sm, (x, y) => {
          const d = Math.hypot(x - px, y - py), edge = pr2 * (0.8 + 0.4 * vnoise(x / 2, y / 2, 9));
          return d < edge ? val * (1 - 0.45 * d / edge) : 0;
        });
      }
    }
    smokeL.push(D.shade(img(S, S), Sm, DUST, { bands: false }));
  }
  return el("met_impact", "Meteorite", S, S,
    [{ name: "smoke", frames: smokeL }, { name: "fire", frames: fireL }, { name: "ring", frames: ringL }, { name: "debris", frames: debrisL }, { name: "embers", frames: sparkL }],
    Array(N).fill(42), [["blast", 0, N - 1]], { radius: 57 });
}

// the scorch it leaves: glowing cracks and a molten centre cooling to black, then (in code) fading
function makeCrater() {
  const S = 96, c = 48, N = 6, r = D.rng(99);
  const cracks = Array.from({ length: 7 }, (_, i) => ({ a: i * TAU / 7 + (r() - 0.5) * 0.5, reach: 22 + r() * 12, seed: 500 + i }));
  const scorch = img(S, S);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const d = Math.hypot(x + 0.5 - c, y + 0.5 - c), n = fbm(x / 6, y / 6, 3), edge = 28 * (0.8 + 0.4 * n);
    if (d >= edge) continue;
    const k = d / edge;
    if (k < 0.55) put(scorch, x, y, P.S0);
    else if (k < 0.8) put(scorch, x, y, D.bayer(x, y) < 0.7 ? P.S1 : P.S0);
    else if (D.bayer(x, y) < 0.4 * (1 - k) / 0.2) put(scorch, x, y, D.bayer(x + 1, y) < 0.5 ? P.R0 : P.S1);
  }
  const crackM = D.mask(S, S);
  for (const k of cracks) {
    const q = D.rng(k.seed);
    D.polyline(crackM, D.bolt(q, c + Math.cos(k.a) * 5, c + Math.sin(k.a) * 5, c + Math.cos(k.a) * k.reach, c + Math.sin(k.a) * k.reach, 6, 3));
  }
  const crackL = [], coreL = [], wispL = [], baseL = [];
  const core = [[P.W, P.G3], [P.G3, P.O2], [P.O2, P.O1], [P.O1, P.R2], [P.R2, P.R1], [P.R1, P.R0]];
  for (let f = 0; f < N; f++) {
    baseL.push(scorch);
    const Cr = img(S, S), edge = [P.O2, P.O1, P.R2, P.R1, P.R0, null][f];
    if (edge) D.paint(Cr, D.minus(D.dilate(crackM, 1, false), crackM), edge, 0.6, f);
    D.paint(Cr, crackM, [P.G3, P.O2, P.O1, P.R2, P.R1, P.R0][f]);
    crackL.push(Cr);
    const Co = img(S, S);
    for (let y = c - 6; y <= c + 6; y++) for (let x = c - 6; x <= c + 6; x++) {
      const d = Math.hypot(x + 0.5 - c, y + 0.5 - c);
      if (d < 5.5) put(Co, x, y, d < 3 ? core[f][0] : core[f][1]);
    }
    coreL.push(Co);
    // a thin curl of smoke off the molten centre
    const Wp = D.field(S, S);
    if (f <= 4) D.each(Wp, (x, y) => {
      const top = c - 10 - f * 6, bottom = c - 3 - f * 2;
      if (y > bottom || y < top) return 0;
      const px = c + Math.sin((y + f * 3) / 4) * 2.5, w = 1.5 + (bottom - y) * 0.2, dx = Math.abs(x - px);
      if (dx > w) return 0;
      return (0.085 - f * 0.009) * (1 - 0.6 * dx / w) * (0.7 + 0.6 * vnoise(x / 2, (y + f * 4) / 2, 21));
    });
    wispL.push(D.shade(img(S, S), Wp, SMOKE, { bands: false }));
  }
  return el("met_crater", "Meteorite", S, S,
    [{ name: "scorch", frames: baseL }, { name: "cracks", frames: crackL }, { name: "core", frames: coreL }, { name: "wisps", frames: wispL }],
    Array(N).fill(140), [["cool", 0, N - 1]], { radius: 57 });
}

// ================================================================ ELECTRICAL AURA
// a crackling ring of lightning around a sparsely charged field, drawn at eight radii so it keeps
// the world's pixel size at every level. the zap lands on each enemy a damage tick hits
const AURA_RADII = [44, 52, 60, 70, 80, 92, 106, 120];
const AURA_FRAMES = 6;
function makeAuraRing() {
  const S = 256, c = 128, fieldL = [], ringL = [], arcL = [];
  AURA_RADII.forEach((R, k) => {
    for (let f = 0; f < AURA_FRAMES; f++) {
      const r = D.rng(6000 + k * 100 + f);
      // the charge only shows toward the edge, so the floor inside stays readable
      const Fd = D.field(S, S);
      D.each(Fd, (x, y) => {
        const d = Math.hypot(x - c, y - c), q = d / R;
        if (q >= 1 || q < 0.62) return 0;
        return d > R - 5 ? 0.072 : 0.05 + 0.02 * Math.pow((q - 0.62) / 0.38, 2);
      });
      fieldL.push(D.shade(img(S, S), Fd, ELECTRO));

      const A = D.mask(S, S), B = D.mask(S, S);
      const n = Math.max(14, Math.round(TAU * R / 7));
      const loop = (M, jitter, gaps) => {
        const pts = [];
        for (let i = 0; i <= n; i++) {
          const a = (i % n) * TAU / n + (r() - 0.5) * 0.35 * TAU / n, rr = R + (r() * 2 - 1) * jitter;
          pts.push(i === n ? pts[0] : [c + Math.cos(a) * rr, c + Math.sin(a) * rr]);
        }
        for (let i = 1; i < pts.length; i++) if (r() > gaps) D.line(M, pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1]);
      };
      loop(A, 2.8, 0.16);
      loop(B, 4, 0.45);
      const Rg = img(S, S);
      D.paint(Rg, D.minus(D.dilate(B, 1, false), B), P.V3, 0.5, f);
      D.paint(Rg, B, P.V5);
      D.paint(Rg, D.minus(D.dilate(A, 1, false), A), P.V4);
      D.paint(Rg, A, f % 2 === 0 ? P.W : P.V6);
      ringL.push(Rg);

      const Ar = img(S, S), M = D.mask(S, S);
      for (let i = 0; i < 4 + Math.round(R / 40); i++) {
        const a0 = r() * TAU, out = r() < 0.55 ? 1 : -1, len = 6 + r() * 9, a1 = a0 + (r() - 0.5) * 0.3;
        D.polyline(M, D.bolt(r, c + Math.cos(a0) * R, c + Math.sin(a0) * R, c + Math.cos(a1) * (R + out * len), c + Math.sin(a1) * (R + out * len), 3, 3));
      }
      D.paint(Ar, D.minus(D.dilate(M, 1, false), M), P.V4, 0.7, f);
      D.paint(Ar, M, P.V6);
      for (let i = 0; i < 7; i++) {
        const a = r() * TAU, d = R + (r() - 0.5) * 16;
        twinkle(Ar, c + Math.cos(a) * d, c + Math.sin(a) * d, r() < 0.4 ? 1 : 0, [P.W, P.V6]);
      }
      arcL.push(Ar);
    }
  });
  const tags = AURA_RADII.map((R, k) => ["r" + R, k * AURA_FRAMES, k * AURA_FRAMES + AURA_FRAMES - 1]);
  return el("aura_ring", "Aura", S, S, [{ name: "field", frames: fieldL }, { name: "ring", frames: ringL }, { name: "arcs", frames: arcL }],
    Array(fieldL.length).fill(60), tags, { radii: AURA_RADII, framesPerSize: AURA_FRAMES });
}


// lightning striking an enemy from above; the strike lands on the canvas centre
function makeZap() {
  const W = 32, H = 48, cx = 16, cy = 24, N = 5;
  const q = D.rng(3100), pts = D.bolt(q, cx + 4, 0, cx, cy, 5, 4);
  const q2 = D.rng(3107), branch = D.bolt(q2, pts[6][0], pts[6][1], pts[6][0] - 7, pts[6][1] + 6, 3, 2);
  const boltL = [], splashL = [];
  for (let f = 0; f < N; f++) {
    const B = img(W, H), M = D.mask(W, H);
    if (f <= 2) {
      D.polyline(M, f === 2 ? pts.slice(Math.floor(pts.length * 0.6)) : pts);
      if (f === 1) D.polyline(M, branch);
      D.paint(B, D.minus(D.dilate(M, 1, f === 0), M), f === 0 ? P.V5 : P.V4, f === 2 ? 0.5 : 1);
      D.paint(B, M, f === 0 ? P.W : f === 1 ? P.V6 : P.V5);
    }
    boltL.push(B);
    const Sp = D.field(W, H);
    if (f === 0) D.flare(Sp, cx, cy, 7, 3, 3, 1, 1, 0.5);
    if (f >= 1 && f <= 3) {
      const rx = [5, 8, 10][f - 1], ry = rx * 0.45, th = [1.8, 1.4, 1.1][f - 1], pk = [0.82, 0.56, 0.36][f - 1];
      D.each(Sp, (x, y) => {
        const dx = (x - cx) / rx, dy = (y - cy - 1) / ry, e = Math.hypot(dx, dy), dpx = (e - 1) * rx;
        if (dpx > 0.5 || dpx < -th) return 0;
        if (f === 3 && vnoise((Math.atan2(dy, dx) + Math.PI) * 3, 4, 7) > 0.55) return 0;
        return pk;
      });
    }
    const Si = D.shade(img(W, H), Sp, ELECTRO);
    if (f >= 1) for (let k = 0; k < 4; k++) {
      const a = k * TAU / 4 + 0.5 + f * 0.3, d = 5 + f * 2.5;
      twinkle(Si, cx + Math.cos(a) * d, cy + 1 + Math.sin(a) * d * 0.45 - f, f === 1 ? 1 : 0, f <= 2 ? [P.W, P.V6] : f === 3 ? [P.V6] : [P.V4]);
    }
    splashL.push(Si);
  }
  return el("aura_zap", "Aura", W, H, [{ name: "splash", frames: splashL }, { name: "bolt", frames: boltL }], Array(N).fill(40), [["zap", 0, N - 1]]);
}

// ================================================================ PEACH TALISMANS
// a yellow paper talisman with cinnabar border and glyph and a red tassel hanging below. flying,
// the paper flutters and the tassel swings; stuck, it burns down from the bottom; spent, it
// crumbles to glowing ash. 20x30, the paper 10x18
const GLYPH = ["####", ".##.", "#..#", "####", ".##.", "#..#", ".##.", "####", ".##.", ".##.", "#..#"];
function paper(glyphCol = P.R1, glyphLit = null) {
  const im = img(20, 30);
  for (let y = 6; y <= 23; y++) for (let x = 5; x <= 14; x++) put(im, x, y, x === 14 || y === 23 ? P.G2 : P.G3);
  put(im, 5, 6, P.W); put(im, 6, 6, P.W); put(im, 5, 7, P.W);
  for (let y = 7; y <= 22; y++) { put(im, 6, y, P.R2); put(im, 13, y, P.R2); }
  for (let x = 6; x <= 13; x++) { put(im, x, 7, P.R2); put(im, x, 22, P.R2); }
  GLYPH.forEach((row, i) => [...row].forEach((ch, j) => { if (ch === "#") put(im, 8 + j, 9 + i, glyphLit ? glyphLit((i + j) % 2) : (i % 3 === 0 ? P.R2 : glyphCol)); }));
  return im;
}
function bend(im, amount) {
  const out = img(im.w, im.h);
  for (let y = 0; y < im.h; y++) {
    const off = y > 14 ? Math.round(amount * (y - 14) / 9) : 0;
    for (let x = 0; x < im.w; x++) { const c = D.get(im, x, y); if (c) put(out, x + off, y, c); }
  }
  return out;
}
function makeTalisman() {
  const N = 6, sway = [0, 1, 1, 0, -1, -1], tassel = [0, 1, 2, 1, 0, -1];
  // a spark of the spell running round the paper's edge
  const edge = [[4, 5], [10, 4], [15, 8], [15, 16], [12, 24], [5, 18]];
  const bodyL = [], sparkL = [];
  for (let f = 0; f < N; f++) {
    const im = bend(paper(), sway[f]);
    const tx = 9 + sway[f];
    for (let k = 0; k < 5; k++) {
      const x = tx + Math.round(tassel[f] * k / 4);
      put(im, x, 24 + k, k === 0 ? P.R3 : P.R2);
      if (k >= 3) put(im, x + 1, 24 + k, P.R1);
    }
    bodyL.push(withOutline(im));
    const Sp = img(20, 30), [ex, ey] = edge[f];
    twinkle(Sp, ex + (ey > 14 ? sway[f] : 0), ey, f % 2 ? 0 : 1, [P.W, P.G3]);
    sparkL.push(Sp);
  }
  return el("peach_talisman", "PeachTalismans", 20, 30, [{ name: "talisman", frames: bodyL }, { name: "spark", frames: sparkL }],
    Array(N).fill(70), [["flutter", 0, N - 1]]);
}

// burning down: 8 steps of the paper shrinking from the bottom, each with two flame flickers.
// frame = step * 2 + flicker; the code picks the step from how long it has been stuck
const BURN_STEPS = 8;
function makeBurn() {
  const paperL = [], fireL = [], emberL = [];
  for (let s = 0; s < BURN_STEPS; s++) for (let fl = 0; fl < 2; fl++) {
    const p = s / (BURN_STEPS - 1), burnY = 23 - Math.round(p * 15);
    const base = paper(P.R1, k => fl === 0 ? (k ? P.G3 : P.O2) : (k ? P.W : P.G3));
    const im = img(20, 30);
    for (let y = 0; y < 30; y++) for (let x = 0; x < 20; x++) {
      const col = D.get(base, x, y);
      if (!col) continue;
      const cut = burnY + (hash2(x, s, 3) < 0.5 ? 0 : -1);
      if (y > cut) continue;
      put(im, x, y, y === cut ? (x % 2 ? P.R0 : P.G0) : y === cut - 1 ? P.G1 : col);
    }
    paperL.push(withOutline(im));

    // tongues of flame licking up from the burning edge, each column its own height
    const F = D.field(20, 30);
    D.each(F, (x, y) => {
      const xi = Math.floor(x);
      if (xi < 5 || xi > 14) return 0;
      const h = burnY + 1 - y, colN = fbm(xi / 1.7, fl * 1.9 + s * 0.7, 41);
      const tongue = (2.5 + 7 * colN) * (1 - Math.pow(Math.abs(xi - 9.5) / 6.5, 2) * 0.6);
      if (h < 0 || h > tongue) return 0;
      return 0.9 - 0.72 * (h / tongue) + (vnoise(xi, y + fl * 5, 9) - 0.5) * 0.2;
    });
    fireL.push(D.shade(img(20, 30), F, FIRE));

    const E = img(20, 30);
    for (let i = 0; i < 3; i++) {
      const y = burnY - 9 - ((i * 5 + fl * 3 + s) % 7), x = 6 + i * 4 + ((fl + i) % 2);
      put(E, x, y, (i + fl) % 2 ? P.O2 : P.G3);
    }
    emberL.push(E);
  }
  return el("peach_burn", "PeachTalismans", 20, 30, [{ name: "paper", frames: paperL }, { name: "fire", frames: fireL }, { name: "embers", frames: emberL }],
    Array(BURN_STEPS * 2).fill(80), Array.from({ length: BURN_STEPS }, (_, s) => ["step" + s, s * 2, s * 2 + 1]), { steps: BURN_STEPS });
}

// the spent talisman: its last scrap flares and breaks into glowing flakes that cool to ash
function makeAsh() {
  const W = 24, H = 32, N = 7, r = D.rng(808);
  const flakes = Array.from({ length: 10 }, (_, i) => ({ x: 8 + (i % 4) * 2.5 + r(), y: 9 + Math.floor(i / 4) * 2, vx: (r() - 0.5) * 9, vy: -6 - r() * 8 }));
  const flakeL = [], flareL = [], smokeL = [];
  const cols = [null, [P.O2, P.G3], [P.O1, P.O2], [P.R2, P.S2], [P.R1, P.S2], [P.S2, P.S3], [P.S3, null]];
  const scrap = paper();
  for (let f = 0; f < N; f++) {
    const Fl = img(W, H);
    if (f === 0) {
      // what's left of the paper, glowing along its burnt edge
      for (let y = 6; y <= 12; y++) for (let x = 5; x <= 14; x++) {
        const col = D.get(scrap, x, y);
        if (!col) continue;
        const burnt = y === 12 || (y === 11 && x % 3 === 0);
        put(Fl, x + 2, y + 2, burnt ? P.O2 : col === P.R2 || col === P.R1 ? P.O1 : P.G3);
      }
    } else {
      const t = f / (N - 1);
      flakes.forEach((k, i) => {
        const x = Math.round(k.x + k.vx * easeOut(t)), y = Math.round(k.y + k.vy * t);
        const [a, b] = cols[f];
        put(Fl, x, y, a);
        if (b) put(Fl, x + ((i + f) % 2 ? 1 : 0), y + ((i + f) % 2 ? 0 : 1), b);
      });
    }
    flakeL.push(f === 0 ? withOutline(Fl, P.R0) : Fl);
    const Fr = D.field(W, H);
    if (f <= 1) D.flare(Fr, 12, 12, f === 0 ? 9 : 5, 3, f === 0 ? 3 : 0, 1, f === 0 ? 0.9 : 0.7, 0.4);
    flareL.push(D.shade(img(W, H), Fr, FIRE));
    const Sm = D.field(W, H);
    if (f >= 2) D.each(Sm, (x, y) => {
      const top = 12 - f * 2.2, px = 12 + Math.sin((y + f * 2) / 3) * 1.5, w = 1.2 + (14 - y) * 0.18, dx = Math.abs(x - px);
      if (y < top - 4 || y > 13 || dx > w) return 0;
      return (0.08 - (f - 2) * 0.007) * (0.7 + 0.6 * vnoise(x / 2, y / 2 + f, 5));
    });
    smokeL.push(D.shade(img(W, H), Sm, SMOKE, { bands: false }));
  }
  return el("peach_ash", "PeachTalismans", W, H, [{ name: "smoke", frames: smokeL }, { name: "flare", frames: flareL }, { name: "flakes", frames: flakeL }],
    Array(N).fill(55), [["crumble", 0, N - 1]]);
}

// the evolution's kill burst: a peach blossom opens in a ring of fire, then its petals spin away.
// 86px across: the code stretches the whole cell over the blast (1.5 units), 28.7 px per unit
function makePeachBurst() {
  const S = 86, c = 43, N = 10, r = D.rng(1414);
  const petals = Array.from({ length: 14 }, (_, i) => ({ a: i * TAU / 14 + r() * 0.3, s: 0.7 + r() * 0.4, spin: Math.floor(r() * 4) }));
  const embers = Array.from({ length: 14 }, () => ({ a: r() * TAU, s: 0.5 + r() * 0.6 }));
  const shapes = [[[0, 0], [1, 0], [-1, 0], [0, -1]], [[0, 0], [0, 1], [0, -1], [1, 0]], [[0, 0], [1, 1], [-1, -1], [1, 0]], [[0, 0], [1, -1], [-1, 1], [0, 1]]];
  const bloomL = [], ringL = [], petalL = [], emberL = [];
  for (let f = 0; f < N; f++) {
    const B = img(S, S);
    if (f <= 6) {
      const Rb = [5, 9, 13, 16, 17, 16, 13][f], rot = f * 0.1, fade = [1, 1, 1, 1, 0.9, 0.7, 0.5][f];
      const F = D.field(S, S);
      D.each(F, (x, y) => {
        const dx = x - c, dy = y - c, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx) - rot;
        const k = Math.round(a / (TAU / 5)), da = Math.abs(a - k * TAU / 5);
        const rp = Rb * (1 - Math.pow(da / 0.63, 2) * 0.45);
        if (d >= rp || (da < 0.12 && d > Rb * 0.8)) return 0;
        return (0.3 + 0.62 * (1 - d / Rb)) * fade;
      });
      D.over(B, withOutline(D.shade(img(S, S), F, PEACH), P.K1));
      for (let k = 0; k < 5; k++) { const a = k * TAU / 5 + rot + 0.63; put(B, c + Math.round(Math.cos(a) * 2.5), c + Math.round(Math.sin(a) * 2.5), P.G2); }
      put(B, c, c, P.G3);
    }
    bloomL.push(B);

    const F = D.field(S, S);
    if (f === 0) D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 12 ? 1 - d / 12 * 0.3 : 0; });
    if (f >= 1 && f <= 7) {
      const R = [0, 14, 24, 32, 37, 40, 42, 43][f], th = [0, 5, 5, 4, 3, 3, 2, 2][f], pk = [0, 1, 0.9, 0.78, 0.62, 0.48, 0.34, 0.22][f];
      ringField(F, c, c, R, th, pk, f >= 4 ? breakup(R, f, [0, 0, 0, 0, 0.85, 0.7, 0.55, 0.4][f]) : null);
    }
    ringL.push(D.shade(img(S, S), F, FIRE));

    const Pt = img(S, S);
    if (f >= 4) {
      const t = (f - 3) / 6;
      petals.forEach((p, i) => {
        const d = lerp(12, 44, easeOut(t)) * p.s, a = p.a + t * 1.2, x = Math.round(c + Math.cos(a) * d), y = Math.round(c + Math.sin(a) * d);
        const shape = shapes[(p.spin + f) % 4];
        shape.forEach(([u, v], j) => put(Pt, x + u, y + v, f >= 9 ? P.K1 : j === 0 ? (f >= 8 ? P.K2 : P.K3) : f >= 8 ? P.K1 : P.K2));
      });
    }
    petalL.push(Pt);

    const E = img(S, S);
    if (f >= 2) embers.forEach((e, i) => {
      const d = (12 + 30 * easeOut((f - 2) / 7)) * e.s;
      if ((i + f) % 3 !== 0) put(E, c + Math.cos(e.a) * d, c + Math.sin(e.a) * d - (f - 2), f < 4 ? P.G3 : f < 6 ? P.O2 : f < 8 ? P.R2 : P.R1);
    });
    emberL.push(E);
  }
  return el("peach_burst", "PeachTalismans", S, S,
    [{ name: "ring", frames: ringL }, { name: "bloom", frames: bloomL }, { name: "petals", frames: petalL }, { name: "embers", frames: emberL }],
    Array(N).fill(45), [["burst", 0, N - 1]]);
}

// ================================================================ SEVEN STAR SWORDS
// a jian: steel blade with the seven stars inlaid in gold, a glint running up it, gold guard, red
// grip and a swinging tassel, in a shimmer of starlight. points up, 15x32
function makeSword() {
  const W = 15, H = 32, N = 6, swing = [0, 1, 1, 0, -1, -1];
  const bodyL = [], auraL = [];
  for (let f = 0; f < N; f++) {
    const im = img(W, H);
    put(im, 7, 1, P.A5);
    for (let y = 2; y <= 19; y++) { put(im, 6, y, P.A4); put(im, 7, y, y === 2 ? P.W : P.A5); put(im, 8, y, P.A2); }
    [4, 6, 8, 10, 12, 14, 16].forEach(y => put(im, 7, y, P.G2));
    const gy = 18 - f * 3;
    if (gy >= 2) { put(im, 6, gy, P.W); put(im, 7, gy - 1, P.W); if (gy + 1 <= 19) put(im, 6, gy + 1, P.A5); }
    for (let x = 3; x <= 11; x++) put(im, x, 20, x === 7 ? P.G3 : P.G2);
    put(im, 3, 19, P.G2); put(im, 11, 19, P.G2);
    for (let x = 4; x <= 10; x++) put(im, x, 21, P.G1);
    for (let y = 22; y <= 25; y++) for (let x = 6; x <= 8; x++) put(im, x, y, (y + (x === 7 ? 1 : 0)) % 2 ? P.R2 : P.R1);
    for (let x = 6; x <= 8; x++) put(im, x, 26, P.G2);
    put(im, 7, 26, P.G3);
    for (let k = 0; k < 4; k++) {
      const x = 7 + Math.round(swing[f] * (k + 1) / 3);
      put(im, x, 27 + k, k % 2 ? P.R3 : P.R2);
      if (k >= 2) put(im, x + 1, 27 + k, P.R1);
    }
    const outlined = withOutline(im);
    bodyL.push(outlined);
    // starlight: a few points of light drifting up beside the blade, twinkling in and out
    const G = img(W, H);
    [[4, 17, 0], [10, 11, 2], [4, 7, 4], [10, 3, 1]].forEach(([x, y0, ph], i) => {
      const age = (f + ph) % 6, y = y0 - age;
      if (age >= 4 || y < 1) return;
      twinkle(G, x + (i % 2 ? -Math.floor(age / 2) : Math.floor(age / 2)), y, age === 1 ? 1 : 0, age === 1 ? [P.W, P.A4] : age === 0 ? [P.A4] : [P.A3]);
    });
    auraL.push(G);
  }
  return el("sss_sword", "SevenStarSwords", W, H, [{ name: "starlight", frames: auraL }, { name: "sword", frames: bodyL }],
    Array(N).fill(80), [["hover", 0, N - 1]]);
}

// a star shot: a four point twinkle turning between + and x. 15x15
function makeStar() {
  const S = 15, c = 7, N = 6;
  const long = [6, 5, 4, 5, 6, 5], diag = [2, 3, 4, 3, 2, 1];
  const armCols = [P.W, P.A5, P.A4, P.A4, P.A3, P.A2], diagCols = [P.A5, P.A4, P.A3, P.A2];
  const frames = [], glowL = [];
  for (let f = 0; f < N; f++) {
    const im = img(S, S);
    for (let i = 1; i <= long[f]; i++) [[i, 0], [-i, 0], [0, i], [0, -i]].forEach(([u, v]) => put(im, c + u, c + v, armCols[i - 1]));
    for (let i = 1; i <= diag[f]; i++) [[i, i], [-i, i], [i, -i], [-i, -i]].forEach(([u, v]) => put(im, c + u, c + v, diagCols[i - 1]));
    put(im, c, c, P.W);
    frames.push(im);
    const G = img(S, S);
    if (f % 2 === 0) for (let y = 0; y < S; y++) for (let x = 0; x < S; x++)
      if (Math.hypot(x - c, y - c) < 3.6 && !D.get(im, x, y) && D.bayer(x, y) < 0.45) put(G, x, y, P.A2);
    glowL.push(G);
  }
  return el("sss_star", "SevenStarSwords", S, S, [{ name: "glow", frames: glowL }, { name: "star", frames }], Array(N).fill(50), [["twinkle", 0, N - 1]]);
}

function makeStarHit() {
  const S = 24, c = 12, N = 5, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    if (f === 0) D.flare(F, c, c, 9, 3, 4, 1, 1, 0.45);
    if (f === 1) D.each(F, (x, y) => Math.hypot(x - c, y - c) < 2 ? 0.8 : 0);
    if (f >= 1 && f <= 3) { const R = [5, 8, 10][f - 1]; ringField(F, c, c, R, [2, 1.5, 1.2][f - 1], [0.7, 0.5, 0.36][f - 1], f >= 2 ? breakup(R, f, 0.6) : null); }
    const im = D.shade(img(S, S), F, AZURE);
    if (f >= 2) for (let k = 0; k < 4; k++) {
      const a = k * TAU / 4 + TAU / 8, d = [0, 0, 8, 10, 11][f];
      twinkle(im, c + Math.cos(a) * d, c + Math.sin(a) * d, f === 2 ? 1 : 0, f === 2 ? [P.W, P.A4] : f === 3 ? [P.A4] : [P.A3]);
    }
    frames.push(im);
  }
  return el("sss_star_hit", "SevenStarSwords", S, S, [{ name: "pop", frames }], Array(N).fill(40), [["pop", 0, N - 1]]);
}

// the flash at a sword as it looses its stars: seven rays for the seven stars
function makeLaunch() {
  const S = 32, c = 16, N = 5, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    if (f === 0) {
      for (let k = 0; k < 7; k++) D.ray(F, c, c, k * TAU / 7 - Math.PI / 2, 13, 3, 1, 0.4);
      D.each(F, (x, y) => Math.hypot(x - c, y - c) < 4 ? 1 : 0);
    }
    if (f === 1) D.each(F, (x, y) => Math.hypot(x - c, y - c) < 2.5 ? 0.85 : 0);
    if (f >= 1 && f <= 3) { const R = [6, 9, 12][f - 1]; ringField(F, c, c, R, [2, 1.6, 1.2][f - 1], [0.8, 0.58, 0.4][f - 1], f >= 2 ? breakup(R, f + 2, 0.6) : null); }
    const im = D.shade(img(S, S), F, AZURE);
    if (f >= 2) for (let k = 0; k < 7; k++) {
      const a = k * TAU / 7 - Math.PI / 2, d = [0, 0, 10, 12, 14][f];
      twinkle(im, c + Math.cos(a) * d, c + Math.sin(a) * d, f === 2 ? 1 : 0, f === 2 ? [P.W, P.A4] : f === 3 ? [P.A5] : [P.A3]);
    }
    frames.push(im);
  }
  return el("sss_launch", "SevenStarSwords", S, S, [{ name: "flash", frames }], Array(N).fill(40), [["launch", 0, N - 1]]);
}

// the evolution's laser, one 32x9 tile repeated along the beam (the renderer tiles it): a white
// core in azure bands, with bright pulses running outward and sparks on the edges. seamless in x
function makeBeam() {
  const W = 32, H = 9, N = 4, frames = [];
  const bands = [P.A2, P.A3, P.A4, P.A5, P.W, P.A5, P.A4, P.A3, P.A2];
  const lit = [P.A3, P.A4, P.A5, P.W, P.W, P.W, P.A5, P.A4, P.A3];
  for (let f = 0; f < N; f++) {
    const im = img(W, H), r = D.rng(4400 + f);
    for (let x = 0; x < W; x++) {
      let node = 99;
      for (let n = 0; n < 2; n++) { const xn = (n * 16 + f * 4) % W; node = Math.min(node, Math.abs(((x - xn + 16 + W * 2) % W) - 16)); }
      for (let y = 0; y < H; y++) {
        const edge = y === 0 || y === H - 1;
        if (edge && node > 1 && D.bayer(x + f, y) > 0.5) continue;
        put(im, x, y, node <= 1 ? lit[y] : node === 2 ? bands[Math.max(0, Math.min(8, y + (y < 4 ? 1 : y > 4 ? -1 : 0)))] : bands[y]);
      }
    }
    for (let k = 0; k < 2; k++) put(im, Math.floor(r() * W), r() < 0.5 ? 0 : H - 1, P.W);
    frames.push(im);
  }
  return el("sss_beam", "SevenStarSwords", W, H, [{ name: "beam", frames }], Array(N).fill(45), [["flow", 0, N - 1]], { pivot: "left", fullRect: true });
}

function makeBeamCap() {
  const S = 24, c = 12, N = 4, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    D.flare(F, c, c, [11, 9, 10, 8][f], 3, [5, 4, 5, 3][f], 1, 1, 0.45, f * Math.PI / 8);
    D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 3.5 ? 1 : d < 5.5 && D.bayer(x, y) < 0.6 ? 0.55 : 0; });
    frames.push(D.shade(img(S, S), F, AZURE));
  }
  return el("sss_beam_cap", "SevenStarSwords", S, S, [{ name: "cap", frames }], Array(N).fill(45), [["flare", 0, N - 1]]);
}

// ================================================================ write everything
function build() {
  const elements = [
    makeArrow(), makeLoose(), makeArrowHit(),
    makeMeteor(), makeMark(), makeImpact(), makeCrater(),
    makeAuraRing(), makeZap(),
    makeTalisman(), makeBurn(), makeAsh(), makePeachBurst(),
    makeSword(), makeStar(), makeStarHit(), makeLaunch(), makeBeam(), makeBeamCap(),
  ];
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [];
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length, flat = [];
    for (const l of e.layers) if (l.frames.length !== n) throw new Error(`${e.name}: layer ${l.name} has ${l.frames.length} frames, not ${n}`);
    for (let f = 0; f < n; f++) {
      const im = img(e.w, e.h);
      e.layers.forEach(l => D.over(im, l.frames[f]));
      let lit = 0; for (let i = 3; i < im.data.length; i += 4) if (im.data[i]) lit++;
      if (!lit) throw new Error(`${e.name} frame ${f} is empty; Unity's importer would drop it`);
      flat.push(im);
      e.layers.forEach((l, li) => png.encode(l.frames[f], path.join(dir, `L${li}_${f}.png`)));
    }
    const { layers, ...meta } = e;
    manifest.push(Object.assign(meta, { frames: n, layers: layers.map(l => l.name) }));
    const scale = e.w <= 24 ? 8 : e.w <= 48 ? 5 : e.w <= 100 ? 3 : e.w <= 170 ? 2 : 1;
    const shown = e.name.startsWith("aura_") && e.w > 200 ? flat.filter((_, i) => Math.floor(i / e.framesPerSize) % 3 === 0) : flat;
    png.encode(png.preview(shown, scale, [22, 18, 30], Math.min(shown.length, e.w <= 48 ? 8 : e.w <= 100 ? 6 : 4), 2), path.join(out, `sheet_${e.name}.png`));
  }
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.frames} [${m.layers.join(",")}]`).join("\n"));
}

// the palette, ramps and drawing helpers, for the other generators (boss/boss.js)
module.exports = { P, FIRE, GOLD, AZURE, PEACH, ELECTRO, SMOKE, DUST, TAU, easeOut, easeInOut, lerp, img, hash2, vnoise, fbm,
  loopNoise, maskOf, withOutline, halo, ringField, breakup, twinkle, el, ringPx };
if (require.main === module) build();
