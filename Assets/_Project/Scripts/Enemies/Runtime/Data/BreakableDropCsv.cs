using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 箱を壊したときに出るものの表（Breakables.csv）を読む。UnityEngine に依存しない。どの層でも同じ表を使う。
    /// 1 行 = 箱の種類 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   kind              … 箱の種類（crate・chest）
    ///   enemy・weapon・nothing … 敵が出る・武器が出る・何も出ない、の重み（0 以上。合計で割って使う）
    /// 武器が出たときのランクは層ごとの表（BreakableWeaponRanks.csv）で決めるので、ここには書かない。
    /// 例: <c>chest,0.35,0.5,0.15</c>
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class BreakableDropCsv
    {
        public const string KindColumn = "kind";
        public const string EnemyColumn = "enemy";
        public const string WeaponColumn = "weapon";
        public const string NothingColumn = "nothing";

        private static readonly string[] RequiredColumns = { KindColumn, EnemyColumn, WeaponColumn, NothingColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly BreakableDropKind Kind;
            public readonly float Enemy;
            public readonly float Weapon;
            public readonly float Nothing;

            public Row(int line, BreakableDropKind kind, float enemy, float weapon, float nothing)
            {
                Line = line;
                Kind = kind;
                Enemy = enemy;
                Weapon = weapon;
                Nothing = nothing;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> columns = null;
            var seen = new HashSet<BreakableDropKind>();
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

                string kindCell = Cell(KindColumn);
                if (!TryParseKind(kindCell, out BreakableDropKind kind))
                {
                    errors?.Add($"{line} 行目: kind「{kindCell}」は crate か chest");
                    continue;
                }

                bool ok = true;
                float Weight(string column)
                {
                    string cell = Cell(column);
                    if (cell.Length == 0)
                    {
                        errors?.Add($"{line} 行目: {kindCell} の {column} が空");
                        ok = false;
                        return 0f;
                    }
                    if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        && !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f) return value;

                    errors?.Add($"{line} 行目: {kindCell} の {column} は 0 以上の数「{cell}」");
                    ok = false;
                    return 0f;
                }

                float enemy = Weight(EnemyColumn);
                float weapon = Weight(WeaponColumn);
                float nothing = Weight(NothingColumn);
                if (!ok) continue;

                if (enemy + weapon + nothing <= 0f)
                {
                    errors?.Add($"{line} 行目: {kindCell} の enemy・weapon・nothing が全部 0");
                    continue;
                }

                if (!seen.Add(kind))
                {
                    errors?.Add($"{line} 行目: {kindCell} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, kind, enemy, weapon, nothing));
            }

            return rows;
        }

        /// <summary>kind 列の書き方（crate・chest。大文字小文字は問わない）。</summary>
        public static bool TryParseKind(string text, out BreakableDropKind kind)
        {
            foreach (BreakableDropKind value in (BreakableDropKind[])Enum.GetValues(typeof(BreakableDropKind)))
            {
                if (!string.Equals(text, value.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                kind = value;
                return true;
            }
            kind = default;
            return false;
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
