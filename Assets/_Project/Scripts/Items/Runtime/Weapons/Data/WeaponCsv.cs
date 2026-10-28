using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器ごとの値の表（Weapons.csv）を読み書きする。UnityEngine に依存しない。
    /// 1 行 = 武器 1 本。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   id          … 武器の id（"Weapon_RustySword" など）
    ///   type        … 武器種の id（WeaponTypes.csv の id。"OneHanded" など）。武器のアセットの武器種と照らすだけで、書き換えはしない。列が無い・空でもよい
    ///   rank        … ランク。E・D・C・B・A・S・U（Unique でもよい）
    ///   strength    … 強さ（0 以上）。意味は武器種による（近接・遠距離なら DPS）
    ///   name        … 画面に出す名前
    ///   description … 説明。空でよい
    /// 例: <c>Weapon_RustySword,OneHanded,E,5,錆びた片手剣,刃こぼれした古い剣。</c>
    /// , を含む欄は "…" で囲む（"" は " 1 つ）。改行は \n と書く。
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class WeaponCsv
    {
        public const string IdColumn = "id";
        public const string TypeColumn = "type";
        public const string RankColumn = "rank";
        public const string StrengthColumn = "strength";
        public const string NameColumn = "name";
        public const string DescriptionColumn = "description";

        public const string Header = IdColumn + "," + TypeColumn + "," + RankColumn + "," + StrengthColumn + "," + NameColumn + "," + DescriptionColumn;

        private static readonly string[] RequiredColumns = { IdColumn, RankColumn, StrengthColumn, NameColumn, DescriptionColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;

            /// <summary>武器種の id。列が無い・空なら ""。</summary>
            public readonly string Type;

            public readonly WeaponRank Rank;
            public readonly float Strength;
            public readonly string Name;
            public readonly string Description;

            public Row(int line, string id, string type, WeaponRank rank, float strength, string name, string description)
            {
                Line = line;
                Id = id;
                Type = type ?? "";
                Rank = rank;
                Strength = strength;
                Name = name;
                Description = description;
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

                string Cell(string column) => columns.TryGetValue(column, out int at) && at < cells.Count ? Unescape(cells[at]) : "";

                string id = Cell(IdColumn);
                if (id.Length == 0)
                {
                    errors?.Add($"{line} 行目: id が空");
                    continue;
                }

                if (!TryParseRank(Cell(RankColumn), out WeaponRank rank))
                {
                    errors?.Add($"{line} 行目: {id} のランク「{Cell(RankColumn)}」が読めない（E・D・C・B・A・S・U）");
                    continue;
                }

                if (!float.TryParse(Cell(StrengthColumn), NumberStyles.Float, CultureInfo.InvariantCulture, out float strength)
                    || strength < 0f || float.IsNaN(strength) || float.IsInfinity(strength))
                {
                    errors?.Add($"{line} 行目: {id} の強さは 0 以上の数「{Cell(StrengthColumn)}」");
                    continue;
                }

                string name = Cell(NameColumn);
                if (name.Length == 0)
                {
                    errors?.Add($"{line} 行目: {id} の名前が空");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: id「{id}」の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, Cell(TypeColumn), rank, strength, name, Cell(DescriptionColumn)));
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
                    .Append(Quote(row.Type)).Append(',')
                    .Append(RankCell(row.Rank)).Append(',')
                    .Append(row.Strength.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Quote(row.Name)).Append(',')
                    .Append(Quote(row.Description)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>ランクの表記を読む。E〜S と U（Unique）。大文字小文字は問わない。</summary>
        public static bool TryParseRank(string cell, out WeaponRank rank)
        {
            switch ((cell ?? "").Trim().ToUpperInvariant())
            {
                case "E": rank = WeaponRank.E; return true;
                case "D": rank = WeaponRank.D; return true;
                case "C": rank = WeaponRank.C; return true;
                case "B": rank = WeaponRank.B; return true;
                case "A": rank = WeaponRank.A; return true;
                case "S": rank = WeaponRank.S; return true;
                case "U":
                case "UNIQUE": rank = WeaponRank.Unique; return true;
                default: rank = WeaponRank.E; return false;
            }
        }

        /// <summary>CSV に書くランクの表記。ユニークは U。</summary>
        public static string RankCell(WeaponRank rank) => rank == WeaponRank.Unique ? "U" : rank.ToString();

        private static string Quote(string cell)
        {
            string escaped = (cell ?? "").Replace("\n", "\\n");
            bool needs = escaped.IndexOf(',') >= 0 || escaped.IndexOf('"') >= 0 || escaped != escaped.Trim();
            return needs ? "\"" + escaped.Replace("\"", "\"\"") + "\"" : escaped;
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

        private static string Unescape(string cell) => cell.Replace("\\n", "\n");

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
