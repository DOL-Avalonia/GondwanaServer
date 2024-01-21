using System;
using AmteScripts.Managers;
using DOL.gameobjects.CustomNPC;
using DOL.GS;
using DOL.GS.Scripts;

namespace DOL.AI.Brain
{
    public class GuardNPCBrain : AmteMobBrain
    {
        public GuardNPCBrain()
        {
            AggroLevel = 100;
        }

        public override bool CanBAF { get => true; set => base.CanBAF = value; }

        public override void Think()
        {
            base.Think();
            if (Body is ITextNPC && !Body.InCombat)
                ((ITextNPC)Body).SayRandomPhrase();
        }

        protected override void CheckPlayerAggro()
        {
            if (Body.AttackState)
                return;
            foreach (GamePlayer pl in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (!pl.IsAlive || pl.ObjectState != GameObject.eObjectState.Active || !GameServer.ServerRules.IsAllowedToAttack(Body, pl, true))
                    continue;

                if (pl.IsStealthed)
                    pl.Stealth(false);

                int aggro = CalculateAggroLevelToTarget(pl);
                if (aggro <= 0)
                    continue;
                AddToAggroList(pl, aggro);
                if (pl.Wanted || pl.Level > Body.Level - 20 || (pl.Group != null && pl.Group.MemberCount >= 2))
                    BringFriends(pl);
            }
        }

        protected override void CheckNPCAggro()
        {
            if (Body.AttackState)
                return;
            foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)AggroRange, Body.CurrentRegion.IsDungeon ? false : true))
            {
                if (npc is ShadowNPC)
                    continue;
                if (npc.Realm != 0 || npc.IsPeaceful ||
                    !npc.IsAlive || npc.ObjectState != GameObject.eObjectState.Active ||
                    npc is GameTaxi ||
                    m_aggroTable.ContainsKey(npc) ||
                    !GameServer.ServerRules.IsAllowedToAttack(Body, npc, true))
                    continue;

                int aggro = CalculateAggroLevelToTarget(npc);
                if (aggro <= 0)
                    continue;
                AddToAggroList(npc, aggro);
                if (npc.Level > Body.Level)
                    BringFriends(npc);
            }
        }

        private void BringReinforcements(GameNPC target)
        {
            int count = (int)Math.Log(target.Level - Body.Level, 2) + 1;
            foreach (GameNPC npc in Body.GetNPCsInRadius(WorldMgr.YELL_DISTANCE))
            {
                if (count <= 0)
                    return;
                if (npc.Brain is GuardNPCBrain == false)
                    continue;
                var brain = npc.Brain as GuardNPCBrain;
                brain.AddToAggroList(target, 1);
                brain.AttackMostWanted();
            }
        }

        public override int CalculateAggroLevelToTarget(GameLiving target)
        {
            if (target is GamePlayer player)
            {
                if (player.Wanted)
                {
                    return 100;
                }
                return GuardsMgr.CalculateAggro(player);
            }
            if (target.Realm == 0)
                return target.Level + 1;
            return base.CalculateAggroLevelToTarget(target);
        }
    }
}
