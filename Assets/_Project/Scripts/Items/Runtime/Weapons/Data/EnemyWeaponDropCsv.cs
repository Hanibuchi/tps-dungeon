using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 敵ごとに落とす武器のランクの確率の表（CSV）を読む。UnityEngine に依存しない。
    /// 1 行 = 敵 1 体。ランク E〜S の列に、倒したときそのランクの武器を落とす確率を 0〜1 の小数で書く（0.1 で 10%）。空欄は 0。
    /// 例: <c>goblin,0.08,0.03,0.01,,,</c>（ゴブリンは E を 8%、D を 3%、C を 1%。残りの 88% は何も落とさない）。
    /// 合計は 1 まで。
    /// 8 列目 count（省いてよい）に 1 以上の数を書くと、倒したとき必ず count 本落とす（ボス用）。
    /// このときランクの列は確率ではなく比率として扱い、1 本ごとに比率でランクを振る。
    /// 先頭の見出し行（enemy で始まる）・空行・# で始まる行は飛ばす。
    /// 読めないセルや行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnemyWeaponDropCsv
    {
        public const string Header = "enemy,E,D,C,B,A,S,count";

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Enemy;

            /// <summary>E〜S（<see cref="EnchantmentRankCsv.RankColumns"/> の並び）を落とす確率。0〜1。</summary>
            public readonly float[] Chances;

            /// <summary>必ず落とす本数。0 なら確率で多くて 1 本。</summary>
            public readonly int Count;

            public Row(int line, string enemy, float[] chances, int count = 0)
            {
                Line = line;
                Enemy = enemy;
                Chances = chances;
                Count = count;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            WeaponRank[] columns = EnchantmentRankCsv.RankColumns;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("enemy", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length > 2 + columns.Length)
                {
                    errors?.Add($"{line} 行目: 列が多すぎる（敵,E,D,C,B,A,S,count）: {raw}");
                    continue;
                }

                string enemy = cells[0];
                if (enemy.Length == 0)
                {
                    errors?.Add($"{line} 行目: 敵の id が空");
                    continue;
                }

                var chances = new float[columns.Length];
                bool ok = true;
                double total = 0;
                for (int r = 0; r < columns.Length && 1 + r < cells.Length; r++)
                {
                    string cell = cells[1 + r];
                    if (cell.Length == 0) continue;

                    if (!TryParseChance(cell, out float chance))
                    {
                        errors?.Add($"{line} 行目: {columns[r]} の列の確率は 0〜1 の小数「{cell}」");
                        ok = false;
                        break;
                    }

                    chances[r] = chance;
                    total += chance;
                }

                if (!ok) continue;

                int count = 0;
                int countIndex = 1 + columns.Length;
                if (countIndex < cells.Length && cells[countIndex].Length > 0 &&
                    (!int.TryParse(cells[countIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 0))
                {
                    errors?.Add($"{line} 行目: count の列は 0 以上の整数「{cells[countIndex]}」");
                    continue;
                }

                if (count > 0 && total <= 0)
                {
                    errors?.Add($"{line} 行目: {enemy} は count {count} 本落とすが、ランクの比率がどれも 0");
                    continue;
                }

                // 必ず落とす行は比率なので、合計が 1 を超えてもよい。
                if (count == 0 && total > 1.0 + 1e-6)
                {
                    errors?.Add($"{line} 行目: {enemy} の確率の合計が 1 を超えている（{total:0.####}）");
                    continue;
                }

                if (!seen.Add(enemy))
                {
                    errors?.Add($"{line} 行目: 敵 {enemy} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, enemy, chances, count));
            }

            return rows;
        }

        /// <summary>「0.1」のような 0〜1 の小数を確率として読む。負の値や 1 を超える値は偽。</summary>
        internal static bool TryParseChance(string text, out float chance)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out chance)) return false;
            return !float.IsNaN(chance) && chance >= 0f && chance <= 1f;
        }
    }
}
