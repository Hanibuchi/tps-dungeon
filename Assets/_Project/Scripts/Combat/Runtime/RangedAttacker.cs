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
    ///   弓     … 狙いの線が当たった所（無ければ射程の先）へ、矢を放つ。数で扇状に増え、多重で遅れて同じ向きへもう一斉射
    ///   持続弓 … 狙いの線が当たった地面（射程の水平距離まで）へ、上に矢を放ってから雨を降らせ、範囲に刻みでダメージ。
    ///            数で狙った所の周りに雨が増え、多重で同じ所にもう一度降る。持っている間は狙う地面に範囲の円を出す
    /// ダメージ・クリティカル・爆発・命中の見た目と音は近接（MeleeAttacker）と同じ作り。当てた 1 回ごとに <see cref="Dealt"/> で知らせる。
    /// 手の見た目と Animator の WeaponType は MeleeAttacker が持ち替えで替えるので、ここは撃つことだけを受け持つ。
    /// キャラのルート（MeleeAttacker と同じ GameObject）に付ける。基礎攻撃力は同じ GameObject の CharacterProgression から読む。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Ranged Attacker")]
    public sealed class RangedAttacker : MonoBehaviour
    {
        // CharacterAnimatorBuilder（エディタ専用アセンブリ）が焼き込んだ値。向こうを変えたらここも揃えること。
        private const string AttackParam = "Attack";
        private const string AttackSpeedParam = "AttackSpeed";

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

        /// <summary>雨の 1 刻みの命中の見た目の大きさ（武器種の命中の見た目に対して）。</summary>
        private const float TickHitEffectRatio = 0.5f;

        [SerializeField, Tooltip("キャラの Animator。未設定なら子から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("矢と照準が当たるレイヤー。")]
        private LayerMask hitMask = ~0;

        [SerializeField, Min(0f), Tooltip("持ち替えてから撃てるまでの秒数。全武器共通（MeleeAttacker の switchCooldown と揃える）。")]
        private float switchCooldown = 0.5f;

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

        // 同じフレームに何本当たっても、命中・クリティカルの音は 1 回だけ鳴らす。
        private int hitSoundFrame = -1;
        private int criticalSoundFrame = -1;

        private readonly List<PendingVolley> volleys = new List<PendingVolley>();
        private readonly HashSet<EnemyHealth> explosionTargets = new HashSet<EnemyHealth>();
        private readonly Collider[] overlap = new Collider[32];
        private readonly RaycastHit[] rayHits = new RaycastHit[32];

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
        }

        private void OnDisable()
        {
            volleys.Clear();
            pressQueued = false;
            aimRing?.SetVisible(false);
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

        private void LateUpdate() => UpdateAimRing();

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
            FaceToward(target);
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

        /// <summary>一斉射 1 回。本撃と数で増えた矢を、direction を中心に扇状に放つ。</summary>
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

            foreach (float angle in SlamPattern.SpreadAngles(shot.ExtraProjectiles + 1, type.VolleySpreadAngle))
            {
                Vector3 arrowDirection = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                ArrowProjectile.Launch(type.ProjectilePrefab, muzzle, arrowDirection, settings,
                    (enemy, point, travel) => ArrowHit(type, shot, enemy, point, travel));
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
            Quaternion facing = Quaternion.LookRotation(toward, Vector3.up);
            (float x, float z)[] offsets = RangedPattern.RainOffsets(shot.ExtraProjectiles + 1, type.RainSpreadDistance * shot.SizeScale);
            var centers = new Vector3[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                centers[i] = i == 0 ? target : GroundAt(target + facing * new Vector3(offsets[i].x, 0f, offsets[i].z), target.y);

            foreach (float delay in RangedPattern.RepeatDelays(shot.MultishotCount, type.RainRepeatInterval))
            {
                foreach (Vector3 center in centers)
                {
                    ArrowRainZone.Spawn(center, new ArrowRainZone.Settings
                    {
                        Radius = radius,
                        Height = type.RainHeight,
                        StartDelay = startDelay + delay,
                        TickInterval = type.RainTickInterval,
                        TickCount = shot.RainTickCount,
                        ArrowsPerSecond = type.RainArrowsPerSecond * radius,
                        ArrowPrefab = type.ProjectilePrefab,
                        RingMaterial = type.RangeRingMaterial,
                        RingColor = type.RainRingColor,
                        HitMask = hitMask,
                        IgnoreRoot = transform,
                        Forward = toward,
                    }, (c, r, h) => RainTick(type, shot, c, r, h));
                }
            }
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

        /// <summary>雨の 1 刻み。円柱の中の生きた敵みんなに刻みのダメージ（クリティカルは毎刻み引く）。押し出しはエンチャントの分だけ。</summary>
        private void RainTick(WeaponTypeDefinition type, RangedWeaponStats shot, Vector3 center, float radius, float height)
        {
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

        private Vector3 Muzzle(WeaponTypeDefinition type) => transform.TransformPoint(type.MuzzleOffset);

        private void FaceToward(Vector3 target)
        {
            Vector3 forward = target - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>持続弓を持っている間、狙う地面に範囲の円を出す。</summary>
        private void UpdateAimRing()
        {
            WeaponTypeDefinition type = heldWeapon != null ? heldWeapon.WeaponType : null;
            bool show = type != null && type.RangedKind == RangedAttackKind.Rain && Aim.HasValue && type.RangeRingMaterial != null;
            if (!show)
            {
                aimRing?.SetVisible(false);
                return;
            }

            if (aimRing == null) aimRing = RangeRing.Create("AimRing", type.RangeRingMaterial, type.AimRingColor);
            aimRing.SetColor(type.AimRingColor);
            aimRing.SetVisible(true);
            aimRing.Set(GroundTarget(type), type.RainRadius * (stats != null ? stats.SizeScale : 1f));
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

        private void CacheAnimatorParameters()
        {
            if (animator == null) return;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == AttackParam) hasAttackParam = true;
                else if (p.name == AttackSpeedParam) hasAttackSpeedParam = true;
            }
        }
    }
}
