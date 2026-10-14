using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// FloorLayout をシーン上の実体に変換する。
    /// 部屋は Prefab をそのまま置き、壁とドアはセル境界の判定から組み立てる。
    /// 生成物はすべて 1 つのルート配下に入れるので、作り直すときはルートごと捨てればよい。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Builder")]
    public sealed class FloorBuilder : MonoBehaviour
    {
        private const string GeneratedRootName = "[Generated Floor]";

        /// <summary>NavMesh のベイクなど、具現化が終わってから走らせたい処理のためのフック。</summary>
        public event Action<FloorLayout> Built;

        public FloorLayout CurrentLayout { get; private set; }
        public FloorOccupancy CurrentOccupancy { get; private set; }
        public Transform GeneratedRoot { get; private set; }
        /// <summary>天井をまとめた子。NavMesh を焼くとき、天井の上を歩ける所にしないよう外すのに使う。</summary>
        public Transform CeilingRoot { get; private set; }
        /// <summary>入口の部屋に置いた下り階段。行き先は FloorBootstrap が入れる。</summary>
        public GameObject EntranceStair { get; private set; }
        /// <summary>出口の部屋に置いた上り階段。</summary>
        public GameObject ExitStair { get; private set; }

        private float cellSize = 3f;
        private float wallHeight = 5f;

        // FeatureCell に置いた物のうち、床・天井・土台の組み方を頼んできたもの（FloorFeature）。
        private readonly Dictionary<GridPos, FloorFeature> featureAt = new Dictionary<GridPos, FloorFeature>();

        public FloorLayout Build(FloorConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            Clear();

            cellSize = config.cellSize;
            wallHeight = config.wallHeight;
            var layout = FloorLayoutGenerator.Generate(config.BuildParams(), seed);
            var occupancy = new FloorOccupancy(layout);

            var root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);

            var rooms = NewGroup(root, "Rooms");
            var boundaries = NewGroup(root, "Walls");
            var features = NewGroup(root, "Features");
            var ceiling = NewGroup(root, "Ceiling");
            var foundation = NewGroup(root, "Foundation");

            var roomObjects = BuildRooms(layout, config, rooms);
            BuildBoundaries(occupancy, config, boundaries, new System.Random(seed));
            BuildFeatures(layout, config, features, roomObjects);
            BuildCeiling(occupancy, config, ceiling);
            BuildFoundation(occupancy, config, foundation);
            BuildVolume(config, root);

            GeneratedRoot = root;
            CeilingRoot = ceiling;
            CurrentLayout = layout;
            CurrentOccupancy = occupancy;

            Built?.Invoke(layout);
            return layout;
        }

        public void Clear()
        {
            CurrentLayout = null;
            CurrentOccupancy = null;
            GeneratedRoot = null;
            CeilingRoot = null;
            EntranceStair = null;
            ExitStair = null;
            featureAt.Clear();

            // 生成物は毎回同じ名前のルートにまとめているので、名前で探して消す。
            // エディタから作り直したときに前回分が残らないよう、子を全部見る。
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != GeneratedRootName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private static Transform NewGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <returns>部屋の番号から置いた実体へ。</returns>
        private Dictionary<int, GameObject> BuildRooms(FloorLayout layout, FloorConfig config, Transform parent)
        {
            var placed = new Dictionary<int, GameObject>();
            var catalog = config.roomCatalog;
            foreach (var room in layout.Rooms)
            {
                var prefab = catalog != null ? catalog.PrefabFor(room.Template.Id) : null;
                if (prefab == null)
                {
                    Debug.LogWarning($"テンプレート '{room.Template.Id}' の Prefab がカタログに無いので部屋 {room.Index} を飛ばした", this);
                    continue;
                }

                var instance = Instantiate(prefab, parent);
                instance.name = $"Room{room.Index}_{room.Template.Id}_{room.Role}";
                instance.transform.SetLocalPositionAndRotation(
                    RoomLocalPosition(room), Quaternion.Euler(0f, 90f * room.Rotation, 0f));
                placed[room.Index] = instance;
            }
            return placed;
        }

        /// <summary>
        /// 回転は Prefab のローカル原点（セル (0,0) の角）まわりに掛かるので、
        /// 回した後の footprint の最小角が Bounds の最小角に来るよう位置をずらす。
        /// </summary>
        private Vector3 RoomLocalPosition(RoomInstance room)
        {
            float unrotatedWidth = room.Template.Width * cellSize;
            float unrotatedHeight = room.Template.Height * cellSize;

            Vector3 offset;
            switch (room.Rotation)
            {
                case 1: offset = new Vector3(0f, 0f, unrotatedWidth); break;
                case 2: offset = new Vector3(unrotatedWidth, 0f, unrotatedHeight); break;
                case 3: offset = new Vector3(unrotatedHeight, 0f, 0f); break;
                default: offset = Vector3.zero; break;
            }

            return new Vector3(room.Bounds.X * cellSize, 0f, room.Bounds.Y * cellSize) + offset;
        }

        /// <summary>
        /// セル境界を 1 つずつ見て壁かドアを建てる。
        /// 各境界を 1 回だけ処理するため、North と East 方向だけを見る。
        /// グリッド外側の境界も拾えるよう、走査は -1 から始める。
        /// 片側が空（外に面した壁）なら、windowChance の確率で窓付きの壁にする。
        /// </summary>
        private void BuildBoundaries(FloorOccupancy occupancy, FloorConfig config, Transform parent, System.Random random)
        {
            if (config.wallPrefab == null)
            {
                Debug.LogWarning("wallPrefab が未設定なので壁を省略した", this);
                return;
            }

            var outward = new[] { Direction.North, Direction.East };

            for (int y = -1; y < occupancy.Height; y++)
            {
                for (int x = -1; x < occupancy.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    foreach (var dir in outward)
                    {
                        var neighbor = cell + dir.Offset();

                        bool cellFilled = occupancy.KindAt(cell) != CellKind.Empty;
                        bool neighborFilled = occupancy.KindAt(neighbor) != CellKind.Empty;

                        BoundaryKind kind;
                        if (cellFilled) kind = occupancy.BoundaryAt(cell, dir);
                        else if (neighborFilled) kind = occupancy.BoundaryAt(neighbor, dir.Opposite());
                        else continue;

                        if (kind == BoundaryKind.None) continue;

                        bool window = kind == BoundaryKind.Wall && cellFilled != neighborFilled
                            && config.windowWallPrefab != null && random.NextDouble() < config.windowChance;

                        var prefab = kind == BoundaryKind.Door && config.doorPrefab != null ? config.doorPrefab
                            : window ? config.windowWallPrefab
                            : config.wallPrefab;

                        var piece = Instantiate(prefab, parent);
                        piece.name = window ? $"Window_{x}_{y}_{dir}" : $"{kind}_{x}_{y}_{dir}";
                        piece.transform.SetLocalPositionAndRotation(
                            BoundaryCenter(cell, dir), Quaternion.Euler(0f, 90f * (int)dir, 0f));
                        // 壁・窓・ドアは実寸で作ってあるのでスケールしない（伸ばすと素材の模様の密度が崩れる）。
                    }
                }
            }
        }

        /// <summary>部屋のセルごとに、壁の上端の高さへ天井を 1 枚ずつ張る。</summary>
        private void BuildCeiling(FloorOccupancy occupancy, FloorConfig config, Transform parent)
        {
            if (config.ceilingPrefab == null) return;

            for (int y = 0; y < occupancy.Height; y++)
            {
                for (int x = 0; x < occupancy.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (occupancy.KindAt(cell) == CellKind.Empty) continue;
                    if (featureAt.TryGetValue(cell, out var feature) && feature.replacesCeiling) continue;

                    var piece = Instantiate(config.ceilingPrefab, parent);
                    piece.name = $"Ceiling_{x}_{y}";
                    piece.transform.SetLocalPositionAndRotation(CellCenter(cell) + Vector3.up * wallHeight, Quaternion.identity);
                }
            }
        }

        /// <summary>
        /// 部屋のセルごとに、床の下から地面まで土台を伸ばす。この階は高い建物の上にある体なので、
        /// 窓から隣の棟を見たときに宙に浮いて見えないように。見せるだけなので当たりは持たせない。
        /// </summary>
        private void BuildFoundation(FloorOccupancy occupancy, FloorConfig config, Transform parent)
        {
            if (config.foundationPrefab == null || config.elevation <= 0f) return;

            for (int y = 0; y < occupancy.Height; y++)
            {
                for (int x = 0; x < occupancy.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (occupancy.KindAt(cell) == CellKind.Empty) continue;

                    var piece = Instantiate(config.foundationPrefab, parent);
                    piece.name = $"Foundation_{x}_{y}";
                    // 部屋の床の板（厚み 0.1）の下から。竪穴を掘っているセルはその底から。
                    float depth = featureAt.TryGetValue(cell, out var feature) ? feature.depthBelowFloor : 0f;
                    piece.transform.SetLocalPositionAndRotation(CellCenter(cell) + Vector3.down * Mathf.Max(0.1f, depth), Quaternion.identity);
                }
            }
        }

        /// <summary>
        /// 階の Global Volume を生成ルートの下に置く。どのシーンで階を作っても（Floor_1_1 でも確認用シーンでも）同じ見た目になるよう、シーンには置かない。
        /// カメラの post-processing は FloorPostProcessing が Play 中だけ有効にし、作り直しや片付けで消えるときに戻す。
        /// </summary>
        private static void BuildVolume(FloorConfig config, Transform parent)
        {
            if (config.volumeProfile == null) return;

            var holder = new GameObject("Global Volume");
            holder.transform.SetParent(parent, false);
            var volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = config.volumeProfile;
            holder.AddComponent<FloorPostProcessing>();
        }

        /// <summary>
        /// 入口には下の階への下り階段、出口には上の階への上り階段、ショップには目印を、それぞれの部屋の FeatureCell に置く。
        /// 置いた物が FloorFeature を持っていれば、そのセルの床・天井・土台をそれに合わせる。
        /// </summary>
        private void BuildFeatures(FloorLayout layout, FloorConfig config, Transform parent, Dictionary<int, GameObject> roomObjects)
        {
            EntranceStair = Spawn(layout.EntranceRoom, config.stairDownPrefab, "StairDown");
            ExitStair = Spawn(layout.ExitRoom, config.stairUpPrefab, "StairUp");
            Spawn(layout.ShopRoom, config.shopMarkerPrefab, "Shop");
            PlaceSpawnPoint(layout, parent);

            GameObject Spawn(int roomIndex, GameObject prefab, string label)
            {
                if (roomIndex < 0 || prefab == null) return null;

                var room = layout.RoomAt(roomIndex);
                if (room == null) return null;

                var marker = Instantiate(prefab, parent);
                marker.name = $"{label}_Room{roomIndex}";
                marker.transform.SetLocalPositionAndRotation(CellCenter(room.FeatureCell), Quaternion.identity);

                if (marker.TryGetComponent(out FloorFeature feature))
                {
                    featureAt[room.FeatureCell] = feature;
                    if (feature.replacesRoomFloor) RemoveRoomFloor(room, roomObjects);
                }
                return marker;
            }
        }

        /// <summary>
        /// 部屋の Prefab の床（子の Floor）を消す。床は部屋全体で 1 枚なので、消してよいのは 1 セルの部屋だけ。
        /// それより大きい部屋では床を残す（穴は開かないが、階段は使える）。
        /// </summary>
        private void RemoveRoomFloor(RoomInstance room, Dictionary<int, GameObject> roomObjects)
        {
            if (!roomObjects.TryGetValue(room.Index, out var instance)) return;
            if (room.Bounds.Width != 1 || room.Bounds.Height != 1)
            {
                Debug.LogWarning($"部屋 {room.Index} は 1 セルでないので、床を階段の床に置き換えなかった", this);
                return;
            }

            var floor = instance.transform.Find("Floor");
            if (floor == null) return;
            // Destroy はフレームの終わりまで残るので、同じフレームの NavMesh の焼きに拾われないよう先に止める。
            floor.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(floor.gameObject);
            else DestroyImmediate(floor.gameObject);
        }

        /// <summary>
        /// 階の入口（入口の部屋。割り当たらなければ先頭の部屋）にパーティーのスポーン地点を置く。
        /// FeatureCell の中心には階段（らせんと手すりで差し渡し 3 m ほど）があり、壁の真ん中にはドアが開くので、
        /// どちらにも掛からないセルの隅へずらす。
        /// </summary>
        private void PlaceSpawnPoint(FloorLayout layout, Transform parent)
        {
            var room = layout.RoomAt(layout.EntranceRoom >= 0 ? layout.EntranceRoom : 0);
            if (room == null) return;

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(parent, false);
            spawn.transform.localPosition = CellCenter(room.FeatureCell) + new Vector3(cellSize * 0.38f, 0.05f, cellSize * 0.38f);
            spawn.AddComponent<PlayerSpawnPoint>();
        }

        /// <summary>1 セルの一辺（メートル）。最後に Build したときの FloorConfig の値。</summary>
        public float CellSize => cellSize;

        /// <summary>ワールド座標が乗っているセル。生成物はこの GameObject のローカル空間に並べているので、そこへ戻して割る。</summary>
        public GridPos WorldToCell(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            return new GridPos(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
        }

        public Vector3 CellCenter(GridPos cell) =>
            new Vector3((cell.X + 0.5f) * cellSize, 0f, (cell.Y + 0.5f) * cellSize);

        /// <summary>cell と dir 側の隣セルが接する境界の中点。</summary>
        public Vector3 BoundaryCenter(GridPos cell, Direction dir)
        {
            var center = CellCenter(cell);
            var step = dir.Offset();
            return center + new Vector3(step.X, 0f, step.Y) * (cellSize * 0.5f);
        }
    }
}
