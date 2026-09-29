using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 床に落ちている、インタラクトで拾えるアイテム。照準を合わせると「[E] 拾う」と、右側にアイテムの情報が出る。
    /// コライダを持つ GameObject（か、その親）に付ける。拾われたら自分ごと消える。
    /// シーンに置いたものは定義だけを持ち、初めて見られたときに個体を作る（武器ならこのときエンチャントが決まる）。
    /// 捨てたものは個体ごと渡されるので、エンチャントは捨てる前のまま。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Item Pickup")]
    public sealed class ItemPickup : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("拾うと手に入るアイテム。")]
        private ItemDefinition definition;

        [SerializeField, Tooltip("武器のランクを情報欄で色付けするための表。未設定なら色を付けない。")]
        private WeaponRankTable rankTable;

        [SerializeField] private string pickupLabel = "拾う";
        [SerializeField] private string fullLabel = "持ちきれない";

        private static readonly System.Random Rng = new System.Random();

        private ItemInstance instance;

        // 案内の文言は満杯かどうかで変わるが、PromptLabel は相手を受け取らないので、直前に CanInteract で見た相手を覚えておく。
        private PlayerInventory lastInventory;

        public ItemDefinition Definition
        {
            get => instance != null ? instance.Definition : definition;
            set
            {
                definition = value;
                instance = null;
            }
        }

        /// <summary>拾うと手に入る個体。まだ無ければ定義から作る。</summary>
        public ItemInstance Instance
        {
            get
            {
                if (instance == null && definition != null) instance = ItemInstance.Create(definition, Rng);
                return instance;
            }
            set
            {
                instance = value;
                definition = value != null ? value.Definition : null;
            }
        }

        public string PromptLabel => lastInventory != null && !lastInventory.Inventory.HasSpace ? fullLabel : pickupLabel;

        public string DetailTitle => Instance != null ? Instance.DisplayName : string.Empty;
        public string DetailBody => Instance != null ? Instance.DetailText(rankTable, lastInventory != null ? lastInventory.OwnerAttack : 0f) : string.Empty;
        public Texture2D DetailIcon => Instance != null ? Instance.Icon : null;

        /// <summary>
        /// 持ちきれないときも触れる扱いにして案内を出したままにする。偽にすると案内ごと消え、なぜ拾えないのか分からないため。
        /// </summary>
        public bool CanInteract(GameObject interactor)
        {
            if (Definition == null || interactor == null) return false;

            lastInventory = interactor.GetComponent<PlayerInventory>();
            return lastInventory != null;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (!lastInventory.TryAdd(Instance)) return;

            // 同じフレームにもう一度拾われないよう、判定から先に外す。
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Destroy(gameObject);
        }
    }
}
