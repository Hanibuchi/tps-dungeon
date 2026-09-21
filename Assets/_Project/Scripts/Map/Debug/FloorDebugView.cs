using UnityEngine;
using UnityEngine.InputSystem;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.DebugTools
{
    /// <summary>
    /// 生成結果を上から確認するためのデバッグ用ビュー。
    /// 俯瞰カメラをフロア全体に合わせ、シードと凡例を表示し、キー操作で作り直せるようにする。
    /// プレイヤーを実装するまでの確認手段なので、製品版のシーンには残さない想定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Debug View")]
    public sealed class FloorDebugView : MonoBehaviour
    {
        [SerializeField, Tooltip("監視する FloorBootstrap。未設定ならシーンから自動で探す。")]
        private FloorBootstrap bootstrap;
        [SerializeField, Tooltip("フロア全体を映す俯瞰カメラ。未設定ならこの GameObject の Camera を使う。")]
        private Camera topDownCamera;

        [SerializeField, Tooltip("俯瞰カメラの周囲にとる余白（セル数）。")]
        private float marginInCells = 2f;

        [SerializeField, Tooltip("別のシードで作り直すキー。")]
        private Key regenerateKey = Key.R;

        [SerializeField, Tooltip("画面左上にシードや部屋数の情報を表示する。")]
        private bool showOverlay = true;

        [Header("Gizmo")]
        [SerializeField, Tooltip("シーンビューに部屋同士の接続を出す。")]
        private bool drawRoomGraph = true;

        private void Reset()
        {
            bootstrap = FindFirstObjectByType<FloorBootstrap>();
            topDownCamera = GetComponent<Camera>();
        }

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindFirstObjectByType<FloorBootstrap>();
            if (topDownCamera == null) topDownCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (bootstrap != null) bootstrap.Generated += OnGenerated;
        }

        private void OnDisable()
        {
            if (bootstrap != null) bootstrap.Generated -= OnGenerated;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (bootstrap != null && keyboard[regenerateKey].wasPressedThisFrame) bootstrap.Regenerate();
        }

        private void OnGenerated(FloorLayout layout)
        {
            FrameFloor(layout);
        }

        /// <summary>フロア全体が収まるように俯瞰カメラを合わせる。</summary>
        private void FrameFloor(FloorLayout layout)
        {
            if (topDownCamera == null || bootstrap.Config == null) return;

            float cellSize = bootstrap.Config.cellSize;

            // 部屋はグリッド全体には広がらないので、実際に部屋がある範囲に寄せる。
            var area = layout.RoomsBounds;
            float width = area.Width * cellSize;
            float depth = area.Height * cellSize;
            float margin = marginInCells * cellSize;

            topDownCamera.orthographic = true;
            topDownCamera.orthographicSize = Mathf.Max(depth, width / Mathf.Max(0.01f, topDownCamera.aspect)) * 0.5f + margin;
            topDownCamera.transform.SetPositionAndRotation(
                new Vector3((area.X + area.Width * 0.5f) * cellSize,
                            Mathf.Max(width, depth),
                            (area.Y + area.Height * 0.5f) * cellSize),
                Quaternion.Euler(90f, 0f, 0f));
            topDownCamera.farClipPlane = Mathf.Max(topDownCamera.farClipPlane, Mathf.Max(width, depth) * 2f);
        }

        private void OnGUI()
        {
            if (!showOverlay || bootstrap == null) return;

            var layout = bootstrap.CurrentLayout;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 200f), GUI.skin.box);
            if (layout == null)
            {
                GUILayout.Label("フロア未生成", style);
            }
            else
            {
                GUILayout.Label($"seed: <b>{bootstrap.CurrentSeed}</b>", style);
                GUILayout.Label($"部屋 {layout.Rooms.Count} / ドア {layout.Edges.Count} 枚", style);
                GUILayout.Label($"上り階段 Room{layout.StairUpRoom} ・ 下り階段 Room{layout.StairDownRoom} ・ ショップ Room{layout.ShopRoom}", style);
                GUILayout.Label($"[{regenerateKey}] 別のシードで作り直す", style);
            }
            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (bootstrap == null || bootstrap.Config == null) return;

            var layout = bootstrap.CurrentLayout;
            if (layout == null) return;

            float cellSize = bootstrap.Config.cellSize;

            foreach (var room in layout.Rooms)
            {
                Gizmos.color = room.Role switch
                {
                    RoomRole.StairUp => new Color(0.3f, 1f, 0.4f, 0.9f),
                    RoomRole.StairDown => new Color(1f, 0.5f, 0.2f, 0.9f),
                    RoomRole.Shop => new Color(1f, 0.9f, 0.2f, 0.9f),
                    _ => new Color(0.4f, 0.7f, 1f, 0.7f),
                };
                DrawRect(room.Bounds, cellSize, 0.1f);
            }

            if (drawRoomGraph)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.8f);
                foreach (var edge in layout.Edges)
                {
                    Gizmos.DrawLine(
                        CenterOf(layout.Rooms[edge.RoomA].Bounds, cellSize),
                        CenterOf(layout.Rooms[edge.RoomB].Bounds, cellSize));
                }
            }
        }

        private void DrawRect(GridRect rect, float cellSize, float y)
        {
            var center = CenterOf(rect, cellSize);
            center.y = y;
            Gizmos.DrawWireCube(center, new Vector3(rect.Width * cellSize, 0.02f, rect.Height * cellSize));
        }

        private static Vector3 CenterOf(GridRect rect, float cellSize) =>
            new Vector3((rect.X + rect.Width * 0.5f) * cellSize, 1f, (rect.Y + rect.Height * 0.5f) * cellSize);
#endif
    }
}
