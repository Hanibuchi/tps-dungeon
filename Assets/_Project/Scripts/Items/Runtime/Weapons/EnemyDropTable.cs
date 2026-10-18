using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 敵ごとに落とす武器の表。ランク E〜S の確率と、ユニーク武器の確率・天井を持つ。
    /// 正は CSV（Settings/Tables/EnemyWeaponDrops.csv・EnemyUniqueDrops.csv）で、エディタが取り込んでここに書き込む。
    /// ランクが決まったら、そのランクの武器（<see cref="droppableWeapons"/>。素手とユニークは入れない）から等確率に 1 本。
    /// ゲーム全体で 1 つだけ使い、Resources/<see cref="ResourcePath"/> に置く（<see cref="Default"/> で引く）。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDropTable", menuName = "TPS Dungeon/Weapons/Enemy Drop Table")]
    public sealed class EnemyDropTable : ScriptableObject
    {
        [Serializable]
        public sealed class UniqueEntry
        {
            public WeaponDefinition weapon;

            [Range(0f, 1f), Tooltip("1 回倒したときに落とす確率（ドロップ増加を掛ける前）。")]
            public float chance;

            [Min(0), Tooltip("出ないまま倒した数 + 1 がこれになったら必ず落とす。0 なら天井なし。")]
            public int pity;
        }

        [Serializable]
        public sealed class EnemyEntry
        {
            [Tooltip("敵の id（EnemyIdentity の id。pink_blob など）。")]
            public string enemyId;

            [Tooltip("E〜S を落とす確率（0〜1）。残りは何も落とさない。")]
            public float[] rankChances = new float[EnchantmentRankCsv.RankColumns.Length];

            [Tooltip("上から順に判定する。")]
            public List<UniqueEntry> uniques = new List<UniqueEntry>();
        }

        /// <summary>Resources から読むときのパス（拡張子なし）。</summary>
        public const string ResourcePath = "Weapons/EnemyDropTable";

        private static EnemyDropTable cached;

        [SerializeField] private List<EnemyEntry> enemies = new List<EnemyEntry>();

        [SerializeField, Tooltip("ランクで落とす武器の候補。素手とユニークは入れない。CSV を取り込むと全部の武器から作り直す。")]
        private List<WeaponDefinition> droppableWeapons = new List<WeaponDefinition>();

        private readonly List<WeaponDefinition> scratch = new List<WeaponDefinition>();

        /// <summary>ゲーム全体で使う表。Resources に無ければ null。</summary>
        public static EnemyDropTable Default
        {
            get
            {
                if (cached == null) cached = Resources.Load<EnemyDropTable>(ResourcePath);
                return cached;
            }
        }

        public IReadOnlyList<EnemyEntry> Enemies => enemies;
        public IReadOnlyList<WeaponDefinition> DroppableWeapons => droppableWeapons;

        public bool TryGet(string enemyId, out EnemyEntry entry)
        {
            foreach (EnemyEntry e in enemies)
            {
                if (e == null || !string.Equals(e.enemyId, enemyId, StringComparison.Ordinal)) continue;
                entry = e;
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>rank の武器から等確率に 1 本。無ければ null。</summary>
        public WeaponDefinition PickWeapon(WeaponRank rank, System.Random random)
        {
            scratch.Clear();
            foreach (WeaponDefinition w in droppableWeapons)
            {
                if (w != null && w.Rank == rank) scratch.Add(w);
            }

            return scratch.Count > 0 ? scratch[random.Next(scratch.Count)] : null;
        }

        /// <summary>中身を差し替える（CSV の取り込み用）。</summary>
        public void Assign(List<EnemyEntry> enemyEntries, List<WeaponDefinition> weapons)
        {
            enemies = enemyEntries ?? new List<EnemyEntry>();
            droppableWeapons = weapons ?? new List<WeaponDefinition>();
        }
    }
}
