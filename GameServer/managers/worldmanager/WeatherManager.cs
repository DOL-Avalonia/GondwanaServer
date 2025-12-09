/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
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
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Sync Lock Object
        /// </summary>
        private readonly object LockObject = new object();

        /// <summary>
        /// Server Scheduler Reference
        /// </summary>
        private SimpleScheduler Scheduler { get; set; }

        /// <summary>
        /// Default Weather Check Timer Interval
        /// </summary>
        private int DefaultTimerInterval { get { return Math.Max(1000, ServerProperties.Properties.WEATHER_CHECK_INTERVAL); } }

        /// <summary>
        /// Default Weather Chance
        /// </summary>
        private int DefaultWeatherChance { get { return Math.Min(99, ServerProperties.Properties.WEATHER_CHANCE); } }

        /// <summary>
        /// Log Weather Change to Info Logger
        /// </summary>
        private bool EventLogWeather { get { return ServerProperties.Properties.WEATHER_LOG_EVENTS; } }

        /// <summary>
        /// Dictionary of Regions to be handled.
        /// </summary>
        private Dictionary<ushort, RegionWeather> RegionsWeather { get; set; }

        /// <summary>
        /// Dictionary of Region's Tasks.
        /// </summary>
        private Dictionary<ushort, ScheduledTask> RegionsTasks { get; set; }

        /// <summary>
        /// Retrieve Region Weather from Region ID
        /// </summary>
        public RegionWeather this[ushort regionId]
        {
            get
            {
                RegionWeather weather;
                lock (LockObject)
                    RegionsWeather.TryGetValue(regionId, out weather);

                return weather;
            }
        }

        /// <summary>
        /// Create a new Instance of <see cref="WeatherManager"/>
        /// </summary>
        public WeatherManager(SimpleScheduler Scheduler)
        {
            this.Scheduler = Scheduler;
            RegionsWeather = new Dictionary<ushort, RegionWeather>();
            RegionsTasks = new Dictionary<ushort, ScheduledTask>();

            GameEventMgr.AddHandler(RegionEvent.RegionStart, OnRegionStart);
            GameEventMgr.AddHandler(RegionEvent.RegionStop, OnRegionStop);
            GameEventMgr.AddHandler(RegionEvent.PlayerEnter, OnPlayerEnter);
        }

        /// <summary>
        /// Start a Random Weather for Region
        /// </summary>
        /// <param name="regionId">Region ID where weather must be changed</param>
        /// <returns>True if weather was changed</returns>
        public bool StartWeather(ushort regionId)
        {
            return ChangeWeather(regionId, weather => weather.CreateWeather(SimpleScheduler.Ticks));
        }

        /// <summary>
        /// Start a Parametrized Weather for Region
        /// </summary>
        /// <param name="regionId">Region ID where weather must be changed</param>
        /// <param name="position">Weather Position</param>
        /// <param name="width">Weather Width</param>
        /// <param name="speed">Weather Speed</param>
        /// <param name="diffusion">Weather Diffusion</param>
        /// <param name="intensity">Weather Intensity</param>
        /// <returns>True if weather was changed</returns>
        public bool StartWeather(ushort regionId, uint position, uint width, ushort speed, ushort diffusion, ushort intensity)
        {
            return ChangeWeather(regionId, weather => weather.CreateWeather(position, width, speed, intensity, diffusion, SimpleScheduler.Ticks));
        }

        public bool StartWeatherAtPlayer(GamePlayer player, WeatherProfile profile)
        {
            if (player == null || player.CurrentRegion == null)
                return false;

            ushort regionId = player.CurrentRegion.ID;
            uint position = (uint)player.Position.X;

            return StartWeather(regionId, position, profile.Width, profile.Speed, profile.FogDiffusion, profile.Intensity);
        }

        /// <summary>
        /// Restart Weather for Region
        /// </summary>
        /// <param name="regionId">Region ID where weather must be restarted</param>
        /// <returns>True if weather was restarted</returns>
        public bool RestartWeather(ushort regionId)
        {
            return ChangeWeather(regionId, weather => weather.CreateWeather(weather.Position, weather.Width, weather.Speed, weather.Intensity, weather.FogDiffusion, SimpleScheduler.Ticks));
        }

        /// <summary>
        /// Stop Weather for Region
        /// </summary>
        /// <param name="regionId">Region ID where weather must be stopped</param>
        /// <returns>True if weather was stopped</returns>
        public bool StopWeather(ushort regionId)
        {
            return ChangeWeather(regionId, StopWeather);
        }

        /// <summary>
        /// Change Current Weather in Region
        /// </summary>
        /// <param name="regionId">Region ID where weather is changed</param>
        /// <param name="change">Weather Object to change</param>
        /// <returns>true if Weather changed</returns>
        public bool ChangeWeather(ushort regionId, Action<RegionWeather> change)
        {
            ScheduledTask task;
            lock (LockObject)
            {
                if (RegionsTasks.TryGetValue(regionId, out task))
                    RegionsTasks.Remove(regionId);
            }

            // Stopping Timer is locking on Task Thread
            if (task != null)
                task.Stop();

            lock (LockObject)
            {
                RegionWeather weather;
                if (!RegionsWeather.TryGetValue(regionId, out weather))
                    return false;

                if (RegionsTasks.ContainsKey(regionId))
                    return false;

                try
                {
                    change(weather);
                }
                catch (Exception ex)
                {
                    if (log.IsErrorEnabled)
                        log.Error("Exception While Changing Weather: ", ex);
                }

                // scope copy for thread safety
                var region = regionId;

                if (weather.StartTime != 0)
                {
                    StartWeather(weather);
                    RegionsTasks.Add(region, Scheduler.Start(() => OnWeatherTick(region), weather.Duration));
                }
                else
                {
                    RegionsTasks.Add(region, Scheduler.Start(() => OnWeatherTick(region), DefaultTimerInterval));
                }
            }

            return true;
        }

        public bool StartWeatherProfile(ushort regionId, WeatherProfile profile)
        {
            return ChangeWeather(regionId, weather => weather.CreateWeather(profile.Width, profile.Speed, profile.Intensity, profile.FogDiffusion, SimpleScheduler.Ticks));
        }
        #region Update Handlers
        /// <summary>
        /// Stop Weather from given Weather Object
        /// </summary>
        /// <param name="weather">Weather to Stop</param>
        private void StopWeather(RegionWeather weather)
        {
            var weatherCurrentPosition = weather.CurrentPosition(SimpleScheduler.Ticks);
            if (EventLogWeather && log.IsInfoEnabled)
                log.InfoFormat("Weather Stopped in Region {0} (ID {1}) CurrentPosition : {2}\n{3}", weather.Region.Description, weather.Region.ID, weatherCurrentPosition, weather);

            weather.Clear();

            foreach (var player in weather.Region.Objects.OfType<GamePlayer>())
            {
                SendWeatherUpdate(weather, player);

                if (player.Position.X > weatherCurrentPosition - weather.Width && player.Position.X < weatherCurrentPosition)
                    player.Out.SendMessage("The sky clears up again as the storm clouds disperse!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// Start Weather according given Weather Object
        /// </summary>
        /// <param name="weather">Weather to Start</param>
        private void StartWeather(RegionWeather weather)
        {
            if (EventLogWeather && log.IsInfoEnabled)
                log.InfoFormat("Weather Started in Region {0} (ID {1})\n{2}", weather.Region.Description, weather.Region.ID, weather);

            foreach (var player in weather.Region.Objects.OfType<GamePlayer>())
                SendWeatherUpdate(weather, player);
        }

        /// <summary>
        /// Send Weather Update for this Region's Player
        /// </summary>
        /// <param name="regionId">Region ID of Player</param>
        /// <param name="player">Player</param>
        private void SendWeatherUpdate(ushort regionId, GamePlayer player)
        {
            var current = this[regionId];
            if (current != null)
                SendWeatherUpdate(current, player);
        }

        /// <summary>
        /// Send Weather Update to Player
        /// </summary>
        /// <param name="weather">Weather to send</param>
        /// <param name="player">Player Targeted</param>
        private void SendWeatherUpdate(RegionWeather weather, GamePlayer player)
        {
            if (player == null || player.ObjectState != GameObject.eObjectState.Active)
                return;

            if (weather.StartTime == 0)
            {
                player.Out.SendWeather(0, 0, 0, 0, 0);
                return;
            }

            // Base values
            uint position = weather.CurrentPosition(SimpleScheduler.Ticks);
            uint width = weather.Width;
            ushort speed = weather.Speed;
            ushort diffusion = weather.FogDiffusion;
            ushort intensity = weather.Intensity;

            // Zone-aware adjustment
            var zone = player.CurrentRegion?.GetZone(player.Coordinate);
            if (zone != null)
            {
                switch (zone.ID)
                {
                    case 247:
                        intensity = (ushort)Math.Min(120, intensity + 20);
                        diffusion = (ushort)Math.Min(40000, diffusion + 4000);
                        break;

                    case 216:
                        diffusion = (ushort)Math.Min(40000, diffusion + 8000);
                        break;

                    case 4:
                        intensity = (ushort)Math.Max(0, intensity - 20);
                        break;
                }
            }

            player.Out.SendWeather(position, width, speed, diffusion, intensity);
        }
        #endregion

        #region Event Handlers

        /// <summary>
        /// Weather Tick happen when Default Timer is Off or When Weather is Finished.
        /// </summary>
        /// <param name="regionId">Region Id of the Weather</param>
        /// <returns>Delay Time for next Tick</returns>
        private int OnWeatherTick(ushort regionId)
        {
            try
            {
                var weather = this[regionId];

                if (weather == null)
                    return 0;

                if (!Util.Chance(DefaultWeatherChance))
                {
                    if (weather.StartTime != 0)
                        StopWeather(weather);

                    return DefaultTimerInterval;
                }

                weather.CreateWeather(SimpleScheduler.Ticks);
                StartWeather(weather);

                return weather.Duration;
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.Error("Exception in Weather Manager On Tick: ", ex);

                return DefaultTimerInterval;
            }
        }

        /// <summary>
        /// When Region Start, Register to WeatherManager
        /// </summary>
        private void OnRegionStart(DOLEvent e, object sender, EventArgs arguments)
        {
            var region = sender as Region;

            if (region != null && !region.IsDungeon)
                RegisterRegion(region);
        }

        /// <summary>
        /// When Region Stop, unRegister to WeatherManager
        /// </summary>
        private void OnRegionStop(DOLEvent e, object sender, EventArgs arguments)
        {
            var region = sender as Region;

            if (region != null && !region.IsDungeon)
                UnRegisterRegion(region);
        }

        /// <summary>
        /// When Player Enter Region, Refresh Current Weather
        /// </summary>
        private void OnPlayerEnter(DOLEvent e, object sender, EventArgs arguments)
        {
            var region = sender as Region;
            var args = arguments as RegionPlayerEventArgs;

            if (region != null && args != null)
                SendWeatherUpdate(region.ID, args.Player);
        }
        #endregion

        #region Registering
        /// <summary>
        /// Register a new Region to Weather Manager
        /// Should not be used Externally
        /// </summary>
        /// <param name="region"></param>
        public void RegisterRegion(Region region)
        {
            lock (LockObject)
            {
                if (!RegionsWeather.ContainsKey(region.ID))
                {
                    try
                    {
                        // scope copy for thread safety
                        var regionId = region.ID;

                        RegionsWeather.Add(regionId, new RegionWeather(region));
                        RegionsTasks.Add(regionId, Scheduler.Start(() => OnWeatherTick(regionId), 1));

                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("Error While Registering Region's Weather : {0} (ID:{1})\n{2}", region.Description, region.ID, ex);
                    }
                }
                else
                {
                    if (log.IsWarnEnabled)
                        log.WarnFormat("Trying to Add Region {0} (ID:{1}) to WeatherManager while already Registered!", region.Description, region.ID);
                }
            }
        }

        /// <summary>
        /// UnRegister a Stopped Region from Weather Manager
        /// Should not be used Externally
        /// </summary>
        /// <param name="region"></param>
        public void UnRegisterRegion(Region region)
        {
            ScheduledTask task;
            lock (LockObject)
            {
                if (RegionsTasks.TryGetValue(region.ID, out task))
                    RegionsTasks.Remove(region.ID);
            }

            // Stopping Timer is locking on Task Thread
            if (task != null)
                task.Stop();

            lock (LockObject)
            {
                RegionWeather weather;
                if (RegionsWeather.TryGetValue(region.ID, out weather))
                {
                    RegionsWeather.Remove(region.ID);

                    if (weather.StartTime != 0)
                        StopWeather(weather);
                }
                else
                {
                    if (log.IsWarnEnabled)
                        log.WarnFormat("Trying to Remove Region {0} (ID:{1}) from WeatherManager but was not registered!", region.Description, region.ID);
                }
            }
        }
        #endregion
    }
}
