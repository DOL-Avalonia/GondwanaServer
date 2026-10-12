using DOL.Database;
using DOL.GS.Keeps;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DoorRequest, "Door Interact Request Handler", eClientStatus.PlayerInGame)]
    public class DoorRequestHandler : PacketHandler
    {
        private const string ADD_DOOR_ID_PROPERTY = "DoorRequestHandler.AddDoorId";

        /// <summary>Kept for other scripts that may read it. The add-door dialog uses the player TempProperty above.</summary>
        public static int m_handlerDoorID;

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player?.CurrentRegion == null)
                return;

            int doorID = (int)packet.ReadInt();
            byte doorState = (byte)packet.ReadByte();
            m_handlerDoorID = doorID;

            int doorType = doorID / 100000000;
            int radius = Properties.WORLD_PICKUP_DISTANCE * 2;
            int zoneDoor = doorID / 1000000;
            string debugText = string.Empty;

            if (player.CurrentRegion.Expansion == (int)eClientExpansion.TrialsOfAtlantis)
            {
                debugText = $"ToA DoorID:{doorID} ";
                doorID -= zoneDoor * 1000000;
                zoneDoor = player.CurrentZone.ID;
                doorID += zoneDoor * 1000000;
                m_handlerDoorID = doorID;

                if (player.CurrentRegion.IsDungeon)
                    radius *= 4;
            }

            if (client.Account.PrivLevel > 1 || Properties.ENABLE_DEBUG)
            {
                if (doorType == 7)
                {
                    int ownerKeepId = (doorID / 100000) % 1000;
                    int towerNum = (doorID / 10000) % 10;
                    int keepID = ownerKeepId + towerNum * 256;
                    int componentID = (doorID / 100) % 100;
                    int doorIndex = doorID % 10;
                    client.Out.SendDebugMessage($"Keep Door ID:{doorID} state:{doorState} (Owner Keep:{ownerKeepId} KeepID:{keepID} ComponentID:{componentID} DoorIndex:{doorIndex} TowerNumber:{towerNum})");

                    if (keepID > 255 && ownerKeepId < 10)
                        ChatUtil.SendDebugMessage(client, "Warning: Towers with an Owner Keep ID < 10 will have untargetable doors!");
                }
                else if (doorType == 9)
                {
                    int doorIndex = doorID - doorType * 10000000;
                    client.Out.SendDebugMessage($"House DoorID:{doorID} state:{doorState} (doorType:{doorType} doorIndex:{doorIndex})");
                }
                else
                {
                    int fixture = doorID - zoneDoor * 1000000;
                    int fixturePiece = fixture;
                    fixture /= 100;
                    fixturePiece -= fixture * 100;
                    client.Out.SendDebugMessage($"{debugText}DoorID:{doorID} state:{doorState} zone:{zoneDoor} fixture:{fixture} fixturePiece:{fixturePiece} Type:{doorType}");
                }
            }

            IDoor door = DoorMgr.GetDoorByID(doorID);

            if (door == null && doorType != 9 && client.Account.PrivLevel > 1 && !player.CurrentRegion.IsInstance)
            {
                // DoorMgr holds every door loaded from the database at startup, so no DB query is needed here (this runs on the game loop).
                if (player.TempProperties.getProperty(DoorMgr.WANT_TO_ADD_DOORS, false))
                {
                    player.TempProperties.setProperty(ADD_DOOR_ID_PROPERTY, doorID);
                    player.Out.SendCustomDialog("This door is not in the database. Place yourself nearest to this door and click Accept to add it.", AddingDoor);
                }
                else
                {
                    player.Out.SendMessage("This door is not in the database. Use '/door show' to enable the add door dialog when targeting doors.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }

            HandleDoorChange(player, door, doorID, doorState, radius);
        }

        private static void AddingDoor(GamePlayer player, byte response)
        {
            if (response != 0x01)
                return;

            int doorId = player.TempProperties.getProperty(ADD_DOOR_ID_PROPERTY, m_handlerDoorID);
            player.TempProperties.removeProperty(ADD_DOOR_ID_PROPERTY);

            int doorType = doorId / 100000000;

            if (doorType == 7)
            {
                PositionMgr.CreateDoor(doorId, player);
                player.Out.SendMessage("Added keep door position to the database!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            DBDoor door = new DBDoor
            {
                ObjectId = null,
                InternalID = doorId,
                Name = "door",
                Type = doorType,
                Level = 20,
                Realm = 6,
                MaxHealth = 2545,
                Health = 2545,
                Locked = 0,
                X = player.Position.X,
                Y = player.Position.Y,
                Z = player.Position.Z,
                Heading = player.Orientation.InHeading
            };

            GameServer.Database.AddObject(door);
            player.Out.SendMessage($"Added door {doorId} to the database!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            // Load only the new door instead of reloading every door of the server.
            if (!DoorMgr.LoadDoor(door))
                player.Out.SendMessage($"Door {doorId} was saved but could not be loaded (zone not found?). It will be loaded at the next restart.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static void HandleDoorChange(GamePlayer player, IDoor door, int doorId, byte doorState, int radius)
        {
            if (door != null)
            {
                bool isEnemy;

                if (door is GameKeepDoor kDoor)
                    isEnemy = GameServer.KeepManager.IsEnemy(kDoor, player);
                else
                    isEnemy = player.Realm != door.Realm && door.Realm != eRealm.None && door.Realm != (eRealm)6;

                if (isEnemy && player.Client.Account.PrivLevel == 1)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameKeepDoor.DoorLocked", door.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (door is GameKeepDoor keepDoor)
                {
                    keepDoor.Interact(player);
                    return;
                }

                if (player.Client.Account.PrivLevel == 1 && !player.IsWithinRadius(door.Position, radius))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.OnTick.TooFarAway", door.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (!door.Interact(player))
                    return;

                if (doorState == 0x01)
                    door.Open(player);
                else
                    door.Close(player);

                return;
            }

            //new frontiers we don't want this, i.e. relic gates etc
            // Door unknown to the server: GMs (and everyone outside region 163) can still open it client-side.
            if (player.CurrentRegionID == 163 && player.Client.Account.PrivLevel == 1)
                return;

            /*
            //create a bug report
            BugReport report = new BugReport();
            report.DateSubmitted = DateTime.Now;
            report.ID = GameServer.Database.GetObjectCount<BugReport>() + 1;
            report.Message = "There is a missing door at location Region: " + player.CurrentRegionID + " X:" + player.X + " Y: " + player.Y + " Z: " + player.Z;
            report.Submitter = player.Name;
            GameServer.Database.AddObject(report);
            */

            player.Out.SendDebugMessage("Door {0} not found in door list, opening via GM door hack.", doorId);

            GameDoor dummyDoor = new GameDoor();
            dummyDoor.DoorID = doorId;
            dummyDoor.Position = player.Position.With(dummyDoor.Orientation);
            dummyDoor.Realm = eRealm.Door;
            dummyDoor.Open(player);
        }
    }
}