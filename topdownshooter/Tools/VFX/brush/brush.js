// the Cinnabar Ink Brush, in the weapons' style: the brush itself, the blots its burning trail is
// painted from and the flames that lick up off it, the seal its evolution stamps, the ink blast
// that goes off all over the inside of a closed loop, and its two level up icons. sizes in world
// pixels (28.46 per unit). node brush.js [names] writes out/ like the other generators (plus
// preview_trail.png: a stroke painted the way the game paints it); make-lua.js then saves the
// files under NewSprites/Asesprites/VFX/Weapons/CinnabarInkBrush/
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { P, FIRE, SMOKE, TAU, easeOut, lerp, img, hash2, vnoise, fbm, withOutline, ringField, breakup, twinkle, el } = W;
const put = D.put;
const GROUP = "CinnabarInkBrush";

// cinnabar, wet and burning (a hot vermilion line down the middle), drying, and dry
const WET = [[0.1, P.R0], [0.24, P.R1], [0.42, P.R2], [0.64, P.O1], [0.82, P.O2], [0.94, P.G3]];
const DRYING = [[0.1, P.R0], [0.28, P.R1], [0.5, P.R2], [0.8, P.R3]];
const DRY = [[0.12, P.R0], [0.34, P.R1], [0.7, P.R2]];
const INK_FIRE = [[0.1, P.R1], [0.22, P.R2], [0.38, P.O1], [0.56, P.O2], [0.74, P.G2], [0.88, P.G3], [0.96, P.W]];
const clamp01 = v => Math.max(0, Math.min(1, v));

// ---------------------------------------------------------------- the trail's blots, 24x24
// the trail is these stamped along the way the brush went, every few pixels, overlapping, turned
// along the stroke (their long side is x). a blot is shaded by its distance across the stroke,
// never along it, so a row of them makes one even stroke: a burning line down the middle, cinnabar
// out to the sides, streaked the way bristles streak ink. it comes wet (two flickers), drying and
// dry, the dry brush breaking up into streaks at the sides; and as a rim, the blot a pixel bigger
// in the darkest cinnabar with a few flecks of spatter. the code draws every rim first and every
// blot over them, so the rim only shows round the outside of the whole stroke.
// frame = shape * 5 + [wet 0, wet 1, drying, dry, rim]
const DAB = 24, DAB_STATES = 5;
const DAB_SHAPES = [{ a: 9.6, b: 6.6, s: 11 }, { a: 10.4, b: 7.0, s: 23 }, { a: 9.0, b: 6.2, s: 37 }, { a: 10.8, b: 6.8, s: 51 }];
function dabShape(sh) {
  const c = DAB / 2;
  return (x, y) => {
    const dx = x - c, dy = y - c;
    const half = sh.b * (0.84 + 0.36 * fbm(x / 3.3, sh.s, sh.s));          // ragged sides
    const len = sh.a * (0.86 + 0.28 * fbm(sh.s, y / 2.1, sh.s + 5));       // ragged ends
    return (dx / len) ** 2 + (dy / half) ** 2 < 1;
  };
}
// across the stroke: 1 in the middle to 0 at the sides, the middle line wobbling a little
function across(x, y, sh) {
  const c = DAB / 2, mid = c + (vnoise(x / 4, sh.s, sh.s + 9) - 0.5) * 1.2;
  return clamp01(1 - Math.abs(y - mid) / sh.b);
}
function makeDabs() {
  const S = DAB, frames = [];
  for (const sh of DAB_SHAPES) {
    const inside = dabShape(sh);
    // bristle streaks: noise stretched along the stroke
    const streak = (x, y, seed) => fbm(x / 7, y / 1.1, sh.s + seed, 2) - 0.5;
    for (let k = 0; k < 4; k++) {
      const F = D.field(S, S);
      D.each(F, (x, y) => {
        if (!inside(x, y)) return 0;
        const t = across(x, y, sh);
        let v = 0.16 + 0.8 * Math.pow(t, 1.6) + streak(x, y, 0) * 0.2;
        if (k < 2) v += streak(x + k * 5, y, 17) * 0.12;                    // the burning line shimmers
        else v *= k === 2 ? 0.86 : 0.7;
        // the dry brush: streaks at the sides run out of ink
        if (k === 3 && t < 0.5 && streak(x, y, 31) < -0.08) return 0;
        return Math.max(0.12, v);
      });
      const im = D.shade(img(S, S), F, k < 2 ? WET : k === 2 ? DRYING : DRY);
      if (k < 2) {
        // sparks in the burning line
        for (let e = 0; e < 3; e++) {
          const ex = 5 + Math.floor(hash2(e, k, sh.s) * 14), ey = S / 2 + (hash2(k, e, sh.s + 1) < 0.5 ? 0 : -1);
          if (D.get(im, ex, ey)) put(im, ex, ey, e === 0 ? P.W : P.G3);
        }
      }
      frames.push(im);
    }
    // the rim, and spatter flicked off the sides
    const M = D.mask(S, S);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) if (inside(x + 0.5, y + 0.5)) M.m[y * S + x] = 1;
    const rim = img(S, S);
    D.paint(rim, D.dilate(M, 1, false), P.R0);
    for (let i = 0; i < (sh.s % 2 ? 2 : 1); i++) {
      const x = 7 + Math.floor(hash2(i, 1, sh.s) * 10), side = i % 2 ? 1 : -1;
      const y = Math.round(S / 2 + side * (sh.b + 1.6));
      put(rim, x, y, P.R1);
    }
    frames.push(rim);
  }
  return el("ink_dab", GROUP, S, S, [{ name: "ink", frames }], Array(frames.length).fill(100),
    DAB_SHAPES.map((_, i) => ["shape" + i, i * DAB_STATES, i * DAB_STATES + DAB_STATES - 1]));
}

// ---------------------------------------------------------------- a flame off the trail, 12x18
// a tongue of cinnabar fire licking up off wet ink and dying back, with a spark lifting off it.
// the code sets them off here and there along the wet part of the trail
function makeFlame() {
  const Wd = 12, H = 18, N = 7, frames = [];
  const height = [4, 8, 11, 12, 10, 7, 3], lean = [0, 0.5, 1, 1.2, 1.4, 1.6, 1.8];
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H), base = 15, h = height[f];
    D.each(F, (x, y) => {
      const up = base - y;
      if (up < -1 || up > h) return 0;
      const k = clamp01(up / h), cx = 6 + lean[f] * k * k * 2 + Math.sin(k * 4 + f) * 0.6;
      const half = (2.8 - 2.4 * k) * (1 + (vnoise(y / 2, f, 5) - 0.5) * 0.5);
      const d = Math.abs(x - cx);
      if (d > half) return 0;
      return (1 - k * 0.75) * (1 - d / half * 0.5) * (f >= 5 ? 0.7 : 1);
    });
    const im = D.shade(img(Wd, H), F, INK_FIRE);
    if (f >= 2 && f <= 5) put(im, 6 + Math.round(lean[f] * 2), base - h - (f - 1), f < 4 ? P.G3 : P.O2);
    frames.push(im);
  }
  return el("ink_flame", GROUP, Wd, H, [{ name: "flame", frames }], Array(N).fill(60), [["lick", 0, N - 1]]);
}

// ---------------------------------------------------------------- the brush, 16x42
// a big calligraphy brush, tip down: a bamboo handle with its nodes and a red cord, a black
// lacquered collar with a gilt band, cream bristles soaked in cinnabar towards a tip that burns.
// a drop of ink swells at the tip and falls, a glint runs down the handle, embers drift off the
// tip. the tip is 16 pixels below the canvas centre
function brushBody(im, ox, oy, frame = 0) {
  const p = (x, y, col) => put(im, ox + x, oy + y, col);
  // the cord, looped through the end of the handle
  [[9, 2], [10, 1], [11, 1], [12, 2], [12, 3], [11, 4], [10, 4]].forEach(([x, y]) => p(x, y, P.R2));
  p(11, 1, P.R3); p(12, 2, P.R3);
  // the handle: bamboo lit from the left
  p(7, 3, P.G1); p(8, 3, P.G0);
  for (let y = 4; y <= 21; y++) {
    p(6, y, P.G2); p(7, y, P.G1); p(8, y, P.G1); p(9, y, P.G0);
    if (y === 8 || y === 15) { p(6, y, P.G1); p(7, y, P.G0); p(8, y, P.G0); p(9, y, P.G0); p(6, y - 1, P.G3); p(7, y - 1, P.G2); }
  }
  // the collar: black lacquer with a gilt band
  for (let x = 5; x <= 10; x++) {
    p(x, 22, x === 5 ? P.V2 : x === 10 ? P.V0 : P.V1);
    p(x, 23, x === 5 ? P.G3 : x === 10 ? P.G0 : P.G2);
    p(x, 24, x === 5 ? P.V2 : x === 10 ? P.V0 : P.V1);
  }
  // bristles: cream at the collar, soaked cinnabar from the middle down, the tip burning
  const half = [3, 3.3, 3.4, 3.4, 3.3, 3.1, 2.8, 2.4, 2, 1.6, 1.2, 0.8, 0.4];
  half.forEach((h, i) => {
    const y = 25 + i, x0 = Math.round(7.5 - h), x1 = Math.round(7.5 + h) - 1;
    for (let x = x0; x <= x1; x++) {
      const u = (x - x0) / Math.max(1, x1 - x0);                 // 0 lit side, 1 shadow side
      const wet = clamp01((i - 3.2 + (D.bayer(x, y) - 0.5) * 2.2) / 2);
      let col;
      if (wet <= 0) col = u < 0.25 ? P.W : u > 0.75 ? P.G1 : P.G3;
      else if (i >= 11) col = i === 12 ? P.G3 : P.O2;
      else if (i >= 9) col = u < 0.4 ? P.O2 : P.O1;
      else col = u < 0.25 ? P.R3 : u > 0.75 ? P.R1 : P.R2;
      // a few hairs of the lit side run down into the red
      if (wet > 0 && u < 0.2 && i < 8 && (x + i) % 3 === 0) col = P.K3;
      p(x, y, col);
    }
  });
  return im;
}
function makeBrush() {
  const Wd = 16, H = 42, N = 6, bodyL = [], glowL = [], dropL = [];
  for (let f = 0; f < N; f++) {
    const im = brushBody(img(Wd, H), 0, 0, f);
    const gy = [4, 7, 10, 13, 17, 20][f];
    put(im, 6, gy, P.W); put(im, 7, gy, P.G3);
    bodyL.push(withOutline(im));

    // the burning tip: a small tongue of flame on it, flickering
    const G = D.field(Wd, H), tip = [2.5, 3.5, 3, 4, 2.5, 3.5][f];
    D.each(G, (x, y) => {
      const up = 37.5 - y, dx = Math.abs(x - 7.5 - (f % 2 ? 0.4 : -0.4) * clamp01(up / tip));
      if (up < 0 || up > tip || dx > 1.6 * (1 - up / tip) + 0.3) return 0;
      return 0.95 - 0.5 * up / tip;
    });
    glowL.push(D.shade(img(Wd, H), G, INK_FIRE));

    // a drop swelling at the tip and falling, and an ember drifting off
    const Dr = img(Wd, H);
    if (f === 2) put(Dr, 7, 38, P.R3);
    if (f === 3) { put(Dr, 7, 38, P.O1); put(Dr, 7, 39, P.R2); }
    if (f === 4) { put(Dr, 7, 40, P.R2); put(Dr, 7, 41, P.R1); }
    if (f === 5) put(Dr, 7, 41, P.R1);
    const ex = [9, 10, 10, 11, 11, 12][f], ey = [35, 33, 31, 29, 27, 25][f];
    put(Dr, ex, ey, f < 3 ? P.G3 : f < 5 ? P.O2 : P.R2);
    dropL.push(Dr);
  }
  return el("ink_brush", GROUP, Wd, H, [{ name: "brush", frames: bodyL }, { name: "flame", frames: glowL }, { name: "drop", frames: dropL }],
    Array(N).fill(100), [["idle", 0, N - 1]]);
}

// ---------------------------------------------------------------- the seal, 72x72
// the evolution's stamp where a loop closes: a square seal pressed in cinnabar, its stone worn at
// the edges, with 令 (the command a Taoist talisman ends on) and a border carved into it. it lands
// with the carving blazing white, a ring of fire and a splash of ink squeezed out round it, glows
// down through gold and vermilion while smoke rises, then breaks up and scatters as ink
const LING = [
  "........##........",
  ".......####.......",
  "......##..##......",
  ".....##....##.....",
  "....##......##....",
  "...##...##...##...",
  "..##....##....##..",
  ".##............##.",
  "..................",
  "..############....",
  "............##....",
  "...........##.....",
  "..........##......",
  "........###.......",
  "........##........",
  "........##........",
  "........##........",
  "........##........",
];
function sealArt(S, c, h, carve, fade, phase) {
  const im = img(S, S);
  // it crumbles in chunks, not a dither: noise over the stone, the lowest going first
  const crumble = (x, y) => fbm(x / 4.5, y / 4.5, 61) * 0.85 + hash2(x, y, 3) * 0.15;
  const gone = (x, y) => fade > 0 && crumble(x, y) < fade;
  const edgeBurn = (x, y) => fade > 0 && crumble(x, y) < fade + 0.07;
  const carved = D.mask(S, S);
  // the carved border, 3 in from the edge
  for (let y = c - h + 3; y <= c + h - 3; y++) for (let x = c - h + 3; x <= c + h - 3; x++)
    if (Math.max(Math.abs(x - c), Math.abs(y - c)) === h - 3) D.mset(carved, x, y);
  LING.forEach((row, j) => [...row].forEach((ch, i) => { if (ch === "#") D.mset(carved, c - 9 + i, c - 9 + j); }));
  for (let y = c - h; y <= c + h; y++) for (let x = c - h; x <= c + h; x++) {
    if (gone(x, y)) continue;
    const edge = Math.max(Math.abs(x - c), Math.abs(y - c));
    if (edge >= h - 1 && hash2(x, y, 5) < 0.16) continue;                // worn stone
    if (D.mget(carved, x, y)) { put(im, x, y, carve[(x + y) % 3 === 0 ? 0 : 1]); continue; }
    // the pressed ink: mottled where the stone didn't take it evenly, lit from the upper left
    const n = fbm(x / 4, y / 4, 44);
    let col = n > 0.7 ? P.R3 : n > 0.3 ? P.R2 : P.R1;
    if (x === c - h || y === c - h) col = P.R3;
    if (x === c + h || y === c + h) col = P.R1;
    if (edgeBurn(x, y)) col = fade < 0.5 ? P.O2 : P.O1;             // burning where it's breaking off
    put(im, x, y, col);
  }
  return withOutline(im, P.V0);
}
function makeSeal() {
  const S = 72, c = 36, h = 17, N = 12, r = D.rng(707);
  const splash = Array.from({ length: 22 }, (_, i) => ({ a: i * TAU / 22 + r() * 0.25, v: 0.6 + r() * 0.5, big: r() < 0.4 }));
  const puffs = Array.from({ length: 7 }, (_, i) => ({ x: c - 18 + r() * 36, y: c - 6 + r() * 20, rad: 4 + r() * 3, rise: 7 + r() * 6 }));
  const carve = [[P.W, P.G3], [P.W, P.G3], [P.G3, P.G2], [P.G2, P.O2], [P.O2, P.O1], [P.O1, P.R3], [P.O1, P.R3], [P.R3, P.R2],
    [P.R2, P.R1], [P.R1, P.R0], [P.R1, P.R0], [P.R0, P.R0]];
  const fade = [0, 0, 0, 0, 0, 0, 0, 0.12, 0.3, 0.5, 0.7, 0.88];
  const ringL = [], smokeL = [], sealL = [], inkL = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    if (f === 0) { D.flare(F, c, c, 30, 5, 18, 2, 1, 0.5); D.each(F, (x, y) => Math.max(Math.abs(x - c), Math.abs(y - c)) < h + 3 ? 0.95 : 0); }
    if (f >= 1 && f <= 7) {
      const R = [0, 24, 29, 32, 34, 35, 35.5, 36][f], th = [0, 6, 5, 4, 3, 3, 2, 2][f], pk = [0, 1, 0.88, 0.72, 0.56, 0.42, 0.3, 0.2][f];
      ringField(F, c, c, R, th, pk, f >= 3 ? breakup(R, f, [0, 0, 0, 0.92, 0.8, 0.66, 0.52, 0.4][f]) : null);
    }
    ringL.push(D.shade(img(S, S), F, FIRE));

    const Sm = D.field(S, S);
    if (f >= 3) puffs.forEach((p, i) => {
      const t = (f - 3) / (N - 4), py = p.y - p.rise * easeOut(t), pr = p.rad * (0.6 + 0.6 * t);
      D.each(Sm, (x, y) => {
        const d = Math.hypot(x - p.x, y - py) / pr;
        return d < 1 ? (0.13 - 0.06 * t) * (1 - d * 0.5) * (0.7 + 0.6 * vnoise(x / 2.5, y / 2.5 + f, i)) : 0;
      });
    });
    smokeL.push(D.shade(img(S, S), Sm, SMOKE, { bands: false }));

    sealL.push(sealArt(S, c, h, carve[f], fade[f], f));

    // ink squeezed out round the seal as it lands, flung out, then scattering as it breaks up
    const I = img(S, S);
    if (f >= 1) splash.forEach((d, i) => {
      const t = (f - 1) / (N - 2), dist = h + 2 + 16 * d.v * easeOut(t), a = d.a;
      const x = c + Math.cos(a) * dist * 1.05, y = c + Math.sin(a) * dist * 0.9 + t * t * 4;
      const col = f < 4 ? P.R3 : f < 7 ? P.R2 : f < 10 ? P.R1 : P.R0;
      if (f >= 10 && (i % 2 || D.bayer(Math.round(x), Math.round(y)) < 0.5)) return;
      put(I, x, y, col);
      if (d.big) { put(I, x + 1, y, col); put(I, x, y + 1, P.R1); if (f < 5) put(I, x - Math.cos(a) * 2, y - Math.sin(a) * 2, P.R2); }
    });
    inkL.push(I);
  }
  return el("ink_seal", GROUP, S, S, [{ name: "smoke", frames: smokeL }, { name: "ring", frames: ringL }, { name: "seal", frames: sealL }, { name: "ink", frames: inkL }],
    Array(N).fill(50), [["stamp", 0, N - 1]]);
}

// ---------------------------------------------------------------- the blast, 48x48
// what goes off all over the inside of a closed loop: a white flash, a ragged ball of cinnabar
// fire that cools and tears open, flicks of ink thrown out like brushstrokes and landing as
// spatter, violet smoke rising off it, embers
function makeBlast() {
  const S = 48, c = 24, N = 10, r = D.rng(909);
  const flicks = Array.from({ length: 9 }, (_, i) => ({ a: i * TAU / 9 + (r() - 0.5) * 0.5, len: 6 + r() * 5, v: 0.7 + r() * 0.4 }));
  const embers = Array.from({ length: 10 }, () => ({ a: r() * TAU, v: 0.4 + r() * 0.6 }));
  const puffs = Array.from({ length: 5 }, (_, i) => ({ a: i * TAU / 5 + r(), d: 4 + r() * 5, rad: 4 + r() * 2.5 }));
  const smokeL = [], fireL = [], inkL = [], emberL = [];
  for (let f = 0; f < N; f++) {
    const Sm = D.field(S, S);
    if (f >= 3) puffs.forEach((p, i) => {
      const t = (f - 3) / (N - 4), px = c + Math.cos(p.a) * (p.d + 4 * t), py = c + Math.sin(p.a) * (p.d + 4 * t) - 6 * easeOut(t), pr = p.rad * (0.7 + 0.5 * t);
      D.each(Sm, (x, y) => {
        const d = Math.hypot(x - px, y - py) / pr;
        return d < 1 ? (0.14 - 0.07 * t) * (1 - d * 0.5) * (0.7 + 0.6 * vnoise(x / 2, y / 2 + f, i + 3)) : 0;
      });
    });
    smokeL.push(D.shade(img(S, S), Sm, SMOKE, { bands: false }));

    const F = D.field(S, S);
    if (f === 0) { D.flare(F, c, c, 17, 3, 9, 1, 1, 0.5); D.each(F, (x, y) => Math.hypot(x - c, y - c) < 6 ? 1 : 0); }
    if (f >= 1 && f <= 6) {
      const R = [0, 9, 12, 13.5, 13, 11.5, 9][f], heat = [0, 1, 0.86, 0.66, 0.46, 0.3, 0.2][f];
      D.each(F, (x, y) => {
        const n = fbm(x / 4 + f * 0.3, y / 4, 71);
        const d = Math.hypot(x - c, y - c) / (R * (0.8 + 0.4 * n));
        if (d >= 1) return 0;
        if (f >= 4 && fbm(x / 3, y / 3 + f, 5) < 0.25 + (f - 4) * 0.08) return 0;   // tearing open as it cools
        return heat * (1.05 - d * 0.6) * (0.75 + 0.5 * n);
      });
    }
    fireL.push(D.shade(img(S, S), F, INK_FIRE));

    // flicks of ink thrown off the fire like brushstrokes: each a drop with a tail pointing back at
    // the blast, only ever outside the fire, flying out and landing as spatter
    const I = D.field(S, S);
    const spots = img(S, S);
    const fireR = [0, 9, 12, 13.5, 13, 11.5, 9, 0, 0, 0][f] * 1.1;
    if (f >= 2) flicks.forEach((k, i) => {
      const t = (f - 2) / (N - 3), far = 12 + 11 * k.v * easeOut(t);
      if (f <= 6) {
        const len = Math.min(k.len * (1.1 - t * 0.6), far - fireR);
        if (len > 1) D.ray(I, c + Math.cos(k.a) * far, c + Math.sin(k.a) * far, k.a + Math.PI, len, 3.6 - t * 1.4, 0.92 - t * 0.3, 0.3);
      } else {
        const x = Math.round(c + Math.cos(k.a) * far), y = Math.round(c + Math.sin(k.a) * far);
        const col = f < 8 ? P.R1 : P.R0;
        if (f === 9 && i % 2) return;
        put(spots, x, y, col); put(spots, x + 1, y, col);
        if (i % 3 === 0) put(spots, x, y + 1, P.R0);
      }
    });
    D.each(I, (x, y) => 0);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++)
      if (Math.hypot(x + 0.5 - c, y + 0.5 - c) < fireR) I.f[y * S + x] = 0;
    inkL.push(D.over(D.shade(img(S, S), I, WET), spots));

    const E = img(S, S);
    if (f >= 2) embers.forEach((e, i) => {
      if ((i + f) % 3 === 0) return;
      const d = 8 + 14 * e.v * easeOut((f - 2) / (N - 3));
      put(E, c + Math.cos(e.a) * d, c + Math.sin(e.a) * d - (f - 2) * 0.9, f < 4 ? P.W : f < 6 ? P.G3 : f < 8 ? P.O2 : P.R2);
    });
    emberL.push(E);
  }
  return el("ink_blast", GROUP, S, S, [{ name: "smoke", frames: smokeL }, { name: "fire", frames: fireL }, { name: "ink", frames: inkL }, { name: "embers", frames: emberL }],
    Array(N).fill(40), [["blast", 0, N - 1]]);
}

// ---------------------------------------------------------------- the level up icons, 32x32
// a stroke of burning cinnabar along points: fat where the brush pressed, thinning, the dry brush
// breaking it into streaks at the tail. shaded across the stroke only, like the trail
function inkStroke(S, pts, width, seed, frame) {
  const F = D.field(S, S), T = D.field(S, S);
  for (let i = 1; i < pts.length; i++) {
    const [x0, y0] = pts[i - 1], [x1, y1] = pts[i], w0 = width(i - 1), w1 = width(i);
    D.each(F, (x, y) => {
      const vx = x1 - x0, vy = y1 - y0, L2 = vx * vx + vy * vy;
      const t = clamp01(((x - x0) * vx + (y - y0) * vy) / L2);
      const d = Math.hypot(x - (x0 + vx * t), y - (y0 + vy * t)), half = lerp(w0, w1, t) / 2;
      if (d >= half) return 0;
      const along = (i - 1 + t) / (pts.length - 1), ac = 1 - d / half;
      // the dry brush at the tail
      if (along > 0.72 && ac < 0.6 && fbm(x / 1.2, y / 1.2, seed + 3, 2) < 0.2 + (along - 0.72) * 1.4) return 0;
      fmaxT(T, x, y, along);
      return 0.16 + 0.8 * Math.pow(ac, 1.3) + (fbm(x / 3, y / 1, seed, 2) - 0.5) * 0.2;
    });
  }
  const im = D.shade(img(S, S), F, WET);
  const M = W.maskOf(im), out = img(S, S);
  D.paint(out, D.minus(D.dilate(M, 1, false), M), P.R0);
  D.over(out, im);
  // sparks creeping along the burning line
  for (let k = 0; k < 2; k++) {
    const i = 1 + ((frame + k * 3) % (pts.length - 2));
    put(out, Math.floor(pts[i][0]), Math.floor(pts[i][1]), k ? P.G3 : P.W);
  }
  return out;
}
function fmaxT(T, x, y, v) { D.fmax(T, Math.floor(x), Math.floor(y), v); }

// the brush drawn leaning 45 degrees, tip down-left: every pixel coloured by how far along the
// brush and across it it is, so the diagonal comes out clean
function diagonalBrush(S, tipX, tipY, frame, k = 1) {
  const im = img(S, S), dx = Math.SQRT1_2, dy = -Math.SQRT1_2;     // along the brush, tip to end
  const glint = [9, 12, 15, 18][frame];
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const px = (x + 0.5 - tipX) / k, py = (y + 0.5 - tipY) / k;
    const u = px * dx + py * dy, v = -px * dy + py * dx;          // v < 0: the lit (upper left) side
    let col = null;
    if (u >= 11 && u <= 25 && Math.abs(v) <= 1.25) {
      // bamboo, with two nodes
      col = v < -0.4 ? P.G2 : v > 0.5 ? P.G0 : P.G1;
      if (Math.abs(u - 16) < 0.6 || Math.abs(u - 21.5) < 0.6) col = v < -0.4 ? P.G3 : P.G0;
      if (Math.abs(u - glint) < 0.7 && v < 0) col = P.W;
    } else if (u >= 9 && u < 11 && Math.abs(v) <= 2.2) {
      col = u >= 10 ? (v < -0.8 ? P.G3 : P.G2) : (v < -0.8 ? P.V2 : P.V1);
    } else if (u >= 0 && u < 9) {
      const half = 0.6 + 2.1 * Math.sin(Math.min(1, u / 8.5) * Math.PI * 0.62);
      if (Math.abs(v) <= half) {
        const s = (v / half + 1) / 2;                                // 0 lit, 1 shadow
        if (u > 6.2) col = s < 0.3 ? P.W : s > 0.7 ? P.G1 : P.G3;
        else if (u > 2.2) col = s < 0.3 ? P.R3 : s > 0.7 ? P.R1 : P.R2;
        else col = u < 1 ? P.G3 : P.O2;
      }
    } else if (u > 25 && u < 27.5 && v > -1.5 && v < 2.5) {
      // the cord
      if (Math.hypot(u - 26.2, v - 0.6) > 0.7) col = P.R2;
    }
    if (col) put(im, x, y, col);
  }
  return withOutline(im);
}
function makeIcon() {
  const S = 32, N = 4, frames = [];
  const pts = [[3.5, 27], [8, 26], [13, 25.6], [18.5, 26], [23, 27], [26.5, 28.2], [29, 26.5]];
  const width = i => [7, 6.2, 5.2, 4.4, 3.6, 2.8, 1.8][i];
  for (let f = 0; f < N; f++) {
    const im = inkStroke(S, pts, width, 3, f);
    D.over(im, diagonalBrush(S, 6.5, 22.5, f, 1.25));
    frames.push(im);
  }
  return el("ink_icon", GROUP, S, S, [{ name: "icon", frames }], Array(N).fill(130), [["idle", 0, N - 1]]);
}

// evolved: an ensō, the stroke closed on itself, with the seal stamped in the middle and a spark
// travelling round the ring
function makeIconEvolved() {
  const S = 32, N = 4, frames = [], c = 16;
  const pts = Array.from({ length: 17 }, (_, i) => {
    const a = -2.4 + i * (TAU * 1.02) / 16;
    return [c + Math.cos(a) * 11.6, c + 0.3 + Math.sin(a) * 11.6];
  });
  const width = i => i === 0 ? 5.6 : i > 13 ? 2.6 - (i - 13) * 0.4 : lerp(4.8, 3.2, i / 13);
  for (let f = 0; f < N; f++) {
    const im = inkStroke(S, pts, width, 9, f);
    const seal = img(S, S), carve = f % 2 ? P.G3 : P.W;
    for (let y = c - 6; y <= c + 6; y++) for (let x = c - 6; x <= c + 6; x++) {
      const e = Math.max(Math.abs(x - c), Math.abs(y - c));
      if (e === 6 && hash2(x, y, 2) < 0.15) continue;
      const n = fbm(x / 2, y / 2, 12);
      put(seal, x, y, x === c - 6 || y === c - 6 ? P.R3 : n > 0.55 ? P.R3 : n > 0.3 ? P.R2 : P.R1);
    }
    ["...#...", "..#.#..", ".#...#.", ".......", ".####..", "....#..", "...#..."].forEach((row, j) =>
      [...row].forEach((ch, i) => { if (ch === "#") put(seal, c - 3 + i, c - 3 + j, carve); }));
    D.over(im, withOutline(seal, P.V0));
    const [sx, sy] = pts[[3, 7, 11, 15][f]];
    twinkle(im, sx, sy, 1, [P.W, P.G3]);
    frames.push(im);
  }
  return el("ink_icon_evolved", GROUP, S, S, [{ name: "icon", frames }], Array(N).fill(130), [["idle", 0, N - 1]]);
}

// ---------------------------------------------------------------- a painted trail, for looking at
// the stroke the game paints: blots every 5 pixels along a path, turned along it, their rims under
// them, wet at the brush, drying and dry towards the tail, fading out at the very end, with a few
// flames licking off the wet part
function previewTrail(dabs, flame) {
  const Wd = 260, H = 150, out = img(Wd, H);
  for (let i = 0; i < Wd * H; i++) { out.data.set([43, 34, 50, 255], i * 4); }
  const path = [];
  for (let s = 0; s <= 1; s += 0.004) {
    const x = 20 + s * 210, y = 80 + Math.sin(s * 7) * 30 - Math.sin(s * 2.3) * 15;
    path.push([x, y]);
  }
  const pts = [path[0]];
  for (const p of path) { const q = pts[pts.length - 1]; if (Math.hypot(p[0] - q[0], p[1] - q[1]) >= 6) pts.push(p); }
  const frames = dabs.layers[0].frames;
  const stamp = (fr, cx, cy, ang, scale, alpha) => {
    const ca = Math.cos(ang), sa = Math.sin(ang), c = DAB / 2;
    for (let y = -18; y <= 18; y++) for (let x = -18; x <= 18; x++) {
      const u = (x * ca + y * sa) / scale + c, v = (-x * sa + y * ca) / scale + c;
      const col = D.get(fr, Math.floor(u), Math.floor(v));
      if (!col) continue;
      const X = Math.round(cx + x), Y = Math.round(cy + y);
      if (X < 0 || Y < 0 || X >= Wd || Y >= H) continue;
      if (alpha < 1 && D.bayer(X, Y) > alpha) continue;
      put(out, X, Y, col);
    }
  };
  const n = pts.length, info = pts.map((p, i) => {
    const q = pts[Math.min(n - 1, i + 1)], o = pts[Math.max(0, i - 1)];
    const age = 1 - i / (n - 1);                                     // the head (brush) is the last point
    return { p, ang: Math.atan2(q[1] - o[1], q[0] - o[0]), shape: Math.floor(hash2(i, 0, 7) * 4), age };
  });
  const state = age => age < 0.45 ? (Math.floor(age * 40) % 2) : age < 0.7 ? 2 : 3;
  const press = i => 0.88 + 0.24 * fbm(i / 6, 0, 13);
  const scaleOf = (age, i) => (age > 0.85 ? 1 - (age - 0.85) / 0.15 * 0.5 : 1) * press(i);
  const alphaOf = age => age > 0.85 ? 1 - (age - 0.85) / 0.15 : 1;
  info.forEach((d, i) => stamp(frames[d.shape * DAB_STATES + 4], d.p[0], d.p[1], d.ang, scaleOf(d.age, i), alphaOf(d.age)));
  info.forEach((d, i) => stamp(frames[d.shape * DAB_STATES + state(d.age)], d.p[0], d.p[1], d.ang, scaleOf(d.age, i), alphaOf(d.age)));
  // flames on the wet part
  [0.9, 0.8, 0.66].forEach((k, i) => {
    const d = info[Math.floor(k * (n - 1))], fr = flame.layers[0].frames[[2, 3, 5][i]];
    D.blit(out, fr, Math.round(d.p[0] - 6), Math.round(d.p[1] - 15));
  });
  return png.preview([out], 4, [43, 34, 50], 1, 0);
}

// ---------------------------------------------------------------- write everything
function build() {
  const only = process.argv.slice(2);
  const all = [makeDabs(), makeFlame(), makeBrush(), makeSeal(), makeBlast(), makeIcon(), makeIconEvolved()];
  const elements = all.filter(e => !only.length || only.includes(e.name));
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
    const scale = e.w <= 18 ? 10 : e.w <= 48 ? 6 : 4;
    png.encode(png.preview(flat, scale, [43, 34, 50], Math.min(flat.length, e.w <= 18 ? 10 : 6), 2), path.join(out, `sheet_${e.name}.png`));
  }
  png.encode(previewTrail(all[0], all[1]), path.join(out, "preview_trail.png"));
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.frames} [${m.layers.join(",")}]`).join("\n"));
}

build();
