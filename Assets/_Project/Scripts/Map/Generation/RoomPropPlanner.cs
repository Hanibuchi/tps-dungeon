using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>部屋に置く物の種類。RoomPropSet の一覧と対応する。</summary>
    public enum RoomPropKind
    {
        /// <summary>壁掛けの松明。壁の内側の面に、床から TorchHeight の高さで付ける。</summary>
        Torch = 0,
        /// <summary>床から天井までの柱。</summary>
        Pillar = 1,
        /// <summary>机。長い辺がローカルの Z。</summary>
        Table = 2,
        /// <summary>椅子。机を向いて置く。</summary>
        Chair = 3,
        /// <summary>机の上の小物（ろうそく・本）。Y が机の天板の高さ。</summary>
        TableTop = 4,
        /// <summary>壁ぎわの床の小物（骨・本・薬瓶・ろうそく）。</summary>
        WallClutter = 5,
        /// <summary>壊せる木箱。</summary>
        Crate = 6,
        /// <summary>壊せる宝箱。</summary>
        Chest = 7,
    }

    /// <summary>セルの辺が何か（部屋の外周の辺について問う）。</summary>
    public enum RoomEdgeKind
    {
        Wall = 0,
        Window = 1,
        Door = 2,
    }

    /// <summary>
    /// 置く物 1 つ。座標は FloorBuilder のローカル空間（m。床の上面が Y 0、セル (x, y) の角が (x, y) × cellSize）。
    /// Yaw は Y 軸まわりの角度（度）で、置く物のプレハブの正面（+Z）が向く方角。0 で北（+Z）、90 で東（+X）。
    /// Variant は一覧から選ぶための乱数（0 以上）。
    /// </summary>
    public readonly struct RoomPropPlacement
    {
        public readonly RoomPropKind Kind;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float Yaw;
        public readonly int Variant;

        public RoomPropPlacement(RoomPropKind kind, float x, float y, float z, float yaw, int variant)
        {
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Variant = variant;
        }

        public override string ToString() => $"{Kind}({X:0.##},{Y:0.##},{Z:0.##} yaw {Yaw:0})";
    }

    /// <summary>部屋の置き物の量と大きさ。層ごとの量は FloorConfig（Floors.csv）から、大きさは RoomPropSet から入れる。</summary>
    public sealed class RoomPropSettings
    {
        public float CellSize = 5f;
        /// <summary>普通の部屋のセル 1 つあたりの木箱の数の平均。</summary>
        public float CratesPerCell = 0.5f;
        /// <summary>普通の部屋 1 つに宝箱を 1 つ置く確率。</summary>
        public float ChestChance = 0.2f;
        /// <summary>ボス階か（松明と柱だけにする）。</summary>
        public bool BossFloor;
        /// <summary>闘技場の部屋のテンプレート ID（ボス階で真ん中を空け、柱を立てる）。</summary>
        public string BossArenaId = "Room_BossArena";

        public float TorchHeight = 2.6f;
        /// <summary>壁の厚みの半分（壁の中心から内側の面まで、m）。</summary>
        public float WallHalfThickness = 0.075f;
        /// <summary>ドアの前に空けておく幅（壁に沿って）と奥行き（m）。</summary>
        public float DoorClearWidth = 2.6f;
        public float DoorClearDepth = 2.4f;

        public float PillarSize = 1.1f;
        public float TableWidth = 1.5f;
        public float TableLength = 2.3f;
        public float TableTopHeight = 1f;
        public float ChairSize = 0.65f;
        public float CrateSize = 0.85f;
        public float ChestWidth = 1.2f;
        public float ChestDepth = 0.7f;
        public float ClutterSize = 0.6f;
    }

    /// <summary>
    /// 部屋 1 つの置き物を決める。UnityEngine に依存しない。同じ部屋・同じ乱数なら同じ結果になる。
    /// - 空けておく所：ドアの内側（DoorClearWidth × DoorClearDepth）、入口・出口・ショップの FeatureCell（階段・目印）、ボス階の闘技場の真ん中 3 × 3 セル。
    /// - 松明：窓でもドアでもない壁に、1 辺おきに付ける。階段のセルの、上り下りする向きの両端の壁には付けない。
    /// - 柱：3 × 3 セル以上の普通の部屋で半分の確率、内側の格子の 4 つの角に。闘技場は真ん中 3 × 3 セルの 4 つの角に。
    /// - 机と椅子：2 × 2 セル以上の普通の部屋で 40%。真ん中寄りに、机の上の小物と一緒に。
    /// - 壁ぎわの小物：ボス階以外の部屋に、広さに合わせて 1〜8 つ。
    /// - 木箱：普通の部屋に、セル数 × CratesPerCell 個くらいを、壁ぎわに 1〜3 個のまとまりで。
    /// - 宝箱：普通の部屋に ChestChance の確率で 1 つ。壁ぎわに、正面を部屋の内側へ向けて。
    /// 置く物どうしは床の上の四角（向きは 90 度刻みなので軸に沿う）で重ならないようにする。
    /// </summary>
    public static class RoomPropPlanner
    {
        /// <summary>置き物の乱数の種の salt（RoomEnemyRoller.RoomSeed）。</summary>
        public const int PropSalt = 3;

        private readonly struct Box
        {
            public readonly float MinX, MinZ, MaxX, MaxZ;

            public Box(float cx, float cz, float sizeX, float sizeZ)
            {
                MinX = cx - sizeX * 0.5f;
                MaxX = cx + sizeX * 0.5f;
                MinZ = cz - sizeZ * 0.5f;
                MaxZ = cz + sizeZ * 0.5f;
            }

            public bool Overlaps(Box o) => MinX < o.MaxX && o.MinX < MaxX && MinZ < o.MaxZ && o.MinZ < MaxZ;
        }

        /// <summary>部屋の外周の辺 1 本（部屋のセルと、外を向く方角）。</summary>
        private readonly struct Side
        {
            public readonly GridPos Cell;
            public readonly Direction Facing;
            public readonly RoomEdgeKind Kind;

            public Side(GridPos cell, Direction facing, RoomEdgeKind kind)
            {
                Cell = cell;
                Facing = facing;
                Kind = kind;
            }
        }

        private sealed class Plan
        {
            public readonly List<RoomPropPlacement> Placements = new List<RoomPropPlacement>();
            public readonly List<Box> Reserved = new List<Box>();
            public Box Room;

            public bool Fits(Box box)
            {
                if (box.MinX < Room.MinX || box.MaxX > Room.MaxX || box.MinZ < Room.MinZ || box.MaxZ > Room.MaxZ) return false;
                foreach (Box r in Reserved)
                {
                    if (r.Overlaps(box)) return false;
                }
                return true;
            }
        }

        /// <param name="edgeAt">部屋のセルの、外を向く辺が何か。</param>
        public static List<RoomPropPlacement> PlanRoom(RoomInstance room, Func<GridPos, Direction, RoomEdgeKind> edgeAt, RoomPropSettings settings, Random random)
        {
            float cell = settings.CellSize;
            var bounds = room.Bounds;
            var plan = new Plan
            {
                Room = new Box((bounds.X + bounds.Width * 0.5f) * cell, (bounds.Y + bounds.Height * 0.5f) * cell,
                    bounds.Width * cell - settings.WallHalfThickness * 2f, bounds.Height * cell - settings.WallHalfThickness * 2f),
            };

            List<Side> sides = Sides(room, edgeAt);
            bool arena = settings.BossFloor && room.Template != null && room.Template.Id == settings.BossArenaId;
            bool normal = !settings.BossFloor && room.Role == RoomRole.Normal;

            ReserveDoors(plan, sides, settings);
            if (room.Role != RoomRole.Normal) plan.Reserved.Add(CellBox(room.FeatureCell, cell, 0f));
            if (arena && bounds.Width >= 3 && bounds.Height >= 3)
            {
                var center = bounds.Center;
                plan.Reserved.Add(new Box((center.X + 0.5f) * cell, (center.Y + 0.5f) * cell, 3f * cell, 3f * cell));
            }

            PlaceTorches(plan, room, sides, settings, random);
            if (arena) PlaceArenaPillars(plan, bounds, settings, random);
            else if (normal && bounds.Width >= 3 && bounds.Height >= 3 && random.NextDouble() < 0.5d) PlacePillars(plan, bounds, settings, random);
            if (settings.BossFloor) return plan.Placements;

            if (normal && bounds.Width >= 2 && bounds.Height >= 2 && random.NextDouble() < 0.4d) PlaceTableSet(plan, bounds, settings, random);
            if (normal)
            {
                if (random.NextDouble() < settings.ChestChance) PlaceChest(plan, sides, settings, random);
                PlaceCrates(plan, sides, bounds.Area, settings, random);
            }
            // 小物は部屋の広さに合わせて（1 セルで 1〜2、セル 2 つごとに 1 つ増やし、8 まで）。
            PlaceClutter(plan, sides, Math.Min(8, 1 + random.Next(2) + bounds.Area / 2), settings, random);
            return plan.Placements;
        }

        // ---- 外周 ----

        private static List<Side> Sides(RoomInstance room, Func<GridPos, Direction, RoomEdgeKind> edgeAt)
        {
            var sides = new List<Side>();
            var b = room.Bounds;
            for (int x = b.X; x <= b.MaxX; x++)
            {
                sides.Add(new Side(new GridPos(x, b.Y), Direction.South, edgeAt(new GridPos(x, b.Y), Direction.South)));
                sides.Add(new Side(new GridPos(x, b.MaxY), Direction.North, edgeAt(new GridPos(x, b.MaxY), Direction.North)));
            }
            for (int y = b.Y; y <= b.MaxY; y++)
            {
                sides.Add(new Side(new GridPos(b.X, y), Direction.West, edgeAt(new GridPos(b.X, y), Direction.West)));
                sides.Add(new Side(new GridPos(b.MaxX, y), Direction.East, edgeAt(new GridPos(b.MaxX, y), Direction.East)));
            }
            return sides;
        }

        /// <summary>辺の中点から、部屋の内側へ inset だけ入った点と、内側を向く角度。along は辺に沿ったずれ（-0.5〜0.5 × セル）。</summary>
        private static (float x, float z, float yaw) Inside(Side side, float along, float inset, float cell)
        {
            var step = side.Facing.Offset();
            float cx = (side.Cell.X + 0.5f + step.X * 0.5f) * cell;
            float cz = (side.Cell.Y + 0.5f + step.Y * 0.5f) * cell;
            // 辺に沿った向き（外向きを時計回りに 90 度）。
            var tangent = side.Facing.RotateCw(1).Offset();
            float x = cx - step.X * inset + tangent.X * along * cell;
            float z = cz - step.Y * inset + tangent.Y * along * cell;
            return (x, z, 90f * (int)side.Facing.Opposite());
        }

        private static bool AlongX(Direction facing) => facing == Direction.North || facing == Direction.South;

        private static void ReserveDoors(Plan plan, List<Side> sides, RoomPropSettings settings)
        {
            foreach (Side side in sides)
            {
                if (side.Kind != RoomEdgeKind.Door) continue;
                var (x, z, _) = Inside(side, 0f, settings.DoorClearDepth * 0.5f, settings.CellSize);
                bool alongX = AlongX(side.Facing);
                plan.Reserved.Add(new Box(x, z, alongX ? settings.DoorClearWidth : settings.DoorClearDepth, alongX ? settings.DoorClearDepth : settings.DoorClearWidth));
            }
        }

        private static Box CellBox(GridPos cell, float cellSize, float shrink) =>
            new Box((cell.X + 0.5f) * cellSize, (cell.Y + 0.5f) * cellSize, cellSize - shrink * 2f, cellSize - shrink * 2f);

        // ---- 置く ----

        private static bool TryPlace(Plan plan, RoomPropKind kind, float x, float z, float sizeX, float sizeZ, float yaw, Random random)
        {
            var box = new Box(x, z, sizeX, sizeZ);
            if (!plan.Fits(box)) return false;
            plan.Reserved.Add(box);
            plan.Placements.Add(new RoomPropPlacement(kind, x, 0f, z, yaw, random.Next(1 << 16)));
            return true;
        }

        private static void PlaceTorches(Plan plan, RoomInstance room, List<Side> sides, RoomPropSettings settings, Random random)
        {
            // 階段は部屋と一緒に回してあり、南のドアから北の壁へ上り下りする。その両端の壁には付けない（段に掛かる）。
            var stairEnds = new[] { Direction.North.RotateCw(room.Rotation), Direction.South.RotateCw(room.Rotation) };
            bool stairRoom = room.Role == RoomRole.Entrance || room.Role == RoomRole.Exit;
            foreach (Side side in sides)
            {
                if (side.Kind != RoomEdgeKind.Wall) continue;
                if (stairRoom && side.Cell == room.FeatureCell && Array.IndexOf(stairEnds, side.Facing) >= 0) continue;
                // 1 辺おき（市松）に付ける。
                if (((side.Cell.X + side.Cell.Y) & 1) != 0) continue;
                var (x, z, yaw) = Inside(side, 0f, settings.WallHalfThickness, settings.CellSize);
                plan.Placements.Add(new RoomPropPlacement(RoomPropKind.Torch, x, settings.TorchHeight, z, yaw, random.Next(1 << 16)));
            }
        }

        private static void PlacePillars(Plan plan, GridRect b, RoomPropSettings settings, Random random)
        {
            float cell = settings.CellSize;
            foreach (var (gx, gz) in new[] { (b.X + 1, b.Y + 1), (b.MaxX, b.Y + 1), (b.X + 1, b.MaxY), (b.MaxX, b.MaxY) })
            {
                TryPlace(plan, RoomPropKind.Pillar, gx * cell, gz * cell, settings.PillarSize, settings.PillarSize, 0f, random);
            }
        }

        private static void PlaceArenaPillars(Plan plan, GridRect b, RoomPropSettings settings, Random random)
        {
            if (b.Width < 3 || b.Height < 3) return;
            float cell = settings.CellSize;
            var c = b.Center;
            float half = 1.5f * cell + settings.PillarSize * 0.5f + 0.1f;
            float cx = (c.X + 0.5f) * cell, cz = (c.Y + 0.5f) * cell;
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                TryPlace(plan, RoomPropKind.Pillar, cx + sx * half, cz + sz * half, settings.PillarSize, settings.PillarSize, 0f, random);
            }
        }

        private static void PlaceTableSet(Plan plan, GridRect b, RoomPropSettings settings, Random random)
        {
            float cell = settings.CellSize;
            bool lengthAlongX = random.NextDouble() < 0.5d;
            float yaw = lengthAlongX ? 90f : 0f;
            float sizeX = lengthAlongX ? settings.TableLength : settings.TableWidth;
            float sizeZ = lengthAlongX ? settings.TableWidth : settings.TableLength;
            // 椅子の分の余白を含めた四角。
            float chairReach = settings.ChairSize + 0.2f;
            float outerX = sizeX + (lengthAlongX ? 0f : chairReach * 2f);
            float outerZ = sizeZ + (lengthAlongX ? chairReach * 2f : 0f);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float x = (b.X + b.Width * 0.5f) * cell + (float)(random.NextDouble() - 0.5d) * cell * (b.Width - 1) * 0.6f;
                float z = (b.Y + b.Height * 0.5f) * cell + (float)(random.NextDouble() - 0.5d) * cell * (b.Height - 1) * 0.6f;
                var outer = new Box(x, z, outerX, outerZ);
                if (!plan.Fits(outer)) continue;

                plan.Reserved.Add(outer);
                plan.Placements.Add(new RoomPropPlacement(RoomPropKind.Table, x, 0f, z, yaw, random.Next(1 << 16)));

                // 長い辺の両側に椅子を 1〜2 脚ずつ、机を向けて。
                float offset = (lengthAlongX ? sizeZ : sizeX) * 0.5f + settings.ChairSize * 0.35f;
                float spread = (lengthAlongX ? sizeX : sizeZ) * 0.25f;
                foreach (int sideSign in new[] { -1, 1 })
                {
                    int chairs = 1 + random.Next(2);
                    for (int i = 0; i < chairs; i++)
                    {
                        float t = chairs == 1 ? (float)(random.NextDouble() - 0.5d) * spread : (i == 0 ? -spread : spread);
                        float cx = lengthAlongX ? x + t : x + sideSign * offset;
                        float cz = lengthAlongX ? z + sideSign * offset : z + t;
                        // 椅子の正面（+Z）を机へ向ける。
                        float chairYaw = lengthAlongX ? (sideSign < 0 ? 0f : 180f) : (sideSign < 0 ? 90f : 270f);
                        chairYaw += (float)(random.NextDouble() - 0.5d) * 20f;
                        plan.Placements.Add(new RoomPropPlacement(RoomPropKind.Chair, cx, 0f, cz, chairYaw, random.Next(1 << 16)));
                    }
                }

                int tops = 1 + random.Next(2);
                for (int i = 0; i < tops; i++)
                {
                    float t = (float)(random.NextDouble() - 0.5d) * (lengthAlongX ? sizeX : sizeZ) * 0.6f;
                    float u = (float)(random.NextDouble() - 0.5d) * (lengthAlongX ? sizeZ : sizeX) * 0.3f;
                    float tx = lengthAlongX ? x + t : x + u;
                    float tz = lengthAlongX ? z + u : z + t;
                    plan.Placements.Add(new RoomPropPlacement(RoomPropKind.TableTop, tx, settings.TableTopHeight, tz, (float)random.NextDouble() * 360f, random.Next(1 << 16)));
                }
                return;
            }
        }

        /// <summary>壁（窓も）ぎわの辺を選び、辺に沿ってずらした所を返す。ドアの辺は選ばない。</summary>
        private static bool PickWallSide(List<Side> sides, Random random, out Side side)
        {
            int count = 0;
            foreach (Side s in sides) if (s.Kind != RoomEdgeKind.Door) count++;
            side = default;
            if (count == 0) return false;

            int pick = random.Next(count);
            foreach (Side s in sides)
            {
                if (s.Kind == RoomEdgeKind.Door) continue;
                if (pick-- == 0)
                {
                    side = s;
                    return true;
                }
            }
            return false;
        }

        private static void PlaceChest(Plan plan, List<Side> sides, RoomPropSettings settings, Random random)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!PickWallSide(sides, random, out Side side)) return;
                float along = (float)(random.NextDouble() - 0.5d) * 0.7f;
                var (x, z, yaw) = Inside(side, along, settings.WallHalfThickness + settings.ChestDepth * 0.5f + 0.15f, settings.CellSize);
                bool alongX = AlongX(side.Facing);
                if (TryPlace(plan, RoomPropKind.Chest, x, z, alongX ? settings.ChestWidth : settings.ChestDepth, alongX ? settings.ChestDepth : settings.ChestWidth, yaw, random)) return;
            }
        }

        private static void PlaceCrates(Plan plan, List<Side> sides, int area, RoomPropSettings settings, Random random)
        {
            double expected = area * Math.Max(0f, settings.CratesPerCell);
            int count = (int)expected + (random.NextDouble() < expected - Math.Floor(expected) ? 1 : 0);
            float size = settings.CrateSize;
            int placed = 0;
            for (int attempt = 0; placed < count && attempt < count * 4 + 4; attempt++)
            {
                if (!PickWallSide(sides, random, out Side side)) return;
                // まとまりの 1 個目。続きは壁に沿って隣へ並べる（入らなければそこで終わり）。
                int cluster = Math.Min(count - placed, 1 + random.Next(3));
                float along = (float)(random.NextDouble() - 0.5d) * 0.8f;
                float step = (size + 0.08f) / settings.CellSize * (random.NextDouble() < 0.5d ? 1f : -1f);
                for (int i = 0; i < cluster; i++)
                {
                    float inset = settings.WallHalfThickness + size * 0.5f + 0.08f + (float)random.NextDouble() * 0.1f;
                    var (x, z, yaw) = Inside(side, along + step * i, inset, settings.CellSize);
                    // 箱は少しだけ斜めに（±10 度。斜めにした分、床の上の四角を広めに取る）。
                    yaw += (float)(random.NextDouble() - 0.5d) * 20f;
                    if (!TryPlace(plan, RoomPropKind.Crate, x, z, size * 1.15f, size * 1.15f, yaw, random)) break;
                    placed++;
                }
            }
        }

        private static void PlaceClutter(Plan plan, List<Side> sides, int count, RoomPropSettings settings, Random random)
        {
            int placed = 0;
            for (int attempt = 0; placed < count && attempt < count * 5; attempt++)
            {
                if (!PickWallSide(sides, random, out Side side)) return;
                float along = (float)(random.NextDouble() - 0.5d) * 0.85f;
                var (x, z, yaw) = Inside(side, along, settings.WallHalfThickness + settings.ClutterSize * 0.5f + 0.05f, settings.CellSize);
                // 壁に沿って置くので、向きをずらすのは少しだけ。
                yaw += (float)(random.NextDouble() - 0.5d) * 30f;
                if (TryPlace(plan, RoomPropKind.WallClutter, x, z, settings.ClutterSize, settings.ClutterSize, yaw, random)) placed++;
            }
        }
    }
}
