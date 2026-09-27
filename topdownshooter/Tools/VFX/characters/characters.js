// the playable cast, drawn the way Vampire Survivors draws its characters: a full-body figure a
// little over 30 pixels tall facing right, big readable shapes in three or four tones each, lit
// from the upper right like everything else in the game, a dark outline, a four-frame idle (a
// breath and whatever they wear stirring) and a four-frame walk (contact, pass, contact, pass).
// 48x48 canvases with the feet on row 45, the same as the Zhuo Lan they replace, so the world
// scale and every clip made for him still fit.
//   Zhuo Lan    a Qing bannerman archer: studded gold armour, red-tasselled helmet, red cape, bow
//   Yun Xi      a Maoshan exorcist: white Taoist robe, red trim, peach-wood sword, yellow talismans
//   Ye Tianshu  a sword immortal of the Northern Dipper: azure robes, silver crown, a straight jian
// node characters.js [names...]
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { hex, put, bayer } = D;
const { img, withOutline, el } = W;

const P = Object.assign({}, W.P, {
  SK0: hex("#8a4638"), SK1: hex("#d8895f"), SK2: hex("#f6c08c"), SK3: hex("#ffe2bc"),   // skin
  HR0: hex("#0d0a18"), HR1: hex("#221a36"), HR2: hex("#3b3160"),                          // black hair, blue sheen
  WD0: hex("#4a2412"), WD1: hex("#7e4220"), WD2: hex("#b8703a"), WD3: hex("#e6a060"),    // wood
  CR0: hex("#8f7f8c"), CR1: hex("#c9bcc0"), CR2: hex("#ece4dc"), CR3: hex("#ffffff"),     // white cloth
  SL0: hex("#4a5470"), SL1: hex("#8a9ab8"), SL2: hex("#c8d6ea"), SL3: hex("#ffffff"),     // silver
  AZ0: hex("#14244a"), AZ1: hex("#1f4a8a"), AZ2: hex("#2f7ac8"), AZ3: hex("#6ec4f0"),     // azure cloth
  TL0: hex("#b88418"), TL1: hex("#f2c230"), TL2: hex("#fff07a"),                          // talisman paper
});
const OUTLINE = hex("#1a0c26");
const S = 48, FEET = 45;

// ------------------------------------------------------------------ drawing kit
// a mask per shape, then one call shades it: lit on the right and along the top (the key light is
// upper right), shade down the left and along the bottom, like a cylinder
const mask = () => D.mask(S, S);
function rect(M, x0, y0, x1, y1) { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) D.mset(M, x, y); return M; }
function ellipse(M, cx, cy, rx, ry) {
  for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++)
    for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++)
      if (((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1) D.mset(M, x, y);
  return M;
}
// a filled polygon, by scanline
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
// a limb: a line swollen to a width
function limb(M, pts, width) {
  const C = D.polyline(mask(), pts);
  const T = width > 1 ? D.dilate(C, Math.floor(width / 2), true) : C;
  for (let i = 0; i < T.m.length; i++) if (T.m[i]) M.m[i] = 1;
  return M;
}
const has = (M, x, y) => D.mget(M, x, y);
// ramp: [dark, mid, light, highlight]. dark: the share of each row's width in shade on the left
function shade(im, M, ramp, { dark = 0.34, light = 0.78, top = true, bottom = true, spec = true } = {}) {
  for (let y = 0; y < S; y++) {
    let l = -1, r = -1;
    for (let x = 0; x < S; x++) if (has(M, x, y)) { if (l < 0) l = x; r = x; }
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
const flat = (im, M, c) => { D.paint(im, M, c); return im; };
const px = (im, pts, c) => pts.forEach(([x, y]) => put(im, x, y, c));
// a small drop shadow under the feet, dithered so the floor shows through
function shadow(im, cx, w) {
  for (let y = FEET - 1; y <= FEET + 1; y++) for (let x = cx - w; x <= cx + w; x++) {
    const d = ((x - cx) / (w + 0.5)) ** 2 + ((y - FEET) / 1.6) ** 2;
    if (d <= 1 && (d < 0.45 || bayer(x, y) < 0.5)) put(im, x, y, [0x0c, 0x06, 0x14, 255]);
  }
}
// legs for the walk: [back foot x offset, front foot x offset, lift] per frame, and the body bob
const WALK = [
  { back: -4, front: 4, bob: 0, lift: 0 },     // contact, front leg forward
  { back: -1, front: 1, bob: -1, lift: 1 },    // passing, up on the planted leg
  { back: 4, front: -4, bob: 0, lift: 0 },     // contact, the other leg forward
  { back: 1, front: -1, bob: -1, lift: 2 },    // passing
];
const IDLE = [{ bob: 0, sway: 0 }, { bob: 0, sway: 1 }, { bob: 1, sway: 1 }, { bob: 1, sway: 0 }];

// a leg from the hip to a boot: thigh and shin in one ramp, the boot in another, the sole in white
function leg(im, hipX, hipY, footX, lift, cloth, boot, sole, far) {
  const kneeX = Math.round((hipX + footX) / 2 + 1), kneeY = Math.round((hipY + FEET) / 2) - lift;
  const M = limb(mask(), [[hipX, hipY], [kneeX, kneeY], [footX, FEET - 3 - lift]], 4);
  shade(im, M, far ? [cloth[0], cloth[0], cloth[1]] : cloth, { spec: false });
  // the boot: from the shin down to the sole, toe pointing right
  const B = mask();
  rect(B, footX - 2, FEET - 4 - lift, footX + 1, FEET - 1 - lift);
  rect(B, footX + 2, FEET - 2 - lift, footX + 3, FEET - 1 - lift);
  shade(im, B, far ? [boot[0], boot[0], boot[1]] : boot, { spec: false });
  if (sole) for (let x = footX - 2; x <= footX + 3; x++) put(im, x, FEET - lift, far ? sole[0] : sole[1]);
}

// ------------------------------------------------------------------ Zhuo Lan
function zhuoLan({ bob = 0, sway = 0, walk = null, f = 0 }) {
  const im = img(S, S);
  const y0 = bob;                       // body offset: +1 is down
  const U = (y) => y + y0;
  shadow(im, 24, 7);

  // cape, behind everything: from the shoulders down to the calves, flaring back as he moves
  const flare = walk ? 2 + (f % 2) : sway;
  const cape = mask();
  poly(cape, [[20, U(20)], [24, U(21)], [22, U(33)], [18 - flare, U(40)], [13 - flare, U(39)], [15 - flare, U(30)], [17, U(22)]]);
  shade(im, cape, [P.R0, P.R1, P.R2, P.R3], { dark: 0.45, light: 0.85 });
  // its hem cut in two tails
  put(im, 16 - flare, U(39), P.R0); put(im, 15 - flare, U(40), [0, 0, 0, 0]);

  // quiver on the back, arrows fletched in white and red
  const quiver = rect(mask(), 16, U(19), 18, U(29));
  shade(im, quiver, [P.WD0, P.WD1, P.WD2]);
  px(im, [[16, U(18)], [17, U(17)], [18, U(18)], [17, U(16)]], P.CR2);
  px(im, [[16, U(17)], [18, U(17)], [17, U(15)]], P.R2);

  // legs: dark blue trousers, black Qing boots on thick white soles
  const cloth = [P.A0, P.A1, P.A2], boot = [P.S0, P.S1, P.S2], sole = [P.CR1, P.CR2];
  const w = walk || { back: -1, front: 1, lift: 0 };
  leg(im, 22, U(33), 22 + w.back, walk && w.back > w.front ? w.lift : 0, cloth, boot, sole, true);
  leg(im, 25, U(33), 25 + w.front, walk && w.front > w.back ? w.lift : 0, cloth, boot, sole, false);

  // the armour coat: gold cloth studded with brass, dark blue trim, split skirt to the knee
  const coat = mask();
  poly(coat, [[19, U(21)], [28, U(21)], [29, U(31)], [30, U(37)], [18, U(37)], [18, U(31)]]);
  shade(im, coat, [P.G0, P.G1, P.G2, P.G3]);
  for (let y = U(22); y <= U(36); y += 3) for (let x = 20 + ((y - y0) % 2); x <= 28; x += 3) if (has(coat, x, y)) put(im, x, y, x > 25 ? P.G3 : P.G0);
  for (let x = 18; x <= 30; x++) if (has(coat, x, U(37))) put(im, x, U(37), P.A1);                // hem trim
  for (let y = U(31); y <= U(37); y++) put(im, 24, y, P.G0);                                     // the split
  for (let x = 19; x <= 29; x++) { put(im, x, U(30), P.A1); put(im, x, U(31), x > 25 ? P.A2 : P.A1); } // belt
  put(im, 26, U(30), P.G3); put(im, 26, U(31), P.G2);                                            // buckle
  // shoulder guard
  const pauldron = ellipse(mask(), 25.5, U(22.5), 3.5, 2.2);
  shade(im, pauldron, [P.G0, P.G1, P.G2, P.G3], { dark: 0.2 });
  for (let x = 23; x <= 28; x++) if (has(pauldron, x, U(24))) put(im, x, U(24), P.A1);

  // head: a face in profile under the helmet, a braid hanging behind
  const face = mask();
  poly(face, [[22, U(13)], [28, U(13)], [29, U(16)], [30, U(17)], [29, U(18)], [28, U(20)], [23, U(20)], [22, U(17)]]);
  shade(im, face, [P.SK0, P.SK1, P.SK2, P.SK3], { dark: 0.3 });
  px(im, [[27, U(16)]], P.S0);                                   // eye
  px(im, [[26, U(15)], [27, U(15)], [28, U(15)]], P.HR1);        // brow
  px(im, [[28, U(19)]], P.SK0);                                  // mouth
  px(im, [[23, U(16)], [23, U(17)]], P.SK0);                     // ear
  // helmet: a dome with a brim, the spike and a red horsehair tassel streaming back
  const dome = ellipse(mask(), 25, U(12), 4.5, 3.5);
  for (let i = 0; i < dome.m.length; i++) if (Math.floor(i / S) > U(13)) dome.m[i] = 0;
  shade(im, dome, [P.S1, P.S2, P.S3, P.CR2], { dark: 0.3 });
  for (let x = 20; x <= 30; x++) put(im, x, U(13), x > 26 ? P.G2 : P.G1);                         // brim
  for (let x = 20; x <= 30; x++) put(im, x, U(14), P.G0);
  put(im, 25, U(7), P.G2); put(im, 25, U(8), P.G1); put(im, 26, U(8), P.G2);                    // spike
  put(im, 25, U(6), P.G3);
  const tassel = mask();
  const blow = walk ? 1 + (f % 2) : sway;
  poly(tassel, [[24, U(8)], [26, U(8)], [25, U(11)], [19 - blow, U(12)], [18 - blow, U(10)]]);
  shade(im, tassel, [P.R1, P.R2, P.R3], { dark: 0.3 });
  // the back of his head under the helmet, and his queue: a black braid down his back, tied in red
  for (let y = U(14); y <= U(17); y++) for (let x = 21; x <= 22; x++) put(im, x, y, x === 21 ? P.HR0 : P.HR1);
  const braid = limb(mask(), [[21, U(15)], [20, U(19)], [20 - blow, U(24)]], 2);
  shade(im, braid, [P.HR0, P.HR1, P.HR2], { spec: false });
  put(im, 20 - blow, U(25), P.R2); put(im, 19 - blow, U(26), P.R1);

  // arms. the far arm draws the string back; the near arm holds the bow out front
  const reach = walk ? [0, 1, 0, 1][f] : 0;
  const farArm = limb(mask(), [[21, U(23)], [22, U(27)], [26, U(28)]], 3);
  shade(im, farArm, [P.G0, P.G0, P.G1], { spec: false });
  const nearArm = limb(mask(), [[26, U(23)], [29, U(26)], [31 + reach, U(26)]], 3);
  shade(im, nearArm, [P.G0, P.G1, P.G2, P.G3]);
  px(im, [[32 + reach, U(25)], [32 + reach, U(26)], [33 + reach, U(26)]], P.SK1);               // hand on the grip

  // the bow: a recurved stave from above his head to his knees, the string straight behind it
  const bx = 33 + reach, top = U(14), bot = U(38);
  for (let y = top; y <= bot; y++) {
    const t = (y - top) / (bot - top), bend = Math.round(Math.sin(t * Math.PI) * 3);
    const c = y === top || y === bot ? P.G2 : t < 0.5 ? P.WD2 : P.WD1;
    put(im, bx + bend, y, c);
    if (Math.abs(t - 0.5) < 0.12) put(im, bx + bend + 1, y, P.WD0);                               // the grip
    put(im, bx, y, y === top || y === bot ? P.G2 : bx + bend === bx ? c : P.CR1);                 // string
  }
  put(im, bx - 1, top, P.G3); put(im, bx - 1, bot, P.G3);                                          // recurved tips
  return withOutline(im, OUTLINE);
}

// ------------------------------------------------------------------ Yun Xi
function yunXi({ bob = 0, sway = 0, walk = null, f = 0 }) {
  const im = img(S, S);
  const y0 = bob, U = (y) => y + y0;
  shadow(im, 24, 7);

  // hair: long and black, down her back to the waist, lifting as she moves
  const lift = walk ? 1 + (f % 2) : sway;
  const hair = mask();
  poly(hair, [[20, U(10)], [27, U(9)], [28, U(14)], [22, U(18)], [21, U(27)], [17 - lift, U(29)], [16 - lift, U(24)], [18, U(15)]]);
  shade(im, hair, [P.HR0, P.HR1, P.HR2], { dark: 0.4 });

  // feet under the robe: white socks, black cloth shoes
  const w = walk || { back: -1, front: 1, lift: 0 };
  for (const [dx, far] of [[w.back, true], [w.front, false]]) {
    const fx = 24 + dx, l = walk && ((far && w.back > w.front) || (!far && w.front > w.back)) ? w.lift : 0;
    const shoe = rect(mask(), fx - 2, FEET - 2 - l, fx + 2, FEET - l);
    shade(im, shoe, far ? [P.S0, P.S0, P.S1] : [P.S0, P.S1, P.S2], { spec: false });
    put(im, fx - 1, FEET - 3 - l, far ? P.CR1 : P.CR2); put(im, fx, FEET - 3 - l, far ? P.CR1 : P.CR2);
  }

  // the robe: white, long, the hem swinging with her step; red trim at the hem and the collar
  const swing = walk ? [2, 0, -2, 0][f] : 0;
  const robe = mask();
  poly(robe, [[20, U(19)], [28, U(19)], [30, U(30)], [31 + swing, FEET - 3], [17 + swing, FEET - 3], [18, U(30)]]);
  shade(im, robe, [P.CR0, P.CR1, P.CR2, P.CR3]);
  for (let x = 16; x <= 32; x++) for (let y = FEET - 4; y <= FEET - 3; y++) if (has(robe, x, y)) put(im, x, y, y === FEET - 4 ? P.R2 : P.R1);
  // a taiji on the back, and the robe's fold down the front
  px(im, [[20, U(26)], [21, U(25)], [21, U(27)], [22, U(26)]], P.S1); px(im, [[21, U(26)]], P.CR3);
  for (let y = U(22); y <= FEET - 5; y++) if (has(robe, 27 + Math.round((y - U(22)) / 9), y)) put(im, 27 + Math.round((y - U(22)) / 9), y, P.CR1);
  // red sash with a tail hanging at the hip
  for (let x = 19; x <= 29; x++) { put(im, x, U(28), P.R2); put(im, x, U(29), x > 25 ? P.R2 : P.R1); }
  const tail = limb(mask(), [[21, U(29)], [19 - lift, U(34)]], 2);
  shade(im, tail, [P.R0, P.R1, P.R2]);
  // collar: crossed, red edged
  px(im, [[24, U(19)], [25, U(20)], [26, U(21)], [27, U(22)], [23, U(20)], [22, U(21)]], P.R2);
  px(im, [[25, U(19)], [26, U(20)], [27, U(21)]], P.R1);

  // head in profile, a bun on top held by a peach-wood pin
  const face = mask();
  poly(face, [[22, U(11)], [28, U(11)], [29, U(14)], [30, U(15)], [29, U(16)], [28, U(18)], [23, U(18)], [22, U(15)]]);
  shade(im, face, [P.SK0, P.SK1, P.SK2, P.SK3], { dark: 0.3 });
  px(im, [[27, U(14)]], P.S0); px(im, [[27, U(13)], [28, U(13)]], P.HR1); px(im, [[28, U(17)]], P.R2);
  // fringe and the hair over the ear
  const fringe = mask();
  poly(fringe, [[21, U(9)], [29, U(9)], [29, U(11)], [26, U(12)], [23, U(16)], [21, U(15)]]);
  shade(im, fringe, [P.HR0, P.HR1, P.HR2], { dark: 0.4 });
  const bun = ellipse(mask(), 24, U(7), 2.6, 2.2);
  shade(im, bun, [P.HR0, P.HR1, P.HR2], { dark: 0.3 });
  px(im, [[21, U(7)], [22, U(7)], [26, U(6)], [27, U(6)]], P.WD2);                       // the pin through it
  px(im, [[28, U(5)], [27, U(5)]], P.K2); put(im, 28, U(4), P.K3);                         // a peach blossom on its end

  // wide sleeves. the far hand holds the peach-wood sword pointing back and down, the near hand a
  // fan of yellow talismans held up in front
  const hold = walk ? [0, 1, 0, 1][f] : sway;
  const farSleeve = mask();
  poly(farSleeve, [[21, U(20)], [24, U(21)], [23, U(28)], [18, U(29)], [18, U(26)]]);
  shade(im, farSleeve, [P.CR0, P.CR0, P.CR1], { spec: false });
  for (let x = 18; x <= 23; x++) if (has(farSleeve, x, U(29))) put(im, x, U(29), P.R1);
  // the sword: a reddish blade with a darker spine, a round guard and a tasselled pommel
  const blade = limb(mask(), [[17, U(30)], [10, U(40)]], 1);
  const bladeW = limb(mask(), [[18, U(30)], [11, U(40)]], 1);
  flat(im, blade, P.WD1); flat(im, bladeW, P.WD3); put(im, 10, U(41), P.WD2);
  for (let k = 0; k < 3; k++) put(im, 15 - k * 2, U(33 + k * 3), P.R2);   // red script down the blade
  px(im, [[17, U(28)], [18, U(28)], [19, U(29)], [16, U(29)], [17, U(29)]], P.G2);   // guard
  px(im, [[18, U(27)], [19, U(27)]], P.G3);
  px(im, [[21, U(26)], [22, U(25)], [22, U(26)]], P.R2);                  // tassel
  const nearSleeve = mask();
  poly(nearSleeve, [[25, U(20)], [28, U(21)], [32, U(24)], [33, U(28)], [28, U(29)], [26, U(25)]]);
  shade(im, nearSleeve, [P.CR0, P.CR1, P.CR2, P.CR3]);
  for (let x = 27; x <= 33; x++) if (has(nearSleeve, x, U(28))) put(im, x, U(28), P.R2);
  px(im, [[33, U(25)], [34, U(25)], [34, U(26)]], P.SK2);                 // fingers
  // three talismans fanned between the fingers, red glyph strokes on the yellow
  for (let k = 0; k < 3; k++) {
    const tx = 33 + k * 2 - hold, ty = U(18) + k;
    for (let y = ty; y <= ty + 6; y++) for (let x = tx; x <= tx + 1; x++) put(im, x, y, x === tx ? P.TL1 : P.TL2);
    put(im, tx, ty + 6, P.TL0);
    put(im, tx, ty + 2, P.R2); put(im, tx + 1, ty + 3, P.R2); put(im, tx, ty + 4, P.R1);
  }
  return withOutline(im, OUTLINE);
}

// ------------------------------------------------------------------ Ye Tianshu
function yeTianshu({ bob = 0, sway = 0, walk = null, f = 0 }) {
  const im = img(S, S);
  const y0 = bob, U = (y) => y + y0;
  shadow(im, 24, 7);

  // a long ribbon sash floating behind in an S, and his long hair
  const drift = walk ? 2 + (f % 2) : sway;
  const ribbon = limb(mask(), [[21, U(21)], [17, U(24)], [14 - drift, U(29)], [12 - drift, U(34)], [9 - drift, U(36 - drift)]], 2);
  shade(im, ribbon, [P.AZ2, P.AZ3, P.SL2], { spec: false, dark: 0.5 });
  const hair = mask();
  poly(hair, [[21, U(10)], [27, U(9)], [27, U(14)], [22, U(17)], [21, U(26)], [18 - drift, U(27)], [18, U(15)]]);
  shade(im, hair, [P.HR0, P.HR1, P.HR2], { dark: 0.45 });

  // legs: white trousers, dark boots
  const cloth = [P.CR0, P.CR1, P.CR2], boot = [P.AZ0, P.S1, P.S2];
  const w = walk || { back: -1, front: 1, lift: 0 };
  leg(im, 22, U(34), 22 + w.back, walk && w.back > w.front ? w.lift : 0, cloth, boot, null, true);
  leg(im, 25, U(34), 25 + w.front, walk && w.front > w.back ? w.lift : 0, cloth, boot, null, false);

  // the robe: an azure outer robe over white, open at the front, knee length, split at the side
  const swing = walk ? [1, 0, -1, 0][f] : 0;
  const robe = mask();
  poly(robe, [[19, U(19)], [28, U(19)], [29, U(29)], [30 + swing, U(39)], [18 + swing, U(39)], [18, U(29)]]);
  shade(im, robe, [P.AZ0, P.AZ1, P.AZ2, P.AZ3]);
  for (let y = U(20); y <= U(39); y++) for (let x = 26; x <= 27; x++) if (has(robe, x + (y > U(30) ? swing : 0), y)) put(im, x + (y > U(30) ? swing : 0), y, x === 26 ? P.CR1 : P.CR2);
  for (let x = 17; x <= 31; x++) if (has(robe, x, U(39))) put(im, x, U(39), P.SL1);               // silver hem
  // the belt: black, studded with the seven gold stars of the Dipper
  for (let x = 19; x <= 29; x++) { put(im, x, U(28), P.S1); put(im, x, U(29), P.S0); }
  [[19, 28], [21, 29], [22, 28], [24, 28], [26, 29], [27, 28], [29, 29]].forEach(([x, y]) => put(im, x, U(y), P.G2));
  put(im, 24, U(28), P.G3);

  // head: face in profile, hair tied up in a topknot under a small silver crown
  const face = mask();
  poly(face, [[22, U(11)], [28, U(11)], [29, U(14)], [30, U(15)], [29, U(16)], [28, U(18)], [23, U(18)], [22, U(15)]]);
  shade(im, face, [P.SK0, P.SK1, P.SK2, P.SK3], { dark: 0.3 });
  px(im, [[27, U(14)]], P.S0); px(im, [[26, U(13)], [27, U(12)], [28, U(12)]], P.HR0); px(im, [[28, U(17)]], P.SK0);
  const fringe = mask();
  poly(fringe, [[21, U(9)], [28, U(8)], [29, U(10)], [25, U(11)], [23, U(15)], [21, U(14)]]);
  shade(im, fringe, [P.HR0, P.HR1, P.HR2], { dark: 0.4 });
  const knot = ellipse(mask(), 24, U(6), 2, 1.8);
  shade(im, knot, [P.HR0, P.HR1, P.HR2]);
  const crown = rect(mask(), 22, U(6), 26, U(8));
  shade(im, crown, [P.SL0, P.SL1, P.SL2, P.SL3], { dark: 0.2 });
  put(im, 24, U(5), P.SL2); put(im, 24, U(4), P.A3);                   // a blue gem on its point
  // strands falling in front of the ear
  px(im, [[22, U(16)], [22, U(17)], [23, U(18)]], P.HR1);

  // arms: the far one held behind his back, the near one holding the jian point down and forward,
  // its silver blade lit, a gold guard and a blue tassel
  const farArm = limb(mask(), [[21, U(21)], [20, U(26)]], 3);
  shade(im, farArm, [P.AZ0, P.AZ0, P.AZ1], { spec: false });
  const lean = walk ? [0, 1, 0, 1][f] : 0;
  const nearArm = limb(mask(), [[26, U(21)], [28, U(25)], [30 + lean, U(27)]], 3);
  shade(im, nearArm, [P.AZ0, P.AZ1, P.AZ2, P.AZ3]);
  px(im, [[31 + lean, U(26)], [31 + lean, U(27)], [32 + lean, U(27)]], P.SK1);
  const hx = 32 + lean, hy = U(27);
  const blade = limb(mask(), [[hx + 1, hy + 1], [hx + 8, hy + 12]], 1);
  const edge = limb(mask(), [[hx + 2, hy + 1], [hx + 9, hy + 12]], 1);
  flat(im, blade, P.SL1); flat(im, edge, P.SL3); put(im, hx + 9, hy + 13, P.SL2);
  px(im, [[hx, hy], [hx + 1, hy - 1], [hx + 2, hy], [hx - 1, hy + 1]], P.G2);                        // guard
  px(im, [[hx - 1, hy - 1], [hx - 2, hy - 2]], P.WD1);                                               // grip
  px(im, [[hx - 3, hy - 2], [hx - 3, hy - 1], [hx - 4, hy], [hx - 4, hy + 1]], P.A2);                // tassel
  return withOutline(im, OUTLINE);
}

// ------------------------------------------------------------------ the cast
function frames(draw) {
  const out = [];
  IDLE.forEach((p, f) => out.push(draw({ bob: p.bob, sway: p.sway, f })));
  WALK.forEach((w, f) => out.push(draw({ bob: w.bob, walk: w, f })));
  return out;
}
const CAST = [
  // Zhuo Lan keeps his file, layer and group, so the clips and the player prefab keep working
  { name: "qing_warrior_vs", group: ".", layer: "warrior", draw: zhuoLan },
  { name: "yun_xi", group: "Characters", layer: "yun xi", draw: yunXi },
  { name: "ye_tianshu", group: "Characters", layer: "ye tianshu", draw: yeTianshu },
];

function build() {
  const only = process.argv.slice(2);
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [], sheet = [];
  for (const c of CAST.filter(c => !only.length || only.includes(c.name))) {
    const fr = frames(c.draw);
    const e = el(c.name, c.group, S, S, [{ name: c.layer, frames: fr }], [220, 220, 220, 220, 150, 150, 150, 150], [["idle", 0, 3], ["walk", 4, 7]]);
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    fr.forEach((im, i) => { png.encode(im, path.join(dir, `L0_${i}.png`)); sheet.push(im); });
    const { layers, ...meta } = e;
    manifest.push(Object.assign(meta, { frames: fr.length, layers: layers.map(l => l.name) }));
  }
  png.encode(png.preview(sheet, 8, [58, 50, 62], 8, 2), path.join(out, "sheet.png"));
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.frames}`).join("\n"));
}
build();
