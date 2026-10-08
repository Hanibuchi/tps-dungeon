using System;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 先頭が通った跡。一定の間隔（<see cref="Spacing"/>）ごとに点を残し、跡に沿って何メートル後ろの点かを返す。
    /// 後ろの仲間はこの点を目標に歩くので、角を曲がっても先頭と同じ道をたどって蛇のように並ぶ。
    /// 古い点はリングバッファで捨てる。跡より後ろを聞かれたら、最後の点から最後の向きに延ばして返す。
    /// </summary>
    public sealed class TrailPath
    {
        private readonly Vector3[] points;
        private int newest = -1;
        private int count;
        private Vector3 head;
        private Vector3 tailDirection = Vector3.back;

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

        /// <summary>
        /// 先頭が position に立ち、後ろへ backward の向きにまっすぐ並んできたことにして跡を作り直す。
        /// 始まりや、先頭が替わったとき・跳んだとき（階段など）に呼ぶ。
        /// </summary>
        public void Reset(Vector3 position, Vector3 backward)
        {
            backward.y = 0f;
            tailDirection = backward.sqrMagnitude > 1e-6f ? backward.normalized : Vector3.back;
            head = position;
            count = 0;
            newest = -1;
            Push(position);
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
            if (count == 0) return head + tailDirection * Mathf.Max(0f, distance);

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

            // 跡が足りない分は、最後の 2 点の向き（無ければ作り直したときの向き）に延ばす。
            Vector3 direction = tailDirection;
            if (count >= 2)
            {
                Vector3 last = Flat(points[Index(count - 1)] - points[Index(count - 2)]);
                if (last.sqrMagnitude > 1e-6f) direction = last.normalized;
            }

            return previous + direction * remaining;
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
