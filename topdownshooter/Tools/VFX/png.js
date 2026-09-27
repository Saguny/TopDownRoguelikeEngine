// minimal PNG read/write (8-bit grey, grey+alpha, rgb, rgba, palette) plus a few helpers
const fs = require("fs");
const zlib = require("zlib");

const CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function crc(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = CRC[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function decode(file) {
  const buf = fs.readFileSync(file);
  let pos = 8, w = 0, h = 0, depth = 0, type = 0, plte = null, trns = null;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos), name = buf.toString("ascii", pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (name === "IHDR") { w = data.readUInt32BE(0); h = data.readUInt32BE(4); depth = data[8]; type = data[9]; }
    else if (name === "PLTE") plte = data;
    else if (name === "tRNS") trns = data;
    else if (name === "IDAT") idat.push(data);
    pos += 12 + len;
  }
  if (depth !== 8) throw new Error(file + ": only 8-bit PNGs, got " + depth);
  const ch = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 }[type];
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * ch, px = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let x = 0; x < stride; x++) {
      const a = x >= ch ? px[y * stride + x - ch] : 0, b = y > 0 ? px[(y - 1) * stride + x] : 0;
      const c = x >= ch && y > 0 ? px[(y - 1) * stride + x - ch] : 0;
      let v = src[x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      px[y * stride + x] = v & 255;
    }
  }
  const out = new Uint8Array(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    const s = i * ch, d = i * 4;
    if (type === 6) { out[d] = px[s]; out[d + 1] = px[s + 1]; out[d + 2] = px[s + 2]; out[d + 3] = px[s + 3]; }
    else if (type === 2) { out[d] = px[s]; out[d + 1] = px[s + 1]; out[d + 2] = px[s + 2]; out[d + 3] = 255; }
    else if (type === 0) { out[d] = out[d + 1] = out[d + 2] = px[s]; out[d + 3] = 255; }
    else if (type === 4) { out[d] = out[d + 1] = out[d + 2] = px[s]; out[d + 3] = px[s + 1]; }
    else { const k = px[s]; out[d] = plte[k * 3]; out[d + 1] = plte[k * 3 + 1]; out[d + 2] = plte[k * 3 + 2]; out[d + 3] = trns && k < trns.length ? trns[k] : 255; }
  }
  return { w, h, data: out };
}

function encode(img, file) {
  const { w, h, data } = img;
  const raw = Buffer.alloc(h * (w * 4 + 1));
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    Buffer.from(data.buffer, data.byteOffset + y * w * 4, w * 4).copy(raw, y * (w * 4 + 1) + 1);
  }
  const chunk = (name, body) => {
    const len = Buffer.alloc(4); len.writeUInt32BE(body.length);
    const nb = Buffer.concat([Buffer.from(name, "ascii"), body]);
    const c = Buffer.alloc(4); c.writeUInt32BE(crc(nb));
    return Buffer.concat([len, nb, c]);
  };
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0))]));
}

const make = (w, h) => ({ w, h, data: new Uint8Array(w * h * 4) });

// nearest-neighbour upscale onto a checker or flat background, for looking at pixel art
function preview(imgs, scale, bg = [24, 20, 34], cols = imgs.length, gap = 2) {
  const cw = Math.max(...imgs.map(i => i.w)), chh = Math.max(...imgs.map(i => i.h));
  const rows = Math.ceil(imgs.length / cols);
  const out = make((cw * cols + gap * (cols + 1)) * scale, (chh * rows + gap * (rows + 1)) * scale);
  for (let i = 0; i < out.w * out.h; i++) { out.data[i * 4] = bg[0]; out.data[i * 4 + 1] = bg[1]; out.data[i * 4 + 2] = bg[2]; out.data[i * 4 + 3] = 255; }
  imgs.forEach((img, n) => {
    const ox = (gap + (n % cols) * (cw + gap)) * scale, oy = (gap + Math.floor(n / cols) * (chh + gap)) * scale;
    for (let y = 0; y < img.h * scale; y++) for (let x = 0; x < img.w * scale; x++) {
      const s = (Math.floor(y / scale) * img.w + Math.floor(x / scale)) * 4, a = img.data[s + 3] / 255;
      const d = ((oy + y) * out.w + ox + x) * 4;
      for (let c = 0; c < 3; c++) out.data[d + c] = Math.round(img.data[s + c] * a + out.data[d + c] * (1 - a));
    }
  });
  return out;
}

module.exports = { decode, encode, make, preview };
