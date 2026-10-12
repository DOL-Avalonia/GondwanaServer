using System;

namespace DOL.GS
{
    [Obsolete("Legacy shim. Use new ECSGameTimer(source, t => ..., delay).")]
    public class RegionTimerAction<T> : ECSGameTimer where T : GameObject
    {
        private readonly T m_source;
        public Func<T, int> OnTickEvent { get; set; }

        public RegionTimerAction(T source) : base(source)
        {
            m_source = source ?? throw new ArgumentNullException(nameof(source));
            base.Callback = _ => OnTickEvent?.Invoke(m_source) ?? 0;
        }
        public RegionTimerAction(T source, Func<T, int> onTick) : this(source) { OnTickEvent = onTick; }
        public RegionTimerAction(T source, Action<T> onTick) : this(source) { OnTickEvent = s => { onTick(s); return 0; }; }
        public RegionTimerAction(T source, int delay, Action<T> onTick) : this(source, onTick) { Start(delay); }
        public RegionTimerAction(T source, int delay, Func<T, int> onTick) : this(source, onTick) { Start(delay); }
    }
}