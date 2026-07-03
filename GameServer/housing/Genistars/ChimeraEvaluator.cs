using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DOL.GS.Scripts
{
    public struct ExplicitRecipe
    {
        public string LoreName;
        public int TemplateID;
        public int MinErudition;
        public int[] RequiredLensIDs;
    }

    public static class ChimeraEvaluator
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // Secret Exact-Match Recipes (Requires >= 15 seconds of exposure per Lens)
        private static readonly ExplicitRecipe[] SecretRecipes = new ExplicitRecipe[]
        {
            new ExplicitRecipe { LoreName = "Magma Colossus", TemplateID = 1505, MinErudition = 5, RequiredLensIDs = new int[] { 31, 51, 5 } }, // Fire + Anatomy AF + Max Health
            new ExplicitRecipe { LoreName = "Fenrir Apex", TemplateID = 1509, MinErudition = 4, RequiredLensIDs = new int[] { 117, 10, 1 } },    // Apex Qui + DPS + Strength
            new ExplicitRecipe { LoreName = "Abyssal Wyrm", TemplateID = 1502, MinErudition = 3, RequiredLensIDs = new int[] { 33, 17, 42 } },   // Matter + Spell ABS + Ethereal Body
            new ExplicitRecipe { LoreName = "Titan Bear", TemplateID = 1507, MinErudition = 3, RequiredLensIDs = new int[] { 36, 94, 61 } }      // Cold Resist + Chimera HP + AI Str
        };

        // Standard RNG Fallback Pools mapped strictly to Complexity Tiers
        private static readonly int[] Tier0Pool = { 1500, 1501, 1502, 1503 }; // Worm, Frog, Snake, Spider
        private static readonly int[] Tier1Pool = { 1510 };                   // Lizard
        private static readonly int[] Tier3Pool = { 1504, 1507, 1508, 1509 }; // Wildcat, Bear, Boar, Wolf
        private static readonly int[] Tier5Pool = { 1505 };                   // Golem
        private static readonly int[] Tier7Pool = { 1506 };                   // Giants

        public static void ExecuteHatchSequence(GamePlayer player, DBGenistar db, GenistarEgg egg)
        {
            log.Info($"[Epigenetic Hatch Sequence]: Initiated for GenistarID '{db.GenistarID}' by Master '{player.Name}'.");

            Dictionary<int, int> rawLedger = GenistarLensMgr.ParseLensHistory(db.LensHistory);
            var viableLenses = rawLedger.Where(kv => kv.Value >= 15000).ToList();
            string lang = player.Client?.Account?.Language ?? "EN";

            int chosenTemplateId = -1;
            string explicitSpeciesName = "";

            // 1. Secret Recipe Check
            foreach (ExplicitRecipe recipe in SecretRecipes)
            {
                if (recipe.RequiredLensIDs.All(id => rawLedger.ContainsKey(id) && rawLedger[id] >= 15000))
                {
                    if (player.EruditionLevel >= recipe.MinErudition)
                    {
                        chosenTemplateId = recipe.TemplateID;
                        explicitSpeciesName = recipe.LoreName;
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Chimera.Legendary", recipe.LoreName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        break;
                    }
                    else
                    {
                        log.Warn($"[Recipe Deficit]: '{player.Name}' formed recipe '{recipe.LoreName}' but lacked Erudition ({player.EruditionLevel}/{recipe.MinErudition}). Fallback triggered.");
                    }
                }
            }

            // 2. Dominant Tier Fallback Check
            if (chosenTemplateId == -1)
            {
                int targetComplexityTier = 0;
                if (viableLenses.Count > 0)
                {
                    int highestLensId = viableLenses.Max(kv => kv.Key);
                    int lensTier = (highestLensId - 1) / 10;

                    targetComplexityTier = lensTier switch
                    {
                        <= 1 => 0,
                        <= 3 => 1,
                        <= 6 => 3,
                        <= 8 => 5,
                        _ => 7
                    };
                }

                if (player.EruditionLevel < targetComplexityTier)
                {
                    int deficit = targetComplexityTier - player.EruditionLevel;
                    int failChance = 5 + (deficit * 20);

                    if (Util.Chance(failChance))
                    {
                        log.Warn($"[Genetic Collapse]: '{player.Name}' failed Erudition roll (Deficit: {deficit}, Chance: {failChance}%). Embryo terminated.");

                        House targetHouse = HouseMgr.GetHouse(db.HouseNumber);
                        if (targetHouse != null)
                        {
                            targetHouse.UpdateGenistarVisual(db.PlaceholderKey, 1293);
                        }

                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Chimera.Collapse"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                        GameServer.Database.DeleteObject(db);
                        egg.RemoveFromWorld();
                        return;
                    }
                }

                chosenTemplateId = targetComplexityTier switch
                {
                    0 => Tier0Pool[Util.Random(Tier0Pool.Length - 1)],
                    1 => Tier1Pool[0],
                    3 => Tier3Pool[Util.Random(Tier3Pool.Length - 1)],
                    5 => Tier5Pool[0],
                    _ => Tier7Pool[0]
                };
            }

            // 3. Extract Baseline Concrete DAoC DNA
            NpcTemplate tmpl = NpcTemplateMgr.GetTemplate(chosenTemplateId) ?? NpcTemplateMgr.GetTemplate(1500);

            db.BaseTemplateID = chosenTemplateId;
            db.Name = string.IsNullOrEmpty(explicitSpeciesName) ? tmpl.Name : explicitSpeciesName;
            db.BodyType = tmpl.BodyType;
            db.CurrentModel = ParseFirstModel(tmpl.Model);

            int trueBaseSize = 50;
            if (tmpl != null && int.TryParse(tmpl.Size, out int parsedSize) && parsedSize > 0)
            {
                trueBaseSize = parsedSize;
            }
            db.Size = trueBaseSize;

            db.EquipmentTemplateID = tmpl!.EquipmentTemplateID ?? "";
            db.VisibleWeaponSlot = tmpl.VisibleActiveWeaponSlot;
            Race raceObj = GameServer.Database.SelectObject<Race>(DB.Column("ID").IsEqualTo((int)tmpl!.Race));

            // 4. THE GREAT BAKE: Rollover pre-natal Counts into baked baseline integers
            db.Strength += (short)(tmpl.Strength + (db.CountStrength / 10));
            db.Constitution += (short)(tmpl.Constitution + (db.CountConstitution / 10));
            db.Dexterity += (short)(tmpl.Dexterity + (db.CountDexterity / 10));
            db.Quickness += (short)(tmpl.Quickness + (db.CountQuickness / 10));
            db.Intelligence += (short)(tmpl.Intelligence + (db.CountIntelligence / 10));
            db.Empathy += (short)(tmpl.Empathy + (db.CountEmpathy / 10));
            db.Piety += (short)(tmpl.Piety + (db.CountPiety / 10));
            db.Charisma += (short)(tmpl.Charisma + (db.CountCharisma / 10));
            db.ArmorFactor += tmpl.ArmorFactor + (db.CountArmorFactor / 10);
            db.WeaponDPS += tmpl.WeaponDps + (db.CountWeaponDPS / 10);

            db.WeaponSpd += tmpl.WeaponSpd + (db.CountWeaponSpd / 10);
            db.ArmorAbsorb += tmpl.ArmorAbsorb + (db.CountArmorAbsorb / 10);
            db.EvadeChance += tmpl.EvadeChance + (db.CountEvadeChance / 10);
            db.BlockChance += tmpl.BlockChance + (db.CountBlockChance / 10);
            db.ParryChance += tmpl.ParryChance + (db.CountParryChance / 10);
            db.LeftHandSwingChance += tmpl.LeftHandSwingChance + (db.CountLeftHandSwingChance / 10);
            db.CounterAttackChance += tmpl.CounterAttackChance + (db.CountCounterAttackChance / 10);
            db.MaxHealth += 0 + (db.CountMaxHealth / 10);
            db.MaxTension = tmpl.MaxTension + (db.CountMaxTension / 10);
            db.CurrentTension = 0;

            db.ResistBody += (raceObj != null ? raceObj.ResistBody : 0) + (db.CountResistBody / 10);
            db.ResistCold += (raceObj != null ? raceObj.ResistCold : 0) + (db.CountResistCold / 10);
            db.ResistCrush += (raceObj != null ? raceObj.ResistCrush : 0) + (db.CountResistCrush / 10);
            db.ResistEnergy += (raceObj != null ? raceObj.ResistEnergy : 0) + (db.CountResistEnergy / 10);
            db.ResistHeat += (raceObj != null ? raceObj.ResistHeat : 0) + (db.CountResistHeat / 10);
            db.ResistMatter += (raceObj != null ? raceObj.ResistMatter : 0) + (db.CountResistMatter / 10);
            db.ResistSlash += (raceObj != null ? raceObj.ResistSlash : 0) + (db.CountResistSlash / 10);
            db.ResistSpirit += (raceObj != null ? raceObj.ResistSpirit : 0) + (db.CountResistSpirit / 10);
            db.ResistThrust += (raceObj != null ? raceObj.ResistThrust : 0) + (db.CountResistThrust / 10);
            db.ResistNatural += (raceObj != null ? raceObj.ResistNatural : 0) + (db.CountResistNatural / 10);

            db.SpellmagicABS += (db.CountSpellmagicABS / 10);
            db.DotABS += (db.CountDotABS / 10);
            db.MeleeABS += (db.CountMeleeABS / 10);
            db.EffectivenessMod += (db.CountEffectivenessMod / 10);
            db.AblativeShield += (db.CountAblativeShield / 10);
            db.CCResist += (db.CountCCResist / 10);
            db.DebuffResist += (db.CountDebuffResist / 10);
            db.CastRange += (db.CountCastRange / 10);
            db.ProcSpellChance += (db.CountProcSpellChance / 10);

            // 5. Blueprint Imprint (Lens Size modifiers stack on top of the SQL base right here!)
            foreach (var kv in viableLenses)
            {
                if (GenistarLensMgr.Lenses.TryGetValue(kv.Key, out LensDefinition def) && def.OnBirthImprint != null)
                {
                    def.OnBirthImprint.Invoke(db);
                }
            }

            ApplyEvolutionUnlocks(db);

            // 6. Hard Reset on accumulators
            ResetAllCounts(db);
            GenistarLensMgr.DisengageCareMode(player, voluntary: false, silent: true);

            db.State = (int)eGenistarState.Hatched;
            GameServer.Database.SaveObject(db);

            House house = HouseMgr.GetHouse(db.HouseNumber);
            if (house != null) house.UpdateGenistarVisual(db.PlaceholderKey, 1682);

            GenistarNPC liveAdult = new GenistarNPC();
            liveAdult.Position = egg.Position;
            liveAdult.LoadFromGenistarDB(db);
            liveAdult.AddToWorld();

            egg.RemoveFromWorld();

            string dispName = !string.IsNullOrEmpty(db.CustomName) ? db.CustomName : db.Name;
            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Chimera.Birth", dispName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static ushort ParseFirstModel(string modelString)
        {
            if (string.IsNullOrEmpty(modelString)) return 667;
            string[] models = modelString.Split(';');
            if (models.Length > 0 && ushort.TryParse(models[Util.Random(models.Length - 1)], out ushort m))
                return m > 0 ? m : (ushort)667;
            return 667;
        }

        private static void StitchSpell(DBGenistar db, string spell)
        {
            if (string.IsNullOrEmpty(db.Spells)) db.Spells = spell;
            else if (!db.Spells.Contains(spell)) db.Spells += ";" + spell;
        }

        private static void StitchStyle(DBGenistar db, string style)
        {
            if (string.IsNullOrEmpty(db.Styles)) db.Styles = style;
            else if (!db.Styles.Contains(style)) db.Styles += ";" + style;
        }

        private static void ApplyEvolutionUnlocks(DBGenistar db)
        {
            if (db.CountEvolveCold >= 50) StitchSpell(db, "genistar_lv0_dd_cold");
            if (db.CountEvolveSpirit >= 50) StitchSpell(db, "genistar_lv0_dd_spirit");
            if (db.CountEvolveMatter >= 50) StitchSpell(db, "genistar_lv0_dot_matter");
            if (db.CountEvolveFire >= 50) StitchSpell(db, "genistar_lv0_dd_fire");
            if (db.CountEvolveEnergy >= 50) StitchSpell(db, "genistar_lv0_dd_energy");
            if (db.CountEvolveBody >= 50) StitchSpell(db, "genistar_lv0_dd_body");
            if (db.CountEvolveHeal >= 50) StitchSpell(db, "genistar_lv0_heal");
            if (db.CountEvolveDot >= 50) StitchSpell(db, "genistar_lv0_dot");
            if (db.CountEvolveDisease >= 50) StitchSpell(db, "genistar_lv0_disease");
            if (db.CountEvolveCC >= 50) StitchSpell(db, "genistar_lv0_cc");
            if (db.CountEvolveSpecial >= 50) StitchSpell(db, "genistar_lv0_special");

            if (db.CountEvolveHammer >= 50) StitchStyle(db, "genistar_lv0_hammer01");
            if (db.CountEvolveSword >= 50) StitchStyle(db, "genistar_lv0_sword01");
            if (db.CountEvolveThrust >= 50) StitchStyle(db, "genistar_lv0_pierce01");
            if (db.CountEvolve2Hand >= 50) StitchStyle(db, "genistar_lv0_2hand01");
            if (db.CountEvolveSpear >= 50) StitchStyle(db, "genistar_lv0_spear01");
            if (db.CountEvolveStaves >= 50) StitchStyle(db, "genistar_lv0_staff01");
            if (db.CountEvolveHand2Hand >= 50) StitchStyle(db, "genistar_lv0_h2h01");
            if (db.CountEvolveFists >= 50) StitchStyle(db, "genistar_lv0_fist01");
        }

        private static void ResetAllCounts(DBGenistar db)
        {
            db.CountStrength = 0; db.CountConstitution = 0; db.CountDexterity = 0; db.CountQuickness = 0;
            db.CountIntelligence = 0; db.CountPiety = 0; db.CountEmpathy = 0; db.CountCharisma = 0;
            db.CountMaxHealth = 0; db.CountMaxTension = 0;

            db.CountWeaponDPS = 0; db.CountWeaponSpd = 0; db.CountArmorFactor = 0; db.CountArmorAbsorb = 0;
            db.CountEvadeChance = 0; db.CountBlockChance = 0; db.CountParryChance = 0;
            db.CountLeftHandSwingChance = 0; db.CountCounterAttackChance = 0; db.CountProcSpellChance = 0;

            db.CountResistBody = 0; db.CountResistCold = 0; db.CountResistCrush = 0; db.CountResistEnergy = 0;
            db.CountResistHeat = 0; db.CountResistMatter = 0; db.CountResistNatural = 0; db.CountResistSlash = 0;
            db.CountResistSpirit = 0; db.CountResistThrust = 0;

            db.CountSpellmagicABS = 0; db.CountDotABS = 0; db.CountMeleeABS = 0; db.CountEffectivenessMod = 0; db.CountCCResist = 0; db.CountDebuffResist = 0; db.CountCastRange = 0;

            db.CountEvolveCold = 0; db.CountEvolveSpirit = 0; db.CountEvolveMatter = 0; db.CountEvolveFire = 0; db.CountEvolveEnergy = 0;
            db.CountEvolveBody = 0; db.CountEvolveHeal = 0; db.CountEvolveDot = 0; db.CountEvolveDisease = 0; db.CountEvolveCC = 0; db.CountEvolveSpecial = 0;

            db.CountEvolveHammer = 0; db.CountEvolveSword = 0; db.CountEvolveThrust = 0; db.CountEvolve2Hand = 0; db.CountEvolveSpear = 0;
            db.CountEvolveStaves = 0; db.CountEvolveHand2Hand = 0; db.CountEvolveFists = 0;
        }
    }
}