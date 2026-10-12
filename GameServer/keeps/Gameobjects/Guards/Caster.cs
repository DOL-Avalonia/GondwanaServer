using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;


namespace DOL.GS.Keeps
{
    public class GuardCaster : GameKeepGuard
    {
        public const int INTERVAL = 360 * 1000;
        private ECSGameTimer m_effectTimer;

        protected virtual int Timer(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != eObjectState.Active)
                return 0;

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.Out.SendSpellCastAnimation(this, 4321, 30);

            new ECSGameTimer(this, ShowEffect, 3000);
            return INTERVAL;
        }

        public int ShowEffect(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != eObjectState.Active)
                return 0;

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.Out.SendSpellEffectAnimation(this, this, 4321, 0, false, 1);

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardCaster.SkinsHardens", Name), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);

            return 0;
        }

        public override bool AddToWorld()
        {
            bool success = base.AddToWorld();
            if (success)
            {
                m_effectTimer?.Stop();
                m_effectTimer = new ECSGameTimer(this, Timer, INTERVAL);
            }
            return success;
        }

        public override bool RemoveFromWorld()
        {
            m_effectTimer?.Stop();
            m_effectTimer = null;
            return base.RemoveFromWorld();
        }

        protected override CharacterClass GetClass()
        {
            if (ModelRealm == eRealm.Albion) return CharacterClass.Wizard;
            else if (ModelRealm == eRealm.Midgard) return CharacterClass.Runemaster;
            else if (ModelRealm == eRealm.Hibernia) return CharacterClass.Eldritch;
            return CharacterClass.None;
        }

        protected override KeepGuardBrain GetBrain() => new CasterBrain();

        protected override void SetName()
        {
            switch (ModelRealm)
            {
                case eRealm.None:
                case eRealm.Albion:
                    if (IsPortalKeepGuard)
                        Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.MasterWizard");
                    else Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Wizard");
                    break;
                case eRealm.Midgard:
                    if (IsPortalKeepGuard)
                        Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.MasterRunes");
                    else Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Runemaster");
                    break;
                case eRealm.Hibernia:
                    if (IsPortalKeepGuard)
                        Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.MasterEldritch");
                    else Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Eldritch");
                    break;
            }

            if (Realm == eRealm.None)
            {
                Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Renegade", Name);
            }
        }
    }
}
