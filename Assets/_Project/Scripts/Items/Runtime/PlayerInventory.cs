using System;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// プレイヤーの持ち物。<see cref="Inventory"/> を持ち、拾う・捨てるの出入り口になる。
    /// ホットバーの枠はインベントリの先頭の枠で、どれを選んでいるかは PlayerHotbar が持つ。
    /// ゲーム中に捨てるキーを押すと、PlayerHotbar で選んでいる枠（手に持っているもの）を捨てる。
    /// プレイヤーのルート（PlayerInput と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Inventory")]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("バッグの枠の数（ホットバーの枠は含まない）。")]
        private int bagCapacity = 12;

        [SerializeField, Tooltip("捨てたものを置く、キャラの前方への距離（メートル）。")]
        private float dropDistance = 1f;

        [SerializeField, Tooltip("捨てたものが重ならないよう、置く位置を散らす半径（メートル）。")]
        private float dropScatter = 0.25f;

        [SerializeField, Tooltip("捨てる位置の床を探すレイと、前の壁を避けるレイが当たるレイヤー。")]
        private LayerMask groundMask = ~0;

        private Inventory inventory;
        private PlayerHotbar hotbar;

        /// <summary>持ち物の枠。先頭の PlayerHotbar.SlotCount 枠がホットバー。</summary>
        public Inventory Inventory
        {
            get
            {
                EnsureInventory();
                return inventory;
            }
        }

        /// <summary>中身か枠の数が変わったら飛ぶ。</summary>
        public event Action<PlayerInventory> Changed;

        private void Awake()
        {
            EnsureInventory();
            hotbar = GetComponent<PlayerHotbar>();
        }

        private void OnEnable()
        {
            if (hotbar != null) hotbar.DropRequested += OnDropRequested;
        }

        private void OnDisable()
        {
            if (hotbar != null) hotbar.DropRequested -= OnDropRequested;
        }

        private void OnDropRequested(int index) => Drop(index);

        private void EnsureInventory()
        {
            if (inventory != null) return;

            inventory = new Inventory(PlayerHotbar.SlotCount, bagCapacity);
            inventory.Changed += _ => Changed?.Invoke(this);
        }

        /// <summary>拾ったものを入れる。入らなければ false。</summary>
        public bool TryAdd(ItemDefinition item) => Inventory.TryAdd(item) >= 0;

        /// <summary>
        /// index 番目の枠のものを足元（キャラの少し前）に捨て、拾える物として置く。
        /// ポーズ中（timeScale 0）でも置けるよう、物理は使わず位置を決めて置くだけにする。
        /// </summary>
        public bool Drop(int index)
        {
            ItemDefinition item = Inventory[index];
            if (item == null) return false;

            if (item.WorldPrefab == null)
            {
                Debug.LogWarning($"{item.DisplayName} には捨てたときに出す物が無いので捨てられない", item);
                return false;
            }

            Inventory.RemoveAt(index);

            Vector3 position = DropPosition();
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            ItemPickup pickup = Instantiate(item.WorldPrefab, position, rotation);
            pickup.name = item.WorldPrefab.name;
            pickup.Definition = item;
            return true;
        }

        /// <summary>キャラの前方の床の上。前に壁があれば手前に寄せる。</summary>
        private Vector3 DropPosition()
        {
            Transform self = transform;
            Vector3 chest = self.position + Vector3.up;
            Vector2 scatter = UnityEngine.Random.insideUnitCircle * dropScatter;
            Vector3 forward = self.forward * dropDistance + new Vector3(scatter.x, 0f, scatter.y);

            float distance = forward.magnitude;
            Vector3 direction = distance > 0f ? forward / distance : self.forward;
            if (Physics.Raycast(chest, direction, out RaycastHit wall, distance, groundMask, QueryTriggerInteraction.Ignore)
                && !wall.transform.IsChildOf(self))
            {
                distance = Mathf.Max(0f, wall.distance - 0.3f);
            }

            Vector3 above = chest + direction * distance;
            if (Physics.Raycast(above, Vector3.down, out RaycastHit ground, 3f, groundMask, QueryTriggerInteraction.Ignore)
                && !ground.transform.IsChildOf(self))
            {
                return ground.point;
            }

            return new Vector3(above.x, self.position.y, above.z);
        }
    }
}
