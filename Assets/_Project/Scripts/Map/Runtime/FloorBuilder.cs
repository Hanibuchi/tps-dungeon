using System;
using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// FloorLayout をシーン上の実体に変換する。
    /// 部屋は Prefab をそのまま置き、外周壁・ドア・廊下の床はセル境界の判定から組み立てる。
    /// 生成物はすべて 1 つのルート配下に入れるので、作り直すときはルートごと捨てればよい。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Builder")]
    public sealed class FloorBuilder : MonoBehaviour
    {
        private const string GeneratedRootName = "[Generated Floor]";

        [SerializeField, Tooltip("壁の高さ（メートル）。壁 Prefab のスケールに使う。")]
        private float wallHeight = 3f;

        /// <summary>NavMesh のベイクなど、具現化が終わってから走らせたい処理のためのフック。</summary>
        public event Action<FloorLayout> Built;

        public FloorLayout CurrentLayout { get; private set; }
        public FloorOccupancy CurrentOccupancy { get; private set; }
        public Transform GeneratedRoot { get; private set; }

        private float cellSize = 3f;

        public FloorLayout Build(FloorConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            Clear();

            cellSize = config.cellSize;
            var layout = FloorLayoutGenerator.Generate(config.BuildParams(), seed);
            var occupancy = new FloorOccupancy(layout);

            var root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);

            var rooms = NewGroup(root, "Rooms");
            var corridors = NewGroup(root, "Corridors");
            var boundaries = NewGroup(root, "Walls");
            var features = NewGroup(root, "Features");

            BuildRooms(layout, config, rooms);
            BuildCorridorFloors(layout, config, corridors);
            BuildBoundaries(occupancy, config, boundaries);
            BuildFeatures(layout, config, features);

            GeneratedRoot = root;
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

        private void BuildRooms(FloorLayout layout, FloorConfig config, Transform parent)
        {
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
            }
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

        private void BuildCorridorFloors(FloorLayout layout, FloorConfig config, Transform parent)
        {
            if (config.corridorFloorPrefab == null)
            {
                Debug.LogWarning("corridorFloorPrefab が未設定なので廊下の床を省略した", this);
                return;
            }

            foreach (var cell in layout.CorridorCells)
            {
                var tile = Instantiate(config.corridorFloorPrefab, parent);
                tile.name = $"Corridor_{cell.X}_{cell.Y}";
                tile.transform.SetLocalPositionAndRotation(CellCenter(cell), Quaternion.identity);
            }
        }

        /// <summary>
        /// セル境界を 1 つずつ見て壁かドアを建てる。
        /// 各境界を 1 回だけ処理するため、North と East 方向だけを見る。
        /// グリッド外側の境界も拾えるよう、走査は -1 から始める。
        /// </summary>
        private void BuildBoundaries(FloorOccupancy occupancy, FloorConfig config, Transform parent)
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

                        BoundaryKind kind;
                        if (occupancy.KindAt(cell) != CellKind.Empty) kind = occupancy.BoundaryAt(cell, dir);
                        else if (occupancy.KindAt(neighbor) != CellKind.Empty) kind = occupancy.BoundaryAt(neighbor, dir.Opposite());
                        else continue;

                        if (kind == BoundaryKind.None) continue;

                        var prefab = kind == BoundaryKind.Door && config.doorPrefab != null
                            ? config.doorPrefab
                            : config.wallPrefab;

                        var piece = Instantiate(prefab, parent);
                        piece.name = $"{kind}_{x}_{y}_{dir}";
                        piece.transform.SetLocalPositionAndRotation(
                            BoundaryCenter(cell, dir), Quaternion.Euler(0f, 90f * (int)dir, 0f));
                        piece.transform.localScale = new Vector3(cellSize, wallHeight, piece.transform.localScale.z);
                    }
                }
            }
        }

        private void BuildFeatures(FloorLayout layout, FloorConfig config, Transform parent)
        {
            Spawn(layout.StairUpRoom, config.stairUpPrefab, "StairUp");
            Spawn(layout.StairDownRoom, config.stairDownPrefab, "StairDown");
            Spawn(layout.ShopRoom, config.shopMarkerPrefab, "Shop");

            void Spawn(int roomIndex, GameObject prefab, string label)
            {
                if (roomIndex < 0 || prefab == null) return;

                var room = layout.RoomAt(roomIndex);
                if (room == null) return;

                var marker = Instantiate(prefab, parent);
                marker.name = $"{label}_Room{roomIndex}";
                marker.transform.SetLocalPositionAndRotation(CellCenter(room.FeatureCell), Quaternion.identity);
            }
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
