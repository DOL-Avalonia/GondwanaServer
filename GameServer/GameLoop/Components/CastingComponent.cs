using System;
using System.Collections.Generic;
using System.Threading;
using DOL.Database;
using DOL.GS.Spells;

namespace DOL.GS
{
    /// <summary>
    /// Gondwana casting scheduler. Cast requests are pooled and executed on the game loop
    /// by CastingService; a single "follow-up" slot replaces the old GamePlayer m_nextSpell fields.
    /// The existing SpellHandler / DelayedCastTimer pipeline is untouched.
    /// </summary>
    public class CastingComponent : IServiceObject
    {
        /// <summary>
        /// With spell-queue OFF, a spell requested in the last N ms of a cast is still accepted as follow-up.
        /// </summary>
        public const int NO_QUEUE_INPUT_BUFFER = 250;
        private const int MAX_PENDING_REQUESTS = 4;
        private const int MAX_POOL_SIZE = 8;

        private readonly Queue<CastSpellRequest> _requests = new();
        private readonly Stack<CastSpellRequest> _pool = new();
        private readonly Lock _lock = new();
        private CastSpellRequest _followUp;

        public GameLiving Owner { get; }
        public ServiceObjectId ServiceObjectId { get; } = new(ServiceObjectType.CastingComponent);

        public ISpellHandler SpellHandler => Owner.CurrentSpellHandler;
        public bool IsCasting => Owner.IsCasting;
        public bool HasFollowUp { get { lock (_lock) return _followUp != null; } }
        public bool HasPendingRequests { get { lock (_lock) return _requests.Count > 0; } }

        public CastingComponent(GameLiving owner) { Owner = owner ?? throw new ArgumentNullException(nameof(owner)); }

        #region requests (thread-safe entry point, e.g. from packet handlers)

        /// <summary>
        /// Post a cast request; it is executed on the next game loop tick via CastingService.
        /// </summary>
        public bool RequestCastSpell(Spell spell, SpellLine line, InventoryItem item = null, GameLiving target = null, ISpellCastingAbilityHandler ability = null)
        {
            if (spell == null || line == null) return false;
            if (Owner.ObjectState != GameObject.eObjectState.Active || !Owner.IsAlive) return false;

            lock (_lock)
            {
                if (_requests.Count >= MAX_PENDING_REQUESTS) return false; // anti-spam / anti-macro flood
                CastSpellRequest request = Rent();
                request.Init(spell, line, item, target ?? Owner.TargetObject as GameLiving, ability);
                _requests.Enqueue(request);
            }
            ServiceObjectStore.Add(this);
            return true;
        }

        public void Tick()
        {
            if (Owner.ObjectState != GameObject.eObjectState.Active) { Stop(); return; }

            while (true)
            {
                CastSpellRequest request;
                lock (_lock) { if (!_requests.TryDequeue(out request)) break; }
                try { Execute(request); }
                finally { lock (_lock) Return(request); }
            }

            if (!HasPendingRequests) ServiceObjectStore.Remove(this);
        }

        protected virtual void Execute(CastSpellRequest request)
        {
            if (!Owner.IsAlive) return;
            if (request.Ability != null) { Owner.CastSpell(request.Ability); return; }

            switch (Owner)
            {
                case GamePlayer player: player.CastSpell(request.Spell, request.SpellLine, request.Item); break;   // keeps chamber / primer / Occultist rules
                case GameNPC npc: npc.CastSpell(request.Spell, request.SpellLine, true); break;              // keeps NPC LoS pipeline
                default: Owner.CastSpell(request.Spell, request.SpellLine); break;
            }
        }

        #endregion

        #region follow-up slot (replaces m_nextSpell / m_nextSpellLine / m_nextSpellTarget)

        public void QueueFollowUp(Spell spell, SpellLine line, GameLiving target, InventoryItem item)
        {
            if (spell == null || line == null) return;
            lock (_lock)
            {
                _followUp ??= Rent();
                _followUp.Init(spell, line, item, target, null); // newest request wins (same as live)
            }
        }

        public bool TryTakeFollowUp(out Spell spell, out SpellLine line, out GameLiving target, out InventoryItem item)
        {
            lock (_lock)
            {
                if (_followUp == null) { spell = null; line = null; target = null; item = null; return false; }
                spell = _followUp.Spell; line = _followUp.SpellLine; target = _followUp.Target; item = _followUp.Item;
                Return(_followUp);
                _followUp = null;
                return true;
            }
        }

        public void ClearFollowUp() { lock (_lock) { if (_followUp != null) { Return(_followUp); _followUp = null; } } }

        /// <summary>
        /// True if the current cast ends within NO_QUEUE_INPUT_BUFFER ms
        /// (input buffering for players without /spellqueue).
        /// </summary>
        public bool IsCurrentCastEndingSoon()
        {
            return Owner.CurrentSpellHandler is SpellHandler handler && handler.IsCastEndingSoon(NO_QUEUE_INPUT_BUFFER);
        }

        /// <summary>
        /// Called on interrupt / death / zone change.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                while (_requests.TryDequeue(out CastSpellRequest r)) Return(r);
                if (_followUp != null) { Return(_followUp); _followUp = null; }
            }
        }

        public void Stop() { ClearAll(); ServiceObjectStore.Remove(this); }

        #endregion

        #region pooling

        private CastSpellRequest Rent() => _pool.Count > 0 ? _pool.Pop() : new CastSpellRequest();
        private void Return(CastSpellRequest request) { request.Reset(); if (_pool.Count < MAX_POOL_SIZE) _pool.Push(request); }

        public sealed class CastSpellRequest
        {
            public Spell Spell { get; private set; }
            public SpellLine SpellLine { get; private set; }
            public InventoryItem Item { get; private set; }
            public GameLiving Target { get; private set; }
            public ISpellCastingAbilityHandler Ability { get; private set; }

            public void Init(Spell spell, SpellLine line, InventoryItem item, GameLiving target, ISpellCastingAbilityHandler ability)
            { Spell = spell; SpellLine = line; Item = item; Target = target; Ability = ability; }

            public void Reset() { Spell = null; SpellLine = null; Item = null; Target = null; Ability = null; }
        }

        #endregion
    }
}