using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DOL.Language;
using DOL.GS;
using DOL.GS.ServerProperties;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    [CmdAttribute(
         "&broadcast",
         new string[] { "&b" },
         ePrivLevel.Player,
         "Commands.Players.Broadcast.Description",
         "Commands.Players.Broadcast.Usage")]
    public class BroadcastCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private enum eBroadcastType : int
        {
            Area = 1,
            Visible = 2,
            Zone = 3,
            Region = 4,
            Realm = 5,
            Server = 6,
        }

        public void OnCommand(GameClient client, string[] args)
        {
            const string BROAD_TICK = "Broad_Tick";
            if (args.Length < 2)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Broadcast.NoText"));
                return;
            }
            if (client.Player.IsMuted)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Broadcast.Muted"), eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
                return;
            }
            string message = string.Join(" ", args, 1, args.Length - 1);

            long BroadTick = client.Player.TempProperties.getProperty<long>(BROAD_TICK);
            if (BroadTick > 0 && BroadTick - client.Player.CurrentRegion.Time <= 0)
            {
                client.Player.TempProperties.removeProperty(BROAD_TICK);
            }
            long changeTime = client.Player.CurrentRegion.Time - BroadTick;
            if (changeTime < 800 && BroadTick > 0)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Broadcast.SlowDown"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Player.TempProperties.setProperty(BROAD_TICK, client.Player.CurrentRegion.Time);
                return;
            }
            Broadcast(client.Player, message);

            client.Player.TempProperties.setProperty(BROAD_TICK, client.Player.CurrentRegion.Time);
        }

        private void Broadcast(GamePlayer player, string message)
        {
            var targets = GetTargets(player);

            if ((eBroadcastType)Properties.BROADCAST_TYPE == eBroadcastType.Server)
            {
                DiscordBot.Instance?.SendMessageBroadcast(player, message);
            }

            if (targets.Count == 0)
                return;

            var keyTranslator = new KeyTranslator("Commands.Players.Broadcast.Message");
            IEnumerable<Task<KeyValuePair<GamePlayer, string>>> tasks;
            if ((eBroadcastType)Properties.BROADCAST_TYPE == eBroadcastType.Server)
            {
                tasks = keyTranslator.TranslatePlayerInput(targets, player.Client.Account.Language, message, (p, msg) => [p.Name, msg]);
            }
            else
            {
                tasks = keyTranslator.TranslatePlayerInput(targets, player.Client.Account.Language, message, (p, msg) => [p.GetPersonalizedName(player), msg]);
            }

            foreach (var t in tasks)
            {
                GameLoopAsync.Then(t, static kv => { if (kv.Key.ObjectState == GameObject.eObjectState.Active)
                        kv.Key.Out.SendMessage(kv.Value, eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow); });
            }
        }

        private List<GamePlayer> GetTargets(GamePlayer player)
        {
            List<GamePlayer> list = new();
            eBroadcastType type = (eBroadcastType)Properties.BROADCAST_TYPE;
            switch (type)
            {
                case eBroadcastType.Area:
                    {
                        bool found = false;
                        foreach (AbstractArea area in player.CurrentAreas)
                        {
                            if (area.CanBroadcast)
                            {
                                found = true;
                                foreach (GameClient thisClient in WorldMgr.GetClientsOfRegion(player.CurrentRegionID))
                                {
                                    if (thisClient.Player.CurrentAreas.Contains(area))
                                    {
                                        list.Add(thisClient.Player);
                                    }
                                }
                            }
                        }
                        if (!found)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Broadcast.NoHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }
                case eBroadcastType.Realm:
                    {
                        foreach (GameClient thisClient in WorldMgr.GetClientsOfRealm(player.Realm))
                        {
                            list.Add(thisClient.Player);
                        }
                        break;
                    }
                case eBroadcastType.Region:
                    {
                        foreach (GameClient thisClient in WorldMgr.GetClientsOfRegion(player.CurrentRegionID))
                        {
                            list.Add(thisClient.Player);
                        }
                        break;
                    }
                case eBroadcastType.Server:
                    {
                        foreach (GameClient thisClient in WorldMgr.GetAllPlayingClients())
                        {
                            list.Add(thisClient.Player);
                        }
                        break;
                    }
                case eBroadcastType.Visible:
                    {
                        foreach (GamePlayer p in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            list.Add(p);
                        }
                        break;
                    }
                case eBroadcastType.Zone:
                    {
                        foreach (GameClient thisClient in WorldMgr.GetClientsOfRegion(player.CurrentRegionID))
                        {
                            if (thisClient.Player.CurrentZone == player.CurrentZone)
                            {
                                list.Add(thisClient.Player);
                            }
                        }
                        break;
                    }
            }

            return list;
        }
    }
}
