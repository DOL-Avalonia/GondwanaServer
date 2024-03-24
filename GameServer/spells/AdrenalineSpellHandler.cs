
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
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

    public abstract class AdrenalineSpellHandler : SpellHandler
    {
        public static readonly int RANGED_ADRENALINE_SPELL_ID = 28003;
        public static readonly int MELEE_ADRENALINE_SPELL_ID = 28001;

        public AdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

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
                    if (gsp.SpellHandler.HasPositiveEffect) // only enemy spells are affected
                        continue;
                    gsp.Cancel(false);
                }
            }
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

        /// <inheritdoc />
        public override string ShortDescription => "You are taken over by battle fever! Your styled attacks against evenly matched enemies cannot miss and your defense and your melee power are greatly enhanced!";

        /// <inheritdoc />
        public TankAdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] += 40;
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

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] -= 40;
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
            AttackData ad = (args as AttackedByEnemyEventArgs)?.AttackData;

            if (ad == null)
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

    [SpellHandler("AdrenalineStealth")]
    public class StealthAdrenalineSpellHandler : AdrenalineSpellHandler
    {

        /// <inheritdoc />
        public override string ShortDescription => "You are taken over by battle fever! Your styled attacks against evenly matched enemies cannot miss and your defense and your melee power are greatly enhanced!";

        /// <inheritdoc />
        public StealthAdrenalineSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
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

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] += 30;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.ArcherySpeed] += 20;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, 2);
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

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.MeleeSpeed] -= 30;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.ArcherySpeed] -= 20;
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
            AttackData ad = (args as AttackedByEnemyEventArgs)?.AttackData;

            if (ad is not { AttackResult: not GameLiving.eAttackResult.HitUnstyled and not GameLiving.eAttackResult.HitStyle })
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
