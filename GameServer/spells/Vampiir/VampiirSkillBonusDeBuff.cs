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
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("VampiirSkillBonusDeBuff")]
    public class VampiirSkillBonusDeBuff : SpellHandler
    {
        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        /// <inheritdoc />
        public override bool HasPositiveEffect => false;


        public override void OnEffectStart(GameSpellEffect effect)
        {

            base.OnEffectStart(effect);
            if (effect.Owner is GamePlayer)
            {
                GamePlayer player = effect.Owner as GamePlayer;

                for (int i = (int)eProperty.Skill_First; i <= (int)eProperty.Skill_Last; i++)
                {
                    if (player!.GetModifiedSpecLevel(SkillBase.GetPropertyName(player.Client, (eProperty)(i))) != 0)
                    {
                        player.BaseBuffBonusCategory[i] = -player.GetModifiedSpecLevel(SkillBase.GetPropertyName(player.Client, (eProperty)(i)));
                    }
                    //					DOLConsole.WriteWarning("Spec " + SkillBase.GetPropertyName((eProperty)(i)) + " " + player.GetModifiedSpecLevel(SkillBase.GetPropertyName((eProperty)(i))));
                }
                player!.PropertiesChanged();
                player.Out.SendCharStatsUpdate();
                player.UpdatePlayerStatus();
                MessageToLiving(effect.Owner, Spell.Message1, eChatType.CT_Spell);
                foreach (GamePlayer player1 in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player1))
                    {
                        player1.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message2,
                            player1.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    }
                }

            }

        }

        /// <inheritdoc />
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (effect.Owner is GamePlayer)
            {
                GamePlayer player = effect.Owner as GamePlayer;
                for (int i = (int)eProperty.Skill_First; i <= (int)eProperty.Skill_Last; i++)
                {
                    player!.BaseBuffBonusCategory[i] = 0;
                }
                player!.PropertiesChanged();
                player.Out.SendCharStatsUpdate();
                player.UpdatePlayerStatus();
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_Spell);
                foreach (GamePlayer player1 in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player1))
                    {
                        player1.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4,
                            player1.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
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
                //Name
                list.Add("Name: " + Spell.Name);
                //Description
                list.Add("Description: " + Spell.Description);
                //Target
                list.Add("Target: " + Spell.Target);
                //Cast
                list.Add("Casting time: " + (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'"));
                //Duration
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add("Duration: Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format("Duration: {0}:{1} min", Spell.Duration / 60000, (Spell.Duration % 60000 / 1000).ToString("00")));
                else if (Spell.Duration != 0)
                    list.Add("Duration: " + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                //Recast
                if (Spell.RecastDelay > 60000)
                    list.Add("Recast time: " + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add("Recast time: " + (Spell.RecastDelay / 1000).ToString() + " sec");
                //Range
                if (Spell.Range != 0) list.Add("Range: " + Spell.Range);
                //Radius
                if (Spell.Radius != 0) list.Add("Radius: " + Spell.Radius);
                //Cost
                if (Spell.Power != 0) list.Add("Power cost: " + Spell.Power.ToString("0;0'%'"));
                //Effect

                if (Spell.Frequency != 0)
                    list.Add("Frequency: " + (Spell.Frequency * 0.001).ToString("0.0"));

                return list;
            }
        }

        public VampiirSkillBonusDeBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.VampiirSkillBonusDeBuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}