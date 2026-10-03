using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 地面から突き出て、しばらくして引っ込んで消える物（範囲連置の氷の棘など）の見た目の動き。
    /// 出した瞬間は地面の下に縮めておき、riseTime 秒で突き出し、holdTime 秒そのまま、sinkTime 秒で地面へ戻って消える。
    /// 根元が原点、+Y が上の物に付ける。当たり判定は持たない（RangedAttacker が与える）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Ground Spike")]
    public sealed class GroundSpike : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("地面から突き出し切るまでの秒数。")]
        private float riseTime = 0.08f;

        [SerializeField, Min(0f), Tooltip("突き出したまま止まっている秒数。")]
        private float holdTime = 0.45f;

        [SerializeField, Min(0.01f), Tooltip("地面へ引っ込むまでの秒数。")]
        private float sinkTime = 0.3f;

        [SerializeField, Range(0f, 0.5f), Tooltip("突き出し切る瞬間に行き過ぎて戻る量（高さに対する割合）。")]
        private float overshoot = 0.15f;

        private Vector3 fullScale;
        private float elapsed;

        private void Awake()
        {
            fullScale = transform.localScale;
            Apply(0f);
        }

        /// <summary>出した直後に大きさを factor 倍にする（Awake で縮めてしまうので、localScale を直接いじらずこれを呼ぶ）。</summary>
        public void Scale(float factor)
        {
            fullScale *= factor;
            Apply(elapsed <= 0f ? 0f : 1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t;
            if (elapsed < riseTime)
            {
                // 行き過ぎてから戻る（素早く突き出す手応え）。
                float x = elapsed / riseTime;
                t = Mathf.Sin(x * Mathf.PI * 0.5f) * (1f + overshoot * Mathf.Sin(x * Mathf.PI));
            }
            else if (elapsed < riseTime + holdTime)
            {
                t = 1f;
            }
            else
            {
                float x = (elapsed - riseTime - holdTime) / sinkTime;
                if (x >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                t = 1f - x * x;
            }

            Apply(t);
        }

        /// <summary>高さは t 倍、太さは少し残す（細い棒にならないよう）。</summary>
        private void Apply(float t)
        {
            float width = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t));
            transform.localScale = new Vector3(fullScale.x * width, fullScale.y * Mathf.Max(0.001f, t), fullScale.z * width);
        }
    }
}
