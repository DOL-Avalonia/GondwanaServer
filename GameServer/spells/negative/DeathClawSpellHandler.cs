using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("DeathClaw")]
    public class DeathClawSpellHandler : SpellHandler
    {
        public DeathClawSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = new AttackData();
            ad.Attacker = Caster;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
            ad.IsSpellResisted = false;

            if (target is GameNPC npc)
            {
                if (npc.Flags.HasFlag(GameNPC.eFlags.GHOST))
                {
                    // No effect on ghosts
                    ad.Damage = 0;
                }
                else if (npc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead)
                {
                    if (npc.IsBoss)
                    {
                        // Reduce undead health by 20%
                        ad.Damage = (int)(npc.MaxHealth * 0.2 - (npc.MaxHealth - npc.Health));
                    }
                    else
                    {
                        // Reduce undead health by 50%
                        ad.Damage = (int)(npc.MaxHealth * 0.5 - (npc.MaxHealth - npc.Health));
                    }
                }
                else if (npc.IsBoss)
                {
                    if (npc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead)
                    {
                        // Reduce undead health by 20%
                        ad.Damage = (int)(npc.MaxHealth * 0.2 - (npc.MaxHealth - npc.Health));
                    }
                    else
                    {
                        // Reduce boss health by 30%
                        ad.Damage = (int)(npc.MaxHealth * 0.3 - (npc.MaxHealth - npc.Health));
                    }
                }
                else
                {
                    // Reduce health by 90% for other NPCs
                    ad.Damage = (int)(npc.MaxHealth * 0.9 - (npc.MaxHealth - npc.Health));
                }
            }
            else
            {
                // Reduce health by 90% for players
                ad.Damage = (int)(target.MaxHealth * 0.9 - (target.MaxHealth - target.Health));
            }

            m_lastAttackData = ad;
            SendDamageMessages(ad);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
            DamageTarget(ad, true);
            return true;
        }

        public static bool IsResistingEntity(GameLiving target)
        {
            bool isShapeShifted = FindEffectOnTarget(target, e => e is GameSpellEffect { SpellHandler: SpiritShapeShift or ChtonicShapeShift or BringerOfDeath or CallOfShadowsSpellHandler }) != null;
            bool isGhostOrUndead = (target is GameNPC npc && (npc.Flags.HasFlag(GameNPC.eFlags.GHOST) || npc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead))
                || FindEffectOnTarget<DamnationSpellHandler>(target) != null
                || FindEffectOnTarget<SummonMonster>(target) != null;
            bool isSpecialClass = target is GamePlayer player
                && ((player.CharacterClass.ID == (int)eCharacterClass.Necromancer && player.IsShade && FindEffectOnTarget(target, e => e is NecromancerShadeEffect) != null)
                    || (player.CharacterClass.ID == (int)eCharacterClass.Bainshee && player.Model is 1883 or 1884 or 1885)
                    || (player.CharacterClass.ID == (int)eCharacterClass.Vampiir));
            bool isBoss = target is GameNPC { IsBoss: true };

            return isShapeShifted || isGhostOrUndead || isSpecialClass || isBoss;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (Spell.AmnesiaChance > 0 && target.Level > Spell.AmnesiaChance)
                return 100;

            var ResistChanceFactor = 2.6;
            if (IsResistingEntity(target))
                return base.CalculateSpellResistChance(target) * (int)ResistChanceFactor;

            return base.CalculateSpellResistChance(target);
        }
        

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.DeathClaw.MainDescription", Spell.Name, Spell.AmnesiaChance);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}