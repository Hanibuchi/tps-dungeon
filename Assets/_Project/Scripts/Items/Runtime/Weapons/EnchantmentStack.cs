namespace TpsDungeon.Items
{
    /// <summary>武器に付いたエンチャント 1 種類と、その段。効果量は 1 段あたりの値 × 段。</summary>
    public readonly struct EnchantmentStack
    {
        public readonly EnchantmentDefinition Definition;
        public readonly int Level;

        public EnchantmentStack(EnchantmentDefinition definition, int level)
        {
            Definition = definition;
            Level = level;
        }
    }
}
