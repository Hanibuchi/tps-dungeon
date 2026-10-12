using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 持続弓が空へ放つ火の矢 1 本（見た目だけ）。弓から上へ飛び出し、弧を描いて雨の真上（ArrowRainZone の空の点）へ、
    /// 雨の見た目が出る瞬間にちょうど着くよう flightTime 秒で飛ぶ。着いたら消え、そこで魔法陣が開いて隕石が降り始める（魔法陣は ArrowRainZone が出す）。
    /// 壁や敵には当たらない。纏う炎（effect）は着いたら放出を止めて切り離し、出ている粒が消えてから消す。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class RainShotArrow : MonoBehaviour
    {
        /// <summary>着いてから、纏う炎の粒と尾が消えるまで待つ秒数。</summary>
        private const float EffectLingerTime = 0.6f;

        /// <summary>飛び出す向きへ伸ばす弧の膨らみ（弓から空の点までの距離に対する割合）。</summary>
        private const float ArcBulge = 0.45f;

        private Vector3 from;
        private Vector3 control;
        private Vector3 to;
        private float flightTime;
        private float elapsed;
        private GameObject effect;

        /// <param name="visual">矢の見た目（原点が矢の先、柄が -Z）。</param>
        /// <param name="effect">矢の先に付けて一緒に動かす炎（任意）。</param>
        /// <param name="launchDirection">弓から飛び出す向き。この向きに膨らんだ弧を描いて to へ向かう。</param>
        public static RainShotArrow Launch(GameObject visual, GameObject effect, Vector3 from, Vector3 launchDirection, Vector3 to, float flightTime)
        {
            if (launchDirection.sqrMagnitude < 1e-8f) launchDirection = Vector3.up;

            var go = new GameObject("RainShot");
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(launchDirection, Vector3.up));
            if (visual != null)
            {
                GameObject model = Instantiate(visual, go.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            }

            var shot = go.AddComponent<RainShotArrow>();
            shot.from = from;
            shot.to = to;
            shot.control = from + launchDirection.normalized * (Vector3.Distance(from, to) * ArcBulge);
            shot.flightTime = Mathf.Max(0.05f, flightTime);
            if (effect != null) shot.effect = Instantiate(effect, go.transform, false);
            return shot;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);

            // 2 次ベジェ。向きは接線。
            float u = 1f - t;
            Vector3 position = u * u * from + 2f * u * t * control + t * t * to;
            Vector3 tangent = 2f * u * (control - from) + 2f * t * (to - control);
            transform.position = position;
            if (tangent.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

            if (t >= 1f) Arrive();
        }

        private void Arrive()
        {
            if (effect != null)
            {
                effect.transform.SetParent(null, true);
                foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                foreach (TrailRenderer trail in effect.GetComponentsInChildren<TrailRenderer>())
                    trail.emitting = false;
                foreach (Light light in effect.GetComponentsInChildren<Light>())
                    light.enabled = false;
                Destroy(effect, EffectLingerTime);
                effect = null;
            }

            Destroy(gameObject);
        }
    }
}
