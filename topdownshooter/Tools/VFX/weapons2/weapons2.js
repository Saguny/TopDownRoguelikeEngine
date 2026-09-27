// the newer weapons' art, in the same style as weapons.js (its palette, ramps, ordered dithering,
// plum outlines on solid things, and its attack language: a white-hot flare with thin rays, a ring
// that breaks into dashes as it spreads, a core cooling down its ramp, embers and violet smoke
// dithering out). sizes in world pixels (28.46 per unit), the back cosmetics at the characters'
// pixel size. every element becomes one layered .aseprite file:
//   node weapons2.js            draws everything into out/ (plus a sheet_*.png preview of each)
//   node ../write-ase.js out "../../../Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"
// then Tools > VFX > Build Weapon FX in Unity points the weapons at them
//   Dragon Line       dl_head, dl_body, dl_tail, dl_line, dl_bite, dl_fire, dl_icon, dl_icon_evolved
//   Ice Cloud         ic_cloud, ic_snow, ic_pile, ic_ice, ic_tornado, ic_burst, ic_icon, ic_icon_evolved
//   Flying Sword      fs_blade, fs_embed, fs_laser, fs_spark, fs_icon, fs_icon_evolved
//   Arena             arena_seal, arena_rise (the Final Rush's ring of spirit seals)
//   Backs             back_bow, back_peach, back_jian (a weapon worn on a character's back)
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { hex, put, bayer } = D;
const { FIRE, GOLD, AZURE, SMOKE, TAU, easeOut, lerp, img, fbm, vnoise, hash2, withOutline, halo, ringField, breakup, twinkle, el } = W;

const P = Object.assign({}, W.P, {
  // qing: the azure-green of the Azure Dragon
  Q0: hex("#0b2533"), Q1: hex("#12505e"), Q2: hex("#1c8585"), Q3: hex("#3cc2a6"), Q4: hex("#9eeed2"),
  // jade, for the flying sword
  J0: hex("#0b2a22"), J1: hex("#155c44"), J2: hex("#26986a"), J3: hex("#54d898"), J4: hex("#aef5cc"), J5: hex("#e6fff0"),
  // ice and snow
  I0: hex("#1a2c52"), I1: hex("#2f5a8e"), I2: hex("#5a94c8"), I3: hex("#9ccbef"), I4: hex("#d4ecff"), I5: hex("#f4fbff"),
  // wood, paper, cloth for the seals and the backs
  WD0: hex("#4a2412"), WD1: hex("#7e4220"), WD2: hex("#b8703a"), WD3: hex("#e6a060"),
  TL0: hex("#b88418"), TL1: hex("#f2c230"), TL2: hex("#fff07a"),
  SL0: hex("#4a5470"), SL1: hex("#8a9ab8"), SL2: hex("#c8d6ea"),
  CR1: hex("#c9bcc0"), CR2: hex("#ece4dc"),
});
const OUT = P.V0;
const QING = [[0.1, P.Q0], [0.25, P.Q1], [0.45, P.Q2], [0.65, P.Q3], [0.85, P.Q4]];
const JADE = [[0.1, P.J1], [0.22, P.J2], [0.38, P.J3], [0.56, P.J4], [0.76, P.J5], [0.92, P.W]];
const ICE = [[0.08, P.I0], [0.18, P.I1], [0.32, P.I2], [0.5, P.I3], [0.7, P.I4], [0.88, P.I5]];
const clamp01 = v => Math.max(0, Math.min(1, v));

// ---------------------------------------------------------------- shape kit
const mask = (w, h) => D.mask(w, h);
const has = (M, x, y) => D.mget(M, x, y);
function rect(M, x0, y0, x1, y1) { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) D.mset(M, x, y); return M; }
function ellipse(M, cx, cy, rx, ry) {
  for (let y = Math.floor(cy - ry - 1); y <= Math.ceil(cy + ry + 1); y++)
    for (let x = Math.floor(cx - rx - 1); x <= Math.ceil(cx + rx + 1); x++)
      if (((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1) D.mset(M, x, y);
  return M;
}
function poly(M, pts) {
  const ys = pts.map(p => p[1]);
  for (let y = Math.floor(Math.min(...ys)); y <= Math.ceil(Math.max(...ys)); y++) {
    const xs = [];
    for (let i = 0; i < pts.length; i++) {
      const [x0, y0] = pts[i], [x1, y1] = pts[(i + 1) % pts.length];
      if ((y0 <= y + 0.5 && y1 > y + 0.5) || (y1 <= y + 0.5 && y0 > y + 0.5)) xs.push(x0 + (y + 0.5 - y0) / (y1 - y0) * (x1 - x0));
    }
    xs.sort((a, b) => a - b);
    for (let i = 0; i + 1 < xs.length; i += 2) for (let x = Math.round(xs[i]); x <= Math.round(xs[i + 1]) - 1; x++) D.mset(M, x, y);
  }
  return M;
}
function limb(M, pts, width) {
  const C = D.polyline(mask(M.w, M.h), pts);
  const T = width > 1 ? D.dilate(C, Math.floor(width / 2), true) : C;
  for (let i = 0; i < T.m.length; i++) if (T.m[i]) M.m[i] = 1;
  return M;
}
// shades a mask lit from the upper right: [dark, mid, light, highlight]
function shade(im, M, ramp, { dark = 0.34, light = 0.72, top = true, bottom = true, spec = true } = {}) {
  for (let y = 0; y < M.h; y++) {
    let l = -1, r = -1;
    for (let x = 0; x < M.w; x++) if (has(M, x, y)) { if (l < 0) l = x; r = x; }
    if (l < 0) continue;
    for (let x = l; x <= r; x++) {
      if (!has(M, x, y)) continue;
      const t = r > l ? (x - l) / (r - l) : 0.6;
      let c = t < dark ? 0 : t > light ? 2 : 1;
      if (top && !has(M, x, y - 1) && c > 0) c = 2;
      if (bottom && !has(M, x, y + 1) && c < 2) c = 0;
      if (spec && ramp[3] && c === 2 && !has(M, x, y - 1) && !has(M, x + 1, y)) c = 3;
      put(im, x, y, ramp[c]);
    }
  }
  return im;
}
// shades a mask by its height: lit on top, dark underneath (for things running left-right)
function shadeRows(im, M, ramp) {
  for (let x = 0; x < M.w; x++) {
    let t = -1, b = -1;
    for (let y = 0; y < M.h; y++) if (has(M, x, y)) { if (t < 0) t = y; b = y; }
    if (t < 0) continue;
    for (let y = t; y <= b; y++) {
      if (!has(M, x, y)) continue;
      const k = b > t ? (y - t) / (b - t) : 0.5;
      const i = Math.min(ramp.length - 1, Math.floor((1 - k) * ramp.length * 0.999));
      put(im, x, y, ramp[i]);
    }
  }
  return im;
}
const px = (im, pts, c) => pts.forEach(([x, y]) => put(im, x, y, c));
// copies an image turned by `ang` radians about its centre, nearest pixel
function rotate(src, ang, W2 = src.w, H2 = src.h) {
  const out = img(W2, H2), c = Math.cos(-ang), s = Math.sin(-ang);
  const cx = src.w / 2, cy = src.h / 2, ox = W2 / 2, oy = H2 / 2;
  for (let y = 0; y < H2; y++) for (let x = 0; x < W2; x++) {
    const dx = x + 0.5 - ox, dy = y + 0.5 - oy;
    const sx = Math.floor(cx + dx * c - dy * s), sy = Math.floor(cy + dx * s + dy * c);
    const col = D.get(src, sx, sy);
    if (col) put(out, x, y, col);
  }
  return out;
}
function shift(src, dx, dy) { const out = img(src.w, src.h); D.blit(out, src, dx, dy); return out; }
// an image with every pixel dithered away but `keep` of them
function thin(src, keep, phase = 0) {
  const out = img(src.w, src.h);
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const c = D.get(src, x, y); if (c && bayer(x + phase, y) < keep) put(out, x, y, c); }
  return out;
}
// a flare with a white-hot core, the frame every impact in the game opens on
function flareFrame(S, c, len, ramp, diag = 0) {
  const F = D.field(S, S);
  D.flare(F, c, c, len, 3, diag, 1, 1, 0.4);
  D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 3.2 ? 1 : d < 5 && bayer(x, y) < 0.6 ? 0.7 : 0; });
  return D.shade(img(S, S), F, ramp);
}
// a broken ring of dashes, for impacts spreading out
function brokenRing(S, c, R, th, peak, ramp, seed, keep) {
  const F = D.field(S, S);
  ringField(F, c, c, R, th, peak, keep < 1 ? breakup(R, seed, keep) : null);
  return D.shade(img(S, S), F, ramp);
}

// ================================================================ DRAGON LINE
// the Azure Dragon, Qing Long: qing scales, a cream belly, a red flame mane, gold antlers and
// whiskers streaming back. drawn facing right; the game turns each part along the dragon's path.
// a body segment is 16px across and the game spaces them 14px apart, so they overlap into one body

// the head, 32x22: a long snout with an upturned nose, jaw open on white fangs, a heavy brow over a
// gold eye, gold antlers sweeping back, a red flame mane and whiskers streaming behind
function dragonHead(f) {
  const Wd = 32, H = 22, im = img(Wd, H);
  // the mane: flame locks off the back of the skull, tips blown back and up
  const mane = mask(Wd, H);
  [[5, 0], [8, 1], [11, 2], [14, 1]].forEach(([y, k]) => {
    const sway = [0, 1, 2, 1][(f + k) % 4];
    poly(mane, [[11, y - 1], [12, y + 3], [7, y + 4], [1 + sway, y + 1 - (k % 2)], [6, y]]);
  });
  for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) if (has(mane, x, y)) put(im, x, y, x < 4 ? P.O2 : x < 6 ? P.R3 : (x + y) % 5 === 0 ? P.R1 : P.R2);
  // the beard under the jaw, gold, blown back
  const beard = poly(mask(Wd, H), [[17, 15], [21, 15], [16, 19], [12, 20], [14, 17]]);
  shade(im, beard, [P.G0, P.G1, P.G2, P.G3], { dark: 0.25 });
  // skull and snout, lit from above
  const head = ellipse(mask(Wd, H), 13, 10, 5.5, 4.6);
  poly(head, [[15, 6], [26, 6.5], [29, 5], [31, 7], [30.5, 11], [28, 12.5], [16, 13.5]]);
  shadeRows(im, head, [P.Q1, P.Q2, P.Q2, P.Q3, P.Q3, P.Q4]);
  // the brow ridge, heavy over the eye, and scales on the crown
  px(im, [[15, 6], [16, 6], [17, 6], [18, 6], [19, 7], [16, 7], [17, 7]], P.Q1);
  px(im, [[10, 8], [12, 7], [11, 10], [13, 9], [9, 11], [12, 12]], P.Q1);
  px(im, [[11, 7], [13, 8], [10, 10], [12, 11]], P.Q4);
  // the eye: gold with a slit pupil and a glint
  px(im, [[17, 8], [18, 8], [19, 8], [17, 9], [18, 9]], P.G2); put(im, 18, 8, P.K); put(im, 18, 9, P.G1);
  if (f !== 3) put(im, 19, 8, P.W);
  // the nose: a nostril and a highlight on the bump
  put(im, 29, 8, P.Q1); px(im, [[29, 6], [30, 7]], P.Q4);
  // the upper lip, cream, over the open mouth
  for (let x = 18; x <= 30; x++) put(im, x, 12, x > 27 ? P.CR2 : P.CR1);
  for (let x = 20; x <= 28; x++) put(im, x, 13, P.R0);
  px(im, [[21, 13], [26, 13]], P.W);                     // upper fangs
  // the lower jaw
  const jaw = poly(mask(Wd, H), [[16, 14], [29, 14], [27, 16], [18, 17]]);
  shadeRows(im, jaw, [P.Q1, P.CR1, P.CR2]);
  px(im, [[23, 14], [28, 14]], P.W);                     // lower fangs
  put(im, 25, 13, P.R2);                                  // the tongue
  // antlers: a gold beam sweeping back off the brow, with a tine
  const horn = limb(mask(Wd, H), [[15, 5], [11, 3], [6, 1]], 2);
  limb(horn, [[10, 3], [9, 0]], 1);
  limb(horn, [[7, 2], [4, 0]], 1);
  shade(im, horn, [P.G0, P.G1, P.G2, P.G3], { dark: 0.15 });
  const out = withOutline(im, OUT);
  // whiskers: two gold strands off the upper lip, waving back under the mane
  for (const [y0, amp, len, ph] of [[12, 1.4, 24, 0], [14, 1.8, 20, 1.3]]) {
    for (let i = 3; i <= len; i++) {
      const x = 28 - i;
      const y = y0 + (i > 10 ? 1 : 0) + Math.round(Math.sin(i * 0.42 - f * 1.57 + ph) * amp * Math.min(1, (i - 3) / 8));
      if (x < 0) break;
      if (D.get(im, x, y)) continue;
      put(out, x, y, i > len - 4 ? P.G1 : P.G2);
    }
  }
  return out;
}

// a body segment, 16x16: a band of qing scales, a ridge of red fin spikes, the cream belly plates
function dragonBody(f) {
  const S = 16, im = img(S, S);
  const band = mask(S, S);
  for (let x = 0; x < S; x++) {
    const bulge = Math.round(Math.sin((x / S) * Math.PI) * 1.2);
    rect(band, x, 5 - bulge, x, 11 + bulge);
  }
  shadeRows(im, band, [P.Q0, P.Q1, P.Q2, P.Q2, P.Q3, P.Q4]);
  // scales: little arcs in rows, a shade darker, drifting a pixel a frame so the body ripples
  for (let y = 6; y <= 10; y += 2) for (let x = (y + f) % 4; x < S; x += 4) if (has(band, x, y)) { put(im, x, y, P.Q1); if (has(band, x + 1, y - 1)) put(im, x + 1, y - 1, P.Q3); }
  // the belly: cream plates along the bottom edge
  for (let x = 0; x < S; x++) {
    let b = -1; for (let y = 0; y < S; y++) if (has(band, x, y)) b = y;
    if (b < 0) continue;
    put(im, x, b, (x + f) % 3 === 0 ? P.CR1 : P.CR2);
    put(im, x, b - 1, (x + f) % 3 === 0 ? P.G1 : P.CR1);
  }
  // the dorsal fin: red flame spikes along the top, flickering
  for (let k = 0; k < 3; k++) {
    const x = 2 + k * 5 + (f % 2), tip = [3, 2, 3, 1][(f + k) % 4];
    const fin = poly(mask(S, S), [[x, 6], [x + 3, 6], [x - 1, tip - 1]]);
    shade(im, fin, [P.R1, P.R2, P.R3], { dark: 0.3, light: 0.6 });
  }
  return withOutline(im, OUT);
}

// a body segment with a clawed leg under it, paddling as it flies (the game puts these at the
// shoulders and the hips)
function dragonLeg(f) {
  // 16x28 with the body in the middle rows, so its centre is the plain segment's centre
  const S = 16, H = 28, o = 6, im = img(S, H), base = dragonBody(f);
  const reach = [0, 1, 2, 1][f];
  const legM = limb(mask(S, H), [[9, o + 11], [7 + reach, o + 15], [4 + reach * 2, o + 17]], 3);
  shade(im, legM, [P.Q1, P.Q2, P.Q3], { dark: 0.3 });
  // three gold claws spread at the end
  const cx = 3 + reach * 2, cy = o + 18;
  px(im, [[cx - 1, cy], [cx, cy + 1], [cx + 1, cy + 1], [cx - 2, cy - 1]], P.G2);
  put(im, cx - 2, cy, P.G3);
  const out = withOutline(im, OUT);
  D.blit(out, base, 0, o);
  return out;
}

// the tail, 18x14: the body tapering to a point with a flame tuft at its end, drawn trailing left
function dragonTail(f) {
  const Wd = 18, H = 14, im = img(Wd, H);
  const tuft = mask(Wd, H);
  for (let k = 0; k < 3; k++) {
    const y = 4 + k * 2, flick = [0, 1, 2, 1][(f + k) % 4];
    poly(tuft, [[7, y + 1], [5, y + 3], [0 + flick, y + 2 + k % 2], [2 + flick, y]]);
  }
  shade(im, tuft, [P.R1, P.R2, P.O2, P.G3], { dark: 0.4, light: 0.8 });
  const body = mask(Wd, H);
  poly(body, [[18, 4], [18, 10], [12, 9], [6, 8], [5, 7], [8, 6], [13, 5]]);
  shadeRows(im, body, [P.Q0, P.Q1, P.Q2, P.Q3, P.Q4]);
  for (let x = 7; x < 18; x++) { let b = -1; for (let y = 0; y < H; y++) if (has(body, x, y)) b = y; if (b > 0) put(im, x, b, P.CR1); }
  for (let x = 9 + (f % 2); x < 18; x += 3) if (has(body, x, 6)) put(im, x, 6, P.Q1);
  const fin = poly(mask(Wd, H), [[13, 5], [16, 5], [14, 2 + (f % 2)]]);
  shade(im, fin, [P.R1, P.R2, P.R3]);
  return withOutline(im, OUT);
}

function makeDragon() {
  const N = 4, head = [], body = [], tail = [], legs = [];
  for (let f = 0; f < N; f++) { head.push(dragonHead(f)); body.push(dragonBody(f)); tail.push(dragonTail(f)); legs.push(dragonLeg(f)); }
  return [
    el("dl_leg", "DragonLine", 16, 28, [{ name: "leg", frames: legs }], Array(N).fill(100), [["paddle", 0, N - 1]]),
    el("dl_head", "DragonLine", 32, 22, [{ name: "head", frames: head }], Array(N).fill(100), [["fly", 0, N - 1]]),
    el("dl_body", "DragonLine", 16, 16, [{ name: "body", frames: body }], Array(N).fill(100), [["fly", 0, N - 1]]),
    el("dl_tail", "DragonLine", 18, 14, [{ name: "tail", frames: tail }], Array(N).fill(100), [["fly", 0, N - 1]]),
  ];
}

// the line cast before the dragon, one 32x7 tile repeated along it: a thread of qing qi with a
// white core, glyph ticks riding along it and a glow either side. seamless in x
function makeLine() {
  const Wd = 32, H = 7, N = 4, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    for (let x = 0; x < Wd; x++) {
      const pulse = ((x - f * 4 + Wd * 4) % 16) < 2;
      put(im, x, 3, pulse ? P.W : P.Q4);
      put(im, x, 2, pulse ? P.Q4 : P.Q3);
      put(im, x, 4, pulse ? P.Q4 : P.Q2);
      if (bayer(x + f, 1) < 0.5) put(im, x, 1, P.Q2);
      if (bayer(x + f + 2, 5) < 0.5) put(im, x, 5, P.Q1);
    }
    // little script marks travelling along it, like a line of written charm
    for (let k = 0; k < 2; k++) {
      const x = (k * 16 + 8 + f * 4) % Wd;
      px(im, [[x, 0], [x, 6], [(x + 1) % Wd, 1], [(x + 1) % Wd, 5]], P.Q3);
    }
    frames.push(im);
  }
  return el("dl_line", "DragonLine", Wd, H, [{ name: "line", frames }], Array(N).fill(70), [["flow", 0, N - 1]], { pivot: "left", fullRect: true });
}

// the head's bite: a qing flare, a ring breaking into dashes and scales flying off. 32x32
function makeBite() {
  const S = 32, c = 16, N = 6, frames = [];
  for (let f = 0; f < N; f++) {
    let im;
    if (f === 0) im = flareFrame(S, c, 13, QING.concat([[0.95, P.W]]), 5);
    else {
      const R = [0, 5, 8, 11, 13, 14][f];
      im = brokenRing(S, c, R, [0, 2.4, 2, 1.6, 1.3, 1][f], [0, 1, 0.88, 0.72, 0.56, 0.4][f], QING.concat([[0.95, P.W]]), 3 + f, f >= 2 ? 0.65 - f * 0.06 : 1);
      if (f === 1) { const F = D.field(S, S); D.each(F, (x, y) => Math.hypot(x - c, y - c) < 3 ? 0.9 : 0); D.over(im, D.shade(img(S, S), F, QING)); }
      // scales knocked loose, tumbling out and dithering away
      const r = D.rng(90);
      for (let k = 0; k < 7; k++) {
        const a = r() * TAU, d = 3 + f * (2.2 + r() * 1.2), x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + f * 0.4;
        if (f >= 4 && bayer(Math.floor(x), Math.floor(y)) > 0.5) continue;
        put(im, x, y, k % 3 === 0 ? P.G2 : P.Q3); put(im, x + 1, y, P.Q1);
      }
      if (f <= 3) twinkle(im, c + [0, 6, 9, 11][f], c - [0, 5, 7, 9][f], f === 1 ? 1 : 0, [P.W, P.Q4]);
    }
    frames.push(im);
  }
  return el("dl_bite", "DragonLine", S, S, [{ name: "bite", frames }], Array(N).fill(40), [["bite", 0, N - 1]]);
}

// the coiling dragon's spat fire, 40x32, flying right: a flash at the mouth, then a stream of
// fireballs rolling out and swelling, white-hot where they leave the mouth, cooling down the
// fire ramp as they go, embers flung ahead, and violet smoke that dithers away
function makeFire() {
  const Wd = 40, H = 32, cy = 16, N = 8, frames = [];
  const balls = [[6, 4.2, 0], [11, 5.4, 1], [17, 6.4, 2], [24, 7.4, 3], [31, 8, 4]];     // [x, radius, frame it appears]
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H), Smk = D.field(Wd, H);
    if (f === 0) {
      D.flare(F, 6, cy, 10, 3, 4, 1, 1, 0.45);
      D.each(F, (x, y) => Math.hypot(x - 6, y - cy) < 3.4 ? 1 : 0);
    } else {
      for (const [bx, br, born] of balls) {
        const age = f - 1 - born;
        if (age < 0) continue;
        const r = br * (0.75 + Math.min(age, 3) * 0.12), heat = Math.max(0, 1 - age * 0.2 - bx / 70);
        const x0 = bx + age * 0.8, y0 = cy - age * 0.6;
        D.each(F, (x, y) => {
          const n = fbm(x / 3.2 - f * 0.9, y / 3.2 + bx, 31, 3);
          const d = Math.hypot(x - x0, (y - y0) * 1.1) / r + (n - 0.5) * 0.55;
          return d < 1 ? heat * (1.05 - d * 0.75) : 0;
        });
        if (age >= 2) D.each(Smk, (x, y) => {
          const n = fbm(x / 4 - f * 0.5, y / 4, 77 + bx, 3);
          const d = Math.hypot(x - x0 - 2, y - y0 + age * 1.4) / (r * 1.15);
          return d < 1 && n > 0.42 ? Math.max(0, 0.15 - (age - 2) * 0.025) * (1.4 - d) : 0;
        });
      }
    }
    const im = D.shade(img(Wd, H), Smk, SMOKE);
    D.over(im, D.shade(img(Wd, H), F, FIRE));
    const r = D.rng(500 + f);
    if (f >= 2) for (let k = 0; k < 6; k++) {
      const x = 10 + r() * (6 + f * 4), y = cy + (r() - 0.5) * (6 + f * 2.5);
      if (f >= 6 && bayer(Math.floor(x), Math.floor(y)) > 0.5) continue;
      put(im, x, y, r() < 0.4 ? P.G3 : P.O2);
    }
    frames.push(im);
  }
  return el("dl_fire", "DragonLine", Wd, H, [{ name: "fire", frames }], Array(N).fill(45), [["spit", 0, N - 1]]);
}

// the level up icons: a line cast across, the dragon riding it in an S; evolved, coiled in a spiral
// round a pearl of fire
function dragonPath(S, pts, width, f) {
  const F = D.field(S, S);
  for (let i = 1; i < pts.length; i++) {
    const [x0, y0] = pts[i - 1], [x1, y1] = pts[i], w0 = width(i - 1), w1 = width(i);
    D.each(F, (x, y) => {
      const vx = x1 - x0, vy = y1 - y0, L2 = vx * vx + vy * vy || 1;
      const t = clamp01(((x - x0) * vx + (y - y0) * vy) / L2);
      const d = Math.hypot(x - (x0 + vx * t), y - (y0 + vy * t)), half = lerp(w0, w1, t) / 2;
      if (d >= half) return 0;
      return 0.2 + 0.75 * (1 - d / half);
    });
  }
  const im = D.shade(img(S, S), F, QING);
  // a cream belly and red fins along it
  for (let i = 1; i < pts.length - 1; i++) {
    const [x, y] = pts[i], [xa, ya] = pts[i + 1], dx = xa - x, dy = ya - y, L = Math.hypot(dx, dy) || 1, nx = -dy / L, ny = dx / L;
    const w = width(i) / 2;
    put(im, x - nx * (w - 0.5), y - ny * (w - 0.5), P.CR2);
    if ((i + f) % 2 === 0) { put(im, x + nx * (w + 0.5), y + ny * (w + 0.5), P.R2); put(im, x + nx * (w + 1.5), y + ny * (w + 1.5), P.R3); }
  }
  return withOutline(im, OUT);
}
// a head small enough for the icons, 14x11, facing right: snout, eye, antler, mane
function miniHead(f) {
  const im = img(14, 11);
  const mane = poly(mask(14, 11), [[5, 2], [6, 8], [1 + (f % 2), 9], [0, 5], [2 + (f % 2), 2]]);
  for (let y = 0; y < 11; y++) for (let x = 0; x < 14; x++) if (has(mane, x, y)) put(im, x, y, x < 2 ? P.O2 : P.R2);
  const head = ellipse(mask(14, 11), 6, 5, 3, 2.8);
  poly(head, [[6, 3], [12, 3.5], [13.5, 5], [13, 7], [7, 7.5]]);
  shadeRows(im, head, [P.Q1, P.Q2, P.Q3, P.Q4]);
  for (let x = 8; x <= 13; x++) put(im, x, 7, P.CR2);
  px(im, [[8, 4], [9, 4]], P.G2); put(im, 9, 4, P.K);
  const horn = limb(mask(14, 11), [[7, 2], [4, 0]], 1);
  shade(im, horn, [P.G1, P.G2, P.G3]);
  const out = withOutline(im, OUT);
  for (let i = 0; i < 7; i++) put(out, 11 - i, 9 + Math.round(Math.sin(i * 0.9 + f * 1.5) * 0.6), P.G2);
  return out;
}
function makeDragonIcons() {
  const S = 32, N = 4, a = [], b = [];
  for (let f = 0; f < N; f++) {
    // the cast line, corner to corner, the dragon riding it in an S up to its head
    const im = img(S, S);
    for (let k = 0; k < S; k++) { const x = k, y = S - 3 - k * 0.8; put(im, x, y, (k + f * 3) % 8 < 2 ? P.W : P.Q3); if (bayer(x, y) < 0.5) put(im, x, y + 1, P.Q1); }
    const pts = Array.from({ length: 9 }, (_, i) => [2 + i * 2.4, 27 - i * 2.2 + Math.sin(i * 0.95 + f * 0.6) * 3]);
    D.over(im, dragonPath(S, pts, i => lerp(1.5, 5.5, i / 8), f));
    D.blit(im, miniHead(f), 17, 4);
    a.push(im);

    // evolved: coiled round a pearl of fire, its head at the spiral's end
    const ev = img(S, S), c = 16;
    const F = D.field(S, S);
    D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 3.5 ? 1 - d / 5 : 0; });
    D.shade(ev, F, FIRE);
    const spiral = Array.from({ length: 22 }, (_, i) => { const t = i / 21, th = Math.PI * 0.5 + t * TAU * 1.5, r = 4 + t * 9.5; return [c + Math.cos(th) * r, c + Math.sin(th) * r]; });
    D.over(ev, dragonPath(S, spiral, i => lerp(1.5, 5, i / 21), f));
    D.blit(ev, miniHead(f), 18, 0);
    for (let k = 0; k < 3; k++) { const th = f * 0.8 + k * 2.1; put(ev, c + Math.cos(th) * 2, c + Math.sin(th) * 2, P.W); }
    b.push(ev);
  }
  return [
    el("dl_icon", "DragonLine", S, S, [{ name: "icon", frames: a }], Array(N).fill(130), [["idle", 0, N - 1]]),
    el("dl_icon_evolved", "DragonLine", S, S, [{ name: "icon", frames: b }], Array(N).fill(130), [["idle", 0, N - 1]]),
  ];
}

// ================================================================ ICE CLOUD
// Yu Xian Shan: snow clouds in ice blues, the snow they drop, the piles it leaves, the ice that
// locks an enemy in place and the evolution's frost tornado

// a six-armed snowflake, the ice's version of the flare every impact opens on
function snowflake(F, cx, cy, len, v0 = 1, rot = 0) {
  for (let k = 0; k < 6; k++) {
    const a = rot + k * TAU / 6;
    D.ray(F, cx, cy, a, len, 2, v0, 0.45);
    // little barbs two thirds out
    const bx = cx + Math.cos(a) * len * 0.6, by = cy + Math.sin(a) * len * 0.6;
    D.ray(F, bx, by, a + 0.9, len * 0.3, 1, v0 * 0.8, 0.4);
    D.ray(F, bx, by, a - 0.9, len * 0.3, 1, v0 * 0.8, 0.4);
  }
}

// the cloud, 64x32: round lobes on top, a flat grey-blue underside, white where the light hits
// the upper right, wisps trailing off its ends. the lobes breathe a little over the loop
function makeCloud() {
  const Wd = 64, H = 36, N = 4, frames = [];
  // [x, y, radius]: the puffs, back to front
  const lobes = [[9, 23, 6], [53, 22, 7], [18, 18, 9], [44, 17, 10], [30, 14, 12], [24, 23, 8], [38, 23, 8]];
  const L = [0.55, -0.83];                                          // light from the upper right
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H), M = mask(Wd, H);
    const shown = lobes.map(([x, y, r], k) => [x, y + [0, -0.5, 0, 0.5][(f + k) % 4], r + [0, 0.35, 0.7, 0.35][(f + k * 2) % 4]]);
    for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) {
      if (y > 28) continue;                                         // a flat bottom
      // the front-most puff covering this pixel lights it like a ball
      let lit = null;
      for (const [cx, cy, r] of shown) {
        const dx = (x + 0.5 - cx) / r, dy = (y + 0.5 - cy) / r, d2 = dx * dx + dy * dy;
        if (d2 > 1) continue;
        const dz = Math.sqrt(1 - d2);
        lit = dx * L[0] + dy * L[1] + dz * 0.45;
      }
      if (lit === null) continue;
      D.mset(M, x, y);
      const v = lit + (bayer(x, y) - 0.5) * 0.18;
      put(im, x, y, v > 0.75 ? P.W : v > 0.45 ? P.I5 : v > 0.15 ? P.I4 : v > -0.15 ? P.I3 : P.I2);
    }
    // the underside's grey-blue shadow
    for (let x = 0; x < Wd; x++) for (let y = 26; y <= 28; y++) if (has(M, x, y)) put(im, x, y, y === 28 ? P.I1 : P.I2);
    const out = withOutline(im, P.I0);
    // wisps drifting off the ends
    for (const [x0, y0, dir] of [[2, 26, -1], [61, 25, 1]]) for (let i = 0; i < 3; i++) {
      const x = x0 + dir * (i + (f % 2)), y = y0 + (i % 2);
      if (bayer(x, y) < 0.6) put(out, x, y, P.I3);
    }
    frames.push(out);
  }
  return el("ic_cloud", "IceCloud", Wd, H, [{ name: "cloud", frames }], Array(N).fill(150), [["drift", 0, N - 1]]);
}

// the snow falling from a cloud, 96x144, the canvas centre on the middle of the ground it lands
// on (drawn for a 1.6 unit radius): just flakes, drifting down from the cloud's underside 50px
// above to their spots on the ground (an ellipse, the floor seen at three quarters), settling for
// a moment as they land. no ring and no frost on the floor: only the snow itself. loops
function makeSnow() {
  const Wd = 96, H = 144, cx = 48, cy = 72, RX = 44, RY = 18, FALL = 52, N = 6;
  const r = D.rng(1600);
  const flakes = [];
  while (flakes.length < 46) {
    // thicker toward the middle of the cloud
    const a = r() * TAU, d = Math.sqrt(r()) * (r() < 0.7 ? 0.8 : 1);
    flakes.push({ gx: cx + Math.cos(a) * d * RX, gy: cy + Math.sin(a) * d * RY, phase: r(), size: r() < 0.25 ? 2 : r() < 0.5 ? 1 : 0, sway: r() * TAU });
  }
  const frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    // the far flakes first, so the near ones fall in front of them
    for (const fl of [...flakes].sort((p, q) => p.gy - q.gy)) {
      const t = (fl.phase + f / N) % 1;
      const x = Math.round(fl.gx + Math.sin(fl.sway + t * TAU) * 2);
      if (t > 0.9) {
        // landed: a dab of snow on the floor that's gone by the next fall
        if (t < 0.97) { put(im, x, fl.gy, P.I4); put(im, x + 1, fl.gy, P.I3); }
        continue;
      }
      const y = Math.round(fl.gy - FALL * (1 - t / 0.9));
      if (fl.size === 2) {
        px(im, [[x, y - 1], [x - 1, y], [x + 1, y], [x, y + 1]], P.I5); put(im, x, y, P.W);
        put(im, x + 1, y + 1, P.I2);                      // a touch of shade so it reads on pale floors
      } else if (fl.size === 1) {
        put(im, x, y, P.W); put(im, x + 1, y, P.I4); put(im, x + 1, y + 1, P.I2);
      } else {
        put(im, x, y, t < 0.15 ? P.I4 : P.I5); put(im, x, y + 1, P.I2);
      }
    }
    frames.push(im);
  }
  return el("ic_snow", "IceCloud", Wd, H, [{ name: "snow", frames }], Array(N).fill(90), [["fall", 0, N - 1]]);
}

// a snow pile, 24x14: heaps up (frames 0-3, resting on 3), then melts away (4-7) into a puddle
function makePile() {
  const Wd = 24, H = 14, N = 8, frames = [];
  const heap = [0.35, 0.65, 0.9, 1, 0.8, 0.55, 0.3, 0.12];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H), k = heap[f], melting = f >= 4;
    const M = mask(Wd, H);
    const h = Math.max(1, 7 * k), w = 9 + 2 * k;
    for (let x = 0; x < Wd; x++) {
      const u = (x + 0.5 - 12) / w;
      if (Math.abs(u) >= 1) continue;
      const top = 11 - h * Math.pow(1 - u * u, 0.7) - (Math.abs(u - 0.25) < 0.2 ? k : 0);
      for (let y = Math.floor(top); y <= 11; y++) D.mset(M, x, y);
    }
    shadeRows(im, M, [P.I2, P.I3, P.I4, P.I5, P.I5, P.W]);
    // sparkles on the snow, and meltwater round the base as it goes
    if (!melting && f >= 2) { put(im, 15, Math.round(11 - h + 1), P.W); if (f === 3) put(im, 9, Math.round(11 - h * 0.7), P.W); }
    const out = withOutline(im, P.I0);
    if (melting) for (let x = 4 - f; x < 20 + f; x++) if (bayer(x, 12) < 0.6 - (f - 4) * 0.12) { put(out, x, 12, P.I2); if (x % 3 === 0) put(out, x, 13, P.I1); }
    frames.push(out);
  }
  return el("ic_pile", "IceCloud", Wd, H, [{ name: "pile", frames }], Array(N).fill(80), [["heap", 0, 3], ["melt", 4, 7]]);
}

// the ice over a frozen enemy, 24x24, played once and held on its last frame: a flash of frost,
// then jagged crystals growing up round the enemy, a thin film of ice over it (dithered, so the
// enemy shows through) and a glint running across
function makeIce() {
  const S = 24, c = 12, N = 6, frames = [];
  const shards = [[4, 22, 7, -0.25], [8, 23, 10, -0.1], [13, 23, 12, 0.05], [17, 22, 9, 0.2], [20, 21, 6, 0.35], [2, 17, 5, -0.5], [22, 16, 5, 0.5]];
  for (let f = 0; f < N; f++) {
    if (f === 0) { const F = D.field(S, S); snowflake(F, c, c + 3, 8, 1); frames.push(D.shade(img(S, S), F, ICE.concat([[0.95, P.W]]))); continue; }
    const grow = Math.min(1, f / 4);
    // the crystals, each a tall sharp wedge, outlined so they read over any enemy
    const solid = img(S, S);
    for (const [x, y, h, lean] of shards) {
      const M = poly(mask(S, S), [[x - 1.5, y], [x + 1.5, y], [x + lean * h * grow, y - h * grow]]);
      shade(solid, M, [P.I2, P.I3, P.I4, P.W], { dark: 0.4 });
    }
    const im = withOutline(solid, P.I0);
    // a thin shell of ice over the enemy: a dotted rim, frost specks and glints, no fill, so the
    // enemy shows through
    const R = 9 * Math.min(1, 0.4 + grow * 0.6);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      if (D.get(im, x, y)) continue;
      const d = Math.hypot((x + 0.5 - c) / R, (y + 0.5 - c - 1) / (R * 1.1));
      if (d > 1) continue;
      if (d > 0.86 && (x + y) % 2 === 0) put(im, x, y, P.I4);
      else if (hash2(x, y, 7) < 0.07 * grow) put(im, x, y, hash2(x, y, 3) < 0.5 ? P.W : P.I3);
    }
    // a glint sliding across the ice
    if (f >= 3) { const gx = 5 + (f - 3) * 5; for (let k = 0; k < 4; k++) put(im, gx + k, 15 - k * 2, k === 1 ? P.W : P.I5); }
    frames.push(im);
  }
  return el("ic_ice", "IceCloud", S, S, [{ name: "ice", frames }], Array(N).fill(60), [["freeze", 0, N - 1]]);
}

// the frost burst on an enemy the tornado's spray catches, 32x32: a snowflake flare, a ring of
// frost breaking up and shards of ice flung out
function makeIceBurst() {
  const S = 32, c = 16, N = 6, frames = [];
  const RAMP = ICE.concat([[0.95, P.W]]);
  for (let f = 0; f < N; f++) {
    let im;
    if (f === 0) { const F = D.field(S, S); snowflake(F, c, c, 12, 1); D.each(F, (x, y) => Math.hypot(x - c, y - c) < 3 ? 1 : 0); im = D.shade(img(S, S), F, RAMP); }
    else {
      const R = [0, 5, 8, 11, 13, 14][f];
      im = brokenRing(S, c, R, [0, 2.4, 2, 1.6, 1.3, 1][f], [0, 1, 0.85, 0.68, 0.52, 0.38][f], RAMP, 11 + f, f >= 2 ? 0.66 - f * 0.06 : 1);
      if (f === 1) { const F = D.field(S, S); snowflake(F, c, c, 6, 0.8, TAU / 12); D.over(im, D.shade(img(S, S), F, RAMP)); }
      const r = D.rng(61);
      for (let k = 0; k < 8; k++) {
        const a = r() * TAU, d = 3 + f * (2 + r() * 1.3), x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
        if (f >= 4 && bayer(Math.floor(x), Math.floor(y)) > 0.5) continue;
        put(im, x, y, P.I5); put(im, x + Math.cos(a), y + Math.sin(a), P.I3);
      }
    }
    frames.push(im);
  }
  return el("ic_burst", "IceCloud", S, S, [{ name: "burst", frames }], Array(N).fill(40), [["burst", 0, N - 1]]);
}

// the frost tornado, 96x120, seen from three quarters above like the rest of the game: a funnel
// of wind bands winding up from a point on the ground to a wide mouth, and into that mouth we see
// down on the vortex itself, spiral arms of snow wheeling round a dark eye. round the foot a
// churning ring of snow; chunks of snow and ice shards whirled round it at every height, behind
// it and in front, with streaks behind them; white gusts wrapping it; snow flung out off the top.
// the base sits 44px below the canvas centre. loops over 6 frames
const TORNADO = { W: 96, H: 120, cx: 48, base: 104, top: 24, N: 6, ws: 1, detail: true };
function tornadoFrame(f, T = TORNADO) {
  const { W: Wd, H, cx, base, top, N, ws, detail } = T;
  const spin = f * TAU / N;
  const width = k => (4 + 32 * Math.pow(k, 1.55)) * ws;              // half width, k 0 at the foot, 1 at the mouth
  const axis = k => cx + (Math.sin(k * 2.6 + spin) * 3 * k + k * 4) * ws;   // it bends and wobbles as it turns
  const back = img(Wd, H), body = img(Wd, H), front = img(Wd, H);

  // the ground: a ring of snow churned up round the foot, in streaks turning with it
  for (let y = Math.floor(base - 16 * ws); y <= Math.ceil(base + 14 * ws); y++) for (let x = 0; x < Wd; x++) {
    const dx = (x + 0.5 - cx) / (42 * ws), dy = (y + 0.5 - base) / (14 * ws), d = Math.hypot(dx, dy);
    if (d > 1 || d < 0.18) continue;
    const ang = Math.atan2(dy, dx), streak = (ang * 3 + d * 9 - spin * 2 + TAU * 8) % TAU;
    if (streak > 2.6) continue;
    if (bayer(x, y) > 1.2 - d * 0.9) continue;
    put(dy < 0 ? back : front, x, y, streak < 0.7 ? P.I4 : d < 0.55 ? P.I3 : P.I2);
  }

  // the funnel: wind bands spiralling up it, lit on the right, the far side of each band darker
  const topY = top + 8 * ws;
  for (let y = topY; y <= base; y++) {
    const k = (base - y) / (base - topY), w = width(k), ax = axis(k);
    for (let x = Math.floor(ax - w); x <= Math.ceil(ax + w); x++) {
      const u = (x + 0.5 - ax) / w;
      if (Math.abs(u) > 1) continue;
      const theta = Math.asin(u);                                    // round the front of the funnel
      const band = ((theta * 6 / Math.PI + y * 0.28 / ws - f * 12 / N) % 6 + 6) % 6;
      const lit = u > 0.25, dark = u < -0.55;
      let c = band < 1.4 ? P.W : band < 2.6 ? P.I4 : band < 4 ? P.I3 : P.I2;
      if (lit && c === P.I3) c = P.I4;
      if (dark) c = c === P.W ? P.I4 : c === P.I4 ? P.I3 : P.I1;
      if (band >= 5.2 && Math.abs(u) < 0.8) c = P.I1;                // the gaps between the bands
      put(body, x, y, c);
    }
  }
  // the mouth, seen from above: spiral arms of snow round a dark eye, a bright lip round it
  const mx = axis(1), rx = width(1) + 2, ry = rx * 0.36;
  for (let y = Math.floor(topY - ry); y <= Math.ceil(topY + ry); y++) for (let x = Math.floor(mx - rx); x <= Math.ceil(mx + rx); x++) {
    const dx = (x + 0.5 - mx) / rx, dy = (y + 0.5 - topY) / ry, r = Math.hypot(dx, dy);
    if (r > 1) continue;
    const phi = Math.atan2(dy, dx);
    const arm = ((phi * 3 - r * 7 + spin * 3) % TAU + TAU) % TAU / TAU;   // three arms, winding in
    let c;
    if (r > 0.86) c = (x + y + f) % 3 === 0 ? P.W : P.I4;                // the lip
    else if (r < 0.2) c = r < 0.1 ? P.I0 : P.I1;                          // the eye
    else c = arm < 0.18 ? P.W : arm < 0.4 ? P.I4 : arm < 0.65 ? P.I3 : r < 0.45 ? P.I1 : P.I2;
    put(body, x, y, c);
  }
  const solid = withOutline(body, P.I0);
  // frost lightning crackling down the funnel now and then
  if (detail && (f === 1 || f === 4)) {
    const r3 = D.rng(300 + f), k0 = 0.85, k1 = 0.15;
    const pts = D.bolt(r3, axis(k0) + (f === 1 ? -8 : 10) * ws, base - k0 * (base - topY), axis(k1) + (f === 1 ? 4 : -3) * ws, base - k1 * (base - topY), 7 * ws, 3);
    const M = D.polyline(mask(Wd, H), pts);
    D.paint(solid, D.minus(D.dilate(M, 1, false), M), P.I3);
    D.paint(solid, M, P.W);
  }
  // a cold glow round it, dithered, so it stands out on any floor
  const glow = detail ? halo(solid, P.I1, 0.5, f, 2) : img(Wd, H);

  // snow chunks and ice shards whirled round it: behind it when on the far side, in front on the
  // near side, each with a short streak behind it
  const orbit = [[0.08, 5, 0.55], [0.3, 6, 0.4], [0.55, 7, 0.34], [0.8, 6, 0.3], [0.97, 5, 0.26]];   // [height, how many, ry/rx]
  if (detail) orbit.forEach(([k, n, sq], j) => {
    const y0 = base - k * (base - topY), R = width(k) + (7 + j * 2) * ws, ax = axis(k);
    for (let i = 0; i < n; i++) {
      const a = spin * (1.4 - k * 0.5) + j * 1.9 + i * TAU / n;
      const layer = Math.sin(a) > 0 ? front : back;
      const x = ax + Math.cos(a) * R, y = y0 + Math.sin(a) * R * sq;
      for (let s2 = 1; s2 <= 6; s2++) {                                 // the streak behind it
        const b2 = a - s2 * 0.07, sx = ax + Math.cos(b2) * R, sy = y0 + Math.sin(b2) * R * sq;
        if (bayer(Math.round(sx), Math.round(sy)) < 1 - s2 * 0.14) put(layer, sx, sy, s2 <= 2 ? P.I4 : s2 <= 4 ? P.I3 : P.I2);
      }
      if ((i + j) % 3 === 0) {                                          // an ice shard, long and glinting
        px(layer, [[x, y - 2], [x, y - 1], [x - 1, y], [x + 1, y], [x, y + 1]], P.I3); put(layer, x, y, P.W); put(layer, x, y - 1, P.I5);
        px(layer, [[x - 1, y - 2], [x + 1, y + 1]], P.I1);
      } else if ((i + j) % 3 === 1) {                                   // a big chunk of snow
        px(layer, [[x - 1, y], [x, y - 1], [x + 1, y - 1], [x, y], [x + 1, y], [x + 2, y], [x, y + 1], [x + 1, y + 1]], P.I5);
        px(layer, [[x + 1, y - 1], [x + 2, y]], P.W); px(layer, [[x - 1, y + 1], [x, y + 2], [x + 1, y + 2]], P.I1);
      } else {                                                          // a small one
        px(layer, [[x, y], [x + 1, y], [x, y + 1], [x + 1, y + 1]], P.I5); put(layer, x + 1, y, P.W); put(layer, x, y + 2, P.I1);
      }
    }
  });
  // gusts: white arcs wrapping the funnel's near side
  for (let g = 0; g < (detail ? 3 : 0); g++) {
    const k = 0.2 + g * 0.3, y0 = base - k * (base - topY), R = width(k) + 3 * ws, ax = axis(k), a0 = spin * 2 + g * 2.2;
    for (let i = 0; i < 14; i++) {
      const a = a0 + i * 0.1;
      if (Math.sin(a) <= 0.05) continue;
      put(front, ax + Math.cos(a) * R, y0 + Math.sin(a) * R * 0.3, i > 10 ? P.I4 : P.W);
    }
  }
  // snow flung out off the top, drifting outward over the loop
  const rr = D.rng(88);
  for (let i = 0; i < (detail ? 14 : 4); i++) {
    const a = rr() * TAU, t = (rr() + f / N) % 1, d = rx * (0.9 + t * 0.6);
    const x = mx + Math.cos(a) * d, y = topY + Math.sin(a) * d * 0.36 - t * 10 * ws;
    if (bayer(Math.round(x), Math.round(y)) < 1 - t * 0.7) put(Math.sin(a) > 0 ? front : back, x, y, t < 0.5 ? P.W : P.I3);
  }

  const out = img(Wd, H);
  D.over(out, glow); D.over(out, back); D.over(out, solid); D.over(out, front);
  return out;
}
function makeTornado() {
  const frames = [];
  for (let f = 0; f < TORNADO.N; f++) frames.push(tornadoFrame(f));
  return el("ic_tornado", "IceCloud", TORNADO.W, TORNADO.H, [{ name: "tornado", frames }], Array(TORNADO.N).fill(70), [["spin", 0, TORNADO.N - 1]]);
}

function makeIceIcons() {
  const S = 32, N = 4, a = [], b = [];
  for (let f = 0; f < N; f++) {
    // a small cloud with snow falling onto a pile under it
    const im = img(S, S), M = mask(S, S);
    [[10, 9, 5], [17, 7, 6.5], [24, 10, 4.5]].forEach(([x, y, r]) => ellipse(M, x, y, r, r * 0.85));
    for (let i = 0; i < M.m.length; i++) if (Math.floor(i / S) > 13) M.m[i] = 0;
    shadeRows(im, M, [P.I2, P.I3, P.I4, P.I5, P.W]);
    const pile = mask(S, S);
    for (let x = 8; x < 25; x++) { const u = (x - 16) / 8.5; for (let y = Math.floor(28 - 4.5 * (1 - u * u)); y <= 28; y++) D.mset(pile, x, y); }
    shadeRows(im, pile, [P.I2, P.I3, P.I4, P.W]);
    const out = withOutline(im, P.I0);
    for (let k = 0; k < 6; k++) { const x = 9 + k * 3, y = 16 + ((k * 5 + f * 2) % 9); twinkle(out, x, y, k % 3 === 0 ? 1 : 0, [P.W, P.I3]); }
    a.push(out);

    // evolved: the frost tornado, a small one, seen from three quarters above like the real one
    const evo = tornadoFrame(f, { W: 32, H: 32, cx: 15, base: 27, top: 3, N: 4, ws: 0.33, detail: false });
    b.push(evo);
  }
  return [
    el("ic_icon", "IceCloud", S, S, [{ name: "icon", frames: a }], Array(N).fill(130), [["idle", 0, N - 1]]),
    el("ic_icon_evolved", "IceCloud", S, S, [{ name: "icon", frames: b }], Array(N).fill(130), [["idle", 0, N - 1]]),
  ];
}

// ================================================================ FLYING SWORD
// Feijian: a slim jade jian flown by telekinesis, and the Imperial Sword Cage's lasers

// the blade alone, pointing right, on a canvas `Wd` wide: jade edges, a white ridge, a gold guard,
// a short grip and a red tassel. glint: where the travelling highlight is (-1 for none)
function sword(Wd, H, cy, glint, tassel) {
  const im = img(Wd, H), tip = Wd - 2;
  for (let x = 8; x <= tip; x++) {
    const taper = x > tip - 4 ? tip - x : 2;
    put(im, x, cy, x === glint || x === glint + 1 ? P.W : P.J5);            // the ridge
    if (taper >= 1) { put(im, x, cy - 1, P.J3); put(im, x, cy + 1, P.J2); }
    if (taper >= 2 && x < tip - 6) put(im, x, cy + 1, P.J2);
  }
  put(im, tip + 1, cy, P.W);
  // the guard, gold, winged; the grip wrapped dark; a pommel
  px(im, [[7, cy - 2], [7, cy - 1], [7, cy], [7, cy + 1], [7, cy + 2], [6, cy - 2], [6, cy + 2]], P.G2);
  px(im, [[7, cy - 1], [7, cy]], P.G3);
  for (let x = 3; x <= 5; x++) put(im, x, cy, x % 2 ? P.S1 : P.S2);
  put(im, 2, cy, P.G2);
  const out = withOutline(im, OUT);
  // the tassel streams back off the pommel
  for (let i = 0; i < 3; i++) put(out, 1 - i + 1, cy + 1 + Math.round(Math.sin(i + tassel) * 0.8), i ? P.R2 : P.R3);
  return out;
}

// in flight, 30x9: the blade with a glint running up it, a dithered qi glow round it and the
// tassel flicking
function makeBlade() {
  const Wd = 30, H = 9, cy = 4, N = 4, frames = [], glows = [];
  for (let f = 0; f < N; f++) {
    frames.push(sword(Wd, H, cy, 10 + f * 4, f * 1.6));
    const G = img(Wd, H);
    for (let y = 1; y < H - 1; y++) for (let x = 8; x < Wd; x++)
      if (Math.abs(y - cy) === 2 || (Math.abs(y - cy) === 3 && x > 14)) if (bayer(x + f, y) < 0.45 - (x - 8) * 0.01) put(G, x, y, P.J2);
    glows.push(G);
  }
  return el("fs_blade", "FlyingSword", Wd, H, [{ name: "glow", frames: glows }, { name: "blade", frames }], Array(N).fill(60), [["fly", 0, N - 1]]);
}

// embedded in the screen's edge, 32x21, tip to the right at x 30: the grip end quivering, and
// where the tip went in, a slit of jade light along the edge with cracks running up and down it,
// qi leaking out and chips of the edge flying on the first frames
function makeEmbed() {
  const Wd = 32, H = 21, cy = 10, N = 4, frames = [];
  const cracks = [[-1, 8, 71], [1, 8, 72], [-1, 5, 73], [1, 5, 74]];      // [up or down, length, seed]
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    // the slit along the edge, brightest at the tip
    for (let y = 0; y < H; y++) {
      const d = Math.abs(y - cy);
      if (d > 9) continue;
      put(im, 30, y, d < 2 ? P.W : d < 4 ? P.J5 : d < 7 ? P.J3 : P.J2);
      if (d < 5 && bayer(29, y + f) < 0.5) put(im, 29, y, P.J2);
    }
    // cracks forking off the slit, back into the screen a little
    for (const [dir, len, seed] of cracks) {
      const r = D.rng(seed), x1 = 30 - 3 - r() * 3, y1 = cy + dir * len;
      const M = D.polyline(mask(Wd, H), D.bolt(r, 30, cy + dir * 1.5, x1, y1, 1.6, 2));
      D.paint(im, M, (f + seed) % 2 ? P.J4 : P.J3);
    }
    const quiver = [0, 1, 0, -1][f];
    const blade = sword(Wd - 1, H, cy, f === 0 ? 20 : -1, f * 1.6);
    // the grip end shakes, the tip stays put in the edge
    for (let y = 0; y < H; y++) for (let x = 0; x < Wd - 1; x++) {
      const c = D.get(blade, x, y);
      if (c && x < 30) put(im, x, y + (x < 12 ? quiver : 0), c);
    }
    if (f < 2) px(im, [[27 - f * 2, cy - 5 - f], [26 - f * 2, cy + 6 + f], [28, cy + 3]], P.J5);
    frames.push(im);
  }
  return el("fs_embed", "FlyingSword", Wd, H, [{ name: "embed", frames }], Array(N).fill(70), [["quiver", 0, N - 1]]);
}

// the cage's laser, one 32x11 tile repeated along it: a white core in jade bands, bright pulses
// running along, sparks on the edges. seamless in x
function makeLaser() {
  const Wd = 32, H = 11, N = 4, frames = [];
  const bands = [P.J1, P.J2, P.J3, P.J4, P.J5, P.W, P.J5, P.J4, P.J3, P.J2, P.J1];
  const lit = [P.J2, P.J3, P.J4, P.J5, P.W, P.W, P.W, P.J5, P.J4, P.J3, P.J2];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H), r = D.rng(3100 + f);
    for (let x = 0; x < Wd; x++) {
      let node = 99;
      for (let n = 0; n < 2; n++) { const xn = (n * 16 + f * 4) % Wd; node = Math.min(node, Math.abs(((x - xn + 16 + Wd * 2) % Wd) - 16)); }
      for (let y = 0; y < H; y++) {
        const edge = y === 0 || y === H - 1;
        if (edge && node > 1 && bayer(x + f, y) > 0.5) continue;
        put(im, x, y, node <= 1 ? lit[y] : bands[y]);
      }
    }
    for (let k = 0; k < 3; k++) put(im, Math.floor(r() * Wd), r() < 0.5 ? 0 : H - 1, P.W);
    frames.push(im);
  }
  return el("fs_laser", "FlyingSword", Wd, H, [{ name: "laser", frames }], Array(N).fill(40), [["burn", 0, N - 1]], { pivot: "left", fullRect: true });
}

// a ricochet, 24x24: a white flare, a jade ring breaking up and hot metal sparks flying off
function makeSpark() {
  const S = 24, c = 12, N = 5, frames = [];
  const RAMP = JADE;
  for (let f = 0; f < N; f++) {
    let im;
    if (f === 0) im = flareFrame(S, c, 10, RAMP, 4);
    else {
      const R = [0, 4, 7, 9, 10][f];
      im = brokenRing(S, c, R, [0, 2, 1.6, 1.3, 1][f], [0, 0.95, 0.75, 0.55, 0.4][f], RAMP, 21 + f, f >= 2 ? 0.6 : 1);
      const r = D.rng(77);
      for (let k = 0; k < 6; k++) {
        const a = r() * TAU, d = 2 + f * (2 + r()), x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
        put(im, x, y, f < 3 ? P.W : P.G3); if (f < 3) put(im, x - Math.cos(a), y - Math.sin(a), P.G2);
      }
    }
    frames.push(im);
  }
  return el("fs_spark", "FlyingSword", S, S, [{ name: "spark", frames }], Array(N).fill(40), [["spark", 0, N - 1]]);
}

function makeSwordIcons() {
  const S = 32, N = 4, a = [], b = [];
  for (let f = 0; f < N; f++) {
    // the sword ricocheting: a zigzag of afterimages off the icon's edges, the sword at its end
    const im = img(S, S);
    const zig = [[2, 26], [12, 6], [22, 24], [30, 10]];
    for (let i = 1; i < zig.length; i++) {
      const M = D.line(mask(S, S), zig[i - 1][0], zig[i - 1][1], zig[i][0], zig[i][1]);
      D.paint(im, M, i === zig.length - 1 ? P.J3 : P.J2, i === 1 ? 0.4 : 0.7, f);
    }
    twinkle(im, 12, 6, 1, [P.W, P.J4]); twinkle(im, 22, 24, f % 2, [P.W, P.J4]);
    const blade = rotate(sword(30, 9, 4, 10 + f * 4, f * 1.6), Math.atan2(10 - 24, 30 - 22), 30, 30);
    D.blit(im, blade, 8, 1);
    a.push(im);

    // evolved: two swords stuck in the icon's left and right edges, the laser burning between them
    const ev = img(S, S), cy = 16;
    for (let x = 1; x <= 30; x++) {
      const pulse = (x + f * 3) % 8 < 2;
      put(ev, x, cy - 2, pulse ? P.J3 : P.J1); put(ev, x, cy - 1, pulse ? P.J5 : P.J3); put(ev, x, cy, P.W);
      put(ev, x, cy + 1, pulse ? P.J5 : P.J3); put(ev, x, cy + 2, pulse ? P.J3 : P.J1);
    }
    const right = sword(16, 9, 4, -1, f * 1.6), left = img(16, 9);
    for (let y = 0; y < 9; y++) for (let x = 0; x < 16; x++) { const c = D.get(right, x, y); if (c) put(left, 15 - x, y, c); }
    D.blit(ev, right, 16, cy - 4 - 7);
    D.blit(ev, left, 0, cy - 4 + 7);
    for (const [x, y] of [[31, cy - 7], [0, cy + 7]]) for (let k = -3; k <= 3; k++) put(ev, x, y + k, Math.abs(k) < 2 ? P.W : P.J3);
    b.push(ev);
  }
  return [
    el("fs_icon", "FlyingSword", S, S, [{ name: "icon", frames: a }], Array(N).fill(130), [["idle", 0, N - 1]]),
    el("fs_icon_evolved", "FlyingSword", S, S, [{ name: "icon", frames: b }], Array(N).fill(130), [["idle", 0, N - 1]]),
  ];
}

// ================================================================ THE ARENA'S SPIRIT SEALS
// the Final Rush's wall: yellow talismans on peach-wood stakes, planted in a ring. the glyph
// burns red and the paper carries the violet rim the Command Token's seals get when charged, so
// the ring reads as a barrier and not as scenery

// one seal, 16x30, the stake's foot on the bottom row. f runs the loop: paper stirring, glyph pulsing
function seal(f, charge = 1) {
  const Wd = 16, H = 30, im = img(Wd, H);
  // the stake, sharpened into the ground, a cord binding the paper to it
  for (let y = 4; y <= 28; y++) { put(im, 7, y, P.WD1); put(im, 8, y, y < 20 ? P.WD2 : P.WD1); }
  put(im, 7, 29, P.WD0); put(im, 8, 3, P.WD3); put(im, 7, 3, P.WD2);
  // the paper, hung off the stake's head, drawn straight, then its lower half bent in the wind
  const lift = [0, 1, 1, 0, -1, 0][f % 6];
  const pap = img(Wd, H), paper = rect(mask(Wd, H), 3, 5, 13, 22);
  shade(pap, paper, [P.TL0, P.TL1, P.TL1, P.TL2], { dark: 0.2, light: 0.75 });
  for (let y = 5; y <= 22; y++) for (let x = 3; x <= 13; x++) if (x === 3 || x === 13 || y === 5 || y === 22) put(pap, x, y, P.R1);
  const glow = [P.R1, P.R2, P.R3, P.R2, P.R1, P.R1][f % 6];
  const g = ["..####..", "...##...", ".######.", "..#..#..", ".######.", "...##...", ".#.##.#.", "...##...", "..#..#.."];
  g.forEach((row, j) => [...row].forEach((ch, i) => { if (ch === "#") put(pap, 4 + i, 8 + j, glow); }));
  px(pap, [[6, 6], [7, 6], [8, 6], [9, 6], [7, 7], [8, 7]], P.R2);          // the cord
  put(pap, 12, 22, P.TL0);                                                  // a curled corner
  for (let y = 0; y < H; y++) {
    const dx = y < 15 ? 0 : Math.round(lift * (y - 14) / 8);
    for (let x = 0; x < Wd; x++) { const c = D.get(pap, x, y); if (c) put(im, x + dx, y, c); }
  }
  const out = withOutline(im, OUT);
  // the charged rim, pulsing
  if (charge > 0) {
    const rim = halo(out, (f % 3 === 0) ? P.V5 : P.V4, charge * (f % 2 ? 0.8 : 0.55), f);
    D.over(rim, out);
    return rim;
  }
  return out;
}
function makeSeal() {
  const N = 6, frames = [];
  for (let f = 0; f < N; f++) frames.push(seal(f));
  return el("arena_seal", "Arena", 16, 30, [{ name: "seal", frames }], Array(N).fill(110), [["stand", 0, N - 1]]);
}

// a seal rising, 32x40, the seal centred as in arena_seal: a violet flare at the ground, a pillar
// of qi, the seal pushing up out of the floor, a ring on the ground breaking into dashes and
// sparks. its last frame is the standing seal's first
function makeRise() {
  const Wd = 32, H = 40, ox = 8, oy = 5, gy = oy + 29, N = 8, frames = [];
  const VIOLET = [[0.1, P.V1], [0.2, P.V2], [0.34, P.V3], [0.5, P.V4], [0.66, P.V5], [0.82, P.V6], [0.94, P.W]];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H), F = D.field(Wd, H);
    if (f === 0) { D.flare(F, 16, gy, 9, 3, 4, 1, 1, 0.4); D.each(F, (x, y) => Math.hypot(x - 16, y - gy) < 2.5 ? 1 : 0); }
    if (f >= 1 && f <= 4) {
      // the pillar: a column of light shooting up, thinning as the seal comes up through it
      const half = [0, 3.5, 3, 2, 1][f], top = [0, 2, 0, 0, 4][f];
      D.each(F, (x, y) => y >= top && y <= gy && Math.abs(x - 16) < half ? 0.95 - Math.abs(x - 16) / (half * 2.2) - (gy - y) / 90 : 0);
    }
    if (f >= 1) {
      // the ring on the ground, squashed into an ellipse, spreading and breaking up
      const R = [0, 4, 7, 9, 11, 12, 13, 14][f];
      D.each(F, (x, y) => {
        const d = Math.hypot(x - 16, (y - gy) * 2.6);
        if (d > R + 0.5 || d < R - 1.6) return 0;
        if (f >= 4 && vnoise(Math.atan2(y - gy, x - 16) * 4, f, 5) > 0.62 - f * 0.03) return 0;
        return [0, 0.95, 0.85, 0.7, 0.58, 0.46, 0.36, 0.26][f];
      });
    }
    D.shade(im, F, VIOLET);
    // the seal coming up out of the ground: only what's above the floor shows
    if (f >= 2) {
      const up = [0, 0, 0.3, 0.6, 0.85, 1, 1, 1][f], sp = seal(f === N - 1 ? 0 : f % 6, f >= 5 ? 1 : 0), cut = 30 - Math.round(30 * up);
      for (let y = 0; y < sp.h; y++) for (let x = 0; x < sp.w; x++) {
        const c = D.get(sp, x, y);
        if (c && y + cut < sp.h) put(im, ox + x, oy + y + cut, f <= 3 && y < 3 ? P.W : c);
      }
    }
    // sparks thrown up
    const r = D.rng(90 + f);
    if (f >= 2 && f <= 6) for (let k = 0; k < 4; k++) put(im, 16 + (r() - 0.5) * (6 + f * 3), gy - 2 - r() * f * 3, k % 2 ? P.V6 : P.R3);
    frames.push(im);
  }
  return el("arena_rise", "Arena", Wd, H, [{ name: "rise", frames }], Array(N).fill(50), [["rise", 0, N - 1]]);
}

// ================================================================ BACKS
// a character's weapon worn on their back, at the characters' pixel size (33.9 px per unit), in
// their palette: slung diagonally, the grip up by the right shoulder and the far end down behind
// the left hip, the way the Cinnabar Ink Brush hangs. 24x32, the character sprite's point
// (18, 25) (its back, between the shoulder blades) sits on this canvas's centre. drawn over the
// character, so it reads the way the quiver on Zhuo Lan's back does

const BACK_W = 24, BACK_H = 32;
// the sling line: from the low end behind the hip up to the grip behind the neck (on the
// character: (10, 37) to (21, 13), clear of the face)
const BX0 = 4, BY0 = 28, BX1 = 15, BY1 = 4, BACK_L = Math.hypot(BX1 - BX0, BY1 - BY0);
// every pixel's place along the diagonal (u, 0 at the low end) and across it (v, < 0 on the lit side)
function diagonal(fn) {
  const im = img(BACK_W, BACK_H), x0 = BX0, y0 = BY0, x1 = BX1, y1 = BY1;
  const L = Math.hypot(x1 - x0, y1 - y0), dx = (x1 - x0) / L, dy = (y1 - y0) / L;
  for (let y = 0; y < BACK_H; y++) for (let x = 0; x < BACK_W; x++) {
    const px0 = x + 0.5 - x0, py0 = y + 0.5 - y0;
    const u = px0 * dx + py0 * dy, v = px0 * dy - py0 * dx;
    const c = fn(u, v, L);
    if (c) put(im, x, y, c);
  }
  return im;
}
const along = (u, L) => [BX0 + (BX1 - BX0) * u / L, BY0 + (BY1 - BY0) * u / L];
function tassel(im, u, L, f, cols) {
  const [x, y] = along(u, L);
  for (let i = 0; i < 4; i++) put(im, x + 1 + i * 0.4 + [0, 1, 0, -1][(f + i) % 4] * (i > 1 ? 1 : 0), y + i, i < 2 ? cols[0] : cols[1]);
}

// Zhuo Lan's recurved bow, the string on the side against his back, a glint along the stave
function backBow(f) {
  const im = diagonal((u, v, L) => {
    if (u < -1 || u > L + 1) return null;
    const t = u / L, bend = Math.sin(t * Math.PI) * 3.2 - (t < 0.08 || t > 0.92 ? 1.2 : 0);   // the recurve at the tips
    if (Math.abs(v - bend) <= 0.8) {
      if (Math.abs(t - 0.5) < 0.08) return P.WD0;                                             // the wrapped grip
      if (t < 0.05 || t > 0.95) return P.G2;                                                  // horn tips
      if (Math.abs(u - (5 + f * 5)) < 0.8) return P.WD3;                                      // a glint
      return v - bend < 0 ? P.WD2 : P.WD1;
    }
    if (Math.abs(v + 0.3) <= 0.5 && t > 0.06 && t < 0.94) return P.CR1;                       // the string
    return null;
  });
  return withOutline(im, OUT);
}

// Yun Xi's peach-wood sword, red script down the blade, a gold guard, a red tassel, and a yellow
// talisman tied under the guard, fluttering
function backPeach(f) {
  const im = diagonal((u, v, L) => {
    if (u < 0 || u > L) return null;
    const t = u / L;
    if (t > 0.78) return Math.abs(v) <= 0.9 ? (t > 0.95 ? P.G2 : (u | 0) % 2 ? P.R1 : P.R0) : null;          // the grip, wrapped red
    if (t > 0.72) return Math.abs(v) <= 2.4 ? (v < 0 ? P.G3 : P.G1) : null;                            // the guard
    const half = t < 0.06 ? 0.6 : 1.3;
    if (Math.abs(v) > half) return null;
    if (Math.abs(v) < 0.5 && (u | 0) % 5 === 2) return P.R2;                                            // script
    return v < -0.2 ? P.WD3 : v > 0.6 ? P.WD1 : P.WD2;
  });
  const out = withOutline(im, OUT);
  tassel(out, BACK_L * 0.84, BACK_L, f, [P.R3, P.R2]);
  // the talisman, hanging off the guard
  const [x, y] = along(BACK_L * 0.7, BACK_L), sway = [0, 1, 1, 0][f];
  const tal = img(BACK_W, BACK_H);
  for (let yy = 0; yy < 7; yy++) for (let xx = 0; xx < 3; xx++) put(tal, x - 5 + xx + (yy > 3 ? sway : 0), y + 1 + yy, xx === 2 ? P.TL2 : P.TL1);
  px(tal, [[x - 4 + (0), y + 3], [x - 4 + sway, y + 5]], P.R2);
  D.over(out, withOutline(tal, OUT));
  return out;
}

// Ye Tianshu's jian in its dark scabbard: silver fittings, a gold guard, a blue tassel
function backJian(f) {
  const im = diagonal((u, v, L) => {
    if (u < 0 || u > L) return null;
    const t = u / L;
    if (t > 0.82) return Math.abs(v) <= 0.9 ? (t > 0.96 ? P.G2 : P.WD1) : null;                        // grip and pommel
    if (t > 0.77) return Math.abs(v) <= 2.2 ? (v < 0 ? P.G3 : P.G1) : null;                            // the guard
    if (Math.abs(v) > 1.3) return null;
    if (t < 0.07 || (t > 0.68 && t < 0.72) || Math.abs(t - 0.4) < 0.025) return v < 0 ? P.SL2 : P.SL1; // silver fittings
    if (Math.abs(u - (4 + f * 4)) < 0.7 && v < 0) return P.SL2;                                         // a glint on the lacquer
    return v < -0.3 ? P.A2 : v > 0.5 ? P.S0 : P.A1;
  });
  const out = withOutline(im, OUT);
  tassel(out, BACK_L * 0.86, BACK_L, f, [P.A3, P.A2]);
  return out;
}

function makeBacks() {
  const N = 4, list = [["back_bow", "Bow", backBow], ["back_peach", "PeachTalismans", backPeach], ["back_jian", "SevenStarSwords", backJian]];
  return list.map(([name, group, draw]) => {
    const frames = [];
    for (let f = 0; f < N; f++) frames.push(draw(f));
    return el(name, group, BACK_W, BACK_H, [{ name: "back", frames }], Array(N).fill(160), [["idle", 0, N - 1]]);
  });
}

// the cast wearing them: each character's frames (from ../characters/out) with their weapon on
// their back where the game puts it, written to out/preview_backs.png
function previewBacks() {
  const dir = path.join(__dirname, "..", "characters", "out");
  if (!fs.existsSync(dir)) return;
  const cast = [["qing_warrior_vs", backBow], ["yun_xi", backPeach], ["ye_tianshu", backJian]];
  const bob = [0, 0, 1, 1, 0, -1, 0, -1], shots = [];
  for (const [name, draw] of cast) for (let i = 0; i < 8; i++) {
    const body = png.decode(path.join(dir, name, `L0_${i}.png`)), im = img(48, 48);
    D.blit(im, body, 0, 0);
    D.blit(im, draw(i % 4), 18 - BACK_W / 2, 25 - BACK_H / 2 + bob[i]);
    shots.push(im);
  }
  png.encode(png.preview(shots, 6, [58, 50, 62], 8, 2), path.join(__dirname, "out", "preview_backs.png"));
}

// ================================================================ write everything
const BUILDERS = {
  dragon: () => [...makeDragon(), makeLine(), makeBite(), makeFire(), ...makeDragonIcons()],
  sword: () => [makeBlade(), makeEmbed(), makeLaser(), makeSpark(), ...makeSwordIcons()],
  arena: () => [makeSeal(), makeRise()],
  backs: () => makeBacks(),
  ice: () => [makeCloud(), makeSnow(), makePile(), makeIce(), makeIceBurst(), makeTornado(), ...makeIceIcons()],
};

function build() {
  const only = process.argv.slice(2);
  const elements = [];
  for (const [name, make] of Object.entries(BUILDERS)) if (!only.length || only.includes(name)) elements.push(...make());
  const out = path.join(__dirname, "out");
  if (!only.length) fs.rmSync(out, { recursive: true, force: true });
  fs.mkdirSync(out, { recursive: true });
  const manifestFile = path.join(out, "manifest.json");
  const old = only.length && fs.existsSync(manifestFile) ? JSON.parse(fs.readFileSync(manifestFile, "utf8")).elements : [];
  const manifest = old.filter(m => !elements.some(e => e.name === m.name));
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.rmSync(dir, { recursive: true, force: true });
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length, flat = [];
    for (const l of e.layers) if (l.frames.length !== n) throw new Error(`${e.name}: layer ${l.name} has ${l.frames.length} frames, not ${n}`);
    if (e.durations.length !== n) throw new Error(`${e.name}: ${e.durations.length} durations for ${n} frames`);
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
    const scale = e.w <= 24 ? 8 : e.w <= 48 ? 6 : e.w <= 100 ? 4 : 2;
    png.encode(png.preview(flat, scale, [22, 18, 30], Math.min(flat.length, e.w <= 48 ? 8 : 6), 2), path.join(out, `sheet_${e.name}.png`));
  }
  fs.writeFileSync(manifestFile, JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(elements.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.layers[0].frames.length}`).join("\n"));
}

module.exports = { P, QING, JADE, ICE, mask, rect, ellipse, poly, limb, shade, shadeRows, px, rotate, thin, flareFrame, brokenRing };
if (require.main === module) build();

// a look at the whole dragon: head, body and tail laid along a curve the way the game places them
// (segments 14px apart, tapering), written to out/preview_dragon.png
function previewDragon() {
  const Wd = 260, H = 90, canvas = png.make(Wd, H);
  for (let i = 0; i < Wd * H; i++) { canvas.data[i * 4] = 40; canvas.data[i * 4 + 1] = 34; canvas.data[i * 4 + 2] = 48; canvas.data[i * 4 + 3] = 255; }
  const pathAt = s => [30 + s, 45 + Math.sin(s * 0.035) * 14];
  const place = (sprite, s, scale = 1) => {
    const [x, y] = pathAt(s), [x2, y2] = pathAt(s + 1), ang = Math.atan2(y2 - y, x2 - x);
    let part = sprite;
    if (scale !== 1) { const sc = img(Math.round(sprite.w * scale), Math.round(sprite.h * scale)); for (let yy = 0; yy < sc.h; yy++) for (let xx = 0; xx < sc.w; xx++) { const c = D.get(sprite, Math.floor(xx / scale), Math.floor(yy / scale)); if (c) put(sc, xx, yy, c); } part = sc; }
    const r = rotate(part, ang, part.w + 12, part.h + 12);
    D.blit(canvas, r, Math.round(x - r.w / 2), Math.round(y - r.h / 2));
  };
  const f = 1, head = 200;
  const taper = i => lerp(1, 0.7, i / 9);
  const at = [];
  let s = head;
  for (let i = 0; i <= 9; i++) { s -= 14 * taper(i); at.push(s); }
  place(dragonTail(f), at[9], 0.65);
  for (let i = 8; i >= 0; i--) place(i === 1 || i === 5 ? dragonLeg((f + i) % 4) : dragonBody((f + i) % 4), at[i], taper(i));
  place(dragonHead(f), head);
  png.encode(png.preview([canvas], 3, [22, 18, 30], 1, 0), path.join(__dirname, "out", "preview_dragon.png"));
}
if (require.main === module && !process.argv.slice(2).length) previewDragon();
if (require.main === module) previewBacks();
