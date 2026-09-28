using System;
using TpsDungeon.Audio.Runtime;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 被弾でスタン・気絶させる。確率は相対ダメージ量（その一撃 ÷ 最大 HP）に比例する（<see cref="DamageReactionRules"/>）。
    ///
    ///   スタン … Hit のひるみを流し、<see cref="stunDuration"/> 秒だけ動けない
    ///   気絶   … ラグドールで倒れ込み、<see cref="faintDuration"/> 秒後に起き上がる
    ///
    /// 重なり: 気絶中の被弾では何も起こさない（延長しない）。スタン中のスタンは時間を取り直し、スタン中の気絶は気絶に格上げする。
    /// 倒れたら両方とも解く（気絶中に倒れたらラグドールのまま起き上がらない）。
    /// 敵の AI は <see cref="CanAct"/> を見て、動けない間は何もしないこと。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Enemy Damage Reaction")]
    public sealed class EnemyDamageReaction : MonoBehaviour
    {
        // MonsterBuilder（エディタ専用アセンブリ）が焼き込んだ Animator のパラメータ名。向こうを変えたらここも揃えること。
        private const string HitParam = "Hit";

        [Header("確率（相対ダメージ量 1 あたり）")]
        [SerializeField, Min(0f), Tooltip("2 なら最大 HP の 1 割を削る一撃で 20%、半分で必ずスタンする。")]
        private float stunPerRelativeDamage = 2f;

        [SerializeField, Min(0f), Tooltip("0.5 なら最大 HP の 1 割を削る一撃で 5%、半分で 25% 気絶する。")]
        private float faintPerRelativeDamage = 0.5f;

        [Header("時間（秒）")]
        [SerializeField, Min(0f)]
        private float stunDuration = 0.5f;

        [SerializeField, Min(0f)]
        private float faintDuration = 3f;

        [Header("気絶で倒れ込む勢い")]
        [SerializeField, Min(0f), Tooltip("攻撃の向きに押し出す速さ（m/s）。")]
        private float faintPush = 2.5f;

        [Header("効果音（未設定なら鳴らさない）")]
        [SerializeField]
        private AudioClip stunClip;

        [SerializeField]
        private AudioClip faintClip;

        [SerializeField, Range(0f, 1f), Tooltip("この敵の状態異常の音だけの音量（SE 音量に掛かる）。")]
        private float volume = 1f;

        private EnemyHealth health;
        private EnemyRagdoll ragdoll;
        private Animator animator;
        private int hitHash;
        private bool resolved;

        private float stunRemaining;
        private float faintRemaining;

        public DamageReactionRules Rules => new DamageReactionRules(stunPerRelativeDamage, faintPerRelativeDamage);

        public bool IsStunned => stunRemaining > 0f;
        public bool IsFainted => faintRemaining > 0f;

        /// <summary>スタンも気絶もしておらず、倒れてもいない。</summary>
        public bool CanAct => !IsStunned && !IsFainted && !Health.IsDead;

        public AudioClip StunClip => stunClip;
        public AudioClip FaintClip => faintClip;

        /// <summary>スタンか気絶が始まった。</summary>
        public event Action<EnemyDamageReaction, DamageReaction> Reacted;

        /// <summary>スタンか気絶が明けて動けるようになった。</summary>
        public event Action<EnemyDamageReaction> Recovered;

        private EnemyHealth Health
        {
            get
            {
                Resolve();
                return health;
            }
        }

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            health = GetComponent<EnemyHealth>();
            ragdoll = GetComponent<EnemyRagdoll>();
            animator = GetComponentInChildren<Animator>();
            hitHash = Animator.StringToHash(HitParam);
        }

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDamaged(EnemyHealth source, DamageInfo damage, int dealt)
        {
            if (source.IsDead)
            {
                stunRemaining = 0f;
                faintRemaining = 0f;
                return;
            }

            // Random.value は 1 も返すので、確率 1 のときに外れないよう 1 未満に収める。
            float faintRoll = Mathf.Min(UnityEngine.Random.value, 0.9999999f);
            float stunRoll = Mathf.Min(UnityEngine.Random.value, 0.9999999f);
            Apply(Rules.Roll(dealt, source.MaxHp, false, faintRoll, stunRoll), damage);
        }

        /// <summary>状態異常を起こす。重なりのルールはここで決まる。起こせたら true。</summary>
        public bool Apply(DamageReaction reaction, DamageInfo damage = default)
        {
            if (reaction == DamageReaction.None || IsFainted || Health.IsDead) return false;

            if (reaction == DamageReaction.Faint)
            {
                stunRemaining = 0f;
                faintRemaining = faintDuration;
                if (ragdoll != null) ragdoll.Fall(PushFor(damage));
                else PlayHit();
                PlaySound(faintClip, damage);
            }
            else
            {
                stunRemaining = stunDuration;
                PlayHit();
                PlaySound(stunClip, damage);
            }

            Reacted?.Invoke(this, reaction);
            return true;
        }

        /// <summary>時間を進める。Update から呼ぶ（テストでは直接）。</summary>
        public void Tick(float deltaTime)
        {
            if (stunRemaining > 0f)
            {
                stunRemaining -= deltaTime;
                if (stunRemaining <= 0f)
                {
                    stunRemaining = 0f;
                    Recovered?.Invoke(this);
                }
            }

            if (faintRemaining > 0f)
            {
                faintRemaining -= deltaTime;
                if (faintRemaining <= 0f)
                {
                    faintRemaining = 0f;
                    if (ragdoll != null) ragdoll.GetUp();
                    Recovered?.Invoke(this);
                }
            }
        }

        private Vector3 PushFor(DamageInfo damage)
        {
            Vector3 direction = damage.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = -transform.forward;
            return direction.normalized * faintPush;
        }

        private void PlayHit()
        {
            if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(hitHash);
        }

        private void PlaySound(AudioClip clip, DamageInfo damage)
        {
            if (clip == null) return;
            GameAudio.Instance?.PlaySeAt(clip, damage.Point ?? transform.position, volume);
        }
    }
}
