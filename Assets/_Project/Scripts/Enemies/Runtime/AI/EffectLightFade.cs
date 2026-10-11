using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 1 回だけ出す効果（当たりの閃光など）の光を、出た瞬間の明るさから短い時間で消す。
    /// 粒が消えるのを待って効果ごと消すまで光が点きっぱなしにならないように。<see cref="EnemyEffects.Spawn"/> が付ける。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class EffectLightFade : MonoBehaviour
    {
        private const float Duration = 0.25f;

        private Light[] lights;
        private float[] intensities;
        private float elapsed;

        private void Awake()
        {
            lights = GetComponentsInChildren<Light>();
            intensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) intensities[i] = lights[i].intensity;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(1f - elapsed / Duration);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = intensities[i] * k;
                if (k <= 0f) lights[i].enabled = false;
            }
            if (k <= 0f) enabled = false;
        }
    }
}
