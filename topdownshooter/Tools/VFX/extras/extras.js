// the small things around the fight, in the weapons' style: an enemy's death (with and without
// blood), the three kinds of wen dropped (bronze, jade, a red envelope, like Vampire Survivors'
// blue, green and red gems) and the longevity peach that heals
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { P, TAU, easeOut, img, withOutline, ringField, twinkle, el } = W;
const put = D.put;
const OUTLINE = [0x1a, 0x0c, 0x26, 255];

// ---------------------------------------------------------------- death, 48x48, 9 frames
// a white pop, droplets thrown out and falling, and the soul leaving as a pale wisp. the burst sits
// on the canvas centre; the wisp rises into the top half. bloodless: the droplets are motes of qi
function makeDeath(blood) {
  const S = 48, c = 24, N = 9, r = D.rng(blood ? 61 : 62);
  const drops = Array.from({ length: 11 }, (_, i) => ({ a: i * TAU / 11 + (r() - 0.5) * 0.5, v: 0.55 + r() * 0.6, big: i % 2 === 0 }));
  const cols = blood
    ? { hot: P.W, core: P.R3, body: P.R2, tail: P.R1, stain: P.R0, ring: [[0.2, P.R2], [0.45, P.R3], [0.8, P.W]] }
    : { hot: P.W, core: P.V6, body: P.V4, tail: P.V3, stain: P.V2, ring: [[0.2, P.V4], [0.45, P.V6], [0.8, P.W]] };
  const blob = (im, x, y, big, fall) => {
    x = Math.round(x); y = Math.round(y);
    put(im, x, y, cols.body);
    if (big) { put(im, x + 1, y, cols.body); put(im, x, y + 1, cols.tail); put(im, x + 1, y + 1, cols.tail); put(im, x, y, cols.core); }
    if (fall) put(im, x, y - 1, cols.tail);
  };
  const popL = [], dropL = [], wispL = [];
  for (let f = 0; f < N; f++) {
    // the pop: a flash, then a ring snapping out
    const F = D.field(S, S);
    if (f === 0) { D.flare(F, c, c, 10, 3, 5, 1, 1, 0.45); D.each(F, (x, y) => Math.hypot(x - c, y - c) < 4 ? 1 : 0); }
    if (f === 1) ringField(F, c, c, 7, 2.2, 0.75);
    if (f === 2) ringField(F, c, c, 10, 1.5, 0.5, a => Math.sin(a * 5 + 1) < 0.35);
    popL.push(D.shade(img(S, S), F, cols.ring));

    // the splash: a burst in the middle breaking into droplets that fly, fall and land
    const Dr = img(S, S);
    if (f === 1 || f === 2) for (let y = c - 4; y <= c + 4; y++) for (let x = c - 4; x <= c + 4; x++) {
      const d = Math.hypot(x - c, y - c);
      if (d < (f === 1 ? 3.6 : 2.4)) put(Dr, x, y, d < 1.4 ? cols.core : cols.body);
    }
    if (f >= 2) {
      const t = (f - 2) / (N - 3);
      for (const d of drops) {
        const dist = 5 + 15 * d.v * easeOut(Math.min(1, t * 1.6)), fall = t * t * 10;
        const x = c + Math.cos(d.a) * dist, y = c + Math.sin(d.a) * dist * 0.75 + fall;
        if (f <= 5) blob(Dr, x, y, d.big && f <= 4, f >= 4);
        else if (d.big || f === 6) {
          // landed: a splat drying out
          const col = f === 6 ? cols.tail : cols.stain;
          if (f === 8 && D.bayer(Math.round(x), Math.round(y)) > 0.5) continue;
          put(Dr, x, y, col); if (d.big) put(Dr, x + 1, y, col);
        }
      }
    }
    dropL.push(Dr);

    // the soul: a pale wisp rising and curling away
    const Wp = img(S, S);
    if (f >= 2) {
      const k = f - 2, len = Math.max(2, 8 - Math.max(0, k - 3) * 1.6), y0 = c - 3 - k * 3;
      for (let i = 0; i < len; i++) {
        const y = Math.round(y0 + i), x = Math.round(c + Math.sin(k * 0.8 + i * 0.5) * 1.6 * (1 - i / len));
        const fading = k >= 4;
        if (fading && D.bayer(x, y) > 0.6) continue;
        put(Wp, x, y, i === 0 && !fading ? P.W : i < 3 ? P.V6 : P.V5);
        if (i < len - 2) put(Wp, x + 1, y, fading ? P.V4 : P.V6);
        if (i > 0 && i < len - 3 && !fading) put(Wp, x - 1, y, P.V4);
      }
    }
    wispL.push(Wp);
  }
  return el(blood ? "enemy_death" : "enemy_death_bloodless", "Enemies", S, S,
    [{ name: "drops", frames: dropL }, { name: "pop", frames: popL }, { name: "soul", frames: wispL }], Array(N).fill(35), [["die", 0, N - 1]]);
}

// ---------------------------------------------------------------- the wen, and the peach
function coin(size, rim, face, light, glint) {
  const im = img(size + 2, size + 2), c = (size + 1) / 2, R = size / 2;
  for (let y = 0; y < size + 2; y++) for (let x = 0; x < size + 2; x++) {
    const dx = x + 0.5 - c - 0.5, dy = y + 0.5 - c - 0.5, d = Math.hypot(dx, dy);
    if (d > R) continue;
    const hole = Math.abs(dx) <= 1.2 && Math.abs(dy) <= 1.2;
    if (hole) continue;
    const holeRim = Math.abs(dx) <= 2.2 && Math.abs(dy) <= 2.2;
    let col = d > R - 1.3 ? rim : holeRim ? rim : face;
    if (!holeRim && d <= R - 1.3 && dx - dy < -R * 0.5) col = light;      // lit upper left
    put(im, x, y, col);
  }
  const g = Math.round(c - R * 0.45);
  put(im, g, g + 1, glint); put(im, g + 1, g, glint);
  return im;
}
function makeWen() {
  const bronze = coin(11, P.G0, P.G1, P.G2, P.G3);
  const jade = coin(12, P.G1, [0x3f, 0xa3, 0x8a, 255], [0x8e, 0xe0, 0xc0, 255], P.W);
  // a red envelope with a gold seal: the big one, and where wen goes when too much is lying about
  const env = img(15, 18);
  for (let y = 2; y <= 16; y++) for (let x = 1; x <= 13; x++) put(env, x, y, x === 13 || y === 16 ? P.R1 : x === 1 || y === 2 ? P.R3 : P.R2);
  for (let x = 1; x <= 13; x++) { const y = 2 + Math.round(5 - Math.abs(x - 7) * 0.75); for (let yy = 2; yy <= y; yy++) put(env, x, yy, yy === y ? P.R1 : P.R2); }
  for (let y = 8; y <= 12; y++) for (let x = 5; x <= 9; x++) {
    const d = Math.hypot(x - 7, y - 10);
    if (d < 2.6) put(env, x, y, d < 1.2 ? P.G3 : P.G2);
  }
  put(env, 7, 10, P.R1); put(env, 3, 4, P.W);
  const out = [["wen_bronze", withOutline(bronze, OUTLINE)], ["wen_jade", withOutline(jade, OUTLINE)], ["wen_envelope", withOutline(env, OUTLINE)]];
  return out.map(([name, im]) => el(name, "Pickups", im.w, im.h, [{ name, frames: [im] }], [100], []));
}
// a wen turning over, for the level up's rain: the face narrowing to the edge and back, one pixel
// of the coin's thickness showing on the side turning away, the far face without its light.
// bronze and jade share one 15x15 file, frames 0-7 and 8-15, so the rain is one texture
function coinSpin(R, rim, face, light, glint, lit, shade) {
  const S = 15, c = 7, N = 8, frames = [];
  for (let f = 0; f < N; f++) {
    const a = f / N * TAU, s = Math.abs(Math.cos(a)), back = Math.cos(a) < -0.01, side = Math.sin(a) >= 0 ? 1 : -1;
    const im = img(S, S);
    const onFace = (dx, dy) => s > 0.2 && Math.hypot(dx / s, dy) <= R;
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const dx = x - c, dy = y - c;
      if (s <= 0.2) {
        // edge on: a bar of rim, lit down the middle
        if ((dx === 0 || dx === side) && Math.abs(dy) <= R - 0.5)
          put(im, x, y, Math.abs(dy) > R - 1.5 ? shade : dx === 0 ? (dy < -R * 0.4 ? glint : lit) : shade);
        continue;
      }
      if (onFace(dx, dy)) {
        const u = dx / s, d = Math.hypot(u, dy);
        if (Math.abs(u) <= 1.2 && Math.abs(dy) <= 1.2) continue;                    // the square hole
        const holeRim = Math.abs(u) <= 2.2 && Math.abs(dy) <= 2.2;
        const edge = d > R - 1.3 || !onFace(dx - 1, dy) || !onFace(dx + 1, dy);
        let col = edge || holeRim ? rim : face;
        if (!back && !edge && !holeRim && u - dy < -R * 0.5) col = light;
        put(im, x, y, col);
      } else if (s < 0.95 && onFace(dx - side, dy)) {
        put(im, x, y, side < 0 ? lit : shade);                                        // its thickness
      }
    }
    if (!back && s > 0.5) {
      const gx = c + Math.round(-2 * s);
      put(im, gx, c - 1, glint);
      if (s > 0.9) put(im, gx + 1, c - 2, glint);
    }
    frames.push(withOutline(im, OUTLINE));
  }
  return frames;
}
function makeWenSpin() {
  const bronze = coinSpin(5.5, P.G0, P.G1, P.G2, P.G3, P.G1, P.G0);
  const jade = coinSpin(5.8, P.G1, [0x3f, 0xa3, 0x8a, 255], [0x8e, 0xe0, 0xc0, 255], P.W, P.G2, P.G0);
  return el("wen_spin", "Pickups", 15, 15, [{ name: "wen", frames: [...bronze, ...jade] }], Array(16).fill(80), [["bronze", 0, 7], ["jade", 8, 15]]);
}
function makePeach() {
  const im = img(16, 16);
  for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) {
    const dx = x + 0.5 - 8, dy = y + 0.5 - 9.5, d = Math.hypot(dx, dy * 1.05);
    // a peach: round, with a point on top and a crease down one side
    const point = dy < -3 && Math.abs(dx) < (dy + 7.5) * 0.9;
    if (d > 6.2 && !point) continue;
    let col = P.K2;
    if (dx + dy > 3) col = P.K1;
    else if (dx - dy < -5 && dx < -1) col = P.K3;
    if (Math.abs(dx - 1.5 + dy * 0.25) < 0.6 && dy > -4 && dy < 4) col = P.K1;   // the crease
    if (dx > 1 && dy > 0 && D.bayer(x, y) < 0.5) col = P.R2;                     // a blush
    put(im, x, y, col);
  }
  put(im, 5, 7, P.W); put(im, 5, 8, P.K3);
  [[9, 2, [0x3f, 0xa3, 0x8a, 255]], [10, 2, [0x3f, 0xa3, 0x8a, 255]], [11, 3, [0x1f, 0x6b, 0x5c, 255]], [10, 3, [0x8e, 0xe0, 0xc0, 255]],
    [6, 2, [0x3f, 0xa3, 0x8a, 255]], [5, 3, [0x1f, 0x6b, 0x5c, 255]], [8, 2, P.G0]].forEach(([x, y, c]) => put(im, x, y, c));
  const lit = withOutline(im, OUTLINE);
  return el("longevity_peach", "Pickups", 16, 16, [{ name: "peach", frames: [lit] }], [100], []);
}

// node extras.js [names...]: every element, or only the ones named
function build() {
  const only = process.argv.slice(2);
  const elements = [makeDeath(true), makeDeath(false), ...makeWen(), makePeach(), makeWenSpin()]
    .filter(e => !only.length || only.includes(e.name));
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [], sheet = [];
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length;
    for (let f = 0; f < n; f++) {
      const im = img(e.w, e.h);
      e.layers.forEach(l => D.over(im, l.frames[f]));
      let lit = 0; for (let i = 3; i < im.data.length; i += 4) if (im.data[i]) lit++;
      if (!lit) throw new Error(`${e.name} frame ${f} is empty; Unity's importer would drop it`);
      sheet.push(im);
      e.layers.forEach((l, li) => png.encode(l.frames[f], path.join(dir, `L${li}_${f}.png`)));
    }
    const { layers, ...meta } = e;
    manifest.push(Object.assign(meta, { frames: n, layers: layers.map(l => l.name) }));
  }
  png.encode(png.preview(sheet, 6, [58, 50, 62], 9, 2), path.join(out, "sheet.png"));
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.frames}`).join("\n"));
}

build();
