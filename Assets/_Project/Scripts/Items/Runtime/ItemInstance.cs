using System;
using System.Collections.Generic;
using System.Text;
using TpsDungeon.Player;
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

        /// <summary>enchantments は 1 つにつき 1 段。同じものを並べた数が段になる。</summary>
        public ItemInstance(ItemDefinition definition, IEnumerable<EnchantmentDefinition> enchantments = null)
            : this(definition, ToStacks(enchantments))
        {
        }

        /// <summary>種類と段を直接渡す。同じ種類が並んだら段を足す。</summary>
        public ItemInstance(ItemDefinition definition, IEnumerable<EnchantmentStack> enchantments)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            this.enchantments = Group(enchantments);
        }

        public ItemDefinition Definition { get; }

        /// <summary>武器なら定義、そうでなければ null。</summary>
        public WeaponDefinition Weapon => Definition as WeaponDefinition;

        /// <summary>付いたエンチャント。同じ種類は 1 つにまとめて段で持つ。並びは最初に付いた順。</summary>
        public IReadOnlyList<EnchantmentStack> Enchantments => enchantments;

        public string DisplayName => Definition.DisplayName;
        public Texture2D Icon => Definition.Icon;
        public ItemPickup WorldPrefab => Definition.WorldPrefab;

        /// <summary>
        /// 定義から新しい個体を作る。武器なら武器種のランク表から、決まった確率でエンチャントを振る（<see cref="EnchantmentRoller"/>）。
        /// ユニークは振らず、定義に書いた固定のエンチャントを付ける（何本拾っても同じ）。
        /// </summary>
        public static ItemInstance Create(ItemDefinition definition, System.Random random)
        {
            if (definition == null) return null;

            var weapon = definition as WeaponDefinition;
            if (weapon != null && weapon.HasFixedEnchantments)
            {
                var fixedStacks = new List<EnchantmentStack>();
                foreach (FixedEnchantmentEntry entry in weapon.FixedEnchantments) fixedStacks.Add(new EnchantmentStack(entry.Definition, entry.Level));
                return new ItemInstance(definition, fixedStacks);
            }

            WeaponTypeDefinition type = weapon?.WeaponType;
            EnchantmentRollSettings settings = type?.EnchantmentRoll;
            if (settings == null) return new ItemInstance(definition);

            var entries = new List<EnchantmentRankEntry>();
            var options = new List<EnchantmentRoller.Option>();
            foreach (EnchantmentRankEntry entry in type.EnchantmentRanks)
            {
                if (entry.Definition == null || entry.Level < 1 || entry.Rank > WeaponRank.S) continue;
                entries.Add(entry);
                options.Add(new EnchantmentRoller.Option((int)entry.Definition.Kind, (int)entry.Rank));
            }

            var rolled = new List<EnchantmentStack>();
            foreach (int index in EnchantmentRoller.Roll(options, settings.ContinueChance, settings.RankWeights,
                         settings.MaxEnchantments, random))
            {
                rolled.Add(new EnchantmentStack(entries[index].Definition, entries[index].Level));
            }

            return new ItemInstance(definition, rolled);
        }

        /// <summary>エンチャントを種類ごとに足し合わせたもの。</summary>
        public EnchantmentTotals EnchantmentTotals()
        {
            var totals = new EnchantmentTotals();
            foreach (EnchantmentStack stack in enchantments)
            {
                totals.Add(stack.Definition.Kind, stack.Definition.Amount, stack.Definition.SecondaryAmount, stack.Level);
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
        /// 盾は攻撃力の代わりに防御力を、治癒持続・治癒は回復量を、召喚はおとりの体力を、ダメージ軽減は加護の効き目を出し、お守りは強さを出さない。
        /// お守り・盾・召喚・治癒持続・ダメージ軽減・治癒は効き方を一行添える。
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
            WeaponTypeDefinition type = weapon.WeaponType;
            if (type != null && type.IsShield) text.Append($"\n防御力 {weapon.Strength:0.#}");
            else if (type != null && type.IsHealField) text.Append($"\n回復量 {weapon.Strength:0.#}/秒");
            else if (type != null && type.IsSummon) text.Append($"\nおとりの体力 {SummonHealth(weapon)}");
            else if (type != null && type.IsBuff) text.Append("\n").Append(BlessingText(weapon));
            else if (type != null && type.IsHeal) text.Append($"\n回復量 {weapon.Strength:0.#}");
            else if (type == null || !type.IsCharm) text.Append($"\n攻撃力 {weapon.Strength:0.#}");

            foreach (EnchantmentStack stack in enchantments)
            {
                EnchantmentDefinition e = stack.Definition;
                text.Append("\n・").Append(EnchantmentLabel.Format(e.Kind, e.DisplayName, e.Amount * stack.Level, e.SecondaryAmount));
            }

            string usage = type != null && type.IsBuff
                ? $"近くの仲間のうち、{BlessingName(weapon.BlessingKind)}の膜が付いていない人を選んで膜を張る。"
                : GearUsage(type);
            if (!string.IsNullOrEmpty(usage)) text.Append("\n\n").Append(usage);
            if (!string.IsNullOrEmpty(Definition.Description)) text.Append("\n\n").Append(Definition.Description);
            return text.ToString();
        }

        /// <summary>召喚した置物 1 体の体力（強さ × 武器種の係数、四捨五入、最低 1）。</summary>
        public static int SummonHealth(WeaponDefinition weapon)
        {
            if (weapon == null || weapon.WeaponType == null) return 1;
            return Math.Max(1, (int)Math.Round(weapon.Strength * weapon.WeaponType.SummonHealthPerStrength, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 加護（ダメージ軽減）の効き目と続く時間（エンチャントの補正前）。武器ごとの種類で
        /// 「被ダメージ −15%（12 秒）」「クリティカル倍率 +0.5（12 秒）」「状態異常耐性 40%（12 秒）」のように出す。
        /// </summary>
        public static string BlessingText(WeaponDefinition weapon)
        {
            WeaponTypeDefinition type = weapon != null ? weapon.WeaponType : null;
            if (type == null || !type.IsBuff) return string.Empty;
            BlessingKind kind = weapon.BlessingKind;
            float amount = Math.Min(Math.Max(0f, weapon.Strength) * type.BlessingPerStrength(kind), type.BlessingCap(kind));
            string effect = kind switch
            {
                BlessingKind.DamageReduction => $"被ダメージ −{amount * 100f:0.#}%",
                BlessingKind.CritMultiplier => $"クリティカル倍率 +{amount:0.##}",
                _ => $"状態異常耐性 {amount * 100f:0.#}%",
            };
            return $"{effect}（{type.BlessingDuration:0.#} 秒）";
        }

        /// <summary>加護の種類の名前（「ダメージ軽減」など）。</summary>
        public static string BlessingName(BlessingKind kind) => kind switch
        {
            BlessingKind.DamageReduction => "ダメージ軽減",
            BlessingKind.CritMultiplier => "クリティカル倍率上昇",
            _ => "状態異常耐性",
        };

        /// <summary>お守り・盾・召喚・治癒持続・治癒の効き方の一行（ダメージ軽減は武器ごとに DetailText で）。それ以外は空。</summary>
        private static string GearUsage(WeaponTypeDefinition type)
        {
            if (type == null) return string.Empty;
            if (type.IsSummon) return "狙った地面に、敵を引きつける置物を呼び出す。呼び直すと前の分は消える。";
            if (type.IsHealField) return "狙った地面に種を投げ、落ちた所に治癒の場を張る。";
            if (type.IsHeal) return "近くの仲間のうち、体力が減っている人を選んで回復する。";
            if (type.IsCharm) return "ホットバーに入れておくだけで効く。";
            if (type.IsShield) return "ホットバーに入れて、片手武器を持っている間だけ効く（盾が複数あれば防御力の高い 1 枚）。";
            return string.Empty;
        }

        private static IEnumerable<EnchantmentStack> ToStacks(IEnumerable<EnchantmentDefinition> source)
        {
            if (source == null) yield break;
            foreach (EnchantmentDefinition e in source) yield return new EnchantmentStack(e, 1);
        }

        private static EnchantmentStack[] Group(IEnumerable<EnchantmentStack> source)
        {
            if (source == null) return Array.Empty<EnchantmentStack>();

            var order = new List<EnchantmentDefinition>();
            var levels = new Dictionary<EnchantmentDefinition, int>();
            foreach (EnchantmentStack stack in source)
            {
                EnchantmentDefinition e = stack.Definition;
                if (e == null || stack.Level <= 0) continue;
                if (levels.TryGetValue(e, out int n)) levels[e] = n + stack.Level;
                else
                {
                    levels[e] = stack.Level;
                    order.Add(e);
                }
            }

            var result = new EnchantmentStack[order.Count];
            for (int i = 0; i < order.Count; i++) result[i] = new EnchantmentStack(order[i], levels[order[i]]);
            return result;
        }
    }
}
