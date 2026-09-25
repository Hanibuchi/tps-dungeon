using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 床に落ちている、インタラクトで拾えるアイテム。照準を合わせると「[E] 拾う」と、右側にアイテムの情報が出る。
    /// コライダを持つ GameObject（か、その親）に付ける。拾われたら自分ごと消える。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Item Pickup")]
    public sealed class ItemPickup : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("拾うと手に入るアイテム。")]
        private ItemDefinition definition;

        [SerializeField] private string pickupLabel = "拾う";
        [SerializeField] private string fullLabel = "持ちきれない";

        // 案内の文言は満杯かどうかで変わるが、PromptLabel は相手を受け取らないので、直前に CanInteract で見た相手を覚えておく。
        private PlayerInventory lastInventory;

        public ItemDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public string PromptLabel => lastInventory != null && !lastInventory.Inventory.HasSpace ? fullLabel : pickupLabel;

        public string DetailTitle => definition != null ? definition.DisplayName : string.Empty;
        public string DetailBody => definition != null ? definition.Description : string.Empty;
        public Texture2D DetailIcon => definition != null ? definition.Icon : null;

        /// <summary>
        /// 持ちきれないときも触れる扱いにして案内を出したままにする。偽にすると案内ごと消え、なぜ拾えないのか分からないため。
        /// </summary>
        public bool CanInteract(GameObject interactor)
        {
            if (definition == null || interactor == null) return false;

            lastInventory = interactor.GetComponent<PlayerInventory>();
            return lastInventory != null;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (!lastInventory.TryAdd(definition)) return;

            // 同じフレームにもう一度拾われないよう、判定から先に外す。
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Destroy(gameObject);
        }
    }
}
