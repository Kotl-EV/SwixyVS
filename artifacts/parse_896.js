const fs = require("fs");
const s = fs.readFileSync("artifacts/group896.svg", "utf8");
console.log("len", s.length);
console.log("viewBox", (s.match(/viewBox="([^"]+)"/) || [])[1]);
console.log("w/h", (s.match(/\bwidth="([^"]+)"/) || [])[1], (s.match(/\bheight="([^"]+)"/) || [])[1]);

const rects = [...s.matchAll(/<rect\b([^>]*)\/?>/g)];
console.log("rects", rects.length);
for (const r of rects.slice(0, 80)) {
  const a = r[1];
  const get = (k) => (a.match(new RegExp(`\\b${k}="([^"]+)"`)) || [])[1];
  const x = get("x"), y = get("y"), w = get("width"), h = get("height");
  if (x == null && y == null) continue;
  console.log("RECT", x, y, w, h, get("rx"), get("fill"), get("stroke"));
}

// text-like paths
const re = /<path\b([^>]*?)\bd="([^"]*)"([^>]*)>/g;
let m;
const items = [];
while ((m = re.exec(s))) {
  const a = m[1] + " " + m[3];
  const d = m[2];
  if (d.length > 8000) continue;
  const fill = (a.match(/\bfill="([^"]*)"/) || [])[1];
  if (!fill || fill === "none") continue;
  let x = 0, y = 0, minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity, ok = false;
  const cmdRe = /([MLHVZmlhvz])([^MLHVZmlhvz]*)/g;
  let c;
  while ((c = cmdRe.exec(d))) {
    const cmd = c[1];
    const args = c[2].trim().split(/[\s,]+/).filter(Boolean).map(Number);
    let i = 0;
    const up = (nx, ny) => {
      if (!Number.isFinite(nx) || !Number.isFinite(ny)) return;
      minX = Math.min(minX, nx); maxX = Math.max(maxX, nx);
      minY = Math.min(minY, ny); maxY = Math.max(maxY, ny);
      x = nx; y = ny; ok = true;
    };
    if (cmd === "M" || cmd === "L") while (i + 1 < args.length) { up(args[i], args[i + 1]); i += 2; }
    else if (cmd === "m" || cmd === "l") while (i + 1 < args.length) { up(x + args[i], y + args[i + 1]); i += 2; }
    else if (cmd === "H") while (i < args.length) up(args[i++], y);
    else if (cmd === "h") while (i < args.length) up(x + args[i++], y);
    else if (cmd === "V") while (i < args.length) up(x, args[i++]);
    else if (cmd === "v") while (i < args.length) up(x, y + args[i++]);
  }
  if (!ok) continue;
  const w = maxX - minX, h = maxY - minY;
  if (h < 40 && w < 250 && d.length < 5000)
    items.push({ fill, minX, minY, w, h, dLen: d.length });
}
items.sort((a, b) => a.minY - b.minY || a.minX - b.minX);
console.log("\ntext-like:");
for (const it of items)
  console.log(`  ${it.fill} x=${it.minX.toFixed(1)} y=${it.minY.toFixed(1)} w=${it.w.toFixed(1)} h=${it.h.toFixed(1)} dLen=${it.dLen}`);
