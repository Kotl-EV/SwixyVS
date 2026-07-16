/**
 * Regenerates default quest branches with craft / detect / have / kill objectives
 * and multi-language title/header/description maps for all VS UI language codes.
 *
 * Item codes validated against Vintage Story 1.22 survival assets.
 * Run: node tools/generate-default-quests.mjs
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(__dirname, "..", "Data", "quests");
const BRANCHES = path.join(OUT, "branches");

const LANGS = [
  "ar", "be", "cs", "da", "de", "en", "eo", "es", "fi", "fr", "hu", "is", "it",
  "ja", "ko", "li", "lt", "nl", "no", "pl", "pt", "ro", "ru", "sk", "sr", "sv",
  "th", "tr", "uk", "vi", "zh",
];

const STEP = 120;
const X0 = 0, Y0 = 0;

/** @param {string} en @param {string} ru @param {Record<string,string>} [extra] */
function L(en, ru, extra = {}) {
  /** @type {Record<string,string>} */
  const o = {};
  for (const lang of LANGS) {
    if (extra[lang]) o[lang] = extra[lang];
    else if (lang === "ru" || lang === "uk" || lang === "be") o[lang] = ru;
    else o[lang] = en;
  }
  return o;
}

function upperMap(map) {
  /** @type {Record<string,string>} */
  const o = {};
  for (const [k, v] of Object.entries(map)) o[k] = v.toLocaleUpperCase(k === "tr" ? "tr" : "en");
  return o;
}

/** @param {string} code @param {number} count @param {string} [objective] */
function req(code, count, objective = "have") {
  return {
    collectibleCode: code,
    count,
    objective,
    consume: objective === "have" || objective === "craft_have",
  };
}

/** @param {string} code @param {number} count */
function rew(code, count) {
  return { collectibleCode: code, count, objective: "have", consume: true };
}

/**
 * @param {object} p
 * @param {number} p.id
 * @param {number} p.x
 * @param {number} p.y
 * @param {string} p.nodeType
 * @param {Record<string,string>} p.description
 * @param {object[]} [p.requiredItems]
 * @param {object[]} [p.rewardItems]
 */
function node({ id, x, y, nodeType, description, requiredItems = [], rewardItems = [] }) {
  return {
    id,
    x,
    y,
    nodeType,
    description,
    requiredItems,
    rewardItems,
    consumeRequiredItems: requiredItems.some(
      (i) =>
        i.consume !== false &&
        (i.objective === "have" || i.objective === "craft_have" || !i.objective)
    ),
  };
}

function chain(ids) {
  const c = [];
  for (let i = 0; i < ids.length - 1; i++) c.push({ startNodeId: ids[i], endNodeId: ids[i + 1] });
  return c;
}

/** Linear spine + extra edge pairs [[from,to],...] */
function edges(spineIds, extra = []) {
  return [...chain(spineIds), ...extra.map(([a, b]) => ({ startNodeId: a, endNodeId: b }))];
}

function writeBranch(file, data) {
  const maxId = Math.max(...data.nodes.map((n) => n.id), -1);
  data.nextNodeId = maxId + 1;
  const p = path.join(BRANCHES, file);
  fs.writeFileSync(p, JSON.stringify(data, null, 2) + "\n", "utf8");
  console.log("wrote", file, "nodes=", data.nodes.length);
}

// ── Verified codes (VS 1.22 survival) ─────────────────────────────────────
// Tools: knife-generic-flint, axe-flint, shovel-flint, hoe-flint, spear-generic-flint
// Metal axe: axe-felling-{metal}  |  iron bloom: ironbloom  |  basket: basket-normal-reed
// Pottery: bowl-blue-raw / bowl-blue-fired, claypot-blue-raw / claypot-blue-fired,
//          crock-blue-raw / crock-blue-fired, crucible-fire-raw / crucible-fire-fired
// Planter: clayplanter-blue-raw  |  torch: torch-basic-extinct-up
// Flax harvest: flaxfibers + grain-flax  |  leather: leather-normal-plain
// Ores need grade+type+rock: ore-poor-malachite-limestone, ore-poor-cassiterite-granite, etc.

const C = {
  stick: "game:stick",
  flint: "game:flint",
  drygrass: "game:drygrass",
  firewood: "game:firewood",
  torch: "game:torch-basic-extinct-up",
  clayBlue: "game:clay-blue",
  clayFire: "game:clay-fire",
  stoneGranite: "game:stone-granite",
  stoneAny: "game:stone-*",
  resin: "game:resin",
  honeycomb: "game:honeycomb",
  beeswax: "game:beeswax",
  charcoal: "game:charcoal",
  flaxtwine: "game:flaxtwine",
  flaxfibers: "game:flaxfibers",
  rope: "game:rope",
  cattailtops: "game:cattailtops",
  cattailroot: "game:cattailroot",
  papyrustops: "game:papyrustops",
  bone: "game:bone",
  fat: "game:fat",
  feather: "game:feather",
  beeswax2: "game:beeswax",
  gearRusty: "game:gear-rusty",
  gearTemporal: "game:gear-temporal",
  copperNugget: "game:nugget-nativecopper",
  goldNugget: "game:nugget-nativegold",
  silverNugget: "game:nugget-nativesilver",
  malachite: "game:ore-poor-malachite-*",
  malachiteExact: "game:ore-poor-malachite-limestone",
  cassiterite: "game:ore-poor-cassiterite-*",
  cassiteriteExact: "game:ore-poor-cassiterite-granite",
  limonite: "game:ore-poor-limonite-*",
  limoniteExact: "game:ore-poor-limonite-shale",
  hematite: "game:ore-poor-hematite-*",
  ironbloom: "game:ironbloom",
  metalbitCu: "game:metalbit-copper",
  metalbitTin: "game:metalbit-tin",
  metalbitIron: "game:metalbit-iron",
  plateCu: "game:metalplate-copper",
  plateBronze: "game:metalplate-tinbronze",
  plateIron: "game:metalplate-iron",
  plateSteel: "game:metalplate-steel",
  ingotCu: "game:ingot-copper",
  ingotBronze: "game:ingot-tinbronze",
  ingotIron: "game:ingot-iron",
  ingotSteel: "game:ingot-steel",
  nailsIron: "game:metalnailsandstrips-iron",
  nailsCu: "game:metalnailsandstrips-copper",
  knifeBladeFlint: "game:knifeblade-flint",
  knifeFlint: "game:knife-generic-flint",
  axeFlint: "game:axe-flint",
  shovelFlint: "game:shovel-flint",
  hoeFlint: "game:hoe-flint",
  spearFlint: "game:spear-generic-flint",
  spearBronze: "game:spear-generic-tinbronze",
  spearCu: "game:spear-generic-copper",
  pickCu: "game:pickaxe-copper",
  pickBronze: "game:pickaxe-tinbronze",
  pickIron: "game:pickaxe-iron",
  pickSteel: "game:pickaxe-steel",
  axeCu: "game:axe-felling-copper",
  axeBronze: "game:axe-felling-tinbronze",
  axeIron: "game:axe-felling-iron",
  axeSteel: "game:axe-felling-steel",
  sawCu: "game:saw-copper",
  sawBronze: "game:saw-tinbronze",
  sawIron: "game:saw-iron",
  sawSteel: "game:saw-steel",
  hammerCu: "game:hammer-copper",
  hammerBronze: "game:hammer-tinbronze",
  hammerIron: "game:hammer-iron",
  hammerSteel: "game:hammer-steel",
  chiselCu: "game:chisel-copper",
  chiselIron: "game:chisel-iron",
  shovelCu: "game:shovel-copper",
  hoeCu: "game:hoe-copper",
  knifeCu: "game:knife-generic-copper",
  anvilCu: "game:anvil-copper",
  anvilBronze: "game:anvil-tinbronze",
  anvilIron: "game:anvil-iron",
  anvilSteel: "game:anvil-steel",
  arrowheadFlint: "game:arrowhead-flint",
  arrowheadCu: "game:arrowhead-copper",
  bowSimple: "game:bow-simple",
  hideRaw: "game:hide-raw-*",
  hideRawMed: "game:hide-raw-medium",
  hideScraped: "game:hide-scraped-*",
  hideSoaked: "game:hide-soaked-*",
  leather: "game:leather-normal-*",
  leatherPlain: "game:leather-normal-plain",
  redmeatCooked: "game:redmeat-cooked",
  redmeatRaw: "game:redmeat-raw",
  poultryCooked: "game:poultry-cooked",
  seedsFlax: "game:seeds-flax",
  seedsSpelt: "game:seeds-spelt",
  seedsCarrot: "game:seeds-carrot",
  grainSpelt: "game:grain-spelt",
  grainFlax: "game:grain-flax",
  flourSpelt: "game:flour-spelt",
  doughSpelt: "game:dough-spelt",
  breadSpelt: "game:bread-spelt-*",
  breadSpeltPerfect: "game:bread-spelt-perfect",
  fruitAny: "game:fruit-*",
  fruitBlueberry: "game:fruit-blueberry",
  vegCarrot: "game:vegetable-carrot",
  vegCabbage: "game:vegetable-cabbage",
  plankOak: "game:plank-oak",
  plankAny: "game:plank-*",
  basket: "game:basket-normal-reed",
  chest: "game:chest-east",
  bed: "game:bed-wood-head-north",
  ladder: "game:ladder-wood-oak-north",
  door: "game:door-sleek-windowed-oak",
  refractoryRaw: "game:refractorybrick-raw-tier1",
  torchholder: "game:torchholder-brass-empty-north",
  bowlRaw: "game:bowl-blue-raw",
  bowlFired: "game:bowl-blue-fired",
  potRaw: "game:claypot-blue-raw",
  potFired: "game:claypot-blue-fired",
  crockRaw: "game:crock-blue-raw",
  crockFired: "game:crock-blue-fired",
  crucibleRaw: "game:crucible-fire-raw",
  crucibleFired: "game:crucible-fire-fired",
  planterRaw: "game:clayplanter-blue-raw",
  planterFired: "game:clayplanter-blue-fired",
  flowerpotRaw: "game:flowerpot-blue-raw",
  mortar: "game:mortar",
  quicklime: "game:quicklime",
  lime: "game:lime",
  clearquartz: "game:clearquartz",
  beeswaxItem: "game:beeswax",
  candle: "game:candle",
  sewingkit: "game:sewingkit",
  thatch: "game:thatch",
  bamboostakes: "game:bamboostakes",
  // creatures
  hare: "game:hare-*",
  deer: "game:deer-*",
  wolf: "game:wolf-*",
  fox: "game:fox-*",
  boar: "game:pig-*",
  chicken: "game:chicken-*",
  drifter: "game:drifter-*",
  locust: "game:locust-*",
  bear: "game:bear-*",
  sheep: "game:sheep-*",
};

/** @type {{headerTitle:string,file:string,icon:string,title:Record<string,string>,header:Record<string,string>,build:()=>{nodes:any[],connections:any[]}}[]} */
const CATEGORIES = [];

function addCat(headerTitle, file, icon, titleEn, titleRu, headerEn, headerRu, build, titleExtra = {}, headerExtra = {}) {
  CATEGORIES.push({
    headerTitle,
    file,
    icon,
    title: L(titleEn, titleRu, titleExtra),
    header: upperMap(L(headerEn, headerRu, headerExtra)),
    build,
  });
}

// ═══════════════ LEGEND ═══════════════
addCat(
  "category.legend.header",
  "category.legend.header.json",
  "game:gear-rusty",
  "Legend",
  "Сказание",
  "Emberstar Path",
  "Путь угольной звезды",
  () => {
    const d = (en, ru) => L(en, ru);
    const n = [];
    let id = 0;
    const add = (opts) => {
      const nodeId = id++;
      n.push(node({ id: nodeId, ...opts }));
      return nodeId;
    };

    const s0 = add({
      x: X0, y: Y0, nodeType: "Start",
      description: d(
        "In your pocket: a scorched journal by a smith named Era. On the cover, a coal shaped like a star. “Find my path — find the last forge.”",
        "В кармане — обгоревший дневник кузнеца Эры. На обложке уголёк в форме звезды. «Кто найдёт мой путь — найдёт последнюю кузню»."
      ),
    });
    const s1 = add({
      x: X0, y: -STEP, nodeType: "Quest",
      description: d("First note: “Begin with wood. Sticks remember the wind.” Gather sticks.", "Первая запись: «Начни с дерева. Палки помнят, откуда ветер». Собери палки."),
      requiredItems: [req(C.stick, 12, "have")],
      rewardItems: [rew(C.flint, 3)],
    });
    const s2 = add({
      x: X0, y: -STEP * 2, nodeType: "Quest",
      description: d("“Grass dry as old letters.” Collect tinder. Era wrote by firelight.", "«Трава сухая, как старые письма». Собери сухую траву. Эра писала при костре."),
      requiredItems: [req(C.drygrass, 16, "have")],
      rewardItems: [rew(C.firewood, 6)],
    });
    const s3 = add({
      x: X0, y: -STEP * 3, nodeType: "Quest",
      description: d("“Fire is the only witness.” Stock firewood so the journal won’t shiver.", "«Огонь — единственный свидетель». Запаси дрова."),
      requiredItems: [req(C.firewood, 24, "have")],
      rewardItems: [rew(C.torch, 6)],
    });
    const s4 = add({
      x: X0, y: -STEP * 4, nodeType: "Quest",
      description: d("Craft basic torches. Night is a room without a door.", "Скрафти факелы. Ночь — комната без двери."),
      requiredItems: [req(C.torch, 8, "craft_have")],
      rewardItems: [rew(C.flint, 4)],
    });
    const cp1 = add({
      x: X0, y: -STEP * 5, nodeType: "Checkpoint",
      description: d("Your camp holds. A map bleeds through the page: stone → clay → copper…", "Стоянка держится. На полях дневника: камень → глина → медь…"),
    });
    const s5 = add({
      x: STEP, y: -STEP * 5, nodeType: "Quest",
      description: d("“Flint cuts the dark.” Find flint — Era sharpened blades and thoughts alike.", "«Кремень режет тьму». Найди кремень."),
      requiredItems: [req(C.flint, 16, "have")],
      rewardItems: [rew(C.stick, 12)],
    });
    const s6 = add({
      x: STEP * 2, y: -STEP * 5, nodeType: "Quest",
      description: d("Loose stones for knapping. Strike stone on stone.", "Камни для оббивки. Ударь камень о камень."),
      requiredItems: [req(C.stoneAny, 24, "have")],
      rewardItems: [rew(C.flint, 4)],
    });
    const s7 = add({
      x: STEP * 2, y: -STEP * 6, nodeType: "Quest",
      description: d("“Clay listens to fire better than people.” Bring blue clay.", "«Глина слушает огонь лучше, чем люди». Принеси синюю глину."),
      requiredItems: [req(C.clayBlue, 20, "have")],
      rewardItems: [rew(C.firewood, 12)],
    });
    const s8 = add({
      x: STEP * 2, y: -STEP * 7, nodeType: "Quest",
      description: d("Fire clay for heat that shatters ordinary pots.", "Огнеупорная глина — для жара, что ломает простые горшки."),
      requiredItems: [req(C.clayFire, 12, "have")],
      rewardItems: [rew(C.clayBlue, 8)],
    });
    const s9 = add({
      x: STEP * 3, y: -STEP * 7, nodeType: "Quest",
      description: d("“Copper is the earth’s blood.” Gather native copper nuggets.", "«Медь — кровь земли». Найди медные самородки."),
      requiredItems: [req(C.copperNugget, 24, "have")],
      rewardItems: [rew(C.clayFire, 8)],
    });
    const s10 = add({
      x: STEP * 3, y: -STEP * 8, nodeType: "Quest",
      description: d("Resin notes: “Conifers remember the storm.” Collect resin.", "На полях смолы: «Хвойные помнят бурю». Собери смолу."),
      requiredItems: [req(C.resin, 10, "have")],
      rewardItems: [rew(C.flaxtwine, 4)],
    });
    const s11 = add({
      x: STEP * 4, y: -STEP * 8, nodeType: "Quest",
      description: d("“Bees keep summer in comb.” Honeycomb — light when ore is scarce.", "«Пчёлы хранят лето в сотах». Соты — свет, когда руды мало."),
      requiredItems: [req(C.honeycomb, 6, "have")],
      rewardItems: [rew(C.beeswax, 3)],
    });
    const s12 = add({
      x: STEP * 4, y: -STEP * 9, nodeType: "Quest",
      description: d("Twine binds the path. Without thread, all unravels.", "Бечёвка связывает путь. Без нити всё распадается."),
      requiredItems: [req(C.flaxtwine, 10, "have")],
      rewardItems: [rew(C.stick, 16)],
    });
    const s13 = add({
      x: STEP * 4, y: -STEP * 10, nodeType: "Quest",
      description: d("Among roots: a rusty gear. Era marked it with an ember star.", "Между корней — ржавая шестерёнка. Эра пометила её угольной звездой."),
      requiredItems: [req(C.gearRusty, 1, "detect")],
      rewardItems: [rew(C.flaxtwine, 6)],
    });
    const s14 = add({
      x: STEP * 4, y: -STEP * 11, nodeType: "Quest",
      description: d("“Charcoal is the forge’s black sun.” Craft charcoal.", "«Уголь — чёрное солнце кузни». Скрафти древесный уголь."),
      requiredItems: [req(C.charcoal, 32, "craft")],
      rewardItems: [rew(C.firewood, 20)],
    });
    const cp2 = add({
      x: STEP * 4, y: -STEP * 12, nodeType: "Checkpoint",
      description: d("The legend thickens. You are following Era, not only surviving.", "Сказание густеет. Ты идёшь по следам Эры, а не просто выживаешь."),
    });
    const s15 = add({
      x: STEP * 5, y: -STEP * 12, nodeType: "Quest",
      description: d("More charcoal — iron will not wake without heat.", "Ещё уголь — без него железо не проснётся."),
      requiredItems: [req(C.charcoal, 48, "have")],
      rewardItems: [rew(C.clayFire, 8)],
    });
    const s16 = add({
      x: STEP * 5, y: -STEP * 13, nodeType: "Quest",
      description: d("Find iron ore. Dig where the stone is heavy and red.", "Найди железную руду. Копай, где камень тяжёл и красен."),
      requiredItems: [req(C.limonite, 12, "have")],
      rewardItems: [rew(C.charcoal, 16)],
    });
    const s17 = add({
      x: STEP * 5, y: -STEP * 14, nodeType: "Quest",
      description: d("Iron bloom from the bloomery. Ugly metal that will become honest.", "Сыродутная крица. Уродливый металл, что станет честным."),
      requiredItems: [req(C.ironbloom, 2, "have")],
      rewardItems: [rew(C.charcoal, 16)],
    });
    const s18 = add({
      x: STEP * 5, y: -STEP * 15, nodeType: "Quest",
      description: d("“When iron sings on the anvil, return to the gears.” Bring an iron ingot.", "«Когда железо запоёт на наковальне — вернись к шестерням». Принеси железный слиток."),
      requiredItems: [req(C.ingotIron, 2, "have")],
      rewardItems: [rew(C.gearRusty, 1)],
    });
    const s19 = add({
      x: STEP * 5, y: -STEP * 16, nodeType: "Quest",
      description: d("More gears from the ruins. Era believed: gather them and hear the hum under the sky.", "Ещё шестерни из руин. Эра верила: собрав их, услышишь гул под небом."),
      requiredItems: [req(C.gearRusty, 4, "have")],
      rewardItems: [rew(C.ingotIron, 1)],
    });
    const s20 = add({
      x: STEP * 6, y: -STEP * 16, nodeType: "Quest",
      description: d("A temporal gear if fate allows — proof the old machines still breathe.", "Временная шестерня, если судьба позволит — доказательство, что старые машины ещё дышат."),
      requiredItems: [req(C.gearTemporal, 1, "detect")],
      rewardItems: [rew(C.gearRusty, 2)],
    });
    const s21 = add({
      x: STEP * 6, y: -STEP * 17, nodeType: "Quest",
      description: d("Steel if you can — or more iron. The last forge asks for the hard metal.", "Сталь, если сумеешь — или ещё железо. Последняя кузня просит твёрдый металл."),
      requiredItems: [req(C.ingotSteel, 1, "have")],
      rewardItems: [rew(C.ingotIron, 2)],
    });
    const end = add({
      x: STEP * 6, y: -STEP * 18, nodeType: "Checkpoint",
      description: d("The legend’s end is open. The ember star burns for whoever finished the journal.", "Финал Сказания открыт. Угольная звезда горит для того, кто дочитал дневник."),
    });

    // Side branch: early hunting token
    const side1 = add({
      x: -STEP, y: -STEP * 5, nodeType: "Quest",
      description: d("Margin note: “Bones teach more than books.” Bring bones from the hunt.", "На полях: «Кости учат лучше книг». Принеси кости с охоты."),
      requiredItems: [req(C.bone, 8, "have")],
      rewardItems: [rew(C.fat, 2)],
    });
    const side2 = add({
      x: -STEP, y: -STEP * 6, nodeType: "Quest",
      description: d("Fat for light and tanning. Soft strength of the wild.", "Жир для света и выделки. Мягкая сила дикой природы."),
      requiredItems: [req(C.fat, 4, "have")],
      rewardItems: [rew(C.flaxtwine, 4)],
    });

    return {
      nodes: n,
      connections: edges(
        [s0, s1, s2, s3, s4, cp1, s5, s6, s7, s8, s9, s10, s11, s12, s13, s14, cp2, s15, s16, s17, s18, s19, s20, s21, end],
        [[cp1, side1], [side1, side2], [side2, s7]]
      ),
    };
  },
  { de: "Legende", fr: "Légende", es: "Leyenda", pl: "Legenda", ja: "伝説", zh: "传说", it: "Leggenda", pt: "Lenda", tr: "Efsane", nl: "Legende", cs: "Legenda", sk: "Legenda", ko: "전설", vi: "Huyền thoại", hu: "Legenda", ro: "Legendă", sv: "Legend", da: "Legende", no: "Legende", fi: "Legenda", lt: "Legenda" },
  { de: "Pfad der Glutstern", fr: "Voie de l'étoile de braise", es: "Senda de la estrella de brasa", pl: "Ścieżka węglowej gwiazdy", ja: "embersターの道", zh: "余烬星之路" }
);

// ═══════════════ STONE ═══════════════
addCat(
  "category.stone.header",
  "category.stone.header.json",
  "game:stone-granite",
  "Stone Age",
  "Каменный век",
  "Stone Age",
  "Каменный век",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Stone Age — Era’s first chapter. “Who cannot live by flint does not deserve steel.”", "Каменный век — первая глава Эры. «Кто не умеет жить с кремнем, не достоин стали».") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Gather sticks. Handles, torches and spears begin in the woods.", "Собери палки. Рукояти, факелы и копья рождаются из леса."), requiredItems: [req(C.stick, 20, "have")], rewardItems: [rew(C.stoneGranite, 6)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Loose stones for knapping. Strike stone on stone — Era’s first lesson.", "Камни для оббивки. Ударь камень о камень — так учила Эра."), requiredItems: [req(C.stoneAny, 28, "have")], rewardItems: [rew(C.flint, 4)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Dry grass is the fire’s breath. Without it, flame stays a dream.", "Сухая трава — дыхание огня. Без неё пламя остаётся сном."), requiredItems: [req(C.drygrass, 20, "have")], rewardItems: [rew(C.firewood, 10)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Flint is king among stones. Stock it while the land is kind.", "Кремень — король камней. Запаси, пока земля добра."), requiredItems: [req(C.flint, 20, "have")], rewardItems: [rew(C.stick, 12)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Craft a flint knife blade — the world’s first honest edge.", "Скрафти лезвие кремнёвого ножа — первый честный край мира."), requiredItems: [req(C.knifeBladeFlint, 1, "craft")], rewardItems: [rew(C.flaxtwine, 2)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Finish a flint knife. Cut grass, meat, cord — and fear.", "Собери кремнёвый нож. Режь траву, мясо, верёвку — и страх."), requiredItems: [req(C.knifeFlint, 1, "craft_have")], rewardItems: [rew(C.drygrass, 10)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Craft a flint axe. The forest becomes supply, not a wall.", "Скрафти кремнёвый топор. Лес станет складом, а не стеной."), requiredItems: [req(C.axeFlint, 1, "craft")], rewardItems: [rew(C.firewood, 16)] }),
      node({ id: 8, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Stock firewood after the first cuts. Night loves an ember.", "Запаси дрова после первых порубок. Ночь любит уголёк."), requiredItems: [req(C.firewood, 40, "have")], rewardItems: [rew(C.torch, 8)] }),
      node({ id: 9, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("A shovel. Clay, soil, beds — all wait for your strike.", "Лопата. Глина, земля, грядки — всё ждёт твоего удара."), requiredItems: [req(C.shovelFlint, 1, "craft_have")], rewardItems: [rew(C.clayBlue, 6)] }),
      node({ id: 10, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("A hoe. Even stone can promise a harvest.", "Мотыга. Даже камень может обещать урожай."), requiredItems: [req(C.hoeFlint, 1, "craft")], rewardItems: [rew(C.cattailtops, 10)] }),
      node({ id: 11, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("A spear. Distance between you and teeth.", "Копьё. Дистанция между тобой и клыками."), requiredItems: [req(C.spearFlint, 1, "craft_have")], rewardItems: [rew(C.bone, 4)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Craft torches. Carry light into the dark.", "Скрафти факелы. Неси свет во тьму."), requiredItems: [req(C.torch, 10, "craft")], rewardItems: [rew(C.flint, 4)] }),
      node({ id: 13, x: 0, y: -STEP * 5, nodeType: "Quest", description: d("Twine binds the path. Without thread, all unravels.", "Бечёвка связывает путь. Без нити всё распадается."), requiredItems: [req(C.flaxtwine, 10, "have")], rewardItems: [rew(C.stick, 16)] }),
      node({ id: 14, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("A reed hand basket. Pockets end before the road does.", "Плетёная корзина. Карманы кончаются раньше, чем дорога."), requiredItems: [req(C.basket, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 15, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("Cattail tops for weaving. The marsh feeds those who look.", "Верхушки рогоза для плетения. Болото кормит внимательных."), requiredItems: [req(C.cattailtops, 20, "have")], rewardItems: [rew(C.cattailroot, 10)] }),
      node({ id: 16, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("Cattail roots — food and fiber from the water’s edge.", "Корни рогоза — еда и волокно с кромки воды."), requiredItems: [req(C.cattailroot, 16, "have")], rewardItems: [rew(C.cattailtops, 8)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("More flint for spare blades. Tools break — stockpiles don’t argue.", "Ещё кремень на запасные лезвия. Инструменты ломаются — запасы не спорят."), requiredItems: [req(C.flint, 24, "have")], rewardItems: [rew(C.stick, 12)] }),
      node({ id: 18, x: STEP * 4, y: -STEP * 6, nodeType: "Quest", description: d("Craft a second flint knife. Redundancy is wisdom.", "Скрафти второй кремнёвый нож. Запас — это мудрость."), requiredItems: [req(C.knifeFlint, 2, "craft")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 19, x: STEP * 3, y: -STEP * 6, nodeType: "Quest", description: d("Resin from the pines. Glue for what stone cannot hold.", "Смола с сосен. Клей для того, что камень не удержит."), requiredItems: [req(C.resin, 8, "have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 20, x: STEP * 2, y: -STEP * 6, nodeType: "Quest", description: d("Bones from small game. Tips, glue, and memory.", "Кости с мелкой дичи. Наконечники, клей и память."), requiredItems: [req(C.bone, 12, "have")], rewardItems: [rew(C.fat, 2)] }),
      node({ id: 21, x: STEP, y: -STEP * 6, nodeType: "Quest", description: d("Dry grass again — winter will take what you leave unburned.", "Снова сухая трава — зима заберёт то, что не сожжёшь."), requiredItems: [req(C.drygrass, 32, "have")], rewardItems: [rew(C.firewood, 12)] }),
      node({ id: 22, x: 0, y: -STEP * 6, nodeType: "Quest", description: d("Thatch if you can, or more dry grass. Roofs begin soft.", "Тростник/сено, если есть — или ещё сухая трава. Крыши начинаются мягко."), requiredItems: [req(C.thatch, 16, "have")], rewardItems: [rew(C.stick, 16)] }),
      node({ id: 23, x: 0, y: -STEP * 7, nodeType: "Checkpoint", description: d("Stone Age sealed. Era would nod: “Now clay.”", "Каменный век закрыт. Эра кивнула бы: «Теперь глина».") }),
      // side: archery prep
      node({ id: 24, x: STEP * 5, y: -STEP * 3, nodeType: "Quest", description: d("Feathers for arrows. Flight needs soft wings.", "Перья для стрел. Полёту нужны мягкие крылья."), requiredItems: [req(C.feather, 12, "have")], rewardItems: [rew(C.stick, 8)] }),
      node({ id: 25, x: STEP * 5, y: -STEP * 4, nodeType: "Quest", description: d("Craft flint arrowheads. Teeth for silent flight.", "Скрафти кремнёвые наконечники. Зубья для тихого полёта."), requiredItems: [req(C.arrowheadFlint, 12, "craft")], rewardItems: [rew(C.feather, 6)] }),
      node({ id: 26, x: STEP * 5, y: -STEP * 5, nodeType: "Quest", description: d("A simple bow. Era’s quiet hunt begins here.", "Простой лук. Тихая охота Эры начинается здесь."), requiredItems: [req(C.bowSimple, 1, "craft_have")], rewardItems: [rew(C.arrowheadFlint, 8)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23],
        [[7, 24], [24, 25], [25, 26], [26, 15]]
      ),
    };
  },
  { de: "Steinzeit", fr: "Âge de pierre", es: "Edad de piedra", pl: "Epoka kamienia", ja: "石器時代", zh: "石器时代", it: "Età della pietra", pt: "Idade da Pedra", tr: "Taş Devri", ko: "석기 시대" }
);

// ═══════════════ CLAY ═══════════════
addCat(
  "category.clay.header",
  "category.clay.header.json",
  "game:clay-blue",
  "Pottery",
  "Гончарное дело",
  "Pottery",
  "Гончарное дело",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Clay is chapter two. “Metal without a vessel is rain without a jug.”", "Глина — вторая глава. «Металл без сосуда — дождь без кувшина».") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Blue clay by water. Dig with patience — Era hated haste.", "Синяя глина у воды. Копай терпеливо — Эра ненавидела спешку."), requiredItems: [req(C.clayBlue, 32, "have")], rewardItems: [rew(C.firewood, 12)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Fire clay for heat that shatters ordinary pots.", "Огнеупорная глина — для жара, что ломает простые горшки."), requiredItems: [req(C.clayFire, 20, "have")], rewardItems: [rew(C.clayBlue, 10)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Shape raw bowls before the fire claims them.", "Сформируй сырые миски, пока огонь не забрал их."), requiredItems: [req(C.bowlRaw, 4, "craft")], rewardItems: [rew(C.clayBlue, 6)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Raw cooking pots. Winter soups begin here.", "Сырые котлы. Зимние похлёбки начинаются здесь."), requiredItems: [req(C.potRaw, 2, "craft")], rewardItems: [rew(C.firewood, 16)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Raw crocks. Stored food is a quiet army.", "Сырые крынки. Запасы еды — тихая армия."), requiredItems: [req(C.crockRaw, 2, "craft")], rewardItems: [rew(C.clayFire, 6)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Raw crucibles of fire clay. Without them copper never becomes an ingot.", "Сырые тигли из огнеупорной глины. Без них медь не станет слитком."), requiredItems: [req(C.crucibleRaw, 2, "craft")], rewardItems: [rew(C.charcoal, 10)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Firewood for the pit kiln — forge of pauper and genius.", "Дрова для ямной печи — кузня бедняка и гения."), requiredItems: [req(C.firewood, 48, "have")], rewardItems: [rew(C.drygrass, 20)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("Fired bowls. Fire grants them the right to hold food.", "Обожжённые миски. Огонь даёт им право держать еду."), requiredItems: [req(C.bowlFired, 4, "have")], rewardItems: [rew(C.flaxtwine, 3)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("Fired pots. True cooking begins.", "Обожжённые котлы. Настоящая готовка начинается."), requiredItems: [req(C.potFired, 2, "have")], rewardItems: [rew(C.firewood, 10)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("Fired crocks. Meals will outlast the road.", "Обожжённые крынки. Еда переживёт дорогу."), requiredItems: [req(C.crockFired, 2, "have")], rewardItems: [rew(C.clayBlue, 10)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("Fired crucibles. Open the door to copper.", "Обожжённые тигли. Открой дверь к меди."), requiredItems: [req(C.crucibleFired, 2, "have")], rewardItems: [rew(C.copperNugget, 6)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("A raw planter. Even smiths kept green by the door.", "Сырое кашпо. Даже кузнецы держали зелень у двери."), requiredItems: [req(C.planterRaw, 1, "craft")], rewardItems: [rew(C.clayBlue, 6)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Fired planter. Roots need a home that remembers the kiln.", "Обожжённое кашпо. Корням нужен дом, помнящий печь."), requiredItems: [req(C.planterFired, 1, "have")], rewardItems: [rew(C.seedsFlax, 4)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("More blue clay for spare vessels. Breakage is a tax of craft.", "Ещё синяя глина на запасные сосуды. Бой — налог ремесла."), requiredItems: [req(C.clayBlue, 40, "have")], rewardItems: [rew(C.firewood, 16)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("Shape extra bowls for the table and the road.", "Сформируй ещё миски для стола и дороги."), requiredItems: [req(C.bowlRaw, 6, "craft")], rewardItems: [rew(C.clayBlue, 8)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Fire the extra bowls. Stack them like quiet victories.", "Обожги запасные миски. Сложи их как тихие победы."), requiredItems: [req(C.bowlFired, 6, "have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Charcoal for hotter firings later. Prepare the black sun.", "Уголь для более горячих обжигов. Готовь чёрное солнце."), requiredItems: [req(C.charcoal, 24, "craft")], rewardItems: [rew(C.firewood, 16)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("One more raw crucible. The forge path never has enough vessels.", "Ещё один сырой тигель. У кузнечного пути никогда не бывает лишних сосудов."), requiredItems: [req(C.crucibleRaw, 1, "craft")], rewardItems: [rew(C.charcoal, 8)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("Fired crucible ready for copper. The metal age knocks.", "Обожжённый тигель готов к меди. Век металла стучится."), requiredItems: [req(C.crucibleFired, 1, "have")], rewardItems: [rew(C.copperNugget, 8)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Checkpoint", description: d("Pottery chapter closed. Vessels ready — metal may come.", "Глава о глине закрыта. Сосуды готовы — металл может прийти.") }),
      node({ id: 21, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("Red clay if the land offers it — color is also a craft.", "Красная глина, если земля даст — цвет тоже ремесло."), requiredItems: [req("game:clay-red", 16, "have")], rewardItems: [rew(C.clayBlue, 8)] }),
      node({ id: 22, x: -STEP, y: -STEP * 3, nodeType: "Quest", description: d("Raw flowerpot. Small green for the windowsill of the forge.", "Сырой цветочный горшок. Маленькая зелень для подоконника кузни."), requiredItems: [req(C.flowerpotRaw, 1, "craft")], rewardItems: [rew(C.clayBlue, 4)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
        [[2, 21], [21, 22], [22, 8]]
      ),
    };
  },
  { de: "Töpferei", fr: "Poterie", es: "Alfarería", pl: "Garncarstwo", ja: "土器", zh: "制陶", it: "Ceramica", pt: "Cerâmica", tr: "Çömlekçilik", ko: "도기" }
);

// ═══════════════ COPPER ═══════════════
addCat(
  "category.copper.header",
  "category.copper.header.json",
  "game:ingot-copper",
  "Copper Age",
  "Медный век",
  "Copper Age",
  "Медный век",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Copper is earth’s soft blood. “Who smelts, rules the night.”", "Медь — мягкая кровь земли. «Кто плавит, тот правит ночью».") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Native copper nuggets. Pick them where the earth blushed green.", "Самородки меди. Собери там, где земля позеленела."), requiredItems: [req(C.copperNugget, 48, "have")], rewardItems: [rew(C.clayFire, 10)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Malachite. Green promise of more metal below.", "Малахит. Зелёное обещание металла внизу."), requiredItems: [req(C.malachite, 16, "have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Charcoal for the crucible. Heat is a tool, not a guest.", "Уголь для тигля. Жар — инструмент, не гость."), requiredItems: [req(C.charcoal, 56, "craft")], rewardItems: [rew(C.firewood, 20)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Fired crucibles ready. Vessels for the first metal.", "Обожжённые тигли. Сосуды для первого металла."), requiredItems: [req(C.crucibleFired, 2, "have")], rewardItems: [rew(C.charcoal, 12)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Copper bits from the crucible. Soft metal, hard lesson.", "Куски меди из тигля. Мягкий металл, жёсткий урок."), requiredItems: [req(C.metalbitCu, 40, "have")], rewardItems: [rew(C.charcoal, 10)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Pour copper ingots. The age of pure metal begins.", "Отлей медные слитки. Век чистого металла начинается."), requiredItems: [req(C.ingotCu, 4, "craft_have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Copper plates for tools that outlast flint.", "Медные пластины для инструментов твёрже кремня."), requiredItems: [req(C.plateCu, 4, "craft")], rewardItems: [rew(C.ingotCu, 1)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("A copper pick. The mine opens to those who paid in sweat.", "Медная кирка. Шахта открывается тем, кто платил потом."), requiredItems: [req(C.pickCu, 1, "craft_have")], rewardItems: [rew(C.malachiteExact, 4)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("A copper felling axe. Timber falls cleaner for the forge.", "Медный валочный топор. Лес падает чище ради кузни."), requiredItems: [req(C.axeCu, 1, "craft")], rewardItems: [rew(C.firewood, 20)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("A copper saw. Boards for chests, doors, and future roofs.", "Медная пила. Доски для сундуков, дверей и будущих крыш."), requiredItems: [req(C.sawCu, 1, "craft_have")], rewardItems: [rew(C.plankOak, 12)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("A copper hammer. Anvil work starts with an honest blow.", "Медный молот. Работа на наковальне начинается с честного удара."), requiredItems: [req(C.hammerCu, 1, "craft")], rewardItems: [rew(C.ingotCu, 1)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("A copper chisel. Detail is also power.", "Медное зубило. Деталь — тоже сила."), requiredItems: [req(C.chiselCu, 1, "craft_have")], rewardItems: [rew(C.metalbitCu, 12)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("A copper shovel. Dig faster than flint ever allowed.", "Медная лопата. Копай быстрее, чем позволял кремень."), requiredItems: [req(C.shovelCu, 1, "craft")], rewardItems: [rew(C.clayBlue, 12)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("A copper hoe. Fields respect metal edges.", "Медная мотыга. Поля уважают металлическую кромку."), requiredItems: [req(C.hoeCu, 1, "craft_have")], rewardItems: [rew(C.seedsSpelt, 8)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("A copper knife. Soft metal, sharp enough for the table.", "Медный нож. Мягкий металл, достаточно острый для стола."), requiredItems: [req(C.knifeCu, 1, "craft")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("A copper spear. Distance again — cleaner than flint.", "Медное копьё. Снова дистанция — чище кремня."), requiredItems: [req(C.spearCu, 1, "craft_have")], rewardItems: [rew(C.fat, 4)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Copper nails and strips. Invisible bones of wooden work.", "Медные гвозди и полосы. Невидимые кости деревянной работы."), requiredItems: [req(C.nailsCu, 24, "craft")], rewardItems: [rew(C.plankOak, 12)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("More copper ingots for the next age’s alloy.", "Ещё медные слитки для сплава следующего века."), requiredItems: [req(C.ingotCu, 8, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("Oak planks from the saw. Walls wait in every trunk.", "Дубовые доски с пилы. Стены ждут в каждом стволе."), requiredItems: [req(C.plankOak, 40, "craft")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("A copper anvil if you can cast one — the workshop’s heart.", "Медная наковальня, если отольёшь — сердце мастерской."), requiredItems: [req(C.anvilCu, 1, "craft_have")], rewardItems: [rew(C.ingotCu, 2)] }),
      node({ id: 21, x: 0, y: -STEP * 5, nodeType: "Checkpoint", description: d("Copper Age complete. Tin waits — and with it, bronze.", "Медный век завершён. Ждёт олово — и с ним бронза.") }),
      node({ id: 22, x: STEP * 5, y: -STEP * 2, nodeType: "Quest", description: d("Arrowheads of copper. The hunt learns metal.", "Медные наконечники. Охота учит металл."), requiredItems: [req(C.arrowheadCu, 12, "craft")], rewardItems: [rew(C.feather, 8)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
        [[6, 22], [22, 10]]
      ),
    };
  },
  { de: "Kupferzeit", fr: "Âge du cuivre", es: "Edad del cobre", pl: "Epoka miedzi", ja: "銅器時代", zh: "铜器时代", it: "Età del rame", pt: "Idade do Cobre", tr: "Bakır Çağı", ko: "동기 시대" }
);

// ═══════════════ BRONZE ═══════════════
addCat(
  "category.bronze.header",
  "category.bronze.header.json",
  "game:ingot-tinbronze",
  "Bronze Age",
  "Бронзовый век",
  "Bronze Age",
  "Бронзовый век",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Bronze is copper that learned discipline. Tin is its tutor.", "Бронза — медь, познавшая дисциплину. Олово — её наставник.") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Cassiterite. The rare tutor of bronze.", "Касситерит. Редкий наставник бронзы."), requiredItems: [req(C.cassiterite, 14, "have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Tin metal bits. Small, but they change everything.", "Куски олова. Малы, но меняют всё."), requiredItems: [req(C.metalbitTin, 24, "have")], rewardItems: [rew(C.ingotCu, 2)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Stock copper for the alloy. Tin alone is only a whisper.", "Запаси медь для сплава. Олово одно — только шёпот."), requiredItems: [req(C.ingotCu, 8, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Charcoal for alloy heat. Two metals, one hotter will.", "Уголь для жара сплава. Два металла — одна более горячая воля."), requiredItems: [req(C.charcoal, 64, "craft")], rewardItems: [rew(C.firewood, 20)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Alloy tin bronze. Two metals, one harder will.", "Сплавь оловянную бронзу. Два металла — одна твёрдая воля."), requiredItems: [req(C.ingotBronze, 6, "craft_have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Bronze plates for tools that shame copper.", "Бронзовые пластины для инструментов, стыдящих медь."), requiredItems: [req(C.plateBronze, 4, "craft")], rewardItems: [rew(C.ingotBronze, 1)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("A bronze pick. Deeper shafts, faster ore.", "Бронзовая кирка. Глубже шахты, быстрее руда."), requiredItems: [req(C.pickBronze, 1, "craft_have")], rewardItems: [rew(C.cassiteriteExact, 4)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("A bronze axe. The forest yields boards for a real house.", "Бронзовый топор. Лес даёт доски для настоящего дома."), requiredItems: [req(C.axeBronze, 1, "craft")], rewardItems: [rew(C.plankOak, 20)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("A bronze saw. Precision is a kind of mercy.", "Бронзовая пила. Точность — тоже милосердие."), requiredItems: [req(C.sawBronze, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("A bronze hammer. Anvils respect bronze more than copper.", "Бронзовый молот. Наковальни уважают бронзу больше меди."), requiredItems: [req(C.hammerBronze, 1, "craft")], rewardItems: [rew(C.ingotBronze, 1)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("A bronze spear. Distance again — but cleaner.", "Бронзовое копьё. Снова дистанция — но чище."), requiredItems: [req(C.spearBronze, 1, "craft_have")], rewardItems: [rew(C.fat, 6)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Bronze anvil. The workshop stops being temporary.", "Бронзовая наковальня. Мастерская перестаёт быть временной."), requiredItems: [req(C.anvilBronze, 1, "craft_have")], rewardItems: [rew(C.ingotBronze, 2)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("More bronze ingots for armor and tools to come.", "Ещё бронзовые слитки для будущих доспехов и инструментов."), requiredItems: [req(C.ingotBronze, 10, "have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("Planks for a real roof over the anvil.", "Доски для настоящей крыши над наковальней."), requiredItems: [req(C.plankAny, 48, "have")], rewardItems: [rew(C.nailsCu, 16)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("More cassiterite for the next pours. Tin is never enough.", "Ещё касситерита для следующих отливок. Олова никогда не бывает достаточно."), requiredItems: [req(C.cassiterite, 10, "have")], rewardItems: [rew(C.ingotCu, 2)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Fired crucibles ready for another alloy day.", "Обожжённые тигли готовы к новому дню сплавов."), requiredItems: [req(C.crucibleFired, 2, "have")], rewardItems: [rew(C.charcoal, 12)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Gold or silver nuggets if fortune smiles — ornaments of a richer age.", "Золотые или серебряные самородки, если повезёт — украшения богатого века."), requiredItems: [req(C.goldNugget, 4, "have")], rewardItems: [rew(C.ingotBronze, 1)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Checkpoint", description: d("Bronze Age complete. Iron still sleeps in the rock.", "Бронзовый век завершён. Железо ещё спит в породе.") }),
      node({ id: 19, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("Extra charcoal stockpile. Alloy days burn hot and long.", "Запас угля. Дни сплавов горят жарко и долго."), requiredItems: [req(C.charcoal, 80, "have")], rewardItems: [rew(C.firewood, 24)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
        [[4, 19], [19, 5]]
      ),
    };
  },
  { de: "Bronzezeit", fr: "Âge du bronze", es: "Edad del bronce", pl: "Epoka brązu", ja: "青銅器時代", zh: "青铜时代", it: "Età del bronzo", pt: "Idade do Bronze", tr: "Tunç Çağı", ko: "청동기 시대" }
);

// ═══════════════ IRON ═══════════════
addCat(
  "category.iron.header",
  "category.iron.header.json",
  "game:ingot-iron",
  "Iron Age",
  "Железный век",
  "Iron Age",
  "Железный век",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Iron is stubborn. Charcoal and patience are its language.", "Железо упрямо. Уголь и терпение — его язык.") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Limonite. Dig where the stone is heavy and yellow-red.", "Лимонит. Копай, где камень тяжёл и жёлто-красен."), requiredItems: [req(C.limonite, 24, "have")], rewardItems: [rew(C.charcoal, 28)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Hematite if you find it — another face of iron.", "Гематит, если найдёшь — другое лицо железа."), requiredItems: [req(C.hematite, 12, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("More charcoal. Iron drinks heat without thanks.", "Ещё уголь. Железо пьёт жар без благодарности."), requiredItems: [req(C.charcoal, 80, "craft")], rewardItems: [rew(C.firewood, 28)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Fire clay and vessels ready for bloomery work.", "Огнеупорная глина и сосуды готовы к сыродутне."), requiredItems: [req(C.clayFire, 24, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Iron blooms. Ugly metal that will become honest.", "Железные крицы. Уродливый металл, что станет честным."), requiredItems: [req(C.ironbloom, 3, "have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Iron ingots. Hammer the bloom until it remembers shape.", "Железные слитки. Куй крицу, пока она не вспомнит форму."), requiredItems: [req(C.ingotIron, 6, "craft_have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Iron plates for tools that outlive bronze.", "Железные пластины для инструментов дольше бронзы."), requiredItems: [req(C.plateIron, 4, "craft")], rewardItems: [rew(C.ingotIron, 1)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("An iron pick. The deep rock finally answers.", "Железная кирка. Глубокая порода наконец отвечает."), requiredItems: [req(C.pickIron, 1, "craft_have")], rewardItems: [rew(C.limoniteExact, 8)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("An iron axe. Houses grow from forests you fell.", "Железный топор. Дома растут из леса, что ты валишь."), requiredItems: [req(C.axeIron, 1, "craft")], rewardItems: [rew(C.plankOak, 28)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("An iron hammer. Smithing becomes real work.", "Железный молот. Кузнечество становится настоящей работой."), requiredItems: [req(C.hammerIron, 1, "craft_have")], rewardItems: [rew(C.ingotIron, 1)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("An iron saw. Boards for the lasting house.", "Железная пила. Доски для долгого дома."), requiredItems: [req(C.sawIron, 1, "craft")], rewardItems: [rew(C.plankOak, 16)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("An iron chisel. Detail survives the ages.", "Железное зубило. Деталь переживает века."), requiredItems: [req(C.chiselIron, 1, "craft_have")], rewardItems: [rew(C.metalbitIron, 12)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("An iron anvil. The heart of a lasting forge.", "Железная наковальня. Сердце долгой кузни."), requiredItems: [req(C.anvilIron, 1, "craft_have")], rewardItems: [rew(C.ingotIron, 2)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("Iron nails. Invisible, but every house leans on them.", "Железные гвозди. Невидимы, но каждый дом на них держится."), requiredItems: [req(C.nailsIron, 40, "craft")], rewardItems: [rew(C.plankOak, 20)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("More iron blooms for the next generation of tools.", "Ещё криц для следующего поколения инструментов."), requiredItems: [req(C.ironbloom, 4, "have")], rewardItems: [rew(C.charcoal, 24)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("A stock of iron ingots. Surplus is safety.", "Запас железных слитков. Избыток — это безопасность."), requiredItems: [req(C.ingotIron, 12, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Rusty gears as tribute to Era — iron of the past and present.", "Ржавые шестерни как дань Эре — железо прошлого и настоящего."), requiredItems: [req(C.gearRusty, 3, "have")], rewardItems: [rew(C.ingotIron, 1)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Checkpoint", description: d("Iron Age complete. Steel is iron that refused to stay soft.", "Железный век завершён. Сталь — железо, отказавшееся быть мягким.") }),
      node({ id: 19, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("Firewood mountain for continuous bloomery fires.", "Гора дров для непрерывных сыродутных огней."), requiredItems: [req(C.firewood, 64, "have")], rewardItems: [rew(C.charcoal, 16)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18],
        [[3, 19], [19, 4]]
      ),
    };
  },
  { de: "Eisenzeit", fr: "Âge du fer", es: "Edad del hierro", pl: "Epoka żelaza", ja: "鉄器時代", zh: "铁器时代", it: "Età del ferro", pt: "Idade do Ferro", tr: "Demir Çağı", ko: "철기 시대" }
);

// ═══════════════ STEEL ═══════════════
addCat(
  "category.steel.header",
  "category.steel.header.json",
  "game:ingot-steel",
  "Steel Age",
  "Стальной век",
  "Steel Age",
  "Стальной век",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Steel is iron that endured the fire twice.", "Сталь — железо, дважды выдержавшее огонь.") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("More iron blooms. Steel begins with surplus.", "Ещё криц. Сталь начинается с избытка."), requiredItems: [req(C.ironbloom, 6, "have")], rewardItems: [rew(C.charcoal, 36)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Iron ingots ready for cementation.", "Железные слитки готовы к цементации."), requiredItems: [req(C.ingotIron, 12, "have")], rewardItems: [rew(C.charcoal, 24)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Cementation and charcoal. Heat is a teacher with a whip.", "Цементация и уголь. Жар — учитель с кнутом."), requiredItems: [req(C.charcoal, 100, "craft")], rewardItems: [rew(C.ingotIron, 2)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Steel ingots. The metal that holds an edge.", "Стальные слитки. Металл, что держит кромку."), requiredItems: [req(C.ingotSteel, 6, "craft_have")], rewardItems: [rew(C.flaxtwine, 8)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Steel plates for tools that finish the path.", "Стальные пластины для инструментов, что завершат путь."), requiredItems: [req(C.plateSteel, 4, "craft")], rewardItems: [rew(C.ingotSteel, 1)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("A steel pick. Even basalt learns respect.", "Стальная кирка. Даже базальт учится уважению."), requiredItems: [req(C.pickSteel, 1, "craft_have")], rewardItems: [rew(C.limoniteExact, 8)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("A steel axe. The forest is no longer an enemy.", "Стальной топор. Лес больше не враг."), requiredItems: [req(C.axeSteel, 1, "craft")], rewardItems: [rew(C.plankOak, 36)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("A steel saw. Boards for the last forge’s roof.", "Стальная пила. Доски для крыши последней кузни."), requiredItems: [req(C.sawSteel, 1, "craft_have")], rewardItems: [rew(C.ingotSteel, 1)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("A steel hammer. Every blow is a sentence in Era’s book.", "Стальной молот. Каждый удар — фраза в книге Эры."), requiredItems: [req(C.hammerSteel, 1, "craft")], rewardItems: [rew(C.gearRusty, 1)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("A steel anvil. The last honest workplace of the path.", "Стальная наковальня. Последнее честное место пути."), requiredItems: [req(C.anvilSteel, 1, "craft_have")], rewardItems: [rew(C.ingotSteel, 2)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("Stock more steel. The last forge never runs lean.", "Запаси ещё сталь. Последняя кузня не любит голод."), requiredItems: [req(C.ingotSteel, 8, "have")], rewardItems: [rew(C.charcoal, 24)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Temporal gear as a key-stone of the path.", "Временная шестерня — краеугольный камень пути."), requiredItems: [req(C.gearTemporal, 1, "detect")], rewardItems: [rew(C.ingotSteel, 1)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Rusty gears for the machine that hums under the sky.", "Ржавые шестерни для машины, что гудит под небом."), requiredItems: [req(C.gearRusty, 6, "have")], rewardItems: [rew(C.ingotIron, 2)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Checkpoint", description: d("Steel Age complete. The ember star burns in your forge.", "Стальной век завершён. Угольная звезда горит в твоей кузне.") }),
      node({ id: 15, x: STEP * 5, y: -STEP * 2, nodeType: "Quest", description: d("Refractory bricks for a hotter forge chamber.", "Огнеупорный кирпич для более горячей камеры кузни."), requiredItems: [req(C.refractoryRaw, 12, "craft")], rewardItems: [rew(C.clayFire, 12)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14],
        [[5, 15], [15, 8]]
      ),
    };
  },
  { de: "Stahlzeit", fr: "Âge de l'acier", es: "Edad del acero", pl: "Epoka stali", ja: "鋼鉄の時代", zh: "钢铁时代", it: "Età dell'acciaio", pt: "Idade do Aço", tr: "Çelik Çağı", ko: "강철 시대" }
);

// ═══════════════ HUNTING ═══════════════
addCat(
  "category.hunting.header",
  "category.hunting.header.json",
  "game:bow-simple",
  "Hunting",
  "Охота",
  "Hunting",
  "Охота",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Hunting. “A smith who cannot feed themselves only forges hunger.”", "Охота. «Кузнец, который не умеет добыть еду, куёт только голод».") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Craft a flint spear. First contract with distance.", "Скрафти кремнёвое копьё. Первый договор с дистанцией."), requiredItems: [req(C.spearFlint, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 3)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Kill", description: d("Hunt hares. Soft game teaches timing and silence.", "Охоться на зайцев. Мягкая дичь учит таймингу и тишине."), requiredItems: [req(C.hare, 6, "kill")], rewardItems: [rew(C.bone, 6)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Bones. Glue, tips, and memory of beasts.", "Кости. Клей, наконечники и память зверей."), requiredItems: [req(C.bone, 20, "have")], rewardItems: [rew(C.fat, 3)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Fat. Light, frying, tanning — white blood of the kill.", "Жир. Свет, жарка, выделка — белая кровь добычи."), requiredItems: [req(C.fat, 8, "have")], rewardItems: [rew(C.flaxtwine, 3)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Feathers. Arrows without them are only sticks.", "Перья. Стрелы без них — просто палки."), requiredItems: [req(C.feather, 20, "have")], rewardItems: [rew(C.stick, 12)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Craft flint arrowheads. Teeth for silent flight.", "Скрафти кремнёвые наконечники. Зубья для тихого полёта."), requiredItems: [req(C.arrowheadFlint, 20, "craft")], rewardItems: [rew(C.feather, 10)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Craft a simple bow. Era’s quiet hunt.", "Скрафти простой лук. Тихая охота Эры."), requiredItems: [req(C.bowSimple, 1, "craft_have")], rewardItems: [rew(C.arrowheadFlint, 10)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Kill", description: d("Hunt chickens or wild fowl if nearby — soft feathers, soft lessons.", "Охоться на кур или дичь — мягкие перья, мягкие уроки."), requiredItems: [req(C.chicken, 4, "kill")], rewardItems: [rew(C.feather, 12)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Kill", description: d("Hunt deer. The forest shares if you are careful.", "Охоться на оленей. Лес делится, если ты осторожен."), requiredItems: [req(C.deer, 4, "kill")], rewardItems: [rew(C.hideRawMed, 2)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("Raw hides. Beginning of leather and warmth.", "Сырые шкуры. Начало кожи и тепла."), requiredItems: [req(C.hideRaw, 8, "have")], rewardItems: [rew(C.fat, 4)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("Soaked hides. Water is the first step of patience.", "Замоченные шкуры. Вода — первый шаг терпения."), requiredItems: [req(C.hideSoaked, 4, "have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Scraped hides. Patience of the tannery.", "Соскобленные шкуры. Терпение выделки."), requiredItems: [req(C.hideScraped, 4, "have")], rewardItems: [rew(C.fat, 4)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Leather. Belts, bags, armor.", "Кожа. Ремни, сумки, броня."), requiredItems: [req(C.leather, 6, "have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Kill", description: d("Hunt wolves. Respect the pack — and the night.", "Охоться на волков. Уважай стаю — и ночь."), requiredItems: [req(C.wolf, 4, "kill")], rewardItems: [rew(C.bone, 10)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Kill", description: d("Hunt foxes. Quick prey for quick hands.", "Охоться на лис. Быстрая добыча для быстрых рук."), requiredItems: [req(C.fox, 3, "kill")], rewardItems: [rew(C.hideRawMed, 1)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Kill", description: d("Hunt wild pigs. Dangerous, but rich in fat and hide.", "Охоться на кабанов. Опасно, но жирно и шкурно."), requiredItems: [req(C.boar, 3, "kill")], rewardItems: [rew(C.fat, 6)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Raw red meat. The forest’s full plate.", "Сырое красное мясо. Полная тарелка леса."), requiredItems: [req(C.redmeatRaw, 16, "have")], rewardItems: [rew(C.fat, 2)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("Cooked red meat. Fullness writes even lines in the journal.", "Жареное мясо. Сытость пишет ровные строки в дневнике."), requiredItems: [req(C.redmeatCooked, 16, "have")], rewardItems: [rew(C.fat, 3)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("More leather for packs and straps of the long road.", "Ещё кожи для сумок и ремней дальней дороги."), requiredItems: [req(C.leatherPlain, 8, "have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("Copper arrowheads when metal arrives. Hunt upgrades with the age.", "Медные наконечники, когда придёт металл. Охота растёт вместе с веком."), requiredItems: [req(C.arrowheadCu, 12, "craft")], rewardItems: [rew(C.feather, 8)] }),
      node({ id: 21, x: 0, y: -STEP * 5, nodeType: "Kill", description: d("Optional: face a bear if your land holds them — or skip with more wolves.", "По желанию: медведь, если есть в краю — или ещё волки."), requiredItems: [req(C.bear, 1, "kill")], rewardItems: [rew(C.hideRawMed, 2)] }),
      node({ id: 22, x: 0, y: -STEP * 6, nodeType: "Checkpoint", description: d("Hunting path complete. The forest knows your name now.", "Путь охоты завершён. Лес теперь знает твоё имя.") }),
      node({ id: 23, x: STEP * 5, y: -STEP, nodeType: "Quest", description: d("Honeycomb from wild hives. Sweetness after blood.", "Соты с диких ульев. Сладость после крови."), requiredItems: [req(C.honeycomb, 4, "have")], rewardItems: [rew(C.beeswax, 2)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22],
        [[4, 23], [23, 6]]
      ),
    };
  },
  { de: "Jagd", fr: "Chasse", es: "Caza", pl: "Polowanie", ja: "狩り", zh: "狩猎", it: "Caccia", pt: "Caça", tr: "Avcılık", ko: "사냥" }
);

// ═══════════════ FARMING ═══════════════
addCat(
  "category.farming.header",
  "category.farming.header.json",
  "game:seeds-flax",
  "Farming",
  "Земледелие",
  "Farming",
  "Земледелие",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Farming. “Who plants, already fights winter.”", "Земледелие. «Кто сеет, уже воюет с зимой».") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("A hoe — even flint can open soil.", "Мотыга — даже кремень может открыть почву."), requiredItems: [req(C.hoeFlint, 1, "craft_have")], rewardItems: [rew(C.cattailtops, 10)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Flax seeds. Thread begins as a green promise.", "Семена льна. Нить начинается как зелёное обещание."), requiredItems: [req(C.seedsFlax, 20, "have")], rewardItems: [rew(C.stick, 10)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Spelt seeds. Bread is a long patience.", "Семена спельты. Хлеб — долгое терпение."), requiredItems: [req(C.seedsSpelt, 20, "have")], rewardItems: [rew(C.drygrass, 10)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Carrot seeds. Color and roots for the stew.", "Семена моркови. Цвет и корни для похлёбки."), requiredItems: [req(C.seedsCarrot, 12, "have")], rewardItems: [rew(C.seedsFlax, 4)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Harvest flax fibers. Stalks for fiber, seeds for tomorrow.", "Собери льняное волокно. Стебли на нить, семена на завтра."), requiredItems: [req(C.flaxfibers, 32, "have")], rewardItems: [rew(C.seedsFlax, 10)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Flax grain from the same harvest. Nothing is wasted.", "Зерно льна с того же урожая. Ничего не пропадает."), requiredItems: [req(C.grainFlax, 16, "have")], rewardItems: [rew(C.flaxfibers, 8)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Harvest spelt grain. The field pays in gold of another kind.", "Собери зерно спельты. Поле платит золотом другого рода."), requiredItems: [req(C.grainSpelt, 40, "have")], rewardItems: [rew(C.seedsSpelt, 10)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("Flax twine from your own field. Independence smells of dust.", "Льняная бечёвка со своего поля. Независимость пахнет пылью."), requiredItems: [req(C.flaxtwine, 20, "craft")], rewardItems: [rew(C.flaxfibers, 10)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("Rope for wells and loads. Twine’s stronger cousin.", "Верёвка для колодцев и грузов. Более сильный родич бечёвки."), requiredItems: [req(C.rope, 4, "craft")], rewardItems: [rew(C.flaxtwine, 8)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("Flour. The first soft power of civilization.", "Мука. Первая мягкая сила цивилизации."), requiredItems: [req(C.flourSpelt, 20, "craft")], rewardItems: [rew(C.grainSpelt, 10)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("Dough. Hands remember the rhythm of hunger.", "Тесто. Руки помнят ритм голода."), requiredItems: [req(C.doughSpelt, 10, "craft")], rewardItems: [rew(C.flourSpelt, 6)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Bread. Eat what you grew — Era would approve.", "Хлеб. Ешь то, что вырастил — Эра одобрила бы."), requiredItems: [req(C.breadSpelt, 10, "have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Perfect loaves if you can. Pride is edible.", "Идеальные буханки, если сумеешь. Гордость съедобна."), requiredItems: [req(C.breadSpeltPerfect, 4, "have")], rewardItems: [rew(C.seedsSpelt, 8)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("Carrots from the garden. Roots for the pot.", "Морковь с огорода. Корни для котла."), requiredItems: [req(C.vegCarrot, 24, "have")], rewardItems: [rew(C.seedsCarrot, 6)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("Cabbage if you planted it — leaves for long storage.", "Капуста, если сажал — листья для долгого хранения."), requiredItems: [req(C.vegCabbage, 12, "have")], rewardItems: [rew(C.seedsSpelt, 4)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Wild fruit. The land’s free dessert.", "Дикие плоды. Бесплатный десерт земли."), requiredItems: [req(C.fruitAny, 16, "have")], rewardItems: [rew(C.fruitBlueberry, 4)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Cattail roots. The marsh feeds when fields sleep.", "Корни рогоза. Болото кормит, когда поля спят."), requiredItems: [req(C.cattailroot, 20, "have")], rewardItems: [rew(C.cattailtops, 10)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("A clay planter. Even smiths kept green by the door.", "Глиняное кашпо. Даже кузнецы держали зелень у двери."), requiredItems: [req(C.planterRaw, 1, "craft")], rewardItems: [rew(C.clayBlue, 6)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("Fired planter. Roots need a home that remembers the kiln.", "Обожжённое кашпо. Корням нужен дом, помнящий печь."), requiredItems: [req(C.planterFired, 1, "have")], rewardItems: [rew(C.seedsFlax, 6)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("Copper hoe when metal arrives. Fields upgrade with the age.", "Медная мотыга, когда придёт металл. Поля растут вместе с веком."), requiredItems: [req(C.hoeCu, 1, "craft_have")], rewardItems: [rew(C.seedsSpelt, 12)] }),
      node({ id: 21, x: 0, y: -STEP * 5, nodeType: "Checkpoint", description: d("Farming path complete. Winter will find your stores full.", "Путь земледелия завершён. Зима найдёт твои запасы полными.") }),
      node({ id: 22, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("Honeycomb for sweet bread and candles later.", "Соты для сладкого хлеба и будущих свечей."), requiredItems: [req(C.honeycomb, 4, "have")], rewardItems: [rew(C.beeswax, 2)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
        [[3, 22], [22, 5]]
      ),
    };
  },
  { de: "Landwirtschaft", fr: "Agriculture", es: "Agricultura", pl: "Rolnictwo", ja: "農業", zh: "农业", it: "Agricoltura", pt: "Agricultura", tr: "Tarım", ko: "농업" }
);

// ═══════════════ CRAFTS ═══════════════
addCat(
  "category.crafts.header",
  "category.crafts.header.json",
  "game:flaxtwine",
  "Crafts",
  "Ремесло",
  "Crafts",
  "Ремесло",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Crafts. Hands that only mine never build a home.", "Ремесло. Руки, что только копают, не построят дом.") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Twine. Everything soft begins with a knot.", "Бечёвка. Всё мягкое начинается с узла."), requiredItems: [req(C.flaxtwine, 16, "craft")], rewardItems: [rew(C.stick, 12)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Torch. Light is a tool you carry.", "Факел. Свет — инструмент, который носят."), requiredItems: [req(C.torch, 12, "craft_have")], rewardItems: [rew(C.firewood, 12)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("A reed basket. Volume is freedom.", "Плетёная корзина. Объём — это свобода."), requiredItems: [req(C.basket, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("Flax fibers stock for more cordage.", "Запас льняного волокна для новых верёвок."), requiredItems: [req(C.flaxfibers, 24, "have")], rewardItems: [rew(C.flaxtwine, 8)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("Rope. Stronger than twine for wells and lifts.", "Верёвка. Сильнее бечёвки для колодцев и подъёма."), requiredItems: [req(C.rope, 6, "craft")], rewardItems: [rew(C.flaxtwine, 8)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Oak planks. Walls wait in every trunk.", "Дубовые доски. Стены ждут в каждом стволе."), requiredItems: [req(C.plankOak, 40, "craft")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("More planks of any wood. Roofs need volume.", "Ещё досок любой древесины. Крышам нужен объём."), requiredItems: [req(C.plankAny, 64, "have")], rewardItems: [rew(C.nailsCu, 12)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Quest", description: d("A wooden chest. Secrets and grain need lids.", "Деревянный сундук. Секретам и зерну нужны крышки."), requiredItems: [req(C.chest, 1, "craft_have")], rewardItems: [rew(C.plankOak, 12)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Quest", description: d("A bed. Sleep is craft for the mind.", "Кровать. Сон — ремесло для ума."), requiredItems: [req(C.bed, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("A wooden ladder. Height is also a resource.", "Деревянная лестница. Высота — тоже ресурс."), requiredItems: [req(C.ladder, 4, "craft")], rewardItems: [rew(C.stick, 20)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("A wooden door. Thresholds make a house.", "Деревянная дверь. Пороги делают дом."), requiredItems: [req(C.door, 1, "craft_have")], rewardItems: [rew(C.plankOak, 12)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Fire bricks. Walls that remember heat.", "Огнеупорный кирпич. Стены, что помнят жар."), requiredItems: [req(C.refractoryRaw, 12, "craft")], rewardItems: [rew(C.clayFire, 12)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Beeswax. Candles and polish from the hive.", "Пчелиный воск. Свечи и полировка из улья."), requiredItems: [req(C.beeswax, 6, "have")], rewardItems: [rew(C.honeycomb, 2)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("Candles. Soft light when torches are too loud.", "Свечи. Мягкий свет, когда факелы слишком громки."), requiredItems: [req(C.candle, 8, "craft")], rewardItems: [rew(C.beeswax, 2)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("Resin for glue and seals.", "Смола для клея и уплотнений."), requiredItems: [req(C.resin, 12, "have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Leather plain. Soft armor of craft and travel.", "Обычная кожа. Мягкая броня ремесла и дороги."), requiredItems: [req(C.leatherPlain, 6, "have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("A brass torch holder. Mark what you built with light.", "Латунная подставка под факел. Отметь постройку светом."), requiredItems: [req(C.torchholder, 1, "detect")], rewardItems: [rew(C.torch, 6)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("Copper nails for strong joints.", "Медные гвозди для крепких стыков."), requiredItems: [req(C.nailsCu, 32, "craft")], rewardItems: [rew(C.plankOak, 16)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("Second chest for the growing household.", "Второй сундук для растущего хозяйства."), requiredItems: [req(C.chest, 2, "craft_have")], rewardItems: [rew(C.plankOak, 16)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("Thatch for soft roofs over workshops.", "Солома/тростник для мягких крыш над мастерскими."), requiredItems: [req(C.thatch, 24, "have")], rewardItems: [rew(C.stick, 16)] }),
      node({ id: 21, x: 0, y: -STEP * 5, nodeType: "Checkpoint", description: d("Crafts path complete. Tools and shelter now share one roof.", "Путь ремесла завершён. Инструменты и кров делят одну крышу.") }),
      node({ id: 22, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("Papyrus tops if marshes allow — another weave for baskets.", "Верхушки папируса, если есть болота — другое плетение для корзин."), requiredItems: [req(C.papyrustops, 16, "have")], rewardItems: [rew(C.cattailtops, 8)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
        [[3, 22], [22, 5]]
      ),
    };
  },
  { de: "Handwerk", fr: "Artisanat", es: "Oficios", pl: "Rzemiosło", ja: "工芸", zh: "工艺", it: "Artigianato", pt: "Ofícios", tr: "Zanaat", ko: "공예" }
);

// ═══════════════ EXPLORE ═══════════════
addCat(
  "category.explore.header",
  "category.explore.header.json",
  "game:gear-temporal",
  "Wanderings",
  "Странствия",
  "Wanderings",
  "Странствия",
  () => {
    const d = (en, ru) => L(en, ru);
    const nodes = [
      node({ id: 0, x: 0, y: 0, nodeType: "Start", description: d("Wanderings. Ruins remember those who listen.", "Странствия. Руины помнят тех, кто умеет слушать.") }),
      node({ id: 1, x: STEP, y: 0, nodeType: "Quest", description: d("Stock torches. Darkness is a room with no door.", "Запаси факелы. Тьма — комната без двери."), requiredItems: [req(C.torch, 16, "have")], rewardItems: [rew(C.flaxtwine, 4)] }),
      node({ id: 2, x: STEP * 2, y: 0, nodeType: "Quest", description: d("Food for the road. Cooked meat travels better than hope.", "Еда в дорогу. Жареное мясо идёт лучше надежды."), requiredItems: [req(C.redmeatCooked, 8, "have")], rewardItems: [rew(C.fat, 2)] }),
      node({ id: 3, x: STEP * 2, y: -STEP, nodeType: "Quest", description: d("Find rusty gears in ruins. Era’s breadcrumbs of iron.", "Найди ржавые шестерни в руинах. Хлебные крошки Эры из железа."), requiredItems: [req(C.gearRusty, 2, "detect")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 4, x: STEP * 3, y: -STEP, nodeType: "Quest", description: d("More gears. The hum under the sky grows louder.", "Ещё шестерни. Гул под небом становится громче."), requiredItems: [req(C.gearRusty, 6, "have")], rewardItems: [rew(C.ingotIron, 1)] }),
      node({ id: 5, x: STEP * 3, y: -STEP * 2, nodeType: "Quest", description: d("A temporal gear if fate allows — or keep hunting rust.", "Временная шестерня, если судьба позволит — или продолжай искать ржавчину."), requiredItems: [req(C.gearTemporal, 1, "detect")], rewardItems: [rew(C.gearRusty, 2)] }),
      node({ id: 6, x: STEP * 4, y: -STEP * 2, nodeType: "Quest", description: d("Resin from the wild road. Glue for broken machines of the past.", "Смола с дикой дороги. Клей для сломанных машин прошлого."), requiredItems: [req(C.resin, 16, "have")], rewardItems: [rew(C.flaxtwine, 6)] }),
      node({ id: 7, x: STEP * 4, y: -STEP * 3, nodeType: "Quest", description: d("Honeycomb from distant hives. Sweetness of unowned land.", "Соты с дальних ульев. Сладость ничьей земли."), requiredItems: [req(C.honeycomb, 8, "have")], rewardItems: [rew(C.beeswax, 3)] }),
      node({ id: 8, x: STEP * 3, y: -STEP * 3, nodeType: "Kill", description: d("Clear drifters near ruins. The past bites those who dig.", "Зачисти дрифтеров у руин. Прошлое кусает тех, кто копает."), requiredItems: [req(C.drifter, 10, "kill")], rewardItems: [rew(C.gearRusty, 1)] }),
      node({ id: 9, x: STEP * 2, y: -STEP * 3, nodeType: "Kill", description: d("Locusts in the deep. Clicking horror of the underground.", "Саранча в глубине. Щёлкающий ужас подземелий."), requiredItems: [req(C.locust, 6, "kill")], rewardItems: [rew(C.gearRusty, 1)] }),
      node({ id: 10, x: STEP, y: -STEP * 3, nodeType: "Quest", description: d("Flint in foreign soil. Even far away, the first tool waits.", "Кремень на чужой земле. Даже вдали ждёт первый инструмент."), requiredItems: [req(C.flint, 24, "have")], rewardItems: [rew(C.stick, 20)] }),
      node({ id: 11, x: 0, y: -STEP * 3, nodeType: "Quest", description: d("Clay from a river you did not name. Maps grow by walking.", "Глина из реки без имени. Карты растут от ходьбы."), requiredItems: [req(C.clayBlue, 24, "have")], rewardItems: [rew(C.firewood, 16)] }),
      node({ id: 12, x: 0, y: -STEP * 4, nodeType: "Quest", description: d("Copper from a distant seam. The path of Era is not a straight line.", "Медь с дальней жилы. Путь Эры — не прямая линия."), requiredItems: [req(C.copperNugget, 20, "have")], rewardItems: [rew(C.charcoal, 20)] }),
      node({ id: 13, x: STEP, y: -STEP * 4, nodeType: "Quest", description: d("Clear quartz from caves. Light trapped in stone.", "Прозрачный кварц из пещер. Свет, пойманный в камень."), requiredItems: [req(C.clearquartz, 8, "have")], rewardItems: [rew(C.flint, 6)] }),
      node({ id: 14, x: STEP * 2, y: -STEP * 4, nodeType: "Quest", description: d("Gold nuggets if fortune allows — soft metal of far roads.", "Золотые самородки, если повезёт — мягкий металл дальних дорог."), requiredItems: [req(C.goldNugget, 6, "have")], rewardItems: [rew(C.gearRusty, 1)] }),
      node({ id: 15, x: STEP * 3, y: -STEP * 4, nodeType: "Quest", description: d("Silver nuggets. Moon-colored metal of the deep.", "Серебряные самородки. Металл цвета луны из глубины."), requiredItems: [req(C.silverNugget, 6, "have")], rewardItems: [rew(C.gearRusty, 1)] }),
      node({ id: 16, x: STEP * 4, y: -STEP * 4, nodeType: "Quest", description: d("Malachite from a distant green seam.", "Малахит с дальней зелёной жилы."), requiredItems: [req(C.malachite, 10, "have")], rewardItems: [rew(C.charcoal, 16)] }),
      node({ id: 17, x: STEP * 4, y: -STEP * 5, nodeType: "Quest", description: d("Wild fruit from foreign groves.", "Дикие плоды с чужих рощ."), requiredItems: [req(C.fruitAny, 20, "have")], rewardItems: [rew(C.fruitBlueberry, 6)] }),
      node({ id: 18, x: STEP * 3, y: -STEP * 5, nodeType: "Quest", description: d("Bones from beasts of far lands.", "Кости зверей дальних земель."), requiredItems: [req(C.bone, 16, "have")], rewardItems: [rew(C.fat, 4)] }),
      node({ id: 19, x: STEP * 2, y: -STEP * 5, nodeType: "Quest", description: d("More rusty gears — the ruins still speak.", "Ещё ржавые шестерни — руины всё ещё говорят."), requiredItems: [req(C.gearRusty, 10, "have")], rewardItems: [rew(C.ingotIron, 2)] }),
      node({ id: 20, x: STEP, y: -STEP * 5, nodeType: "Quest", description: d("Return with charcoal ready for the home forge.", "Вернись с углём для домашней кузни."), requiredItems: [req(C.charcoal, 40, "have")], rewardItems: [rew(C.firewood, 20)] }),
      node({ id: 21, x: 0, y: -STEP * 5, nodeType: "Checkpoint", description: d("Wanderings complete. You return heavier — with knowledge, not only loot.", "Странствия завершены. Ты возвращаешься тяжелее — знанием, не только добычей.") }),
      node({ id: 22, x: -STEP, y: -STEP * 2, nodeType: "Quest", description: d("A spear for the road. Distance is a traveler’s friend.", "Копьё в дорогу. Дистанция — друг странника."), requiredItems: [req(C.spearFlint, 1, "craft_have")], rewardItems: [rew(C.flaxtwine, 4)] }),
    ];
    return {
      nodes,
      connections: edges(
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
        [[1, 22], [22, 3]]
      ),
    };
  },
  { de: "Wanderungen", fr: "Pérégrinations", es: "Andanzas", pl: "Wędrówki", ja: "放浪", zh: "漫游", it: "Peregrinazioni", pt: "Andanças", tr: "Yolculuklar", ko: "방랑" }
);

// ─── Write ────────────────────────────────────────────────────────────────

fs.mkdirSync(BRANCHES, { recursive: true });

const manifest = {
  version: "3.1-emberstar-expanded-fixed-codes",
  categories: [],
};

let totalNodes = 0;
for (const cat of CATEGORIES) {
  const { nodes, connections } = cat.build();
  totalNodes += nodes.length;
  writeBranch(cat.file, {
    iconItemCode: cat.icon,
    title: cat.title,
    headerTitle: cat.headerTitle,
    header: cat.header,
    nodes,
    connections,
  });
  manifest.categories.push({ headerTitle: cat.headerTitle, file: cat.file });
}

fs.writeFileSync(path.join(OUT, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n", "utf8");
console.log("wrote manifest.json, categories=", manifest.categories.length, "totalNodes=", totalNodes);
console.log("done.");
