// the two guardians of the underworld's gate, Huangquan Road's rush bosses, 96x96, facing right.
// made like the horde (flat, black-outlined, lit by ../restyle.js, then their own light laid on),
// posed from a few joints so every animation stays on model:
//   bull_head            Niú Tóu, Ox-Head: a hulking ox-demon in an iron pauldron and a red war
//                        skirt, bone horns sweeping forward, a gold ring through his nose, ember
//                        eyes, a steel trident at his back. march 6 (a heavy stride, steam off his
//                        nostrils), telegraph 4 (head down, horns levelled, a hoof pawing the
//                        road, eyes blazing), charge 4 (a flat-out gallop, horns first), stun 4
//                        (rocked back, eyes spinning, stars round his horns), death 10 (he drops to
//                        a knee and bursts into embers and red smoke)
//   horse_face           Mǎ Miàn, Horse-Face: a tall, floating underworld official, a long pale
//                        horse's skull of a face with an icy eye, a black mane, the black gauze
//                        cap with its two wings, a dark teal robe with a rank badge and a jade
//                        belt, an ivory court tablet in one hand, a chain whip in the other with
//                        a soul lantern on its end; below the waist he's azure mist. float 6, raise
//                        3 (the chain swung up, the lantern held high and blazing), death 10
const K = require("./kit");
const { P, D, W, K2, put, bayer, img, TAU, lerp, M, blob, stroke, dissolve, motes, pick, glowAround, C, inked } = K;
const MB = require("./mobs");
const { light, area, dot, HAIR, PAPER, ROBE, GOLD, WOOD, IRON } = MB;

const S = 96;
const HIDE = ["#1c0c0a", "#2a1410", "#4e2418", "#7a3c24", "#a8603a"];
const HORN = ["#4a4034", "#6e6250", "#b0a284", "#e0d4b8", "#fff4e0"];
const STEEL = ["#2b2232", "#4a5470", "#8a9ab8", "#c8d6ea"];
const FACE = ["#1c2430", "#34465a", "#587690", "#8eaec2", "#c8dcea"];
const TEAL = ["#0b2533", "#12505e", "#1c8585", "#3cc2a6"];
const JADE = ["#155c44", "#26986a", "#54d898", "#aef5cc"];
const STEEL_C = STEEL.map(C);

// ================================================================ Ox-Head

// a pose: where his hip is, how far his upper body leans forward about it (radians), his head's
// own tilt about his neck, and his two feet
function bullPose(kind, f) {
  const p = { hip: [44, 60], lean: 0, nod: 0, near: [52, 91], far: [38, 91], nearLift: 0, farLift: 0, arm: 0, bob: 0, tridentBack: 0 };
  if (kind === "march") {
    const ph = f / 6 * TAU, s = Math.sin(ph);
    p.near = [48 + s * 8, 91 - Math.max(0, Math.cos(ph)) * 4];
    p.far = [40 - s * 8, 91 - Math.max(0, -Math.cos(ph)) * 4];
    p.bob = Math.round(Math.abs(Math.sin(ph)) * 2);
    p.lean = 0.06; p.nod = 0.05 * Math.sin(ph * 2); p.arm = s * 3;
  } else if (kind === "telegraph") {
    p.lean = 0.2; p.nod = 0.42; p.bob = 2;
    const paw = f % 2;
    p.near = paw ? [60, 86] : [54, 91];
    p.far = [34, 91];
  } else if (kind === "charge") {
    const ph = f / 4 * TAU, s = Math.sin(ph);
    p.lean = 0.5; p.nod = 0.55; p.hip = [40, 62]; p.bob = Math.round(Math.cos(ph) * 2);
    p.near = [50 + s * 14, 90 - Math.max(0, Math.cos(ph)) * 7];
    p.far = [34 - s * 14, 90 - Math.max(0, -Math.cos(ph)) * 7];
    p.arm = -6; p.tridentBack = 1;
  } else if (kind === "stun") {
    p.lean = -0.14 + Math.sin(f / 4 * TAU) * 0.04; p.nod = -0.2 + Math.sin(f / 4 * TAU) * 0.08;
    p.near = [56, 91]; p.far = [30, 91]; p.bob = 3; p.arm = 4;
  }
  return p;
}

// the body's points, leant about the hip, and the head's, nodded about the neck
function bullFrame(p) {
  const [hx, hy0] = p.hip, hy = hy0 + p.bob;
  const ca = Math.cos(p.lean), sa = Math.sin(p.lean);
  const T = ([x, y]) => { const dx = x - hx, dy = y - hy0; return [hx + dx * ca - dy * sa, hy + dx * sa + dy * ca]; };
  const neck = T([62, 32]);
  const cn = Math.cos(p.nod), sn = Math.sin(p.nod);
  const Hd = ([x, y]) => { const q = T([x, y]); const dx = q[0] - neck[0], dy = q[1] - neck[1]; return [neck[0] + dx * cn - dy * sn, neck[1] + dx * sn + dy * cn]; };
  return { T, Hd, hx, hy };
}

// the hand-drawn head, turned by the pose's lean and nod, placed with its poll on the neck: the
// image to lay into the flat source, and where any of its pixels ends up
const HEAD_MAP = require("./bullhead_map");
const HEAD_KEY = { H: HIDE[2], h: HIDE[1], L: HIDE[3], B: HORN[2], b: HORN[1], W: HORN[3], f: HAIR[1], g: GOLD[1], k: "#000000", e: HIDE[1] };
function bullHead(p) {
  const { T } = bullFrame(p);
  const w = HEAD_MAP[0].length, h = HEAD_MAP.length, flat = img(w, h);
  HEAD_MAP.forEach((row, y) => [...row].forEach((ch, x) => { if (HEAD_KEY[ch]) dot(flat, x, y, HEAD_KEY[ch]); }));
  const ang = p.lean + p.nod * 0.85, ca = Math.cos(ang), sa = Math.sin(ang);
  const poll = T([67, 13]), pl = [9, 9];
  const at = (x, y) => { const dx = x - pl[0], dy = y - pl[1]; return [poll[0] + dx * ca - dy * sa, poll[1] + dx * sa + dy * ca]; };
  const R = 44, turned = Math.abs(ang) < 0.02 ? null : K2.rotate(flat, ang, R, R);
  const image = K.canvas(S, S);
  if (!turned) D.blit(image, flat, Math.round(poll[0] - pl[0]), Math.round(poll[1] - pl[1]));
  else { const c = at(w / 2, h / 2); D.blit(image, turned, Math.round(c[0] - R / 2), Math.round(c[1] - R / 2)); }
  return { image, at };
}

// a leg from the hip to a foot, the knee bent forward between them
function leg(hip, foot, thick, knee = 1) {
  const mx = (hip[0] + foot[0]) / 2, my = (hip[1] + foot[1]) / 2;
  const len = Math.hypot(foot[0] - hip[0], foot[1] - hip[1]);
  const bend = Math.max(0, 32 - len) * 0.6 + 3;
  return [hip, [mx + bend * knee, my], foot];
}

function bullFlat(kind, f) {
  const im = K.canvas(S, S), p = bullPose(kind, f), { T, Hd, hx, hy } = bullFrame(p);
  const poly = pts => K2.poly(M(S, S), pts.map(T));
  const hpoly = pts => K2.poly(M(S, S), pts.map(Hd));
  const hip = [hx, hy];

  // ---- behind him: the tail, the far leg, the trident and the far arm
  const tsw = Math.sin(f / 3 * Math.PI) * 2;
  area(im, stroke(M(S, S), [T([30, 50]), T([24, 58]), T([22 + tsw, 68])], 2), HIDE[2]);
  area(im, K2.ellipse(M(S, S), ...T([22 + tsw, 70]), 2.2, 3), HAIR[1]);

  const farLeg = leg([hip[0] - 6, hip[1] + 2], p.far, 8, 1);
  area(im, stroke(M(S, S), farLeg, 7), HIDE[1]);
  area(im, K2.rect(M(S, S), p.far[0] - 4, p.far[1] - 3, p.far[0] + 3, p.far[1]), IRON[1]);

  // the steel trident: shaft through his far fist, three tines and a crescent guard at its head
  const grip = T([31, 46]);
  const up = p.tridentBack ? [-0.93, -0.36] : [0.16, -0.99];
  const top = [grip[0] + up[0] * 40, grip[1] + up[1] * 40], butt = [grip[0] - up[0] * 44, grip[1] - up[1] * 44];
  area(im, stroke(M(S, S), [butt, top], 2), WOOD[1]);
  const side = [-up[1], up[0]];
  const at = (a, b) => [top[0] + up[0] * a + side[0] * b, top[1] + up[1] * a + side[1] * b];
  area(im, stroke(M(S, S), [at(0, -5), at(0, 5)], 2), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, 0), at(12, 0)], 2), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, -5), at(7, -6), at(10, -5)], 1), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, 5), at(7, 6), at(10, 5)], 1), STEEL[2]);
  area(im, stroke(M(S, S), [at(-2, -1), at(-4, 0), at(-2, 1)], 1), ROBE[2]);    // a red tassel

  area(im, stroke(M(S, S), [T([42, 26]), T([33, 36]), grip], 7), HIDE[1]);
  area(im, K2.ellipse(M(S, S), grip[0], grip[1], 3.5, 3.5), HIDE[2]);

  // ---- the torso: a great hump of shoulder over a deep chest, narrowing to the waist
  area(im, poly([[28, 32], [34, 22], [46, 16], [58, 17], [68, 24], [70, 36], [64, 46], [60, 56], [38, 57], [33, 46]]), HIDE[2]);
  // a shaggy black mane down the hump, like a bison's, ragged at its edge
  const mane = [[62, 12], [56, 10], [48, 12], [40, 16], [33, 22], [28, 30], [27, 38]];
  const maneM = stroke(M(S, S), mane.map(T), 5);
  for (let k = 0; k < mane.length - 1; k++) {
    const [x0, y0] = T(mane[k]), [x1, y1] = T(mane[k + 1]);
    const nx = -(y1 - y0), ny = x1 - x0, l = Math.hypot(nx, ny) || 1;
    // ragged clumps sticking up and back off it
    const mx = (x0 + x1) / 2, my = (y0 + y1) / 2, ux = nx / l, uy = ny / l;
    const tip = [mx + ux * 5 - 2.5, my + uy * 5 - 1];
    K2.poly(maneM, [[mx - uy * 2.5, my + ux * 2.5], [mx + uy * 2.5, my - ux * 2.5], tip]);
  }
  area(im, maneM, HAIR[1]);
  // chest and belly muscle, drawn in the darker hide
  for (const q of [[[56, 32], [64, 38]], [[48, 42], [60, 42]], [[50, 48], [58, 48]], [[44, 26], [56, 30]]]) area(im, stroke(M(S, S), q.map(T), 1), HIDE[1]);

  // a leather bandolier across the chest, studded, a little skull hung on it
  area(im, stroke(M(S, S), [T([40, 20]), T([50, 34]), T([60, 52])], 3), IRON[1]);
  area(im, K2.ellipse(M(S, S), ...T([50, 34]), 2.5, 2.5), HORN[2]);
  // ---- the war skirt: red, iron plates over it, gold studs; an iron belt with a skull buckle
  const skirt = poly([[37, 55], [61, 55], [66, 74], [58, 76], [48, 73], [40, 76], [32, 72]]);
  area(im, skirt, ROBE[2]);
  for (const [x0, x1] of [[37, 43], [47, 53], [57, 63]]) area(im, poly([[x0, 58], [x1, 58], [x1 + 1, 70], [x0 + 1, 70]]), IRON[2]);
  area(im, poly([[35, 54], [64, 54], [64, 58], [35, 58]]), IRON[1]);
  area(im, K2.ellipse(M(S, S), ...T([52, 56]), 3, 3), GOLD[2]);

  // ---- the near leg, a hoof like an anvil
  const nearLeg = leg([hip[0] + 6, hip[1] + 4], p.near, 9, 1);
  area(im, stroke(M(S, S), nearLeg, 9), HIDE[2]);
  area(im, K2.rect(M(S, S), p.near[0] - 5, p.near[1] - 4, p.near[0] + 4, p.near[1]), IRON[1]);
  // a shin guard
  const kx = nearLeg[1][0], ky = nearLeg[1][1];
  area(im, K2.poly(M(S, S), [[kx - 4, ky + 1], [kx + 4, ky + 1], [kx + 2, ky + 9], [kx - 3, ky + 9]]), IRON[2]);

  // ---- the head, drawn by hand (bullhead_map.js), turned with the pose, on a bull's thick neck
  const hd = bullHead(p);
  area(im, K2.poly(M(S, S), [T([50, 18]), T([60, 16]), hd.at(8, 10), hd.at(6, 20), hd.at(12, 23), T([64, 34]), T([54, 30])]), HIDE[2]);

  // ---- the near arm, huge, over everything, and the pauldron on its shoulder
  const out = inked(im);
  const fg = K.canvas(S, S);
  const sh = T([60, 26]), el = T([67 + p.arm * 0.3, 40]), fist = T([70 + p.arm, 53]);
  area(fg, stroke(M(S, S), [sh, el, fist], 8), HIDE[2]);
  area(fg, K2.poly(M(S, S), [T([62, 42]), T([70, 42]), T([72 + p.arm, 50]), T([64 + p.arm, 50])]), IRON[2]);   // the bracer
  area(fg, K2.ellipse(M(S, S), fist[0], fist[1] + 1, 4.5, 4), HIDE[3]);
  // the pauldron: three lames of black iron stepping down the shoulder, a spike off the top one
  area(fg, K2.poly(M(S, S), [T([51, 21]), T([57, 16]), T([65, 17]), T([69, 23]), T([66, 25]), T([52, 25])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([51, 25]), T([67, 25]), T([69, 29]), T([52, 30])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([53, 30]), T([69, 29]), T([69, 33]), T([55, 34])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([54, 17]), T([58, 16]), T([50, 10])]), IRON[3]);
  // the head last, over the shoulder: it's what he leads with
  const head = K.canvas(S, S);
  D.over(head, hd.image);
  return D.over(D.over(out, inked(fg)), inked(head));
}

// his light: the eye, the steel and gold catching, the nose ring, and his breath
function bullLight(out, kind, f) {
  const p = bullPose(kind, f), { T, Hd } = bullFrame(p);
  const hd = bullHead(p), eye = hd.at(9.5, 12);
  if (kind === "stun") {
    // eyes spinning
    const a = f / 4 * TAU;
    for (let k = 0; k < 4; k++) put(out, eye[0] + Math.round(Math.cos(a + k * 1.6) * 1.2), eye[1] + Math.round(Math.sin(a + k * 1.6) * 1.2), k % 2 ? P.G2 : P.W);
  } else {
    const hot = kind === "march" ? [P.R2, P.R3] : [P.R3, P.W];
    put(out, eye[0], eye[1], hot[1]); put(out, eye[0] - 1, eye[1], hot[0]); put(out, eye[0] + 1, eye[1], hot[0]); put(out, eye[0], eye[1] - 1, P.R1);
    // the brow over it
    for (let k = -2; k <= 2; k++) put(out, eye[0] + k, eye[1] - 2 - (k > 0 ? 1 : 0), P.S0);
  }
  // nostrils and the gold ring through them
  const nose = hd.at(22, 18);
  put(out, nose[0], nose[1], P.S0); put(out, nose[0], nose[1] + 1, P.S0); put(out, nose[0] - 1, nose[1] + 1, P.S0);
  const ring = hd.at(21, 23);
  for (let a = 0; a < 8; a++) put(out, ring[0] + Math.round(Math.cos(a / 8 * TAU) * 2.2), ring[1] + Math.round(Math.sin(a / 8 * TAU) * 2), a < 4 ? P.G2 : P.G3);
  // gold studs on the skirt's plates, rivets on the pauldron
  for (const q of [[40, 62], [50, 62], [60, 62], [40, 67], [50, 67], [60, 67]]) { const s = T(q); put(out, s[0], s[1], P.G2); }
  for (const q of [[53, 24], [57, 24], [61, 24], [65, 24], [55, 29], [60, 29], [65, 28]]) { const s = T(q); put(out, s[0], s[1], P.G2); }
  // the skull buckle
  // the bandolier's studs and its little skull's eyes
  for (const q of [[43, 24], [46, 28], [54, 40], [57, 46]]) { const s = T(q); put(out, s[0], s[1], P.G2); }
  const sk = T([50, 34]);
  put(out, sk[0] - 1, sk[1], P.S0); put(out, sk[0] + 1, sk[1], P.S0);
  const b = T([52, 56]);
  put(out, b[0] - 1, b[1], P.S0); put(out, b[0] + 1, b[1], P.S0); put(out, b[0], b[1] + 2, P.S0);
  // hell runes cut into his hide, glowing: down the arm and a seal on the chest, flaring as he
  // readies a charge
  const hot = kind === "telegraph" || kind === "charge";
  const pulse = hot ? 2 : [0, 1, 1, 0, 0, 1][f % 6];
  const runeCol = [P.R1, P.R2, P.R3][pulse] || P.R3, runeHot = hot ? P.G3 : P.R3;
  const arm = [[61, 30], [63, 34], [65, 33], [66, 37], [68, 36]].map(T);
  arm.forEach(([x, y], k) => put(out, x, y, k % 2 ? runeHot : runeCol));
  const seal = T([50, 36]);
  for (let a = 0; a < 10; a++) put(out, seal[0] + Math.round(Math.cos(a / 10 * TAU) * 3), seal[1] + Math.round(Math.sin(a / 10 * TAU) * 3), a % 3 ? runeCol : runeHot);
  put(out, seal[0], seal[1], runeHot); put(out, seal[0] - 1, seal[1] + 1, runeCol); put(out, seal[0] + 1, seal[1] - 1, runeCol);
  if (hot) {
    // their heat bleeding out round him
    const glow = K.blank(out);
    for (const [x, y] of [...arm, seal]) put(glow, x, y, P.R2);
    D.over(out, K.glowAround(glow, 2, P.R1, P.R0, f), );
  }
  // a chain wrapped round the forearm, its loose end swinging
  const fist = T([70 + p.arm, 53]), sw = Math.sin(f / (kind === "march" ? 6 : 4) * TAU) * 3 - (kind === "charge" ? 8 : 0);
  for (let k = 0; k < 4; k++) { const q = T([63 + k * 2 + p.arm * 0.5, 45 + k]); put(out, q[0], q[1], k % 2 ? STEEL_C[3] : STEEL_C[2]); }
  for (let k = 0; k <= 10; k++) {
    const t = k / 10, x = fist[0] - 2 + sw * t - t * 3, y = fist[1] + 3 + t * 12 - Math.sin(t * Math.PI) * 1;
    put(out, x, y, k % 2 ? STEEL_C[3] : STEEL_C[1]);
    if (k % 2 === 0) put(out, x + 1, y, STEEL_C[2]);
  }
  // steam snorting off his nostrils
  const puffs = kind === "telegraph" ? 3 : kind === "charge" ? 2 : kind === "march" && f % 3 === 0 ? 1 : 0;
  for (let k = 0; k < puffs; k++) {
    const t = ((f + k * 1.3) % 3) / 3;
    const x = nose[0] + 3 + t * 8 + k * 2, y = nose[1] + 3 + t * 3 - k;
    const r = 1.2 + t * 2.2;
    for (let yy = -3; yy <= 3; yy++) for (let xx = -4; xx <= 4; xx++) if (Math.hypot(xx, yy) < r && bayer(x + xx, y + yy) < 0.75 - t * 0.5) put(out, x + xx, y + yy, t < 0.5 ? P.CR2 : P.CR1);
  }
  if (kind === "stun") {
    // stars round his horns
    const c = hd.at(12, -2);
    for (let k = 0; k < 4; k++) {
      const a = f / 4 * TAU + k * TAU / 4, x = c[0] + Math.cos(a) * 12, y = c[1] + Math.sin(a) * 4;
      W.twinkle(out, x, y, Math.sin(a) > 0 ? 1 : 0, [P.W, P.G2]);
    }
  }
  if (kind === "telegraph") {
    // the pawed road kicking up behind
    const r = D.rng(70 + f);
    for (let k = 0; k < 10; k++) put(out, p.near[0] - 6 - r() * 12, p.near[1] - r() * 6, r() < 0.5 ? P.ST3 : P.ST2);
  }
  if (kind === "charge") {
    // speed lines and dust off the hooves
    const r = D.rng(90 + f);
    for (let k = 0; k < 8; k++) {
      const y = 20 + r() * 60, x0 = r() * 10, len = 6 + r() * 12;
      for (let x = x0; x < x0 + len; x++) if (!D.get(out, x, y) && bayer(Math.round(x), Math.round(y)) < 0.7 - (x - x0) / len * 0.5) put(out, x, y, k % 2 ? P.CR1 : P.AS3);
    }
    for (let k = 0; k < 10; k++) put(out, 20 + r() * 20, 84 + r() * 8, r() < 0.5 ? P.ST3 : P.ST2);
  }
  return out;
}

const BULL_RAMPS = [HIDE, HORN, STEEL, ROBE, IRON, GOLD, WOOD, HAIR];
function bulls(kind, n) {
  const lit = light(Array.from({ length: n }, (_, f) => bullFlat(kind, f)), BULL_RAMPS, { ao: 3, minArea: 14 });
  return lit.map((out, f) => bullLight(out, kind, f));
}

function makeBull() {
  const march = bulls("march", 6), telegraph = bulls("telegraph", 4), charge = bulls("charge", 4), stun = bulls("stun", 4);
  // rocked back (his stun pose), a flash, then a burn front sweeping through him from his cracked
  // runes outward, its edge white-hot, what's behind it gone to embers and red smoke
  const death = [];
  const base = stun[0];
  for (let f = 0; f < 10; f++) {
    const t = f / 9;
    let im;
    if (f === 0) im = K.silhouette(base, P.W);
    else if (f === 1) im = K.silhouette(base, P.R3);
    else {
      im = K.blank(base);
      const sink = Math.round(Math.min(1, (t - 0.2) * 1.5) * 4);
      for (let y = -base.oy; y < base.h - base.oy; y++) for (let x = -base.ox; x < base.w - base.ox; x++) {
        const c = D.get(base, x, y);
        if (!c) continue;
        // how early this pixel burns: near the chest seal first, the extremities last, broken up
        const v = Math.hypot(x - 50, y - 36) / 60 * 0.6 + W.fbm(x / 6, y / 6, 5) * 0.55;
        const d = (t - 0.15) * 1.5 - v;
        if (d > 0.1) continue;
        const col = d > 0.06 ? P.W : d > 0.02 ? P.G3 : d > -0.02 ? P.O2 : d > -0.07 ? P.R2 :
          t > 0.3 && bayer(x, y) < t * 0.6 ? P.R0 : c;
        put(im, x, y + sink, col);
      }
    }
    // embers and smoke rising off him
    const r = D.rng(300);
    for (let k = 0; k < 46; k++) {
      const x = 26 + r() * 52, ph = r(), sp = 0.5 + r();
      const y = 88 - ((t * sp + ph) % 1) * 84 * Math.min(1, t * 1.4);
      if (t < 0.2 || bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.7) * 3) continue;
      put(im, x + Math.sin(y * 0.2 + k) * 2, y, k % 3 === 0 ? P.G3 : k % 3 === 1 ? P.O2 : k % 5 === 0 ? P.AS2 : P.R2);
    }
    death.push(im);
  }
  return { bull_head: march, bull_head_telegraph: telegraph, bull_head_charge: charge, bull_head_stun: stun, bull_head_death: death };
}

// ================================================================ Horse-Face

const HORSE_MAP = require("./horsehead_map");
const HORSE_KEY = { F: FACE[2], f: FACE[1], l: FACE[3], K: HAIR[1], m: HAIR[2], g: GOLD[1], k: "#000000", e: FACE[1] };
const LANTERN_AT = { float: null, raise: [74, 8] };

// where things are for a frame: his float, the near arm raised (0 at his side, 1 overhead), and
// the lantern on the chain's end
function horsePose(kind, f) {
  const n = kind === "raise" ? 3 : kind === "death" ? 1 : 6;
  const ph = f / n * TAU;
  const p = { bob: Math.round(Math.sin(ph) * 2), raise: 0, swing: Math.sin(ph) * 0.35, ph };
  if (kind === "raise") { p.raise = [0.35, 0.8, 1][f]; p.bob = -1; p.swing = 0; }
  // the near hand, and the lantern hanging off the chain below it (or held up, swinging over)
  const down = [64, 54], up = [70, 14];
  p.hand = [lerp(down[0], up[0], p.raise), lerp(down[1], up[1], p.raise) + p.bob];
  const chain = lerp(18, 10, p.raise);
  const a = Math.PI / 2 - p.swing - p.raise * 2.4;           // straight down, swung up over his head
  p.lantern = [p.hand[0] + Math.cos(a) * chain, p.hand[1] + Math.sin(a) * chain];
  return p;
}

function horseFlat(kind, f) {
  const im = K.canvas(S, S), p = horsePose(kind, f), b = p.bob;
  const Y = y => y + b;
  // ---- the robe: shoulders, a deep chest, the skirt flaring and trailing off behind into mist
  const tail = Math.sin(p.ph) * 3;
  area(im, K2.poly(M(S, S), [[36, Y(28)], [48, Y(24)], [60, Y(28)], [64, Y(40)], [62, Y(54)], [66, Y(66)], [58, Y(74)],
    [46, Y(80)], [34, Y(86) + tail], [22, Y(90) + tail], [26, Y(78)], [32, Y(66)], [34, Y(52)], [32, Y(40)]]), TEAL[1]);
  // the lapel crossing over the chest, the inner robe's white collar
  area(im, stroke(M(S, S), [[46, Y(26)], [52, Y(36)], [60, Y(42)]], 2), PAPER[2]);
  // the rank badge: a gold-framed square on the chest
  area(im, K2.rect(M(S, S), 42, Y(36), 52, Y(45)), GOLD[1]);
  area(im, K2.rect(M(S, S), 43, Y(37), 51, Y(44)), IRON[0]);
  // the jade belt, stiff, riding low at the front
  area(im, K2.poly(M(S, S), [[32, Y(50)], [62, Y(54)], [62, Y(57)], [32, Y(53)]]), JADE[1]);
  // ---- the far arm: a wide sleeve, the hand holding the ivory court tablet up before him
  area(im, K2.poly(M(S, S), [[40, Y(30)], [34, Y(40)], [38, Y(52)], [50, Y(50)], [52, Y(44)], [46, Y(34)]]), TEAL[1]);
  area(im, K2.poly(M(S, S), [[53, Y(24)], [57, Y(24)], [57, Y(46)], [53, Y(46)]]), PAPER[2]);
  area(im, K2.ellipse(M(S, S), 54, Y(44), 2.5, 2.5), FACE[2]);
  // ---- the neck, under the head
  area(im, K2.poly(M(S, S), [[44, Y(18)], [54, Y(16)], [58, Y(28)], [44, Y(30)]]), FACE[2]);
  // ---- the head, drawn by hand, the mane swaying
  const head = img(HORSE_MAP[0].length, HORSE_MAP.length);
  HORSE_MAP.forEach((row, y) => [...row].forEach((ch, x) => { if (HORSE_KEY[ch]) dot(head, x, y, HORSE_KEY[ch]); }));
  const out = inked(im);
  // ---- the near arm over it all: the sleeve, the hand, and the chain whip
  const fg = K.canvas(S, S);
  const sh = [57, Y(34)], hand = p.hand;
  const el = [lerp(64, 70, p.raise), lerp(Y(44), Y(24), p.raise)];
  area(fg, stroke(M(S, S), [sh, el, hand], 7), TEAL[1]);
  // the sleeve's wide cuff
  area(fg, K2.ellipse(M(S, S), el[0] * 0.3 + hand[0] * 0.7, el[1] * 0.3 + hand[1] * 0.7, 4.5, 4.5), TEAL[2]);
  area(fg, K2.ellipse(M(S, S), hand[0], hand[1], 2.5, 2.5), FACE[2]);
  // the lantern: a small paper one, capped in iron
  const [lx, ly] = p.lantern;
  area(fg, K2.ellipse(M(S, S), lx, ly + 5, 3.5, 4.2), PAPER[2]);
  area(fg, K2.rect(M(S, S), lx - 2, ly, lx + 2, ly + 1), IRON[2]);
  area(fg, K2.rect(M(S, S), lx - 2, ly + 9, lx + 2, ly + 10), IRON[2]);
  // the head last, over the shoulder
  const top = K.canvas(S, S);
  D.blit(top, head, 38, Y(2));
  return D.over(D.over(out, inked(fg)), inked(top));
}

// his light: the icy eye, the badge's crane, the chain's links, the lantern's soul fire, and the
// mist he trails instead of legs
function horseLight(out, kind, f) {
  const p = horsePose(kind, f), b = p.bob, Y = y => y + b;
  const hot = kind === "raise";
  // the eye (the map's 'e' at (13, 10))
  const ex = 38 + 13, ey = Y(2 + 10);
  put(out, ex, ey, hot ? P.W : P.A4); put(out, ex + 1, ey, P.A3); put(out, ex, ey - 1, P.A2);
  // the badge: a white crane over a red sun
  put(out, 48, Y(39), P.R2); put(out, 49, Y(39), P.R2); put(out, 48, Y(40), P.R2);
  for (const [x, y] of [[45, 42], [46, 41], [47, 41], [48, 42], [49, 42], [46, 43], [50, 41]]) put(out, x, Y(y), P.CR2);
  // gold plaques on the jade belt
  for (let x = 35; x <= 60; x += 5) put(out, x, Y(51 + (x - 32) * 0.13), P.G2);
  // the chain: iron links from the hand to the lantern, sagging a little
  const [hx, hy] = p.hand, [lx, ly] = p.lantern;
  const n = Math.ceil(Math.hypot(lx - hx, ly - hy));
  for (let i = 0; i <= n; i++) {
    const t = i / n, x = lerp(hx, lx, t) + Math.sin(t * Math.PI) * 1.5 * (1 - p.raise), y = lerp(hy, ly, t) + Math.sin(t * Math.PI) * 1.5 * (1 - p.raise);
    put(out, x, y, i % 2 ? P.SL1 : P.SL2);
  }
  // the lantern's soul fire through its paper, and its light round it
  const fire = K.blank(out);
  for (let y = 2; y <= 8; y++) for (let x = -2; x <= 2; x++) {
    const d = Math.hypot(x / 2.5, (y - 5) / 3.5);
    if (d > 1) continue;
    put(fire, lx + x, ly + y, d < 0.35 ? P.W : d < 0.7 ? P.A4 : P.A3);
  }
  const glow = K.glowAround(fire, hot ? 4 : 2, P.A2, P.A1, f);
  D.over(out, glow);
  // the skirt thinning into azure mist below the knee, wisps curling off it
  for (let y = Y(60); y < Y(96); y++) for (let x = 16; x < 70; x++) {
    const c = D.get(out, x, y);
    if (!c || x >= p.hand[0] - 4 || Math.hypot(x - lx, y - ly - 5) < 8) continue;      // not the arm, the chain or the lantern
    const k = (y - Y(60)) / 30;
    if (bayer(x + f, y) < k * 0.9 - 0.2) { put(out, x, y, null); out.data[((y + out.oy) * out.w + x + out.ox) * 4 + 3] = 0; continue; }
    if (k > 0.25 && bayer(x, y + f) < k) put(out, x, y, k > 0.6 ? P.A3 : P.A2);
  }
  const r = D.rng(40);
  for (let k = 0; k < 12; k++) {
    const t = ((f / 6) + r()) % 1, x = 30 + r() * 30 - t * 10, y = Y(70) + t * 20;
    if (bayer(Math.round(x), Math.round(y)) < t) continue;
    put(out, x, y, t < 0.5 ? P.A4 : P.A2);
  }
  return out;
}

const HORSE_RAMPS = [FACE, TEAL, PAPER, GOLD, IRON, JADE, HAIR];
function horses(kind, n) {
  const lit = light(Array.from({ length: n }, (_, f) => horseFlat(kind, f)), HORSE_RAMPS, { ao: 0, minArea: 14 });
  return lit.map((out, f) => horseLight(out, kind, f));
}

function makeHorse() {
  const float = horses("float", 6), raise = horses("raise", 3);
  // his end: the chain drops, he unravels into azure mist from the hem up, the lantern last
  const base = float[0], death = [];
  for (let f = 0; f < 10; f++) {
    const t = f / 9;
    let im;
    if (f === 0) im = K.silhouette(base, P.A5);
    else {
      im = K.blank(base);
      for (let y = -base.oy; y < base.h - base.oy; y++) for (let x = -base.ox; x < base.w - base.ox; x++) {
        const c = D.get(base, x, y);
        if (!c) continue;
        const v = (1 - (y + 10) / 100) * 0.7 + W.fbm(x / 7, y / 7, 9) * 0.4;
        const d = t * 1.4 - v;
        if (d > 0.08) continue;
        const col = d > 0.03 ? P.W : d > -0.02 ? P.A4 : d > -0.07 ? P.A3 : c;
        put(im, x + (d > -0.07 ? Math.round(Math.sin(y * 0.3 + f) * 2 * t) : 0), y - (d > -0.07 ? Math.round(t * 4) : 0), col);
      }
    }
    motes(im, f, 10, 30, 71, 20, 76, 0, 90, [P.A4, P.A2]);
    death.push(im);
  }
  return { horse_face: float, horse_face_raise: raise, horse_face_death: death };
}

// where each guardian's eye is in his first frame, in that frame's own pixels (for his statue)
function eyes(bullFrame0, horseFrame0) {
  const hd = bullHead(bullPose("march", 0)), e = hd.at(9.5, 12);
  return {
    ox: [Math.round(e[0]) + (bullFrame0.ox | 0), Math.round(e[1]) + (bullFrame0.oy | 0)],
    horse: [38 + 13 + (horseFrame0.ox | 0), 2 + 10 + horsePose("float", 0).bob + (horseFrame0.oy | 0)],
  };
}

module.exports = { eyes, makeHorse, horsePose, makeBull, bullFlat, bulls, S, HIDE, HORN, STEEL, FACE, TEAL, JADE };
