using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// パーティクルのエフェクトを 1 回だけ出して、しばらくしたら消す。
    /// ThirdParty の VFX はデモ用にループする作りなので、出すときにループを切る（出ている粒は自然に消える）。
    /// 大きさを変えるときは、全部の粒がルートの拡大に従うようにする（素材には自分の Transform しか見ない物が混ざっている）。
    /// </summary>
    public static class OneShotEffect
    {
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, float lifetime = 3f)
        {
            if (prefab == null) return null;

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            Prepare(instance, scale, false);
            Object.Destroy(instance, lifetime);
            return instance;
        }

        /// <summary>
        /// parent の子として local の位置・向きに出し、parent と一緒に動かす（走りながら出す斬撃など）。
        /// 出た粒も置き去りにならないよう、パーティクルは parent の座標系で動かす。
        /// </summary>
        public static GameObject SpawnAttached(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation,
            float scale = 1f, float lifetime = 3f)
        {
            if (prefab == null || parent == null) return null;

            GameObject instance = Object.Instantiate(prefab, parent);
            instance.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            Prepare(instance, scale, true);
            Object.Destroy(instance, lifetime);
            return instance;
        }

        private static void Prepare(GameObject instance, float scale, bool local)
        {
            bool scaled = !Mathf.Approximately(scale, 1f);
            if (scaled) instance.transform.localScale *= scale;

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
                if (scaled) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (local) main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }
    }
}
