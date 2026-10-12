using DOL.GS.Effects;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.RemoveConcentrationEffect, "Handles Concentration Effect Remove Request", eClientStatus.PlayerInGame)]
    public class RemoveConcentrationEffectHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            int index = packet.ReadByte();
            GamePlayer player = client.Player;
            if (player == null)
                return;

            IConcentrationEffect effect = null;

            lock (player.ConcentrationEffects)
            {
                if (index < player.ConcentrationEffects.Count)
                {
                    effect = player.ConcentrationEffects[index];
                    player.ConcentrationEffects.Remove(effect);
                }
            }

            effect?.Cancel(true);
        }
    }
}