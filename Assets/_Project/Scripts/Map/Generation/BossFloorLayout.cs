using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>ボス階を組むのに使う 3 つの部屋テンプレート。</summary>
    public sealed class BossFloorTemplates
    {
        /// <summary>
        /// 入口（下り階段）と出口（上り階段）の部屋。1x1 で、南の辺にドア候補を持つ。
        /// 入口は回して北へ向け、出口はそのまま（ドアが南のボス部屋の側）。
        /// </summary>
        public RoomTemplateData Entrance;
        /// <summary>入口を出てすぐの横長の部屋。3x1。</summary>
        public RoomTemplateData Hall;
        /// <summary>大扉の先のボス部屋。5x5。奥の壁の真ん中の閉ざされた扉の先に出口の部屋。</summary>
        public RoomTemplateData Arena;
    }

    /// <summary>
    /// 間取りが決まっているボス階のレイアウト。北が奥で、左右対称。
    /// <code>
    ///        [出口]        上り階段。ボスを倒すまで扉が閉ざされている
    ///        ╫ 閉ざされた扉
    ///   [ 5x5 ボス部屋 ]
    ///        ╫ 大扉
    ///    [ 3x1 の部屋 ★]   ★ ショートカット解放用の場所（東端）
    ///        [入口]
    /// </code>
    /// 部屋はグリッドの真ん中に置く（階の外の景色はグリッドの真ん中を基準に置くため）。
    /// シードは窓の並びにだけ効き、形はいつも同じ。UnityEngine には依存しない。
    /// </summary>
    public static class BossFloorLayout
    {
        public const int HallWidth = 3;
        public const int ArenaSize = 5;

        /// <summary>入口・3x1・ボス部屋・出口を縦に積んだ高さ（セル数）。</summary>
        public const int TotalHeight = 1 + 1 + ArenaSize + 1;

        public static FloorLayout Generate(BossFloorTemplates templates, int width, int height, int seed)
        {
            if (templates == null) throw new ArgumentNullException(nameof(templates));
            Require(templates.Entrance, 1, 1, "入口");
            Require(templates.Hall, HallWidth, 1, "入口を出てすぐの部屋");
            Require(templates.Arena, ArenaSize, ArenaSize, "ボス部屋");
            if (width < ArenaSize || height < TotalHeight)
            {
                throw new ArgumentException($"ボス階のグリッド {width}x{height} が狭すぎる（{ArenaSize}x{TotalHeight} 以上要る）");
            }

            int center = (width - 1) / 2;
            int bottom = (height - TotalHeight) / 2;

            var rooms = new List<RoomInstance>();
            // 入口の部屋はドアが南にしかないので、半周回して北（奥）へ向ける。階段も部屋と一緒に回る。
            var entrance = Place(rooms, templates.Entrance, 2, new GridRect(center, bottom, 1, 1));
            var hall = Place(rooms, templates.Hall, 0, new GridRect(center - HallWidth / 2, bottom + 1, HallWidth, 1));
            var arena = Place(rooms, templates.Arena, 0, new GridRect(center - ArenaSize / 2, bottom + 2, ArenaSize, ArenaSize));
            var exit = Place(rooms, templates.Entrance, 0, new GridRect(center, arena.Bounds.MaxY + 1, 1, 1));

            var edges = new List<RoomEdge>();
            Connect(edges, entrance, hall, new GridPos(center, bottom), Direction.North);
            var grand = Connect(edges, hall, arena, new GridPos(center, bottom + 1), Direction.North);
            grand.IsGrandDoor = true;
            var gate = Connect(edges, arena, exit, new GridPos(center, arena.Bounds.MaxY), Direction.North);
            gate.IsLocked = true;

            entrance.Role = RoomRole.Entrance;
            exit.Role = RoomRole.Exit;

            return new FloorLayout(seed, width, height, rooms, edges)
            {
                EntranceRoom = entrance.Index,
                ExitRoom = exit.Index,
                ShopRoom = -1,
                ShortcutCell = new GridPos(hall.Bounds.MaxX, hall.Bounds.Y),
                MirrorColumn = center,
            };
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
