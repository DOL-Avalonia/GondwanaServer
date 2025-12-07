//Andraste v2.0 -Vico

using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using DOL.GS.Keeps;
using DOL.Language;
using System.Numerics;

namespace DOL.GS.Spells
{
    [SpellHandler("SiegeArrow")]
    public class SiegeArrow : BoltSpellHandler
    {
        /// <summary>
        /// Does this spell break stealth on start?
        /// </summary>
        public override bool UnstealthCasterOnStart
        {
            get { return false; }
        }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (Caster != null && Caster is GamePlayer player && Caster.AttackWeapon != null && (Caster.AttackWeapon.Object_Type == 15 || Caster.AttackWeapon.Object_Type == 18 || Caster.AttackWeapon.Object_Type == 9))
            {
                if (!(selectedTarget is GameKeepComponent || selectedTarget is Keeps.GameKeepDoor))
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.SiegeArrow.InvalidTarget"), eChatType.CT_Spell);
                    return false;
                }
                return base.CheckBeginCast(selectedTarget, quiet);
                /*if (!Caster.IsWithinRadius(selectedTarget, Spell.Range))
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.TargetTooFar"), eChatType.CT_Spell);
                    return false;
                }*/
            }
            return false;
        }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (!(target is GameKeepComponent || target is Keeps.GameKeepDoor))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.SiegeArrow.InvalidComponentTarget"), eChatType.CT_SpellResisted);
                return;
            }

            target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
            Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;

            base.FinishSpellCast(target, force);
        }

        public override void SendSpellMessages()
        {
            if (Caster is GamePlayer player)
            {
                MessageToCaster(LanguageMgr.GetTranslation(player.Client, "SpellHandler.SiegeArrow.PrepareSpell", Spell.Name), eChatType.CT_Spell);
            }
        }

        public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = base.CalculateDamageToTarget(target, effectiveness);
            ad.Damage *= 30;  // actual value unknown
            return ad;
        }

        public override int CalculateToHitChance(GameLiving target)
        {
            if ((target is GameKeepComponent || target is Keeps.GameKeepDoor))
                return 100;

            return 0;
        }

        public override int CalculatePowerCost(GameLiving target) { return 0; }

        public override int CalculateEnduranceCost() { return (int)(Caster.MaxEndurance * (Spell.Power * .01)); }

        public SiegeArrow(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
