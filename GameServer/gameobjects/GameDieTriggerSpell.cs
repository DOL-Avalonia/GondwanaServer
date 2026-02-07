using System;
using System.Reflection;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using log4net;

namespace DOL.GS
{
    public class DieTriggerSpell : GameInventoryItem
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public DieTriggerSpell() : base() { }
        public DieTriggerSpell(ItemTemplate template) : base(template) { }
        public DieTriggerSpell(InventoryItem item) : base(item) { }

        /// <summary>
        /// Called from GamePlayer.Die(...) after base.Die(killer).
        /// We forcibly activate the spells if the item is in the backpack.
        /// </summary>
        public void OnPlayerDie(GamePlayer deadOwner, GameObject killer)
        {
            if (deadOwner.Level < this.LevelRequirement)
            {
                if (log.IsDebugEnabled)
                    log.DebugFormat("DieTriggerSpell [Id={0}, Name={1}] not triggered; player level {2} < required {3}.", this.Id_nb, this.Name, deadOwner.Level, this.LevelRequirement);
                return;
            }

            if (SlotPosition < (int)eInventorySlot.FirstBackpack || SlotPosition > (int)eInventorySlot.LastBackpack)
                return;

            if (deadOwner.CurrentRegion.IsRvR && !CanUseInRvR)
            {
                deadOwner.Out.SendMessage(LanguageMgr.GetTranslation(deadOwner.Client, "Items.Specialitems.GuarkRingCannotUseHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            bool usedAnySpell = false;

            bool usedSpell1 = TryActivateDieTriggerSpell(deadOwner, killer, SpellID, ref m_charges, MaxCharges);
            usedAnySpell |= usedSpell1;

            bool usedSpell2 = TryActivateDieTriggerSpell(deadOwner, killer, SpellID1, ref m_charges1, MaxCharges1);
            usedAnySpell |= usedSpell2;

            if (usedAnySpell)
            {
                if (deadOwner.Inventory != null)
                {
                    if (deadOwner.Inventory.RemoveItem(this))
                    {
                        deadOwner.Out.SendMessage(LanguageMgr.GetTranslation(deadOwner.Client, "Items.Specialitems.DieTriggerSpell.Destroyed", Name), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        deadOwner.Out.SendMessage(LanguageMgr.GetTranslation(deadOwner.Client, "Items.Specialitems.DieTriggerSpell.FailDestroy"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        /// <summary>
        /// Helper to forcibly trigger the item’s stored spell (SpellID or SpellID1).
        /// - Decrements charges if used.
        /// - Ignores normal cast checks (caster is dead, etc.).
        /// 
        /// Returns true if the spell was successfully applied.
        /// </summary>
        private bool TryActivateDieTriggerSpell(GamePlayer deadOwner, GameObject killer, int spellId, ref int currentCharges, int maxCharges)
        {
            if (spellId <= 0) return false;

            if (maxCharges > 0 && currentCharges <= 0)
                return false;

            SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects) ?? SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            if (line == null) return false;

            Spell spell = SkillBase.FindSpell(spellId, line);
            if (spell == null)
            {
                log.Warn($"[DieTriggerSpell] SpellID={spellId} not found in line={line.KeyName}!");
                return false;
            }

            if (IsResurrectionSpell(spell))
            {
                if (deadOwner.IsDamned && deadOwner.IsRadioactiveAreaActive)
                {
                    if (spell.SpellType != "ReanimateCorpse")
                    {
                        deadOwner.Out.SendMessage(LanguageMgr.GetTranslation(deadOwner.Client, "Items.Specialitems.DieTriggerSpell.FailRez"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                }
            }

            ISpellHandler handler = ScriptMgr.CreateSpellHandler(deadOwner, spell, line);
            if (handler == null)
            {
                log.Warn($"[DieTriggerSpell] Could not create SpellHandler for SpellID={spellId}!");
                return false;
            }

            if (IsResurrectionSpell(spell))
            {
                if (handler is SpellHandler sh)
                {
                    sh.OnDirectEffect(deadOwner, 1.0);
                }
                else
                {
                    log.Warn($"[DieTriggerSpell] Resurrect-like spellID={spellId} but handler is not a SpellHandler!");
                    return false;
                }
            }
            else
            {
                // For direct damage, disease, CC, etc. target = killer
                GameLiving livingKiller = killer as GameLiving;
                if (livingKiller == null)
                {
                    return false;
                }

                if (handler is SpellHandler sh)
                {
                    if (spell.Duration > 0 || spell.Concentration > 0)
                    {
                        sh.OnDurationEffectApply(livingKiller, 1.0);
                    }
                    else
                    {
                        sh.OnDirectEffect(livingKiller, 1.0);
                    }
                }
                else
                {
                    log.Warn($"[DieTriggerSpell] SpellID={spellId} has non-SpellHandler instance?");
                    return false;
                }
            }

            if (maxCharges > 0 && currentCharges > 0)
            {
                currentCharges--;
                if (currentCharges < 0)
                    currentCharges = 0;
            }

            return true;
        }

        /// <summary>
        /// Quick check if the given spell is a resurrect/respec-likely type
        /// by name or type. Adjust as needed for your DB.
        /// </summary>
        private bool IsResurrectionSpell(Spell spell)
        {
            if (spell == null) return false;

            string sType = (spell.SpellType ?? "").ToLowerInvariant();

            return (sType.Contains("resurrect") ||
                    sType.Contains("rez") ||
                    sType.Contains("reanimate"));
        }
    }
}