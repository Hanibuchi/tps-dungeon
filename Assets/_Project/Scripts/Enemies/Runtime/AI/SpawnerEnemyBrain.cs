using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 手下を呼ぶ敵（08 スポナー）。5 層のボス。
    ///
    /// 相手が見えていないか遠ければ寄り、間合い（<see cref="maxRange"/>）に入ったらその場に構える。下がりはしない（玉座の主は退かない）。
    /// 相手が近すぎる（<see cref="slamRange"/>）と、呼ぶより先に溜めて（<see cref="slamWindup"/> 秒）周りを叩き潰す（<see cref="slamRadius"/>、強いダメージ）。
    /// 溜めの間に離れれば避けられる。殴るアニメーションと叩き潰しの音は、振り下ろしと音の山が叩き潰す瞬間に来るよう、溜めの終わりから逆算して流す
    /// （<see cref="slamAnimationLead"/>・<see cref="slamClipLead"/>）。
    /// 相手が見えていて、呼んだ手下が上限（<see cref="maxAlive"/>）より少なければ、止まって溜め（<see cref="windup"/> 秒）、
    /// 自分の周り（<see cref="summonRadius"/>）の NavMesh の上に手下（<see cref="minions"/> から等確率）を呼ぶ → <see cref="cooldown"/> 秒は呼ばない。
    /// 溜め始めに手下の出る所を決めて、そこに魔法陣（<see cref="summonCircle"/>）を広げておき、溜め終わりにその上へ手下を出す。
    /// ボスの段階（<see cref="BossPhases"/>）が上がると、1 回に呼ぶ数と上限が増え、強い手下（<see cref="elites"/>）も混ぜ、合間に弾も撃つ。
    /// 呼んだ手下は経験値も武器も落とさない（<see cref="EnemyHealth.IsSummoned"/>。呼び続けて稼げないように）。本体が倒れたら手下も倒れる。
    /// 溜めの間のスタン・気絶で取り消す。
    /// 敵のプレハブのルートに付ける。強さの値は Enemies.csv から入る（EnemyStatsWriter）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Spawner)")]
    public sealed class SpawnerEnemyBrain : EnemyBrain
    {
        [Header("立ち位置")]
        [SerializeField, Min(1f), Tooltip("これより遠いと寄る（m）。")]
        private float maxRange = 14f;

        [Header("叩き潰し（近づきすぎた相手へ）")]
        [SerializeField, Min(0.5f), Tooltip("相手の足元がこの距離に入ったら叩き潰す（m）。")]
        private float slamRange = 3.5f;

        [SerializeField, Min(0.5f), Tooltip("叩き潰しの届く半径（m）。中心ほど痛い。")]
        private float slamRadius = 4.5f;

        [SerializeField, Min(0f), Tooltip("叩き潰すまでの溜め（秒）。この間に離れれば避けられる。")]
        private float slamWindup = 0.9f;

        [SerializeField, Min(0f), Tooltip("叩き潰してから次に叩き潰せるまで（秒）。")]
        private float slamCooldown = 3f;

        [SerializeField, Min(0f), Tooltip("叩き潰しの中心でのダメージ（1 回の攻撃のダメージに掛ける）。")]
        private float slamDamageScale = 4f;

        [SerializeField, Range(0f, 1f), Tooltip("叩き潰しの縁でのダメージの割合。")]
        private float slamEdgeScale = 0.4f;

        [SerializeField, Tooltip("叩き潰しの見た目（地面に出す）。")]
        private GameObject slamEffect;

        [SerializeField, Min(0.01f)]
        private float slamEffectScale = 1.2f;

        [SerializeField, Tooltip("叩き潰しの溜めの間、頭の上に出す予兆。")]
        private GameObject slamWindupEffect;

        [SerializeField, Tooltip("叩き潰しの音。")]
        private AudioClip slamClip;

        [SerializeField, Tooltip("叩き潰しの溜めの音（溜め始めに鳴らす）。未設定なら鳴らさない（呼ぶ溜めの音は魔法陣の音なので使わない）。")]
        private AudioClip slamWindupClip;

        [SerializeField, Min(0f), Tooltip("叩き潰す何秒前に殴るアニメーション（Attack）を流すか。Punch の振り下ろし（0.38 秒目）と移りの 0.1 秒の分。")]
        private float slamAnimationLead = 0.45f;

        [SerializeField, Min(0f), Tooltip("叩き潰す何秒前に叩き潰しの音を鳴らすか。音の頭から一番大きい所までの長さ（重いキック1 は 0.4 秒）。")]
        private float slamClipLead = 0.4f;

        [Header("呼ぶ")]
        [SerializeField, Tooltip("呼ぶ手下のプレハブ。等確率で選ぶ。")]
        private GameObject[] minions = new GameObject[0];

        [SerializeField, Min(0f), Tooltip("止まってから呼ぶまで（秒）。")]
        private float windup = 1.2f;

        [SerializeField, Min(1f), Tooltip("手下を出す、自分の周りの半径（m）。")]
        private float summonRadius = 5f;

        [SerializeField, Tooltip("段階 1〜3 で 1 回に呼ぶ数。")]
        private int[] summonCounts = { 2, 3, 4 };

        [SerializeField, Tooltip("段階 1〜3 で、生きている手下の上限。これ以上は呼ばない。")]
        private int[] maxAlive = { 4, 6, 8 };

        [SerializeField, Tooltip("手下を出した所に出す効果。")]
        private GameObject summonEffect;

        [SerializeField, Min(0.01f)]
        private float summonEffectScale = 1f;

        [SerializeField, Tooltip("呼ぶ溜めの間、手下の出る所の地面に広げる魔法陣。自分では消えない素材でもよい（薄くして消す）。")]
        private GameObject summonCircle;

        [SerializeField, Min(0.01f), Tooltip("魔法陣の大きさの倍率。強い手下の所は 1.5 倍。")]
        private float summonCircleScale = 0.5f;

        [SerializeField, Min(0f), Tooltip("手下が出てから魔法陣を残しておく時間（秒）。そのあと 0.6 秒で薄れて消える。")]
        private float summonCircleLinger = 0.4f;

        [Header("ボスの段階（0 なら使わない）")]
        [SerializeField, Min(0), Tooltip("この段階から、呼ぶたびに強い手下を 1 体混ぜる（生きている数が eliteMax まで）。")]
        private int eliteStage;

        [SerializeField, Tooltip("強い手下のプレハブ。等確率で選ぶ。")]
        private GameObject[] elites = new GameObject[0];

        [SerializeField, Min(0), Tooltip("生きている強い手下の上限。")]
        private int eliteMax = 1;

        [SerializeField, Min(0), Tooltip("この段階から、呼ぶ合間に弾を撃つ。")]
        private int shotStage;

        [SerializeField, Min(0.3f), Tooltip("弾を撃つ間（秒）。")]
        private float shotInterval = 2.5f;

        [SerializeField, Range(0f, 2f), Tooltip("弾のダメージ（1 回の攻撃のダメージに掛ける）。")]
        private float shotDamageScale = 0.5f;

        [Header("弾")]
        [SerializeField, Min(1f)]
        private float projectileSpeed = 11f;

        [SerializeField, Min(0.01f)]
        private float projectileRadius = 0.3f;

        [SerializeField]
        private GameObject projectileEffect;

        [SerializeField, Min(0.01f)]
        private float projectileEffectScale = 0.6f;

        [SerializeField]
        private GameObject impactEffect;

        [SerializeField, Min(0.01f)]
        private float impactEffectScale = 0.6f;

        private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
        private readonly List<EnemyTarget> slamHits = new List<EnemyTarget>();
        private readonly List<EnemyHealth> aliveElites = new List<EnemyHealth>();
        private readonly List<(Vector3 at, bool elite)> pendingSpawns = new List<(Vector3, bool)>();
        private readonly List<EffectFadeOut> circles = new List<EffectFadeOut>();
        private bool summoning;
        private bool slamming;
        private bool slamAnimated;
        private bool slamSounded;
        private float timer;
        private float shotTimer;
        private float slamReady;
        private GameObject activeSlamWindup;
        private RangedStance stance;

        /// <summary>今生きている呼んだ手下の数。</summary>
        public int AliveMinions
        {
            get
            {
                Prune();
                return alive.Count;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            summoning = false;
            slamming = false;
            CancelSummon();
            shotTimer = shotInterval;
        }

        protected override void Think(float dt)
        {
            // 下がらない（近すぎても構えたまま。近ければ叩き潰す）。
            stance = HasTarget
                ? EnemyAttackRules.Stance(TargetDistance, TargetVisible, 0f, maxRange)
                : RangedStance.Approach;

            if (slamming)
            {
                Motor.Destination = null;
                if (HasTarget) Motor.LookAt = Target.Position;
                timer -= dt;
                SlamCues();
                if (timer <= 0f) Slam();
                return;
            }

            // 近づきすぎた相手は、呼んでいる途中でも取りやめて叩き潰す。
            if (HasTarget && TargetVisible && TargetDistance <= slamRange && Time.time >= slamReady)
            {
                if (summoning)
                {
                    summoning = false;
                    StopWindupEffect();
                    CancelSummon();
                }
                StartSlam();
                return;
            }

            if (summoning)
            {
                Motor.Destination = null;
                if (HasTarget) Motor.LookAt = Target.Position;
                timer -= dt;
                if (timer <= 0f) Summon();
                return;
            }

            if (StageReached(shotStage) && HasTarget && TargetVisible)
            {
                shotTimer -= dt;
                if (shotTimer <= 0f)
                {
                    shotTimer = Scaled(shotInterval);
                    Shoot();
                }
            }

            switch (stance)
            {
                case RangedStance.Approach:
                    Motor.LookAt = null;
                    Motor.Destination = ChasePoint();
                    Motor.StopDistance = TargetVisible ? maxRange * 0.8f : 0.5f;
                    break;

                default:
                    Motor.Destination = null;
                    Motor.LookAt = Target.Position;
                    break;
            }

            if (HasTarget && TargetVisible && CooldownRemaining <= 0f && AliveMinions < ByStage(maxAlive, 4)) StartSummon();
        }

        private void StartSummon()
        {
            summoning = true;
            timer = Scaled(windup);
            Motor.Destination = null;
            PlayAttack();
            PlaySound(windupClip, Center);
            StartWindupEffect(HeadPoint);

            // 出る所を今決めて、溜めの間そこに魔法陣を広げておく（溜め終わりにその上へ出す）。
            Prune();
            int room = ByStage(maxAlive, 4) - alive.Count;
            int count = Mathf.Min(ByStage(summonCounts, 2), room);
            bool elite = StageReached(eliteStage) && elites.Length > 0 && aliveElites.Count < eliteMax && count > 0;
            pendingSpawns.Clear();
            for (int i = 0; i < count; i++)
            {
                bool useElite = elite && i == 0;
                Vector3 at = SpawnPoint();
                pendingSpawns.Add((at, useElite));
                OpenCircle(at, useElite);
            }
        }

        private void Summon()
        {
            summoning = false;
            Motor.LookAt = null;
            CooldownRemaining = Cooldown;
            StopWindupEffect();

            foreach ((Vector3 at, bool useElite) in pendingSpawns)
            {
                GameObject prefab = Pick(useElite ? elites : minions);
                if (prefab == null) continue;
                EnemyHealth spawned = SpawnMinion(prefab, at);
                if (spawned == null) continue;
                alive.Add(spawned);
                if (useElite) aliveElites.Add(spawned);
            }
            pendingSpawns.Clear();
            circles.Clear(); // 開いたときに付けた時間どおり、少し残ってから薄れて消える。
            PlaySound(attackClip, Center);
        }

        /// <summary>手下の出る所に魔法陣を広げる。溜めの間と、出てから少しのあいだ見せて、薄くして消す。</summary>
        private void OpenCircle(Vector3 at, bool elite)
        {
            if (summonCircle == null) return;
            GameObject circle = Instantiate(summonCircle, at + Vector3.up * 0.03f, Quaternion.Euler(0f, Random.value * 360f, 0f));
            EnemyEffects.Prepare(circle, summonCircleScale * (elite ? 1.5f : 1f), false);
            EffectFadeOut fader = EffectFadeOut.Attach(circle, Scaled(windup) + summonCircleLinger, 0.6f);
            if (fader != null) circles.Add(fader);
        }

        /// <summary>呼ぶのを取りやめた（叩き潰しに切り替えた・スタンした・倒れた）。出す予定の所の魔法陣をすぐ薄くする。</summary>
        private void CancelSummon()
        {
            pendingSpawns.Clear();
            foreach (EffectFadeOut circle in circles)
                if (circle != null) circle.FadeNow();
            circles.Clear();
        }

        private void StartSlam()
        {
            slamming = true;
            timer = Scaled(slamWindup);
            Motor.Destination = null;
            Motor.LookAt = Target.Position;
            // 殴るアニメーションと叩き潰しの音は、溜めの終わりに合わせて SlamCues で流す。
            slamAnimated = false;
            slamSounded = false;
            PlaySound(slamWindupClip, Center);
            SlamCues();
            if (slamWindupEffect != null)
            {
                activeSlamWindup = Instantiate(slamWindupEffect, HeadPoint, transform.rotation, transform);
                EnemyEffects.Prepare(activeSlamWindup, 1.5f, true);
            }
        }

        /// <summary>
        /// 溜めの残りが lead を切ったら、殴るアニメーションと叩き潰しの音を流す。振り下ろしと音の一番大きい所が、叩き潰す瞬間に重なる。
        /// 段階が上がって溜めが lead より短くなったら、溜め始めにすぐ流す。
        /// </summary>
        private void SlamCues()
        {
            if (!slamAnimated && timer <= slamAnimationLead)
            {
                slamAnimated = true;
                Play(AttackParam);
            }
            if (!slamSounded && timer <= slamClipLead)
            {
                slamSounded = true;
                PlaySound(slamClip != null ? slamClip : attackClip, EnemyMotor.OnNavMesh(transform.position, 3f));
            }
        }

        /// <summary>足元から周りを叩き潰す。中心ほど痛く、壁の向こうと、跳んで高い所に居る相手には当たらない。</summary>
        private void Slam()
        {
            slamming = false;
            slamReady = Time.time + Scaled(slamCooldown);
            Motor.LookAt = null;
            StopSlamWindup();

            Vector3 ground = EnemyMotor.OnNavMesh(transform.position, 3f);
            Vector3 origin = ground + Vector3.up * 0.5f;
            EnemyEffects.Spawn(slamEffect, ground, Quaternion.identity, slamEffectScale, 3f);
            if (!slamSounded) PlaySound(slamClip != null ? slamClip : attackClip, ground);

            int center = Mathf.RoundToInt(damage * slamDamageScale);
            EnemyHits.Collect(origin, slamRadius + EnemyTarget.DecoyRadius, slamHits);
            foreach (EnemyTarget target in slamHits)
            {
                Vector3 aim = target.AimPoint;
                if (!EnemyHits.Clear(origin, aim, transform)) continue;
                if (target.Position.y - ground.y > 1.5f) continue;
                int dealt = EnemyAttackRules.ExplosionDamage(center, Vector3.Distance(origin, aim), slamRadius, slamEdgeScale);
                if (dealt > 0)
                {
                    target.Damage(dealt, gameObject);
                    PlaySound(hitClip, aim);
                    SpawnHitEffect(aim);
                }
            }
        }

        private void StopSlamWindup()
        {
            if (activeSlamWindup == null) return;
            EnemyEffects.Release(activeSlamWindup, 0.6f);
            activeSlamWindup = null;
        }

        /// <summary>自分の周り（summonRadius）の NavMesh の上で、壁の向こうでない所。見つからなければ自分の足元。</summary>
        private Vector3 SpawnPoint()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(summonRadius * 0.4f, summonRadius);
                Vector3 p = transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                // 壁の向こう（隣の部屋）には出さない。
                if (NavMesh.Raycast(transform.position, hit.position, out _, NavMesh.AllAreas)) continue;
                return hit.position;
            }
            return transform.position;
        }

        private EnemyHealth SpawnMinion(GameObject prefab, Vector3 at)
        {
            Quaternion rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            GameObject minion = Instantiate(prefab, at, rotation, transform.parent);
            minion.name = $"{prefab.name}_Summoned";
            EnemyEffects.Spawn(summonEffect, at, Quaternion.identity, summonEffectScale, 2f);
            if (!minion.TryGetComponent(out EnemyHealth health)) return null;
            health.MarkSummoned();
            return health;
        }

        private void Shoot()
        {
            Vector3 muzzle = Center + transform.forward * 0.6f;
            Vector3 direction = Target.AimPoint - muzzle;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;
            EnemyProjectile.Launch(projectileEffect, projectileEffectScale, muzzle, direction, new EnemyProjectile.Settings
            {
                Speed = projectileSpeed,
                Range = maxRange * 1.5f,
                Radius = projectileRadius,
                Damage = Mathf.Max(1, Mathf.RoundToInt(damage * shotDamageScale)),
                Owner = transform,
                HitEffect = impactEffect,
                HitEffectScale = impactEffectScale,
                HitClip = hitClip,
                Volume = volume,
            });
            PlaySound(attackClip, muzzle);
        }

        /// <summary>倒れたら、呼んだ手下もみな倒れる。</summary>
        protected override void OnDeath()
        {
            StopSlamWindup();
            CancelSummon();
            Prune();
            foreach (EnemyHealth minion in alive.ToArray())
            {
                if (minion != null && !minion.IsDead) minion.TakeDamage(new DamageInfo(minion.CurrentHp, null, Vector3.up));
            }
            alive.Clear();
            aliveElites.Clear();
        }

        private void Prune()
        {
            alive.RemoveAll(h => h == null || h.IsDead);
            aliveElites.RemoveAll(h => h == null || h.IsDead);
        }

        private int ByStage(int[] values, int fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            return Mathf.Max(0, values[Mathf.Clamp(Stage - 1, 0, values.Length - 1)]);
        }

        private static GameObject Pick(GameObject[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[Random.Range(0, pool.Length)];
        }

        public override string StateLabel
        {
            get
            {
                if (slamming) return $"叩き潰し {timer:0.0}s";
                if (summoning) return $"呼ぶ {timer:0.0}s";
                if (!HasTarget) return "待つ";
                return $"手下 {alive.Count}/{ByStage(maxAlive, 4)}";
            }
        }

        protected override void DrawRanges()
        {
            Disc(transform.position, slamRange, new Color(1f, 0.1f, 0.1f, 0.9f));
            Disc(transform.position, slamRadius, new Color(1f, 0.5f, 0.1f, 0.6f));
            Disc(transform.position, maxRange, new Color(1f, 0.3f, 0.2f, 0.9f));
            Disc(transform.position, summonRadius, new Color(0.8f, 0.4f, 1f, 0.7f));
        }

        protected override void Interrupted()
        {
            if (slamming)
            {
                slamming = false;
                StopSlamWindup();
                Motor.LookAt = null;
                slamReady = Time.time + Scaled(slamCooldown) * 0.5f;
            }
            if (!summoning) return;
            summoning = false;
            CancelSummon();
            Motor.LookAt = null;
            CooldownRemaining = Mathf.Max(CooldownRemaining, Cooldown * 0.5f);
        }
    }
}
