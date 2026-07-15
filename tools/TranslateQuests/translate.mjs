/**
 * Translate all quest i18n fields to every VS language (real MT via Google gtx).
 * Strategy: for each unique EN string, translate to ALL target langs in parallel,
 * then pause briefly. Resume via translate-cache.json.
 */
import fs from "fs";

const questsPath = process.argv[2] || "C:\\temp\\quests-work.json";
const cachePath = "C:\\temp\\TranslateQuests\\translate-cache.json";
const RESULT_COPY = "E:\\рабочие файлы\\GitHub\\SwixyVS\\SwixyQuestBook\\Data\\quests.json";

const LANG_MAP = {
  ar: "ar", be: "be", cs: "cs", da: "da", de: "de", en: "en", eo: "eo",
  "es-419": "es", "es-es": "es", fi: "fi", fr: "fr", hu: "hu", is: "is",
  it: "it", ja: "ja", ko: "ko", li: "nl", lt: "lt", nl: "nl", no: "no",
  pl: "pl", "pt-br": "pt", "pt-pt": "pt", ro: "ro", ru: "ru", sk: "sk",
  sr: "sr", "sv-se": "sv", th: "th", tr: "tr", uk: "uk", vi: "vi",
  "zh-cn": "zh-CN", "zh-tw": "zh-TW",
};

const ALL_VS = Object.keys(LANG_MAP);
const GOOGLE_LANGS = [...new Set(ALL_VS.filter((l) => l !== "en").map((l) => LANG_MAP[l]))];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function loadCache() {
  try {
    return fs.existsSync(cachePath) ? JSON.parse(fs.readFileSync(cachePath, "utf8")) : {};
  } catch {
    return {};
  }
}
function saveCache(c) {
  fs.writeFileSync(cachePath, JSON.stringify(c), "utf8");
}

function isLangMap(obj) {
  if (!obj || typeof obj !== "object" || Array.isArray(obj)) return false;
  const keys = Object.keys(obj);
  return (
    keys.length > 0 &&
    keys.every((k) => typeof obj[k] === "string" && /^[a-z]{2}(-[a-z0-9]{2,8})?$/i.test(k))
  );
}

function collectLangMaps(node, out = []) {
  if (Array.isArray(node)) for (const x of node) collectLangMaps(x, out);
  else if (node && typeof node === "object") {
    if (isLangMap(node)) out.push(node);
    else for (const v of Object.values(node)) collectLangMaps(v, out);
  }
  return out;
}

async function translateGtx(text, to) {
  const url =
    "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=" +
    encodeURIComponent(to) +
    "&dt=t&q=" +
    encodeURIComponent(text);
  const res = await fetch(url, {
    headers: {
      "User-Agent":
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    },
    signal: AbortSignal.timeout(20000),
  });
  if (!res.ok) throw new Error(`gtx ${res.status}`);
  const data = await res.json();
  const out = (data?.[0] || []).map((x) => x?.[0]).filter(Boolean).join("").trim();
  if (!out) throw new Error("empty");
  return out;
}

async function translateOne(text, to, cache) {
  const key = `${to}::${text}`;
  if (cache[key]) return cache[key];
  for (let i = 0; i < 4; i++) {
    try {
      const out = await translateGtx(text, to);
      cache[key] = out;
      return out;
    } catch (e) {
      await sleep(500 * (i + 1) + Math.random() * 300);
      if (i === 3) {
        console.warn(`FAIL ${to}: ${e.message || e}`);
        cache[key] = text;
        return text;
      }
    }
  }
  cache[key] = text;
  return text;
}

async function main() {
  console.log("quests:", questsPath);
  const db = JSON.parse(fs.readFileSync(questsPath, "utf8"));
  const cache = loadCache();
  const maps = collectLangMaps(db);
  const uniqueEn = [...new Set(maps.map((m) => (m.en || "").trim()).filter(Boolean))];
  console.log(`maps=${maps.length} uniqueEn=${uniqueEn.length} cacheKeys=${Object.keys(cache).length} targets=${GOOGLE_LANGS.length}`);

  let completedTexts = 0;
  for (const text of uniqueEn) {
    const missing = GOOGLE_LANGS.filter((g) => !cache[`${g}::${text}`]);
    if (missing.length === 0) {
      completedTexts++;
      continue;
    }
    // Parallel per source string (all languages at once)
    await Promise.all(missing.map((g) => translateOne(text, g, cache)));
    completedTexts++;
    saveCache(cache);
    if (completedTexts % 5 === 0 || completedTexts === uniqueEn.length) {
      console.log(`texts ${completedTexts}/${uniqueEn.length} cache=${Object.keys(cache).length}`);
    }
    await sleep(250 + Math.random() * 200);
  }
  saveCache(cache);

  console.log("applying to quests...");
  let applied = 0;
  for (const map of maps) {
    const en = (map.en || Object.values(map).find((v) => typeof v === "string" && String(v).trim()) || "").trim();
    if (!en) continue;
    map.en = en;
    const authoredRu = (map.ru || "").trim();
    const keepRu = authoredRu && authoredRu !== en;

    for (const vs of ALL_VS) {
      if (vs === "en") continue;
      if (vs === "ru" && keepRu) {
        map.ru = authoredRu;
        continue;
      }
      const g = LANG_MAP[vs];
      map[vs] = cache[`${g}::${en}`] || en;
      applied++;
    }
  }

  db.version = String(db.version || "1.0")
    .replace(/\+alllangs/g, "")
    .replace(/\+mt-alllangs/g, "")
    .replace(/\+i18n$/g, "+i18n") + "+mt-alllangs";
  if (!String(db.version).includes("+i18n")) db.version = db.version.replace("+mt-alllangs", "+i18n+mt-alllangs");

  fs.writeFileSync(questsPath, JSON.stringify(db, null, 2) + "\n", "utf8");
  try {
    fs.copyFileSync(questsPath, RESULT_COPY);
    console.log("copied to repo quests.json");
  } catch (e) {
    console.warn("copy failed:", e.message);
  }
  try {
    fs.copyFileSync(cachePath, "E:\\рабочие файлы\\GitHub\\SwixyVS\\tools\\TranslateQuests\\translate-cache.json");
  } catch {}
  console.log("DONE applied=", applied);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
