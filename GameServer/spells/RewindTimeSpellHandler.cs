using System;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("RewindTime")]
    public class RewindTimeSpellHandler : SpellHandler
    {
        public RewindTimeSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        private static string T(GamePlayer p, string key, params object[] args)
        {
            if (p == null) return "";
            return LanguageMgr.GetTranslation(p.Client, key, args);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;

            // Calculate how far back to go (Spell Value is usually stored as double)
            int rewindMillis = (int)(Spell.Value * 1000);

            if (rewindMillis <= 0) rewindMillis = 5000; // Default to 5s if not set

            Position? pastPos = target.GetPositionFromPast(rewindMillis);

            if (pastPos.HasValue)
            {
                Position dest = pastPos.Value;

                foreach (GamePlayer p in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    p.Out.SendSpellEffectAnimation(Caster, target, Spell.ClientEffect, 0, false, 1);
                }

                target.MoveTo(dest);

                if (target is GamePlayer player)
                {
                    string msg = T(player, "SpellHandler.RewindTime.Success", Spell.Value);
                    player.Out.SendMessage(msg, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }

                foreach (GamePlayer p in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    p.Out.SendSpellEffectAnimation(Caster, target, Spell.ClientEffect, 0, false, 1);
                }

                return true;
            }
            else
            {
                if (Caster is GamePlayer casterPlayer)
                {
                    string msg = T(casterPlayer, "SpellHandler.RewindTime.NoHistory");
                    MessageToCaster(msg, eChatType.CT_System);
                }
                return false;
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.RewindTime.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}