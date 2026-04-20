using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Territories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Beware, Changing this class Name or Namespace breaks TerritoryManager
    /// </summary>
    public class TerritoryGuard : AmteMob, IGuardNPC
    {
        public TerritoryGuard()
        {
            var brain = new TerritoryBrain();
            brain.AggroLink = 3;
            brain.AggroRange = 500;
            SetOwnBrain(brain);
        }

        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            var brain = this.Brain as TerritoryBrain;
            Mob mob = obj as Mob;

            if (brain != null && mob != null)
            {
                if (mob.AggroRange > 0)
                    brain.AggroRange = mob.AggroRange;
            }
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);

            if ((killer as GameLiving)?.GetController() is GamePlayer plKiller && !string.IsNullOrEmpty(GuildName))
            {
                var guild = GuildMgr.GetGuildByName(GuildName);
                if (guild == null)
                    return;

                foreach (GamePlayer member in guild.GetListOfOnlineMembers())
                {
                    string killerPersoName = member.GetPersonalizedName(plKiller);
                    string killerText;

                    if (!string.IsNullOrEmpty(plKiller.GuildName))
                    {
                        killerText = LanguageMgr.GetTranslation(member.Client.Account.Language, "TerritoryGuard.Die.GuildMember", killerPersoName, plKiller.GuildName);
                    }
                    else
                    {
                        killerText = killerPersoName;
                    }

                    string broadcastMsg = LanguageMgr.GetTranslation(member.Client.Account.Language, "TerritoryGuard.Die.Broadcast", killerText);
                    member.Out.SendMessage(broadcastMsg, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                }

                if (guild.alliance != null)
                {
                    foreach (Guild guildAlly in guild.alliance.Guilds)
                    {
                        // Skip the main guild to avoid double messaging them
                        if (guildAlly == guild) continue;

                        foreach (GamePlayer member in guildAlly.GetListOfOnlineMembers())
                        {
                            string killerPersoName = member.GetPersonalizedName(plKiller);
                            string killerText;

                            if (!string.IsNullOrEmpty(plKiller.GuildName))
                            {
                                killerText = LanguageMgr.GetTranslation(member.Client.Account.Language, "TerritoryGuard.Die.GuildMember", killerPersoName, plKiller.GuildName);
                            }
                            else
                            {
                                killerText = killerPersoName;
                            }

                            string broadcastMsg = LanguageMgr.GetTranslation(member.Client.Account.Language, "TerritoryGuard.Die.Broadcast", killerText);
                            member.Out.SendMessage(broadcastMsg, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld())
                return false;
            //if (Captain != null)
            //    RefreshEmblem();
            return true;
        }
    }
}

namespace DOL.AI.Brain
{
    public class TerritoryBrain : AmteMobBrain
    {
        public TerritoryBrain() : base()
        {
            AggroLevel = 100;
        }

        protected override void CheckPlayerAggro()
        {
            if (Body.AttackState)
                return;
            foreach (GamePlayer pl in Body.GetPlayersInRadius((ushort)AggroRange))
            {
                if (!pl.IsAlive || pl.ObjectState != GameObject.eObjectState.Active || !GameServer.ServerRules.IsAllowedToAttack(Body, pl, true))
                    continue;

                int aggro = CalculateAggroLevelToTarget(pl);
                if (aggro <= 0)
                    continue;
                AddToAggroList(pl, aggro);
                if (pl.Level > Body.Level - 20 || (pl.Group != null && pl.Group.MemberCount > 2))
                    // Use new BAF system
                    BringFriends(pl);
            }
        }

        protected override void CheckNPCAggro()
        {
            if (Body.AttackState)
                return;
            foreach (GameNPC npc in Body.GetNPCsInRadius((ushort)AggroRange, Body.CurrentRegion.IsDungeon ? false : true))
            {
                bool isTaxi = npc as GameTaxi != null;
                if (npc.Realm != 0 || npc.IsPeaceful ||
                    !npc.IsAlive || npc.ObjectState != GameObject.eObjectState.Active ||
                    isTaxi ||
                    m_aggroTable.ContainsKey(npc) ||
                    !GameServer.ServerRules.IsAllowedToAttack(Body, npc, true))
                    continue;

                int aggro = CalculateAggroLevelToTarget(npc);
                if (aggro <= 0)
                    continue;
                AddToAggroList(npc, aggro);
                if (npc.Level > Body.Level)
                    // Use new BAF system
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
                var brain = npc.Brain as TerritoryBrain;
                if (brain == null)
                    continue;
                brain.AddToAggroList(target, 1);
                brain.AttackMostWanted();
            }
        }

        public override int CalculateAggroLevelToTarget(GameLiving target)
        {
            if (target is GamePlayer targetPlayer)
            {
                //if (Captain != null)
                //{
                //    var plGuildId = player.Guild != null ? player.GuildID : "NOGUILD";
                //    if (target.GuildName == Body.GuildName || Captain.safeGuildIds.Contains(plGuildId))
                //        return 0;
                //    return 100;
                //}
                return targetPlayer.GuildName == Body.GuildName ? 0 : 100;
            }
            if (target.Realm == 0)
                return 0;
            return base.CalculateAggroLevelToTarget(target);
        }
    }
}

