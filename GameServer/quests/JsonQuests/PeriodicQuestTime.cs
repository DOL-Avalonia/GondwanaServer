using System;
using DOL.Database;

namespace DOL.GS.Quests
{
    public static class PeriodicQuestTime
    {
        private static TimeZoneInfo _parisTz;

        public static TimeZoneInfo ParisTz
        {
            get
            {
                if (_parisTz != null) return _parisTz;

                try { _parisTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
                catch
                {
                    try { _parisTz = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
                    catch { _parisTz = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); }
                }
                return _parisTz;
            }
        }

        public static DateTime UtcNow => DateTime.UtcNow;

        public static DateTime GetNextDailyResetUtc(DateTime nowUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ParisTz);
            var nextLocalMidnight = local.Date.AddDays(1);
            return TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, ParisTz);
        }

        public static DateTime GetNextWeeklyResetUtc(DateTime nowUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ParisTz);
            var today = local.Date;

            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;

            var nextMondayLocal = today.AddDays(daysUntilMonday);
            return TimeZoneInfo.ConvertTimeToUtc(nextMondayLocal, ParisTz);
        }

        public static DateTime GetNextResetUtc(byte repeatInterval, DateTime nowUtc)
        {
            return (eQuestRepeatInterval)repeatInterval switch
            {
                eQuestRepeatInterval.Daily => GetNextDailyResetUtc(nowUtc),
                eQuestRepeatInterval.Weekly => GetNextWeeklyResetUtc(nowUtc),
                _ => DateTime.MaxValue
            };
        }
    }
}
