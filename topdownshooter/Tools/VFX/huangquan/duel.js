// the end boss's duel (BossDuel, DuelWeapon): the pillar of light each duel weapon comes down in,
// and the three duel weapons' art, written by build.js to Resources/Duel. they're kept clean on
// purpose, fewer and brighter pixels than the evolutions', so the boss's danmaku reads through them
//   the Sun-Shooter's Bow: a slim light arrow for the stream, the sunshot (a sun on an arrow),
//     the sun gathering on the string, its burst and the three-legged crow knocked out of it
//   the Big Dipper Sword Formation: a star-sword, the star each hangs at, its strike, and the
//     Dipper stamped over the boss when all seven meet
//   the Peach Wood Decree: its talisman flying, stuck and burning, flaring as the ring lights,
//     the bagua seal under the ring, the decree's blast, ash and the slap of a talisman sticking
const K = require("./kit");
const { P, D, W, put, bayer, img, TAU, lerp, blob, glowAround } = K;
const { ringField, breakup, easeOut, twinkle } = W;
const burst = require("./fx").burst;

const GOLD = W.GOLD, AZ = W.AZURE, FIRE = W.FIRE;
const PEACH = [[0.1, P.R1], [0.24, P.K1], [0.42, P.K2], [0.62, P.K3], [0.88, P.W]];
const SUN = [[0.08, P.R1], [0.18, P.R2], [0.32, P.O1], [0.48, P.O2], [0.64, P.G2], [0.8, P.G3], [0.93, P.W]];
const outline = im => W.withOutline(im);
function shaded(S, ramp, fill, opts) { const F = D.field(S, S); fill(F); return D.shade(img(S, S), F, ramp, opts); }
function shadedWH(w, h, ramp, fill, opts) { const F = D.field(w, h); fill(F); return D.shade(img(w, h), F, ramp, opts); }

// ================================================================ the pillar
// a column of light slamming down onto the player, its foot the frame's middle (the player's
// middle): a streak falling, the strike, the column standing and swelling with light running up
// it and marks rising, then thinning to a thread and gone in motes; a ring on the ground spreading
function pillar(ramp, markCols, seed) {
  const S = 184, c = S / 2, foot = c + 12, N = 13, out = [];
  const r = D.rng(seed);
  const marks = Array.from({ length: 14 }, () => ({ x: (r() - 0.5) * 26, ph: r(), sp: 0.7 + r() * 0.6 }));
  for (let f = 0; f < N; f++) {
    const im = img(S, S);
    // how wide the column is: a thread falling, the strike, standing, thinning
    const w = f === 0 ? 1.5 : f === 1 ? 2.5 : f === 2 ? 18 : f <= 7 ? 13 - (f - 3) * 0.6 : Math.max(0, 10 - (f - 7) * 2.2);
    const top = f === 0 ? foot - 70 : 6;
    const inten = f <= 2 ? 1 : f <= 7 ? 0.92 - (f - 3) * 0.04 : 0.7 - (f - 8) * 0.13;
    if (w > 0) {
      const F = D.field(S, S);
      D.each(F, (x, y) => {
        if (y < top || y > foot + 2) return 0;
        const d = Math.abs(x - c) / w;
        if (d > 1) return 0;
        // light running up the column, and the top fading into the sky
        const run = 0.8 + 0.35 * W.vnoise(x / 2.5, y / 9 + f * 1.6, seed);
        const fadeTop = Math.min(1, (y - top) / 40);
        return inten * (1 - d * d * 0.75) * run * (0.35 + 0.65 * fadeTop);
      });
      D.over(im, D.shade(img(S, S), F, ramp));
      // the white core
      const core = Math.max(0.5, w * 0.22);
      for (let y = Math.max(top, 0); y <= foot; y++) for (let x = Math.round(c - core); x <= Math.round(c + core); x++)
        if (y > top + 30 || bayer(x, y) < (y - top) / 30) put(im, x, y, P.W);
    }
    // the ground ring spreading from the foot, flattened
    if (f >= 2) {
      const t = (f - 2) / (N - 3), R = 10 + easeOut(t) * 50, th = Math.max(1.5, 5 * (1 - t));
      const Rg = D.field(S, S);
      D.each(Rg, (x, y) => {
        const dx = x - c, dy = (y - foot) / 0.34, d = Math.hypot(dx, dy);
        if (d > R + 0.5 || d <= R - th) return 0;
        if (t > 0.3 && !breakup(R, f + seed, 1.1 - t)(Math.atan2(dy, dx))) return 0;
        return (1 - t * 0.7) * (0.55 + 0.45 * (d - (R - th)) / th);
      });
      D.over(im, D.shade(img(S, S), Rg, ramp));
    }
    // the strike: a flare at the foot
    if (f === 2 || f === 3) {
      const F = D.field(S, S);
      D.flare(F, c, foot, f === 2 ? 30 : 20, 4, f === 2 ? 14 : 8, 2, 1, 0.5);
      D.over(im, D.shade(img(S, S), F, ramp));
    }
    // marks rising up the column: little glints and ticks of script
    if (f >= 3) for (const m of marks) {
      const t = ((f - 3) / 10 * m.sp + m.ph) % 1, y = foot - 6 - t * 84, x = c + m.x * (0.6 + 0.4 * t);
      if (f > 9 && bayer(Math.round(x), Math.round(y)) < (f - 9) / 4) continue;
      if (m.ph < 0.3) twinkle(im, x, y, 1, markCols);
      else { put(im, x, y, markCols[1]); put(im, x, y - 1, markCols[0]); }
    }
    out.push(im);
  }
  return out;
}

// ================================================================ the Sun-Shooter's Bow
// the stream's arrow: slim, light and quick. points right
function sunDart() {
  const out = [];
  for (let f = 0; f < 4; f++) {
    const im = img(34, 15), y = 7;
    for (let x = 6; x <= 21; x++) put(im, x, y, x > 17 ? P.G3 : P.G2);
    for (let x = 9; x <= 19; x++) if ((x + f) % 4 === 0) put(im, x, y, P.W);
    [[22, 0, P.G3], [23, 0, P.W], [24, 0, P.W], [25, 0, P.W], [22, -1, P.G2], [22, 1, P.G2], [23, -1, P.G3], [23, 1, P.G3]].forEach(([x, d, c]) => put(im, x, y + d, c));
    // fletching of light, flicking
    const fl = f % 2;
    [[6, 1], [7, 1], [5, 2], [6, 2]].forEach(([x, d]) => { put(im, x + fl, y - d, P.G1); put(im, x + fl, y + d, P.G1); });
    const lit = outline(im);
    // a faint wake behind
    for (let x = 1; x <= 4; x++) if ((x + f) % 2 === 0) put(lit, x, y, x > 2 ? P.G2 : P.G1);
    out.push(lit);
  }
  return out;
}

// the sunshot: a sun blazing at the head of a golden arrow, a short gold flame streaming back
// off it. gold and white, never red, so it can't be taken for one of the boss's bullets
const SUNLIGHT = [[0.1, P.O1], [0.25, P.O2], [0.45, P.G2], [0.7, P.G3], [0.9, P.W]];
function sunArrow() {
  const out = [], w = 64, h = 29, y = 14, hx = 48;
  for (let f = 0; f < 4; f++) {
    const im = img(w, h);
    // the shaft, two pixels thick, and its fletching
    for (let x = 12; x <= hx - 4; x++) { put(im, x, y, x % 4 === f ? P.W : P.G3); put(im, x, y + 1, P.G1); }
    [[12, -1], [13, -2], [14, -3], [11, -2], [12, -3]].forEach(([x, d]) => { put(im, x, y + d, P.G2); put(im, x, y + 1 - d, P.G2); });
    const lit = outline(im);
    // flame streaming back off the sun, over the front of the shaft
    D.over(lit, shadedWH(w, h, SUNLIGHT, F => {
      D.each(F, (x, yy) => {
        const u = hx - x;
        if (u < 0 || u > 18) return 0;
        const spread = 1.5 + u * 0.2, d = Math.abs(yy - y - 0.5 - Math.sin(u / 3 + f * 1.6) * 0.8) / spread;
        if (d > 1) return 0;
        return (1 - u / 18) * (1 - d) * (0.8 + 0.5 * W.fbm(x / 3 + f * 1.3, yy / 3, 17));
      });
    }));
    // the sun: a white core in a gold disc, its rays turning
    D.over(lit, shadedWH(w, h, SUNLIGHT, F => {
      blob(F, hx, y + 0.5, 6, 6, 1, 0.6);
      for (let k = 0; k < 8; k++) D.ray(F, hx, y + 0.5, k * TAU / 8 + f * TAU / 32, 10 + ((k + f) % 2) * 2, 1.8, 0.85, 0.3);
    }));
    for (let yy = -2; yy <= 2; yy++) for (let x = -2; x <= 2; x++) if (x * x + yy * yy <= 5) put(lit, hx + x, y + yy, P.W);
    out.push(glowAround(lit, 1, P.G1, P.G0, f));
  }
  return out;
}

// the sun gathering on the string: light drawn in from a ring, the disc swelling, rays turning
function sunCharge() {
  const S = 56, c = 28, N = 8, out = [];
  for (let f = 0; f < N; f++) {
    const k = f / (N - 1), im = img(S, S);
    const R = lerp(22, 10, k);
    D.over(im, shaded(S, GOLD, F => ringField(F, c, c, R, 1.6, 0.5 + 0.3 * k, breakup(R, f, 0.75))));
    // motes drawn in
    const r = D.rng(9);
    for (let q = 0; q < 12; q++) {
      const a = r() * TAU + f * 0.3, d = lerp(24, 6, (k + r() * 0.6) % 1);
      put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, d < 12 ? P.W : P.G2);
    }
    D.over(im, shaded(S, SUN, F => {
      blob(F, c, c, 2 + k * 5, 2 + k * 5, 1, 0.5);
      D.flare(F, c, c, 5 + k * 9, 2, 3 + k * 5, 1, 1, 0.4, f * 0.25);
    }));
    out.push(im);
  }
  return out;
}

function sunSpark() {
  const S = 22, c = 11, out = [];
  for (let f = 0; f < 5; f++) {
    const im = img(S, S), t = f / 4;
    const F = D.field(S, S);
    D.flare(F, c, c, 3 + t * 5, 1.5, 2 + t * 2, 1, 1 - t * 0.5, 0.3, t * 0.6);
    D.over(im, D.shade(img(S, S), F, GOLD));
    if (f < 2) put(im, c, c, P.W);
    out.push(im);
  }
  return out;
}

// the sunshot landing: the sun bursting, rays thrown out on its first frames
function sunBurst() {
  const frames = burst({ S: 128, N: 10, R: 18, ringR: 46, seed: 71, ramp: SUN, ringRamp: GOLD, debris: 10, embers: 14,
    hot: [P.W, P.G3, P.O2, P.R2], emberCols: [P.W, P.G3, P.G2, P.O2, P.R2] });
  return frames.map((im, f) => {
    if (f > 3) return im;
    const F = D.field(128, 128);
    for (let k = 0; k < 12; k++) D.ray(F, 64, 64, k * TAU / 12 + 0.13, 30 + f * 8 + (k % 2) * 10, 3 - f * 0.5, 1 - f * 0.2, 0.2);
    const rays = D.shade(img(128, 128), F, SUN);
    return D.over(rays, im);
  });
}

// the three-legged crow, the sun's spirit, knocked out of it: front on with its wings spread the
// way it's painted in the sun's disc, black with the sun's gold still on its edges, a red eye and
// its three legs hanging, beating its wings as it tumbles
function sunCrow() {
  const out = [], S = 44, c = 22;
  const lift = [-9, -4, 2, 6, 2, -4];
  for (let f = 0; f < 6; f++) {
    const M = D.mask(S, S), ty = lift[f];
    W2.ellipse(M, c, c, 3.2, 5);
    W2.ellipse(M, c + 0.5, c - 6, 2.6, 2.4);
    W2.poly(M, [[c + 2, c - 7], [c + 6, c - 6], [c + 2, c - 5]]);
    W2.poly(M, [[c - 2, c + 3], [c + 2, c + 3], [c + 3, c + 7], [c - 3, c + 7]]);
    for (const s of [-1, 1]) {
      W2.poly(M, [[c + s * 1, c - 4], [c + s * 8, c - 4 + ty * 0.6], [c + s * 14, c - 3 + ty], [c + s * 13, c + ty + 1], [c + s * 9, c + 1 + ty * 0.5], [c + s * 2, c + 2]]);
      // the trailing edge in feathers
      for (let k = 0; k < 4; k++) {
        const u = 0.3 + k * 0.2, x = c + s * lerp(2, 13, u), y = lerp(c + 2, c + ty + 1, u);
        W2.poly(M, [[x - 1, y - 1], [x + 1, y - 1], [x + s * 0.5, y + 2]]);
      }
    }
    const im = img(S, S);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      if (!M.m[y * S + x]) continue;
      const up = M.m[(y - 1) * S + x], up2 = M.m[(y - 2) * S + x];
      put(im, x, y, !up ? ((x + f) % 3 === 0 ? P.G3 : P.G2) : !up2 ? P.G0 : y > c + 2 ? P.S0 : P.S1);
    }
    // three legs, hanging
    for (const lx of [c - 2, c, c + 2]) for (let k = 0; k < 3; k++) put(im, lx + (k === 2 ? Math.sign(lx - c) : 0), c + 8 + k, k === 2 ? P.G2 : P.G1);
    put(im, c + 1, c - 7, P.R3);
    put(im, c + 5, c - 6, P.G3);
    const lit = outline(im);
    out.push(D.over(W.halo(lit, P.O1, 0.3, f), lit));
  }
  return out;
}
const W2 = K.K2;

// ================================================================ the Big Dipper Sword Formation
// a star-sword, pointing up: a slim azure blade with a glint running up it, a gold guard with a
// star set in it and a star for a pommel, starlight drifting beside it
function dipperSword() {
  const Wd = 17, H = 36, N = 6, out = [], cx = 8;
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    put(im, cx, 2, P.A5);
    for (let y = 3; y <= 23; y++) { put(im, cx - 1, y, P.A4); put(im, cx, y, y === 3 ? P.W : P.A5); put(im, cx + 1, y, P.A2); }
    // the fuller, a line of stars
    [6, 10, 14, 18].forEach(y => put(im, cx, y, P.A3));
    const gy = 22 - f * 4;
    if (gy >= 3) { put(im, cx - 1, gy, P.W); put(im, cx, gy - 1, P.W); }
    for (let x = cx - 4; x <= cx + 4; x++) put(im, x, 24, Math.abs(x - cx) === 4 ? P.G1 : P.G2);
    put(im, cx - 4, 23, P.G2); put(im, cx + 4, 23, P.G2);
    put(im, cx, 24, f % 2 ? P.W : P.A4);
    for (let y = 25; y <= 28; y++) { put(im, cx - 1, y, P.A1); put(im, cx, y, y % 2 ? P.A2 : P.A1); put(im, cx + 1, y, P.A1); }
    twinkle(im, cx, 30, f % 3 === 0 ? 1 : 0, [P.W, P.A4]);
    put(im, cx, 30, P.W);
    const lit = outline(im);
    // starlight beside the blade
    [[3, 20, 0], [13, 14, 2], [3, 9, 4], [13, 5, 1]].forEach(([x, y0, ph]) => {
      const a = (f + ph) % 6, y = y0 - a;
      if (a < 4) twinkle(lit, x, y, a === 1 ? 1 : 0, a <= 1 ? [P.W, P.A4] : [P.A3]);
    });
    out.push(lit);
  }
  return out;
}

// the star each sword hangs at
function dipperNode() {
  const S = 17, c = 8, out = [];
  const long = [4, 5, 6, 5, 4, 3], diag = [1, 2, 2, 1, 1, 0];
  for (let f = 0; f < 6; f++) {
    const im = img(S, S);
    for (let i = 1; i <= long[f]; i++) [[i, 0], [-i, 0], [0, i], [0, -i]].forEach(([u, v]) => put(im, c + u, c + v, i === 1 ? P.A5 : i <= 3 ? P.A4 : P.A2));
    for (let i = 1; i <= diag[f]; i++) [[i, i], [-i, i], [i, -i], [-i, -i]].forEach(([u, v]) => put(im, c + u, c + v, P.A3));
    put(im, c, c, P.W);
    out.push(im);
  }
  return out;
}

// a sword striking: a cross of light cut into the air, a ring of starlight, sparks
function dipperHit() {
  const S = 56, c = 28, N = 7, out = [];
  for (let f = 0; f < N; f++) {
    const t = f / (N - 1), im = img(S, S);
    if (f <= 4) {
      const F = D.field(S, S), len = 8 + easeOut(t * 1.4) * 16, wd = Math.max(1, 3.5 - f * 0.7);
      for (const a of [0.7, 0.7 + Math.PI, 2.3, 2.3 + Math.PI]) D.ray(F, c, c, a, len, wd, 1 - t * 0.6, 0.3);
      D.over(im, D.shade(img(S, S), F, AZ));
    }
    if (f >= 1) {
      const R = 6 + easeOut(t) * 18;
      D.over(im, shaded(S, AZ, F => ringField(F, c, c, R, Math.max(1.2, 3 * (1 - t)), 0.9 - t * 0.5, t > 0.4 ? breakup(R, f, 1.1 - t) : null)));
    }
    const r = D.rng(13);
    for (let q = 0; q < 9; q++) {
      const a = r() * TAU, d = easeOut(t) * (10 + r() * 14);
      if (bayer(q, f) < t - 0.4) continue;
      put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, t < 0.5 ? P.W : P.A4);
    }
    if (f === 0) twinkle(im, c, c, 2, [P.W, P.W, P.A5]);
    out.push(im);
  }
  return out;
}

// the seven meet: the Dipper stamped over the boss. its stars light one after another down the
// Dipper, the starlight draws between them, it all flashes, and a ring throws it off
function dipperSeal() {
  const S = 150, c = 75, N = 13, sc = 30, out = [];
  // the same Dipper the swords hang in (DipperFormation.Stars), y up there, down here
  const stars = [[1.1, 0.5], [1.02, -0.18], [0.36, -0.32], [0.3, 0.32], [-0.33, 0.46], [-0.94, 0.55], [-1.5, 0.26]]
    .map(([x, y]) => [c + (x + 0.2) * sc, c - (y - 0.15) * sc]);
  const lines = [[0, 1], [1, 2], [2, 3], [3, 0], [3, 4], [4, 5], [5, 6]];
  for (let f = 0; f < N; f++) {
    const im = img(S, S), flash = f === 7 || f === 8, fade = f > 8 ? (f - 8) / 4 : 0;
    // the starlight between them, drawing in behind the stars
    lines.forEach(([a, b], i) => {
      const k = Math.min(1, Math.max(0, (f - 1 - i * 0.8) / 1.5));
      if (k <= 0) return;
      const [x0, y0] = stars[a], [x1, y1] = stars[b], n = Math.ceil(Math.hypot(x1 - x0, y1 - y0));
      for (let s = 0; s <= n * k; s++) {
        const x = lerp(x0, x1, s / n), y = lerp(y0, y1, s / n);
        if (fade && bayer(Math.round(x), Math.round(y)) < fade) continue;
        put(im, x, y, flash ? P.W : P.A4);
        if (flash || f >= 6) { put(im, x, y - 1, P.A2); put(im, x, y + 1, P.A2); }
      }
    });
    stars.forEach(([x, y], i) => {
      if (f < i * 0.8) return;
      if (fade && bayer(i, f) < fade) return;
      const F = D.field(S, S), big = flash ? 12 : f - i * 0.8 < 1.5 ? 9 : 6;
      D.flare(F, x, y, big, 2, big * 0.5, 1, 1, 0.4);
      D.over(im, D.shade(img(S, S), F, AZ));
      put(im, x, y, P.W);
    });
    if (f >= 8) {
      const t = (f - 8) / 4, R = 40 + easeOut(t) * 28;
      D.over(im, shaded(S, AZ, F => ringField(F, c, c, R, Math.max(1.5, 4 * (1 - t)), 1 - t * 0.6, t > 0.3 ? breakup(R, f, 1.1 - t) : null)));
    }
    out.push(im);
  }
  return out;
}

// ================================================================ the Peach Wood Decree
// 敕, the decree's character, in a few pixels: its left side a stack of strokes, its right a
// sweep, the way the talismans' own glyph (weapons.js) is abstracted
const CHI = ["#.#.#..", "#####.#", "..#..#.", "####.##", "#..#.#.", "####..#", "..#.#.#", ".#.#..#", "#..#.#."];
// the decree's talisman: gold paper with a red and gold double border, 敕 in red ink, a peach
// blossom at its head and a red tassel. glyphCol lights the glyph
function decreePaper(glyphCol = null, paperCols = [P.G3, P.G2]) {
  const im = img(28, 42);
  for (let y = 5; y <= 27; y++) for (let x = 5; x <= 17; x++) put(im, x, y, x === 17 || y === 27 ? paperCols[1] : paperCols[0]);
  for (let y = 6; y <= 26; y++) { put(im, 6, y, P.R2); put(im, 16, y, P.R2); }
  for (let x = 6; x <= 16; x++) { put(im, x, 6, P.R2); put(im, x, 26, P.R2); }
  for (let y = 8; y <= 24; y++) { put(im, 7, y, P.G1); put(im, 15, y, P.G1); }
  // the peach blossom at its head
  [[11, 8, P.K2], [10, 9, P.K2], [12, 9, P.K2], [11, 10, P.K2], [11, 9, P.G2]].forEach(([x, y, c]) => put(im, x, y, c));
  CHI.forEach((row, j) => [...row].forEach((ch, i) => { if (ch === "#") put(im, 8 + i, 12 + j, glyphCol ? glyphCol((i + j) % 2) : j % 3 === 0 ? P.R2 : P.R1); }));
  return im;
}
function sway(im, amount) {
  const out = img(im.w, im.h);
  for (let y = 0; y < im.h; y++) {
    const off = y > 16 ? Math.round(amount * (y - 16) / 11) : 0;
    for (let x = 0; x < im.w; x++) { const c = D.get(im, x, y); if (c) put(out, x + off, y, c); }
  }
  return out;
}
function tasselOn(im, f, sw) {
  for (let k = 0; k < 5; k++) {
    const x = 11 + sw + Math.round([0, 1, 2, 1, 0, -1][f % 6] * k / 4);
    put(im, x, 28 + k, k === 0 ? P.G2 : P.R2);
    if (k >= 3) put(im, x + 1, 28 + k, P.R1);
  }
}

function decreeTalisman() {
  const out = [], sw = [0, 1, 1, 0, -1, -1];
  for (let f = 0; f < 6; f++) {
    const im = sway(decreePaper(), sw[f]);
    tasselOn(im, f, sw[f]);
    const lit = outline(im);
    // a spark of the decree running round its edge
    const edge = [[5, 5], [17, 7], [18, 16], [17, 25], [5, 26], [4, 15]][f];
    twinkle(lit, edge[0] + (edge[1] > 16 ? sw[f] : 0), edge[1], f % 2 ? 0 : 1, [P.W, P.G3]);
    out.push(lit);
  }
  return out;
}

// stuck and burning: fire licking up its lower edge, the glyph smouldering
function flames(im, f, height, cols) {
  for (let x = 5; x <= 17; x++) {
    const h = Math.round(height * (0.5 + 0.5 * W.vnoise(x / 2, f * 1.7, 41)));
    for (let k = 0; k < h; k++) {
      const y = 27 - k;
      if (k > h - 2 && bayer(x, y + f) < 0.5) continue;
      put(im, x, y, cols[Math.min(cols.length - 1, Math.floor(k / h * cols.length))]);
    }
  }
}
function decreeBurn() {
  const out = [];
  for (let f = 0; f < 4; f++) {
    const im = decreePaper(k => f % 2 ? (k ? P.O2 : P.R2) : (k ? P.G3 : P.O2));
    tasselOn(im, f, 0);
    flames(im, f, 8, [P.W, P.G3, P.O2, P.O1, P.R2]);
    const lit = outline(im);
    const r = D.rng(50 + f);
    for (let q = 0; q < 3; q++) put(lit, 6 + r() * 12, 20 - r() * 14, q ? P.O2 : P.G3);
    out.push(lit);
  }
  return out;
}
// the ring lit: the talisman white-hot, its glyph blazing, fire all up it
function decreeFlare() {
  const out = [];
  for (let f = 0; f < 4; f++) {
    const im = decreePaper(k => k ? P.W : P.G3, [P.W, P.G3]);
    tasselOn(im, f, 0);
    flames(im, f + 2, 12, [P.W, P.G3, P.O2, P.O1]);
    out.push(glowAround(outline(im), 2, P.O2, P.R2, f));
  }
  return out;
}

// the bagua seal under the ring: an octagon of the eight trigrams round a taiji, drawing itself
// in as it turns, brightening until the decree goes off
function decreeSeal() {
  const S = 124, c = 62, N = 10, out = [];
  // the eight trigrams, top line first: 1 whole, 0 broken
  const TRI = ["111", "011", "101", "001", "110", "010", "100", "000"];
  const RING = [[0.12, P.R1], [0.3, P.R2], [0.55, P.O2], [0.8, P.G3], [0.95, P.W]];
  for (let f = 0; f < N; f++) {
    const k = f / (N - 1), im = img(S, S), rot = (1 - easeOut(Math.min(1, k * 1.4))) * 0.8, pk = 0.55 + 0.45 * k;
    const draw = Math.min(1, k * 2);
    D.over(im, shaded(S, RING, F => {
      ringField(F, c, c, 52, 2, pk, a => ((a + Math.PI) / TAU) < draw);
      ringField(F, c, c, 30, 1.5, pk * 0.9, a => ((a + Math.PI) / TAU) < draw);
    }));
    // the octagon between the rings and the trigrams on its sides
    for (let s = 0; s < 8; s++) {
      if (s / 8 > draw + 0.05) continue;
      const a = rot + s * TAU / 8 - Math.PI / 2, a2 = rot + (s + 1) * TAU / 8 - Math.PI / 2;
      const Ro = 45;
      const n = 30;
      for (let q = 0; q <= n; q++) put(im, c + lerp(Math.cos(a - TAU / 16), Math.cos(a2 - TAU / 16), q / n) * Ro, c + lerp(Math.sin(a - TAU / 16), Math.sin(a2 - TAU / 16), q / n) * Ro, k > 0.7 ? P.G3 : P.G2);
      const tri = TRI[s], ux = Math.cos(a), uy = Math.sin(a), vx = -uy, vy = ux;
      for (let l = 0; l < 3; l++) {
        const R = 40 - l * 3;
        for (let t = -5; t <= 5; t++) {
          if (tri[l] === "0" && Math.abs(t) <= 1) continue;
          put(im, c + ux * R + vx * t, c + uy * R + vy * t, k > 0.8 ? P.W : P.G3);
        }
      }
    }
    // the taiji in the middle
    if (k > 0.3) for (let y = -10; y <= 10; y++) for (let x = -10; x <= 10; x++) {
      const d = Math.hypot(x, y);
      if (d > 10) continue;
      const ca = Math.cos(rot * 2), sa = Math.sin(rot * 2), u = x * ca + y * sa, v = -x * sa + y * ca;
      let dark = u < 0;
      if (Math.hypot(u, v - 5) < 5) dark = false;
      if (Math.hypot(u, v + 5) < 5) dark = true;
      if (Math.hypot(u, v - 5) < 1.6) dark = true;
      if (Math.hypot(u, v + 5) < 1.6) dark = false;
      if (d > 9) put(im, c + x, c + y, P.G2);
      else put(im, c + x, c + y, dark ? P.R1 : (k > 0.8 ? P.W : P.G3));
    }
    out.push(im);
  }
  return out;
}

const PETAL = [P.K3, P.K2, P.K1];
function decreeBlast() {
  const frames = burst({ S: 172, N: 10, R: 22, ringR: 60, seed: 83, ramp: FIRE, ringRamp: PEACH, debris: 12, embers: 16,
    emberCols: [P.W, P.K3, P.K2, P.G3, P.O2], smoke: [[0.05, P.S1], [0.09, P.S2], [0.13, P.S3]] });
  // peach petals thrown out with it
  const r = D.rng(84);
  const petals = Array.from({ length: 14 }, () => ({ a: r() * TAU, s: 0.5 + r() * 0.7, spin: r() * 4 }));
  return frames.map((im, f) => {
    if (f === 0) return im;
    const t = f / 9;
    for (const p of petals) {
      const d = 14 + easeOut(t) * 50 * p.s, x = 86 + Math.cos(p.a) * d, y = 86 + Math.sin(p.a) * d + t * t * 10;
      if (bayer(Math.round(x), Math.round(y)) < t - 0.5) continue;
      const turn = Math.floor(p.spin + f) % 2;
      put(im, x, y, PETAL[0]); put(im, x + (turn ? 1 : 0), y + (turn ? 0 : 1), PETAL[1]); put(im, x + 1, y + 1, PETAL[2]);
    }
    return im;
  });
}

function decreeAsh() {
  const S = 44, c = 22, out = [];
  const r = D.rng(19);
  const flakes = Array.from({ length: 12 }, () => ({ x: (r() - 0.5) * 10, y: (r() - 0.5) * 14, rise: 0.8 + r() * 0.8, drift: (r() - 0.5) * 1.2, ember: r() < 0.3 }));
  for (let f = 0; f < 7; f++) {
    const im = img(S, S), t = f / 6;
    for (const fl of flakes) {
      const x = c + fl.x + fl.drift * f, y = c + fl.y - fl.rise * f * 1.2;
      if (bayer(Math.round(x), Math.round(y)) < t - 0.3) continue;
      put(im, x, y, fl.ember ? (f < 3 ? P.O2 : P.R2) : (f < 3 ? P.AS3 : P.AS2));
      if (!fl.ember && f < 4) put(im, x + 1, y, P.AS1);
    }
    out.push(im);
  }
  return out;
}

function decreeSpark() {
  const S = 28, c = 14, out = [];
  for (let f = 0; f < 5; f++) {
    const im = img(S, S), t = f / 4, r = D.rng(27);
    for (let q = 0; q < 10; q++) {
      const a = r() * TAU, d = 2 + easeOut(t) * (6 + r() * 6);
      for (let s = 0; s < 3 - f * 0.6; s++) put(im, c + Math.cos(a) * (d - s), c + Math.sin(a) * (d - s), s === 0 ? (t < 0.5 ? P.W : P.G3) : P.O2);
    }
    if (f < 2) twinkle(im, c, c, 1, [P.W, P.G3]);
    out.push(im);
  }
  return out;
}

function makeDuel() {
  return {
    duel_pillar_gold: pillar(GOLD, [P.W, P.G2], 3),
    duel_pillar_azure: pillar(AZ, [P.W, P.A4], 5),
    duel_pillar_peach: pillar(PEACH, [P.W, P.K2], 7),
    sun_dart: sunDart(),
    sun_arrow: sunArrow(),
    sun_charge: sunCharge(),
    sun_spark: sunSpark(),
    sun_burst: sunBurst(),
    sun_crow: sunCrow(),
    dipper_sword: dipperSword(),
    dipper_node: dipperNode(),
    dipper_hit: dipperHit(),
    dipper_seal: dipperSeal(),
    decree_talisman: decreeTalisman(),
    decree_burn: decreeBurn(),
    decree_flare: decreeFlare(),
    decree_seal: decreeSeal(),
    decree_blast: decreeBlast(),
    decree_ash: decreeAsh(),
    decree_spark: decreeSpark(),
  };
}

module.exports = { makeDuel };
