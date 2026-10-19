using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの、箱（木箱・宝箱）から武器が出たときのランクの重み（BreakableWeaponRanks.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 層 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   layer          … 層（1 始まり）
    ///   E・D・C・B・A・S … そのランクの武器が選ばれる重み（0 以上。空は 0。合計で割って使う）。どれかを 0 より大きくする
    /// 例: <c>1,10,3,0.5,,,</c>
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class BreakableWeaponRankCsv
    {
        public const string LayerColumn = "layer";

        /// <summary>ランクの列の名前（WeaponRank の 0〜5 の順）。</summary>
        public static readonly string[] RankColumns = { "E", "D", "C", "B", "A", "S" };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;

            /// <summary>E〜S の重み（6 つ）。</summary>
            public readonly float[] Weights;

            public Row(int line, int layer, float[] weights)
            {
                Line = line;
                Layer = layer;
                Weights = weights;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> columns = null;
            var seen = new HashSet<int>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();

                if (columns == null)
                {
                    columns = ReadHeader(cells, line, errors);
                    if (columns == null) return rows;
                    continue;
                }

                string Cell(string column) => columns.TryGetValue(column, out int at) && at < cells.Length ? cells[at] : "";

                if (!int.TryParse(Cell(LayerColumn), NumberStyles.None, CultureInfo.InvariantCulture, out int layer) || layer < 1)
                {
                    errors?.Add($"{line} 行目: layer は 1 以上の整数「{Cell(LayerColumn)}」");
                    continue;
                }

                bool ok = true;
                float total = 0f;
                var weights = new float[RankColumns.Length];
                for (int r = 0; r < RankColumns.Length; r++)
                {
                    string cell = Cell(RankColumns[r]);
                    if (cell.Length == 0) continue;
                    if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f)
                    {
                        weights[r] = value;
                        total += value;
                        continue;
                    }
                    errors?.Add($"{line} 行目: 層 {layer} の {RankColumns[r]} は 0 以上の数「{cell}」");
                    ok = false;
                    break;
                }
                if (!ok) continue;

                if (total <= 0f)
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {string.Join("・", RankColumns)} が全部 0（箱から武器が出ない）");
                    continue;
                }

                if (!seen.Add(layer))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, weights));
            }

            return rows;
        }

        private static Dictionary<string, int> ReadHeader(string[] cells, int line, List<string> errors)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].Length > 0 && !columns.ContainsKey(cells[c])) columns[cells[c]] = c;
            }

            var missing = new List<string>();
            if (!columns.ContainsKey(LayerColumn)) missing.Add(LayerColumn);
            foreach (string rank in RankColumns)
            {
                if (!columns.ContainsKey(rank)) missing.Add(rank);
            }
            if (missing.Count == 0) return columns;

            errors?.Add($"{line} 行目: 先頭の見出し行に {string.Join("・", missing)} の列が無い（見出しは {LayerColumn},{string.Join(",", RankColumns)}）");
            return null;
        }
    }
}
