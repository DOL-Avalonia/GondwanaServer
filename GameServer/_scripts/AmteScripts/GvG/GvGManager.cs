using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Territories;

namespace DOL.GS.Scripts
{
    public static class GvGManager
    {
        public static bool ForceOpen = false;

        /// <summary>
        /// Checks if a territory can be captured based on GvG rules and Time.
        /// </summary>
        public static bool IsCaptureAllowed(Territory territory, GamePlayer attacker)
        {
            if (territory.Type == Territory.eType.Subterritory)
                return true;

            if (ForceOpen)
                return true;

            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo parisZone;
            try
            {
                parisZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            }
            catch
            {
                parisZone = TimeZoneInfo.CreateCustomTimeZone("ParisFallback", TimeSpan.FromHours(1), "Paris Fallback", "Paris Fallback");
            }

            DateTime parisTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, parisZone);

            if (parisTime.Hour >= 0 && parisTime.Hour < 11)
            {
                if (attacker != null)
                {
                    attacker.Out.SendMessage(LanguageMgr.GetTranslation(attacker.Client.Account.Language, "GvG.Manager.CaptureRestricted"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                return false;
            }

            return true;
        }
    }
}