// the VFX look laid over flat pixel art (the enemies, the boss): each colour ramp gets two darker,
// violet-shifted steps and a lighter, warm-shifted one; a key light from the upper right shades
// every patch across its whole shape in dithered bands, the way the fire and lightning are shaded,
// with a specular where it's brightest; the edges facing the light catch it; a cool violet rim
// catches the far side; the feet sink into a dithered occlusion; and the outline turns from black
// to a deep plum.
// drop shadows baked into the art are taken out (the game draws them now). ramps are listed per
// sprite, dark to light, so every change is deliberate
const D = require("./draw");

const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
const key = (r, g, b) => (r << 16) | (g << 8) | b;

function rgbToHsv([r, g, b]) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  let h = 0;
  if (d > 0) h = mx === r ? ((g - b) / d) % 6 : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return [(h * 60 + 360) % 360, mx === 0 ? 0 : d / mx, mx];
}
function hsvToRgb([h, s, v]) {
  const c = v * s, x = c * (1 - Math.abs(((h / 60) % 2) - 1)), m = v - c;
  const [r, g, b] = h < 60 ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] : h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x];
  return [Math.round((r + m) * 255), Math.round((g + m) * 255), Math.round((b + m) * 255)];
}
// turns hue toward a target by up to `amount` degrees
function towards(h, target, amount) {
  let d = ((target - h + 540) % 360) - 180;
  return (h + Math.sign(d) * Math.min(Math.abs(d), amount) + 360) % 360;
}
function darker(rgb) {
  const [h, s, v] = rgbToHsv(rgb);
  return hsvToRgb([s < 0.08 ? 265 : towards(h, 270, 22), Math.min(1, s + (s < 0.08 ? 0.18 : 0.12)), v * 0.66]);
}
function lighter(rgb) {
  const [h, s, v] = rgbToHsv(rgb);
  return hsvToRgb([s < 0.08 ? h : towards(h, 52, 14), s * 0.72, Math.min(1, v * 1.1 + 0.1)]);
}
const mix = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * t));

// frames: images {w, h, data}. opts:
//   ramps       [[hex, ...], ...] dark to light
//   outline     the outline colour in the source (default pure black) and outlineTo, its new colour
//   minArea     smallest patch of one ramp that gets lit and shaded (details stay as drawn)
//   ao          rows at the feet that sink into shadow
//   rim, rimAmount   the far-side rim light
//   glow        hex colours that are light sources: never shaded, never rimmed
function restyle(frames, opts) {
  const ramps = opts.ramps.map(r => {
    const base = r.map(hex);
    return [darker(darker(base[0])), darker(base[0]), ...base, lighter(base[base.length - 1])];
  });
  const lookup = new Map();
  opts.ramps.forEach((r, ri) => r.forEach((h, i) => lookup.set(key(...hex(h)), [ri, i + 2])));
  const outlineIn = key(...hex(opts.outline || "#000000"));
  const outlineTo = hex(opts.outlineTo || "#1a0c26");
  const rim = hex(opts.rim || "#a45ce8"), rimAmount = opts.rimAmount === undefined ? 0.32 : opts.rimAmount;
  const minArea = opts.minArea || 14, aoRows = opts.ao === undefined ? 3 : opts.ao;
  const glow = new Set((opts.glow || []).map(h => key(...hex(h))));

  return frames.map((src, fi) => {
    const { w, h } = src, out = D.image(w, h);
    const px = (x, y) => (x < 0 || y < 0 || x >= w || y >= h) ? null : src.data.subarray((y * w + x) * 4, (y * w + x) * 4 + 4);
    const kindAt = new Int16Array(w * h).fill(-3);   // -3 empty, -2 outline, -1 other, else ramp
    const idxAt = new Int16Array(w * h);
    let top = h, bottom = -1;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const p = px(x, y);
      if (p[3] < 255) continue;                    // baked shadows (and any soft pixels) go
      const k = key(p[0], p[1], p[2]), i = y * w + x;
      if (k === outlineIn) kindAt[i] = -2;
      else if (lookup.has(k) && !glow.has(k)) { kindAt[i] = lookup.get(k)[0]; idxAt[i] = lookup.get(k)[1]; }
      else kindAt[i] = -1;
      top = Math.min(top, y); bottom = Math.max(bottom, y);
    }

    // patches of one ramp, and how big they are
    const comp = new Int32Array(w * h).fill(-1), area = [];
    for (let i = 0; i < w * h; i++) {
      if (kindAt[i] < 0 || comp[i] >= 0) continue;
      const id = area.length, stack = [i];
      comp[i] = id;
      let n = 0;
      while (stack.length) {
        const j = stack.pop(), x = j % w, y = (j / w) | 0;
        n++;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const nj = ny * w + nx;
          if (comp[nj] < 0 && kindAt[nj] === kindAt[i]) { comp[nj] = id; stack.push(nj); }
        }
      }
      area.push(n);
    }

    const box = area.map(() => [1e9, 1e9, -1, -1]);
    for (let i = 0; i < w * h; i++) {
      if (comp[i] < 0) continue;
      const b = box[comp[i]], x = i % w, y = (i / w) | 0;
      b[0] = Math.min(b[0], x); b[1] = Math.min(b[1], y); b[2] = Math.max(b[2], x); b[3] = Math.max(b[3], y);
    }
    const solid = (x, y) => x >= 0 && y >= 0 && x < w && y < h && kindAt[y * w + x] >= -1;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x, k = kindAt[i];
      if (k === -3) continue;
      if (k === -2) { D.put(out, x, y, [...outlineTo, 255]); continue; }
      const p = px(x, y);
      if (k === -1) { D.put(out, x, y, [p[0], p[1], p[2], 255]); continue; }

      const ramp = ramps[k];
      let idx = idxAt[i];
      const big = area[comp[i]] >= minArea;
      if (big) {
        // the light across the whole patch, in dithered bands
        const [bx0, by0, bx1, by1] = box[comp[i]];
        const rx = Math.max(2, (bx1 - bx0) / 2), ry = Math.max(2, (by1 - by0) / 2);
        // clean bands with a thin dithered seam between them, weighted to the light so the
        // crowd stays bright against the floor
        const t = ((x - (bx0 + bx1) / 2) / rx) * 0.5 - ((y - (by0 + by1) / 2) / ry) * 0.85 + (D.bayer(x, y) - 0.5) * 0.16;
        if (t > 0.95) idx += 2;
        else if (t > 0.3) idx += 1;
        else if (t < -1.15) idx -= 2;
        else if (t < -0.6) idx -= 1;
        const litSide = !solid(x + 1, y) || !solid(x, y - 1);
        const darkSide = !solid(x - 1, y) || !solid(x, y + 1);
        if (litSide && !darkSide && solid(x - 1, y + 1)) idx++;
        else if (darkSide && !litSide && solid(x + 1, y - 1)) idx--;
        if (aoRows > 0 && y > bottom - aoRows && D.bayer(x, y) < 0.5) idx--;
      }
      idx = Math.max(0, Math.min(ramp.length - 1, idx));
      let c = ramp[idx];
      if (big && !solid(x - 1, y) && y < top + (bottom - top) * 0.72 && idx >= 1) c = mix(c, rim, rimAmount);
      D.put(out, x, y, [...c, 255]);
    }
    return out;
  });
}

module.exports = { restyle, hex, darker, lighter, mix, rgbToHsv, hsvToRgb };
