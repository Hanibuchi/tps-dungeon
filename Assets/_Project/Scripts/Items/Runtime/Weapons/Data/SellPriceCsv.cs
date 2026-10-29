using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ランクごとの売値の表（SellPrices.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = ランク 1 つ。weapon はそのランクの武器に、enchantment はそのランクのエンチャント 1 つに足す金額（0 以上の整数）。
    /// 例: <c>C,60,20</c>（C の武器は 60、C のエンチャント 1 つにつき 20）。ランクは E・D・C・B・A・S・U（Unique でもよい）。
    /// 武器だけの金額は Weapons.csv の price で、売値はそれと合わせた合計（<see cref="WeaponPrice"/>）。
    /// 先頭の見出し行（rank で始まる）・空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class SellPriceCsv
    {
        public const string Header = "rank,weapon,enchantment";

        public readonly struct Row
        {
            public readonly int Line;
            public readonly WeaponRank Rank;
            public readonly int Weapon;
            public readonly int Enchantment;

            public Row(int line, WeaponRank rank, int weapon, int enchantment)
            {
                Line = line;
                Rank = rank;
                Weapon = weapon;
                Enchantment = enchantment;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            var seen = new HashSet<WeaponRank>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("rank", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length < 3)
                {
                    errors?.Add($"{line} 行目: 列が足りない（{Header}）: {raw}");
                    continue;
                }

                if (!WeaponCsv.TryParseRank(cells[0], out WeaponRank rank))
                {
                    errors?.Add($"{line} 行目: ランク「{cells[0]}」が読めない（E・D・C・B・A・S・U）");
                    continue;
                }

                if (!TryParsePrice(cells[1], out int weapon) || !TryParsePrice(cells[2], out int enchantment))
                {
                    errors?.Add($"{line} 行目: {WeaponRanks.Label(rank)} の金額は 0 以上の整数: {raw}");
                    continue;
                }

                if (!seen.Add(rank))
                {
                    errors?.Add($"{line} 行目: ランク {WeaponRanks.Label(rank)} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, rank, weapon, enchantment));
            }

            return rows;
        }

        private static bool TryParsePrice(string cell, out int price)
        {
            if (cell.Length == 0)
            {
                price = 0;
                return true;
            }

            return int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out price) && price >= 0;
        }
    }
}
