namespace TpsDungeon.Items
{
    /// <summary>武器に付いたエンチャント 1 種類と、その個数。</summary>
    public readonly struct EnchantmentStack
    {
        public readonly EnchantmentDefinition Definition;
        public readonly int Count;

        public EnchantmentStack(EnchantmentDefinition definition, int count)
        {
            Definition = definition;
            Count = count;
        }
    }
}
