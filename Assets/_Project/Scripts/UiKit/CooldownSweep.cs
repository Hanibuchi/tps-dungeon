using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// 待ちの残りを見せる暗幕。親いっぱいに広がり、残りの割合（<see cref="Fraction"/>）だけ 12 時から扇形に暗くする。
    /// 残りが減るにつれ、暗幕の縁が時計回りに進んで欠けていく。縁には細い光の線を引く。0 なら何も描かない。
    /// 扇は要素の四角に沿って切って描くので、角まで覆えて外へははみ出さない。
    /// 色は USS のカスタムプロパティ（--cooldown-shade / --cooldown-edge）で変えられる。
    /// </summary>
    [UxmlElement]
    public partial class CooldownSweep : VisualElement
    {
        public const string UssClassName = "cooldown-sweep";

        private static readonly CustomStyleProperty<Color> ShadeProperty = new CustomStyleProperty<Color>("--cooldown-shade");
        private static readonly CustomStyleProperty<Color> EdgeProperty = new CustomStyleProperty<Color>("--cooldown-edge");

        private Color shade = new Color(0.03f, 0.02f, 0.02f, 0.7f);
        private Color edge = new Color32(248, 220, 150, 200);
        private float fraction;

        private readonly List<Vector2> outline = new List<Vector2>();

        /// <summary>残りの割合。1 で全部暗く、0 で消える。</summary>
        [UxmlAttribute]
        public float Fraction
        {
            get => fraction;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, fraction)) return;

                fraction = v;
                MarkDirtyRepaint();
            }
        }

        public CooldownSweep()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;

            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ICustomStyle custom = evt.customStyle;
            if (custom.TryGetValue(ShadeProperty, out Color c)) shade = c;
            if (custom.TryGetValue(EdgeProperty, out c)) edge = c;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (fraction <= 0f || r.width < 1f || r.height < 1f) return;

            // 角度は右が 0 度で時計回り（y が下向き）。12 時が -90 度。暗いのは縁から 12 時まで。
            float start = -90f + (1f - fraction) * 360f;
            const float end = 270f;
            Outline(r, start, end, outline);

            Painter2D painter = context.painter2D;
            painter.fillColor = shade;
            painter.BeginPath();
            painter.MoveTo(r.center);
            foreach (Vector2 p in outline) painter.LineTo(p);
            painter.ClosePath();
            painter.Fill();

            if (fraction >= 1f) return;

            painter.strokeColor = edge;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(r.center);
            painter.LineTo(outline[0]);
            painter.Stroke();
        }

        /// <summary>中心から start 度〜end 度の向きに伸ばした線が四角の縁と交わる点を、角を挟んで順に並べる。</summary>
        private static void Outline(Rect r, float start, float end, List<Vector2> result)
        {
            result.Clear();
            result.Add(EdgePoint(r, start));

            float hw = r.width * 0.5f;
            float hh = r.height * 0.5f;
            // 角の向き。右上・右下・左下・左上の順に時計回り。12 時の -90 度より手前に来たものは 1 周ずらす。
            float topRight = Mathf.Atan2(-hh, hw) * Mathf.Rad2Deg;
            float bottomRight = Mathf.Atan2(hh, hw) * Mathf.Rad2Deg;
            float bottomLeft = Mathf.Atan2(hh, -hw) * Mathf.Rad2Deg;
            float topLeft = Mathf.Atan2(-hh, -hw) * Mathf.Rad2Deg + 360f;
            foreach (float corner in new[] { topRight, bottomRight, bottomLeft, topLeft })
            {
                if (corner > start && corner < end) result.Add(EdgePoint(r, corner));
            }

            result.Add(EdgePoint(r, end));
        }

        private static Vector2 EdgePoint(Rect r, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float hw = r.width * 0.5f;
            float hh = r.height * 0.5f;
            float tx = Mathf.Abs(dir.x) > 1e-5f ? hw / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-5f ? hh / Mathf.Abs(dir.y) : float.MaxValue;
            return r.center + dir * Mathf.Min(tx, ty);
        }
    }
}
