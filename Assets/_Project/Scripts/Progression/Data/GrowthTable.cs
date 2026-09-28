using System.Collections.Generic;
using System.Text;

namespace TpsDungeon.Progression
{
    /// <summary>バランス確認用に、レベルごとの MaxHP・基礎攻撃力・必要経験値・累計経験値を表にする。</summary>
    public static class GrowthTable
    {
        /// <summary>1, 10, 20, …, 90 と最大レベル。</summary>
        public static IEnumerable<int> DefaultLevels(GrowthCurve curve)
        {
            yield return GrowthCurve.MinLevel;
            for (int level = 10; level < curve.MaxLevel; level += 10) yield return level;
            if (curve.MaxLevel > GrowthCurve.MinLevel) yield return curve.MaxLevel;
        }

        /// <summary>Markdown の表。「累計」はそのレベルに到達するまでの合計。</summary>
        public static string Format(GrowthCurve curve, IEnumerable<int> levels = null)
        {
            var text = new StringBuilder();
            text.AppendLine("| L | MaxHP | 基礎攻撃力 | 次まで | 累計 |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (int level in levels ?? DefaultLevels(curve))
            {
                int next = curve.ExpToNext(level);
                text.Append("| ").Append(level)
                    .Append(" | ").Append(curve.MaxHp(level))
                    .Append(" | ").Append(curve.BaseAttack(level).ToString("0.0"))
                    .Append(" | ").Append(next > 0 ? next.ToString("N0") : "—")
                    .Append(" | ").Append(curve.TotalExpToReach(level).ToString("N0"))
                    .AppendLine(" |");
            }
            return text.ToString();
        }
    }
}
