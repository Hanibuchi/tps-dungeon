using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 部屋テンプレート Prefab のルートに付けるコンポーネント。
    /// Prefab が持つのは床と内装とドア候補だけで、外周壁は FloorBuilder がセル単位で建てる。
    /// Prefab のローカル原点はセル (0,0) の角。セル (x,y) の中心は ((x+0.5)*cellSize, 0, (y+0.5)*cellSize)。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Room Template")]
    public sealed class RoomTemplate : MonoBehaviour
    {
        [SerializeField, Tooltip("部屋の大きさ（セル数）。")]
        private Vector2Int sizeInCells = new Vector2Int(6, 6);

        [SerializeField, Tooltip("1 セルの一辺（メートル）。FloorConfig と揃えること。")]
        private float cellSize = 3f;

        [SerializeField, Tooltip("この部屋をどの役割に使えるか。")]
        private RoomTag tags = RoomTag.Normal;

        [SerializeField, Tooltip("90 度単位の回転を許すか。向きに意味のある部屋（玄関ホール等）は false。")]
        private bool allowRotation = true;

        [SerializeField, Min(0f), Tooltip("抽選の重み。0 だと選ばれない。")]
        private float weight = 1f;

        public Vector2Int SizeInCells => sizeInCells;
        public float CellSize => cellSize;
        public RoomTag Tags => tags;
        public bool AllowRotation => allowRotation;
        public float Weight => weight;

        public void Configure(Vector2Int size, float cell, RoomTag roomTags, bool rotation, float pickWeight)
        {
            sizeInCells = size;
            cellSize = cell;
            tags = roomTags;
            allowRotation = rotation;
            weight = pickWeight;
        }

        public List<DoorSocket> CollectSockets()
        {
            var sockets = new List<DoorSocket>(GetComponentsInChildren<DoorSocket>(true));
            // 階層の並び順に生成結果が左右されないよう、決まった順に整列する。
            sockets.Sort((a, b) =>
            {
                int byFacing = ((int)a.Facing).CompareTo((int)b.Facing);
                if (byFacing != 0) return byFacing;
                int byX = a.LocalCell.x.CompareTo(b.LocalCell.x);
                return byX != 0 ? byX : a.LocalCell.y.CompareTo(b.LocalCell.y);
            });
            return sockets;
        }

        /// <param name="id">生成器側でテンプレートを識別する名前。通常は Prefab 名。</param>
        /// <param name="weightOverride">0 より大きければ、この値で Prefab 側の重みを上書きする。</param>
        public RoomTemplateData ToData(string id, float weightOverride = 0f)
        {
            var sockets = new List<DoorSocketData>();
            foreach (var socket in CollectSockets()) sockets.Add(socket.ToData());

            return new RoomTemplateData(
                id,
                sizeInCells.x,
                sizeInCells.y,
                tags,
                allowRotation,
                weightOverride > 0f ? weightOverride : weight,
                sockets);
        }

        /// <summary>四辺すべてにドア候補があるか。1 辺でも欠けると、その方向の隣室と繋げられない。</summary>
        public bool HasSocketOnEverySide()
        {
            int mask = 0;
            foreach (var socket in CollectSockets()) mask |= 1 << (int)socket.Facing;
            return mask == 0b1111;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sizeInCells = new Vector2Int(Mathf.Max(1, sizeInCells.x), Mathf.Max(1, sizeInCells.y));
            cellSize = Mathf.Max(0.1f, cellSize);

            foreach (var socket in GetComponentsInChildren<DoorSocket>(true)) socket.SnapToCell(cellSize);
        }

        private void OnDrawGizmos()
        {
            var size = new Vector3(sizeInCells.x * cellSize, 0f, sizeInCells.y * cellSize);
            var center = transform.position + transform.rotation * (size * 0.5f);
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.1f, size.z));
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
