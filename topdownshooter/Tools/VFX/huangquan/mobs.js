// Huangquan Road's horde, made the way the Courtyard's enemies are (../enemies): each drawn flat in
// a few local colours with a black outline, then lit by ../restyle.js (a key light from the upper
// right in dithered bands, a violet rim, a specular, the outline turned deep plum), then the light
// that belongs to it laid over: eyes, fire, glows, motes. drawn facing right; the game flips them.
//   wandering_soul      28x30  a drowned soul drifting down the road: long black hair streaming,
//                       hollow eyes, a mouth open in a wail, thin arms reaching ahead of it, its
//                       body a tail of pale sea-green mist. a ghost-fire glow round it.
//                       loop 6, death 6 (it gasps, flashes and comes apart into motes)
//   spider_lily         40x44  Bǐ'àn Huā Jīng: a spider lily grown into a demon, a crimson head of
//                       recurved petals round a cup with one slit gold eye and a ring of fangs,
//                       stamens like long lashes, two tendril arms ending in shut buds, rooted in a
//                       mound of grave soil. loop 6, rise 6 (it bursts up and opens), act 5 (the
//                       petals clench, the eye blazes: the ring's warning), death 7
//   paper_servant       32x44  a funeral effigy on a bamboo frame, pasted from coloured paper: a
//                       painted face with a fixed smile and round rouge, twin buns with red ribbons,
//                       a red robe trimmed with gold clouds, white paper cuffs, a gold ingot held in
//                       its paper hands, a mourning streamer from its head. loop 4 (stiff hops), aim
//                       3 (it crouches, its painted eyes open red), dash 3, death 6 (it tears and burns)
//   hell_money_burner   38x44  hunched in ash hemp with a white mourning band, a pale jaw and two
//                       coals in its hood, spirit money strung at its belt, a brazier of burning
//                       notes held out in bony hands, its hem fraying into smoke. loop 6, act 5 (it
//                       straightens and holds a blazing wad of notes high), death 6
//   soul_lantern        32x44  a guiding lantern with no one holding it: a gold-lacquered roof cap, a
//                       paper body painted 引 lit from within by green soul fire (the flame's shadow
//                       showing through the paper), talisman strips and a long red tassel hanging,
//                       three wisps circling it. loop 6, death 8 (it flares, the paper burns green, out)
const K = require("./kit");
const { P, D, W, K2, put, bayer, img, TAU, lerp, M, fill, blob, stroke, dissolve, motes, pick, glowAround, C, inked } = K;
const { restyle } = require("../restyle");

const ramp = (...h) => h;
const GH = ramp("#155c44", "#26986a", "#54d898", "#aef5cc", "#e6fff0");
const HAIR = ramp("#1e1a26", "#2b2232", "#463a4b", "#6b5b6c");
const PAPER = ramp("#8a7e84", "#c9bcc0", "#ece4dc", "#fff8f0");
const ROBE = ramp("#3e0812", "#7e1426", "#d02838", "#ff6a5a");
const GOLD = ramp("#6e3e14", "#c88830", "#f8d068", "#fff0b0");
const WOOD = ramp("#4a2412", "#7e4220", "#b8703a", "#e6a060");
const STEM = ramp("#0b2a22", "#1a4a38", "#2c7a52", "#58b07a");
const SOIL = ramp("#1b1722", "#312a38", "#4c4354", "#6e6476");
const ASH = ramp("#2a2428", "#463e42", "#6a6064", "#948a8a", "#bfb4ae");
const IRON = ramp("#17111d", "#2b2232", "#463a4b", "#6b5b6c", "#978c9c");
const TALIS = ramp("#b88418", "#f2c230", "#fff07a");

const light = (frames, ramps, opts = {}) => restyle(frames, Object.assign({ ramps, ao: 2, minArea: 10 }, opts));
const dot = (im, x, y, c) => put(im, x, y, typeof c === "string" ? C(c) : c);
const area = (im, Mk, h) => fill(im, Mk, C(h));

// ================================================================ the wandering soul
const SOUL_W = 28, SOUL_H = 30;
const SOUL_X = 16;
function soulFlat(f, N) {
  const im = img(SOUL_W, SOUL_H), ph = f / N * TAU, bob = Math.round(Math.sin(ph) * 1.2);
  const hx = SOUL_X, hy = 9 + bob;
  // the body: a tail of mist from the shoulders, thinning and curling away behind
  const body = M(SOUL_W, SOUL_H);
  for (let i = 0; i <= 40; i++) {
    const t = i / 40, cx = hx - 1 - t * 14, cy = hy + 7 + t * 10 + Math.sin(t * 5.5 - ph) * 2.2 * t, r = 4.6 * (1 - t) + 0.6;
    K2.ellipse(body, cx, cy, r, r * 0.9);
  }
  area(im, body, GH[3]);
  // the head
  area(im, K2.ellipse(M(SOUL_W, SOUL_H), hx, hy, 5.2, 5.6), GH[3]);
  // hair: a cap over the crown and the back of the head, streaming out behind
  const sway = Math.sin(ph - 0.8) * 1.5;
  const hair = K2.poly(M(SOUL_W, SOUL_H), [[hx - 5, hy + 1], [hx - 4, hy - 4], [hx, hy - 6], [hx + 4, hy - 5], [hx + 6, hy - 2],
    [hx + 3, hy - 3], [hx, hy - 2], [hx - 1, hy + 2], [hx - 3, hy + 7], [hx - 7, hy + 11 + sway], [hx - 11, hy + 12 + sway],
    [hx - 8, hy + 8 + sway], [hx - 6, hy + 4]]);
  area(im, hair, HAIR[1]);
  const out = inked(im);
  // arms reaching out ahead, hands hanging limp: their own outline, so they read over the body
  const reach = Math.round(Math.sin(ph + 1) * 0.8);
  const arms = img(SOUL_W, SOUL_H);
  area(arms, stroke(M(SOUL_W, SOUL_H), [[hx - 1, hy + 8], [hx + 4, hy + 9 + reach], [hx + 8, hy + 10 + reach], [hx + 9, hy + 12 + reach]], 1), GH[3]);
  return D.over(out, inked(arms));
}

function soulLight(frames, N) {
  const lit = light(frames, [GH, HAIR], { ao: 0, rim: "#c98cf6", rimAmount: 0.25 });
  return lit.map((out, f) => {
    const hx = SOUL_X, hy = 9 + Math.round(Math.sin(f / N * TAU) * 1.2);
    // hollow eyes, a wailing mouth, a tear of light running down
    for (const ex of [hx + 1, hx + 4]) { put(out, ex, hy - 1, P.S0); put(out, ex, hy, P.S0); put(out, ex, hy + 1, P.V1); }
    const wide = f % 3 !== 0;
    put(out, hx + 2, hy + 2, P.S0); put(out, hx + 3, hy + 2, P.S0); put(out, hx + 2, hy + 3, P.S0); put(out, hx + 3, hy + 3, P.S0);
    if (wide) { put(out, hx + 2, hy + 4, P.S0); put(out, hx + 3, hy + 4, P.V1); }
    put(out, hx + 1, hy + 2 + (f % 3), P.J5);
    const g = glowAround(out, 2, P.J2, P.J1, f + 1);
    // a mote lifting off the tail's tip
    put(g, 5 + (f % 3), 26 - f * 2, f % 2 ? P.J5 : P.W);
    return g;
  });
}

function makeSoul() {
  const N = 6, loop = soulLight(Array.from({ length: N }, (_, f) => soulFlat(f, N)), N);
  const death = [];
  for (let f = 0; f < 6; f++) {
    const base = loop[0], k = f / 5;
    let im;
    if (f === 0) im = K.silhouette(base, P.J5);
    else if (f === 1) im = K.silhouette(base, P.W);
    else im = dissolve(base, 0.15 + k * 0.85, f * 2.5, f < 4 ? P.J4 : P.J3, f);
    motes(im, f, 6, 10, 17, 6, 24, 0, 22, [P.J5, P.J3]);
    death.push(im);
  }
  return { wandering_soul: loop, wandering_soul_death: death };
}

// ================================================================ the spider lily demon
const LS = { w: 40, h: 44, cx: 21, cy: 15 };
const PETALS = [-190, -168, -148, -128, -108, -90, -72, -52, -32, -12, 10];

// the flat source: the mound, leaves, stem and arms, the flower `open` (1 wide, 0 clenched)
function lilyFlat(f, { open = 1, sway = 0, grow = 1, droop = 0 } = {}) {
  const { w, h } = LS, im = img(w, h);
  const cx = LS.cx + sway, top = lerp(40, LS.cy, grow), cy = top + droop * 10;
  // grave soil and roots
  area(im, K2.ellipse(M(w, h), 20, 41, 9.5, 2.6), SOIL[2]);
  for (const [x0, x1] of [[12, 8], [28, 33], [16, 13], [25, 27]]) area(im, stroke(M(w, h), [[x0, 41], [x1, 43]], 1), WOOD[1]);
  // the strap leaves at its foot, arching
  for (const [x1, y1, xm] of [[9, 36, 13], [31, 35, 27], [7, 40, 12], [34, 39, 29]]) {
    if (grow < 0.3) break;
    area(im, stroke(M(w, h), [[20, 40], [xm, 36], [x1, y1]], 1), STEM[2]);
  }
  if (grow < 0.08) return inked(im);
  // the stem, thick, curving up to the flower (bowing over as it dies)
  const stem = [];
  for (let i = 0; i <= 14; i++) {
    const t = i / 14;
    stem.push([lerp(20, cx, t * t) + droop * t * t * 7, lerp(40, cy, t)]);
  }
  area(im, stroke(M(w, h), stem, 3), STEM[2]);
  // two tendril arms off the stem, curling up, each ending in a shut red bud
  if (grow > 0.6 && droop < 0.5) {
    const s = Math.sin(f / 6 * TAU) * 1.2;
    const armL = [[20, 32], [15, 30], [11, 26 + s], [10, 22 + s]], armR = [[21, 30], [26, 28], [30, 24 - s], [30, 20 - s]];
    area(im, stroke(M(w, h), armL, 1), STEM[2]);
    area(im, stroke(M(w, h), armR, 1), STEM[2]);
    for (const [bx, by] of [[10, 20 + s], [30, 18 - s]]) {
      area(im, K2.ellipse(M(w, h), bx, by, 1.6, 2.3), ROBE[2]);
      dot(im, bx, by - 3, ROBE[3]);
    }
  }
  if (grow < 0.75) {
    area(im, K2.ellipse(M(w, h), cx, cy, 2, 2.5), ROBE[2]);          // a bud on the way up
    return inked(im);
  }
  // the petals: long, recurved, wavy, a darker underside where they curl back
  const L = lerp(5, 11.5, open), curl = lerp(1.2, 4.5, open);
  const petals = M(w, h), under = M(w, h);
  PETALS.forEach((a0, k) => {
    const a = a0 * Math.PI / 180 + (1 - open) * (a0 < -90 ? 0.55 : -0.55) * Math.min(1, Math.abs(a0 + 90) / 90);
    const len = L * (k % 2 ? 0.86 : 1) * (1 - droop * 0.3);
    const pts = [];
    for (let i = 0; i <= 10; i++) {
      const t = i / 10, wav = Math.sin(t * 7 + k) * 0.5 * t;
      pts.push([cx + Math.cos(a) * len * t - Math.sin(a) * wav, cy + Math.sin(a) * len * t + Math.cos(a) * wav + curl * t * t + droop * 4 * t]);
    }
    stroke(petals, pts.slice(0, 5), 3);
    stroke(petals, pts.slice(4, 8), 2);
    stroke(petals, pts.slice(7, 10), 1);
    for (const [x, y] of pts.slice(7)) D.mset(under, x, y + 1);
  });
  area(im, under, ROBE[1]);
  area(im, petals, ROBE[2]);
  // the cup at its heart
  area(im, K2.ellipse(M(w, h), cx, cy, 3.4, 2.8), ROBE[0]);
  // stamens: long lashes arching out past the petals
  if (open > 0.25 && droop < 0.4) PETALS.forEach((a0, k) => {
    if (k % 2) return;
    const a = a0 * Math.PI / 180, len = lerp(4, 15, open);
    for (let i = 3; i <= Math.round(len); i++) {
      const t = i / len;
      dot(im, cx + Math.cos(a) * i * 1.04, cy + Math.sin(a) * i - 3.2 * Math.sin(t * Math.PI) * open, ROBE[3]);
    }
  });
  return inked(im);
}

// its eye, its fangs, the gold tips of the stamens, and its heat
function lilyLight(out, f, { open = 1, sway = 0, heat = 0, grow = 1, droop = 0 } = {}) {
  if (grow < 0.75 || droop > 0.4) return out;
  const cx = LS.cx + sway, cy = LS.cy + droop * 10;
  // fangs round the cup's lower rim
  for (const dx of [-2, 0, 2]) put(out, cx + dx, cy + 2, P.CR2);
  put(out, cx - 1, cy + 1, P.CR1); put(out, cx + 1, cy + 1, P.CR1);
  // the slit gold eye
  const iris = heat > 0.6 ? P.W : heat > 0.25 ? P.G3 : P.G2;
  for (let dx = -2; dx <= 2; dx++) put(out, cx + dx, cy - 1, Math.abs(dx) === 2 ? P.G1 : iris);
  for (let dx = -1; dx <= 1; dx++) { put(out, cx + dx, cy - 2, P.G1); put(out, cx + dx, cy, P.G1); }
  const blink = f % 6 === 4 && heat === 0;
  put(out, cx, cy - 1, blink ? P.G1 : heat > 0.6 ? P.R3 : P.S0);
  if (!blink) put(out, cx, cy - 2, heat > 0.6 ? P.R3 : P.S0);
  // the stamens' gold tips
  if (open > 0.25) PETALS.forEach((a0, k) => {
    if (k % 2) return;
    const a = a0 * Math.PI / 180, len = lerp(4, 15, open);
    const x = cx + Math.cos(a) * len * 1.04, y = cy + Math.sin(a) * len - 3.2 * Math.sin(Math.PI) * open;
    put(out, x, y - 1, P.G3); put(out, x + (Math.cos(a) > 0 ? 1 : -1), y - 1, P.G2);
  });
  // a red glow round the flower's head only: round the thin stems it would just be noise
  const head = img(LS.w, LS.h);
  for (let y = 0; y < LS.h; y++) for (let x = 0; x < LS.w; x++) if (y < cy + 5 && Math.abs(x - cx) < 15) { const c = D.get(out, x, y); if (c) put(head, x, y, c); }
  const halo = glowAround(head, 2, P.R1, P.R0, f + 3);
  let g = D.over(halo, out);
  if (heat > 0) {
    const F = D.field(LS.w, LS.h);
    blob(F, cx, cy - 1, 3 + heat * 6, 3 + heat * 6, heat * 1.05, 0.05);
    D.over(g, D.shade(img(LS.w, LS.h), F, [[0.18, P.R2], [0.4, P.R3], [0.65, P.G3], [0.85, P.W]], { fadeBand: 0.9 }));
    // pollen drawn into it
    const r = D.rng(31 + f);
    for (let k = 0; k < 8; k++) {
      const a = r() * TAU, d = (1 - heat) * 14 + 4 + r() * 3;
      put(g, cx + Math.cos(a) * d, cy + Math.sin(a) * d, r() < 0.5 ? P.G3 : P.R3);
    }
  }
  return g;
}

const LILY_RAMPS = [ROBE, STEM, SOIL, WOOD];
function lilies(list) {
  const lit = light(list.map(o => lilyFlat(o.f, o)), LILY_RAMPS, { ao: 2 });
  return lit.map((out, i) => lilyLight(out, list[i].f, list[i]));
}

function makeLily() {
  const sways = [0, 0, 1, 1, 0, -1];
  const loop = lilies(sways.map((s, f) => ({ f, sway: s })));
  loop.forEach((im, f) => motes(im, f, 6, 4, 41, 8, 34, 0, 16, [P.R3, P.R2]));
  const rise = lilies(Array.from({ length: 6 }, (_, f) => ({ f, grow: Math.min(1, f / 5 * 1.3), open: Math.max(0, (f / 5 - 0.7) / 0.3) })));
  rise.forEach((im, f) => { if (f < 4) { const r = D.rng(50 + f); for (let k = 0; k < 9; k++) put(im, 10 + r() * 20, 40 - r() * (3 + f * 3), r() < 0.5 ? P.ST3 : P.ST2); } });
  const act = lilies(Array.from({ length: 5 }, (_, f) => ({ f, open: 1 - f / 4 * 0.75, heat: f / 4 })));
  const death = [];
  const falling = lilies(Array.from({ length: 7 }, (_, f) => ({ f, droop: Math.min(1, f / 6 * 1.4), open: 1 - f / 6 })));
  falling.forEach((stem, f) => {
    const t = f / 6;
    if (f === 0) { death.push(K.silhouette(stem, P.R3)); return; }
    const r = D.rng(77);
    PETALS.forEach((a0) => {
      const a = a0 * Math.PI / 180, d = 6 + t * (9 + r() * 7), fall = t * t * 16;
      const x = LS.cx + Math.cos(a) * d, y = LS.cy + Math.sin(a) * d + fall;
      if (bayer(Math.round(x), Math.round(y)) < t * 0.85) return;
      put(stem, x, y, P.R2); put(stem, x + 1, y, t < 0.5 ? P.R3 : P.R1); put(stem, x, y + 1, P.R1);
    });
    death.push(t > 0.5 ? dissolve(stem, (t - 0.5) * 2, 0, null, f) : stem);
  });
  return { spider_lily: loop, spider_lily_rise: rise, spider_lily_act: act, spider_lily_death: death };
}

// ================================================================ the paper servant
const PS = { w: 32, h: 44 };
function servantFlat(f, { hop = 0, crouch = 0, arms = 0 } = {}) {
  const { w, h } = PS, im = img(w, h);
  const y0 = -hop + crouch;
  const flutter = [0, 1, 0, -1][f % 4];
  // the mourning streamer from the back of its head, fluttering behind
  const st = [];
  for (let i = 0; i <= 8; i++) { const t = i / 8; st.push([11 - t * 9, 7 + y0 + t * 9 + Math.sin(t * 5 + f * 1.6) * 1.2 * t]); }
  area(im, stroke(M(w, h), st, 1), PAPER[2]);
  // bamboo legs under the hem and little black paper shoes
  for (const lx of [12, 19]) {
    area(im, stroke(M(w, h), [[lx, 34 + y0], [lx, 40 + Math.max(0, y0)]], 1), WOOD[2]);
    area(im, K2.rect(M(w, h), lx - 1, 41 + Math.min(0, y0), lx + 2, 42 + Math.min(0, y0)), HAIR[1]);
  }
  // the robe: wide at the hem, gold-trimmed
  const robe = K2.poly(M(w, h), [[11, 17 + y0], [21, 17 + y0], [25, 35 + y0], [7, 35 + y0]]);
  area(im, robe, ROBE[2]);
  const hem = K2.poly(M(w, h), [[8, 31 + y0], [24, 31 + y0], [25, 35 + y0], [7, 35 + y0]]);
  area(im, hem, GOLD[2]);
  // a white paper under-collar crossing at the neck
  area(im, K2.poly(M(w, h), [[13, 17 + y0], [19, 17 + y0], [16, 21 + y0]]), PAPER[2]);
  // the black sash with its gold knot
  area(im, K2.rect(M(w, h), 10, 24 + y0, 22, 25 + y0), HAIR[1]);
  // the head: a paper face, hair painted over its crown and two buns
  area(im, K2.ellipse(M(w, h), 16.5, 11 + y0, 5, 5.5), PAPER[2]);
  area(im, K2.poly(M(w, h), [[11, 10 + y0], [12, 6 + y0], [16, 5 + y0], [21, 6 + y0], [22, 10 + y0], [20, 8 + y0], [13, 8 + y0]]), HAIR[1]);
  area(im, K2.ellipse(M(w, h), 11.5, 5 + y0, 2.4, 2.4), HAIR[1]);
  area(im, K2.ellipse(M(w, h), 21.5, 5 + y0, 2.4, 2.4), HAIR[1]);
  const out = inked(im);
  // wide sleeves hanging to paper cuffs, the hands clasped over a gold ingot: over the robe, with
  // their own outline
  const lift = arms, sv = img(w, h);
  const sl = K2.poly(M(w, h), [[11, 18 + y0], [14, 20 + y0], [14, 28 + y0 - lift], [7, 30 + y0 - lift], [8, 22 + y0]]);
  const sr = K2.poly(M(w, h), [[21, 18 + y0], [18, 20 + y0], [18, 28 + y0 - lift], [25, 30 + y0 - lift], [24, 22 + y0]]);
  area(sv, sl, ROBE[1]); area(sv, sr, ROBE[1]);
  area(sv, K2.rect(M(w, h), 7, 29 + y0 - lift, 13, 30 + y0 - lift), PAPER[2]);
  area(sv, K2.rect(M(w, h), 19, 29 + y0 - lift, 25, 30 + y0 - lift), PAPER[2]);
  area(sv, K2.poly(M(w, h), [[12, 26 + y0], [20, 26 + y0], [19, 29 + y0], [13, 29 + y0]]), GOLD[2]);
  area(sv, K2.ellipse(M(w, h), 16, 25.5 + y0, 1.8, 1.5), GOLD[2]);
  return D.over(out, inked(sv));
}

function servantLight(out, f, { hop = 0, crouch = 0, red = false, arms = 0 } = {}) {
  const y0 = -hop + crouch;
  // gold clouds worked on the hem, a knot on the sash, red ribbons in the buns
  for (let x = 9; x <= 23; x += 4) { put(out, x, 33 + y0, P.G0); put(out, x + 1, 32 + y0, P.G0); put(out, x + 2, 33 + y0, P.G0); }
  put(out, 16, 24 + y0, P.G2); put(out, 16, 25 + y0, P.G1); put(out, 15, 26 + y0, P.G1); put(out, 17, 26 + y0, P.G1);
  put(out, 12, 3 + y0, P.R2); put(out, 13, 3 + y0, P.R3); put(out, 20, 3 + y0, P.R3); put(out, 21, 3 + y0, P.R2);
  // the painted face: arched brows, eyes curved shut in a smile (or painted open, red), round
  // rouge, a small red mouth
  put(out, 13, 9 + y0, P.S1); put(out, 14, 8 + y0, P.S1); put(out, 18, 8 + y0, P.S1); put(out, 19, 9 + y0, P.S1);
  if (red) {
    for (const ex of [14, 18]) { put(out, ex, 11 + y0, P.R3); put(out, ex, 10 + y0, P.R2); put(out, ex + 1, 11 + y0, P.W); }
  } else {
    for (const ex of [13, 17]) { put(out, ex, 11 + y0, P.S1); put(out, ex + 1, 10 + y0, P.S1); put(out, ex + 2, 11 + y0, P.S1); }
  }
  for (const [rx, ry] of [[13, 13], [19, 13]]) { put(out, rx, ry + y0, P.K2); put(out, rx + 1, ry + y0, P.K2); put(out, rx, ry + 1 + y0, P.K1); put(out, rx + 1, ry + 1 + y0, P.K2); }
  put(out, 16, 14 + y0, P.R2); put(out, 17, 14 + y0, P.R3);
  if (red) return glowAround(out, 1, P.R1, P.R0, f + 1);
  return out;
}

const SERVANT_RAMPS = [PAPER, ROBE, GOLD, HAIR, WOOD];
function servants(list) {
  const lit = light(list.map(o => servantFlat(o.f, o)), SERVANT_RAMPS, { ao: 0 });
  return lit.map((out, i) => servantLight(out, list[i].f, list[i]));
}

function makeServant() {
  const loop = servants([0, 2, 3, 1].map((hop, f) => ({ f, hop })));
  const aim = servants([0, 1, 2].map(f => ({ f, crouch: [1, 2, 2][f], red: f > 0, arms: -1 })));
  const dash = servants([0, 1, 2].map(f => ({ f, red: true, arms: 3 }))).map((body, f) => {
    const im = img(PS.w, PS.h), r = D.rng(5 + f);
    // speed lines streaming behind, and scraps of paper whipped off it
    for (let k = 0; k < 9; k++) {
      const y = 6 + r() * 32, len = 5 + r() * 10, x0 = r() * 5;
      for (let x = x0; x < x0 + len; x++) if (bayer(Math.round(x), Math.round(y)) < 0.85 - (x - x0) / len * 0.6) put(im, x, y, k % 3 ? P.CR1 : P.W);
    }
    D.over(im, K2.rotate(body, -0.38));
    for (let k = 0; k < 3; k++) { const x = 2 + r() * 6, y = 8 + r() * 26; put(im, x, y, P.CR2); put(im, x + 1, y, P.R2); }
    return im;
  });
  // torn into three and burning from the tears
  const whole = loop[0], death = [];
  const tear = y => 16 + Math.sin(y * 0.9) * 1.8;
  const part = (x, y) => y < 16 ? 0 : x < tear(y) ? 1 : 2;
  const fly = [[0.8, -2.6], [-2.4, 0.8], [2.4, 1]];
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(PS.w, PS.h);
    for (let y = 0; y < PS.h; y++) for (let x = 0; x < PS.w; x++) {
      const c = D.get(whole, x, y);
      if (!c) continue;
      const p = part(x, y), [vx, vy] = fly[p];
      const spin = p === 0 ? 0 : (p === 1 ? -1 : 1) * t * 0.5;
      const dx = x - 16, dy = y - 24;
      const nx = 16 + dx * Math.cos(spin) - dy * Math.sin(spin) + vx * t * 6, ny = 24 + dx * Math.sin(spin) + dy * Math.cos(spin) + vy * t * 6 + t * t * 8;
      const edge = Math.abs(y - 16) < 1.6 || (y >= 16 && Math.abs(x - tear(y)) < 1.6);
      const burn = edge ? t * 4 : Math.max(0, t * 3 - 1.2) * (1 - Math.abs(dx) / 16);
      let col = c;
      if (f === 0) col = P.W;
      else if (burn > 2.2) col = bayer(x, y) < 0.5 ? P.AS3 : P.AS2;
      else if (burn > 1.2) col = P.O1;
      else if (burn > 0.4) col = bayer(x, y) < 0.5 ? P.O2 : P.G2;
      if (bayer(x + f, y) < Math.max(0, t - 0.4) * 1.7) continue;
      put(im, nx, ny, col);
    }
    if (f >= 1) motes(im, f, 6, 8, 88, 6, 26, 2, 30, [P.O2, P.AS3]);
    death.push(im);
  }
  return { paper_servant: loop, paper_servant_aim: aim, paper_servant_dash: dash, paper_servant_death: death };
}

// ================================================================ the hell money burner
const HB = { w: 38, h: 44 };
function burnerFlat(f, { rise = 0, raise = 0 } = {}) {
  const { w, h } = HB, im = img(w, h), y0 = -rise;
  const fr = [0, 1, 1, 0, -1, -1][f % 6];
  // the robe: a hunched back, the hood pushed forward, the hem fraying into smoke
  const robe = K2.poly(M(w, h), [[9, 16 + y0], [13, 9 + y0], [20, 6 + y0], [27, 8 + y0], [30, 13 + y0], [28, 20 + y0],
    [29, 30], [27, 36], [23, 38 + fr], [19, 36], [15, 39 - fr], [11, 36], [8, 38 + fr], [7, 28], [8, 21]]);
  area(im, robe, ASH[2]);
  // the hood's mouth, and a pale jaw in it
  area(im, K2.poly(M(w, h), [[23, 11 + y0], [29, 12 + y0], [30, 18 + y0], [25, 19 + y0], [23, 15 + y0]]), IRON[0]);
  area(im, K2.poly(M(w, h), [[25, 16 + y0], [29, 16 + y0], [28, 18 + y0], [26, 18 + y0]]), PAPER[1]);
  // the white mourning band round the hood, its tails hanging behind
  area(im, stroke(M(w, h), [[15, 10 + y0], [20, 8 + y0], [27, 9 + y0]], 1), PAPER[2]);
  area(im, stroke(M(w, h), [[15, 10 + y0], [12, 14 + y0 + fr], [11, 19 + y0]], 1), PAPER[2]);
  // a hemp cord at the waist, and a string of paper ingots hanging off it at the hip
  for (let x = 8; x <= 28; x++) { const y = Math.round(24 + (x - 8) * -0.05); if (D.mget(robe, x, y)) dot(im, x, y, ASH[1]); }
  for (let k = 0; k < 3; k++) {
    const ix = 10 + k * 3, iy = 27 + k * 2;
    dot(im, ix + 1, iy - 1, WOOD[1]);
    area(im, K2.poly(M(w, h), [[ix, iy], [ix + 3, iy], [ix + 2, iy + 2], [ix + 1, iy + 2]]), GOLD[2]);
  }
  // a bundle of spirit money slung at its back
  area(im, K2.rect(M(w, h), 6, 17 + y0, 11, 22 + y0), TALIS[1]);
  if (raise < 0.5) {
    // the long sleeve out to the brazier, bony hands round its rim
    area(im, K2.poly(M(w, h), [[22, 18 + y0], [27, 19 + y0], [31, 24], [27, 27], [21, 23 + y0]]), ASH[2]);
    area(im, K2.rect(M(w, h), 29, 23, 31, 25), PAPER[1]);
    const bowl = K2.poly(M(w, h), [[28, 25], [37, 25], [36, 30], [29, 30]]);
    area(im, bowl, IRON[2]);
    area(im, stroke(M(w, h), [[30, 31], [29, 33]], 1), IRON[2]);
    area(im, stroke(M(w, h), [[35, 31], [36, 33]], 1), IRON[2]);
  } else {
    // one arm raised, a wad of notes in its fist; the brazier hangs at its side in the other
    area(im, K2.poly(M(w, h), [[22, 16 + y0], [27, 15 + y0], [30, 10 + y0], [29, 7 + y0], [26, 8 + y0], [23, 13 + y0]]), ASH[2]);
    area(im, K2.rect(M(w, h), 27, 5 + y0, 30, 7 + y0), PAPER[1]);
    area(im, K2.rect(M(w, h), 26, 3 + y0, 32, 5 + y0), TALIS[1]);
    const bowl = K2.poly(M(w, h), [[26, 28], [33, 28], [32, 32], [27, 32]]);
    area(im, bowl, IRON[2]);
  }
  return inked(im);
}

function flames(im, cx, base, width, height, f, seed, hot = 0.8) {
  for (let x = Math.floor(cx - width / 2); x <= Math.ceil(cx + width / 2); x++) {
    const edge = 1 - Math.abs(x - cx) / (width / 2 + 0.5);
    const hh = height * Math.max(0, edge) * (0.55 + 0.45 * W.fbm(x / 2.2, f * 0.8, seed));
    for (let y = 0; y <= hh; y++) {
      const k = y / Math.max(1, hh), wob = Math.round(Math.sin(y * 0.8 + f * 1.7 + x) * k);
      const c = pick(W.FIRE, hot - k * 0.75 + (bayer(x, y + f) - 0.5) * 0.1);
      if (c) put(im, x + wob, base - y, c);
    }
  }
}

// firelight: the lit pixels near a fire warmed toward orange
function firelight(im, fx, fy, reach, strength = 0.5) {
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    const c = D.get(im, x, y);
    if (!c) continue;
    const d = Math.hypot(x - fx, y - fy);
    if (d > reach) continue;
    const k = (1 - d / reach) * strength;
    if (bayer(x, y) < k) put(im, x, y, [Math.min(255, c[0] + 70), Math.min(255, c[1] + 28), c[2], 255]);
  }
}

function burnerLight(out, f, { rise = 0, raise = 0 } = {}) {
  const y0 = -rise;
  // coal eyes in the hood
  const eye = f % 6 === 2 ? P.O1 : P.O2;
  put(out, 26, 13 + y0, eye); put(out, 28, 13 + y0, eye); put(out, 26, 14 + y0, P.R2); put(out, 28, 14 + y0, P.R2);
  // the red seal on the bundle and the ingots' shine
  put(out, 8, 19 + y0, P.R2); put(out, 9, 19 + y0, P.R2); put(out, 8, 20 + y0, P.R1);
  if (raise < 0.5) {
    flames(out, 32.5, 24, 9, 7 + raise * 8, f, 11);
    firelight(out, 32, 22, 11, 0.55);
  } else {
    flames(out, 29, 3 + y0, 8 + raise * 3, 3 + raise * 3, f, 29, 0.9 + raise * 0.1);
    flames(out, 29.5, 27, 6, 4, f, 13);
    firelight(out, 29, 3 + y0, 12, 0.6);
    firelight(out, 29, 27, 7, 0.4);
  }
  const r = D.rng(23);
  motes(out, f, 6, 6, 23, 26, 37, 0, 18, [P.O2, P.AS3]);
  // the hem's smoke, curling off
  for (let k = 0; k < 5; k++) { const x = 8 + k * 4 + r() * 2, y = 39 + ((f + k) % 3); if (bayer(Math.round(x), y) < 0.6) put(out, x, y, P.AS2); }
  return out;
}

const BURNER_RAMPS = [ASH, PAPER, WOOD, GOLD, TALIS, IRON];
function burners(list) {
  const lit = light(list.map(o => burnerFlat(o.f, o)), BURNER_RAMPS, { ao: 3 });
  return lit.map((out, i) => burnerLight(out, list[i].f, list[i]));
}

function makeBurner() {
  const loop = burners(Array.from({ length: 6 }, (_, f) => ({ f })));
  const act = burners(Array.from({ length: 5 }, (_, f) => ({ f, rise: Math.round(f / 4 * 2), raise: f / 4 })));
  const whole = loop[0], death = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(HB.w, HB.h);
    // the robe sinks into a heap of ash, the brazier tips and spills
    for (let y = 0; y < HB.h; y++) for (let x = 0; x < HB.w; x++) {
      const c = D.get(whole, x, y);
      if (!c) continue;
      const ny = y + (39 - y) * Math.min(1, t * 1.25) * 0.8 + (W.hash2(x, 3, 1) - 0.5) * t * 2;
      if (bayer(x, y + f) < Math.max(0, t - 0.55) * 2.2) continue;
      put(im, x + (x > 27 ? t * 4 : 0), ny, f === 0 ? P.W : f < 2 ? c : W.hash2(x, y, f) < 0.5 ? P.AS2 : P.AS1);
    }
    const r = D.rng(9);
    for (let k = 0; k < 14; k++) {
      const x = 32 + r() * 3 + t * (3 + r() * 8), y = 26 - Math.sin(t * Math.PI) * (4 + r() * 8) + t * 10;
      if (bayer(Math.round(x), Math.round(y)) < t * 0.7) continue;
      put(im, x, y, r() < 0.5 ? P.O2 : P.G2);
    }
    motes(im, f, 6, 8, 91, 8, 30, 6, 38, [P.AS3, P.AS2]);
    death.push(im);
  }
  return { hell_money_burner: loop, hell_money_burner_act: act, hell_money_burner_death: death };
}

// ================================================================ the soul guiding lantern
const LN = { w: 32, h: 44, cx: 16, cy: 18 };
const YIN = ["11101", "00101", "11101", "10001", "11101", "00101", "01101"];

function lanternFlat(f, { sway = 0 } = {}) {
  const { w, h } = LN, im = img(w, h), cx = LN.cx, cy = LN.cy;
  // the cord it hangs from, from nothing
  area(im, stroke(M(w, h), [[cx, 1], [cx, 7]], 1), WOOD[1]);
  // the roof cap: gold lacquer with upturned eaves and a red ridge
  area(im, K2.poly(M(w, h), [[cx - 3, 7], [cx + 3, 7], [cx + 8, 10], [cx + 9, 8], [cx + 8, 11], [cx - 8, 11], [cx - 9, 8], [cx - 8, 10]]), GOLD[2]);
  area(im, K2.rect(M(w, h), cx - 2, 6, cx + 2, 7), ROBE[2]);
  // the paper body
  area(im, K2.ellipse(M(w, h), cx, cy + 1, 9, 8.2), PAPER[2]);
  // the base: a gold lacquered rim
  area(im, K2.poly(M(w, h), [[cx - 5, cy + 8], [cx + 5, cy + 8], [cx + 4, cy + 11], [cx - 4, cy + 11]]), GOLD[2]);
  // talisman strips hanging off its sides, swinging
  for (const [sx, ph] of [[cx - 9, 0], [cx + 9, 2]]) {
    const s = Math.sin((f + ph) / 6 * TAU) * 1.2 - sway * 0.5;
    area(im, K2.poly(M(w, h), [[sx - 1, cy + 3], [sx + 1, cy + 3], [sx + 1 + s, cy + 14], [sx - 1 + s, cy + 14]]), TALIS[1]);
  }
  // the tassel: a gold bead, then long red silk
  area(im, K2.ellipse(M(w, h), cx, cy + 13, 1.6, 1.6), GOLD[2]);
  const tip = cx - sway * 1.5;
  area(im, K2.poly(M(w, h), [[cx - 1, cy + 14], [cx + 1, cy + 14], [tip + 2, cy + 24], [tip - 2, cy + 24]]), ROBE[2]);
  return inked(im);
}

function lanternLight(out, f, { bright = 0.6, sway = 0 } = {}) {
  const { w, h, cx, cy } = LN;
  // the green soul fire lighting the paper from inside, and the flame's own shadow on it
  const flick = [0, 1, 0, -1, 0, 1][f % 6];
  for (let y = cy - 7; y <= cy + 9; y++) for (let x = cx - 9; x <= cx + 9; x++) {
    const c = D.get(out, x, y);
    if (!c || !(c[0] > 150 && c[1] > 140 && c[2] > 140)) continue;     // the paper only
    const d = Math.hypot((x - cx) / 8.5, (y - cy - 1) / 8);
    const v = (1 - d) * (0.7 + bright * 0.5) + (bayer(x, y) - 0.5) * 0.15;
    const inFlame = Math.abs(x - cx - flick * ((cy + 6 - y) / 10)) < Math.max(0, (y - (cy - 5)) / 3.2) && y < cy + 6;
    let col = v > 0.72 ? P.J5 : v > 0.5 ? P.J4 : v > 0.3 ? C("#c8f0da") : null;
    if (inFlame) col = y > cy + 2 ? P.J3 : P.J2;
    if (col) put(out, x, y, col);
  }
  // ribs round the paper, and 引 painted on it
  for (const dy of [-4, 2, 7]) for (let x = cx - 9; x <= cx + 9; x++) { const c = D.get(out, x, cy + dy); if (c && c[1] > 150 && bayer(x, dy + 8) < 0.7) put(out, x, cy + dy, P.J2); }
  YIN.forEach((row, j) => [...row].forEach((b, i) => { if (b === "1") put(out, cx - 2 + i, cy - 3 + j, P.R1); }));
  // the talismans' red script
  for (const [sx, ph] of [[cx - 9, 0], [cx + 9, 2]]) {
    const s = Math.sin((f + ph) / 6 * TAU) * 1.2 - sway * 0.5;
    for (let y = cy + 5; y <= cy + 12; y += 2) put(out, sx + s * (y - cy - 3) / 11, y, P.R2);
  }
  let g = glowAround(out, 2, P.J2, P.J1, f + 1);
  // three wisps circling it
  for (let k = 0; k < 3; k++) {
    const a = f / 6 * TAU + k * TAU / 3, x = cx + Math.cos(a) * 13, y = cy + 2 + Math.sin(a) * 5;
    put(g, x, y, P.J5); put(g, x - Math.sin(a) * 1.5, y + Math.cos(a) * 0.6, P.J3);
    if (Math.sin(a) > 0) put(g, x + 1, y, P.J4);
  }
  return g;
}

const LANTERN_RAMPS = [PAPER, GOLD, ROBE, TALIS, WOOD];
function lanterns(list) {
  const lit = light(list.map(o => lanternFlat(o.f, o)), LANTERN_RAMPS, { ao: 0 });
  return lit.map((out, i) => lanternLight(out, list[i].f, list[i]));
}

function makeLantern() {
  const sways = [0, 1, 2, 1, 0, -1];
  const loop = lanterns(sways.map((sway, f) => ({ f, sway, bright: 0.55 + 0.35 * Math.sin(f / 6 * TAU) })));
  const death = [];
  const full = lanterns([{ f: 0, bright: 1.4 }])[0];
  for (let f = 0; f < 8; f++) {
    const t = f / 7;
    let im;
    if (f === 0) im = K.silhouette(full, P.J5);
    else {
      // the paper catching green fire from the middle out, then gone
      im = img(LN.w, LN.h);
      for (let y = 0; y < LN.h; y++) for (let x = 0; x < LN.w; x++) {
        const c = D.get(full, x, y);
        if (!c) continue;
        const d = Math.hypot(x - LN.cx, y - LN.cy) / 16;
        const burnt = t * 1.6 - d;
        if (burnt > 0.55 || bayer(x + f, y) < Math.max(0, t - 0.5) * 2) continue;
        put(im, x, y - (burnt > 0 ? t * 4 : 0), burnt > 0.3 ? P.J3 : burnt > 0 ? P.J5 : c);
      }
    }
    const r = D.rng(99);
    for (let k = 0; k < 18; k++) {
      const a = r() * TAU, d = 3 + t * (9 + r() * 10);
      const x = LN.cx + Math.cos(a) * d, y = LN.cy + Math.sin(a) * d - t * 6;
      if (bayer(Math.round(x), Math.round(y)) < t * 0.75) continue;
      put(im, x, y, r() < 0.4 ? P.J5 : P.J3);
    }
    death.push(im);
  }
  return { soul_lantern: loop, soul_lantern_death: death };
}

function makeMobs() {
  return Object.assign({}, makeSoul(), makeLily(), makeServant(), makeBurner(), makeLantern());
}

module.exports = { makeMobs, flames, firelight, light, ramp, area, dot, GH, HAIR, PAPER, ROBE, GOLD, WOOD, STEM, SOIL, ASH, IRON, TALIS };
