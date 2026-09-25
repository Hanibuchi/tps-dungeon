using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// アイテム 1 種類の定義。インベントリの枠にはこれへの参照が入る（重ね持ちはしないので個数は持たない）。
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "TPS Dungeon/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("セーブなどで使う識別子。種類ごとに重ならないようにする。")]
        private string id;

        [SerializeField, Tooltip("画面に出す名前。")]
        private string displayName;

        [SerializeField, TextArea(2, 6), Tooltip("情報欄に出す説明。")]
        private string description;

        [SerializeField, Tooltip("枠と情報欄に出す絵。")]
        private Texture2D icon;

        [SerializeField, Tooltip("捨てたときに足元に出す、拾える物のプレハブ（ItemPickup 付き）。")]
        private ItemPickup worldPrefab;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public Texture2D Icon => icon;
        public ItemPickup WorldPrefab => worldPrefab;
    }
}
