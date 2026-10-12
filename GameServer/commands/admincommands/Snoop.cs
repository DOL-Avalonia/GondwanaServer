using System.Collections.Generic;
using System.Threading;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [Cmd("&snoop", ePrivLevel.Admin, "Commands.Admin.Snoop.Description", "Commands.Admin.Snoop.Usage")]
    public class SnoopCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            string lang = client.Account.Language;

            GamePlayer target = args.Length <= 1
                ? client.Player.TargetObject as GamePlayer
                : WorldMgr.GuessClientByPlayerNameAndRealm(args[1], eRealm.None, true, out _)?.Player;

            if (target == null)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(lang, "Commands.Admin.Snoop.InvalidTarget"));
                return;
            }
            if (target == client.Player)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(lang, "Commands.Admin.Snoop.CannotSnoopSelf"));
                return;
            }
            if (target.Client.Account.PrivLevel > client.Account.PrivLevel)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(lang, "Commands.Admin.Snoop.InsufficientPrivileges"));
                return;
            }

            bool enabled = SnoopManager.ToggleSnoop(client.Player, target);

            string responseMsg = enabled
                ? LanguageMgr.GetTranslation(lang, "Commands.Admin.Snoop.Started", target.Name)
                : LanguageMgr.GetTranslation(lang, "Commands.Admin.Snoop.Stopped", target.Name);

            DisplayMessage(client, responseMsg);
        }
    }

    public static class SnoopManager
    {
        private static readonly SnoopTimer _snoopTimer = new(null);
        private static readonly Dictionary<GamePlayer, List<GamePlayer>> _activeObservers = new();
        private static readonly Lock _lock = new();
        private static volatile int _observedCount; // lock-free fast path for every SendMessage

        public static void CheckAndBroadcast(GamePlayer target, string message, eChatType chatType, eChatLoc chatLoc)
        {
            if (_observedCount == 0 || target == null) return;

            GamePlayer[] observers;
            lock (_lock)
            {
                if (!_activeObservers.TryGetValue(target, out List<GamePlayer> list)) return;
                observers = list.ToArray();
            }

            foreach (GamePlayer observer in observers)
            {
                if (observer.ObjectState is not GameObject.eObjectState.Active) continue;

                string observerLang = observer.Client?.Account?.Language ?? "EN";
                string formattedMessage = LanguageMgr.GetTranslation(observerLang, "Commands.Admin.Snoop.MessageFormat", target.Name, message);

                observer.Out.SendRawMessage(formattedMessage, chatType, chatLoc);
            }
        }

        public static bool ToggleSnoop(GamePlayer observer, GamePlayer target)
        {
            lock (_lock)
            {
                if (!_activeObservers.TryGetValue(target, out List<GamePlayer> list))
                {
                    list = new();
                    _activeObservers[target] = list;
                }

                if (list.Remove(observer))
                {
                    if (list.Count == 0) _activeObservers.Remove(target);
                    _observedCount = _activeObservers.Count;
                    return false;
                }

                list.Add(observer);
                _observedCount = _activeObservers.Count;
                if (!_snoopTimer.IsAlive) _snoopTimer.Start(10000);
                return true;
            }
        }

        private static bool CheckOnlineStatus()
        {
            lock (_lock)
            {
                List<GamePlayer> toRemove = null;

                foreach (var pair in _activeObservers)
                {
                    if (pair.Key.ObjectState is GameObject.eObjectState.Deleted) { (toRemove ??= new()).Add(pair.Key); continue; }
                    pair.Value.RemoveAll(static o => o.ObjectState is GameObject.eObjectState.Deleted);
                    if (pair.Value.Count == 0) (toRemove ??= new()).Add(pair.Key);
                }

                if (toRemove != null)
                    foreach (GamePlayer p in toRemove) _activeObservers.Remove(p);

                _observedCount = _activeObservers.Count;
                return _observedCount > 0;
            }
        }

        private sealed class SnoopTimer : ECSGameTimerWrapperBase
        {
            public SnoopTimer(GameObject owner) : base(owner) { }
            protected override int OnTick(ECSGameTimer timer) => CheckOnlineStatus() ? 10000 : 0;
        }
    }
}