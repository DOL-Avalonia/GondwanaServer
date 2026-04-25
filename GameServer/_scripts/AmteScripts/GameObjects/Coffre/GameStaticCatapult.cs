using DOL.GS;

namespace DOL.GS.Scripts
{
    public class GameStaticCatapult : GameCoffre
    {
        protected GameNPCCatapult m_npcCatapult;

        public GameNPCCatapult NPCCatapult => m_npcCatapult;

        public override bool AddToWorld()
        {
            bool success = base.AddToWorld();

            if (success)
            {
                if (m_npcCatapult == null)
                {
                    m_npcCatapult = new GameNPCCatapult(this)
                    {
                        Position = this.Position,
                        Heading = this.Heading,
                        CurrentRegion = this.CurrentRegion,
                        Realm = this.Realm
                    };
                    m_npcCatapult.AddToWorld();
                }
                else if (m_npcCatapult.ObjectState != eObjectState.Active)
                {
                    m_npcCatapult.Position = this.Position;
                    m_npcCatapult.Heading = this.Heading;
                    m_npcCatapult.AddToWorld();
                }
            }
            return success;
        }

        public override bool RemoveFromWorld()
        {
            if (m_npcCatapult != null && m_npcCatapult.ObjectState == eObjectState.Active)
            {
                m_npcCatapult.RemoveFromWorld();
            }
            return base.RemoveFromWorld();
        }

        public override void Delete()
        {
            base.Delete();
            if (m_npcCatapult != null && m_npcCatapult.ObjectState != eObjectState.Deleted)
            {
                m_npcCatapult.Delete();
                m_npcCatapult = null;
            }
        }
    }
}