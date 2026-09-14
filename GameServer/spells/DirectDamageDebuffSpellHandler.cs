using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Events;
using log4net;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Damages the target and lowers their resistance to the spell's type.
    /// </summary>
    [SpellHandler("DirectDamageWithDebuff")]
    public class DirectDamageDebuffSpellHandler : AbstractResistDebuff
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public override eProperty Property1 { get { return Caster.GetResistTypeForDamage(Spell.DamageType); } }
        public override string DebuffTypeName { get { return GlobalConstants.DamageTypeToName(Spell.DamageType); } }

        /// <summary>
        /// execute direct effect
        /// </summary>
        /// <param name="target">target that gets the damage</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null)
                return false;

            if (NeedsEndOfCastLosCheck(target) && StartEndOfCastLosCheck(target, effectiveness))
                return true;

            DealDamage(target, effectiveness);
            return true;
        }

        public override void OnEndOfCastLosCheck(GameLiving target, double effectiveness, LosCheckResponse response)
        {
            if (Caster == null || Caster.ObjectState != GameObject.eObjectState.Active)
                return;
            if (response == LosCheckResponse.False)
                return;

            DealDamage(target, effectiveness);
            GameEventMgr.Notify(GameLivingEvent.CastFinished, m_caster, new CastingEventArgs(this, target, m_lastAttackData));
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if ((target is Keeps.GameKeepComponent) == false && (target is Keeps.GameKeepDoor) == false)
            {
                if (!base.ApplyEffectOnTarget(target, effectiveness))
                    return false;
            }

            if ((Spell.Duration > 0 && Spell.Target != "area") || Spell.Concentration > 0)
                OnDirectEffect(target, effectiveness);

            return true;
        }

        private void DealDamage(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return;

            if (target is Keeps.GameKeepDoor || target is Keeps.GameKeepComponent)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NoEffectOnKeepComponent"), eChatType.CT_SpellResisted);
                return;
            }
            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            SendDamageMessages(ad);
            DamageTarget(ad, true);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
            /*
			if (target.IsAlive)
				base.ApplyEffectOnTarget(target, effectiveness);*/
        }

        public override void OnSpellResisted(GameLiving target)
        {
            if (NeedsResistLosCheck(target) && StartResistLosCheck(target))
                return;

            base.OnSpellResisted(target);
        }

        /// <summary>
        /// Delve Info
        /// </summary>
        public override IList<string> DelveInfo
        {
            get
            {
                /*
				<Begin Info: Lesser Raven Bolt>
				Function: dmg w/resist decrease
 
				Damages the target, and lowers the target's resistance to that spell type.
 
				Damage: 32
				Resist decrease (Cold): 10%
				Target: Targetted
				Range: 1500
				Duration: 1:0 min
				Power cost: 5
				Casting time:      3.0 sec
				Damage: Cold
 
				<End Info>
				*/

                var list = new List<string>();

                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DirectDamageDebuffSpellHandler.DelveInfo.Function"));
                list.Add(" "); //empty line
                list.Add(Spell.Description);
                list.Add(" "); //empty line
                if (Spell.Damage != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Damage", Spell.Damage.ToString("0.###;0.###'%'")));
                if (Spell.Value != 0)
                    list.Add(String.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DirectDamageDebuffSpellHandler.DelveInfo.Decrease", DebuffTypeName, Spell.Value)));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + " min"));
                else if (Spell.Duration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Frequency != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Frequency", (Spell.Frequency * 0.001).ToString("0.0")));
                if (Spell.Power != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Concentration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.ConcentrationCost", Spell.Concentration));
                if (Spell.Radius != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Radius", Spell.Radius));
                if (Spell.DamageType != eDamageType.Natural)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Damage", GlobalConstants.DamageTypeToName(Spell.DamageType)));

                return list;
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DirectDamageWithDebuff.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType), LanguageMgr.GetProperty(delveClient, Property1), Spell.Value);

            if (Spell.SubSpellID != 0)
            {
                Spell subSpell = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                if (subSpell != null)
                {
                    ISpellHandler subSpellHandler = ScriptMgr.CreateSpellHandler(m_caster, subSpell, null);
                    if (subSpellHandler != null)
                    {
                        string subspelldesc = subSpellHandler.GetDelveDescription(delveClient);
                        description += "\n\n" + subspelldesc;
                    }
                }
            }

            if (Spell.IsSecondary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.SecondarySpell");
                description += "\n\n" + secondaryMessage;
            }

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + thirdDesc;
            }

            return description;
        }

        public DirectDamageDebuffSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
