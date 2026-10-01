using System;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 矢の雨（持続弓）1 つ。置いた瞬間から地面に範囲の円を薄く出して落ちる場所を知らせ、StartDelay 秒で降り始める。
    /// 降っている間は TickInterval 秒ごとに onTick(中心, 半径, 高さ) を呼び（ダメージは RangedAttacker が与える）、
    /// 範囲の中へ見た目の矢を上から落とし続ける。TickCount 回刻んだら円を消して自分も消える。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ArrowRainZone : MonoBehaviour
    {
        /// <summary>見た目の矢を落とし始める高さ（m）。</summary>
        private const float FallHeight = 9f;

        /// <summary>見た目の矢の落ちる速さ（m/s）。降り始め（最初の刻み）に地面へ届くよう、その分だけ先に落とし始める。</summary>
        private const float FallSpeed = 30f;

        /// <summary>見た目の矢を斜めに落とすずれ（m）。真上からだと矢の姿が点になって見えないため。</summary>
        private const float FallSlant = 1.2f;

        /// <summary>降り始める前の円の不透明度（本番に対する割合）。</summary>
        private const float TelegraphAlpha = 0.45f;

        /// <summary>刻み終えてから円を消すまでの秒数。</summary>
        private const float FadeTime = 0.3f;

        public struct Settings
        {
            public float Radius;
            public float Height;
            public float StartDelay;
            public float TickInterval;
            public int TickCount;

            /// <summary>見た目の矢を 1 秒あたりに落とす数。</summary>
            public float ArrowsPerSecond;

            public GameObject ArrowPrefab;
            public Material RingMaterial;
            public Color RingColor;
            public LayerMask HitMask;
            public Transform IgnoreRoot;

            /// <summary>見た目の矢を斜めに落とす向き（水平）。撃った向きにすると、撃った側から降ってくるように見える。</summary>
            public Vector3 Forward;
        }

        private Settings settings;
        private Action<Vector3, float, float> onTick;
        private RangeRing ring;
        private float elapsed;
        private int ticksDone;
        private float arrowBudget;

        private float FallTime => FallHeight / FallSpeed;
        private float RainEnd => settings.StartDelay + Mathf.Max(0, settings.TickCount - 1) * settings.TickInterval;

        public static ArrowRainZone Spawn(Vector3 center, Settings settings, Action<Vector3, float, float> onTick)
        {
            var go = new GameObject("ArrowRain");
            go.transform.position = center;
            var zone = go.AddComponent<ArrowRainZone>();
            zone.settings = settings;
            zone.onTick = onTick;
            zone.ring = RangeRing.Create("Ring", settings.RingMaterial, settings.RingColor, 0.09f, go.transform);
            zone.ring?.Set(center, settings.Radius);
            zone.ring?.SetAlpha(TelegraphAlpha);
            return zone;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Vector3 center = transform.position;

            if (elapsed >= settings.StartDelay - FallTime && elapsed < RainEnd - FallTime + settings.TickInterval) DropArrows();

            while (ticksDone < settings.TickCount && elapsed >= settings.StartDelay + ticksDone * settings.TickInterval)
            {
                ticksDone++;
                onTick?.Invoke(center, settings.Radius, settings.Height);
            }

            if (ring != null)
            {
                float fade = Mathf.Clamp01(1f - (elapsed - RainEnd) / FadeTime);
                ring.SetAlpha(elapsed < settings.StartDelay ? TelegraphAlpha : fade);
            }

            if (ticksDone >= settings.TickCount && elapsed >= RainEnd + FadeTime) Destroy(gameObject);
        }

        private void DropArrows()
        {
            if (settings.ArrowPrefab == null || settings.ArrowsPerSecond <= 0f) return;

            arrowBudget += settings.ArrowsPerSecond * Time.deltaTime;
            Vector3 slant = settings.Forward;
            slant.y = 0f;
            slant = slant.sqrMagnitude > 1e-6f ? slant.normalized * FallSlant : Vector3.zero;

            for (; arrowBudget >= 1f; arrowBudget -= 1f)
            {
                Vector2 spot = UnityEngine.Random.insideUnitCircle * settings.Radius;
                Vector3 target = transform.position + new Vector3(spot.x, 0f, spot.y);
                Vector3 start = target + Vector3.up * FallHeight - slant;
                Vector3 path = target - start;
                ArrowProjectile.Launch(settings.ArrowPrefab, start, path, new ArrowProjectile.Settings
                {
                    Speed = FallSpeed,
                    Range = path.magnitude + 1f,
                    Radius = 0.03f,
                    HitMask = settings.HitMask,
                    IgnoreRoot = settings.IgnoreRoot,
                    VisualOnly = true,
                });
            }
        }
    }
}
