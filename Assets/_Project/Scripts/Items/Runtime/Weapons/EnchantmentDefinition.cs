using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// エンチャント 1 種類の定義。効果量は 1 個あたりで、同じ種類が複数付いたら足される。
    /// 効果の中身（何に効くか）は <see cref="Kind"/> で決まり、読む側（MeleeWeaponStats など）が解釈する。
    /// </summary>
    [CreateAssetMenu(fileName = "Enchantment", menuName = "TPS Dungeon/Weapons/Enchantment")]
    public sealed class EnchantmentDefinition : ScriptableObject
    {
        [SerializeField] private EnchantmentKind kind;

        [SerializeField, Tooltip("画面に出す名前。")]
        private string displayName;

        [SerializeField, TextArea(1, 4), Tooltip("情報欄に出す説明。")]
        private string description;

        [SerializeField, Tooltip("1 個あたりの効果量。割合は 0.15 で +15%。値は仮。")]
        private float amount;

        [SerializeField, Tooltip("副次の値（爆発なら半径 m）。複数付いても足さず、大きい方を使う。")]
        private float secondaryAmount;

        public EnchantmentKind Kind => kind;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? kind.ToString() : displayName;
        public string Description => description ?? string.Empty;
        public float Amount => amount;
        public float SecondaryAmount => secondaryAmount;
    }
}
