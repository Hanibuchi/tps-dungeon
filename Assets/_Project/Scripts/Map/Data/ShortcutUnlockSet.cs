using System;
using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// どの層のショートカット（ボス階と地上を結ぶ井戸とロープ）を解放したか。永続アップグレードで層ごとに解放する。
    /// 層は 0 始まり（DungeonFloor.Layer と同じ）。保存の形（JSON に書く欄）を兼ねるので、欄の名前は変えないこと。UnityEngine には依存しない。
    /// </summary>
    [Serializable]
    public sealed class ShortcutUnlockSet
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>解放した層（小さい順、重複なし）。</summary>
        public List<int> unlockedLayers = new List<int>();

        public bool IsUnlocked(int layer) => unlockedLayers != null && unlockedLayers.Contains(layer);

        /// <summary>解放する（unlocked が偽なら閉じる）。変わったら真。</summary>
        public bool Set(int layer, bool unlocked)
        {
            if (layer < 0) return false;
            if (unlockedLayers == null) unlockedLayers = new List<int>();
            if (unlocked == IsUnlocked(layer)) return false;

            if (unlocked)
            {
                unlockedLayers.Add(layer);
                unlockedLayers.Sort();
            }
            else
            {
                unlockedLayers.RemoveAll(l => l == layer);
            }
            return true;
        }

        /// <summary>読み込んだ値を均す（欄が無い・負の層・重複）。</summary>
        public void Normalize()
        {
            var layers = new SortedSet<int>();
            if (unlockedLayers != null)
            {
                foreach (int layer in unlockedLayers)
                {
                    if (layer >= 0) layers.Add(layer);
                }
            }
            unlockedLayers = new List<int>(layers);
        }
    }
}
