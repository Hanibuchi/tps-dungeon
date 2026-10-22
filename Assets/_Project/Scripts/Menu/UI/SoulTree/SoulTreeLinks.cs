using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 魂の強化の木の枝（前の強化から先の強化への線）を Painter2D で描く。木の面（中心が根）の子に、大きさ 0 で置く（線は外へはみ出して描かれる）。
    /// - 解放した強化どうし: 金の線に淡い光。
    /// - 解放した強化から、まだの強化へ: 水色の点線が先へ向かって流れる（買える所の目印）。
    /// 線は根から見た向きに沿って緩く曲げ、マインドマップの枝のようにする。
    /// 枝ごとの伸び具合（0〜1）で、先へ向かって線が伸びていく途中を描ける（解放したときの演出）。
    /// </summary>
    public sealed class SoulTreeLinks : VisualElement
    {
        public enum State
        {
            /// <summary>両端とも解放した。</summary>
            Owned,
            /// <summary>前だけ解放した（先は買える・まだ買えない）。</summary>
            Next,
        }

        public sealed class Link
        {
            public Vector2 From;
            public Vector2 Control;
            public Vector2 To;
            public State State;
            /// <summary>どこまで伸びているか（0〜1）。</summary>
            public float Progress;
            /// <summary>先がまだ見えていないので描かない。</summary>
            public bool Hidden;
        }

        private static readonly Color OwnedColor = new Color(0.98f, 0.82f, 0.48f, 1f);
        private static readonly Color OwnedGlow = new Color(0.98f, 0.72f, 0.32f, 0.12f);
        private static readonly Color NextColor = new Color(0.62f, 0.9f, 1f, 0.85f);
        private static readonly Color NextGlow = new Color(0.45f, 0.8f, 1f, 0.08f);

        private const int Segments = 28;
        private const float DashLength = 9f;
        private const float GapLength = 7f;
        private const float DashSpeed = 26f;

        private readonly List<Link> links = new List<Link>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly List<Vector2> points = new List<Vector2>(Segments + 1);
        private IVisualElementScheduledItem ticker;

        public SoulTreeLinks()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = 0;
            style.height = 0;
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ => ticker = schedule.Execute(MarkDirtyRepaint).Every(33));
            RegisterCallback<DetachFromPanelEvent>(_ => ticker?.Pause());
        }

        public IReadOnlyList<Link> Links => links;

        public void ClearLinks() => links.Clear();

        public Link Add(Vector2 from, Vector2 to)
        {
            // 根から見て外へ膨らむように、先の向きの上で前と同じ距離の点を曲げの目安にする。
            float fromRadius = from.magnitude;
            Vector2 control = to.sqrMagnitude > 1e-4f ? to.normalized * fromRadius : (from + to) * 0.5f;
            if (fromRadius < 1f) control = Vector2.Lerp(from, to, 0.5f);
            var link = new Link { From = from, Control = Vector2.Lerp(control, (from + to) * 0.5f, 0.35f), To = to };
            links.Add(link);
            return link;
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D p = context.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            float time = (float)clock.Elapsed.TotalSeconds;

            // 光（太く淡い線）を先にまとめて描き、その上に芯を描く。
            foreach (Link link in links)
            {
                if (link.Hidden || link.Progress <= 0f) continue;
                Sample(link);
                p.strokeColor = link.State == State.Owned ? OwnedGlow : NextGlow;
                p.lineWidth = link.State == State.Owned ? 12f : 9f;
                StrokeSolid(p);
            }

            foreach (Link link in links)
            {
                if (link.Hidden || link.Progress <= 0f) continue;
                Sample(link);
                if (link.State == State.Owned)
                {
                    p.strokeColor = OwnedColor;
                    p.lineWidth = 3f;
                    StrokeSolid(p);
                }
                else
                {
                    p.strokeColor = NextColor;
                    p.lineWidth = 2.2f;
                    StrokeDashed(p, time * DashSpeed);
                }
            }
        }

        /// <summary>曲線を伸び具合までの点にする。</summary>
        private void Sample(Link link)
        {
            points.Clear();
            float end = Mathf.Clamp01(link.Progress);
            for (int i = 0; i <= Segments; i++)
            {
                float t = end * i / Segments;
                float u = 1f - t;
                points.Add(u * u * link.From + 2f * u * t * link.Control + t * t * link.To);
            }
        }

        private void StrokeSolid(Painter2D p)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++) p.LineTo(points[i]);
            p.Stroke();
        }

        /// <summary>点線。offset だけ先へずらして、線の上を光が流れていくように見せる。</summary>
        private void StrokeDashed(Painter2D p, float offset)
        {
            float period = DashLength + GapLength;
            float along = -(offset % period);
            p.BeginPath();
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                float length = Vector2.Distance(a, b);
                float start = along;
                along += length;
                if (length <= 0f) continue;

                // この区間に掛かる点線の山を全部描く。
                float k = Mathf.Floor(start / period) * period;
                for (; k < along; k += period)
                {
                    float s0 = Mathf.Max(k, start);
                    float s1 = Mathf.Min(k + DashLength, along);
                    if (s1 <= s0) continue;
                    p.MoveTo(Vector2.Lerp(a, b, (s0 - start) / length));
                    p.LineTo(Vector2.Lerp(a, b, (s1 - start) / length));
                }
            }
            p.Stroke();
        }
    }
}
