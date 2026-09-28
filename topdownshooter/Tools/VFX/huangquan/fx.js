// Huangquan Road's effects, played by FxBatch or held by its hazards; all at the world's pixel
// size, centred on where they happen:
//   lily_bloom        a spider lily's ring leaving it: a flash in its cup, a ring of red petals
//   paper_trail       scraps of paper and a streak of white left behind a dashing servant
//   paper_ghost_r/_l  a servant's afterimage, facing right / left, paling away
//   ember             a wad of burning spirit money tumbling through the air (the game spins it)
//   ember_shadow      its shadow on the ground
//   ember_mark        where it'll land: a red ring drawing in, frame by frame as it falls
//   ember_splash      its landing: a burst of fire, sparks and ash
//   fire_pool         the paper fire it leaves, a scorched patch with flames licking up, looping
//   lantern_aura      a soul lantern's reach: a turning ring of green soul fire, looping
//   haste_mote        a green spark off a mob the lantern is quickening
//   wisp_puff         a wisp leaving the lantern
//   charge_lane       Ox-Head's lane: red chevrons streaming along a dark band (the game tiles it)
//   charge_launch     the road bursting under him as he sets off: a flattened shock ring, dust
//   charge_crack_0-7  the road he's charged down, split and glowing, cooling; eight headings
//   charge_dust       dust off his hooves
//   bull_ghost_r/_l   his afterimage mid-charge, facing right / left, a red silhouette burning off
//   trample           something of the horde going under his hooves
//   charge_crash      into a wall: a white flash, a star of cracks, rubble and dust thrown out
//   horse_halo        the ring of light swelling round Horse-Face's lantern before a pulse
//   horse_pulse       the pulse leaving it: an azure shock ring, chain-link sparks
//   statue_burst      a guardian statue bursting: stone shards and a red shock ring
//   statue_restore    a statue made whole: motes drawing in, a flash
const K = require("./kit");
const { P, D, W, K2, put, bayer, img, TAU, lerp, blob, pick, glowAround } = K;
const { ringField, breakup } = W;

const RED = K.RED, FIRE = W.FIRE, AZ = W.AZURE, JADE = K2.JADE, GOLD = W.GOLD;
const HOT = [[0.1, P.R0], [0.25, P.R1], [0.45, P.R2], [0.65, P.R3], [0.82, P.G3], [0.95, P.W]];
const STONE = [[0.1, P.ST1], [0.3, P.ST2], [0.55, P.ST3], [0.8, P.ST4]];
const DUSTR = [[0.08, P.ST1], [0.18, P.ST2], [0.32, P.ST3], [0.5, P.AS3]];
const easeOut = W.easeOut;

// a field shaded straight onto a new square image
function shaded(S, ramp, fill, opts) { const F = D.field(S, S); fill(F); return D.shade(img(S, S), F, ramp, opts); }
// a flat ring of pixels
function ring(im, cx, cy, R, col, rx = 1, ry = 1, gate = null) {
  const n = Math.ceil(R * 7);
  for (let i = 0; i < n; i++) { const a = i / n * TAU; if (gate && !gate(a)) continue; put(im, cx + Math.cos(a) * R * rx, cy + Math.sin(a) * R * ry, col); }
}
// flecks flung out from the middle: count, seed, how far they get by t, colours
function flecks(im, c, t, n, seed, reach, cols, size = 1, gravity = 0) {
  const r = D.rng(seed);
  for (let k = 0; k < n; k++) {
    const a = r() * TAU, sp = 0.5 + r() * 0.8, d = easeOut(t) * reach * sp;
    const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + gravity * t * t;
    if (bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.55) * 2.2) continue;
    const col = cols[Math.min(cols.length - 1, Math.floor(t * cols.length + r() * 0.8))];
    put(im, x, y, col);
    if (size > 1) { put(im, x + 1, y, col); put(im, x, y + 1, col); }
  }
}

// ---------------------------------------------------------------- the burst
// the layered blast every impact here is built from, the way the meteor's is (weapons.js
// makeImpact): a white flare with rays on the first frame, then a fireball of noisy lobes swelling
// and burning out through `ramp`, a shock ring breaking up as it spreads, debris thrown out hot and
// cooling, embers lifting and twinkling, and smoke rolling off. `squash` flattens it onto the ground
function burst(o) {
  const S = o.S, c = S / 2, N = o.N || 9, r = D.rng(o.seed || 1), sq = o.squash || 1;
  const R = o.R, ringR = o.ringR || R * 1.5;
  const debris = Array.from({ length: o.debris || 0 }, () => ({ a: r() * TAU, s: 0.45 + r() * 0.75, big: r() < 0.45, delay: r() < 0.3 ? 1 : 0 }));
  const embers = Array.from({ length: o.embers || 0 }, () => ({ a: r() * TAU, d: R * 0.4 + r() * R * 1.3, rise: 1 + r() * 1.4, ph: Math.floor(r() * 3) }));
  const puffs = Array.from({ length: o.smoke ? 9 : 0 }, () => ({ a: r() * TAU, s: 0.3 + r() * 0.8, size: 0.5 + r() * 0.6, rise: 0.8 + r() * 1.2 }));
  const hotC = o.hot || [P.G3, P.O2, P.O1, P.R2], coolC = o.cool || [P.S3, P.S2];
  const emberC = o.emberCols || [P.W, P.G3, P.O2, P.R2, P.R1];
  const frames = [];
  for (let f = 0; f < N; f++) {
    const k = f / (N - 1), im = img(S, S);
    // smoke under everything
    if (o.smoke && f >= 2) {
      const Sm = D.field(S, S), i = f - 2, pr = R * 0.9 + i * R * 0.12, rad = R * 0.22 + i * R * 0.04, val = 0.13 - i * 0.008;
      for (const p of puffs) {
        const px = c + Math.cos(p.a) * pr * p.s, py = c + Math.sin(p.a) * pr * p.s * sq - i * 1.4 * p.rise;
        D.each(Sm, (x, y) => { const d = Math.hypot(x - px, (y - py) / sq), e = rad * p.size * (0.8 + 0.4 * W.vnoise(x / 2, y / 2, 9)); return d < e ? val * (1 - 0.45 * d / e) : 0; });
      }
      D.over(im, D.shade(img(S, S), Sm, o.smoke, { bands: false }));
    }
    // the fireball: noisy lobes swelling then burning out, holes eaten in it as it goes
    const F = D.field(S, S);
    if (f === 0) {
      D.each(F, (x, y) => { const d = Math.hypot(x - c, (y - c) / sq); return d < R * 0.5 ? 1 : d < R * 0.7 ? 0.92 - (d - R * 0.5) / (R * 0.2) * 0.3 : 0; });
      if (o.flare !== false) {
        D.flare(F, c, c, R * 1.6, Math.max(3, R * 0.18), R * 0.8, 2, 1, 0.5);
        for (let t = 0; t < 10; t++) D.ray(F, c, c, r() * TAU, R * (0.9 + r() * 0.8), 1.5, 0.66, 0.3);
      }
    } else {
      const grow = Math.min(1, f / (N * 0.35)), inten = Math.max(0, 1 - (f - 1) / (N - 1) * 1.05);
      const rb = R * (0.65 + 0.35 * grow) * (f > N * 0.6 ? 1 - (f - N * 0.6) / N * 0.8 : 1);
      D.each(F, (x, y) => {
        const dx = x - c, dy = (y - c) / sq, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
        const n = W.fbm(x / 6, y / 6 - f * 0.8, 5 + f), lobes = 0.75 + 0.5 * W.vnoise((a + Math.PI) * 2.2, f * 0.7, 13);
        const edge = rb * lobes * (0.85 + 0.3 * n);
        if (d > edge) return 0;
        let v = inten * (1.05 - 0.6 * d / edge) + (n - 0.5) * 0.45 * inten;
        if (k > 0.45 && n < 0.3 + (k - 0.45) * 0.8) v *= 0.35;
        return v;
      });
    }
    D.over(im, D.shade(img(S, S), F, o.ramp));
    // the shock ring
    if (f >= 1 && k <= 0.75) {
      const Rg = D.field(S, S), t = f / (N * 0.75), Rr = R * 0.9 + easeOut(t) * (ringR - R * 0.9);
      const th = Math.max(2.5, (1 - t) * R * 0.32), pk = 1 - t * 0.75;
      D.each(Rg, (x, y) => {
        const d = Math.hypot(x - c, (y - c) / sq);
        if (d > Rr + 0.5 || d <= Rr - th) return 0;
        const a = Math.atan2((y - c) / sq, x - c);
        if (t > 0.35 && !breakup(Rr, f, 1.05 - t * 0.6)(a)) return 0;
        return pk * (0.55 + 0.45 * Math.pow((d - (Rr - th)) / th, 1.5));
      });
      D.over(im, D.shade(img(S, S), Rg, o.ringRamp || o.ramp));
    }
    // debris, hot on the way out, cooling
    for (const d of debris) {
      const t = (f - 1 - d.delay) / (N - 1);
      if (t < 0 || (t > 0.8 && !d.big)) continue;
      const dist = R * 0.8 + R * 1.3 * d.s * easeOut(t), x = Math.round(c + Math.cos(d.a) * dist), y = Math.round(c + Math.sin(d.a) * dist * sq + (o.gravity || 0) * t * t);
      const hot = t < 0.45, s = d.big ? 2 : 1;
      if (hot) for (let q = 2; q <= 4; q++) put(im, x - Math.round(Math.cos(d.a) * q), y - Math.round(Math.sin(d.a) * q * sq), hotC[Math.min(hotC.length - 1, q - 1)]);
      for (let v = 0; v < s; v++) for (let u = 0; u < s; u++) put(im, x + u, y + v, hot ? hotC[1] : coolC[u + v === 0 ? 0 : 1]);
      if (hot) put(im, x, y, hotC[0]);
    }
    // embers lifting, twinkling
    if (f >= 1) embers.forEach((e, i) => {
      const q = f - 1, x = c + Math.cos(e.a) * e.d * (1 + 0.25 * q / N), y = c + Math.sin(e.a) * e.d * sq * (1 + 0.25 * q / N) - q * e.rise * 1.4;
      if ((f + e.ph) % 3 === 0 && f > 2) return;
      const col = emberC[Math.min(emberC.length - 1, Math.floor(k * emberC.length))];
      if (f === 1 && i % 2 === 0) W.twinkle(im, x, y, 1, [emberC[0], emberC[1]]); else put(im, x, y, col);
    });
    frames.push(im);
  }
  return frames;
}

// ---------------------------------------------------------------- the spider lily
function lilyBloom() {
  const S = 64, c = 32;
  const frames = burst({ S, N: 8, R: 10, ringR: 22, seed: 3, ramp: HOT, ringRamp: HOT, embers: 10, emberCols: [P.W, P.G3, P.R3, P.R2], flare: true });
  // petals flung out, turning, falling away
  return frames.map((im, f) => {
    const t = f / 7, r = D.rng(8);
    for (let k = 0; k < 12; k++) {
      const a = k / 12 * TAU + r() * 0.3, d = 5 + easeOut(t) * (16 + r() * 6);
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + t * t * 5;
      if (f === 0 || (t > 0.7 && bayer(Math.round(x), Math.round(y)) < (t - 0.7) * 3)) continue;
      const tw = Math.cos(a + t * 6);
      put(im, x, y, P.R2); put(im, x + tw, y + Math.sin(a + t * 6), P.R3); put(im, x - Math.cos(a), y - Math.sin(a), P.R1);
    }
    return im;
  });
}

// ---------------------------------------------------------------- the paper servant
function paperTrail() {
  const S = 28, c = 14, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(S, S), r = D.rng(21);
    // a white streak, shortening
    for (let x = -6; x <= 6; x++) if (bayer(c + x, c) < (1 - t) * (1 - Math.abs(x) / 7)) put(im, c + x, c, P.CR2);
    for (let k = 0; k < 9; k++) {
      const x = c + (r() - 0.5) * 10 + (r() - 0.5) * t * 10, y = c + (r() - 0.5) * 8 + t * t * 6 - t * 3;
      if (bayer(Math.round(x), Math.round(y)) < t * 0.95) continue;
      const col = k % 4 === 0 ? P.R2 : k % 3 === 0 ? P.G2 : P.CR2;
      put(im, x, y, col); if (k % 2) put(im, x + 1, y, t < 0.5 ? P.CR1 : P.AS3);
    }
    out.push(im);
  }
  return out;
}

// an afterimage: a pose's silhouette in `cols` (bright to dim), paling and dithering away
function ghost(pose, cols, n = 5, mirror = false) {
  const src = K.fit([pose])[0];
  const out = [];
  for (let f = 0; f < n; f++) {
    const t = f / (n - 1), im = img(src.w, src.h);
    for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
      const i = (y * src.w + x) * 4;
      if (!src.data[i + 3]) continue;
      if (bayer(x + f, y) < 0.2 + t * 0.8) continue;
      // its own light and shade kept, in the tint's colours, dimming as it goes
      const lum = (src.data[i] * 0.3 + src.data[i + 1] * 0.59 + src.data[i + 2] * 0.11) / 255;
      const idx = Math.min(cols.length - 1, Math.floor((1 - lum) * 1.6 + t * cols.length * 0.8));
      put(im, mirror ? src.w - 1 - x : x, y, cols[Math.max(0, idx)]);
    }
    out.push(im);
  }
  return out;
}

// ---------------------------------------------------------------- the hell money burner
function ember() {
  const S = 16, c = 8, out = [];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    // flames wreathing it
    const F = D.field(S, S);
    D.each(F, (x, y) => {
      const d = Math.hypot(x - c, y - c), a = Math.atan2(y - c, x - c);
      const tongue = 0.5 + 0.5 * Math.sin(a * 5 + f * 1.6);
      return d < 3.2 + tongue * 3 ? 0.85 - d / (6.5 + tongue * 2) : 0;
    });
    D.shade(im, F, FIRE);
    // the notes: a yellow wad with a red seal
    for (let y = -2; y <= 2; y++) for (let x = -2; x <= 2; x++) put(im, c + x, c + y, Math.abs(x) === 2 || Math.abs(y) === 2 ? P.TL0 : P.TL1);
    put(im, c, c, P.R2); put(im, c + 1, c, P.R1);
    put(im, c - 2 + (f % 2), c - 2, P.W);
    out.push(im);
  }
  return out;
}

function emberShadow() {
  const S = 16, im = img(S, S);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) if (((x + 0.5 - 8) / 5.5) ** 2 + ((y + 0.5 - 8) / 2.2) ** 2 <= 1) put(im, x, y, P.K);
  return [im];
}

function emberMark() {
  const S = 48, c = 24, R = 20, out = [];
  for (let f = 0; f < 8; f++) {
    const t = f / 7, im = img(S, S);
    // the outer ring, dashed and turning
    ring(im, c, c, R, t > 0.8 ? P.R3 : P.R2, 1, 1, a => ((a + t * 1.5) * 16 / TAU) % 2 < 1.2);
    ring(im, c, c, R - 1, P.R0, 1, 1, a => ((a + t * 1.5) * 16 / TAU) % 2 < 1.2);
    // the inner ring drawing in, and the marks pointing at the middle
    const r2 = lerp(R - 3, 3, t);
    ring(im, c, c, r2, t > 0.6 ? P.G3 : P.R3);
    for (let k = 0; k < 4; k++) {
      const a = k / 4 * TAU + Math.PI / 4;
      for (let d = r2 + 2; d < r2 + 5; d++) put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, P.R2);
    }
    // a dim fill so the danger reads on any floor
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) if (Math.hypot(x + 0.5 - c, y + 0.5 - c) < R - 1 && !D.get(im, x, y) && bayer(x, y) < 0.12 + t * 0.12) put(im, x, y, P.R0);
    put(im, c, c, t > 0.6 ? P.W : P.G2);
    out.push(im);
  }
  return out;
}

function emberSplash() {
  const S = 64;
  return burst({ S, N: 9, R: 14, ringR: 26, seed: 5, squash: 0.62, ramp: FIRE, debris: 8, hot: [P.G3, P.TL1, P.O1, P.R2], cool: [P.AS3, P.AS2],
    embers: 16, smoke: K.ASH, gravity: 6 });
}

function firePool() {
  const S = 48, c = 24, R = 20, out = [];
  for (let f = 0; f < 6; f++) {
    const im = img(S, S);
    // scorched ground, darker to the middle, a glowing rim of cinders
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const d = Math.hypot((x + 0.5 - c) / R, (y + 0.5 - c) / (R * 0.8));
      if (d > 1) continue;
      const n = W.fbm(x / 4, y / 4, 3);
      if (d > 0.82 - n * 0.2) { if (bayer(x + f, y) < 0.6) put(im, x, y, (x * 7 + y * 3 + f) % 5 === 0 ? P.O2 : P.R1); continue; }
      // a bed of embers glowing through the ash
      if (n > 0.62 && bayer(x + f, y) < 0.5) put(im, x, y, (x + y + f) % 4 === 0 ? P.O2 : P.R2);
      else if (bayer(x, y) < 0.6) put(im, x, y, n > 0.5 ? P.R1 : n > 0.4 ? P.R0 : P.V0);
    }
    // flames licking up from the burning notes scattered in it
    const r = D.rng(12);
    for (let k = 0; k < 15; k++) {
      const x0 = c + (r() - 0.5) * 30, y0 = c + (r() - 0.5) * 20, h = 5 + r() * 8, ph = r() * 6;
      if (Math.hypot((x0 - c) / R, (y0 - c) / (R * 0.8)) > 0.78) continue;
      const hh = h * (0.6 + 0.4 * Math.sin((f + ph) / 6 * TAU));
      for (let y = 0; y <= hh; y++) {
        const k2 = y / hh, wob = Math.round(Math.sin(y * 0.9 + f * 1.7 + k) * k2);
        const col = pick(FIRE, 0.85 - k2 * 0.7);
        if (!col) continue;
        put(im, x0 + wob, y0 - y, col);
        if (k2 < 0.4) put(im, x0 + wob + 1, y0 - y, pick(FIRE, 0.7 - k2 * 0.7) || col);
      }
      put(im, x0 - 1, y0 + 1, P.TL1); put(im, x0, y0 + 1, P.TL0); put(im, x0 + 1, y0 + 1, P.TL1);
    }
    out.push(im);
  }
  return out;
}

// ---------------------------------------------------------------- the soul lantern
function lanternAura() {
  const S = 96, c = 48, R = 44, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 6;
    const im = shaded(S, [[0.12, P.J1], [0.3, P.J2], [0.55, P.J3], [0.8, P.J4]], F => {
      ringField(F, c, c, R, 2.5, 0.7, a => Math.sin(a * 6 + t * TAU) > -0.55);
      ringField(F, c, c, R - 5, 1.2, 0.35, a => Math.sin(a * 10 - t * TAU * 2) > 0.2);
    });
    // a faint haze inside
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) if (Math.hypot(x + 0.5 - c, y + 0.5 - c) < R - 7 && bayer(x + f, y + f) < 0.05) put(im, x, y, P.J1);
    // motes circling the rim
    for (let k = 0; k < 8; k++) {
      const a = k / 8 * TAU + t * TAU / 4, x = c + Math.cos(a) * (R - 2), y = c + Math.sin(a) * (R - 2);
      W.twinkle(im, x, y, k % 2, [P.J5, P.J3]);
    }
    out.push(im);
  }
  return out;
}

function hasteMote() {
  const S = 16, out = [];
  for (let f = 0; f < 5; f++) {
    const t = f / 4, im = img(S, S), y0 = 8 - t * 6;
    for (const dy of [0, 3]) for (let k = -2; k <= 2; k++) {
      if (bayer(8 + k, Math.round(y0 + dy)) < t * 0.9) continue;
      put(im, 8 + k, y0 + dy + Math.abs(k), dy === 0 ? P.J5 : P.J3);
    }
    out.push(im);
  }
  return out;
}

function wispPuff() {
  return burst({ S: 36, N: 6, R: 6, ringR: 11, seed: 8, ramp: JADE, embers: 5, emberCols: [P.J5, P.J4, P.J3], flare: false });
}

// ---------------------------------------------------------------- Ox-Head's charge
function chargeLane() {
  const S = 24, out = [];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    for (let y = 4; y <= 19; y++) for (let x = 0; x < S; x++) {
      // the band: dark red, dithered; its edges bright
      if (y === 4 || y === 19) { put(im, x, y, P.R2); continue; }
      if (y === 5 || y === 18) { if (bayer(x, y) < 0.6) put(im, x, y, P.R1); continue; }
      // chevrons streaming along it, 12 px apart, moving 3 px a frame
      const u = ((x - f * 3) % 12 + 12) % 12, v = Math.abs(y - 11.5);
      const on = Math.abs(u - (8 - v * 0.8)) < 1.6;
      if (on) put(im, x, y, u > 7 - v * 0.8 ? P.R3 : P.R2);
      else if (bayer(x, y) < 0.22) put(im, x, y, P.R0);
    }
    out.push(im);
  }
  return out;
}

function chargeLaunch() {
  // the road bursting under his hooves: a fireball of dust and red light pressed flat, a ring
  // skimming out along the ground, stones kicked up
  return burst({ S: 112, N: 8, R: 20, ringR: 46, seed: 11, squash: 0.4, ramp: HOT, ringRamp: HOT, debris: 12,
    hot: [P.R3, P.R2, P.R1, P.R0], cool: [P.ST3, P.ST2], embers: 8, emberCols: [P.W, P.R3, P.R2], smoke: DUSTR, gravity: 4 });
}

function chargeCrack() {
  const S = 56, c = 28, out = [];
  // a split in the road along the charge (drawn horizontal; the game lays it in his direction),
  // white-hot along its spine, glowing, cooling to a dark scar, then gone
  const r = D.rng(17), pts = [];
  let y = c;
  for (let x = 6; x <= 50; x += 4) { pts.push([x, y]); y += (r() - 0.5) * 4; }
  const branches = [[14, 1], [26, -1], [38, 1]].map(([x, s]) => [[x, c], [x + 4, c + s * 5], [x + 7, c + s * 9]]);
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(S, S);
    const spine = D.polyline(D.mask(S, S), pts), side = D.mask(S, S);
    branches.forEach(bp => D.polyline(side, bp));
    const wide = D.dilate(spine, 1, false);
    const core = t < 0.25 ? P.W : t < 0.45 ? P.G3 : t < 0.65 ? P.O2 : P.R1;
    const edge = t < 0.45 ? P.R3 : t < 0.65 ? P.R2 : P.ST1;
    for (let yy = 0; yy < S; yy++) for (let x = 0; x < S; x++) {
      const i = yy * S + x;
      if (t > 0.7 && bayer(x, yy) < (t - 0.7) * 3.3) continue;
      if (spine.m[i]) put(im, x, yy, core);
      else if (wide.m[i] || side.m[i]) put(im, x, yy, side.m[i] && !wide.m[i] ? (t < 0.5 ? P.R2 : P.ST1) : edge);
    }
    out.push(t < 0.6 ? glowAround(im, 2, P.R1, P.R0, f) : im);
  }
  return out;
}

function chargeDust() {
  const S = 44, c = 22, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 5;
    out.push(shaded(S, DUSTR, F => {
      const r = D.rng(5);
      for (let k = 0; k < 5; k++) {
        const x = c + (r() - 0.5) * 10 - t * 6, y = c + (r() - 0.5) * 6 - t * 5, rad = 2.5 + t * 5 + r() * 2;
        blob(F, x, y, rad, rad * 0.8, 0.5 * (1 - t) + 0.05, 0.4);
      }
    }, { fadeBand: 0.8 }));
  }
  return out;
}

function trample() {
  // a soul crushed under a hoof: a violet-red splat pressed onto the road, its spirit thrown up
  const SPIRIT = [[0.1, P.V1], [0.25, P.V2], [0.42, P.V3], [0.6, P.R2], [0.78, P.R3], [0.92, P.W]];
  return burst({ S: 48, N: 7, R: 10, ringR: 17, seed: 13, squash: 0.5, ramp: SPIRIT, debris: 6, hot: [P.W, P.R3, P.V4, P.V3],
    cool: [P.V3, P.V2], embers: 8, emberCols: [P.V6, P.V5, P.V4, P.V3], flare: false, gravity: 3 });
}

function chargeCrash() {
  // into a wall: white flash, a molten star of cracks, a blast of fire and rubble, dust rolling off
  const S = 144, c = 72;
  const frames = burst({ S, N: 10, R: 26, ringR: 54, seed: 17, ramp: HOT, ringRamp: FIRE, debris: 16, hot: [P.W, P.G3, P.O2, P.R2],
    cool: [P.ST4, P.ST3], embers: 20, smoke: DUSTR, gravity: 10 });
  return frames.map((im, f) => {
    if (f === 0) return im;
    const t = f / 9, F = D.field(S, S);
    for (let k = 0; k < 9; k++) {
      const a = k / 9 * TAU + (k % 2) * 0.25, len = 16 + (k % 3) * 8;
      D.ray(F, c, c, a, len * Math.min(1, t * 3), 3, 0.95 - t * 0.8, 0.3);
    }
    return D.over(D.shade(img(S, S), F, HOT), im);
  });
}

// ---------------------------------------------------------------- Horse-Face
function horseHalo() {
  const S = 64, c = 32, out = [];
  for (let f = 0; f < 4; f++) {
    const t = f / 4;
    const im = shaded(S, AZ, F => {
      blob(F, c, c, 6, 6, 0.9, 0.2);
      ringField(F, c, c, 20, 2.4, 0.85, null);
      ringField(F, c, c, 26, 1.2, 0.45, a => Math.sin(a * 8 + t * TAU) > 0);
      // eight short rays off the ring
      for (let k = 0; k < 8; k++) D.ray(F, c + Math.cos(k / 8 * TAU + t) * 20, c + Math.sin(k / 8 * TAU + t) * 20, k / 8 * TAU + t, 6, 2, 0.7, 0.2);
    });
    out.push(im);
  }
  return out;
}

function horsePulse() {
  // the lantern's pulse: a flash of soul fire, an azure shock ring, links of chain flung out on it
  const S = 112, c = 56;
  const frames = burst({ S, N: 9, R: 17, ringR: 46, seed: 19, ramp: AZ, ringRamp: AZ, embers: 16, emberCols: [P.W, P.A5, P.A4, P.A3, P.A2], flare: true });
  return frames.map((im, f) => {
    const t = f / 8, r = D.rng(6);
    if (f === 0) return im;
    for (let k = 0; k < 12; k++) {
      const a = k / 12 * TAU + r() * 0.2, d = 10 + easeOut(t) * 38;
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
      if (bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.5) * 2) continue;
      put(im, x, y, P.SL2); put(im, x + 1, y, P.SL1); put(im, x, y + 1, P.SL1); put(im, x + 1, y + 1, P.SL2);
    }
    return im;
  });
}

// ---------------------------------------------------------------- the statues
function statueBurst() {
  // the stone shell bursting off the guardian: a red blast, shards of statue, dust
  const S = 176, c = 88;
  const frames = burst({ S, N: 11, R: 32, ringR: 72, seed: 23, ramp: HOT, ringRamp: HOT, debris: 22, hot: [P.W, P.R3, P.R2, P.R1],
    cool: [P.ST4, P.ST3], embers: 26, emberCols: [P.W, P.R3, P.R2, P.R1], smoke: DUSTR, gravity: 18 });
  return frames.map((im, f) => {
    const t = f / 10, r = D.rng(44);
    for (let k = 0; k < 18; k++) {
      const a = -Math.PI / 2 + (r() - 0.5) * 3.4, sp = 0.4 + r() * 0.9, d = easeOut(t) * 60 * sp;
      const x = c + Math.cos(a) * d, y = c - 6 + Math.sin(a) * d + t * t * 34;
      if (f === 0 || bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.65) * 2.6) continue;
      // a stone shard: a little wedge, lit on top
      put(im, x, y, P.ST4); put(im, x + 1, y, P.ST3); put(im, x, y + 1, P.ST3); put(im, x + 1, y + 1, P.ST2); put(im, x + 2, y + 1, P.ST1);
    }
    return im;
  });
}

function statueRestore() {
  const S = 128, c = 64, out = [];
  for (let f = 0; f < 9; f++) {
    const t = f / 8, im = img(S, S);
    // motes of violet and gold drawn in to it
    const r = D.rng(90);
    for (let k = 0; k < 40; k++) {
      const a = r() * TAU, d0 = 30 + r() * 30, d = d0 * (1 - easeOut(Math.min(1, t * 1.2)));
      const x = c + Math.cos(a + t * 1.5) * d, y = c + Math.sin(a + t * 1.5) * d * 0.9;
      if (t > 0.85) continue;
      put(im, x, y, k % 3 === 0 ? P.G3 : k % 3 === 1 ? P.V5 : P.V4);
      put(im, x - Math.cos(a + t * 1.5) * 2, y - Math.sin(a + t * 1.5) * 2, P.V2);
    }
    if (f >= 6) D.over(im, shaded(S, [[0.15, P.V2], [0.35, P.V4], [0.6, P.G3], [0.85, P.W]], F => {
      blob(F, c, c, 14 + (f - 6) * 6, 22 + (f - 6) * 6, 1 - (f - 6) * 0.3, 0.2);
    }));
    out.push(im);
  }
  return out;
}

function makeFx(poses = {}) {
  const fx = {
    lily_bloom: lilyBloom(), paper_trail: paperTrail(), ember: ember(), ember_shadow: emberShadow(),
    ember_mark: emberMark(), ember_splash: emberSplash(), fire_pool: firePool(), lantern_aura: lanternAura(),
    haste_mote: hasteMote(), wisp_puff: wispPuff(), charge_lane: chargeLane(), charge_launch: chargeLaunch(),
    charge_crack: chargeCrack(), charge_dust: chargeDust(), trample: trample(), charge_crash: chargeCrash(),
    horse_halo: horseHalo(), horse_pulse: horsePulse(), statue_burst: statueBurst(), statue_restore: statueRestore(),
  };
  // the road's cracks turned to eight headings over a half turn (the game picks the nearest; a
  // crack runs both ways)
  const crack = fx.charge_crack;
  delete fx.charge_crack;
  for (let k = 0; k < 8; k++) fx["charge_crack_" + k] = crack.map(im => K.turn(im, k / 8 * Math.PI));
  const RED_GHOST = [P.R3, P.R2, P.R1, P.R0], PAPER_GHOST = [P.W, P.CR2, P.CR1, P.AS3];
  if (poses.bull) { fx.bull_ghost_r = ghost(poses.bull, RED_GHOST); fx.bull_ghost_l = ghost(poses.bull, RED_GHOST, 5, true); }
  if (poses.servant) { fx.paper_ghost_r = ghost(poses.servant, PAPER_GHOST, 4); fx.paper_ghost_l = ghost(poses.servant, PAPER_GHOST, 4, true); }
  return fx;
}

module.exports = { makeFx, ghost };
