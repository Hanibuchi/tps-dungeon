using TpsDungeon.Audio.Runtime;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の弾 1 発。まっすぐ飛び、毎フレーム進む分だけ球を滑らせて当たりを見る。
    /// メンバーか置物に当たればダメージを与えて消え、壁や床に当たれば何もせず消える。ほかの敵と撃った本人は素通りする。
    /// <see cref="Settings.Range"/> 飛んでも当たらなければ消える。
    /// 置物は当たり判定を持たないことがあるので、進んだ線分の近くに置物が居れば当たりにする。
    /// 見た目（Visual）はループする粒の素材を子にして運び、消えるときに放出を止めて、出ている粒が消えてから消す。
    /// RangedEnemyBrain が撃つ。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private const float VisualLingerTime = 1f;

        public struct Settings
        {
            public float Speed;
            public float Range;
            public float Radius;
            public int Damage;
            /// <summary>撃った敵。当たりを見ない。</summary>
            public Transform Owner;

            public GameObject HitEffect;
            public float HitEffectScale;
            public AudioClip HitClip;
            public float Volume;
        }

        private static readonly RaycastHit[] castHits = new RaycastHit[16];

        private Settings settings;
        private GameObject visual;
        private Vector3 direction;
        private float travelled;

        public static EnemyProjectile Launch(GameObject visualPrefab, float visualScale, Vector3 position, Vector3 direction, Settings settings)
        {
            var go = new GameObject("EnemyProjectile");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            var projectile = go.AddComponent<EnemyProjectile>();
            projectile.settings = settings;
            projectile.direction = direction.normalized;

            if (visualPrefab != null)
            {
                projectile.visual = Instantiate(visualPrefab, go.transform, false);
                EnemyEffects.Prepare(projectile.visual, visualScale, true);
            }
            return projectile;
        }

        private void Update()
        {
            float step = settings.Speed * Time.deltaTime;
            Vector3 from = transform.position;

            if (TryHit(from, step, out Vector3 point, out EnemyTarget target, out bool hitSomething))
            {
                target.Damage(settings.Damage);
                Finish(point, true);
                return;
            }
            if (hitSomething)
            {
                Finish(point, false);
                return;
            }

            transform.position = from + direction * step;
            travelled += step;
            if (travelled >= settings.Range) Finish(transform.position, false);
        }

        /// <summary>
        /// from から step 進む間に当たる一番近い物を探す。相手に当たれば true、壁なら false で hitSomething が真。
        /// </summary>
        private bool TryHit(Vector3 from, float step, out Vector3 point, out EnemyTarget target, out bool hitSomething)
        {
            point = from + direction * step;
            target = default;
            hitSomething = false;

            float nearest = float.MaxValue;
            int count = Physics.SphereCastNonAlloc(from, settings.Radius, direction, castHits, step, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (hit.distance >= nearest) continue;
                if (settings.Owner != null && hit.collider.transform.IsChildOf(settings.Owner)) continue;
                if (hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;

                bool isTarget = EnemyTarget.TryFrom(hit.collider, out EnemyTarget t);
                // 倒れたメンバーなど、狙えない相手の体は素通りする。
                if (!isTarget && EnemyHits.IsSeeThrough(hit.collider, settings.Owner)) continue;

                nearest = hit.distance;
                hitSomething = true;
                target = t;
                // 始めから重なっていると distance が 0・point が原点になるので、そのときは今の位置にする。
                point = hit.distance > 0f ? hit.point : from;
            }

            // 当たり判定を持たない置物は、進んだ線分からの距離で拾う。
            float reach = settings.Radius + EnemyTarget.DecoyRadius;
            foreach (DecoyTarget decoy in DecoyTarget.All)
            {
                var candidate = new EnemyTarget(decoy);
                if (!candidate.IsValid) continue;
                Vector3 aim = candidate.AimPoint;
                float along = Mathf.Clamp(Vector3.Dot(aim - from, direction), 0f, step);
                if (along >= nearest) continue;
                Vector3 closest = from + direction * along;
                if ((aim - closest).sqrMagnitude > reach * reach) continue;

                nearest = along;
                hitSomething = true;
                target = candidate;
                point = closest;
            }

            return hitSomething && target.IsValid;
        }

        private void Finish(Vector3 point, bool hitTarget)
        {
            EnemyEffects.Spawn(settings.HitEffect, point, Quaternion.LookRotation(-direction, Vector3.up), settings.HitEffectScale, 2f);
            if (hitTarget && settings.HitClip != null) GameAudio.Instance?.PlaySeAt(settings.HitClip, point, settings.Volume);

            transform.position = point;
            EnemyEffects.Release(visual, VisualLingerTime);
            visual = null;
            Destroy(gameObject);
        }
    }
}
