namespace Misty.Services
{
    /// <summary>Range une texture dans une catégorie proche de l'inventaire créatif (d'après son nom).</summary>
    internal static class TextureCategories
    {
        public static readonly string[] Ordre =
        {
            "Blocs de construction", "Blocs colorés", "Blocs naturels", "Blocs fonctionnels", "Redstone",
            "Outils et utilitaires", "Combat", "Nourriture et boissons", "Ingrédients",
            "Œufs d'apparition", "Potions", "Disques", "Objets divers",
            "Entités", "Interface", "Environnement", "Particules", "Autres",
        };

        private static readonly string[] Couleurs =
        {
            "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
            "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black",
        };

        /// <param name="chemin">Chemin relatif au dossier textures, ex. "block/stone.png".</param>
        public static string Categorie(string chemin)
        {
            int barre = chemin.IndexOf('/');
            string dossier = barre < 0 ? "" : chemin[..barre];
            string nom = Path.GetFileNameWithoutExtension(chemin).ToLowerInvariant();

            return dossier switch
            {
                "block" => Bloc(nom),
                "item" => Objet(nom),
                "entity" or "models" => "Entités",
                "gui" => "Interface",
                "environment" => "Environnement",
                "particle" => "Particules",
                _ => "Autres",
            };
        }

        private static bool Has(string n, params string[] mots) => mots.Any(m => n.Contains(m));

        private static string Bloc(string n)
        {
            if (n.EndsWith("_ore")) return "Blocs naturels";

            if (Has(n, "redstone", "repeater", "comparator", "observer", "piston", "hopper", "dropper", "dispenser",
                       "lever", "button", "pressure_plate", "tripwire", "target_", "daylight", "tnt", "note_block",
                       "rail", "sculk_sensor", "lightning_rod", "crafter", "slime_block", "honey_block"))
                return "Redstone";

            foreach (var c in Couleurs)
                if (n.StartsWith(c + "_") && !Has(n, "mushroom", "sand", "_ice", "nether"))
                    return "Blocs colorés";

            if (Has(n, "crafting_table", "furnace", "smoker", "chest", "barrel", "anvil", "enchanting", "brewing",
                       "cauldron", "torch", "lantern", "campfire", "door", "ladder", "bookshelf", "lectern", "loom",
                       "grindstone", "stonecutter", "cartography", "smithing", "fletching", "composter", "jukebox",
                       "beacon", "bell", "conduit", "end_portal", "respawn_anchor", "lodestone", "scaffolding",
                       "chain", "flower_pot", "candle", "spawner", "vault", "trial", "shulker", "command_block",
                       "structure", "jigsaw"))
                return "Blocs fonctionnels";

            if (Has(n, "stone", "brick", "plank", "slab", "stairs", "wall", "polished", "smooth", "cut_", "chiseled",
                       "quartz", "prismarine", "purpur", "terracotta", "concrete", "glass", "wool", "copper", "tiles",
                       "deepslate", "blackstone", "sandstone", "tuff_", "mud_", "mosaic", "bars"))
                return "Blocs de construction";

            if (Has(n, "dirt", "grass", "sand", "gravel", "log", "stem", "leaves", "sapling", "flower", "tulip",
                       "orchid", "allium", "azure", "bluet", "dandelion", "poppy", "cornflower", "lily", "rose",
                       "peony", "fern", "vine", "moss", "ice", "snow", "clay", "mycelium", "podzol", "netherrack",
                       "soul_", "obsidian", "bedrock", "coral", "sponge", "kelp", "seagrass", "cactus", "bamboo",
                       "cane", "pumpkin", "melon", "hay", "azalea", "dripstone", "amethyst", "calcite", "tuff",
                       "basalt", "magma", "glowstone", "mushroom", "fungus", "wart", "roots", "sprouts", "wheat",
                       "beetroots", "carrots", "potatoes", "berry", "dead_bush", "cobweb", "mud", "sculk", "egg",
                       "nylium", "chorus", "lava", "water", "frogspawn", "petals", "pitcher", "torchflower",
                       "eyeblossom", "leaf_litter", "bush", "dripleaf", "hanging", "ancient_debris"))
                return "Blocs naturels";

            return "Blocs de construction";
        }

        private static string Objet(string n)
        {
            if (n.EndsWith("_spawn_egg")) return "Œufs d'apparition";
            if (n.StartsWith("music_disc") || n == "disc_fragment_5") return "Disques";
            if (Has(n, "potion", "tipped_arrow")) return "Potions";
            if (n is "carrot_on_a_stick" or "warped_fungus_on_a_stick") return "Outils et utilitaires";
            if (n.EndsWith("_bucket") && n != "milk_bucket") return "Outils et utilitaires";

            if (Has(n, "apple", "bread", "beef", "porkchop", "chicken", "mutton", "cod", "salmon", "fish", "carrot",
                       "potato", "melon_slice", "cookie", "cake", "pie", "stew", "soup", "honey_bottle", "berries",
                       "beetroot", "dried_kelp", "chorus_fruit", "milk_bucket", "rotten_flesh", "spider_eye",
                       "cooked_", "baked_"))
                return "Nourriture et boissons";

            if (Has(n, "sword", "crossbow", "arrow", "trident", "shield", "helmet", "chestplate", "leggings", "boots",
                       "mace", "totem", "horse_armor", "wolf_armor", "spear") || n == "bow" || n.StartsWith("bow_"))
                return "Combat";

            if (n.EndsWith("_axe") || n == "lead" ||
                Has(n, "pickaxe", "shovel", "hoe", "shears", "flint_and_steel", "fishing_rod", "bucket", "compass",
                       "clock", "spyglass", "brush", "name_tag", "saddle", "minecart", "boat", "raft", "bundle",
                       "goat_horn", "firework_rocket", "elytra", "map"))
                return "Outils et utilitaires";

            if (Has(n, "ingot", "nugget", "dust", "diamond", "emerald", "coal", "charcoal", "stick", "string",
                       "feather", "bone", "leather", "paper", "book", "slime_ball", "gunpowder", "shard", "scrap",
                       "flint", "clay_ball", "brick", "quartz", "lapis", "amethyst", "prismarine", "blaze", "ghast",
                       "ender_pearl", "ender_eye", "wheat", "seeds", "sugar", "egg", "dye", "pattern", "template",
                       "rabbit", "phantom", "nautilus", "scute", "honeycomb", "ink_sac", "heart_of_the_sea", "echo",
                       "fire_charge", "experience_bottle", "netherite", "raw_", "copper"))
                return "Ingrédients";

            return "Objets divers";
        }
    }
}