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
        public static bool DebugMode = false;
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
                    OnGvGStatusChanged?.Invoke();
                }
            }
        }

        // Timer to check the schedule
        private static System.Timers.Timer _gvgTimer;

        // Debug mode internal trackers
        private static DateTime _debugPhaseStartTime;
        private static bool _debugNextPhaseOpen = false;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            DateTime parisTime = GetParisTime();
            _isOpen = (parisTime.Hour >= 10);
            _gvgTimer = new System.Timers.Timer(20000);
            _gvgTimer.Elapsed += (s, ev) => EvaluateSchedule();
            _gvgTimer.Start();
        }

        public static void ToggleDebugMode()
        {
            DebugMode = !DebugMode;
            if (DebugMode)
            {
                _debugNextPhaseOpen = true;
                _debugPhaseStartTime = DateTime.UtcNow;
                _isOpen = false;
                EvaluateSchedule();
            }
            else
            {
                ForceOpen = false;
                EvaluateSchedule();
            }
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
            if (DebugMode)
            {
                DateTime now = DateTime.UtcNow;
                if (_isOpen)
                {
                    if ((now - _debugPhaseStartTime).TotalMinutes >= 5)
                    {
                        _isOpen = false;
                        _debugPhaseStartTime = now;
                        OnGvGStatusChanged?.Invoke();
                    }
                }
                else
                {
                    if (_debugNextPhaseOpen || (now - _debugPhaseStartTime).TotalMinutes >= 2)
                    {
                        _isOpen = true;
                        _debugNextPhaseOpen = false;
                        _debugPhaseStartTime = now;
                        OnGvGStatusChanged?.Invoke();
                        AmteScripts.Managers.TerritoryRelicManager.OnGvGOpened();
                    }
                }
                return;
            }

            if (ForceOpen)
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    OnGvGStatusChanged?.Invoke();
                }
                return;
            }

            DateTime parisTime = GetParisTime();

            // GvG is active from 10:00 until Midnight (0:00 to 10:00 is restricted)
            bool shouldBeOpen = (parisTime.Hour >= 10);

            if (_isOpen == false && shouldBeOpen == true)
            {
                _isOpen = true;
                OnGvGStatusChanged?.Invoke();
                AmteScripts.Managers.TerritoryRelicManager.OnGvGOpened();
            }
            else if (_isOpen == true && shouldBeOpen == false)
            {
                _isOpen = false;
                OnGvGStatusChanged?.Invoke();
            }
        }

        public static bool IsCaptureAllowed(Territory territory, GamePlayer attacker)
        {
            if (territory.Type == Territory.eType.Subterritory)
                return true;

            if (ForceOpen || DebugMode)
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