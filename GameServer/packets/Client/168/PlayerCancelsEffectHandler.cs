using DOL.GS.Effects;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerCancelsEffect, "Handle Player Effect Cancel Request.", eClientStatus.PlayerInGame)]
    public class PlayerCancelsEffectHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            int effectId = packet.ReadShort();
            GamePlayer player = client.Player;
            if (player == null)
                return;

            bool matchSpellInternalId = client.Version > GameClient.eClientVersion.Version1109;
            IGameEffect found = null;

            lock (player.EffectList)
            {
                foreach (IGameEffect effect in player.EffectList)
                {
                    bool match = matchSpellInternalId
                        ? effect is GameSpellEffect spellEffect && spellEffect.Spell.InternalID == effectId
                        : effect.InternalID == effectId;

                    if (match)
                    {
                        found = effect;
                        break;
                    }
                }
            }

            found?.Cancel(true);
        }
    }
}