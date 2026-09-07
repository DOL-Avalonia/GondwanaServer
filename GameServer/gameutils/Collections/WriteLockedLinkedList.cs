using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS
{
    public class WriteLockedLinkedList<T>
    {
        private readonly LinkedList<T> _list = new();
        private readonly Lock _lock = new();

        public Lock Lock => _lock;                 // Hold this lock while iterating with First/Next.
        public int Count => _list.Count;
        public LinkedListNode<T> First => _list.First;

        public void AddLast(LinkedListNode<T> node, Action<LinkedListNode<T>, SubZone> onAdded, SubZone state)
        {
            lock (_lock)
            {
                _list.AddLast(node);
                onAdded(node, state);
            }
        }

        public void Remove(LinkedListNode<T> node, Action<LinkedListNode<T>> onRemoved)
        {
            lock (_lock)
            {
                _list.Remove(node);
                onRemoved(node);
            }
        }

        public static void Move(LinkedListNode<T> node, WriteLockedLinkedList<T> from, WriteLockedLinkedList<T> to,
            int fromId, int toId, Action<LinkedListNode<T>, SubZone> onMoved, SubZone state)
        {
            // Ordered locking (by subzone id) to prevent deadlocks.
            Lock first, second;

            if (fromId < toId)
            {
                first = from._lock;
                second = to._lock;
            }
            else
            {
                first = to._lock;
                second = from._lock;
            }

            lock (first)
            {
                lock (second)
                {
                    from._list.Remove(node);
                    to._list.AddLast(node);
                    onMoved(node, state);
                }
            }
        }
    }
}