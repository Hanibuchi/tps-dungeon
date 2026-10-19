using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの階の広さと高さ（Floors.csv）を読み、部屋数の範囲に直す。UnityEngine に依存しない。
    /// 1 行 = 層 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   layer     … 層（1 始まり）
    ///   area      … 階の平均面積（セル数、0 より大きい）。部屋の面積の合計の平均がこの値になるよう部屋数を決める
    ///   elevation … 階の床の地面からの高さ（メートル、0 以上。FloorConfig.elevation）。列が無いか空なら設定を変えない
    ///   crates_per_cell … 普通の部屋のセル 1 つあたりの木箱の数の平均（0 以上。FloorConfig.cratesPerCell）。列が無いか空なら設定を変えない
    ///   chest_chance    … 普通の部屋 1 つに宝箱を置く確率（0〜1。FloorConfig.chestChance）。列が無いか空なら設定を変えない
    /// 例: <c>1,27,20,0.5,0.2</c>
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class FloorLayerCsv
    {
        public const string LayerColumn = "layer";
        public const string AreaColumn = "area";
        public const string ElevationColumn = "elevation";
        public const string CratesPerCellColumn = "crates_per_cell";
        public const string ChestChanceColumn = "chest_chance";

        private static readonly string[] RequiredColumns = { LayerColumn, AreaColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;
            public readonly float Area;
            /// <summary>階の高さ（メートル）。書いていなければ NaN。</summary>
            public readonly float Elevation;
            /// <summary>セル 1 つあたりの木箱の数。書いていなければ NaN。</summary>
            public readonly float CratesPerCell;
            /// <summary>部屋 1 つに宝箱を置く確率。書いていなければ NaN。</summary>
            public readonly float ChestChance;

            public Row(int line, int layer, float area, float elevation = float.NaN, float cratesPerCell = float.NaN, float chestChance = float.NaN)
            {
                Line = line;
                Layer = layer;
                Area = area;
                Elevation = elevation;
                CratesPerCell = cratesPerCell;
                ChestChance = chestChance;
            }

            public bool HasElevation => !float.IsNaN(Elevation);
            public bool HasCratesPerCell => !float.IsNaN(CratesPerCell);
            public bool HasChestChance => !float.IsNaN(ChestChance);
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

                string areaCell = Cell(AreaColumn);
                if (!float.TryParse(areaCell, NumberStyles.Float, CultureInfo.InvariantCulture, out float area)
                    || float.IsNaN(area) || float.IsInfinity(area) || area <= 0f)
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {AreaColumn} は 0 より大きい数「{areaCell}」");
                    continue;
                }

                float elevation = float.NaN;
                string elevationCell = Cell(ElevationColumn);
                if (elevationCell.Length > 0
                    && (!float.TryParse(elevationCell, NumberStyles.Float, CultureInfo.InvariantCulture, out elevation)
                        || float.IsNaN(elevation) || float.IsInfinity(elevation) || elevation < 0f))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {ElevationColumn} は 0 以上の数「{elevationCell}」");
                    continue;
                }

                if (!TryOptional(CratesPerCellColumn, float.PositiveInfinity, out float cratesPerCell)) continue;
                if (!TryOptional(ChestChanceColumn, 1f, out float chestChance)) continue;

                if (!seen.Add(layer))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, area, elevation, cratesPerCell, chestChance));

                // 空なら NaN（設定を変えない）。書いてあれば 0〜max の数。
                bool TryOptional(string column, float max, out float value)
                {
                    value = float.NaN;
                    string cell = Cell(column);
                    if (cell.Length == 0) return true;
                    if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                        && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= max) return true;

                    errors?.Add(float.IsPositiveInfinity(max)
                        ? $"{line} 行目: 層 {layer} の {column} は 0 以上の数「{cell}」"
                        : $"{line} 行目: 層 {layer} の {column} は 0〜{max} の数「{cell}」");
                    value = float.NaN;
                    return false;
                }
            }

            return rows;
        }

        /// <summary>
        /// 普通の部屋 1 つあたりの平均面積（セル数）。部屋の面積を抽選の重みで平均する。重み 0 の部屋は出ないので数えない。
        /// 重みのある部屋が 1 つも無ければ 0。
        /// </summary>
        public static double MeanRoomArea(IEnumerable<RoomLayerCsv.Row> rooms)
        {
            double weighted = 0d, total = 0d;
            foreach (RoomLayerCsv.Row room in rooms)
            {
                if (room.Weight <= 0f) continue;
                weighted += room.Weight * room.Width * room.Height;
                total += room.Weight;
            }
            return total > 0d ? weighted / total : 0d;
        }

        /// <summary>
        /// 平均面積 area の階にする部屋数の範囲。上限は下限の 2 倍。
        /// 部屋数は下限〜上限から一様に引くので平均は下限の 1.5 倍で、それが「特別な部屋 specialCount 個（面積の合計 specialArea）
        /// ＋残りの面積を普通の部屋 1 つあたり meanRoomArea で割った数」になるようにする。下限は minimum を割らない。
        /// </summary>
        public static (int min, int max) RoomCountRange(float area, double meanRoomArea, int specialCount, float specialArea, int minimum)
        {
            double rooms = specialCount + Math.Max(0d, area - specialArea) / meanRoomArea;
            int min = Math.Max(minimum, (int)Math.Round(rooms / 1.5d, MidpointRounding.AwayFromZero));
            return (min, min * 2);
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
