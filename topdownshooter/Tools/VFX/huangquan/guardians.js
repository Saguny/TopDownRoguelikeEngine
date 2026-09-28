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

// the head's own frame: u along the face from the poll down to the muzzle, v across it (down
// and back); the face held low at 35 degrees, nodding with the pose
function headOf(p) {
  const { T } = bullFrame(p);
  const poll = T([66, 24]), th = 0.62 + p.nod + p.lean;
  const a = [Math.cos(th), Math.sin(th)], b = [-Math.sin(th), Math.cos(th)];
  return (u, v) => [poll[0] + 4 + u * a[0] + v * b[0], poll[1] + 2 + u * a[1] + v * b[1]];
}

// a leg from the hip to a foot, the knee bent forward between them
function leg(hip, foot, thick, knee = 1) {
  const mx = (hip[0] + foot[0]) / 2, my = (hip[1] + foot[1]) / 2;
  const len = Math.hypot(foot[0] - hip[0], foot[1] - hip[1]);
  const bend = Math.max(0, 32 - len) * 0.6 + 3;
  return [hip, [mx + bend * knee, my], foot];
}

function bullFlat(kind, f) {
  const im = img(S, S), p = bullPose(kind, f), { T, Hd, hx, hy } = bullFrame(p);
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
  const grip = T([34, 50]);
  const up = p.tridentBack ? [-0.93, -0.36] : [0.22, -0.97];
  const top = [grip[0] + up[0] * 44, grip[1] + up[1] * 44], butt = [grip[0] - up[0] * 38, grip[1] - up[1] * 38];
  area(im, stroke(M(S, S), [butt, top], 2), WOOD[1]);
  const side = [-up[1], up[0]];
  const at = (a, b) => [top[0] + up[0] * a + side[0] * b, top[1] + up[1] * a + side[1] * b];
  area(im, stroke(M(S, S), [at(0, -5), at(0, 5)], 2), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, 0), at(12, 0)], 2), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, -5), at(7, -6), at(10, -5)], 1), STEEL[2]);
  area(im, stroke(M(S, S), [at(0, 5), at(7, 6), at(10, 5)], 1), STEEL[2]);
  area(im, stroke(M(S, S), [at(-2, -1), at(-4, 0), at(-2, 1)], 1), ROBE[2]);    // a red tassel

  area(im, stroke(M(S, S), [T([44, 34]), T([36, 44]), grip], 7), HIDE[1]);
  area(im, K2.ellipse(M(S, S), grip[0], grip[1], 3.5, 3.5), HIDE[2]);

  // ---- the torso: a barrel of a chest under a shoulder hump
  area(im, poly([[30, 42], [34, 30], [46, 23], [60, 24], [70, 32], [72, 44], [66, 54], [60, 61], [38, 62], [31, 54]]), HIDE[2]);
  // chest and belly muscle, drawn in the darker hide
  for (const q of [[[58, 38], [64, 44]], [[50, 46], [62, 46]], [[52, 51], [60, 51]], [[46, 32], [58, 36]]]) area(im, stroke(M(S, S), q.map(T), 1), HIDE[1]);

  // ---- the war skirt: red, iron plates over it, gold studs; an iron belt with a skull buckle
  const skirt = poly([[36, 56], [62, 56], [66, 74], [58, 76], [48, 73], [40, 76], [32, 72]]);
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

  // ---- the head, on a bull's thick neck
  // a bull's neck, thick as his chest, carrying the head low
  const H = headOf(p), hp = pts => K2.poly(M(S, S), pts.map(([u, v]) => H(u, v)));
  area(im, K2.poly(M(S, S), [T([50, 24]), T([62, 20]), H(2, -6), H(8, -6), H(6, 9), H(-2, 11), T([60, 46]), T([52, 38])]), HIDE[2]);
  // the skull: a straight face angled down to a square muzzle, the jaw heavy under it
  area(im, hp([[-3, -5], [6, -6], [14, -6], [19, -5], [22, -3], [23, 1], [22, 5], [18, 7], [12, 7], [6, 9], [0, 10], [-4, 6], [-4, -1]]), HIDE[2]);
  area(im, hp([[15, -6], [19, -5], [22, -3], [23, 1], [22, 5], [18, 7], [15, 6], [14, 0]]), HIDE[3]);
  area(im, stroke(M(S, S), [H(14, 6), H(18, 5), H(22, 4)], 1), HIDE[1]);            // the mouth
  area(im, stroke(M(S, S), [H(1, -7), H(5, -6), H(9, -5)], 2), HIDE[1]);            // the brow
  // the ear, flicked out behind
  area(im, hp([[0, -3], [-8, -6], [-9, -3], [-1, 0]]), HIDE[1]);
  // horns out of the poll: the far one behind, darker; the near one sweeping up and forward
  const poll = H(1, -6), hw = ([x, y]) => [poll[0] + x, poll[1] + y];
  const hornF = [[-4, 1], [-8, -4], [-8, -9], [-4, -12], [0, -12]].map(hw);
  area(im, stroke(M(S, S), hornF.slice(0, 3), 3), HORN[1]);
  area(im, stroke(M(S, S), hornF.slice(2), 1), HORN[1]);
  const hornN = [[0, 0], [-3, -5], [-2, -10], [3, -13], [9, -13], [12, -10]].map(hw);
  area(im, stroke(M(S, S), hornN.slice(0, 3), 4), HORN[2]);
  area(im, stroke(M(S, S), hornN.slice(2, 5), 2), HORN[2]);
  area(im, stroke(M(S, S), hornN.slice(4), 1), HORN[3]);
  // a bronze band round the horn's root
  area(im, stroke(M(S, S), [hw([-4, -3]), hw([1, -4])], 1), GOLD[1]);
  // a shock of black mane on the poll, falling onto the neck
  area(im, K2.poly(M(S, S), [hw([-6, 2]), hw([-5, -3]), hw([-2, 0]), hw([-1, -4]), hw([2, 1]), H(-2, 4)]), HAIR[1]);

  // ---- the near arm, huge, over everything, and the pauldron on its shoulder
  const out = inked(im);
  const fg = img(S, S);
  const sh = T([60, 34]), el = T([68 + p.arm * 0.3, 48]), fist = T([70 + p.arm, 60]);
  area(fg, stroke(M(S, S), [sh, el, fist], 8), HIDE[2]);
  area(fg, K2.poly(M(S, S), [T([63, 50]), T([71, 50]), T([72 + p.arm, 57]), T([64 + p.arm, 57])]), IRON[2]);   // the bracer
  area(fg, K2.ellipse(M(S, S), fist[0], fist[1] + 1, 4.5, 4), HIDE[3]);
  // the pauldron: three lames of black iron stepping down the shoulder, a spike off the top one
  area(fg, K2.poly(M(S, S), [T([51, 28]), T([57, 23]), T([65, 24]), T([69, 30]), T([66, 32]), T([52, 32])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([51, 32]), T([67, 32]), T([69, 36]), T([52, 37])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([53, 37]), T([69, 36]), T([69, 40]), T([55, 41])]), IRON[2]);
  area(fg, K2.poly(M(S, S), [T([55, 24]), T([59, 23]), T([54, 17])]), IRON[3]);
  return D.over(out, inked(fg));
}

// his light: the eye, the steel and gold catching, the nose ring, and his breath
function bullLight(out, kind, f) {
  const p = bullPose(kind, f), { T, Hd } = bullFrame(p);
  const H = headOf(p), eye = H(6, -3);
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
  const nose = H(21, -2);
  put(out, nose[0], nose[1], P.S0); put(out, nose[0], nose[1] + 1, P.S0); put(out, nose[0] - 1, nose[1] + 1, P.S0);
  const ring = H(22, 2);
  for (let a = 0; a < 8; a++) put(out, ring[0] + Math.round(Math.cos(a / 8 * TAU) * 2.2), ring[1] + Math.round(Math.sin(a / 8 * TAU) * 2), a < 4 ? P.G2 : P.G3);
  // gold studs on the skirt's plates, rivets on the pauldron
  for (const q of [[40, 62], [50, 62], [60, 62], [40, 67], [50, 67], [60, 67]]) { const s = T(q); put(out, s[0], s[1], P.G2); }
  for (const q of [[53, 31], [57, 31], [61, 31], [65, 31], [55, 36], [60, 36], [65, 35]]) { const s = T(q); put(out, s[0], s[1], P.G2); }
  // the skull buckle
  const b = T([52, 56]);
  put(out, b[0] - 1, b[1], P.S0); put(out, b[0] + 1, b[1], P.S0); put(out, b[0], b[1] + 2, P.S0);
  // hell runes cut into his hide, glowing: down the arm and a seal on the chest, flaring as he
  // readies a charge
  const hot = kind === "telegraph" || kind === "charge";
  const pulse = hot ? 2 : [0, 1, 1, 0, 0, 1][f % 6];
  const runeCol = [P.R1, P.R2, P.R3][pulse] || P.R3, runeHot = hot ? P.G3 : P.R3;
  const arm = [[61, 38], [63, 42], [65, 41], [66, 45], [68, 44]].map(T);
  arm.forEach(([x, y], k) => put(out, x, y, k % 2 ? runeHot : runeCol));
  const seal = T([52, 42]);
  for (let a = 0; a < 10; a++) put(out, seal[0] + Math.round(Math.cos(a / 10 * TAU) * 3), seal[1] + Math.round(Math.sin(a / 10 * TAU) * 3), a % 3 ? runeCol : runeHot);
  put(out, seal[0], seal[1], runeHot); put(out, seal[0] - 1, seal[1] + 1, runeCol); put(out, seal[0] + 1, seal[1] - 1, runeCol);
  if (hot) {
    // their heat bleeding out round him
    const glow = img(S, S);
    for (const [x, y] of [...arm, seal]) put(glow, x, y, P.R2);
    D.over(out, K.glowAround(glow, 2, P.R1, P.R0, f), );
  }
  // a chain wrapped round the forearm, its loose end swinging
  const fist = T([70 + p.arm, 60]), sw = Math.sin(f / (kind === "march" ? 6 : 4) * TAU) * 3 - (kind === "charge" ? 8 : 0);
  for (let k = 0; k < 4; k++) { const q = T([64 + k * 2 + p.arm * 0.5, 52 + k]); put(out, q[0], q[1], k % 2 ? STEEL_C[3] : STEEL_C[2]); }
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
    const c = H(-2, -16);
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
  // down on a knee, then embers and red smoke out of every crack
  const death = [];
  const base = telegraph[0];
  for (let f = 0; f < 10; f++) {
    const t = f / 9;
    let im;
    if (f < 2) im = f === 0 ? K.silhouette(base, P.W) : K.silhouette(base, P.R3);
    else {
      im = img(S, S);
      const sink = Math.min(1, (t - 0.2) * 2) * 10;
      for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
        const c = D.get(base, x, y);
        if (!c) continue;
        const burn = t * 1.7 - W.fbm(x / 7, y / 7, 5) - (1 - y / S) * 0.3;
        if (burn > 0.55) continue;
        const col = burn > 0.35 ? P.G3 : burn > 0.2 ? P.O2 : burn > 0.05 ? P.R2 : c;
        put(im, x, y + sink * (y / S), col);
      }
    }
    // embers and smoke rising off him
    const r = D.rng(300);
    for (let k = 0; k < 40; k++) {
      const x = 24 + r() * 60, ph = r(), sp = 0.5 + r();
      const y = 88 - ((t * sp + ph) % 1) * 80 * t;
      if (t < 0.15 || bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.6) * 2) continue;
      put(im, x + Math.sin(y * 0.2 + k) * 2, y, k % 3 === 0 ? P.G3 : k % 3 === 1 ? P.O2 : P.R2);
    }
    death.push(im);
  }
  return { bull_head: march, bull_head_telegraph: telegraph, bull_head_charge: charge, bull_head_stun: stun, bull_head_death: death };
}

module.exports = { makeBull, bullFlat, bulls, S, HIDE, HORN, STEEL, FACE, TEAL, JADE };
