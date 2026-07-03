using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class LensDefinition
    {
        public Action<DBGenistar, int> InjectCounts { get; }
        public Action<DBGenistar> OnBirthImprint { get; }

        public LensDefinition(Action<DBGenistar, int> injectCounts, Action<DBGenistar> onBirthImprint = null)
        {
            InjectCounts = injectCounts;
            OnBirthImprint = onBirthImprint;
        }
    }

    public static class GenistarLensMgr
    {
        public static readonly Dictionary<int, LensDefinition> Lenses = new Dictionary<int, LensDefinition>();

        public static void ApplyLensTick(DBGenistar db, GameInventoryItem lensItem, int elapsedMilliseconds, int playerErudition, GamePlayer owner)
        {
            string lang = owner?.Client?.Account?.Language ?? "EN";

            if (lensItem == null || lensItem.Template == null || lensItem.Template.Flags != 27) return;

            if (Lenses.Count == 0) InitializeLenses();
            int lensId = lensItem.DPS_AF;

            if (lensId == 0)
            {
                owner?.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.LensMgr.InvalidLens"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // 1. Thread-safe String-to-Dict serialization of the Masterpiece Ledger
            Dictionary<int, int> history = ParseLensHistory(db.LensHistory);
            history[lensId] = history.GetValueOrDefault(lensId, 0) + elapsedMilliseconds;
            db.LensHistory = SerializeLensHistory(history);

            // 2. High-speed integer Count injection
            if (Lenses.TryGetValue(lensId, out LensDefinition def))
            {
                // Time Credit: Standard tick is 5000ms (Multiplier = 1). A 50,000ms GM tick = 10.
                int timeCredit = Math.Max(1, elapsedMilliseconds / 5000);

                // Erudition Math: Base weight is 1. Every 3 Erudition levels grants +1 count multiplier.
                int eruditionBonus = 1 + (playerErudition / 3);

                int totalWeight = timeCredit * eruditionBonus;
                def.InjectCounts?.Invoke(db, totalWeight);

                // Convert 10-counts into 1 permanent stat point!
                ProcessRollovers(db, owner);
            }
            else
            {
                owner?.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.LensMgr.UnknownLens", lensId), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private static void ProcessRollovers(DBGenistar db, GamePlayer owner)
        {
            // Core Stats
            CheckAndRoll(db, owner, () => db.CountStrength, v => db.CountStrength = v, () => db.Strength, v => db.Strength = v, "Strength");
            CheckAndRoll(db, owner, () => db.CountConstitution, v => db.CountConstitution = v, () => db.Constitution, v => db.Constitution = v, "Constitution");
            CheckAndRoll(db, owner, () => db.CountDexterity, v => db.CountDexterity = v, () => db.Dexterity, v => db.Dexterity = v, "Dexterity");
            CheckAndRoll(db, owner, () => db.CountQuickness, v => db.CountQuickness = v, () => db.Quickness, v => db.Quickness = v, "Quickness");
            CheckAndRoll(db, owner, () => db.CountIntelligence, v => db.CountIntelligence = v, () => db.Intelligence, v => db.Intelligence = v, "Intelligence");
            CheckAndRoll(db, owner, () => db.CountPiety, v => db.CountPiety = v, () => db.Piety, v => db.Piety = v, "Piety");
            CheckAndRoll(db, owner, () => db.CountEmpathy, v => db.CountEmpathy = v, () => db.Empathy, v => db.Empathy = v, "Empathy");
            CheckAndRoll(db, owner, () => db.CountCharisma, v => db.CountCharisma = v, () => db.Charisma, v => db.Charisma = v, "Charisma");

            // Resists
            CheckAndRoll(db, owner, () => db.CountResistBody, v => db.CountResistBody = v, () => db.ResistBody, v => db.ResistBody = v, "Body Resist");
            CheckAndRoll(db, owner, () => db.CountResistCold, v => db.CountResistCold = v, () => db.ResistCold, v => db.ResistCold = v, "Cold Resist");
            CheckAndRoll(db, owner, () => db.CountResistCrush, v => db.CountResistCrush = v, () => db.ResistCrush, v => db.ResistCrush = v, "Crush Resist");
            CheckAndRoll(db, owner, () => db.CountResistEnergy, v => db.CountResistEnergy = v, () => db.ResistEnergy, v => db.ResistEnergy = v, "Energy Resist");
            CheckAndRoll(db, owner, () => db.CountResistHeat, v => db.CountResistHeat = v, () => db.ResistHeat, v => db.ResistHeat = v, "Heat Resist");
            CheckAndRoll(db, owner, () => db.CountResistMatter, v => db.CountResistMatter = v, () => db.ResistMatter, v => db.ResistMatter = v, "Matter Resist");
            CheckAndRoll(db, owner, () => db.CountResistSlash, v => db.CountResistSlash = v, () => db.ResistSlash, v => db.ResistSlash = v, "Slash Resist");
            CheckAndRoll(db, owner, () => db.CountResistSpirit, v => db.CountResistSpirit = v, () => db.ResistSpirit, v => db.ResistSpirit = v, "Spirit Resist");
            CheckAndRoll(db, owner, () => db.CountResistThrust, v => db.CountResistThrust = v, () => db.ResistThrust, v => db.ResistThrust = v, "Thrust Resist");
            CheckAndRoll(db, owner, () => db.CountResistNatural, v => db.CountResistNatural = v, () => db.ResistNatural, v => db.ResistNatural = v, "Essence Resist");

            // Combat Stats
            CheckAndRoll(db, owner, () => db.CountWeaponDPS, v => db.CountWeaponDPS = v, () => db.WeaponDPS, v => db.WeaponDPS = v, "Weapon DPS");
            CheckAndRoll(db, owner, () => db.CountArmorFactor, v => db.CountArmorFactor = v, () => db.ArmorFactor, v => db.ArmorFactor = v, "Armor Factor");
            CheckAndRoll(db, owner, () => db.CountEvadeChance, v => db.CountEvadeChance = v, () => db.EvadeChance, v => db.EvadeChance = v, "Evade Chance");
            CheckAndRoll(db, owner, () => db.CountBlockChance, v => db.CountBlockChance = v, () => db.BlockChance, v => db.BlockChance = v, "Block Chance");
            CheckAndRoll(db, owner, () => db.CountParryChance, v => db.CountParryChance = v, () => db.ParryChance, v => db.ParryChance = v, "Parry Chance");
            CheckAndRoll(db, owner, () => db.CountLeftHandSwingChance, v => db.CountLeftHandSwingChance = v, () => db.LeftHandSwingChance, v => db.LeftHandSwingChance = v, "Left Hand Swing Chance");
            CheckAndRoll(db, owner, () => db.CountCounterAttackChance, v => db.CountCounterAttackChance = v, () => db.CounterAttackChance, v => db.CounterAttackChance = v, "Counterattack Chance");
            CheckAndRoll(db, owner, () => db.CountProcSpellChance, v => db.CountProcSpellChance = v, () => db.ProcSpellChance, v => db.ProcSpellChance = v, "Proc Spell Chance");
            CheckAndRoll(db, owner, () => db.CountMaxTension, v => db.CountMaxTension = v, () => db.MaxTension, v => db.MaxTension = v, "Max Tension Reduction");

            // Unique Enhancements
            CheckAndRoll(db, owner, () => db.CountSpellmagicABS, v => db.CountSpellmagicABS = v, () => db.SpellmagicABS, v => db.SpellmagicABS = v, "Spell Magic ABS");
            CheckAndRoll(db, owner, () => db.CountDotABS, v => db.CountDotABS = v, () => db.DotABS, v => db.DotABS = v, "Dot ABS");
            CheckAndRoll(db, owner, () => db.CountMeleeABS, v => db.CountMeleeABS = v, () => db.MeleeABS, v => db.MeleeABS = v, "Melee ABS");
            CheckAndRoll(db, owner, () => db.CountMaxHealth, v => db.CountMaxHealth = v, () => db.MaxHealth, v => db.MaxHealth = v, "Max Health");
            CheckAndRoll(db, owner, () => db.CountEffectivenessMod, v => db.CountEffectivenessMod = v, () => db.EffectivenessMod, v => db.EffectivenessMod = v, "Effectiveness Modifier");
            //CheckAndRoll(db, owner, () => db.CountAblativeShield, v => db.CountAblativeShield = v, () => db.AblativeShield, v => db.AblativeShield = v, "Ablative Shield Modifier");
            CheckAndRoll(db, owner, () => db.CountCCResist, v => db.CountCCResist = v, () => db.CCResist, v => db.CCResist = v, "Crowd Control Resist Chance");
            CheckAndRoll(db, owner, () => db.CountDebuffResist, v => db.CountDebuffResist = v, () => db.DebuffResist, v => db.DebuffResist = v, "Debuff Resist Chance");
            CheckAndRoll(db, owner, () => db.CountCastRange, v => db.CountCastRange = v, () => db.CastRange, v => db.CastRange = v, "Cast Range Increase");

            GameServer.Database.SaveObject(db);
        }

        private static void CheckAndRoll(DBGenistar db, GamePlayer owner, Func<int> getCount, Action<int> setCount, Func<int> getStat, Action<int> setStat, string statName)
        {
            int currentCount = getCount();
            if (currentCount >= 10)
            {
                int earned = currentCount / 10;
                int leftover = currentCount % 10;

                setStat(getStat() + earned);
                setCount(leftover);

                string lang = owner?.Client?.Account?.Language ?? "EN";
                owner?.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.LensMgr.Absorbs", earned, statName), eChatType.CT_Chat, eChatLoc.CL_SystemWindow);
            }
        }

        public static Dictionary<int, int> ParseLensHistory(string historyString)
        {
            var dict = new Dictionary<int, int>();
            if (string.IsNullOrEmpty(historyString)) return dict;

            foreach (string pair in historyString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int ms))
                {
                    dict[id] = ms;
                }
            }
            return dict;
        }

        public static string SerializeLensHistory(Dictionary<int, int> dict)
        {
            return string.Join(";", dict.Select(kv => $"{kv.Key}:{kv.Value}"));
        }

        #region Alchemical Care Controller
        public static void EngageCareMode(GamePlayer player, GenistarEgg egg)
        {
            if (player == null || egg == null) return;

            if (!player.IsAfkDelayElapsed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Afk.Wait"), eChatType.CT_Chat, eChatLoc.CL_SystemWindow);
                return;
            }

            player.TempProperties.setProperty("IsAfkCareMode", true);
            player.TempProperties.setProperty("AfkCareTarget", egg);

            if (!player.IsAfkActive())
            {
                player.InitAfkTimers();
                player.SetAFK("AFK Care", showMessage: true);
            }

            RegionTimer emoteTimer = new RegionTimer(player, new RegionTimerCallback(CareEmotePulseCallback));
            emoteTimer.Properties.setProperty("caringPlayer", player);
            emoteTimer.Start(2000);

            player.TempProperties.setProperty("GenistarCareEmoteTimer", emoteTimer);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Genistar.LensMgr.Kneel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public static void DisengageCareMode(GamePlayer player, bool voluntary = true, bool silent = false)
        {
            if (player == null) return;

            RegionTimer emoteTimer = player.TempProperties.getProperty<RegionTimer>("GenistarCareEmoteTimer", null);
            if (emoteTimer != null)
            {
                emoteTimer.Stop();
                player.TempProperties.removeProperty("GenistarCareEmoteTimer");
            }

            player.TempProperties.removeProperty("IsAfkCareMode");
            player.TempProperties.removeProperty("AfkCareTarget");

            if (player.IsAfkActive())
            {
                player.ClearAFK(showMessage: false);
            }

            if (!silent)
            {
                string transKey = voluntary ? "Genistar.LensMgr.BreakFocus" : "Genistar.LensMgr.FocusBroken";
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, transKey), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private static int CareEmotePulseCallback(RegionTimer timer)
        {
            GamePlayer p = timer.Properties.getProperty<GamePlayer>("caringPlayer", null);

            if (p == null || !p.IsAlive || !p.TempProperties.getProperty<bool>("IsAfkCareMode", false))
            {
                timer.Stop();
                return 0;
            }

            p.Out.SendEmoteAnimation(p, eEmote.PlayerPickup);
            return 2000;
        }
        #endregion

        public static void InitializeLenses()
        {
            Lenses.Clear();

            // Category 1: Biometric (1-10) Physical output
            Lenses.Add(1, new LensDefinition((db, w) => { db.CountStrength += w * 2; db.CountEvolveHammer += w; }));
            Lenses.Add(2, new LensDefinition((db, w) => { db.CountConstitution += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(3, new LensDefinition((db, w) => { db.CountStrength += w; db.CountConstitution += w; }));
            Lenses.Add(4, new LensDefinition((db, w) => { db.CountEvadeChance += w; db.CountEvolveThrust += w; }));
            Lenses.Add(5, new LensDefinition((db, w) => { db.CountMaxHealth += w * 5; db.CountEvolveHammer += w; }));
            Lenses.Add(6, new LensDefinition((db, w) => db.CountResistCrush += w));
            Lenses.Add(7, new LensDefinition((db, w) => db.CountResistSlash += w));
            Lenses.Add(8, new LensDefinition((db, w) => db.CountResistThrust += w));
            Lenses.Add(9, new LensDefinition((db, w) => { db.CountArmorFactor += w * 2; db.CountMeleeABS += w; }));
            Lenses.Add(10, new LensDefinition((db, w) => { db.CountWeaponDPS += w * 2; db.CountEvolveSword += w; }));

            // Category 2: Neural (11-20) Mental output
            Lenses.Add(11, new LensDefinition((db, w) => { db.CountIntelligence += w * 2; db.CountEvolveCold += w; }));
            Lenses.Add(12, new LensDefinition((db, w) => { db.CountPiety += w * 2; db.CountEvolveHeal += w; }));
            Lenses.Add(13, new LensDefinition((db, w) => { db.CountEmpathy += w * 2; db.CountEvolveSpecial += w; }));
            Lenses.Add(14, new LensDefinition((db, w) => { db.CountEffectivenessMod += w; db.CountEvolveSpirit += w; }));
            Lenses.Add(15, new LensDefinition((db, w) => { db.CountWeaponSpd += w * 2; db.CountEvolveEnergy += w; }));
            Lenses.Add(16, new LensDefinition((db, w) => db.CountCCResist += w * 2));
            Lenses.Add(17, new LensDefinition((db, w) => { db.CountSpellmagicABS += w; db.CountEvolveMatter += w; }));
            Lenses.Add(18, new LensDefinition((db, w) => { db.CountDotABS += w; db.CountEvolveHeal += w; }));
            Lenses.Add(19, new LensDefinition((db, w) => { db.CountIntelligence += w; db.CountEvolveSpecial += w; }));
            Lenses.Add(20, new LensDefinition((db, w) => { db.CountEvolveEnergy += w; db.CountEvolveHeal += w; }));

            // Category 3: Kinetic (21-30) Agility/Reflex
            Lenses.Add(21, new LensDefinition((db, w) => { db.CountQuickness += w * 2; db.CountEvolveThrust += w; }));
            Lenses.Add(22, new LensDefinition((db, w) => { db.CountWeaponSpd += w * 2; db.CountEvolveEnergy += w; }));
            Lenses.Add(23, new LensDefinition((db, w) => { db.CountWeaponSpd += w; db.CountEvolveThrust += w; }));
            Lenses.Add(24, new LensDefinition((db, w) => db.CountEvadeChance += w * 2, db => db.IsStealthed = true));
            Lenses.Add(25, new LensDefinition((db, w) => { db.CountParryChance += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(26, new LensDefinition((db, w) => { db.CountQuickness += w; db.CountDexterity += w; }));
            Lenses.Add(27, new LensDefinition((db, w) => { db.CountCCResist += w; db.CountEvolveMatter += w; }));
            Lenses.Add(28, new LensDefinition((db, w) => { db.CountWeaponDPS += w; db.CountEvolveThrust += w; }));
            Lenses.Add(29, new LensDefinition((db, w) => db.CountBlockChance += w * 2));
            Lenses.Add(30, new LensDefinition((db, w) => { db.CountQuickness += w; db.CountEvolveThrust += w; }));

            // Category 4: Elemental (31-40) Resists/Nature
            Lenses.Add(31, new LensDefinition((db, w) => db.CountEvolveFire += w * 2, db => db.DamageTypeConvert = "Heat"));
            Lenses.Add(32, new LensDefinition((db, w) => db.CountEvolveCold += w * 2, db => db.DamageTypeConvert = "Cold"));
            Lenses.Add(33, new LensDefinition((db, w) => db.CountEvolveMatter += w * 2, db => db.DamageTypeConvert = "Matter"));
            Lenses.Add(34, new LensDefinition((db, w) => db.CountEvolveEnergy += w * 2, db => db.DamageTypeConvert = "Energy"));
            Lenses.Add(35, new LensDefinition((db, w) => { db.CountResistHeat += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(36, new LensDefinition((db, w) => { db.CountResistCold += w * 2; db.CountEvolveHammer += w; }));
            Lenses.Add(37, new LensDefinition((db, w) => { db.CountResistMatter += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(38, new LensDefinition((db, w) => { db.CountResistEnergy += w * 2; db.CountEvolveHammer += w; }));
            Lenses.Add(39, new LensDefinition((db, w) => { db.CountEvolveFire += w; db.CountEvolveCold += w; }));
            Lenses.Add(40, new LensDefinition((db, w) => { db.CountResistEnergy += w; db.CountResistHeat += w; }));

            // Category 5: Ethereal (41-50) Occult/Undead
            Lenses.Add(41, new LensDefinition((db, w) => { db.CountResistSpirit += w * 2; db.CountEvolveSpirit += w; }));
            Lenses.Add(42, new LensDefinition((db, w) => { db.CountResistBody += w * 2; db.CountEvolveMatter += w; }));
            Lenses.Add(43, new LensDefinition((db, w) => { db.CountEvolveSpecial += w * 2; db.CountEvolveThrust += w; }, db => db.ProcSpellID = "LifedrainProc"));
            Lenses.Add(44, new LensDefinition((db, w) => { db.CountEvolveSpirit += w; db.CountEvolveMatter += w; }));
            Lenses.Add(45, new LensDefinition((db, w) => db.CountEvolveSpirit += w * 2, db => db.IsGhost = true));
            Lenses.Add(46, new LensDefinition((db, w) => db.CountEvolveMatter += w * 2, db => db.IsStealthed = true));
            Lenses.Add(47, new LensDefinition((db, w) => { db.CountEffectivenessMod += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(48, new LensDefinition((db, w) => { db.CountEvolveSpirit += w; db.CountEvolveHammer += w; }, db => db.DamageTypeConvert = "Spirit"));
            Lenses.Add(49, new LensDefinition((db, w) => { db.CountEvolveBody += w; db.CountEvolveSword += w; }, db => db.DamageTypeConvert = "Body"));
            Lenses.Add(50, new LensDefinition((db, w) => { db.CountEvolveSpirit += w; db.CountEvolveHammer += w; }));

            // Category 6: Morphological (51-60) Anatomy overrides
            Lenses.Add(51, new LensDefinition((db, w) => { db.CountArmorFactor += w * 3; db.CountEvolveSpecial += w; }));
            Lenses.Add(52, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveThrust += w; }));
            Lenses.Add(53, new LensDefinition((db, w) => { db.CountEvolveSpecial += w; db.CountEvolveSword += w; }));
            Lenses.Add(54, new LensDefinition((db, w) => { db.CountEvolveMatter += w; db.CountEvolveThrust += w; }));
            Lenses.Add(55, new LensDefinition((db, w) => { db.CountEvolveMatter += w; db.CountEvolveHammer += w; }));
            Lenses.Add(56, new LensDefinition((db, w) => { db.CountResistMatter += w * 3; db.CountEvolveSword += w; }));
            Lenses.Add(57, new LensDefinition((db, w) => { db.CountResistBody += w * 3; db.CountEvolveHammer += w; }));
            Lenses.Add(58, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveSpecial += w; }));
            Lenses.Add(59, new LensDefinition((db, w) => { db.CountWeaponDPS += w * 2; db.CountEvolveThrust += w; }));
            Lenses.Add(60, new LensDefinition((db, w) => { db.CountStrength += w * 2; db.CountEvolveHammer += w; }));

            // Category 7: Behavioral (61-70) AI Modifiers
            Lenses.Add(61, new LensDefinition((db, w) => { db.CountStrength += w * 2; db.CountEvolveHammer += w; }));
            Lenses.Add(62, new LensDefinition((db, w) => { db.CountBlockChance += w * 2; db.CountEvolveHeal += w; }));
            Lenses.Add(63, new LensDefinition((db, w) => { db.CountMeleeABS += w * 2; db.CountEvolveSpecial += w; }));
            Lenses.Add(64, new LensDefinition((db, w) => { db.CountMaxHealth += w * 10; db.CountEvolveHeal += w; }));
            Lenses.Add(65, new LensDefinition((db, w) => { db.CountQuickness += w * 2; db.CountEvolveThrust += w; }));
            Lenses.Add(66, new LensDefinition((db, w) => { db.CountWeaponSpd += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(67, new LensDefinition((db, w) => { db.CountParryChance += w * 2; db.CountEvolveSpecial += w; }));
            Lenses.Add(68, new LensDefinition((db, w) => { db.CountWeaponDPS += w * 2; db.CountEvolveHammer += w; }));
            Lenses.Add(69, new LensDefinition((db, w) => { db.CountEvadeChance += w * 2; db.CountEvolveHeal += w; }));
            Lenses.Add(70, new LensDefinition((db, w) => { db.CountStrength += w * 2; db.CountEvolveThrust += w; }));

            // Category 8: Radiant (71-80) Healing & Support
            Lenses.Add(71, new LensDefinition((db, w) => { db.CountEvolveHeal += w * 2; db.CountEvolveSpecial += w; }));
            Lenses.Add(72, new LensDefinition((db, w) => { db.CountEvolveHeal += w * 2; db.CountIntelligence += w * 2; }));
            Lenses.Add(73, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveSpirit += w; }));
            Lenses.Add(74, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveCold += w; }));
            Lenses.Add(75, new LensDefinition((db, w) => { db.CountEvolveSpecial += w * 2; db.CountQuickness += w * 2; }));
            Lenses.Add(76, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveEnergy += w; }));
            Lenses.Add(77, new LensDefinition((db, w) => { db.CountEvolveSpecial += w; db.CountArmorFactor += w * 2; }));
            Lenses.Add(78, new LensDefinition((db, w) => { db.CountMaxHealth += w * 15; db.CountEvolveHeal += w; }));
            Lenses.Add(79, new LensDefinition((db, w) => { db.CountEvolveFire += w; db.CountEvolveSpecial += w; }));
            Lenses.Add(80, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountResistMatter += w; }));

            // Category 9: Exotic (81-90) Rare Utility
            Lenses.Add(81, new LensDefinition((db, w) => db.CountEvolveHammer += w * 2));
            Lenses.Add(82, new LensDefinition((db, w) => { db.CountConstitution += w * 2; db.CountEvolveSword += w; }));
            Lenses.Add(83, new LensDefinition((db, w) => { db.CountArmorFactor += w * 3; db.CountEvolveHammer += w; }));
            Lenses.Add(84, new LensDefinition((db, w) => db.CountEvolveThrust += w * 2));
            Lenses.Add(85, new LensDefinition((db, w) => { db.CountEvadeChance += w * 3; db.CountEvolveSpecial += w; }));
            Lenses.Add(86, new LensDefinition((db, w) => { db.CountEvolveEnergy += w; db.CountEvolveSpirit += w; }));
            Lenses.Add(87, new LensDefinition((db, w) => { db.CountEvolveFire += w; db.CountEvolveSword += w; }));
            Lenses.Add(88, new LensDefinition((db, w) => { db.CountEvolveHeal += w; db.CountEvolveHammer += w; }));
            Lenses.Add(89, new LensDefinition((db, w) => { db.CountQuickness += w * 2; db.CountEvolveThrust += w; }));
            Lenses.Add(90, new LensDefinition((db, w) => { db.CountEvolveCold += w; db.CountEvolveSword += w; }));

            // Category 10: Chimerical (91-100) Fusions
            Lenses.Add(91, new LensDefinition((db, w) => { db.CountEvolveFire += w; db.CountEvolveHeal += w; db.CountEvolveSword += w; db.CountEvolveHammer += w; }));
            Lenses.Add(92, new LensDefinition((db, w) => { db.CountEvolveMatter += w * 2; db.CountEvolveThrust += w; db.CountEvolveSword += w; }));
            Lenses.Add(93, new LensDefinition((db, w) => { db.CountEvolveSpirit += w; db.CountEvolveCold += w; db.CountEvolveHeal += w; }));
            Lenses.Add(94, new LensDefinition((db, w) => { db.CountMaxHealth += w * 20; db.CountEvolveHammer += w * 2; }));
            Lenses.Add(95, new LensDefinition((db, w) => { db.CountArmorFactor += w * 2; db.CountEvolveSword += w * 2; }, db => db.ErodibleAblative = 1));
            Lenses.Add(96, new LensDefinition((db, w) => { db.CountMeleeABS += w * 2; db.CountEvolveSpecial += w; db.CountEvolveHammer += w; }));
            Lenses.Add(97, new LensDefinition((db, w) => { db.CountWeaponDPS += w * 3; db.CountEvolveEnergy += w; db.CountEvolveThrust += w; }));
            Lenses.Add(98, new LensDefinition((db, w) => { db.CountEffectivenessMod += w * 2; db.CountEvolveHeal += w; db.CountEvolveCold += w; }));
            Lenses.Add(99, new LensDefinition((db, w) => { db.CountCCResist += w * 5; db.CountEvolveSword += w * 2; }));
            Lenses.Add(100, new LensDefinition((db, w) => { db.CountEvolveCold += w; db.CountEvolveFire += w; db.CountEvolveHeal += w; db.CountEvolveMatter += w; db.CountEvolveHammer += w; db.CountEvolveSword += w; db.CountEvolveThrust += w; }));

            // Category 11: Cosmic (101-110) Celestial/Void
            Lenses.Add(101, new LensDefinition((db, w) => { db.CountMaxHealth += w * 25; db.CountEvolveEnergy += w; db.CountEvolveSpirit += w; }));
            Lenses.Add(102, new LensDefinition((db, w) => { db.CountMeleeABS += w * 2; db.CountSpellmagicABS += w * 2; }));
            Lenses.Add(103, new LensDefinition((db, w) => db.CountWeaponDPS += w * 4));
            Lenses.Add(104, new LensDefinition((db, w) => db.CountEvolveFire += w * 3, db => db.DamageTypeConvert = "Energy"));
            Lenses.Add(105, new LensDefinition((db, w) => db.CountEvadeChance += w * 4, db => db.IsStealthed = true));
            Lenses.Add(106, new LensDefinition((db, w) => { db.CountWeaponSpd += w * 4; db.CountEvolveThrust += w * 2; }));
            Lenses.Add(107, new LensDefinition((db, w) => db.CountEvolveCC += w * 3));
            Lenses.Add(108, new LensDefinition((db, w) => { db.CountCCResist += w * 4; db.CountResistSpirit += w * 2; }));
            Lenses.Add(109, new LensDefinition((db, w) => { db.CountEvolveFire += w * 2; db.CountEvolveCold += w * 2; db.CountEffectivenessMod += w * 2; }));
            Lenses.Add(110, new LensDefinition((db, w) => { db.CountResistBody += w; db.CountResistEnergy += w; db.CountResistHeat += w; db.CountResistCold += w; db.CountResistMatter += w; db.CountResistSpirit += w; }));

            // Category 12: Apex (111-120) Legendary Tier
            Lenses.Add(111, new LensDefinition((db, w) => { db.CountStrength += w * 5; db.CountConstitution += w * 5; db.CountEvolveHammer += w * 2; }));
            Lenses.Add(112, new LensDefinition((db, w) => { db.CountIntelligence += w * 5; db.CountPiety += w * 5; db.CountEmpathy += w * 5; db.CountEvolveSpirit += w; db.CountEvolveHeal += w; }));
            Lenses.Add(113, new LensDefinition((db, w) => { db.CountMaxHealth += w * 30; db.CountResistCrush += w * 2; db.CountResistSlash += w * 2; db.CountResistThrust += w * 2; }));
            Lenses.Add(114, new LensDefinition((db, w) => db.CountEvolveSword += w * 3, db => db.AblativeShield = 0.30));
            Lenses.Add(115, new LensDefinition((db, w) => { db.CountArmorFactor += w * 5; db.CountEvolveHammer += w; db.CountEvolveSword += w; db.CountEvolveThrust += w; }));
            Lenses.Add(116, new LensDefinition((db, w) => { db.CountEffectivenessMod += w * 5; db.CountEvolveFire += w; db.CountEvolveCold += w; db.CountEvolveEnergy += w; db.CountEvolveSpirit += w; db.CountEvolveMatter += w; }));
            Lenses.Add(117, new LensDefinition((db, w) => { db.CountQuickness += w * 5; db.CountEvolveThrust += w * 3; }, db => db.IsStealthed = true));
            Lenses.Add(118, new LensDefinition((db, w) => { db.CountEvolveHeal += w * 3; db.CountEvolveSpecial += w * 2; db.CountMaxHealth += w * 20; }));
            Lenses.Add(119, new LensDefinition((db, w) => { db.CountMeleeABS += w * 4; db.CountSpellmagicABS += w * 4; }));
            Lenses.Add(120, new LensDefinition((db, w) => { db.CountWeaponDPS += w * 5; db.CountArmorFactor += w * 5; db.CountMaxHealth += w * 25; }));
        }
    }
}