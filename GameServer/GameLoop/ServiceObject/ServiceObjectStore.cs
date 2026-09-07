using System.Collections.Frozen;
using System.Collections.Generic;

namespace DOL.GS
{
    public static class ServiceObjectStore
    {
        private static readonly FrozenDictionary<ServiceObjectType, IServiceObjectArray> _serviceObjectArrays =
            new Dictionary<ServiceObjectType, IServiceObjectArray>()
            {
                { ServiceObjectType.Timer, new ShardedServiceObjectArray<ECSGameTimer>(8192) },
                { ServiceObjectType.Client, new ServiceObjectArray<GameClient>(1024) },
                { ServiceObjectType.SubZoneObject, new ServiceObjectArray<SubZoneObject>(32768) },
            }.ToFrozenDictionary();

        public static bool Add<T>(T serviceObject) where T : class, IServiceObject
        {
            ServiceObjectId id = serviceObject.ServiceObjectId;

            if (id.IsRegistered && id.PeekAction() is not ServiceObjectId.PendingAction.Remove and not ServiceObjectId.PendingAction.Schedule)
                return false;

            if (!id.TrySetAction(ServiceObjectId.PendingAction.Add))
                return false;

            (_serviceObjectArrays[id.Type] as ServiceObjectArrayBase<T>)!.Add(serviceObject);
            return true;
        }

        public static bool Schedule<T>(T serviceObject, long nextTickMs) where T : class, ISchedulableServiceObject
        {
            SchedulableServiceObjectId id = serviceObject.ServiceObjectId;

            if (!id.TrySetAction(ServiceObjectId.PendingAction.Schedule))
                return false;

            (_serviceObjectArrays[id.Type] as ServiceObjectArrayBase<T>)!.Schedule(serviceObject, nextTickMs);
            return true;
        }

        public static bool Remove<T>(T serviceObject) where T : class, IServiceObject
        {
            ServiceObjectId id = serviceObject.ServiceObjectId;

            if (!id.IsRegistered && id.PeekAction() is not ServiceObjectId.PendingAction.Add and not ServiceObjectId.PendingAction.Schedule)
                return false;

            if (!id.TrySetAction(ServiceObjectId.PendingAction.Remove))
                return false;

            (_serviceObjectArrays[id.Type] as ServiceObjectArrayBase<T>)!.Remove(serviceObject);
            return true;
        }

        public static ServiceObjectView<T> UpdateAndGetView<T>(ServiceObjectType type) where T : class, IServiceObject
        {
            ServiceObjectArrayBase<T> array = _serviceObjectArrays[type] as ServiceObjectArrayBase<T>;
            array!.Update(GameLoop.GameLoopTime);

            if (array.IsSharded)
                return new(array.Shards, array.ShardStartIndices, array.TotalValidCount);
            else
                return new(array.Items, array.LastValidIndex + 1);
        }
    }
}