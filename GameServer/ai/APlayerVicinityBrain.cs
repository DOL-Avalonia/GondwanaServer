using System.Reflection;
using System.Text;
using System.Threading;
using DOL.GS;
using DOL.GS.Geometry;
using log4net;
using System;

namespace DOL.AI
{
    /// <summary>
    /// <p>This class is the base brain of all npc's that only stay active when players are close</p>
    /// <p>This class defines the base for a brain that activates itself when players get close to
    /// it's body and becomes dormat again after a certain amount of time when no players are close
    /// to it's body anymore.</p>
    /// <p>Useful to save CPU for MANY mobs that have no players in range, they will stay dormant.</p>
    /// </summary>
    public abstract class APlayerVicinityBrain : ABrain
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// constructor of this brain
        /// </summary>
        public APlayerVicinityBrain() : base()
        {
        }

        /// <summary>
        /// Returns the string representation of the APlayerVicinityBrain
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString() + ", noPlayersStopCountdown=" + noPlayersStopCountdown.ToString() + " resetTimer=" + m_resetTimer?.TimeUntilElapsed;
        }

        /// <summary>
        /// The number of ticks this brain stays active while no player
        /// is in the vicinity.
        /// </summary>
        protected int noPlayersStopCountdown;

        /// <summary>
        /// The number of milliseconds this brain will stay active even when no player is close
        /// This abstract class always returns 45 Seconds
        /// </summary>
        protected virtual int NoPlayersStopDelay => 45000;

        private ECSGameTimer? m_resetTimer = null;

        protected virtual int NoPlayersResetDelay => NoPlayersStopDelay * 60;

        /// <summary>
        /// Starts the brain thinking and resets the inactivity countdown
        /// </summary>
        /// <returns>true if started</returns>
        public override bool Start()
        {
            if (!Body.IsVisibleToPlayers)
                return false;
            
            Interlocked.Exchange(ref m_resetTimer, null)?.Stop();
            Interlocked.Exchange(ref noPlayersStopCountdown, NoPlayersStopDelay/ThinkInterval);
            return base.Start();
        }

        /// <summary>
        /// Called whenever the brain should do some thinking.
        /// We check if there is at least one player around and nothing
        /// bad has happened. If so, we shutdown our brain.
        /// </summary>
        /// <param name="callingTimer"></param>
        protected override int BrainTimerCallback(ECSGameTimer callingTimer)
        {
            var prev = Interlocked.Decrement(ref noPlayersStopCountdown);
            if (prev <= 0)
            {
                // Stop the brain timer
                Stop();

                // Full reload on far from spawn & no players around at all
                if (NoPlayersResetDelay > 0 && Body != null && Body.SpawnPosition != Position.Nowhere)
                {
                    int dist = Body.MaxDistance;
                    if (dist == 0)
                        dist = (int)Math.Floor(Body.RoamingRange * 1.25);

                    dist += GameNPC.CONST_WALKTOTOLERANCE;
                    var position = Body.Position;
                    if (!position.Coordinate.IsWithinDistance(Body.SpawnPosition, dist))
                    {
                        var delay = NoPlayersResetDelay - NoPlayersStopDelay;
                        if (delay <= 0)
                        {
                            Body.Reload();
                        }
                        else
                        {
                            m_resetTimer = new ECSGameTimer(Body, _ =>
                            {
                                Body.Reload();
                                return 0;
                            }, NoPlayersResetDelay - NoPlayersStopDelay);
                        }
                    }
                }
                return 0;
            }
            return base.BrainTimerCallback(callingTimer);
        }
    }
}
