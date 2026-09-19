using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 部屋テンプレート上のドア候補地点。
    /// 生成時に隣室の方向に合う候補が選ばれ、選ばれたものだけが実際のドアになる。
    /// 選ばれなかった候補はただの壁になるので、多めに打っておいてよい。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Door Socket")]
    public sealed class DoorSocket : MonoBehaviour
    {
        [SerializeField, Tooltip("部屋ローカルのセル座標。部屋の外周セルを指すこと。")]
        private Vector2Int localCell;

        [SerializeField, Tooltip("部屋の外側を向く方向。")]
        private Direction facing = Direction.North;

        public Vector2Int LocalCell => localCell;
        public Direction Facing => facing;

        public DoorSocketData ToData() => new DoorSocketData(new GridPos(localCell.x, localCell.y), facing);

        /// <summary>プレースホルダ生成やエディタ操作から呼ぶ設定用。</summary>
        public void Configure(Vector2Int cell, Direction direction)
        {
            localCell = cell;
            facing = direction;
        }

        /// <summary>自分のセルの中心に Transform を合わせる。見た目とデータをずらさないため。</summary>
        public void SnapToCell(float cellSize)
        {
            transform.localPosition = new Vector3((localCell.x + 0.5f) * cellSize, 0f, (localCell.y + 0.5f) * cellSize);
            transform.localRotation = Quaternion.Euler(0f, 90f * (int)facing, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            // Facing はローカルの +Z がその向きを指すように SnapToCell で合わせてある。
            Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
        }
#endif
    }
}
