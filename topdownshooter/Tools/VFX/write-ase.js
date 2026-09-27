// writes the layered, timed, tagged .aseprite files straight from a generator's out/ folder, the
// same files assemble.lua has Aseprite build, for machines without Aseprite. RGBA, one layer per
// generator layer, the palette carried along for editing.
//   node write-ase.js <out dir> <dest dir>
// e.g. node write-ase.js weapons/out "../../Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"
// the format: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");
const png = require("./png");

class Out {
  constructor() { this.parts = []; }
  byte(v) { const b = Buffer.alloc(1); b.writeUInt8(v & 0xff); this.parts.push(b); return this; }
  word(v) { const b = Buffer.alloc(2); b.writeUInt16LE(v & 0xffff); this.parts.push(b); return this; }
  short(v) { const b = Buffer.alloc(2); b.writeInt16LE(v); this.parts.push(b); return this; }
  dword(v) { const b = Buffer.alloc(4); b.writeUInt32LE(v >>> 0); this.parts.push(b); return this; }
  zeros(n) { this.parts.push(Buffer.alloc(n)); return this; }
  bytes(buf) { this.parts.push(Buffer.from(buf)); return this; }
  string(s) { const b = Buffer.from(s, "utf8"); this.word(b.length); this.parts.push(b); return this; }
  done() { return Buffer.concat(this.parts); }
}

function chunk(type, body) {
  return new Out().dword(body.length + 6).word(type).bytes(body).done();
}

// the part of an image that has anything in it, or null when it's empty
function bounds(im) {
  let x0 = im.w, y0 = im.h, x1 = -1, y1 = -1;
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++)
    if (im.data[(y * im.w + x) * 4 + 3]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
}

// a whole-canvas cel at the origin, the way Aseprite saves an image placed at (0, 0); empty ones
// are left out, as Aseprite does
function cel(layer, im) {
  if (!bounds(im)) return null;
  const px = Buffer.from(im.data.buffer, im.data.byteOffset, im.w * im.h * 4);
  const body = new Out().word(layer).short(0).short(0).byte(255).word(2).short(0).zeros(5)
    .word(im.w).word(im.h).bytes(zlib.deflateSync(px, { level: 9 })).done();
  return chunk(0x2005, body);
}

// layers: [{ name, frames: [image...] }], durations in ms, tags: [[name, from, to]], palette: [[r,g,b,a]...]
function write(file, w, h, layers, durations, tags, palette) {
  const frames = [];
  for (let f = 0; f < durations.length; f++) {
    const chunks = [];
    if (f === 0) {
      // in Aseprite's own order: colour profile, palette, tags (each with its user data), layers
      chunks.push(chunk(0x2007, new Out().word(1).word(0).dword(0).zeros(8).done()));      // sRGB
      const colours = [[0, 0, 0, 0], ...palette];
      const pal = new Out().dword(colours.length).dword(0).dword(colours.length - 1).zeros(8);
      colours.forEach(c => pal.word(0).byte(c[0]).byte(c[1]).byte(c[2]).byte(c[3] === undefined ? 255 : c[3]));
      chunks.push(chunk(0x2019, pal.done()));
      if (tags.length) {
        const t = new Out().word(tags.length).zeros(8);
        tags.forEach(([name, from, to]) => t.word(from).word(to).byte(0).word(0).zeros(6).byte(0).byte(0).byte(0).byte(0).string(name));
        chunks.push(chunk(0x2018, t.done()));
        tags.forEach(() => chunks.push(chunk(0x2020, new Out().dword(2).byte(0).byte(0).byte(0).byte(255).done())));
      }
      layers.forEach(l => chunks.push(chunk(0x2004,
        new Out().word(3).word(0).word(0).word(0).word(0).word(0).byte(255).zeros(3).string(l.name).done())));
    }
    layers.forEach((l, li) => { const c = cel(li, l.frames[f]); if (c) chunks.push(c); });
    const body = Buffer.concat(chunks);
    const head = new Out().dword(16 + body.length).word(0xf1fa).word(Math.min(chunks.length, 0xffff)).word(durations[f]).zeros(2).dword(chunks.length).done();
    frames.push(head, body);
  }
  const rest = Buffer.concat(frames);
  const header = new Out().dword(128 + rest.length).word(0xa5e0).word(durations.length).word(w).word(h).word(32).dword(1).word(100)
    .dword(0).dword(0).byte(0).zeros(3).word(Math.min(256, palette.length + 1) & 0xff).byte(1).byte(1).short(0).short(0).word(16).word(16).zeros(84).done();
  if (header.length !== 128) throw new Error("header is " + header.length + " bytes");
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, Buffer.concat([header, rest]));
}

// every element in a generator's out/manifest.json, into <dest>/<group>/<name>.aseprite
function fromOut(outDir, dest) {
  const manifest = JSON.parse(fs.readFileSync(path.join(outDir, "manifest.json"), "utf8"));
  const palette = Object.values(manifest.palette);
  for (const e of manifest.elements) {
    const layers = e.layers.map((name, li) => ({
      name,
      frames: Array.from({ length: e.frames }, (_, f) => png.decode(path.join(outDir, e.name, `L${li}_${f}.png`))),
    }));
    const file = path.join(dest, e.group, e.name + ".aseprite");
    write(file, e.w, e.h, layers, e.durations, e.tags, palette);
    console.log("wrote " + path.relative(process.cwd(), file));
  }
}

module.exports = { write, fromOut };
if (require.main === module) {
  if (process.argv.length < 4) { console.log("usage: node write-ase.js <out dir> <dest dir>"); process.exit(1); }
  fromOut(path.resolve(process.argv[2]), path.resolve(process.argv[3]));
}
