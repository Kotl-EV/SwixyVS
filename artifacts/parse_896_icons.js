const fs = require("fs");
const s = fs.readFileSync("artifacts/group896.svg", "utf8");

const paths = [...s.matchAll(/<path\b([^>]*)\/?>/g)];
console.log("paths", paths.length);
for (const p of paths) {
  const a = p[1];
  const stroke = (a.match(/stroke="([^"]+)"/) || [])[1];
  const fill = (a.match(/fill="([^"]+)"/) || [])[1];
  const d = (a.match(/\bd="([^"]+)"/) || [])[1] || "";
  if (!stroke && !fill) continue;
  const col = stroke || fill;
  if (!["#AD00C8", "#5BD5DD", "#FFC74C", "#FD5A53"].includes(col)) continue;
  // first M coords
  const m = d.match(/M\s*([\d.]+)\s+([\d.]+)/);
  const y = m ? +m[2] : 99;
  if (y > 40 && y < 120) continue; // skip mid
  console.log(col, "y~", y, "d", d.slice(0, 160));
}

// legend rects
console.log("\nlegend chips:");
const rects = [...s.matchAll(/<rect\b([^>]*)\/?>/g)];
for (const r of rects) {
  const a = r[1];
  const get = (k) => (a.match(new RegExp(`\\b${k}="([^"]+)"`)) || [])[1];
  const y = +get("y");
  if (y > 0 && y < 25) console.log(get("x"), get("y"), get("width"), get("height"), get("fill"), get("stroke"));
}

// decode labels by measuring common words widths is hard; print path clusters at y~5.5
console.log("\nlabel paths y 1-15:");
for (const p of paths) {
  const a = p[1];
  const fill = (a.match(/fill="([^"]+)"/) || [])[1];
  if (fill !== "#555555") continue;
  const d = (a.match(/\bd="([^"]+)"/) || [])[1] || "";
  const m = d.match(/M\s*([\d.]+)\s+([\d.]+)/);
  if (!m) continue;
  const x = +m[1], y = +m[2];
  if (y < 1 || y > 20) continue;
  console.log("label@", x, y, "len", d.length, d.slice(0, 80));
}
