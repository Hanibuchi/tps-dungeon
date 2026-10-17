using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 敵ごとに落とすユニーク武器の表（CSV）を読む。UnityEngine に依存しない。
    /// 1 行 = 「敵の id, ユニーク武器の id, 確率（0〜1 の小数）, 天井」。
    /// 例: <c>orc,Weapon_FallenStarHammer,0.003,300</c>（オークはこのユニークを 0.3% で落とし、出ないまま 300 体目を倒したら必ず落とす）。
    /// 天井は「その武器が出ないまま倒した数 + 1」がこの回数になったら確定、の回数。空欄か 0 なら天井なし。
    /// 1 体の敵に何行書いてもよく、上の行から先に判定する。
    /// 先頭の見出し行（enemy で始まる）・空行・# で始まる行は飛ばす。
    /// 読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    public static class EnemyUniqueDropCsv
    {
        public const string Header = "enemy,weapon,chance,pity";

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Enemy;
            public readonly string Weapon;

            /// <summary>1 回倒したときに落とす確率。0〜1。</summary>
            public readonly float Chance;

            /// <summary>天井。0 なら無し。</summary>
            public readonly int Pity;

            public Row(int line, string enemy, string weapon, float chance, int pity)
            {
                Line = line;
                Enemy = enemy;
                Weapon = weapon;
                Chance = chance;
                Pity = pity;
            }
        }

        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(text)) return rows;

            var seen = new HashSet<(string, string)>();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                string raw = lines[i].Trim().TrimStart('﻿');
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] cells = raw.Split(',');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells[0].Equals("enemy", StringComparison.OrdinalIgnoreCase)) continue;

                if (cells.Length < 3 || cells.Length > 4)
                {
                    errors?.Add($"{line} 行目: 列は 3 つか 4 つ（敵,武器,確率,天井）: {raw}");
                    continue;
                }

                string enemy = cells[0];
                string weapon = cells[1];
                if (enemy.Length == 0 || weapon.Length == 0)
                {
                    errors?.Add($"{line} 行目: 敵か武器の id が空");
                    continue;
                }

                if (!EnemyWeaponDropCsv.TryParseChance(cells[2], out float chance))
                {
                    errors?.Add($"{line} 行目: 確率は 0〜1 の小数「{cells[2]}」");
                    continue;
                }

                int pity = 0;
                if (cells.Length > 3 && cells[3].Length > 0
                    && (!int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out pity) || pity < 0))
                {
                    errors?.Add($"{line} 行目: 天井は 0 以上の整数「{cells[3]}」");
                    continue;
                }

                if (chance <= 0f && pity == 0)
                {
                    errors?.Add($"{line} 行目: {enemy} の {weapon} は確率も天井も 0 で、落ちることがない");
                    continue;
                }

                if (!seen.Add((enemy, weapon)))
                {
                    errors?.Add($"{line} 行目: {enemy} の {weapon} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, enemy, weapon, chance, pity));
            }

            return rows;
        }
    }
}
