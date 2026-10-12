using System;

using DOL.Events;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Language;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// GameDuel is an Helper Class for Player Duels
    /// </summary>
    public class GameDuel
    {
        private ECSGameTimer _distanceTimer;

        /// <summary>
        /// Duel Initiator
        /// </summary>
        public GamePlayer Starter { get; protected set; }

        /// <summary>
        /// Duel Target
        /// </summary>
        public GamePlayer Target
        {
            get;
            protected set;
        }

        /// <summary>
        /// Is Duel Started ?
        /// </summary>
        public bool Started { get { return m_started; } protected set { m_started = value; } }
        protected volatile bool m_started;

        internal const string REMATCH_KEY_PREFIX = "DuelRematchUntil:";

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="starter"></param>
        /// <param name="target"></param>
        public GameDuel(GamePlayer starter, GamePlayer target)
        {
            Starter = starter;
            Target = target;
            Started = false;
        }

        private static void ApplyPairCooldown(GamePlayer a, GamePlayer b)
        {
            int cool = ServerProperties.Properties.DUEL_REMATCH_COOLDOWN_SECONDS;
            if (cool <= 0) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long until = now + cool;
            
            // TODO: This is called twice...

            if (a != null)
            {
                lock (a.TempProperties)
                    a.TempProperties.setProperty(REMATCH_KEY_PREFIX + b.InternalID, until);
            }

            if (b != null)
            {
                lock (b.TempProperties)
                    b.TempProperties.setProperty(REMATCH_KEY_PREFIX + a.InternalID, until);
            }
        }

        /// <summary>
        /// Start Duel if is not running.
        /// </summary>
        public void Start()
        {
            if (Started) return;
            Started = true;

            Target.DuelStart(Starter);

            // Track both players
            Wire(Starter);
            Wire(Target);

            _distanceTimer = new ECSGameTimer(Starter, DistanceWatchdog, 3000);
        }

        public void Stop(GamePlayer? winner = null)
        {
            if (!Started) return;
            Started = false;

            // Snapshot and null Target AFTER we’re done with cleanup
            var a = Starter;
            var b = Target;

            // Clear duel state on target player first (keeps legacy behavior)
            b?.DuelStop(winner);

            // Cancel hostile effects both ways
            if (a != null && b != null)
            {
                foreach (GameSpellEffect effect in a.EffectList.GetAllOfType<GameSpellEffect>())
                    if (effect.SpellHandler.Caster == b && !effect.SpellHandler.HasPositiveEffect)
                        effect.Cancel(false);

                foreach (GameSpellEffect effect in b.EffectList.GetAllOfType<GameSpellEffect>())
                    if (effect.SpellHandler.Caster == a && !effect.SpellHandler.HasPositiveEffect)
                        effect.Cancel(false);
            }

            if (winner != null)
            {
                ApplyPairCooldown(Starter, Target);
            }

            Unwire(Starter);
            Unwire(Target);

            Starter.XPGainers.Clear();
            if (b != null) b.XPGainers.Clear();

            if (_distanceTimer != null)
            {
                _distanceTimer.Stop();
                _distanceTimer = null;
            }

            Target = null;
            Starter.Out.SendMessage(LanguageMgr.GetTranslation(Starter.Client, "GameObjects.GamePlayer.DuelStop.DuelEnds"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        protected virtual void Wire(GamePlayer p)
        {
            if (p == null) return;
            GameEventMgr.AddHandler(p, GamePlayerEvent.Quit, DuelOnPlayerQuit);
            GameEventMgr.AddHandler(p, GamePlayerEvent.Linkdeath, DuelOnPlayerQuit);
            GameEventMgr.AddHandler(p, GamePlayerEvent.RegionChanged, DuelOnPlayerQuit);
            GameEventMgr.AddHandler(p, GameLivingEvent.AttackedByEnemy, DuelOnAttack);
            GameEventMgr.AddHandler(p, GameLivingEvent.AttackFinished, DuelOnAttack);
        }

        protected virtual void Unwire(GamePlayer p)
        {
            if (p == null) return;
            GameEventMgr.RemoveHandler(p, GamePlayerEvent.Quit, DuelOnPlayerQuit);
            GameEventMgr.RemoveHandler(p, GamePlayerEvent.Linkdeath, DuelOnPlayerQuit);
            GameEventMgr.RemoveHandler(p, GamePlayerEvent.RegionChanged, DuelOnPlayerQuit);
            GameEventMgr.RemoveHandler(p, GameLivingEvent.AttackedByEnemy, DuelOnAttack);
            GameEventMgr.RemoveHandler(p, GameLivingEvent.AttackFinished, DuelOnAttack);
        }

        private int DistanceWatchdog(ECSGameTimer _)
        {
            try
            {
                if (!Started || Starter == null || Target == null) return 0;
                if (Starter.ObjectState != GameObject.eObjectState.Active || Target.ObjectState != GameObject.eObjectState.Active) { Stop(); return 0; }
                if (Starter.CurrentRegion != Target.CurrentRegion) { Stop(); return 0; }

                if (!Starter.IsWithinRadius(Target, WorldMgr.VISIBILITY_DISTANCE))
                {
                    Starter.Out.SendMessage(LanguageMgr.GetTranslation(Starter.Client, "Commands.Players.Duel.DistanceTooFar"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    Target.Out.SendMessage(LanguageMgr.GetTranslation(Target.Client, "Commands.Players.Duel.DistanceTooFar"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    Stop();
                    return 0;
                }
                return 3000;
            }
            catch { return 3000; }
        }

        /// <summary>
        /// Stops the duel if player attack or is attacked by anything other that duel target
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        protected virtual void DuelOnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            if (!Started || Starter == null || Target == null) return;

            // Who fired this event (attacker for AttackFinished, victim for AttackedByEnemy, but we use controller-owner anyway)
            var meLiving = sender as GameLiving;
            if (meLiving == null) return;
            meLiving = meLiving.GetController();

            var me = meLiving as GamePlayer;
            if (me == null) return;

            // The two duelists (controller/owners)
            var a = (GameLiving)Starter;
            var b = (GameLiving)Target;
            a = a.GetController();
            b = b.GetController();

            AttackData ad = null;
            GameLiving attacker = null;
            GameLiving victim = null;

            // Normalize attacker/victim for both event types
            if (arguments is AttackFinishedEventArgs afea)
            {
                ad = afea.AttackData;
                attacker = ad?.Attacker?.GetController();
                victim = ad?.Target?.GetController();
            }
            else if (arguments is AttackedByEnemyEventArgs abeea)
            {
                ad = abeea.AttackData;
                attacker = ad?.Attacker?.GetController();
                victim = meLiving; // this event is raised on the victim
            }

            if (ad == null || attacker == null || victim == null) return;

            // If either duelist is grouped (or joins one), cancel to avoid outside help
            if ((Starter.Group != null) || (Target.Group != null))
            {
                Stop();
                return;
            }

            bool attackerIsDuelist = ReferenceEquals(attacker, a) || ReferenceEquals(attacker, b);
            bool victimIsDuelist = ReferenceEquals(victim, a) || ReferenceEquals(victim, b);

            // Allow attacks ONLY if they are strictly between the two duelists
            if (attackerIsDuelist && victimIsDuelist)
            {
                // Legit duel hit in either direction -> keep going
                return;
            }

            // A duelist hit someone else OR a third party attacked a duelist -> cancel duel
            // If you want to require an actual landed hit to cancel, uncomment the next line:
            // if (!ad.IsHit) return;
            Stop();
        }

        /// <summary>
        /// Stops the duel on quit/link death
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        protected virtual void DuelOnPlayerQuit(DOLEvent e, object sender, EventArgs arguments)
        {
            Stop();
        }

    }
}
