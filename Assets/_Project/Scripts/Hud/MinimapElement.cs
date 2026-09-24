using TpsDungeon.Map.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 入った部屋だけを俯瞰で描く小さな地図。北（ワールド +Z）が常に上で、プレイヤーを中心に据える。
    /// 部屋・ドア・階段/ショップの印・プレイヤーの向きを Painter2D で描く。
    /// 色と縮尺は USS のカスタムプロパティ（--map-*）で変えられる。
    /// </summary>
    [UxmlElement]
    public partial class MinimapElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> RoomColorProperty = new CustomStyleProperty<Color>("--map-room");
        private static readonly CustomStyleProperty<Color> CurrentRoomColorProperty = new CustomStyleProperty<Color>("--map-room-current");
        private static readonly CustomStyleProperty<Color> RoomEdgeColorProperty = new CustomStyleProperty<Color>("--map-room-edge");
        private static readonly CustomStyleProperty<Color> DoorColorProperty = new CustomStyleProperty<Color>("--map-door");
        private static readonly CustomStyleProperty<Color> FeatureColorProperty = new CustomStyleProperty<Color>("--map-feature");
        private static readonly CustomStyleProperty<Color> PlayerColorProperty = new CustomStyleProperty<Color>("--map-player");
        private static readonly CustomStyleProperty<float> PixelsPerCellProperty = new CustomStyleProperty<float>("--map-pixels-per-cell");

        private Color roomColor = new Color32(74, 68, 60, 255);
        private Color currentRoomColor = new Color32(112, 102, 88, 255);
        private Color roomEdgeColor = new Color32(150, 141, 127, 255);
        private Color doorColor = new Color32(214, 158, 74, 255);
        private Color featureColor = new Color32(236, 228, 214, 255);
        private Color playerColor = new Color32(214, 158, 74, 255);
        private float pixelsPerCell = 32f;

        private FloorExploration exploration;
        private Vector2 playerCell;
        private float playerYaw;
        private bool hasPlayer;

        public MinimapElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        /// <summary>描くフロアと探索状況。null ならプレイヤーの印だけ描く。</summary>
        public void SetExploration(FloorExploration value)
        {
            if (exploration == value) return;
            exploration = value;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// プレイヤーの位置（セル単位の実数。セル (x,y) の中心は (x+0.5, y+0.5)）と向き（ワールドの Y 回転、度）。
        /// 見た目が変わらないほど小さな動きでは描き直さない。
        /// </summary>
        public void SetPlayer(Vector2 cell, float yawDegrees)
        {
            bool moved = !hasPlayer
                || (cell - playerCell).sqrMagnitude * pixelsPerCell * pixelsPerCell > 0.25f
                || Mathf.Abs(Mathf.DeltaAngle(yawDegrees, playerYaw)) > 1f;
            if (!moved) return;

            playerCell = cell;
            playerYaw = yawDegrees;
            hasPlayer = true;
            MarkDirtyRepaint();
        }

        public void ClearPlayer()
        {
            if (!hasPlayer) return;
            hasPlayer = false;
            MarkDirtyRepaint();
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            var custom = evt.customStyle;
            if (custom.TryGetValue(RoomColorProperty, out var c)) roomColor = c;
            if (custom.TryGetValue(CurrentRoomColorProperty, out c)) currentRoomColor = c;
            if (custom.TryGetValue(RoomEdgeColorProperty, out c)) roomEdgeColor = c;
            if (custom.TryGetValue(DoorColorProperty, out c)) doorColor = c;
            if (custom.TryGetValue(FeatureColorProperty, out c)) featureColor = c;
            if (custom.TryGetValue(PlayerColorProperty, out c)) playerColor = c;
            if (custom.TryGetValue(PixelsPerCellProperty, out var ppc) && ppc > 0f) pixelsPerCell = ppc;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = mgc.painter2D;
            var center = rect.center;

            if (exploration != null) DrawRooms(painter, center);
            if (hasPlayer) DrawPlayer(painter, center);
        }

        private void DrawRooms(Painter2D painter, Vector2 center)
        {
            var layout = exploration.Layout;

            foreach (var room in layout.Rooms)
            {
                if (!exploration.IsVisited(room.Index)) continue;

                var b = room.Bounds;
                var min = ToScreen(center, new Vector2(b.X, b.Y));
                var max = ToScreen(center, new Vector2(b.X + b.Width, b.Y + b.Height));
                // セルの Y は上向き、画面の y は下向きなので、min/max の y は入れ替わる。
                var screenRect = Rect.MinMaxRect(min.x, max.y, max.x, min.y);

                painter.fillColor = room.Index == exploration.CurrentRoom ? currentRoomColor : roomColor;
                painter.strokeColor = roomEdgeColor;
                painter.lineWidth = 1.5f;
                // 隣の部屋と辺を共有するので、少し内側に縮めて境目を見せる。
                RectPath(painter, Inset(screenRect, 1.5f));
                painter.Fill();
                painter.Stroke();
            }

            foreach (var room in layout.Rooms)
            {
                if (!exploration.IsVisited(room.Index)) continue;

                foreach (var socket in room.UsedSockets()) DrawDoor(painter, center, socket);
                DrawFeature(painter, center, room);
            }
        }

        /// <summary>ドアは部屋の境目を跨ぐ短い太線で描く。先の部屋が未探索でも、そこに扉があることは分かる。</summary>
        private void DrawDoor(Painter2D painter, Vector2 center, PlacedSocket socket)
        {
            var step = socket.Facing.Offset();
            var mid = new Vector2(socket.Cell.X + 0.5f + step.X * 0.5f, socket.Cell.Y + 0.5f + step.Y * 0.5f);
            // 境目に沿う向き（Facing と直交）。
            var along = new Vector2(step.Y, step.X) * 0.2f;

            painter.strokeColor = doorColor;
            painter.lineWidth = Mathf.Max(2f, pixelsPerCell * 0.18f);
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.MoveTo(ToScreen(center, mid - along));
            painter.LineTo(ToScreen(center, mid + along));
            painter.Stroke();
        }

        private void DrawFeature(Painter2D painter, Vector2 center, RoomInstance room)
        {
            if (room.Role == RoomRole.Normal) return;

            var p = ToScreen(center, new Vector2(room.FeatureCell.X + 0.5f, room.FeatureCell.Y + 0.5f));
            float s = pixelsPerCell * 0.22f;

            painter.fillColor = featureColor;
            painter.BeginPath();
            switch (room.Role)
            {
                case RoomRole.StairUp:
                    painter.MoveTo(p + new Vector2(0f, -s));
                    painter.LineTo(p + new Vector2(s, s));
                    painter.LineTo(p + new Vector2(-s, s));
                    break;
                case RoomRole.StairDown:
                    painter.MoveTo(p + new Vector2(0f, s));
                    painter.LineTo(p + new Vector2(-s, -s));
                    painter.LineTo(p + new Vector2(s, -s));
                    break;
                default:
                    // ショップなど。菱形。
                    painter.MoveTo(p + new Vector2(0f, -s));
                    painter.LineTo(p + new Vector2(s, 0f));
                    painter.LineTo(p + new Vector2(0f, s));
                    painter.LineTo(p + new Vector2(-s, 0f));
                    break;
            }
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>向きの分かる三角形。ワールドの Y 回転は上から見て時計回りなので、画面でもそのまま時計回りに回す。</summary>
        private void DrawPlayer(Painter2D painter, Vector2 center)
        {
            float rad = playerYaw * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            var right = new Vector2(-forward.y, forward.x);
            float size = Mathf.Max(6f, pixelsPerCell * 0.35f);

            painter.fillColor = playerColor;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.6f);
            painter.lineWidth = 1f;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(center + forward * size);
            painter.LineTo(center - forward * (size * 0.6f) + right * (size * 0.7f));
            painter.LineTo(center - forward * (size * 0.3f));
            painter.LineTo(center - forward * (size * 0.6f) - right * (size * 0.7f));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        /// <summary>セル座標を、プレイヤーを中心に据えた画面座標にする。</summary>
        private Vector2 ToScreen(Vector2 center, Vector2 cell)
        {
            var origin = hasPlayer ? playerCell : Vector2.zero;
            var d = (cell - origin) * pixelsPerCell;
            return new Vector2(center.x + d.x, center.y - d.y);
        }

        private static Rect Inset(Rect r, float amount) =>
            Rect.MinMaxRect(r.xMin + amount, r.yMin + amount, r.xMax - amount, r.yMax - amount);

        private static void RectPath(Painter2D painter, Rect r)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMin));
            painter.LineTo(new Vector2(r.xMax, r.yMax));
            painter.LineTo(new Vector2(r.xMin, r.yMax));
            painter.ClosePath();
        }
    }
}
