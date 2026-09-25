using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// 石板のパネルに重ねる真鍮と金の飾り枠。親いっぱいに広がり、クリックは受けない。
    /// 外側の細い真鍮線と内側の金の二重線を Painter2D で描き、四隅の菱形と上辺中央の飾りは子要素にしてある。
    /// 子要素にしておくと、USS のトランジションで回したり拡げたりできる（開くときに飾りが遅れて現れる演出など）。
    /// 色と寸法は USS のカスタムプロパティ（--frame-*）で変えられる。
    /// </summary>
    [UxmlElement]
    public partial class OrnateFrame : VisualElement
    {
        public const string UssClassName = "ornate-frame";
        public const string CornerUssClassName = UssClassName + "__corner";
        public const string CrestUssClassName = UssClassName + "__crest";

        private static readonly CustomStyleProperty<Color> BrassProperty = new CustomStyleProperty<Color>("--frame-brass");
        private static readonly CustomStyleProperty<Color> GoldProperty = new CustomStyleProperty<Color>("--frame-gold");
        private static readonly CustomStyleProperty<float> InsetProperty = new CustomStyleProperty<float>("--frame-inset");
        private static readonly CustomStyleProperty<float> GapProperty = new CustomStyleProperty<float>("--frame-gap");
        private static readonly CustomStyleProperty<float> CornerGapProperty = new CustomStyleProperty<float>("--frame-corner-gap");

        private Color brass = new Color32(150, 116, 64, 255);
        private Color gold = new Color32(214, 170, 92, 255);
        private float inset = 6f;
        private float gap = 3f;
        private float cornerGap = 9f;

        /// <summary>上辺中央に飾りを付けるか。表題のあるパネル向け。</summary>
        [UxmlAttribute]
        public bool Crest
        {
            get => crestElement != null && crestElement.style.display != DisplayStyle.None;
            set { if (crestElement != null) crestElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; }
        }

        private readonly VisualElement crestElement;

        public OrnateFrame()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;

            foreach (string corner in new[] { "tl", "tr", "bl", "br" })
            {
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList(CornerUssClassName);
                element.AddToClassList(CornerUssClassName + "--" + corner);
                Add(element);
            }

            crestElement = new VisualElement { pickingMode = PickingMode.Ignore };
            crestElement.AddToClassList(CrestUssClassName);
            crestElement.style.display = DisplayStyle.None;
            Add(crestElement);

            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ICustomStyle custom = evt.customStyle;
            if (custom.TryGetValue(BrassProperty, out Color c)) brass = c;
            if (custom.TryGetValue(GoldProperty, out c)) gold = c;
            if (custom.TryGetValue(InsetProperty, out float f)) inset = f;
            if (custom.TryGetValue(GapProperty, out f)) gap = f;
            if (custom.TryGetValue(CornerGapProperty, out f)) cornerGap = f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width < 4f || r.height < 4f) return;

            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Miter;

            // 外周の細い真鍮線。
            StrokeRect(painter, Shrink(r, 0.75f), brass, 1.5f, 0f);

            // 内側の金の二重線。四隅は菱形の飾りが乗るので、その分だけ切り欠いておく。
            StrokeRect(painter, Shrink(r, inset), gold, 1.25f, cornerGap);
            StrokeRect(painter, Shrink(r, inset + gap), WithAlpha(gold, gold.a * 0.45f), 1f, cornerGap);
        }

        /// <summary>矩形を描く。notch が正なら四隅を notch だけ空けた 4 本の線として描く。</summary>
        private static void StrokeRect(Painter2D painter, Rect r, Color color, float width, float notch)
        {
            if (r.width <= 0f || r.height <= 0f) return;

            painter.strokeColor = color;
            painter.lineWidth = width;

            if (notch <= 0f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.xMin, r.yMin));
                painter.LineTo(new Vector2(r.xMax, r.yMin));
                painter.LineTo(new Vector2(r.xMax, r.yMax));
                painter.LineTo(new Vector2(r.xMin, r.yMax));
                painter.ClosePath();
                painter.Stroke();
                return;
            }

            Line(painter, new Vector2(r.xMin + notch, r.yMin), new Vector2(r.xMax - notch, r.yMin));
            Line(painter, new Vector2(r.xMin + notch, r.yMax), new Vector2(r.xMax - notch, r.yMax));
            Line(painter, new Vector2(r.xMin, r.yMin + notch), new Vector2(r.xMin, r.yMax - notch));
            Line(painter, new Vector2(r.xMax, r.yMin + notch), new Vector2(r.xMax, r.yMax - notch));
        }

        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            if ((to - from).sqrMagnitude < 1f) return;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static Rect Shrink(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f);

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
