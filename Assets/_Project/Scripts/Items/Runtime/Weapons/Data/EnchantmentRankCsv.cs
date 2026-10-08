using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種ごとのエンチャントのランク表（CSV）を読む。UnityEngine に依存しない。
    /// 1 行 = 「武器種の番号, 種類, 段, ランク」。例: <c>01,DamageUp,2,C</c>（片手剣のダメージ増加 2 段はランク C）。
    /// 先頭の見出し行（weaponType で始まる）・空行・# で始まる行は飛ばす。
    /// 読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnchantmentRankCsv
    {
        public const string Header = "weaponType,kind,level,rank";

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

            var seen = new HashSet<(string, EnchantmentKind, int)>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("weaponType", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length < 4)
                {
                    errors?.Add($"{line} 行目: 列が足りない（武器種,種類,段,ランク）: {raw}");
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

                if (!int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level < 1)
                {
                    errors?.Add($"{line} 行目: 段は 1 以上の整数「{cells[2]}」");
                    continue;
                }

                if (!TryParseRank(cells[3], out WeaponRank rank))
                {
                    errors?.Add($"{line} 行目: ランクは E・D・C・B・A・S のどれか「{cells[3]}」");
                    continue;
                }

                if (!seen.Add((type, kind, level)))
                {
                    errors?.Add($"{line} 行目: 武器種 {type} の {kind} {level} 段が重複している");
                    continue;
                }

                rows.Add(new Row(line, type, kind, level, rank));
            }

            return rows;
        }

        private static bool TryParseKind(string text, out EnchantmentKind kind)
        {
            kind = default;
            if (string.IsNullOrEmpty(text) || char.IsDigit(text[0]) || text[0] == '-') return false;
            return Enum.TryParse(text, true, out kind) && Enum.IsDefined(typeof(EnchantmentKind), kind);
        }

        private static bool TryParseRank(string text, out WeaponRank rank)
        {
            rank = default;
            if (text == null || text.Length != 1) return false;
            switch (char.ToUpperInvariant(text[0]))
            {
                case 'E': rank = WeaponRank.E; return true;
                case 'D': rank = WeaponRank.D; return true;
                case 'C': rank = WeaponRank.C; return true;
                case 'B': rank = WeaponRank.B; return true;
                case 'A': rank = WeaponRank.A; return true;
                case 'S': rank = WeaponRank.S; return true;
                default: return false;
            }
        }
    }
}
