using System;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 治癒の場（治癒持続）1 つ。張った瞬間に地面へ範囲の円と見た目（Healing circle など）を出し、
    /// TickInterval 秒ごとに onTick(中心, 半径, 高さ, 何回目か) を呼ぶ（回復は RangedAttacker が与える）。最初の刻みは張った瞬間。
    /// 見た目は水平だけ半径に合わせて縮め、1 周を場の長さに引き伸ばす（Healing circle は 1 周 4 秒で、頭で広がり、終わりの 2 割で薄れて消える。
    /// ループさせると 4 秒ごとに消えて広がり直し、場の終わりでは途中で切れてしまうので、1 周だけを場の長さで流す）。TickCount 回刻んだら円を消して自分も消える。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class HealingFieldZone : MonoBehaviour
    {
        /// <summary>場が終わってから、出ている細かい粒（火花など）が消えるまで待つ秒数。</summary>
        private const float EffectLingerTime = 1.2f;

        /// <summary>刻み終えてから円を消すまでの秒数。</summary>
        private const float FadeTime = 0.4f;

        public struct Settings
        {
            public float Radius;
            public float Height;
            public float StartDelay;
            public float TickInterval;
            public int TickCount;

            /// <summary>場の間の見た目（任意）。</summary>
            public GameObject Effect;

            /// <summary>Effect の素材そのままの範囲の半径（m）。</summary>
            public float EffectRadius;

            public Material RingMaterial;
            public Color RingColor;
        }

        private Settings settings;
        private Action<Vector3, float, float, int> onTick;
        private RangeRing ring;
        private GameObject effect;
        private bool started;
        private float elapsed;
        private int ticksDone;

        private float FieldEnd => settings.StartDelay + Mathf.Max(0, settings.TickCount - 1) * settings.TickInterval;

        public static HealingFieldZone Spawn(Vector3 center, Settings settings, Action<Vector3, float, float, int> onTick)
        {
            var go = new GameObject("HealingField");
            go.transform.position = center;
            var zone = go.AddComponent<HealingFieldZone>();
            zone.settings = settings;
            zone.onTick = onTick;
            return zone;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < settings.StartDelay) return;

            Vector3 center = transform.position;
            if (!started) StartField(center);

            while (ticksDone < settings.TickCount && elapsed >= settings.StartDelay + ticksDone * settings.TickInterval)
            {
                onTick?.Invoke(center, settings.Radius, settings.Height, ticksDone);
                ticksDone++;
            }

            if (ring != null) ring.SetAlpha(Mathf.Clamp01(1f - (elapsed - FieldEnd) / FadeTime));

            if (ticksDone >= settings.TickCount && elapsed >= FieldEnd + FadeTime)
            {
                ReleaseEffect();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 円と見た目を出す。見た目は水平だけ半径に合わせて縮める。ループは切り、1 周の長さ（main.duration）だけ生きる粒（魔法陣・側面・明かり）は
        /// 寿命を場の長さに、繰り返すバースト（火花など）は場の長さまで続くよう回数を伸ばす。こうすると素材の広がり・薄れ方が場の頭と終わりに合う。
        /// </summary>
        private void StartField(Vector3 center)
        {
            started = true;
            ring = RangeRing.Create("Ring", settings.RingMaterial, settings.RingColor, 0.07f, transform);
            ring?.Set(center, settings.Radius);

            if (settings.Effect == null) return;

            effect = Instantiate(settings.Effect, center, Quaternion.identity, transform);
            float horizontal = settings.Radius / Mathf.Max(0.01f, settings.EffectRadius);
            effect.transform.localScale = Vector3.Scale(effect.transform.localScale, new Vector3(horizontal, 1f, horizontal));
            // 円が消えるのに合わせて、見た目も薄れ終わるようにする。
            float length = Mathf.Max(0.1f, FieldEnd - elapsed + FadeTime);
            ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem particles in systems) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (ParticleSystem particles in systems)
            {
                ParticleSystem.MainModule main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                float cycle = main.duration;
                main.loop = false;
                if (Mathf.Abs(main.startLifetimeMultiplier - cycle) < 0.01f) main.startLifetimeMultiplier = length;
                main.duration = Mathf.Max(cycle, length);

                ParticleSystem.EmissionModule emission = particles.emission;
                for (int i = 0; i < emission.burstCount; i++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(i);
                    if (burst.cycleCount <= 1 || burst.repeatInterval <= 0f) continue;

                    // 寿命ぶん早めに打ち止めて、場が消えるときに火花だけ残らないようにする。
                    float until = Mathf.Max(burst.repeatInterval, length - burst.time - main.startLifetimeMultiplier);
                    burst.cycleCount = Mathf.Max(burst.cycleCount, Mathf.CeilToInt(until / burst.repeatInterval));
                    emission.SetBurst(i, burst);
                }
            }

            ParticleSystem root = effect.GetComponent<ParticleSystem>();
            if (root != null) root.Play(true);
            else foreach (ParticleSystem particles in systems) particles.Play(false);
        }

        /// <summary>見た目を場から切り離し、放出を止めて、出ている細かい粒が消えてから消す（魔法陣はもう薄れ終えている）。</summary>
        private void ReleaseEffect()
        {
            if (effect == null) return;

            effect.transform.SetParent(null, true);
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(effect, EffectLingerTime);
            effect = null;
        }
    }
}
