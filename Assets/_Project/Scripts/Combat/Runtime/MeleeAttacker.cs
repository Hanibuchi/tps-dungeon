using System;
using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// ホットバーで持っている近接武器で攻撃する。攻撃キーを押すたびにコンボを 1 段ずつ進め（MeleeComboState）、
    /// 段ごとの判定の瞬間に前方の箱の中の敵へダメージを与え、振りと命中のエフェクトを出す。
    /// 数値とエフェクトは武器（WeaponDefinition）と武器種（WeaponTypeDefinition）から出す。
    /// 持ち替えたら Animator の WeaponType と手の見た目も替える。素手や武器でない物を持っているときは素手の武器（unarmedWeapon）で殴る。
    /// 近接でない武器（弓など）はここでは振らない。
    /// プレイヤーのルート（PlayerInput・PlayerHotbar・PlayerInventory と同じ GameObject）に付ける。
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

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("攻撃のアクション名。")]
        private string attackActionName = "Player/Attack";

        [SerializeField, Tooltip("キャラの Animator。未設定なら子から探す。")]
        private Animator animator;

        [SerializeField, Tooltip("攻撃判定が当たるレイヤー。")]
        private LayerMask hitMask = ~0;

        [SerializeField, Tooltip("段を振り始めるときに、体をカメラの向きへ向ける。")]
        private bool faceCameraOnAttack = true;

        [SerializeField, Tooltip("素手や武器でない物を持っているときに振る武器（インベントリには入れない）。未設定なら素手では攻撃しない。")]
        private WeaponDefinition unarmedWeapon;

        [SerializeField, Tooltip("クリティカルのとき、命中のエフェクトに重ねて出す見た目（任意）。")]
        private GameObject criticalHitEffect;

        [SerializeField, Min(0.01f)]
        private float criticalHitEffectScale = 1f;

        [SerializeField, Tooltip("爆発のエンチャントで出す見た目（任意）。出して数秒で消す。")]
        private GameObject explosionEffect;

        [SerializeField, Min(0.01f)]
        private float explosionEffectScale = 1f;

        [SerializeField, Tooltip("当てたダメージをコンソールに出す（調整用）。")]
        private bool logHits;

        private PlayerHotbar hotbar;
        private PlayerInventory inventory;
        private CharacterProgression progression;
        private InputAction attackAction;
        private Transform cameraTransform;

        private bool refreshed;
        private ItemInstance held;
        private WeaponDefinition heldWeapon;
        private GameObject heldModel;
        private MeleeComboState combo;
        private MeleeWeaponStats stats;

        private readonly HashSet<EnemyHealth> hitThisSwing = new HashSet<EnemyHealth>();
        private readonly HashSet<EnemyHealth> explosionTargets = new HashSet<EnemyHealth>();
        private readonly Collider[] overlap = new Collider[32];

        private bool hasWeaponTypeParam;
        private bool hasAttackParam;
        private bool hasComboStepParam;
        private bool hasAttackSpeedParam;

        /// <summary>今振る武器（素手なら unarmedWeapon）。振れなければ null。</summary>
        public WeaponDefinition HeldWeapon => heldWeapon;

        /// <summary>今振っている段（0 始まり）。振っていなければ -1。</summary>
        public int ComboStep => combo != null ? combo.Step : -1;

        /// <summary>判定を出した。(段, 当てた敵の数)。</summary>
        public event Action<int, int> Swung;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            hotbar = GetComponent<PlayerHotbar>();
            inventory = GetComponent<PlayerInventory>();
            progression = GetComponent<CharacterProgression>();
            CacheAnimatorParameters();
        }

        private void OnEnable()
        {
            if (playerInput != null && playerInput.actions != null)
            {
                attackAction = playerInput.actions.FindAction(attackActionName);
                if (attackAction == null) Debug.LogWarning($"アクション '{attackActionName}' が {playerInput.actions.name} に無い", this);
            }

            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            RefreshHeld();
        }

        private void OnDisable()
        {
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            attackAction = null;
        }

        private void OnHotbarChanged(PlayerHotbar _) => RefreshHeld();

        private void OnInventoryChanged(PlayerInventory _) => RefreshHeld();

        private void Update()
        {
            if (combo == null) return;

            bool pressed = attackAction != null && attackAction.WasPressedThisFrame()
                           && playerInput != null && attackAction.actionMap == playerInput.currentActionMap;

            if (pressed && !combo.IsSwinging) stats = ComputeStats();
            float speed = stats != null ? stats.AttackSpeed : 1f;

            // 先行入力があると、判定と次の段の始まりが同じフレームに来ることがある。判定は進める前の段で出す。
            int swinging = combo.Step;
            ComboEvents events = combo.Tick(Time.deltaTime, pressed, speed);
            if ((events & ComboEvents.Hit) != 0) DealHit(swinging >= 0 ? swinging : combo.Step);
            if ((events & ComboEvents.StepStarted) != 0) BeginStep(combo.Step);
        }

        // ---- 持ち替え ----

        private void RefreshHeld()
        {
            ItemInstance current = hotbar != null && inventory != null ? inventory.Inventory[hotbar.SelectedIndex] : null;
            if (refreshed && current == held) return;

            refreshed = true;
            held = current;
            WeaponDefinition weapon = current?.Weapon;
            bool isWeapon = weapon != null && weapon.WeaponType != null;
            heldWeapon = IsMelee(weapon) ? weapon : !isWeapon && IsMelee(unarmedWeapon) ? unarmedWeapon : null;

            combo = heldWeapon != null ? new MeleeComboState(Timings(heldWeapon.WeaponType), heldWeapon.WeaponType.ComboChainGrace) : null;
            stats = heldWeapon != null ? ComputeStats() : null;

            WeaponDefinition pose = isWeapon ? weapon : heldWeapon;
            if (animator != null && hasWeaponTypeParam)
                animator.SetInteger(WeaponTypeParam, pose != null ? pose.WeaponType.AnimatorWeaponType : 0);

            AttachHeldModel(weapon);
        }

        private static bool IsMelee(WeaponDefinition weapon) => weapon != null && weapon.WeaponType != null && weapon.WeaponType.IsMelee;

        private static List<ComboStepTiming> Timings(WeaponTypeDefinition type)
        {
            var result = new List<ComboStepTiming>();
            foreach (MeleeComboStep step in type.ComboSteps) result.Add(new ComboStepTiming(step.duration, step.hitTime));
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
            EnchantmentTotals enchantments = held != null && held.Weapon == heldWeapon ? held.EnchantmentTotals() : EnchantmentTotals.Empty;
            float attack = progression != null ? progression.BaseAttack : 0f;
            return heldWeapon.ComputeMeleeStats(enchantments, attack, critChance, critMultiplier);
        }

        private void AttachHeldModel(WeaponDefinition weapon)
        {
            if (heldModel != null) Destroy(heldModel);
            heldModel = null;

            if (weapon == null || weapon.HeldModel == null || animator == null || !animator.isHuman) return;

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;

            heldModel = Instantiate(weapon.HeldModel, hand);
            heldModel.name = weapon.HeldModel.name;
            WeaponTypeDefinition type = weapon.WeaponType;
            heldModel.transform.SetLocalPositionAndRotation(
                type != null ? type.HeldLocalPosition : Vector3.zero,
                type != null ? type.HeldLocalRotation : Quaternion.identity);
            foreach (Collider c in heldModel.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        // ---- 振る ----

        private void BeginStep(int step)
        {
            hitThisSwing.Clear();
            if (faceCameraOnAttack) FaceCamera();

            if (animator == null) return;
            if (hasComboStepParam) animator.SetInteger(ComboStepParam, step);
            if (hasAttackSpeedParam) animator.SetFloat(AttackSpeedParam, stats != null ? stats.AttackSpeed : 1f);
            if (hasAttackParam) animator.SetTrigger(AttackParam);
        }

        private void FaceCamera()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform == null) return;

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void DealHit(int step)
        {
            if (heldWeapon == null || stats == null || step < 0) return;

            WeaponTypeDefinition type = heldWeapon.WeaponType;
            MeleeComboStep shape = type.ComboSteps[step];
            HitBox(shape, out Vector3 center, out Vector3 halfExtents);
            OneShotEffect.Spawn(type.SwingEffect, transform.TransformPoint(type.SwingEffectOffset),
                transform.rotation * Quaternion.Euler(shape.swingEffectEuler), type.SwingEffectScale);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlap, transform.rotation, hitMask, QueryTriggerInteraction.Ignore);

            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !hitThisSwing.Add(enemy)) continue;

                Vector3 point = overlap[i].ClosestPoint(center);
                bool critical = UnityEngine.Random.value < stats.CritChance;
                int damage = stats.HitDamage(step);
                if (critical) damage = stats.ApplyCritical(damage);

                Vector3 direction = enemy.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 1e-6f) direction = transform.forward;

                int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction, critical,
                    shape.knockback + stats.KnockbackBonus, stats.ReactionScale));
                hits++;
                Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up);
                OneShotEffect.Spawn(type.HitEffect, point, facing, type.HitEffectScale);
                if (critical) OneShotEffect.Spawn(criticalHitEffect, point, facing, criticalHitEffectScale);
                if (logHits) Debug.Log($"{step + 1} 段目 → {enemy.name}: {dealt}{(critical ? "（クリティカル）" : string.Empty)}", enemy);

                Explode(point, stats.ExplosionDamage(damage));
            }

            Swung?.Invoke(step, hits);
        }

        /// <summary>爆発のエンチャント。範囲の敵（当てた敵も含む）に追加のダメージ。爆発からは爆発しない。</summary>
        private void Explode(Vector3 point, int damage)
        {
            if (damage <= 0) return;

            OneShotEffect.Spawn(explosionEffect, point, Quaternion.identity, explosionEffectScale);

            explosionTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, stats.ExplosionRadius, overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !explosionTargets.Add(enemy)) continue;

                Vector3 direction = enemy.transform.position - point;
                int dealt = enemy.TakeDamage(new DamageInfo(damage, point, direction));
                if (logHits) Debug.Log($"爆発 → {enemy.name}: {dealt}", enemy);
            }
        }

        /// <summary>段の判定の箱（ワールド）。サイズのエンチャントで大きさと前への伸びが増える。</summary>
        private void HitBox(MeleeComboStep shape, out Vector3 center, out Vector3 halfExtents)
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

            HitBox(steps[step], out Vector3 center, out Vector3 halfExtents);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
