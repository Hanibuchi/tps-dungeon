using System;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// キャラ 1 人の持ち物の窓口。手持ち（ホットバーの 4 枠、<see cref="Inventory"/>）はキャラごとに持ち、
    /// バッグ（<see cref="Bag"/>）はパーティーで 1 つを共有する（Party が全員に同じものを渡す）。
    /// パーティーが無いとき（1 人だけのシーンやテスト）は、自分用のバッグを作って持つ。
    /// どの枠を選んでいるかは PlayerHotbar が持つ。
    /// ゲーム中に捨てるキーを押すと、PlayerHotbar で選んでいる枠（手に持っているもの）を捨てる。
    /// キャラのルート（PlayerHotbar と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Inventory")]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("パーティーが無いときに自分で持つバッグの枠の数。パーティーではバッグを共有するので使わない。")]
        private int bagCapacity = 12;

        [SerializeField, Tooltip("捨てたものを置く、キャラの前方への距離（メートル）。")]
        private float dropDistance = 1f;

        [SerializeField, Tooltip("捨てたものが重ならないよう、置く位置を散らす半径（メートル）。")]
        private float dropScatter = 0.25f;

        [SerializeField, Tooltip("捨てる位置の床を探すレイと、前の壁を避けるレイが当たるレイヤー。")]
        private LayerMask groundMask = ~0;

        private Inventory inventory;
        private Inventory bag;
        private PlayerHotbar hotbar;

        /// <summary>手持ち（ホットバーの PlayerHotbar.SlotCount 枠）。バッグの枠は持たない。</summary>
        public Inventory Inventory
        {
            get
            {
                EnsureInventory();
                return inventory;
            }
        }

        /// <summary>
        /// バッグ。パーティーでは全員が同じものを持つ。差し替えると Changed が飛ぶ。
        /// null を入れると自分用のバッグを作り直す。
        /// </summary>
        public Inventory Bag
        {
            get
            {
                EnsureBag();
                return bag;
            }
            set
            {
                if (value == bag && value != null) return;
                if (bag != null) bag.Changed -= OnItemsChanged;
                bag = value ?? new Inventory(0, bagCapacity);
                bag.Changed += OnItemsChanged;
                Changed?.Invoke(this);
            }
        }

        /// <summary>手持ちかバッグのどこかに空きがあるか。</summary>
        public bool HasSpace => Inventory.HasSpace || Bag.HasSpace;

        /// <summary>手持ちかバッグの中身、またはバッグの差し替えで飛ぶ。</summary>
        public event Action<PlayerInventory> Changed;

        private void Awake()
        {
            EnsureInventory();
            EnsureBag();
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

        private void OnItemsChanged(Inventory _) => Changed?.Invoke(this);

        private void EnsureInventory()
        {
            if (inventory != null) return;

            inventory = new Inventory(PlayerHotbar.SlotCount, 0);
            inventory.Changed += OnItemsChanged;
        }

        private void EnsureBag()
        {
            if (bag != null) return;

            bag = new Inventory(0, bagCapacity);
            bag.Changed += OnItemsChanged;
        }

        /// <summary>拾ったものを入れる。手持ちの左から空きを探し、埋まっていればバッグの先頭から。入らなければ false。</summary>
        public bool TryAdd(ItemInstance item)
        {
            if (item == null) return false;
            return Inventory.TryAdd(item) >= 0 || Bag.TryAdd(item) >= 0;
        }

        /// <summary>手持ちの index 番目の枠のものを足元に捨てる。</summary>
        public bool Drop(int index) => Drop(Inventory, index);

        /// <summary>
        /// source（手持ちかバッグ）の index 番目の枠のものを、このキャラの足元（少し前）に捨て、拾える物として置く。
        /// ポーズ中（timeScale 0）でも置けるよう、物理は使わず位置を決めて置くだけにする。
        /// </summary>
        public bool Drop(Inventory source, int index)
        {
            if (source == null) return false;
            ItemInstance item = source[index];
            if (item == null) return false;

            if (item.WorldPrefab == null)
            {
                Debug.LogWarning($"{item.DisplayName} には捨てたときに出す物が無いので捨てられない", item.Definition);
                return false;
            }

            source.RemoveAt(index);

            Vector3 position = DropPosition();
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            ItemPickup pickup = Instantiate(item.WorldPrefab, position, rotation);
            pickup.name = item.WorldPrefab.name;
            pickup.Instance = item;
            pickup.PlayDropSound();
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
