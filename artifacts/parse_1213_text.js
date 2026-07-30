const fs = require("fs");
const s = fs.readFileSync("artifacts/group1213/Group1213.svg", "utf8");

// All paths with fill that look like pixel text
const re = /<path\b([^>]*?)\bd="([^"]*)"([^>]*)>/g;
let m;
const items = [];
while ((m = re.exec(s))) {
  const a = m[1] + " " + m[3];
  const d = m[2];
  const get = (k) => {
    const r = a.match(new RegExp(`\\b${k}="([^"]*)"`));
    return r ? r[1] : null;
  };
  const fill = get("fill");
  if (!fill || fill === "none") continue;
  if (d.length > 50000) continue; // skip huge frames

  // bbox via M/L/H/V
  let x = 0, y = 0;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  const cmdRe = /([MLHVZmlhvz])([^MLHVZmlhvz]*)/g;
  let c;
  let ok = false;
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
    if (cmd === "M" || cmd === "L") {
      while (i + 1 < args.length) { up(args[i], args[i + 1]); i += 2; }
    } else if (cmd === "m" || cmd === "l") {
      while (i + 1 < args.length) { up(x + args[i], y + args[i + 1]); i += 2; }
    } else if (cmd === "H") { while (i < args.length) up(args[i++], y); }
    else if (cmd === "h") { while (i < args.length) up(x + args[i++], y); }
    else if (cmd === "V") { while (i < args.length) up(x, args[i++]); }
    else if (cmd === "v") { while (i < args.length) up(x, y + args[i++]); }
  }
  if (!ok) continue;
  items.push({
    fill,
    minX, minY, maxX, maxY,
    w: maxX - minX,
    h: maxY - minY,
    dLen: d.length,
  });
}

// Cluster likely text by color
const interesting = ["#555555", "#AAAAAA", "white", "#FFFFFF", "#55FFFF", "#FFAA00", "#666666", "#FAFFFD"];
for (const color of interesting) {
  const arr = items
    .filter((it) => (it.fill || "").toUpperCase() === color.toUpperCase() || it.fill === color)
    .sort((a, b) => a.minY - b.minY || a.minX - b.minX);
  console.log("\n===", color, "n=" + arr.length, "===");
  for (const it of arr) {
    console.log(
      `  x=${it.minX.toFixed(1)} y=${it.minY.toFixed(1)} w=${it.w.toFixed(1)} h=${it.h.toFixed(1)} ` +
        `cx=${((it.minX + it.maxX) / 2).toFixed(1)} cy=${((it.minY + it.maxY) / 2).toFixed(1)} dLen=${it.dLen}`
    );
  }
}

// Also white without hash
const whites = items.filter((it) => it.fill === "white" || it.fill === "#FFFFFF" || it.fill === "#fff");
console.log("\nwhite total", whites.length);
