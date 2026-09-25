using System;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 持ち物の枠の並び。先頭の <see cref="HotbarSize"/> 枠がホットバー、その後ろがバッグ。
    /// 1 枠に 1 個（重ね持ちはしない）。空き枠は null。
    /// 並べ替えの規則だけを持ち、見た目や入力は扱わない。
    /// </summary>
    public sealed class Inventory
    {
        private ItemDefinition[] slots;

        public Inventory(int hotbarSize, int bagCapacity)
        {
            if (hotbarSize < 0) throw new ArgumentOutOfRangeException(nameof(hotbarSize));
            if (bagCapacity < 0) throw new ArgumentOutOfRangeException(nameof(bagCapacity));

            HotbarSize = hotbarSize;
            slots = new ItemDefinition[hotbarSize + bagCapacity];
        }

        /// <summary>ホットバーの枠の数。枠番号 0 から HotbarSize - 1 がホットバー。</summary>
        public int HotbarSize { get; }

        /// <summary>バッグの枠の数。<see cref="ExpandBag"/> で増える。</summary>
        public int BagCapacity => slots.Length - HotbarSize;

        /// <summary>全部の枠の数（ホットバー＋バッグ）。</summary>
        public int Count => slots.Length;

        /// <summary>中身か枠の数が変わったら飛ぶ。</summary>
        public event Action<Inventory> Changed;

        /// <summary>index 番目の枠の中身。空きや範囲外なら null。</summary>
        public ItemDefinition this[int index] => IsValid(index) ? slots[index] : null;

        public bool IsHotbar(int index) => index >= 0 && index < HotbarSize;

        public bool IsBag(int index) => index >= HotbarSize && index < slots.Length;

        public bool IsEmpty(int index) => IsValid(index) && slots[index] == null;

        /// <summary>どこかに空き枠があるか。</summary>
        public bool HasSpace => FirstEmpty(0, slots.Length) >= 0;

        /// <summary>
        /// 拾ったものを入れる。ホットバーの左から順に空きを探し、埋まっていればバッグの先頭から。
        /// 入れた枠の番号を返す。満杯なら -1。
        /// </summary>
        public int TryAdd(ItemDefinition item)
        {
            if (item == null) return -1;

            int index = FirstEmpty(0, slots.Length);
            if (index < 0) return -1;

            slots[index] = item;
            Changed?.Invoke(this);
            return index;
        }

        /// <summary>
        /// from の中身を to へ動かす。to が空いていれば移し、埋まっていれば入れ替える。
        /// 同じ枠・範囲外・from が空のときは何もしない。動いたら true。
        /// </summary>
        public bool Move(int from, int to)
        {
            if (from == to || !IsValid(from) || !IsValid(to) || slots[from] == null) return false;

            (slots[from], slots[to]) = (slots[to], slots[from]);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>
        /// ホットバーとバッグの間で持ち替える（Shift＋クリック）。
        /// バッグの枠ならホットバーの左から最初の空きへ、ホットバーの枠ならバッグの先頭から最初の空きへ移す。
        /// 行き先に空きが無ければ何もしない。動いたら true。
        /// </summary>
        public bool QuickMove(int index)
        {
            if (!IsValid(index) || slots[index] == null) return false;

            int to = IsHotbar(index)
                ? FirstEmpty(HotbarSize, slots.Length)
                : FirstEmpty(0, HotbarSize);
            return to >= 0 && Move(index, to);
        }

        /// <summary>index 番目の枠を空け、入っていたものを返す。空きや範囲外なら null。</summary>
        public ItemDefinition RemoveAt(int index)
        {
            if (!IsValid(index) || slots[index] == null) return null;

            ItemDefinition item = slots[index];
            slots[index] = null;
            Changed?.Invoke(this);
            return item;
        }

        /// <summary>バッグの枠を extra 個増やす（容量アップ）。今の中身はそのまま。</summary>
        public void ExpandBag(int extra)
        {
            if (extra <= 0) return;

            Array.Resize(ref slots, slots.Length + extra);
            Changed?.Invoke(this);
        }

        private bool IsValid(int index) => index >= 0 && index < slots.Length;

        /// <summary>[start, end) の範囲で最初の空き枠。無ければ -1。</summary>
        private int FirstEmpty(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (slots[i] == null) return i;
            }

            return -1;
        }
    }
}
