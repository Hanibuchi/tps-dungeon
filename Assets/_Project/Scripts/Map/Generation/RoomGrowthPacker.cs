using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>成長パッキング 1 回分の結果。</summary>
    public sealed class PackResult
    {
        public List<RoomInstance> Rooms { get; }
        public List<RoomEdge> Edges { get; }

        public PackResult(List<RoomInstance> rooms, List<RoomEdge> edges)
        {
            Rooms = rooms;
            Edges = edges;
        }
    }

    /// <summary>
    /// 中心の部屋から、未使用のドア候補を辿って部屋を貼り付けていく成長パッキング。
    /// 新しい部屋は「自分のドア候補が相手のドア候補の真向かいに来る」位置に置くので、
    /// 2 つの部屋は必ず辺を共有し、その 1 マスがそのままドアになる。廊下は掘らない。
    /// 埋まらなかったセルは掘られていない岩盤として残るため、フロアの外形はデコボコになる。
    ///
    /// Run() は何度でも呼べる。作り直すときも同じ Random を回し続けることで、
    /// 「同じシードなら必ず同じフロア」という決定性を保つ。
    /// </summary>
    public sealed class RoomGrowthPacker
    {
        private const int Vacant = -1;

        private readonly struct FrontierEntry
        {
            public readonly int RoomIndex;
            public readonly int SocketIndex;

            public FrontierEntry(int roomIndex, int socketIndex)
            {
                RoomIndex = roomIndex;
                SocketIndex = socketIndex;
            }
        }

        /// <summary>そのソケットに吸着させられる部屋の置き方 1 通り。</summary>
        private readonly struct Candidate
        {
            public readonly int TemplateIndex;
            public readonly int Rotation;
            public readonly int SocketIndex;
            public readonly GridRect Bounds;

            public Candidate(int templateIndex, int rotation, int socketIndex, GridRect bounds)
            {
                TemplateIndex = templateIndex;
                Rotation = rotation;
                SocketIndex = socketIndex;
                Bounds = bounds;
            }
        }

        private readonly FloorGenerationParams p;
        private readonly Random rng;

        /// <summary>[テンプレート][回転] の回転済みドア候補。毎回作ると重いので 1 度だけ作る。</summary>
        private readonly List<DoorSocketData>[][] rotatedSockets;

        /// <summary>セルを占めている部屋の番号。Vacant は岩盤。</summary>
        private readonly int[] roomGrid;

        private readonly List<RoomInstance> rooms = new List<RoomInstance>();
        private readonly List<RoomEdge> edges = new List<RoomEdge>();
        private readonly List<FrontierEntry> frontier = new List<FrontierEntry>();

        /// <summary>まだ置けていない必須タグ。残り枠が少なくなったらこれを優先して置く。</summary>
        private readonly List<RoomTag> quota = new List<RoomTag>();

        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<float> candidateWeights = new List<float>();

        public RoomGrowthPacker(FloorGenerationParams parameters, Random random)
        {
            p = parameters;
            rng = random;
            roomGrid = new int[p.Width * p.Height];

            rotatedSockets = new List<DoorSocketData>[p.Templates.Count][];
            for (int i = 0; i < p.Templates.Count; i++)
            {
                var template = p.Templates[i];
                rotatedSockets[i] = new List<DoorSocketData>[4];
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    // 回転を許さないテンプレートは 0 以外を null のままにして、候補列挙から外す。
                    if (rotation > 0 && !template.AllowRotation) continue;
                    rotatedSockets[i][rotation] = template.SocketsAfterRotation(rotation);
                }
            }
        }

        public PackResult Run()
        {
            Reset();
            if (!PlaceSeedRoom()) return Snapshot();

            int target = rng.Next(p.MinRoomCount, p.MaxRoomCount + 1);
            while (rooms.Count < target && frontier.Count > 0)
            {
                var entry = PopRandom();
                var socket = rooms[entry.RoomIndex].Sockets[entry.SocketIndex];
                if (socket.IsUsed) continue;

                // 置けなかったソケットはフロンティアから落とすだけ。壁のまま残り、後で追加ドアの候補になる。
                TryGrow(entry.RoomIndex, socket, target);
            }

            AddLoopDoors();
            return Snapshot();
        }

        /// <summary>次の Run() で作業用リストを使い回すので、結果は複製して返す。</summary>
        private PackResult Snapshot() =>
            new PackResult(new List<RoomInstance>(rooms), new List<RoomEdge>(edges));

        private void Reset()
        {
            for (int i = 0; i < roomGrid.Length; i++) roomGrid[i] = Vacant;
            rooms.Clear();
            edges.Clear();
            frontier.Clear();

            // 積むのは「実際に置けるタグ」だけ。持っているテンプレートが無いタグを積むと、
            // ForcedTag が先頭のそれを返し続けて、後ろに並んだタグが一度も強制されなくなる。
            quota.Clear();
            if (HasPlaceableTemplateWithTag(RoomTag.Stair))
            {
                quota.Add(RoomTag.Stair);
                quota.Add(RoomTag.Stair);
            }
            if (p.IncludeShop && HasPlaceableTemplateWithTag(RoomTag.Shop)) quota.Add(RoomTag.Shop);
        }

        /// <summary>そのタグを持ち、かつ抽選対象になるテンプレートがあるか。</summary>
        private bool HasPlaceableTemplateWithTag(RoomTag tag)
        {
            foreach (var template in p.Templates)
            {
                if (template.Weight > 0f && (template.Tags & tag) != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 最初の部屋はフロア中心付近に置く。階段用テンプレートを優先するのは、階段部屋が
        /// 1 つも出ないシードを避けるため。どの部屋が階段になるかは SpecialRoomAssigner が
        /// グラフ距離で決めるので、この種部屋がそのまま階段になるわけではない。
        /// </summary>
        private bool PlaceSeedRoom()
        {
            int templateIndex = PickTemplateWithTag(RoomTag.Stair);
            if (templateIndex < 0) templateIndex = PickTemplateWithTag(null);
            if (templateIndex < 0) return false;

            var template = p.Templates[templateIndex];
            int rotation = PickRotation(template);
            var (width, height) = template.SizeAfterRotation(rotation);

            int centerX = p.Width / 2 + Jitter();
            int centerY = p.Height / 2 + Jitter();
            int x = Clamp(centerX - width / 2, 0, p.Width - width);
            int y = Clamp(centerY - height / 2, 0, p.Height - height);

            var room = PlaceRoom(templateIndex, rotation, new GridRect(x, y, width, height));
            PushFrontier(room, -1);
            return true;
        }

        private bool TryGrow(int parentIndex, PlacedSocket parentSocket, int target)
        {
            var targetCell = parentSocket.ExitCell;
            if (!InsideGrid(targetCell) || roomGrid[Index(targetCell)] != Vacant) return false;

            var inward = parentSocket.Facing.Opposite();
            var forced = ForcedTag(target);

            int found = CollectCandidates(targetCell, inward, forced);
            // 必須タグの部屋がここに収まらないなら枠は次のソケットに持ち越し、今回は普通に埋める。
            if (found == 0 && forced.HasValue) found = CollectCandidates(targetCell, inward, null);
            if (found == 0) return false;

            var chosen = PickWeighted();
            var room = PlaceRoom(chosen.TemplateIndex, chosen.Rotation, chosen.Bounds);
            Connect(parentIndex, parentSocket, room, chosen.SocketIndex);
            PushFrontier(room, chosen.SocketIndex);
            return true;
        }

        /// <summary>
        /// targetCell を覆い、そこに inward 向きのドア候補を持つ部屋の置き方を全部集める。
        /// 原点はドア候補の位置から逆算するので、ソケットの位置が「合う / 合わない」は起きない。
        /// 弾かれるのはグリッドからはみ出す場合と、既存の部屋と重なる場合だけ。
        /// </summary>
        private int CollectCandidates(GridPos targetCell, Direction inward, RoomTag? requiredTag)
        {
            candidates.Clear();
            candidateWeights.Clear();

            for (int t = 0; t < p.Templates.Count; t++)
            {
                var template = p.Templates[t];
                if (template.Weight <= 0f) continue;
                if (requiredTag.HasValue && (template.Tags & requiredTag.Value) == 0) continue;

                int before = candidates.Count;
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var local = rotatedSockets[t][rotation];
                    if (local == null) continue;

                    var (width, height) = template.SizeAfterRotation(rotation);
                    for (int s = 0; s < local.Count; s++)
                    {
                        if (local[s].Facing != inward) continue;

                        var origin = targetCell - local[s].LocalCell;
                        var bounds = new GridRect(origin.X, origin.Y, width, height);
                        if (!FitsInGrid(bounds)) continue;
                        if (OverlapsAnyRoom(bounds)) continue;

                        candidates.Add(new Candidate(t, rotation, s, bounds));
                    }
                }

                // 回転やドア候補が多いテンプレートが有利にならないよう、出した候補数で重みを割る。
                int produced = candidates.Count - before;
                if (produced == 0) continue;

                float share = template.Weight / produced;
                for (int i = 0; i < produced; i++) candidateWeights.Add(share);
            }

            return candidates.Count;
        }

        private Candidate PickWeighted()
        {
            float total = 0f;
            foreach (float weight in candidateWeights) total += weight;

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidateWeights[i];
                if (roll <= 0d) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private RoomInstance PlaceRoom(int templateIndex, int rotation, GridRect bounds)
        {
            var template = p.Templates[templateIndex];
            var local = rotatedSockets[templateIndex][rotation];

            var sockets = new List<PlacedSocket>(local.Count);
            foreach (var socket in local)
            {
                sockets.Add(new PlacedSocket(
                    new GridPos(bounds.X + socket.LocalCell.X, bounds.Y + socket.LocalCell.Y), socket.Facing));
            }

            var room = new RoomInstance(rooms.Count, template, rotation, bounds, sockets);
            rooms.Add(room);
            Stamp(bounds, room.Index);
            ConsumeQuota(template.Tags);
            return room;
        }

        private void Connect(int roomA, PlacedSocket socketA, RoomInstance roomB, int socketIndexB)
        {
            var socketB = roomB.Sockets[socketIndexB];
            var edge = new RoomEdge(edges.Count, roomA, roomB.Index)
            {
                SocketA = socketA,
                SocketB = socketB,
            };
            edges.Add(edge);
            socketA.UsedByEdge = edge.Index;
            socketB.UsedByEdge = edge.Index;
        }

        /// <summary>
        /// 使ったソケット以外をフロンティアに積む。
        /// この時点で外が埋まっている／グリッド外のものは、どう転んでも置けないので積まない。
        /// </summary>
        private void PushFrontier(RoomInstance room, int skipSocketIndex)
        {
            for (int i = 0; i < room.Sockets.Count; i++)
            {
                if (i == skipSocketIndex) continue;

                var exit = room.Sockets[i].ExitCell;
                if (!InsideGrid(exit) || roomGrid[Index(exit)] != Vacant) continue;

                frontier.Add(new FrontierEntry(room.Index, i));
            }
        }

        private FrontierEntry PopRandom()
        {
            int i = rng.Next(frontier.Count);
            var entry = frontier[i];
            frontier[i] = frontier[frontier.Count - 1];
            frontier.RemoveAt(frontier.Count - 1);
            return entry;
        }

        /// <summary>
        /// たまたま向かい合っている未使用ソケット対をドアにして、返り道を作る。
        /// 既に辺で繋がっている部屋ペアは除く。同じサイズの部屋同士だと 2 対とも揃うことがあり、
        /// 除かないと「同じ 2 部屋の 2 枚目のドア」で枠を使い切って本当のループができない。
        /// </summary>
        private void AddLoopDoors()
        {
            if (p.ExtraDoorCount <= 0) return;

            var unused = new Dictionary<(GridPos, Direction), (int room, int socket)>();
            foreach (var room in rooms)
            {
                for (int i = 0; i < room.Sockets.Count; i++)
                {
                    var socket = room.Sockets[i];
                    if (!socket.IsUsed) unused[(socket.Cell, socket.Facing)] = (room.Index, i);
                }
            }

            var connected = new HashSet<long>();
            foreach (var edge in edges) connected.Add(PairKey(edge.RoomA, edge.RoomB));

            // 列挙順は部屋番号 → ソケット定義順で決まるので、Dictionary の並びには依存しない。
            // 同じ境界を 2 回拾わないよう、北と東を向いた側からだけ見る。
            var pairs = new List<(int roomA, int socketA, int roomB, int socketB)>();
            foreach (var room in rooms)
            {
                for (int i = 0; i < room.Sockets.Count; i++)
                {
                    var socket = room.Sockets[i];
                    if (socket.IsUsed) continue;
                    if (socket.Facing != Direction.North && socket.Facing != Direction.East) continue;
                    if (!unused.TryGetValue((socket.ExitCell, socket.Facing.Opposite()), out var other)) continue;
                    if (other.room == room.Index) continue;
                    if (connected.Contains(PairKey(room.Index, other.room))) continue;

                    pairs.Add((room.Index, i, other.room, other.socket));
                }
            }

            Shuffle(pairs);

            int added = 0;
            foreach (var pair in pairs)
            {
                if (added >= p.ExtraDoorCount) break;

                var socketA = rooms[pair.roomA].Sockets[pair.socketA];
                var socketB = rooms[pair.roomB].Sockets[pair.socketB];
                if (socketA.IsUsed || socketB.IsUsed) continue;
                if (!connected.Add(PairKey(pair.roomA, pair.roomB))) continue;

                Connect(pair.roomA, socketA, rooms[pair.roomB], pair.socketB);
                added++;
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>残り枠が必須タグの数と同じになったら、そのタグの部屋を優先して置く。</summary>
        private RoomTag? ForcedTag(int target)
        {
            if (quota.Count == 0) return null;
            return target - rooms.Count <= quota.Count ? quota[0] : (RoomTag?)null;
        }

        private void ConsumeQuota(RoomTag tags)
        {
            for (int i = 0; i < quota.Count; i++)
            {
                if ((tags & quota[i]) == 0) continue;
                quota.RemoveAt(i);
                return;
            }
        }

        private int PickTemplateWithTag(RoomTag? tag)
        {
            float total = 0f;
            for (int i = 0; i < p.Templates.Count; i++)
            {
                if (!IsSeedCandidate(i, tag)) continue;
                total += p.Templates[i].Weight;
            }
            if (total <= 0f) return -1;

            double roll = rng.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < p.Templates.Count; i++)
            {
                if (!IsSeedCandidate(i, tag)) continue;
                last = i;
                roll -= p.Templates[i].Weight;
                if (roll <= 0d) return i;
            }
            return last;
        }

        private bool IsSeedCandidate(int templateIndex, RoomTag? tag)
        {
            var template = p.Templates[templateIndex];
            if (template.Weight <= 0f) return false;
            if (tag.HasValue && (template.Tags & tag.Value) == 0) return false;
            return template.Width <= p.Width && template.Height <= p.Height;
        }

        private int PickRotation(RoomTemplateData template)
        {
            if (!template.AllowRotation) return 0;

            int rotation = rng.Next(4);
            var (width, height) = template.SizeAfterRotation(rotation);
            return width <= p.Width && height <= p.Height ? rotation : 0;
        }

        private int Jitter() => p.StartJitter <= 0 ? 0 : rng.Next(-p.StartJitter, p.StartJitter + 1);

        private int Index(GridPos cell) => cell.Y * p.Width + cell.X;

        private bool InsideGrid(GridPos cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < p.Width && cell.Y < p.Height;

        private bool FitsInGrid(GridRect rect) =>
            rect.X >= 0 && rect.Y >= 0 && rect.MaxX < p.Width && rect.MaxY < p.Height;

        private bool OverlapsAnyRoom(GridRect rect)
        {
            for (int y = rect.Y; y <= rect.MaxY; y++)
            {
                int row = y * p.Width;
                for (int x = rect.X; x <= rect.MaxX; x++)
                {
                    if (roomGrid[row + x] != Vacant) return true;
                }
            }
            return false;
        }

        private void Stamp(GridRect rect, int roomIndex)
        {
            for (int y = rect.Y; y <= rect.MaxY; y++)
            {
                int row = y * p.Width;
                for (int x = rect.X; x <= rect.MaxX; x++) roomGrid[row + x] = roomIndex;
            }
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        private static long PairKey(int a, int b)
        {
            int lo = Math.Min(a, b);
            int hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }
    }
}
