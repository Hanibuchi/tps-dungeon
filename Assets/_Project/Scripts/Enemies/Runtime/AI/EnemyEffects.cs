using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>敵が出すパーティクルの下ごしらえ（Combat の OneShotEffect と同じやり方。Enemies からは Combat が見えないので写してある）。</summary>
    public static class EnemyEffects
    {
        /// <summary>1 回出して、lifetime 秒後に消す。ループする素材はループを切る。</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, float lifetime = 3f)
        {
            if (prefab == null) return null;
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            Prepare(instance, scale, false);
            if (instance.GetComponentInChildren<Light>() != null) instance.AddComponent<EffectLightFade>();
            Object.Destroy(instance, lifetime);
            return instance;
        }

        /// <summary>大きさを全部の粒に効かせる。loop が偽ならループを切って、出ている粒が自然に消えるようにする。</summary>
        public static void Prepare(GameObject instance, float scale, bool loop)
        {
            bool scaled = !Mathf.Approximately(scale, 1f);
            if (scaled) instance.transform.localScale *= scale;

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                if (!loop) main.loop = false;
                if (scaled) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        /// <summary>
        /// 放出を止め、出ている粒と尾が消えてから消す。粒でも尾でもない見た目（弾の球など）と光はすぐ消す。
        /// </summary>
        public static void Release(GameObject instance, float linger)
        {
            if (instance == null) return;
            instance.transform.SetParent(null, true);
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (Light light in instance.GetComponentsInChildren<Light>()) light.enabled = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r is TrailRenderer trail) trail.emitting = false;
                else if (!(r is ParticleSystemRenderer)) r.enabled = false;
            }
            Object.Destroy(instance, linger);
        }

        /// <summary>全部の粒の色に tint を掛ける（灰色の煙を毒の緑にするなど）。</summary>
        public static void Tint(GameObject instance, Color tint)
        {
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                ParticleSystem.MinMaxGradient color = main.startColor;
                switch (color.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        color.color *= tint;
                        break;
                    case ParticleSystemGradientMode.TwoColors:
                        color.colorMin *= tint;
                        color.colorMax *= tint;
                        break;
                    default:
                        color = new ParticleSystem.MinMaxGradient(tint);
                        break;
                }
                main.startColor = color;
            }
        }
    }
}
