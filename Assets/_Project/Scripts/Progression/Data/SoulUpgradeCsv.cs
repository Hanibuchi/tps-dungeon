using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TpsDungeon.Progression
{
    /// <summary>魂の強化の木のノード 1 つ。</summary>
    [Serializable]
    public struct SoulUpgradeNode
    {
        public string id;
        public string name;
        /// <summary>先に解放しておく強化の id。空なら木の根（真ん中）。</summary>
        public string parent;
        public int cost;
        public SoulUpgradeEffect effect;
        public float value;
        public string description;
    }

    /// <summary>
    /// 魂の強化の表（SoulUpgrades.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 強化 1 つ（段階ごとに別の行にして、前の段を parent にする）。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   id          … 強化の ID（保存に書く。後から変えると買った物が消える）
    ///   name        … 画面に出す名前
    ///   parent      … 先に解放しておく強化の id。空なら木の根（真ん中に出る）。根はちょうど 1 つ
    ///   cost        … 要るソウル
    ///   effect      … 効果（<see cref="SoulUpgradeEffects"/> の名前。exp_rate・max_hp など）
    ///   value       … その段まで解放したときの効果の量（差分ではない。割合は 0.1 で 10%、時間は秒、ショートカットは層の番号）。
    ///                 前の強化が同じ効果なら続きの段として扱い、前の段の値を引いた分だけ上乗せする（<see cref="SoulUpgradeTree"/>）
    ///   description … 説明（空でもよい）
    /// 欄を "…" で囲めば中に , を書ける（"" で " 1 つ）。\n は改行になる。空行・# で始まる行は飛ばす。
    /// 読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class SoulUpgradeCsv
    {
        private static readonly string[] RequiredColumns = { "id", "name", "parent", "cost", "effect", "value", "description" };

        public static List<SoulUpgradeNode> Parse(string text, List<string> errors)
        {
            var rows = new List<SoulUpgradeNode>();
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

                string Cell(string column) => columns.TryGetValue(column, out int at) && at < cells.Count ? cells[at].Replace("\\n", "\n") : "";

                string id = Cell("id");
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
                if (!int.TryParse(Cell("cost"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost) || cost < 0)
                {
                    errors?.Add($"{line} 行目: cost「{Cell("cost")}」が 0 以上の整数でない");
                    continue;
                }
                if (!SoulUpgradeEffects.TryParse(Cell("effect"), out SoulUpgradeEffect effect))
                {
                    errors?.Add($"{line} 行目: effect「{Cell("effect")}」を知らない（{string.Join("・", SoulUpgradeEffects.Names)}）");
                    continue;
                }
                string valueText = Cell("value");
                float value = 0f;
                if (valueText.Length > 0 && !float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    errors?.Add($"{line} 行目: value「{valueText}」が数でない");
                    continue;
                }

                rows.Add(new SoulUpgradeNode
                {
                    id = id,
                    name = Cell("name"),
                    parent = Cell("parent"),
                    cost = cost,
                    effect = effect,
                    value = value,
                    description = Cell("description"),
                });
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
