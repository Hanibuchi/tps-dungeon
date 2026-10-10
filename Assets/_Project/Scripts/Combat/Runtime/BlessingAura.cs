using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 加護（<see cref="CharacterBuffs"/>）が付いている間、キャラの体に膜の見た目（Magic shield など）を付けておく。
    /// 膜はキャラの子にしてループのまま流し、粒もキャラと一緒に動かす。加護が外れたら放出を止めて薄くして消す（<see cref="EffectFadeOut"/>）。
    /// 掛ける側（RangedAttacker）が <see cref="Ensure"/> で付ける。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BlessingAura : MonoBehaviour
    {
        /// <summary>加護が外れてから膜が消えきるまでの秒数。</summary>
        private const float FadeTime = 0.5f;

        private CharacterBuffs buffs;
        private GameObject effect;

        /// <summary>buffs のキャラに膜を出す（もう出ていればそのまま）。prefab が無ければ何もしない。</summary>
        public static void Ensure(CharacterBuffs buffs, GameObject prefab, float scale)
        {
            if (buffs == null || prefab == null || !buffs.HasBlessing) return;

            BlessingAura aura = buffs.TryGetComponent(out BlessingAura existing) ? existing : buffs.gameObject.AddComponent<BlessingAura>();
            aura.Bind(buffs);
            if (aura.effect == null) aura.effect = Spawn(prefab, buffs.transform, scale);
        }

        private void Bind(CharacterBuffs target)
        {
            if (buffs == target) return;
            if (buffs != null) buffs.Changed -= OnChanged;
            buffs = target;
            buffs.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (buffs != null) buffs.Changed -= OnChanged;
        }

        private void OnChanged(CharacterBuffs changed)
        {
            if (changed.HasBlessing || effect == null) return;

            // 子のまま薄くすると、消えるまでの間もキャラに付いて動く。消し終えたら EffectFadeOut が自分ごと消す。
            EffectFadeOut.Attach(effect, 0f, FadeTime);
            effect = null;
        }

        private static GameObject Spawn(GameObject prefab, Transform parent, float scale)
        {
            GameObject instance = Instantiate(prefab, parent);
            // 膜の素材（Magic shield）は地面に置く作りで、球は根元から 1 m 上に出る。足元に置けば体を包む。
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            bool scaled = !Mathf.Approximately(scale, 1f);
            if (scaled) instance.transform.localScale *= scale;

            // 素材はデモ用にループする作り。加護の間はループのまま流し、粒はキャラの座標系で動かして置き去りにしない。
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                if (scaled) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            return instance;
        }
    }
}
