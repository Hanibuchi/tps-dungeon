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
    /// 持たされた遠距離武器（弓・持続弓）で撃つ実行役。入力は持たず、外から <see cref="Equip"/>（持ち替え）・<see cref="PressAttack"/>（攻撃の押下）・
    /// <see cref="Aim"/>（狙いの線。プレイヤーならカメラの中心）を渡してもらう。プレイヤーは PlayerMeleeInput が叩く。
    /// 押した瞬間に、待ちが明けていれば撃つ（押しっぱなしでは撃たない）。撃つ間隔は武器種の fireInterval を速射で縮めたもの。
    ///   弓     … 狙いの線が当たった所（無ければ射程の先）へ、弓を持つ手から矢を放つ。数で増えた矢は照準の右左へ交互に開き（1 本は必ず照準へ）、
    ///            多重で遅れて同じ向きへもう一斉射。爆発は敵に当たっても、壁や床に刺さっても起きる
    ///   持続弓 … 狙いの線が当たった地面（射程の水平距離まで）へ、上に矢を放ってから雨を降らせ、範囲に刻みでダメージ。
    ///            数で増えた雨は狙った所の付近のランダムな所に、多重で同じ所にもう一度降る。持っている間は狙う地面に範囲の円を出す
    /// 遠距離の武器を持っていて狙いの線がある間は、毎フレーム（Animator の後で）体ごと狙いの方へ回し、背骨を曲げて弓を持つ腕を狙いへ向ける
    /// （持続弓は空へ向けて反らせる）。歩く向きへ回す ThirdPersonController より後に上書きする。
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
        private static readonly int BowDrawState = Animator.StringToHash("Bow Draw");

        /// <summary>
        /// 弓のモーションの 1 周（Bow Release で放してから、つがえ直して Bow Draw で引き切るまで。BowShot の 13〜29F と 0〜7F、30fps）。
        /// 撃つ間隔がこれより短いと置いていかれるので、その分だけ速めて流す。
        /// </summary>
        private const float BowCycleSeconds = (16f + 7f) / 30f;

        /// <summary>持続弓で上に放つ見た目の矢の速さ（m/s）と、飛ぶ距離（m）。</summary>
        private const float UpShotSpeed = 40f;
        private const float UpShotRange = 14f;

        /// <summary>雨の 1 刻みの命中の音の大きさ（武器種の音量に対して）。刻みは何度も来るので控えめに。</summary>
        private const float TickHitSoundRatio = 0.4f;

        /// <summary>雨の刻みごとに鳴らす降る音の大きさ（降り始めの音に対して）。</summary>
        private const float RainTickSoundRatio = 0.5f;

        /// <summary>雨の 1 刻みの命中の見た目の大きさ（武器種の命中の見た目に対して）。</summary>
        private const float TickHitEffectRatio = 0.5f;

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
        private readonly HashSet<EnemyHealth> explosionTargets = new HashSet<EnemyHealth>();
        private readonly Collider[] overlap = new Collider[32];
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

        /// <summary>次に撃てるまでの待ちの残りの割合（1 で待ち始め、0 で撃てる）。</summary>
        public float CooldownFraction => cooldownDuration > 0f && cooldownRemaining > 0f ? Mathf.Clamp01(cooldownRemaining / cooldownDuration) : 0f;

        /// <summary>狙いの線（プレイヤーならカメラの中心）。null なら体の前へ撃ち、持続弓の照準の円も出さない。</summary>
        public Ray? Aim { get; set; }

        /// <summary>敵に当てた 1 回ごと（矢・爆発・雨の刻み）。ダメージ表示や試験の窓が読む。</summary>
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
            CacheAnimatorParameters();
            CacheBones();
        }

        private void OnDisable()
        {
            volleys.Clear();
            pressQueued = false;
            if (aimRing != null) aimRing.SetVisible(false);
        }

        private void OnDestroy()
        {
            if (aimRing != null) Destroy(aimRing.gameObject);
        }

        /// <summary>攻撃を 1 回押す。次の Update で、待ちが明けていれば撃つ。</summary>
        public void PressAttack() => pressQueued = true;

        /// <summary>
        /// item を手に持つ（null なら素手）。遠距離武器でなければ何も撃たない。switched が真なら、中身が同じでも持ち替えとして待たせる。
        /// 最初の 1 回は待たせない。前の武器の待ちが長ければ引き継ぎ、往復で消させない。
        /// </summary>
        public void Equip(ItemInstance item, bool switched = false)
        {
            if (equipped && item == held && !switched) return;

            Initialize();
            float switchWait = equipped ? Mathf.Max(switchCooldown, cooldownRemaining) : 0f;
            bool wasRanged = heldWeapon != null;
            equipped = true;
            volleys.Clear();

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

            bool pressed = pressQueued;
            pressQueued = false;
            if (pressed && heldWeapon != null && cooldownRemaining <= 0f) Fire();
        }

        private void LateUpdate()
        {
            WeaponTypeDefinition type = heldWeapon != null ? heldWeapon.WeaponType : null;
            UpdateDrawSound(type);
            if (type == null || !Aim.HasValue)
            {
                if (aimRing != null) aimRing.SetVisible(false);
                return;
            }

            Vector3 target = type.RangedKind == RangedAttackKind.Rain ? GroundTarget(type) : AimPoint(type);
            float remaining = AimBody(type, target, aimLocked ? 180f : aimTurnSpeed * Time.deltaTime, true);
            if (Mathf.Abs(remaining) < 1f) aimLocked = true;
            UpdateAimRing(type, target);
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

            EnchantmentTotals enchantments = held != null ? held.EnchantmentTotals() : EnchantmentTotals.Empty;
            float attack = progression != null ? progression.BaseAttack : 0f;
            return heldWeapon.ComputeRangedStats(enchantments, attack, critChance, critMultiplier);
        }

        // ---- 撃つ ----

        private void Fire()
        {
            stats = ComputeStats();
            WeaponTypeDefinition type = heldWeapon.WeaponType;
            cooldownRemaining = cooldownDuration = stats.FireInterval;

            bool rain = type.RangedKind == RangedAttackKind.Rain;
            Vector3 target = rain ? GroundTarget(type) : AimPoint(type);
            // 撃つ瞬間は向き切る。背骨の曲げは前のフレームの LateUpdate のものが手に残っている。
            AimBody(type, target, 360f, false);
            PlayFireAnimation();

            Vector3 muzzle = Muzzle(type);
            PlaySound(type.SwingSound, muzzle, type.SoundVolume);
            if (rain) FireRain(type, stats, muzzle, target);
            else FireBow(type, stats, muzzle, target);
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
            };

            foreach (float angle in RangedPattern.VolleyAngles(shot.ExtraProjectiles + 1, type.VolleySpreadAngle))
            {
                Vector3 arrowDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                ArrowProjectile.Launch(type.ProjectilePrefab, muzzle, arrowDirection, settings,
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
            for (int i = 0; i < shot.ExtraProjectiles; i++) centers.Add(ScatterPoint(type, shot, target));

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
        /// 数で増えた雨の置き場所。狙った所から決めた距離の範囲のランダムな所の地面。
        /// 狙った所との間に壁があれば引き直し、5 回だめなら狙った所に重ねる。
        /// </summary>
        private Vector3 ScatterPoint(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 target)
        {
            Vector3 lift = Vector3.up * 0.5f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                (float x, float z) = RangedPattern.RainOffset(type.RainScatterMin * shot.SizeScale, type.RainScatterMax * shot.SizeScale, random);
                Vector3 candidate = target + new Vector3(x, 0f, z);
                Vector3 path = candidate - target;
                if (path.sqrMagnitude < 1e-6f || !Raycast(new Ray(target + lift, path), path.magnitude, true, out _))
                    return GroundAt(candidate, target.y);
            }

            return target;
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

        /// <summary>矢の出る所。弓を持つ手（人型でなければ武器種の muzzleOffset）。</summary>
        private Vector3 Muzzle(WeaponTypeDefinition type)
        {
            Transform hand = type.HeldInLeftHand ? leftHand : rightHand;
            return hand != null ? hand.position : transform.TransformPoint(type.MuzzleOffset);
        }

        /// <summary>
        /// bend なら、腕の上下の向きが狙い（持続弓は武器種の rainAimPitch）に合うよう、背骨を体の右軸まわりに曲げる（Animator の評価の後に呼ぶこと）。
        /// そのうえで、弓を持つ腕（付け根から手）が水平に target を向くよう、体を最大 maxYaw 度回す。回し残した角度を返す。
        /// 曲げると腕の水平の向きも少し変わるので、曲げてから回す。人型でなければ、体の前を target へ向けるだけ。
        /// </summary>
        private float AimBody(WeaponTypeDefinition type, Vector3 target, float maxYaw, bool bend)
        {
            Transform shoulder = type.HeldInLeftHand ? leftUpperArm : rightUpperArm;
            Transform hand = type.HeldInLeftHand ? leftHand : rightHand;
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

        /// <summary>持続弓を持っている間、狙う地面（target）に範囲の円を出す。</summary>
        private void UpdateAimRing(WeaponTypeDefinition type, Vector3 target)
        {
            bool show = type.RangedKind == RangedAttackKind.Rain && type.RangeRingMaterial != null;
            if (!show)
            {
                if (aimRing != null) aimRing.SetVisible(false);
                return;
            }

            if (aimRing == null) aimRing = RangeRing.Create("AimRing", type.RangeRingMaterial, type.AimRingColor);
            aimRing.SetColor(type.AimRingColor);
            aimRing.SetVisible(true);
            aimRing.Set(target, type.RainRadius * (stats != null ? stats.SizeScale : 1f));
        }

        // ---- 共通 ----

        private void PlayFireAnimation()
        {
            if (animator == null) return;
            if (hasAttackSpeedParam) animator.SetFloat(AttackSpeedParam, Mathf.Max(1f, BowCycleSeconds / Mathf.Max(0.01f, stats.FireInterval)));
            if (hasAttackParam) animator.SetTrigger(AttackParam);
        }

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
            }
        }
    }
}
