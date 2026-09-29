// Meng Po, the Lady of Forgetting, Huangquan Road's final boss, at Naihe Bridge over the river of
// forgetting. she faces the player, as Yama does. made like the guardians (flat, outlined, lit by
// ../restyle.js, then her light laid on):
//   mengpo      128x128, 14 frames: her old form 0-6, her true form 7-13; in each, idle 0-3 and
//               cast 4-6 (4 the ladle dipped in the soup, 5 raised high and pouring, 6 the bowl
//               flung forward)
//     the old form: a kindly old woman, white hair in a bun with a jade pin and a lotus, eyes shut
//     in a smile, lavender robes, stirring a bronze ding cauldron of glowing violet soup of
//     forgetting with a long ladle, a white porcelain bowl in her other hand
//     the true form: young and terrible, long white hair loose and streaming upward as if under
//     water, a lotus crown, eyes open and blazing azure, robes of pale azure and jade, enthroned on
//     a great lotus instead of the cauldron, the ladle become a jade sceptre
//   portrait, portrait_true   the spell card cut-ins, 112x112: head and shoulders, twice the size
const K = require("./kit");
const { P, D, W, K2, put, bayer, img, TAU, lerp, M, blob, stroke, pick, glowAround, C, inked } = K;
const MB = require("./mobs");
const { light, area, dot, HAIR, PAPER, GOLD, IRON } = MB;
const { hex } = D;

const S = 128, CX = 64;
const Q = Object.assign({}, P, {
  // her face
  SK0: hex("#6a4c5a"), SK1: hex("#a07884"), SK2: hex("#d4aeb0"), SK3: hex("#f2d8d2"),
  // lavender robes (old) and azure-white (true)
  LV0: hex("#2e2440"), LV1: hex("#4e4266"), LV2: hex("#7a6c96"), LV3: hex("#a89cc0"), LV4: hex("#d4ccea"),
  AW0: hex("#34507a"), AW1: hex("#5a82ae"), AW2: hex("#9cc4e6"), AW3: hex("#dcecfa"),
  // the bronze of the cauldron
  BZ0: hex("#3a2412"), BZ1: hex("#6a4420"), BZ2: hex("#9a6a30"), BZ3: hex("#c8984c"),
  // the soup of forgetting
  SP0: hex("#2a1450"), SP1: hex("#4a2a8a"), SP2: hex("#7a5ad0"), SP3: hex("#b49cf4"), SP4: hex("#e8deff"),
  // lotus
  LO0: hex("#8a2c54"), LO1: hex("#c85488"), LO2: hex("#f08cb8"), LO3: hex("#ffd0e4"),
});
const H = c => "#" + c.slice(0, 3).map(v => v.toString(16).padStart(2, "0")).join("");
const R = (...cs) => cs.map(H);
const SKIN = R(Q.SK0, Q.SK1, Q.SK2, Q.SK3);
const ROBE = R(Q.LV1, Q.LV2, Q.LV3, Q.LV4);
const TRUE = R(Q.AW0, Q.AW1, Q.AW2, Q.AW3);
const BRONZE = R(Q.BZ0, Q.BZ1, Q.BZ2, Q.BZ3);
const LOTUS = R(Q.LO0, Q.LO1, Q.LO2, Q.LO3);
const WHITE = R(hex("#8e8aa0"), hex("#c4c0d4"), hex("#ece8f4"), hex("#ffffff"));
const JADEC = R(Q.J1, Q.J2, Q.J3, Q.J4);
const SOUP = [[0.1, Q.SP0], [0.25, Q.SP1], [0.45, Q.SP2], [0.68, Q.SP3], [0.88, Q.SP4]];

// ---------------------------------------------------------------- the pose
// cast: 0 idle (f sways her), 1 the ladle dipped, 2 raised and pouring, 3 the bowl flung
function hands(cast, f) {
  const sway = [0, 1, 0, -1][f % 4];
  switch (cast) {
    case 1: return { lh: [42, 60], lt: [60, 74], rh: [88, 58], bowl: [90, 56], sway };        // ladle's bowl in the soup
    case 2: return { lh: [34, 26], lt: [52, 16], rh: [80, 44], bowl: [74, 40], sway: 0 };     // raised, pouring into the bowl
    case 3: return { lh: [42, 56], lt: [58, 70], rh: [96, 34], bowl: [100, 28], sway: 0 };    // the bowl flung up and out
    default: return { lh: [42, 56], lt: [56 + sway, 72], rh: [88, 60], bowl: [90, 58], sway };
  }
}

// ---------------------------------------------------------------- the old form
function oldFlat(f, cast) {
  const im = K.canvas(S, S), h = hands(cast, f), bob = h.sway ? Math.round(h.sway * 0.5) : 0;
  const Y = y => y + bob;
  // her robe: shoulders and a body behind the cauldron
  area(im, K2.poly(M(S, S), [[44, Y(38)], [56, Y(34)], [72, Y(34)], [84, Y(38)], [90, Y(52)], [88, 76], [40, 76], [38, Y(52)]]), ROBE[1]);
  // the dark inner collar crossing, and a white under-collar edge
  area(im, K2.poly(M(S, S), [[56, Y(34)], [72, Y(34)], [64, Y(48)]]), R(Q.LV0)[0]);
  area(im, stroke(M(S, S), [[57, Y(34)], [64, Y(46)], [71, Y(34)]], 1), WHITE[2]);
  // the sleeves, wide, down to the hands
  const sleeve = (sh, hand, side) => {
    const mid = [(sh[0] + hand[0]) / 2 + side * 6, (sh[1] + hand[1]) / 2 + 2];
    area(im, K2.poly(M(S, S), [sh, [sh[0] + side * 4, sh[1] + 6], [mid[0] + side * 3, mid[1] + 5], [hand[0] + side * 4, hand[1] + 3], [hand[0] - side * 3, hand[1] - 2], [mid[0] - side * 4, mid[1] - 4]]), ROBE[1]);
    area(im, K2.ellipse(M(S, S), hand[0] + side * 1, hand[1], 3.2, 2.6), WHITE[2]);      // the cuff
  };
  sleeve([46, Y(40)], h.lh, -1);
  sleeve([82, Y(40)], h.rh, 1);
  // the neck and the head
  area(im, K2.rect(M(S, S), 61, Y(30), 67, Y(36)), SKIN[2]);
  area(im, K2.ellipse(M(S, S), 64, Y(24), 8, 9.5), SKIN[2]);
  // white hair: swept back over the crown, a bun on top, wisps at the temples
  area(im, K2.poly(M(S, S), [[55, Y(22)], [56, Y(15)], [60, Y(12)], [68, Y(12)], [72, Y(15)], [73, Y(22)], [70, Y(17)], [64, Y(15)], [58, Y(17)]]), WHITE[2]);
  area(im, K2.ellipse(M(S, S), 64, Y(9), 6.5, 5), WHITE[2]);
  area(im, stroke(M(S, S), [[56, Y(20)], [55, Y(27)], [56, Y(31)]], 1), WHITE[2]);
  area(im, stroke(M(S, S), [[72, Y(20)], [73, Y(27)], [72, Y(31)]], 1), WHITE[2]);
  // the jade pin through the bun, and a lotus tucked beside it
  area(im, stroke(M(S, S), [[55, Y(12)], [74, Y(6)]], 1), JADEC[2]);
  area(im, K2.ellipse(M(S, S), 72, Y(12), 2.5, 2.2), LOTUS[2]);
  // the ladle: a long handle of dark wood from her hand to its bronze bowl
  area(im, stroke(M(S, S), [h.lh, h.lt], 1), R(Q.WD1)[0]);
  area(im, K2.ellipse(M(S, S), h.lt[0], h.lt[1], 3, 2.2), BRONZE[2]);
  // the porcelain bowl in her other hand
  const [bx, by] = h.bowl;
  area(im, K2.poly(M(S, S), [[bx - 5, by - 2], [bx + 5, by - 2], [bx + 3, by + 2], [bx - 3, by + 2]]), WHITE[2]);
  // the cauldron in front of her: a bronze ding on three legs, handles up from its rim
  if (cast !== 2 || true) {
    area(im, K2.poly(M(S, S), [[36, 76], [92, 76], [88, 94], [78, 102], [50, 102], [40, 94]]), BRONZE[2]);
    for (const [x0, x1] of [[44, 48], [61, 67], [80, 84]]) area(im, K2.poly(M(S, S), [[x0, 100], [x1, 100], [x1 + 1, 112], [x0 - 1, 112]]), BRONZE[1]);
    area(im, K2.rect(M(S, S), 32, 72, 96, 77), BRONZE[3]);
    for (const x of [30, 92]) { area(im, K2.rect(M(S, S), x, 60, x + 5, 73), BRONZE[2]); area(im, K2.rect(M(S, S), x + 1, 62, x + 4, 66), R(Q.BZ0)[0]); }
    // the soup's surface
    area(im, K2.ellipse(M(S, S), 64, 74, 29, 3.5), R(Q.SP1)[0]);
  }
  return inked(im);
}

function oldLight(out, f, cast) {
  const h = hands(cast, f), bob = h.sway ? Math.round(h.sway * 0.5) : 0, Y = y => y + bob;
  // her face: eyes shut in a smile (open and azure as she casts), the lines of her years, a
  // little rouge
  const open = cast > 0;
  for (const ex of [60, 68]) {
    if (open) { put(out, ex - 1, Y(23), Q.A3); put(out, ex, Y(23), Q.A5); put(out, ex + 1, Y(23), Q.A3); put(out, ex, Y(22), Q.S0); }
    else { put(out, ex - 1, Y(23), Q.S1); put(out, ex, Y(24), Q.S1); put(out, ex + 1, Y(23), Q.S1); }
    put(out, ex + (ex < 64 ? -2 : 2), Y(25), Q.SK1);
  }
  put(out, 64, Y(26), Q.SK1); put(out, 63, Y(29), Q.SK0); put(out, 64, Y(29), Q.SK0); put(out, 65, Y(29), Q.SK0);
  put(out, 62, Y(28), Q.SK1); put(out, 66, Y(28), Q.SK1);
  put(out, 59, Y(27), Q.LO2); put(out, 69, Y(27), Q.LO2);
  // folds down her robe, and prayer beads of dark red wood across her chest
  for (const [x0, x1] of [[50, 47], [78, 81], [58, 56], [70, 72]]) for (let y = Y(44); y <= 70; y++) {
    const x = lerp(x0, x1, (y - Y(44)) / (70 - Y(44)));
    const c = D.get(out, x, y);
    if (c && c[2] > 110 && c[2] < 200 && bayer(Math.round(x), y) < 0.75) put(out, x, y, Q.LV1);
  }
  for (let k = 0; k <= 12; k++) {
    const t = k / 12, x = lerp(52, 76, t), y = Y(38) + Math.sin(t * Math.PI) * 9;
    put(out, x, y, k % 2 ? Q.R1 : Q.R2);
    if (k === 6) { put(out, x, y + 1, Q.G2); put(out, x, y + 2, Q.R2); put(out, x, y + 3, Q.R1); }
  }
  // the pin's bead, the lotus's heart
  put(out, 74, Y(7), Q.J4); put(out, 75, Y(8), Q.J3); put(out, 72, Y(12), Q.LO3);
  // the soup: violet light boiling, bubbles bursting, lighting the rim and her from below
  const F = D.field(out.w, out.h), ox = out.ox, oy = out.oy;
  D.each(F, (x, y) => {
    const dx = (x - ox - 64) / 28, dy = (y - oy - 74) / 3.2, d = dx * dx + dy * dy;
    return d < 1 ? 0.55 + 0.35 * W.fbm((x - ox) / 5 + f, (y - oy) / 2, 7) + (d < 0.3 ? 0.2 : 0) : 0;
  });
  D.over(out, D.shade(img(out.w, out.h), F, SOUP, { fadeBand: 0.4 }));
  const r = D.rng(11 + f);
  for (let k = 0; k < 6; k++) { const x = 40 + r() * 48, y = 73 + r() * 2; W.twinkle(out, x, y, (k + f) % 3 === 0 ? 1 : 0, [Q.SP4, Q.SP3]); }
  // the taotie band worked round the cauldron
  for (let x = 42; x <= 86; x += 6) { put(out, x, 84, Q.BZ0); put(out, x + 1, 83, Q.BZ0); put(out, x + 2, 84, Q.BZ0); put(out, x + 1, 86, Q.BZ3); }
  // steam curling up off the soup
  for (let k = 0; k < 14; k++) {
    const t = ((f / 4) + k / 14) % 1, x = 40 + (k * 37 % 48) + Math.sin(t * 6 + k) * 3, y = 70 - t * 34;
    if (bayer(Math.round(x), Math.round(y)) < t * 0.9) continue;
    put(out, x, y, t < 0.4 ? Q.LV4 : Q.LV3);
    if (t < 0.5) put(out, x + 1, y, Q.LV3);
  }
  // the ladle dripping, or pouring a stream into the bowl
  if (cast === 1) for (let k = 1; k <= 3; k++) put(out, h.lt[0], h.lt[1] + 2 + k * 2, Q.SP3);
  if (cast === 2) {
    const [x0, y0] = h.lt, [x1, y1] = h.bowl;
    for (let k = 0; k <= 14; k++) { const t = k / 14; put(out, lerp(x0, x1, t), lerp(y0, y1 - 2, t) + Math.sin(t * Math.PI) * -3, t < 0.3 ? Q.SP4 : Q.SP3); }
  }
  // the soup in her bowl
  const [bx, by] = h.bowl;
  for (let x = bx - 3; x <= bx + 3; x++) put(out, x, by - 2, Q.SP3);
  if (cast === 3) for (let k = 0; k < 8; k++) { const a = -0.6 - k * 0.2; put(out, bx + Math.cos(a) * (6 + k), by + Math.sin(a) * (6 + k) + k * 0.5, k % 2 ? Q.SP3 : Q.SP4); }
  return out;
}

// ---------------------------------------------------------------- the true form
// her hands and the bowl: cast 0 the bowl held at her heart, 1 arms flung wide and the bowl
// floating over her, 2 the bowl raised over her head, 3 one hand sweeping out, the bowl tipped
function trueHands(cast) {
  switch (cast) {
    case 1: return { lh: [30, 46], rh: [98, 46], bowl: [64, 4] };
    case 2: return { lh: [56, 12], rh: [72, 12], bowl: [64, 6] };
    case 3: return { lh: [56, 52], rh: [100, 60], bowl: [104, 56] };
    default: return { lh: [58, 52], rh: [70, 52], bowl: [64, 49] };
  }
}

function trueFlat(f, cast) {
  const im = K.canvas(S, S), h = trueHands(cast), ph = f / 4 * TAU;
  // her hair: a great fall of white behind her, floating out as if under water, rippling
  const hairPts = [[54, 12], [74, 12]];
  const right = [], left = [];
  for (let i = 0; i <= 10; i++) {
    const t = i / 10, y = 14 + t * 74, spread = 12 + t * 26 + Math.sin(t * 5 + ph) * 3 * t;
    right.push([64 + spread + Math.sin(t * 7 + ph) * 2, y]);
    left.push([64 - spread - Math.sin(t * 7 + ph + 1) * 2, y]);
  }
  area(im, K2.poly(M(S, S), [...hairPts, ...right, ...left.reverse()]), WHITE[1]);
  // the lotus throne: an outer ring of petals opening up, an inner one, dark petals turned down
  for (let k = 0; k < 7; k++) {
    const a = Math.PI * (0.18 + 0.64 * k / 6), cx = 64, cy = 98;
    const petal = (len, wide, col, lift) => {
      const tip = [cx + Math.cos(a) * len, cy + Math.sin(a) * len * 0.6];
      const n = [-Math.sin(a) * wide, Math.cos(a) * wide * 0.6];
      area(im, K2.poly(M(S, S), [[cx + n[0] * 0.3, cy + n[1] * 0.3], [cx + Math.cos(a) * len * 0.55 + n[0], cy + Math.sin(a) * len * 0.35 + n[1] - lift],
        tip, [cx + Math.cos(a) * len * 0.55 - n[0], cy + Math.sin(a) * len * 0.35 - n[1] - lift], [cx - n[0] * 0.3, cy - n[1] * 0.3]]), col);
    };
    petal(12, 4, LOTUS[0], 0);                                 // turned down, dark
  }
  // her gown: a close bodice, the skirt falling into the lotus
  area(im, K2.poly(M(S, S), [[52, 36], [58, 32], [70, 32], [76, 36], [74, 58], [82, 92], [46, 92], [54, 58]]), TRUE[2]);
  area(im, K2.poly(M(S, S), [[58, 32], [70, 32], [64, 44]]), TRUE[0]);
  area(im, K2.rect(M(S, S), 53, 57, 75, 60), JADEC[1]);
  // the head and its lotus crown
  area(im, K2.rect(M(S, S), 61, 28, 67, 34), SKIN[2]);
  area(im, K2.ellipse(M(S, S), 64, 22, 7.5, 9), SKIN[2]);
  area(im, K2.poly(M(S, S), [[56, 20], [57, 13], [62, 10], [66, 10], [71, 13], [72, 20], [69, 15], [59, 15]]), WHITE[2]);
  for (let k = -2; k <= 2; k++) area(im, K2.poly(M(S, S), [[64 + k * 4 - 2, 11], [64 + k * 4 + 2, 11], [64 + k * 4.5, 4 - (2 - Math.abs(k)) * 1.5]]), LOTUS[2]);
  const out = inked(im);
  // her long water sleeves over the rest: from the shoulders to the hands, then streaming on
  const fg = K.canvas(S, S);
  const sleeve = (sh, hand, side) => {
    area(fg, stroke(M(S, S), [sh, [(sh[0] + hand[0]) / 2 + side * 3, (sh[1] + hand[1]) / 2 + 2], hand], 5), TRUE[2]);
    const pts = [];
    for (let i = 0; i <= 8; i++) {
      const t = i / 8;
      pts.push([hand[0] + side * (4 + t * 10) + Math.sin(t * 5 + ph + side) * 3 * t, hand[1] + 2 + t * 34]);
    }
    area(fg, stroke(M(S, S), pts, 3), WHITE[2]);
    area(fg, K2.ellipse(M(S, S), hand[0], hand[1], 2.2, 2.2), SKIN[2]);
  };
  sleeve([54, 38], h.lh, -1);
  sleeve([74, 38], h.rh, 1);
  // the bowl
  const [bx, by] = h.bowl;
  area(fg, K2.poly(M(S, S), [[bx - 6, by - 2], [bx + 6, by - 2], [bx + 4, by + 3], [bx - 4, by + 3]]), WHITE[2]);
  // the lotus she's enthroned in, in front of her skirt: an outer ring of petals opening up and
  // out, an inner ring paler between them
  const lotus = K.canvas(S, S);
  const petal = (a, len, wide, col) => {
    const cx = 64, cy = 100;
    const tip = [cx + Math.cos(a) * len, cy + Math.sin(a) * len * 0.62];
    const n = [-Math.sin(a) * wide, Math.cos(a) * wide];
    area(lotus, K2.poly(M(S, S), [[cx + n[0] * 0.2, cy], [cx + Math.cos(a) * len * 0.5 + n[0], cy + Math.sin(a) * len * 0.31 + n[1] * 0.6], tip,
      [cx + Math.cos(a) * len * 0.5 - n[0], cy + Math.sin(a) * len * 0.31 - n[1] * 0.6], [cx - n[0] * 0.2, cy]]), col);
  };
  for (let k = 0; k < 9; k++) petal(-Math.PI * (0.04 + 0.92 * k / 8), 34, 7, LOTUS[2]);
  for (let k = 0; k < 6; k++) petal(-Math.PI * (0.2 + 0.6 * (k + 0.5) / 6), 22, 6, LOTUS[3]);
  return D.over(D.over(out, inked(fg)), inked(lotus));
}

function trueLight(out, f, cast) {
  const h = trueHands(cast);
  // eyes open and blazing, a mark on her brow, a thin red mouth
  for (const ex of [60, 68]) { put(out, ex - 1, 21, Q.A4); put(out, ex, 21, Q.W); put(out, ex + 1, 21, Q.A4); put(out, ex, 20, Q.A3); }
  put(out, 64, 15, Q.A4); put(out, 64, 16, Q.A3);
  put(out, 63, 26, Q.LO1); put(out, 64, 26, Q.LO1); put(out, 65, 26, Q.LO1);
  // strands of her hair, and its ends lit azure
  for (let k = 0; k < 7; k++) {
    const x0 = 44 + k * 7;
    for (let y = 30; y < 88; y += 1) {
      const x = x0 + (x0 - 64) * (y - 30) / 60 + Math.sin(y * 0.18 + k + f * 1.5) * 1.5;
      const c = D.get(out, x, y);
      if (c && c[2] > 180 && c[0] > 150 && bayer(Math.round(x), y) < 0.7) put(out, x, y, y > 70 ? Q.A3 : Q.LV3);
    }
  }
  // gold at the lotus's heart, its petals' tips blushing deeper
  for (let x = 56; x <= 72; x++) if (bayer(x, 99) < 0.6) put(out, x, 99 + ((x + f) % 2), Q.G3);
  // petals and motes drifting up round her
  for (let k = 0; k < 18; k++) {
    const t = ((f / 4) + k / 18) % 1, x = 26 + (k * 29 % 78) + Math.sin(t * 5 + k) * 4, y = 110 - t * 104;
    if (bayer(Math.round(x), Math.round(y)) < t * 0.8) continue;
    put(out, x, y, k % 3 === 0 ? Q.LO2 : k % 3 === 1 ? Q.A4 : Q.J4);
  }
  // the soup in the bowl, blazing
  const g = K.blank(out), [bx, by] = h.bowl;
  for (let x = bx - 4; x <= bx + 4; x++) { put(g, x, by - 2, Q.SP4); put(g, x, by - 3, Q.SP3); }
  put(g, bx, by - 4, Q.W);
  if (cast === 3) for (let k = 0; k < 10; k++) put(g, bx + 2 + k * 0.6, by + 2 + k * 1.4, k % 2 ? Q.SP3 : Q.SP4);
  D.over(out, glowAround(g, cast ? 4 : 3, Q.SP2, Q.SP1, f));
  return out;
}

// ---------------------------------------------------------------- all of her
function makeMengPo() {
  const plan = [];
  for (const form of ["old", "true"]) {
    for (let f = 0; f < 4; f++) plan.push({ form, f, cast: 0 });
    for (let c = 1; c <= 3; c++) plan.push({ form, f: 0, cast: c });
  }
  const oldPlan = plan.filter(p => p.form === "old"), truePlan = plan.filter(p => p.form === "true");
  const oldLit = light(oldPlan.map(p => oldFlat(p.f, p.cast)), [SKIN, ROBE, WHITE, BRONZE, LOTUS, JADEC, R(Q.LV0), R(Q.SP1), R(Q.WD1)], { ao: 0, minArea: 16 });
  const trueLit = light(truePlan.map(p => trueFlat(p.f, p.cast)), [SKIN, TRUE, WHITE, LOTUS, JADEC], { ao: 0, minArea: 16, rim: "#9cc4e6" });
  const frames = [
    ...oldLit.map((im, i) => oldLight(im, oldPlan[i].f, oldPlan[i].cast)),
    ...trueLit.map((im, i) => trueLight(im, truePlan[i].f, truePlan[i].cast)),
  ];
  const portrait = form => {
    const src = frames[form === "old" ? 0 : 7], out = img(112, 112);
    for (let y = 0; y < 56; y++) for (let x = 0; x < 56; x++) {
      const c = D.get(src, 36 + x, -2 + y);
      if (!c) continue;
      for (let v = 0; v < 2; v++) for (let u = 0; u < 2; u++) put(out, x * 2 + u, y * 2 + v, c);
    }
    return out;
  };
  return { mengpo: frames, portrait: [portrait("old")], portrait_true: [portrait("true")] };
}

module.exports = { makeMengPo, Q };
