using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.Geometry;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
         "&keepcomponent",
         ePrivLevel.GM,
         "Commands.GM.KeepComponents.Description",
         "Commands.GM.KeepComponents.Usage.Create.TID",
         "Commands.GM.KeepComponents.Usage.Create.T",
         "Commands.GM.KeepComponents.Usage.Skin",
         "/keepcomponent move - move to your position",
         "/keepcomponent movex <value> - move step by step on X axis (can be negative)",
         "/keepcomponent movey <value> - move step by step on Y axis (can be negative)",
         "/keepcomponent rotate [0 - 3]",
         "/keepcomponent reload",
         "'/keepcomponent save' to save the component in the DB",
         "/keepcomponent info - display detailed component and door info",
         "Commands.GM.KeepComponents.Usage.Delete")]
    public class KeepComponentCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private readonly ushort INVISIBLE_MODEL = 150;

        private GameKeepComponent GetTargetedComponent(GameObject target)
        {
            if (target is GameKeepComponent comp)
                return comp;

            if (target is IKeepItem keepItem && keepItem.Component is GameKeepComponent parentComp)
                return parentComp;

            return null;
        }

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1) { DisplaySyntax(client); return; }

            AbstractGameKeep myKeep = GameServer.KeepManager.GetKeepCloseToSpot(client.Player.Position, WorldMgr.OBJ_UPDATE_DISTANCE);

            if (myKeep == null)
            {
                DisplayMessage(client, "You are not near a keep.");
                return;
            }

            GameKeepComponent component = GetTargetedComponent(client.Player.TargetObject);

            switch (args[1].ToLower())
            {
                case "create":
                    {
                        if (args.Length < 3) { DisplaySyntax(client); return; }
                        int skin = 0;
                        try { skin = Convert.ToInt32(args[2]); }
                        catch { DisplaySyntax(client); return; }

                        if (args.Length >= 4)
                        {
                            try { myKeep = GameServer.KeepManager.GetKeepByID(Convert.ToInt32(args[3])); }
                            catch { DisplaySyntax(client); return; }
                        }

                        GameKeepComponent newComp = new GameKeepComponent();
                        newComp.ComponentHeading = (client.Player.Orientation - myKeep.Orientation).InHeading / 1024;
                        newComp.Position = client.Player.Position.With(Angle.Degrees(newComp.ComponentHeading * 90) + myKeep.Orientation);
                        newComp.Keep = myKeep;

                        var angle = myKeep.Orientation.InRadians;
                        newComp.ComponentX = CalcCX(client.Player, myKeep, angle);
                        newComp.ComponentY = CalcCY(client.Player, myKeep, angle);

                        newComp.Name = myKeep.Name;
                        newComp.Model = INVISIBLE_MODEL;
                        newComp.Skin = skin;
                        newComp.Level = (byte)myKeep.Level;
                        newComp.Health = newComp.MaxHealth;
                        newComp.ID = myKeep.KeepComponents.Count;
                        newComp.Keep.KeepComponents.Add(newComp);
                        newComp.SaveInDB = true;
                        newComp.AddToWorld();
                        newComp.SaveIntoDatabase();

                        client.Out.SendKeepInfo(myKeep);
                        client.Out.SendKeepComponentInfo(newComp);
                        client.Out.SendMessage("Keep component created.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "move":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }

                        component.ComponentHeading = (client.Player.Orientation - myKeep.Orientation).InDegrees / 90;
                        component.Position = client.Player.Position.With(Angle.Heading(component.ComponentHeading * 1024) + myKeep.Orientation);
                        component.Keep = myKeep;

                        var angle = myKeep.Orientation.InRadians;
                        component.ComponentX = CalcCX(client.Player, myKeep, angle);
                        component.ComponentY = CalcCY(client.Player, myKeep, angle);

                        client.Out.SendKeepInfo(myKeep);
                        client.Out.SendKeepComponentInfo(component);
                        client.Out.SendKeepComponentDetailUpdate(component);
                        client.Out.SendMessage($"Component moved to X:{component.ComponentX} Y:{component.ComponentY}. Use /keepcomponent save to save.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "movex":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        if (args.Length < 3) { DisplaySyntax(client); return; }

                        try
                        {
                            int val = Convert.ToInt32(args[2]);
                            component.ComponentX += val;

                            // Recalculate World Position using internal Grid offsets
                            var angle = component.Keep.Orientation;
                            var offset = Vector.Create(148 * (sbyte)component.ComponentX, -148 * (sbyte)component.ComponentY, 0).RotatedClockwise(angle);
                            component.Position = component.Keep.Position.With(angle + Angle.Degrees(component.ComponentHeading * 90)) + offset;

                            foreach (GameClient cli in WorldMgr.GetClientsOfRegion(client.Player.CurrentRegionID))
                            {
                                cli.Out.SendKeepComponentInfo(component);
                                cli.Out.SendKeepComponentDetailUpdate(component);
                            }
                            client.Out.SendMessage($"Component moved on X axis by {val}. New CX: {component.ComponentX}. Use /keepcomponent save to save.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        catch { DisplaySyntax(client); return; }
                    }
                    break;

                case "movey":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        if (args.Length < 3) { DisplaySyntax(client); return; }

                        try
                        {
                            int val = Convert.ToInt32(args[2]);
                            component.ComponentY += val;

                            // Recalculate World Position using internal Grid offsets
                            var angle = component.Keep.Orientation;
                            var offset = Vector.Create(148 * (sbyte)component.ComponentX, -148 * (sbyte)component.ComponentY, 0).RotatedClockwise(angle);
                            component.Position = component.Keep.Position.With(angle + Angle.Degrees(component.ComponentHeading * 90)) + offset;

                            foreach (GameClient cli in WorldMgr.GetClientsOfRegion(client.Player.CurrentRegionID))
                            {
                                cli.Out.SendKeepComponentInfo(component);
                                cli.Out.SendKeepComponentDetailUpdate(component);
                            }
                            client.Out.SendMessage($"Component moved on Y axis by {val}. New CY: {component.ComponentY}. Use /keepcomponent save to save.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        catch { DisplaySyntax(client); return; }
                    }
                    break;

                case "rotate":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        try
                        {
                            ushort amount = Convert.ToUInt16(args[2]);
                            if (amount > 3) amount = 3;

                            component.ComponentHeading = amount;
                            component.Orientation = Angle.Heading(component.ComponentHeading * 1024) + myKeep.Orientation;

                            client.Out.SendKeepInfo(myKeep);
                            client.Out.SendKeepComponentInfo(component);
                            client.Out.SendKeepComponentDetailUpdate(component);
                            client.Out.SendMessage($"Component rotated to {amount}. Use /keepcomponent save to save.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        catch { DisplayMessage(client, "/keepcomponent rotate [0 - 3]"); }
                    }
                    break;

                case "skin":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        if (args.Length < 3) { DisplaySyntax(client); return; }

                        try { component.Skin = Convert.ToInt32(args[2]); }
                        catch { DisplaySyntax(client); return; }

                        foreach (GameClient cli in WorldMgr.GetClientsOfRegion(client.Player.CurrentRegionID))
                        {
                            cli.Out.SendKeepComponentInfo(component);
                            cli.Out.SendKeepComponentDetailUpdate(component);
                        }
                        client.Out.SendMessage($"Component skin updated to {component.Skin}. Use /keepcomponent save to save.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "delete":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        component.RemoveFromWorld();
                        component.Delete();
                        component.DeleteFromDatabase();
                        client.Out.SendMessage("Component deleted.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "save":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        component.SaveIntoDatabase();
                        client.Out.SendMessage($"Saved ComponentID: {component.ID}, KeepID: {(component.Keep == null ? "0" : component.Keep.KeepID.ToString())}, Skin: {component.Skin}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "reload":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }
                        var dbcomponent = DOLDB<DBKeepComponent>.SelectObject(DB.Column(nameof(DBKeepComponent.KeepID)).IsEqualTo(component.Keep.KeepID).And(DB.Column(nameof(DBKeepComponent.ID)).IsEqualTo(component.ID)));
                        if (dbcomponent != null)
                        {
                            component.ComponentX = dbcomponent.X;
                            component.ComponentY = dbcomponent.Y;
                            component.ComponentHeading = dbcomponent.Heading;
                            component.Skin = dbcomponent.Skin;

                            foreach (GameClient cli in WorldMgr.GetClientsOfRegion(client.Player.CurrentRegionID))
                            {
                                cli.Out.SendKeepComponentInfo(component);
                                cli.Out.SendKeepComponentDetailUpdate(component);
                            }
                            client.Out.SendMessage("Component Reloaded", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;

                case "info":
                    {
                        if (component == null)
                        {
                            DisplayMessage(client, "You must target a Keep Component, Door, or Guard first.");
                            return;
                        }

                        List<string> text = new List<string>();
                        text.Add("-- Keep Component Info --");
                        text.Add($"+ Keep: {component.Keep.Name}");
                        text.Add($"+ Keep ID: {component.Keep.KeepID}");
                        text.Add($"+ Component ID: {component.ID}");
                        text.Add($"+ Internal DB ID: {component.InternalID}");

                        string skinName = Enum.IsDefined(typeof(GameKeepComponent.eComponentSkin), (byte)component.Skin)
                                            ? ((GameKeepComponent.eComponentSkin)component.Skin).ToString()
                                            : "Unknown";
                        text.Add($"+ Skin: {component.Skin} ({skinName})");
                        text.Add($"+ Health: {component.Health} / {component.MaxHealth} ({component.HealthPercent}%)");

                        text.Add("");
                        text.Add("-- Positions --");
                        text.Add($"+ Absolute: X:{component.Position.X}  Y:{component.Position.Y}  Z:{component.Position.Z}");
                        text.Add($"+ Absolute Heading: {component.Orientation.InHeading}");
                        text.Add($"+ Relative Grid: CX:{component.ComponentX}  CY:{component.ComponentY}");
                        text.Add($"+ Relative Heading: {component.ComponentHeading}");

                        // Find all doors linked to this exact component
                        var linkedDoors = component.Keep.Doors.Values.Where(d => d.Component == component).ToList();

                        text.Add("");
                        text.Add($"-- Linked Doors ({linkedDoors.Count}) --");

                        if (linkedDoors.Count > 0)
                        {
                            for (int i = 0; i < linkedDoors.Count; i++)
                            {
                                var door = linkedDoors[i];
                                text.Add($"Door #{i + 1}: {door.Name}");
                                text.Add($"  - Internal ID: {door.DoorID}");
                                text.Add($"  - Position: X:{door.Position.X}  Y:{door.Position.Y}  Z:{door.Position.Z}");
                                text.Add($"  - Heading: {door.Orientation.InHeading}");
                                text.Add($"  - Realm: {door.Realm}");
                                text.Add($"  - Level: {door.Level}");
                                text.Add($"  - Health: {door.Health} / {door.MaxHealth}");
                                text.Add($"  - State: {door.State}");
                                text.Add("");
                            }
                        }

                        client.Player.Out.SendCustomTextWindow("Keep Component Details", text);
                    }
                    break;

                default: DisplaySyntax(client); return;
            }
        }

        public int CalcCX(GamePlayer player, AbstractGameKeep myKeep, double angle)
        {
            var keepPos = myKeep.Position;
            var playerPos = player.Position;
            if (Math.Abs(Math.Sin(angle)) < 0.0001) return (playerPos.X - keepPos.X) / 148;
            return (int)((148 * Math.Sin(angle) * keepPos.X - 148 * Math.Sin(angle) * playerPos.X + playerPos.Y - keepPos.Y) / (148 * Math.Sin(angle) - 148 * 148 * 2 * Math.Sin(angle) * Math.Cos(angle)));
        }

        public int CalcCY(GamePlayer player, AbstractGameKeep myKeep, double angle)
        {
            var keepPos = myKeep.Position;
            var playerPos = player.Position;
            if (Math.Abs(Math.Sin(angle)) < 0.0001) return (keepPos.Y - playerPos.Y) / 148;
            int cx = CalcCX(player, myKeep, angle);
            return (int)((keepPos.Y - playerPos.Y + 148 * Math.Sin(angle) * cx) / (148 * Math.Cos(angle)));
        }
    }
}