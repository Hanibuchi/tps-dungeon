using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 倒れた敵が武器を落とす。敵のプレハブのルートに付ける（MonsterBuilder が付ける）。
    /// 何を落とすかは <see cref="EnemyDropTable.Default"/> の、敵の id（<see cref="EnemyIdentity"/>）の行で決める（<see cref="WeaponDropRoller"/>）。
    /// 表に行が無い敵は何も落とさない。
    /// 確率には操作キャラのドロップ増加（手の武器のエンチャント＋ホットバーのお守り・盾）が掛かる。
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

        private void OnDied(EnemyHealth _)
        {
            ItemInstance item = RollDrop();
            if (item != null) Spawn(item);
        }

        /// <summary>落とす物を決める（ユニークの天井の数えも進める）。何も落とさなければ null。</summary>
        public ItemInstance RollDrop()
        {
            EnemyDropTable table = EnemyDropTable.Default;
            string enemyId = EnemyId;
            if (table == null || string.IsNullOrEmpty(enemyId) || !table.TryGet(enemyId, out EnemyDropTable.EnemyEntry entry)) return null;

            var uniques = new List<WeaponDropRoller.UniqueOption>(entry.uniques.Count);
            var misses = new int[entry.uniques.Count];
            for (int i = 0; i < entry.uniques.Count; i++)
            {
                EnemyDropTable.UniqueEntry u = entry.uniques[i];
                uniques.Add(new WeaponDropRoller.UniqueOption(u.chance, u.pity));
                misses[i] = u.weapon != null ? UniqueDropPityStore.Misses(enemyId, u.weapon.Id) : 0;
            }

            WeaponDropRoller.Result result = WeaponDropRoller.Roll(entry.rankChances, uniques, misses, 1f + LeaderDropBonus(), Rng);

            bool changed = false;
            for (int i = 0; i < entry.uniques.Count; i++)
            {
                WeaponDefinition weapon = entry.uniques[i].weapon;
                if (weapon != null) changed |= UniqueDropPityStore.SetMisses(enemyId, weapon.Id, misses[i]);
            }
            if (changed) UniqueDropPityStore.Save();

            WeaponDefinition dropped = result.Kind switch
            {
                WeaponDropRoller.ResultKind.Unique => entry.uniques[result.UniqueIndex].weapon,
                WeaponDropRoller.ResultKind.Rank => table.PickWeapon(result.Rank, Rng),
                _ => null,
            };

            if (dropped == null && result.Kind == WeaponDropRoller.ResultKind.Rank)
            {
                Debug.LogWarning($"{enemyId} が {result.Rank} の武器を落とすはずが、ドロップ表に {result.Rank} の武器が無い", table);
            }

            return dropped != null ? ItemInstance.Create(dropped, Rng) : null;
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

        private void Spawn(ItemInstance item)
        {
            Vector2 offset = Random.insideUnitCircle * scatter;
            ItemDropper.Drop(item, transform.position + new Vector3(offset.x, spawnLift, offset.y));
        }
    }
}
