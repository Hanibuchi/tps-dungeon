using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種ごとのエンチャントのランク表（CSV）を読む。UnityEngine に依存しない。
    /// 1 行 = 武器種 1 つ × 種類 1 つ。ランク E〜S の列に、そのランクで付く段（1, 2, 3…）を書く。空欄ならそのランクでは付かない。
    /// 例: <c>01,DamageUp,1,2,3,,,</c>（片手剣のダメージ増加は 1 段が E、2 段が D、3 段が C）。
    /// 先頭の見出し行（weaponType で始まる）・空行・# で始まる行は飛ばす。
    /// 読めないセルや行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnchantmentRankCsv
    {
        public const string Header = "weaponType,kind,E,D,C,B,A,S";

        /// <summary>ランクの列の並び（3 列目から）。</summary>
        public static readonly WeaponRank[] RankColumns = { WeaponRank.E, WeaponRank.D, WeaponRank.C, WeaponRank.B, WeaponRank.A, WeaponRank.S };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string WeaponType;
            public readonly EnchantmentKind Kind;
            public readonly int Level;
            public readonly WeaponRank Rank;

            public Row(int line, string weaponType, EnchantmentKind kind, int level, WeaponRank rank)
            {
                Line = line;
                WeaponType = weaponType;
                Kind = kind;
                Level = level;
                Rank = rank;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            var seen = new HashSet<(string, EnchantmentKind)>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('\uFEFF');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("weaponType", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length < 2)
                {
                    errors?.Add($"{line} 行目: 列が足りない（武器種,種類,E,D,C,B,A,S）: {raw}");
                    continue;
                }

                if (cells.Length > 2 + RankColumns.Length)
                {
                    errors?.Add($"{line} 行目: 列が多すぎる（武器種,種類,E,D,C,B,A,S）: {raw}");
                    continue;
                }

                string type = cells[0];
                if (type.Length == 0)
                {
                    errors?.Add($"{line} 行目: 武器種の番号が空");
                    continue;
                }

                if (!TryParseKind(cells[1], out EnchantmentKind kind))
                {
                    errors?.Add($"{line} 行目: 知らないエンチャントの種類「{cells[1]}」");
                    continue;
                }

                if (!seen.Add((type, kind)))
                {
                    errors?.Add($"{line} 行目: 武器種 {type} の {kind} の行が重複している");
                    continue;
                }

                var levels = new HashSet<int>();
                for (int r = 0; r < RankColumns.Length && 2 + r < cells.Length; r++)
                {
                    string cell = cells[2 + r];
                    if (cell.Length == 0) continue;

                    WeaponRank rank = RankColumns[r];
                    if (!int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level < 1)
                    {
                        errors?.Add($"{line} 行目: {rank} の列の段は 1 以上の整数「{cell}」");
                        continue;
                    }

                    if (!levels.Add(level))
                    {
                        errors?.Add($"{line} 行目: {kind} の {level} 段が 2 つのランクに書かれている");
                        continue;
                    }

                    rows.Add(new Row(line, type, kind, level, rank));
                }
            }

            return rows;
        }

        private static bool TryParseKind(string text, out EnchantmentKind kind)
        {
            kind = default;
            if (string.IsNullOrEmpty(text) || char.IsDigit(text[0]) || text[0] == '-') return false;
            return Enum.TryParse(text, true, out kind) && Enum.IsDefined(typeof(EnchantmentKind), kind);
        }
    }
}
