/**
 * SwixyQuestBook — narrative progression generator.
 * Story arc: «Путь Угольной Звезды» / Emberstar Path
 * Main ages + life skills + ruins exploration.
 */
const fs = require("fs");
const path = require("path");

const STEP_X = 170;
const FORK_Y = 125;

const item = (code, count) => ({ collectibleCode: code, count });
const node = (id, x, y, nodeType, description, requiredItems = [], rewardItems = []) => ({
  id, x, y, nodeType, description, requiredItems, rewardItems,
});

/**
 * stages: { type?, req?, rew?, fork?: 'up'|'down' }
 * Forks are optional side bonuses (never gate the main spine).
 */
function buildCategory(meta, stages) {
  const nodes = [];
  const connections = [];
  let id = 0;
  let prevMain = 0;
  let mainX = 0;

  nodes.push(node(0, 0, 0, "Start", `quest.${meta.key}.0.description`, [], []));
  id = 1;
  mainX = STEP_X;

  for (const s of stages) {
    const type = s.type || "Quest";
    const desc = `quest.${meta.key}.${id}.description`;

    if (s.fork === "up" || s.fork === "down") {
      const y = s.fork === "up" ? -FORK_Y : FORK_Y;
      const forkId = id++;
      nodes.push(node(forkId, mainX - STEP_X * 0.45, y, type, desc, s.req || [], s.rew || []));
      connections.push({ startNodeId: prevMain, endNodeId: forkId });
      continue;
    }

    const mainId = id++;
    nodes.push(node(mainId, mainX, 0, type, desc, s.req || [], s.rew || []));
    connections.push({ startNodeId: prevMain, endNodeId: mainId });
    prevMain = mainId;
    mainX += STEP_X;
  }

  // rewrite descriptions to match final ids (already set above)
  for (const n of nodes) {
    n.description = `quest.${meta.key}.${n.id}.description`;
  }

  return {
    iconItemCode: meta.icon,
    title: `category.${meta.key}.title`,
    headerTitle: `category.${meta.key}.header`,
    nodes,
    connections,
  };
}

// =============================================================================
// CATEGORIES
// =============================================================================
const categories = [];

// 0. LEGEND — story spine
categories.push(buildCategory({ key: "legend", icon: "game:gear-rusty" }, [
  { req: [item("game:stick", 8)], rew: [item("game:flint", 2)] },
  { req: [item("game:drygrass", 6)], rew: [item("game:firewood", 6)] },
  { req: [item("game:firewood", 12)], rew: [item("game:torch-basic-extinct-up", 2)] },
  { type: "Checkpoint", req: [], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:flint", 4)], rew: [item("game:stick", 8)] },
  { req: [item("game:clay-blue", 8)], rew: [item("game:drygrass", 8)] },
  { req: [item("game:nugget-nativecopper", 5)], rew: [item("game:charcoal", 4)] },
  { type: "Checkpoint", req: [], rew: [item("game:flaxtwine", 4)] },
  { req: [item("game:resin", 4)], rew: [item("game:fat", 2)], fork: "up" },
  { req: [item("game:honeycomb", 2)], rew: [item("game:flaxfibers", 8)], fork: "down" },
  { req: [item("game:gear-rusty", 1)], rew: [item("game:ingot-copper", 1)] },
  { req: [item("game:charcoal", 16)], rew: [item("game:firewood", 16)] },
  { type: "Checkpoint", req: [], rew: [item("game:leather-normal-plain", 2)] },
  { req: [item("game:ingot-iron", 1)], rew: [item("game:charcoal", 8)] },
  { req: [item("game:gear-rusty", 3)], rew: [item("game:ingot-iron", 1)] },
  { type: "Checkpoint", req: [], rew: [item("game:ingot-steel", 1)] },
]));

// 1. STONE
categories.push(buildCategory({ key: "stone", icon: "game:flint" }, [
  { req: [item("game:stick", 16)], rew: [item("game:flint", 4)] },
  { req: [item("game:stone-*", 16)], rew: [item("game:stick", 8)] },
  { req: [item("game:drygrass", 16)], rew: [item("game:firewood", 8)] },
  { req: [item("game:flint", 12)], rew: [item("game:drygrass", 8)] },
  { req: [item("game:cattailtops", 12)], rew: [item("game:stick", 4)], fork: "up" },
  { req: [item("game:cattailroot", 6)], rew: [item("game:firewood", 4)], fork: "down" },
  { req: [item("game:knifeblade-flint", 1)], rew: [item("game:stick", 8)] },
  { req: [item("game:knife-generic-flint", 1)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:axe-flint", 1)], rew: [item("game:firewood", 16)] },
  { req: [item("game:shovel-flint", 1)], rew: [item("game:clay-blue", 12)] },
  { req: [item("game:hoe-flint", 1)], rew: [item("game:seeds-spelt", 4)] },
  { req: [item("game:spear-generic-flint", 1)], rew: [item("game:bone", 4)], fork: "up" },
  { req: [item("game:club-generic-wood", 1)], rew: [item("game:stick", 8)], fork: "down" },
  { req: [item("game:firewood", 32)], rew: [item("game:torch-basic-extinct-up", 6)] },
  { req: [item("game:basket-normal-reed", 1)], rew: [item("game:drygrass", 12)] },
  { req: [item("game:flaxtwine", 4)], rew: [item("game:flaxfibers", 8)] },
  { type: "Checkpoint", req: [], rew: [item("game:flint", 8)] },
]));

// 2. CLAY
categories.push(buildCategory({ key: "clay", icon: "game:clay-blue" }, [
  { req: [item("game:clay-blue", 32)], rew: [item("game:drygrass", 12)] },
  { req: [item("game:clay-red", 16)], rew: [item("game:firewood", 8)], fork: "up" },
  { req: [item("game:clay-fire", 12)], rew: [item("game:charcoal", 4)], fork: "down" },
  { req: [item("game:bowl-blue-raw", 4)], rew: [item("game:clay-blue", 8)] },
  { req: [item("game:claypot-blue-raw", 2)], rew: [item("game:firewood", 12)] },
  { req: [item("game:crock-blue-raw", 2)], rew: [item("game:clay-blue", 8)] },
  { req: [item("game:crucible-blue-raw", 2)], rew: [item("game:firewood", 16)] },
  { req: [item("game:clayplanter-blue-raw", 1)], rew: [item("game:seeds-flax", 4)], fork: "up" },
  { req: [item("game:firewood", 40)], rew: [item("game:drygrass", 16)] },
  { req: [item("game:bowl-blue-fired", 4)], rew: [item("game:fat", 2)] },
  { req: [item("game:claypot-blue-fired", 2)], rew: [item("game:firewood", 8)] },
  { req: [item("game:crock-blue-fired", 2)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:crucible-blue-fired", 2)], rew: [item("game:nugget-nativecopper", 8)] },
  { req: [item("game:clayplanter-blue-fired", 1)], rew: [item("game:seeds-carrot", 4)], fork: "down" },
  { req: [item("game:charcoal", 12)], rew: [item("game:nugget-nativecopper", 5)] },
  { type: "Checkpoint", req: [], rew: [item("game:nugget-nativecopper", 12)] },
]));

// 3. COPPER
categories.push(buildCategory({ key: "copper", icon: "game:nugget-nativecopper" }, [
  { req: [item("game:nugget-nativecopper", 30)], rew: [item("game:charcoal", 12)] },
  { req: [item("game:charcoal", 24)], rew: [item("game:firewood", 16)] },
  { req: [item("game:ingot-copper", 4)], rew: [item("game:flaxtwine", 4)] },
  { req: [item("game:hammer-copper", 1)], rew: [item("game:ingot-copper", 1)] },
  { req: [item("game:pickaxe-copper", 1)], rew: [item("game:nugget-nativecopper", 12)] },
  { req: [item("game:prospectingpick-copper", 1)], rew: [item("game:charcoal", 8)], fork: "up" },
  { req: [item("game:axe-felling-copper", 1)], rew: [item("game:firewood", 32)] },
  { req: [item("game:shovel-copper", 1)], rew: [item("game:clay-blue", 20)] },
  { req: [item("game:hoe-copper", 1)], rew: [item("game:seeds-spelt", 6)], fork: "down" },
  { req: [item("game:knife-generic-copper", 1)], rew: [item("game:flaxtwine", 4)] },
  { req: [item("game:chisel-copper", 1)], rew: [item("game:ingot-copper", 1)] },
  { req: [item("game:saw-copper", 1)], rew: [item("game:firewood", 24)] },
  { req: [item("game:scythe-copper", 1)], rew: [item("game:flaxfibers", 12)], fork: "up" },
  { req: [item("game:shears-copper", 1)], rew: [item("game:flaxfibers", 8)], fork: "down" },
  { req: [item("game:anvil-copper", 1)], rew: [item("game:ingot-copper", 2)] },
  { req: [item("game:spear-generic-copper", 1)], rew: [item("game:bone", 6)] },
  { type: "Checkpoint", req: [], rew: [item("game:ingot-copper", 3)] },
]));

// 4. BRONZE
categories.push(buildCategory({ key: "bronze", icon: "game:ingot-tinbronze" }, [
  { req: [item("game:ingot-tin", 2)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:ingot-copper", 8)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:ingot-tinbronze", 4)], rew: [item("game:flaxtwine", 6)] },
  { req: [item("game:hammer-tinbronze", 1)], rew: [item("game:ingot-tinbronze", 1)] },
  { req: [item("game:pickaxe-tinbronze", 1)], rew: [item("game:ingot-tinbronze", 1)] },
  { req: [item("game:axe-felling-tinbronze", 1)], rew: [item("game:firewood", 40)] },
  { req: [item("game:saw-tinbronze", 1)], rew: [item("game:firewood", 24)] },
  { req: [item("game:shovel-tinbronze", 1)], rew: [item("game:clay-fire", 12)], fork: "up" },
  { req: [item("game:knife-generic-tinbronze", 1)], rew: [item("game:flaxtwine", 4)], fork: "down" },
  { req: [item("game:chisel-tinbronze", 1)], rew: [item("game:ingot-tinbronze", 1)] },
  { req: [item("game:scythe-tinbronze", 1)], rew: [item("game:grain-spelt", 8)], fork: "up" },
  { req: [item("game:prospectingpick-tinbronze", 1)], rew: [item("game:charcoal", 12)], fork: "down" },
  { req: [item("game:anvil-tinbronze", 1)], rew: [item("game:ingot-tinbronze", 2)] },
  { req: [item("game:spear-generic-tinbronze", 1)], rew: [item("game:leather-normal-plain", 2)] },
  { type: "Checkpoint", req: [], rew: [item("game:ingot-tinbronze", 3)] },
]));

// 5. IRON
categories.push(buildCategory({ key: "iron", icon: "game:ingot-iron" }, [
  { req: [item("game:charcoal", 48)], rew: [item("game:firewood", 32)] },
  { req: [item("game:firewood", 64)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:ironbloom", 4)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:ingot-iron", 4)], rew: [item("game:flaxtwine", 6)] },
  { req: [item("game:hammer-iron", 1)], rew: [item("game:ingot-iron", 1)] },
  { req: [item("game:pickaxe-iron", 1)], rew: [item("game:ingot-iron", 1)] },
  { req: [item("game:axe-felling-iron", 1)], rew: [item("game:firewood", 48)] },
  { req: [item("game:saw-iron", 1)], rew: [item("game:firewood", 32)] },
  { req: [item("game:shovel-iron", 1)], rew: [item("game:clay-fire", 16)], fork: "up" },
  { req: [item("game:scythe-iron", 1)], rew: [item("game:grain-rye", 12)], fork: "down" },
  { req: [item("game:chisel-iron", 1)], rew: [item("game:ingot-iron", 1)] },
  { req: [item("game:knife-generic-iron", 1)], rew: [item("game:leather-normal-plain", 2)] },
  { req: [item("game:prospectingpick-iron", 1)], rew: [item("game:charcoal", 16)], fork: "up" },
  { req: [item("game:shears-iron", 1)], rew: [item("game:cloth-plain", 1)], fork: "down" },
  { req: [item("game:anvil-iron", 1)], rew: [item("game:ingot-iron", 2)] },
  { req: [item("game:spear-generic-iron", 1)], rew: [item("game:bone", 8)] },
  { type: "Checkpoint", req: [], rew: [item("game:ingot-iron", 3)] },
]));

// 6. STEEL
categories.push(buildCategory({ key: "steel", icon: "game:ingot-steel" }, [
  { req: [item("game:coke", 24)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:ingot-iron", 10)], rew: [item("game:coke", 8)] },
  { req: [item("game:charcoal", 32)], rew: [item("game:firewood", 24)] },
  { req: [item("game:ingot-steel", 4)], rew: [item("game:flaxtwine", 8)] },
  { req: [item("game:hammer-steel", 1)], rew: [item("game:ingot-steel", 1)] },
  { req: [item("game:pickaxe-steel", 1)], rew: [item("game:ingot-steel", 1)] },
  { req: [item("game:axe-felling-steel", 1)], rew: [item("game:firewood", 48)] },
  { req: [item("game:saw-steel", 1)], rew: [item("game:firewood", 32)], fork: "up" },
  { req: [item("game:chisel-steel", 1)], rew: [item("game:ingot-steel", 1)], fork: "down" },
  { req: [item("game:knife-generic-steel", 1)], rew: [item("game:leather-normal-plain", 2)] },
  { req: [item("game:scythe-steel", 1)], rew: [item("game:grain-spelt", 16)], fork: "up" },
  { req: [item("game:prospectingpick-steel", 1)], rew: [item("game:coke", 8)], fork: "down" },
  { req: [item("game:anvil-steel", 1)], rew: [item("game:ingot-steel", 2)] },
  { req: [item("game:spear-generic-steel", 1)], rew: [item("game:gear-rusty", 1)] },
  { type: "Checkpoint", req: [], rew: [item("game:ingot-steel", 3)] },
]));

// 7. HUNTING
categories.push(buildCategory({ key: "hunting", icon: "game:bow-simple" }, [
  { req: [item("game:spear-generic-flint", 1)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:bone", 12)], rew: [item("game:fat", 2)] },
  { req: [item("game:fat", 4)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:feather", 12)], rew: [item("game:stick", 8)], fork: "up" },
  { req: [item("game:redmeat-raw", 8)], rew: [item("game:firewood", 12)] },
  { req: [item("game:redmeat-cooked", 8)], rew: [item("game:fat", 2)] },
  { req: [item("game:bushmeat-raw", 4)], rew: [item("game:bone", 4)], fork: "down" },
  { req: [item("game:hide-raw-*", 6)], rew: [item("game:fat", 4)] },
  { req: [item("game:hide-scraped-*", 4)], rew: [item("game:flaxtwine", 4)] },
  { req: [item("game:arrowhead-flint", 12)], rew: [item("game:feather", 8)] },
  { req: [item("game:bow-simple", 1)], rew: [item("game:arrowhead-flint", 8)] },
  { req: [item("game:leather-normal-plain", 6)], rew: [item("game:flaxtwine", 4)] },
  { req: [item("game:armor-head-sewn-leather", 1)], rew: [item("game:leather-normal-plain", 2)] },
  { req: [item("game:armor-body-improvised-wood", 1)], rew: [item("game:firewood", 16)], fork: "up" },
  { req: [item("game:fishraw-*", 2)], rew: [item("game:fat", 1)], fork: "down" },
  { req: [item("game:fishchunk-*", 2)], rew: [item("game:flaxtwine", 2)] },
  { type: "Checkpoint", req: [], rew: [item("game:leather-normal-plain", 4)] },
]));

// 8. FARMING
categories.push(buildCategory({ key: "farming", icon: "game:seeds-spelt" }, [
  { req: [item("game:seeds-spelt", 12)], rew: [item("game:drygrass", 12)] },
  { req: [item("game:seeds-flax", 12)], rew: [item("game:stick", 8)] },
  { req: [item("game:seeds-carrot", 12)], rew: [item("game:clay-blue", 8)] },
  { req: [item("game:seeds-onion", 8)], rew: [item("game:drygrass", 8)], fork: "up" },
  { req: [item("game:seeds-turnip", 8)], rew: [item("game:stick", 8)], fork: "down" },
  { req: [item("game:grain-spelt", 24)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:grain-rye", 16)], rew: [item("game:seeds-rye", 4)] },
  { req: [item("game:flaxfibers", 32)], rew: [item("game:seeds-flax", 6)] },
  { req: [item("game:vegetable-carrot", 16)], rew: [item("game:seeds-carrot", 4)] },
  { req: [item("game:vegetable-onion", 12)], rew: [item("game:seeds-onion", 4)], fork: "up" },
  { req: [item("game:vegetable-turnip", 12)], rew: [item("game:seeds-turnip", 4)], fork: "down" },
  { req: [item("game:compost", 12)], rew: [item("game:seeds-spelt", 6)] },
  { req: [item("game:egg-chicken-raw", 6)], rew: [item("game:grain-spelt", 8)], fork: "up" },
  { req: [item("game:flour-spelt", 12)], rew: [item("game:grain-spelt", 8)] },
  { req: [item("game:quern-*", 1)], rew: [item("game:flour-spelt", 8)] },
  { type: "Checkpoint", req: [], rew: [item("game:seeds-spelt", 12)] },
]));

// 9. CRAFTS (home)
categories.push(buildCategory({ key: "crafts", icon: "game:cloth-plain" }, [
  { req: [item("game:flaxfibers", 24)], rew: [item("game:stick", 8)] },
  { req: [item("game:flaxtwine", 12)], rew: [item("game:flaxfibers", 12)] },
  { req: [item("game:cloth-plain", 4)], rew: [item("game:flaxtwine", 6)] },
  { req: [item("game:linensack", 1)], rew: [item("game:cloth-plain", 1)], fork: "up" },
  { req: [item("game:flour-spelt", 12)], rew: [item("game:grain-spelt", 8)] },
  { req: [item("game:dough-spelt", 6)], rew: [item("game:firewood", 12)] },
  { req: [item("game:bread-spelt-perfect", 4)], rew: [item("game:fat", 2)] },
  { req: [item("game:bread-rye-perfect", 2)], rew: [item("game:flaxtwine", 2)], fork: "down" },
  { req: [item("game:plank-*", 24)], rew: [item("game:firewood", 16)] },
  { req: [item("game:resin", 6)], rew: [item("game:firewood", 8)] },
  { req: [item("game:cobblestone-*", 24)], rew: [item("game:clay-blue", 12)] },
  { req: [item("game:burnedbrick-fire", 12)], rew: [item("game:charcoal", 12)] },
  { req: [item("game:burnedbrick-red", 12)], rew: [item("game:clay-red", 8)], fork: "up" },
  { req: [item("game:beeswax", 4)], rew: [item("game:honeycomb", 2)], fork: "down" },
  { req: [item("game:candle", 4)], rew: [item("game:beeswax", 2)] },
  { req: [item("game:butter", 2)], rew: [item("game:bread-spelt-perfect", 1)], fork: "up" },
  { type: "Checkpoint", req: [], rew: [item("game:cloth-plain", 4)] },
]));

// 10. EXPLORATION / ruins
categories.push(buildCategory({ key: "explore", icon: "game:gear-temporal" }, [
  { req: [item("game:fruit-blueberry", 8)], rew: [item("game:stick", 8)] },
  { req: [item("game:fruit-*", 16)], rew: [item("game:flaxtwine", 2)] },
  { req: [item("game:flower-horsetail-free", 6)], rew: [item("game:drygrass", 8)], fork: "up" },
  { req: [item("game:flower-catmint-free", 4)], rew: [item("game:flaxfibers", 4)], fork: "down" },
  { req: [item("game:resin", 8)], rew: [item("game:firewood", 12)] },
  { req: [item("game:honeycomb", 4)], rew: [item("game:flaxfibers", 8)] },
  { req: [item("game:beeswax", 4)], rew: [item("game:candle", 2)] },
  { req: [item("game:gear-rusty", 2)], rew: [item("game:ingot-copper", 1)] },
  { req: [item("game:gear-rusty", 5)], rew: [item("game:charcoal", 16)] },
  { req: [item("game:nugget-nativegold", 5)], rew: [item("game:flaxtwine", 4)], fork: "up" },
  { req: [item("game:nugget-nativesilver", 5)], rew: [item("game:flaxtwine", 4)], fork: "down" },
  { req: [item("game:clearquartz", 4)], rew: [item("game:flint", 4)] },
  { req: [item("game:gear-temporal", 1)], rew: [item("game:ingot-steel", 1)] },
  { type: "Checkpoint", req: [], rew: [item("game:gear-rusty", 2)] },
]));

// =============================================================================
// LOCALES + STORY TEXTS
// =============================================================================

const ru = {
  title: "КВЕСТЫ",
  title_bar: "{0} · {1}%",
  close: "ЗАКРЫТЬ",
  empty_category: "В этой ветке пока нет квестов.",
  hotkey_name: "Журнал квестов",

  "category.legend.title": "Сказание",
  "category.legend.header": "ПУТЬ УГОЛЬНОЙ ЗВЕЗДЫ",
  "category.stone.title": "Каменный век",
  "category.stone.header": "КАМЕННЫЙ ВЕК",
  "category.clay.title": "Гончарное дело",
  "category.clay.header": "ГОНЧАРНОЕ ДЕЛО",
  "category.copper.title": "Медный век",
  "category.copper.header": "МЕДНЫЙ ВЕК",
  "category.bronze.title": "Бронзовый век",
  "category.bronze.header": "БРОНЗОВЫЙ ВЕК",
  "category.iron.title": "Железный век",
  "category.iron.header": "ЖЕЛЕЗНЫЙ ВЕК",
  "category.steel.title": "Стальной век",
  "category.steel.header": "СТАЛЬНОЙ ВЕК",
  "category.hunting.title": "Охота",
  "category.hunting.header": "ОХОТА",
  "category.farming.title": "Земледелие",
  "category.farming.header": "ЗЕМЛЕДЕЛИЕ",
  "category.crafts.title": "Ремесло",
  "category.crafts.header": "РЕМЕСЛО",
  "category.explore.title": "Странствия",
  "category.explore.header": "СТРАНСТВИЯ",
  "category.new_branch.title": "Ветка {0}",
  "category.new_branch.header": "ВЕТКА {0}",
};

const en = {
  title: "QUESTS",
  title_bar: "{0} · {1}%",
  close: "CLOSE",
  empty_category: "This branch has no quests yet.",
  hotkey_name: "Quest Journal",

  "category.legend.title": "Legend",
  "category.legend.header": "EMBERSTAR PATH",
  "category.stone.title": "Stone Age",
  "category.stone.header": "STONE AGE",
  "category.clay.title": "Pottery",
  "category.clay.header": "POTTERY",
  "category.copper.title": "Copper Age",
  "category.copper.header": "COPPER AGE",
  "category.bronze.title": "Bronze Age",
  "category.bronze.header": "BRONZE AGE",
  "category.iron.title": "Iron Age",
  "category.iron.header": "IRON AGE",
  "category.steel.title": "Steel Age",
  "category.steel.header": "STEEL AGE",
  "category.hunting.title": "Hunting",
  "category.hunting.header": "HUNTING",
  "category.farming.title": "Farming",
  "category.farming.header": "FARMING",
  "category.crafts.title": "Crafts",
  "category.crafts.header": "CRAFTS",
  "category.explore.title": "Wanderings",
  "category.explore.header": "WANDERINGS",
  "category.new_branch.title": "Branch {0}",
  "category.new_branch.header": "BRANCH {0}",
};

/** Story + progression texts keyed by category then node id (string). */
const texts = {
  legend: {
    ru: {
      0: "В кармане — обгоревший дневник кузнеца Эры. На обложке уголёк в форме звезды. «Кто найдёт мой путь — найдёт последнюю кузню».",
      1: "Первая запись: «Начни с дерева. Палки помнят, откуда ветер». Собери палки — будто перелистываешь первую страницу.",
      2: "«Трава сухая, как старые письма». Собери хворост для огня. Эра писала ночью при костре.",
      3: "«Огонь — единственный свидетель». Запаси дрова. В тепле дневник не дрожит от холода.",
      4: "Ты разжёг стоянку. Ночь больше не хозяин. На полях дневника проступает карта: камень → глина → медь…",
      5: "«Кремень режет тьму». Найди кремень — Эра точила им и лезвия, и мысли.",
      6: "«Глина слушает огонь лучше, чем люди». Принеси синюю глину. Без неё металл — только сказка.",
      7: "«Медь — кровь земли, если знать, где она спит». Найди самородки. Дневник теплеет в руках.",
      8: "Ты прошёл первые главы Сказания. Дальше путь ветвится: века металла, охота, поля и руины.",
      9: "На полях смолы: «Хвойные помнят бурю». Собери смолу — клей для того, что нельзя сковать.",
      10: "«Пчёлы хранят лето в сотах». Мёд и воск — свет, когда руды мало.",
      11: "Между корней — ржавая шестерёнка. Эра пометила её угольной звездой: «Осколок Времени До».",
      12: "«Уголь — чёрное солнце кузни». Запаси древесный уголь. Без него железо не проснётся.",
      13: "Сказание густеет. Ты уже не просто выживаешь — ты идёшь по следам Эры.",
      14: "«Когда железо запоёт на наковальне — вернись к шестерням». Принеси железный слиток как клятву.",
      15: "Ещё шестерни из руин. Эра верила: собрав их, услышишь гул «машин под небом».",
      16: "Финал Сказания открыт. Сталь, руины и последняя кузня ждут того, кто дочитал дневник до конца.",
    },
    en: {
      0: "In your pocket: a scorched journal by a smith named Era. On the cover, a coal shaped like a star. “Find my path — find the last forge.”",
      1: "First note: “Begin with wood. Sticks remember the wind.” Gather sticks — turn the first page.",
      2: "“Grass dry as old letters.” Collect tinder. Era wrote by firelight.",
      3: "“Fire is the only witness.” Stock firewood so the journal won’t shiver.",
      4: "Your camp holds. Night is no longer master. A map bleeds through the page: stone → clay → copper…",
      5: "“Flint cuts the dark.” Find flint — Era sharpened blades and thoughts alike.",
      6: "“Clay listens to fire better than people.” Bring blue clay. Without it, metal is a fairy tale.",
      7: "“Copper is the earth’s blood, if you know where it sleeps.” Gather nuggets. The journal warms in your hands.",
      8: "First chapters complete. The path branches: metal ages, hunt, fields, and ruins.",
      9: "Resin notes: “Conifers remember the storm.” Collect resin — glue for what iron cannot bind.",
      10: "“Bees keep summer in comb.” Honey and wax — light when ore is scarce.",
      11: "Among roots: a rusty gear. Era marked it with an ember star — “Shard of the Time Before.”",
      12: "“Charcoal is the forge’s black sun.” Stock it. Iron will not wake without heat.",
      13: "The legend thickens. You are no longer only surviving — you are following Era.",
      14: "“When iron sings on the anvil, return to the gears.” Bring an iron ingot as a vow.",
      15: "More gears from the ruins. Era believed that gathering them reveals the hum of “machines under the sky.”",
      16: "The legend’s end is open. Steel, ruins, and the last forge wait for whoever finishes the journal.",
    },
  },

  stone: {
    ru: {
      0: "Каменный век — первая глава Эры. «Кто не умеет жить с кремнем, не достоин стали».",
      1: "Собери палки. Рукояти, факелы и копья рождаются из простого леса.",
      2: "Камни для knapping. Ударь камень о камень — так Эра учила учеников.",
      3: "Сухая трава — дыхание костра. Без неё огонь остаётся сном.",
      4: "Кремень — король камней. Запаси его, пока земля щедра.",
      5: "Рогоз у воды. Верхушки — на плетёные вещи; корни — еда в голодный день.",
      6: "Корни рогоза съедобны. Эра писала: «Болото кормит умных».",
      7: "Сколи клинок ножа. Первый резец мира — острый и честный.",
      8: "Собери кремнёвый нож целиком. Режь траву, мясо, верёвки — и страх.",
      9: "Топор. Лес перестанет быть стеной и станет складом.",
      10: "Лопата. Глина, земля, грядки — всё ждёт твоего удара.",
      11: "Мотыга. Даже в каменном веке можно пообещать земле урожай.",
      12: "Копьё. Дистанция между тобой и клыками.",
      13: "Дубина. Когда копьё далеко, остаётся твёрдое дерево.",
      14: "Запаси дрова. Ночь любит тех, у кого тлеет уголёк.",
      15: "Плетёная корзина. Карманы кончаются быстрее, чем дорога.",
      16: "Шпагат связывает путь. Без нити всё разваливается.",
      17: "Каменный век закрыт. Эра бы кивнула: «Теперь глина».",
    },
    en: {
      0: "Stone Age — Era’s first chapter. “Who cannot live by flint does not deserve steel.”",
      1: "Gather sticks. Handles, torches and spears begin in the woods.",
      2: "Stones for knapping. Strike stone on stone — Era’s first lesson.",
      3: "Dry grass is the fire’s breath. Without it, flame stays a dream.",
      4: "Flint is king among stones. Stock it while the land is kind.",
      5: "Cattails by water. Tops for weaving; roots for hungry days.",
      6: "Cattail roots feed the clever. “The marsh feeds those who look.”",
      7: "Knapp a knife blade — the world’s first honest edge.",
      8: "Finish a flint knife. Cut grass, meat, cord — and fear.",
      9: "An axe. The forest becomes supply, not a wall.",
      10: "A shovel. Clay, soil, beds — all wait for your strike.",
      11: "A hoe. Even stone can promise a harvest.",
      12: "A spear. Distance between you and teeth.",
      13: "A club. When the spear is far, hard wood remains.",
      14: "Stock firewood. Night loves those who keep an ember.",
      15: "A hand basket. Pockets end before the road does.",
      16: "Twine binds the path. Without thread, all unravels.",
      17: "Stone Age sealed. Era would nod: “Now clay.”",
    },
  },

  clay: {
    ru: {
      0: "Глина — вторая глава. «Металл без сосуда — дождь без кувшина».",
      1: "Синяя глина у воды. Копай терпеливо — Эра ненавидела спешку.",
      2: "Красная глина тоже служит. Разные жилы — разные песни.",
      3: "Огнеупорная глина. Для жара, который ломает обычную посуду.",
      4: "Сырые миски. Форма прежде огня.",
      5: "Сырые горшки. Здесь будут супы долгих зим.",
      6: "Сырые кроки. Запасы еды — тихая армия.",
      7: "Сырые тигли. Без них медь не станет слитком.",
      8: "Цветочный горшок. Даже кузнец сажал зелень у порога.",
      9: "Дрова для ямной печи. Pit kiln — печь бедняка и гения.",
      10: "Обожжённые миски. Огонь дал им право быть посудой.",
      11: "Обожжённые горшки. Теперь можно варить по-настоящему.",
      12: "Обожжённые кроки. Еда переживёт дорогу.",
      13: "Обожжённые тигли. Открой дверь в медный век.",
      14: "Обожжённый вазон. Маленький сад у большой кузни.",
      15: "Уголь для сильного жара. Керамика любит уголь, как металл.",
      16: "Гончарное дело завершено. Эра писала на глиняном черепке: «Дальше — блеск».",
    },
    en: {
      0: "Clay is chapter two. “Metal without a vessel is rain without a jug.”",
      1: "Blue clay by water. Dig with patience — Era hated haste.",
      2: "Red clay serves too. Different seams, different songs.",
      3: "Fire clay for heat that shatters ordinary pots.",
      4: "Raw bowls. Shape before fire.",
      5: "Raw cooking pots. Winter soups begin here.",
      6: "Raw crocks. Stored food is a quiet army.",
      7: "Raw crucibles. Without them copper never becomes an ingot.",
      8: "A planter. Even smiths grew green by the door.",
      9: "Firewood for the pit kiln — forge of pauper and genius.",
      10: "Fired bowls. Fire grants them the right to hold food.",
      11: "Fired pots. True cooking begins.",
      12: "Fired crocks. Meals will outlast the road.",
      13: "Fired crucibles. Open the door to copper.",
      14: "A fired planter. A small garden by a great forge.",
      15: "Charcoal for fierce heat. Ceramics love coal as metal does.",
      16: "Pottery complete. Era scratched on a shard: “Next — the gleam.”",
    },
  },

  copper: {
    ru: {
      0: "Медный век. «Первый металл, который слушается рук».",
      1: "Самородная медь. Собирай по жилам, как Эра — по снам.",
      2: "Уголь для плавки. Жар должен быть долгим и злым.",
      3: "Медные слитки. Твёрдая валюта ремесла.",
      4: "Медный молот. С этого удара начинается настоящая кузня.",
      5: "Медная кирка. Земля откроет то, что прятала от кремня.",
      6: "Поисковая кирка. Эра чертила карты руд, как звёзды.",
      7: "Медный топор. Лес сдаётся быстрее.",
      8: "Медная лопата. Глина и шахты благодарят.",
      9: "Медная мотыга. Поля рядом с кузней — мудрость, не слабость.",
      10: "Медный нож. Острее камня, честнее ржавчины.",
      11: "Зубило. Тонкая работа — тоже сила.",
      12: "Пила. Доски ровные, как строки дневника.",
      13: "Коса. Жатва кормит кузнеца не хуже руды.",
      14: "Ножницы. Овцы и лён не ждут, пока ты копаешь медь.",
      15: "Медная наковальня. Сердце мастерской стучит ровно.",
      16: "Медное копьё. Металл на древке — уже не каменный век.",
      17: "Медь покорена. В дневнике: «Ищи олово. Сплав сильнее гордыни».",
    },
    en: {
      0: "Copper Age. “The first metal that obeys the hands.”",
      1: "Native copper. Gather along seams as Era followed dreams.",
      2: "Charcoal for the melt. Heat must be long and fierce.",
      3: "Copper ingots — hard currency of craft.",
      4: "A copper hammer. True smithing begins with this strike.",
      5: "A copper pick. Earth yields what flint could not take.",
      6: "A prospecting pick. Era mapped ores like stars.",
      7: "A copper axe. The forest yields faster.",
      8: "A copper shovel. Clay and mines thank you.",
      9: "A copper hoe. Fields beside a forge are wisdom, not weakness.",
      10: "A copper knife. Sharper than stone, cleaner than rust.",
      11: "A chisel. Fine work is still strength.",
      12: "A saw. Planks as straight as journal lines.",
      13: "A scythe. Harvest feeds a smith as well as ore.",
      14: "Shears. Sheep and flax will not wait while you dig copper.",
      15: "A copper anvil. The workshop’s heart beats steady.",
      16: "A copper spear. Metal on a shaft — stone age is over.",
      17: "Copper conquered. Journal: “Seek tin. Alloy outranks pride.”",
    },
  },

  bronze: {
    ru: {
      0: "Бронзовый век. «Два металла, одна воля».",
      1: "Оловянные слитки. Редкость, которая меняет эпоху.",
      2: "Медь для сплава. Бронза любит точную долю.",
      3: "Оловянная бронза. Звон наковальни становится выше.",
      4: "Бронзовый молот. Удар глубже, след чище.",
      5: "Бронзовая кирка. Жилы, что смеялись над медью, замолкают.",
      6: "Бронзовый топор. Лес помнит твои шаги.",
      7: "Бронзовая пила. Дом растёт из ровных досок.",
      8: "Бронзовая лопата. Глубже в глину и шахту.",
      9: "Бронзовый нож. Для кожи, еды и тонкой работы.",
      10: "Бронзовое зубило. Узоры и замки механизмов.",
      11: "Бронзовая коса. Поля не ждут, пока ты куёшь.",
      12: "Бронзовая поисковая кирка. Эра: «Слушай камень — он врёт редко».",
      13: "Бронзовая наковальня. Тяжёлая, как обещание.",
      14: "Бронзовое копьё. Охота и стража границ.",
      15: "Бронза завершена. Дальше — чёрный жар железа.",
    },
    en: {
      0: "Bronze Age. “Two metals, one will.”",
      1: "Tin ingots. Rarity that remakes an era.",
      2: "Copper for alloy. Bronze loves exact measure.",
      3: "Tin bronze. The anvil’s ring rises higher.",
      4: "A bronze hammer. Deeper strike, cleaner mark.",
      5: "A bronze pick. Veins that mocked copper fall silent.",
      6: "A bronze axe. The forest remembers your steps.",
      7: "A bronze saw. Homes grow from true planks.",
      8: "A bronze shovel. Deeper into clay and mine.",
      9: "A bronze knife. For hide, food and fine work.",
      10: "A bronze chisel. Patterns and the locks of machines.",
      11: "A bronze scythe. Fields will not wait on the forge.",
      12: "A bronze prospecting pick. Era: “Listen to stone — it rarely lies.”",
      13: "A bronze anvil. Heavy as a vow.",
      14: "A bronze spear. Hunt and border-watch.",
      15: "Bronze complete. Next — iron’s black heat.",
    },
  },

  iron: {
    ru: {
      0: "Железный век. «Не лей — куй. Железо рождается в упорстве».",
      1: "Много угля. Домна голодна всегда.",
      2: "Ещё дров — уголь не падает с неба.",
      3: "Железные крицы. Сырой металл эпохи Эры.",
      4: "Железные слитки. Прокуй крицу в послушную сталь завтрашнего дня.",
      5: "Железный молот. Кузня звучит иначе.",
      6: "Железная кирка. Глубины открывают рот.",
      7: "Железный топор. Лес кланяется.",
      8: "Железная пила. Мастерские и стены растут быстро.",
      9: "Железная лопата. Шахты и глина не успевают закрыться.",
      10: "Железная коса. Урожай для большой кузни.",
      11: "Железное зубило. Точность после силы.",
      12: "Железный нож. Долго служит, если беречь.",
      13: "Железная поисковая кирка. Карта недр полнеет.",
      14: "Железные ножницы. Стадо и лён на службе поселения.",
      15: "Железная наковальня. Сердце большой кузни Эры.",
      16: "Железное копьё. Стража на пороге руин.",
      17: "Железо твоё. Дневник шепчет: «Сталь — угольная звезда в полночи».",
    },
    en: {
      0: "Iron Age. “Do not cast — smith. Iron is born of persistence.”",
      1: "Much charcoal. The bloomery is always hungry.",
      2: "More firewood — charcoal does not fall from the sky.",
      3: "Iron blooms. Raw metal of Era’s age.",
      4: "Iron ingots. Hammer blooms into tomorrow’s obedience.",
      5: "An iron hammer. The forge sounds different.",
      6: "An iron pick. The deep opens its mouth.",
      7: "An iron axe. The forest bows.",
      8: "An iron saw. Workshops and walls rise fast.",
      9: "An iron shovel. Mines and clay cannot close in time.",
      10: "An iron scythe. Harvest for a great forge.",
      11: "An iron chisel. Precision after power.",
      12: "An iron knife. Long service if cared for.",
      13: "An iron prospecting pick. The map of depths grows.",
      14: "Iron shears. Flock and flax serve the settlement.",
      15: "An iron anvil. Heart of Era’s great forge.",
      16: "An iron spear. Guard at the threshold of ruins.",
      17: "Iron is yours. The journal whispers: “Steel is the emberstar at midnight.”",
    },
  },

  steel: {
    ru: {
      0: "Стальной век — финал пути Эры. «Кто дошёл сюда, слышит гул кузни под небом».",
      1: "Кокс. Чистый яростный жар.",
      2: "Запас железа. Сталь ест упорство.",
      3: "Ещё угля. Ночь у наковальни длинна.",
      4: "Стальные слитки. Вершина металлургии выживания.",
      5: "Стальной молот. Удар, после которого эхо живёт в стенах.",
      6: "Стальная кирка. Ломает то, что ломало других.",
      7: "Стальной топор. Лес — лишь материал.",
      8: "Стальная пила. Идеальные доски для последней мастерской.",
      9: "Стальное зубило. Тонкость на краю силы.",
      10: "Стальной нож. Клинок, который помнит все предыдущие.",
      11: "Стальная коса. Поля для целого поселения.",
      12: "Стальная поисковая кирка. Ты читаешь камень, как дневник.",
      13: "Стальная наковальня. Трон, о котором писала Эра.",
      14: "Стальное копьё. И в нём — отблеск ржавых шестерён руин.",
      15: "Стальной век покорён. Угольная звезда горит в твоей кузне. Сказание закрыто — и открыто заново в странствиях.",
    },
    en: {
      0: "Steel Age — Era’s finale. “Whoever reaches here hears the forge under the sky.”",
      1: "Coke. Clean, furious heat.",
      2: "Iron stock. Steel eats persistence.",
      3: "More charcoal. Nights at the anvil run long.",
      4: "Steel ingots. Peak of survival metallurgy.",
      5: "A steel hammer. A strike whose echo lives in the walls.",
      6: "A steel pick. Breaks what broke others.",
      7: "A steel axe. Forest is only material.",
      8: "A steel saw. Perfect planks for the last workshop.",
      9: "A steel chisel. Finesse at the edge of power.",
      10: "A steel knife. A blade that remembers every earlier one.",
      11: "A steel scythe. Fields for a whole settlement.",
      12: "A steel prospecting pick. You read stone like a journal.",
      13: "A steel anvil. The throne Era wrote of.",
      14: "A steel spear — and in it, a glint of rusty ruin-gears.",
      15: "Steel Age conquered. The emberstar burns in your forge. The legend closes — and opens again in wanderings.",
    },
  },

  hunting: {
    ru: {
      0: "Охота. «Кузнец, который не умеет добыть еду, куёт только голод».",
      1: "Кремнёвое копьё. Первый договор с дистанцией.",
      2: "Кости. Из них — клей, наконечники и память зверей.",
      3: "Жир. Свет, жарка, выделка — белая кровь добычи.",
      4: "Перья. Стрелы без них — просто палки.",
      5: "Сырое мясо. Не ешь так — огонь ждёт.",
      6: "Жареное мясо. Сытость пишет ровные строки в дневнике.",
      7: "Мясо дичи. Лес делится, если ты осторожен.",
      8: "Сырые шкуры. Начало кожи и тепла.",
      9: "Соскобленные шкуры. Терпение выделки.",
      10: "Кремнёвые наконечники. Зубья для стрел.",
      11: "Простой лук. Тихая охота Эры.",
      12: "Кожа. Ремни, сумки, броня.",
      13: "Кожаный шлем. Голова — тоже инструмент.",
      14: "Импровизированная броня. Пока нет стали — есть смекалка.",
      15: "Сырая рыба. Река тоже кормит странника.",
      16: "Копчёная рыба. Дорожный запас без спешки.",
      17: "Охотник готов. Эра: «Мясо на столе — металл в руках спокойнее».",
    },
    en: {
      0: "Hunting. “A smith who cannot take food only forges hunger.”",
      1: "A flint spear. First treaty with distance.",
      2: "Bones — glue, points, and memory of beasts.",
      3: "Fat. Light, frying, tanning — pale blood of the kill.",
      4: "Feathers. Arrows without them are sticks.",
      5: "Raw meat. Do not eat it so — fire waits.",
      6: "Cooked meat. Satiety writes steady journal lines.",
      7: "Bushmeat. The woods share if you are careful.",
      8: "Raw hides. Start of leather and warmth.",
      9: "Scraped hides. Patience of the tannery.",
      10: "Flint arrowheads. Teeth for arrows.",
      11: "A simple bow. Era’s quiet hunt.",
      12: "Leather. Straps, bags, armor.",
      13: "A leather helm. The head is a tool too.",
      14: "Improvised armor. Before steel — wit.",
      15: "Raw fish. The river feeds wanderers.",
      16: "Smoked fish. Trail rations without haste.",
      17: "Hunter ready. Era: “Meat on the table steadies metal in the hand.”",
    },
  },

  farming: {
    ru: {
      0: "Земледелие. «Кто сеет, тот переживает зиму кузни».",
      1: "Семена полбы. Хлеб будущего.",
      2: "Семена льна. Нити, ткань, верёвки.",
      3: "Семена моркови. Простая радость грядки.",
      4: "Лук. Острый вкус долгих супов.",
      5: "Репа. Скромная, но верная.",
      6: "Урожай полбы. Мельница уже зовёт.",
      7: "Урожай ржи. Другой хлеб — другая зима.",
      8: "Льняное волокно. Поля одевают людей.",
      9: "Морковь с грядок. Цвет на столе.",
      10: "Лук с грядок. Слезы — тоже часть урожая.",
      11: "Репа с грядок. Земля отвечает заботе.",
      12: "Компост. То, что сгнило, снова кормит.",
      13: "Яйца. Двор живёт рядом с полем.",
      14: "Мука. Без неё хлеб — только мечта.",
      15: "Жёрнов. Камень, который кормит лучше кирки.",
      16: "Поля в порядке. Эра: «Сытый кузнец куёт ровнее».",
    },
    en: {
      0: "Farming. “Who sows outlasts the forge-winter.”",
      1: "Spelt seeds. Bread of tomorrow.",
      2: "Flax seeds. Thread, cloth, rope.",
      3: "Carrot seeds. Simple garden joy.",
      4: "Onion. Sharp comfort for long soups.",
      5: "Turnip. Humble and true.",
      6: "Spelt harvest. The quern is calling.",
      7: "Rye harvest. Another bread, another winter.",
      8: "Flax fiber. Fields clothe people.",
      9: "Carrots from the beds. Color on the table.",
      10: "Onions from the beds. Tears are part of harvest.",
      11: "Turnips from the beds. Soil answers care.",
      12: "Compost. What rotted feeds again.",
      13: "Eggs. The yard lives beside the field.",
      14: "Flour. Without it bread is only a dream.",
      15: "A quern. Stone that feeds better than a pick.",
      16: "Fields ready. Era: “A fed smith strikes truer.”",
    },
  },

  crafts: {
    ru: {
      0: "Ремесло дома. «Кузня без дома — костёр в поле».",
      1: "Льняное волокно. Пряжа пути.",
      2: "Шпагат. Связующая сила поселения.",
      3: "Полотно. Одежда и паруса уюта.",
      4: "Лён-ткань. Тонкая работа ткача.",
      5: "Мука. Белая пыль хлеба.",
      6: "Тесто. Руки помнят тепло.",
      7: "Хлеб из полбы. Запах, ради которого стоит жить.",
      8: "Ржаной хлеб. Другая корка, та же сила.",
      9: "Доски. Стены растут из ровных линий.",
      10: "Смола. Клей и защита дерева.",
      11: "Булыжник. Пол, который не боится сапог.",
      12: "Огнеупорный кирпич. Печи и кузни любят его.",
      13: "Красный кирпич. Цвет жилья.",
      14: "Воск. Свет без копоти леса.",
      15: "Свечи. Ночные страницы дневника.",
      16: "Масло. К хлебу и к тишине вечера.",
      17: "Дом готов. Эра бы села у стола и улыбнулась.",
    },
    en: {
      0: "Home crafts. “A forge without a house is a fire in a field.”",
      1: "Flax fiber. Yarn of the path.",
      2: "Twine. Binding force of a settlement.",
      3: "Cloth. Clothes and sails of comfort.",
      4: "Linen. A weaver’s finer work.",
      5: "Flour. White dust of bread.",
      6: "Dough. Hands remember warmth.",
      7: "Spelt bread. A smell worth living for.",
      8: "Rye bread. Another crust, same strength.",
      9: "Planks. Walls grow from straight lines.",
      10: "Resin. Glue and wood’s armor.",
      11: "Cobble. A floor unafraid of boots.",
      12: "Fire bricks. Kilns and forges love them.",
      13: "Red bricks. Color of a home.",
      14: "Wax. Light without forest soot.",
      15: "Candles. Night pages of the journal.",
      16: "Butter. For bread and evening quiet.",
      17: "Home ready. Era would sit at the table and smile.",
    },
  },

  explore: {
    ru: {
      0: "Странствия. Последние страницы дневника — про руины и «машины под небом».",
      1: "Черника у тропы. Сладость на языке странника.",
      2: "Разные ягоды. Лес пишет меню каждый день.",
      3: "Хвощ. Лечит и напоминает: природа старше кузни.",
      4: "Котовник. Запах, который успокаивает после шахты.",
      5: "Смола в глубине леса. Деревья помнят бурю Эры.",
      6: "Соты. Дикий мёд — золото без рудника.",
      7: "Воск. Свет для катакомб и длинных дорог.",
      8: "Ржавые шестерни. Осколки Времени До — как в Сказании.",
      9: "Ещё шестерни. Эра складывала их в круг угольной звезды.",
      10: "Золотые самородки. Редкость, что блестит в пыли руин.",
      11: "Серебро. Холодный блеск старых залов.",
      12: "Кварц. Прозрачные слёзы камня.",
      13: "Временная шестерня. Сердце легенды. Эра дошла сюда — и исчезла в гуле.",
      14: "Странствия не кончаются. Угольная звезда горит в тебе: куй, сей, иди дальше.",
    },
    en: {
      0: "Wanderings. The journal’s last pages speak of ruins and “machines under the sky.”",
      1: "Blueberries by the trail. Sweetness on a wanderer’s tongue.",
      2: "Mixed fruit. The forest rewrites the menu daily.",
      3: "Horsetail. Heals and reminds: nature is older than the forge.",
      4: "Catmint. A scent that calms after the mine.",
      5: "Deep-woods resin. Trees remember Era’s storm.",
      6: "Honeycomb. Wild honey — gold without a mine.",
      7: "Wax. Light for catacombs and long roads.",
      8: "Rusty gears. Shards of the Time Before — as in the Legend.",
      9: "More gears. Era laid them in an ember-star circle.",
      10: "Gold nuggets. Rarity glittering in ruin dust.",
      11: "Silver. Cold gleam of old halls.",
      12: "Quartz. Clear tears of stone.",
      13: "A temporal gear. Heart of the legend. Era reached here — and vanished into the hum.",
      14: "Wanderings never end. The emberstar burns in you: smith, sow, walk on.",
    },
  },
};

// Fill locale quest strings; pad missing ids with a safe fallback
for (const cat of categories) {
  const key = cat.title.replace("category.", "").replace(".title", "");
  const pack = texts[key] || { ru: {}, en: {} };
  for (const n of cat.nodes) {
    const id = String(n.id);
    const ruText = pack.ru?.[id] || pack.ru?.[n.id] || `Продолжай путь: этап ${n.id}.`;
    const enText = pack.en?.[id] || pack.en?.[n.id] || `Continue the path: step ${n.id}.`;
    ru[`quest.${key}.${n.id}.description`] = ruText;
    en[`quest.${key}.${n.id}.description`] = enText;
  }
}

function mergeUiKeys(target, langPath) {
  if (!fs.existsSync(langPath)) return;
  const old = JSON.parse(fs.readFileSync(langPath, "utf8"));
  for (const [k, v] of Object.entries(old)) {
    if (k.startsWith("quest.") || k.startsWith("category.")) continue;
    if (target[k] == null) target[k] = v;
  }
}

const root = path.resolve(__dirname, "..");
const questsPath = path.join(root, "SwixyQuestBook", "Data", "quests.json");
const ruPath = path.join(root, "SwixyQuestBook", "assets", "swixyquestbook", "lang", "ru.json");
const enPath = path.join(root, "SwixyQuestBook", "assets", "swixyquestbook", "lang", "en.json");

mergeUiKeys(ru, ruPath);
mergeUiKeys(en, enPath);

const database = {
  version: "2.1-emberstar-story",
  categories,
};

fs.writeFileSync(questsPath, JSON.stringify(database, null, 2), "utf8");
fs.writeFileSync(ruPath, JSON.stringify(ru, null, 2), "utf8");
fs.writeFileSync(enPath, JSON.stringify(en, null, 2), "utf8");

let totalNodes = 0;
let totalConn = 0;
let missingRu = 0;
for (const c of categories) {
  totalNodes += c.nodes.length;
  totalConn += c.connections.length;
  const key = c.title.replace("category.", "").replace(".title", "");
  for (const n of c.nodes) {
    const t = ru[`quest.${key}.${n.id}.description`] || "";
    if (t.startsWith("Продолжай путь")) missingRu++;
  }
  console.log(`${c.headerTitle}: nodes=${c.nodes.length} conn=${c.connections.length}`);
}
console.log(`TOTAL cats=${categories.length} nodes=${totalNodes} conn=${totalConn} fallbackTexts=${missingRu}`);
console.log("Wrote", questsPath);
