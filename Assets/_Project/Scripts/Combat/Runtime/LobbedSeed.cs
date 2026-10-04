using System;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 放物線を描いて狙った地面へ落ちる、見た目だけの物（治癒持続の種）。当たり判定は持たず、壁も抜ける。
    /// from から to へ FlightTime 秒で飛び、途中で線分から ArcHeight m 上がる。着いたら onLanded(to) を呼んで消える。
    /// RangedAttacker が出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class LobbedSeed : MonoBehaviour
    {
        /// <summary>見た目を回す速さ（度/秒）。</summary>
        private const float SpinRate = 360f;

        private Vector3 from;
        private Vector3 to;
        private float flightTime;
        private float arcHeight;
        private float elapsed;
        private Action<Vector3> onLanded;

        public static LobbedSeed Launch(GameObject visual, Vector3 from, Vector3 to, float flightTime, float arcHeight, Action<Vector3> onLanded)
        {
            var go = new GameObject("LobbedSeed");
            go.transform.position = from;
            if (visual != null) Instantiate(visual, go.transform, false);

            var seed = go.AddComponent<LobbedSeed>();
            seed.from = from;
            seed.to = to;
            seed.flightTime = Mathf.Max(0.01f, flightTime);
            seed.arcHeight = Mathf.Max(0f, arcHeight);
            seed.onLanded = onLanded;
            return seed;
        }

        /// <summary>t（0〜1）のときの位置。線分の上に 4h t(1 − t) だけ持ち上げる。</summary>
        public static Vector3 Evaluate(Vector3 from, Vector3 to, float arcHeight, float t) =>
            Vector3.Lerp(from, to, t) + Vector3.up * (4f * arcHeight * t * (1f - t));

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            transform.position = Evaluate(from, to, arcHeight, t);
            transform.Rotate(Vector3.right, SpinRate * Time.deltaTime, Space.Self);
            if (t < 1f) return;

            onLanded?.Invoke(to);
            Destroy(gameObject);
        }
    }
}
