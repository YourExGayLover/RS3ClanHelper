using System.Collections.Generic;

namespace RS3ClanHelper.Utils
{
    public static class EnumerableBatch
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(IEnumerable<T> source, int size)
        {
            var list = new List<T>(size);
            foreach (var it in source)
            {
                list.Add(it);
                if (list.Count == size)
                {
                    yield return list.ToArray();
                    list.Clear();
                }
            }
            if (list.Count > 0) yield return list.ToArray();
        }
    }
}
