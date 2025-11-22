using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Numerics;

namespace DOL.GS.Spells
{
    [SpellHandler("Ascendance")]
    public class AscendanceSpellHandler : SpellHandler
    {
        private int m_bonus;
        
        public override bool HasPositiveEffect => true;

        public AscendanceSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            
            m_bonus = (int)Spell.Value;
            effect.Owner.SpecBuffBonusCategory[eProperty.LivingEffectiveness] += m_bonus;


            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();

                MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.Cleric.Ascendance.OnStart"), eChatType.CT_Spell);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            if (m_bonus != 0)
            {
                effect.Owner.SpecBuffBonusCategory[eProperty.LivingEffectiveness] -= m_bonus;
            }

            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();

                if (!noMessages)
                {
                    MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.Cleric.Ascendance.OnEnd"), eChatType.CT_Spell);
                }
            }

            return base.OnEffectExpires(effect, noMessages);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string lang = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string description = LanguageMgr.GetTranslation(lang, "SpellDescription.Ascendance.MainDescription", (int)Spell.Value);
            if (Spell.AmnesiaChance == 0)
            {
                string rest = LanguageMgr.GetTranslation(lang, "SpellDescription.Ascendance.Restrictions");
                string interact = LanguageMgr.GetTranslation(lang, "SpellDescription.Ascendance.Interaction");
                description += "\n\n" + rest + "\n\n" + interact;
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(lang, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + secondDesc;
            }

            return description;
        }
    }
}