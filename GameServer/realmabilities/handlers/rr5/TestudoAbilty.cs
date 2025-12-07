using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Effects;
using DOL.Language;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Testudo Realm Ability
    /// </summary>
    public class TestudoAbility : RR5RealmAbility
    {
        public TestudoAbility(DBAbility dba, int level) : base(dba, level) { }

        /// <summary>
        /// Action
        /// </summary>
        /// <param name="living"></param>
        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED)) return;


            InventoryItem shield = living.Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            if (shield == null)
                return;
            if (shield.Object_Type != (int)eObjectType.Shield)
                return;
            if (living.TargetObject == null)
                return;
            if (living.ActiveWeaponSlot == GameLiving.eActiveWeaponSlot.Distance)
                return;
            if (living.AttackWeapon.Hand == 1)
                return;

            GamePlayer player = living as GamePlayer;
            if (player != null)
            {
                SendCasterSpellEffectAndCastMessage(player, 7068, true);
                TestudoEffect effect = new TestudoEffect();
                effect.Start(player);
            }
            DisableSkill(living);
        }

        public override int GetReUseDelay(int level)
        {
            return 900;
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info1"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info2"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info3"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info4"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info5"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "TestudoAbility.AddEffectsInfo.Info6"));
        }

    }
}