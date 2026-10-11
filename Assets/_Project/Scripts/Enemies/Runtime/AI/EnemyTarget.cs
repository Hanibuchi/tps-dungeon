using System.Collections.Generic;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵が狙う相手 1 人。パーティーのメンバー（<see cref="PlayerHealth"/>）か、召喚した置物（<see cref="DecoyTarget"/>）のどちらか。
    /// 攻撃が当たったら <see cref="Damage"/> を呼べば、相手の種類に合わせて体力を減らす。
    /// </summary>
    public readonly struct EnemyTarget
    {
        /// <summary>置物の狙う所の高さ（m）。置物は当たり判定を持たないことがあるので、足元から決め打ちで上げる。</summary>
        private const float DecoyAimHeight = 0.8f;

        /// <summary>置物に当たったとみなす、置物の芯からの距離（m）。攻撃の範囲に足す。</summary>
        public const float DecoyRadius = 0.5f;

        public readonly PlayerHealth Character;
        public readonly DecoyTarget Decoy;

        public EnemyTarget(PlayerHealth character)
        {
            Character = character;
            Decoy = null;
        }

        public EnemyTarget(DecoyTarget decoy)
        {
            Character = null;
            Decoy = decoy;
        }

        /// <summary>まだ狙える（居なくなっておらず、倒れても壊れてもいない）。</summary>
        public bool IsValid => Character != null
            ? Character.isActiveAndEnabled && !Character.IsDead
            : Decoy != null && Decoy.isActiveAndEnabled && !Decoy.IsBroken;

        public Transform Transform => Character != null ? Character.transform : Decoy != null ? Decoy.transform : null;

        /// <summary>足元。</summary>
        public Vector3 Position => Transform != null ? Transform.position : Vector3.zero;

        /// <summary>撃つ・見るときに狙う所。当たり判定の中心（無ければ足元から少し上）。</summary>
        public Vector3 AimPoint
        {
            get
            {
                if (Character != null)
                {
                    var collider = Character.GetComponent<Collider>();
                    return collider != null && collider.enabled ? collider.bounds.center : Character.transform.position + Vector3.up;
                }
                return Decoy != null ? Decoy.transform.position + Vector3.up * DecoyAimHeight : Vector3.zero;
            }
        }

        public bool SameAs(EnemyTarget other) => Character == other.Character && Decoy == other.Decoy;

        /// <summary>amount だけ体力を減らす。メンバーなら盾と加護の軽減が掛かる。</summary>
        public void Damage(int amount)
        {
            if (amount <= 0 || !IsValid) return;
            if (Character != null) Character.Damage(amount);
            else Decoy.TakeDamage(amount);
        }

        /// <summary>当たり判定から相手を引く。メンバーでも置物でもなければ false。</summary>
        public static bool TryFrom(Collider collider, out EnemyTarget target)
        {
            target = default;
            if (collider == null) return false;

            var character = collider.GetComponentInParent<PlayerHealth>();
            if (character != null)
            {
                target = new EnemyTarget(character);
                return target.IsValid;
            }

            var decoy = collider.GetComponentInParent<DecoyTarget>();
            if (decoy != null)
            {
                target = new EnemyTarget(decoy);
                return target.IsValid;
            }
            return false;
        }
    }

    /// <summary>
    /// 敵の攻撃の当たりを集める。球の中に居るメンバーと置物を、1 人 1 回ずつ results に入れる。
    /// 置物は当たり判定を持たないことがあるので、当たり判定に加えて <see cref="DecoyTarget.All"/> からも距離で拾う。
    /// </summary>
    public static class EnemyHits
    {
        private static readonly Collider[] overlap = new Collider[32];
        private static readonly RaycastHit[] rayHits = new RaycastHit[16];

        public static void Collect(Vector3 center, float radius, List<EnemyTarget> results)
        {
            results.Clear();

            int count = Physics.OverlapSphereNonAlloc(center, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (EnemyTarget.TryFrom(overlap[i], out EnemyTarget target)) AddOnce(results, target);
            }

            float reach = radius + EnemyTarget.DecoyRadius;
            foreach (DecoyTarget decoy in DecoyTarget.All)
            {
                var target = new EnemyTarget(decoy);
                if (target.IsValid && (target.AimPoint - center).sqrMagnitude <= reach * reach) AddOnce(results, target);
            }
        }

        /// <summary>まだ入っていなければ入れる。入れたら true。</summary>
        public static bool AddOnce(List<EnemyTarget> list, EnemyTarget target)
        {
            foreach (EnemyTarget t in list)
            {
                if (t.SameAs(target)) return false;
            }
            list.Add(target);
            return true;
        }

        /// <summary>from から to まで壁に遮られていないか（メンバー・置物・敵・ignore の中は素通し）。</summary>
        public static bool Clear(Vector3 from, Vector3 to, Transform ignore)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-3f) return true;

            int count = Physics.RaycastNonAlloc(from, delta / distance, rayHits, distance, AllyLayer.Exclude(~0), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsSeeThrough(rayHits[i].collider, ignore)) continue;
                return false;
            }
            return true;
        }

        /// <summary>見通しや弾を遮らない物（自分・ほかの敵・メンバー・置物）か。</summary>
        public static bool IsSeeThrough(Collider collider, Transform ignore)
        {
            if (ignore != null && collider.transform.IsChildOf(ignore)) return true;
            return collider.GetComponentInParent<EnemyHealth>() != null
                || collider.GetComponentInParent<PlayerHealth>() != null
                || collider.GetComponentInParent<DecoyTarget>() != null;
        }
    }
}
