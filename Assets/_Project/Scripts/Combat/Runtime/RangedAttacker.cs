using System;
using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 持たされた遠距離武器（弓・持続弓・杖）で撃つ実行役。入力は持たず、外から <see cref="Equip"/>（持ち替え）・<see cref="PressAttack"/>（攻撃の押下）・
    /// <see cref="AttackHeld"/>（押している間。炎だけが読む）・<see cref="Aim"/>（狙いの線。プレイヤーならカメラの中心）を渡してもらう。プレイヤーは PlayerMeleeInput が叩く。
    /// 押した瞬間に、待ちが明けていれば撃つ（押しっぱなしでは撃たない。炎は押している間ずっと吐く）。撃つ間隔は武器種の fireInterval を速射で縮めたもの。
    ///   弓     … 狙いの線が当たった所（無ければ射程の先）へ、弓を持つ手から矢を放つ。数で増えた矢は照準の右左へ交互に開き（1 本は必ず照準へ）、
    ///            多重で遅れて同じ向きへもう一斉射。爆発は敵に当たっても、壁や床に刺さっても起きる
    ///   持続弓 … 狙いの線が当たった地面（射程の水平距離まで）へ、上に矢を放ってから雨を降らせ、範囲に刻みでダメージ。
    ///            数で増えた雨は狙った所の付近のランダムな所に、多重で同じ所にもう一度降る。持っている間は狙う地面に範囲の円を出す
    ///   雷     … 照準の線に近い敵（決めた角度の中で照準にいちばん近く、杖の先から見通せる敵）へ杖の先から雷を放ち、
    ///            当てた敵から近くのまだ当てていない敵へ次々と飛び移る。数で飛び移る回数が増え、多重で遅れてもう一度放つ。敵が居なければ照準の先へ空撃ち
    ///   連置   … 照準の水平の向きへ、足元の少し先から地面に沿って範囲に当たる結晶を手前から順に走らせる（1 列は同じ敵に 1 回）。壁で止まる。
    ///            数で扇状に列が増え、持続時間で列が伸び、多重で同じ向きへもう一列
    ///   投擲   … 弓と同じく照準の先へまっすぐ飛ばす。飛ぶのは手に持った武器の見た目そのもので、武器ごとの速さで縦に回る。
    ///            腕を振り切る瞬間に放ち、放してから次が投げられるまで手を空にする
    ///   召喚   … 腕を振り切る瞬間に、狙いの線が当たった地面へ置物（おとり）を呼び出す。数で狙った所の周りのランダムな所に増え、持続時間で長く居る。
    ///            呼び直すと前の分は消える（持ち替えても残り、置いてから別の武器で戦える）。攻撃はしない
    ///   治癒持続 … 腕を振り切る瞬間に、狙いの線が当たった地面へ種を放物線で投げ、落ちた所に治癒の場を張って中の味方を刻みで回復する。
    ///            数で付近に場が増え、多重で同じ所にもう一度張り、サイズで広がる
    ///   ダメージ軽減 … 腕を振り切る瞬間に、近くの仲間（自分も含む）のうちその杖の種類（被ダメージ軽減・クリティカル倍率・状態異常耐性）の加護が
    ///            付いていない人から 3 ＋ 数 人をランダムに選び、その色の膜を張る。足りなければ残りの短い人を掛け直す。多重で選び直してもう一度。狙いは使わない
    ///   治癒   … 同じく、近くの仲間から体力の割合の低い順（同じなら体力の少ない順）に 3 ＋ 数 人を選んですぐ回復する。多重で選び直してもう一度
    ///   炎     … 押している間、杖の先から照準へ炎を吐き、刻みごとに炎の円錐の中の敵にダメージ。決めた時間吐いたか離したら止まり、撃つ間隔 × 吐いた割合 だけ待つ。
    ///            サイズで太く、弾速で遠くまで届き、数で炎の筋が扇状に増え、ホーミングで筋が近くの敵へ曲がる
    /// 遠距離の武器を持っていて狙いの線がある間は、毎フレーム（Animator の後で）体ごと狙いの方へ回し（狙わずに仲間へ掛ける支援の杖は回さず、近接と同じく歩く向きのまま）、背骨を曲げて弓を持つ腕を狙いへ向ける
    /// （持続弓は空へ向けて反らせる。杖・投擲・召喚・治癒持続・ダメージ軽減・治癒は腕ではなく体の前を向け、背骨は曲げない）。歩く向きへ回す ThirdPersonController より後に上書きする。
    /// 杖は手の武器の見た目の子の "Tip"（杖の先）から放つ。無ければ持つ手から。
    /// ダメージ・クリティカル・爆発・命中の見た目と音は近接（MeleeAttacker）と同じ作り。当てた 1 回ごとに <see cref="Dealt"/> で知らせる。
    /// 手の見た目と Animator の WeaponType は MeleeAttacker が持ち替えで替えるので、ここは撃つことだけを受け持つ。
    /// キャラのルート（MeleeAttacker と同じ GameObject）に付ける。基礎攻撃力は同じ GameObject の CharacterProgression から読む。
    /// </summary>
    // 体の向きを ThirdPersonController の LateUpdate（カメラの目標を回す）より先に決め、カメラが揺れないようにする。押下は PlayerMeleeInput（-10）の後で使う。
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Ranged Attacker")]
    public sealed class RangedAttacker : MonoBehaviour
    {
        // CharacterAnimatorBuilder（エディタ専用アセンブリ）が焼き込んだ値。向こうを変えたらここも揃えること。
        private const string AttackParam = "Attack";
        private const string AttackSpeedParam = "AttackSpeed";
        private const string UpperBodyLayer = "UpperBody";
        private const string CastingParam = "Casting";
        private static readonly int BowDrawState = Animator.StringToHash("Bow Draw");

        /// <summary>炎を止めてから、出ている炎の粒が消えるまで見た目を残す秒数。</summary>
        private const float FlameLingerTime = 1.2f;

        /// <summary>
        /// 弓のモーションの 1 周（Bow Release で放してから、つがえ直して Bow Draw で引き切るまで。BowShot の 13〜29F と 0〜7F、30fps）。
        /// 撃つ間隔がこれより短いと置いていかれるので、その分だけ速めて流す。
        /// </summary>
        private const float BowCycleSeconds = (16f + 7f) / 30f;

        /// <summary>
        /// 投げのモーションの 1 周（Kevin の Attack1H01_R、1.1 秒を CharacterAnimatorBuilder が 1.5 倍で流し、0.85 で構えへ戻り始める）。
        /// 撃つ間隔がこれより短いと置いていかれるので、その分だけ速めて流す。放す瞬間（castDelay）も同じだけ早める。
        /// </summary>
        private const float ThrowCycleSeconds = 1.1f * 0.85f / 1.5f;

        /// <summary>持続弓で上に放つ見た目の矢の速さ（m/s）と、飛ぶ距離（m）。</summary>
        private const float UpShotSpeed = 40f;
        private const float UpShotRange = 14f;

        /// <summary>雨の 1 刻みの命中の音の大きさ（武器種の音量に対して）。刻みは何度も来るので控えめに。</summary>
        private const float TickHitSoundRatio = 0.4f;

        /// <summary>雨の刻みごとに鳴らす降る音の大きさ（降り始めの音に対して）。</summary>
        private const float RainTickSoundRatio = 0.5f;

        /// <summary>雨の 1 刻みの命中の見た目の大きさ（武器種の命中の見た目に対して）。</summary>
        private const float TickHitEffectRatio = 0.5f;

        /// <summary>治癒の場の刻みごとの音の大きさ（張った瞬間に対する割合）。</summary>
        private const float HealTickSoundRatio = 0.4f;

        /// <summary>ダメージ軽減・治癒で掛けた相手に出す見た目を見せる秒数と、そのあと薄くして消す秒数。</summary>
        private const float SupportHitHold = 0.8f;
        private const float SupportHitFade = 0.8f;

        /// <summary>召喚の照準の円の半径（置物を置く範囲に足す余白、m）。</summary>
        private const float SummonRingPadding = 0.6f;

        /// <summary>数で増えた置物の置き場所を、ほかの置物から離れた所が見つかるまで引き直す回数。</summary>
        private const int SummonPlaceAttempts = 12;

        [SerializeField, Tooltip("キャラの Animator。未設定なら子から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("矢と照準が当たるレイヤー。")]
        private LayerMask hitMask = ~0;

        [SerializeField, Min(0f), Tooltip("持ち替えてから撃てるまでの秒数。全武器共通（MeleeAttacker の switchCooldown と揃える）。")]
        private float switchCooldown = 0.5f;

        [SerializeField, Min(0f), Tooltip("遠距離の武器に持ち替えたとき、体を狙いの方へ回す速さ（度/秒）。向き切ったあとは毎フレーム狙いに合わせ続ける。")]
        private float aimTurnSpeed = 900f;

        [SerializeField, Range(0f, 89f), Tooltip("狙いの上下に合わせて背骨を曲げる角度の上限（度）。")]
        private float maxAimPitch = 60f;

        [SerializeField, Tooltip("クリティカルのとき、命中のエフェクトに重ねて出す見た目（任意）。")]
        private GameObject criticalHitEffect;

        [SerializeField, Min(0.01f)]
        private float criticalHitEffectScale = 1f;

        [SerializeField, Tooltip("クリティカルのとき、命中の音に重ねて鳴らす音（任意、1 フレームに 1 回）。")]
        private AudioClip criticalHitSound;

        [SerializeField, Range(0f, 1f)]
        private float criticalHitSoundVolume = 0.7f;

        [SerializeField, Tooltip("爆発のエンチャントで出す見た目（任意）。")]
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
        // 手の武器の見た目を持っている役（投擲で手を空にするのに使う）。
        private MeleeAttacker melee;
        // 投擲で手の武器を消している間は真。待ちが明けて投げ残りも無くなったら出し直す。
        private bool handEmptied;
        private bool initialized;
        private bool equipped;
        private ItemInstance held;
        private WeaponDefinition heldWeapon;
        private RangedWeaponStats stats;
        private float cooldownRemaining;
        private float cooldownDuration;
        private bool pressQueued;
        private RangeRing aimRing;

        // 持ち替えてから体が狙いの方へ向き切ったか。向き切るまでは aimTurnSpeed で回し、そのあとは歩く向きへ回されても毎フレーム狙いへ戻す。
        private bool aimLocked;

        // 同じフレームに何本当たっても、命中・クリティカルの音は 1 回だけ鳴らす。
        private int hitSoundFrame = -1;
        private int stickSoundFrame = -1;
        private int rainSoundFrame = -1;

        // 弓のモーションが引き絞り（Bow Draw）に入った瞬間に引き絞る音を鳴らすため、前のフレームに引き絞っていたかを覚える。
        private int upperBodyLayer = -1;
        private bool wasDrawing;
        private int criticalSoundFrame = -1;

        private readonly List<PendingVolley> volleys = new List<PendingVolley>();
        private readonly List<Scheduled> scheduled = new List<Scheduled>();
        private readonly HashSet<EnemyHealth> explosionTargets = new HashSet<EnemyHealth>();
        private readonly Collider[] overlap = new Collider[32];
        // 雷の狙いと炎は遠くまで探すので、床や壁の当たりも多く拾う。
        private readonly Collider[] wideOverlap = new Collider[256];
        private readonly RaycastHit[] rayHits = new RaycastHit[32];
        private readonly System.Random random = new System.Random();

        // 背骨（下から順に曲げを分ける）と、弓を持つ腕の付け根・手。人型でなければ空・null。
        private Transform[] spine = Array.Empty<Transform>();
        private Transform leftUpperArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightHand;

        private bool hasAttackParam;
        private bool hasAttackSpeedParam;
        private bool hasCastingParam;

        // 杖の先。持ち替えで見た目が作り直されるので、持ち替えたら探し直す。
        private Transform staffTip;
        private bool staffTipSearched;

        // 炎を吐いている間の状態。
        private bool burning;
        private RangedWeaponStats burnStats;
        private int ticksLeft;
        private float burnElapsed;
        private float flameTickTimer;
        private float flameSoundTimer;
        private readonly List<FlameJet> jets = new List<FlameJet>();

        // 召喚で今居る置物。呼び直したら全部消す。持ち替えても消さない。
        private readonly List<SummonedDecoy> activeSummons = new List<SummonedDecoy>();
        private readonly HashSet<PlayerHealth> healTargets = new HashSet<PlayerHealth>();
        private readonly List<PlayerHealth> supportPool = new List<PlayerHealth>();
        private readonly List<SupportCandidate> supportCandidates = new List<SupportCandidate>();
        private readonly List<int> supportPicks = new List<int>();
        private readonly System.Random supportRandom = new System.Random();
        private int healSoundFrame = -1;

        /// <summary>遅れて起こすこと 1 つ（雷の飛び移り・多重の雷・連置の 1 つずつ）。</summary>
        private sealed class Scheduled
        {
            public float Delay;
            public Action Run;
        }

        /// <summary>吐いている炎の筋 1 本。Angle は照準からの水平の角度、Direction は今の向き。</summary>
        private sealed class FlameJet
        {
            public float Angle;
            public Vector3 Direction;
            public GameObject Effect;
        }

        /// <summary>多重で遅れて放つ一斉射 1 回。</summary>
        private sealed class PendingVolley
        {
            public float Delay;
            public Vector3 Direction;
            public WeaponTypeDefinition Type;
            public RangedWeaponStats Stats;
        }

        /// <summary>今持っている遠距離武器。遠距離武器でなければ null。</summary>
        public WeaponDefinition HeldWeapon => heldWeapon;

        /// <summary>今持っている遠距離武器の数値（持ち替えか撃った時点のもの）。持っていなければ null。</summary>
        public RangedWeaponStats Stats => stats;

        /// <summary>
        /// 次に撃てるまでの待ちの残りの割合（1 で待ち始め、0 で撃てる）。
        /// 炎を吐いている間は、吐ける量のうち使った割合（0 で吐き始め、1 で吐き切る）。吐き切ればそのまま 1 から待ちが減っていき、
        /// 途中で離せば使った割合の所から減っていくので、ホットバーの暗幕が途切れずに伸びて縮む。
        /// </summary>
        public float CooldownFraction => burning ? FlameUsedFraction
            : cooldownDuration > 0f && cooldownRemaining > 0f ? Mathf.Clamp01(cooldownRemaining / cooldownDuration) : 0f;

        /// <summary>次に撃てるまでの待ちの残り（秒）。炎を吐いている間は 0。</summary>
        public float CooldownRemaining => burning ? 0f : cooldownRemaining;

        /// <summary>持続弓・召喚・治癒持続を持っている間、狙う地面に範囲の円を出すか。仲間の AI では出さない。</summary>
        public bool ShowAimRing { get; set; } = true;

        /// <summary>今吐いている炎の、吐ける量のうち使った割合。吐いていなければ 0。</summary>
        private float FlameUsedFraction
        {
            get
            {
                if (!burning || burnStats == null) return 0f;
                float length = burnStats.FlameTickCount * burnStats.FlameTickInterval;
                return length > 0f ? Mathf.Clamp01(burnElapsed / length) : 1f;
            }
        }

        /// <summary>狙いの線（プレイヤーならカメラの中心）。null なら体の前へ撃ち、持続弓の照準の円も出さない。</summary>
        public Ray? Aim { get; set; }

        /// <summary>攻撃を押し続けているか。炎はこれが真の間吐き続ける（押した瞬間は <see cref="PressAttack"/> でも始まる）。</summary>
        public bool AttackHeld { get; set; }

        /// <summary>炎を吐いているか。</summary>
        public bool IsBurning => burning;

        /// <summary>敵に当てた 1 回ごと（矢・爆発・雨の刻み・雷・連置・炎の刻み）。ダメージ表示や試験の窓が読む。</summary>
        public event Action<MeleeHitRecord> Dealt;

        /// <summary>召喚で今居る置物（消え始めた物は除く）。</summary>
        public IReadOnlyList<SummonedDecoy> ActiveSummons => activeSummons;

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
            // パーティーの味方（自分も含む）には矢を刺さず、照準も味方で止めない。
            hitMask = AllyLayer.Exclude(hitMask);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            progression = GetComponent<CharacterProgression>();
            gear = GetComponent<PlayerGear>();
            melee = GetComponent<MeleeAttacker>();
            CacheAnimatorParameters();
            CacheBones();
        }

        private void OnDisable()
        {
            volleys.Clear();
            scheduled.Clear();
            StopFlame(false);
            if (handEmptied) SetHandEmpty(false);
            pressQueued = false;
            AttackHeld = false;
            if (aimRing != null) aimRing.SetVisible(false);
        }

        private void OnDestroy()
        {
            if (aimRing != null) Destroy(aimRing.gameObject);
            // シーンを閉じるときに消える見た目を出さないよう、置物はそのまま消す。
            foreach (SummonedDecoy decoy in activeSummons)
                if (decoy != null) Destroy(decoy.gameObject);
            activeSummons.Clear();
        }

        /// <summary>攻撃を 1 回押す。次の Update で、待ちが明けていれば撃つ。</summary>
        public void PressAttack() => pressQueued = true;

        /// <summary>
        /// item を手に持つ（null なら素手）。遠距離武器でなければ何も撃たない。switched が真なら、中身が同じでも持ち替えとして待たせる。
        /// 最初の 1 回は待たせない。inheritCooldown が真なら前の武器の待ちが長ければ引き継ぎ、往復で消させない。
        /// 仲間の AI は武器ごとの待ちを自分で覚えているので、偽にして持ち替えの待ちだけにする。
        /// </summary>
        public void Equip(ItemInstance item, bool switched = false, bool inheritCooldown = true)
        {
            if (equipped && item == held && !switched) return;

            Initialize();
            float switchWait = equipped ? Mathf.Max(switchCooldown, inheritCooldown ? cooldownRemaining : 0f) : 0f;
            bool wasRanged = heldWeapon != null;
            equipped = true;
            volleys.Clear();
            scheduled.Clear();
            StopFlame(false);
            // 手の見た目は MeleeAttacker が持ち替えで作り直すので、消していた印だけ戻す。
            handEmptied = false;
            staffTip = null;
            staffTipSearched = false;

            held = item;
            WeaponDefinition weapon = item?.Weapon;
            heldWeapon = weapon != null && weapon.WeaponType != null && weapon.WeaponType.IsRanged ? weapon : null;
            stats = heldWeapon != null ? ComputeStats() : null;
            cooldownRemaining = cooldownDuration = switchWait;
            aimLocked = false;

            // 弓で立てたまま残った Attack が、持ち替え先の攻撃で後から効かないように。
            if (wasRanged && animator != null && hasAttackParam) animator.ResetTrigger(AttackParam);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
            TickVolleys(dt);
            TickScheduled(dt);
            if (handEmptied && cooldownRemaining <= 0f && volleys.Count == 0 && scheduled.Count == 0) SetHandEmpty(false);

            bool pressed = pressQueued;
            pressQueued = false;
            if (heldWeapon == null) return;

            if (heldWeapon.WeaponType.RangedKind == RangedAttackKind.Flame) UpdateFlame(dt, pressed || AttackHeld);
            else if (pressed && cooldownRemaining <= 0f) Fire();
        }

        private void LateUpdate()
        {
            WeaponTypeDefinition type = heldWeapon != null ? heldWeapon.WeaponType : null;
            UpdateDrawSound(type);
            if (type == null || !Aim.HasValue || !type.AimsWithCamera)
            {
                if (aimRing != null) aimRing.SetVisible(false);
                return;
            }

            Vector3 target = type.AimsAtGround ? GroundTarget(type) : AimPoint(type);
            float remaining = AimBody(type, target, aimLocked ? 180f : aimTurnSpeed * Time.deltaTime, true);
            if (Mathf.Abs(remaining) < 1f) aimLocked = true;
            UpdateAimRing(type, target);
            if (burning) UpdateJets(type, target, Time.deltaTime);
        }

        private RangedWeaponStats ComputeStats()
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

            // 加護（血走りの杖の膜）は、永続アップグレードの補正の後のクリティカル倍率に (1 ＋ 上げ幅) を掛ける。加護は掛けられたときに後から付く。
            float blessingCrit = TryGetComponent(out CharacterBuffs buffs) ? buffs.CritMultiplierBonus : 0f;
            if (blessingCrit > 0f)
            {
                Func<float, float> inner = critMultiplier;
                critMultiplier = x => (inner != null ? inner(x) : x) * (1f + blessingCrit);
            }

            EnchantmentTotals enchantments = held != null ? held.EnchantmentTotals() : new EnchantmentTotals();
            // ホットバーのお守り・盾のクリティカル率・ドロップ増加・数・多重を足す（素手でも）。
            if (gear != null) enchantments.AddAll(gear.CurrentBonuses().WeaponTotals);
            float attack = progression != null ? progression.BaseAttack : 0f;
            return heldWeapon.ComputeRangedStats(enchantments, attack, critChance, critMultiplier);
        }

        // ---- 撃つ ----

        private void Fire()
        {
            stats = ComputeStats();
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            cooldownRemaining = cooldownDuration = stats.FireInterval;

            Vector3 target = type.AimsAtGround ? GroundTarget(type) : AimPoint(type);
            // 撃つ瞬間は向き切る。背骨の曲げは前のフレームの LateUpdate のものが手に残っている。狙わない支援は向きを変えない。
            if (type.AimsWithCamera) AimBody(type, target, 360f, false);
            PlayFireAnimation();

            Vector3 muzzle = Muzzle(type);
            RangedWeaponStats shot = stats;
            switch (type.RangedKind)
            {
                case RangedAttackKind.Rain:
                    PlaySound(type.SwingSound, muzzle, type.SoundVolume);
                    FireRain(type, shot, muzzle, target);
                    break;
                case RangedAttackKind.Throw:
                    // 腕を振り切る瞬間に、その瞬間の照準へ投げる。速射でモーションを速めた分だけ早く放す。持ち替えたら Equip が予定ごと消す。
                    Schedule(type.CastDelay / ThrowAnimationSpeed(), () =>
                    {
                        Vector3 hand = Muzzle(type);
                        PlaySound(type.SwingSound, hand, type.SoundVolume);
                        FireBow(type, shot, hand, AimPoint(type));
                    });
                    break;
                case RangedAttackKind.Summon:
                    // 腕を振り切る瞬間に、その瞬間の狙った地面へ呼び出す。持ち替えたら Equip が予定ごと消す（呼び出した後の置物は残る）。
                    Schedule(type.CastDelay / ThrowAnimationSpeed(), () =>
                    {
                        Vector3 ground = GroundTarget(type);
                        PlaySound(type.SwingSound, ground, type.SoundVolume);
                        FireSummon(type, shot, ground);
                    });
                    break;
                case RangedAttackKind.HealField:
                    Schedule(type.CastDelay / ThrowAnimationSpeed(), () =>
                    {
                        Vector3 hand = Muzzle(type);
                        PlaySound(type.SwingSound, hand, type.SoundVolume);
                        FireHealField(type, shot, hand, GroundTarget(type));
                    });
                    break;
                case RangedAttackKind.Buff:
                case RangedAttackKind.Heal:
                    // 狙いは使わず、腕を振り切る瞬間に近くの仲間を選んで掛ける。多重は選び直してもう一度。持ち替えたら Equip が予定ごと消す。
                    Schedule(type.CastDelay / ThrowAnimationSpeed(), () =>
                    {
                        PlaySound(type.SwingSound, Muzzle(type), type.SoundVolume);
                        foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.SupportRepeatInterval))
                        {
                            if (delay <= 0f) CastSupport(type, shot);
                            else Schedule(delay, () => CastSupport(type, shot));
                        }
                    });
                    break;
                case RangedAttackKind.Chain:
                case RangedAttackKind.Line:
                    // 杖は撃ち出しのモーションで突き出す瞬間に放つ。持ち替えたら Equip が予定ごと消す。
                    Schedule(type.CastDelay, () =>
                    {
                        PlaySound(type.SwingSound, Muzzle(type), type.SoundVolume);
                        if (type.RangedKind == RangedAttackKind.Chain) FireChain(type, shot);
                        else FireLine(type, shot);
                    });
                    break;
                default:
                    PlaySound(type.SwingSound, muzzle, type.SoundVolume);
                    FireBow(type, shot, muzzle, target);
                    break;
            }
        }

        private void FireBow(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 muzzle, Vector3 target)
        {
            Vector3 direction = target - muzzle;
            Vector3 fallback = Aim.HasValue ? Aim.Value.direction : transform.forward;
            // 狙いが筒先より手前（体に重なる壁など）なら、狙いの線の向きへそのまま撃つ。
            if (direction.sqrMagnitude < 0.25f || Vector3.Dot(direction, fallback) <= 0f) direction = fallback;
            direction.Normalize();

            foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.MultishotInterval))
            {
                if (delay <= 0f) Volley(type, shot, direction, false);
                else volleys.Add(new PendingVolley { Delay = delay, Direction = direction, Type = type, Stats = shot });
            }
        }

        /// <summary>一斉射 1 回。本撃は direction へ、数で増えた矢はその右左へ交互に開いて放つ。</summary>
        private void Volley(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 direction, bool playSound)
        {
            Vector3 muzzle = Muzzle(type);
            if (playSound) PlaySound(type.SwingSound, muzzle, type.SoundVolume);

            // 投擲は手に持った武器の見た目を飛ばし、手からは消す。
            bool thrown = type.IsThrow && heldWeapon != null && heldWeapon.HeldModel != null;
            GameObject visual = thrown ? heldWeapon.HeldModel : type.ProjectilePrefab;
            if (type.IsThrow) SetHandEmpty(true);

            var settings = new ArrowProjectile.Settings
            {
                Speed = type.ProjectileSpeed * shot.ProjectileSpeedScale,
                Range = type.ProjectileRange,
                Radius = type.ProjectileRadius,
                Pierce = shot.PierceCount,
                Homing = shot.Homing,
                HomingTurnRate = type.HomingTurnRate,
                HomingRange = type.HomingRange,
                HitMask = hitMask,
                IgnoreRoot = transform,
                HeldModelVisual = thrown,
                SpinRate = thrown ? heldWeapon.ThrownSpinRate : 0f,
                Bounce = thrown && heldWeapon.ThrownBounces,
            };

            foreach (float angle in RangedPattern.VolleyAngles(shot.ExtraProjectiles + 1, type.VolleySpreadAngle))
            {
                Vector3 arrowDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                ArrowProjectile.Launch(visual, muzzle, arrowDirection, settings,
                    (enemy, point, travel) => ArrowHit(type, shot, enemy, point, travel),
                    point => ArrowStuck(type, shot, point));
            }
        }

        private void TickVolleys(float dt)
        {
            for (int i = volleys.Count - 1; i >= 0; i--)
            {
                PendingVolley v = volleys[i];
                v.Delay -= dt;
                if (v.Delay > 0f) continue;

                volleys.RemoveAt(i);
                Volley(v.Type, v.Stats, v.Direction, true);
            }
        }

        private void FireRain(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 muzzle, Vector3 target)
        {
            // 見た目だけ、狙った所の方へ少し傾けて真上に放つ。
            Vector3 toward = Flat(target - muzzle, transform.forward);
            ArrowProjectile.Launch(type.ProjectilePrefab, muzzle, Vector3.up * 3f + toward, new ArrowProjectile.Settings
            {
                Speed = UpShotSpeed,
                Range = UpShotRange,
                Radius = 0.03f,
                HitMask = hitMask,
                IgnoreRoot = transform,
                VisualOnly = true,
            });

            float radius = type.RainRadius * shot.SizeScale;
            float startDelay = type.RainDelay / shot.ProjectileSpeedScale;
            var centers = new List<Vector3> { target };
            for (int i = 0; i < shot.ExtraProjectiles; i++)
                centers.Add(ScatterPoint(target, type.RainScatterMin * shot.SizeScale, type.RainScatterMax * shot.SizeScale));

            foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.RainRepeatInterval))
            {
                foreach (Vector3 center in centers)
                {
                    int ticks = 0;
                    ArrowRainZone.Spawn(center, new ArrowRainZone.Settings
                    {
                        Radius = radius,
                        Height = type.RainHeight,
                        StartDelay = startDelay + delay,
                        TickInterval = type.RainTickInterval,
                        TickCount = shot.RainTickCount,
                        Effect = type.RainEffect,
                        EffectRadius = type.RainEffectRadius,
                        RingMaterial = type.RangeRingMaterial,
                        RingColor = type.RainRingColor,
                    }, (c, r, h) => RainTick(type, shot, c, r, h, ticks++ == 0));
                }
            }
        }

        /// <summary>
        /// 数で増えた雨・治癒の場・置物の置き場所。狙った所から min〜max m のランダムな所の地面。
        /// 狙った所との間に壁があれば引き直し、5 回だめなら狙った所に重ねる。
        /// </summary>
        private Vector3 ScatterPoint(Vector3 target, float min, float max)
        {
            Vector3 lift = Vector3.up * 0.5f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                (float x, float z) = RangedPattern.RainOffset(min, max, random);
                Vector3 candidate = target + new Vector3(x, 0f, z);
                Vector3 path = candidate - target;
                if (path.sqrMagnitude < 1e-6f || !Raycast(new Ray(target + lift, path), path.magnitude, true, out _))
                    return GroundAt(candidate, target.y);
            }

            return target;
        }

        // ---- 召喚 ----

        /// <summary>
        /// 前の置物を全部消してから、1 体目を target に、数で増えた分を target の周り（決めた距離の範囲のランダムな所）に呼び出す。
        /// 置物どうしや持ち主に近すぎる所と、狙った所との間に壁がある所は引き直す。置物はそれぞれ持ち主の方を向く。
        /// </summary>
        private void FireSummon(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 target)
        {
            DismissSummons();

            var settings = new SummonedDecoy.Settings
            {
                Model = heldWeapon != null ? heldWeapon.SummonModel : null,
                Duration = shot.SummonDuration,
                Health = ItemInstance.SummonHealth(heldWeapon),
                SpawnEffect = type.SummonEffect,
                SpawnEffectScale = type.SummonEffectScale,
                DismissEffect = type.DismissEffect,
                DismissEffectScale = type.DismissEffectScale,
            };

            var placed = new List<Vector3> { target };
            for (int i = 0; i < shot.ExtraProjectiles; i++) placed.Add(SummonPoint(type, target, placed));

            foreach (Vector3 point in placed)
            {
                Quaternion facing = Quaternion.LookRotation(Flat(transform.position - point, -transform.forward), Vector3.up);
                SummonedDecoy decoy = SummonedDecoy.Spawn(point, facing, settings);
                decoy.Vanished += d => activeSummons.Remove(d);
                activeSummons.Add(decoy);
            }
        }

        /// <summary>
        /// 数で増えた置物 1 体の置き場所。target から決めた距離の範囲のランダムな所（壁の向こうは除く。<see cref="ScatterPoint"/>）で、
        /// もう置いた所と持ち主から最低の間隔だけ離れた所。決めた回数引いても見つからなければ、いちばん離れていた所にする。
        /// </summary>
        private Vector3 SummonPoint(WeaponTypeDefinition type, Vector3 target, List<Vector3> placed)
        {
            Vector3 best = target;
            float bestGap = -1f;
            for (int attempt = 0; attempt < SummonPlaceAttempts; attempt++)
            {
                Vector3 candidate = ScatterPoint(target, type.SummonScatterMin, type.SummonScatterMax);
                // 持ち主の足元にも重ねない。
                float gap = FlatDistance(candidate, transform.position);
                foreach (Vector3 other in placed) gap = Mathf.Min(gap, FlatDistance(candidate, other));

                if (gap >= type.SummonMinGap) return candidate;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }
            }

            return best;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>今居る置物を全部消す（呼び直し）。</summary>
        private void DismissSummons()
        {
            // Dismiss の中で Vanished から一覧を外すので、写してから回す。
            foreach (SummonedDecoy decoy in activeSummons.ToArray())
                if (decoy != null) decoy.Dismiss();
            activeSummons.Clear();
        }

        // ---- 治癒の場（治癒持続） ----

        /// <summary>
        /// 種を target へ放物線で投げ、落ちた所に場を張る。数で付近へ場を足し（種もそれぞれへ投げる）、多重で同じ所へもう一度張る。
        /// </summary>
        private void FireHealField(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 hand, Vector3 target)
        {
            float radius = type.HealRadius * shot.SizeScale;
            var centers = new List<Vector3> { target };
            for (int i = 0; i < shot.ExtraProjectiles; i++)
                centers.Add(ScatterPoint(target, type.HealScatterMin * shot.SizeScale, type.HealScatterMax * shot.SizeScale));

            float[] repeats = RangedPattern.RepeatDelays(shot.MultishotCount, type.HealRepeatInterval);
            foreach (Vector3 center in centers)
            {
                LobbedSeed.Launch(type.HealSeedPrefab, hand, center, type.HealFlightTime, type.HealArcHeight, landed =>
                {
                    foreach (float delay in repeats)
                    {
                        HealingFieldZone.Spawn(landed, new HealingFieldZone.Settings
                        {
                            Radius = radius,
                            Height = type.HealHeight,
                            StartDelay = delay,
                            TickInterval = type.HealTickInterval,
                            TickCount = shot.HealTickCount,
                            Effect = type.HealEffect,
                            EffectRadius = type.HealEffectRadius,
                            RingMaterial = type.RangeRingMaterial,
                            RingColor = type.HealRingColor,
                        }, (c, r, h, tick) => HealTick(type, shot, c, r, h, tick == 0));
                    }
                });
            }
        }

        /// <summary>場の中の味方（PlayerHealth を持つ物。今は主人公だけ）を 1 刻み分回復する。張った瞬間は音を大きく鳴らす。</summary>
        private void HealTick(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 center, float radius, float height, bool first)
        {
            healTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(radius, height) + height, wideOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                PlayerHealth health = wideOverlap[i].GetComponentInParent<PlayerHealth>();
                if (health == null || health.IsDead || !healTargets.Add(health)) continue;

                Vector3 offset = health.transform.position - center;
                float up = offset.y;
                offset.y = 0f;
                if (offset.magnitude > radius || up < -0.5f || up > Mathf.Max(0f, height)) healTargets.Remove(health);
            }

            bool healed = false;
            foreach (PlayerHealth health in healTargets)
            {
                if (health.CurrentHp >= health.MaxHp) continue;
                health.Heal(shot.HealTickAmount);
                healed = true;
                if (logHits) Debug.Log($"治癒の場 → {health.name}: +{shot.HealTickAmount}", health);
            }

            healTargets.Clear();
            // 張った瞬間は必ず、あとの刻みは回復できたときだけ控えめに鳴らす。同じフレームに何か所あっても 1 回。
            if ((first || healed) && healSoundFrame != Time.frameCount)
            {
                healSoundFrame = Time.frameCount;
                PlaySound(type.HealSound, center, type.SoundVolume * type.HealSoundVolume * (first ? 1f : HealTickSoundRatio));
            }
        }

        // ---- ダメージ軽減（加護）・治癒 ----

        /// <summary>
        /// 範囲内の仲間（自分も含む）から 武器種の人数（3）＋ 数 人を選んで掛ける。
        /// ダメージ軽減はその杖の種類の加護が付いていない人からランダムに選び、足りなければその種類の残りが短い人を掛け直す。
        /// 治癒は体力の割合の低い順（同じなら体力の少ない順）に選ぶ。満タンの人も選ぶので、誰も傷ついていなくても掛けた見た目は出る（回復はしない）。
        /// </summary>
        private void CastSupport(WeaponTypeDefinition type, RangedWeaponStats shot)
        {
            if (this == null) return;
            bool buff = type.IsBuff;
            CollectSupportPool(type.SupportRange);
            supportCandidates.Clear();
            BlessingKind kind = shot.BlessingKind;
            foreach (PlayerHealth health in supportPool)
            {
                CharacterBuffs buffs = buff ? health.GetComponent<CharacterBuffs>() : null;
                bool blessed = buffs != null && buffs.Has(kind);
                supportCandidates.Add(new SupportCandidate
                {
                    Fresh = !blessed, Remaining = blessed ? buffs.Remaining(kind) : 0f,
                    HealthFraction = health.Fraction, Health = health.CurrentHp,
                });
            }

            if (buff) SupportTargeting.Pick(supportCandidates, shot.TargetCount, true, supportRandom, supportPicks);
            else SupportTargeting.PickMostHurt(supportCandidates, shot.TargetCount, supportPicks);
            bool any = false;
            foreach (int index in supportPicks)
            {
                PlayerHealth health = supportPool[index];
                if (buff) Bless(type, shot, health);
                else
                {
                    health.Heal(shot.HealAmount);
                    if (logHits) Debug.Log($"治癒 → {health.name}: +{shot.HealAmount}", health);
                }

                Vector3 feet = health.transform.position;
                // 素材は出し続ける作りなので、少し見せてから放出を止めて薄くして消す（途中でぱっと消さない）。
                OneShotEffect.SpawnFading(type.SupportHitEffect, feet, Quaternion.identity, type.SupportHitEffectScale, SupportHitHold, SupportHitFade);
                any = true;
            }

            // 掛けた相手が居れば、同じフレームに何人居ても 1 回鳴らす。
            if (any && healSoundFrame != Time.frameCount)
            {
                healSoundFrame = Time.frameCount;
                PlaySound(type.HealSound, transform.position, type.SoundVolume * type.HealSoundVolume);
            }
        }

        private void Bless(WeaponTypeDefinition type, RangedWeaponStats shot, PlayerHealth health)
        {
            BlessingKind kind = shot.BlessingKind;
            CharacterBuffs buffs = CharacterBuffs.On(health.gameObject);
            buffs.Apply(kind, shot.BlessingAmount, shot.BlessingDuration);
            BlessingAura.Ensure(buffs, kind, type.BlessingEffect(kind), type.BlessingEffectScale);
            if (logHits) Debug.Log($"加護 → {health.name}: {kind} {shot.BlessingAmount:0.###}（{shot.BlessingDuration:0.#} 秒）", health);
        }

        /// <summary>自分から range 以内に居る、生きている仲間（パーティーの並び。並びが無ければ自分だけ）を supportPool に集める。</summary>
        private void CollectSupportPool(float range)
        {
            supportPool.Clear();
            IReadOnlyList<GameObject> members = PartyRoster.Members;
            if (members.Count == 0)
            {
                if (TryGetComponent(out PlayerHealth self) && !self.IsDead) supportPool.Add(self);
                return;
            }

            Vector3 origin = transform.position;
            float sqrRange = range * range;
            foreach (GameObject member in members)
            {
                if (member == null || !member.activeInHierarchy) continue;
                if (!member.TryGetComponent(out PlayerHealth health) || health.IsDead) continue;
                if (member != gameObject && (member.transform.position - origin).sqrMagnitude > sqrRange) continue;
                supportPool.Add(health);
            }
        }

        // ---- 雷（電撃） ----

        /// <summary>雷を放つ。多重で遅れて同じ狙いの線へもう一度（そのとき改めて敵を選ぶ）。</summary>
        private void FireChain(WeaponTypeDefinition type, RangedWeaponStats shot)
        {
            Ray aim = AimRayOrForward(type);
            foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.MultishotInterval))
            {
                if (delay <= 0f) CastChain(type, shot, aim, false);
                else Schedule(delay, () => CastChain(type, shot, aim, true));
            }
        }

        /// <summary>雷 1 回。狙いの敵が居れば当てて飛び移らせ、居なければ照準の先へ空撃ち（見た目だけ）。</summary>
        private void CastChain(WeaponTypeDefinition type, RangedWeaponStats shot, Ray aim, bool playSound)
        {
            Vector3 from = Muzzle(type);
            if (playSound) PlaySound(type.SwingSound, from, type.SoundVolume);

            EnemyHealth first = AimedEnemy(type, aim, from, out Vector3 point);
            if (first == null)
            {
                float distance = type.AimMaxDistance + Vector3.Distance(aim.origin, from);
                Vector3 end = Raycast(aim, distance, false, out RaycastHit hit) ? hit.point : aim.GetPoint(distance);
                LightningBolt.Spawn(from, end, type.BoltMaterial, type.BoltColor, type.BoltWidth, type.BoltDuration,
                    StaffTip(type.HeldInLeftHand ? leftHand : rightHand));
                OneShotEffect.Spawn(type.HitEffect, end, Quaternion.identity, type.HitEffectScale * 0.6f);
                return;
            }

            ChainHit(type, shot, from, first, point, new HashSet<EnemyHealth>(), type.ChainJumpsFor(shot.ExtraProjectiles),
                StaffTip(type.HeldInLeftHand ? leftHand : rightHand));
        }

        /// <summary>from から enemy へ雷を走らせて当て、残りの回数があれば少し遅れて近くの次の敵へ飛び移る。</summary>
        private void ChainHit(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 from, EnemyHealth enemy, Vector3 point,
            HashSet<EnemyHealth> struck, int jumpsLeft, Transform anchor = null)
        {
            struck.Add(enemy);
            LightningBolt.Spawn(from, point, type.BoltMaterial, type.BoltColor, type.BoltWidth, type.BoltDuration, anchor);
            if (!enemy.IsDead) DealHit(type, shot, enemy, point, point - from, MeleeHitKind.Hit, shot.ShotDamage, 1f, 1f, "雷");
            if (jumpsLeft <= 0) return;

            Schedule(type.ChainJumpDelay, () =>
            {
                EnemyHealth next = NearestEnemy(point, type.ChainRange, struck, out Vector3 nextPoint);
                if (next != null) ChainHit(type, shot, point, next, nextPoint, struck, jumpsLeft - 1);
            });
        }

        /// <summary>
        /// 雷の最初の的。杖の先から aimMaxDistance 以内で、狙いの線からの角度が chainAimAngle 以内、杖の先から見通せる生きた敵のうち、
        /// 角度がいちばん小さい敵（同じなら近い方）。point はその体の真ん中。
        /// </summary>
        private EnemyHealth AimedEnemy(WeaponTypeDefinition type, Ray aim, Vector3 from, out Vector3 point)
        {
            point = default;
            EnemyHealth best = null;
            float bestAngle = float.MaxValue;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(from, type.AimMaxDistance, wideOverlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = wideOverlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 center = wideOverlap[i].bounds.center;
                Vector3 toCenter = center - aim.origin;
                float angle = Vector3.Angle(aim.direction, toCenter);
                float distance = Vector3.Distance(from, center);
                if (angle > type.ChainAimAngle || distance > type.AimMaxDistance) continue;
                if (angle > bestAngle + 0.01f || (Mathf.Abs(angle - bestAngle) <= 0.01f && distance >= bestDistance)) continue;
                if (!InSight(from, center)) continue;

                best = enemy;
                bestAngle = angle;
                bestDistance = distance;
                point = center;
            }

            return best;
        }

        /// <summary>from から range 以内で、いちばん近い、exclude に無い、見通せる生きた敵。point はその体の真ん中。</summary>
        private EnemyHealth NearestEnemy(Vector3 from, float range, HashSet<EnemyHealth> exclude, out Vector3 point)
        {
            point = default;
            EnemyHealth best = null;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(from, range, wideOverlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = wideOverlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || exclude.Contains(enemy)) continue;

                Vector3 center = wideOverlap[i].bounds.center;
                float distance = Vector3.Distance(from, center);
                if (distance > range || distance >= bestDistance || !InSight(from, center)) continue;

                best = enemy;
                bestDistance = distance;
                point = center;
            }

            return best;
        }

        /// <summary>a から b の手前まで、壁や床（敵と撃った本人は除く）に遮られていないか。</summary>
        private bool InSight(Vector3 a, Vector3 b)
        {
            Vector3 path = b - a;
            float length = path.magnitude;
            return length < 0.3f || !Raycast(new Ray(a, path), length - 0.25f, true, out _);
        }

        // ---- 連置（範囲連置） ----

        /// <summary>照準の水平の向きへ列を出す。数で扇状に列が増え、多重で同じ向きへもう一列。</summary>
        private void FireLine(WeaponTypeDefinition type, RangedWeaponStats shot)
        {
            Vector3 forward = Flat(AimRayOrForward(type).direction, transform.forward);
            int pillars = type.LinePillarsFor(shot.DurationScale);
            foreach (float angle in RangedPattern.VolleyAngles(shot.ExtraProjectiles + 1, type.LineSpreadAngle))
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.LineRepeatInterval))
                {
                    if (delay <= 0f) CastLine(type, shot, direction, pillars, false);
                    else Schedule(delay, () => CastLine(type, shot, direction, pillars, true));
                }
            }
        }

        /// <summary>
        /// 1 列。足元から direction へ lineStartDistance 先を 1 つ目に、lineSpacing ずつ離して手前から順に当てる。
        /// 膝の高さで壁に当たったら、その手前で止める。1 列は同じ敵に 1 回だけ当たる。
        /// 見た目（lineEffect）は列の頭から列の長さだけ走らせ、当たりはその見た目が届く時刻に出す（無ければ lineInterval ずつ遅らせる）。
        /// </summary>
        private void CastLine(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 direction, int pillars, bool playSound)
        {
            Vector3 origin = transform.position;
            if (playSound) PlaySound(type.SwingSound, origin, type.SoundVolume);

            float reach = type.LineStartDistance + (pillars - 1) * type.LineSpacing;
            if (Raycast(new Ray(origin + Vector3.up * 0.5f, direction), reach + 0.3f, true, out RaycastHit wall))
                reach = wall.distance - 0.3f;

            int count = 0;
            while (count < pillars && type.LineStartDistance + count * type.LineSpacing <= reach) count++;
            if (count == 0) return;

            // 見た目は 1 つ目の半間隔手前から、最後の 1 つの半間隔先まで。
            float length = count * type.LineSpacing;
            if (type.LineEffect != null)
            {
                Vector3 head = origin + direction * (type.LineStartDistance - type.LineSpacing * 0.5f);
                SpawnLineEffect(type, GroundAt(head + Vector3.up * 1.5f, head.y), direction, length);
            }

            var struck = new HashSet<EnemyHealth>();
            for (int i = 0; i < count; i++)
            {
                Vector3 point = origin + direction * (type.LineStartDistance + i * type.LineSpacing);
                float delay = type.LineEffect != null ? type.LineEffectTravelTime * (i + 0.5f) / count : i * type.LineInterval;
                if (delay <= 0f) Pillar(type, shot, point, struck);
                else Schedule(delay, () => Pillar(type, shot, point, struck));
            }
        }

        /// <summary>
        /// 列の見た目を head から direction へ length だけ走らせる。素材は +X へ lineEffectLength 走るので、+X を direction に合わせ、
        /// 根の粒の速さ（届く秒数は変えない）と、原点から離れて置かれた終わりの飾りの位置を length に合わせて伸び縮みさせる。
        /// </summary>
        private static void SpawnLineEffect(WeaponTypeDefinition type, Vector3 head, Vector3 direction, float length)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
            GameObject effect = OneShotEffect.Spawn(type.LineEffect, head, rotation);
            if (effect == null) return;

            float ratio = length / Mathf.Max(0.1f, type.LineEffectLength);
            if (effect.TryGetComponent(out ParticleSystem root))
            {
                ParticleSystem.MainModule main = root.main;
                main.startSpeedMultiplier *= ratio;
            }

            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                Transform t = particles.transform;
                if (t != effect.transform && t.localPosition.x > 1f) t.localPosition = Vector3.Scale(t.localPosition, new Vector3(ratio, 1f, 1f));
            }
        }

        /// <summary>point の真下の地面を中心に、範囲の中のまだこの列に当たっていない敵に当てる。</summary>
        private void Pillar(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 point, HashSet<EnemyHealth> struck)
        {
            // 上りの段でも段の上で当たるよう、少し上から地面を探す。
            Vector3 ground = GroundAt(point + Vector3.up * 1.5f, point.y);
            foreach ((EnemyHealth enemy, Vector3 hitPoint) in EnemiesInCircle(ground, type.LineRadius, type.LineHeight))
            {
                if (!struck.Add(enemy)) continue;
                DealHit(type, shot, enemy, hitPoint, enemy.transform.position - ground, MeleeHitKind.Hit, shot.ShotDamage, 1f, 1f, "連置");
            }
        }

        // ---- 炎（火炎放射器） ----

        /// <summary>wants（押した・押している）の間、待ちが明けていれば吐き始め、吐き続ける。離すか時間が尽きたら止めて、吐いた割合だけ待たせる。</summary>
        private void UpdateFlame(float dt, bool wants)
        {
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            if (!burning)
            {
                if (wants && cooldownRemaining <= 0f) StartFlame(type);
                return;
            }

            // 刻みの頭に当て、刻みを使い切ってその刻みの間隔が過ぎたら止まる（吐く長さ ＝ 刻みの数 × 間隔）。
            burnElapsed += dt;
            flameTickTimer -= dt;
            if (!wants || (ticksLeft <= 0 && flameTickTimer <= 0f))
            {
                StopFlame(true);
                return;
            }

            while (ticksLeft > 0 && flameTickTimer <= 0f)
            {
                ticksLeft--;
                FlameTick(type, burnStats);
                flameTickTimer += Mathf.Max(0.02f, burnStats.FlameTickInterval);
            }

            flameSoundTimer -= dt;
            if (flameSoundTimer <= 0f)
            {
                PlaySound(type.SwingSound, Muzzle(type), type.SoundVolume);
                flameSoundTimer += Mathf.Max(0.05f, type.FlameSoundInterval);
            }
        }

        private void StartFlame(WeaponTypeDefinition type)
        {
            stats = burnStats = ComputeStats();
            burning = true;
            ticksLeft = burnStats.FlameTickCount;
            burnElapsed = 0f;
            flameTickTimer = 0f;
            flameSoundTimer = 0f;

            Vector3 target = AimPoint(type);
            AimBody(type, target, 360f, false);
            SetCasting(true);

            Vector3 muzzle = Muzzle(type);
            Vector3 aim = AimDirection(muzzle, target);
            foreach (float angle in RangedPattern.VolleyAngles(burnStats.ExtraProjectiles + 1, type.FlameSpreadAngle))
            {
                var jet = new FlameJet { Angle = angle, Direction = Quaternion.AngleAxis(angle, Vector3.up) * aim };
                if (type.FlameEffect != null)
                {
                    jet.Effect = Instantiate(type.FlameEffect, muzzle, Quaternion.LookRotation(jet.Direction));
                    foreach (ParticleSystem particles in jet.Effect.GetComponentsInChildren<ParticleSystem>())
                    {
                        ParticleSystem.MainModule main = particles.main;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    }
                }

                jets.Add(jet);
            }

            UpdateJets(type, target, 0f);
        }

        /// <summary>
        /// 炎を止める。cooldown なら 撃つ間隔 × 吐いた割合 だけ待たせる（吐き切れば撃つ間隔まるごと。持ち替えや無効化のときは待たせない）。
        /// 待ちの長さは撃つ間隔のまま残りだけ縮めるので、待ちの割合は吐いた割合から始まる。
        /// </summary>
        private void StopFlame(bool cooldown)
        {
            if (!burning) return;

            float used = FlameUsedFraction;
            burning = false;
            SetCasting(false);
            foreach (FlameJet jet in jets)
            {
                if (jet.Effect == null) continue;
                foreach (ParticleSystem particles in jet.Effect.GetComponentsInChildren<ParticleSystem>())
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                foreach (Light light in jet.Effect.GetComponentsInChildren<Light>()) light.enabled = false;
                Destroy(jet.Effect, FlameLingerTime);
            }

            jets.Clear();
            if (cooldown && burnStats != null)
            {
                cooldownDuration = burnStats.FireInterval;
                cooldownRemaining = burnStats.FireInterval * used;
            }
        }

        /// <summary>
        /// 炎の筋の向きを照準（target）に合わせ、見た目を杖の先へ置く。ホーミングなら筋の向きの近くの敵へ、決めた速さで曲げる。
        /// 見た目は届く距離に合わせて伸ばし、太さはサイズで広げる。
        /// </summary>
        private void UpdateJets(WeaponTypeDefinition type, Vector3 target, float dt)
        {
            if (burnStats == null) return;

            Vector3 muzzle = Muzzle(type);
            Vector3 aim = AimDirection(muzzle, target);
            float range = type.FlameRange * burnStats.ProjectileSpeedScale;
            foreach (FlameJet jet in jets)
            {
                Vector3 want = Quaternion.AngleAxis(jet.Angle, Vector3.up) * aim;
                if (burnStats.Homing && HomingTarget(muzzle, want, range, type.FlameHomingAngle, out Vector3 enemyPoint))
                {
                    want = (enemyPoint - muzzle).normalized;
                    jet.Direction = dt > 0f
                        ? Vector3.RotateTowards(jet.Direction, want, type.FlameHomingTurnRate * Mathf.Deg2Rad * dt, 0f)
                        : want;
                }
                else
                {
                    // 照準へは遅れずに付いていく（曲がっていた筋も同じ速さで戻す）。
                    jet.Direction = dt > 0f && burnStats.Homing
                        ? Vector3.RotateTowards(jet.Direction, want, type.FlameHomingTurnRate * Mathf.Deg2Rad * dt, 0f)
                        : want;
                }

                if (jet.Effect == null) continue;
                float length = range / Mathf.Max(0.1f, type.FlameEffectLength);
                float width = length * burnStats.SizeScale;
                jet.Effect.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(jet.Direction));
                jet.Effect.transform.localScale = new Vector3(width, width, length);
            }
        }

        /// <summary>炎の 1 刻み。筋ごとに、壁までの円錐（杖の先で flameBaseRadius、先へ広がりの角度で太る）の中の生きた敵に当てる。</summary>
        private void FlameTick(WeaponTypeDefinition type, RangedWeaponStats shot)
        {
            Vector3 muzzle = Muzzle(type);
            float range = type.FlameRange * shot.ProjectileSpeedScale;
            float spread = Mathf.Tan(Mathf.Clamp(type.FlameAngle * shot.SizeScale, 1f, 80f) * Mathf.Deg2Rad);
            float baseRadius = type.FlameBaseRadius * shot.SizeScale;

            foreach (FlameJet jet in jets)
            {
                Vector3 direction = jet.Direction;
                float reach = Raycast(new Ray(muzzle, direction), range, true, out RaycastHit wall) ? wall.distance : range;

                explosionTargets.Clear();
                int count = Physics.OverlapSphereNonAlloc(muzzle, reach + baseRadius + spread * reach, wideOverlap, hitMask,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    EnemyHealth enemy = wideOverlap[i].GetComponentInParent<EnemyHealth>();
                    if (enemy == null || enemy.IsDead || explosionTargets.Contains(enemy)) continue;

                    // 体の真ん中に近い、炎の中心の線上の点から測った、体のいちばん近い所。
                    Vector3 center = wideOverlap[i].bounds.center;
                    Vector3 onAxis = muzzle + direction * Mathf.Clamp(Vector3.Dot(center - muzzle, direction), 0f, reach);
                    Vector3 point = wideOverlap[i].ClosestPoint(onAxis);
                    Vector3 offset = point - muzzle;
                    float along = Vector3.Dot(offset, direction);
                    if (along < -0.3f || along > reach) continue;

                    float apart = (offset - direction * along).magnitude;
                    if (apart > baseRadius + spread * Mathf.Max(0f, along)) continue;

                    explosionTargets.Add(enemy);
                    DealHit(type, shot, enemy, point, direction, MeleeHitKind.Tick, shot.FlameTickDamage,
                        TickHitEffectRatio, TickHitSoundRatio, "炎");
                }
            }

            explosionTargets.Clear();
        }

        /// <summary>muzzle から direction の angle 度以内・range 以内で、見通せる生きた敵のうち向きにいちばん近い敵の体の真ん中。</summary>
        private bool HomingTarget(Vector3 muzzle, Vector3 direction, float range, float angle, out Vector3 point)
        {
            point = default;
            float best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(muzzle, range, wideOverlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = wideOverlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 center = wideOverlap[i].bounds.center;
                float a = Vector3.Angle(direction, center - muzzle);
                if (a > angle || a >= best || !InSight(muzzle, center)) continue;

                best = a;
                point = center;
            }

            return best < float.MaxValue;
        }

        /// <summary>杖の先から照準の点への向き。照準が杖の先に近すぎるか後ろなら、狙いの線の向き。</summary>
        private Vector3 AimDirection(Vector3 muzzle, Vector3 target)
        {
            Vector3 direction = target - muzzle;
            Vector3 fallback = Aim.HasValue ? Aim.Value.direction : transform.forward;
            if (direction.sqrMagnitude < 0.25f || Vector3.Dot(direction, fallback) <= 0f) direction = fallback;
            return direction.normalized;
        }

        /// <summary>投擲で手の武器の見た目を消す・出し直す。</summary>
        private void SetHandEmpty(bool empty)
        {
            handEmptied = empty;
            if (melee != null) melee.SetHeldModelVisible(!empty);
        }

        private void SetCasting(bool casting)
        {
            if (animator != null && hasCastingParam) animator.SetBool(CastingParam, casting);
        }

        // ---- 遅れて起こすこと ----

        private void Schedule(float delay, Action run) => scheduled.Add(new Scheduled { Delay = delay, Run = run });

        private void TickScheduled(float dt)
        {
            // 起こした中でさらに足されることがある（雷の飛び移り）ので、今ある分だけを見る。足された物は次のフレームから数える。
            for (int i = scheduled.Count - 1; i >= 0; i--) scheduled[i].Delay -= dt;

            for (int i = 0; i < scheduled.Count; i++)
            {
                Scheduled s = scheduled[i];
                if (s.Delay > 0f) continue;

                scheduled.RemoveAt(i--);
                s.Run();
            }
        }

        /// <summary>杖の雷・連置・炎の 1 撃。クリティカルを引いて当て、見た目と音を出し、Dealt で知らせる。</summary>
        private void DealHit(WeaponTypeDefinition type, RangedWeaponStats shot, EnemyHealth enemy, Vector3 point, Vector3 travel,
            MeleeHitKind kind, int damage, float effectRatio, float soundRatio, string label)
        {
            bool critical = UnityEngine.Random.value < shot.CritChance;
            if (critical) damage = shot.ApplyCritical(damage);

            Vector3 direction = Flat(travel, transform.forward);
            float knockback = (kind == MeleeHitKind.Tick ? 0f : type.ProjectileKnockback) + shot.KnockbackBonus;
            int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical, knockback, shot.ReactionScale));
            PlayHitFeedback(type, point, direction, critical, effectRatio, soundRatio);
            if (logHits) Debug.Log($"{label} → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}", enemy);
            Dealt?.Invoke(new MeleeHitRecord(kind, enemy, 0, damage, dealt, critical, point));
        }

        // ---- 当てる ----

        private void ArrowHit(WeaponTypeDefinition type, RangedWeaponStats shot, EnemyHealth enemy, Vector3 point, Vector3 travel)
        {
            if (enemy == null || enemy.IsDead) return;

            bool critical = UnityEngine.Random.value < shot.CritChance;
            int damage = shot.ShotDamage;
            if (critical) damage = shot.ApplyCritical(damage);

            Vector3 direction = Flat(travel, transform.forward);
            int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical,
                type.ProjectileKnockback + shot.KnockbackBonus, shot.ReactionScale));
            PlayHitFeedback(type, point, direction, critical, 1f, 1f);
            if (logHits) Debug.Log($"矢 → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}", enemy);
            Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.Hit, enemy, 0, damage, dealt, critical, point));

            Explode(point, shot.ExplosionDamage(damage), shot);
        }

        /// <summary>矢が壁や床に刺さった。刺さる音（同じフレームに何本刺さっても 1 回）と、爆発のエンチャント。</summary>
        private void ArrowStuck(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 point)
        {
            if (stickSoundFrame != Time.frameCount)
            {
                stickSoundFrame = Time.frameCount;
                PlaySound(type.StickSound, point, type.SoundVolume * type.StickSoundVolume);
            }

            Explode(point, shot.ExplosionDamage(shot.ShotDamage), shot);
        }

        /// <summary>雨の 1 刻み。円柱の中の生きた敵みんなに刻みのダメージ（クリティカルは毎刻み引く）。押し出しはエンチャントの分だけ。</summary>
        private void RainTick(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 center, float radius, float height, bool first)
        {
            // 降る音は当たっても外れても鳴らす。降り始めは大きく、あとの刻みは控えめに。同じフレームに何か所降っても 1 回。
            if (rainSoundFrame != Time.frameCount)
            {
                rainSoundFrame = Time.frameCount;
                PlaySound(type.RainSound, center, type.SoundVolume * type.RainSoundVolume * (first ? 1f : RainTickSoundRatio));
            }

            foreach ((EnemyHealth enemy, Vector3 point) in EnemiesInCircle(center, radius, height))
            {
                bool critical = UnityEngine.Random.value < shot.CritChance;
                int damage = shot.RainTickDamage;
                if (critical) damage = shot.ApplyCritical(damage);

                Vector3 direction = Flat(enemy.transform.position - center, transform.forward);
                int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical, shot.KnockbackBonus, shot.ReactionScale));
                PlayHitFeedback(type, point, direction, critical, TickHitEffectRatio, TickHitSoundRatio);
                if (logHits) Debug.Log($"矢の雨 → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}", enemy);
                Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.Tick, enemy, 0, damage, dealt, critical, point));
            }
        }

        /// <summary>命中の見た目と音。音は同じフレームに何本当たっても 1 回だけ。</summary>
        private void PlayHitFeedback(WeaponTypeDefinition type, Vector3 point, Vector3 direction, bool critical, float effectRatio, float soundRatio)
        {
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            OneShotEffect.Spawn(type.HitEffect, point, facing, type.HitEffectScale * effectRatio);
            if (critical) OneShotEffect.Spawn(criticalHitEffect, point, facing, criticalHitEffectScale * effectRatio);

            if (hitSoundFrame != Time.frameCount)
            {
                hitSoundFrame = Time.frameCount;
                PlaySound(type.HitSound, point, type.SoundVolume * soundRatio);
            }

            if (critical && criticalSoundFrame != Time.frameCount)
            {
                criticalSoundFrame = Time.frameCount;
                PlaySound(criticalHitSound, point, criticalHitSoundVolume);
            }
        }

        /// <summary>爆発のエンチャント。範囲の敵（当てた敵も含む）に追加のダメージ。爆発からは爆発しない。</summary>
        private void Explode(Vector3 point, int damage, RangedWeaponStats shot)
        {
            if (damage <= 0) return;

            OneShotEffect.Spawn(explosionEffect, point, Quaternion.identity, explosionEffectScale * shot.SizeScale);
            PlaySound(explosionSound, point, explosionSoundVolume);

            explosionTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, shot.ExplosionRadius, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !explosionTargets.Add(enemy)) continue;

                Vector3 direction = enemy.transform.position - point;
                int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction));
                if (logHits) Debug.Log($"爆発 → {enemy.name}: {dealt}", enemy);
                Dealt?.Invoke(new MeleeHitRecord(MeleeHitKind.Explosion, enemy, 0, damage, dealt, false, overlap[i].ClosestPoint(point)));
            }
        }

        /// <summary>center から水平に radius 以内、上下 height 以内にいる生きた敵（1 体 1 回）と、当たった場所。</summary>
        private List<(EnemyHealth, Vector3)> EnemiesInCircle(Vector3 center, float radius, float height)
        {
            var result = new List<(EnemyHealth, Vector3)>();
            if (radius <= 0f) return result;

            explosionTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(radius, height) + height, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 point = overlap[i].ClosestPoint(center);
                Vector3 flat = point - center;
                float up = flat.y;
                flat.y = 0f;
                if (flat.magnitude > radius || up < -0.5f || up > Mathf.Max(0f, height)) continue;
                if (explosionTargets.Add(enemy)) result.Add((enemy, point));
            }

            explosionTargets.Clear();
            return result;
        }

        // ---- 狙い ----

        /// <summary>弓の狙い。狙いの線が最初に当たった物（撃った本人を除く）、無ければ射程の先。</summary>
        private Vector3 AimPoint(WeaponTypeDefinition type)
        {
            Ray ray = AimRayOrForward(type);
            float distance = type.AimMaxDistance + Vector3.Distance(ray.origin, Muzzle(type));
            return Raycast(ray, distance, false, out RaycastHit hit) ? hit.point : ray.GetPoint(distance);
        }

        /// <summary>
        /// 持続弓の狙い。狙いの線が当たった所の真下の地面。水平に aimMaxDistance より遠ければ、その向きの射程の端で切る。
        /// 何にも当たらない（空を見ている）ときも、その向きの射程の端。
        /// </summary>
        private Vector3 GroundTarget(WeaponTypeDefinition type)
        {
            Ray ray = AimRayOrForward(type);
            Vector3 origin = transform.position;
            float far = type.AimMaxDistance + Vector3.Distance(ray.origin, origin) + 1f;
            Vector3 point = Raycast(ray, far, false, out RaycastHit hit)
                ? hit.point + hit.normal * 0.2f
                : ray.GetPoint(far);

            Vector3 flat = point - origin;
            flat.y = 0f;
            if (flat.magnitude > type.AimMaxDistance) point = origin + flat.normalized * type.AimMaxDistance + Vector3.up * (point.y - origin.y);
            return GroundAt(point, origin.y);
        }

        /// <summary>point の真下の地面（敵と撃った本人は除く）。見つからなければ高さ fallbackY に置く。</summary>
        private Vector3 GroundAt(Vector3 point, float fallbackY)
        {
            // 天井の上に置いてしまわないよう、持ち主より高い所からは探し始めない。
            float startY = Mathf.Min(point.y, transform.position.y + 2f) + 0.5f;
            var down = new Ray(new Vector3(point.x, startY, point.z), Vector3.down);
            return Raycast(down, 30f, true, out RaycastHit hit) ? hit.point : new Vector3(point.x, fallbackY, point.z);
        }

        /// <summary>ray の先で一番近い当たり。撃った本人は除き、skipEnemies なら敵も除く。</summary>
        private bool Raycast(Ray ray, float distance, bool skipEnemies, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            int count = Physics.RaycastNonAlloc(ray, rayHits, distance, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = rayHits[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(transform)) continue;
                if (skipEnemies && hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;

                best = hit.distance;
                nearest = hit;
            }

            return best < float.MaxValue;
        }

        private Ray AimRayOrForward(WeaponTypeDefinition type) => Aim ?? new Ray(Muzzle(type), transform.forward);

        /// <summary>矢の出る所。弓を持つ手、杖なら杖の先（人型でなければ武器種の muzzleOffset）。</summary>
        private Vector3 Muzzle(WeaponTypeDefinition type)
        {
            Transform hand = type.HeldInLeftHand ? leftHand : rightHand;
            if (type.IsStaff)
            {
                Transform tip = StaffTip(hand);
                if (tip != null) return tip.position;
            }

            return hand != null ? hand.position : transform.TransformPoint(type.MuzzleOffset);
        }

        /// <summary>手の武器の見た目の中の杖の先（<see cref="WeaponTypeDefinition.StaffTipName"/>）。持ち替えたら 1 回だけ探し直す。</summary>
        private Transform StaffTip(Transform hand)
        {
            if (staffTip != null) return staffTip;
            if (staffTipSearched || hand == null) return null;

            staffTipSearched = true;
            foreach (Transform child in hand.GetComponentsInChildren<Transform>())
            {
                if (child.name != WeaponTypeDefinition.StaffTipName) continue;
                staffTip = child;
                break;
            }

            return staffTip;
        }

        /// <summary>
        /// bend なら、腕の上下の向きが狙い（持続弓は武器種の rainAimPitch）に合うよう、背骨を体の右軸まわりに曲げる（Animator の評価の後に呼ぶこと）。
        /// そのうえで、弓を持つ腕（付け根から手）が水平に target を向くよう、体を最大 maxYaw 度回す。回し残した角度を返す。
        /// 曲げると腕の水平の向きも少し変わるので、曲げてから回す。人型でないか杖・投擲なら、体の前を target へ向けるだけ。
        /// </summary>
        private float AimBody(WeaponTypeDefinition type, Vector3 target, float maxYaw, bool bend)
        {
            // 杖は腕の向きが構えで変わり、投擲は腕を振るので、腕ではなく体の前を狙いへ向け、背骨も曲げない。
            Transform shoulder = type.FacesBodyToAim ? null : type.HeldInLeftHand ? leftUpperArm : rightUpperArm;
            Transform hand = type.FacesBodyToAim ? null : type.HeldInLeftHand ? leftHand : rightHand;
            Vector3 from = shoulder != null ? shoulder.position : transform.position;
            Vector3 arm = shoulder != null && hand != null ? hand.position - shoulder.position : transform.forward;

            Vector3 toTarget = target - from;
            if (bend && spine.Length > 0 && shoulder != null && hand != null)
            {
                float want = type.RangedKind == RangedAttackKind.Rain ? type.RainAimPitch : Pitch(toTarget);
                float angle = Mathf.Clamp(want - Pitch(arm), -maxAimPitch, maxAimPitch);
                // 体の右軸まわりの正の回転は前を下げるので、上げるときは負に回す。
                Quaternion step = Quaternion.AngleAxis(-angle / spine.Length, transform.right);
                foreach (Transform bone in spine) bone.rotation = step * bone.rotation;
                arm = hand.position - shoulder.position;
            }

            Vector3 flatTarget = toTarget;
            flatTarget.y = 0f;
            // 足元に近すぎる狙いでは向きが定まらないので回さない。
            if (flatTarget.sqrMagnitude <= 1f) return 0f;

            float yaw = Vector3.SignedAngle(Flat(arm, transform.forward), flatTarget, Vector3.up);
            float turn = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            transform.rotation = Quaternion.AngleAxis(turn, Vector3.up) * transform.rotation;
            return yaw - turn;
        }

        /// <summary>水平からの仰角（度、上が正）。</summary>
        private static float Pitch(Vector3 direction) =>
            Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;

        /// <summary>持続弓・召喚・治癒持続を持っている間、狙う地面（target）に範囲の円を出す。</summary>
        private void UpdateAimRing(WeaponTypeDefinition type, Vector3 target)
        {
            bool show = ShowAimRing && type.AimsAtGround && type.RangeRingMaterial != null;
            if (!show)
            {
                if (aimRing != null) aimRing.SetVisible(false);
                return;
            }

            if (aimRing == null) aimRing = RangeRing.Create("AimRing", type.RangeRingMaterial, type.AimRingColor);
            aimRing.SetColor(type.AimRingColor);
            aimRing.SetVisible(true);
            aimRing.Set(target, AimRingRadius(type));
        }

        /// <summary>照準の円の半径。持続弓は雨、治癒持続は場の半径、召喚は置物を置く範囲（数が無ければ 1 体分）に余白を足したもの。</summary>
        private float AimRingRadius(WeaponTypeDefinition type)
        {
            float size = stats != null ? stats.SizeScale : 1f;
            if (type.IsHealField) return type.HealRadius * size;
            if (type.IsSummon) return (stats != null && stats.ExtraProjectiles > 0 ? type.SummonScatterMax : 0f) + SummonRingPadding;
            return type.RainRadius * size;
        }

        // ---- 共通 ----

        private void PlayFireAnimation()
        {
            if (animator == null) return;
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            if (hasAttackSpeedParam)
            {
                if (type.UsesThrowMotion) animator.SetFloat(AttackSpeedParam, ThrowAnimationSpeed());
                else if (!type.IsStaff) animator.SetFloat(AttackSpeedParam, Mathf.Max(1f, BowCycleSeconds / Mathf.Max(0.01f, stats.FireInterval)));
            }

            if (hasAttackParam) animator.SetTrigger(AttackParam);
        }

        /// <summary>投げのモーションを流す速さ（撃つ間隔が 1 周より短い分だけ速める）。</summary>
        private float ThrowAnimationSpeed() => Mathf.Max(1f, ThrowCycleSeconds / Mathf.Max(0.01f, stats != null ? stats.FireInterval : 1f));

        private static Vector3 Flat(Vector3 direction, Vector3 fallback)
        {
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

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;

            var bones = new List<Transform>();
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t != null) bones.Add(t);
            }

            spine = bones.ToArray();
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        /// <summary>弓のモーションが引き絞り（Bow Draw）に入った瞬間に、引き絞る音を鳴らす（持ち替えたときと、撃って引き直すとき）。</summary>
        private void UpdateDrawSound(WeaponTypeDefinition type)
        {
            bool drawing = false;
            if (type != null && animator != null && upperBodyLayer >= 0)
            {
                drawing = animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash == BowDrawState
                          || (animator.IsInTransition(upperBodyLayer)
                              && animator.GetNextAnimatorStateInfo(upperBodyLayer).shortNameHash == BowDrawState);
            }

            if (drawing && !wasDrawing) PlaySound(type.DrawSound, Muzzle(type), type.SoundVolume);
            wasDrawing = drawing;
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null) return;

            upperBodyLayer = animator.GetLayerIndex(UpperBodyLayer);

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == AttackParam) hasAttackParam = true;
                else if (p.name == AttackSpeedParam) hasAttackSpeedParam = true;
                else if (p.name == CastingParam) hasCastingParam = true;
            }
        }
    }
}
