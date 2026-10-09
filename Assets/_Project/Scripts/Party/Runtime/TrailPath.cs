using System;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 先頭が通った跡。一定の間隔（<see cref="Spacing"/>）ごとに点を残し、跡に沿って何メートル後ろの点かを返す。
    /// 後ろの仲間はこの点を目標に歩くので、角を曲がっても先頭と同じ道をたどって蛇のように並ぶ。
    /// 古い点はリングバッファで捨てる。跡より後ろを聞かれたら、一番古い点を返す（壁の向こうへ延ばさない）。
    /// </summary>
    public sealed class TrailPath
    {
        private readonly Vector3[] points;
        private int newest = -1;
        private int count;
        private Vector3 head;

        public TrailPath(float spacing, float length)
        {
            if (spacing <= 0f) throw new ArgumentOutOfRangeException(nameof(spacing));
            Spacing = spacing;
            points = new Vector3[Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 2)];
        }

        /// <summary>点を残す間隔（m）。</summary>
        public float Spacing { get; }

        /// <summary>残っている点の数（先頭の今の位置は含まない）。</summary>
        public int Count => count;

        /// <summary>先頭の今の位置。</summary>
        public Vector3 Head => head;

        /// <summary>先頭から一番古い点までの、跡に沿った長さ（m）。</summary>
        public float Length
        {
            get
            {
                float length = 0f;
                Vector3 previous = head;
                for (int i = 0; i < count; i++)
                {
                    Vector3 point = points[Index(i)];
                    length += Flat(point - previous).magnitude;
                    previous = point;
                }

                return length;
            }
        }

        /// <summary>跡の一番古い点から、跡が来た向き（古い方へ向かう向き）。跡が 2 点未満なら null。</summary>
        public Vector3? TailDirection
        {
            get
            {
                if (count < 2) return null;
                Vector3 d = Flat(points[Index(count - 1)] - points[Index(count - 2)]);
                return d.sqrMagnitude > 1e-6f ? d.normalized : (Vector3?)null;
            }
        }

        /// <summary>
        /// 先頭が position に立ち、後ろへ backward の向きに length メートルまっすぐ歩いてきたことにして跡を作り直す。
        /// 始まりや、先頭が替わったとき・跳んだとき（階段など）に呼ぶ。length は壁に当たるまでの長さにする（跡はそれより後ろへ延ばさない）。
        /// </summary>
        public void Reset(Vector3 position, Vector3 backward, float length)
        {
            backward.y = 0f;
            Vector3 direction = backward.sqrMagnitude > 1e-6f ? backward.normalized : Vector3.back;
            length = Mathf.Clamp(length, 0f, Spacing * (points.Length - 1));
            head = position;
            count = 0;
            newest = -1;

            // 古い方（後ろの端）から順に積む。
            int steps = Mathf.FloorToInt(length / Spacing);
            if (length - steps * Spacing > 1e-4f) Push(position + direction * length);
            for (int i = steps; i >= 0; i--) Push(position + direction * (Spacing * i));
        }

        /// <summary>先頭が position へ動いた。最後の点から間隔ぶん離れるたびに点を足す（大きく動いたら間を埋める）。</summary>
        public void Record(Vector3 position)
        {
            head = position;
            if (count == 0)
            {
                Push(position);
                return;
            }

            Vector3 last = points[newest];
            Vector3 offset = Flat(position - last);
            float distance = offset.magnitude;
            if (distance < Spacing) return;

            Vector3 direction = offset / distance;
            int steps = Mathf.FloorToInt(distance / Spacing);
            float dy = (position.y - last.y) / (distance / Spacing);
            for (int i = 1; i <= steps; i++) Push(last + direction * (Spacing * i) + Vector3.up * (dy * i));
        }

        /// <summary>先頭の今の位置から、跡に沿って distance メートル後ろの点。</summary>
        public Vector3 PointAt(float distance)
        {
            if (count == 0) return head;

            Vector3 previous = head;
            float remaining = Mathf.Max(0f, distance);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = points[Index(i)];
                float segment = Flat(point - previous).magnitude;
                if (segment >= remaining)
                    return segment > 1e-6f ? Vector3.Lerp(previous, point, remaining / segment) : point;

                remaining -= segment;
                previous = point;
            }

            // 跡より後ろは一番古い点に留める（延ばすと壁の向こうに出ることがある）。
            return previous;
        }

        private void Push(Vector3 point)
        {
            newest = (newest + 1) % points.Length;
            points[newest] = point;
            if (count < points.Length) count++;
        }

        /// <summary>新しい方から i 番目（0 が一番新しい）の点の格納位置。</summary>
        private int Index(int i) => ((newest - i) % points.Length + points.Length) % points.Length;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
