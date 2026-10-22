using System;
using System.Collections.Generic;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 魂の強化の画面の上を飛ぶ光の玉。画面いっぱいの、クリックを受けない層（fx）に足しては消す。
    /// 場所はパネルの座標（worldBound）で受け取り、層の座標に直して飛ばす。道は少し脇へ膨らむ曲線。
    /// </summary>
    internal sealed class SoulFx
    {
        private const string OrbClass = "soul-orb";

        private readonly VisualElement layer;
        private readonly List<UiTween.Handle> running = new List<UiTween.Handle>();
        private readonly System.Random random = new System.Random();

        public SoulFx(VisualElement layer)
        {
            this.layer = layer;
        }

        /// <summary>
        /// from から to へ玉を 1 つ飛ばす（パネルの座標）。delayMs 待ってから durationMs かけて飛び、着いたら onArrive を呼んで消える。
        /// </summary>
        public void Orb(Vector2 from, Vector2 to, long delayMs, long durationMs, Action onArrive = null, float size = 12f,
            string extraClass = null)
        {
            if (layer == null) return;

            Vector2 a = layer.WorldToLocal(from);
            Vector2 b = layer.WorldToLocal(to);
            Vector2 mid = (a + b) * 0.5f;
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized;
            float bend = ((float)random.NextDouble() * 2f - 1f) * Mathf.Min(160f, (b - a).magnitude * 0.35f);
            Vector2 control = mid + normal * bend;

            var orb = new VisualElement { pickingMode = PickingMode.Ignore };
            orb.AddToClassList(OrbClass);
            if (!string.IsNullOrEmpty(extraClass)) orb.AddToClassList(extraClass);
            orb.style.width = size;
            orb.style.height = size;
            orb.style.opacity = 0f;
            Place(orb, a, size, 1f);
            layer.Add(orb);

            UiTween.Handle handle = null;
            handle = UiTween.Run(orb, durationMs, t =>
            {
                float u = 1f - t;
                Vector2 at = u * u * a + 2f * u * t * control + t * t * b;
                // 出るときにふくらみ、着く前に縮む。
                float scale = Mathf.Lerp(0.6f, 1.25f, Mathf.Sin(t * Mathf.PI));
                orb.style.opacity = Mathf.Clamp01(t * 6f);
                Place(orb, at, size, scale);
            }, () =>
            {
                running.Remove(handle);
                orb.RemoveFromHierarchy();
                onArrive?.Invoke();
            }, UiTween.EaseInOutCubic, delayMs);
            running.Add(handle);
        }

        /// <summary>着いた所で輪が広がって消える。</summary>
        public void Ring(Vector2 at, float size, string extraClass = null)
        {
            if (layer == null) return;
            Vector2 p = layer.WorldToLocal(at);
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("soul-ring");
            if (!string.IsNullOrEmpty(extraClass)) ring.AddToClassList(extraClass);
            ring.style.width = size;
            ring.style.height = size;
            Place(ring, p, size, 0.4f);
            layer.Add(ring);

            UiTween.Handle handle = null;
            handle = UiTween.Run(ring, 650, t =>
            {
                Place(ring, p, size, Mathf.Lerp(0.4f, 2.2f, t));
                ring.style.opacity = 1f - t;
            }, () =>
            {
                running.Remove(handle);
                ring.RemoveFromHierarchy();
            });
            running.Add(handle);
        }

        /// <summary>飛んでいる物を全部消す（終わりの処理は呼ばない）。</summary>
        public void Clear()
        {
            foreach (UiTween.Handle handle in running.ToArray()) handle.Stop();
            running.Clear();
            layer?.Clear();
        }

        private static void Place(VisualElement element, Vector2 center, float size, float scale)
        {
            element.style.left = center.x - size * 0.5f;
            element.style.top = center.y - size * 0.5f;
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }
    }
}
