using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの階の光と色（FloorMoods.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 層 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。全部の列が要る。
    ///   layer           … 層（1 始まり）
    ///   ambient         … 環境光の色（#RRGGBB。空の側。地平線・床の側はこれを暗くして使う）
    ///   filter          … 画面全体に掛ける色（#RRGGBB。ColorAdjustments の colorFilter）
    ///   saturation      … 彩度（-100〜100）
    ///   vignette        … 画面の周りの暗さ（0〜1）
    ///   torch_color     … 松明の光の色（#RRGGBB）
    ///   torch_intensity … 松明の光の強さ（0 以上）
    ///   torch_range     … 松明の光の届く距離（メートル、0 より大きい）
    ///   flicker         … 松明のゆらめきの幅（明るさの割合、0〜1）
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class FloorMoodCsv
    {
        public const string LayerColumn = "layer";
        public const string AmbientColumn = "ambient";
        public const string FilterColumn = "filter";
        public const string SaturationColumn = "saturation";
        public const string VignetteColumn = "vignette";
        public const string TorchColorColumn = "torch_color";
        public const string TorchIntensityColumn = "torch_intensity";
        public const string TorchRangeColumn = "torch_range";
        public const string FlickerColumn = "flicker";

        private static readonly string[] RequiredColumns =
        {
            LayerColumn, AmbientColumn, FilterColumn, SaturationColumn, VignetteColumn,
            TorchColorColumn, TorchIntensityColumn, TorchRangeColumn, FlickerColumn,
        };

        /// <summary>0〜1 の RGB。</summary>
        public readonly struct Rgb
        {
            public readonly float R, G, B;

            public Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }

            public override string ToString() => $"({R:0.###},{G:0.###},{B:0.###})";
        }

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;
            public readonly Rgb Ambient;
            public readonly Rgb Filter;
            public readonly float Saturation;
            public readonly float Vignette;
            public readonly Rgb TorchColor;
            public readonly float TorchIntensity;
            public readonly float TorchRange;
            public readonly float Flicker;

            public Row(int line, int layer, Rgb ambient, Rgb filter, float saturation, float vignette, Rgb torchColor, float torchIntensity,
                float torchRange, float flicker)
            {
                Line = line;
                Layer = layer;
                Ambient = ambient;
                Filter = filter;
                Saturation = saturation;
                Vignette = vignette;
                TorchColor = torchColor;
                TorchIntensity = torchIntensity;
                TorchRange = torchRange;
                Flicker = flicker;
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
                Rgb Color(string column)
                {
                    if (TryParseColor(Cell(column), out Rgb value)) return value;
                    if (ok) errors?.Add($"{line} 行目: 層 {layer} の {column} は #RRGGBB の色「{Cell(column)}」");
                    ok = false;
                    return default;
                }
                float Number(string column, float min, float max, bool minExclusive = false)
                {
                    string cell = Cell(column);
                    if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        && !float.IsNaN(value) && !float.IsInfinity(value) && (minExclusive ? value > min : value >= min) && value <= max) return value;
                    if (ok)
                    {
                        string range = float.IsPositiveInfinity(max) ? (minExclusive ? $"{min} より大きい" : $"{min} 以上の") : $"{min}〜{max} の";
                        errors?.Add($"{line} 行目: 層 {layer} の {column} は {range}数「{cell}」");
                    }
                    ok = false;
                    return 0f;
                }

                Rgb ambient = Color(AmbientColumn);
                Rgb filter = Color(FilterColumn);
                float saturation = Number(SaturationColumn, -100f, 100f);
                float vignette = Number(VignetteColumn, 0f, 1f);
                Rgb torchColor = Color(TorchColorColumn);
                float torchIntensity = Number(TorchIntensityColumn, 0f, float.PositiveInfinity);
                float torchRange = Number(TorchRangeColumn, 0f, float.PositiveInfinity, minExclusive: true);
                float flicker = Number(FlickerColumn, 0f, 1f);
                if (!ok) continue;

                if (!seen.Add(layer))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, ambient, filter, saturation, vignette, torchColor, torchIntensity, torchRange, flicker));
            }

            return rows;
        }

        /// <summary>「#RRGGBB」（# は省いてもよい）を 0〜1 の RGB に。</summary>
        public static bool TryParseColor(string text, out Rgb color)
        {
            color = default;
            if (string.IsNullOrEmpty(text)) return false;
            string hex = text.StartsWith("#", StringComparison.Ordinal) ? text.Substring(1) : text;
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)) return false;
            color = new Rgb(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
            return true;
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
