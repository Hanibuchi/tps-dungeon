using System;
using System.Collections.Generic;
using System.Globalization;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の台帳（Enemies.csv）を読む。UnityEngine に依存しない。
    /// 1 行 = 敵 1 種。列は先頭の見出し行の名前で引くので、並びは自由で、知らない列は飛ばす。
    ///   id     … 敵の id（EnemyIdentity の id。小文字の英字・数字・_）
    ///   kind   … 攻撃の型。melee（近距離）・ranged（遠距離）・leap（飛びつき）・bomber（自爆）・poison_gas（毒霧）・spawner（手下を呼ぶ）。
    ///            型はプレハブに付いている EnemyBrain で決まり、この列は表で見えるようにするためと確かめのためにある
    ///            （食い違えば取り込みでエラー）。空欄なら確かめない
    ///   cost   … 部屋に出すときの 1 体あたりのコスト（部屋の予算からこの分を使う）。layers が空なら空でよい
    ///   layers … 出る層。<c>1-5</c>・<c>1;3</c>・<c>2-3;5</c> の形。空なら部屋には出ない。0 は地上（草花から出る敵。部屋には出ない。草花の表は PlantSpawns.csv）
    /// 強さの列（<see cref="Stats"/>）。どれも省いてよく、空欄ならプレハブの値をそのまま使う。値は層の倍率などを掛けない、そのままの値:
    ///   hp              … 最大 HP
    ///   damage          … 1 回の攻撃のダメージ（毒霧は 1 刻みごと、自爆は爆心での値）
    ///   move_speed      … 移動の速さ（m/秒）
    ///   attack_interval … 攻撃を終えてから次の攻撃までの間（秒）。自爆する敵には無い
    ///   windup          … 攻撃までの溜め（秒）。飛びつきはかがむ時間、自爆は導火から爆発まで
    ///   attack_range    … 攻撃を始める距離（m）。近距離は間合い、飛びつきは飛ぶ距離、自爆は導火を始める距離、毒霧は霧を出す距離、遠距離は撃てる一番遠い距離
    ///   area_radius     … 爆発・毒霧の半径（m）。自爆と毒霧の敵だけ
    ///   detect_range    … 相手に気付く距離（m）
    ///   exp             … 倒したときの経験値（パーティの経験値倍率を掛ける前）
    /// 例: <c>goblin,melee,2,1,40,10,3,1.4,0.5,1.8,,14,8</c>
    /// 空行・# で始まる行は飛ばす。読めない行は飛ばして、行番号付きでエラーに積む。
    /// </summary>
    /// <summary>敵の攻撃の型（Enemies.csv の kind 列）。Unknown は空欄（確かめない）。</summary>
    public enum EnemyKind
    {
        Unknown,
        Melee,
        Ranged,
        Leap,
        Bomber,
        PoisonGas,
        Spawner,
    }

    public static class EnemyCsv
    {
        public const string IdColumn = "id";
        public const string KindColumn = "kind";
        public const string CostColumn = "cost";
        public const string LayersColumn = "layers";
        public const string HpColumn = "hp";
        public const string DamageColumn = "damage";
        public const string MoveSpeedColumn = "move_speed";
        public const string AttackIntervalColumn = "attack_interval";
        public const string WindupColumn = "windup";
        public const string AttackRangeColumn = "attack_range";
        public const string AreaRadiusColumn = "area_radius";
        public const string DetectRangeColumn = "detect_range";
        public const string ExpColumn = "exp";

        /// <summary>強さの列。空欄の値は null（プレハブの値をそのまま使う）。</summary>
        public readonly struct Stats
        {
            public readonly int? Hp;
            public readonly int? Damage;
            public readonly float? MoveSpeed;
            public readonly float? AttackInterval;
            public readonly float? Windup;
            public readonly float? AttackRange;
            public readonly float? AreaRadius;
            public readonly float? DetectRange;
            public readonly int? Exp;

            public Stats(int? hp, int? damage, float? moveSpeed, float? attackInterval, float? windup, float? attackRange,
                float? areaRadius, float? detectRange, int? exp)
            {
                Hp = hp;
                Damage = damage;
                MoveSpeed = moveSpeed;
                AttackInterval = attackInterval;
                Windup = windup;
                AttackRange = attackRange;
                AreaRadius = areaRadius;
                DetectRange = detectRange;
                Exp = exp;
            }
        }

        public readonly struct Row
        {
            public readonly int Line;
            public readonly string Id;

            /// <summary>攻撃の型。空欄なら Unknown。</summary>
            public readonly EnemyKind Kind;

            public readonly float Cost;

            /// <summary>部屋に出る層（1 始まり、小さい順、重複なし）。</summary>
            public readonly int[] Layers;

            public readonly Stats Stats;

            public Row(int line, string id, EnemyKind kind, float cost, int[] layers, Stats stats)
            {
                Line = line;
                Id = id;
                Kind = kind;
                Cost = cost;
                Layers = layers;
                Stats = stats;
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

                if (!TryParseKind(Cell(KindColumn), out EnemyKind kind))
                {
                    errors?.Add($"{line} 行目: {id} の kind「{Cell(KindColumn)}」は {KindNames} のどれか（空欄なら確かめない）");
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

                var statErrors = new List<string>();
                int? IntStat(string column, int min) => ReadInt(column, Cell(column), min, statErrors);
                float? FloatStat(string column, float min) => ReadFloat(column, Cell(column), min, statErrors);
                var stats = new Stats(
                    IntStat(HpColumn, 1),
                    IntStat(DamageColumn, 0),
                    FloatStat(MoveSpeedColumn, 0.1f),
                    FloatStat(AttackIntervalColumn, 0f),
                    FloatStat(WindupColumn, 0f),
                    FloatStat(AttackRangeColumn, 0.3f),
                    FloatStat(AreaRadiusColumn, 0.5f),
                    FloatStat(DetectRangeColumn, 1f),
                    IntStat(ExpColumn, 0));
                if (statErrors.Count > 0)
                {
                    foreach (string error in statErrors) errors?.Add($"{line} 行目: {id} の {error}");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors?.Add($"{line} 行目: 敵 {id} の行が重複している");
                    continue;
                }

                rows.Add(new Row(line, id, kind, cost, layers, stats));
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

        /// <summary>空なら null。整数でないか min より小さければエラーを積んで null。</summary>
        private static int? ReadInt(string column, string cell, int min, List<string> errors)
        {
            if (cell.Length == 0) return null;
            if (int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min) return value;
            errors.Add($"{column} は {min} 以上の整数「{cell}」");
            return null;
        }

        /// <summary>空なら null。数でないか min より小さければエラーを積んで null。</summary>
        private static float? ReadFloat(string column, string cell, float min, List<string> errors)
        {
            if (cell.Length == 0) return null;
            if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                !float.IsNaN(value) && !float.IsInfinity(value) && value >= min) return value;
            errors.Add($"{column} は {min.ToString(CultureInfo.InvariantCulture)} 以上の数「{cell}」");
            return null;
        }

        private static readonly (EnemyKind Kind, string Name)[] Kinds =
        {
            (EnemyKind.Melee, "melee"),
            (EnemyKind.Ranged, "ranged"),
            (EnemyKind.Leap, "leap"),
            (EnemyKind.Bomber, "bomber"),
            (EnemyKind.PoisonGas, "poison_gas"),
            (EnemyKind.Spawner, "spawner"),
        };

        private const string KindNames = "melee・ranged・leap・bomber・poison_gas・spawner";

        /// <summary>kind 列の書き方（melee など）。Unknown は空文字。</summary>
        public static string KindName(EnemyKind kind)
        {
            foreach (var (k, name) in Kinds)
            {
                if (k == kind) return name;
            }
            return "";
        }

        /// <summary>kind 列を読む。空なら Unknown で true。大文字小文字は問わない。知らない値なら false。</summary>
        public static bool TryParseKind(string text, out EnemyKind kind)
        {
            kind = EnemyKind.Unknown;
            if (string.IsNullOrWhiteSpace(text)) return true;

            string trimmed = text.Trim();
            foreach (var (k, name) in Kinds)
            {
                if (!string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)) continue;
                kind = k;
                return true;
            }
            return false;
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

        /// <summary>「1-3;5」のような層の書き方を読む。空なら空の配列。層は 0 以上（0 は地上）。</summary>
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

                if (from < 0 || to < from)
                {
                    error = $"「{item}」の層は 0 以上（0 は地上）で、範囲は小さい方から書く";
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
