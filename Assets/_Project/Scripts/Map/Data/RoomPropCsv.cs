using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの部屋の置き物の顔ぶれ（RoomProps.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 層 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。全部の列が要る。
    ///   layer          … 層（1 始まり）
    ///   bones・books・potions・candles・candles_unlit … 壁ぎわの小物（と机の上の物）が選ばれる重み（0 以上）。骨は小・中の 2 つに半分ずつ
    ///   table_chance   … 普通の部屋に机と椅子を置く確率（0〜1）
    /// 例: <c>5,5,1,0.5,0.5,2,0.15</c>（5 層は骨と火の消えたろうそくが多く、机は少ない）
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class RoomPropCsv
    {
        public const string LayerColumn = "layer";
        public const string BonesColumn = "bones";
        public const string BooksColumn = "books";
        public const string PotionsColumn = "potions";
        public const string CandlesColumn = "candles";
        public const string CandlesUnlitColumn = "candles_unlit";
        public const string TableChanceColumn = "table_chance";

        private static readonly string[] RequiredColumns =
        {
            LayerColumn, BonesColumn, BooksColumn, PotionsColumn, CandlesColumn, CandlesUnlitColumn, TableChanceColumn,
        };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;
            public readonly float Bones;
            public readonly float Books;
            public readonly float Potions;
            public readonly float Candles;
            public readonly float CandlesUnlit;
            public readonly float TableChance;

            public Row(int line, int layer, float bones, float books, float potions, float candles, float candlesUnlit, float tableChance)
            {
                Line = line;
                Layer = layer;
                Bones = bones;
                Books = books;
                Potions = potions;
                Candles = candles;
                CandlesUnlit = candlesUnlit;
                TableChance = tableChance;
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
                float Number(string column, float max)
                {
                    string cell = Cell(column);
                    if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= max) return value;
                    if (ok) errors?.Add(float.IsPositiveInfinity(max)
                        ? $"{line} 行目: 層 {layer} の {column} は 0 以上の数「{cell}」"
                        : $"{line} 行目: 層 {layer} の {column} は 0〜{max} の数「{cell}」");
                    ok = false;
                    return 0f;
                }

                float bones = Number(BonesColumn, float.PositiveInfinity);
                float books = Number(BooksColumn, float.PositiveInfinity);
                float potions = Number(PotionsColumn, float.PositiveInfinity);
                float candles = Number(CandlesColumn, float.PositiveInfinity);
                float candlesUnlit = Number(CandlesUnlitColumn, float.PositiveInfinity);
                float tableChance = Number(TableChanceColumn, 1f);
                if (!ok) continue;

                if (!seen.Add(layer))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, bones, books, potions, candles, candlesUnlit, tableChance));
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
            foreach (string required in RequiredColumns)
            {
                if (!columns.ContainsKey(required)) missing.Add(required);
            }
            if (missing.Count == 0) return columns;

            errors?.Add($"{line} 行目: 先頭の見出し行に {string.Join("・", missing)} の列が無い（見出しは {string.Join(",", RequiredColumns)}）");
            return null;
        }
    }
}
