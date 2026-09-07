using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.GS.Effects;

namespace DOL.GS
{
    public sealed class TickListPoolManager
    {
        private static readonly FrozenDictionary<Type, PooledListKey> _typeToKeyMap = new Dictionary<Type, PooledListKey>
        {
            { typeof(GameClient), PooledListKey.Client },
            { typeof(GameLiving), PooledListKey.Living },
            { typeof(GamePlayer), PooledListKey.Player },
            { typeof(GameNPC), PooledListKey.Npc },
            { typeof(GameStaticItem), PooledListKey.Item },
            { typeof(House), PooledListKey.House },
            { typeof(IArea), PooledListKey.Area },
            // Gondwana-specific types you may pool later:
            // { typeof(GamePet), PooledListKey.Pet },
        }.ToFrozenDictionary();

        private readonly FrozenDictionary<PooledListKey, TickPoolBase> _pools = new Dictionary<PooledListKey, TickPoolBase>
        {
            { PooledListKey.Client, new TickListPool<GameClient>() },
            { PooledListKey.Living, new TickListPool<GameLiving>() },
            { PooledListKey.Player, new TickListPool<GamePlayer>() },
            { PooledListKey.Npc, new TickListPool<GameNPC>() },
            { PooledListKey.Item, new TickListPool<GameStaticItem>() },
            { PooledListKey.House, new TickListPool<House>() },
            { PooledListKey.Area, new TickListPool<IArea>() },
        }.ToFrozenDictionary();

        public List<T> GetForTick<T>() where T : IPooledList<T>
        {
            if (!_typeToKeyMap.TryGetValue(typeof(T), out PooledListKey key))
                throw new ArgumentException($"No pool is registered for lists of type '{typeof(T).Name}'.", nameof(T));

            if (_pools[key] is not TickListPool<T> typedPool)
                throw new InvalidCastException($"The pool for key '{key}' is not of the expected type '{typeof(T).Name}'.");

            return typedPool.GetForTick();
        }

        public void Reset()
        {
            foreach (var pair in _pools)
                pair.Value.Reset();
        }
    }

    public enum PooledListKey
    {
        Client, Living, Player, Npc, Item, House, Area
    }

    public interface IPooledList<T> { }
}
