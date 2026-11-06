using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 地上の草花を壊したときに出てくる敵の表（PlantSpawns.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 敵 1 種。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   enemy  … 敵の id（Enemies.csv の id。地上の敵は layers を 0 にしておく）
    ///   chance … 1 本壊したときにこの敵が出る確率（0〜1 の小数。0.05 で 5%）。合計の残りは何も出ない
    /// 例: <c>field_imp,0.05</c>
    /// 上の行から判定する（<see cref="PlantSpawnTable"/>）。空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class PlantSpawnCsv
    {
        public const string EnemyColumn = "enemy";
        public const string ChanceColumn = "chance";

        private static readonly string[] RequiredColumns = { EnemyColumn, ChanceColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string EnemyId;
            public readonly float Chance;

            public Row(int line, string enemyId, float chance)
            {
                Line = line;
                EnemyId = enemyId;
                Chance = chance;
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

                string id = Cell(EnemyColumn);
                if (id.Length == 0)
                {
                    errors?.Add($"{line} 行目: enemy が空");
                    continue;
                }

                string chanceCell = Cell(ChanceColumn);
                if (!float.TryParse(chanceCell, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance)
                    || float.IsNaN(chance) || chance < 0f || chance > 1f)
                {
                    errors?.Add($"{line} 行目: {id} の chance は 0〜1 の数「{chanceCell}」");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: {id} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, chance));
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
