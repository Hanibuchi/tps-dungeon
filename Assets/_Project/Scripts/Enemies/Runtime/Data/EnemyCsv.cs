using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の台帳（Enemies.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 敵 1 種。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列（後から足す hp など）は飛ばす。
    ///   id     … 敵の id（EnemyIdentity の id。小文字の英字・数字・_）
    ///   cost   … 部屋に出すときの 1 体あたりのコスト（部屋の予算からこの分を使う）。layers が空なら空でよい
    ///   layers … 部屋に出る層。<c>1-5</c>・<c>1;3</c>・<c>2-3;5</c> の形。空なら部屋には出ない
    /// 例: <c>pink_blob,1,1-5</c>
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnemyCsv
    {
        public const string IdColumn = "id";
        public const string CostColumn = "cost";
        public const string LayersColumn = "layers";

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;
            public readonly float Cost;

            /// <summary>部屋に出る層（1 始まり、小さい順、重複なし）。</summary>
            public readonly int[] Layers;

            public Row(int line, string id, float cost, int[] layers)
            {
                Line = line;
                Id = id;
                Cost = cost;
                Layers = layers;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> columns = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
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

                string id = Cell(IdColumn);
                if (!IsValidId(id))
                {
                    errors?.Add(id.Length == 0
                        ? $"{line} 行目: 敵の id が空"
                        : $"{line} 行目: 敵の id「{id}」は小文字の英字・数字・_ で書く");
                    continue;
                }

                if (!TryParseLayers(Cell(LayersColumn), out int[] layers, out string layerError))
                {
                    errors?.Add($"{line} 行目: {id} の layers「{Cell(LayersColumn)}」が読めない（{layerError}）");
                    continue;
                }

                string costCell = Cell(CostColumn);
                float cost = 0f;
                if (costCell.Length > 0 &&
                    (!float.TryParse(costCell, NumberStyles.Float, CultureInfo.InvariantCulture, out cost) || float.IsNaN(cost) || cost < 0f))
                {
                    errors?.Add($"{line} 行目: {id} の cost は 0 以上の数「{costCell}」");
                    continue;
                }

                if (layers.Length > 0 && cost <= 0f)
                {
                    errors?.Add($"{line} 行目: {id} は部屋に出る（layers {Cell(LayersColumn)}）ので、cost を 0 より大きくする");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: 敵 {id} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, cost, layers));
            }

            return rows;
        }

        /// <summary>見出し行から列の位置を引く。id・cost・layers のどれかが無ければエラーを積んで null。</summary>
        private static Dictionary<string, int> ReadHeader(string[] cells, int line, List<string> errors)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].Length > 0 && !columns.ContainsKey(cells[c])) columns[cells[c]] = c;
            }

            var missing = new List<string>();
            foreach (string required in new[] { IdColumn, CostColumn, LayersColumn })
            {
                if (!columns.ContainsKey(required)) missing.Add(required);
            }
            if (missing.Count == 0) return columns;

            errors?.Add($"{line} 行目: 先頭の見出し行に {string.Join("・", missing)} の列が無い（見出しは {IdColumn},{CostColumn},{LayersColumn},…）");
            return null;
        }

        /// <summary>小文字の英字・数字・_ だけで、空でない。</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            }
            return true;
        }

        /// <summary>「1-3;5」のような層の書き方を読む。空なら空の配列。層は 1 以上。</summary>
        public static bool TryParseLayers(string text, out int[] layers, out string error)
        {
            var set = new SortedSet<int>();
            layers = Array.Empty<int>();
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            foreach (string part in text.Split(';'))
            {
                string item = part.Trim();
                if (item.Length == 0) continue;

                int dash = item.IndexOf('-');
                string fromText = dash >= 0 ? item.Substring(0, dash).Trim() : item;
                string toText = dash >= 0 ? item.Substring(dash + 1).Trim() : item;
                if (!int.TryParse(fromText, NumberStyles.None, CultureInfo.InvariantCulture, out int from) ||
                    !int.TryParse(toText, NumberStyles.None, CultureInfo.InvariantCulture, out int to))
                {
                    error = $"「{item}」は 1 つの層（3）か範囲（1-3）で書き、; で区切る";
                    return false;
                }

                if (from < 1 || to < from)
                {
                    error = $"「{item}」の層は 1 以上で、範囲は小さい方から書く";
                    return false;
                }

                for (int layer = from; layer <= to; layer++) set.Add(layer);
            }

            layers = new int[set.Count];
            set.CopyTo(layers);
            return true;
        }
    }
}
