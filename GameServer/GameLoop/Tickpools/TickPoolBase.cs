using System;

namespace DOL.GS
{
    public abstract class TickPoolBase
    {
        protected const int INITIAL_CAPACITY = 64;
        private const double TRIM_SAFETY_FACTOR = 2.5;
        private const int HALF_LIFE = 20_000;
        private const int TRIM_DELAY_MS = 5_000;

        private static readonly double _decayFactor;
        private static readonly int _trimDelayInTicks;

        protected int _used;
        protected int _logicalSize;
        private double _smoothedUsage;
        private int _trimCooldown;

        static TickPoolBase()
        {
            _decayFactor = Math.Exp(-Math.Log(2) / (GameLoop.TickDuration * HALF_LIFE / 1000));
            _trimDelayInTicks = (int)Math.Ceiling(TRIM_DELAY_MS / GameLoop.TickDuration);
        }

        public void Reset()
        {
            OnResetItems(_used);
            _smoothedUsage = Math.Max(_used, _smoothedUsage * _decayFactor + _used * (1 - _decayFactor));
            int newLogicalSize = (int)(_smoothedUsage * TRIM_SAFETY_FACTOR);

            if (newLogicalSize < INITIAL_CAPACITY)
                newLogicalSize = INITIAL_CAPACITY;

            if (_logicalSize > newLogicalSize)
            {
                _trimCooldown--;

                if (_trimCooldown <= 0)
                {
                    OnTrim(_logicalSize, newLogicalSize);
                    _logicalSize = newLogicalSize;
                    _trimCooldown = _trimDelayInTicks;
                }
            }
            else
                _trimCooldown = _trimDelayInTicks;

            _used = 0;
        }

        protected abstract void OnResetItems(int itemsInUse);
        protected abstract void OnTrim(int currentSize, int newSize);
    }
}