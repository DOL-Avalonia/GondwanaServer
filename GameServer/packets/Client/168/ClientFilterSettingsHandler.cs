using System;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.ClientFilterSettings, "Client chat and effect filter settings handler.", eClientStatus.PlayerInGame)]
    public class ClientFilterSettingsHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            int mask = 0;

            for (eChatType chatType = eChatType.CT_Spell; chatType <= eChatType.CT_SpellPulse; chatType++)
            {
                bool isEnabled = packet.ReadByte() == 0x00;
                if (!isEnabled)
                    mask |= 1 << (chatType - eChatType.CT_Spell);
            }

            client.DisabledChatTypeMask = mask;

            byte effectSetting = (byte)packet.ReadByte();
            if (Enum.IsDefined(typeof(eEffectFilter), effectSetting))
                client.EffectFilter = (eEffectFilter)effectSetting;
        }
    }
}