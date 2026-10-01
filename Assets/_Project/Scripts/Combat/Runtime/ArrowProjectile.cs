using System;
using System.Collections.Generic;
using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 飛んでいる矢 1 本。重力は無く、まっすぐ（ホーミングなら敵へ曲がりながら）飛び、毎フレーム進む分だけ球を流して当たりを見る。
    /// 生きた敵に当たったら onHit を呼ぶ（1 本が同じ敵に当たるのは 1 回）。貫通の数だけ抜けて飛び続け、尽きたらそこで消える。
    /// 敵でない物（壁・床）に当たったら刺さって、しばらくして消える。死んだ敵の体はすり抜ける。
    /// 見た目だけの矢（矢の雨・上へ放つ矢）は敵をすり抜けて、床に刺さるだけ。
    /// 見た目のプレハブは「原点が矢の先、柄が -Z」に置く（刺さったときに先が当たった面に来るように）。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ArrowProjectile : MonoBehaviour
    {
        /// <summary>刺さってから消えるまでの秒数。</summary>
        private const float StuckLifetime = 2f;

        /// <summary>刺さるときに面へめり込ませる深さ（m）。</summary>
        private const float StuckDepth = 0.12f;

        /// <summary>ホーミングで追う敵を選び直す間隔（秒）。</summary>
        private const float RetargetInterval = 0.15f;

        /// <summary>ホーミングで追う敵の、矢の向きからの角度の上限（度）。前にいる敵だけを追う。</summary>
        private const float HomingConeDegrees = 70f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Nearby = new Collider[32];

        /// <summary>矢の飛び方。</summary>
        public struct Settings
        {
            public float Speed;
            public float Range;
            public float Radius;

            /// <summary>貫ける敵の数。0 なら最初の敵で止まる。</summary>
            public int Pierce;

            public bool Homing;
            public float HomingTurnRate;
            public float HomingRange;

            public LayerMask HitMask;

            /// <summary>この下の当たり判定は無視する（撃った本人）。</summary>
            public Transform IgnoreRoot;

            /// <summary>見た目だけ（敵に当てない）。</summary>
            public bool VisualOnly;
        }

        private Settings settings;
        private Action<EnemyHealth, Vector3, Vector3> onHit;
        private Vector3 direction;
        private float remaining;
        private int pierceLeft;
        private bool stuck;
        private readonly HashSet<EnemyHealth> struck = new HashSet<EnemyHealth>();

        private Collider homingTarget;
        private EnemyHealth homingEnemy;
        private float retargetTimer;

        /// <summary>
        /// visual（無ければ見た目無し）の矢を position から direction へ放つ。onHit(敵, 当たった場所, 矢の向き) は生きた敵に当たるたびに呼ぶ。
        /// </summary>
        public static ArrowProjectile Launch(GameObject visual, Vector3 position, Vector3 direction, Settings settings,
            Action<EnemyHealth, Vector3, Vector3> onHit = null)
        {
            if (direction.sqrMagnitude < 1e-8f) direction = Vector3.forward;
            direction.Normalize();

            var go = new GameObject("Arrow");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            if (visual != null)
            {
                GameObject model = Instantiate(visual, go.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            }

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow.settings = settings;
            arrow.onHit = onHit;
            arrow.direction = direction;
            arrow.remaining = Mathf.Max(0f, settings.Range);
            arrow.pierceLeft = Mathf.Max(0, settings.Pierce);
            return arrow;
        }

        private void Update()
        {
            if (stuck) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (settings.Homing && !settings.VisualOnly) Steer(dt);

            float step = Mathf.Min(Mathf.Max(0f, settings.Speed) * dt, remaining);
            Vector3 position = transform.position;
            int count = Physics.SphereCastNonAlloc(position, Mathf.Max(0.01f, settings.Radius), direction, Hits, step,
                settings.HitMask, QueryTriggerInteraction.Ignore);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                Collider other = hit.collider;
                if (other == null || (settings.IgnoreRoot != null && other.transform.IsChildOf(settings.IgnoreRoot))) continue;

                EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    if (settings.VisualOnly || enemy.IsDead || !struck.Add(enemy)) continue;

                    // 流し始めから重なっていた物は hit.point が取れない（ゼロ）。体の面の近い点で代える。
                    Vector3 point = hit.distance <= 0f ? other.ClosestPoint(position) : hit.point;
                    onHit?.Invoke(enemy, point, direction);
                    if (pierceLeft-- <= 0)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    continue;
                }

                Stick(hit.distance <= 0f ? position : hit.point);
                return;
            }

            transform.SetPositionAndRotation(position + direction * step, Quaternion.LookRotation(direction, Vector3.up));
            remaining -= step;
            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>壁や床に刺さって止まる。</summary>
        private void Stick(Vector3 point)
        {
            stuck = true;
            transform.SetPositionAndRotation(point + direction * StuckDepth, Quaternion.LookRotation(direction, Vector3.up));
            Destroy(gameObject, StuckLifetime);
        }

        /// <summary>前にいる一番近い生きた敵へ、向きを毎秒 HomingTurnRate 度まで寄せる。</summary>
        private void Steer(float dt)
        {
            retargetTimer -= dt;
            if (homingEnemy == null || homingEnemy.IsDead || homingTarget == null || retargetTimer <= 0f)
            {
                retargetTimer = RetargetInterval;
                FindHomingTarget();
            }

            if (homingTarget == null) return;

            Vector3 desired = homingTarget.bounds.center - transform.position;
            if (desired.sqrMagnitude < 1e-6f) return;
            direction = Vector3.RotateTowards(direction, desired.normalized, settings.HomingTurnRate * Mathf.Deg2Rad * dt, 0f).normalized;
        }

        private void FindHomingTarget()
        {
            homingTarget = null;
            homingEnemy = null;
            if (settings.HomingRange <= 0f) return;

            Vector3 position = transform.position;
            float minDot = Mathf.Cos(HomingConeDegrees * Mathf.Deg2Rad);
            float best = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(position, settings.HomingRange, Nearby, settings.HitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Nearby[i];
                EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
                // 当てた敵は追わない（貫通して次の敵へ向かう）。
                if (enemy == null || enemy.IsDead || struck.Contains(enemy)) continue;

                Vector3 to = other.bounds.center - position;
                float distance = to.magnitude;
                if (distance < 1e-3f || Vector3.Dot(to / distance, direction) < minDot || distance >= best) continue;

                best = distance;
                homingTarget = other;
                homingEnemy = enemy;
            }
        }

        private static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > key.distance)
                {
                    Hits[j + 1] = Hits[j];
                    j--;
                }

                Hits[j + 1] = key;
            }
        }
    }
}
