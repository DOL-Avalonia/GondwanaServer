/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System.Linq;
using System.Collections.Generic;

using DOL.Database;
using DOL.GS.Keeps;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DoorRequest, "Door Interact Request Handler", eClientStatus.PlayerInGame)]
    public class DoorRequestHandler : IPacketHandler
    {
        public static int m_handlerDoorID;

        /// <summary>
        /// door index which is unique
        /// </summary>
        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            var doorID = (int)packet.ReadInt();
            m_handlerDoorID = doorID;
            var doorState = (byte)packet.ReadByte();
            int doorType = doorID / 100000000;

            int radius = Properties.WORLD_PICKUP_DISTANCE * 2;
            int zoneDoor = doorID / 1000000;

            string debugText = "";

            // For ToA the client always sends the same ID so we need to construct an id using the current zone
            if (client.Player.CurrentRegion.Expansion == (int)eClientExpansion.TrialsOfAtlantis)
            {
                debugText = $"ToA DoorID:{doorID} ";

                doorID -= zoneDoor * 1000000;
                zoneDoor = client.Player.CurrentZone.ID;
                doorID += zoneDoor * 1000000;
                m_handlerDoorID = doorID;

                // experimental to handle a few odd TOA door issues
                if (client.Player.CurrentRegion.IsDungeon)
                    radius *= 4;
            }

            // debug text
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
                    {
                        ChatUtil.SendDebugMessage(client, "Warning: Towers with an Owner Keep ID < 10 will have untargetable doors!");
                    }
                }
                else if (doorType == 9)
                {
                    int doorIndex = doorID - doorType * 10000000;
                    client.Out.SendDebugMessage($"House DoorID:{doorID} state:{doorState} (doorType:{doorType} doorIndex:{doorIndex})");
                }
                else
                {
                    int fixture = (doorID - zoneDoor * 1000000);
                    int fixturePiece = fixture;
                    fixture /= 100;
                    fixturePiece = fixturePiece - fixture * 100;

                    client.Out.SendDebugMessage($"{debugText}DoorID:{doorID} state:{doorState} zone:{zoneDoor} fixture:{fixture} fixturePiece:{fixturePiece} Type:{doorType}");
                }
            }

            // Check if the door exists in memory before firing the missing door dialog
            var memDoor = DoorMgr.GetDoorByID(doorID);
            var dbDoor = DOLDB<DBDoor>.SelectObject(DB.Column(nameof(DBDoor.InternalID)).IsEqualTo(doorID));

            if (memDoor == null && dbDoor == null)
            {
                if (doorType != 9 && client.Account.PrivLevel > 1 && client.Player.CurrentRegion.IsInstance == false)
                {
                    if (client.Player.TempProperties.getProperty(DoorMgr.WANT_TO_ADD_DOORS, false))
                    {
                        client.Player.Out.SendCustomDialog(
                            "This door is not in the database. Place yourself nearest to this door and click Accept to add it.", AddingDoor);
                    }
                    else
                    {
                        client.Player.Out.SendMessage("This door is not in the database. Use '/door show' to enable the add door dialog when targeting doors.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            // Schedule the Door Action unconditionally. We will handle privileges and range directly inside the Tick.
            new ChangeDoorAction(client.Player, doorID, doorState, radius).Start(1);
        }


        public void AddingDoor(GamePlayer player, byte response)
        {
            if (response != 0x01)
                return;

            int doorType = m_handlerDoorID / 100000000;
            if (doorType == 7)
            {
                PositionMgr.CreateDoor(m_handlerDoorID, player);
                player.Out.SendMessage("Added keep door position to the database!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }
            var door = new DBDoor();
            door.ObjectId = null;
            door.InternalID = m_handlerDoorID;
            door.Name = "door";
            door.Type = m_handlerDoorID / 100000000;
            door.Level = 20;
            door.Realm = 6;
            door.MaxHealth = 2545;
            door.Health = 2545;
            door.Locked = 0;
            door.X = player.Position.X;
            door.Y = player.Position.Y;
            door.Z = player.Position.Z;
            door.Heading = player.Orientation.InHeading;
            GameServer.Database.AddObject(door);

            player.Out.SendMessage("Added door " + m_handlerDoorID + " to the database!", eChatType.CT_Important,
                                   eChatLoc.CL_SystemWindow);
            DoorMgr.Init();
        }

        /// <summary>
        /// Handles the door state change actions
        /// </summary>
        protected class ChangeDoorAction : RegionAction
        {
            /// <summary>
            /// The target door Id
            /// </summary>
            protected readonly int m_doorId;

            /// <summary>
            /// The door state
            /// </summary>
            protected readonly int m_doorState;

            /// <summary>
            /// allowed distance to door
            /// </summary>
            protected readonly int m_radius;

            /// <summary>
            /// Constructs a new ChangeDoorAction
            /// </summary>
            /// <param name="actionSource">The action source</param>
            /// <param name="doorId">The target door Id</param>
            /// <param name="doorState">The door state</param>
            /// /// <param name="radius">Interaction radius</param>
            public ChangeDoorAction(GamePlayer actionSource, int doorId, int doorState, int radius)
                : base(actionSource)
            {
                m_doorId = doorId;
                m_doorState = doorState;
                m_radius = radius;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                var player = (GamePlayer)m_actionSource;
                IDoor mydoor = DoorMgr.GetDoorByID(m_doorId);

                if (mydoor != null)
                {
                    bool isEnemy = false;

                    if (mydoor is GameKeepDoor kDoor)
                    {
                        isEnemy = GameServer.KeepManager.IsEnemy(kDoor, player);
                    }
                    else
                    {
                        isEnemy = (player.Realm != mydoor.Realm && mydoor.Realm != eRealm.None && mydoor.Realm != (eRealm)6);
                    }

                    if (isEnemy && player.Client.Account.PrivLevel == 1)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameKeepDoor.DoorLocked", mydoor.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    if (mydoor is GameKeepDoor keepDoor)
                    {
                        keepDoor.Interact(player);
                    }
                    else
                    {
                        if (player.Client.Account.PrivLevel == 1 && !player.IsWithinRadius(mydoor.Position, m_radius))
                        {
                            player.Out.SendMessage(
                                LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.OnTick.TooFarAway", mydoor.Name),
                                eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!mydoor.Interact(player))
                        {
                            Stop();
                            return;
                        }

                        if (m_doorState == 0x01)
                            mydoor.Open(player);
                        else
                            mydoor.Close(player);
                    }
                }
                else
                {
                    //new frontiers we don't want this, i.e. relic gates etc
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

                    player.Out.SendDebugMessage("Door {0} not found in door list, opening via GM door hack.", m_doorId);

                    //else basic quick hack
                    var door = new GameDoor();
                    door.DoorID = m_doorId;
                    door.Position = player.Position.With(door.Orientation);
                    door.Realm = eRealm.Door;
                    door.Open(player);
                }
            }
        }
    }
}