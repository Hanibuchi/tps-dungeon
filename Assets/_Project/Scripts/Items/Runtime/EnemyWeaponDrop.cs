using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 倒れた敵が武器を落とす。敵のプレハブのルートに付ける。
    /// 何を落とすかは <see cref="EnemyDropTable.Default"/> の、敵の id（<see cref="EnemyIdentity"/>）の行で決める（<see cref="WeaponDropRoller"/>）。
    /// 表に行が無い敵は何も落とさない。
    /// 確率には操作キャラのドロップ増加（手の武器のエンチャント＋ホットバーのお守り・盾）と、魂の強化のドロップ率（ユニークはユニークの分も）が掛かる。
    /// ユニークの天井の数えは <see cref="UniqueDropPityStore"/> に持ち越す。
    /// 落とした物は敵の居た所（浮く敵なら宙）に出し、床まで落とす（<see cref="ItemDropper"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyIdentity))]
    [AddComponentMenu("TPS Dungeon/Enemy Weapon Drop")]
    public sealed class EnemyWeaponDrop : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("落とす場所を敵の居た所からずらす半径（m）。")]
        private float scatter = 0.3f;

        [SerializeField, Min(0f), Tooltip("落とす物を出す高さ（敵のルートから上へ、m）。地面に居る敵で床に埋まらないように少し浮かせる。")]
        private float spawnLift = 0.3f;

        private static readonly System.Random Rng = new System.Random();

        private EnemyHealth health;
        private EnemyIdentity identity;

        /// <summary>ドロップ表（CSV）の敵の id（<see cref="EnemyIdentity"/>）。</summary>
        public string EnemyId
        {
            get
            {
                if (identity == null) identity = GetComponent<EnemyIdentity>();
                return identity != null ? identity.Id : null;
            }
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            identity = GetComponent<EnemyIdentity>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(EnemyHealth dead)
        {
            // 呼ばれた手下は落とさない（呼び続けて稼げないように）。
            if (dead != null && dead.IsSummoned) return;
            List<ItemInstance> drops = RollDrops();
            for (int i = 0; i < drops.Count; i++) Spawn(drops[i], i, drops.Count);
        }

        /// <summary>
        /// 落とす物を決める（ユニークの天井の数えも進める）。
        /// 普通の敵は多くて 1 本。必ず落とす本数（<see cref="EnemyDropTable.EnemyEntry.guaranteedCount"/>）のある敵（ボス）は、
        /// ユニークを確率と天井で振ったうえで、ランクの比率で決めた武器をその本数だけ落とす。
        /// </summary>
        public List<ItemInstance> RollDrops()
        {
            var drops = new List<ItemInstance>();
            EnemyDropTable table = EnemyDropTable.Default;
            string enemyId = EnemyId;
            if (table == null || string.IsNullOrEmpty(enemyId) || !table.TryGet(enemyId, out EnemyDropTable.EnemyEntry entry)) return drops;

            var uniques = new List<WeaponDropRoller.UniqueOption>(entry.uniques.Count);
            var misses = new int[entry.uniques.Count];
            for (int i = 0; i < entry.uniques.Count; i++)
            {
                EnemyDropTable.UniqueEntry u = entry.uniques[i];
                uniques.Add(new WeaponDropRoller.UniqueOption(u.chance, u.pity));
                misses[i] = u.weapon != null ? UniqueDropPityStore.Misses(enemyId, u.weapon.Id) : 0;
            }

            bool guaranteed = entry.guaranteedCount > 0;
            float multiplier = DropMultiplier();
            // 必ず落とす敵は、ここではユニークだけを振る（ランクの分は下で本数だけ振る）。
            WeaponDropRoller.Result result = WeaponDropRoller.Roll(guaranteed ? null : entry.rankChances, uniques, misses, multiplier,
                multiplier * UniqueDropMultiplier(), Rng);

            bool changed = false;
            for (int i = 0; i < entry.uniques.Count; i++)
            {
                WeaponDefinition weapon = entry.uniques[i].weapon;
                if (weapon != null) changed |= UniqueDropPityStore.SetMisses(enemyId, weapon.Id, misses[i]);
            }
            if (changed) UniqueDropPityStore.Save();

            if (result.Kind == WeaponDropRoller.ResultKind.Unique) Add(drops, entry.uniques[result.UniqueIndex].weapon);
            else if (result.Kind == WeaponDropRoller.ResultKind.Rank) AddRank(drops, table, enemyId, result.Rank);

            for (int i = 0; i < entry.guaranteedCount; i++)
            {
                WeaponDropRoller.Result rank = WeaponDropRoller.RollGuaranteedRank(entry.rankChances, Rng);
                if (rank.Kind == WeaponDropRoller.ResultKind.Rank) AddRank(drops, table, enemyId, rank.Rank);
            }

            return drops;
        }

        private static void AddRank(List<ItemInstance> drops, EnemyDropTable table, string enemyId, WeaponRank rank)
        {
            WeaponDefinition weapon = table.PickWeapon(rank, Rng);
            if (weapon == null) Debug.LogWarning($"{enemyId} が {rank} の武器を落とすはずが、ドロップ表に {rank} の武器が無い", table);
            Add(drops, weapon);
        }

        private static void Add(List<ItemInstance> drops, WeaponDefinition weapon)
        {
            if (weapon != null) drops.Add(ItemInstance.Create(weapon, Rng));
        }

        /// <summary>ドロップの確率に掛ける倍率。先頭のドロップ増加（エンチャント）と、魂の強化のドロップ率。</summary>
        public static float DropMultiplier()
        {
            float multiplier = 1f + LeaderDropBonus();
            PartyProgression progression = PartyProgression.Current;
            return progression != null ? (float)progression.Modifiers.DropRate.Apply(multiplier) : multiplier;
        }

        /// <summary>ユニークにだけ掛け足す倍率（魂の強化のユニークのドロップ率）。</summary>
        public static float UniqueDropMultiplier()
        {
            PartyProgression progression = PartyProgression.Current;
            return progression != null ? (float)progression.Modifiers.UniqueDropRate.Apply(1.0) : 1f;
        }

        /// <summary>
        /// 操作キャラ（パーティの先頭）のドロップ増加。手の武器のエンチャントと、ホットバーのお守り・盾の分を足す。
        /// 手に持っているのがお守り・盾なら、その分はホットバーの分に入っているので足さない。
        /// </summary>
        public static float LeaderDropBonus()
        {
            GameObject leader = PartyRoster.Leader;
            if (leader == null) return 0f;

            var totals = new EnchantmentTotals();
            var inventory = leader.GetComponent<PlayerInventory>();
            var hotbar = leader.GetComponent<PlayerHotbar>();
            if (inventory != null && hotbar != null)
            {
                ItemInstance held = inventory.Inventory[hotbar.SelectedIndex];
                WeaponTypeDefinition type = held?.Weapon?.WeaponType;
                if (held != null && held.Weapon != null && (type == null || !type.IsPassiveGear)) totals.AddAll(held.EnchantmentTotals());
            }

            var gear = leader.GetComponent<PlayerGear>();
            if (gear != null) totals.AddAll(gear.CurrentBonuses().WeaponTotals);
            return Mathf.Max(0f, totals.Amount(EnchantmentKind.DropUp));
        }

        private void Spawn(ItemInstance item, int index, int count)
        {
            // 何本も落とすときは、重ならないように輪に並べて広げる。
            Vector2 offset = count > 1
                ? new Vector2(Mathf.Cos(index * Mathf.PI * 2f / count), Mathf.Sin(index * Mathf.PI * 2f / count)) * Mathf.Max(scatter, 1.2f)
                : Random.insideUnitCircle * scatter;
            ItemDropper.Drop(item, transform.position + new Vector3(offset.x, spawnLift, offset.y));
        }
    }
}
