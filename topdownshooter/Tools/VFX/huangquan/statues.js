// the guardians' statues by Huangquan Road: each is the guardian himself turned to weathered stone
// (his own lit sprite, its light kept and its colour gone to grey-violet stone, moss in the hollows
// that face up, the eyes still faintly lit) on a carved plinth with a red lacquered tablet, three
// sticks of incense smoking before it. drawn square, the statue standing in the middle, so its
// centre is the statue's centre in the playfield:
//   statue_ox / statue_horse               idle 4 (the eyes' ember breathing, the incense smoking)
//   statue_ox_wake / statue_horse_wake     8 (cracks racing over it, red light out of them, a
//                                          flash, the shell gone: only the stumps of its feet stay)
//   statue_ox_empty / statue_horse_empty   4 (the plinth and the broken stumps, the cracks still
//                                          warm, dust in the air)
const K = require("./kit");
const { P, D, W, put, bayer, img, TAU } = K;

const S = 144, PLINTH_TOP = 112;
const STONE = [P.ST0, P.ST1, P.ST2, P.ST3, P.ST4];

// the plinth: a slab, a body with a red tablet on its face, a wider foot
function plinth(im) {
  const slab = [26, PLINTH_TOP, 118, PLINTH_TOP + 6], body = [32, PLINTH_TOP + 6, 112, PLINTH_TOP + 24], foot = [24, PLINTH_TOP + 24, 120, PLINTH_TOP + 29];
  for (const [x0, y0, x1, y1] of [slab, body, foot]) for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
    const edge = x === x0 || x === x1 || y === y0 || y === y1;
    const lit = y === y0 + 1 || x === x1 - 1;
    const n = W.fbm(x / 5, y / 5, 7);
    put(im, x, y, edge ? P.ST0 : lit ? P.ST4 : n > 0.62 ? P.ST3 : n < 0.35 ? P.ST1 : P.ST2);
  }
  // the red lacquered tablet, a gold border and its script
  for (let y = PLINTH_TOP + 9; y <= PLINTH_TOP + 21; y++) for (let x = 60; x <= 84; x++) {
    const edge = x === 60 || x === 84 || y === PLINTH_TOP + 9 || y === PLINTH_TOP + 21;
    put(im, x, y, edge ? P.G1 : P.R1);
  }
  for (let k = 0; k < 4; k++) for (let y = PLINTH_TOP + 12; y <= PLINTH_TOP + 18; y++) if ((y + k) % 3) put(im, 64 + k * 5, y, P.G2);
  // moss creeping up its foot
  for (let x = 24; x <= 120; x++) { const h = Math.round(W.fbm(x / 4, 1, 8) * 4 - 1); for (let y = 0; y < h; y++) put(im, x, PLINTH_TOP + 28 - y, y === h - 1 ? P.MS2 : P.MS1); }
}

// a lit sprite turned to stone: its light kept as stone's, moss where it faces up, cracks
function petrify(src, eyes, seed) {
  const out = img(src.w, src.h);
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
    const i = (y * src.w + x) * 4;
    if (!src.data[i + 3]) continue;
    const r = src.data[i], g = src.data[i + 1], b = src.data[i + 2];
    const lum = (r * 0.3 + g * 0.59 + b * 0.11) / 255;
    let k = lum < 0.1 ? 0 : lum < 0.22 ? 1 : lum < 0.38 ? 2 : lum < 0.58 ? 3 : 4;
    if (k > 0 && k < 4 && bayer(x, y) < ((lum * 10) % 1) * 0.5) k++;
    let c = STONE[k];
    // moss in the upward-facing hollows
    const up = y > 0 && !src.data[((y - 1) * src.w + x) * 4 + 3];
    if (!up && k >= 1 && k <= 2 && W.fbm(x / 3, y / 3, seed) > 0.66) c = W.hash2(x, y, seed) < 0.5 ? P.MS1 : P.MS2;
    put(out, x, y, c);
  }
  // weathering: a few hairline cracks
  const r = D.rng(seed);
  for (let k = 0; k < 5; k++) {
    let x = 20 + r() * (src.w - 40), y = 20 + r() * (src.h - 40);
    for (let s = 0; s < 8; s++) {
      if (out.data[(Math.round(y) * src.w + Math.round(x)) * 4 + 3]) put(out, x, y, P.ST0);
      x += (r() - 0.5) * 3; y += r() * 2;
    }
  }
  return { im: out, eyes };
}

// the incense before it: three sticks with red tips and smoke curling up
function incense(im, f) {
  for (const x of [66, 72, 78]) {
    for (let y = PLINTH_TOP - 8; y < PLINTH_TOP; y++) put(im, x, y, P.WD1);
    put(im, x, PLINTH_TOP - 9, (f + x) % 2 ? P.R3 : P.O2);
    for (let k = 0; k < 7; k++) {
      const t = ((f / 4) + k / 7) % 1, y = PLINTH_TOP - 11 - t * 26, sx = x + Math.sin(t * 7 + x) * 2.5 * t;
      if (bayer(Math.round(sx), Math.round(y)) < t * 0.9) continue;
      put(im, sx, y, t < 0.4 ? P.AS3 : P.AS2);
    }
  }
  // the bowl they stand in
  for (let x = 63; x <= 81; x++) { put(im, x, PLINTH_TOP - 1, P.G1); put(im, x, PLINTH_TOP - 2, x === 63 || x === 81 ? P.G0 : P.G2); }
}

// the guardian on his plinth: his sprite centred over it, feet on the slab
function place(stone, feetRow) {
  const im = img(S, S);
  const ox = Math.round(S / 2 - stone.w / 2), oy = PLINTH_TOP - feetRow;
  D.blit(im, stone, ox, oy);
  return { im, ox, oy };
}

// where it stands: the lowest row solid enough to be feet (not a spear's butt or a wisp of mist)
function feet(src) {
  for (let y = src.h - 1; y >= 0; y--) {
    let n = 0;
    for (let x = 0; x < src.w; x++) if (src.data[(y * src.w + x) * 4 + 3]) n++;
    if (n >= 10) return y;
  }
  return src.h - 1;
}

function makeStatue(kind, sprite, eyeAt, seed) {
  const src = K.fit([sprite])[0];
  const { im: stone } = petrify(src, eyeAt, seed);
  const row = feet(src);
  const idle = [], wake = [], empty = [];
  const eyeCol = kind === "ox" ? [P.R1, P.R2, P.R3, P.R2] : [P.A1, P.A2, P.A3, P.A2];

  const base = img(S, S);
  plinth(base);
  const { ox, oy } = place(stone, row);
  const eye = [ox + eyeAt[0] - Math.round((sprite.w - src.w) / 2), oy + eyeAt[1] - Math.round((sprite.h - src.h) / 2)];

  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    D.over(im, base);
    D.blit(im, stone, ox, oy);
    incense(im, f);
    put(im, eye[0], eye[1], eyeCol[f]); put(im, eye[0] + 1, eye[1], eyeCol[Math.max(0, f - 1)]);
    idle.push(im);
  }

  // cracks racing over it, then the shell bursting off: only the stumps of the feet stay
  const r = D.rng(seed + 5), cracks = [];
  for (let k = 0; k < 14; k++) {
    const pts = [[eye[0] + (r() - 0.5) * 30, eye[1] + r() * 50]];
    for (let s = 0; s < 6; s++) { const [x, y] = pts[pts.length - 1]; pts.push([x + (r() - 0.5) * 10, y + (r() - 0.3) * 9]); }
    cracks.push(pts);
  }
  const stumps = img(S, S);
  for (let y = row - 8; y <= row; y++) for (let x = 0; x < src.w; x++) {
    const i = (y * src.w + x) * 4;
    if (!stone.data[i + 3]) continue;
    const jag = row - 8 + Math.round(W.hash2(x, 1, seed) * 5);
    if (y < jag) continue;
    put(stumps, ox + x, oy + y, y === jag ? P.ST4 : [P.ST1, P.ST2, P.ST3, P.ST2][(stone.data[i] >> 5) & 3]);
  }
  for (let f = 0; f < 8; f++) {
    const im = img(S, S);
    D.over(im, base);
    if (f < 3) {
      D.blit(im, stone, ox, oy);
      const glow = img(S, S);
      cracks.slice(0, 5 + f * 5).forEach(pts => {
        const M2 = D.polyline(D.mask(S, S), pts.slice(0, 3 + f * 2));
        D.paint(glow, M2, f === 2 ? P.W : P.R3);
      });
      // only where there's stone to crack
      for (let i = 0; i < S * S; i++) if (glow.data[i * 4 + 3] && !im.data[i * 4 + 3]) glow.data[i * 4 + 3] = 0;
      D.over(im, K.glowAround(glow, 1, P.R2, P.R1));
      put(im, eye[0], eye[1], P.W); put(im, eye[0] + 1, eye[1], P.W);
    } else if (f === 3) {
      D.over(im, K.silhouette(Object.assign(img(S, S), (() => { const t = img(S, S); D.blit(t, stone, ox, oy); return t; })()), P.W));
    } else {
      D.over(im, stumps);
      // dust settling round the plinth
      const q = D.rng(f);
      for (let k = 0; k < 20 - f * 2; k++) put(im, 30 + q() * 84, PLINTH_TOP - q() * (30 - f * 3), q() < 0.5 ? P.ST3 : P.AS3);
    }
    wake.push(im);
  }

  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    D.over(im, base);
    D.over(im, stumps);
    // the stumps' broken tops still warm
    for (let x = 0; x < S; x++) for (let y = PLINTH_TOP - 12; y < PLINTH_TOP; y++) {
      const c = D.get(stumps, x, y);
      if (c && !D.get(stumps, x, y - 1) && bayer(x + f, y) < 0.5) put(im, x, y, f % 2 ? P.R2 : P.R1);
    }
    // the incense burnt down, the smoke thinner
    for (const x of [66, 72, 78]) for (let y = PLINTH_TOP - 3; y < PLINTH_TOP; y++) put(im, x, y, P.WD1);
    for (let x = 63; x <= 81; x++) { put(im, x, PLINTH_TOP - 1, P.G1); put(im, x, PLINTH_TOP - 2, P.G2); }
    const q = D.rng(70 + f);
    for (let k = 0; k < 6; k++) put(im, 40 + q() * 64, PLINTH_TOP - 10 - q() * 40, P.AS2);
    empty.push(im);
  }
  return { [`statue_${kind}`]: idle, [`statue_${kind}_wake`]: wake, [`statue_${kind}_empty`]: empty };
}

module.exports = { makeStatue, S, PLINTH_TOP };
