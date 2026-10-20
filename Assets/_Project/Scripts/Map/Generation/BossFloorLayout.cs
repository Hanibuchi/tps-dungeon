using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>ボス階を組むのに使う部屋テンプレート。</summary>
    public sealed class BossFloorTemplates
    {
        /// <summary>
        /// 入口（下り階段）と出口（上り階段）の部屋。1x1 で、南の辺にドア候補を持つ。
        /// 入口は回してドアを手前の部屋へ向け、出口はそのまま（ドアが南のボス部屋の側）。
        /// </summary>
        public RoomTemplateData Entrance;
        /// <summary>大扉の手前の横長の部屋。3x1。</summary>
        public RoomTemplateData Hall;
        /// <summary>大扉の先のボス部屋。5x5。奥の壁の真ん中の閉ざされた扉の先に出口の部屋。</summary>
        public RoomTemplateData Arena;
        /// <summary>ショートカットの部屋。1x1 で、西の辺にだけドア候補を持つ（3x1 の東端に付く）。</summary>
        public RoomTemplateData Shortcut;
        /// <summary>入口から 3x1 の部屋までの道のりに使う部屋（その層の普通の部屋）。重みで選ぶ。</summary>
        public List<RoomTemplateData> PathRooms = new List<RoomTemplateData>();
    }

    /// <summary>ボス階の生成条件。層ごとの値は FloorConfig（Floors.csv の列）から入る。</summary>
    public sealed class BossFloorParams
    {
        public int Width = 16;
        public int Height = 16;
        public BossFloorTemplates Templates = new BossFloorTemplates();

        /// <summary>道のりの形を決めるシード。層ごとに固定して、同じ層はいつも同じ形にする。</summary>
        public int LayoutSeed = 1;
        /// <summary>入口から 3x1 の部屋までに通る部屋の数。0 なら入口がすぐ 3x1 の部屋の南（1 層の形）。</summary>
        public int PathRooms;
        /// <summary>道のりから生やす行き止まりの脇道の部屋の数。</summary>
        public int SideRooms;

        public bool HasPath => PathRooms > 0 || SideRooms > 0;
    }

    /// <summary>
    /// ボス階のレイアウト。北が奥。
    /// <code>
    ///        [出口]        上り階段。ボスを倒すまで扉が閉ざされている
    ///        ╫ 閉ざされた扉
    ///   [ 5x5 ボス部屋 ]
    ///        ╫ 大扉
    ///    [ 3x1 の部屋 ]╫[★]   ★ ショートカットの部屋（1x1。解放するまで扉が閉ざされている）
    ///        ↑
    ///   入口から続く道のり（層ごとに形が違う）
    /// </code>
    /// 3x1 の部屋から上は、どの層も同じ形。道のりは、3x1 の部屋の南か西の辺から部屋を継ぎ足して作り、
    /// 最後の部屋に入口を付け、途中から行き止まりの脇道を生やす。形は LayoutSeed だけで決まり（同じ層はいつも同じ形）、
    /// Generate のシードは窓の並びなどにだけ効く。道のりの無い層（1 層）は左右対称で、入口がすぐ 3x1 の部屋の南。
    /// 部屋全体はグリッドの真ん中に置く（階の外の景色はグリッドの真ん中を基準に置くため）。UnityEngine には依存しない。
    /// </summary>
    public static class BossFloorLayout
    {
        public const int HallWidth = 3;
        public const int ArenaSize = 5;

        /// <summary>道のりの無い層の、入口・3x1・ボス部屋・出口を縦に積んだ高さ（セル数）。</summary>
        public const int TotalHeight = 1 + 1 + ArenaSize + 1;

        /// <summary>道のりを組み直す回数の上限。置き場が無くて詰まったら最初から組み直す。</summary>
        private const int MaxAttempts = 200;

        /// <summary>道のりの無いボス階（1 層の形）。</summary>
        public static FloorLayout Generate(BossFloorTemplates templates, int width, int height, int seed) =>
            Generate(new BossFloorParams { Width = width, Height = height, Templates = templates }, seed);

        public static FloorLayout Generate(BossFloorParams parameters, int seed)
        {
            var plan = BuildPlan(parameters);
            var bounds = plan.Bounds();
            int width = parameters.Width, height = parameters.Height;
            if (width < bounds.Width || height < bounds.Height)
            {
                throw new ArgumentException($"ボス階のグリッド {width}x{height} が狭すぎる（{bounds.Width}x{bounds.Height} 以上要る）");
            }

            // 部屋全体の外接矩形がグリッドの真ん中に来るようにずらす。
            var offset = new GridPos((width - bounds.Width) / 2 - bounds.X, (height - bounds.Height) / 2 - bounds.Y);

            var rooms = new List<RoomInstance>();
            foreach (var room in plan.Rooms)
            {
                var rect = room.Rect;
                room.Placed = Place(rooms, room.Template, room.Rotation, new GridRect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height));
            }

            var edges = new List<RoomEdge>();
            foreach (var link in plan.Links)
            {
                var edge = Connect(edges, link.From.Placed, link.To.Placed, link.Cell + offset, link.Facing);
                edge.IsGrandDoor = link.Kind == LinkKind.Grand;
                edge.IsLocked = link.Kind == LinkKind.Locked;
                edge.IsShortcutDoor = link.Kind == LinkKind.Shortcut;
            }

            var entrance = plan.Entrance.Placed;
            var exit = plan.Exit.Placed;
            entrance.Role = RoomRole.Entrance;
            exit.Role = RoomRole.Exit;

            return new FloorLayout(seed, width, height, rooms, edges)
            {
                EntranceRoom = entrance.Index,
                ExitRoom = exit.Index,
                ShopRoom = -1,
                ShortcutCell = plan.Shortcut.Placed.Bounds.Center,
                MirrorColumn = parameters.HasPath ? (int?)null : plan.Hall.Placed.Bounds.Center.X,
            };
        }

        /// <summary>この条件で組んだ部屋全体の外接矩形の大きさ（セル数）。グリッドの広さを決めるのに使う。</summary>
        public static (int width, int height) RequiredSize(BossFloorParams parameters)
        {
            var bounds = BuildPlan(parameters).Bounds();
            return (bounds.Width, bounds.Height);
        }

        // --- 間取りの下書き（グリッドに置く前。3x1 の部屋の左下を (-1, 0) とする座標） ---

        private enum LinkKind { Normal, Grand, Locked, Shortcut }

        private sealed class PlannedRoom
        {
            public RoomTemplateData Template;
            public int Rotation;
            public GridRect Rect;
            public int Doors;
            public readonly HashSet<(GridPos, Direction)> UsedSockets = new HashSet<(GridPos, Direction)>();
            public RoomInstance Placed;
        }

        private sealed class Link
        {
            public PlannedRoom From;
            public PlannedRoom To;
            public GridPos Cell;
            public Direction Facing;
            public LinkKind Kind;
        }

        private sealed class Plan
        {
            public readonly List<PlannedRoom> Rooms = new List<PlannedRoom>();
            public readonly List<Link> Links = new List<Link>();
            public PlannedRoom Entrance, Hall, Arena, Exit, Shortcut;

            public GridRect Bounds()
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var room in Rooms)
                {
                    minX = Math.Min(minX, room.Rect.X);
                    minY = Math.Min(minY, room.Rect.Y);
                    maxX = Math.Max(maxX, room.Rect.MaxX);
                    maxY = Math.Max(maxY, room.Rect.MaxY);
                }
                return new GridRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }

            public bool Overlaps(GridRect rect)
            {
                foreach (var room in Rooms)
                {
                    if (room.Rect.Overlaps(rect)) return true;
                }
                return false;
            }

            public PlannedRoom Add(RoomTemplateData template, int rotation, GridRect rect)
            {
                var room = new PlannedRoom { Template = template, Rotation = rotation, Rect = rect };
                Rooms.Add(room);
                return room;
            }

            public void Link(PlannedRoom from, PlannedRoom to, GridPos cell, Direction facing, LinkKind kind = LinkKind.Normal)
            {
                Links.Add(new Link { From = from, To = to, Cell = cell, Facing = facing, Kind = kind });
                from.Doors++;
                to.Doors++;
                from.UsedSockets.Add((cell, facing));
                to.UsedSockets.Add((cell + facing.Offset(), facing.Opposite()));
            }
        }

        private static Plan BuildPlan(BossFloorParams parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            var templates = parameters.Templates ?? throw new ArgumentException("ボス階のテンプレートが無い");
            Require(templates.Entrance, 1, 1, "入口");
            Require(templates.Hall, HallWidth, 1, "大扉の手前の部屋");
            Require(templates.Arena, ArenaSize, ArenaSize, "ボス部屋");
            Require(templates.Shortcut, 1, 1, "ショートカットの部屋");

            if (!parameters.HasPath) return FixedPlan(templates, true);

            var pool = new List<RoomTemplateData>();
            foreach (var template in templates.PathRooms)
            {
                if (template != null && template.Weight > 0f && template.Sockets.Count > 0) pool.Add(template);
            }
            if (pool.Count == 0) throw new ArgumentException("ボス階の道のりに使える部屋のテンプレートが無い");

            var random = new Random(parameters.LayoutSeed);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var plan = FixedPlan(templates, false);
                if (TryGrowPath(plan, pool, templates.Entrance, Math.Max(0, parameters.PathRooms), Math.Max(0, parameters.SideRooms), random)) return plan;
            }
            throw new InvalidOperationException($"ボス階の道のり（{parameters.PathRooms} 部屋・脇道 {parameters.SideRooms} 部屋）を {MaxAttempts} 回組んでも置けなかった");
        }

        /// <summary>3x1・ボス部屋・出口・ショートカットの部屋（どの層も同じ）。withEntrance なら入口を 3x1 の真ん中の南に付ける（1 層の形）。</summary>
        private static Plan FixedPlan(BossFloorTemplates templates, bool withEntrance)
        {
            var plan = new Plan();
            if (withEntrance)
            {
                // 入口の部屋はドアが南にしかないので、半周回して北（奥）へ向ける。階段も部屋と一緒に回る。
                plan.Entrance = plan.Add(templates.Entrance, 2, new GridRect(0, -1, 1, 1));
            }
            plan.Hall = plan.Add(templates.Hall, 0, new GridRect(-HallWidth / 2, 0, HallWidth, 1));
            plan.Arena = plan.Add(templates.Arena, 0, new GridRect(-ArenaSize / 2, 1, ArenaSize, ArenaSize));
            plan.Exit = plan.Add(templates.Entrance, 0, new GridRect(0, 1 + ArenaSize, 1, 1));
            plan.Shortcut = plan.Add(templates.Shortcut, 0, new GridRect(plan.Hall.Rect.MaxX + 1, 0, 1, 1));

            if (withEntrance) plan.Link(plan.Entrance, plan.Hall, new GridPos(0, -1), Direction.North);
            plan.Link(plan.Hall, plan.Arena, new GridPos(0, 0), Direction.North, LinkKind.Grand);
            plan.Link(plan.Arena, plan.Exit, new GridPos(0, ArenaSize), Direction.North, LinkKind.Locked);
            plan.Link(plan.Hall, plan.Shortcut, new GridPos(plan.Hall.Rect.MaxX, 0), Direction.East, LinkKind.Shortcut);
            return plan;
        }

        /// <summary>
        /// 3x1 の部屋から道のりの部屋を pathRooms 個つなぎ、最後の部屋に入口を付け、脇道を sideRooms 部屋ぶん生やす。
        /// 道のりは 3x1 の部屋の南か西の辺から始める（北はボス部屋、東はショートカットの部屋）。置き場が尽きたら false。
        /// </summary>
        private static bool TryGrowPath(Plan plan, List<RoomTemplateData> pool, RoomTemplateData stair, int pathRooms, int sideRooms, Random random)
        {
            var path = new List<PlannedRoom>();
            var tail = plan.Hall;
            for (int i = 0; i < pathRooms; i++)
            {
                var next = Attach(plan, tail, SidesFrom(plan, tail), pool, random);
                if (next == null) return false;
                path.Add(next);
                tail = next;
            }

            plan.Entrance = AttachStair(plan, tail, stair, random);
            if (plan.Entrance == null) return false;

            int left = sideRooms;
            int guard = 0;
            while (left > 0)
            {
                if (++guard > 64) return false;
                // 脇道は道のりの部屋（無ければ 3x1 の部屋）から生やす。1〜2 部屋の行き止まり。
                var hosts = path.Count > 0 ? path : new List<PlannedRoom> { plan.Hall };
                var host = hosts[random.Next(hosts.Count)];
                int length = Math.Min(left, 1 + random.Next(2));
                var from = host;
                for (int i = 0; i < length; i++)
                {
                    var room = Attach(plan, from, SidesFrom(plan, from), pool, random);
                    if (room == null) break;
                    left--;
                    from = room;
                }
            }
            return true;
        }

        /// <summary>from の空いているドア候補（sides の向き）のどれかに、pool から引いた部屋をつなぐ。置けなければ null。</summary>
        private static PlannedRoom Attach(Plan plan, PlannedRoom from, Direction[] sides, List<RoomTemplateData> pool, Random random)
        {
            if (!from.Template.AllowsMoreDoors(from.Doors)) return null;

            var doors = FreeSockets(from, sides);
            Shuffle(doors, random);
            foreach (var (cell, facing) in doors)
            {
                for (int tries = 0; tries < 6; tries++)
                {
                    var template = PickWeighted(pool, random);
                    int rotation = template.AllowRotation ? random.Next(4) : 0;
                    var rect = RandomRectTouching(template, rotation, cell + facing.Offset(), facing, random);
                    if (!rect.HasValue || plan.Overlaps(rect.Value) || Touches(rect.Value, plan.Shortcut)) continue;
                    if (!HasSocket(template, rotation, rect.Value, cell + facing.Offset(), facing.Opposite())) continue;

                    var room = plan.Add(template, rotation, rect.Value);
                    plan.Link(from, room, cell, facing);
                    return room;
                }
            }
            return null;
        }

        /// <summary>from の空いている面のどこかに、ドアをそちらへ向けた入口（階段の部屋）を付ける。</summary>
        private static PlannedRoom AttachStair(Plan plan, PlannedRoom from, RoomTemplateData stair, Random random)
        {
            if (!from.Template.AllowsMoreDoors(from.Doors)) return null;

            var doors = FreeSockets(from, SidesFrom(plan, from));
            Shuffle(doors, random);
            foreach (var (cell, facing) in doors)
            {
                var at = cell + facing.Offset();
                var rect = new GridRect(at.X, at.Y, 1, 1);
                // 入口は 3x1・ボス部屋・ショートカットの部屋に接しない所に（すぐ脇に出ると道のりの意味が無い）。
                if (plan.Overlaps(rect) || Touches(rect, plan.Hall) || Touches(rect, plan.Arena) || Touches(rect, plan.Shortcut)) continue;
                // 階段の部屋のドアは南。from の方を向くまで回す（南を時計回りに rotation 回すと facing の逆）。
                int rotation = (((int)facing.Opposite() - (int)Direction.South) % 4 + 4) % 4;
                if (!HasSocket(stair, rotation, rect, at, facing.Opposite())) continue;

                var room = plan.Add(stair, rotation, rect);
                plan.Link(from, room, cell, facing);
                return room;
            }
            return null;
        }

        /// <summary>from から部屋を継げる向き。3x1 の部屋は南と西だけ（北はボス部屋、東はショートカットの部屋）。</summary>
        private static Direction[] SidesFrom(Plan plan, PlannedRoom from) =>
            from == plan.Hall ? new[] { Direction.South, Direction.West } : DirectionUtil.All;

        private static List<(GridPos, Direction)> FreeSockets(PlannedRoom room, Direction[] sides)
        {
            var result = new List<(GridPos, Direction)>();
            foreach (var socket in room.Template.SocketsAfterRotation(room.Rotation))
            {
                if (Array.IndexOf(sides, socket.Facing) < 0) continue;
                var cell = new GridPos(room.Rect.X + socket.LocalCell.X, room.Rect.Y + socket.LocalCell.Y);
                if (!room.UsedSockets.Contains((cell, socket.Facing))) result.Add((cell, socket.Facing));
            }
            return result;
        }

        /// <summary>入口のセル at を含み、facing の向きへ広がる矩形を、at の位置をずらしながら 1 つ引く。</summary>
        private static GridRect? RandomRectTouching(RoomTemplateData template, int rotation, GridPos at, Direction facing, Random random)
        {
            var (w, h) = template.SizeAfterRotation(rotation);
            switch (facing)
            {
                case Direction.North: return new GridRect(at.X - random.Next(w), at.Y, w, h);
                case Direction.South: return new GridRect(at.X - random.Next(w), at.Y - h + 1, w, h);
                case Direction.East: return new GridRect(at.X, at.Y - random.Next(h), w, h);
                case Direction.West: return new GridRect(at.X - w + 1, at.Y - random.Next(h), w, h);
                default: return null;
            }
        }

        /// <summary>rect が room に辺で接するか（重なりは別に見る）。ショートカットの部屋は道のりから離しておく。</summary>
        private static bool Touches(GridRect rect, PlannedRoom room) => room != null && GridRect.Distance(rect, room.Rect) <= 1;

        private static bool HasSocket(RoomTemplateData template, int rotation, GridRect rect, GridPos cell, Direction facing)
        {
            foreach (var socket in template.SocketsAfterRotation(rotation))
            {
                if (socket.Facing == facing && rect.X + socket.LocalCell.X == cell.X && rect.Y + socket.LocalCell.Y == cell.Y) return true;
            }
            return false;
        }

        private static RoomTemplateData PickWeighted(List<RoomTemplateData> pool, Random random)
        {
            double total = 0d;
            foreach (var template in pool) total += template.Weight;
            double roll = random.NextDouble() * total;
            foreach (var template in pool)
            {
                roll -= template.Weight;
                if (roll < 0d) return template;
            }
            return pool[pool.Count - 1];
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void Require(RoomTemplateData template, int width, int height, string label)
        {
            if (template == null) throw new ArgumentException($"ボス階の{label}のテンプレートが無い");
            if (template.Width != width || template.Height != height)
            {
                throw new ArgumentException($"ボス階の{label}（{template.Id}）は {width}x{height} のはずが {template.Width}x{template.Height}");
            }
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
