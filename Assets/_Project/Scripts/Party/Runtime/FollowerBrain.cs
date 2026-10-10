using System.Collections.Generic;
using TpsDungeon.Combat;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティーの後ろの人の戦い。敵を見つけたら手持ちの 4 枠から武器を選び（<see cref="FollowerWeaponPicker"/>）、
    /// 近接なら列を離れて寄って振り、遠距離なら列に並んだまま撃つ。敵がいなくなるか先頭から離れすぎたら列に戻る。
    /// 攻撃そのものは MeleeAttacker / RangedAttacker に任せ、ここは操作しているキャラの入力（PlayerMeleeInput）の代わりに押すだけ。
    /// 武器ごとの待ちは攻撃の役が覚えている（持ち替えても他の武器へは移らない）。選ぶときは武器ごとの待ちだけを見て、持ち替えの待ちは見ない
    /// （持ち替えた先を「待ち中」と見て、また別の武器へ持ち替え続けないように）。
    /// 持ち替えは PlayerHotbar.Select で行うので、ホットバーの表示・盾の効き（PlayerGear）は先頭のときと同じに動く。
    /// キャラのルートに付ける。パーティーの先頭の間は PartyMember が止める。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Follower Brain")]
    public sealed class FollowerBrain : MonoBehaviour
    {
        private const int OverlapBufferSize = 64;
        private const int RayBufferSize = 16;

        [SerializeField, Min(1f), Tooltip("敵を見つける距離（m）。")]
        private float detectRadius = 14f;

        [SerializeField, Min(1f), Tooltip("狙っている敵がこれより離れたら諦める（m）。")]
        private float loseRadius = 18f;

        [SerializeField, Min(0f), Tooltip("敵がこれより近ければ近接の武器を優先する（m）。")]
        private float closeDistance = 4f;

        [SerializeField, Min(1f), Tooltip("近接で寄るとき、先頭からこれより離れたら追うのをやめて列に戻る（m）。")]
        private float leashDistance = 12f;

        [SerializeField, Min(0.05f), Tooltip("敵を探し直す間隔（秒）。全員が同じフレームに探さないよう、人ごとにずらす。")]
        private float scanInterval = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("治癒の杖・治癒の場は、パーティーの誰かの体力がこの割合を切ったときだけ使う（1 なら少しでも減っていれば使う）。")]
        private float healBelow = 1f;

        [SerializeField, Tooltip("見通しの判定で遮る物とみなすレイヤー（味方は除く）。")]
        private LayerMask sightMask = ~0;

        [SerializeField, Tooltip("狙いの線を出す目の高さ（m）。")]
        private float eyeHeight = 1.5f;

        private readonly Collider[] overlap = new Collider[OverlapBufferSize];
        private readonly RaycastHit[] rayHits = new RaycastHit[RayBufferSize];
        private readonly List<FollowerWeaponOption> options = new List<FollowerWeaponOption>(PlayerHotbar.SlotCount);

        private PartyMember member;
        private PlayerHotbar hotbar;
        private PlayerInventory inventory;
        private MeleeAttacker melee;
        private RangedAttacker ranged;
        private FollowerLocomotion locomotion;

        private EnemyHealth target;
        private float nextScan;

        /// <summary>今狙っている敵。いなければ null。</summary>
        public EnemyHealth Target => target;

        private void Awake()
        {
            member = GetComponent<PartyMember>();
            hotbar = GetComponent<PlayerHotbar>();
            inventory = GetComponent<PlayerInventory>();
            melee = GetComponent<MeleeAttacker>();
            ranged = GetComponent<RangedAttacker>();
            locomotion = GetComponent<FollowerLocomotion>();
            sightMask = AllyLayer.Exclude(sightMask);
        }

        private void OnEnable()
        {
            target = null;
            nextScan = Time.time + Random.value * scanInterval;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            Equip(false);
        }

        private void OnDisable()
        {
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            StopAiming();
            target = null;
        }

        private void OnHotbarChanged(PlayerHotbar _) => Equip(true);

        private void OnInventoryChanged(PlayerInventory _) => Equip(false);

        private ItemInstance Current() => hotbar != null && inventory != null ? inventory.Inventory[hotbar.SelectedIndex] : null;

        private void Equip(bool switched)
        {
            ItemInstance item = Current();
            if (melee != null) melee.Equip(item, switched);
            if (ranged != null) ranged.Equip(item, switched);
        }

        private void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + scanInterval;
                target = KeepOrFindTarget();
            }

            if (target == null || target.IsDead || !WithinLeash())
            {
                StopAiming();
                return;
            }

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 chest = TargetPoint(target);
            float distance = Flat(chest - transform.position).magnitude;

            int slot = ChooseSlot(distance);
            if (slot != hotbar.SelectedIndex) hotbar.Select(slot);

            ItemInstance item = Current();
            WeaponTypeDefinition type = item?.Weapon != null ? item.Weapon.WeaponType : null;
            bool rangedWeapon = type != null && type.IsRanged && !type.IsMelee && !type.IsPassiveGear;
            if (rangedWeapon) Shoot(type, eye, chest, distance);
            else Swing(type, chest, distance);
        }

        /// <summary>item の武器ごとの待ちが明ける時刻（持ち替えの待ちは含まない）。</summary>
        private float ReadyAt(ItemInstance item)
        {
            float remaining = Mathf.Max(melee != null ? melee.WeaponCooldownRemaining(item) : 0f,
                ranged != null ? ranged.WeaponCooldownRemaining(item) : 0f);
            return Time.time + remaining;
        }

        /// <summary>今持っている武器で攻撃できるか（武器ごとの待ちも持ち替えの待ちも明けている）。</summary>
        private bool HeldReady() => (melee == null || melee.CooldownRemaining <= 0f) && (ranged == null || ranged.CooldownRemaining <= 0f);

        private int ChooseSlot(float distance)
        {
            Inventory hand = inventory.Inventory;
            bool needHeal = SomeoneHurt();
            options.Clear();
            for (int i = 0; i < hand.Count; i++)
            {
                FollowerWeaponOption option = FollowerWeaponOption.From(hand[i], ReadyAt(hand[i]));
                WeaponTypeDefinition type = hand[i]?.Weapon != null ? hand[i].Weapon.WeaponType : null;
                if (type != null && (type.IsHealField || type.IsHeal)) option.Usable = needHeal;
                else if (type != null && type.IsBuff) option.Usable = SomeoneUnblessed(type.SupportRange, hand[i].Weapon.BlessingKind);
                options.Add(option);
            }

            // 振っている途中の近接は振り終えるまで持ち替えない。
            if (melee != null && (melee.IsSwinging || melee.IsLunging)) return hotbar.SelectedIndex;
            // 撃った遠距離も、出し終えるまで持ち替えない（持ち替えると召喚・治癒などの予約が消え、待ちだけ残る）。
            if (ranged != null && ranged.IsCasting) return hotbar.SelectedIndex;

            int pick = FollowerWeaponPicker.Pick(options, distance, closeDistance, Time.time, hotbar.SelectedIndex);
            return pick >= 0 ? pick : UnarmedSlot(hand);
        }

        /// <summary>素手で殴れる枠（空きか、お守り・盾などの攻撃に使えない物）。今の枠がそうならそのまま。</summary>
        private int UnarmedSlot(Inventory hand)
        {
            if (!options[hotbar.SelectedIndex].IsAttack) return hotbar.SelectedIndex;
            for (int i = 0; i < hand.Count; i++)
            {
                if (!options[i].IsAttack) return i;
            }

            return hotbar.SelectedIndex;
        }

        private void Swing(WeaponTypeDefinition type, Vector3 targetPoint, float distance)
        {
            if (ranged != null)
            {
                ranged.Aim = null;
                ranged.AttackHeld = false;
            }

            float reach = type != null && type.IsMelee
                ? FollowerWeaponOption.MeleeReach(type)
                : melee != null && melee.HeldWeapon != null ? FollowerWeaponOption.MeleeReach(melee.HeldWeapon.WeaponType) : 1.2f;

            Vector3 toTarget = Flat(targetPoint - transform.position);
            if (locomotion != null)
            {
                locomotion.ChaseTarget = target.transform.position;
                locomotion.ChaseStopDistance = Mathf.Max(0.6f, reach * 0.7f);
                locomotion.FaceMovement = distance > reach;
            }

            if (distance > reach) return;

            if (toTarget.sqrMagnitude > 1e-4f)
            {
                melee.AimForward = toTarget;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), 720f * Time.deltaTime);
            }

            if (HeldReady()) melee.PressAttack();
        }

        private void Shoot(WeaponTypeDefinition type, Vector3 eye, Vector3 chest, float distance)
        {
            if (locomotion != null)
            {
                locomotion.ChaseTarget = null;
                locomotion.FaceMovement = false;
            }

            Vector3 aimAt = chest;
            if (type.IsHealField || type.TargetsAllies) aimAt = transform.position;
            else if (type.AimsAtGround) aimAt = target.transform.position;

            Vector3 direction = aimAt - eye;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;
            ranged.Aim = new Ray(eye, direction.normalized);

            bool inRange = distance <= FollowerWeaponOption.RangedReach(type) + 0.5f && (HeldReady() || ranged.IsBurning);
            ranged.AttackHeld = inRange && type.RangedKind == RangedAttackKind.Flame;
            if (inRange) ranged.PressAttack();
        }

        private void StopAiming()
        {
            if (ranged != null)
            {
                ranged.Aim = null;
                ranged.AttackHeld = false;
            }

            if (locomotion != null)
            {
                locomotion.ChaseTarget = null;
                locomotion.FaceMovement = true;
            }
        }

        private bool WithinLeash()
        {
            Party party = member != null ? member.Party : null;
            if (party == null || party.Leader == null) return false;
            return Vector3.Distance(transform.position, party.Leader.transform.position) <= leashDistance
                   || Vector3.Distance(target.transform.position, party.Leader.transform.position) <= leashDistance;
        }

        private bool SomeoneHurt()
        {
            Party party = member != null ? member.Party : null;
            if (party == null) return false;
            foreach (PartyMember m in party.Members)
            {
                PlayerHealth health = m != null ? m.Health : null;
                if (health != null && !health.IsDead && health.Fraction < healBelow) return true;
            }

            return false;
        }

        /// <summary>自分から range 以内に、kind の加護が付いていない生きた仲間（自分も含む）が居るか。</summary>
        private bool SomeoneUnblessed(float range, BlessingKind kind)
        {
            Party party = member != null ? member.Party : null;
            if (party == null) return false;
            foreach (PartyMember m in party.Members)
            {
                PlayerHealth health = m != null ? m.Health : null;
                if (health == null || health.IsDead) continue;
                if (Vector3.Distance(transform.position, health.transform.position) > range) continue;
                if (!health.TryGetComponent(out CharacterBuffs buffs) || !buffs.Has(kind)) return true;
            }

            return false;
        }

        private EnemyHealth KeepOrFindTarget()
        {
            if (target != null && !target.IsDead
                && Vector3.Distance(transform.position, target.transform.position) <= loseRadius
                && CanSee(target))
            {
                return target;
            }

            EnemyHealth best = null;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, overlap, sightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = overlap[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance >= bestDistance || !CanSee(enemy)) continue;

                best = enemy;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>目から敵の胸まで、壁に遮られていないか（味方と自分は素通し）。</summary>
        private bool CanSee(EnemyHealth enemy)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 to = TargetPoint(enemy) - eye;
            float distance = to.magnitude;
            if (distance < 1e-3f) return true;

            int count = Physics.RaycastNonAlloc(eye, to / distance, rayHits, distance, sightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = rayHits[i].collider;
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(enemy.transform)) continue;
                if (hit.GetComponentInParent<EnemyHealth>() != null) continue;
                return false;
            }

            return true;
        }

        /// <summary>敵の狙う所。当たりの中心（無ければ足元から 1 m 上）。</summary>
        private static Vector3 TargetPoint(EnemyHealth enemy)
        {
            Collider collider = enemy.GetComponent<Collider>();
            return collider != null ? collider.bounds.center : enemy.transform.position + Vector3.up;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
