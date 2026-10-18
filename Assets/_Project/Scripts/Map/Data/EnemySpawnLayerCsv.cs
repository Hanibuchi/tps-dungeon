using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 層ごとの部屋の敵の値（EnemySpawnLayers.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 層 1 つ。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   layer                    … 層（1 始まり）
    ///   budget_per_cell          … 部屋の予算の上限の係数（0 以上）。上限はこれ × budget_basis で決めた部屋の大きさ
    ///   budget_basis             … 予算の上限を何に比例させるか。area = セル数（1x1 なら 1、2x2 なら 4）、side = 辺の平均（2x2 なら 2）。
    ///                              省ける（列が無い・空なら area）
    ///   monster_house_chance     … 普通の部屋がモンスターハウスになる確率（0〜1）
    ///   monster_house_multiplier … モンスターハウスの予算の上限に掛ける倍率（0 以上）
    ///   single_weight            … 単種（層に出る敵ごとに 1 つ作る、1 種だけの編成）の重み（0 以上。0 なら単種を作らない）
    /// 例: <c>1,2,side,0.2,2,0.5</c>（見出しが layer,budget_per_cell,budget_basis,… のとき）
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnemySpawnLayerCsv
    {
        public const string LayerColumn = "layer";
        public const string BudgetPerCellColumn = "budget_per_cell";
        public const string BudgetBasisColumn = "budget_basis";
        public const string AreaBasis = "area";
        public const string SideBasis = "side";
        public const string MonsterHouseChanceColumn = "monster_house_chance";
        public const string MonsterHouseMultiplierColumn = "monster_house_multiplier";
        public const string SingleWeightColumn = "single_weight";

        private static readonly string[] RequiredColumns =
            { LayerColumn, BudgetPerCellColumn, MonsterHouseChanceColumn, MonsterHouseMultiplierColumn, SingleWeightColumn };

        public readonly struct Row
        {
            public readonly int Line;
            public readonly int Layer;
            public readonly float BudgetPerCell;
            public readonly RoomBudgetBasis BudgetBasis;
            public readonly float MonsterHouseChance;
            public readonly float MonsterHouseMultiplier;
            public readonly float SingleWeight;

            public Row(int line, int layer, float budgetPerCell, RoomBudgetBasis budgetBasis, float monsterHouseChance,
                float monsterHouseMultiplier, float singleWeight)
            {
                Line = line;
                Layer = layer;
                BudgetPerCell = budgetPerCell;
                BudgetBasis = budgetBasis;
                MonsterHouseChance = monsterHouseChance;
                MonsterHouseMultiplier = monsterHouseMultiplier;
                SingleWeight = singleWeight;
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

                string layerCell = Cell(LayerColumn);
                if (!int.TryParse(layerCell, NumberStyles.None, CultureInfo.InvariantCulture, out int layer) || layer < 1)
                {
                    errors?.Add($"{line} 行目: layer は 1 以上の整数「{layerCell}」");
                    continue;
                }

                if (!TryParse(Cell(BudgetPerCellColumn), float.PositiveInfinity, out float budget))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {BudgetPerCellColumn} は 0 以上の数「{Cell(BudgetPerCellColumn)}」");
                    continue;
                }

                if (!TryParseBasis(Cell(BudgetBasisColumn), out RoomBudgetBasis basis))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {BudgetBasisColumn} は {AreaBasis} か {SideBasis}「{Cell(BudgetBasisColumn)}」");
                    continue;
                }

                if (!TryParse(Cell(MonsterHouseChanceColumn), 1f, out float chance))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {MonsterHouseChanceColumn} は 0〜1 の小数「{Cell(MonsterHouseChanceColumn)}」");
                    continue;
                }

                if (!TryParse(Cell(MonsterHouseMultiplierColumn), float.PositiveInfinity, out float multiplier))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {MonsterHouseMultiplierColumn} は 0 以上の数「{Cell(MonsterHouseMultiplierColumn)}」");
                    continue;
                }

                if (!TryParse(Cell(SingleWeightColumn), float.PositiveInfinity, out float singleWeight))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の {SingleWeightColumn} は 0 以上の数「{Cell(SingleWeightColumn)}」");
                    continue;
                }

                if (!seen.Add(layer))
                {
                    errors?.Add($"{line} 行目: 層 {layer} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, layer, budget, basis, chance, multiplier, singleWeight));
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

        /// <summary>area か side（大文字小文字は問わない）。空なら area。</summary>
        private static bool TryParseBasis(string text, out RoomBudgetBasis basis)
        {
            basis = RoomBudgetBasis.Area;
            if (text.Length == 0 || string.Equals(text, AreaBasis, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(text, SideBasis, StringComparison.OrdinalIgnoreCase)) return false;
            basis = RoomBudgetBasis.MeanSide;
            return true;
        }

        /// <summary>0 以上 max 以下の数。空は読めない扱い。</summary>
        private static bool TryParse(string text, float max, out float value)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            return !float.IsNaN(value) && value >= 0f && value <= max;
        }
    }
}
