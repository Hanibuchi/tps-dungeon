using System;
using System.Collections;
using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.Rendering;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// HP が 0 になった敵の始末。
    ///
    ///   1. 気絶と同じラグドールで、最後の一撃の向きへ倒れ込む（気絶で倒れていたらそのまま）
    ///   2. 死亡音を鳴らす（未設定なら気絶の音）。当たり判定を切り、経験値を配る（<see cref="EnemyExpReward"/> があれば）
    ///   3. <see cref="corpseDelay"/> 秒横たわったあと、<see cref="fadeDuration"/> 秒かけて透明になって消える
    ///
    /// ラグドールを持たない敵だけは、Animator の Dead で倒れるアニメーションに落とす。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Enemy Death")]
    public sealed class EnemyDeath : MonoBehaviour
    {
        // MonsterBuilder（エディタ専用アセンブリ）が焼き込んだ Animator のパラメータ名。向こうを変えたらここも揃えること。
        private const string DeadParam = "Dead";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        [Header("倒れ込み")]
        [SerializeField, Min(0f), Tooltip("最後の一撃の向きに押し出す速さ（m/s）。一撃のノックバックも足される。")]
        private float deathPush = 3f;

        [Header("効果音")]
        [SerializeField, Tooltip("死んだときの音。未設定なら EnemyDamageReaction の気絶の音。")]
        private AudioClip deathClip;

        [SerializeField, Range(0f, 1f), Tooltip("死亡音の音量（SE 音量に掛かる）。")]
        private float volume = 1f;

        [Header("消え方（秒）")]
        [SerializeField, Min(0f), Tooltip("倒れてから透明になり始めるまで。")]
        private float corpseDelay = 2f;

        [SerializeField, Min(0.01f), Tooltip("透明になりきるまで。なりきったら GameObject ごと消す。")]
        private float fadeDuration = 1.5f;

        [SerializeField, Tooltip("半透明の URP Lit 材質。透明にするときはこれを元に、元の材質の絵と色を写した材質に差し替える。" +
                                 "未設定なら元の材質をその場で半透明に切り替える（ビルドでは半透明の変種が落ちていることがある）。")]
        private Material fadeTemplate;

        private EnemyHealth health;
        private DamageInfo lastDamage;
        private readonly List<Material> fadeMaterials = new List<Material>();

        /// <summary>始末を終えた（倒れ込んで経験値を配った直後。消えるのはこのあと）。</summary>
        public event Action<EnemyDeath> Finished;

        /// <summary>死亡音。未設定なら気絶の音。</summary>
        public AudioClip DeathClip
        {
            get
            {
                if (deathClip != null) return deathClip;
                var reaction = GetComponent<EnemyDamageReaction>();
                return reaction != null ? reaction.FaintClip : null;
            }
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDestroy()
        {
            foreach (Material m in fadeMaterials)
            {
                if (m != null) Destroy(m);
            }
            fadeMaterials.Clear();
        }

        // 倒れた一撃でも Died より先に飛ぶので、倒れ込む向きに使える。
        private void OnDamaged(EnemyHealth _, DamageInfo damage, int __) => lastDamage = damage;

        private void OnDied(EnemyHealth _)
        {
            var ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                if (!ragdoll.IsRagdoll) ragdoll.Fall(PushFor(lastDamage));
            }
            else
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null) animator.SetBool(DeadParam, true);
            }

            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;

            AudioClip clip = DeathClip;
            if (clip != null) GameAudio.Instance?.PlaySeAt(clip, lastDamage.Point ?? transform.position, volume);

            var reward = GetComponent<EnemyExpReward>();
            if (reward != null) reward.Grant();

            Finished?.Invoke(this);

            if (isActiveAndEnabled) StartCoroutine(FadeOutAndDestroy());
        }

        private Vector3 PushFor(DamageInfo damage)
        {
            Vector3 direction = damage.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = -transform.forward;
            return direction.normalized * (deathPush + Mathf.Max(0f, damage.Knockback));
        }

        private IEnumerator FadeOutAndDestroy()
        {
            if (corpseDelay > 0f) yield return new WaitForSeconds(corpseDelay);

            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                // 透明の途中で影だけ濃く残らないよう、影は落とさない。
                r.shadowCastingMode = ShadowCastingMode.Off;
                Material[] source = r.sharedMaterials;
                var replaced = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    replaced[i] = source[i] != null ? CreateFadeMaterial(source[i]) : null;
                    if (replaced[i] != null) fadeMaterials.Add(replaced[i]);
                }
                r.sharedMaterials = replaced;
            }

            var opaque = new Color[fadeMaterials.Count];
            for (int i = 0; i < fadeMaterials.Count; i++)
            {
                opaque[i] = fadeMaterials[i].HasProperty(BaseColorId) ? fadeMaterials[i].GetColor(BaseColorId) : Color.white;
            }

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                float alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                for (int i = 0; i < fadeMaterials.Count; i++)
                {
                    Color c = opaque[i];
                    c.a *= alpha;
                    fadeMaterials[i].SetColor(BaseColorId, c);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>元の材質の見た目（絵・色・つや）を持った、半透明の材質を作る。</summary>
        private Material CreateFadeMaterial(Material original)
        {
            Material m;
            if (fadeTemplate != null)
            {
                m = new Material(fadeTemplate) { name = original.name + " (Fade)" };
                if (original.HasProperty(BaseMapId) && m.HasProperty(BaseMapId))
                {
                    m.SetTexture(BaseMapId, original.GetTexture(BaseMapId));
                    m.SetTextureScale(BaseMapId, original.GetTextureScale(BaseMapId));
                    m.SetTextureOffset(BaseMapId, original.GetTextureOffset(BaseMapId));
                }
                CopyColor(original, m, BaseColorId);
                CopyFloat(original, m, SmoothnessId);
                CopyFloat(original, m, MetallicId);
            }
            else
            {
                m = new Material(original) { name = original.name + " (Fade)" };
                MakeTransparent(m);
            }
            return m;
        }

        /// <summary>URP Lit の材質を半透明（アルファで混ぜる）に切り替える。エディタで半透明の雛形を作るときにも使う。</summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void CopyColor(Material from, Material to, int id)
        {
            if (from.HasProperty(id) && to.HasProperty(id)) to.SetColor(id, from.GetColor(id));
        }

        private static void CopyFloat(Material from, Material to, int id)
        {
            if (from.HasProperty(id) && to.HasProperty(id)) to.SetFloat(id, from.GetFloat(id));
        }
    }
}
