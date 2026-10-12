using DOL.GS.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Handles spell cast requests from client
    /// </summary>
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.UseSpell, "Handles Player Use Spell Request.", eClientStatus.PlayerInGame)]
    public class UseSpellHandler : PacketHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player == null) return;

            int flagSpeedData, spellLevel, spellLineIndex;

            if (client.Version >= GameClient.eClientVersion.Version1124)
            {
                int x = (int)packet.ReadFloatLowEndian();
                int y = (int)packet.ReadFloatLowEndian();
                int z = (int)packet.ReadFloatLowEndian();
                short speed = (short)packet.ReadFloatLowEndian();
                ushort heading = packet.ReadShort();
                player.Position = Position.Create(player.CurrentRegionID, x, y, z, heading);
                player.CurrentSpeed = speed;
                flagSpeedData = packet.ReadShort();
                spellLevel = packet.ReadByte();
                spellLineIndex = packet.ReadByte();
            }
            else
            {
                flagSpeedData = packet.ReadShort();
                int heading = packet.ReadShort();
                if (client.Version > GameClient.eClientVersion.Version171)
                {
                    int xOffsetInZone = packet.ReadShort();
                    int yOffsetInZone = packet.ReadShort();
                    int currentZoneID = packet.ReadShort();
                    int realZ = packet.ReadShort();
                    Zone newZone = WorldMgr.GetZone((ushort)currentZoneID);
                    if (newZone == null)
                        Log.Warn($"Unknown zone in UseSpellHandler: {currentZoneID} player: {player.Name}");
                    else
                        player.Position = Position.Create(player.CurrentRegionID, newZone.Offset.X + xOffsetInZone, newZone.Offset.Y + yOffsetInZone, realZ, player.Orientation);
                }
                spellLevel = packet.ReadByte();
                spellLineIndex = packet.ReadByte();
                player.Orientation = Angle.Heading(heading);
            }

            player.SetCurrentSpeed((flagSpeedData & 0x200) != 0 ? (short)-(flagSpeedData & 0x1ff) : (short)(flagSpeedData & 0x1ff));
            player.IsStrafing = (flagSpeedData & 0x4000) != 0;
            player.TargetInView = (flagSpeedData & 0xa000) != 0;
            player.GroundTargetInView = (flagSpeedData & 0x1000) != 0;

            List<Tuple<SpellLine, List<Skill>>> snap = player.GetAllUsableListSpells();
            Skill sk = null;
            SpellLine sl = null;

            if (spellLineIndex < snap.Count)
            {
                int index = snap[spellLineIndex].Item2.FindIndex(s => s switch
                {
                    Spell spell => spell.Level == spellLevel,
                    Styles.Style style => style.SpecLevelRequirement == spellLevel,
                    Ability ability => ability.SpecLevelRequirement == spellLevel,
                    _ => false
                });
                if (index > -1) sk = snap[spellLineIndex].Item2[index];
                sl = snap[spellLineIndex].Item1;
            }

            switch (sk)
            {
                case Spell spell when sl != null:
                    player.castingComponent.RequestCastSpell(spell, sl);
                    break;
                case Styles.Style style:
                    player.ExecuteWeaponStyle(style);
                    break;
                case Ability ab:
                    SkillBase.GetAbilityActionHandler(ab.KeyName)?.Execute(ab, player);
                    ab.Execute(player);
                    break;
                default:
                    if (Log.IsWarnEnabled) Log.Warn($"Client <{client.Account.Name}> requested incorrect spell at level {spellLevel} in spell-line {sl?.Name ?? "unknown"}");
                    player.Out.SendMessage($"Error : Spell (Line {spellLineIndex}, Level {spellLevel}) can't be resolved...", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    break;
            }
        }
    }
}