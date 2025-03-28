/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;

using log4net;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    public abstract class BaseProcSpellHandler : SpellHandler
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        protected BaseProcSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine)
            : base(caster, spell, spellLine)
        {
            m_procSpellLine = SkillBase.GetSpellLine(SubSpellLineName);
            m_procSpell = SkillBase.GetSpellByID((int)spell.Value);
        }

        protected abstract DOLEvent EventType { get; }
        protected abstract string SubSpellLineName { get; }
        protected abstract void EventHandler(DOLEvent e, object sender, EventArgs arguments);
        protected Spell m_procSpell;
        protected SpellLine m_procSpellLine;

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;
            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            return (int)duration;
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            eChatType chatType = Spell.Pulse == 0 ? eChatType.CT_Spell : eChatType.CT_SpellPulse;

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            if (ownerPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                MessageToLiving(effect.Owner, message1, chatType);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, effect.Owner.GetName(0, false));
                MessageToLiving(effect.Owner, message1, chatType);
            }

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (effect.Owner != player)
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, chatType, eChatLoc.CL_SystemWindow);
                }
            }
            GameEventMgr.AddHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

                if (ownerPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(ownerPlayer);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, effect.Owner.GetName(0, false));
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (effect.Owner != player)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            GameEventMgr.RemoveHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
            return 0;
        }

        public override bool IsNewEffectBetter(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            Spell oldProcSpell = SkillBase.GetSpellByID((int)oldeffect.Spell.Value);
            Spell newProcSpell = SkillBase.GetSpellByID((int)neweffect.Spell.Value);

            if (oldProcSpell == null || newProcSpell == null)
                return true;

            // do not replace active proc with different type proc
            if (oldProcSpell.SpellType != newProcSpell.SpellType) return false;

            if (oldProcSpell.Concentration > 0) return false;

            // if the new spell does less damage return false
            if (oldProcSpell.Damage > newProcSpell.Damage) return false;

            // if the new spell is lower than the old one return false
            if (oldProcSpell.Value > newProcSpell.Value) return false;

            //makes problems for immunity effects
            if (oldeffect is GameSpellAndImmunityEffect == false || ((GameSpellAndImmunityEffect)oldeffect).ImmunityState == false)
            {
                if (neweffect.Duration <= oldeffect.RemainingTime) return false;
            }

            return true;
        }
        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (compare.Spell.SpellType != Spell.SpellType)
                return false;
            Spell oldProcSpell = SkillBase.GetSpellByID((int)Spell.Value);
            Spell newProcSpell = SkillBase.GetSpellByID((int)compare.Spell.Value);
            if (oldProcSpell == null || newProcSpell == null)
                return true;
            if (oldProcSpell.SpellType != newProcSpell.SpellType)
                return false;
            return true;
        }

        public override PlayerXEffect GetSavedEffect(GameSpellEffect e)
        {
            PlayerXEffect eff = new PlayerXEffect();
            eff.Var1 = Spell.ID;
            eff.Duration = e.RemainingTime;
            eff.IsHandler = true;
            eff.Var2 = (int)(Spell.Value * e.Effectiveness);
            eff.SpellLine = SpellLine.KeyName;
            return eff;
        }

        public override void OnEffectRestored(GameSpellEffect effect, int[] vars)
        {
            GameEventMgr.AddHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
            if (!noMessages && Spell.Pulse == 0)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

                if (ownerPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(ownerPlayer);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, effect.Owner.GetName(0, false));
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (effect.Owner != player)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);
                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            return 0;
        }

        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();

                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "ProcSpellHandler.DelveInfo.Function", (string)(Spell.SpellType == "" ? "(not implemented)" : Spell.SpellType)));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Duration") + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + "min"));

                else if (Spell.Duration != 0) list.Add("Duration: " + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Power != 0) list.Add("Power cost: " + Spell.Power.ToString("0;0'%'"));
                list.Add("Casting time: " + (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'"));
                if (Spell.RecastDelay > 60000) list.Add("Recast time: " + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0) list.Add("Recast time: " + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Concentration != 0) list.Add("Concentration cost: " + Spell.Concentration);
                if (Spell.Radius != 0) list.Add("Radius: " + Spell.Radius);

                byte nextDelveDepth = (byte)(DelveInfoDepth + 1);
                if (nextDelveDepth > MAX_DELVE_RECURSION)
                {
                    list.Add("(recursion - see server logs)");
                    log.ErrorFormat("Spell delve info recursion limit reached. Source spell ID: {0}, Sub-spell ID: {1}", m_spell.ID, m_procSpell.ID);
                }
                else
                {
                    // add subspell specific informations
                    list.Add(" ");
                    list.Add("Sub-spell informations: ");
                    list.Add(" ");
                    ISpellHandler subSpellHandler = ScriptMgr.CreateSpellHandler(Caster, m_procSpell, m_procSpellLine);
                    if (subSpellHandler == null)
                    {
                        list.Add("unable to create subspell handler: '" + SubSpellLineName + "', " + m_spell.Value);
                        return list;
                    }
                    subSpellHandler.DelveInfoDepth = nextDelveDepth;

                    IList<string> subSpellDelve = subSpellHandler.DelveInfo;
                    if (subSpellDelve.Count > 0)
                    {
                        subSpellDelve.RemoveAt(0);
                        list.AddRange(subSpellDelve);
                    }
                }

                return list;
            }
        }
    }

    [SpellHandler("OffensiveProc")]
    public class OffensiveProcSpellHandler : BaseProcSpellHandler
    {
        protected override DOLEvent EventType => GameLivingEvent.AttackFinished;
        protected override string SubSpellLineName => "OffensiveProc";

        protected override void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackFinishedEventArgs args = arguments as AttackFinishedEventArgs;

            if (args is not { AttackData: { AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle, AttackType: not AttackData.eAttackType.Spell and not AttackData.eAttackType.DoT } })
                return;

            AttackData ad = args.AttackData;

            int baseChance = Spell.Frequency / 100;

            if (ad.AttackType == AttackData.eAttackType.MeleeDualWield)
                baseChance /= 2;

            if (baseChance < 1)
                baseChance = 1;

            if (ad.Attacker == ad.Attacker as GameNPC)
            {
                Spell baseSpell = null;

                GameNPC pet = ad.Attacker as GameNPC;
                var procSpells = new List<Spell>();
                foreach (Spell spell in pet!.Spells)
                {
                    if (pet.GetSkillDisabledDuration(spell) == 0)
                    {
                        if (spell.SpellType.ToLower() == "offensiveproc")
                            procSpells.Add(spell);
                    }
                }
                if (procSpells.Count > 0)
                {
                    baseSpell = procSpells[Util.Random((procSpells.Count - 1))];
                }
                m_procSpell = SkillBase.GetSpellByID((int)baseSpell!.Value);
            }
            if (Util.Chance(baseChance))
            {
                ISpellHandler handler = ScriptMgr.CreateSpellHandler((GameLiving)sender, m_procSpell, m_procSpellLine);
                if (handler != null)
                {
                    switch (m_procSpell.Target.ToLower())
                    {
                        case "enemy":
                            handler.StartSpell(ad.Target);
                            break;
                        default:
                            handler.StartSpell(ad.Attacker);
                            break;
                    }
                }
            }
        }

        public OffensiveProcSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            ISpellHandler subSpell = ScriptMgr.CreateSpellHandler(m_caster, m_procSpell, m_procSpellLine);
            string subDesc = subSpell != null ? subSpell.GetDelveDescription(delveClient) : $"Spell with ID {Spell.Value} not found";
            string baseDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.OffensiveProc.MainDescription", Spell.Name, (Spell.Frequency / 100));

            return baseDesc + "\n\n" + subDesc;
        }
    }

    [SpellHandler("DefensiveProc")]
    public class DefensiveProcSpellHandler : BaseProcSpellHandler
    {
        protected override DOLEvent EventType => GameLivingEvent.AttackedByEnemy;
        protected override string SubSpellLineName => "DefensiveProc";

        protected override void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackedByEnemyEventArgs args = arguments as AttackedByEnemyEventArgs;

            if (args is not { AttackData: { AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle, AttackType: not AttackData.eAttackType.Spell and not AttackData.eAttackType.DoT } })
                return;

            AttackData ad = args.AttackData;

            int baseChance = Spell.Frequency / 100;

            if (ad.AttackType == AttackData.eAttackType.MeleeDualWield)
                baseChance /= 2;

            if (baseChance < 1)
                baseChance = 1;

            if (Util.Chance(baseChance))
            {
                ISpellHandler handler = ScriptMgr.CreateSpellHandler((GameLiving)sender, m_procSpell, m_procSpellLine);
                if (handler != null)
                {
                    switch (m_procSpell.Target.ToLower())
                    {
                        case "enemy":
                            handler.StartSpell(ad.Attacker);
                            break;
                        default:
                            handler.StartSpell(ad.Target);
                            break;
                    }
                }
            }
        }

        public DefensiveProcSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            ISpellHandler subSpell = ScriptMgr.CreateSpellHandler(m_caster, m_procSpell, m_procSpellLine);
            string subDesc = subSpell != null ? subSpell.GetDelveDescription(delveClient) : $"Spell with ID {Spell.Value} not found";
            string baseDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.DefensiveProc.MainDescription", Spell.Name, (Spell.Frequency / 100));

            return baseDesc + "\n\n" + subDesc;
        }
    }

    [SpellHandler("OffensiveProcPvE")]
    public class OffensiveProcPvESpellHandler : OffensiveProcSpellHandler
    {
        protected override void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is AttackFinishedEventArgs { AttackData.Target: {} target } && target.GetController() is GameNPC)
                base.EventHandler(e, sender, arguments);
        }

        public OffensiveProcPvESpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
