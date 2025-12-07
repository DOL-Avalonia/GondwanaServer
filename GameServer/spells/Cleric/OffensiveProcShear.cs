using System;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Shears any active OffensiveProc effects from the target and, if configured,
    /// immediately applies a replacement OffensiveProc buff whose sub-spell heals the enemy.
    /// </summary>
    [SpellHandler("OffProcShear")]
    public class OffProcShear : SpellHandler
    {
        public OffProcShear(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (!base.OnDirectEffect(target, effectiveness))
                return false;

            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return false;

            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            if (target is GameNPC npc && npc.Brain is IOldAggressiveBrain aggro)
                aggro.AddToAggroList(Caster, 1);

            bool removedAny = false;
            var toCancel = new List<GameSpellEffect>();

            foreach (GameSpellEffect eff in target.EffectList.GetAllOfType<GameSpellEffect>())
            {
                if (eff?.Spell == null) continue;

                bool isOffProcType =
                    eff.SpellHandler is OffensiveProcSpellHandler ||
                    eff.SpellHandler is OffensiveProcPvESpellHandler ||
                    string.Equals(eff.Spell.SpellType, "OffensiveProc", StringComparison.OrdinalIgnoreCase);

                if (isOffProcType)
                    toCancel.Add(eff);
            }

            if (toCancel.Count > 0)
            {
                foreach (var eff in toCancel)
                {
                    SendEffectAnimation(target, 0, false, 1);
                    eff.Cancel(false);
                    removedAny = true;
                }

                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Cleric.OffProcShear.Removed"), eChatType.CT_Spell);
                MessageToLiving(target, LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.Cleric.OffProcShear.YourProcRipped"), eChatType.CT_Spell);
            }

            // Apply replacement cursed proc if we have one configured in SubSpellID
            bool appliedReplacement = false;
            if (removedAny && Spell.SubSpellID > 0)
            {
                Spell replacementBuff = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                SpellLine offProcLine = SkillBase.GetSpellLine("OffensiveProc");

                if (replacementBuff != null && offProcLine != null)
                {
                    ISpellHandler h = ScriptMgr.CreateSpellHandler(Caster, replacementBuff, offProcLine);
                    if (h != null)
                    {
                        h.StartSpell(target);
                        appliedReplacement = true;

                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Cleric.OffProcShear.AppliedCursedProc"), eChatType.CT_Spell);
                        MessageToLiving(target, LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.Cleric.OffProcShear.YouAreCursed"), eChatType.CT_Important);
                    }
                }
            }

            if (!removedAny)
            {
                SendEffectAnimation(target, 0, false, 0);
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Cleric.OffProcShear.NoEnhancementFound"), eChatType.CT_SpellResisted);
                return false;
            }

            return true;
        }

        public override void OnSpellResisted(GameLiving target)
        {
            base.OnSpellResisted(target);
            if (Spell.Damage == 0 && Spell.CastTime == 0)
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;

            string main1 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.OffProcShear.Main1");
            string main2 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.OffProcShear.Main2");

            var subIds = new List<int>();
            if (Spell.SubSpellID > 0) subIds.Add(Spell.SubSpellID);
            if (Spell.MultipleSubSpells != null) subIds.AddRange(Spell.MultipleSubSpells);

            string subBlock = string.Empty;
            if (subIds.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                string header = LanguageMgr.GetTranslation(delveClient, "SpellDescription.OffProcShear.ReplacementHeader");

                foreach (int sid in subIds)
                {
                    Spell sub = SkillBase.GetSpellByID(sid);
                    if (sub == null) continue;

                    ISpellHandler subHandler = ScriptMgr.CreateSpellHandler(m_caster, sub, SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells));

                    if (subHandler == null) continue;

                    string subDesc = subHandler.GetDelveDescription(delveClient);
                    if (string.IsNullOrWhiteSpace(subDesc)) continue;

                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(subDesc);
                }

                if (sb.Length > 0)
                    subBlock = header + "\n" + sb.ToString();
            }

            string body = main1 + "\n" + main2;
            if (!string.IsNullOrEmpty(subBlock))
                body += "\n\n" + subBlock;

            if (Spell.RecastDelay > 0)
            {
                string cd = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                body += "\n\n" + cd;
            }

            return body;
        }
    }
}