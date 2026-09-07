using System.Threading;

namespace DOL.GS
{
    public class ServiceObjectId
    {
        public const int UNSET_ID = -1;
        private readonly Lock _lock = new();
        private PendingAction _action = PendingAction.None;
        public int Value { get; private set; } = UNSET_ID;
        public ServiceObjectType Type { get; }
        public bool IsRegistered => Value != UNSET_ID;
        public bool IsRunning => Value >= 0;
        public bool IsDormant => IsRegistered && !IsRunning;
        public ServiceObjectId(ServiceObjectType type)
        {
            Type = type;
        }
        public bool TrySetAction(PendingAction action)
        {
            lock (_lock)
            {
                if (_action == action)
                    return false;
                _action = action;
                return true;
            }
        }
        public bool TryConsumeAction(PendingAction expectedAction)
        {
            lock (_lock)
            {
                if (_action != expectedAction)
                    return false;
                _action = PendingAction.None;
                return true;
            }
        }
        public PendingAction PeekAction()
        {
            lock (_lock) return _action;
        }
        public virtual void MoveTo(int index)
        {
            Value = index;
        }
        public void Unset()
        {
            lock (_lock) _action = PendingAction.None;
            MoveTo(UNSET_ID);
        }
        public enum PendingAction
        {
            None,
            Add,
            Schedule,
            Remove
        }
    }
}