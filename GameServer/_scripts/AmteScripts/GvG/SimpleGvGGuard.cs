using System;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;

namespace DOL.GS.Scripts
{
    public class SimpleGvGGuard : AmteMob
    {
        public GuildCaptainGuard Captain
        {
            get
            {
                var brain = Brain as SimpleGvGGuardBrain;
                if (brain == null)
                    return null;

                return brain.Captain;
            }
            set
            {
                var guard = Brain as SimpleGvGGuardBrain;
                if (guard != null)
                    guard.Captain = value;
            }
        }

        public override string GuildName
        {
            get
            {
                return base.GuildName;
            }
            set
            {
                var old = base.GuildName;
                base.GuildName = value;
                if (old != value)
                    RefreshEmblem();
            }
        }

        public SimpleGvGGuard()
        {
            var brain = new SimpleGvGGuardBrain();
            brain.AggroLink = 3;
            SetOwnBrain(brain);
        }

        public override bool Interact(GamePlayer player)
        {
            if (player.Client.Account.PrivLevel == 1 && !IsWithinRadius(player, InteractDistance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObject.Interact.TooFarAway", GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Notify(GameObjectEvent.InteractFailed, this, new InteractEventArgs(player));
                return false;
            }
            Notify(GameObjectEvent.Interact, this, new InteractEventArgs(player));
            player.Notify(GameObjectEvent.InteractWith, player, new InteractWithEventArgs(this));

            if (string.IsNullOrWhiteSpace(GuildName) || player.Guild == null)
                return false;
            if (player.Client.Account.PrivLevel == 1 && player.GuildName != GuildName)
                return false;
            if (!player.GuildRank.Claim)
            {
                string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "SimpleGvGGuard.Interact.TooLowRank", player.Name);
                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            var cloaks = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsLike("gvg_guard_%").And(DB.Column("Slot").IsEqualTo(26)));
            string cloakList = string.Join("\n", cloaks.Select(c => string.Format("[{0}]", c.TemplateID.Substring(10))));

            string interactMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "SimpleGvGGuard.Interact.ChooseEquip", player.Name, cloakList);
            player.Out.SendMessage(interactMsg, eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }
        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || string.IsNullOrWhiteSpace(GuildName))
                return false;
            var player = source as GamePlayer;
            if (player == null || player.Guild == null)
                return false;
            if (player.Client.Account.PrivLevel == 1 && player.GuildName != GuildName)
                return false;
            if (!player.GuildRank.Claim)
            {
                string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "SimpleGvGGuard.Interact.TooLowRank", player.Name);
                player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            var cloaks = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsLike("gvg_guard_%").And(DB.Column("Slot").IsEqualTo(26)));
            text = string.Format("gvg_guard_{0}", text);
            if (cloaks.Any(c => c.TemplateID == text))
                LoadEquipmentTemplateFromDatabase(text);
            RefreshEmblem();
            return true;
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);
            
            if ((killer as GameLiving)?.GetController() is GamePlayer plKiller && !string.IsNullOrEmpty(GuildName))
            {
                var guild = GuildMgr.GetGuildByName(GuildName);
                if (guild == null)
                    return;
                string captainName = (Captain == null || string.IsNullOrEmpty(Captain.Name)) ? "Capitaine" : Captain.Name;

                // Loop through online members to send personalized killer names
                foreach (GamePlayer member in guild.GetListOfOnlineMembers())
                {
                    string killerPersoName = member.GetPersonalizedName(plKiller);
                    string killerText;

                    if (!string.IsNullOrEmpty(plKiller.GuildName))
                    {
                        killerText = LanguageMgr.GetTranslation(member.Client.Account.Language, "SimpleGvGGuard.Die.GuildMember", killerPersoName, plKiller.GuildName);
                    }
                    else
                    {
                        killerText = killerPersoName;
                    }

                    string translatedCaptain = captainName == "Capitaine" ? LanguageMgr.GetTranslation(member.Client.Account.Language, "SimpleGvGGuard.Die.CaptainName") : captainName;

                    string broadcastMsg = LanguageMgr.GetTranslation(member.Client.Account.Language, "SimpleGvGGuard.Die.Broadcast", translatedCaptain, killerText);

                    member.Out.SendMessage(broadcastMsg, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                }
            }
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld())
                return false;
            if (Captain != null)
                RefreshEmblem();
            return true;
        }

        public void RefreshEmblem()
        {
            if (string.IsNullOrWhiteSpace(GuildName) || ObjectState != eObjectState.Active || CurrentRegion == null || Inventory == null || Inventory.VisibleItems == null)
                return;
            var guild = GuildMgr.GetGuildByName(GuildName);
            if (guild == null)
                return;
            foreach (var item in Inventory.VisibleItems)
                if (item.Emblem != 0 || item.Color == GuildCaptainGuard.NEUTRAL_EMBLEM)
                    item.Emblem = guild.Emblem;
            SaveIntoDatabase();
        }
    }
}

namespace DOL.AI.Brain
{
    public class SimpleGvGGuardBrain : AmteMobBrain
    {
        private long _lastCaptainUpdate = 0;
        private GuildCaptainGuard _captain;

        public GuildCaptainGuard Captain
        {
            get
            {
                if (_lastCaptainUpdate > DateTime.Now.Ticks)
                    return _captain;
                _captain = GuildCaptainGuard.allCaptains.OrderBy(c => Body.GetDistanceTo(c)).FirstOrDefault();
                var name = _captain != null ? _captain.GuildName : string.Empty;
                if (name != Body.GuildName)
                    Body.GuildName = name;
                _lastCaptainUpdate = DateTime.Now.Ticks + 60 * 1000 * 10000;
                return _captain;
            }
            set
            {
                _captain = value;
                _lastCaptainUpdate = DateTime.Now.Ticks + 60 * 1000 * 10000;
                var name = _captain == null || _captain.GuildName == null ? string.Empty : _captain.GuildName;
                if (name != Body.GuildName)
                    Body.GuildName = name;
            }
        }

        public SimpleGvGGuardBrain()
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
                var brain = npc.Brain as SimpleGvGGuardBrain;
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
                if (Captain != null)
                {
                    var plGuildId = targetPlayer.Guild != null ? targetPlayer.GuildID : "NOGUILD";
                    if (targetPlayer.GuildName == Body.GuildName || Captain.safeGuildIds.Contains(plGuildId))
                        return 0;
                    return 100;
                }
                return targetPlayer.GuildName == Body.GuildName ? 0 : 100;
            }
            if (target.Realm == 0)
                return 0;
            return base.CalculateAggroLevelToTarget(target);
        }
    }
}