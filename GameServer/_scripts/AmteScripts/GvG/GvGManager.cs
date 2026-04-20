using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Territories;
using System;

namespace DOL.GS.Scripts
{
    public static class GvGManager
    {
        public static bool ForceOpen = false;
        public static event Action OnGvGStatusChanged;

        private static bool _isOpen;
        public static bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    if (_isOpen)
                        AmteScripts.Managers.TerritoryRelicManager.OnGvGOpened();

                    OnGvGStatusChanged?.Invoke();
                }
            }
        }

        // Timer to check the schedule every 60 seconds
        private static System.Timers.Timer _gvgTimer;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            DateTime parisTime = GetParisTime();
            _isOpen = (parisTime.Hour >= 10);
            _gvgTimer = new System.Timers.Timer(60000);
            _gvgTimer.Elapsed += (s, ev) => EvaluateSchedule();
            _gvgTimer.Start();
        }

        public static DateTime GetParisTime()
        {
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
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, parisZone);
        }

        public static void EvaluateSchedule()
        {
            if (ForceOpen)
            {
                IsOpen = true;
                return;
            }

            DateTime parisTime = GetParisTime();

            // GvG is active from 11:00 until Midnight (0:00 to 10:00 is restricted)
            bool shouldBeOpen = (parisTime.Hour >= 10);

            if (_isOpen == false && shouldBeOpen == true)
            {
                IsOpen = true;
            }
            else if (_isOpen == true && shouldBeOpen == false)
            {
                IsOpen = false;
            }
        }

        public static bool IsCaptureAllowed(Territory territory, GamePlayer attacker)
        {
            if (territory.Type == Territory.eType.Subterritory)
                return true;

            if (ForceOpen)
                return true;

            if (!IsOpen)
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