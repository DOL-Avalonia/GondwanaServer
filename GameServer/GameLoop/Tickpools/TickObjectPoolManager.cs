using DOL.GS.PacketHandler;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace DOL.GS
{
    public sealed class TickObjectPoolManager
    {
        private static readonly FrozenDictionary<Type, PooledObjectKey> _typeToKeyMap =
            new Dictionary<Type, PooledObjectKey>
            {
                { typeof(GSTCPPacketOut), PooledObjectKey.TcpOutPacket },
                { typeof(GSUDPPacketOut), PooledObjectKey.UdpOutPacket },
            }.ToFrozenDictionary();

        private readonly FrozenDictionary<PooledObjectKey, TickPoolBase> _pools =
            new Dictionary<PooledObjectKey, TickPoolBase>
            {
                { PooledObjectKey.TcpOutPacket, new TickObjectPool<GSTCPPacketOut>() },
                { PooledObjectKey.UdpOutPacket, new TickObjectPool<GSUDPPacketOut>() },
            }.ToFrozenDictionary();

        public T GetForTick<T>() where T : IPooledObject<T>, new()
        {
            if (!_typeToKeyMap.TryGetValue(typeof(T), out PooledObjectKey key))
                throw new ArgumentException($"No pool is registered for objects of type '{typeof(T).Name}'.", nameof(T));

            if (_pools[key] is not TickObjectPool<T> typedPool)
                throw new InvalidCastException($"The pool for key '{key}' is not of the expected type '{typeof(T).Name}'.");

            return typedPool.GetForTick();
        }

        public void Reset()
        {
            foreach (var pair in _pools)
                pair.Value.Reset();
        }
    }

    public enum PooledObjectKey
    {
        InPacket,
        TcpOutPacket,
        UdpOutPacket
    }

    public interface IPooledObject<T>
    {
        long IssuedTimestamp { get; set; }
    }

    public static class PooledObjectFactory
    {
        public static T GetForTick<T>() where T : IPooledObject<T>, new() => GameLoop.GetObjectForTick<T>();
    }

    public static class PooledObjectExtensions
    {
        public static void ReleasePooledObject<T>(this IPooledObject<T> pooledObject)
        {
            pooledObject.IssuedTimestamp = 0;
        }
    }
}