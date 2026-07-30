const fs = require("fs");
const s = fs.readFileSync("artifacts/group671.svg", "utf8");

// All rects with explicit x,y,w,h that look like chrome (not tiny pattern crumbs)
const rects = [...s.matchAll(/<rect\b([^>]*)\/?>/g)];
const rows = [];
for (const r of rects) {
  const a = r[1];
  const get = (k) => (a.match(new RegExp(`\\b${k}="([^"]+)"`)) || [])[1];
  const x = get("x"),
    y = get("y"),
    w = get("width"),
    h = get("height");
  if (x == null || y == null || w == null || h == null) continue;
  const nx = +x,
    ny = +y,
    nw = +w,
    nh = +h;
  if (!Number.isFinite(nx) || !Number.isFinite(ny)) continue;
  if (nw < 3 && nh < 3) continue;
  // skip tiny
  if (nw < 20 && nh < 20 && !a.includes("#")) continue;
  rows.push({
    x: nx,
    y: ny,
    w: nw,
    h: nh,
    fill: get("fill"),
    stroke: get("stroke"),
    rx: get("rx"),
  });
}

rows.sort((a, b) => a.y - b.y || a.x - b.x);
console.log("chrome/layout rects:");
for (const r of rows) {
  if (r.w >= 30 || r.h >= 30 || r.x > 250)
    console.log(
      `  x=${r.x} y=${r.y} w=${r.w} h=${r.h} fill=${r.fill} stroke=${r.stroke}`
    );
}

// transforms near scrollbar
const tr = [...s.matchAll(/transform="([^"]+)"/g)].slice(0, 30);
console.log("\ntransforms sample", tr.length);
for (const t of tr.slice(0, 20)) console.log(" ", t[1]);

// look for 278x40 cards with y
console.log("\n278x40 cards:");
for (const r of rows) {
  if (Math.abs(r.w - 278) < 1 && Math.abs(r.h - 40) < 1)
    console.log(" card", r.x, r.y);
}

// EXIT-like 288x39
console.log("\n288x39 buttons:");
for (const r of rows) {
  if (Math.abs(r.w - 288) < 2 && Math.abs(r.h - 39) < 2)
    console.log(" btn", r.x, r.y, r.stroke, r.fill);
}

// scrollbar-ish at x near 285
console.log("\nright-edge rects x>=280:");
for (const r of rows) {
  if (r.x >= 280) console.log(" ", r);
}
