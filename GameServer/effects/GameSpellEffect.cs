using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using log4net;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace DOL.GS.Effects
{
    /// <summary>
    /// Spell Effect assists SpellHandler with duration spells
    /// </summary>
    public class GameSpellEffect : IGameEffect, IConcentrationEffect
    {
        #region private internal

        /// <summary>
        /// Lock object for thread access
        /// </summary>
        private readonly object m_LockObject = new object(); // dummy object for thread sync - Mannen

        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        #endregion

        #region Protected / Getters

        /// <summary>
        /// The spell handler of this effect
        /// </summary>
        protected ISpellHandler m_handler;
        /// <summary>
        /// associated Spell handler
        /// </summary>
        public ISpellHandler SpellHandler
        {
            get { return m_handler; }
            protected set { RestoredEffect = false; m_handler = value; }
        }

        /// <summary>
        /// The owner of this effect
        /// </summary>
        protected GameLiving m_owner;
        /// <summary>
        /// The living to that this effect is applied to
        /// </summary>
        public GameLiving Owner
        {
            get { return m_owner; }
            protected set { m_owner = value; }
        }

        /// <summary>
        /// The internal unique ID of this effect
        /// </summary>
        protected ushort m_id;
        /// <summary>
        /// unique id for identification in effect list
        /// </summary>
        public ushort InternalID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        /// <summary>
        /// The effect duration in milliseconds
        /// </summary>
        protected int m_duration;


        protected long m_startedTick = 0;
        
        /// <summary>
        /// Duration of the spell effect in milliseconds
        /// </summary>
        public int Duration
        {
            get { return m_duration; }
            protected set
            {
                lock (m_LockObject)
                {
                    if (m_duration != value)
                    {
                        m_duration = value;
                        StartDurationTimer();
                        UpdateEffect();
                    }
                }
            }
        }

        /// <summary>
        /// The effect frequency in milliseconds
        /// </summary>
        protected int m_pulseFreq;
        /// <summary>
        /// Effect frequency
        /// </summary>
        public int PulseFreq
        {
            get { return m_pulseFreq; }
            protected set { m_pulseFreq = value; }
        }

        /// <summary>
        /// The effectiveness of this effect
        /// </summary>
        protected double m_effectiveness;
        /// <summary>
        /// Effectiveness of the spell effect 0..1
        /// </summary>
        public double Effectiveness
        {
            get { return m_effectiveness; }
            protected set { m_effectiveness = value; }
        }

        protected bool m_disabled;

        public bool IsDisabled
        {
            get { return m_disabled; }
            protected set { m_disabled = value; }
        }

        /// <summary>
        /// The flag indicating that this effect has expired
        /// </summary>
        protected bool m_expired;

        public bool IsExpired
        {
            get { return m_expired; }
            protected set { m_expired = value; }
        }

        protected int m_immunityDuration = 0;

        public int ImmunityDuration
        {
            get { return m_immunityDuration; }
            protected set { m_immunityDuration = value; }
        }

        /// <summary>
        /// The timer for pulsing effects
        /// </summary>
        protected PulsingEffectTimer m_pulseTimer;

        protected ECSGameTimer m_effectTimer;

        public bool CancelledByPurge { get; set; } = false;

        #endregion

        #region public Getters

        /// <summary>
        /// Name of the effect
        /// </summary>
        public string Name
        {
            get
            {
                if (Spell != null)
                    return Spell.Name;

                return string.Empty;
            }
        }

        /// <summary>
        /// The name of the owner
        /// </summary>
        public string OwnerName
        {
            get
            {
                if (Owner != null)
                    return Owner.Name;

                return string.Empty;
            }
        }

        /// <summary>
        /// Amount of concentration used by effect
        /// </summary>
        public byte Concentration
        {
            get
            {
                if (Spell != null)
                    return Spell.Concentration;

                return 0;
            }
        }

        /// <summary>
        /// Icon to show on players Effects bar
        /// </summary>
        public ushort Icon
        {
            get
            {
                if (Spell != null)
                    return Spell.Icon;

                return 0;
            }
        }

        /// <summary>
        /// Remaining Effect duration in milliseconds
        /// </summary>
        public int RemainingTime
        {
            get
            {
                if (Duration == 0)
                    return 0;

                if (m_effectTimer != null && m_effectTimer.IsAlive)
                {
                    int timeLeft = m_effectTimer.TimeUntilElapsed;
                    return Math.Min(ushort.MaxValue * 1000, timeLeft);
                }

                return 0;
            }
        }

        public bool IsPermanent => Duration == 0;

        /// <summary>
        /// Spell thats used
        /// </summary>
        public Spell Spell
        {
            get
            {
                if (SpellHandler != null)
                    return SpellHandler.Spell;

                return null;
            }
        }

        /// <summary>
        /// True if effect is in immunity state
        /// </summary>
        public bool ImmunityState
        {
            get { return IsExpired && m_startedTick > 0; }
        }

        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new game spell effect
        /// </summary>
        /// <param name="handler">the spell handler</param>
        /// <param name="duration">the spell duration in milliseconds</param>
        /// <param name="pulseFreq">the pulse frequency in milliseconds</param>
        public GameSpellEffect(ISpellHandler handler, int duration, int pulseFreq) : this(handler, duration, pulseFreq, 1)
        {
        }

        /// <summary>
        /// Creates a new game spell effect
        /// </summary>
        /// <param name="handler">the spell handler</param>
        /// <param name="duration">the spell duration in milliseconds</param>
        /// <param name="pulseFreq">the pulse frequency in milliseconds</param>
        /// <param name="effectiveness">the effectiveness</param>
        public GameSpellEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness)
        {
            m_handler = handler;
            m_duration = duration;
            m_pulseFreq = pulseFreq;
            m_effectiveness = effectiveness;
            m_expired = true; // not started = expired
            m_disabled = true; // not enabled
        }
        #endregion

        #region effect enable/disable

        /// <summary>
        /// Enable Effect on Target Without Adding
        /// </summary>
        public virtual void EnableEffect()
        {
            // Check if need enabling
            bool canEnable = false;
            lock (m_LockObject)
            {
                if (IsDisabled && !IsExpired)
                {
                    canEnable = true;
                    IsDisabled = false;
                }
            }

            if (canEnable)
            {
                if (RestoredEffect)
                    SpellHandler.OnEffectRestored(this, RestoreVars);
                else
                    SpellHandler.OnEffectStart(this);

                UpdateEffect();
            }
        }

        /// <summary>
        /// Disable Effect on Target without Removing
        /// </summary>
        /// <param name="noMessages"></param>
        public virtual void DisableEffect(bool noMessages)
        {
            // Check if need disabling.
            bool canDisable = false;
            lock (m_LockObject)
            {
                if (!IsDisabled)
                {
                    canDisable = true;
                    IsDisabled = true;
                }
            }

            if (canDisable)
            {
                int immunityDuration = 0;
                if (RestoredEffect)
                    immunityDuration = SpellHandler.OnRestoredEffectExpires(this, RestoreVars, noMessages);
                else
                    immunityDuration = SpellHandler.OnEffectExpires(this, noMessages);

                UpdateEffect();

                // Save Immunity Duration returned.
                lock (m_LockObject)
                {
                    ImmunityDuration = immunityDuration;
                }
            }
        }

        /// <summary>
        /// Remove effect Completely from Owner
        /// </summary>
        /// <param name="noMessages"></param>
        protected virtual void RemoveEffect(bool noMessages)
        {
            lock (m_LockObject)
            {
                StopTimers();

                // Expire Effect
                IsExpired = true;

                // Remove concentration Effect from Caster List.
                if (Concentration > 0 && SpellHandler != null && SpellHandler.Caster != null && SpellHandler.Caster.ConcentrationEffects != null)
                    SpellHandler.Caster.ConcentrationEffects.Remove(this);

                // Remove effect from Owner list
                if (Owner != null && Owner.EffectList != null)
                    Owner.EffectList.Remove(this);
            }

            // Try disabling Effect
            DisableEffect(false);
        }

        /// <summary>
        /// Add Effect and Enable when First Starting on Target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="enable"></param>
        protected virtual void AddEffect(GameLiving target, bool enable)
        {
            bool commitChange = false;
            try
            {
                lock (m_LockObject)
                {
                    // already started ?
                    if (!IsExpired)
                    {
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("Tried to start non-expired effect ({0})).\n{1}", this, Environment.StackTrace);
                        return;
                    }

                    // already added ?
                    if (Owner == null)
                    {
                        //Enable Effect
                        Owner = target;
                        IsExpired = false;

                        try
                        {
                            Owner.EffectList.BeginChanges();
                            commitChange = true;
                        }
                        catch (Exception ex)
                        {
                            if (log.IsWarnEnabled)
                                log.WarnFormat("Effect ({0}) Could not Begin Change in living - {1} - Spell Effect List, {2}", this, Owner, ex);
                            commitChange = false;
                        }

                        // Insert into Owner Effect List
                        if (!Owner.EffectList.Add(this))
                        {
                            if (log.IsWarnEnabled)
                                log.WarnFormat("{0}: effect was not added to the effects list, not starting it either. (effect class:{1} spell type:{2} spell name:'{3}')", Owner.Name, GetType().FullName, Spell.SpellType, Name);
                            return;
                        }
                    }
                    else if (Owner != target)
                    {
                        if (log.IsErrorEnabled)
                            log.ErrorFormat("Tried to start an already owned effect ({0})).\n{1}", this, Environment.StackTrace);
                        return;
                    }
                    else
                    {
                        IsExpired = false;
                    }

                    // Add concentration Effect To Caster List.
                    if (Concentration > 0 && SpellHandler != null && SpellHandler.Caster != null && SpellHandler.Caster.ConcentrationEffects != null)
                        SpellHandler.Caster.ConcentrationEffects.Add(this);

                    StartTimers();
                }

                // Try Enabling Effect
                if (enable)
                    EnableEffect();

                SpellHandler!.OnEffectAdd(this);
            }
            finally
            {
                if (commitChange)
                    Owner.EffectList.CommitChanges();
            }

            // Start first pulse.
            PulseCallback();
        }

        /// <summary>
        /// Update Effect in Owner List
        /// </summary>
        protected virtual void UpdateEffect()
        {
            // Update effect in player display.
            if (Owner != null && Owner.EffectList != null)
            {
                Owner.EffectList.BeginChanges();
                try
                {
                    Owner.EffectList.OnEffectsChanged(this);
                }
                finally
                {
                    Owner.EffectList.CommitChanges();
                }
            }
        }
        #endregion

        #region effects methods

        /// <summary>
        /// Starts the effect
        /// </summary>
        /// <param name="target">the target</param>
        public virtual void Start(GameLiving target)
        {
            m_startedTick = GameLoop.GameLoopTime;
            AddEffect(target, true);
        }

        /// <summary>
        /// Starts the effect without enabling it.
        /// </summary>
        /// <param name="target">the target</param>
        public virtual void StartDisabled(GameLiving target)
        {
            AddEffect(target, false);
        }

        /// <summary>
        /// Cancels the effect
        /// </summary>
        /// <param name="playerCanceled">true if canceled by the player</param>
        /// <param name="force">true to also remove immunity states</param>
        public virtual bool Cancel(bool playerCanceled, bool force = false)
        {
            bool wasImmunity = false;
            lock (m_LockObject)
            {
                if (!force)
                {
                    // Player can't remove negative effect or Effect in Immunity State
                    if (playerCanceled && (SpellHandler is { CanBeRightClicked: false } || ImmunityState))
                    {
                        GamePlayer player = Owner as GamePlayer;
                        if (player != null)
                            player.Out.SendMessage(LanguageMgr.GetTranslation((Owner as GamePlayer)!.Client, "Effects.GameSpellEffect.CantRemoveEffect"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    // Can't Cancel Immunity Effect from Alive Living
                    if (ImmunityState && Owner is { IsAlive: true })
                        return false;
                }

                wasImmunity = ImmunityState;
                // Expire Effect
                IsExpired = true;
            }

            // Check if Immunity needed.
            Owner.EffectList.BeginChanges();
            try
            {
                DisableEffect(false);
                SpellHandler.OnEffectRemove(this, false);
                lock (m_LockObject)
                {
                    if (!wasImmunity && m_immunityDuration > 0)
                    {
                        m_startedTick = GameLoop.GameLoopTime;
                        m_duration = m_immunityDuration;
                        StartDurationTimer();
                        UpdateEffect();
                        return true;
                    }
                }

                RemoveEffect(false);
            }
            finally
            {
                Owner.EffectList.CommitChanges();
            }
            return true;
        }

        /// <summary>
        /// Overwrites the effect
        /// concentration based effects should never be overwritten
        /// </summary>
        /// <param name="effect">the new effect</param>
        /// <param name="enable">Start new Effect or not</param>
        protected virtual void ReplaceEffect(GameSpellEffect effect, bool enable)
        {
            if (Concentration > 0)
            {

                if (log.IsWarnEnabled)
                    log.WarnFormat("{0} is trying to overwrite {1},  which has concentration {2}", effect.Name, Name, Concentration);
                return;
            }

            // Prevent further change to this effect.
            lock (m_LockObject)
            {
                StopTimers();
                IsExpired = true;
            }

            DisableEffect(true);
            SpellHandler.OnEffectRemove(this, true);

            lock (m_LockObject)
            {
                if (!IsDisabled)
                {
                    if (log.IsWarnEnabled)
                        log.WarnFormat("{0} is trying to overwrite an enabled effect {1}", effect.Name, Name);
                    return;
                }

                SpellHandler = effect.SpellHandler;
                PulseFreq = effect.PulseFreq;
                Effectiveness = effect.Effectiveness;

                // Add concentration Effect To Caster List.
                if (Concentration > 0 && SpellHandler != null && SpellHandler.Caster != null && SpellHandler.Caster.ConcentrationEffects != null)
                    SpellHandler.Caster.ConcentrationEffects.Add(this);

                // Restart Effect
                IsExpired = false;
                m_startedTick = GameLoop.GameLoopTime;
                m_duration = effect.Duration;
                StartDurationTimer();
                UpdateEffect();
            }

            // Try Enabling Effect
            if (enable)
                EnableEffect();
            else
                UpdateEffect();

            SpellHandler!.OnEffectAdd(this);

            PulseCallback();
        }

        /// <summary>
        /// Overwrite existing Effect and Enable new One.
        /// </summary>
        /// <param name="effect"></param>
        public virtual void Overwrite(GameSpellEffect effect)
        {
            ReplaceEffect(effect, true);
        }

        /// <summary>
        /// Overwrite existing Effect without Starting.
        /// </summary>
        /// <param name="effect"></param>
        public virtual void OverwriteDisabled(GameSpellEffect effect)
        {
            ReplaceEffect(effect, false);
        }

        /// <summary>
        /// Starts the timers for this effect
        /// </summary>
        protected void StartTimers()
        {
            StopTimers();
            StartDurationTimer();
            StartPulseTimer();
        }

        protected virtual void StartDurationTimer()
        {
            // Duration => 0 = endless until explicit stop
            if (Duration == 0 || Spell.Concentration > 0)
                return;

            m_effectTimer?.Stop();
            long timeLeft = m_startedTick + Duration - GameLoop.GameLoopTime;
            if (m_startedTick != 0 && timeLeft > 0)
            {
                m_effectTimer ??= new ECSGameTimer(m_owner, ExpiredCallback);
                m_effectTimer.Start((int)timeLeft);
            }
        }

        protected virtual void StartPulseTimer()
        {
            if (PulseFreq > 0)
            {
                m_pulseTimer = new PulsingEffectTimer(this);
                m_pulseTimer.Interval = PulseFreq;
                m_pulseTimer.Start(PulseFreq);
            }
        }
        

        /// <summary>
        /// Stops the timers for this effect
        /// </summary>
        protected virtual void StopTimers()
        {
            m_pulseTimer?.Stop();
            m_pulseTimer = null;
            m_effectTimer?.Stop();
        }

        /// <summary>
        /// The callback method when the effect expires
        /// </summary>
        protected virtual int ExpiredCallback(ECSGameTimer timer)
        {
            bool removeEffect = false;
            lock (m_LockObject)
            {
                StopTimers();
                removeEffect = IsExpired;
            }

            if (removeEffect)
                RemoveEffect(false);
            else
                Cancel(false);
            return 0;
        }

        /// <summary>
        /// Pulse callback
        /// </summary>
        protected virtual void PulseCallback()
        {
            bool canPulse = false;
            lock (m_LockObject)
            {
                if (!IsDisabled && !IsExpired && PulseFreq > 0)
                    canPulse = true;
            }

            if (canPulse)
                SpellHandler.OnEffectPulse(this);
        }
        #endregion

        public int AddRemainingTime(int additionalDuration)
        {
            int actualAddedDuration = 0;

            lock (m_LockObject)
            {
                int maxDuration = ServerProperties.Properties.MAX_DAMNATION_DURATION * 1000;

                long currentTime = GameLoop.GameLoopTime;

                int elapsedTime = (int)(currentTime - m_startedTick);
                int remainingTime = Duration - elapsedTime;

                int proposedTotalDuration = Duration + additionalDuration;
                int cappedTotalDuration = maxDuration > 0 ? Math.Min(proposedTotalDuration, maxDuration) : proposedTotalDuration;
                actualAddedDuration = cappedTotalDuration - Duration;

                if (actualAddedDuration <= 0)
                {
                    actualAddedDuration = 0;
                }
                
                m_duration = cappedTotalDuration;
                StartDurationTimer();
                UpdateEffect();
            }

            return actualAddedDuration;
        }

        /// <summary>
        /// Delve information
        /// </summary>
        public virtual IList<string> DelveInfo
        {
            get
            {
                IList<string> list = m_handler.DelveInfo;

                int seconds = RemainingTime / 1000;
                if (seconds > 0)
                {
                    list.Add(" "); //empty line
                    if (seconds > 60)
                        list.Add(LanguageMgr.GetTranslation(((GamePlayer)Owner).Client, "Effects.DelveInfo.MinutesRemaining", (seconds / 60), (seconds % 60).ToString("00")));
                    else
                        list.Add(LanguageMgr.GetTranslation(((GamePlayer)Owner).Client, "Effects.DelveInfo.SecondsRemaining", seconds));
                }

                return list;
            }
        }

        public int[] RestoreVars = Array.Empty<int>();
        public bool RestoredEffect = false;

        public PlayerXEffect getSavedEffect()
        {
            if (this.RestoredEffect && this.RemainingTime > 5000)
            {
                PlayerXEffect eff = new PlayerXEffect();
                eff.Duration = this.RemainingTime;
                eff.IsHandler = true;
                eff.Var1 = this.RestoreVars[0];
                eff.Var2 = this.RestoreVars[1];
                eff.Var3 = this.RestoreVars[2];
                eff.Var4 = this.RestoreVars[3];
                eff.Var5 = this.RestoreVars[4];
                eff.Var6 = this.RestoreVars[5];
                eff.SpellLine = this.SpellHandler.SpellLine.KeyName;
                return eff;
            }
            if (m_handler != null)
            {
                PlayerXEffect eff = m_handler.GetSavedEffect(this);
                return eff;
            }
            return null;
        }

        /// <summary>
        /// Returns the string representation of the GameSpellEffect
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("Duration={0}, Owner.Name={1}, PulseFreq={2}, RemainingTime={3}, Effectiveness={4}, m_expired={5}\nSpellHandler info: {6}",
                                 Duration, Owner == null ? "(null)" : Owner.Name, PulseFreq, RemainingTime, Effectiveness, m_expired, SpellHandler == null ? "(null)" : SpellHandler.ToString());
        }

        /// <summary>
        /// Restart Timers after a Region Froze
        /// </summary>
        public virtual void RestartTimers()
        {
            lock (m_LockObject)
            {
                StartTimers();
            }

            UpdateEffect();
        }

        #region timer subclass		
        /// <summary>
        /// Handles effect pulses
        /// </summary>
        protected sealed class PulsingEffectTimer : ECSGameTimerWrapperBase
        {
            /// <summary>
            /// The pulsing effect
            /// </summary>
            private readonly GameSpellEffect m_effect;

            /// <summary>
            /// Get tick when effect Last Started.
            /// </summary>
            public long LastStartedTick
            {
                get;
                private set;
            }

            /// <summary>
            /// Constructs a new pulsing timer
            /// </summary>
            /// <param name="effect">The pulsing effect</param>
            public PulsingEffectTimer(GameSpellEffect effect) : base(effect.m_owner)
            {
                m_effect = effect ?? throw new ArgumentNullException(nameof(effect));
            }

            public void Change(int newInterval)
            {
                Interval = newInterval;
            }

            /// <summary>
            /// Starts the timer with defined initial delay
            /// </summary>
            /// <param name="initialDelay">The initial timer delay. Must be more than 0 and less than MaxInterval</param>
            public new void Start(int initialDelay)
            {
                base.Start(initialDelay);
                LastStartedTick = GameLoop.GameLoopTime;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            protected override int OnTick(ECSGameTimer timer)
            {
                {
                    if (m_effect.IsExpired)
                        return 0;

                    m_effect.PulseCallback();
                    return Interval;
                }
            }

            /// <summary>
            /// Returns short information about the timer
            /// </summary>
            /// <returns>Short info about the timer</returns>
            public override string ToString()
            {
                return string.Format(" effect: ({0})", m_effect);
            }
        }
        #endregion
    }
}