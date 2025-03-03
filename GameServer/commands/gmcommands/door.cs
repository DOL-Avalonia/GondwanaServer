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

/*
  * New system by Niko jan 2009
  */

using System;
using System.Linq;
using System.Collections.Generic;

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.MobGroups;
using DOL.GS.Keeps;

namespace DOL.GS.Commands
{
    [Cmd(
        "&door",
        ePrivLevel.GM,
        "Commands.GM.door.Description",
        "'/door show' toggle enable/disable add dialog when targeting doors",
        "Commands.GM.door.Add",
        "Commands.GM.door.Update",
        "Commands.GM.door.Delete",
        "Commands.GM.door.Name",
        "Commands.GM.door.Level",
        "Commands.GM.door.Realm",
        "Commands.GM.door.Guild",
        "Commands.GM.door.Usage.Sound",
        "Commands.GM.door.Usage.GroupMob",
        "Commands.GM.door.Usage.Key",
        "Commands.GM.door.Usage.KeyChance",
        "Commands.GM.door.Usage.IsRenaissance",
        "Commands.GM.door.Usage.PunishSpell",
        "Commands.GM.door.Info",
        "Commands.GM.door.Heal",
        "Commands.GM.door.Locked",
        "Commands.GM.door.Unlocked")]
    public class NewDoorCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private int DoorID;
        private int doorType;
        private string Realmname;
        private string statut;

        #region ICommandHandler Members

        public void OnCommand(GameClient client, string[] args)
        {
            IDoor targetIDoor = null;

            if (args.Length > 1 && args[1] == "show" && client.Player != null)
            {
                if (client.Player.TempProperties.getProperty(DoorMgr.WANT_TO_ADD_DOORS, false))
                {
                    client.Player.TempProperties.removeProperty(DoorMgr.WANT_TO_ADD_DOORS);
                    client.Out.SendMessage("You will no longer be shown the add door dialog.", eChatType.CT_System,
                                           eChatLoc.CL_SystemWindow);
                }
                else
                {
                    client.Player.TempProperties.setProperty(DoorMgr.WANT_TO_ADD_DOORS, true);
                    client.Out.SendMessage("You will now be shown the add door dialog if door is not found in the DB.",
                                           eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                return;
            }

            if (client.Player!.CurrentRegion.IsInstance)
            {
                client.Out.SendMessage("You can't add doors inside an instance.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (client.Player.TargetObject == null)
            {
                client.Out.SendMessage("You must target a door", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (client.Player.TargetObject != null &&
                (client.Player.TargetObject is GameNPC || client.Player.TargetObject is GamePlayer))
            {
                client.Out.SendMessage("You must target a door", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (client.Player.TargetObject != null && client.Player.TargetObject is IDoor)
            {
                targetIDoor = (IDoor)client.Player.TargetObject;
                DoorID = targetIDoor.DoorID;
                doorType = targetIDoor.DoorID / 100000000;
            }

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }
            if (targetIDoor is GameDoor targetDoor)
            {
                switch (args[1])
                {
                    case "name":
                        name(client, targetDoor, args);
                        break;
                    case "guild":
                        guild(client, targetDoor, args);
                        break;
                    case "level":
                        level(client, targetDoor, args);
                        break;
                    case "realm":
                        realm(client, targetDoor, args);
                        break;
                    case "info":
                        info(client, targetDoor);
                        break;
                    case "heal":
                        heal(client, targetDoor);
                        break;
                    case "locked":
                        locked(client, targetDoor);
                        break;
                    case "unlocked":
                        unlocked(client, targetDoor);
                        break;
                    case "kill":
                        kill(client, targetDoor, args);
                        break;
                    case "delete":
                        delete(client, targetDoor);
                        break;
                    case "add":
                        add(client, targetDoor);
                        break;
                    case "update":
                        update(client, targetDoor);
                        break;
                    case "sound":
                        sound(client, targetDoor, args);
                        break;
                    case "groupmob":
                        GroupMob(client, targetDoor, args);
                        break;
                    case "key":
                        Key(client, targetDoor, args);
                        break;
                    case "key_chance":
                        Key_Chance(client, targetDoor, args);
                        break;
                    case "isrenaissance":
                        IsRenaissance(client, targetDoor, args);
                        break;
                    case "punishspell":
                        PunishSpell(client, targetDoor, args);
                        break;

                    default:
                        DisplaySyntax(client);
                        return;
                }
            }
            else if (targetIDoor is GameKeepDoor targetKeepDoor)
            {
                switch (args[1])
                {
                    case "add":
                        add(client, targetKeepDoor);
                        break;
                    case "info":
                        info(client, targetKeepDoor);
                        break;
                }
            }
        }
        #endregion

        /// <summary>
        /// Method to add a groopmob to a door
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetDoor"></param>
        /// <param name="args"></param>
        private void GroupMob(GameClient client, GameDoor targetDoor, string[] args)
        {
            string groupId = null;
            if (args.Length > 2)
                groupId = args[2];
            if (!string.IsNullOrEmpty(groupId) && !MobGroupManager.Instance.Groups.ContainsKey(groupId))
            {
                client.Out.SendMessage($"Le groupe {groupId} n'existe pas.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                return;
            }
            targetDoor.Group_Mob_Id = groupId;
            targetDoor.SaveIntoDatabase();
        }

        /// <summary>
        /// Add a itemtemplate id to open the door
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetDoor"></param>
        /// <param name="args"></param>
        private void Key(GameClient client, GameDoor targetDoor, string[] args)
        {
            if (args.Length == 2)
                targetDoor.Key = null;
            else
                targetDoor.Key = args[2];
            targetDoor.SaveIntoDatabase();
        }

        /// <summary>
        /// Add a chance to fail to open the door
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetDoor"></param>
        /// <param name="args"></param>
        private void Key_Chance(GameClient client, GameDoor targetDoor, string[] args)
        {
            if (args.Length == 2)
                targetDoor.Key_Chance = 0;
            else
                targetDoor.Key_Chance = short.Parse(args[2]);
            targetDoor.SaveIntoDatabase();
        }

        /// <summary>
        /// Toggle enable/disable Renaissance requirment 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetDoor"></param>
        /// <param name="args"></param>
        private void IsRenaissance(GameClient client, GameDoor targetDoor, string[] args)
        {
            targetDoor.IsRenaissance = !targetDoor.IsRenaissance;
            targetDoor.SaveIntoDatabase();
        }

        /// <summary>
        /// Add a spell id to punish the opener
        /// </summary>
        /// <param name="client"></param>
        /// <param name="targetDoor"></param>
        /// <param name="args"></param>
        private void PunishSpell(GameClient client, GameDoor targetDoor, string[] args)
        {
            if (args.Length == 2)
                targetDoor.PunishSpell = 0;
            else
                targetDoor.PunishSpell = int.Parse(args[2]);
            targetDoor.SaveIntoDatabase();
        }

        private void add(GameClient client, GameDoor targetDoor)
        {
            var DOOR = DOLDB<DBDoor>.SelectObject(DB.Column(nameof(DBDoor.InternalID)).IsEqualTo(DoorID));

            if (DOOR != null)
            {
                client.Out.SendMessage("The door is already in the database", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (DOOR == null)
            {
                if (doorType != 9)
                {
                    var door = new DBDoor();
                    door.ObjectId = null;
                    door.InternalID = DoorID;
                    door.Name = "door";
                    door.Type = DoorID / 100000000;
                    door.Level = 20;
                    door.Realm = 6;
                    door.X = targetDoor.Position.X;
                    door.Y = targetDoor.Position.Y;
                    door.Z = targetDoor.Position.Z;
                    door.Heading = targetDoor.Orientation.InHeading;
                    door.Health = 2545;
                    GameServer.Database.AddObject(door);
                    (targetDoor).AddToWorld();
                    client.Player.Out.SendMessage("Added door ID:" + DoorID + "to the database", eChatType.CT_Important,
                                                  eChatLoc.CL_SystemWindow);
                    //DoorMgr.Init( );
                    return;
                }
            }
        }
        private void add(GameClient client, GameKeepDoor targetDoor)
        {
            var DOOR = DOLDB<DBDoor>.SelectObject(DB.Column(nameof(DBDoor.InternalID)).IsEqualTo(DoorID));

            if (DOOR != null)
            {
                client.Out.SendMessage("The door is already in the database", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (DOOR == null)
            {
                if (doorType != 9)
                {
                    var door = new DBDoor();
                    door.ObjectId = null;
                    door.InternalID = DoorID;
                    door.Name = "door";
                    door.Type = DoorID / 100000000;
                    door.Level = 20;
                    door.Realm = 6;
                    door.X = targetDoor.Position.X;
                    door.Y = targetDoor.Position.Y;
                    door.Z = targetDoor.Position.Z;
                    door.Heading = targetDoor.Orientation.InHeading;
                    door.Health = 2545;
                    GameServer.Database.AddObject(door);
                    (targetDoor).AddToWorld();
                    client.Player.Out.SendMessage("Added door ID:" + DoorID + "to the database", eChatType.CT_Important,
                                                  eChatLoc.CL_SystemWindow);
                    //DoorMgr.Init( );
                    return;
                }
            }
        }

        private void update(GameClient client, GameDoor targetDoor)
        {
            delete(client, targetDoor);

            if (targetDoor != null)
            {
                if (doorType != 9)
                {
                    var door = new DBDoor();
                    door.ObjectId = null;
                    door.InternalID = DoorID;
                    door.Name = "door";
                    door.Type = DoorID / 100000000;
                    door.Level = targetDoor.Level;
                    door.Realm = (byte)targetDoor.Realm;
                    door.Health = targetDoor.Health;
                    door.Locked = targetDoor.Locked;
                    door.X = client.Player.Position.X;
                    door.Y = client.Player.Position.Y;
                    door.Z = client.Player.Position.Z;
                    door.Heading = client.Player.Orientation.InHeading;
                    GameServer.Database.AddObject(door);
                    (targetDoor).AddToWorld();
                    client.Player.Out.SendMessage("Added door " + DoorID + " to the database", eChatType.CT_Important,
                                                  eChatLoc.CL_SystemWindow);
                    return;
                }
            }
        }

        private void delete(GameClient client, GameDoor targetDoor)
        {
            var DOOR = DOLDB<DBDoor>.SelectObject(DB.Column(nameof(DBDoor.InternalID)).IsEqualTo(DoorID));

            if (DOOR != null)
            {
                GameServer.Database.DeleteObject(DOOR);
                client.Out.SendMessage("Door removed", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (DOOR == null)
            {
                client.Out.SendMessage("This door doesn't exist in the database", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
        }


        private void name(GameClient client, GameDoor targetDoor, string[] args)
        {
            string doorName = "";

            if (args.Length > 2)
                doorName = String.Join(" ", args, 2, args.Length - 2);

            if (doorName != "")
            {
                targetDoor.Name = CheckName(doorName, client);
                targetDoor.SaveIntoDatabase();
                client.Out.SendMessage("You changed the door name to " + targetDoor.Name, eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            }
            else
            {
                DisplaySyntax(client, args[1]);
            }
        }

        private void sound(GameClient client, GameDoor targetDoor, string[] args)
        {
            uint doorSound = 0;

            try
            {
                if (args.Length > 2)
                {
                    doorSound = Convert.ToUInt16(args[2]);
                    targetDoor.Flag = doorSound;
                    targetDoor.SaveIntoDatabase();
                    client.Out.SendMessage("You set the door sound to " + doorSound, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    DisplaySyntax(client, args[1]);
                }
            }
            catch
            {
                DisplaySyntax(client, args[1]);
            }
        }

        private void guild(GameClient client, GameDoor targetDoor, string[] args)
        {
            string guildName = "";

            if (args.Length > 2)
                guildName = String.Join(" ", args, 2, args.Length - 2);

            if (guildName != "")
            {
                targetDoor.GuildName = CheckGuildName(guildName, client);
                targetDoor.SaveIntoDatabase();
                client.Out.SendMessage("You changed the door guild to " + targetDoor.GuildName, eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            }
            else
            {
                if (targetDoor.GuildName != "")
                {
                    targetDoor.GuildName = "";
                    targetDoor.SaveIntoDatabase();
                    client.Out.SendMessage("Door guild removed", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                    DisplaySyntax(client, args[1]);
            }
        }

        private void level(GameClient client, GameDoor targetDoor, string[] args)
        {
            byte level;

            try
            {
                level = Convert.ToByte(args[2]);
                targetDoor.Level = level;
                targetDoor.Health = targetDoor.MaxHealth;
                targetDoor.SaveIntoDatabase();
                client.Out.SendMessage("You changed the door level to " + targetDoor.Level, eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            }
            catch (Exception)
            {
                DisplaySyntax(client, args[1]);
            }
        }

        private void realm(GameClient client, GameDoor targetDoor, string[] args)
        {
            byte realm;

            try
            {
                realm = Convert.ToByte(args[2]);
                targetDoor.Realm = (eRealm)realm;
                targetDoor.SaveIntoDatabase();
                client.Out.SendMessage("You changed the door realm to " + targetDoor.Realm, eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            }
            catch (Exception)
            {
                DisplaySyntax(client, args[1]);
            }
        }

        private void info(GameClient client, GameDoor targetDoor)
        {
            if (targetDoor.Realm == eRealm.None)
                Realmname = "None";

            if (targetDoor.Realm == eRealm.Albion)
                Realmname = "Albion";

            if (targetDoor.Realm == eRealm.Midgard)
                Realmname = "Midgard";

            if (targetDoor.Realm == eRealm.Hibernia)
                Realmname = "Hibernia";

            if (targetDoor.Realm == eRealm.Door)
                Realmname = "All";

            if (targetDoor.Locked == 1)
                statut = " Locked";

            if (targetDoor.Locked == 0)
                statut = " Unlocked";

            int doorType = DoorRequestHandler.m_handlerDoorID / 100000000;

            var info = new List<string>();

            info.Add(" + Door Info :  " + targetDoor.Name);
            info.Add("  ");
            info.Add(" + Name : " + targetDoor.Name);
            info.Add(" + ID : " + DoorID);
            info.Add(" + Realm : " + (int)targetDoor.Realm + " : " + Realmname);
            info.Add(" + Level : " + targetDoor.Level);
            info.Add(" + Guild : " + targetDoor.GuildName);
            info.Add(" + Health : " + targetDoor.Health + " / " + targetDoor.MaxHealth);
            info.Add(" + Statut : " + statut);
            info.Add(" + Type : " + doorType);
            info.Add(" + X : " + targetDoor.Position.X);
            info.Add(" + Y : " + targetDoor.Position.Y);
            info.Add(" + Z : " + targetDoor.Position.Z);
            info.Add(" + Heading : " + targetDoor.Orientation.InHeading);
            info.Add(" + Group Mob : " + targetDoor.Group_Mob_Id);
            info.Add(" + Key : " + targetDoor.Key);
            info.Add(" + Key Chance : " + targetDoor.Key_Chance);
            info.Add(" + IsRenaissance : " + targetDoor.IsRenaissance);
            info.Add(" + Punish Spell : " + targetDoor.PunishSpell);

            client.Out.SendCustomTextWindow("Door Information", info);
        }

        private void info(GameClient client, GameKeepDoor targetDoor)
        {
            if (targetDoor.Realm == eRealm.None)
                Realmname = "None";

            if (targetDoor.Realm == eRealm.Albion)
                Realmname = "Albion";

            if (targetDoor.Realm == eRealm.Midgard)
                Realmname = "Midgard";

            if (targetDoor.Realm == eRealm.Hibernia)
                Realmname = "Hibernia";

            if (targetDoor.Realm == eRealm.Door)
                Realmname = "All";

            int doorType = DoorRequestHandler.m_handlerDoorID / 100000000;

            var info = new List<string>();

            info.Add(" + Door Info :  " + targetDoor.Name);
            info.Add("  ");
            info.Add(" + Name : " + targetDoor.Name);
            info.Add(" + ID : " + DoorID);
            info.Add(" + Realm : " + (int)targetDoor.Realm + " : " + Realmname);
            info.Add(" + Level : " + targetDoor.Level);
            info.Add(" + Guild : " + targetDoor.GuildName);
            info.Add(" + Health : " + targetDoor.Health + " / " + targetDoor.MaxHealth);
            info.Add(" + Statut : " + statut);
            info.Add(" + Type : " + doorType);
            info.Add(" + X : " + targetDoor.Position.X.ToString("F0"));
            info.Add(" + Y : " + targetDoor.Position.Y.ToString("F0"));
            info.Add(" + Z : " + targetDoor.Position.Z.ToString("F0"));
            info.Add(" + Heading : " + targetDoor.Heading);

            client.Out.SendCustomTextWindow("Door Information", info);
        }

        private void heal(GameClient client, GameDoor targetDoor)
        {
            targetDoor.Health = targetDoor.MaxHealth;
            targetDoor.SaveIntoDatabase();
            client.Out.SendMessage("You change the door health to " + targetDoor.Health, eChatType.CT_System,
                                   eChatLoc.CL_SystemWindow);
        }

        private void locked(GameClient client, GameDoor targetDoor)
        {
            targetDoor.Locked = 1;
            targetDoor.SaveIntoDatabase();
            client.Out.SendMessage("Door " + targetDoor.Name + " is locked", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private void unlocked(GameClient client, GameDoor targetDoor)
        {
            targetDoor.Locked = 0;
            targetDoor.SaveIntoDatabase();
            client.Out.SendMessage("Door " + targetDoor.Name + " is unlocked", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }


        private void kill(GameClient client, GameDoor targetDoor, string[] args)
        {
            try
            {
                targetDoor.AddAttacker(client.Player);
                targetDoor.AddXPGainer(client.Player, targetDoor.Health);
                targetDoor.Die(client.Player);
                targetDoor.XPGainers.Clear();
                client.Out.SendMessage("Door " + targetDoor.Name + " health reaches 0", eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            }
            catch (Exception e)
            {
                client.Out.SendMessage(e.ToString(), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private string CheckName(string name, GameClient client)
        {
            if (name.Length > 47)
                client.Out.SendMessage("The door name must not be longer than 47 bytes", eChatType.CT_System,
                                       eChatLoc.CL_SystemWindow);
            return name;
        }

        private string CheckGuildName(string name, GameClient client)
        {
            if (name.Length > 47)
                client.Out.SendMessage("The guild name is " + name.Length + ", but only 47 bytes 'll be displayed",
                                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return name;
        }
    }
}