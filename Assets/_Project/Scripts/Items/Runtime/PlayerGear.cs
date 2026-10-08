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
    /// - 自然回復・防御（盾） → PlayerHealth.RegenPerSecond / DamageTaken
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

        private PlayerInventory inventory;
        private PlayerHotbar hotbar;
        private PlayerHealth health;
        private PlayerLocomotionSpeed locomotion;
        private CharacterProgression progression;
        private PartyProgression party;
        private ItemInstance activeShield;

        /// <summary>最後に効かせた効果。</summary>
        public GearBonuses Bonuses { get; private set; } = GearBonuses.None;

        /// <summary>効いている盾（左手に持たせる）。無ければ null。</summary>
        public ItemInstance ActiveShield => activeShield;

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
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            PartyRoster.LeaderChanged -= OnLeaderChanged;
            Apply(GearBonuses.None, null);
        }

        // 経験値の上乗せはパーティーに 1 つなので、先頭が替わったら新しい先頭の分に書き直す。
        private void OnLeaderChanged(GameObject leader)
        {
            if (leader == gameObject) Refresh();
        }

        private void OnInventoryChanged(PlayerInventory _) => Refresh();

        private void OnHotbarChanged(PlayerHotbar _) => Refresh();

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

            WeaponDefinition held = items[hotbar.SelectedIndex]?.Weapon;
            bool holdingShieldWeapon = held != null && held.WeaponType != null && held.WeaponType.CanUseShield && !held.WeaponType.IsPassiveGear;
            return GearBonuses.Compute(slots, holdingShieldWeapon);
        }

        /// <summary>出し直して効かせる。</summary>
        public void Refresh()
        {
            GearBonuses bonuses = CurrentBonuses();
            ItemInstance shield = bonuses.ActiveShieldIndex >= 0 ? inventory.Inventory[bonuses.ActiveShieldIndex] : null;
            Apply(bonuses, shield);
        }

        private void Apply(GearBonuses bonuses, ItemInstance shield)
        {
            Bonuses = bonuses;
            activeShield = shield;

            if (health != null)
            {
                health.DamageTaken = bonuses.DamageTaken;
                health.RegenPerSecond = bonuses.RegenPerSecond;
            }

            if (locomotion != null) locomotion.Multiplier = 1f + bonuses.MoveSpeedPercent;
            if (progression != null) progression.SetGearMaxHpPercent(bonuses.MaxHpPercent);

            // パーティー全体に掛かる経験値の上乗せは、人数で積み上がらないよう先頭の手持ちの分だけにする。
            PartyProgression target = party != null ? party : PartyProgression.Current;
            if (target != null && PartyRoster.IsLeader(gameObject)) target.GearExpBonus = bonuses.ExpPercent;

            Changed?.Invoke(this);
        }
    }
}
