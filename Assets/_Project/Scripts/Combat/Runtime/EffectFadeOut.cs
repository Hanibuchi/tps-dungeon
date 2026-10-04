using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// パーティクルのエフェクトを、Hold 秒見せてから Fade 秒かけて薄くして消す。
    /// ThirdParty の魔法陣などはデモで繰り返す作りで、1 周の終わりに消える動きを持たない（寿命が尽きた瞬間にぱっと消える）ので、
    /// 放出を止めたうえで、出ている粒の色（透明度と、加算の素材のために明るさも）を毎フレーム下げていく。消し終えたら自分ごと消える。
    /// <see cref="OneShotEffect.SpawnFading"/> が付ける。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class EffectFadeOut : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];
        private float hold;
        private float fade;
        private float elapsed;
        private float applied = 1f;
        private bool stopped;

        public static void Attach(GameObject effect, float hold, float fade)
        {
            if (effect == null) return;

            var fader = effect.AddComponent<EffectFadeOut>();
            fader.systems = effect.GetComponentsInChildren<ParticleSystem>();
            fader.hold = Mathf.Max(0f, hold);
            fader.fade = Mathf.Max(0.01f, fade);
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (elapsed < hold) return;

            if (!stopped)
            {
                stopped = true;
                foreach (ParticleSystem particles in systems)
                    if (particles != null) particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            float remaining = Mathf.Clamp01(1f - (elapsed - hold) / fade);
            // 前のフレームまでに掛けた分を除いて、今の割合になるよう掛け足す。
            float step = applied > 1e-4f ? remaining / applied : 0f;
            applied = remaining;
            foreach (ParticleSystem particles in systems) Dim(particles, step);

            if (remaining <= 0f) Destroy(gameObject);
        }

        private void Dim(ParticleSystem particles, float step)
        {
            if (particles == null) return;

            int count = particles.particleCount;
            if (count == 0) return;
            if (buffer.Length < count) buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];

            count = particles.GetParticles(buffer, count);
            for (int i = 0; i < count; i++)
            {
                Color32 c = buffer[i].startColor;
                buffer[i].startColor = new Color32((byte)(c.r * step), (byte)(c.g * step), (byte)(c.b * step), (byte)(c.a * step));
            }

            particles.SetParticles(buffer, count);
        }
    }
}
