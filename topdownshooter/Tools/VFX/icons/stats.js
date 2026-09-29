// the stat and passive icons (NewSprites/Sprites/Icons): the character panel's rows, the level up
// passives, in the weapon icons' style (Icons/Weapons, the *_icon art): 32x32, lit from the top
// left, three tones and a highlight a part, one dark plum outline round the whole, unoutlined
// sparkles. each is a thing from the setting rather than a symbol:
//   heart 1          Max Health       a red heart with a gold knot tassel
//   recovery         Recovery         a lingzhi mushroom, green motes of healing rising off it
//   armor            Armor            a lamellar cuirass, steel scales, red trim, gold buckle
//   movespeed        Move Speed       a cloud-toed boot on a running cloud
//   might            Might            a fist in a red wrap, a gold burst behind it
//   cooldownnecklace Cooldown         a bronze censer, three sticks of incense burning down
//   area             Area             a jade orb in a ring of light, arrows spreading out
//   speed            Weapon Speed     a swallow's feather on the wind
//   pierce           Armour Piercing  a spear through a cracked bronze shield
//   piercing         Piercing         an arrow through two jade discs
//   crit             Crit Chance      a bullseye with the arrow dead centre, a glint on it (left)
//                    Crit Damage      a great burst, split with a crack (right)
//   magnet-export    Magnet           a red lacquer horseshoe magnet pulling in copper coins
//   growth           Growth           a jade sprout, gold chevrons rising
//   greedl           Greed            a gold ingot on a string of copper coins
//   revival          Revival          a life lamp: a red lantern, its flame lit
//   reroll           Reroll           the fortune sticks' cup, one stick leaping out
//   skip             Skip             two jade chevrons, wind trailing them
//   banish           Banish           a yellow sealing talisman, a violet ghost fleeing it
// node icons/stats.js writes them over the placeholders (same files, so every reference keeps
// working) and out/stats_preview.png
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const { hex } = D;

const S = 32;
const OUT = hex("#2e1c2c");
const ramp = a => a.map(hex);
const R = {
  red: ramp(["#5a0e22", "#8d1c30", "#c42a36", "#f04a4a", "#ff9a8a"]),
  gold: ramp(["#6e2e1c", "#b8600e", "#dc9b00", "#ffc300", "#fff0a0"]),
  steel: ramp(["#3a3e52", "#5a6278", "#95adb4", "#dfeff2", "#ffffff"]),
  jade: ramp(["#123e3a", "#1e6a58", "#2e9c78", "#5ad2a0", "#c8f8e0"]),
  azure: ramp(["#1a2e6a", "#2a5ab0", "#5ac8f0", "#a0e8ff", "#ffffff"]),
  wood: ramp(["#4a2418", "#6e2e1c", "#8a5424", "#c8883a", "#eab860"]),
  skin: ramp(["#5a2a24", "#8a4a34", "#c8764e", "#eaa070", "#ffd0a0"]),
  violet: ramp(["#2a1440", "#5a2e8a", "#9a5ad8", "#d7a8ff", "#f4e4ff"]),
  fire: ramp(["#8d1c30", "#cb1e31", "#ff6a00", "#ffc300", "#fffbe0"]),
  paper: ramp(["#8a5a1c", "#c8923a", "#f2c860", "#fff0a0", "#ffffff"]),
  indigo: ramp(["#141430", "#26264e", "#3a3a72", "#5a5aa0", "#9090d0"]),
  cloud: ramp(["#5a6278", "#95adb4", "#dfeff2", "#ffffff", "#ffffff"]),
  bronze: ramp(["#3a1e14", "#6e3a1c", "#a0602a", "#d09040", "#f8d068"]),
  copper: ramp(["#4a2418", "#8a4a24", "#c0703a", "#e8a060", "#ffd8a0"]),
  smoke: ramp(["#5a4a6a", "#8a7a9a", "#bcb0cc", "#e4dcee", "#ffffff"]),
};

// ---------------------------------------------------------------- shapes: (x, y) at pixel centres
const circle = (cx, cy, r) => (x, y) => (x - cx) ** 2 + (y - cy) ** 2 <= r * r;
const ellipse = (cx, cy, rx, ry, rot = 0) => (x, y) => {
  const c = Math.cos(rot), s = Math.sin(rot), u = (x - cx) * c + (y - cy) * s, v = -(x - cx) * s + (y - cy) * c;
  return (u / rx) ** 2 + (v / ry) ** 2 <= 1;
};
const rect = (x0, y0, x1, y1) => (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
function poly(pts) {
  return (x, y) => {
    let inside = false;
    for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
      const [xi, yi] = pts[i], [xj, yj] = pts[j];
      if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
    }
    return inside;
  };
}
function segDist(x, y, x0, y0, x1, y1) {
  const dx = x1 - x0, dy = y1 - y0, l = dx * dx + dy * dy;
  const t = l ? Math.max(0, Math.min(1, ((x - x0) * dx + (y - y0) * dy) / l)) : 0;
  return Math.hypot(x - x0 - t * dx, y - y0 - t * dy);
}
const capsule = (x0, y0, x1, y1, r) => (x, y) => segDist(x, y, x0, y0, x1, y1) <= r;
const ring = (cx, cy, r0, r1) => (x, y) => { const d = Math.hypot(x - cx, y - cy); return d >= r0 && d <= r1; };
const and = (...f) => (x, y) => f.every(g => g(x, y));
const or = (...f) => (x, y) => f.some(g => g(x, y));
const not = f => (x, y) => !f(x, y);
// a heart: two lobes and a point
const heart = (cx, cy, s) => (x, y) => {
  const u = (x - cx) / s, v = -(y - cy) / s + 0.2;
  return (u * u + v * v - 1) ** 3 - u * u * v * v * v <= 0;
};

// ---------------------------------------------------------------- the icon: parts, lit and outlined
class Icon {
  constructor(w = S, h = S) {
    this.w = w; this.h = h;
    this.im = D.image(w, h);
    this.M = D.mask(w, h);
    this.owner = new Int16Array(w * h).fill(-1);
    this.n = 0;
  }
  // a part: a shape, a ramp, shaded by its own silhouette (light from the top left), optionally a
  // dark line where it lies over earlier parts (sep), and a tone(x, y, t) hook for its details
  // (return a ramp index, a colour, or undefined to keep the shading)
  part(shape, rp, o = {}) {
    const { w, h } = this, id = this.n++;
    const inside = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (shape(x + 0.5, y + 0.5)) inside[y * w + x] = 1;
    const at = (x, y) => x >= 0 && y >= 0 && x < w && y < h && inside[y * w + x];
    if (o.sep) {
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        if (at(x, y) || this.owner[y * w + x] < 0) continue;
        if (at(x - 1, y) || at(x + 1, y) || at(x, y - 1) || at(x, y + 1)) D.put(this.im, x, y, o.sepColor || OUT);
      }
    }
    // a blurred silhouette: its slope is the part's surface
    const B = (x, y) => {
      let s = 0, n = 0;
      for (let j = -2; j <= 2; j++) for (let i = -2; i <= 2; i++) { s += at(x + i, y + j) ? 1 : 0; n++; }
      return s / n;
    };
    let minY = h, maxY = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (at(x, y)) { minY = Math.min(minY, y); maxY = Math.max(maxY, y); }
    const light = o.light || [-0.7, -0.7];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      if (!at(x, y)) continue;
      const gx = B(x + 1, y) - B(x - 1, y), gy = B(x, y + 1) - B(x, y - 1);
      const lam = -(gx * light[0] + gy * light[1]);      // the outward normal is -grad
      const vert = maxY > minY ? (y - minY) / (maxY - minY) : 0.5;
      let t = 0.5 + lam * 1.4 - (vert - 0.5) * (o.fall ?? 0.35);
      // the rims: bright up and left, dark down and right
      if (!at(x - 1, y) || !at(x, y - 1)) t += 0.22;
      if (!at(x + 1, y) || !at(x, y + 1)) t -= 0.28;
      let k = o.flat != null ? o.flat : t > 0.78 ? 3 : t > 0.36 ? 2 : t > 0.02 ? 1 : 0;
      if (o.min != null) k = Math.max(k, o.min);
      if (o.max != null) k = Math.min(k, o.max);
      let c = rp[k];
      if (o.tone) {
        const r = o.tone(x, y, t, k);
        if (typeof r === "number") c = rp[Math.max(0, Math.min(rp.length - 1, r))];
        else if (r) c = r;
      }
      if (!c) continue;
      D.put(this.im, x, y, c);
      if (!o.noOutline) D.mset(this.M, x, y);
      this.owner[y * w + x] = id;
    }
    return this;
  }
  px(x, y, c) { D.put(this.im, x, y, c); return this; }
  pxs(list, c) { for (const [x, y] of list) D.put(this.im, x, y, c); return this; }
  mark(x, y, c) { D.put(this.im, x, y, c); D.mset(this.M, x, y); return this; }
  // the dark plum outline round everything outlined so far
  outline() {
    const edge = D.minus(D.dilate(this.M, 1, false), this.M);
    D.paint(this.im, edge, OUT);
    return this;
  }
  // a four point sparkle, unoutlined: a white heart, coloured arms
  sparkle(x, y, arm, col, big = false) {
    D.put(this.im, x, y, hex("#ffffff"));
    for (let i = 1; i <= arm; i++) {
      const c = i === 1 && big ? hex("#ffffff") : col;
      D.put(this.im, x + i, y, c); D.put(this.im, x - i, y, c); D.put(this.im, x, y + i, c); D.put(this.im, x, y - i, c);
    }
    return this;
  }
}

const W = hex("#ffffff"), CREAM = hex("#fff0a0");

// ---------------------------------------------------------------- the icons

function maxHealth() {
  const I = new Icon();
  // the tassel under it first, so the heart sits over its cord
  I.part(capsule(16, 25, 16, 27.5, 0.6), R.gold, { max: 3 });
  I.part(poly([[13.5, 28], [18.5, 28], [19.5, 31.5], [12.5, 31.5]]), R.red, { tone: (x, y) => (x % 2 === 0 ? 1 : 2) });
  I.part(rect(13.5, 27, 18.5, 28.5), R.gold, { sep: true });
  I.part(heart(16, 13.5, 11.2), R.red, {
    sep: true,
    tone: (x, y, t, k) => {
      // a gloss up on the left lobe
      const d = Math.hypot(x + 0.5 - 10, y + 0.5 - 8.5);
      if (d < 1.6) return 4;
      if (d < 2.9 && k >= 2) return 3;
    },
  });
  I.outline();
  I.px(9, 8, W);
  I.sparkle(26, 5, 2, R.gold[3]);
  I.sparkle(4, 21, 1, R.red[4]);
  return I.im;
}

function recovery() {
  const I = new Icon();
  // the stalk, curving down
  I.part(or(capsule(15, 17, 13.5, 24, 2), capsule(13.5, 24, 15, 28, 2.1)), R.wood);
  I.part(ellipse(15, 28.5, 5.5, 1.6), R.jade, { max: 2 });   // moss at its foot
  // the cap: a fan of bands, gold at the rim, deep red at the heart
  // the cap: a kidney of bands round where the stalk meets it, gold at the rim, deep red at the heart
  I.part(and(ellipse(15, 14, 12.5, 9.5), not(ellipse(15, 24, 10, 5.5)), not(ellipse(15, 19.5, 2.5, 2))), R.red, {
    sep: true,
    tone: (x, y, t, k) => {
      const d = Math.hypot((x + 0.5 - 15) / 1.35, y + 0.5 - 19);
      const rim = Math.hypot((x + 0.5 - 15) / 12.5, (y + 0.5 - 14) / 9.5);
      if (rim > 0.9) return k >= 2 ? R.gold[3] : R.gold[2];
      if (rim > 0.82) return R.gold[1];
      if ((d > 5.2 && d < 6) || (d > 2.8 && d < 3.5)) return R.red[0];
      if (k >= 3) return 3;
    },
  });
  I.outline();
  // motes of healing rising off it
  const plus = (x, y) => I.pxs([[x, y], [x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]], R.jade[3]).px(x, y, W);
  plus(27, 8); plus(5, 5); I.px(24, 3, R.jade[3]); I.px(29, 14, R.jade[4]); I.px(3, 12, R.jade[3]);
  return I.im;
}

function armor() {
  const I = new Icon();
  // the cuirass: shoulders wide, waist in, a skirt of scales below the belt
  const body = poly([[5, 7], [11, 4], [21, 4], [27, 7], [26, 14], [23, 18], [23, 21], [9, 21], [9, 18], [6, 14]]);
  I.part(body, R.steel, {
    tone: (x, y, t, k) => {
      // scale rows: a dark line every third row, the plates' seams staggered
      if (y % 3 === 0) return Math.max(0, k - 2);
      if ((x + (Math.floor(y / 3) % 2) * 2) % 4 === 0) return Math.max(0, k - 1);
    },
  });
  // the collar and the shoulder guards' red trim
  I.part(poly([[11, 4], [21, 4], [19, 7], [13, 7]]), R.red, { sep: true });
  I.part(capsule(6.5, 8, 10, 5.5, 1.3), R.red, { sep: true });
  I.part(capsule(25.5, 8, 22, 5.5, 1.3), R.red, { sep: true });
  // the skirt
  I.part(poly([[9, 21], [23, 21], [25, 28], [16, 29.5], [7, 28]]), R.steel, {
    sep: true,
    tone: (x, y, t, k) => {
      if (x % 3 === 0) return Math.max(0, k - 2);
      if (y === 27 || y === 28) return R.red[k >= 2 ? 2 : 1];
    },
  });
  // the belt and its gold buckle
  I.part(rect(8.5, 19, 23.5, 21.9), R.red, { sep: true });
  I.part(ellipse(16, 20.5, 2.6, 2.2), R.gold, { sep: true, tone: (x, y) => (x === 16 && y === 20 ? R.red[1] : undefined) });
  I.outline();
  I.sparkle(27, 3, 2, R.azure[3]);
  return I.im;
}

function moveSpeed() {
  const I = new Icon();
  // the cloud it runs on, trailing back
  I.part(or(circle(10, 25, 4), circle(16, 24, 5), circle(22, 25.5, 3.6), ellipse(15, 27, 10, 2.8)), R.cloud, { fall: 0.6 });
  // the boot: a tall black shaft, the toe curling up, a white sole
  const boot = or(
    poly([[11, 3], [19, 3], [19, 15], [25, 16], [28.5, 13], [29, 17], [26, 20.5], [11, 20.5]]),
    circle(13, 18, 3),
  );
  I.part(boot, R.indigo, {
    sep: true,
    tone: (x, y, t, k) => {
      if (y === 5 || y === 6) return R.red[k >= 2 ? 2 : 1];            // the red band round the top
      if (x >= 26 && y <= 15) return R.gold[3];                         // the curled toe's gold cap
    },
  });
  I.part(poly([[11, 20.5], [26.5, 20.5], [25, 22.5], [11, 22.5]]), R.cloud, { sep: true, min: 2 });
  I.outline();
  // speed lines behind
  for (const [x0, y, len] of [[1, 9, 7], [3, 13, 6], [1, 17, 7]]) for (let i = 0; i < len; i++) I.px(x0 + i, y, i < 2 ? R.azure[2] : i < len - 2 ? R.azure[3] : W);
  I.sparkle(29, 6, 1, R.azure[3]);
  return I.im;
}

function might() {
  const I = new Icon();
  // a gold burst behind the fist
  const burst = (x, y) => {
    const a = Math.atan2(y - 12, x - 18), d = Math.hypot(x - 18, y - 12);
    return d < 9.5 + 4 * Math.cos(a * 8) ** 8;
  };
  I.part(burst, R.gold, { fall: 0, noOutline: true, tone: (x, y) => (Math.hypot(x - 18, y - 12) < 7 ? 4 : 3) });
  // the forearm and its red wrap
  I.part(poly([[10, 21], [20, 21], [21, 31], [9, 31]]), R.red, { tone: (x, y, t, k) => ((x + y) % 4 === 0 ? Math.max(0, k - 1) : undefined) });
  // the fist: four knuckles, the thumb folded across
  const fist = or(rect(8, 8.5, 22, 20), circle(9.5, 10.5, 2.4), circle(20.5, 10.5, 2.4), rect(9.5, 7.5, 20.5, 12));
  I.part(fist, R.skin, {
    sep: true,
    tone: (x, y, t, k) => {
      if (y <= 13 && (x === 11 || x === 15 || x === 18)) return Math.max(0, k - 2);   // between the fingers
      if (y === 7 || y === 8) return k >= 2 ? 4 : undefined;                            // knuckle light
    },
  });
  I.part(capsule(9.5, 16, 17, 14.5, 2.2), R.skin, { sep: true, light: [-0.3, -0.9] });
  I.outline();
  I.sparkle(27, 4, 2, CREAM, true);
  I.sparkle(4, 5, 1, R.gold[3]);
  return I.im;
}

function cooldown() {
  const I = new Icon();
  // the smoke first, curling up out of the censer
  for (const [sx, ph, top] of [[12, 0, 4], [16, 2.2, 1], [20, 4.1, 5]]) {
    for (let y = 11; y >= top; y--) {
      const x = Math.round(sx + 1.8 * Math.sin((11 - y) * 0.62 + ph) * Math.min(1, (12 - y) / 4));
      if (y < top + 3 && D.bayer(x, y) < 0.5) continue;       // thinning out at the top
      I.px(x, y, y > 8 ? R.smoke[2] : y > 5 ? R.smoke[3] : R.smoke[4]);
    }
  }
  // three sticks, their tips glowing
  for (const sx of [12, 16, 20]) {
    I.part(rect(sx - 0.4, 13, sx + 0.5, 20), R.red, { flat: 1 });
    I.px(sx, 13, R.fire[3]); I.px(sx, 12, R.fire[4]);
  }
  // the censer: a round bronze belly, two ears, three legs
  I.part(or(capsule(5.5, 29, 7.5, 25, 1.1), capsule(26.5, 29, 24.5, 25, 1.1), capsule(16, 29.5, 16, 25, 1.1)), R.bronze);
  I.part(or(capsule(5.5, 17, 5.5, 21, 1.4), capsule(26.5, 17, 26.5, 21, 1.4)), R.bronze, { sep: true });
  I.part(and(ellipse(16, 19.5, 11, 8.5), rect(0, 18.5, 32, 32)), R.bronze, {
    sep: true,
    tone: (x, y, t, k) => {
      if (y === 21 && x % 2 === 0) return R.gold[3];            // a band of gold studs
      if (y === 20 || y === 22) return Math.max(0, k - 1);
    },
  });
  I.part(ellipse(16, 18.5, 11, 1.7), R.bronze, { sep: true, min: 3, tone: (x, y) => (y === 19 ? R.bronze[0] : undefined) });
  I.outline();
  I.sparkle(27, 9, 1, R.gold[3]);
  return I.im;
}

function area() {
  const I = new Icon();
  // the ring of light
  I.part(ring(16, 16, 8.5, 11), R.azure, { fall: 0.2, tone: (x, y, t, k) => (k >= 2 && ((x + y) & 3) === 0 ? 4 : undefined) });
  // arrows spreading out along the diagonals
  for (const [dx, dy] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) {
    const tipX = 16 + dx * 14.2, tipY = 16 + dy * 14.2, bx = 16 + dx * 9.8, by = 16 + dy * 9.8;
    const px = -dy * 3.4 / Math.SQRT2 * Math.SQRT2, py = dx * 3.4 / Math.SQRT2 * Math.SQRT2;
    I.part(poly([[tipX, tipY], [bx + px, by + py], [bx - px, by - py]]), R.gold, { sep: true });
  }
  // the jade orb at the heart
  I.part(circle(16, 16, 5.4), R.jade, {
    tone: (x, y) => {
      const d = Math.hypot(x + 0.5 - 14, y + 0.5 - 14);
      if (d < 1.2) return 4;
      if (d < 2.2) return 3;
    },
  });
  I.outline();
  I.px(14, 13, W);
  return I.im;
}

function weaponSpeed() {
  const I = new Icon();
  // wind streaks behind, then the feather along the diagonal: a curved vane, a gold quill
  for (const [x0, y0, len] of [[2, 12, 8], [5, 19, 7], [10, 25, 6]]) for (let i = 0; i < len; i++) I.px(x0 + i, y0 + Math.round(i * -0.5), i < 2 ? R.azure[1] : i < len - 2 ? R.azure[2] : R.azure[3]);
  const spine = t => [8 + t * 19, 27 - t * 22 - Math.sin(t * Math.PI) * 2.5];
  const vane = (x, y) => {
    for (let i = 0; i <= 40; i++) {
      const t = i / 40, [sx, sy] = spine(t);
      if (t < 0.18) continue;
      const wv = 4.6 * Math.sin(Math.min(1, (t - 0.18) / 0.82) * Math.PI * 0.95) + 0.6;
      if (Math.hypot(x - sx, y - sy) < wv) return true;
    }
    return false;
  };
  I.part(vane, R.azure, {
    tone: (x, y, t, k) => {
      // barbs: diagonal seams across the vane, the tip white
      if ((x * 2 + y * 3) % 7 === 0) return Math.max(0, k - 1);
      if (x >= 24 && y <= 8) return k >= 2 ? 4 : 3;
    },
  });
  // the quill down its middle
  const quill = (x, y) => {
    for (let i = 0; i <= 40; i++) { const [sx, sy] = spine(i / 40 * 0.95); if (Math.hypot(x - sx, y - sy) < 0.75) return true; }
    return false;
  };
  I.part(quill, R.gold, { sep: false, min: 2, tone: (x, y) => (y > 23 ? R.gold[1] : undefined) });
  I.outline();
  I.sparkle(27, 16, 1, R.azure[3]);
  I.sparkle(20, 3, 1, W);
  return I.im;
}

function armourPiercing() {
  const I = new Icon();
  const cx = 14, cy = 17, r = 10.5;
  // the shield: a round bronze face, a gold rim, a boss, cracked where the spear broke through
  I.part(circle(cx, cy, r), R.bronze, {
    tone: (x, y, t, k) => {
      const d = Math.hypot(x + 0.5 - cx, y + 0.5 - cy);
      if (d > r - 1.6) return k >= 2 ? R.gold[3] : R.gold[1];
      if (d > r - 4.2 && d < r - 3.2) return Math.max(0, k - 1);
      // the cracks, running out from the breach
      for (const a of [0.4, 1.9, 3.3, 4.6]) {
        const u = Math.cos(a), v = Math.sin(a), along = (x + 0.5 - 19) * u + (y + 0.5 - 12) * v;
        const off = Math.abs(-(x + 0.5 - 19) * v + (y + 0.5 - 12) * u + Math.sin(along * 1.3) * 0.6);
        if (along > 1 && along < 7 && off < 0.55) return R.bronze[0];
      }
    },
  });
  // the spear: its shaft coming from the lower left, the head out through the top right
  I.part(capsule(1.5, 30.5, 17, 15, 1.2), R.wood, { sep: true });
  I.part(poly([[17, 15.5], [19.8, 9.4], [29, 3], [22.6, 12.2], [16.5, 14.5]]), R.steel, { sep: true, tone: (x, y, t, k) => (x - y > 14 ? 4 : undefined) });
  I.part(ellipse(17.5, 14.5, 2.2, 1.2, -0.78), R.red, { sep: true });
  I.outline();
  // the breach's sparks
  I.pxs([[21, 7], [23, 13], [17, 9], [25, 16]], R.gold[3]).px(19, 8, W);
  I.sparkle(28, 9, 1, W);
  return I.im;
}

function piercing() {
  const I = new Icon();
  // the arrow's tail and fletching first; the discs over it; the head out past them
  const disc = (x0, y0) => and(circle(x0, y0, 6), not(circle(x0, y0, 2.2)));
  I.part(capsule(4, 28, 28, 4, 1.2), R.wood, { min: 2 });
  I.part(poly([[3, 25.5], [7.5, 25], [5.5, 28.5]]), R.red, { sep: true });
  I.part(poly([[6.5, 29], [7, 24.5], [3.5, 26.5]]), R.red, { sep: true });
  for (const [x0, y0] of [[11, 21], [20, 12]]) {
    I.part(disc(x0, y0), R.jade, {
      sep: true,
      tone: (x, y, t, k) => {
        const d = Math.hypot(x + 0.5 - x0, y + 0.5 - y0);
        if (d > 3.3 && d < 3.9 && k >= 1) return k - 1;       // the ring carved round it
      },
    });
  }
  // the shaft through the discs' holes, redrawn over them
  I.part(and(capsule(4, 28, 28, 4, 1.2), or(circle(11, 21, 2.2), circle(20, 12, 2.2))), R.wood, { min: 2 });
  I.part(poly([[30.8, 1.2], [22.2, 4.4], [27.6, 9.8]]), R.steel, { sep: true, min: 2, tone: (x, y) => (x - y > 20 ? 4 : undefined) });
  I.outline();
  I.pxs([[28, 11], [16, 7], [7, 15]], R.gold[3]);
  I.sparkle(29, 13, 1, W);
  return I.im;
}

function critChance() {
  const I = new Icon();
  // a red and cream bullseye, an arrow in its heart, a glint off it
  I.part(circle(14, 18, 10.5), R.red, {
    tone: (x, y, t, k) => {
      const d = Math.hypot(x + 0.5 - 14, y + 0.5 - 18);
      if (d > 7.2 && d < 9.4) return R.paper[k >= 2 ? 3 : 2];
      if (d > 3.2 && d < 5.4) return R.paper[k >= 2 ? 3 : 2];
      if (k >= 3) return 3;
    },
  });
  I.part(capsule(14, 18, 24, 8, 0.8), R.wood, { sep: true, min: 2 });
  I.part(or(poly([[23, 5], [27.5, 4.5], [24.5, 9]]), poly([[27, 9], [27.5, 4.5], [23, 7.5]])), R.red, { sep: true, min: 2 });
  I.outline();
  I.px(14, 18, R.steel[3]).px(13, 19, R.steel[2]);
  I.sparkle(6, 7, 2, CREAM, true);
  I.sparkle(27, 16, 1, R.gold[3]);
  return I.im;
}

function critDamage() {
  const I = new Icon();
  // a great burst: a jagged star, red at the edge, white hot at the heart, split by a crack
  const star = (x, y) => {
    const a = Math.atan2(y - 16, x - 16), d = Math.hypot(x - 16, y - 16);
    const spikes = 0.5 + 0.5 * Math.cos(a * 7 + 0.4);
    return d < 6.5 + 7.5 * spikes ** 3 + 1.2 * Math.sin(a * 3);
  };
  I.part(star, R.fire, {
    fall: 0,
    tone: (x, y, t, k) => {
      const d = Math.hypot(x + 0.5 - 16, y + 0.5 - 16);
      if (d < 3) return 4;
      if (d < 5.5) return 3;
      if (d < 8.5) return 2;
      return k >= 2 ? 1 : 0;
    },
  });
  I.outline();
  // the crack across it
  const crack = [[9, 9], [12, 13], [11, 15], [15, 17], [16, 20], [20, 22], [22, 25]];
  for (let i = 1; i < crack.length; i++) {
    const M = D.line(D.mask(S, S), crack[i - 1][0], crack[i - 1][1], crack[i][0], crack[i][1]);
    D.paint(I.im, M, OUT);
  }
  // shards flung off
  I.pxs([[28, 5], [4, 27], [27, 27], [3, 6]], R.fire[3]).pxs([[29, 4], [3, 28]], W);
  return I.im;
}

function magnet() {
  const I = new Icon();
  // copper coins drawn in, a square hole in each
  const coin = (x0, y0) => {
    I.part(circle(x0, y0, 3.3), R.copper, { tone: (x, y) => (Math.abs(x + 0.5 - x0) < 1 && Math.abs(y + 0.5 - y0) < 1 ? OUT : undefined) });
  };
  coin(26, 25); coin(26.5, 16.5);
  // the horseshoe: red lacquer, steel poles pointing right
  const U = and(or(ring(12, 14, 4.2, 10), rect(12, 4, 20, 8.8), rect(12, 19.2, 20, 24)), not(and(circle(12, 14, 4.2), rect(0, 0, 32, 32))), or(rect(0, 0, 12, 32), rect(12, 4, 20, 8.8), rect(12, 19.2, 20, 24)));
  I.part(U, R.red, { sep: true, tone: (x, y, t, k) => (x >= 17 ? R.steel[Math.min(4, k + 1)] : undefined) });
  I.outline();
  // the pull
  for (const [x, y] of [[22, 6], [23, 11], [22, 20]]) I.px(x, y, R.azure[3]);
  I.pxs([[21, 12], [21, 19]], R.azure[2]);
  I.sparkle(28, 5, 1, W);
  return I.im;
}

function growth() {
  const I = new Icon();
  // a mound of earth, a jade sprout out of it: a stem, two leaves, a bud
  I.part(ellipse(15, 28, 10, 3.5), R.wood, { fall: 0.8 });
  I.part(capsule(15, 27, 15.5, 13, 1.1), R.jade, { sep: true, min: 1 });
  I.part(ellipse(9.5, 17.5, 6, 2.6, 0.5), R.jade, { sep: true, tone: (x, y) => (Math.abs((y - 17.5) - (x - 9.5) * 0.5) < 0.6 && x < 14 ? R.jade[1] : undefined) });
  I.part(ellipse(21.5, 14, 6, 2.6, -0.55), R.jade, { sep: true, tone: (x, y) => (Math.abs((y - 14) + (x - 21.5) * 0.6) < 0.6 && x > 16 ? R.jade[1] : undefined) });
  I.part(ellipse(15.5, 10, 2.3, 3.3), R.jade, { sep: true, min: 2, tone: (x, y) => (y <= 8 ? 4 : undefined) });
  I.outline();
  // gold chevrons rising beside it
  for (const [x0, y0] of [[26, 26], [26, 21], [26, 16]]) {
    for (let i = -2; i <= 2; i++) I.px(x0 + i, y0 - (2 - Math.abs(i)), i === 0 ? CREAM : R.gold[3]);
  }
  I.sparkle(5, 6, 2, R.jade[3], true);
  return I.im;
}

function greed() {
  const I = new Icon();
  // a copper coin standing behind, a square hole through it, a red cord
  I.part(circle(22, 9.5, 6.2), R.copper, {
    tone: (x, y, t, k) => {
      const u = Math.abs(x + 0.5 - 22), v = Math.abs(y + 0.5 - 9.5), d = Math.hypot(x + 0.5 - 22, y + 0.5 - 9.5);
      if (u < 1.6 && v < 1.6) return OUT;
      if (d > 4.3 && d < 5) return Math.max(0, k - 1);
    },
  });
  I.part(capsule(22, 9.5, 29, 3, 0.6), R.red, { flat: 2 });
  // the ingot: a boat of gold, its ends curled up high, a round crown rising out of it
  I.part(or(poly([[4, 16], [10, 20], [22, 20], [28, 16], [25.5, 28], [6.5, 28]]), circle(5, 16, 2.6), circle(27, 16, 2.6)), R.gold, { sep: true });
  I.part(ellipse(16, 17.5, 6, 5.5), R.gold, { sep: true, tone: (x, y) => { const d = Math.hypot(x + 0.5 - 14, y + 0.5 - 15); return d < 1.3 ? 4 : d < 2.4 ? 3 : undefined; } });
  I.part(rect(8, 21.2, 24, 21.9), R.gold, { flat: 1 });
  I.outline();
  I.sparkle(6, 7, 2, CREAM, true);
  I.sparkle(12, 3, 1, R.gold[3]);
  return I.im;
}

function revival() {
  const I = new Icon();
  // the glow round the lamp
  I.part(circle(16, 16, 12.5), R.fire, { noOutline: true, fall: 0, tone: (x, y) => ((x + y) % 2 === 0 && Math.hypot(x + 0.5 - 16, y + 0.5 - 16) > 10.5 ? null : undefined), flat: 0, max: 0 });
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const d = Math.hypot(x + 0.5 - 16, y + 0.5 - 16);
    if (d > 10.5 || D.bayer(x, y) > (12.5 - d) / 4) I.im.data[(y * S + x) * 4 + 3] = 0;   // a dithered halo only
  }
  // the lantern: gold caps top and bottom, red paper body with ribs, a flame inside, a tassel
  I.part(capsule(16, 1.5, 16, 4, 0.6), R.gold, { max: 3 });
  I.part(rect(11, 4, 21, 6.5), R.gold, {});
  I.part(ellipse(16, 15, 10, 9), R.red, {
    sep: true,
    tone: (x, y, t, k) => {
      const d = Math.hypot((x + 0.5 - 16) / 1.1, y + 0.5 - 16);
      if (d < 3.5) return R.fire[d < 1.8 ? 4 : 3];               // the flame showing through
      if (d < 5.5) return R.fire[2];
      if (Math.abs(x + 0.5 - 16) > 1 && Math.round(Math.abs(x + 0.5 - 16) * 1.1) % 4 === 0) return Math.max(0, k - 1);
    },
  });
  I.part(rect(11, 23.5, 21, 26), R.gold, { sep: true });
  I.part(poly([[14.5, 26], [17.5, 26], [18.5, 31], [13.5, 31]]), R.red, { tone: (x) => (x % 2 ? 1 : 2) });
  I.outline();
  I.px(16, 13, W);
  I.sparkle(27, 5, 1, CREAM);
  return I.im;
}

function reroll() {
  const I = new Icon();
  // the sticks in the cup, one leaping out
  for (const [x, top] of [[10, 9], [13, 7], [18, 8], [21, 10]]) I.part(rect(x - 0.5, top, x + 0.5, 18), R.wood, { tone: (xx, y) => (y <= top + 1 ? R.red[2] : undefined), min: 2 });
  I.part(capsule(20, 7, 25, 1.8, 0.9), R.wood, { min: 2, tone: (x, y) => (x >= 24 ? R.red[2] : undefined) });
  // the cup: a red lacquer cylinder, gold bands, its mouth
  I.part(rect(7, 13, 24, 29), R.red, {
    sep: true,
    tone: (x, y, t, k) => {
      if (y === 15 || y === 16 || y === 26 || y === 27) return R.gold[k >= 2 ? 3 : 2];
      if (y >= 19 && y <= 22 && x >= 14 && x <= 17) return (x + y) % 2 ? R.gold[3] : R.gold[2];   // a fortune mark
    },
    light: [-0.9, -0.3],
  });
  I.part(ellipse(15.5, 13, 8.5, 1.6), R.red, { sep: true, flat: 0 });
  I.outline();
  // the leap
  I.pxs([[27, 5], [28, 7], [18, 3]], R.gold[3]).px(22, 0, CREAM);
  I.sparkle(4, 8, 1, R.gold[3]);
  return I.im;
}

function skip() {
  const I = new Icon();
  const chevron = x0 => poly([[x0, 5], [x0 + 6, 5], [x0 + 15, 16], [x0 + 6, 27], [x0, 27], [x0 + 9, 16]]);
  I.part(chevron(3), R.jade, { tone: (x, y, t, k) => (y === 16 ? Math.max(0, k - 1) : undefined) });
  I.part(chevron(14), R.jade, { sep: true, tone: (x, y, t, k) => (y === 16 ? Math.max(0, k - 1) : undefined) });
  I.outline();
  // wind trailing them
  I.pxs([[0, 10], [1, 10], [0, 22], [1, 22]], R.jade[3]);
  I.sparkle(29, 4, 1, W);
  return I.im;
}

function banish() {
  const I = new Icon();
  // the ghost fleeing up and out, fading as it goes
  const ghost = or(circle(22.5, 9, 5.2), poly([[17.5, 9], [27.5, 9], [30, 18], [26, 15], [23, 19], [20, 15]]));
  I.part(ghost, R.violet, {
    noOutline: true,
    tone: (x, y, t, k) => {
      if ((x === 21 || x === 24) && (y === 8 || y === 9)) return OUT;           // its eyes
      if (y >= 14 && D.bayer(x, y) < (y - 13) / 6) return null;                 // coming apart at the tail
    },
  });
  // the talisman, tilted: yellow paper, a red script down it, a red seal at its foot
  const c = Math.cos(-0.25), s = Math.sin(-0.25);
  const rot = (x, y) => [(x - 12) * c - (y - 17) * s, (x - 12) * s + (y - 17) * c];
  I.part((x, y) => { const [u, v] = rot(x, y); return Math.abs(u) <= 5.5 && Math.abs(v) <= 12.5; }, R.paper, {
    tone: (x, y, t, k) => {
      const [u, v] = rot(x + 0.5, y + 0.5);
      if (Math.abs(u) < 0.7 && v > -9 && v < 5) return R.red[2];                               // the stroke down the middle
      if (Math.abs(v + 6) < 0.6 && Math.abs(u) < 3.5) return R.red[2];                          // its cross strokes
      if (Math.abs(v + 1) < 0.6 && Math.abs(u) < 2.6) return R.red[2];
      if (Math.abs(v - 3.5) < 0.6 && u > -3.5 && u < 0) return R.red[1];
      if (v > 6.5 && v < 10.5 && Math.abs(u) < 2.4) return (Math.abs(u) > 1.4 || Math.abs(v - 8.5) > 1.2) ? R.red[2] : R.red[4];   // the seal
      if (Math.abs(u) > 4.7) return R.paper[1];
    },
  });
  I.outline();
  I.pxs([[28, 22], [30, 20], [19, 3], [29, 4]], R.violet[3]).px(26, 2, W);
  return I.im;
}

// ---------------------------------------------------------------- writing them out

const DEST = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "NewSprites", "Sprites", "Icons");
const ICONS = [
  ["heart 1", maxHealth], ["recovery", recovery], ["armor", armor], ["movespeed", moveSpeed],
  ["might", might], ["cooldownnecklace 1", cooldown], ["area", area], ["speed", weaponSpeed],
  ["pierce", armourPiercing], ["piercing", piercing], ["magnet-export", magnet], ["growth", growth],
  ["greedl", greed], ["revival", revival], ["reroll", reroll], ["skip", skip], ["banish", banish],
];

function main() {
  const all = [];
  for (const [name, make] of ICONS) {
    const im = make();
    png.encode(im, path.join(DEST, name + ".png"));
    all.push(im);
  }
  // Crit Chance and Crit Damage share a sheet, 28x28 sprites at (2, 2) and (34, 2)
  const sheet = D.image(64, 32), a = critChance(), b = critDamage();
  D.blit(sheet, a, 0, 0); D.blit(sheet, b, 32, 0);
  png.encode(sheet, path.join(DEST, "crit.png"));
  all.push(a, b);
  fs.mkdirSync(path.join(__dirname, "out"), { recursive: true });
  png.encode(png.preview(all, 6, [30, 26, 34], 10, 4), path.join(__dirname, "out", "stats_preview.png"));
  console.log(`wrote ${all.length} icons to ${DEST}`);
}

if (require.main === module) main();
module.exports = { ICONS, critChance, critDamage };
