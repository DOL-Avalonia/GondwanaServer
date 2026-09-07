using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using DOL.Events;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS.GameEvents
{
    public static class StatPrint
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static volatile Timer m_timer;

        private static long m_lastBytesIn;
        private static long m_lastBytesOut;
        private static long m_lastPacketsIn;
        private static long m_lastPacketsOut;
        private static long m_lastMeasureTick = DateTime.Now.Ticks;
        private static long m_lastPathToCalls;
        private static long m_lastPathToCalculateNextTargetCalls;

        private static long m_lastGen0;
        private static long m_lastGen1;
        private static long m_lastGen2;

        private static PerformanceCounter m_systemCpuUsedCounter;
        private static PerformanceCounter m_processCpuUsedCounter;
        private static PerformanceCounter m_memoryPages;
        private static PerformanceCounter m_physicalDisk;

        [GameServerStartedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            lock (typeof(StatPrint))
            {
                m_timer = new Timer(PrintStats, null, 10000, Timeout.Infinite);

                if (OperatingSystem.IsWindows())
                {
                    m_systemCpuUsedCounter ??= CreatePerformanceCounter("Processor", "% processor time", "_Total");
                    m_processCpuUsedCounter ??= CreatePerformanceCounter("Process", "% processor time", GetProcessCounterName());
                    m_memoryPages ??= CreatePerformanceCounter("Memory", "Pages/sec", null);
                    m_physicalDisk ??= CreatePerformanceCounter("PhysicalDisk", "Disk Transfers/sec", "_Total");
                }
            }
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            lock (typeof(StatPrint))
            {
                if (m_timer != null)
                {
                    m_timer.Change(Timeout.Infinite, Timeout.Infinite);
                    m_timer.Dispose();
                    m_timer = null;
                }

                if (OperatingSystem.IsWindows())
                {
                    ReleasePerformanceCounter(ref m_systemCpuUsedCounter);
                    ReleasePerformanceCounter(ref m_processCpuUsedCounter);
                    ReleasePerformanceCounter(ref m_memoryPages);
                    ReleasePerformanceCounter(ref m_physicalDisk);
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static string GetProcessCounterName()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                var perfCategory = new PerformanceCounterCategory("Process");
                var categoryData = perfCategory.ReadCategory();

                if (categoryData != null && categoryData.Contains("id process"))
                {
                    var idProcessInstances = categoryData["id process"];
                    if (idProcessInstances != null)
                    {
                        foreach (System.Collections.DictionaryEntry entry in idProcessInstances)
                        {
                            if (entry.Value is InstanceData data && data.RawValue == currentPid)
                                return entry.Key?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
            }

            return Process.GetCurrentProcess().ProcessName;
        }

        public static void PrintStats(object state)
        {
            try
            {
                if (!log.IsInfoEnabled)
                    return;

                var prevPriority = Thread.CurrentThread.Priority;
                Thread.CurrentThread.Priority = ThreadPriority.Lowest;

                long newTick = DateTime.Now.Ticks;
                long time = (newTick - m_lastMeasureTick) / 10000000L;
                m_lastMeasureTick = newTick;

                if (time < 1)
                    time = 1;

                // Network & Pathing rates
                long inRate = (Statistics.BytesIn - m_lastBytesIn) / time;
                long outRate = (Statistics.BytesOut - m_lastBytesOut) / time;
                long inPckRate = (Statistics.PacketsIn - m_lastPacketsIn) / time;
                long outPckRate = (Statistics.PacketsOut - m_lastPacketsOut) / time;
                long lastPathToCall = (Statistics.PathToCalls - m_lastPathToCalls) / time;
                long lastPathToCallNext = (Statistics.PathToCalculateNextTargetCalls - m_lastPathToCalculateNextTargetCalls) / time;

                m_lastBytesIn = Statistics.BytesIn;
                m_lastBytesOut = Statistics.BytesOut;
                m_lastPacketsIn = Statistics.PacketsIn;
                m_lastPacketsOut = Statistics.PacketsOut;
                m_lastPathToCalls = Statistics.PathToCalls;
                m_lastPathToCalculateNextTargetCalls = Statistics.PathToCalculateNextTargetCalls;

                // GC stats
                long gen0 = GC.CollectionCount(0);
                long gen1 = GC.CollectionCount(1);
                long gen2 = GC.CollectionCount(2);
                long dGen0 = gen0 - m_lastGen0;
                long dGen1 = gen1 - m_lastGen1;
                long dGen2 = gen2 - m_lastGen2;
                m_lastGen0 = gen0;
                m_lastGen1 = gen1;
                m_lastGen2 = gen2;

                // ThreadPool stats
                ThreadPool.GetAvailableThreads(out int poolCurrent, out int iocpCurrent);
                ThreadPool.GetMinThreads(out int poolMin, out int iocpMin);
                ThreadPool.GetMaxThreads(out int poolMax, out int iocpMax);

                var clients = WorldMgr.GetAllClients();
                StringBuilder stats = new StringBuilder(512);

                stats.Append("-stats- Mem=").Append(GC.GetTotalMemory(false) / 1024 / 1024).Append("MB")
                     .Append($" GC(0/1/2)=+{dGen0}/+{dGen1}/+{dGen2}")
                     .Append(" Clients=").Append(WorldMgr.GetAllClientsCount())
                     .Append(" Players=").Append(clients.Count(c => c.IsPlaying && c.Player?.ObjectState == GameObject.eObjectState.Active));

                // GameLoop TPS stats
                try
                {
                    var tps = GameLoop.GetAverageTps();
                    if (tps != null && tps.Count > 0)
                    {
                        foreach (var (interval, avg) in tps)
                            stats.Append($" TPS({interval / 1000}s)={avg:0.0}");
                    }
                }
                catch
                {
                }

                stats.AppendFormat(" Path(c/s)={0}/{1}", lastPathToCall, lastPathToCallNext)
                     .Append(" Down=").Append(inRate / 1024).Append("kb/s (").Append(Statistics.BytesIn / 1024 / 1024).Append("MB)")
                     .Append(" Up=").Append(outRate / 1024).Append("kb/s (").Append(Statistics.BytesOut / 1024 / 1024).Append("MB)")
                     .Append(" In=").Append(inPckRate).Append("p/s")
                     .Append(" Out=").Append(outPckRate).Append("p/s")
                     .AppendFormat(" Pool={0}/{1}", poolMax - poolCurrent, poolMax)
                     .AppendFormat(" IOCP={0}/{1}", iocpMax - iocpCurrent, iocpMax)
                     .AppendFormat(" GH/OH={0}/{1}", GameEventMgr.NumGlobalHandlers, GameEventMgr.NumObjectHandlers);

                if (OperatingSystem.IsWindows())
                {
                    if (m_systemCpuUsedCounter != null)
                        stats.Append(" CPU=").Append(m_systemCpuUsedCounter.NextValue().ToString("0.0")).Append('%');
                    if (m_processCpuUsedCounter != null)
                        stats.Append(" Process=").Append(m_processCpuUsedCounter.NextValue().ToString("0.0")).Append('%');
                    if (m_memoryPages != null)
                        stats.Append(" pg/s=").Append(m_memoryPages.NextValue().ToString("0.0"));
                    if (m_physicalDisk != null)
                        stats.Append(" dsk/s=").Append(m_physicalDisk.NextValue().ToString("0.0"));
                }

                log.Info(stats.ToString());
                Thread.CurrentThread.Priority = prevPriority;
            }
            catch (Exception e)
            {
                log.Error("StatPrint callback failed", e);
            }
            finally
            {
                lock (typeof(StatPrint))
                {
                    if (m_timer != null)
                    {
                        int freq = ServerProperties.Properties.STATPRINT_FREQUENCY;
                        m_timer.Change(freq <= 0 ? 10000 : freq, Timeout.Infinite);
                    }
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static PerformanceCounter CreatePerformanceCounter(string categoryName, string counterName, string instanceName)
        {
            try
            {
                var counter = string.IsNullOrEmpty(instanceName)
                    ? new PerformanceCounter(categoryName, counterName)
                    : new PerformanceCounter(categoryName, counterName, instanceName);
                counter.NextValue();
                return counter;
            }
            catch (Exception ex)
            {
                if (log.IsWarnEnabled)
                    log.Warn($"Performance counter '{categoryName}/{counterName}' disabled: {ex.Message}");
                return null;
            }
        }

        [SupportedOSPlatform("windows")]
        private static void ReleasePerformanceCounter(ref PerformanceCounter performanceCounter)
        {
            if (performanceCounter != null)
            {
                try
                {
                    performanceCounter.Close();
                    performanceCounter.Dispose();
                }
                catch
                {
                }
                performanceCounter = null;
            }
        }
    }
}