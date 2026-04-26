#nullable enable

using Discord.Net;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using log4net;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmteScripts.PvP
{
    public class PredatorPair(PvPEntity predator, PvPEntity? prey = null)
    {
        private const long REPEAT_KILL_COOLDOWN_MS = 30 * 1000;
        
        private PvPEntity? m_prey = prey;
        private PredatorPair? m_huntedBy;

        public PvPEntity Predator => predator;

        public override string ToString()
        {
            return KeyValuePair.Create(Predator, Prey).ToString();
        }

        public record KillRecord(PvPEntity Killer, long Timestamp);

        public PvPEntity? Prey
        {
            get => m_prey;
            set
            {
                if (m_prey != value)
                {
                    m_killRecords.Clear();
                }
                m_prey = value;
            }
        }

        public bool IsPredator(GameObject? player) { return player != null && Predator.GetPlayers().Contains(player); }

        public bool RecordKill(PvPEntity killer, string victim)
        {
            var time = GameServer.Instance.TickCount;
            bool added = true;
            m_killRecords.AddOrUpdate(
                killer.InternalID,
                _ => [new KillRecord(killer, time)],
                (_, list) =>
                {
                    if (list.Count > 0 && (list.Last().Timestamp + REPEAT_KILL_COOLDOWN_MS) > time)
                    {
                        added = false;
                    }
                    else
                    {
                        list.Add(new(killer, time));
                    }
                    return list;
                }
            );
            return added;
        }

        public void NotifyNewPrey()
        {
            string? name = Prey?.Name;
            Task.Run(async () =>
            {
                IEnumerable<Task> centerTasks;
                IEnumerable<Task> messageTasks;
                if (name is null)
                {
                    centerTasks = Enumerable.Empty<Task>();
                    messageTasks = Predator.SendTranslation("PvP.Predator.PreyLost", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    centerTasks = Predator.SendTranslation("PvP.Predator.Prey", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow, name);
                    messageTasks = Predator.SendTranslation("PvP.Predator.PreyAssigned", eChatType.CT_System, eChatLoc.CL_SystemWindow, name);
                }
                await Task.WhenAll(centerTasks);
                await Task.WhenAll(messageTasks);
            });
        }

        private readonly ConcurrentDictionary<string, List<KillRecord>> m_killRecords = new();
    }

    public abstract class AbstractPredatorManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType!)!;

        private readonly object m_predatorsLock = new();
        private readonly List<PredatorPair> m_currentPredators = new();
        private readonly ReaderWriterDictionary<GamePlayer, PredatorPair> m_preyLookup = new();

        private readonly object m_queueLock = new();
        private readonly List<PvPEntity> m_queue = new();

        public void Start(IEnumerable<PvPEntity> players)
        {
            var pairs = AssignPairs(players, null);
            if (pairs.Count > 0)
            {
                var last = pairs.Last();
                var first = pairs.First();
                if (last.Predator.AssociatedGuild != first.Predator.AssociatedGuild || last.Predator.AssociatedGuild == null && first.Predator.AssociatedGuild == null)
                {
                    last.Prey = first.Predator;
                }
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Predator starting: " + string.Join(", ", pairs));
            }

            lock (m_predatorsLock)
            {
                m_preyLookup.Clear();
                m_currentPredators.Clear();
                m_currentPredators.AddRange(pairs);
                foreach (var pair in pairs)
                {
                    if (pair.Prey != null)
                    {
                        foreach (GamePlayer player in pair.Prey.GetPlayers())
                        {
                            if (!m_preyLookup.TryAdd(player, pair))
                            {
                                log.ErrorFormat("Predator {0} has a prey {1} that is already being hunted by {2} ; there is an error in the implementation. Removing prey from {0}", pair.Predator, player, m_preyLookup[player]);
                                pair.Prey = null;
                                continue;
                            }

                            GameEventMgr.AddHandler(player, GamePlayerEvent.Dying, PreyKilledHandler);
                        }
                    }
                    pair.NotifyNewPrey();
                }
            }
        }

        protected void PreyKilledHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is not DyingEventArgs args || sender is not GamePlayer victim)
                return;

            if (!m_preyLookup.TryGetValue(victim, out PredatorPair bounty))
            {
                log.ErrorFormat("Could not find predator for dying prey {0}", victim);
                return;
            }

            OnPreyKilled(victim, bounty, args);
        }

        protected virtual void OnPreyKilled(GamePlayer prey, PredatorPair predator, DyingEventArgs dyingArgs)
        {
            if (predator.IsPredator(dyingArgs.Killer))
            {
                var playerKiller = dyingArgs.Killer as GamePlayer;
                playerKiller.SendMessage("you killed your prey!");
                prey.SendMessage("you were killed by your predator!");
            }
            else
            {
                log.Info("not predator");
            }
        }

        public void Start()
        {
            Start(Dequeue());
        }

        public void Stop()
        {
            lock (m_queueLock)
            {
                lock (m_currentPredators)
                {
                    m_queue.Clear();
                    m_currentPredators.Clear();
                }
            }
        }

        protected List<PredatorPair> AssignPairs(IEnumerable<PvPEntity> entities, PvPEntity? prey)
        {
            // Here what we do, is we find a predator suited for `prey`
            // We remove the predator from the list of available predators
            // And the predator becomes prey
            // To do this we reverse the list as an optimization to keep adding to the end
            List<PredatorPair> pairs = new();
            if (entities is ICollection collection)
                pairs.Capacity = collection.Count;

            var allPredators = entities.Reverse()
                .GroupBy(e => (object?)e.AssociatedGuild ?? e, (g, e) => KeyValuePair.Create(g, new Queue<PvPEntity>(e)))
                .ToList();
            PvPEntity? nextPredator = null;

            bool GetNextPredator()
            {
                // Dequeue from the guild with most players first. This ensures we spread guilds as much as possible
                // and end up with the minimum amount of leftover players.
                // This has the side effect of moving some players from big guilds to the end of the queue
                // Which in PvP sessions, means that we change the priority order normally based on score...
                var bucket = allPredators
                    .Where(e => e.Key != prey && e.Key != prey?.AssociatedGuild)
                    .Select(e => e.Value)
                    .Cast<Queue<PvPEntity>?>() // https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.maxby?view=net-10.0#remarks
                    .MaxBy(e => e!.Count);

                if (bucket is not { Count: > 0 })
                    return false;

                nextPredator = bucket.Dequeue();
                return true;
            }

            while (GetNextPredator())
            {
                Debug.Assert(nextPredator != null);
                pairs.Add(new PredatorPair(nextPredator, prey));
                prey = nextPredator;
            }

            foreach (var bucket in allPredators.Select(e => e.Value))
            {
                while (bucket.TryDequeue(out PvPEntity? leftover))
                {
                    pairs.Add(new PredatorPair(leftover));
                }
            }

            pairs.Reverse();
            return pairs;
        }

        class Bucket(object? key)
        {
            public object? Key => key;

            public int SelectedCount
            {
                get;
                set;
            }
            
            public Queue<PredatorPair>? Predators { get; set; }
            public Queue<PvPEntity>? Preys { get; set; }
        }

        protected virtual List<PredatorPair> Recombobulate(ICollection<PredatorPair> preylessPredators, IEnumerable<PvPEntity> predatorlessPreys)
        {
            List<Bucket> buckets = new(preylessPredators.Count);
            Bucket GetOrCreateBucket(object key)
            {
                Bucket? b = buckets.Find(b => b.Key == key);
                if (b is null)
                {
                    b = new Bucket(key);
                    buckets.Add(b);
                }
                return b;
            }

            foreach (var group in preylessPredators.GroupBy(e => (object?)e.Predator.AssociatedGuild ?? e.Predator))
            {
                GetOrCreateBucket(group.Key).Predators = new Queue<PredatorPair>(group);
            }

            foreach (var group in predatorlessPreys.GroupBy(e => (object?)e.AssociatedGuild ?? e))
            {
                GetOrCreateBucket(group.Key).Preys = new Queue<PvPEntity>(group);
            }
            
            List<PredatorPair> ret = new(preylessPredators.Count);
            PredatorPair? pair = null;
            bool ComputeNext()
            {
                var predatorBucket = buckets.MaxBy(b => b.Predators?.Count ?? 0);
                if (predatorBucket is not { Predators.Count: > 0 })
                    return false;

                var predator = predatorBucket.Predators.Dequeue();
                ++predatorBucket.SelectedCount;
                var preyBucket = buckets
                    .Where(e => e.Key != predator.Predator && e.Key != predator.Predator.AssociatedGuild)
                    .Where(b => b.Preys is { Count: >0 })
                    .MaxBy(b => b.Preys!.Count - b.SelectedCount);

                if (preyBucket is null)
                    return false;

                pair = predator;
                pair.Prey = preyBucket.Preys!.Dequeue();
                return true;
            }

            while (ComputeNext())
            {
                ret.Add(pair!);
            }
            return ret;
        }

        protected virtual IEnumerable<PredatorPair> Stitch(int currentIndex, bool dequeue)
        {
            /*
            Debug.Assert(Monitor.IsEntered(m_predatorsLock));

            var current = m_currentPredators[currentIndex];
            if (dequeue)
            {
                var dequeued = Dequeue();
                PvPEntity? next = m_currentPredators.Count <= 1 ? null : m_currentPredators[(currentIndex + 1) % m_currentPredators.Count].Predator;
                var (assigned, leftover) = AssignPairs(dequeued, current, next);
            }
            else
            {

            }
            return m_currentPredators.Skip(currentIndex).Take(toInsertCount + 1);
            */
            return [];
        }

        protected virtual IEnumerable<PredatorPair> RemovePrey(int predatorIndex, bool dequeue)
        {
            var removingIndex = (predatorIndex + 1) % m_currentPredators.Count;
            m_currentPredators.RemoveAt(removingIndex);
            return Stitch(predatorIndex, dequeue);
        }

        protected virtual void OnAssignNewPreys(IEnumerable<PredatorPair> changes)
        {
            changes.Foreach(p => p.NotifyNewPrey());
        }

        protected static IEnumerable<KeyValuePair<string, Queue<PvPEntity>>> SortGuilds(IEnumerable<PvPEntity> entities)
        {
            Dictionary<string, Queue<PvPEntity>> queues = new();

            foreach (var entity in entities)
            {
                var key = entity.AssociatedGuild?.GuildID ?? string.Empty;
                if (!queues.TryGetValue(key, out var list))
                {
                    list = new([entity]);
                    queues[key] = list;
                }
                else
                {
                    list.Enqueue(entity);
                }
            }
            return queues;
        }

        protected List<PvPEntity> Dequeue()
        {
            List<PvPEntity> values = new(m_queue.Count);
            lock (m_queueLock)
            {
                values.AddRange(m_queue);
                m_queue.Clear();
            }
            return values;
        }

        public void Queue(PvPEntity entity)
        {
            lock (m_queueLock)
            {
                m_queue.Add(entity);
            }
        }

        public void Queue(IEnumerable<PvPEntity> entities)
        {
            lock (m_queueLock)
            {
                m_queue.AddRange(entities);
            }
        }

        public void Queue(GamePlayer player)
        {
            Queue(new PvPPlayerEntity(player));
        }

        protected bool Remove(PvPEntity entity)
        {
            IEnumerable<PredatorPair> changed = [];
            bool removed = false;
            lock (m_predatorsLock)
            {
                for (int i = 0; i < m_currentPredators.Count; ++i)
                {
                    PredatorPair current = m_currentPredators[i];
                    if (current.Prey == entity)
                    {
                        //changed = RemovePrey(i);
                        removed = true;
                    }
                }
            }
            OnAssignNewPreys(changed);
            return removed;
        }

        public virtual bool IsActive => m_currentPredators.Count > 0;
    }
}
