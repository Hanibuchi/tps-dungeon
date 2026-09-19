using System;
using System.Collections.Generic;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// A* 用の最小ヒープ。.NET Standard 2.1 には PriorityQueue が無いので用意している。
    /// 同コストの要素は挿入順に取り出されるので、探索結果は決定的になる。
    /// </summary>
    internal sealed class MinHeap
    {
        private readonly List<(int priority, int order, int value)> items = new List<(int, int, int)>();
        private int nextOrder;

        public int Count => items.Count;

        public void Clear()
        {
            items.Clear();
            nextOrder = 0;
        }

        public void Push(int priority, int value)
        {
            items.Add((priority, nextOrder++, value));
            int child = items.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (Compare(items[child], items[parent]) >= 0) break;
                (items[child], items[parent]) = (items[parent], items[child]);
                child = parent;
            }
        }

        public int Pop()
        {
            if (items.Count == 0) throw new InvalidOperationException("空のヒープから取り出そうとした");

            int result = items[0].value;
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);

            int parent = 0;
            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= items.Count) break;
                int right = left + 1;
                int smallest = right < items.Count && Compare(items[right], items[left]) < 0 ? right : left;
                if (Compare(items[smallest], items[parent]) >= 0) break;
                (items[smallest], items[parent]) = (items[parent], items[smallest]);
                parent = smallest;
            }

            return result;
        }

        private static int Compare((int priority, int order, int value) a, (int priority, int order, int value) b)
        {
            int byPriority = a.priority.CompareTo(b.priority);
            return byPriority != 0 ? byPriority : a.order.CompareTo(b.order);
        }
    }
}
