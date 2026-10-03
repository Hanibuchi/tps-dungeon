using System;
using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 持たされた近接武器で攻撃する実行役。入力は持たず、外から <see cref="Equip"/>（持ち替え）・<see cref="PressAttack"/>（攻撃の押下）・
    /// <see cref="AimForward"/>（向き）を渡してもらう。プレイヤーは PlayerMeleeInput が、仲間は AI が叩く。
    /// 押すたびにコンボを 1 段ずつ進め（MeleeComboState）、段の判定の瞬間に段の当て方（MeleeComboStep.motion）で当てる:
    ///   Swing … 前方の箱の中の敵へ
    ///   Lunge … 前へ走り、走っている間ずっと前方の箱で当てる（同じ敵には 1 走り 1 回）。壁で止まる。敵はすり抜けるか止まるかを武器種で選ぶ。
    ///           「多重」1 つで走る回数が 1 回増える。走り終えたら段の頭から（溜めて突き出して）もう一度走る
    ///   Slam  … 前方の着弾点の円の中の敵へ。「数」で叩きつけが増え、持ち主を中心に扇状に並ぶ。
    ///           「多重」で叩きつけごとに、その向きへずらした追撃が遅れて落ちる（本撃と同じダメージ）
    /// 数値とエフェクトは武器（WeaponDefinition）と武器種（WeaponTypeDefinition）から出す。
    /// 段のモーションは判定の時計と同じフレームに、段のステートへ直接 CrossFade して頭から再生する（連打してもずれていかないように）。
    /// 持ち替えたら Animator の WeaponType と手の見た目も替える。素手や武器でない物を持っているときは素手の武器（unarmedWeapon）で殴る。
    /// 近接でない武器（弓など）はここでは振らない（撃つのは RangedAttacker）。見た目（Animator の WeaponType と手のモデル）だけ替える。
    /// コンボが切れたら武器種の待ち（WeaponTypeDefinition.ComboCooldown）、持ち替えたら全武器共通の待ち（switchCooldown）の間は振れない。
    /// コンボボーナスの段数（ComboChain）は周をまたいで数える。空振りするか、待ちが明けて comboKeepTime 秒振らなければ途切れる。
    /// サイズのエンチャントは判定に合わせて、振り・叩きつけ・追撃・爆発のエフェクトも大きくする。
    /// キャラのルート（CharacterController と同じ GameObject）に付ける。基礎攻撃力は同じ GameObject の CharacterProgression から読む。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Melee Attacker")]
    public sealed class MeleeAttacker : MonoBehaviour
    {
        // CharacterAnimatorBuilder（エディタ専用アセンブリ）が焼き込んだ値。向こうを変えたらここも揃えること。
        private const string WeaponTypeParam = "WeaponType";
        private const string AttackParam = "Attack";
        private const string ComboStepParam = "ComboStep";
        private const string AttackSpeedParam = "AttackSpeed";

        // CharacterAnimatorBuilder.BuildCombo が付ける段のステート名「{武器名} Attack {段+1}」の武器名。添え字は AnimatorWeaponType。
        private static readonly string[] ComboStatePrefixes = { "Unarmed", "OneHanded", "TwoHanded", null, null, "DashThrust", "Hammer" };

        /// <summary>段のステートへ溶け込む秒数（CharacterAnimatorBuilder の段への遷移と同じ）。</summary>
        private const float ComboCrossFade = 0.08f;

        /// <summary>走っているとき、進めた量が予定のこの割合を切ったら壁に当たったとして止める。</summary>
        private const float LungeBlockedRatio = 0.3f;

        /// <summary>
        /// 敵をすり抜ける走りで、走り終えたときにまだ敵に重なっていたら、抜けるまでこの距離（m）まで走り足す。
        /// 重なったままぶつかり直すと、押し戻されて手前へ戻ってしまうため。
        /// </summary>
        private const float LungeMaxOverrun = 1.5f;

        [SerializeField, Tooltip("キャラの Animator。未設定なら子から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("攻撃判定が当たるレイヤー。走る段で壁を探すのにも使う。")]
        private LayerMask hitMask = ~0;

        [SerializeField, Tooltip("段を振り始めるときに、体を AimForward へ向ける。")]
        private bool faceAimOnAttack = true;

        [SerializeField, Min(0f), Tooltip("持ち替えてから振れるまでの秒数。全武器共通。")]
        private float switchCooldown = 0.5f;

        [SerializeField, Tooltip("素手や武器でない物を持っているときに振る武器（インベントリには入れない）。未設定なら素手では攻撃しない。")]
        private WeaponDefinition unarmedWeapon;

        [SerializeField, Tooltip("クリティカルのとき、命中のエフェクトに重ねて出す見た目（任意）。")]
        private GameObject criticalHitEffect;

        [SerializeField, Min(0.01f)]
        private float criticalHitEffectScale = 1f;

        [SerializeField, Tooltip("クリティカルのとき、命中の音に重ねて鳴らす音（任意、1 振り 1 回）。")]
        private AudioClip criticalHitSound;

        [SerializeField, Range(0f, 1f)]
        private float criticalHitSoundVolume = 0.7f;

        [SerializeField, Min(0f), Tooltip("コンボが切れて待ちが明けてから、コンボボーナスの段数を持ち越す秒数。この間に振り始めれば続く。")]
        private float comboKeepTime = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("コンボボーナスの段が上がったときの合図（生成した低い「ドン」。段が進むほど高く硬い）の大きさ。振りや命中の音の下に控えめに。")]
        private float comboSoundVolume = 0.3f;

        [SerializeField, Tooltip("爆発のエンチャントで出す見た目（任意）。出して数秒で消す。")]
        private GameObject explosionEffect;

        [SerializeField, Min(0.01f)]
        private float explosionEffectScale = 1f;

        [SerializeField, Tooltip("爆発のエンチャントで鳴らす音（任意）。")]
        private AudioClip explosionSound;

        [SerializeField, Range(0f, 1f)]
        private float explosionSoundVolume = 1f;

        [SerializeField, Tooltip("当てたダメージをコンソールに出す（調整用）。")]
        private bool logHits;

        private CharacterProgression progression;
        private PlayerGear gear;
        private CharacterController characterController;

        private bool initialized;
        private bool equipped;
        private ItemInstance held;
        private WeaponDefinition heldWeapon;
        private GameObject heldModel;
        private MeleeComboState combo;
        private MeleeWeaponStats stats;
        private bool pressQueued;
        private readonly ComboChain chain = new ComboChain();
        private bool criticalSoundThisSwing;

        private readonly HashSet<EnemyHealth> hitThisSwing = new HashSet<EnemyHealth>();
        private readonly HashSet<EnemyHealth> explosionTargets = new HashSet<EnemyHealth>();
        private readonly Collider[] overlap = new Collider[32];

        private Lunge lunge;

        // 多重で走り足す残りの回数と、次に走り出すのを待っている段（待っていなければ -1）・その残り秒数。
        private int dashesLeft;
        private int nextDashStep = -1;
        private float nextDashDelay;

        // 溜めてから放つ振りのエフェクト（SpawnLeadingSwingEffect）の、放つ粒。判定の瞬間にこちらから出す。
        private readonly List<ParticleSystem> pendingRelease = new List<ParticleSystem>();
        private readonly List<Collider> lungeIgnored = new List<Collider>();
        private readonly List<FollowUp> followUps = new List<FollowUp>();

        private bool hasWeaponTypeParam;
        private bool hasAttackParam;
        private bool hasComboStepParam;
        private bool hasAttackSpeedParam;

        /// <summary>走る段で走っている最中の状態。</summary>
        private sealed class Lunge
        {
            public int Step;
            public Vector3 Direction;
            public float Speed;
            public float Remaining;
            public float Overrun;
            public int Hits;
        }

        /// <summary>叩きつけのあとに遅れて落ちる追撃 1 回。</summary>
        private sealed class FollowUp
        {
            public int Step;
            public WeaponTypeDefinition Type;
            public MeleeComboStep Shape;
            public Vector3 Center;
            public Vector3 Forward;
            public float Delay;
            public int Damage;
            public float ReactionScale;
            /// <summary>着弾の音と揺れを出すか。同じ時刻に落ちる追撃（数で増えた向きの分）では 1 つだけ。</summary>
            public bool Loud;
        }

        /// <summary>今振る武器（素手なら unarmedWeapon）。振れなければ null。</summary>
        public WeaponDefinition HeldWeapon => heldWeapon;

        /// <summary>今振っている段（0 始まり）。振っていなければ -1。</summary>
        public int ComboStep => combo != null ? combo.Step : -1;

        /// <summary>次に振れるまでの待ちの残りの割合（1 で待ち始め、0 で振れる）。振れる武器が無いときも 0。</summary>
        public float CooldownFraction =>
            combo != null && combo.IsCoolingDown && combo.CooldownDuration > 0f ? Mathf.Clamp01(combo.CooldownRemaining / combo.CooldownDuration) : 0f;

        /// <summary>
        /// コンボボーナスの段数（続けて当てた段の数。次の 1 撃にこの分上乗せする）。コンボボーナスの無い武器では 0。
        /// </summary>
        public int ComboCount => stats != null && stats.ComboBonus > 0f ? chain.Count : 0;

        /// <summary>走る段で走っている最中か。持ち主の歩きはこの間止めること（プレイヤーなら PlayerMeleeInput が止める）。</summary>
        public bool IsLunging => lunge != null || nextDashStep >= 0;

        /// <summary>走っていて、武器種が走る間の無敵を持っているか。被ダメージ側が読む。</summary>
        public bool IsInvulnerable => IsLunging && heldWeapon != null && heldWeapon.WeaponType.InvulnerableDuringLunge;

        /// <summary>段を振り始めるときに体を向ける向き（水平に直して使う）。ゼロなら向きを変えない。</summary>
        public Vector3 AimForward { get; set; }

        /// <summary>判定を出し終えた。(段, 当てた敵の数)。走る段は走り終えたとき、叩きつけは本撃だけを数える。</summary>
        public event Action<int, int> Swung;

        /// <summary>敵に当てた 1 回ごと（本撃・爆発・追撃）。試験の窓や、ダメージ表示が読む。</summary>
        public event Action<MeleeHitRecord> Dealt;

        /// <summary>当てたダメージをコンソールにも出すか（調整用）。</summary>
        public bool LogHits
        {
            get => logHits;
            set => logHits = value;
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake() => Initialize();

        // 入力役の OnEnable が先に Equip を呼ぶことがあるので、Awake を待たずに要る物を揃えられるようにしておく。
        private void Initialize()
        {
            if (initialized) return;

            initialized = true;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            progression = GetComponent<CharacterProgression>();
            gear = GetComponent<PlayerGear>();
            characterController = GetComponent<CharacterController>();
            CacheAnimatorParameters();
        }

        private void OnDisable()
        {
            CancelExtraDashes();
            EndLunge();
            pressQueued = false;
        }

        /// <summary>攻撃を 1 回押す。次の Update で使う（待ち中や振っている途中の押下の扱いは MeleeComboState に従う）。</summary>
        public void PressAttack() => pressQueued = true;

        /// <summary>
        /// item を手に持つ（null なら素手）。switched が真なら、中身が同じでも持ち替えとして待たせる（枠を選び直したときなど）。
        /// 最初の 1 回は待たせない。前の武器の待ちが長ければ引き継ぎ、往復で消させない。
        /// </summary>
        public void Equip(ItemInstance item, bool switched = false)
        {
            if (equipped && item == held && !switched) return;

            Initialize();
            float switchWait = equipped ? Mathf.Max(switchCooldown, combo != null ? combo.CooldownRemaining : 0f) : 0f;
            equipped = true;
            CancelExtraDashes();
            EndLunge();
            chain.Reset();

            held = item;
            WeaponDefinition weapon = item?.Weapon;
            // お守り・盾は振らないので、宝石などと同じく素手で殴る（構えも素手）。見た目は手に持たせる。
            bool isWeapon = weapon != null && weapon.WeaponType != null && !weapon.WeaponType.IsPassiveGear;
            heldWeapon = IsMelee(weapon) ? weapon : !isWeapon && IsMelee(unarmedWeapon) ? unarmedWeapon : null;

            stats = heldWeapon != null ? ComputeStats() : null;
            WeaponTypeDefinition heldType = heldWeapon != null ? heldWeapon.WeaponType : null;
            combo = heldType != null ? new MeleeComboState(Timings(heldType, stats), heldType.ComboChainGrace, heldType.ComboCooldown) : null;
            combo?.StartCooldown(switchWait);

            WeaponDefinition pose = isWeapon ? weapon : heldWeapon;
            if (animator != null && hasWeaponTypeParam)
                animator.SetInteger(WeaponTypeParam, pose != null ? pose.WeaponType.AnimatorWeaponType : 0);

            AttachHeldModel(weapon);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            chain.Tick(dt);
            TickFollowUps(dt);
            if (lunge != null) TickLunge(dt);
            TickExtraDash(dt);

            bool pressed = pressQueued;
            pressQueued = false;
            if (combo == null) return;

            if (pressed && !combo.IsSwinging) stats = ComputeStats();
            float speed = stats != null ? stats.AttackSpeed : 1f;

            // 先行入力があると、判定と次の段の始まりが同じフレームに来ることがある。判定は進める前の段で出す。
            int swinging = combo.Step;
            ComboEvents events = combo.Tick(dt, pressed, speed);
            if ((events & ComboEvents.Hit) != 0) DealHit(swinging >= 0 ? swinging : combo.Step);
            if ((events & ComboEvents.StepStarted) != 0) BeginStep(combo.Step);
            if ((events & ComboEvents.Ended) != 0) chain.StartTimeout(combo.CooldownRemaining + comboKeepTime);
        }

        // ---- 持ち替え ----

        private static bool IsMelee(WeaponDefinition weapon) => weapon != null && weapon.WeaponType != null && weapon.WeaponType.IsMelee;

        /// <summary>
        /// 段の時間。走る段は、持続時間のエンチャントで走る時間が延びた分と、
        /// 多重で走り足す 1 回ごとの溜め（hitTime）と走る時間を足して段を延ばす。
        /// </summary>
        private static List<ComboStepTiming> Timings(WeaponTypeDefinition type, MeleeWeaponStats stats)
        {
            float lungeScale = stats != null ? stats.LungeTimeScale : 1f;
            int extraDashes = stats != null ? stats.FollowUpCount : 0;
            var result = new List<ComboStepTiming>();
            foreach (MeleeComboStep step in type.ComboSteps)
            {
                float extra = 0f;
                if (step.motion == MeleeStepMotion.Lunge)
                {
                    float lunge = Mathf.Max(0f, step.lungeDuration) * lungeScale;
                    extra = Mathf.Max(0f, step.lungeDuration * (lungeScale - 1f)) + extraDashes * (Mathf.Max(0f, step.hitTime) + lunge);
                }

                result.Add(new ComboStepTiming(step.duration + extra, step.hitTime));
            }

            return result;
        }

        private MeleeWeaponStats ComputeStats()
        {
            if (heldWeapon == null) return null;

            ProgressionModifiers modifiers = PartyProgression.Current != null ? PartyProgression.Current.Modifiers : null;
            Func<float, float> critChance = null;
            Func<float, float> critMultiplier = null;
            if (modifiers != null)
            {
                critChance = x => (float)modifiers.CritChance.Apply(x);
                critMultiplier = x => (float)modifiers.CritMultiplier.Apply(x);
            }

            // 素手で殴っているとき、手の物のエンチャントは乗せない。
            EnchantmentTotals enchantments = held != null && held.Weapon == heldWeapon ? held.EnchantmentTotals() : new EnchantmentTotals();
            // ホットバーのお守り・盾のクリティカル率・ドロップ増加・数・多重を足す（素手でも）。
            if (gear != null) enchantments.AddAll(gear.CurrentBonuses().WeaponTotals);
            float attack = progression != null ? progression.BaseAttack : 0f;
            return heldWeapon.ComputeMeleeStats(enchantments, attack, critChance, critMultiplier);
        }

        private void AttachHeldModel(WeaponDefinition weapon)
        {
            if (heldModel != null) Destroy(heldModel);
            heldModel = null;

            if (weapon == null || weapon.HeldModel == null || animator == null || !animator.isHuman) return;

            Transform hand = animator.GetBoneTransform(weapon.WeaponType != null && weapon.WeaponType.HeldInLeftHand
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand);
            if (hand == null) return;

            heldModel = Instantiate(weapon.HeldModel, hand);
            heldModel.name = weapon.HeldModel.name;
            heldModel.AddComponent<HeldWeaponGrip>().Init(weapon.WeaponType);
            foreach (Collider c in heldModel.GetComponentsInChildren<Collider>()) Destroy(c);
            WeaponAura.Attach(heldModel, weapon.Rank);
        }

        // ---- 振る ----

        private void BeginStep(int step)
        {
            CancelExtraDashes();
            hitThisSwing.Clear();
            criticalSoundThisSwing = false;
            chain.CancelTimeout();
            if (faceAimOnAttack) FaceAim();
            SpawnLeadingSwingEffect(step);
            PlayStepAnimation(step);
        }

        /// <summary>段のモーションを頭から流す。</summary>
        private void PlayStepAnimation(int step)
        {
            if (animator == null) return;
            if (hasComboStepParam) animator.SetInteger(ComboStepParam, step);
            if (hasAttackSpeedParam) animator.SetFloat(AttackSpeedParam, stats != null ? stats.AttackSpeed : 1f);

            // トリガーに任せると、溶け込みの最中に立てたトリガーが残って後から効いたり、1 段しかない武器は自分の段へ戻れず
            // 振り終わりを待ったりして、連打するほど判定より遅れていく。段のステートがあれば直接頭から流す。
            if (TryFindComboState(step, out int layer, out int state)) animator.CrossFadeInFixedTime(state, ComboCrossFade, layer, 0f);
            else if (hasAttackParam) animator.SetTrigger(AttackParam);
        }

        /// <summary>今の武器の段 step のステート（層と、層の名前込みのハッシュ）。Animator に無ければ偽。</summary>
        private bool TryFindComboState(int step, out int layer, out int state)
        {
            layer = -1;
            state = 0;
            int weaponType = heldWeapon != null ? heldWeapon.WeaponType.AnimatorWeaponType : -1;
            if (animator == null || weaponType < 0 || weaponType >= ComboStatePrefixes.Length || ComboStatePrefixes[weaponType] == null)
                return false;

            string name = $"{ComboStatePrefixes[weaponType]} Attack {step + 1}";
            for (int i = 0; i < animator.layerCount; i++)
            {
                int hash = Animator.StringToHash($"{animator.GetLayerName(i)}.{name}");
                if (!animator.HasState(i, hash)) continue;

                layer = i;
                state = hash;
                return true;
            }

            return false;
        }

        private void FaceAim()
        {
            Vector3 forward = AimForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void DealHit(int step)
        {
            if (heldWeapon == null || stats == null || step < 0) return;

            ReleaseLeadingEffect();
            MeleeComboStep shape = heldWeapon.WeaponType.ComboSteps[step];
            switch (shape.motion)
            {
                case MeleeStepMotion.Lunge:
                    dashesLeft = stats.FollowUpCount;
                    BeginLunge(step, shape);
                    break;
                case MeleeStepMotion.Slam:
                    Slam(step, shape);
                    break;
                default:
                    Swing(step, shape);
                    break;
            }
        }

        private void Swing(int step, MeleeComboStep shape)
        {
            PlaySwing(shape);
            int hits = 0;
            HitBox(step, shape, ref hits);
            FinishSwing(step, hits);
        }

        /// <summary>段の判定を出し終えた。コンボボーナスの段数を進めるか途切れさせ、上がったら合図を鳴らす。</summary>
        private void FinishSwing(int step, int hits)
        {
            chain.SwingFinished(hits);
            Swung?.Invoke(step, hits);

            // 段数はこの段を含めて続けて当てた数（数字に添える COMBO と同じ）。上乗せが乗り始める 2 から鳴らし、2 を 1 段目の高さにする。
            int count = ComboCount;
            if (hits > 0 && count >= 2 && GameAudio.Instance != null)
                GameAudio.Instance.PlaySe(SynthSounds.ComboThud(count - 1), comboSoundVolume);
        }

        /// <summary>段の振りのエフェクトと音。当たっても外れても出す。</summary>
        private void PlaySwing(MeleeComboStep shape)
        {
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            Vector3 offset = SwingEffectOffset(shape);
            Vector3 swingPoint = transform.TransformPoint(offset);
            float scale = type.SwingEffectScale * SizeScale;
            // 溜めてから放つ素材は、振り始めに SpawnLeadingSwingEffect で出してあるので、ここでは音だけ。
            bool alreadySpawned = type.SwingEffectLeadTime > 0f;
            // 走る段の斬撃は、走り出しの位置に置き去りにせず体に付けて一緒に走らせる。
            if (!alreadySpawned && shape.motion == MeleeStepMotion.Lunge)
                OneShotEffect.SpawnAttached(type.SwingEffect, transform, offset, Quaternion.Euler(shape.swingEffectEuler), scale);
            else if (!alreadySpawned)
                OneShotEffect.Spawn(type.SwingEffect, swingPoint, transform.rotation * Quaternion.Euler(shape.swingEffectEuler), scale);
            PlaySound(shape.swingSound != null ? shape.swingSound : type.SwingSound, swingPoint, type.SoundVolume);
        }

        /// <summary>
        /// 溜めてから放つ振りのエフェクト（武器種の SwingEffectLeadTime が正）を、段の振り始めに体へ付けて出す。
        /// 溜め（素材の中で SwingEffectLeadTime より前に出る粒）はそのまま流し、放つ粒（それより後にしか出ない粒）は止めておいて、
        /// 段の判定の瞬間（ReleaseLeadingEffect）にこちらから出す。素材の時計に任せると、判定とずれて先に飛んでしまうため。
        /// 放つ粒は世界の座標で動かし、放ったあとは体から離れて自分の速さで飛ぶ。
        /// </summary>
        private void SpawnLeadingSwingEffect(int step)
        {
            pendingRelease.Clear();
            if (heldWeapon == null || step < 0) return;

            WeaponTypeDefinition type = heldWeapon.WeaponType;
            if (type.SwingEffectLeadTime <= 0f || type.SwingEffect == null) return;

            MeleeComboStep shape = type.ComboSteps[step];
            GameObject effect = OneShotEffect.SpawnAttached(type.SwingEffect, transform, SwingEffectOffset(shape),
                Quaternion.Euler(shape.swingEffectEuler), type.SwingEffectScale * SizeScale);
            if (effect == null) return;

            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                if (!EmitsOnlyAfter(particles, type.SwingEffectLeadTime)) continue;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;
                ParticleSystem.MainModule main = particles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                pendingRelease.Add(particles);
            }
        }

        /// <summary>particles が time 秒より後のバーストでしか粒を出さないか（ずっと出し続ける物は偽）。</summary>
        private static bool EmitsOnlyAfter(ParticleSystem particles, float time)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            if (emission.burstCount == 0 || emission.rateOverTime.constantMax > 0f || emission.rateOverDistance.constantMax > 0f) return false;

            for (int i = 0; i < emission.burstCount; i++)
            {
                if (emission.GetBurst(i).time < time - 0.01f) return false;
            }

            return true;
        }

        /// <summary>止めておいた放つ粒を、バーストの数だけ今出す（段の判定の瞬間）。</summary>
        private void ReleaseLeadingEffect()
        {
            foreach (ParticleSystem particles in pendingRelease)
            {
                if (particles == null) continue;

                ParticleSystem.EmissionModule emission = particles.emission;
                int count = 0;
                for (int i = 0; i < emission.burstCount; i++) count += Mathf.RoundToInt(emission.GetBurst(i).count.constantMax);
                particles.Emit(Mathf.Max(1, count));
            }

            pendingRelease.Clear();
        }

        /// <summary>今の位置の段の箱に入った、この振りでまだ当てていない敵に当てる。</summary>
        private void HitBox(int step, MeleeComboStep shape, ref int hits)
        {
            BoxOf(shape, out Vector3 center, out Vector3 halfExtents);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlap, transform.rotation, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !hitThisSwing.Add(enemy)) continue;
                Strike(enemy, overlap[i].ClosestPoint(center), step, shape, ref hits);
            }
        }

        /// <summary>
        /// 段の 1 撃を enemy に当てる。クリティカル・命中のエフェクトと音・爆発のエンチャントまで。
        /// 命中の音は 1 振りの最初の 1 体だけ。playHitSound が偽なら鳴らさない（叩きつけは着弾の瞬間に鳴らしている）。
        /// </summary>
        private void Strike(EnemyHealth enemy, Vector3 point, int step, MeleeComboStep shape, ref int hits, bool playHitSound = true)
        {
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            bool critical = UnityEngine.Random.value < stats.CritChance;
            int comboCount = ComboCount;
            int damage = stats.HitDamage(step, comboCount);
            if (critical) damage = stats.ApplyCritical(damage);

            Vector3 direction = FlatDirection(transform.position, enemy.transform.position, transform.forward);
            int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical,
                shape.knockback + stats.KnockbackBonus, ReactionScale(shape)));
            if (hits == 0 && playHitSound) PlaySound(shape.hitSound != null ? shape.hitSound : type.HitSound, point, type.SoundVolume);
            if (critical && !criticalSoundThisSwing)
            {
                criticalSoundThisSwing = true;
                PlaySound(criticalHitSound, point, criticalHitSoundVolume);
            }

            hits++;

            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            OneShotEffect.Spawn(type.HitEffect, point, facing, type.HitEffectScale);
            if (critical) OneShotEffect.Spawn(criticalHitEffect, point, facing, criticalHitEffectScale);
            if (logHits)
                Debug.Log($"{step + 1} 段目 → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}{(comboCount > 0 ? $"（コンボ {comboCount + 1}）" : string.Empty)}", enemy);
            // 数字に添える COMBO は、この 1 撃を含めて続けて当てた段の数。上乗せが乗っていなければ 0。
            Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.Hit, enemy, step, damage, dealt, critical, point, comboCount > 0 ? comboCount + 1 : 0));

            Explode(point, stats.ExplosionDamage(damage), step);
        }

        private float ReactionScale(MeleeComboStep shape) => stats.ReactionScale * (1f + Mathf.Max(0f, shape.reactionBonus));

        /// <summary>爆発のエンチャント。範囲の敵（当てた敵も含む）に追加のダメージ。爆発からは爆発しない。</summary>
        private void Explode(Vector3 point, int damage, int step)
        {
            if (damage <= 0) return;

            // 半径はサイズの補正込み（MeleeWeaponStats）。見た目も同じだけ大きくする。
            OneShotEffect.Spawn(explosionEffect, point, Quaternion.identity, explosionEffectScale * SizeScale);
            PlaySound(explosionSound, point, explosionSoundVolume);

            explosionTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, stats.ExplosionRadius, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !explosionTargets.Add(enemy)) continue;

                Vector3 direction = enemy.transform.position - point;
                int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction));
                if (logHits) Debug.Log($"爆発 → {enemy.name}: {dealt}", enemy);
                // 爆発の中心ではなく、巻き込んだ敵の体の上で知らせる（数字が 1 か所に重ならないように）。
                Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.Explosion, enemy, step, damage, dealt, false, overlap[i].ClosestPoint(point)));
            }
        }

        // ---- 走る（Lunge） ----

        private void BeginLunge(int step, MeleeComboStep shape)
        {
            EndLunge();
            PlaySwing(shape);

            float duration = Mathf.Max(0.01f, shape.lungeDuration);
            lunge = new Lunge
            {
                Step = step,
                Direction = FlatDirection(Vector3.zero, transform.forward, Vector3.forward),
                Speed = Mathf.Max(0f, shape.lungeDistance) / duration,
                Remaining = duration * stats.LungeTimeScale,
            };

            // 走り出す前に目の前にいる敵にも当てる。
            HitBox(step, shape, ref lunge.Hits);
        }

        private void TickLunge(float dt)
        {
            if (heldWeapon == null || stats == null)
            {
                EndLunge();
                return;
            }

            MeleeComboStep shape = heldWeapon.WeaponType.ComboSteps[lunge.Step];
            bool overrunning = lunge.Remaining <= 0f;
            float time = overrunning ? dt : Mathf.Min(dt, lunge.Remaining);
            lunge.Remaining -= time;
            Vector3 delta = lunge.Direction * (lunge.Speed * time);

            float moved = MoveLunge(delta);
            if (overrunning) lunge.Overrun += moved;
            HitBox(lunge.Step, shape, ref lunge.Hits);

            bool blocked = delta.sqrMagnitude > 1e-8f && moved < delta.magnitude * LungeBlockedRatio;
            bool timeUp = lunge.Remaining <= 0f && (lunge.Overrun >= LungeMaxOverrun || !InsidePassedEnemy());
            if (!timeUp && !blocked) return;

            int step = lunge.Step;
            EndLunge();
            QueueExtraDash(step);
        }

        /// <summary>
        /// 多重で走り足す回数が残っていれば、段の頭からもう一度振る（向き直し・溜めのエフェクト・モーション）。
        /// 段の判定と同じ hitTime だけ溜めたら <see cref="TickExtraDash"/> が走り出す。
        /// </summary>
        private void QueueExtraDash(int step)
        {
            if (dashesLeft <= 0 || heldWeapon == null || stats == null) return;

            dashesLeft--;
            nextDashStep = step;
            nextDashDelay = Mathf.Max(0f, heldWeapon.WeaponType.ComboSteps[step].hitTime) / Mathf.Max(0.1f, stats.AttackSpeed);
            hitThisSwing.Clear();
            criticalSoundThisSwing = false;
            if (faceAimOnAttack) FaceAim();
            SpawnLeadingSwingEffect(step);
            PlayStepAnimation(step);
        }

        private void TickExtraDash(float dt)
        {
            if (nextDashStep < 0) return;

            nextDashDelay -= dt;
            if (nextDashDelay > 0f) return;

            int step = nextDashStep;
            nextDashStep = -1;
            if (heldWeapon == null || stats == null) return;

            ReleaseLeadingEffect();
            BeginLunge(step, heldWeapon.WeaponType.ComboSteps[step]);
        }

        private void CancelExtraDashes()
        {
            dashesLeft = 0;
            nextDashStep = -1;
        }

        /// <summary>体を delta だけ動かし、実際に水平に進めた距離を返す。</summary>
        private float MoveLunge(Vector3 delta)
        {
            bool passThrough = heldWeapon.WeaponType.LungePassesThroughEnemies;
            Vector3 before = transform.position;

            if (characterController != null && characterController.enabled)
            {
                if (passThrough) IgnoreEnemiesAhead(delta);
                characterController.Move(delta);
            }
            else
            {
                // CharacterController の無い持ち主（仲間の AI など）。壁の手前までだけ動かす。
                float distance = delta.magnitude;
                if (distance > 1e-6f && FindWall(before + Vector3.up * 0.5f, delta / distance, distance + 0.3f, passThrough, out float wall))
                    distance = Mathf.Max(0f, wall - 0.3f);
                transform.position = before + delta.normalized * distance;
            }

            Vector3 moved = transform.position - before;
            moved.y = 0f;
            return moved.magnitude;
        }

        /// <summary>進む先のカプセルに入る敵とは、走り終えるまでぶつからないようにする。</summary>
        private void IgnoreEnemiesAhead(Vector3 delta)
        {
            CharacterController cc = characterController;
            float radius = cc.radius + cc.skinWidth + 0.1f;
            Vector3 center = transform.TransformPoint(cc.center) + delta;
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            Vector3 bottom = center - Vector3.up * half;
            Vector3 top = center + Vector3.up * half;

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlap[i];
                if (other == cc || lungeIgnored.Contains(other) || other.GetComponentInParent<EnemyHealth>() == null) continue;

                Physics.IgnoreCollision(cc, other, true);
                lungeIgnored.Add(other);
            }
        }

        /// <summary>すり抜けている最中の敵に、今の体が重なっているか。</summary>
        private bool InsidePassedEnemy()
        {
            CharacterController cc = characterController;
            if (cc == null || lungeIgnored.Count == 0) return false;

            float radius = cc.radius + cc.skinWidth;
            Vector3 center = transform.TransformPoint(cc.center);
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * half, center + Vector3.up * half, radius, overlap, hitMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (lungeIgnored.Contains(overlap[i])) return true;
            }

            return false;
        }

        private bool FindWall(Vector3 origin, Vector3 direction, float distance, bool skipEnemies, out float wall)
        {
            wall = distance;
            bool found = false;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.3f, direction, distance, hitMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (skipEnemies && hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;
                if (hit.distance < wall) wall = hit.distance;
                found = true;
            }

            return found;
        }

        private void EndLunge()
        {
            if (characterController != null)
            {
                foreach (Collider other in lungeIgnored)
                {
                    if (other != null) Physics.IgnoreCollision(characterController, other, false);
                }
            }

            lungeIgnored.Clear();
            if (lunge == null) return;

            Lunge finished = lunge;
            lunge = null;
            FinishSwing(finished.Step, finished.Hits);
        }

        // ---- 叩きつける（Slam） ----

        private void Slam(int step, MeleeComboStep shape)
        {
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            PlaySwing(shape);

            // 叩きつけの音（武器種の命中の音）は、当たっても外れても着弾の瞬間に 1 回だけ鳴らす。揺れも 1 回。
            Vector3 front = SlamCenter(shape, 0f);
            ImpactShake.At(front, type.SlamShake);
            PlaySound(shape.hitSound != null ? shape.hitSound : type.HitSound, front, type.SoundVolume);

            // 数のエンチャントで増えた分も本撃と合わせて、持ち主を中心に前から左右対称に回して並べる。
            // 叩きつけごとに別の判定なので、重なった所にいる敵は重なった数だけ当たる。
            int hits = 0;
            float radius = Mathf.Max(0f, shape.slamRadius) * stats.HitboxScale;
            foreach (float angle in SlamPattern.SpreadAngles(stats.ExtraSlamCount + 1, type.SlamSpreadAngle))
            {
                Vector3 center = SlamCenter(shape, angle);
                SpawnLayers(type.SlamEffects, center, transform.rotation * Quaternion.AngleAxis(angle, Vector3.up), SizeScale);
                foreach ((EnemyHealth enemy, Vector3 point) in EnemiesInCircle(center, radius, shape.slamHeight))
                    Strike(enemy, point, step, shape, ref hits, false);
            }

            // 追撃は叩きつけ（数で増えた分も）ごとに、その向きへずらして落とす。ダメージは本撃と同じ（クリティカルは落ちたときに引く）。
            int damage = stats.HitDamage(step, ComboCount);
            float[] angles = SlamPattern.SpreadAngles(stats.ExtraSlamCount + 1, type.SlamSpreadAngle);
            foreach (SlamFollowUp plan in SlamPattern.FollowUps(stats.FollowUpCount, type.FollowUpSpacing, type.FollowUpInterval))
            {
                for (int a = 0; a < angles.Length; a++)
                {
                    Vector3 forward = Quaternion.AngleAxis(angles[a], Vector3.up) * transform.forward;
                    followUps.Add(new FollowUp
                    {
                        Step = step,
                        Type = type,
                        Shape = shape,
                        Center = SlamCenter(shape, angles[a]) + forward * plan.ForwardOffset,
                        Forward = forward,
                        Delay = plan.Delay,
                        Damage = damage,
                        ReactionScale = ReactionScale(shape),
                        Loud = a == angles.Length / 2,
                    });
                }
            }

            FinishSwing(step, hits);
        }

        /// <summary>
        /// 着弾点。hitboxCenter のキャラから見た位置（前への距離はサイズで伸ばす）を、持ち主を中心に angle 度（右が正）回した所。
        /// </summary>
        private Vector3 SlamCenter(MeleeComboStep shape, float angle)
        {
            Vector3 local = shape.hitboxCenter;
            local.z *= stats != null ? stats.HitboxScale : 1f;
            return transform.TransformPoint(Quaternion.AngleAxis(angle, Vector3.up) * local);
        }

        /// <summary>center から水平に radius 以内、上下 height 以内にいる生きた敵（1 体 1 回）と、当たった場所。</summary>
        private List<(EnemyHealth, Vector3)> EnemiesInCircle(Vector3 center, float radius, float height)
        {
            var result = new List<(EnemyHealth, Vector3)>();
            if (radius <= 0f) return result;

            explosionTargets.Clear();
            float reach = Mathf.Max(radius, height);
            int count = Physics.OverlapSphereNonAlloc(center, reach + height, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 point = overlap[i].ClosestPoint(center);
                Vector3 flat = point - center;
                float up = flat.y;
                flat.y = 0f;
                if (flat.magnitude > radius || Mathf.Abs(up) > Mathf.Max(0f, height)) continue;
                if (explosionTargets.Add(enemy)) result.Add((enemy, point));
            }

            explosionTargets.Clear();
            return result;
        }

        private void TickFollowUps(float dt)
        {
            for (int i = followUps.Count - 1; i >= 0; i--)
            {
                FollowUp f = followUps[i];
                f.Delay -= dt;
                if (f.Delay > 0f) continue;

                followUps.RemoveAt(i);
                SpawnLayers(f.Type.SlamEffects, f.Center, Quaternion.LookRotation(f.Forward, Vector3.up), f.Type.FollowUpEffectScale * SizeScale);
                if (f.Loud)
                {
                    ImpactShake.At(f.Center, f.Type.SlamShake * f.Type.FollowUpShakeRatio);
                    PlaySound(f.Shape.hitSound != null ? f.Shape.hitSound : f.Type.HitSound, f.Center, f.Type.SoundVolume);
                }

                float radius = Mathf.Max(0f, f.Shape.slamRadius) * (stats != null ? stats.HitboxScale : 1f);
                foreach ((EnemyHealth enemy, Vector3 point) in EnemiesInCircle(f.Center, radius, f.Shape.slamHeight))
                {
                    bool critical = stats != null && UnityEngine.Random.value < stats.CritChance;
                    int damage = critical ? stats.ApplyCritical(f.Damage) : f.Damage;
                    float knockback = f.Shape.knockback + (stats != null ? stats.KnockbackBonus : 0f);
                    Vector3 direction = FlatDirection(f.Center, enemy.transform.position, f.Forward);
                    int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical, knockback, f.ReactionScale));
                    OneShotEffect.Spawn(f.Type.HitEffect, point, Quaternion.LookRotation(direction, Vector3.up), f.Type.HitEffectScale);
                    if (critical) OneShotEffect.Spawn(criticalHitEffect, point, Quaternion.LookRotation(direction, Vector3.up), criticalHitEffectScale);
                    if (logHits) Debug.Log($"追撃 → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}", enemy);
                    Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.FollowUp, enemy, f.Step, damage, dealt, critical, point));
                }
            }
        }

        // ---- 共通 ----

        /// <summary>重ねのエフェクトを全部、position に rotation の向きで出す。scale は重ね全体に掛ける倍率。</summary>
        private static void SpawnLayers(IReadOnlyList<EffectLayer> layers, Vector3 position, Quaternion rotation, float scale)
        {
            foreach (EffectLayer layer in layers)
            {
                if (layer.prefab == null) continue;
                OneShotEffect.Spawn(layer.prefab, position + rotation * layer.offset, rotation, Mathf.Max(0.01f, layer.scale) * scale);
            }
        }

        private static Vector3 FlatDirection(Vector3 from, Vector3 to, Vector3 fallback)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = fallback;
                direction.y = 0f;
            }

            return direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized;
        }

        private static void PlaySound(AudioClip clip, Vector3 position, float volume)
        {
            if (clip != null) GameAudio.Instance?.PlaySeAt(clip, position, volume);
        }

        /// <summary>サイズのエンチャントの倍率。判定に合わせるエフェクトにも掛ける。</summary>
        private float SizeScale => stats != null ? stats.HitboxScale : 1f;

        /// <summary>振りのエフェクトを出す位置（キャラから見たローカル）。判定の箱と同じく、前への距離をサイズで伸ばす。</summary>
        private Vector3 SwingEffectOffset(MeleeComboStep shape)
        {
            Vector3 local = shape.swingEffectOffset;
            local.z *= SizeScale;
            return local;
        }

        /// <summary>段の判定の箱（ワールド）。サイズのエンチャントで大きさと前への伸びが増える。</summary>
        private void BoxOf(MeleeComboStep shape, out Vector3 center, out Vector3 halfExtents)
        {
            float scale = stats != null ? stats.HitboxScale : 1f;
            Vector3 local = shape.hitboxCenter;
            local.z *= scale;
            center = transform.TransformPoint(local);
            halfExtents = shape.hitboxSize * (0.5f * scale);
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null) return;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == WeaponTypeParam) hasWeaponTypeParam = true;
                else if (p.name == AttackParam) hasAttackParam = true;
                else if (p.name == ComboStepParam) hasComboStepParam = true;
                else if (p.name == AttackSpeedParam) hasAttackSpeedParam = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (heldWeapon == null) return;

            IReadOnlyList<MeleeComboStep> steps = heldWeapon.WeaponType.ComboSteps;
            int step = combo != null && combo.IsSwinging ? combo.Step : 0;
            if (step >= steps.Count) return;

            MeleeComboStep shape = steps[step];
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            if (shape.motion == MeleeStepMotion.Slam)
            {
                float radius = shape.slamRadius * (stats != null ? stats.HitboxScale : 1f);
                int count = (stats != null ? stats.ExtraSlamCount : 0) + 1;
                foreach (float angle in SlamPattern.SpreadAngles(count, heldWeapon.WeaponType.SlamSpreadAngle))
                    Gizmos.DrawWireSphere(SlamCenter(shape, angle), radius);
                return;
            }

            BoxOf(shape, out Vector3 center, out Vector3 halfExtents);
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
