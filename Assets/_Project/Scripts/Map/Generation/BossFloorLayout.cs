using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// ボス階のレイアウトを、設計図（<see cref="BossFloorBlueprint"/>。Settings/Tables/BossFloors.md）のとおりに組む。北が奥。
    /// - 部屋は設計図の矩形のまま置き、部屋全体をグリッドの真ん中に寄せる（階の外の景色はグリッドの真ん中を基準に置くため）。
    /// - 扉は設計図に書いた組と、決まった 2 枚（H-A の大扉、A-X のボスを倒すまで閉ざされた扉）。S の扉を書いていなければ隣の H と繋ぐ。
    ///   扉の位置は、2 部屋の接している辺の真ん中（大扉はボス部屋の真ん中に一番近い所）。
    /// - S（ショートカットの部屋）の扉はその層を解放するまで閉ざしておく（RoomEdge.IsShortcutDoor）。
    /// - 敵は設計図の「敵:」に書いた部屋にだけ出す（RoomInstance.HostsEnemies）。
    /// 部屋のテンプレートは ID で引く（<see cref="TemplateId"/>）。シードは窓の並びなどにだけ効き、形はいつも同じ。UnityEngine には依存しない。
    /// </summary>
    public static class BossFloorLayout
    {
        public const string StairId = "Room_Stair";
        public const string ArenaId = "Room_BossArena";
        public const string ShortcutId = "Room_BossShortcut";
        public const string HallPrefix = "Room_BossHall_";
        public const string PathPrefix = "Room_BossPath_";

        /// <summary>設計図の部屋に使うテンプレートの ID。大扉の手前の部屋と道のりの部屋は大きさごとに 1 つ（回さずに置く）。</summary>
        public static string TemplateId(char room, GridRect rect)
        {
            switch (room)
            {
                case BossFloorBlueprint.Entrance:
                case BossFloorBlueprint.Exit: return StairId;
                case BossFloorBlueprint.Arena: return ArenaId;
                case BossFloorBlueprint.Shortcut: return ShortcutId;
                case BossFloorBlueprint.Hall: return $"{HallPrefix}{rect.Width}x{rect.Height}";
                default: return $"{PathPrefix}{rect.Width}x{rect.Height}";
            }
        }

        /// <summary>
        /// 設計図の部屋のテンプレートのタグ。大扉の手前の部屋・ボス部屋・ショートカットの部屋は Boss（置き物は松明と柱だけ）、
        /// 道のりの部屋は Normal（机や小物も置く）。入口・出口は階段の部屋。
        /// </summary>
        public static RoomTag TagOf(char room) => room == BossFloorBlueprint.Entrance || room == BossFloorBlueprint.Exit ? RoomTag.Stair
            : char.IsLower(room) ? RoomTag.Normal
            : RoomTag.Boss;

        /// <param name="templates">テンプレート ID からテンプレート。無ければ null を返す（そのときは例外で知らせる）。</param>
        public static FloorLayout Generate(BossFloorBlueprint blueprint, Func<string, RoomTemplateData> templates, int width, int height, int seed)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (templates == null) throw new ArgumentNullException(nameof(templates));

            // 部屋全体の外接矩形をグリッドの真ん中に寄せる。
            var bounds = Bounds(blueprint);
            if (width < bounds.Width || height < bounds.Height)
                throw new ArgumentException($"ボス階のグリッド {width}x{height} が狭すぎる（{bounds.Width}x{bounds.Height} 以上要る）");
            var offset = new GridPos((width - bounds.Width) / 2 - bounds.X, (height - bounds.Height) / 2 - bounds.Y);

            var links = Links(blueprint);
            var rooms = new List<RoomInstance>();
            var placed = new Dictionary<char, RoomInstance>();
            foreach (char c in PlaceOrder(blueprint))
            {
                var rect = blueprint.Rooms[c];
                string id = TemplateId(c, rect);
                var template = templates(id) ?? throw new ArgumentException($"ボス階の部屋「{c}」のテンプレート {id} が無い（「ボス階の設計図を取り込む」を回すこと）");
                if (template.Width != rect.Width || template.Height != rect.Height)
                    throw new ArgumentException($"ボス階の部屋「{c}」（{id}）は {rect.Width}x{rect.Height} のはずが {template.Width}x{template.Height}");

                var bounded = new GridRect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
                var room = Place(rooms, template, RotationOf(c, blueprint, links), bounded);
                room.HostsEnemies = blueprint.IsEnemyRoom(c);
                placed[c] = room;
            }

            var edges = new List<RoomEdge>();
            var arenaCenter = placed[BossFloorBlueprint.Arena].Bounds.Center;
            foreach (var link in links)
            {
                var a = placed[link.A];
                var b = placed[link.B];
                var (cell, facing) = DoorCell(a.Bounds, b.Bounds, link.Kind == LinkKind.Grand ? arenaCenter : (GridPos?)null);
                var edge = Connect(edges, a, b, cell, facing);
                edge.IsGrandDoor = link.Kind == LinkKind.Grand;
                edge.IsLocked = link.Kind == LinkKind.Locked;
                edge.IsShortcutDoor = link.Kind == LinkKind.Shortcut;
            }

            var entrance = placed[BossFloorBlueprint.Entrance];
            var exit = placed[BossFloorBlueprint.Exit];
            entrance.Role = RoomRole.Entrance;
            exit.Role = RoomRole.Exit;

            var layout = new FloorLayout(seed, width, height, rooms, edges)
            {
                EntranceRoom = entrance.Index,
                ExitRoom = exit.Index,
                ShopRoom = -1,
                ShortcutCell = placed[BossFloorBlueprint.Shortcut].Bounds.Center,
            };
            if (!FloorLayoutAnalysis.IsFullyConnected(layout))
                throw new ArgumentException("ボス階の設計図で、入口から行けない部屋がある（扉が足りない）");
            return layout;
        }

        /// <summary>設計図の部屋全体の外接矩形（グリッドに置く前の座標）。</summary>
        public static GridRect Bounds(BossFloorBlueprint blueprint)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var rect in blueprint.Rooms.Values)
            {
                minX = Math.Min(minX, rect.X);
                minY = Math.Min(minY, rect.Y);
                maxX = Math.Max(maxX, rect.MaxX);
                maxY = Math.Max(maxY, rect.MaxY);
            }
            return minX > maxX ? new GridRect(0, 0, 0, 0) : new GridRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private enum LinkKind { Normal, Grand, Locked, Shortcut }

        private readonly struct Link
        {
            public readonly char A;
            public readonly char B;
            public readonly LinkKind Kind;

            public Link(char a, char b, LinkKind kind)
            {
                A = a;
                B = b;
                Kind = kind;
            }

            public bool Has(char c) => A == c || B == c;
            public char Other(char c) => A == c ? B : A;
        }

        /// <summary>扉の一覧。決まった 2 枚（大扉・出口の扉）を先に、続けて設計図の順。S の扉が無ければ H と繋ぐ。</summary>
        private static List<Link> Links(BossFloorBlueprint blueprint)
        {
            const char a = BossFloorBlueprint.Arena, h = BossFloorBlueprint.Hall, x = BossFloorBlueprint.Exit, s = BossFloorBlueprint.Shortcut;
            foreach (char required in new[] { a, h, x, s, BossFloorBlueprint.Entrance })
            {
                if (!blueprint.Rooms.ContainsKey(required)) throw new ArgumentException($"ボス階の設計図に「{required}」が無い");
            }

            var links = new List<Link> { new Link(h, a, LinkKind.Grand), new Link(a, x, LinkKind.Locked) };
            bool shortcutLinked = false;
            foreach (var (p, q) in blueprint.Doors)
            {
                if ((p == h && q == a) || (p == a && q == h) || (p == a && q == x) || (p == x && q == a)) continue;
                if (!blueprint.Rooms.ContainsKey(p) || !blueprint.Rooms.ContainsKey(q) || !BossFloorBlueprint.Touching(blueprint.Rooms[p], blueprint.Rooms[q]))
                    throw new ArgumentException($"ボス階の設計図で、扉「{p}-{q}」の 2 部屋が接していない");
                bool shortcut = p == s || q == s;
                if (shortcut && shortcutLinked) throw new ArgumentException("ボス階の設計図で、ショートカットの部屋「S」の扉が 2 枚ある");
                shortcutLinked |= shortcut;
                links.Add(new Link(p, q, shortcut ? LinkKind.Shortcut : LinkKind.Normal));
            }
            if (!shortcutLinked)
            {
                if (!BossFloorBlueprint.Touching(blueprint.Rooms[s], blueprint.Rooms[h]))
                    throw new ArgumentException("ボス階の設計図で、ショートカットの部屋「S」の扉が無く、「H」にも接していない");
                links.Add(new Link(h, s, LinkKind.Shortcut));
            }

            foreach (char one in new[] { BossFloorBlueprint.Entrance, x, s })
            {
                int count = 0;
                foreach (var link in links) if (link.Has(one)) count++;
                if (count != 1) throw new ArgumentException($"ボス階の設計図で、「{one}」の扉は 1 枚にする（{count} 枚ある）");
            }
            return links;
        }

        /// <summary>部屋を置く順。入口・大扉の手前の部屋・ボス部屋・出口・ショートカットの部屋、続けて道のりの部屋を地図に出てきた順。</summary>
        private static List<char> PlaceOrder(BossFloorBlueprint blueprint)
        {
            var order = new List<char> { BossFloorBlueprint.Entrance, BossFloorBlueprint.Hall, BossFloorBlueprint.Arena, BossFloorBlueprint.Exit, BossFloorBlueprint.Shortcut };
            foreach (char c in blueprint.RoomOrder)
            {
                if (!order.Contains(c)) order.Add(c);
            }
            return order;
        }

        /// <summary>
        /// 部屋の回し方。入口・出口の階段の部屋はドア候補が南の 1 面、ショートカットの部屋は西の 1 面なので、扉の向きへ回す（階段や井戸も部屋と一緒に回る）。
        /// ほかの部屋は大きさごとにテンプレートがあるので回さない。
        /// </summary>
        private static int RotationOf(char c, BossFloorBlueprint blueprint, List<Link> links)
        {
            Direction socket;
            if (c == BossFloorBlueprint.Entrance || c == BossFloorBlueprint.Exit) socket = Direction.South;
            else if (c == BossFloorBlueprint.Shortcut) socket = Direction.West;
            else return 0;

            foreach (var link in links)
            {
                if (!link.Has(c)) continue;
                var (_, facing) = DoorCell(blueprint.Rooms[c], blueprint.Rooms[link.Other(c)], null);
                return (((int)facing - (int)socket) % 4 + 4) % 4;
            }
            return 0;
        }

        /// <summary>a と b の接している辺の、a 側のセルと b へ向かう向き。辺の真ん中のセルにする（toward があれば、そこに一番近いセル）。</summary>
        private static (GridPos cell, Direction facing) DoorCell(GridRect a, GridRect b, GridPos? toward)
        {
            Direction facing;
            var cells = new List<GridPos>();
            bool vertical = a.MaxY + 1 == b.Y || b.MaxY + 1 == a.Y;
            if (vertical)
            {
                facing = a.MaxY + 1 == b.Y ? Direction.North : Direction.South;
                int y = facing == Direction.North ? a.MaxY : a.Y;
                for (int x = Math.Max(a.X, b.X); x <= Math.Min(a.MaxX, b.MaxX); x++) cells.Add(new GridPos(x, y));
            }
            else
            {
                facing = a.MaxX + 1 == b.X ? Direction.East : Direction.West;
                int x = facing == Direction.East ? a.MaxX : a.X;
                for (int y = Math.Max(a.Y, b.Y); y <= Math.Min(a.MaxY, b.MaxY); y++) cells.Add(new GridPos(x, y));
            }
            if (cells.Count == 0) throw new ArgumentException($"{a} と {b} が接していない");
            if (!toward.HasValue) return (cells[(cells.Count - 1) / 2], facing);

            var best = cells[0];
            foreach (var cell in cells)
            {
                if (Along(cell) < Along(best)) best = cell;
            }
            return (best, facing);

            int Along(GridPos cell) => vertical ? Math.Abs(cell.X - toward.Value.X) : Math.Abs(cell.Y - toward.Value.Y);
        }

        private static RoomInstance Place(List<RoomInstance> rooms, RoomTemplateData template, int rotation, GridRect bounds)
        {
            var sockets = new List<PlacedSocket>();
            foreach (var socket in template.SocketsAfterRotation(rotation))
            {
                sockets.Add(new PlacedSocket(new GridPos(bounds.X + socket.LocalCell.X, bounds.Y + socket.LocalCell.Y), socket.Facing));
            }

            var room = new RoomInstance(rooms.Count, template, rotation, bounds, sockets);
            rooms.Add(room);
            return room;
        }

        /// <summary>from の cell から facing の向きのドア候補と、その向かいの to のドア候補を辺で結ぶ。</summary>
        private static RoomEdge Connect(List<RoomEdge> edges, RoomInstance from, RoomInstance to, GridPos cell, Direction facing)
        {
            var socketA = FindSocket(from, cell, facing);
            var socketB = FindSocket(to, cell + facing.Offset(), facing.Opposite());
            var edge = new RoomEdge(edges.Count, from.Index, to.Index) { SocketA = socketA, SocketB = socketB };
            edges.Add(edge);
            socketA.UsedByEdge = edge.Index;
            socketB.UsedByEdge = edge.Index;
            return edge;
        }

        private static PlacedSocket FindSocket(RoomInstance room, GridPos cell, Direction facing)
        {
            foreach (var socket in room.Sockets)
            {
                if (socket.Cell == cell && socket.Facing == facing) return socket;
            }
            throw new ArgumentException($"{room.Template.Id} に {cell} の {facing} 向きのドア候補が無い");
        }
    }
}
