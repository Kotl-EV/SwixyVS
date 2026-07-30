const fs = require("fs");
const s = fs.readFileSync("artifacts/group671.svg", "utf8");
console.log("len", s.length);
console.log("viewBox", (s.match(/viewBox="([^"]+)"/) || [])[1]);
console.log("w/h", (s.match(/\bwidth="([^"]+)"/) || [])[1], (s.match(/\bheight="([^"]+)"/) || [])[1]);

const rects = [...s.matchAll(/<rect\b([^>]*)\/?>/g)];
console.log("\nrects", rects.length);
for (const r of rects) {
  const a = r[1];
  const get = (k) => (a.match(new RegExp(`\\b${k}="([^"]+)"`)) || [])[1];
  console.log(
    "RECT",
    get("x"),
    get("y"),
    get("width"),
    get("height"),
    get("rx"),
    get("fill"),
    get("stroke")
  );
}

// images
const imgs = [...s.matchAll(/<image\b([^>]*)\/?>/g)];
console.log("\nimages", imgs.length);
for (const r of imgs) {
  const a = r[1];
  const get = (k) => (a.match(new RegExp(`\\b${k}="([^"]+)"`)) || [])[1];
  console.log("IMG", get("x"), get("y"), get("width"), get("height"));
}

// small path text bboxes
const re = /<path\b([^>]*?)\bd="([^"]*)"([^>]*)>/g;
let m;
const items = [];
while ((m = re.exec(s))) {
  const a = m[1] + " " + m[3];
  const d = m[2];
  if (d.length > 6000) continue;
  const fill = (a.match(/\bfill="([^"]*)"/) || [])[1];
  if (!fill || fill === "none") continue;
  let x = 0,
    y = 0,
    minX = Infinity,
    minY = Infinity,
    maxX = -Infinity,
    maxY = -Infinity,
    ok = false;
  const cmdRe = /([MLHVZmlhvz])([^MLHVZmlhvz]*)/g;
  let c;
  while ((c = cmdRe.exec(d))) {
    const cmd = c[1];
    const args = c[2]
      .trim()
      .split(/[\s,]+/)
      .filter(Boolean)
      .map(Number);
    let i = 0;
    const up = (nx, ny) => {
      if (!Number.isFinite(nx) || !Number.isFinite(ny)) return;
      minX = Math.min(minX, nx);
      maxX = Math.max(maxX, nx);
      minY = Math.min(minY, ny);
      maxY = Math.max(maxY, ny);
      x = nx;
      y = ny;
      ok = true;
    };
    if (cmd === "M" || cmd === "L")
      while (i + 1 < args.length) {
        up(args[i], args[i + 1]);
        i += 2;
      }
    else if (cmd === "m" || cmd === "l")
      while (i + 1 < args.length) {
        up(x + args[i], y + args[i + 1]);
        i += 2;
      }
    else if (cmd === "H") while (i < args.length) up(args[i++], y);
    else if (cmd === "h") while (i < args.length) up(x + args[i++], y);
    else if (cmd === "V") while (i < args.length) up(x, args[i++]);
    else if (cmd === "v") while (i < args.length) up(x, y + args[i++]);
  }
  if (!ok) continue;
  items.push({
    fill,
    minX,
    minY,
    maxX,
    maxY,
    w: maxX - minX,
    h: maxY - minY,
    dLen: d.length,
  });
}

console.log("\ntext-like paths:");
items
  .filter((it) => it.h < 50 && it.w < 350 && it.dLen < 5000)
  .sort((a, b) => a.minY - b.minY || a.minX - b.minX)
  .forEach((it) =>
    console.log(
      `  ${it.fill} x=${it.minX.toFixed(1)} y=${it.minY.toFixed(1)} w=${it.w.toFixed(1)} h=${it.h.toFixed(1)}`
    )
  );
