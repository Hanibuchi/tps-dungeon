using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 吊るしたロープをゆっくり揺らす。この Transform（ロープの上端の吊り元）を、X と Z のまわりに数度ずつ別々の周期で振る。
    /// 当たりは揺らさない（揺れる物に当たりを持たせない）。ロープごとに揺れ方の位相をずらす。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Rope Sway")]
    public sealed class RopeSway : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("揺れの大きさ（度）。長いロープほど小さくてよい。")]
        private float angle = 0.6f;

        [SerializeField, Min(0.01f), Tooltip("揺れの周期（秒）。")]
        private float period = 7f;

        private Quaternion baseRotation;
        private float phase;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            float t = (Time.time + phase) * (2f * Mathf.PI / period);
            transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t) * angle, 0f, Mathf.Sin(t * 0.73f + 1.3f) * angle * 0.7f);
        }
    }
}
