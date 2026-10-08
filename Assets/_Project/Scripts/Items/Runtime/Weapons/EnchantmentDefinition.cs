using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// エンチャント 1 種類の定義。効果量は 1 段あたりの固定の値で、付いた段を掛ける（2 段なら 2 倍）。
    /// どの武器種にどの段が何ランクで付くかは武器種のランク表（<see cref="WeaponTypeDefinition.EnchantmentRanks"/>）が持つ。
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

        [SerializeField, Tooltip("1 段あたりの効果量。割合は 0.2 で +20%。値は仮。")]
        private float amount;

        [SerializeField, Tooltip("副次の値（爆発なら半径 m）。段では増えず、お守りなどと重なったら大きい方を使う。")]
        private float secondaryAmount;

        public EnchantmentKind Kind => kind;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? kind.ToString() : displayName;
        public string Description => description ?? string.Empty;
        public float Amount => amount;
        public float SecondaryAmount => secondaryAmount;
    }
}
