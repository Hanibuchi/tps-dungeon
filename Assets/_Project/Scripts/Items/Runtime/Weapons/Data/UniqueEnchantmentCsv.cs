using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ユニーク武器に必ず付くエンチャントの表（CSV）を読む。UnityEngine に依存しない。
    /// 1 行 = 「武器の id, 種類, 段」。例: <c>Weapon_ShadowRunnerBlade,CritChance,3</c>（影駆けの魔剣にクリティカル率 3 段）。
    /// 先頭の見出し行（weapon で始まる）・空行・# で始まる行は飛ばす。
    /// 読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class UniqueEnchantmentCsv
    {
        public const string Header = "weapon,kind,level";

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Weapon;
            public readonly EnchantmentKind Kind;
            public readonly int Level;

            public Row(int line, string weapon, EnchantmentKind kind, int level)
            {
                Line = line;
                Weapon = weapon;
                Kind = kind;
                Level = level;
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
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("weapon", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length != 3)
                {
                    errors?.Add($"{line} 行目: 列は 3 つ（武器の id,種類,段）: {raw}");
                    continue;
                }

                string weapon = cells[0];
                if (weapon.Length == 0)
                {
                    errors?.Add($"{line} 行目: 武器の id が空");
                    continue;
                }

                if (!EnchantmentRankCsv.TryParseKind(cells[1], out EnchantmentKind kind))
                {
                    errors?.Add($"{line} 行目: 知らないエンチャントの種類「{cells[1]}」");
                    continue;
                }

                if (!int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level < 1)
                {
                    errors?.Add($"{line} 行目: 段は 1 以上の整数「{cells[2]}」");
                    continue;
                }

                if (!seen.Add((weapon, kind)))
                {
                    errors?.Add($"{line} 行目: {weapon} の {kind} が重複している（段は 1 行にまとめる）");
                    continue;
                }

                rows.Add(new Row(line, weapon, kind, level));
            }

            return rows;
        }
    }
}
