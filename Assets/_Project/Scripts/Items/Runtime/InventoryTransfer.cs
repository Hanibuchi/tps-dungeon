namespace TpsDungeon.Items
{
    /// <summary>
    /// 別々の <see cref="Inventory"/> の間で物を動かす規則。キャラの手持ちと共有のバッグ、仲間どうしの手持ちの間で使う。
    /// 同じ Inventory どうしなら <see cref="Inventory.Move"/> と同じ（空きへ移す・埋まっていれば入れ替える）。
    /// </summary>
    public static class InventoryTransfer
    {
        /// <summary>
        /// from の fromIndex の物を to の toIndex へ動かす。行き先が空いていれば移し、埋まっていれば入れ替える。
        /// 範囲外・動かす物が無い・同じ枠のときは何もしない。動いたら true。
        /// </summary>
        public static bool Move(Inventory from, int fromIndex, Inventory to, int toIndex)
        {
            if (from == null || to == null) return false;
            if (from == to) return from.Move(fromIndex, toIndex);
            if (fromIndex < 0 || fromIndex >= from.Count || toIndex < 0 || toIndex >= to.Count) return false;

            ItemInstance moving = from[fromIndex];
            if (moving == null) return false;

            // 先に行き先を差し替えてから元の枠に戻す。どちらも 1 回ずつ Changed が飛ぶ。
            ItemInstance displaced = to.Place(toIndex, moving);
            from.Place(fromIndex, displaced);
            return true;
        }

        /// <summary>
        /// 手持ち（hand）とバッグ（bag）の間で持ち替える（Shift＋クリック）。
        /// 手持ちの物はバッグの先頭から最初の空きへ、バッグの物は手持ちの左から最初の空きへ移す。
        /// source は hand か bag のどちらか。行き先に空きが無ければ何もしない。動いたら true。
        /// </summary>
        public static bool QuickMove(Inventory source, int index, Inventory hand, Inventory bag)
        {
            if (source == null || hand == null || bag == null) return false;
            if (source[index] == null) return false;

            Inventory target = source == hand ? bag : source == bag ? hand : null;
            if (target == null) return false;

            int to = FirstEmpty(target);
            return to >= 0 && Move(source, index, target, to);
        }

        /// <summary>最初の空き枠。無ければ -1。</summary>
        public static int FirstEmpty(Inventory inventory)
        {
            if (inventory == null) return -1;
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory.IsEmpty(i)) return i;
            }

            return -1;
        }
    }
}
