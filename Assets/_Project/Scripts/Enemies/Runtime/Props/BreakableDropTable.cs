using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 箱（木箱・宝箱）を壊したときに、敵・武器・何も出ないのどれになるかの表。どの層でも同じ 1 つを使い、箱のプレハブから差す。
    /// 敵は箱に差し込まれたその層の敵から重みで（<see cref="BreakableProp.SetEnemyPool"/>）、
    /// 武器のランクは差し込まれたその層のランクの重みで選ぶ（<see cref="BreakableProp.SetWeaponRanks"/>。FloorConfig.boxWeaponRanks）。
    /// 中身は Settings/Tables/Breakables.csv から取り込む（BreakableDropCsvImporter）。ここで直しても次の取り込みで上書きされる。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Breakable Drop Table", fileName = "BreakableDrops")]
    public sealed class BreakableDropTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public BreakableDropKind kind;

            [Min(0f), Tooltip("敵が出る重み。")]
            public float enemy;

            [Min(0f), Tooltip("武器が出る重み。")]
            public float weapon;

            [Min(0f), Tooltip("何も出ない重み。")]
            public float nothing;
        }

        [SerializeField, Tooltip("箱の種類ごとの重み。Breakables.csv の行。")]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(BreakableDropKind kind, out Entry entry)
        {
            foreach (Entry e in entries)
            {
                if (e.kind != kind) continue;
                entry = e;
                return true;
            }
            entry = default;
            return false;
        }

        public void SetEntries(IEnumerable<Entry> values)
        {
            entries = new List<Entry>(values);
        }
    }
}
