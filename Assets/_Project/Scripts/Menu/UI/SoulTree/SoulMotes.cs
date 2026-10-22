using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 魂の強化の画面の背景。暗い中の真ん中に淡い光だまりがあり、光の粒がゆっくり上へ漂いながら明滅する。クリックは受けない。
    /// 粒の場所は要素の大きさに対する割合で持つので、画面の大きさが変わっても散らばり方は変わらない。
    /// 見えている間だけ動かす（<see cref="Running"/>）。
    /// </summary>
    [UxmlElement]
    public partial class SoulMotes : VisualElement
    {
        private const int Count = 70;

        private struct Mote
        {
            public float X;
            public float Y;
            public float Speed;
            public float Sway;
            public float Phase;
            public float Size;
            public float Warmth;
        }

        private readonly Mote[] motes = new Mote[Count];
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private IVisualElementScheduledItem ticker;
        private bool running;

        public SoulMotes()
        {
            AddToClassList("soul-motes");
            pickingMode = PickingMode.Ignore;
            var random = new System.Random(1234);
            for (int i = 0; i < Count; i++)
            {
                motes[i] = new Mote
                {
                    X = (float)random.NextDouble(),
                    Y = (float)random.NextDouble(),
                    Speed = 0.006f + (float)random.NextDouble() * 0.018f,
                    Sway = 0.004f + (float)random.NextDouble() * 0.012f,
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f,
                    Size = 1.2f + (float)random.NextDouble() * 2.6f,
                    Warmth = (float)random.NextDouble(),
                };
            }
            generateVisualContent += Draw;
        }

        /// <summary>動かすか。見えていない間は止めて、描き直しを減らす。</summary>
        public bool Running
        {
            get => running;
            set
            {
                if (running == value) return;
                running = value;
                if (running)
                {
                    ticker ??= schedule.Execute(MarkDirtyRepaint).Every(33);
                    ticker.Resume();
                }
                else ticker?.Pause();
            }
        }

        /// <summary>真ん中の淡い光だまり。薄い円を重ねて、縁の無いぼかしにする。ゆっくり息をするように大きさが揺れる。</summary>
        private static void DrawGlow(Painter2D p, Rect r, float time)
        {
            const int steps = 48;
            Vector2 center = r.center;
            float radius = Mathf.Max(r.width, r.height) * (0.42f + 0.015f * Mathf.Sin(time * 0.5f));
            p.fillColor = new Color(0.16f, 0.2f, 0.38f, 0.013f);
            for (int i = 0; i < steps; i++)
            {
                float k = 1f - i / (float)steps;
                p.BeginPath();
                p.Arc(center, radius * k * k, 0f, 360f);
                p.Fill();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;

            float time = (float)clock.Elapsed.TotalSeconds;
            Painter2D p = context.painter2D;
            DrawGlow(p, r, time);
            for (int i = 0; i < Count; i++)
            {
                Mote m = motes[i];
                float y = Mathf.Repeat(m.Y - time * m.Speed, 1f);
                float x = m.X + Mathf.Sin(time * 0.4f + m.Phase) * m.Sway;
                // 上へ行くほど薄く、ゆっくり明滅する。
                float twinkle = 0.55f + 0.45f * Mathf.Sin(time * (0.8f + m.Speed * 30f) + m.Phase);
                float fade = Mathf.SmoothStep(0f, 1f, y * 1.6f) * Mathf.SmoothStep(0f, 1f, (1f - y) * 6f);
                float alpha = twinkle * fade;
                if (alpha <= 0.01f) continue;

                Vector2 at = new Vector2(r.x + x * r.width, r.y + y * r.height);
                Color cold = new Color(0.55f, 0.85f, 1f);
                Color warm = new Color(1f, 0.85f, 0.55f);
                Color color = Color.Lerp(cold, warm, m.Warmth * m.Warmth);

                // 外の淡い光と、芯の明るい点。
                p.fillColor = new Color(color.r, color.g, color.b, 0.05f * alpha);
                p.BeginPath();
                p.Arc(at, m.Size * 3.2f, 0f, 360f);
                p.Fill();
                p.fillColor = new Color(color.r, color.g, color.b, 0.55f * alpha);
                p.BeginPath();
                p.Arc(at, m.Size * 0.7f, 0f, 360f);
                p.Fill();
            }
        }
    }
}
