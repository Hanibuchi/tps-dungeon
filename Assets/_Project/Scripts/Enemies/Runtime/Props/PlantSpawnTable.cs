using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 草花を壊したときに出てくる敵の表。敵と確率の組をいくつでも並べる。
    /// 1 本壊すたびに 1 回だけ振り、上から確率を足していって当たった敵を 1 体出す。確率の合計の残りは何も出ない。
    /// 合計が 1 を超えたら 1 に縮めて（比で）振る。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Plant Spawn Table", fileName = "PlantSpawnTable")]
    public sealed class PlantSpawnTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("出てくる敵のプレハブ。")]
            public GameObject enemy;

            [Range(0f, 1f), Tooltip("この敵が出る確率（0〜1 の小数。0.05 で 5%）。")]
            public float chance;
        }

        [SerializeField, Tooltip("敵と確率の組。上から判定する。")]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>何かが出る確率の合計（1 で頭打ち）。</summary>
        public float TotalChance
        {
            get
            {
                float total = 0f;
                foreach (Entry e in entries) total += Mathf.Max(0f, e.chance);
                return Mathf.Min(1f, total);
            }
        }

        /// <summary>1 回振って出てくる敵を返す。何も出なければ null。roll は 0 以上 1 未満の乱数。</summary>
        public GameObject Roll(float roll)
        {
            var chances = new float[entries.Count];
            for (int i = 0; i < chances.Length; i++) chances[i] = entries[i].enemy != null ? entries[i].chance : 0f;
            int index = Pick(chances, roll);
            return index >= 0 ? entries[index].enemy : null;
        }

        /// <summary>
        /// 確率の並び chances から、roll（0 以上 1 未満）で当たった番号を返す。外れなら -1。
        /// 負の確率は 0 とみなし、合計が 1 を超えたら合計で割って比にする。
        /// </summary>
        public static int Pick(IReadOnlyList<float> chances, float roll)
        {
            float total = 0f;
            for (int i = 0; i < chances.Count; i++) total += Mathf.Max(0f, chances[i]);
            if (total <= 0f) return -1;

            float scale = total > 1f ? 1f / total : 1f;
            float cumulative = 0f;
            for (int i = 0; i < chances.Count; i++)
            {
                float chance = Mathf.Max(0f, chances[i]) * scale;
                if (chance <= 0f) continue;
                cumulative += chance;
                if (roll < cumulative) return i;
            }
            return -1;
        }

        public void SetEntries(IEnumerable<Entry> values)
        {
            entries = new List<Entry>(values);
        }
    }
}
