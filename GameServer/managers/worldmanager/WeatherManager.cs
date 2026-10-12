using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Scheduler;

using log4net;

namespace DOL.GS
{
    public class WeatherProfile
    {
        public string Name { get; set; }

        public uint Width { get; set; }
        public ushort Speed { get; set; }
        public ushort Intensity { get; set; }
        public ushort FogDiffusion { get; set; }

        // Optional: restrict to certain zones inside the region.
        public HashSet<ushort> AllowedZones { get; set; } = new HashSet<ushort>();
    }
    public static class WeatherProfiles
    {
        public static readonly Dictionary<string, WeatherProfile> Profiles
            = new(StringComparer.OrdinalIgnoreCase)
            {
                ["storm_04"] = new WeatherProfile
                {
                    Name = "Storm 04",
                    Width = 50000,
                    Speed = 200,
                    Intensity = 110,
                    FogDiffusion = 20000,
                    //AllowedZones = { 216 }
                },
                ["storm_03"] = new WeatherProfile
                {
                    Name = "Storm 03",
                    Width = 60000,
                    Speed = 180,
                    Intensity = 140,
                    FogDiffusion = 34000,
                    //AllowedZones = { 247 }
                },
                ["storm_02"] = new WeatherProfile
                {
                    Name = "Storm 02",
                    Width = 80000,
                    Speed = 150,
                    Intensity = 120,
                    FogDiffusion = 26000,
                    //AllowedZones = { 247 }
                },
                ["storm_01"] = new WeatherProfile
                {
                    Name = "Storm 01",
                    Width = 60000,
                    Speed = 80,
                    Intensity = 40,
                    FogDiffusion = 18000
                },
            };
    }

    /// <summary>
    /// WeatherManager class handle current weather in compatible Regions.
    /// </summary>
    public sealed class WeatherManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private readonly object LockObject = new();
        private readonly Dictionary<ushort, RegionWeather> RegionsWeather = new();
        private readonly Dictionary<ushort, ECSGameTimer> RegionsTasks = new();

        private static long Now => Timing.MonotonicTime.NowMs;
        private int DefaultTimerInterval => Math.Max(1000, ServerProperties.Properties.WEATHER_CHECK_INTERVAL);
        private int DefaultWeatherChance => Math.Min(99, ServerProperties.Properties.WEATHER_CHANCE);
        private bool EventLogWeather => ServerProperties.Properties.WEATHER_LOG_EVENTS;

        public RegionWeather this[ushort regionId]
        {
            get { lock (LockObject) return RegionsWeather.TryGetValue(regionId, out RegionWeather w) ? w : null; }
        }

        public WeatherManager()
        {
            GameEventMgr.AddHandler(RegionEvent.RegionStart, OnRegionStart);
            GameEventMgr.AddHandler(RegionEvent.RegionStop, OnRegionStop);
            GameEventMgr.AddHandler(RegionEvent.PlayerEnter, OnPlayerEnter);
        }

        public bool StartWeather(ushort regionId) => ChangeWeather(regionId, w => w.CreateWeather(Now));
        public bool StartWeather(ushort regionId, uint position, uint width, ushort speed, ushort diffusion, ushort intensity)
            => ChangeWeather(regionId, w => w.CreateWeather(position, width, speed, intensity, diffusion, Now));
        public bool StartWeatherAtPlayer(GamePlayer player, WeatherProfile profile)
        {
            if (player?.CurrentRegion == null) return false;
            return StartWeather(player.CurrentRegion.ID, (uint)player.Position.X, profile.Width, profile.Speed, profile.FogDiffusion, profile.Intensity);
        }
        public bool RestartWeather(ushort regionId)
            => ChangeWeather(regionId, w => w.CreateWeather(w.Position, w.Width, w.Speed, w.Intensity, w.FogDiffusion, Now));
        public bool StopWeather(ushort regionId) => ChangeWeather(regionId, StopWeather);
        public bool StartWeatherProfile(ushort regionId, WeatherProfile profile)
            => ChangeWeather(regionId, w => w.CreateWeather(profile.Width, profile.Speed, profile.Intensity, profile.FogDiffusion, Now));

        public bool ChangeWeather(ushort regionId, Action<RegionWeather> change)
        {
            ECSGameTimer task;
            lock (LockObject)
                if (RegionsTasks.Remove(regionId, out task)) { }
            task?.Stop();

            lock (LockObject)
            {
                if (!RegionsWeather.TryGetValue(regionId, out RegionWeather weather)) return false;
                if (RegionsTasks.ContainsKey(regionId)) return false;

                try { change(weather); }
                catch (Exception ex) { if (log.IsErrorEnabled) log.Error("Exception While Changing Weather: ", ex); }

                ushort region = regionId;
                int delay = weather.StartTime != 0 ? weather.Duration : DefaultTimerInterval;
                if (weather.StartTime != 0) StartWeather(weather);
                RegionsTasks.Add(region, new ECSGameTimer(null, _ => OnWeatherTick(region), delay));
            }
            return true;
        }

        private int OnWeatherTick(ushort regionId)
        {
            try
            {
                RegionWeather weather = this[regionId];
                if (weather == null) return 0;

                if (!Util.Chance(DefaultWeatherChance))
                {
                    if (weather.StartTime != 0) StopWeather(weather);
                    return DefaultTimerInterval;
                }

                weather.CreateWeather(Now);
                StartWeather(weather);
                return weather.Duration;
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Exception in Weather Manager On Tick: ", ex);
                return DefaultTimerInterval;
            }
        }

        private void StopWeather(RegionWeather weather)
        {
            uint current = weather.CurrentPosition(Now);
            if (EventLogWeather && log.IsInfoEnabled)
                log.Info($"Weather Stopped in Region {weather.Region.Description} (ID {weather.Region.ID}) CurrentPosition : {current}\n{weather}");
            weather.Clear();
            foreach (GameClient client in WorldMgr.GetClientsOfRegion(weather.Region.ID))
            {
                GamePlayer player = client.Player;
                SendWeatherUpdate(weather, player);
                if (player.Position.X > current - weather.Width && player.Position.X < current)
                    player.Out.SendMessage("The sky clears up again as the storm clouds disperse!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        private void StartWeather(RegionWeather weather)
        {
            if (EventLogWeather && log.IsInfoEnabled)
                log.Info($"Weather Started in Region {weather.Region.Description} (ID {weather.Region.ID})\n{weather}");
            foreach (GameClient client in WorldMgr.GetClientsOfRegion(weather.Region.ID))
                SendWeatherUpdate(weather, client.Player);
        }

        private void SendWeatherUpdate(ushort regionId, GamePlayer player)
        {
            RegionWeather current = this[regionId];
            if (current != null) SendWeatherUpdate(current, player);
        }

        private void SendWeatherUpdate(RegionWeather weather, GamePlayer player)
        {
            if (player == null || player.ObjectState != GameObject.eObjectState.Active) return;
            if (weather.StartTime == 0) { player.Out.SendWeather(0, 0, 0, 0, 0); return; }

            uint position = weather.CurrentPosition(Now);
            ushort diffusion = weather.FogDiffusion, intensity = weather.Intensity;
            Zone zone = player.CurrentRegion?.GetZone(player.Coordinate);
            switch (zone?.ID)
            {
                case 247: intensity = (ushort)Math.Min(120, intensity + 20); diffusion = (ushort)Math.Min(40000, diffusion + 4000); break;
                case 216: diffusion = (ushort)Math.Min(40000, diffusion + 8000); break;
                case 4: intensity = (ushort)Math.Max(0, intensity - 20); break;
            }
            player.Out.SendWeather(position, weather.Width, weather.Speed, diffusion, intensity);
        }

        private void OnRegionStart(DOLEvent e, object sender, EventArgs a) { if (sender is Region r && !r.IsDungeon) RegisterRegion(r); }
        private void OnRegionStop(DOLEvent e, object sender, EventArgs a) { if (sender is Region r && !r.IsDungeon) UnRegisterRegion(r); }
        private void OnPlayerEnter(DOLEvent e, object sender, EventArgs a) { if (sender is Region r && a is RegionPlayerEventArgs args) SendWeatherUpdate(r.ID, args.Player); }

        public void RegisterRegion(Region region)
        {
            lock (LockObject)
            {
                if (RegionsWeather.ContainsKey(region.ID))
                {
                    if (log.IsWarnEnabled) log.Warn($"Trying to Add Region {region.Description} (ID:{region.ID}) to WeatherManager while already Registered!");
                    return;
                }
                if (!region.Zones.Any()) return;
                try
                {
                    ushort regionId = region.ID;
                    RegionsWeather.Add(regionId, new RegionWeather(region));
                    RegionsTasks.Add(regionId, new ECSGameTimer(null, _ => OnWeatherTick(regionId), 1));
                }
                catch (Exception ex)
                {
                    if (log.IsErrorEnabled) log.Error($"Error While Registering Region's Weather : {region.Description} (ID:{region.ID})", ex);
                }
            }
        }

        public void UnRegisterRegion(Region region)
        {
            ECSGameTimer task;
            lock (LockObject) RegionsTasks.Remove(region.ID, out task);
            task?.Stop();
            lock (LockObject)
            {
                if (RegionsWeather.Remove(region.ID, out RegionWeather weather))
                {
                    if (weather.StartTime != 0) StopWeather(weather);
                }
                else if (log.IsWarnEnabled)
                    log.Warn($"Trying to Remove Region {region.Description} (ID:{region.ID}) from WeatherManager but was not registered!");
            }
        }
    }
}
