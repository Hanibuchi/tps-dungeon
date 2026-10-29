using System;
using System.Collections.Generic;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ホットバーのお守り・盾の効果（<see cref="GearBonuses"/>）を持ち主に効かせる。
    /// ホットバーの中身か選んでいる枠が変わるたびに出し直し、次へ書き込む。
    /// - 体力増加 → CharacterProgression.SetGearMaxHpPercent
    /// - 移動速度 → PlayerLocomotionSpeed.Multiplier
    /// - 経験値 → PartyProgression.GearExpBonus
    /// - 自然回復 → PlayerHealth.RegenPerSecond
    /// - 防御（盾） → PlayerHealth.DamageTaken。防御力は自分と並びの両隣（PartyRoster）の盾の合計（<see cref="Defense"/>）。
    ///   倒れている人（HP 0）の盾は数えない。自分か隣の盾、並びが変わるたびに書き直す。
    /// クリティカル率・ドロップ増加・数・多重は、攻撃の実行役（MeleeAttacker / RangedAttacker）が数値を出すときに
    /// <see cref="CurrentBonuses"/> の WeaponTotals を手の武器のエンチャントに足す。
    /// 経験値だけはパーティーに 1 つなので、パーティーの先頭のときだけ書く。
    /// キャラのルート（PlayerInventory・PlayerHotbar と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Gear")]
    public sealed class PlayerGear : MonoBehaviour
    {
        private readonly List<GearSlot> slots = new List<GearSlot>(PlayerHotbar.SlotCount);
        private static readonly List<float> rosterDefenses = new List<float>();

        private PlayerInventory inventory;
        private PlayerHotbar hotbar;
        private PlayerHealth health;
        private PlayerLocomotionSpeed locomotion;
        private CharacterProgression progression;
        private PartyProgression party;
        private ItemInstance offHandShield;
        private bool wasDead;

        /// <summary>最後に効かせた効果。</summary>
        public GearBonuses Bonuses { get; private set; } = GearBonuses.None;

        /// <summary>自分と両隣の盾を合わせた防御力。受けるダメージはこれで決まる。</summary>
        public float Defense { get; private set; }

        /// <summary>オフハンドに持っている盾（左手に持たせる）。無ければ null。選んでいる盾は手の物として MeleeAttacker が持たせる。</summary>
        public ItemInstance OffHandShield => offHandShield;

        /// <summary>効果が出し直された。</summary>
        public event Action<PlayerGear> Changed;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<PlayerHotbar>();
            health = GetComponent<PlayerHealth>();
            locomotion = GetComponent<PlayerLocomotionSpeed>();
            progression = GetComponent<CharacterProgression>();
            party = GetComponent<PartyProgression>();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            PartyRoster.LeaderChanged += OnLeaderChanged;
            PartyRoster.Changed += OnRosterChanged;
            if (health != null) health.Changed += OnHealthChanged;
            wasDead = health != null && health.IsDead;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            PartyRoster.LeaderChanged -= OnLeaderChanged;
            PartyRoster.Changed -= OnRosterChanged;
            if (health != null) health.Changed -= OnHealthChanged;
            Apply(GearBonuses.None, null);
        }

        // 経験値の上乗せはパーティーに 1 つなので、先頭が替わったら新しい先頭の分に書き直す。
        private void OnLeaderChanged(GameObject leader)
        {
            if (leader == gameObject) Refresh();
        }

        private void OnInventoryChanged(PlayerInventory _) => Refresh();

        // 並びが変われば隣が替わる。全員がそれぞれ自分の分を書き直す。
        private void OnRosterChanged() => RefreshDefense();

        // 倒れた・起きたで、隣に盾を貸すかが変わる。
        private void OnHealthChanged(PlayerHealth _)
        {
            if (health.IsDead == wasDead) return;
            wasDead = health.IsDead;
            RefreshNeighborhood();
        }

        private void OnHotbarChanged(PlayerHotbar _) => Refresh();

        /// <summary>
        /// 操作キャラ（パーティーの先頭）のお守りの鑑定の段（合計の切り捨て）。並びが無いシーンではシーンの持ち主を見る。
        /// 武器の情報欄はこれで詳しい数値を出す（ItemInstance.DetailTextForLeader）。
        /// </summary>
        public static int LeaderAppraisal()
        {
            GameObject leader = PartyRoster.Leader;
            PlayerGear gear = leader != null ? leader.GetComponent<PlayerGear>() : FindAnyObjectByType<PlayerGear>();
            if (gear == null) return 0;
            return (int)Math.Floor(gear.Bonuses.Totals.Amount(EnchantmentKind.Appraisal) + 0.0001f);
        }

        /// <summary>
        /// 今のホットバーから効果を出す。持ち替えの通知の順番に左右されないよう、攻撃の実行役はこれを読む
        /// （<see cref="Bonuses"/> はまだ出し直す前のことがある）。
        /// </summary>
        public GearBonuses CurrentBonuses()
        {
            if (inventory == null || hotbar == null) return GearBonuses.None;

            Inventory items = inventory.Inventory;
            slots.Clear();
            for (int i = 0; i < items.HotbarSize; i++)
            {
                WeaponDefinition weapon = items[i]?.Weapon;
                PassiveGear gear = weapon != null && weapon.WeaponType != null ? weapon.WeaponType.PassiveGear : PassiveGear.None;
                slots.Add(gear == PassiveGear.None
                    ? default
                    : new GearSlot(gear, weapon.Strength, items[i].EnchantmentTotals()));
            }

            // 選んでいる盾はメインハンドで効き、選んでいる物の武器種が盾を持てる（canUseShield。お守り・盾も表で選べる）なら、ほかの盾をオフハンドに持つ。
            WeaponDefinition held = items[hotbar.SelectedIndex]?.Weapon;
            bool offHand = held != null && held.WeaponType != null && held.WeaponType.CanUseShield;
            return GearBonuses.Compute(slots, offHand, hotbar.SelectedIndex);
        }

        /// <summary>出し直して効かせる。</summary>
        public void Refresh()
        {
            GearBonuses bonuses = CurrentBonuses();
            ItemInstance shield = bonuses.OffHandShieldIndex >= 0 ? inventory.Inventory[bonuses.OffHandShieldIndex] : null;
            Apply(bonuses, shield);
        }

        private void Apply(GearBonuses bonuses, ItemInstance shield)
        {
            Bonuses = bonuses;
            offHandShield = shield;

            if (health != null) health.RegenPerSecond = bonuses.RegenPerSecond;
            RefreshNeighborhood();

            if (locomotion != null) locomotion.Multiplier = 1f + bonuses.MoveSpeedPercent;
            if (progression != null) progression.SetGearMaxHpPercent(bonuses.MaxHpPercent);

            // パーティー全体に掛かる経験値の上乗せは、人数で積み上がらないよう先頭の手持ちの分だけにする。
            PartyProgression target = party != null ? party : PartyProgression.Current;
            if (target != null && PartyRoster.IsLeader(gameObject)) target.GearExpBonus = bonuses.ExpPercent;

            Changed?.Invoke(this);
        }

        // 自分の盾は自分と両隣の防御力に入るので、3 人分を書き直す。
        private void RefreshNeighborhood()
        {
            IReadOnlyList<GameObject> members = PartyRoster.Members;
            int index = IndexIn(members);
            if (index < 0)
            {
                RefreshDefense();
                return;
            }

            for (int i = Mathf.Max(0, index - 1); i <= Mathf.Min(members.Count - 1, index + 1); i++)
            {
                if (members[i] != null && members[i].TryGetComponent(out PlayerGear gear)) gear.RefreshDefense();
            }
        }

        /// <summary>自分と両隣の盾から防御力を出し直し、受けるダメージの倍率へ書く。並びに居なければ（1 人のテストなど）自分の盾だけ。</summary>
        private void RefreshDefense()
        {
            IReadOnlyList<GameObject> members = PartyRoster.Members;
            int index = IndexIn(members);
            float defense;
            if (index < 0)
            {
                defense = OwnDefense();
            }
            else
            {
                rosterDefenses.Clear();
                foreach (GameObject member in members)
                {
                    rosterDefenses.Add(member != null && member.TryGetComponent(out PlayerGear gear) ? gear.OwnDefense() : 0f);
                }
                defense = GearBonuses.WithNeighbors(rosterDefenses, index);
            }

            if (Mathf.Approximately(defense, Defense) && health != null && Mathf.Approximately(health.DamageTaken, GearBonuses.DamageTakenFor(defense))) return;

            Defense = defense;
            if (health != null) health.DamageTaken = GearBonuses.DamageTakenFor(defense);
            Changed?.Invoke(this);
        }

        // 隣へ貸す分。効いていない（無効・倒れている）なら 0。
        private float OwnDefense() => isActiveAndEnabled && (health == null || !health.IsDead) ? Bonuses.ShieldDefense : 0f;

        private int IndexIn(IReadOnlyList<GameObject> members)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == gameObject) return i;
            }
            return -1;
        }
    }
}
