using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の頭の共通部分。狙う相手を探して覚え、種類ごとの動き（派生クラスの <see cref="Think"/>）を毎フレーム呼ぶ。
    ///
    /// 狙う相手:
    ///   ・召喚した置物（<see cref="DecoyTarget"/>）が見えていれば、メンバーより先に狙う
    ///   ・次に、見えている一番近いメンバー（Ally レイヤーの <see cref="PlayerHealth"/>）
    ///   ・見失っても <see cref="memoryTime"/> 秒は最後に見た所へ向かう。<see cref="loseRadius"/> より離れたら諦める
    ///   ・撃たれたら、見えていなくても近くのメンバーを狙う
    /// スタン・気絶の間は <see cref="Think"/> を呼ばず、代わりに <see cref="Interrupted"/> を 1 回呼ぶ（溜めていた攻撃を取り消す）。
    /// 攻撃には必ず溜め（予備動作）を挟んで、見てから避けられるようにする。
    /// 動き（歩く先・向き）は <see cref="EnemyMotor"/> に書き込む。
    /// </summary>
    [RequireComponent(typeof(EnemyMotor))]
    [RequireComponent(typeof(EnemyHealth))]
    public abstract class EnemyBrain : MonoBehaviour
    {
        // MonsterBuilder（エディタ専用アセンブリ）が焼き込んだ Animator のパラメータ名。向こうを変えたらここも揃えること。
        protected const string AttackParam = "Attack";

        /// <summary>気付いたときの鳴き声を、続けて鳴らさない間（秒）。</summary>
        private const float NoticeInterval = 6f;

        [Header("見つける")]
        [SerializeField, Min(1f), Tooltip("この距離の中で見えている相手に気付く（m）。")]
        private float detectRadius = 14f;

        [SerializeField, Min(1f), Tooltip("狙っている相手がこれより離れたら諦める（m）。")]
        private float loseRadius = 22f;

        [SerializeField, Min(0f), Tooltip("見失ってから、最後に見た所へ向かい続ける時間（秒）。")]
        private float memoryTime = 5f;

        [SerializeField, Min(0.05f), Tooltip("相手を探し直す間隔（秒）。敵ごとにずらす。")]
        private float scanInterval = 0.2f;

        [Header("攻撃")]
        [SerializeField, Min(0), Tooltip("1 回の攻撃のダメージ。")]
        protected int damage = 10;

        [SerializeField, Min(0f), Tooltip("攻撃を終えてから次の攻撃までの間（秒）。")]
        protected float cooldown = 1.5f;

        [SerializeField, Tooltip("攻撃で流す Animator のパラメータ。既定の Attack はグループの攻撃（Punch など）。" +
            "武器を持っている敵は Weapon（武器を振る）にする。グループに無ければ Attack を流す。")]
        protected string attackAnimation = AttackParam;

        [Header("効果音（未設定なら鳴らさない）")]
        [SerializeField, Tooltip("相手に気付いたときの鳴き声（見失って探し直したときも。続けては鳴らさない）。")]
        protected AudioClip noticeClip;

        [SerializeField, Tooltip("溜め始めの音。")]
        protected AudioClip windupClip;

        [SerializeField, Tooltip("攻撃を出した瞬間の音（振り・発射・爆発・霧を出す）。")]
        protected AudioClip attackClip;

        [SerializeField, Tooltip("攻撃が当たった音。")]
        protected AudioClip hitClip;

        [SerializeField, Range(0f, 1f)]
        protected float volume = 1f;

        [Header("見た目（未設定なら出さない）")]
        [SerializeField, Tooltip("溜めの間に出す効果。近接は頭の上の予兆、遠距離は撃ち出す所に集まる光。溜めが終わるか取り消されたら消す。")]
        protected GameObject windupEffect;

        [SerializeField, Tooltip("攻撃が相手に当たった所に出す効果。")]
        protected GameObject hitEffect;

        private static readonly Collider[] overlap = new Collider[32];

        private GameObject activeWindupEffect;
        private float nextScan;
        private float nextNotice;
        private float lastSeenTime = float.NegativeInfinity;
        private bool wasActing = true;
        private Collider body;
        private Animator animator;
        private readonly Dictionary<int, AnimatorControllerParameterType> animatorParams = new Dictionary<int, AnimatorControllerParameterType>();

        protected EnemyMotor Motor { get; private set; }
        protected EnemyHealth Health { get; private set; }
        protected EnemyDamageReaction Reaction { get; private set; }

        /// <summary>今狙っている相手。<see cref="HasTarget"/> が偽なら意味がない。</summary>
        public EnemyTarget Target { get; private set; }
        public bool HasTarget { get; private set; }

        /// <summary>今相手が見えている（最後の探し直しの時点で）。</summary>
        public bool TargetVisible { get; private set; }

        /// <summary>最後に相手を見た所（足元）。</summary>
        public Vector3 LastSeenPosition { get; private set; }

        /// <summary>次の攻撃までの残り（秒）。0 で撃てる。</summary>
        public float CooldownRemaining { get; protected set; }

        /// <summary>今何をしているか（確認用の窓やシーンの表示に出す短い言葉）。</summary>
        public abstract string StateLabel { get; }

        /// <summary>この敵の攻撃の 1 回のダメージ（毒霧なら 1 刻み）。</summary>
        public int Damage => damage;

        /// <summary>体の中心（当たり判定の中心、無ければ足元から 1 m 上）。</summary>
        protected Vector3 Center => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up;

        /// <summary>体の高さ（m）。</summary>
        protected float BodyHeight => body != null ? body.bounds.size.y : 1.8f;

        /// <summary>頭の少し上（予兆を出す所）。</summary>
        protected Vector3 HeadPoint
        {
            get
            {
                Vector3 p = transform.position;
                float top = body != null && body.enabled ? body.bounds.max.y : p.y + 1.8f;
                return new Vector3(p.x, top + 0.25f, p.z);
            }
        }

        /// <summary>相手の足元までの水平の距離（m）。</summary>
        protected float TargetDistance => HasTarget ? Flat(Target.Position - transform.position).magnitude : float.PositiveInfinity;

        protected virtual void Awake()
        {
            Motor = GetComponent<EnemyMotor>();
            Health = GetComponent<EnemyHealth>();
            Reaction = GetComponent<EnemyDamageReaction>();
            body = GetComponent<Collider>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters) animatorParams[p.nameHash] = p.type;
            }
        }

        protected virtual void OnEnable()
        {
            nextScan = Time.time + UnityEngine.Random.value * scanInterval;
            wasActing = true;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        protected virtual void OnDisable()
        {
            Health.Damaged -= OnDamaged;
            Health.Died -= OnDied;
        }

        private void OnDied(EnemyHealth _)
        {
            StopWindupEffect();
            OnDeath();
            enabled = false;
        }

        /// <summary>倒れた。溜めていた物の後始末をする（見た目を消すなど）。</summary>
        protected virtual void OnDeath() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);

            bool acting = Reaction == null || Reaction.CanAct;
            if (!acting)
            {
                if (wasActing)
                {
                    StopWindupEffect();
                    Interrupted();
                }
                wasActing = false;
                WhileHeld(dt);
                return;
            }
            wasActing = true;

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + scanInterval;
                Scan();
            }

            Think(dt);
        }

        /// <summary>種類ごとの動き。動ける間、毎フレーム呼ぶ。</summary>
        protected abstract void Think(float deltaTime);

        /// <summary>スタン・気絶で動けなくなった瞬間。溜めていた攻撃を取り消す。</summary>
        protected virtual void Interrupted() { }

        /// <summary>動けない間、毎フレーム呼ぶ（自爆の導火のように止まらない物だけが使う）。</summary>
        protected virtual void WhileHeld(float deltaTime) { }

        // ---- 相手を探す ----

        private void Scan()
        {
            if (HasTarget && !Target.IsValid) Forget();

            if (HasTarget)
            {
                float distance = Vector3.Distance(transform.position, Target.Position);
                TargetVisible = distance <= loseRadius && CanSee(Target);
                if (TargetVisible) Remember();
                else if (distance > loseRadius || Time.time - lastSeenTime > memoryTime) Forget();
            }

            // 置物は見えたらすぐ乗り換える（置物どうしなら今のままでよい）。
            if (HasTarget && Target.Decoy != null) return;
            if (TryFindDecoy(out EnemyTarget decoy))
            {
                SetTarget(decoy);
                return;
            }

            if (!HasTarget && TryFindCharacter(detectRadius, true, out EnemyTarget character)) SetTarget(character);
        }

        private bool TryFindDecoy(out EnemyTarget found)
        {
            found = default;
            float best = detectRadius;
            foreach (DecoyTarget decoy in DecoyTarget.All)
            {
                var target = new EnemyTarget(decoy);
                float distance = Vector3.Distance(transform.position, target.Position);
                if (!target.IsValid || distance > best || !CanSee(target)) continue;
                best = distance;
                found = target;
            }
            return found.Decoy != null;
        }

        private bool TryFindCharacter(float radius, bool needSight, out EnemyTarget found)
        {
            found = default;
            int mask = AllyLayer.Mask;
            if (mask == 0) mask = ~0;

            float best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlap, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var character = overlap[i].GetComponentInParent<PlayerHealth>();
                if (character == null) continue;

                var target = new EnemyTarget(character);
                float distance = Vector3.Distance(transform.position, target.Position);
                if (!target.IsValid || distance >= best) continue;
                if (needSight && !CanSee(target)) continue;
                best = distance;
                found = target;
            }
            return found.Character != null;
        }

        private void SetTarget(EnemyTarget target)
        {
            // 相手が居なかったところから気付いたら鳴く。群れで一斉に気付いたときや見失い直しで鳴きすぎないよう、間を空ける。
            if (!HasTarget && Time.time >= nextNotice)
            {
                nextNotice = Time.time + NoticeInterval;
                PlaySound(noticeClip, Center);
            }

            Target = target;
            HasTarget = true;
            TargetVisible = true;
            Remember();
        }

        private void Remember()
        {
            lastSeenTime = Time.time;
            LastSeenPosition = Target.Position;
        }

        private void Forget()
        {
            HasTarget = false;
            TargetVisible = false;
            Target = default;
        }

        /// <summary>撃たれたら、見えていなくても近くのメンバーへ向かう（背中から撃たれて棒立ちにならないように）。</summary>
        private void OnDamaged(EnemyHealth _, DamageInfo __, int ___)
        {
            if (HasTarget || Health.IsDead) return;
            if (TryFindCharacter(loseRadius, false, out EnemyTarget character)) SetTarget(character);
        }

        /// <summary>体の中心から相手の狙う所まで、壁に遮られていないか。</summary>
        protected bool CanSee(EnemyTarget target) => EnemyHits.Clear(Center, target.AimPoint, transform);

        // ---- 便利なもの ----

        /// <summary>
        /// Animator のクリップを流す。trigger なら 1 回、bool（ループするクリップ）なら立てる。
        /// プレハブのグループに無いパラメータなら何もしない。
        /// </summary>
        protected void Play(string name)
        {
            if (!TryParam(name, out int hash, out AnimatorControllerParameterType type)) return;
            if (type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(hash);
            else if (type == AnimatorControllerParameterType.Bool) animator.SetBool(hash, true);
        }

        /// <summary><see cref="Play"/> で立てた bool を下ろす。trigger なら何もしない。</summary>
        protected void Stop(string name)
        {
            if (TryParam(name, out int hash, out AnimatorControllerParameterType type) && type == AnimatorControllerParameterType.Bool)
                animator.SetBool(hash, false);
        }

        /// <summary>攻撃のアニメーションを流す（<see cref="attackAnimation"/>、グループに無ければ Attack）。</summary>
        protected void PlayAttack() => Play(!string.IsNullOrEmpty(attackAnimation) && HasParam(attackAnimation) ? attackAnimation : AttackParam);

        protected bool HasParam(string name) => animatorParams.ContainsKey(Animator.StringToHash(name));

        private bool TryParam(string name, out int hash, out AnimatorControllerParameterType type)
        {
            hash = Animator.StringToHash(name);
            type = default;
            return animator != null && animator.isActiveAndEnabled && animatorParams.TryGetValue(hash, out type);
        }

        protected void PlaySound(AudioClip clip, Vector3 position)
        {
            if (clip != null) GameAudio.Instance?.PlaySeAt(clip, position, volume);
        }

        /// <summary>
        /// 相手の足元の近くの点を、迷わず向かえる先として返す。相手が居なければ最後に見た所。
        /// 見失って覚えている時間も過ぎたら null。
        /// </summary>
        protected Vector3? ChasePoint()
        {
            if (!HasTarget) return null;
            return TargetVisible ? Target.Position : LastSeenPosition;
        }

        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>溜めの効果を at に出す。体の子にして一緒に動かす（向きを変えても付いてくる）。</summary>
        protected void StartWindupEffect(Vector3 at)
        {
            StopWindupEffect();
            if (windupEffect == null) return;
            activeWindupEffect = Instantiate(windupEffect, at, transform.rotation, transform);
            EnemyEffects.Prepare(activeWindupEffect, 1f, true);
        }

        /// <summary>溜めの効果を止める。出ている粒は消えるまで残す。</summary>
        protected void StopWindupEffect()
        {
            if (activeWindupEffect == null) return;
            EnemyEffects.Release(activeWindupEffect, 0.6f);
            activeWindupEffect = null;
        }

        /// <summary>当たりの効果を at に出す。</summary>
        protected void SpawnHitEffect(Vector3 at)
        {
            EnemyEffects.Spawn(hitEffect, at, Quaternion.identity, 1f, 1.5f);
        }

        // ---- 確認用の表示（シーンで選んだときだけ） ----

        private void OnDrawGizmosSelected()
        {
            Disc(transform.position, detectRadius, new Color(1f, 0.9f, 0.2f, 0.5f));
            Disc(transform.position, loseRadius, new Color(1f, 0.9f, 0.2f, 0.2f));
            DrawRanges();

            if (!Application.isPlaying || !HasTarget) return;
            Gizmos.color = TargetVisible ? Color.red : Color.gray;
            Gizmos.DrawLine(Center, TargetVisible ? Target.AimPoint : LastSeenPosition + Vector3.up);
        }

        /// <summary>種類ごとの間合い（選んだときに足元へ円で描く）。</summary>
        protected virtual void DrawRanges() { }

        /// <summary>足元に水平の円を描く（エディタの中だけ）。</summary>
        protected static void Disc(Vector3 center, float radius, Color color)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawWireDisc(center + Vector3.up * 0.05f, Vector3.up, radius);
#endif
        }
    }
}
