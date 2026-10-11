using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 近づいて自爆する敵（04 自爆）。
    ///
    ///   追う → 相手が <see cref="fuseRange"/> に入ったら導火（<see cref="fuseTime"/> 秒）
    ///   → 導火の間は赤く点滅しながら膨らみ、遅くなって追い続ける
    ///   → 爆発。<see cref="blastRadius"/> の中で爆心から壁に遮られていない相手に、距離で減るダメージ（<see cref="EnemyAttackRules.ExplosionDamage"/>）
    ///   → 自分は消える（経験値は出さない。倒れる演出も無し）
    /// 導火を始める前に倒されたら爆発しない。導火を始めたらスタン・気絶でも止まらない（気絶なら倒れた所で爆発する）。
    /// 導火の途中で倒されたら爆発しない（倒したご褒美として経験値は出る）。
    /// MonsterBuilder がプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Bomber)")]
    public sealed class BomberEnemyBrain : EnemyBrain
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // 導火の間に流すクリップ（Blob の見本にある、体を揺する踊り）。無いグループなら流さない。
        private const string FuseParam = "Dance";

        [Header("自爆")]
        [SerializeField, Min(0.3f), Tooltip("相手の足元までこの距離に入ったら導火を始める（m）。")]
        private float fuseRange = 2.2f;

        [SerializeField, Min(0.1f), Tooltip("導火してから爆発するまで（秒）。")]
        private float fuseTime = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("導火の間の移動の速さの倍率。")]
        private float fuseSpeedScale = 0.4f;

        [SerializeField, Min(0.5f), Tooltip("爆発の半径（m）。")]
        private float blastRadius = 3.5f;

        [SerializeField, Range(0f, 1f), Tooltip("爆発の縁でのダメージの倍率（爆心で 1）。")]
        private float edgeDamageScale = 0.3f;

        [Header("見た目")]
        [SerializeField, Tooltip("爆発のエフェクト。")]
        private GameObject explosionEffect;

        [SerializeField, Min(0.01f), Tooltip("爆発のエフェクトの大きさ。")]
        private float explosionEffectScale = 1f;

        [SerializeField, Tooltip("導火の間に点滅させる色。")]
        private Color flashColor = new Color(1f, 0.15f, 0.05f);

        [SerializeField, Min(0f), Tooltip("導火の終わりに膨らむ割合（0.3 なら 1.3 倍）。")]
        private float swell = 0.3f;

        private readonly List<EnemyTarget> hits = new List<EnemyTarget>();
        private Renderer[] renderers;
        // renderers の同じ番号の、材質ごとの元の色（Quaternius の体は色ごとに材質が分かれている）。
        private Color[][] baseColors;
        private MaterialPropertyBlock block;
        private Transform model;
        private Vector3 modelScale;
        private EnemyRagdoll ragdoll;
        private bool fusing;
        private float fuseElapsed;

        protected override void Awake()
        {
            base.Awake();
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                baseColors[i] = new Color[materials.Length];
                for (int m = 0; m < materials.Length; m++)
                {
                    baseColors[i][m] = materials[m] != null && materials[m].HasProperty(BaseColorId) ? materials[m].GetColor(BaseColorId) : Color.white;
                }
            }
            block = new MaterialPropertyBlock();
            var animator = GetComponentInChildren<Animator>();
            model = animator != null ? animator.transform : null;
            if (model != null) modelScale = model.localScale;
            ragdoll = GetComponent<EnemyRagdoll>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            fusing = false;
        }

        protected override void Think(float dt)
        {
            Motor.LookAt = null;
            Motor.Destination = ChasePoint();
            Motor.StopDistance = 0.6f;

            if (fusing)
            {
                Tick(dt);
                return;
            }

            if (HasTarget && TargetVisible && TargetDistance <= fuseRange) Light();
        }

        protected override void WhileHeld(float dt)
        {
            if (fusing) Tick(dt);
        }

        private void Light()
        {
            fusing = true;
            fuseElapsed = 0f;
            Motor.SpeedScale = fuseSpeedScale;
            Play(FuseParam);
            PlaySound(windupClip, Center);
        }

        private void Tick(float dt)
        {
            fuseElapsed += dt;
            float t = Mathf.Clamp01(fuseElapsed / fuseTime);

            // 点滅は終わりへ向かって速くなる。
            float blink = Mathf.PingPong(fuseElapsed * Mathf.Lerp(4f, 16f, t), 1f);
            SetFlash(blink);
            if (model != null) model.localScale = modelScale * (1f + swell * t * t);

            if (fuseElapsed >= fuseTime) Explode();
        }

        private void Explode()
        {
            fusing = false;

            // 気絶で倒れていたら、ルートではなく倒れた体の所で爆発する。
            Vector3 center = ragdoll != null && ragdoll.IsRagdoll && ragdoll.Bodies.Count > 0 && ragdoll.Bodies[0] != null
                ? ragdoll.Bodies[0].position
                : Center;

            EnemyEffects.Spawn(explosionEffect, center, Quaternion.identity, explosionEffectScale, 4f);
            PlaySound(attackClip, center);

            EnemyHits.Collect(center, blastRadius + EnemyTarget.DecoyRadius, hits);
            foreach (EnemyTarget target in hits)
            {
                Vector3 aim = target.AimPoint;
                if (!EnemyHits.Clear(center, aim, transform)) continue;
                int dealt = EnemyAttackRules.ExplosionDamage(damage, Vector3.Distance(center, aim), blastRadius, edgeDamageScale);
                target.Damage(dealt, gameObject);
            }

            Destroy(gameObject);
        }

        public override string StateLabel => fusing ? $"導火 {Mathf.Max(0f, fuseTime - fuseElapsed):0.0}s" : HasTarget ? "追う" : "待つ";

        protected override void DrawRanges()
        {
            Disc(transform.position, fuseRange, new Color(1f, 0.3f, 0.2f, 0.9f));
            Disc(transform.position, blastRadius, new Color(1f, 0.6f, 0f, 0.6f));
        }

        protected override void OnDeath()
        {
            if (!fusing) return;
            fusing = false;
            SetFlash(0f);
            if (model != null) model.localScale = modelScale;
        }

        /// <summary>材質ごとの元の色を、amount（0〜1）だけ点滅の色へ寄せる。0 なら元に戻す。</summary>
        private void SetFlash(float amount)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                for (int m = 0; m < baseColors[i].Length; m++)
                {
                    block.Clear();
                    if (amount > 0f) block.SetColor(BaseColorId, Color.Lerp(baseColors[i][m], flashColor, amount));
                    r.SetPropertyBlock(block, m);
                }
            }
        }
    }
}
