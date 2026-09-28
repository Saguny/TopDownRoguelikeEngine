// the menus' flair, in the weapons' style (their palette, ramps, ordered dithering, plum outlines):
// the pointer, the stars that trace a button's edge while it's hovered, and the burst a click makes.
// written straight to Resources/UI as PNGs (the frames of an animation side by side, square), which
// the game slices at run time (UiFlair), so there's nothing to set up in Unity:
//   node ui.js
//   ui_cursor, ui_cursor_down   the pointer, drawn at 16px and written at 2x (32x32), its tip at (2, 2)
//   ui_star                     a four-point star twinkling, 9x9, 6 frames
//   ui_click                    a click's burst, 40x40, 8 frames: a flare, a broken ring, sparks
// out/sheet_*.png are previews
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { put, bayer, hex } = D;
const { GOLD, TAU, easeOut, lerp, img, hash2, withOutline, ringField, breakup, twinkle } = W;
const { mask, poly } = K2;
const P = K2.P;
const OUT = P.V0;
const DEST = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources", "UI");

// ---------------------------------------------------------------- the pointer

// a gold arrowhead with a lacquered edge: lit along its leading edge, a jade inlay down its spine,
// a red seal knotted at its tail. pressed, it dips a pixel and its inlay lights up
function cursor(pressed) {
  const S = 16, im = img(S, S);
  const M = poly(mask(S, S), [[1, 1], [1, 13.5], [4.2, 10.6], [6.6, 15], [8.6, 14], [6.3, 9.6], [10.8, 9.6]]);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    if (!D.mget(M, x, y)) continue;
    // lit from the upper left: the leading edges bright, the far side falling off
    const edge = !D.mget(M, x - 1, y) || !D.mget(M, x, y - 1);
    const far = !D.mget(M, x + 1, y) || !D.mget(M, x, y + 1);
    let c = P.G2;
    if (edge) c = P.G3;
    else if (far) c = P.G1;
    put(im, x, y, c);
  }
  // the jade inlay: a single line down the spine, from near the tip toward the tail
  for (let y = 4; y <= 9; y++) put(im, 3, y, pressed ? P.J5 : y < 6 ? P.J4 : P.J3);
  put(im, 4, 8, pressed ? P.J5 : P.J3);
  // the seal knotted at the tail
  put(im, 7, 13, P.R2); put(im, 7, 12, P.R3);
  let out = withOutline(im, OUT);
  if (pressed) {
    // pressed: pushed a pixel in, the tip flashing
    const moved = img(S, S);
    D.blit(moved, out, 1, 1);
    out = moved;
    put(out, 1, 1, P.W);
    put(out, 0, 1, P.G3); put(out, 1, 0, P.G3);
  }
  return out;
}

// ---------------------------------------------------------------- the stars

// a four-point star, 9x9: its arms grow out of a white core and draw back in, the diagonals
// flicking out at its brightest, a dithered glow round it
function star(f, N) {
  const S = 9, c = 4, im = img(S, S);
  const k = Math.sin((f / N) * Math.PI);                 // 0 → 1 → 0 over the loop
  const arm = Math.round(1 + k * 3);
  // thin arms fading out along their length: white at the core, gold, then deep gold at the tips
  for (let i = 1; i <= arm; i++) {
    const col = i === 1 ? P.G3 : i === arm ? P.G1 : P.G2;
    put(im, c + i, c, col); put(im, c - i, c, col); put(im, c, c + i, col); put(im, c, c - i, col);
  }
  // at its brightest, short diagonals and a faint dithered halo
  if (k > 0.8) {
    [[1, 1], [-1, 1], [1, -1], [-1, -1]].forEach(([u, v]) => put(im, c + u, c + v, P.G2));
    [[2, 2], [-2, 2], [2, -2], [-2, -2]].forEach(([u, v]) => put(im, c + u, c + v, P.G1));
  }
  put(im, c, c, P.W);
  if (arm >= 3) { put(im, c + 1, c, P.W); put(im, c - 1, c, P.G3); }
  return im;
}

// ---------------------------------------------------------------- the click

// a click's burst, 40x40: a white-hot flare, a gold ring breaking into dashes as it spreads, sparks
// thrown out and falling, all dithering away
function click(f, N) {
  const S = 40, c = S / 2, t = f / (N - 1), im = img(S, S);
  if (f <= 2) {
    const F = D.field(S, S);
    D.flare(F, c, c, [6, 11, 7][f], [3, 2, 2][f], [0, 5, 3][f], 1, 1, 0.4, 0);
    D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < [3, 3.5, 2][f] ? 1 : 0; });
    D.over(im, D.shade(img(S, S), F, GOLD));
  }
  if (f >= 1) {
    const R = lerp(3, 17, easeOut((f - 1) / (N - 2)));
    const F = D.field(S, S);
    ringField(F, c, c, R, lerp(2.2, 1, t), lerp(0.95, 0.35, t), f > 2 ? breakup(R, 4, lerp(0.85, 0.35, t)) : null);
    D.over(im, D.shade(img(S, S), F, GOLD));
  }
  for (let k = 0; k < 8; k++) {
    const a = (k / 8) * TAU + hash2(k, 1, 7) * 0.5, v = 0.6 + hash2(k, 2, 7) * 0.5;
    const d = lerp(3, 18, easeOut(t)) * v, x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + t * t * 3;
    if (f > 0 && bayer(Math.round(x), Math.round(y)) < 1.1 - t) twinkle(im, x, y, f < 3 ? 1 : 0, [P.W, k % 2 ? P.G2 : P.R3]);
  }
  return im;
}

// ---------------------------------------------------------------- write

function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}
function scale2(im) {
  const out = img(im.w * 2, im.h * 2);
  for (let y = 0; y < out.h; y++) for (let x = 0; x < out.w; x++) { const c = D.get(im, x >> 1, y >> 1); if (c) put(out, x, y, c); }
  return out;
}

function build() {
  fs.mkdirSync(DEST, { recursive: true });
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
  const items = {
    ui_cursor: [scale2(cursor(false))],
    ui_cursor_down: [scale2(cursor(true))],
    ui_star: Array.from({ length: 6 }, (_, f) => star(f, 6)),
    ui_click: Array.from({ length: 8 }, (_, f) => click(f, 8)),
  };
  for (const [name, frames] of Object.entries(items)) {
    png.encode(frames.length === 1 ? frames[0] : strip(frames), path.join(DEST, name + ".png"));
    png.encode(png.preview(frames, frames[0].w <= 16 ? 10 : 6, [22, 18, 30], frames.length, 2), path.join(preview, `sheet_${name}.png`));
    console.log(`${name} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
}
if (require.main === module) build();
