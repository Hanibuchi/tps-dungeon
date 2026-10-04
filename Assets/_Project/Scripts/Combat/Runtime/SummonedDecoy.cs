using System;
using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 召喚で呼び出した置物 1 体。攻撃はせず、敵を引きつける的（<see cref="DecoyTarget"/>）として立っているだけ。
    /// 出た瞬間に足元へ見た目（魔法陣など）を出し（少し見せてから薄くして消す）、地面の下から RiseTime 秒で迫り上がる。
    /// Duration 秒経つか、体力が尽きるか、<see cref="Dismiss"/> されたら（呼び直し）消える見た目を出して消える。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SummonedDecoy : MonoBehaviour
    {
        /// <summary>地面の下から迫り上がる秒数。</summary>
        private const float RiseTime = 0.3f;

        /// <summary>迫り上がる前に沈めておく深さ（置物の高さに対する割合）。</summary>
        private const float SinkRatio = 0.6f;

        /// <summary>消える見た目を出してから、置物が縮んで消えるまでの秒数。</summary>
        private const float VanishTime = 0.2f;

        /// <summary>出るときの見た目（魔法陣）を見せる秒数と、そのあと薄くして消す秒数。魔法陣は自分では消えないので、迫り上がり終えた少し後にこちらで消す。</summary>
        private const float SpawnEffectHold = 1.2f;
        private const float SpawnEffectFade = 0.6f;

        public struct Settings
        {
            public GameObject Model;
            public float Duration;
            public int Health;
            public GameObject SpawnEffect;
            public float SpawnEffectScale;
            public GameObject DismissEffect;
            public float DismissEffectScale;
        }

        private Settings settings;
        private DecoyTarget target;
        private Vector3 groundPosition;
        private float sinkDepth;
        private float elapsed;
        private float vanishElapsed = -1f;
        private Vector3 baseScale;

        /// <summary>消え始めた（呼び直し・時間切れ・体力切れ）。</summary>
        public bool IsVanishing => vanishElapsed >= 0f;

        public DecoyTarget Target => target;

        /// <summary>消え始めた。持ち主が一覧から外す。</summary>
        public event Action<SummonedDecoy> Vanished;

        /// <summary>地面の点 position に、facing を前に向けて出す。Model が無ければ見た目の無い的だけを置く。</summary>
        public static SummonedDecoy Spawn(Vector3 position, Quaternion facing, Settings settings)
        {
            var go = new GameObject("SummonedDecoy");
            go.transform.SetPositionAndRotation(position, facing);
            if (settings.Model != null) Instantiate(settings.Model, go.transform, false);

            var decoy = go.AddComponent<SummonedDecoy>();
            decoy.settings = settings;
            decoy.groundPosition = position;
            decoy.baseScale = go.transform.localScale;
            decoy.sinkDepth = Height(go) * SinkRatio;
            go.transform.position = position + Vector3.down * decoy.sinkDepth;

            decoy.target = go.AddComponent<DecoyTarget>();
            decoy.target.SetMaxHp(settings.Health);
            decoy.target.Broken += _ => decoy.Dismiss();

            OneShotEffect.SpawnFading(settings.SpawnEffect, position, facing, settings.SpawnEffectScale, SpawnEffectHold, SpawnEffectFade);
            return decoy;
        }

        /// <summary>消える見た目を出して消す。2 回目からは何もしない。</summary>
        public void Dismiss()
        {
            if (IsVanishing) return;

            vanishElapsed = 0f;
            OneShotEffect.Spawn(settings.DismissEffect, groundPosition, transform.rotation, settings.DismissEffectScale);
            // 消え始めたら的としては外す（敵が消えかけの置物を狙い続けないように）。
            if (target != null) target.enabled = false;
            Vanished?.Invoke(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (IsVanishing)
            {
                vanishElapsed += dt;
                float shrink = Mathf.Clamp01(1f - vanishElapsed / VanishTime);
                transform.localScale = baseScale * shrink;
                if (vanishElapsed >= VanishTime) Destroy(gameObject);
                return;
            }

            elapsed += dt;
            float rise = Mathf.Clamp01(elapsed / RiseTime);
            // 出だしは速く、最後はゆっくり止まる。
            float eased = 1f - (1f - rise) * (1f - rise);
            transform.position = groundPosition + Vector3.down * (sinkDepth * (1f - eased));

            if (elapsed >= settings.Duration) Dismiss();
        }

        /// <summary>置物の見た目の高さ（m）。見た目が無ければ 0。</summary>
        private static float Height(GameObject root)
        {
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            return any ? bounds.size.y : 0f;
        }
    }
}
