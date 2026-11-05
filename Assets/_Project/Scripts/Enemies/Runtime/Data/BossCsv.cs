using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// ボスの表（Bosses.csv）を読む。UnityEngine に依存しない。1 行 = ボス 1 体。列は先頭の見出し行の名前で引く。
    ///   id        … 敵の id（Enemies.csv と同じ。強さはそちらに書く）
    ///   layer     … ボス階の層（1 以上）。1 つの層にボスは 1 体
    ///   name      … HUD に出す名前
    ///   phase2_hp … 残り HP の割合がこれ以下で段階 2（0〜1）
    ///   phase3_hp … これ以下で段階 3（0〜phase2_hp）
    ///   tempo2    … 段階 2 のテンポ（攻撃の間・溜めを割り、歩く速さに掛ける。1 以上）
    ///   tempo3    … 段階 3 のテンポ
    ///   bgm       … ボス部屋の扉を開けてから倒すまで流す曲。Assets/ThirdParty/Sound/ から先のパス（例 DOVA-SYNDROME/ボス戦1_Demon_Shaft_Crash.mp3）。
    ///               列が無いか空なら曲を変えない（任意の列）
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class BossCsv
    {
        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;
            public readonly int Layer;
            public readonly string Name;
            public readonly float Phase2Hp;
            public readonly float Phase3Hp;
            public readonly float Tempo2;
            public readonly float Tempo3;
            public readonly string Bgm;

            public Row(int line, string id, int layer, string name, float phase2Hp, float phase3Hp, float tempo2, float tempo3, string bgm = "")
            {
                Bgm = bgm ?? "";
                Line = line;
                Id = id;
                Layer = layer;
                Name = name;
                Phase2Hp = phase2Hp;
                Phase3Hp = phase3Hp;
                Tempo2 = tempo2;
                Tempo3 = tempo3;
            }
        }

        private static readonly string[] Columns = { "id", "layer", "name", "phase2_hp", "phase3_hp", "tempo2", "tempo3" };

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            Dictionary<string, int> header = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var layers = new HashSet<int>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();

                if (header == null)
                {
                    header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 0; c < cells.Length; c++) header[cells[c]] = c;
                    foreach (string column in Columns)
                    {
                        if (!header.ContainsKey(column)) errors?.Add($"{line} 行目: 見出しに {column} の列が無い");
                    }
                    continue;
                }

                string Cell(string column) => header.TryGetValue(column, out int index) && index < cells.Length ? cells[index] : "";

                string id = Cell("id");
                if (!EnemyCsv.IsValidId(id))
                {
                    errors?.Add($"{line} 行目: id「{id}」は小文字の英字・数字・_ で書く");
                    continue;
                }

                if (!int.TryParse(Cell("layer"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int layer) || layer < 1)
                {
                    errors?.Add($"{line} 行目: {id} の layer「{Cell("layer")}」は 1 以上の整数");
                    continue;
                }

                if (!TryFloat(Cell("phase2_hp"), out float phase2) || phase2 < 0f || phase2 > 1f ||
                    !TryFloat(Cell("phase3_hp"), out float phase3) || phase3 < 0f || phase3 > phase2)
                {
                    errors?.Add($"{line} 行目: {id} の phase2_hp・phase3_hp は 0 ≤ phase3_hp ≤ phase2_hp ≤ 1 の小数");
                    continue;
                }

                if (!TryFloat(Cell("tempo2"), out float tempo2) || tempo2 < 1f || !TryFloat(Cell("tempo3"), out float tempo3) || tempo3 < 1f)
                {
                    errors?.Add($"{line} 行目: {id} の tempo2・tempo3 は 1 以上の小数");
                    continue;
                }

                if (!ids.Add(id))
                {
                    errors?.Add($"{line} 行目: {id} の行が重複している");
                    continue;
                }

                if (!layers.Add(layer))
                {
                    errors?.Add($"{line} 行目: 第 {layer} 層のボスが 2 体ある");
                    continue;
                }

                string name = Cell("name");
                rows.Add(new Row(line, id, layer, name.Length > 0 ? name : id, phase2, phase3, tempo2, tempo3, Cell("bgm")));
            }

            return rows;
        }

        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value);
    }
}
