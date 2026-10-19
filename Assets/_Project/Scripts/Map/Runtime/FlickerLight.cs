using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 炎の光のゆらめき。同じ GameObject の Light の明るさを Perlin ノイズで ±flicker の割合だけゆっくり揺らし、位置も数 cm 揺らす。
    /// 点滅ではなく炎の揺れに見えるよう、速さは控えめにする。光ごとに揺れ方の位相をずらす。
    /// 色・強さ・距離・揺れの幅は、階を作るときに FloorBuilder が層の光と色（FloorMood）から入れる（<see cref="Apply"/>）。
    /// プレハブの intensityScale・rangeScale で、ろうそくなど小さい火は松明より弱く短くする。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("TPS Dungeon/Flicker Light")]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("層の松明の強さに掛ける倍率（ろうそくは小さく）。")]
        private float intensityScale = 1f;

        [SerializeField, Min(0f), Tooltip("層の松明の届く距離に掛ける倍率。")]
        private float rangeScale = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("明るさの揺れの幅（割合）。層の値で上書きされる。")]
        private float flicker = 0.12f;

        [SerializeField, Min(0f), Tooltip("揺れの速さ（1 秒あたり）。")]
        private float speed = 2.2f;

        [SerializeField, Min(0f), Tooltip("位置の揺れの大きさ（メートル）。")]
        private float wobble = 0.03f;

        private Light target;
        private float baseIntensity;
        private Vector3 basePosition;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            basePosition = transform.localPosition;
            seed = Random.value * 100f;
        }

        /// <summary>層の光の色・強さ・距離・揺れの幅を入れる。</summary>
        public void Apply(Color color, float intensity, float range, float flickerAmount)
        {
            if (target == null) target = GetComponent<Light>();
            target.color = color;
            target.intensity = baseIntensity = intensity * intensityScale;
            target.range = range * rangeScale;
            flicker = flickerAmount;
        }

        private void Update()
        {
            float t = Time.time * speed + seed;
            // 2 つの速さのノイズを重ねて、ゆっくりした揺れに時々小さな揺らぎが乗るようにする。
            float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, seed + 17f) * 0.3f;
            target.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * flicker);
            transform.localPosition = basePosition + new Vector3(
                Mathf.PerlinNoise(t * 0.8f, seed + 3f) - 0.5f,
                Mathf.PerlinNoise(t * 0.8f, seed + 7f) - 0.5f,
                Mathf.PerlinNoise(t * 0.8f, seed + 11f) - 0.5f) * (wobble * 2f);
        }
    }
}
