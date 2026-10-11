using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 毒霧 1 つ。その場にとどまり、<see cref="Settings.Duration"/> 秒の間 <see cref="Settings.TickInterval"/> 秒ごとに、
    /// 中に居るメンバーと置物を削る。外へ出れば削れない（毒の状態は残らない）。
    /// 見た目は Effect（煙の素材）を緑に染めて半径に合わせて出し、終わったら放出を止めて、出ている煙が消えてから消える。
    /// PoisonGasEnemyBrain が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PoisonCloud : MonoBehaviour
    {
        /// <summary>放出を止めてから、出ている煙が消えるまで待つ秒数。</summary>
        private const float EffectLingerTime = 2.5f;

        public struct Settings
        {
            public float Radius;
            /// <summary>霧の高さ（m）。足元からこの高さまでの円柱の中を削る。</summary>
            public float Height;
            public float Duration;
            public float TickInterval;
            public int TickDamage;

            public GameObject Effect;
            /// <summary>Effect の素材そのままの半径（m）。</summary>
            public float EffectRadius;
            public Color Tint;
        }

        private readonly List<EnemyTarget> hits = new List<EnemyTarget>();
        private Settings settings;
        private GameObject effect;
        private float elapsed;
        private float nextTick;

        /// <summary>地面の点 center に霧を出す。</summary>
        public static PoisonCloud Spawn(Vector3 center, Settings settings)
        {
            var go = new GameObject("PoisonCloud");
            go.transform.position = center;
            var cloud = go.AddComponent<PoisonCloud>();
            cloud.settings = settings;
            cloud.nextTick = settings.TickInterval * 0.5f;

            if (settings.Effect != null)
            {
                cloud.effect = Instantiate(settings.Effect, center, Quaternion.identity, go.transform);
                float scale = settings.Radius / Mathf.Max(0.01f, settings.EffectRadius);
                EnemyEffects.Prepare(cloud.effect, scale, true);
                EnemyEffects.Tint(cloud.effect, settings.Tint);
            }
            return cloud;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            while (elapsed >= nextTick && nextTick <= settings.Duration)
            {
                nextTick += settings.TickInterval;
                Tick();
            }

            if (elapsed >= settings.Duration)
            {
                EnemyEffects.Release(effect, EffectLingerTime);
                effect = null;
                Destroy(gameObject);
            }
        }

        /// <summary>円柱（半径 Radius、高さ Height）の中の相手を削る。円柱を包む球で拾ってから、足元の高さと水平の距離で絞る。</summary>
        private void Tick()
        {
            Vector3 center = transform.position + Vector3.up * (settings.Height * 0.5f);
            float reach = Mathf.Sqrt(settings.Radius * settings.Radius + settings.Height * settings.Height * 0.25f);
            EnemyHits.Collect(center, reach, hits);
            foreach (EnemyTarget target in hits)
            {
                Vector3 offset = target.Position - transform.position;
                if (offset.y < -0.5f || offset.y > settings.Height) continue;
                offset.y = 0f;
                if (offset.magnitude > settings.Radius) continue;
                target.Damage(settings.TickDamage);
            }
        }
    }
}
