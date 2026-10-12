using System;
using System.Collections;
using System.Reflection;
using System.Text;
using DOL.Events;
using DOL.GS;
using log4net;

namespace DOL.AI
{
    /// <summary>
    /// This class is the base of all arteficial intelligence in game objects
    /// </summary>
    public abstract class ABrain
    {
        private readonly object m_LockObject = new object(); // dummy object for locking - Mannen. // use this object for locking, instead of locking on 'this'

        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // /// <summary>
        // /// Action queue
        // /// </summary>
        // protected Stack m_actions = new Stack();

        /// <summary>
        /// The body of this brain
        /// </summary>
        protected GameNPC m_body;

        /// <summary>
        /// The timer used to check for player proximity
        /// </summary>
        private ECSGameTimer m_brainTimer;

        /// <summary>
        /// Constructs a new brain for a body
        /// </summary>
        public ABrain()
        {
        }

        /// <summary>
        /// Returns the string representation of the ABrain
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return new StringBuilder(128)
                .Append("body name='").Append(Body == null ? "(null)" : Body.Name)
                .Append("' (id=").Append(Body == null ? "(null)" : Body.ObjectID.ToString())
                .Append("), active=").Append(IsActive)
                .Append(", ThinkInterval=").Append(ThinkInterval)
                .ToString();
        }

        /// <summary>
        /// Gets/sets the body of this brain
        /// </summary>
        public GameNPC Body
        {
            get { return m_body; }
            set { SetBody(value); }
        }

        protected virtual void SetBody(GameNPC npc)
        {
            m_body = npc;
        }

        /// <summary>
        /// Picks a player able to answer a client LoS request between the body and <paramref name="target"/>:
        /// the target itself if it is a player, its owner, our own owner (pets), otherwise a random nearby player.
        /// Returns null when nobody can answer; callers then proceed without LoS (old behaviour).
        /// </summary>
        public virtual GamePlayer GetLosChecker(GameObject target)
        {
            GameNPC body = Body;
            if (body == null || target == null || target == body) return null;

            if (target is GamePlayer targetPlayer && LosCheckHandler.CanReplyToLosChecks(targetPlayer))
                return targetPlayer;

            if (target is GameLiving targetLiving && targetLiving.GetPlayerOwner() is GamePlayer targetOwner && LosCheckHandler.CanReplyToLosChecks(targetOwner))
                return targetOwner;

            if (this is Brain.IControlledBrain && body.GetPlayerOwner() is GamePlayer ourOwner && LosCheckHandler.CanReplyToLosChecks(ourOwner))
                return ourOwner;

            return LosCheckHandler.FindRandomLosChecker(body);
        }

        /// <summary>
        /// Returns weather this brain is active or not
        /// </summary>
        public virtual bool IsActive => m_brainTimer is { IsAlive: true };

        /// <summary>
        /// The interval at which the brain will fire, in milliseconds
        /// </summary>
        public virtual int ThinkInterval => 2500;

        /// <summary>
        /// How fast can this brain cast, in milliseconds
        /// </summary>
        public virtual int CastInterval
        {
            get { return ThinkInterval; }
            set { }
        }

        /// <summary>
        /// Starts the brain thinking
        /// </summary>
        /// <returns>true if started</returns>
        public virtual bool Start()
        {
            //Do not start brain if we are dead or inactive
            if (!m_body.IsAlive || m_body.ObjectState != GameObject.eObjectState.Active)
                return false;

            lock (m_LockObject)
            {
                if (IsActive) return false;
                m_brainTimer ??= new ECSGameTimer(m_body, BrainTimerCallback);
                m_brainTimer.Start(ThinkInterval);
            }
            return true;
        }

        /// <summary>
        /// Stops the brain thinking
        /// </summary>
        /// <returns>true if stopped</returns>
        public virtual bool Stop()
        {
            lock (m_LockObject)
            {
                if (!IsActive) return false;
                m_brainTimer.Stop();
                m_brainTimer = null;
            }
            return true;
        }

        /// <summary>
        /// Asks the brain to Think() on the very next game loop tick instead of waiting the full ThinkInterval.
        /// Called when a mob gets its first aggro entry (e.g. an asynchronous LOS answer).
        /// </summary>
        public virtual void WakeUp()
        {
            lock (m_LockObject)
            {
                if (m_brainTimer == null || !m_brainTimer.IsAlive)
                    return;

                if (m_brainTimer.TimeUntilElapsed <= 100)
                    return;

                m_brainTimer.Start(1);
            }
        }

        /// <summary>
        /// The callback timer for the brain ticks
        /// </summary>
        /// <param name="callingTimer">the calling timer</param>
        /// <returns>the new tick intervall</returns>
        protected virtual int BrainTimerCallback(ECSGameTimer callingTimer)
        {
            if (!m_body.IsAlive || m_body.ObjectState != GameObject.eObjectState.Active)
            {
                //Stop the brain for dead or inactive bodies
                Stop();
                return 0;
            }

            Think();
            GameEventMgr.Notify(GameNPCEvent.OnAICallback, m_body);
            return ThinkInterval;
        }

        /// <summary>
        /// Receives all messages of the body
        /// </summary>
        /// <param name="e">The event received</param>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments</param>
        public virtual void Notify(DOLEvent e, object sender, EventArgs args)
        {
        }

        /// <summary>
        /// This method is called whenever the brain does some thinking
        /// </summary>
        public abstract void Think();
    }
}
