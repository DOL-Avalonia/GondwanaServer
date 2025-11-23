
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

namespace DOL.GS.Spells
{
    public class AdrenalineSpellEffect : GameSpellEffect
    {
        /// <inheritdoc />
        public AdrenalineSpellEffect(ISpellHandler handler, int duration, int pulseFreq) : base(handler, duration, pulseFreq)
        {
        }

        /// <inheritdoc />
        public AdrenalineSpellEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness) : base(handler, duration, pulseFreq, effectiveness)
        {
        }
    }
    public class AdrenalineStealthSpellEffect : GameSpellEffect
    {
        /// <inheritdoc />
        public AdrenalineStealthSpellEffect(ISpellHandler handler, int duration, int pulseFreq) : base(handler, duration, pulseFreq)
        {
        }

        /// <inheritdoc />
        public AdrenalineStealthSpellEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness) : base(handler, duration, pulseFreq, effectiveness)
        {
        }
    }
    public class AdrenalineMageSpellEffect : GameSpellEffect
    {
        public static readonly int HIT_BONUS = 25;

        /// <inheritdoc />
        public AdrenalineMageSpellEffect(ISpellHandler handler, int duration, int pulseFreq) : base(handler, duration, pulseFreq)
        {
        }

        /// <inheritdoc />
        public AdrenalineMageSpellEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness) : base(handler, duration, pulseFreq, effectiveness)
        {
        }
    }

    [SpellHandler("BattleFeverDurationBuff")]
    public class BattleFeverDurationBuffHandler : SpellHandler
    {
        /// <inheritdoc />
        public BattleFeverDurationBuffHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.BattleFeverDurationBuff.MainDescription", Spell.Value);
        }
    }


    public abstract class AdrenalineSpellHandler : SpellHandler
    {
        public static readonly int RANGED_ADRENALINE_SPELL_ID = 28003;
        public static readonly int MAGE_ADRENALINE_SPELL_ID = 28002;
        public static readonly int TANK_ADRENALINE_SPELL_ID = 28001;

        public AdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        public override bool CanBeRightClicked => false;

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            lock (effect.Owner.EffectList)
            {
                foreach (IGameEffect otherEffect in effect.Owner.EffectList)
                {
                    GameSpellEffect gsp = otherEffect as GameSpellEffect;
                    if (gsp == null)
                        continue;
                    if (gsp is GameSpellAndImmunityEffect { ImmunityState: true })
                        continue; // ignore immunity effects
                    if (gsp.SpellHandler is { HasPositiveEffect: true }) // only enemy spells are affected
                        continue;
                    if (gsp.SpellHandler is { IsUnPurgeAble: true }) // do not purge unpurgeable
                        continue;
                    gsp.Cancel(false);
                }
            }
        }

        /// <inheritdoc />
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            var baseDuration =  base.CalculateEffectDuration(target, effectiveness);
            double rate = 1.00f;

            target.FindEffectsOnTarget(typeof(BattleFeverDurationBuffHandler)).ForEach(e => rate += e.Spell.Value / 100);

            return (int)(baseDuration * rate + 0.5f);
        }

        /// <inheritdoc />
        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new AdrenalineSpellEffect(this, this.CalculateEffectDuration(target, effectiveness), 0);
        }
    }

    [SpellHandler("AdrenalineTank")]
    public class TankAdrenalineSpellHandler : AdrenalineSpellHandler
    {
        public readonly int MELEESPEED_BONUS = 40;

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.TankAdrenaline.MainDescription");
        }

        /// <inheritdoc />
        public TankAdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] += MELEESPEED_BONUS;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
                player.Out.SendUpdateWeaponAndArmorStats();
            }
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public override void OnEffectRemove(GameSpellEffect effect, bool overwrite)
        {
            base.OnEffectRemove(effect, overwrite);

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] -= MELEESPEED_BONUS;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
                player.Out.SendUpdateWeaponAndArmorStats();
            }
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public void OnOutgoingAttack(DOLEvent e, object sender, EventArgs args)
        {
            AttackData ad = (args as AttackStartedEventArgs)?.AttackData;

            if (ad == null)
            {
                return;
            }

            if (ad.Style != null && ad.Target.EffectiveLevel < ad.Attacker.EffectiveLevel + 10)
            {
                ad.FumbleChance = 0;
                ad.MissChance = 0;
            }

            if (ad.AttackType is AttackData.eAttackType.MeleeDualWield or AttackData.eAttackType.MeleeOneHand or AttackData.eAttackType.MeleeTwoHand)
            {
                ad.criticalChance += ad.Target is GamePlayer ? 10 : 35;
            }
        }

        /// <inheritdoc />
        public void OnIncomingHit(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AttackedByEnemyEventArgs { AttackData: { AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle } ad })
            {
                return;
            }

            double absorbPercent = 0.50;
            if (ad.AttackResult is not GameLiving.eAttackResult.HitUnstyled and not GameLiving.eAttackResult.HitStyle)
                return;
            if (ad.AttackType is AttackData.eAttackType.Spell or AttackData.eAttackType.DoT)
                absorbPercent = 0.25;

            int damageAbsorbed = (int)(absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (ad.Target is GamePlayer player)
                player.SendTranslatedMessage("Adrenaline.Self.Absorb", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
            if (ad.Attacker is GamePlayer attacker)
                attacker.SendTranslatedMessage("Adrenaline.Target.Absorbs", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
        }
    }

    [SpellHandler("AdrenalineMage")]
    public class MageAdrenalineSpellHandler : AdrenalineSpellHandler
    {
        public static readonly int CASTSPEED_BONUS = 30;

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MageAdrenaline.MainDescription");
        }

        /// <inheritdoc />
        public MageAdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new AdrenalineMageSpellEffect(this, CalculateEffectDuration(target, effectiveness), 0);
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            effect.Owner.OtherBuffBonus[eProperty.CastingSpeed] += CASTSPEED_BONUS;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
            }
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public override void OnEffectRemove(GameSpellEffect effect, bool overwrite)
        {
            base.OnEffectRemove(effect, overwrite);

            effect.Owner.OtherBuffBonus[eProperty.CastingSpeed] -= CASTSPEED_BONUS;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
            }
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public void OnOutgoingAttack(DOLEvent e, object sender, EventArgs args)
        {
            AttackData ad = (args as AttackStartedEventArgs)?.AttackData;

            if (ad == null)
            {
                return;
            }

            if (ad.AttackType is AttackData.eAttackType.Spell or AttackData.eAttackType.DoT)
            {
                ad.criticalChance += ad.Target is GamePlayer ? 10 : 25;
            }
        }

        /// <inheritdoc />
        public void OnIncomingHit(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AttackedByEnemyEventArgs { AttackData: { AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle } ad })
            {
                return;
            }

            int damageAbsorbed = (int)(0.50 * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (ad.Target is GamePlayer player)
                player.SendTranslatedMessage("Adrenaline.Self.Absorb", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
            if (ad.Attacker is GamePlayer attacker)
                attacker.SendTranslatedMessage("Adrenaline.Target.Absorbs", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
        }
    }

    [SpellHandler("AdrenalineStealth")]
    public class StealthAdrenalineSpellHandler : AdrenalineSpellHandler
    {
        public readonly int MELEESPEED_BONUS = 30;
        public readonly int RANGEDSPEED_BONUS = 20;
        public readonly double MAXSPEED_BONUS = 1.25;
        public readonly int STEALTHEFFECTIVENESS_BONUS = 30;
        public readonly int STEALTHDETECTION_BONUS = 30;

        public StealthAdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.StealthAdrenaline.MainDescription");
        }

        /// <inheritdoc />
        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new AdrenalineStealthSpellEffect(this, CalculateEffectDuration(target, effectiveness), 0);
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] += MELEESPEED_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.ArcherySpeed] += RANGEDSPEED_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StealthEffectivenessBonus] += STEALTHEFFECTIVENESS_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StealthDetectionBonus] += STEALTHDETECTION_BONUS;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, MAXSPEED_BONUS);
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendUpdateMaxSpeed();
            }
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public override void OnEffectRemove(GameSpellEffect effect, bool overwrite)
        {
            base.OnEffectRemove(effect, overwrite);

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] -= MELEESPEED_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.ArcherySpeed] -= RANGEDSPEED_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StealthEffectivenessBonus] -= STEALTHEFFECTIVENESS_BONUS;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.StealthDetectionBonus] -= STEALTHDETECTION_BONUS;
            effect.Owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendUpdateMaxSpeed();
            }
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackStarted, OnOutgoingAttack);
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, OnIncomingHit);
        }

        /// <inheritdoc />
        public void OnOutgoingAttack(DOLEvent e, object sender, EventArgs args)
        {
            AttackData ad = (args as AttackStartedEventArgs)?.AttackData;

            if (ad == null)
            {
                return;
            }

            if (ad.Style != null && ad.Target.EffectiveLevel < ad.Attacker.EffectiveLevel + 5)
            {
                ad.FumbleChance = 0;
                ad.MissChance = 0;
            }

            if (ad.AttackType is AttackData.eAttackType.MeleeDualWield or AttackData.eAttackType.MeleeOneHand or AttackData.eAttackType.MeleeTwoHand or AttackData.eAttackType.Ranged)
            {
                ad.criticalChance += ad.Target is GamePlayer ? 15 : 25;
            }
        }

        /// <inheritdoc />
        public void OnIncomingHit(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AttackedByEnemyEventArgs { AttackData: { AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle } ad })
            {
                return;
            }

            double absorbPercent = 0.40;
            if (ad.AttackType is AttackData.eAttackType.Spell or AttackData.eAttackType.DoT)
                absorbPercent = 0.20;

            int damageAbsorbed = (int)(absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (ad.Target is GamePlayer player)
                player.SendTranslatedMessage("Adrenaline.Self.Absorb", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
            if (ad.Attacker is GamePlayer attacker)
                attacker.SendTranslatedMessage("Adrenaline.Target.Absorbs", eChatType.CT_Spell, eChatLoc.CL_SystemWindow, damageAbsorbed);
        }
    }
}
