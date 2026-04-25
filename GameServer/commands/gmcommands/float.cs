using System;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&float",
        ePrivLevel.GM,
        "Toggles smooth vampir flight mechanics.",
        "/float")]
    public class FloatCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client == null || client.Player == null)
                return;

            GamePlayer player = client.Player;

            bool isFloating = player.TempProperties.getProperty("GM_IsFloating", false);
            bool newState = !isFloating;

            player.TempProperties.setProperty("GM_IsFloating", newState);
            player.Out.SendVampireEffect(player, newState);

            foreach (GamePlayer p in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (p != null && p != player)
                {
                    p.Out.SendVampireEffect(player, newState);
                }
            }

            DisplayMessage(client, "Float mode is now " + (newState ? "ON" : "OFF"));
        }
    }
}