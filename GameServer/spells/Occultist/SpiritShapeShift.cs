using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Numerics;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Self-morph: Ghostly Spirit form.
    /// - Model chosen from Spell.LifeDrainReturn
    /// - Absorb Spell.Value% of incoming damage, convert absorbed damage into Power
    /// - Extra Power regen per tick: round(Level * (Spell.ResurrectMana / 100))
    /// - Stealth detection increased by Spell.AmnesiaChance
    /// </summary>
    [SpellHandler("SpiritShapeShift")]
    public class SpiritShapeShift : AbstractMorphSpellHandler
    {
        // --- Store absorb % (Spell.Value) ---
        private int m_absorbPct;

        // --- Extra Power Regen (flat), scaling by Level and ResurrectMana% ---
        // extra = round(Level * ResurrectMana / 100)
        private int m_regenBonus;

        public SpiritShapeShift(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
            // Priority = 10;

            m_absorbPct = Math.Max(0, (int)Spell.Value);
            m_regenBonus = Spell.ResurrectMana;
            if (m_regenBonus != 0)
            {
                m_regenBonus = (int)Math.Round(caster.Level * (m_regenBonus / 100.0));
            }
        }

        public override bool HasPositiveEffect => true;

        public override bool HasPositiveOrSpeedEffect() => true;

        public override bool IsCancellable(GameSpellEffect compare)
        {
            if (compare.SpellHandler is BringerOfDeath)
                return true;

            return base.IsCancellable(compare);
        }

        public override ushort GetModelFor(GameLiving living)
        {
            return (ushort)Spell.LifeDrainReturn;
        }

        public override bool CheckBeginCast(GameLiving target, bool quiet)
        {
            if (Caster.TempProperties.getProperty<bool>(OccultistForms.KEY_SPIRIT, false) ||
                Caster.TempProperties.getProperty<bool>(OccultistForms.KEY_DECREPIT, false) ||
                Caster.TempProperties.getProperty<bool>(OccultistForms.KEY_CHTONIC, false))
            {
                if (!quiet)
                    MessageTranslationToCaster("SpellHandler.Occultist.CastCondition4", eChatType.CT_System);
                return false;
            }

            if (Caster is GamePlayer player && player.IsRiding)
            {
                if (!quiet)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CannotCastRiding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                return false;
            }

            return base.CheckBeginCast(target, quiet);
        }

        public void ToggleEffects(GameLiving target, bool apply)
        {
            target.TempProperties.setProperty(OccultistForms.KEY_SPIRIT, apply);

            var sign = (sbyte)(apply ? 1 : -1);
            if (m_regenBonus != 0)
            {
                target.BaseBuffBonusCategory[eProperty.PowerRegenerationRate] += sign * m_regenBonus;
            }

            if (Spell.AmnesiaChance != 0)
            {
                target.BaseBuffBonusCategory[(int)eProperty.StealthDetectionBonus] += sign * Spell.AmnesiaChance;
            }

            // --- Unified event hook for incoming damage to perform absorb->mana conversion ---
            if (apply)
                GameEventMgr.AddHandler(target, GameLivingEvent.AttackedByEnemy, OnAttackedByEnemy);
            else
                GameEventMgr.RemoveHandler(target, GameLivingEvent.AttackedByEnemy, OnAttackedByEnemy);

            if (target is GamePlayer gp)
            {
                gp.Out.SendUpdateWeaponAndArmorStats();
                gp.Out.SendCharStatsUpdate();
                gp.UpdatePlayerStatus();
                gp.Out.SendUpdatePlayer();
            }
            else
            {
                target.UpdateHealthManaEndu();
            }

            // if caster already has pets, flip them to Spirit template
            if (target is GamePlayer occOwner)
            {
                OccultistForms.SetOccultistPetForm(occOwner, apply);
            }
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            var owner = effect.Owner;

            ToggleEffects(owner, true);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            var owner = effect.Owner;

            owner.TempProperties.removeProperty(OccultistForms.KEY_SPIRIT);

            ToggleEffects(effect.Owner, false);

            return base.OnEffectExpires(effect, noMessages);
        }

        /// <summary>
        /// Absorb Spell.Value% of the incoming hit and convert it to Power (Mana).
        /// Applies to all damaging hits (melee, ranged, spells, DoTs).
        /// </summary>
        private void OnAttackedByEnemy(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is not AttackedByEnemyEventArgs { AttackData: { } ad })
                return;

            var owner = (GameLiving)sender;

            // Only apply on actual damaging hits
            if (ad.Damage + ad.CriticalDamage <= 0)
                return;

            if (m_absorbPct <= 0)
                return;

            // Subtract from the hit
            var pct = (m_absorbPct / 100.0);
            int reduceBase = (int)Math.Round(ad.Damage * pct);
            int reduceCrit = (int)Math.Round(ad.CriticalDamage * pct);
            ad.Damage -= reduceBase;
            ad.CriticalDamage -= reduceCrit;

            // Convert absorbed amount into power (positive change)
            int total = reduceBase + reduceCrit;
            if (total > 0)
            {
                int changed = owner.ChangeMana(Caster, GameLiving.eManaChangeType.Spell, total);

                if (owner is GamePlayer p && changed > 0)
                {
                    p.SendTranslatedMessage("SpellHandler.Occultist.Self.AbsorbToPower",
                        eChatType.CT_Spell, eChatLoc.CL_SystemWindow, changed);
                }
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int absorb = (int)Spell.Value;
            int stealthDet = Spell.AmnesiaChance;
            double perLevel = Spell.ResurrectMana / 100.0;

            string description1 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.SpiritShapeShift1", absorb);
            string description2 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.SpiritShapeShift2", perLevel, stealthDet);
            string description3 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.SpiritShapeShift3");

            return description1 + "\n" + description2 + "\n" + description3;
        }
    }
}
