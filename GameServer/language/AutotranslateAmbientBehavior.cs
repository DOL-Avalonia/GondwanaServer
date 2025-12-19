using DOL.GS;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    public partial class GameNPC
    {
        /// <summary>
        /// Simple auto-translate hook for ambient texts
        /// (MobXAmbientBehaviour.Text).
        /// </summary>
        partial void BeforeAmbientText(eAmbientTrigger trigger, GamePlayer player, MobXAmbientBehaviour behaviour, ref string text, ref bool handled)
        {
            if (player == null || string.IsNullOrWhiteSpace(text))
                return;

            text = string.Empty;  // AutoTranslateManager.MaybeTranslateServerText(player, text);
        }

        partial void BeforeSayTo(GamePlayer target, ref eChatLoc loc, ref string message, ref bool handled)
        {

        }

        partial void BeforeWhisperReceive(GamePlayer player, string text, ref bool handled)
        {

        }
    }
}