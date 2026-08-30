using System.Collections.Concurrent;
using System.Threading;
using DOL.Database;

namespace DOL.GS
{
    public static class ConsignmentStateManager
    {
        private static readonly ConcurrentDictionary<string, ConsignmentState> _states = new ConcurrentDictionary<string, ConsignmentState>();

        public static ConsignmentState GetState(string ownerId)
        {
            return string.IsNullOrEmpty(ownerId) ? null : _states.GetOrAdd(ownerId, id => new ConsignmentState(id));
        }

        public static void RemoveState(string ownerId) => _states.TryRemove(ownerId, out _);
    }

    public class ConsignmentState
    {
        private const int EXPIRES_AFTER = 60000;
        private readonly string _ownerId;
        private readonly object _lock = new object();
        private long _money;
        private bool _moneyLoaded;
        private bool _isDisposed;
        private Timer _cleanupTimer;

        public ConsignmentState(string ownerId)
        {
            _ownerId = ownerId;
            _cleanupTimer = new Timer(OnTick, null, EXPIRES_AFTER, Timeout.Infinite);
        }

        private void ExtendTimer()
        {
            if (!_isDisposed && _cleanupTimer != null)
                try { _cleanupTimer.Change(EXPIRES_AFTER, Timeout.Infinite); } catch { }
        }

        private void OnTick(object state)
        {
            lock (_lock)
            {
                if (_isDisposed) return;

                // Force a DB save when the cache expires to prevent DB Spam on active merchants
                if (_moneyLoaded)
                {
                    var houseCm = GameServer.Database.SelectObject<HouseConsignmentMerchant>(DB.Column("OwnerID").IsEqualTo(_ownerId));
                    if (houseCm != null) { houseCm.Money = _money; GameServer.Database.SaveObject(houseCm); }
                }

                _isDisposed = true;
                if (_cleanupTimer != null) { _cleanupTimer.Dispose(); _cleanupTimer = null; }
                ConsignmentStateManager.RemoveState(_ownerId);
            }
        }

        public long GetTotalMoney()
        {
            lock (_lock)
            {
                if (_isDisposed) return ConsignmentStateManager.GetState(_ownerId)?.GetTotalMoney() ?? 0;
                ExtendTimer();

                if (!_moneyLoaded)
                {
                    var houseCm = GameServer.Database.SelectObject<HouseConsignmentMerchant>(DB.Column("OwnerID").IsEqualTo(_ownerId));
                    if (houseCm != null) _money = houseCm.Money;
                    _moneyLoaded = true;
                }
                return _money;
            }
        }

        public void SetTotalMoney(long amount)
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    ConsignmentStateManager.GetState(_ownerId)?.SetTotalMoney(amount);
                    return;
                }

                ExtendTimer();
                _money = amount;
                _moneyLoaded = true;
            }
        }
    }
}