using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 加護（<see cref="CharacterBuffs"/>）の 1 種類が付いている間、キャラの体にその色の膜の見た目（Magic shield など）を付けておく。種類ごとに 1 つ付く。
    /// 膜はキャラの子にして、粒もキャラと一緒に動かす。
    /// 素材の球は 1 周（4 秒）に粒を 1 つ出し、すぐ大きくなって、終わりに薄れて消える作りなので、ループさせると 1 周ごとに消えては出直す。
    /// そこで、粒が 1 周ずっと生きる系（球・地面の円）は、大きくなりきった所（<see cref="HoldTime"/>）で止めて、そのまま見せ続ける。
    /// 寿命の短いきらめき（Trails）はループのまま流す。加護が外れたら、放出を止めて薄くして消す（<see cref="EffectFadeOut"/>）。
    /// 体を包む球（メッシュの粒）と、胸の高さでカメラへ向く大きな光の円盤は、止めた所から <see cref="VeilFadeTime"/> 秒で薄くして消す。
    /// 出し続けると、肩越しのカメラからは操作キャラの周りが膜越しにしか見えず、敵や照準が見づらかった。
    /// 掛けた直後の膨らむ様子は見せ、そのあとは足元の円ときらめきだけで加護が付いていることを示す。
    /// 掛ける側（RangedAttacker）が <see cref="Ensure"/> で付ける。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BlessingAura : MonoBehaviour
    {
        /// <summary>加護が外れてから膜が消えきるまでの秒数。</summary>
        private const float FadeTime = 0.5f;

        /// <summary>
        /// 1 周ずっと生きる粒の系を止めるまでの秒数。Magic shield の球は 0.26 秒までに出て、寿命の 1 割（0.44 秒）で大きくなりきり、
        /// 8 割（3.2 秒）から薄れ始めるので、その間で止める。
        /// </summary>
        private const float HoldTime = 1.5f;

        /// <summary>
        /// 膜を足元からこれだけ浮かせて出す（m）。足元の円は地面と同じ高さの板なので、そのままだと床の凹凸や段に一部が埋まって欠けて見えた。
        /// </summary>
        private const float GroundLift = 0.1f;

        /// <summary>球と光の円盤を、止めてから消しきるまでの秒数。</summary>
        private const float VeilFadeTime = 0.6f;

        /// <summary>カメラへ向く板の粒が、この大きさ（m、膜の倍率の後）以上なら視界をふさぐ膜とみなす。きらめき（0.06 m）は残す。</summary>
        private const float VeilBillboardSize = 1f;

        /// <summary>粒の寿命が 1 周のこの割合以上ある系を、止めて見せ続ける系とみなす。</summary>
        private const float HoldLifetimeRatio = 0.8f;

        private CharacterBuffs buffs;
        private BlessingKind kind;
        private GameObject effect;
        private readonly List<ParticleSystem> holds = new List<ParticleSystem>();
        private float elapsed;
        private bool held;
        private float effectScale = 1f;

        /// <summary>buffs のキャラに kind の膜を出す（もう出ていればそのまま）。prefab が無ければ何もしない。</summary>
        public static void Ensure(CharacterBuffs buffs, BlessingKind kind, GameObject prefab, float scale)
        {
            if (buffs == null || prefab == null || !buffs.Has(kind)) return;

            BlessingAura aura = null;
            foreach (BlessingAura existing in buffs.GetComponents<BlessingAura>())
            {
                if (existing.buffs == buffs && existing.kind == kind) aura = existing;
            }

            if (aura == null)
            {
                aura = buffs.gameObject.AddComponent<BlessingAura>();
                aura.buffs = buffs;
                aura.kind = kind;
                buffs.Changed += aura.OnChanged;
            }

            if (aura.effect == null) aura.Spawn(prefab, scale);
        }

        private void OnDestroy()
        {
            if (buffs != null) buffs.Changed -= OnChanged;
        }

        private void Update()
        {
            if (effect == null || held) return;

            elapsed += Time.deltaTime;
            if (elapsed < HoldTime) return;

            held = true;
            foreach (ParticleSystem particles in holds)
                if (particles != null) particles.Pause(false);

            // 視界をふさぐ球と光の円盤を薄くして消す（消し終えたら EffectFadeOut がその子ごと消す）。足元の円（根元の系）は残す。
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                if (particles.gameObject == effect || !IsVeil(particles, effectScale)) continue;
                holds.Remove(particles);
                EffectFadeOut.Attach(particles.gameObject, 0f, VeilFadeTime);
            }
        }

        /// <summary>体を包む球（メッシュの粒）か、カメラへ向く大きな板の粒か。</summary>
        internal static bool IsVeil(ParticleSystem particles, float scale)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return false;
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh) return true;
            if (renderer.renderMode != ParticleSystemRenderMode.Billboard) return false;
            if (renderer.alignment != ParticleSystemRenderSpace.View && renderer.alignment != ParticleSystemRenderSpace.Facing) return false;
            return particles.main.startSize.constantMax * scale >= VeilBillboardSize;
        }

        private void OnChanged(CharacterBuffs changed, BlessingKind changedKind)
        {
            if (changedKind != kind || changed.Has(kind) || effect == null) return;

            // 子のまま薄くすると、消えるまでの間もキャラに付いて動く。止めた粒もそのまま薄れる。消し終えたら EffectFadeOut が自分ごと消す。
            EffectFadeOut.Attach(effect, 0f, FadeTime);
            effect = null;
            holds.Clear();
        }

        private void Spawn(GameObject prefab, float scale)
        {
            effect = Instantiate(prefab, transform);
            // 膜の素材（Magic shield）は地面に置く作りで、球は根元から 1 m 上に出る。足元（から少し上）に置けば体を包む。
            // 大きさは種類ごとに違う（WeaponTypeDefinition.BlessingEffectScaleFor）ので、3 種類が付いても足元の円が重ならない。
            effect.transform.SetLocalPositionAndRotation(Vector3.up * GroundLift, Quaternion.identity);
            effectScale = scale;
            bool scaled = !Mathf.Approximately(scale, 1f);
            if (scaled) effect.transform.localScale *= scale;

            holds.Clear();
            elapsed = 0f;
            held = false;
            // 素材はデモ用にループする作り。粒はキャラの座標系で動かして置き去りにしない。
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                if (scaled) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (main.startLifetime.constantMax >= main.duration * HoldLifetimeRatio) holds.Add(particles);
            }
        }
    }
}
