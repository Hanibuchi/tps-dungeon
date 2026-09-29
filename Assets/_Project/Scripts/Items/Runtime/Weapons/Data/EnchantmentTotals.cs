using System;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 1 本の武器に付いたエンチャントを種類ごとに足し合わせたもの。UnityEngine に依存しない。
    /// 同じ種類が複数付いたら効果量を足す（Amount）。副次の値（爆発の半径など）は大きい方を採る（Secondary）。
    /// </summary>
    public sealed class EnchantmentTotals
    {
        private static readonly int KindCount = Enum.GetValues(typeof(EnchantmentKind)).Length;

        private readonly float[] amounts = new float[KindCount];
        private readonly float[] secondaries = new float[KindCount];
        private readonly int[] stacks = new int[KindCount];

        public static EnchantmentTotals Empty => new EnchantmentTotals();

        /// <summary>効果量の合計。付いていなければ 0。</summary>
        public float Amount(EnchantmentKind kind) => IsValid(kind) ? amounts[(int)kind] : 0f;

        /// <summary>副次の値のうち最大のもの。付いていなければ 0。</summary>
        public float Secondary(EnchantmentKind kind) => IsValid(kind) ? secondaries[(int)kind] : 0f;

        /// <summary>付いている個数。</summary>
        public int Stacks(EnchantmentKind kind) => IsValid(kind) ? stacks[(int)kind] : 0;

        public bool Has(EnchantmentKind kind) => Stacks(kind) > 0;

        public void Add(EnchantmentKind kind, float amount, float secondary = 0f, int count = 1)
        {
            if (!IsValid(kind) || count <= 0) return;

            int i = (int)kind;
            amounts[i] += amount * count;
            secondaries[i] = Math.Max(secondaries[i], secondary);
            stacks[i] += count;
        }

        private static bool IsValid(EnchantmentKind kind) => (int)kind >= 0 && (int)kind < KindCount;
    }
}
