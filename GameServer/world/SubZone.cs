using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS
{
    public class SubZone
    {
        private static int _nextId;
        private readonly WriteLockedLinkedList<GameObject>[] _objects;
        private readonly int _id;

        public Zone ParentZone { get; }

        public SubZone(Zone parentZone)
        {
            _id = Interlocked.Increment(ref _nextId);
            ParentZone = parentZone;
            _objects = new WriteLockedLinkedList<GameObject>[Enum.GetValues<Zone.eGameObjectType>().Length];

            for (int i = 0; i < _objects.Length; i++)
                _objects[i] = new();
        }

        public void AddObject(LinkedListNode<GameObject> node)
        {
            _objects[(int)node.Value.GameObjectType].AddLast(node, static (n, subZone) => n.Value.SubZoneObject.CurrentSubZone = subZone, this);
        }

        public void RemoveObject(LinkedListNode<GameObject> node)
        {
            _objects[(int)node.Value.GameObjectType].Remove(node, static n => n.Value.SubZoneObject.CurrentSubZone = null);
        }

        public void AddObjectToThisAndRemoveFromOther(LinkedListNode<GameObject> node, SubZone otherSubZone)
        {
            if (this == otherSubZone)
                return;

            int type = (int)node.Value.GameObjectType;
            WriteLockedLinkedList<GameObject>.Move(node, otherSubZone._objects[type], _objects[type], otherSubZone._id, _id,
                static (n, subZone) => n.Value.SubZoneObject.CurrentSubZone = subZone, this);
        }

        public WriteLockedLinkedList<GameObject> this[Zone.eGameObjectType objectType] => _objects[(int)objectType];
    }

    public class SubZoneObject : IServiceObject
    {
        private bool _isInitiatingTransition;
        private bool _isTransitionQueued;

        public LinkedListNode<GameObject> Node { get; }
        public SubZone CurrentSubZone { get; set; }
        public Zone DestinationZone { get; private set; }
        public SubZone DestinationSubZone { get; private set; }
        public ServiceObjectId ServiceObjectId { get; } = new(ServiceObjectType.SubZoneObject);

        public SubZoneObject(GameObject obj)
        {
            Node = new(obj);
        }

        public void InitiateSubZoneTransition(Zone destinationZone, SubZone destinationSubZone)
        {
            if (Interlocked.CompareExchange(ref _isInitiatingTransition, true, false) != false)
                return;

            try
            {
                DestinationZone = destinationZone;
                DestinationSubZone = destinationSubZone;

                if (_isTransitionQueued)
                    return;

                if (!ServiceObjectStore.Add(this))
                {
                    DestinationZone = null;
                    DestinationSubZone = null;
                    return;
                }

                _isTransitionQueued = true;
            }
            finally
            {
                Interlocked.Exchange(ref _isInitiatingTransition, false);
            }
        }

        public void OnSubZoneTransition()
        {
            if (!_isTransitionQueued)
                return;

            if (!ServiceObjectStore.Remove(this))
                return;

            _isTransitionQueued = false;
            DestinationZone = null;
            DestinationSubZone = null;
        }
    }
}