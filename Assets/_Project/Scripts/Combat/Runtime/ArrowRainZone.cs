using System;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 矢の雨（持続弓）1 つ。置いた瞬間から地面に範囲の円を薄く出して落ちる場所を知らせ、StartDelay 秒で降り始める。
    /// 降っている間は TickInterval 秒ごとに onTick(中心, 半径, 高さ) を呼ぶ（ダメージは RangedAttacker が与える）。
    /// 見た目は Effect（Meteors AOE など、上から落ちてくる素材）を、最初の刻みに落ち始めが届くよう少し早めて中心に出し、
    /// 水平だけ半径に合わせて縮め、繰り返すバーストを雨の長さまで延ばす。TickCount 回刻んだら放出を止め、円を消して自分も消える。
    /// 落ちてくる物は中心の SkyHeight m 上（空の点）から出し、出し始めに空の点へ SkyEffect（魔法陣）を出す。
    /// 持続弓の火の矢（RainShotArrow）は、ちょうどこの瞬間に空の点へ着くよう飛ぶ（<see cref="LeadTime"/>）。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ArrowRainZone : MonoBehaviour
    {
        /// <summary>見た目の素材の落下物が落ちる速さ（m/秒。Meteors AOE は秒速 25 m）。</summary>
        private const float FallSpeed = 25f;

        /// <summary>雨が終わって放出を止めてから、出ている粒（着弾の火花や土煙）が消えるまで待つ秒数。</summary>
        private const float EffectLingerTime = 2f;

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

            /// <summary>降っている間の見た目（任意）。</summary>
            public GameObject Effect;

            /// <summary>Effect の素材そのままの範囲の半径（m）。</summary>
            public float EffectRadius;

            public Material RingMaterial;
            public Color RingColor;

            /// <summary>落ちてくる物を出す、中心からの高さ（m）。Effect の一番上の粒の出る所をここへ移す。</summary>
            public float SkyHeight;

            /// <summary>降り始めに空の点へ出す見た目（任意）。半径 1 m の作りで、雨の半径に合わせて大きさを変える。雨の間続け、終わったら放出を止める。</summary>
            public GameObject SkyEffect;
        }

        /// <summary>見た目を出してから最初の落下物が地面に届くまでの秒数。雨は StartDelay のこれだけ前に空の点から降り始める。</summary>
        public static float LeadTime(float skyHeight) => skyHeight / FallSpeed;

        private Settings settings;
        private Action<Vector3, float, float> onTick;
        private RangeRing ring;
        private GameObject effect;
        private GameObject skyEffect;
        private bool effectStarted;
        private float elapsed;
        private int ticksDone;

        private float RainEnd => settings.StartDelay + Mathf.Max(0, settings.TickCount - 1) * settings.TickInterval;

        public static ArrowRainZone Spawn(Vector3 center, Settings settings, Action<Vector3, float, float> onTick)
        {
            var go = new GameObject("ArrowRain");
            go.transform.position = center;
            var zone = go.AddComponent<ArrowRainZone>();
            zone.settings = settings;
            zone.onTick = onTick;
            zone.ring = RangeRing.Create("Ring", settings.RingMaterial, settings.RingColor, 0.07f, go.transform);
            zone.ring?.Set(center, settings.Radius);
            zone.ring?.SetAlpha(TelegraphAlpha);
            return zone;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Vector3 center = transform.position;

            if (!effectStarted && elapsed >= settings.StartDelay - LeadTime(settings.SkyHeight)) StartEffect();

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

            if (ticksDone >= settings.TickCount && elapsed >= RainEnd + FadeTime)
            {
                ReleaseEffect();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 見た目を中心に出す。水平だけ半径に合わせて縮め（縦は落ちてくる高さを保つ）、
        /// 繰り返すバーストを雨の終わりまで延ばして、雨の間だけ降らせる。
        /// </summary>
        private void StartEffect()
        {
            effectStarted = true;
            StartSkyEffect();
            if (settings.Effect == null) return;

            effect = Instantiate(settings.Effect, transform.position, Quaternion.identity, transform);
            float horizontal = settings.Radius / Mathf.Max(0.01f, settings.EffectRadius);
            effect.transform.localScale = Vector3.Scale(effect.transform.localScale, new Vector3(horizontal, 1f, horizontal));

            float length = Mathf.Max(0.1f, RainEnd - elapsed);
            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem particles in systems) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (ParticleSystem particles in systems)
            {
                ParticleSystem.MainModule main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = false;
                main.duration = Mathf.Max(main.duration, length);

                ParticleSystem.EmissionModule emission = particles.emission;
                for (int i = 0; i < emission.burstCount; i++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(i);
                    if (burst.cycleCount <= 1 || burst.repeatInterval <= 0f) continue;

                    burst.cycleCount = Mathf.Max(1, Mathf.CeilToInt(length / burst.repeatInterval));
                    emission.SetBurst(i, burst);
                }
            }

            // サブエミッタは親が着弾で出すので、親（一番上）から子ごと流す。落ちてくる物は空の点から出す。
            ParticleSystem root = effect.GetComponent<ParticleSystem>();
            if (root != null && settings.SkyHeight > 0f)
            {
                ParticleSystem.ShapeModule shape = root.shape;
                shape.position = new Vector3(shape.position.x, settings.SkyHeight / Mathf.Max(0.01f, effect.transform.lossyScale.y), shape.position.z);
            }

            if (root != null) root.Play(true);
            else foreach (ParticleSystem particles in systems) particles.Play(false);
        }

        /// <summary>空の点に魔法陣を出す。ずっと出す粒は ReleaseEffect で止めるまで続く。</summary>
        private void StartSkyEffect()
        {
            if (settings.SkyEffect == null) return;

            Vector3 sky = transform.position + Vector3.up * settings.SkyHeight;
            skyEffect = Instantiate(settings.SkyEffect, sky, Quaternion.identity, transform);
            skyEffect.transform.localScale = settings.SkyEffect.transform.localScale * settings.Radius;
            foreach (ParticleSystem particles in skyEffect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        /// <summary>見た目を雨から切り離し、放出を止めて、出ている粒が消えてから消す。</summary>
        private void ReleaseEffect()
        {
            Release(effect);
            Release(skyEffect);
            effect = skyEffect = null;
        }

        private static void Release(GameObject go)
        {
            if (go == null) return;

            go.transform.SetParent(null, true);
            foreach (ParticleSystem particles in go.GetComponentsInChildren<ParticleSystem>())
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (Light light in go.GetComponentsInChildren<Light>())
                light.enabled = false;
            Destroy(go, EffectLingerTime);
        }
    }
}
