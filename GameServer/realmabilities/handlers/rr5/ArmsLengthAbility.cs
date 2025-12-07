using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Arms Length Realm Ability
    /// </summary>
    public class ArmsLengthAbility : RR5RealmAbility
    {
        public ArmsLengthAbility(DBAbility dba, int level) : base(dba, level) { }

        /// <summary>
        /// Action
        /// </summary>
        /// <param name="living"></param>
        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED)) return;
            GamePlayer player = living as GamePlayer;

            if (player != null)
            {
                if (player.TempProperties.getProperty("Charging", false)
                    || player.EffectList.CountOfType(typeof(SpeedOfSoundEffect), typeof(ArmsLengthEffect), typeof(ChargeEffect)) > 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "DashingDefenseAbility.Execute.AlreadyEffect"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    return;
                }

                GameSpellEffect speed = Spells.SpellHandler.FindEffectOnTarget(player, "SpeedEnhancement");
                if (speed != null)
                    speed.Cancel(false);
                new ArmsLengthEffect().Start(player);
                SendCasterSpellEffectAndCastMessage(player, 7068, true);
            }
            DisableSkill(living);
        }

        public override int GetReUseDelay(int level)
        {
            return 600;
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ArmsLengthAbility.AddEffectsInfo.Info1"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ArmsLengthAbility.AddEffectsInfo.Info2"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ArmsLengthAbility.AddEffectsInfo.Info3"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "ArmsLengthAbility.AddEffectsInfo.Info4"));
        }

    }
}
