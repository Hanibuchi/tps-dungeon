using System;
using System.Collections.Generic;
using System.Text;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// インタラクトできる場所の文字（InteractionTexts.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 場所 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。全部の列が要る（中身は空でもよい）。
    ///   id            … 文字の ID（SceneGate などの textId に書く名前）
    ///   prompt        … 通れるとき・触れるときに画面中央に出す案内
    ///   closed_prompt … 閉じているときの案内。空なら閉じている間は案内を出さず、触れない
    ///   title         … 右の情報欄の見出し
    ///   description   … 右の情報欄の本文
    /// ショートカットの見出しと本文の {0} は層の番号になる。欄を "…" で囲めば中に , を書ける（"" で " 1 つ）。\n は改行になる。
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class InteractionTextCsv
    {
        public const string IdColumn = "id";
        public const string PromptColumn = "prompt";
        public const string ClosedPromptColumn = "closed_prompt";
        public const string TitleColumn = "title";
        public const string DescriptionColumn = "description";

        private static readonly string[] RequiredColumns = { IdColumn, PromptColumn, ClosedPromptColumn, TitleColumn, DescriptionColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;
            public readonly string Prompt;
            public readonly string ClosedPrompt;
            public readonly string Title;
            public readonly string Description;

            public Row(int line, string id, string prompt, string closedPrompt, string title, string description)
            {
                Line = line;
                Id = id;
                Prompt = prompt;
                ClosedPrompt = closedPrompt;
                Title = title;
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
                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: id「{id}」の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, Cell(PromptColumn), Cell(ClosedPromptColumn), Cell(TitleColumn), Cell(DescriptionColumn)));
            }

            return rows;
        }

        /// <summary>1 行を , で区切る。"…" で囲んだ欄は中の , を区切りにしない（"" は " 1 つ）。囲みが閉じていなければ null。</summary>
        private static List<string> SplitCells(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
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
                }
                else if (c == ',')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                }
                else cell.Append(c);
            }
            if (quoted) return null;
            cells.Add(cell.ToString().Trim());
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

            errors?.Add($"{line} 行目: 先頭の見出し行に {string.Join("・", missing)} の列が無い（見出しは {string.Join(",", RequiredColumns)}）");
            return null;
        }
    }
}
