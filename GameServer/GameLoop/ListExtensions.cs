using System.Collections.Generic;

namespace DOL.GS
{
    public static class ServiceListExtensions
    {
        public static void Resize<T>(this List<T> list, int newCapacity)
        {
            if (list.Capacity < newCapacity)
                list.Capacity = newCapacity;
        }

        public static void SwapRemoveAt<T>(this List<T> list, int index)
        {
            int last = list.Count - 1;
            if (index != last) list[index] = list[last];
            list.RemoveAt(last);
        }
    }
}