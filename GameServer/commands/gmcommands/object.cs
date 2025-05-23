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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute("&object", //command to handle
                  ePrivLevel.GM, //minimum privelege level
                  "Various Object commands!", //command description
                                              //usage
                  "'/object info' to get information about the object",
                  "'/object movehere' to move object to your location",
                  "'/object create [ObjectClassName]' to create a default object",
                  "'/object fastcreate [name] [modelID]' to create the specified object",
                  "'/object model <newModel>' to set the model to newModel",
                  "'/object modelinc' Increment the object model by 1",
                  "'/object modeldec' Decrement the object model by 1",
                  "'/object emblem <newEmblem>' to set the emblem to newEmblem",
                  "'/object realm <0/1/2/3>' to set the targeted object realm",
                  "'/object name <newName>' to set the targeted object name to newName",
                  "'/object noname' to remove the targeted object name",
                  "'/object respawn <seconds>' to set a respawn time if this object is removed from the world",
                  "'/object remove' to remove the targeted object",
                  "'/object copy' to copy the targeted object",
                  "'/object class' to change the class of the targeted object",
                  "'/object save' to save the object",
                  "'/object target' to automatically target the nearest object",
                  "'/object quests' to load any dataquests associated with the target object")]
    public class ObjectCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        void setClass(GameClient client, GameStaticItem targetObj, string[] args)
        {
            if (args.Length < 3)
            {
                DisplaySyntax(client, args[1]);
                return;
            }

            GameStaticItem obj = null;

            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    obj = assembly.CreateInstance(args[2], true) as GameStaticItem;

                    if (obj != null)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("/obj class - obj", ex);
            }

            if (obj == null)
            {
                try
                {
                    foreach (Assembly assembly in ScriptMgr.Scripts)
                    {
                        obj = assembly.CreateInstance(args[2], true) as GameStaticItem;

                        if (obj != null)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("/obj class - obj", ex);
                }
            }

            if (obj == null)
            {
                client.Out.SendMessage("There was an error creating an instance of " + args[2] + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            obj.Position = client.Player.Position;
            obj.Level = targetObj.Level;
            obj.Name = targetObj.Name;
            obj.Model = targetObj.Model;
            obj.Realm = targetObj.Realm;
            obj.Emblem = targetObj.Emblem;
            obj.LoadedFromScript = false;
            obj.CustomCopy(targetObj);

            obj.AddToWorld();
            obj.SaveIntoDatabase();

            // delete old obj
            targetObj.DeleteFromDatabase();
            targetObj.Delete();

            client.Out.SendMessage("obj class changed: OID=" + obj.ObjectID, eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }
            string param = "";
            if (args.Length > 2)
                param = String.Join(" ", args, 2, args.Length - 2);

            GameStaticItem targetObject = client.Player.TargetObject as GameStaticItem;

            if (targetObject == null && args[1] != "create" && args[1] != "fastcreate" && args[1] != "target" && args[1] != "quests")
            {
                client.Out.SendMessage("Type /object for command overview", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            switch (args[1])
            {
                case "info":
                    {
                        List<string> info = new List<string>();

                        string name = "(blank name)";
                        if (!string.IsNullOrEmpty(targetObject!.Name))
                            name = targetObject.Name;

                        info.Add(" OID: " + targetObject.ObjectID);
                        info.Add(" Type: " + targetObject.GetType());
                        info.Add(" ");
                        info.Add(" Name: " + name);
                        info.Add(" Model: " + targetObject.Model);
                        info.Add(" Emblem: " + targetObject.Emblem);
                        info.Add(" Realm: " + targetObject.Realm);
                        if (targetObject.Owners.LongLength > 0)
                        {
                            info.Add(" ");
                            info.Add(" Owner: " + targetObject.Owners[0].Name);
                        }
                        if (string.IsNullOrEmpty(targetObject.OwnerID) == false)
                        {
                            info.Add(" ");
                            info.Add(" OwnerID: " + targetObject.OwnerID);
                        }
                        if (targetObject.RespawnInterval > 0)
                        {
                            info.Add("RespawnInterval (seconds): " + targetObject.RespawnInterval);
                        }

                        info.Add(" ");

                        WorldInventoryItem invItem = targetObject as WorldInventoryItem;
                        if (invItem != null)
                        {
                            info.Add(" Count: " + invItem.Item.Count);
                        }

                        info.Add(" ");
                        info.Add(" Coordinate: X= " + targetObject.Position.X + " ,Y= " + targetObject.Position.Y + " ,Z= " + targetObject.Position.Z);

                        // --- Infos related to PVP Chests ---
                        if (targetObject is PVPChest pvpChest)
                        {
                            info.Add(" ");
                            info.Add("----- PvPChest Deposited Items -----");
                            IList<string> chestInfo = pvpChest.DelveInfo();
                            foreach (string line in chestInfo)
                            {
                                info.Add(line);
                            }
                        }

                        client.Out.SendCustomTextWindow("[ " + name + " ]", info);
                        break;
                    }
                case "movehere":
                    {
                        targetObject!.Position = client.Player.Position;
                        targetObject.Heading = client.Player.Heading;
                        targetObject.SaveIntoDatabase();
                        break;
                    }
                case "create":
                    {
                        string theType = "DOL.GS.GameStaticItem";
                        if (args.Length > 2)
                            theType = args[2];

                        GameStaticItem obj = CreateItem(client, theType);

                        if (obj != null)
                            DisplayMessage(client, "Obj created: OID=" + obj.ObjectID);

                        break;
                    }
                case "fastcreate":
                    {
                        string objName = "new object";
                        ushort modelID = 100;

                        if (args.Length > 2)
                            objName = args[2];

                        if (args.Length > 3)
                            ushort.TryParse(args[3], out modelID);

                        GameStaticItem obj = CreateItem(client, null);

                        if (obj != null)
                        {
                            obj.Name = objName;
                            obj.Model = modelID;
                            obj.SaveIntoDatabase();
                            DisplayMessage(client, "Object created: OID = " + obj.ObjectID);
                        }

                        break;
                    }
                case "model":
                    {
                        ushort model;
                        try
                        {
                            model = Convert.ToUInt16(args[2]);
                            targetObject!.Model = model;
                            targetObject.SaveIntoDatabase();
                            DisplayMessage(client, "Object model changed to: " + targetObject.Model);
                        }
                        catch (Exception)
                        {
                            DisplayMessage(client, "Type /object for command overview");
                            return;
                        }
                        break;
                    }
                case "modelinc":
                    {
                        ushort model = targetObject!.Model;
                        try
                        {
                            if (model < 8000)
                            {
                                model++;
                                targetObject.Model = model;
                                targetObject.SaveIntoDatabase();
                                DisplayMessage(client, "Object model changed to: " + targetObject.Model);
                            }
                            else
                            {
                                DisplayMessage(client, "Highest object model reached!");
                            }
                        }
                        catch (Exception)
                        {
                            DisplayMessage(client, "Type /object for command overview");
                            return;
                        }
                        break;
                    }
                case "modeldec":
                    {
                        ushort model = targetObject!.Model;
                        try
                        {
                            if (model != 1)
                            {
                                model--;
                                targetObject.Model = model;
                                targetObject.SaveIntoDatabase();
                                DisplayMessage(client, "Object model changed to: " + targetObject.Model);
                            }
                            else
                            {
                                DisplayMessage(client, "Object model cannot be 0!");
                            }
                        }
                        catch (Exception)
                        {
                            DisplayMessage(client, "Type /object for command overview");
                            return;
                        }
                        break;
                    }
                case "emblem":
                    {
                        int emblem;
                        try
                        {
                            emblem = Convert.ToInt32(args[2]);
                            targetObject!.Emblem = emblem;
                            targetObject.SaveIntoDatabase();
                            DisplayMessage(client, "Object emblem changed to: " + targetObject.Emblem);

                            foreach (GamePlayer player in targetObject.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                            {
                                player.Out.SendObjectCreate(targetObject);
                            }
                        }
                        catch (Exception)
                        {
                            DisplayMessage(client, "Type /object for command overview");
                            return;
                        }
                        break;
                    }
                case "realm":
                    {
                        eRealm realm = eRealm.None;
                        if (args[2] == "0") realm = eRealm.None;
                        if (args[2] == "1") realm = eRealm.Albion;
                        if (args[2] == "2") realm = eRealm.Midgard;
                        if (args[2] == "3") realm = eRealm.Hibernia;
                        targetObject!.Realm = realm;
                        targetObject.SaveIntoDatabase();
                        DisplayMessage(client, "Object realm changed to: " + targetObject.Realm);

                        break;
                    }
                case "name":
                    {
                        if (param != "")
                        {
                            targetObject!.Name = param;
                            targetObject.SaveIntoDatabase();
                            DisplayMessage(client, "Object name changed to: " + targetObject.Name);
                        }
                        break;
                    }
                case "noname":
                    {
                        targetObject!.Name = "";
                        targetObject.SaveIntoDatabase();
                        DisplayMessage(client, "Object name removed");
                        break;
                    }
                case "copy":
                    {
                        GameStaticItem item = CreateItemInstance(client, targetObject!.GetType().FullName);
                        if (item == null)
                        {
                            ChatUtil.SendSystemMessage(client, "There was an error creating an instance of " + targetObject.GetType().FullName + "!");
                            return;
                        }
                        item.Position = client.Player.Position;
                        item.Level = targetObject.Level;
                        item.Name = targetObject.Name;
                        item.Model = targetObject.Model;
                        item.Realm = targetObject.Realm;
                        item.Emblem = targetObject.Emblem;
                        item.LoadedFromScript = targetObject.LoadedFromScript;
                        item.CustomCopy(targetObject);
                        item.AddToWorld();
                        item.SaveIntoDatabase();
                        DisplayMessage(client, "Obj created: OID=" + item.ObjectID);
                        break;
                    }

                case "class":
                    {
                        setClass(client, targetObject, args);
                        break;
                    }

                case "save":
                    {
                        targetObject!.LoadedFromScript = false;
                        targetObject.SaveIntoDatabase();
                        DisplayMessage(client, "Object saved to Database");
                        break;
                    }
                case "remove":
                    {
                        targetObject!.DeleteFromDatabase();
                        targetObject.Delete();
                        DisplayMessage(client, "Object removed from Clients and Database");
                        break;
                    }
                case "target":
                    {
                        foreach (GameStaticItem item in client.Player.GetItemsInRadius(1000))
                        {
                            client.Player.TargetObject = item;
                            DisplayMessage(client, "Target set to nearest object!");
                            return;
                        }

                        DisplayMessage(client, "No objects in 1000 unit range!");
                        break;
                    }
                case "respawn":
                    {
                        int respawn = 0;
                        if (int.TryParse(args[2], out respawn))
                        {
                            targetObject!.RespawnInterval = respawn;
                            targetObject.SaveIntoDatabase();
                            DisplayMessage(client, "Object RespawnInterval set to " + targetObject.RespawnInterval + " seconds.");
                        }

                        break;
                    }
                case "quests":
                    {
                        try
                        {

                            if (client.Player.TargetObject is GameNPC npc)
                            {
                                foreach (GamePlayer player in client.Player.TargetObject.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                                {
                                    player.Out.SendNPCsQuestEffect(npc, npc.GetQuestIndicator(player));
                                }

                                client.Out.SendMessage(npc.QuestIdListToGive.Count + " Quests loaded for this npc.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        catch (Exception)
                        {
                            DisplayMessage(client, "Error refreshing quests.");
                        }

                        break;
                    }
            }
        }

        GameStaticItem CreateItemInstance(GameClient client, string itemClassName)
        {
            GameStaticItem obj = null;

            foreach (Assembly script in ScriptMgr.GameServerScripts)
            {
                try
                {
                    client.Out.SendDebugMessage(script.FullName);
                    obj = (GameStaticItem)script.CreateInstance(itemClassName, false);

                    if (obj != null)
                        break;
                }
                catch (Exception e)
                {
                    DisplayMessage(client, e.ToString());
                }
            }
            return obj;
        }

        GameStaticItem CreateItem(GameClient client, string itemClassName)
        {
            GameStaticItem obj;

            if (!string.IsNullOrEmpty(itemClassName))
                obj = CreateItemInstance(client, itemClassName);
            else
                obj = new GameStaticItem();


            if (obj == null)
            {
                client.Out.SendMessage("There was an error creating an instance of " + itemClassName + "!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return null;
            }

            //Fill the object variables
            obj.LoadedFromScript = false;
            obj.Position = client.Player.Position;
            obj.Name = "New Object";
            obj.Model = 100;
            obj.Emblem = 0;
            obj.Realm = 0;
            obj.AddToWorld();
            obj.SaveIntoDatabase();

            return obj;
        }
    }
}