using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 持ち物の 1 個。定義（ItemDefinition）と、その個体だけのもの（武器のエンチャント）を持つ。
    /// 同じ定義から作っても個体は別物で、インベントリの枠や床に落ちた物はこれを持ち回る。
    /// エンチャントの無いアイテム（宝石・鍵など）は定義を包むだけ。
    /// </summary>
    public sealed class ItemInstance
    {
        private readonly EnchantmentStack[] enchantments;

        public ItemInstance(ItemDefinition definition, IEnumerable<EnchantmentDefinition> enchantments = null)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            this.enchantments = Group(enchantments);
        }

        public ItemDefinition Definition { get; }

        /// <summary>武器なら定義、そうでなければ null。</summary>
        public WeaponDefinition Weapon => Definition as WeaponDefinition;

        /// <summary>付いたエンチャント。同じ種類はまとめて個数で持つ。並びは最初に付いた順。</summary>
        public IReadOnlyList<EnchantmentStack> Enchantments => enchantments;

        public string DisplayName => Definition.DisplayName;
        public Texture2D Icon => Definition.Icon;
        public ItemPickup WorldPrefab => Definition.WorldPrefab;

        /// <summary>
        /// 定義から新しい個体を作る。武器なら武器種の候補から、決まった確率でエンチャントを振る。
        /// </summary>
        public static ItemInstance Create(ItemDefinition definition, System.Random random)
        {
            if (definition == null) return null;

            WeaponTypeDefinition type = (definition as WeaponDefinition)?.WeaponType;
            if (type == null || type.EnchantmentRoll == null) return new ItemInstance(definition);

            IReadOnlyList<EnchantmentDefinition> candidates = type.AllowedEnchantments;
            var rolled = new List<EnchantmentDefinition>();
            foreach (int index in EnchantmentRoller.Roll(candidates.Count, type.EnchantmentRoll.ContinueChance, random))
            {
                if (candidates[index] != null) rolled.Add(candidates[index]);
            }

            return new ItemInstance(definition, rolled);
        }

        /// <summary>エンチャントを種類ごとに足し合わせたもの。</summary>
        public EnchantmentTotals EnchantmentTotals()
        {
            var totals = new EnchantmentTotals();
            foreach (EnchantmentStack stack in enchantments)
            {
                totals.Add(stack.Definition.Kind, stack.Definition.Amount, stack.Definition.SecondaryAmount, stack.Count);
            }

            return totals;
        }

        /// <summary>武器ならランクの色（<see cref="WeaponRankTable.Default"/>）を返す。武器でないか表が無ければ偽。</summary>
        public bool TryGetRankColor(out Color color)
        {
            color = default;
            WeaponDefinition weapon = Weapon;
            WeaponRankTable table = WeaponRankTable.Default;
            if (weapon == null || table == null) return false;

            color = table.ColorOf(weapon.Rank);
            return true;
        }

        /// <summary>
        /// 情報欄の本文。武器なら「ランク」「攻撃力」「エンチャント」を説明の前に並べる。
        /// ランクは表（<see cref="WeaponRankTable.Default"/>）があればその色で塗る（リッチテキスト）。
        /// </summary>
        public string DetailText()
        {
            WeaponDefinition weapon = Weapon;
            if (weapon == null) return Definition.Description;

            var text = new StringBuilder();
            string rank = WeaponRanks.Label(weapon.Rank);
            if (TryGetRankColor(out Color color)) rank = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{rank}</color>";
            text.Append("ランク ").Append(rank);
            text.Append($"\n攻撃力 {weapon.Strength:0.#}");

            foreach (EnchantmentStack stack in enchantments)
            {
                EnchantmentDefinition e = stack.Definition;
                text.Append("\n・").Append(EnchantmentLabel.Format(e.Kind, e.DisplayName, e.Amount * stack.Count, e.SecondaryAmount));
            }

            if (!string.IsNullOrEmpty(Definition.Description)) text.Append("\n\n").Append(Definition.Description);
            return text.ToString();
        }

        private static EnchantmentStack[] Group(IEnumerable<EnchantmentDefinition> source)
        {
            if (source == null) return Array.Empty<EnchantmentStack>();

            var order = new List<EnchantmentDefinition>();
            var counts = new Dictionary<EnchantmentDefinition, int>();
            foreach (EnchantmentDefinition e in source)
            {
                if (e == null) continue;
                if (counts.TryGetValue(e, out int n)) counts[e] = n + 1;
                else
                {
                    counts[e] = 1;
                    order.Add(e);
                }
            }

            var result = new EnchantmentStack[order.Count];
            for (int i = 0; i < order.Count; i++) result[i] = new EnchantmentStack(order[i], counts[order[i]]);
            return result;
        }
    }
}
