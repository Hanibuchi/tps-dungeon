using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// ボス階の設計図（Settings/Tables/BossFloors.md の 1 層分）。UnityEngine に依存しない。
    /// 1 文字が 1 セルで、上の行が北。同じ文字のかたまりが 1 つの部屋（四角形）。'.' は何も無い所。
    ///   A … ボス部屋（5x5）、H … 大扉の手前の部屋、X … 出口（1x1）、E … 入口（1x1）、S … ショートカットの部屋（1x1）、小文字 … 道のりの部屋
    ///   扉: a-b … a と b のあいだに扉（接している辺の真ん中）。H-A（大扉）と A-X（閉ざされた扉）は書かなくても付く
    ///   敵: a b … 敵を出す部屋
    /// '>' で始まる行は説明として読み飛ばす。
    /// </summary>
    public sealed class BossFloorBlueprint
    {
        public const char Arena = 'A';
        public const char Hall = 'H';
        public const char Exit = 'X';
        public const char Entrance = 'E';
        public const char Shortcut = 'S';
        public const char Empty = '.';

        private const string DoorsPrefix = "扉:";
        private const string EnemiesPrefix = "敵:";

        private static readonly Regex LayerHeader = new Regex(@"^#\s*第\s*(\d+)\s*層\s*$");

        /// <summary>層（1 始まり）。設計図の 1 層分だけを読んだときは 0。</summary>
        public int Layer { get; private set; }

        /// <summary>見出しの行番号（ファイル全体を読んだとき）。</summary>
        public int Line { get; private set; }

        /// <summary>部屋の文字から、その部屋の矩形（X は西から、Y は南から数えたセル。地図の一番下の行が Y 0）。</summary>
        public IReadOnlyDictionary<char, GridRect> Rooms => rooms;

        /// <summary>扉で結ぶ部屋の組（書いた順）。</summary>
        public IReadOnlyList<(char a, char b)> Doors => doors;

        /// <summary>敵を出す部屋。</summary>
        public IReadOnlyCollection<char> EnemyRooms => enemyRooms;

        /// <summary>部屋の並び（地図に最初に出てきた順）。</summary>
        public IReadOnlyList<char> RoomOrder => order;

        /// <summary>設計図の本文（見出しを除いた地図と扉・敵の行）。設定に入れて、実行中はこれを読み直す。</summary>
        public string Body { get; private set; } = "";

        private readonly Dictionary<char, GridRect> rooms = new Dictionary<char, GridRect>();
        private readonly List<(char, char)> doors = new List<(char, char)>();
        private readonly HashSet<char> enemyRooms = new HashSet<char>();
        private readonly List<char> order = new List<char>();

        public bool IsEnemyRoom(char room) => enemyRooms.Contains(room);

        /// <summary>
        /// 設計図のファイル全体を読み、層ごとに分ける（見出し「# 第 n 層」ごと）。読めない所は行番号付きで errors に積み、その層は飛ばす。
        /// </summary>
        public static List<BossFloorBlueprint> ParseFile(string text, List<string> errors)
        {
            var result = new List<BossFloorBlueprint>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = -1, layer = 0;
            var seen = new HashSet<int>();
            for (int i = 0; i <= lines.Length; i++)
            {
                var match = i < lines.Length ? LayerHeader.Match(lines[i].Trim().TrimStart('﻿')) : null;
                if (i < lines.Length && !match.Success) continue;

                if (start >= 0)
                {
                    var body = string.Join("\n", lines, start + 1, i - start - 1);
                    var sectionErrors = new List<string>();
                    var blueprint = Parse(body, sectionErrors, start + 2);
                    if (!seen.Add(layer)) sectionErrors.Add($"{start + 1} 行目: 第 {layer} 層が 2 つある");
                    foreach (var error in sectionErrors) errors?.Add($"第 {layer} 層: {error}");
                    if (sectionErrors.Count == 0)
                    {
                        blueprint.Layer = layer;
                        blueprint.Line = start + 1;
                        result.Add(blueprint);
                    }
                }
                if (i == lines.Length) break;
                start = i;
                layer = int.Parse(match.Groups[1].Value);
            }
            return result;
        }

        /// <summary>1 層分の本文（地図と扉・敵の行）を読む。firstLine は本文の 1 行目の、ファイルでの行番号（知らせに使う）。</summary>
        public static BossFloorBlueprint Parse(string body, List<string> errors, int firstLine = 1)
        {
            var blueprint = new BossFloorBlueprint { Body = (body ?? "").Trim('\n', '\r') };
            var map = new List<(string row, int line)>();
            var doorLines = new List<(string text, int line)>();
            var enemyLines = new List<(string text, int line)>();

            string[] lines = (body ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int line = firstLine + i;
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw.StartsWith(">", StringComparison.Ordinal)) continue;
                if (raw.StartsWith(DoorsPrefix, StringComparison.Ordinal)) doorLines.Add((raw.Substring(DoorsPrefix.Length), line));
                else if (raw.StartsWith(EnemiesPrefix, StringComparison.Ordinal)) enemyLines.Add((raw.Substring(EnemiesPrefix.Length), line));
                else if (IsMapRow(raw)) map.Add((raw, line));
                else errors?.Add($"{line} 行目: 読めない行「{raw}」");
            }

            if (map.Count == 0)
            {
                errors?.Add($"{firstLine} 行目から: 地図が無い");
                return blueprint;
            }

            blueprint.ReadRooms(map, errors);
            foreach (var (text, line) in doorLines) blueprint.ReadDoors(text, line, errors);
            foreach (var (text, line) in enemyLines) blueprint.ReadEnemies(text, line, errors);
            blueprint.Check(map[0].line, errors);
            return blueprint;
        }

        private static bool IsMapRow(string raw)
        {
            foreach (char c in raw)
            {
                if (c != Empty && !IsRoomChar(c)) return false;
            }
            return true;
        }

        private static bool IsRoomChar(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        /// <summary>地図から部屋の矩形を拾う。同じ文字は 1 つの四角形でなければならない。</summary>
        private void ReadRooms(List<(string row, int line)> map, List<string> errors)
        {
            int height = map.Count;
            var cells = new Dictionary<char, List<GridPos>>();
            var firstLineOf = new Dictionary<char, int>();
            for (int r = 0; r < height; r++)
            {
                var (row, line) = map[r];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == Empty) continue;
                    if (char.IsUpper(c) && c != Arena && c != Hall && c != Exit && c != Entrance && c != Shortcut)
                    {
                        errors?.Add($"{line} 行目: 大文字は A・H・X・E・S だけ（「{c}」）。道のりの部屋は小文字で書く");
                        continue;
                    }
                    if (!cells.TryGetValue(c, out var list))
                    {
                        cells[c] = list = new List<GridPos>();
                        firstLineOf[c] = line;
                        order.Add(c);
                    }
                    list.Add(new GridPos(x, height - 1 - r));
                }
            }

            foreach (var pair in cells)
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var p in pair.Value)
                {
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }
                var rect = new GridRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                if (rect.Area != pair.Value.Count)
                {
                    errors?.Add($"{firstLineOf[pair.Key]} 行目から: 部屋「{pair.Key}」が四角形になっていない（離れている・欠けている）");
                    continue;
                }
                rooms[pair.Key] = rect;
            }
        }

        private void ReadDoors(string text, int line, List<string> errors)
        {
            foreach (string token in text.Split(new[] { ' ', '\t', ',', '、', '　' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split('-');
                if (parts.Length != 2 || parts[0].Length != 1 || parts[1].Length != 1)
                {
                    errors?.Add($"{line} 行目: 扉は「a-b」のように書く（「{token}」）");
                    continue;
                }
                char a = parts[0][0], b = parts[1][0];
                if (a == b)
                {
                    errors?.Add($"{line} 行目: 「{token}」は同じ部屋どうし");
                    continue;
                }
                if (HasDoor(a, b))
                {
                    errors?.Add($"{line} 行目: 扉「{token}」が 2 回書いてある");
                    continue;
                }
                doors.Add((a, b));
            }
        }

        private void ReadEnemies(string text, int line, List<string> errors)
        {
            foreach (string token in text.Split(new[] { ' ', '\t', ',', '、', '　' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length != 1 || !IsRoomChar(token[0]))
                {
                    errors?.Add($"{line} 行目: 敵を出す部屋は 1 文字ずつ書く（「{token}」）");
                    continue;
                }
                enemyRooms.Add(token[0]);
            }
        }

        public bool HasDoor(char a, char b)
        {
            foreach (var (x, y) in doors)
            {
                if ((x == a && y == b) || (x == b && y == a)) return true;
            }
            return false;
        }

        /// <summary>決まった部屋の有無と大きさ、扉の部屋が接しているかを確かめる。</summary>
        private void Check(int line, List<string> errors)
        {
            RequireRoom(Arena, 5, 5, "ボス部屋");
            RequireRoom(Exit, 1, 1, "出口");
            RequireRoom(Entrance, 1, 1, "入口");
            RequireRoom(Shortcut, 1, 1, "ショートカットの部屋");
            if (!rooms.ContainsKey(Hall)) errors?.Add($"{line} 行目から: 大扉の手前の部屋「{Hall}」が無い");

            if (rooms.ContainsKey(Hall) && rooms.ContainsKey(Arena) && !Touching(rooms[Hall], rooms[Arena]))
                errors?.Add($"{line} 行目から: 「{Hall}」が「{Arena}」に接していない（大扉が開けられない）");
            if (rooms.ContainsKey(Exit) && rooms.ContainsKey(Arena) && !Touching(rooms[Exit], rooms[Arena]))
                errors?.Add($"{line} 行目から: 出口「{Exit}」が「{Arena}」に接していない");

            foreach (var (a, b) in doors)
            {
                if (!rooms.ContainsKey(a) || !rooms.ContainsKey(b))
                {
                    errors?.Add($"{line} 行目から: 扉「{a}-{b}」の部屋が地図に無い");
                    continue;
                }
                if (!Touching(rooms[a], rooms[b])) errors?.Add($"{line} 行目から: 扉「{a}-{b}」の 2 部屋が接していない");
                if (IsFixedDoor(a, b)) continue;
                if (a == Arena || b == Arena || a == Exit || b == Exit)
                    errors?.Add($"{line} 行目から: 扉「{a}-{b}」は開けられない（ボス部屋の扉は大扉と出口の扉だけ）");
            }
            foreach (char room in enemyRooms)
            {
                if (!rooms.ContainsKey(room)) errors?.Add($"{line} 行目から: 敵を出す部屋「{room}」が地図に無い");
            }

            void RequireRoom(char c, int width, int height, string label)
            {
                if (!rooms.TryGetValue(c, out var rect)) errors?.Add($"{line} 行目から: {label}「{c}」が無い");
                else if (rect.Width != width || rect.Height != height)
                    errors?.Add($"{line} 行目から: {label}「{c}」は {width}x{height} のはずが {rect.Width}x{rect.Height}");
            }
        }

        private static bool IsFixedDoor(char a, char b) =>
            (a == Hall && b == Arena) || (a == Arena && b == Hall) || (a == Arena && b == Exit) || (a == Exit && b == Arena);

        /// <summary>2 つの矩形が辺を共有して接しているか（角だけは接していない）。</summary>
        public static bool Touching(GridRect a, GridRect b)
        {
            if (a.Overlaps(b)) return false;
            bool sideBySide = (a.MaxX + 1 == b.X || b.MaxX + 1 == a.X) && a.Y <= b.MaxY && b.Y <= a.MaxY;
            bool stacked = (a.MaxY + 1 == b.Y || b.MaxY + 1 == a.Y) && a.X <= b.MaxX && b.X <= a.MaxX;
            return sideBySide || stacked;
        }
    }
}
