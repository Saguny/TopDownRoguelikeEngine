// writes assemble.lua: Aseprite builds each element as a layered, timed, tagged .aseprite file
// under <dest>/<group>/. usage: node make-lua.js [out dir] [dest]; by default the weapons'
// out/ and NewSprites/Asesprites/VFX/Weapons/
const fs = require("fs");
const path = require("path");

const OUT = path.resolve(process.argv[2] || path.join(__dirname, "out")).split(path.sep).join("/");
const DEST = process.argv[3] || "C:/Workspace/topdownshooter/topdownshooter/Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons/";
const manifest = JSON.parse(fs.readFileSync(path.join(OUT, "manifest.json"), "utf8"));
const q = s => JSON.stringify(s);
const lines = [];
const emit = s => lines.push(s);

const palette = Object.values(manifest.palette);
emit(`local function palette()
  local pal = Palette(${palette.length + 1})
  pal:setColor(0, Color{ r = 0, g = 0, b = 0, a = 0 })`);
palette.forEach((c, i) => emit(`  pal:setColor(${i + 1}, Color{ r = ${c[0]}, g = ${c[1]}, b = ${c[2]}, a = 255 })`));
emit(`  return pal
end

local function build(group, name, w, h, layers, durations, tags)
  local spr = Sprite(w, h, ColorMode.RGB)
  spr:setPalette(palette())
  spr.layers[1].name = layers[1]
  for i = 2, #layers do spr:newLayer().name = layers[i] end
  for f = 2, #durations do spr:newEmptyFrame() end
  for f = 1, #durations do spr.frames[f].duration = durations[f] / 1000 end
  for li = 1, #layers do
    for f = 1, #durations do
      local img = Image{ fromFile = ${q(OUT + "/")} .. name .. "/L" .. (li - 1) .. "_" .. (f - 1) .. ".png" }
      if not img:isEmpty() then spr:newCel(spr.layers[li], f, img, Point(0, 0)) end
    end
  end
  for _, t in ipairs(tags) do
    local tag = spr:newTag(t[2] + 1, t[3] + 1)
    tag.name = t[1]
  end
  app.fs.makeAllDirectories(${q(DEST)} .. group)
  spr:saveAs(${q(DEST)} .. group .. "/" .. name .. ".aseprite")
  spr:close()
  print("built " .. group .. "/" .. name .. " (" .. #layers .. " layers, " .. #durations .. " frames)")
end
`);
for (const e of manifest.elements) {
  const tags = e.tags.map(t => `{ ${q(t[0])}, ${t[1]}, ${t[2]} }`).join(", ");
  emit(`build(${q(e.group)}, ${q(e.name)}, ${e.w}, ${e.h}, { ${e.layers.map(q).join(", ")} }, { ${e.durations.join(", ")} }, { ${tags} })`);
}
fs.writeFileSync(path.join(OUT, "..", "assemble.lua"), lines.join("\n"));
console.log("assemble.lua written, " + manifest.elements.length + " elements");
