using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Mystic Crystal Lore, power heal
    /// </summary>
    public class MysticCrystalLoreAbility : TimedRealmAbility
    {
        public MysticCrystalLoreAbility(DBAbility dba, int level) : base(dba, level) { }

        /// <summary>
        /// Action
        /// </summary>
        /// <param name="living"></param>
        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED | INCOMBAT)) return;

            int heal = 0;
            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                switch (Level)
                {
                    case 1: heal = 25; break;
                    case 2: heal = 40; break;
                    case 3: heal = 60; break;
                    case 4: heal = 80; break;
                    case 5: heal = 100; break;
                }
            }
            else
            {
                switch (Level)
                {
                    case 1: heal = 25; break;
                    case 2: heal = 60; break;
                    case 3: heal = 100; break;
                }
            }
            int healed = living.ChangeMana(living, GameLiving.eManaChangeType.Spell, living.MaxMana * heal / 100);

            SendCasterSpellEffectAndCastMessage(living, 7009, healed > 0);

            GamePlayer player = living as GamePlayer;
            if (player != null)
            {
                if (healed > 0) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "MysticCrystalLoreAbility.YouGainMana", healed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                if (heal > healed)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "MysticCrystalLoreAbility.YouFullMana"), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
            if (healed > 0) DisableSkill(living);
        }

        public override int GetReUseDelay(int level)
        {
            return 180;
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.NoScaleInfo1"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.NoScaleInfo2"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.NoScaleInfo3"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.NoScaleInfo4"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.NoScaleInfo5"));
                list.Add("");
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info4"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info5"));
                list.Add("");
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info6"));
            }
            else
            {
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info1"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info2"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info3"));
                list.Add("");
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info4"));
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info5"));
                list.Add("");
                list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "MysticCrystalLoreAbility.AddEffectsInfo.Info6"));
            }
        }
    }
}