using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// 画面の四辺を暗く沈める。USS にはグラデーションが無いので、
    /// 外周から内へ少しずつ薄くなる枠を Painter2D で重ねて描く。クリックは受けない。
    /// </summary>
    [UxmlElement]
    public partial class Vignette : VisualElement
    {
        private static readonly CustomStyleProperty<Color> ColorProperty = new CustomStyleProperty<Color>("--vignette-color");
        private static readonly CustomStyleProperty<float> DepthProperty = new CustomStyleProperty<float>("--vignette-depth");

        private const int Steps = 24;

        private Color color = new Color(0f, 0f, 0f, 0.7f);
        private float depth = 0.22f;

        public Vignette()
        {
            AddToClassList("rpg-vignette");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if (evt.customStyle.TryGetValue(ColorProperty, out Color c)) color = c;
                if (evt.customStyle.TryGetValue(DepthProperty, out float d)) depth = d;
                MarkDirtyRepaint();
            });
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;

            // 短い辺に対する割合で、暗くする帯の厚みを決める。
            float band = Mathf.Min(r.width, r.height) * depth;
            float step = band / Steps;
            Painter2D painter = context.painter2D;
            painter.lineWidth = step + 0.5f;

            for (int i = 0; i < Steps; i++)
            {
                // 外側ほど濃く。二乗で落として、内側の境目を目立たせない。
                float t = 1f - (float)i / Steps;
                painter.strokeColor = new Color(color.r, color.g, color.b, color.a * t * t);

                float inset = step * (i + 0.5f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.xMin + inset, r.yMin + inset));
                painter.LineTo(new Vector2(r.xMax - inset, r.yMin + inset));
                painter.LineTo(new Vector2(r.xMax - inset, r.yMax - inset));
                painter.LineTo(new Vector2(r.xMin + inset, r.yMax - inset));
                painter.ClosePath();
                painter.Stroke();
            }
        }
    }
}
