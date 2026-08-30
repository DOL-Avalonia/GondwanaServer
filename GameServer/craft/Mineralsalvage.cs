using System;
using System.Collections.Generic;
using DOL.Database;

namespace DOL.GS.SalvageCalc
{
    public class MineralSalvageRecipe
    {
        public int Tier;
        public List<SalvageYieldEntry> Components;
    }

    /// <summary>
    /// Fixed salvage recipes for LootGeneratorMinerals items.
    /// All recipes follow the pattern: main x4 + rare x1 + residue x3.
    /// </summary>
    public static class MineralSalvage
    {
        private static readonly Dictionary<string, MineralSalvageRecipe> Recipes =
            new Dictionary<string, MineralSalvageRecipe>(StringComparer.OrdinalIgnoreCase);

        // Index = tier (0 unused). T1 free, T2 = 100, T3 = 250, T4 = 400, T5 = 600
        private static readonly int[] TierSkillRequirement = { 0, 0, 100, 250, 400, 600 };

        private static readonly eCraftingSkill[] AcceptedSkills =
        {
            eCraftingSkill.MetalWorking,
            eCraftingSkill.GemCutting,
            eCraftingSkill.ArmorCrafting,
            eCraftingSkill.WeaponCrafting,
            eCraftingSkill.Fletching
        };

        public static bool IsMineral(InventoryItem item)
        {
            return item?.Id_nb != null && Recipes.ContainsKey(item.Id_nb);
        }

        public static MineralSalvageRecipe GetRecipe(string idnb)
        {
            if (string.IsNullOrEmpty(idnb)) return null;
            Recipes.TryGetValue(idnb, out var recipe);
            return recipe;
        }

        public static int GetRequiredSkill(int tier)
        {
            if (tier < 1 || tier > 5) return 0;
            return TierSkillRequirement[tier];
        }

        public static bool HasRequiredSkill(GamePlayer player, int tier)
        {
            int required = GetRequiredSkill(tier);
            if (required <= 0) return true;

            foreach (eCraftingSkill skill in AcceptedSkills)
            {
                if (player.GetCraftingSkillValue(skill) >= required)
                    return true;
            }
            return false;
        }

        private static void Add(int tier, string mineral, string main, string rare, string residue)
        {
            Recipes[mineral] = new MineralSalvageRecipe
            {
                Tier = tier,
                Components = new List<SalvageYieldEntry>
                {
                    new SalvageYieldEntry { ID = main,    Count = 4, IsPrimary = true },
                    new SalvageYieldEntry { ID = rare,    Count = 1, IsPrimary = false },
                    new SalvageYieldEntry { ID = residue, Count = 3, IsPrimary = false }
                }
            };
        }

        static MineralSalvage()
        {
            // ================= TIER 1 =================
            Add(1, "malachite_nodule", "copper_ore", "limestone_flux", "charcoal_binder");
            Add(1, "cassiterite_pebble", "tin_ore", "copper_ore", "limestone_flux");
            Add(1, "hematite_chunk", "iron_ore", "ferrite_dust", "charcoal_binder");
            Add(1, "bog_iron_concretion", "ferrite_dust", "iron_ore", "limestone_flux");
            Add(1, "azurite_crystal", "copper_ore", "zinc_ore", "resin_binder");
            Add(1, "chalcocite_stone", "copper_ore", "lead_ore", "charcoal_binder");
            Add(1, "bornite_pebble", "copper_ore", "iron_ore", "limestone_flux");
            Add(1, "cuprite_chunk", "copper_ore", "tin_ore", "bone_ash");
            Add(1, "stannite_rock", "tin_ore", "iron_ore", "charcoal_binder");
            Add(1, "goethite_ore", "iron_ore", "zinc_ore", "limestone_flux");
            Add(1, "limonite_crust", "iron_ore", "lead_ore", "bone_ash");
            Add(1, "siderite_nodule", "iron_ore", "coal_lump", "charcoal_binder");
            Add(1, "taconite_shard", "ferrite_dust", "quartz_sand", "limestone_flux");
            Add(1, "meteoric_iron_dust", "iron_ore", "nickel_ore", "bone_ash");
            Add(1, "chalcopyrite_fragment", "copper_ore", "iron_ore", "charcoal_binder");
            Add(1, "tetrahedrite_pebble", "copper_ore", "silver_ore", "resin_binder");
            Add(1, "enargite_rock", "copper_ore", "brimstone_powder", "limestone_flux");
            Add(1, "tenantite_stone", "copper_ore", "zinc_ore", "charcoal_binder");
            Add(1, "chrysocolla_chunk", "copper_ore", "quartz_sand", "bone_ash");
            Add(1, "covellite_flake", "copper_ore", "lead_ore", "resin_binder");
            Add(1, "arsenopyrite_stone", "iron_ore", "brimstone_powder", "charcoal_binder");
            Add(1, "pyrrhotite_pebble", "iron_ore", "nickel_ore", "limestone_flux");
            Add(1, "marcasite_nodule", "iron_ore", "coal_lump", "bone_ash");
            Add(1, "galena_shard", "lead_ore", "silver_ore", "charcoal_binder");
            Add(1, "cerussite_stone", "lead_ore", "zinc_ore", "limestone_flux");
            Add(1, "anglesite_crystal", "lead_ore", "copper_ore", "resin_binder");
            Add(1, "bauxite_pebble", "ferrite_dust", "titanium_fragment", "charcoal_binder");
            Add(1, "sphalerite_grit", "zinc_ore", "iron_ore", "limestone_flux");
            Add(1, "smithsonite_chunk", "zinc_ore", "copper_ore", "bone_ash");
            Add(1, "hemimorphite_rock", "zinc_ore", "lead_ore", "resin_binder");
            Add(1, "willemite_crystal", "zinc_ore", "quartz_sand", "charcoal_binder");
            Add(1, "zincite_nodule", "zinc_ore", "ferrite_dust", "limestone_flux");
            Add(1, "hydrozincite_crust", "zinc_ore", "coal_lump", "bone_ash");
            Add(1, "franklinite_pebble", "zinc_ore", "iron_ore", "resin_binder");

            // ================= TIER 2 =================
            Add(2, "bituminous_coal_lump", "coal_lump", "iron_ore", "borax_flux");
            Add(2, "quartz_geode", "quartz_sand", "borax_flux", "iron_ore");
            Add(2, "dolomite_nodule", "dolomite_flux", "quartz_sand", "borax_flux");
            Add(2, "pyrite_cluster", "iron_ore", "coal_lump", "borax_flux");
            Add(2, "anthracite_coal_chunk", "coal_lump", "ferrite_dust", "dolomite_flux");
            Add(2, "lignite_coal_pebble", "coal_lump", "peat_moss", "borax_flux");
            Add(2, "graphite_flake", "coal_lump", "carbide_powder", "dolomite_flux");
            Add(2, "clear_quartz_shard", "quartz_sand", "silver_ore", "borax_flux");
            Add(2, "smoky_quartz_crystal", "quartz_sand", "coal_lump", "dolomite_flux");
            Add(2, "rose_quartz_pebble", "quartz_sand", "copper_ore", "brimstone_powder");
            Add(2, "amethyst_geode_fragment", "quartz_sand", "iron_ore", "borax_flux");
            Add(2, "citrine_crystal_shard", "quartz_sand", "gold_nugget", "dolomite_flux");
            Add(2, "agate_nodule", "quartz_sand", "zinc_ore", "brimstone_powder");
            Add(2, "onyx_pebble", "quartz_sand", "lead_ore", "borax_flux");
            Add(2, "carnelian_stone", "quartz_sand", "iron_ore", "dolomite_flux");
            Add(2, "chalcedony_chunk", "quartz_sand", "nickel_ore", "brimstone_powder");
            Add(2, "jasper_rock", "quartz_sand", "ferrite_dust", "borax_flux");
            Add(2, "flint_nodule", "quartz_sand", "charcoal_binder", "dolomite_flux");
            Add(2, "chert_shard", "quartz_sand", "limestone_flux", "brimstone_powder");
            Add(2, "magnesite_stone", "dolomite_flux", "iron_ore", "borax_flux");
            Add(2, "brucite_pebble", "dolomite_flux", "zinc_ore", "brimstone_powder");
            Add(2, "talc_rock", "dolomite_flux", "quartz_sand", "borax_flux");
            Add(2, "chlorite_flake", "dolomite_flux", "copper_ore", "brimstone_powder");
            Add(2, "epidote_crystal", "dolomite_flux", "iron_ore", "borax_flux");
            Add(2, "prehnite_stone", "dolomite_flux", "calcium_dust", "brimstone_powder");
            Add(2, "wollastonite_chunk", "quartz_sand", "dolomite_flux", "borax_flux");
            Add(2, "rhodonite_pebble", "zinc_ore", "quartz_sand", "dolomite_flux");
            Add(2, "rhodochrosite_crystal", "zinc_ore", "iron_ore", "borax_flux");
            Add(2, "manganite_stone", "iron_ore", "zinc_ore", "brimstone_powder");
            Add(2, "pyrolusite_nodule", "iron_ore", "lead_ore", "borax_flux");
            Add(2, "bixbyite_shard", "iron_ore", "copper_ore", "dolomite_flux");
            Add(2, "hausmannite_rock", "iron_ore", "coal_lump", "brimstone_powder");
            Add(2, "braunite_pebble", "iron_ore", "quartz_sand", "borax_flux");
            Add(2, "psilomelane_crust", "iron_ore", "silver_ore", "dolomite_flux");
            Add(2, "barite_rose", "lead_ore", "dolomite_flux", "borax_flux");

            // ================= TIER 3 =================
            Add(3, "pentlandite_orepiece", "nickel_ore", "zinc_ore", "aether_salt");
            Add(3, "spinel_gemstone", "cobalt_ore", "aether_salt", "borax_flux");
            Add(3, "silvery_mithril_vein", "mithril_vein", "aether_salt", "dolomite_flux");
            Add(3, "topaz_rough", "carbide_powder", "coal_lump", "aether_salt");
            Add(3, "zircon_crystal", "quartz_sand", "zirconium_dust", "aether_salt");
            Add(3, "smaltite_nodule", "cobalt_ore", "nickel_ore", "quicksilver_drop");
            Add(3, "cobaltite_rock", "cobalt_ore", "brimstone_powder", "aether_salt");
            Add(3, "skutterudite_stone", "cobalt_ore", "iron_ore", "quicksilver_drop");
            Add(3, "erythrite_crystal", "cobalt_ore", "copper_ore", "aether_salt");
            Add(3, "glaucodot_pebble", "cobalt_ore", "zinc_ore", "quicksilver_drop");
            Add(3, "garnierite_chunk", "nickel_ore", "quartz_sand", "aether_salt");
            Add(3, "millerite_stone", "nickel_ore", "brimstone_powder", "quicksilver_drop");
            Add(3, "niccolite_nodule", "nickel_ore", "iron_ore", "aether_salt");
            Add(3, "chloanthite_rock", "nickel_ore", "cobalt_ore", "quicksilver_drop");
            Add(3, "breithauptite_pebble", "nickel_ore", "lead_ore", "aether_salt");
            Add(3, "beryl_crystal", "mithril_vein", "quartz_sand", "ectoplasmic_residue");
            Add(3, "aquamarine_rough", "mithril_vein", "iron_ore", "aether_salt");
            Add(3, "emerald_flaw", "mithril_vein", "chromium_dust", "ectoplasmic_residue");
            Add(3, "heliodor_stone", "mithril_vein", "gold_nugget", "aether_salt");
            Add(3, "morganite_rough", "mithril_vein", "zinc_ore", "ectoplasmic_residue");
            Add(3, "tourmaline_crystal", "carbide_powder", "quartz_sand", "aether_salt");
            Add(3, "schorl_rock", "carbide_powder", "iron_ore", "ectoplasmic_residue");
            Add(3, "dravite_pebble", "carbide_powder", "dolomite_flux", "aether_salt");
            Add(3, "elbaite_stone", "carbide_powder", "copper_ore", "ectoplasmic_residue");
            Add(3, "garnet_rough", "carbide_powder", "iron_ore", "aether_salt");
            Add(3, "almandine_crystal", "carbide_powder", "cobalt_ore", "ectoplasmic_residue");
            Add(3, "pyrope_stone", "carbide_powder", "dolomite_flux", "aether_salt");
            Add(3, "spessartine_nodule", "carbide_powder", "zinc_ore", "ectoplasmic_residue");
            Add(3, "grossular_pebble", "carbide_powder", "limestone_flux", "aether_salt");
            Add(3, "andradite_rock", "carbide_powder", "iron_ore", "ectoplasmic_residue");
            Add(3, "uvarovite_crystal", "carbide_powder", "chromium_dust", "aether_salt");
            Add(3, "spodumene_chunk", "mithril_vein", "quartz_sand", "ectoplasmic_residue");
            Add(3, "kunzite_rough", "mithril_vein", "zinc_ore", "aether_salt");
            Add(3, "hiddenite_stone", "mithril_vein", "chromium_dust", "ectoplasmic_residue");
            Add(3, "petalite_pebble", "mithril_vein", "quartz_sand", "aether_salt");
            Add(3, "lepidolite_flake", "mithril_vein", "dolomite_flux", "ectoplasmic_residue");
            Add(3, "amblygonite_rock", "mithril_vein", "bone_ash", "aether_salt");
            Add(3, "turquoise_rough", "copper_ore", "bone_ash", "ectoplasmic_residue");
            Add(3, "variscite_nodule", "iron_ore", "bone_ash", "aether_salt");
            Add(3, "wavellite_crystal", "zinc_ore", "bone_ash", "ectoplasmic_residue");

            // ================= TIER 4 =================
            Add(4, "corundum_fragment", "adamantite_ore", "aether_salt", "star_mote");
            Add(4, "sapphire_rough", "sapphire_dust", "adamantite_ore", "star_mote");
            Add(4, "stibnite_asterite", "asterite_fragment", "sapphire_dust", "star_mote");
            Add(4, "diamond_rough", "diamond_dust", "carbide_powder", "star_mote");
            Add(4, "alexandrite_shard", "adamantite_ore", "chromium_dust", "ley_line_dust");
            Add(4, "ruby_rough", "adamantite_ore", "iron_ore", "star_mote");
            Add(4, "padparadscha_stone", "adamantite_ore", "zinc_ore", "ley_line_dust");
            Add(4, "rutile_crystal", "titanium_fragment", "quartz_sand", "star_mote");
            Add(4, "anatase_pebble", "titanium_fragment", "dolomite_flux", "ley_line_dust");
            Add(4, "brookite_rock", "titanium_fragment", "iron_ore", "star_mote");
            Add(4, "ilmenite_chunk", "titanium_fragment", "iron_ore", "ley_line_dust");
            Add(4, "perovskite_stone", "titanium_fragment", "limestone_flux", "star_mote");
            Add(4, "titanite_crystal", "titanium_fragment", "quartz_sand", "ley_line_dust");
            Add(4, "columbite_nodule", "tungsten_ore", "iron_ore", "star_mote");
            Add(4, "tantalite_rock", "tungsten_ore", "zinc_ore", "ley_line_dust");
            Add(4, "wolframite_chunk", "tungsten_ore", "iron_ore", "star_mote");
            Add(4, "scheelite_crystal", "tungsten_ore", "limestone_flux", "ley_line_dust");
            Add(4, "ferberite_pebble", "tungsten_ore", "iron_ore", "star_mote");
            Add(4, "huebnerite_stone", "tungsten_ore", "zinc_ore", "ley_line_dust");
            Add(4, "molybdenite_flake", "molybdenum_dust", "brimstone_powder", "star_mote");
            Add(4, "wulfenite_crystal", "molybdenum_dust", "lead_ore", "ley_line_dust");
            Add(4, "powellite_rock", "molybdenum_dust", "limestone_flux", "star_mote");
            Add(4, "bismutite_nodule", "bismuth_shard", "limestone_flux", "ley_line_dust");
            Add(4, "bismuthinite_stone", "bismuth_shard", "brimstone_powder", "star_mote");
            Add(4, "cinnabar_chunk", "quicksilver_drop", "brimstone_powder", "ley_line_dust");
            Add(4, "realgar_crystal", "brimstone_powder", "iron_ore", "star_mote");
            Add(4, "orpiment_rock", "brimstone_powder", "zinc_ore", "ley_line_dust");
            Add(4, "stibnite_pebble", "asterite_fragment", "brimstone_powder", "star_mote");
            Add(4, "kermesite_stone", "asterite_fragment", "quicksilver_drop", "ley_line_dust");
            Add(4, "tetrahedrite_crystal", "silver_ore", "copper_ore", "star_mote");
            Add(4, "bournonite_nodule", "lead_ore", "copper_ore", "ley_line_dust");
            Add(4, "pyrargyrite_rock", "silver_ore", "asterite_fragment", "star_mote");
            Add(4, "proustite_crystal", "silver_ore", "brimstone_powder", "ley_line_dust");
            Add(4, "stephanite_pebble", "silver_ore", "asterite_fragment", "star_mote");
            Add(4, "polybasite_stone", "silver_ore", "copper_ore", "ley_line_dust");
            Add(4, "acanthite_chunk", "silver_ore", "brimstone_powder", "star_mote");
            Add(4, "argentite_crystal", "silver_ore", "lead_ore", "ley_line_dust");
            Add(4, "chlorargyrite_rock", "silver_ore", "quicksilver_drop", "star_mote");
            Add(4, "bromargyrite_pebble", "silver_ore", "bone_ash", "ley_line_dust");
            Add(4, "iodargyrite_stone", "silver_ore", "zinc_ore", "star_mote");

            // ================= TIER 5 =================
            Add(5, "ilmenite_nethershard", "netherium_ore", "abyssal_slag", "void_ichor");
            Add(5, "black_opal_void", "void_ichor", "netherium_ore", "abyssal_slag");
            Add(5, "labradorite_astral", "arcanium_fragments", "philosopher_shard", "void_ichor");
            Add(5, "benitoite_astral", "philosopher_shard", "arcanium_fragments", "star_mote");
            Add(5, "painite_relic", "arcanium_fragments", "void_ichor", "abyssal_slag");
            Add(5, "taaffeite_relic", "arcanium_fragments", "philosopher_shard", "entropy_dust");
            Add(5, "chaos_emerald_flaw", "chaos_matter", "diamond_dust", "abyssal_slag");
            Add(5, "law_stone_fragment", "astral_dust", "arcanium_fragments", "philosopher_shard");
            Add(5, "runeblade_splinter", "netherium_ore", "carbide_powder", "entropy_dust");
            Add(5, "ariyoch_blood_ruby", "chaos_matter", "adamantite_ore", "void_ichor");
            Add(5, "xiombarg_tear_crystal", "astral_dust", "sapphire_dust", "philosopher_shard");
            Add(5, "mabden_soul_gem", "netherium_ore", "bone_ash", "abyssal_slag");
            Add(5, "melnibonean_jade", "arcanium_fragments", "emerald_flaw", "chronal_ash");
            Add(5, "chronocrystal_shard", "astral_dust", "quartz_sand", "chronal_ash");
            Add(5, "entropy_geode", "chaos_matter", "void_ichor", "entropy_dust");
            Add(5, "void_touched_obsidian", "netherium_ore", "quartz_sand", "abyssal_slag");
            Add(5, "abyssal_pearl", "void_ichor", "bone_ash", "entropy_dust");
            Add(5, "star_metal_slag", "astral_dust", "iron_ore", "chronal_ash");
            Add(5, "astral_diamond_dust", "diamond_dust", "astral_dust", "philosopher_shard");
            Add(5, "nether_quartz_anomaly", "netherium_ore", "quartz_sand", "void_ichor");
            Add(5, "eldritch_amethyst", "chaos_matter", "quartz_sand", "abyssal_slag");
            Add(5, "warped_space_spinel", "astral_dust", "cobalt_ore", "chronal_ash");
            Add(5, "demonic_bloodstone", "netherium_ore", "iron_ore", "entropy_dust");
            Add(5, "celestial_celestite", "astral_dust", "dolomite_flux", "philosopher_shard");
            Add(5, "ethereal_moonstone", "arcanium_fragments", "quartz_sand", "chronal_ash");
            Add(5, "phantom_quartz_core", "chaos_matter", "quartz_sand", "void_ichor");
            Add(5, "twilight_topaz", "netherium_ore", "carbide_powder", "abyssal_slag");
            Add(5, "eclipse_sunstone", "astral_dust", "gold_nugget", "chronal_ash");
            Add(5, "paradox_prism", "chaos_matter", "diamond_dust", "entropy_dust");
            Add(5, "singularity_stone", "void_ichor", "netherium_ore", "abyssal_slag");
            Add(5, "aetherial_amber", "arcanium_fragments", "resin_binder", "philosopher_shard");
            Add(5, "pandemonium_pyrite", "chaos_matter", "iron_ore", "entropy_dust");
            Add(5, "leylined_lapis", "astral_dust", "copper_ore", "chronal_ash");
            Add(5, "oblivion_onyx", "netherium_ore", "lead_ore", "void_ichor");
            Add(5, "hyperdimensional_halite", "chaos_matter", "aether_salt", "abyssal_slag");
        }
    }
}