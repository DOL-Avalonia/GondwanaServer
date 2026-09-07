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
    }
}