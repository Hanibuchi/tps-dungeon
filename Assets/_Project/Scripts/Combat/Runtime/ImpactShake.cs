using Unity.Cinemachine;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 叩きつけの着弾などでカメラを短く揺らす（Cinemachine 3 の Impulse）。
    /// 揺れの出どころ（CinemachineImpulseSource）は初めて揺らすときに作る。
    /// 受け手（CinemachineImpulseListener）もシーンを書き換えずに済むよう、シーンの CinemachineCamera に無ければその場で足す。
    /// </summary>
    public static class ImpactShake
    {
        /// <summary>揺れの長さ（秒）。</summary>
        private const float Duration = 0.3f;

        private static CinemachineImpulseSource source;

        /// <summary>position から強さ strength（おおよそ揺れ幅 m）で揺らす。0 以下なら何もしない。</summary>
        public static void At(Vector3 position, float strength)
        {
            if (strength <= 0f) return;

            EnsureListeners();
            if (source == null) source = CreateSource();
            source.GenerateImpulseAtPositionWithVelocity(position, Vector3.down * strength);
        }

        /// <summary>受け手を、揺れの出どころの既定（CinemachineImpulseListener.Reset と同じ値）で整える。シーンを組むエディタからも使う。</summary>
        public static void Configure(CinemachineImpulseListener listener)
        {
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                AmplitudeGain = 1f,
                FrequencyGain = 1f,
                Duration = 1f,
            };
        }

        private static void EnsureListeners()
        {
            foreach (CinemachineCamera camera in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                if (camera.GetComponent<CinemachineImpulseListener>() != null) continue;
                Configure(camera.gameObject.AddComponent<CinemachineImpulseListener>());
            }
        }

        private static CinemachineImpulseSource CreateSource()
        {
            var go = new GameObject("[ImpactShake]") { hideFlags = HideFlags.HideInHierarchy };
            var impulse = go.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseChannel = 1,
                ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                CustomImpulseShape = new AnimationCurve(),
                ImpulseDuration = Duration,
                // 近いほど強く、遠いと弱く（30 m でほぼ消える）。
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating,
                DissipationDistance = 30f,
                DissipationRate = 0.25f,
                PropagationSpeed = 343f,
            };
            impulse.DefaultVelocity = Vector3.down;
            return impulse;
        }
    }
}
