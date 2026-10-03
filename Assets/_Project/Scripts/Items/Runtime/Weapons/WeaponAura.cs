using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ユニークなど特別なランクの武器がまとう光。武器の見た目（手に持つモデルや、床に落ちている物）の子に作る。
    /// 形は武器の見た目の箱に合わせ、そこから火の粉のような光の粒が立ちのぼり、表面には小さな光がまたたき、明かりが周りを照らす。
    /// 色や材質はランク表（<see cref="WeaponRankTable"/>）の設定で、材質の無いランクには付けない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAura : MonoBehaviour
    {
        private const string ObjectName = "RankAura";

        /// <summary>立ちのぼる粒の 1 m あたり・1 秒あたりの数。</summary>
        private const float EmbersPerMeter = 40f;

        /// <summary>表面でまたたく粒の 1 m あたり・1 秒あたりの数。</summary>
        private const float GlintsPerMeter = 18f;

        /// <summary>明かりの届く距離（m）の、武器の長さに対する倍率と下限。</summary>
        private const float LightRangeRatio = 1.6f;
        private const float MinLightRange = 1.2f;

        private Light glow;
        private float baseIntensity;
        private float phase;

        /// <summary>
        /// model の見た目に rank の光をまとわせる。そのランクに光の設定が無ければ何もしない（null を返す）。
        /// 見た目の大きさは、この時点の model 以下の描画物から測る。
        /// </summary>
        public static WeaponAura Attach(GameObject model, WeaponRank rank)
        {
            if (model == null) return null;

            WeaponRankTable table = WeaponRankTable.Default;
            if (table == null || !table.TryGet(rank, out WeaponRankTable.Entry style) || style.auraParticles == null) return null;

            var root = new GameObject(ObjectName);
            root.transform.SetParent(model.transform, false);
            if (!TryMeasure(model, root.transform, out Bounds bounds))
            {
                Destroy(root);
                return null;
            }

            var aura = root.AddComponent<WeaponAura>();
            aura.Build(bounds, style);
            return aura;
        }

        private void Build(Bounds bounds, WeaponRankTable.Entry style)
        {
            float length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            Color color = style.auraColor;
            Color hot = Color.Lerp(color, Color.white, 0.3f);

            ParticleSystem embers = CreateParticles("Embers", style.auraParticles, bounds, ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, hot);
            main.gravityModifier = -0.05f;
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = embers.emission;
            emission.rateOverTime = EmbersPerMeter * length;
            ParticleSystem.VelocityOverLifetimeModule velocity = embers.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            ParticleSystem.NoiseModule noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 1.5f;
            FadeInOut(embers, 0.15f);
            ShrinkOverLifetime(embers);

            ParticleSystem glints = CreateParticles("Glints", style.auraParticles, bounds, ParticleSystemSimulationSpace.Local);
            main = glints.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, hot);
            main.maxParticles = 60;
            emission = glints.emission;
            emission.rateOverTime = GlintsPerMeter * length;
            FadeInOut(glints, 0.4f);

            if (style.auraLightIntensity > 0f)
            {
                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = bounds.center;
                glow = lightObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = color;
                glow.range = Mathf.Max(MinLightRange, length * LightRangeRatio);
                glow.intensity = baseIntensity = style.auraLightIntensity;
                glow.shadows = LightShadows.None;
            }

            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (glow == null) return;

            // ゆっくり脈打たせ、少しだけ揺らぐ。止まっていると電球に見えるため。
            float t = Time.time + phase;
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 2.4f) + 0.06f * (Mathf.PerlinNoise(t * 6f, 0f) - 0.5f);
            glow.intensity = baseIntensity * pulse;
        }

        /// <summary>武器の見た目の箱から粒を出す粒子を作る。</summary>
        private ParticleSystem CreateParticles(string name, Material material, Bounds bounds, ParticleSystemSimulationSpace space)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = bounds.center;
            shape.scale = bounds.size;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
            return particles;
        }

        /// <summary>寿命の初めの fade の割合で現れ、終わりへ向けて消える。</summary>
        private static void FadeInOut(ParticleSystem particles, float fade)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fade), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        private static void ShrinkOverLifetime(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        /// <summary>model 以下のメッシュの描画物をまとめた箱を、space から見た向きと大きさで測る。</summary>
        private static bool TryMeasure(GameObject model, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (renderer.transform.IsChildOf(space)) continue;

                Bounds local = renderer.localBounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = toSpace.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (!any) bounds = new Bounds(corner, Vector3.zero);
                    else bounds.Encapsulate(corner);
                    any = true;
                }
            }

            return any;
        }
    }
}
