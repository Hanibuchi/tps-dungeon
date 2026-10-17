using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 部屋の敵の編成タイプ（EnemyCompositions.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 編成 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   name   … 見分けるための名前（混成・主役＋脇役など）
    ///   layers … この編成を使う層。Enemies.csv と同じ書き方（<c>1-5</c>・<c>1;3</c>）
    ///   weight … 部屋ごとにこの編成が選ばれる重み（0 以上）
    ///   shares … 種類ごとの予算の比率。<c>orc:0.6;green_blob:0.4</c> のように id:比率 を ; で区切る。
    ///            比率を省くと 1（<c>orc</c> は <c>orc:1</c>）。<c>*</c> だけなら、その層に出る敵全部を同じ比率で（混成）
    /// 単種（1 種だけの編成）は書かなくてよい。層に出る敵ごとに取り込みで作る（重みは EnemySpawnLayers.csv の single_weight）。
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnemyCompositionCsv
    {
        public const string NameColumn = "name";
        public const string LayersColumn = "layers";
        public const string WeightColumn = "weight";
        public const string SharesColumn = "shares";

        /// <summary>その層に出る敵全部を同じ比率で、の印。</summary>
        public const string AllEnemies = "*";

        private static readonly string[] RequiredColumns = { NameColumn, LayersColumn, WeightColumn, SharesColumn };

        public readonly struct Share
        {
            public readonly string EnemyId;
            public readonly float Value;

            public Share(string enemyId, float value)
            {
                EnemyId = enemyId;
                Value = value;
            }
        }

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Name;

            /// <summary>この編成を使う層（1 始まり、小さい順、重複なし）。</summary>
            public readonly int[] Layers;

            public readonly float Weight;

            /// <summary>真なら、その層に出る敵全部を比率 1 で（<see cref="Shares"/> は空）。</summary>
            public readonly bool AllEnemies;

            public readonly Share[] Shares;

            public Row(int line, string name, int[] layers, float weight, bool allEnemies, Share[] shares)
            {
                Line = line;
                Name = name;
                Layers = layers;
                Weight = weight;
                AllEnemies = allEnemies;
                Shares = shares;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> columns = null;
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

                string name = Cell(NameColumn);
                if (name.Length == 0)
                {
                    errors?.Add($"{line} 行目: 編成の name が空");
                    continue;
                }

                if (!EnemyCsv.TryParseLayers(Cell(LayersColumn), out int[] layers, out string layerError))
                {
                    errors?.Add($"{line} 行目: {name} の layers「{Cell(LayersColumn)}」が読めない（{layerError}）");
                    continue;
                }

                string weightCell = Cell(WeightColumn);
                if (!float.TryParse(weightCell, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight) || float.IsNaN(weight) || weight < 0f)
                {
                    errors?.Add($"{line} 行目: {name} の weight は 0 以上の数「{weightCell}」");
                    continue;
                }

                if (!TryParseShares(Cell(SharesColumn), out bool all, out Share[] shares, out string shareError))
                {
                    errors?.Add($"{line} 行目: {name} の shares「{Cell(SharesColumn)}」が読めない（{shareError}）");
                    continue;
                }

                rows.Add(new Row(line, name, layers, weight, all, shares));
            }

            return rows;
        }

        /// <summary>
        /// 「orc:0.6;green_blob:0.4」「orc」「*」のような比率の書き方を読む。
        /// * なら all が真で shares は空。空・読めない比率・負の比率・同じ id が 2 回・* と id を混ぜたものは偽。
        /// </summary>
        public static bool TryParseShares(string text, out bool all, out Share[] shares, out string error)
        {
            all = false;
            shares = Array.Empty<Share>();
            error = null;

            var items = new List<string>();
            foreach (string part in (text ?? "").Split(';'))
            {
                string item = part.Trim();
                if (item.Length > 0) items.Add(item);
            }

            if (items.Count == 0)
            {
                error = "id:比率 を ; で区切って書くか、その層の敵全部なら *";
                return false;
            }

            if (items.Contains(AllEnemies))
            {
                if (items.Count > 1)
                {
                    error = "* はその層の敵全部なので、ほかの id と一緒には書かない";
                    return false;
                }

                all = true;
                return true;
            }

            var list = new List<Share>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string item in items)
            {
                int colon = item.IndexOf(':');
                string id = (colon >= 0 ? item.Substring(0, colon) : item).Trim();
                float value = 1f;
                if (colon >= 0)
                {
                    string valueText = item.Substring(colon + 1).Trim();
                    if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || value < 0f)
                    {
                        error = $"「{item}」の比率は 0 以上の数";
                        return false;
                    }
                }

                if (!EnemyCsv.IsValidId(id))
                {
                    error = $"「{item}」の id は小文字の英字・数字・_ で書く";
                    return false;
                }

                if (!seen.Add(id))
                {
                    error = $"{id} が 2 回書かれている";
                    return false;
                }

                list.Add(new Share(id, value));
            }

            shares = list.ToArray();
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
