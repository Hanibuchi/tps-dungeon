using System;
using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 1 つの層の、部屋に出る敵の表。道順（DungeonRoute）の層ごとに 1 つ差し、その層のランダムな階はどれもこれを使う。
    /// 部屋の予算は 0 〜 budgetPerCell × 部屋のセル数 から一様に引き、編成タイプを重みで引いて、
    /// 種類ごとの体数とグループを決める（RoomEnemyRoller）。普通の部屋だけに出し、monsterHouseChance でモンスターハウスにする。
    /// 敵・コスト・予算・モンスターハウスの確率と倍率・編成タイプは CSV（Settings/Tables/Enemies.csv・EnemySpawnLayers.csv・
    /// EnemyCompositions.csv）から取り込む（EnemySpawnCsvImporter）。ここで直しても次の取り込みで上書きされる。音だけはここで直す。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Enemy Spawn Table", fileName = "EnemySpawnTable")]
    public sealed class EnemySpawnTable : ScriptableObject
    {
        [Serializable]
        public struct Enemy
        {
            [Tooltip("敵の id（Enemies.csv の id）。")]
            public string id;

            [Tooltip("出てくる敵のプレハブ。")]
            public GameObject prefab;

            [Min(0.01f), Tooltip("1 体あたりのコスト。部屋の予算からこの分を使う。")]
            public float cost;
        }

        [Serializable]
        public struct Share
        {
            [Tooltip("敵のプレハブ。上の敵の一覧に入っているもの（入っていなければ出ない）。")]
            public GameObject enemy;

            [Min(0f), Tooltip("この種類に回す予算の比率（出現数の比率ではない）。合計で割って使うので、合計が 1 でなくてもよい。")]
            public float share;
        }

        [Serializable]
        public struct Composition
        {
            [Tooltip("見分けるための名前（混成・主役＋脇役・単種など）。")]
            public string name;

            [Min(0f), Tooltip("この編成タイプが選ばれる重み。")]
            public float weight;

            [Tooltip("種類ごとの予算の比率。単種なら 1 つだけ。")]
            public List<Share> shares;
        }

        [Header("CSV から取り込む（ここを直しても上書きされる）")]
        [SerializeField, Min(0f), Tooltip("部屋のセル 1 つあたりの予算の上限。部屋の予算は 0 〜 これ × セル数（1x1 なら 1、2x2 なら 4）から一様に決まる。EnemySpawnLayers.csv の budget_per_cell。")]
        private float budgetPerCell = 2f;

        [SerializeField, Range(0f, 1f), Tooltip("普通の部屋がモンスターハウスになる確率。ドアを開けると全員が中に入れられ、中の敵を全部倒すまで出られない。EnemySpawnLayers.csv の monster_house_chance。")]
        private float monsterHouseChance = 0.2f;

        [SerializeField, Min(0f), Tooltip("モンスターハウスの予算の上限に掛ける倍率。2 なら普通の部屋の 2 倍まで出る。EnemySpawnLayers.csv の monster_house_multiplier。")]
        private float monsterHouseBudgetMultiplier = 2f;

        [SerializeField, Tooltip("この層に出る敵とコスト。Enemies.csv の layers にこの層が入っている敵。")]
        private List<Enemy> enemies = new List<Enemy>();

        [Header("モンスターハウスの音（ここで直す。未設定なら鳴らさない）")]
        [SerializeField, Tooltip("ドアを開けて閉じ込められたときの音。")]
        private AudioClip trappedSound;

        [SerializeField, Tooltip("中の敵を全部倒して、ドアの鍵が開いたときの音。")]
        private AudioClip releasedSound;

        [SerializeField, Range(0f, 1f), Tooltip("閉じ込め・鍵が開く音の大きさ。")]
        private float soundVolume = 1f;

        [Header("編成タイプ（CSV から取り込む。ここを直しても上書きされる）")]
        [SerializeField, Tooltip("編成タイプ。部屋ごとに 1 つを重みで選ぶ。EnemyCompositions.csv の、この層を使う編成と、この層に出る敵ごとの単種。")]
        private List<Composition> compositions = new List<Composition>();

        public float BudgetPerCell => budgetPerCell;
        public float MonsterHouseChance => monsterHouseChance;
        public float MonsterHouseBudgetMultiplier => monsterHouseBudgetMultiplier;
        public AudioClip TrappedSound => trappedSound;
        public AudioClip ReleasedSound => releasedSound;
        public float SoundVolume => soundVolume;
        public IReadOnlyList<Enemy> Enemies => enemies;
        public IReadOnlyList<Composition> Compositions => compositions;

        /// <summary>生成器に渡す形にする。敵の Id は enemies の並びの番号。</summary>
        public EnemySpawnRules BuildRules()
        {
            var rules = new EnemySpawnRules
            {
                BudgetPerCell = budgetPerCell,
                MonsterHouseChance = monsterHouseChance,
                MonsterHouseBudgetMultiplier = monsterHouseBudgetMultiplier,
            };
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].prefab != null) rules.Enemies.Add(new EnemyKind(i, enemies[i].cost));
            }
            foreach (var c in compositions)
            {
                var composition = new EnemyComposition { Weight = c.weight };
                if (c.shares != null)
                {
                    foreach (var s in c.shares)
                    {
                        int id = IdOf(s.enemy);
                        if (id >= 0) composition.Shares.Add(new EnemyShare(id, s.share));
                    }
                }
                rules.Compositions.Add(composition);
            }
            return rules;
        }

        /// <summary>BuildRules の Id の敵のプレハブ。無ければ null。</summary>
        public GameObject PrefabOf(int id) => id >= 0 && id < enemies.Count ? enemies[id].prefab : null;

        private int IdOf(GameObject prefab)
        {
            if (prefab == null) return -1;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].prefab == prefab) return i;
            }
            return -1;
        }

        /// <summary>空いている音だけ埋める（手で差し替えた音は残す）。何か埋めたら true。</summary>
        public bool FillMissingSounds(AudioClip trapped, AudioClip released)
        {
            bool changed = false;
            if (trappedSound == null && trapped != null) { trappedSound = trapped; changed = true; }
            if (releasedSound == null && released != null) { releasedSound = released; changed = true; }
            return changed;
        }

        /// <summary>CSV から取り込んだ値に差し替える。音は触らない。</summary>
        public void Assign(float perCell, float houseChance, float houseMultiplier, IEnumerable<Enemy> newEnemies,
            IEnumerable<Composition> newCompositions)
        {
            budgetPerCell = perCell;
            monsterHouseChance = houseChance;
            monsterHouseBudgetMultiplier = houseMultiplier;
            enemies = new List<Enemy>(newEnemies);
            compositions = new List<Composition>(newCompositions);
        }
    }
}
