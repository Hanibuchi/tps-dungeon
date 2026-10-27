using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種ごとの値の表（WeaponTypes.csv）を読み書きする。UnityEngine に依存しない。
    /// 1 行 = 武器種 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   id       … 武器種の id（"OneHanded" など。EnchantmentRanks.csv の weaponType と同じ）
    ///   name     … 画面に出す武器種の名前
    ///   cooldown … クールタイム（秒、0 以上）。近接はコンボが切れてから次に振れるまで、遠距離は撃つ間隔（速射の補正前）。
    ///              お守り・盾は使わないので空。空なら今の値のまま
    ///   shield   … この武器種を持っている間、ホットバーの盾が効くか。1・0（true・false でもよい）
    /// 例: <c>OneHanded,片手近距離,0.3,1</c>
    /// , を含む欄は "…" で囲む（"" は " 1 つ）。
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class WeaponTypeCsv
    {
        public const string IdColumn = "id";
        public const string NameColumn = "name";
        public const string CooldownColumn = "cooldown";
        public const string ShieldColumn = "shield";

        public const string Header = IdColumn + "," + NameColumn + "," + CooldownColumn + "," + ShieldColumn;

        private static readonly string[] RequiredColumns = { IdColumn, NameColumn, CooldownColumn, ShieldColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;
            public readonly string Name;

            /// <summary>クールタイム（秒）。null なら今の値のまま。</summary>
            public readonly float? Cooldown;

            public readonly bool CanUseShield;

            public Row(int line, string id, string name, float? cooldown, bool canUseShield)
            {
                Line = line;
                Id = id;
                Name = name;
                Cooldown = cooldown;
                CanUseShield = canUseShield;
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

                List<string> cells = SplitCells(raw);
                if (cells == null)
                {
                    errors?.Add($"{line} 行目: \" で囲んだ欄が閉じていない");
                    continue;
                }

                if (columns == null)
                {
                    columns = ReadHeader(cells, line, errors);
                    if (columns == null) return rows;
                    continue;
                }

                string Cell(string column) => columns.TryGetValue(column, out int at) && at < cells.Count ? cells[at] : "";

                string id = Cell(IdColumn);
                if (id.Length == 0)
                {
                    errors?.Add($"{line} 行目: id が空");
                    continue;
                }

                string name = Cell(NameColumn);
                if (name.Length == 0)
                {
                    errors?.Add($"{line} 行目: {id} の名前が空");
                    continue;
                }

                float? cooldown = null;
                string cooldownCell = Cell(CooldownColumn);
                if (cooldownCell.Length > 0)
                {
                    if (!float.TryParse(cooldownCell, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                        || seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                    {
                        errors?.Add($"{line} 行目: {id} のクールタイムは 0 以上の秒数か空「{cooldownCell}」");
                        continue;
                    }

                    cooldown = seconds;
                }

                if (!TryParseBool(Cell(ShieldColumn), out bool canUseShield))
                {
                    errors?.Add($"{line} 行目: {id} の shield は 1 か 0「{Cell(ShieldColumn)}」");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: id「{id}」の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, name, cooldown, canUseShield));
            }

            return rows;
        }

        /// <summary>見出し行から始まる CSV にする（行番号は使わない）。, や " を含む欄だけ "…" で囲む。</summary>
        public static string Format(IEnumerable<Row> rows)
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            foreach (Row row in rows)
            {
                text.Append(Quote(row.Id)).Append(',')
                    .Append(Quote(row.Name)).Append(',')
                    .Append(row.Cooldown.HasValue ? row.Cooldown.Value.ToString("0.###", CultureInfo.InvariantCulture) : "").Append(',')
                    .Append(row.CanUseShield ? '1' : '0').Append('\n');
            }

            return text.ToString();
        }

        private static bool TryParseBool(string cell, out bool value)
        {
            switch ((cell ?? "").Trim().ToLowerInvariant())
            {
                case "1":
                case "true": value = true; return true;
                case "0":
                case "false": value = false; return true;
                default: value = false; return false;
            }
        }

        private static string Quote(string cell)
        {
            string text = cell ?? "";
            bool needs = text.IndexOf(',') >= 0 || text.IndexOf('"') >= 0 || text != text.Trim();
            return needs ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }

        /// <summary>1 行を , で区切る。"…" で囲んだ欄は中の , を区切りにしない（"" は " 1 つ）。囲みが閉じていなければ null。</summary>
        private static List<string> SplitCells(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            bool wasQuoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else if (c == '"' && cell.ToString().Trim().Length == 0)
                {
                    cell.Clear();
                    quoted = true;
                    wasQuoted = true;
                }
                else if (c == ',')
                {
                    cells.Add(wasQuoted ? cell.ToString() : cell.ToString().Trim());
                    cell.Clear();
                    wasQuoted = false;
                }
                else if (!wasQuoted) cell.Append(c);
            }
            if (quoted) return null;
            cells.Add(wasQuoted ? cell.ToString() : cell.ToString().Trim());
            return cells;
        }

        private static Dictionary<string, int> ReadHeader(List<string> cells, int line, List<string> errors)
        {
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < cells.Count; c++)
            {
                if (cells[c].Length > 0 && !columns.ContainsKey(cells[c])) columns[cells[c]] = c;
            }

            var missing = new List<string>();
            foreach (string required in RequiredColumns)
            {
                if (!columns.ContainsKey(required)) missing.Add(required);
            }
            if (missing.Count == 0) return columns;

            errors?.Add($"{line} 行目: 先頭の見出し行に {string.Join("・", missing)} の列が無い（見出しは {Header}）");
            return null;
        }
    }
}
