// the end of a Final Rush: the spirit seal's shockwave that rolls out from the player and seals
// every enemy it passes, in the Command Token's and the arena seals' language (gold talisman light,
// red seal stamps, a white-hot flare, dashes breaking up, plum-free because it's all light).
// written straight to Resources/FinalRush as PNG strips of square frames, which the game slices at
// run time (SealWave), so there's nothing to set up in Unity:
//   node rush.js
//   rush_dash     one dash of the wave's front, lying along it (tangent = x), 16x16, 4 frames: a
//                 white-hot core in gold with red flecks trailing, flickering
//   rush_seal     a small red seal riding the front between the dashes, 12x12, 4 frames
//   rush_burst    an enemy sealed as the front passes it, 40x40, 7 frames: a gold flare, a red seal
//                 stamped on it, the stamp cracking into gold dashes
//   rush_origin   where it starts, round the player, 96x96, 8 frames: a white-gold flare with long
//                 thin rays, a ring snapping out and breaking into dashes, sparks
// out/sheet_*.png are previews
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { put, bayer } = D;
const { GOLD, TAU, easeOut, lerp, img, hash2, ringField, breakup, twinkle } = W;
const { px } = K2;
const P = K2.P;
const DEST = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources", "FinalRush");
const SEAL_RED = [[0.1, P.R0], [0.3, P.R1], [0.55, P.R2], [0.8, P.R3], [0.95, P.W]];

// one dash of the front, lying along x. the game lays the sprite's +y outward, the way the wave
// rolls, and a PNG's top row is +y: so the bright leading edge is on top, the white core under it,
// gold falling off below and red flecks shed behind (lower). it tapers to points at its ends
function dash(f) {
  const S = 16, c = 7, im = img(S, S);
  const len = [12, 11, 12, 10][f], x0 = Math.round(c + 1 - len / 2);
  for (let i = 0; i < len; i++) {
    const x = x0 + i, e = Math.min(i, len - 1 - i);                    // how far from an end
    if (e >= 1) put(im, x, c - 1, e >= 2 ? P.G3 : P.G2);              // the leading edge
    put(im, x, c, e === 0 ? P.G2 : (i + f * 2) % 6 === 0 ? P.G3 : P.W);   // the hot core, a glint running
    if (e >= 2) put(im, x, c + 1, P.G2);
    if (e >= 3 && bayer(x + f, c + 2) < 0.6) put(im, x, c + 2, P.G1);
  }
  // red flecks shed behind it
  const r = D.rng(40 + f);
  for (let k = 0; k < 3; k++) put(im, x0 + 2 + r() * (len - 4), c + 3 + Math.floor(r() * 2), k % 2 ? P.R2 : P.R3);
  return im;
}

// 王 on a seal: three bars and a stroke through them, in a box from (x0, y0), 4 wide and 5 tall
function wang(im, x0, y0, col) {
  for (let x = x0; x <= x0 + 3; x++) { put(im, x, y0, col); put(im, x, y0 + 2, col); put(im, x, y0 + 4, col); }
  put(im, x0 + 1, y0 + 1, col); put(im, x0 + 1, y0 + 3, col); put(im, x0 + 2, y0 + 1, col); put(im, x0 + 2, y0 + 3, col);
}

// a small seal stamp riding the front: a red square with a gold rim and a white glyph, its glow
// breathing
function seal(f) {
  const S = 12, im = img(S, S), glow = [0, 1, 1, 0][f];
  for (let y = 2; y <= 9; y++) for (let x = 2; x <= 9; x++) {
    const rim = x === 2 || x === 9 || y === 2 || y === 9;
    put(im, x, y, rim ? (glow ? P.G3 : P.G2) : (x + y) % 5 === 0 ? P.R3 : P.R2);
  }
  wang(im, 4, 4, glow ? P.W : P.G3);
  if (glow) for (const [x, y] of [[1, 5], [10, 6], [5, 1], [6, 10]]) put(im, x, y, P.G2);
  return im;
}

// an enemy sealed: a gold flare, a red stamp slammed on and flashing, then cracking into dashes
function burst(f, N) {
  const S = 40, c = 20, im = img(S, S);
  if (f === 0) return K2.flareFrame(S, c, 12, GOLD, 5);
  if (f <= 2) {
    // the stamp, landing big and settling
    const half = [0, 7, 5][f];
    for (let y = c - half; y <= c + half; y++) for (let x = c - half; x <= c + half; x++) {
      const rim = Math.abs(x - c) === half || Math.abs(y - c) === half;
      put(im, x, y, rim ? (f === 1 ? P.W : P.G3) : f === 1 ? P.R3 : P.R2);
    }
    wang(im, c - 2, c - 2, P.W);
    const F = D.field(S, S);
    ringField(F, c, c, half + 3 + f, 1.4, 0.8, null);
    D.over(im, D.shade(img(S, S), F, GOLD));
    return im;
  }
  // it cracks: pieces of the stamp and gold dashes flying out, a ring breaking up
  const t = (f - 2) / (N - 3), R = lerp(8, 17, easeOut(t));
  const F = D.field(S, S);
  ringField(F, c, c, R, lerp(1.5, 0.8, t), lerp(0.8, 0.3, t), breakup(R, f + 7, lerp(0.7, 0.35, t)));
  D.shade(im, F, GOLD);
  for (let k = 0; k < 6; k++) {
    const a = k / 6 * TAU + 0.4, d = lerp(5, 15, easeOut(t)) * (0.8 + hash2(k, 3, 9) * 0.4);
    const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
    if (bayer(Math.round(x), Math.round(y)) > 1.15 - t) continue;
    put(im, x, y, k % 2 ? P.R2 : P.R3); put(im, x + Math.cos(a), y + Math.sin(a), k % 2 ? P.G2 : P.W);
  }
  return im;
}

// where it starts: a white-gold flare with long thin rays, a ring snapping out and breaking into
// dashes, sparks thrown
function origin(f, N) {
  const S = 96, c = 48, im = img(S, S);
  if (f <= 1) {
    const F = D.field(S, S);
    D.flare(F, c, c, [40, 46][f], [4, 3][f], [22, 26][f], 1.5, 1, 0.35);
    for (let k = 0; k < 12; k++) D.ray(F, c, c, k / 12 * TAU + 0.13, 18 + (k % 3) * 6, 1.2, 0.7, 0.3);
    D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < [9, 7][f] ? 1 : d < [12, 10][f] ? 0.75 : 0; });
    D.shade(im, F, GOLD);
    return im;
  }
  const t = (f - 2) / (N - 3), R = lerp(14, 44, easeOut(t));
  const F = D.field(S, S);
  ringField(F, c, c, R, lerp(3, 1, t), lerp(0.95, 0.3, t), f > 3 ? breakup(R, f, lerp(0.8, 0.35, t)) : null);
  D.shade(im, F, GOLD);
  const F2 = D.field(S, S);
  ringField(F2, c, c, R * 0.7, 1.2, lerp(0.7, 0.2, t), breakup(R * 0.7, f + 20, 0.5));
  D.over(im, D.shade(img(S, S), F2, SEAL_RED));
  for (let k = 0; k < 14; k++) {
    const a = k / 14 * TAU + hash2(k, 1, 5), d = (R + 4) * (0.7 + hash2(k, 2, 5) * 0.5);
    const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
    if (bayer(Math.round(x), Math.round(y)) < 1.05 - t) twinkle(im, x, y, t < 0.4 ? 1 : 0, [P.W, k % 3 ? P.G3 : P.R3]);
  }
  return im;
}

// ---------------------------------------------------------------- write

function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}

function build() {
  fs.mkdirSync(DEST, { recursive: true });
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
  const items = {
    rush_dash: Array.from({ length: 4 }, (_, f) => dash(f)),
    rush_seal: Array.from({ length: 4 }, (_, f) => seal(f)),
    rush_burst: Array.from({ length: 7 }, (_, f) => burst(f, 7)),
    rush_origin: Array.from({ length: 8 }, (_, f) => origin(f, 8)),
  };
  for (const [name, frames] of Object.entries(items)) {
    png.encode(strip(frames), path.join(DEST, name + ".png"));
    png.encode(png.preview(frames, frames[0].w <= 16 ? 10 : frames[0].w <= 48 ? 6 : 3, [22, 18, 30], frames.length, 2), path.join(preview, `sheet_${name}.png`));
    console.log(`${name} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
}
if (require.main === module) build();
