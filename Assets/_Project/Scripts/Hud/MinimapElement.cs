using System.Collections.Generic;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 入った部屋だけを俯瞰で描く小さな地図。北（ワールド +Z）が常に上で、プレイヤーを中心に据える。
    /// 部屋・ドア・階段/ショップの印・プレイヤーの向きを Painter2D で描く。
    /// 地上では、真上から撮った地上の絵（<see cref="SurfaceMapSource"/>）を下に敷き、城の土台・門・解放したショートカットの印を重ねる。
    /// 色と縮尺は USS のカスタムプロパティ（--map-*）で変えられる。--map-fit-surface を 1 にすると、地上では地上全体を収めて描く（大きな地図）。
    /// 描くのは子の要素（下から 絵・図形・番号）。この要素自身は何も描かない。
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
        private static readonly CustomStyleProperty<Color> CastleColorProperty = new CustomStyleProperty<Color>("--map-castle");
        private static readonly CustomStyleProperty<Color> ShortcutColorProperty = new CustomStyleProperty<Color>("--map-shortcut");
        private static readonly CustomStyleProperty<float> FitSurfaceProperty = new CustomStyleProperty<float>("--map-fit-surface");

        /// <summary>地上の地図の 1 セルの大きさ（m）。階のセルと同じにして、縮尺を揃える。</summary>
        public const float SurfaceMetersPerCell = 5f;

        private Color roomColor = new Color32(74, 68, 60, 255);
        private Color currentRoomColor = new Color32(112, 102, 88, 255);
        private Color roomEdgeColor = new Color32(150, 141, 127, 255);
        private Color doorColor = new Color32(214, 158, 74, 255);
        private Color featureColor = new Color32(236, 228, 214, 255);
        private Color playerColor = new Color32(214, 158, 74, 255);
        private float pixelsPerCell = 32f;
        private Color castleColor = new Color32(52, 46, 40, 255);
        private Color shortcutColor = new Color32(120, 210, 230, 255);
        private bool fitSurface;

        private readonly VisualElement backdrop;
        private readonly VisualElement canvas;
        private readonly VisualElement labels;
        private readonly List<Label> labelPool = new List<Label>();

        private SurfaceMapSource surface;
        private readonly List<SurfaceMapSource.Marker> markers = new List<SurfaceMapSource.Marker>();

        private FloorExploration exploration;
        private Vector2 playerCell;
        private float playerYaw;
        private bool hasPlayer;

        public MinimapElement()
        {
            pickingMode = PickingMode.Ignore;
            backdrop = Layer("minimap__backdrop");
            backdrop.style.display = DisplayStyle.None;
            canvas = Layer("minimap__shapes");
            canvas.style.left = 0;
            canvas.style.top = 0;
            canvas.style.right = 0;
            canvas.style.bottom = 0;
            canvas.generateVisualContent += Draw;
            labels = Layer("minimap__labels");
            labels.style.left = 0;
            labels.style.top = 0;
            labels.style.right = 0;
            labels.style.bottom = 0;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(_ => Repaint());
        }

        private VisualElement Layer(string className)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList(className);
            layer.style.position = Position.Absolute;
            Add(layer);
            return layer;
        }

        /// <summary>描き直す（図形も、地上の絵の位置と印の番号も）。</summary>
        public void Repaint()
        {
            canvas.MarkDirtyRepaint();
            LayoutSurface();
        }

        /// <summary>地上の地図の元。null なら地上の絵を敷かない（階では null）。</summary>
        public void SetSurface(SurfaceMapSource value)
        {
            if (surface == value) return;
            surface = value;
            backdrop.style.backgroundImage = surface != null && surface.Image != null ? new StyleBackground(surface.Image) : new StyleBackground();
            backdrop.style.display = surface != null && surface.Image != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (surface == null) markers.Clear();
            Repaint();
        }

        /// <summary>地上の地図に重ねる印（城の門・解放したショートカット）。</summary>
        public void SetMarkers(List<SurfaceMapSource.Marker> value)
        {
            markers.Clear();
            if (value != null) markers.AddRange(value);
            Repaint();
        }

        /// <summary>描くフロアと探索状況。null ならプレイヤーの印だけ描く。</summary>
        public void SetExploration(FloorExploration value)
        {
            if (exploration == value) return;
            exploration = value;
            Repaint();
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
            Repaint();
        }

        public void ClearPlayer()
        {
            if (!hasPlayer) return;
            hasPlayer = false;
            Repaint();
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
            if (custom.TryGetValue(CastleColorProperty, out c)) castleColor = c;
            if (custom.TryGetValue(ShortcutColorProperty, out c)) shortcutColor = c;
            if (custom.TryGetValue(FitSurfaceProperty, out var fit)) fitSurface = fit > 0.5f;
            Repaint();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = mgc.painter2D;
            var center = rect.center;

            if (exploration != null) DrawRooms(painter, center);
            if (surface != null) DrawSurface(painter, center);
            if (hasPlayer) DrawPlayer(painter, center);
        }

        // ---- 地上 ----

        private static Vector2 WorldToCell(Vector2 world) => world / SurfaceMetersPerCell;

        private static Rect WorldToCell(Rect world) =>
            Rect.MinMaxRect(world.xMin / SurfaceMetersPerCell, world.yMin / SurfaceMetersPerCell, world.xMax / SurfaceMetersPerCell, world.yMax / SurfaceMetersPerCell);

        /// <summary>セルの矩形を画面の矩形にする（セルの Y は上向き、画面の y は下向き）。</summary>
        private Rect ToScreenRect(Vector2 center, Rect cells)
        {
            var min = ToScreen(center, new Vector2(cells.xMin, cells.yMin));
            var max = ToScreen(center, new Vector2(cells.xMax, cells.yMax));
            return Rect.MinMaxRect(min.x, max.y, max.x, min.y);
        }

        /// <summary>地上の絵を写す範囲に合わせて置き、ショートカットの番号を印の上に並べる。</summary>
        private void LayoutSurface()
        {
            var rect = contentRect;
            bool show = surface != null && surface.Image != null && rect.width > 0f;
            backdrop.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            int used = 0;
            if (show)
            {
                var screen = ToScreenRect(rect.center, WorldToCell(surface.Area));
                backdrop.style.left = screen.xMin;
                backdrop.style.top = screen.yMin;
                backdrop.style.width = screen.width;
                backdrop.style.height = screen.height;

                foreach (var marker in markers)
                {
                    if (marker.Kind != SurfaceMapSource.MarkerKind.Shortcut) continue;
                    if (used == labelPool.Count)
                    {
                        var created = new Label { pickingMode = PickingMode.Ignore };
                        created.AddToClassList("minimap__marker-label");
                        created.style.position = Position.Absolute;
                        labels.Add(created);
                        labelPool.Add(created);
                    }
                    var label = labelPool[used++];
                    var p = ToScreen(rect.center, WorldToCell(marker.Position));
                    float size = MarkerSize() * 2f;
                    label.text = (marker.Layer + 1).ToString();
                    label.style.left = p.x - size * 0.5f;
                    label.style.top = p.y - size * 0.5f;
                    label.style.width = size;
                    label.style.height = size;
                    label.style.display = DisplayStyle.Flex;
                }
            }
            for (int i = used; i < labelPool.Count; i++) labelPool[i].style.display = DisplayStyle.None;
        }

        /// <summary>地上の印の半径（画面の画素）。縮尺で変えすぎず、番号が読める大きさにする。</summary>
        private float MarkerSize() => Mathf.Clamp(Scale() * 0.3f, 8f, 10f);

        /// <summary>城の土台と、城の門・解放したショートカットの印。</summary>
        private void DrawSurface(Painter2D painter, Vector2 center)
        {
            var castle = ToScreenRect(center, WorldToCell(surface.Castle));
            painter.fillColor = castleColor;
            painter.strokeColor = roomEdgeColor;
            painter.lineWidth = 1.5f;
            RectPath(painter, castle);
            painter.Fill();
            painter.Stroke();

            float s = MarkerSize();
            foreach (var marker in markers)
            {
                var p = ToScreen(center, WorldToCell(marker.Position));
                painter.strokeColor = new Color(0f, 0f, 0f, 0.7f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                if (marker.Kind == SurfaceMapSource.MarkerKind.Gate)
                {
                    // 城の門。上が丸いアーチ。
                    painter.fillColor = doorColor;
                    painter.MoveTo(p + new Vector2(-s, s));
                    painter.LineTo(p + new Vector2(-s, 0f));
                    painter.Arc(p, s, 180f, 360f);
                    painter.LineTo(p + new Vector2(s, s));
                    painter.ClosePath();
                }
                else
                {
                    // ショートカット。丸の中に層の番号（番号は labels）。
                    painter.fillColor = shortcutColor;
                    painter.Arc(p, s, 0f, 360f);
                    painter.ClosePath();
                }
                painter.Fill();
                painter.Stroke();
            }
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
                case RoomRole.Exit:
                    // 出口は上の階への上り階段。上向きの三角。
                    painter.MoveTo(p + new Vector2(0f, -s));
                    painter.LineTo(p + new Vector2(s, s));
                    painter.LineTo(p + new Vector2(-s, s));
                    break;
                case RoomRole.Entrance:
                    // 入口は下の階への下り階段。下向きの三角。
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
        private void DrawPlayer(Painter2D painter, Vector2 screenCenter)
        {
            // ふだんはプレイヤーが真ん中。地上全体を収めて描くときは、プレイヤーのいる所。
            var center = ToScreen(screenCenter, playerCell);
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

        /// <summary>地上全体を収めて描くか（--map-fit-surface が 1 で、地上の地図の元があるとき）。</summary>
        private bool FittingSurface => fitSurface && surface != null && surface.Area.width > 0f && surface.Area.height > 0f;

        /// <summary>1 セルあたりの画面の大きさ。地上全体を収めるときは、地上の範囲が枠に収まる大きさ。</summary>
        private float Scale()
        {
            if (!FittingSurface) return pixelsPerCell;
            var cells = WorldToCell(surface.Area);
            var rect = contentRect;
            return Mathf.Min(rect.width / cells.width, rect.height / cells.height);
        }

        /// <summary>セル座標を画面座標にする。ふだんはプレイヤーを真ん中に、地上全体を収めるときは地上の範囲の真ん中を真ん中に据える。</summary>
        private Vector2 ToScreen(Vector2 center, Vector2 cell)
        {
            var origin = FittingSurface ? WorldToCell(surface.Area).center : hasPlayer ? playerCell : Vector2.zero;
            var d = (cell - origin) * Scale();
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
