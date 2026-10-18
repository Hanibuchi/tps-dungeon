using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの普通の部屋（Rooms.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 層 1 つの部屋 1 種類。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   layer  … 層（1 始まり）
    ///   size_x … 部屋の幅（セル数、1 以上）
    ///   size_y … 部屋の奥行き（セル数、1 以上）
    ///   weight … 抽選の重み（0 以上。0 なら出ない）
    ///   max_doors … ドアの最大数（0 以上の整数。空か 0 なら無制限。列ごと無くてもよい）
    /// 例: <c>1,1,2,0.8,2</c>
    /// 階段・ショップの部屋は書かない（カタログのものをそのまま使う）。
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class RoomLayerCsv
    {
        public const string LayerColumn = "layer";
        public const string SizeXColumn = "size_x";
        public const string SizeYColumn = "size_y";
        public const string WeightColumn = "weight";
        public const string MaxDoorsColumn = "max_doors";

        private static readonly string[] RequiredColumns = { LayerColumn, SizeXColumn, SizeYColumn, WeightColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;
            public readonly int Width;
            public readonly int Height;
            public readonly float Weight;

            /// <summary>ドアの最大数。0 なら無制限。</summary>
            public readonly int MaxDoors;

            public Row(int line, int layer, int width, int height, float weight, int maxDoors)
            {
                Line = line;
                Layer = layer;
                Width = width;
                Height = height;
                Weight = weight;
                MaxDoors = maxDoors;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> columns = null;
            var seen = new HashSet<(int, int, int)>();
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

                if (!TryParsePositive(Cell(LayerColumn), out int layer))
                {
                    errors?.Add($"{line} 行目: layer は 1 以上の整数「{Cell(LayerColumn)}」");
                    continue;
                }

                if (!TryParsePositive(Cell(SizeXColumn), out int width))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {SizeXColumn} は 1 以上の整数「{Cell(SizeXColumn)}」");
                    continue;
                }

                if (!TryParsePositive(Cell(SizeYColumn), out int height))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {SizeYColumn} は 1 以上の整数「{Cell(SizeYColumn)}」");
                    continue;
                }

                string weightCell = Cell(WeightColumn);
                if (!float.TryParse(weightCell, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight)
                    || float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {width}x{height} の {WeightColumn} は 0 以上の数「{weightCell}」");
                    continue;
                }

                string maxDoorsCell = Cell(MaxDoorsColumn);
                int maxDoors = 0;
                if (maxDoorsCell.Length > 0 && !int.TryParse(maxDoorsCell, NumberStyles.None, CultureInfo.InvariantCulture, out maxDoors))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {width}x{height} の {MaxDoorsColumn} は 0 以上の整数か空「{maxDoorsCell}」");
                    continue;
                }

                if (!seen.Add((layer, width, height)))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {width}x{height} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, width, height, weight, maxDoors));
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

        /// <summary>1 以上の整数。</summary>
        private static bool TryParsePositive(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1;
    }
}
