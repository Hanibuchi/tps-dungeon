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
    /// 地上では、真上から撮った地上の絵（<see cref="SurfaceMapSource"/>）を下に敷き、城の土台と、ダンジョンの入口・武器屋・銀行・解放したショートカット（層のローマ数字）の印を重ねる。
    /// 色と縮尺は USS のカスタムプロパティ（--map-*）で変えられる。--map-fit-surface を 1 にすると、地上では地上全体を収めて描く。--map-show-names を 1 にすると、建物・入口の印の下に名前を出す（どちらも大きな地図）。
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
        private static readonly CustomStyleProperty<float> ShowNamesProperty = new CustomStyleProperty<float>("--map-show-names");
        private static readonly CustomStyleProperty<Color> MarkerRimColorProperty = new CustomStyleProperty<Color>("--map-marker-rim");
        private static readonly CustomStyleProperty<Color> MarkerGlyphColorProperty = new CustomStyleProperty<Color>("--map-marker-glyph");

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
        private bool showNames;
        private Color markerRimColor = new Color32(212, 172, 96, 255);
        private Color markerGlyphColor = new Color32(240, 226, 196, 255);

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
            if (custom.TryGetValue(ShowNamesProperty, out var names)) showNames = names > 0.5f;
            if (custom.TryGetValue(MarkerRimColorProperty, out c)) markerRimColor = c;
            if (custom.TryGetValue(MarkerGlyphColorProperty, out c)) markerGlyphColor = c;
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

        /// <summary>地上の絵を写す範囲に合わせて置き、ショートカットの印にローマ数字、建物・入口の印の下に名前を並べる。</summary>
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
                    var p = ToScreen(rect.center, WorldToCell(marker.Position));
                    float r = BadgeRadius(marker.Kind);
                    if (marker.Kind == SurfaceMapSource.MarkerKind.Shortcut)
                    {
                        // 印の真ん中に層のローマ数字。
                        var numeral = NextLabel(ref used, "minimap__marker-numeral");
                        numeral.text = Roman(marker.Layer + 1);
                        Place(numeral, new Rect(p.x - r, p.y - r, r * 2f, r * 2f));
                    }
                    else if (showNames && !string.IsNullOrEmpty(marker.Label))
                    {
                        // 印のすぐ下に名前（大きな地図だけ）。
                        var name = NextLabel(ref used, "minimap__marker-name");
                        name.text = marker.Label;
                        Place(name, new Rect(p.x - 40f, p.y + r + 2f, 80f, 14f));
                    }
                }
            }
            for (int i = used; i < labelPool.Count; i++) labelPool[i].style.display = DisplayStyle.None;
        }

        private Label NextLabel(ref int used, string className)
        {
            if (used == labelPool.Count)
            {
                var created = new Label { pickingMode = PickingMode.Ignore };
                created.style.position = Position.Absolute;
                labels.Add(created);
                labelPool.Add(created);
            }
            var label = labelPool[used++];
            label.ClearClassList();
            label.AddToClassList(className);
            return label;
        }

        private static void Place(Label label, Rect at)
        {
            label.style.left = at.x;
            label.style.top = at.y;
            label.style.width = at.width;
            label.style.height = at.height;
            label.style.display = DisplayStyle.Flex;
        }

        /// <summary>1〜10 のローマ数字（石板に刻んだ番号と同じ）。</summary>
        private static string Roman(int value) => value switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X",
            _ => value.ToString(),
        };

        /// <summary>地上の印の半径（画面の画素）。縮尺で変えすぎず、記号と番号が読める大きさにする。ダンジョンの入口は一回り大きい。</summary>
        private float BadgeRadius(SurfaceMapSource.MarkerKind kind)
        {
            float r = Mathf.Clamp(Scale() * 0.32f, 8.5f, 11f);
            return kind == SurfaceMapSource.MarkerKind.DungeonEntrance ? r * 1.25f : r;
        }

        /// <summary>城の土台と、ダンジョンの入口・武器屋・銀行・解放したショートカットの印。</summary>
        private void DrawSurface(Painter2D painter, Vector2 center)
        {
            // 城の土台。石の色の四角に、内側の縁取り。
            var castle = ToScreenRect(center, WorldToCell(surface.Castle));
            painter.fillColor = castleColor;
            painter.strokeColor = markerRimColor;
            painter.lineWidth = 2f;
            RectPath(painter, castle);
            painter.Fill();
            painter.Stroke();
            painter.strokeColor = new Color(markerRimColor.r, markerRimColor.g, markerRimColor.b, 0.35f);
            painter.lineWidth = 1f;
            RectPath(painter, Inset(castle, 4f));
            painter.Stroke();

            foreach (var marker in markers)
            {
                var p = ToScreen(center, WorldToCell(marker.Position));
                float r = BadgeRadius(marker.Kind);
                DrawBadge(painter, p, r, BadgeFill(marker.Kind));
                switch (marker.Kind)
                {
                    case SurfaceMapSource.MarkerKind.DungeonEntrance: DrawArch(painter, p, r); break;
                    case SurfaceMapSource.MarkerKind.WeaponShop: DrawSwords(painter, p, r); break;
                    case SurfaceMapSource.MarkerKind.Bank: DrawCoin(painter, p, r); break;
                    // ショートカットは番号（labels）だけ。
                }
            }
        }

        private Color BadgeFill(SurfaceMapSource.MarkerKind kind) => kind switch
        {
            SurfaceMapSource.MarkerKind.DungeonEntrance => new Color32(92, 34, 28, 255),
            SurfaceMapSource.MarkerKind.WeaponShop => new Color32(44, 50, 62, 255),
            SurfaceMapSource.MarkerKind.Bank => new Color32(38, 62, 42, 255),
            _ => shortcutColor,
        };

        /// <summary>丸い札。影・金の縁・中の色。</summary>
        private void DrawBadge(Painter2D painter, Vector2 p, float r, Color fill)
        {
            painter.fillColor = new Color(0f, 0f, 0f, 0.45f);
            Circle(painter, p + new Vector2(1f, 1.5f), r + 2f);
            painter.Fill();
            painter.fillColor = markerRimColor;
            Circle(painter, p, r + 2f);
            painter.Fill();
            painter.fillColor = fill;
            Circle(painter, p, r);
            painter.Fill();
        }

        /// <summary>ダンジョンの入口。上が丸いアーチの扉。</summary>
        private void DrawArch(Painter2D painter, Vector2 p, float r)
        {
            float w = r * 0.5f, top = -r * 0.15f, bottom = r * 0.6f;
            painter.fillColor = markerGlyphColor;
            painter.BeginPath();
            painter.MoveTo(p + new Vector2(-w, bottom));
            painter.LineTo(p + new Vector2(-w, top));
            painter.Arc(p + new Vector2(0f, top), w, 180f, 360f);
            painter.LineTo(p + new Vector2(w, bottom));
            painter.ClosePath();
            painter.Fill();
            // 扉の合わせ目。
            painter.strokeColor = BadgeFill(SurfaceMapSource.MarkerKind.DungeonEntrance);
            painter.lineWidth = Mathf.Max(1f, r * 0.12f);
            painter.BeginPath();
            painter.MoveTo(p + new Vector2(0f, top - w * 0.6f));
            painter.LineTo(p + new Vector2(0f, bottom));
            painter.Stroke();
        }

        /// <summary>武器屋。交差した 2 本の剣（刃・鍔）。</summary>
        private void DrawSwords(Painter2D painter, Vector2 p, float r)
        {
            painter.strokeColor = markerGlyphColor;
            painter.lineCap = LineCap.Round;
            foreach (float side in new[] { -1f, 1f })
            {
                var tip = p + new Vector2(-side * r * 0.55f, -r * 0.55f);
                var hilt = p + new Vector2(side * r * 0.5f, r * 0.5f);
                var dir = (hilt - tip).normalized;
                var across = new Vector2(-dir.y, dir.x);
                painter.lineWidth = Mathf.Max(1.5f, r * 0.16f);
                painter.BeginPath();
                painter.MoveTo(tip);
                painter.LineTo(hilt);
                painter.Stroke();
                // 鍔。
                var guard = hilt - dir * r * 0.22f;
                painter.lineWidth = Mathf.Max(1.2f, r * 0.12f);
                painter.BeginPath();
                painter.MoveTo(guard - across * r * 0.22f);
                painter.LineTo(guard + across * r * 0.22f);
                painter.Stroke();
            }
            painter.lineCap = LineCap.Butt;
        }

        /// <summary>銀行。重ねた金貨。</summary>
        private void DrawCoin(Painter2D painter, Vector2 p, float r)
        {
            foreach (var offset in new[] { new Vector2(-r * 0.18f, r * 0.12f), new Vector2(r * 0.14f, -r * 0.1f) })
            {
                var c = p + offset;
                painter.fillColor = markerGlyphColor;
                Circle(painter, c, r * 0.42f);
                painter.Fill();
                painter.strokeColor = BadgeFill(SurfaceMapSource.MarkerKind.Bank);
                painter.lineWidth = Mathf.Max(1f, r * 0.08f);
                Circle(painter, c, r * 0.28f);
                painter.Stroke();
            }
        }

        private static void Circle(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
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
