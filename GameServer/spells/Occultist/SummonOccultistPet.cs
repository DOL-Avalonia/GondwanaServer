using System;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Occultist summoning handler: gives an OccultistPetBrain and enforces level scaling with owner.
    /// - Spell.Value can optionally cap the pet level (0 => no cap).
    /// - If you set Spell.Damage >= 0 in DB, that becomes a fixed pet level (override).
    ///   Otherwise we scale from owner level (-100% by default).
    /// </summary>
    [SpellHandler("SummonOccultistPet")]
    public class SummonOccultistPet : SummonSpellHandler
    {
        public SummonOccultistPet(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (Caster is GamePlayer gp && gp.ControlledBrain != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation(gp.Client, "Summon.CheckBeginCast.AlreadyHaveaPet"),
                    eChatType.CT_SpellResisted);
                return false;
            }
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            if (Caster is GamePlayer owner)
            {
                new RegionTimer(owner, _ =>
                {
                    SetupOccultistPet(owner);
                    return 0;
                }).Start(1);
            }
        }

        private static int ReadIntParam(Spell spell, string key, int fallback = 0)
        {
            if (spell.CustomParamsDictionary != null &&
                spell.CustomParamsDictionary.TryGetValue(key, out var list) &&
                list is { Count: > 0 } &&
                int.TryParse(list[0], out var v))
            {
                return v;
            }
            return fallback;
        }

        private static double ReadDoubleParam(Spell spell, string key, double fallback = 0)
        {
            if (spell.CustomParamsDictionary != null &&
                spell.CustomParamsDictionary.TryGetValue(key, out var list) &&
                list is { Count: > 0 } &&
                double.TryParse(list[0], System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
            return fallback;
        }

        private void SetupOccultistPet(GamePlayer owner)
        {
            var brain = owner.ControlledBrain;
            if (brain?.Body is not GamePet pet) return;

            // ----- 1) Level Scaling via GamePet’s built-in fields -----
            // SummonSpellDamage: negative = % of owner level (e.g., -100 == match owner)
            // SummonSpellValue : optional hard cap
            double pct = ReadDoubleParam(Spell, "PetLevelPct", 100.0);
            int cap = ReadIntParam(Spell, "PetLevelCap", 0);

            pet.SummonSpellDamage = -(Math.Abs(pct));
            pet.SummonSpellValue = Math.Max(0, cap);

            if (pet.SetPetLevel())
            {
                pet.AutoSetStats();
                pet.SortSpells(owner.Level);
            }

            // ----- 2) Store base/spirit template ids for SpiritShapeShift flipping -----
            // Prefer what SummonSpellHandler already set on the pet; only fill if missing.
            int baseTplId = pet.TempProperties.getProperty<int>(OccultistForms.PET_BASE_TPL, 0);
            int spiritTplId = pet.TempProperties.getProperty<int>(OccultistForms.PET_SPIRIT_TPL, 0);

            if (baseTplId <= 0)
                baseTplId = Spell.LifeDrainReturn;

            if (spiritTplId <= 0)
                spiritTplId = ReadIntParam(Spell, "SpiritPetTplId", Spell.AmnesiaChance);

            if (baseTplId > 0)
                pet.TempProperties.setProperty(OccultistForms.PET_BASE_TPL, baseTplId);

            if (spiritTplId > 0)
                pet.TempProperties.setProperty(OccultistForms.PET_SPIRIT_TPL, spiritTplId);

            if (OccultistForms.IsSpiritLikeActive(owner) && spiritTplId > 0)
            {
                OccultistForms.ApplyTemplateFromPetMemory(pet, toSpirit: true);
            }

            owner.Out?.SendObjectUpdate(pet);
            owner.Out?.SendPetWindow(pet, ePetWindowAction.Update, brain.AggressionState, brain.WalkState);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc;
            switch (Spell.ID)
            {
                case 25159:
                    // Umbral Aegis
                    mainDesc =
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.UmbralAegis1") + "\n\n" +
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.UmbralAegis2");
                    break;

                case 25160:
                    // Soultorn
                    mainDesc =
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.Soultorn1") + "\n\n" +
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.Soultorn2");
                    break;

                case 25161:
                    // Umbral Hulk
                    mainDesc =
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.UmbralHulk1") + "\n\n" +
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.UmbralHulk2");
                    break;

                case 25162:
                    // Image of Arawn (healer)
                    mainDesc =
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.ImageOfArawn1") + "\n\n" +
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.ImageOfArawn2");
                    break;

                case 25163:
                    // Succubus / Arch Demon (CC/debuff)
                    mainDesc =
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.SuccubusArchDemon1") + "\n\n" +
                        LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.Summon.SuccubusArchDemon2");
                    break;

                default:
                    mainDesc = " ";
                    break;
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            switch (Spell.ID)
            {
                case 25159:
                case 25160:
                case 25161:
                case 25162:
                case 25163:
                    {
                        var viewer = delveClient?.Player;
                        if (viewer?.CharacterClass.ID == (int)eCharacterClass.Occultist)
                        {
                            mainDesc += "\n\n" + LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.ConditionDescription1");
                        }
                        break;
                    }
            }

            return mainDesc;
        }
    }
}