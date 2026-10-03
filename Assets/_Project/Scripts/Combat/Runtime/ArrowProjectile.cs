using System;
using System.Collections.Generic;
using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 飛んでいる矢 1 本。重力は無く、まっすぐ（ホーミングなら敵へ曲がりながら）飛び、毎フレーム進む分だけ球を流して当たりを見る。
    /// 生きた敵に当たったら onHit を呼ぶ（1 本が同じ敵に当たるのは 1 回）。貫通の数だけ抜けて飛び続け、尽きたらそこで消える。
    /// 敵でない物（壁・床）に当たったら刺さって onStick を呼び（爆発のエンチャント用）、しばらくして消える。死んだ敵の体はすり抜ける。
    /// 見た目だけの矢（矢の雨・上へ放つ矢）は敵をすり抜けて、床に刺さるだけ。
    /// 見た目のプレハブは「原点が矢の先、柄が -Z」に置く（刺さったときに先が当たった面に来るように）。
    /// 投擲では手に持つ武器の見た目をそのまま飛ばし（Settings.HeldModelVisual）、寝かせて縦に回す（Settings.SpinRate）。
    /// 石のように刺さらない物（Settings.Bounce）は、壁や床で跳ね返って転がる。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ArrowProjectile : MonoBehaviour
    {
        /// <summary>刺さってから消えるまでの秒数。</summary>
        private const float StuckLifetime = 2f;

        /// <summary>刺さるときに面へめり込ませる深さ（m）。</summary>
        private const float StuckDepth = 0.12f;

        /// <summary>跳ね返るとき、当たる前の速さのうち残す割合。</summary>
        private const float BounceRestitution = 0.25f;

        /// <summary>跳ね返った物を置くレイヤー（Ignore Raycast）。転がっている間に照準や矢を遮らないように。</summary>
        private const int BounceLayer = 2;

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

            /// <summary>
            /// 見た目が手に持つ武器の見た目（握りが原点で長い向きが +Y）。投擲で使う。長い向きを飛ぶ向きへ寝かせ、
            /// 真ん中を先から半分の長さだけ後ろに置く（回らなければ先が当たった面に来て、回っても刺さったときに面へ食い込んで見える）。
            /// </summary>
            public bool HeldModelVisual;

            /// <summary>見た目を縦に回す速さ（度/秒、上を前へ倒す向き）。刺さったら止まる。0 なら回さない。</summary>
            public float SpinRate;

            /// <summary>壁や床に刺さらず、跳ね返って転がる（石）。onStick は同じく呼ぶ。</summary>
            public bool Bounce;
        }

        private Settings settings;
        // 回す見た目の節点（HeldModelVisual のときだけ）。ルートの向きは毎フレーム飛ぶ向きに合わせ直すので、回すのはこの子。
        private Transform spinPivot;
        // 見た目の半分の長さ（HeldModelVisual のときだけ）。跳ね返った物の当たりの半径に使う。
        private float visualHalfLength;
        private Action<EnemyHealth, Vector3, Vector3> onHit;
        private Action<Vector3> onStick;
        private Vector3 direction;
        private float remaining;
        private int pierceLeft;
        private bool stuck;
        private readonly HashSet<EnemyHealth> struck = new HashSet<EnemyHealth>();

        private Collider homingTarget;
        private EnemyHealth homingEnemy;
        private float retargetTimer;

        /// <summary>
        /// visual（無ければ見た目無し）の矢を position から direction へ放つ。onHit(敵, 当たった場所, 矢の向き) は生きた敵に当たるたびに、
        /// onStick(刺さった場所) は敵でない物に刺さったときに呼ぶ（見た目だけの矢では呼ばない）。
        /// </summary>
        public static ArrowProjectile Launch(GameObject visual, Vector3 position, Vector3 direction, Settings settings,
            Action<EnemyHealth, Vector3, Vector3> onHit = null, Action<Vector3> onStick = null)
        {
            if (direction.sqrMagnitude < 1e-8f) direction = Vector3.forward;
            direction.Normalize();

            var go = new GameObject(settings.HeldModelVisual ? "Thrown" : "Arrow");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            Transform pivot = null;
            float halfLength = 0f;
            if (visual != null)
            {
                if (settings.HeldModelVisual)
                {
                    pivot = new GameObject("Spin").transform;
                    pivot.SetParent(go.transform, false);
                }

                GameObject model = Instantiate(visual, pivot != null ? pivot : go.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                if (pivot != null) halfLength = LayAlongTravel(model.transform, pivot);
            }

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow.spinPivot = pivot;
            arrow.visualHalfLength = halfLength;
            arrow.settings = settings;
            arrow.onHit = onHit;
            arrow.onStick = onStick;
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

                Vector3 surface = hit.distance <= 0f ? position : hit.point;
                if (settings.Bounce) BounceOff(surface, hit.distance <= 0f ? -direction : hit.normal);
                else Stick(surface);
                return;
            }

            transform.SetPositionAndRotation(position + direction * step, Quaternion.LookRotation(direction, Vector3.up));
            if (spinPivot != null && settings.SpinRate != 0f)
                spinPivot.localRotation = Quaternion.AngleAxis(settings.SpinRate * dt, Vector3.right) * spinPivot.localRotation;
            remaining -= step;
            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>
        /// 手に持つ見た目（長い向きが +Y）を、長い向きが飛ぶ向き（+Z）になるよう寝かせ、真ん中を pivot に合わせる。
        /// pivot は先から半分の長さだけ後ろ（-Z）に置き、回らなければ前の端が矢の先（ルートの原点）に来る。半分の長さを返す。
        /// </summary>
        private static float LayAlongTravel(Transform model, Transform pivot)
        {
            model.localRotation = Quaternion.Euler(90f, 0f, 0f) * model.localRotation;
            if (!TryMeasure(model, pivot, out Bounds bounds)) return 0f;

            // 縦に回す軸（X）に厚みの向きを合わせ、平たい面（斧の頭・刃）が縦の面の中で回るようにする。
            if (bounds.extents.y < bounds.extents.x)
            {
                model.localRotation = Quaternion.Euler(0f, 0f, 90f) * model.localRotation;
                TryMeasure(model, pivot, out bounds);
            }

            model.localPosition -= bounds.center;
            pivot.localPosition = new Vector3(0f, 0f, -bounds.extents.z);
            return bounds.extents.z;
        }

        /// <summary>model のメッシュを合わせた境界を space の座標系で測る。</summary>
        private static bool TryMeasure(Transform model, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                Matrix4x4 matrix = toSpace * filter.transform.localToWorldMatrix;
                Bounds local = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = matrix.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }

            return any;
        }

        /// <summary>
        /// 壁や床に当たって跳ね返る。ここからは物理に任せて転がし、しばらくして消す。
        /// </summary>
        private void BounceOff(Vector3 point, Vector3 normal)
        {
            stuck = true;
            float radius = Mathf.Max(0.01f, visualHalfLength > 0f ? Mathf.Min(settings.Radius, visualHalfLength) : settings.Radius);
            // 見た目の真ん中（回す節点）が当たりの球の真ん中に来るよう、ルートを当たった面から浮かせる。
            Vector3 center = point + normal * radius;
            transform.position = spinPivot != null ? center - (spinPivot.position - transform.position) : center;
            gameObject.layer = BounceLayer;
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius;
            if (spinPivot != null) sphere.center = spinPivot.localPosition;
            var body = gameObject.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.Reflect(direction * settings.Speed, normal) * BounceRestitution;
            body.angularVelocity = spinPivot != null ? transform.right * settings.SpinRate * Mathf.Deg2Rad * BounceRestitution : Vector3.zero;
            Destroy(gameObject, StuckLifetime);
            if (!settings.VisualOnly) onStick?.Invoke(point);
        }

        /// <summary>壁や床に刺さって止まる。</summary>
        private void Stick(Vector3 point)
        {
            stuck = true;
            transform.SetPositionAndRotation(point + direction * StuckDepth, Quaternion.LookRotation(direction, Vector3.up));
            Destroy(gameObject, StuckLifetime);
            if (!settings.VisualOnly) onStick?.Invoke(point);
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
