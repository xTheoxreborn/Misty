//! Range une texture dans une catégorie proche de l'inventaire créatif (d'après son nom).
//! Les noms de catégories servent aussi de clés de traduction ("Cat_" + nom).

pub const ORDRE: &[&str] = &[
    "Blocs de construction", "Blocs colorés", "Blocs naturels", "Blocs fonctionnels", "Redstone",
    "Outils et utilitaires", "Combat", "Nourriture et boissons", "Ingrédients",
    "Œufs d'apparition", "Potions", "Disques", "Objets divers",
    "Entités", "Interface", "Environnement", "Particules", "Autres",
];

const COULEURS: &[&str] = &[
    "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
    "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black",
];

/// `chemin` : relatif au dossier textures, ex. "block/stone.png".
pub fn categorie(chemin: &str) -> &'static str {
    let dossier = chemin.split_once('/').map(|(d, _)| d).unwrap_or("");
    let fichier = chemin.rsplit('/').next().unwrap_or(chemin);
    let nom = fichier.rsplit_once('.').map(|(n, _)| n).unwrap_or(fichier).to_lowercase();

    match dossier {
        "block" => bloc(&nom),
        "item" => objet(&nom),
        "entity" | "models" => "Entités",
        "gui" => "Interface",
        "environment" => "Environnement",
        "particle" => "Particules",
        _ => "Autres",
    }
}

fn has(n: &str, mots: &[&str]) -> bool {
    mots.iter().any(|m| n.contains(m))
}

fn bloc(n: &str) -> &'static str {
    if n.ends_with("_ore") {
        return "Blocs naturels";
    }

    if has(n, &["redstone", "repeater", "comparator", "observer", "piston", "hopper", "dropper", "dispenser",
        "lever", "button", "pressure_plate", "tripwire", "target_", "daylight", "tnt", "note_block",
        "rail", "sculk_sensor", "lightning_rod", "crafter", "slime_block", "honey_block"]) {
        return "Redstone";
    }

    for c in COULEURS {
        if n.starts_with(&format!("{c}_")) && !has(n, &["mushroom", "sand", "_ice", "nether"]) {
            return "Blocs colorés";
        }
    }

    if has(n, &["crafting_table", "furnace", "smoker", "chest", "barrel", "anvil", "enchanting", "brewing",
        "cauldron", "torch", "lantern", "campfire", "door", "ladder", "bookshelf", "lectern", "loom",
        "grindstone", "stonecutter", "cartography", "smithing", "fletching", "composter", "jukebox",
        "beacon", "bell", "conduit", "end_portal", "respawn_anchor", "lodestone", "scaffolding",
        "chain", "flower_pot", "candle", "spawner", "vault", "trial", "shulker", "command_block",
        "structure", "jigsaw"]) {
        return "Blocs fonctionnels";
    }

    if has(n, &["stone", "brick", "plank", "slab", "stairs", "wall", "polished", "smooth", "cut_", "chiseled",
        "quartz", "prismarine", "purpur", "terracotta", "concrete", "glass", "wool", "copper", "tiles",
        "deepslate", "blackstone", "sandstone", "tuff_", "mud_", "mosaic", "bars"]) {
        return "Blocs de construction";
    }

    if has(n, &["dirt", "grass", "sand", "gravel", "log", "stem", "leaves", "sapling", "flower", "tulip",
        "orchid", "allium", "azure", "bluet", "dandelion", "poppy", "cornflower", "lily", "rose",
        "peony", "fern", "vine", "moss", "ice", "snow", "clay", "mycelium", "podzol", "netherrack",
        "soul_", "obsidian", "bedrock", "coral", "sponge", "kelp", "seagrass", "cactus", "bamboo",
        "cane", "pumpkin", "melon", "hay", "azalea", "dripstone", "amethyst", "calcite", "tuff",
        "basalt", "magma", "glowstone", "mushroom", "fungus", "wart", "roots", "sprouts", "wheat",
        "beetroots", "carrots", "potatoes", "berry", "dead_bush", "cobweb", "mud", "sculk", "egg",
        "nylium", "chorus", "lava", "water", "frogspawn", "petals", "pitcher", "torchflower",
        "eyeblossom", "leaf_litter", "bush", "dripleaf", "hanging", "ancient_debris"]) {
        return "Blocs naturels";
    }

    "Blocs de construction"
}

fn objet(n: &str) -> &'static str {
    if n.ends_with("_spawn_egg") {
        return "Œufs d'apparition";
    }
    if n.starts_with("music_disc") || n == "disc_fragment_5" {
        return "Disques";
    }
    if has(n, &["potion", "tipped_arrow"]) {
        return "Potions";
    }
    if n == "carrot_on_a_stick" || n == "warped_fungus_on_a_stick" {
        return "Outils et utilitaires";
    }
    if n.ends_with("_bucket") && n != "milk_bucket" {
        return "Outils et utilitaires";
    }

    if has(n, &["apple", "bread", "beef", "porkchop", "chicken", "mutton", "cod", "salmon", "fish", "carrot",
        "potato", "melon_slice", "cookie", "cake", "pie", "stew", "soup", "honey_bottle", "berries",
        "beetroot", "dried_kelp", "chorus_fruit", "milk_bucket", "rotten_flesh", "spider_eye",
        "cooked_", "baked_"]) {
        return "Nourriture et boissons";
    }

    if has(n, &["sword", "crossbow", "arrow", "trident", "shield", "helmet", "chestplate", "leggings", "boots",
        "mace", "totem", "horse_armor", "wolf_armor", "spear"]) || n == "bow" || n.starts_with("bow_") {
        return "Combat";
    }

    if n.ends_with("_axe") || n == "lead"
        || has(n, &["pickaxe", "shovel", "hoe", "shears", "flint_and_steel", "fishing_rod", "bucket", "compass",
            "clock", "spyglass", "brush", "name_tag", "saddle", "minecart", "boat", "raft", "bundle",
            "goat_horn", "firework_rocket", "elytra", "map"]) {
        return "Outils et utilitaires";
    }

    if has(n, &["ingot", "nugget", "dust", "diamond", "emerald", "coal", "charcoal", "stick", "string",
        "feather", "bone", "leather", "paper", "book", "slime_ball", "gunpowder", "shard", "scrap",
        "flint", "clay_ball", "brick", "quartz", "lapis", "amethyst", "prismarine", "blaze", "ghast",
        "ender_pearl", "ender_eye", "wheat", "seeds", "sugar", "egg", "dye", "pattern", "template",
        "rabbit", "phantom", "nautilus", "scute", "honeycomb", "ink_sac", "heart_of_the_sea", "echo",
        "fire_charge", "experience_bottle", "netherite", "raw_", "copper"]) {
        return "Ingrédients";
    }

    "Objets divers"
}
