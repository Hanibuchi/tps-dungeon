using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵の攻撃の見た目（弾・溜め・撃ち出し・当たり・予兆・着地の土煙）をパーティクルで組んでプレハブにする。EnemyBehaviourSetup から呼ぶ。
    /// Hovl の素材は粒が細かく散るだけのものが多く、敵の攻撃として目立たないので、Hovl の絵（材質）だけを借りてここで組む。
    /// 色は敵ごとに <see cref="FxColor"/> で選ぶ。どれも大きさ 1 で使う前提の実寸（m）で作る。
    /// </summary>
    public static class EnemyEffectsBuilder
    {
        public const string Folder = "Assets/_Project/Prefabs/Enemies/Effects";
        private const string Materials = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Materials/";

        /// <summary>効果の色。</summary>
        public enum FxColor { Pink, Blue, Orange, Green, Purple, Red }

        public static string ShotPath(FxColor c) => $"{Folder}/Shot_{c}.prefab";
        public static string ChargePath(FxColor c) => $"{Folder}/Charge_{c}.prefab";
        public static string MuzzlePath(FxColor c) => $"{Folder}/Muzzle_{c}.prefab";
        public static string ImpactPath(FxColor c) => $"{Folder}/Impact_{c}.prefab";
        public static string GlintPath => $"{Folder}/Glint.prefab";
        public static string DustPath => $"{Folder}/Dust.prefab";

        private static Color Tint(FxColor c) => c switch
        {
            FxColor.Pink => new Color(1f, 0.35f, 0.85f),
            FxColor.Blue => new Color(0.35f, 0.75f, 1f),
            FxColor.Orange => new Color(1f, 0.6f, 0.2f),
            FxColor.Green => new Color(0.55f, 1f, 0.3f),
            FxColor.Purple => new Color(0.7f, 0.4f, 1f),
            _ => new Color(1f, 0.25f, 0.15f),
        };

        private static Material point, flare, flash, glow, circle, trail, smoke;

        /// <summary>全部の効果を作り直す。</summary>
        public static void WriteAll(List<string> log)
        {
            EnsureFolder(Folder);
            point = Load("Point", log);
            flare = Load("Flare", log);
            flash = Load("Flash", log);
            glow = Load("GlowFree1", log);
            circle = Load("Circle", log);
            trail = Load("Trail67", log);
            smoke = Load("SmokeFree1", log);

            // 赤は予兆の色なので当たりは作らない（使わない物は作らない）。
            foreach (FxColor c in new[] { FxColor.Pink, FxColor.Blue, FxColor.Orange, FxColor.Green, FxColor.Purple })
            {
                Save(BuildImpact(c), ImpactPath(c), log);
            }
            foreach (FxColor c in new[] { FxColor.Pink, FxColor.Blue })
            {
                Save(BuildShot(c), ShotPath(c), log);
                Save(BuildCharge(c), ChargePath(c), log);
                Save(BuildMuzzle(c), MuzzlePath(c), log);
            }
            Save(BuildGlint(), GlintPath, log);
            Save(BuildDust(), DustPath, log);
        }

        // ---- 弾 ----

        /// <summary>
        /// 飛ぶ弾。白く光る芯・色の付いたまたたく光輪・通った跡に残る光の粒・尾・まわりを照らす光。
        /// 芯と光輪は弾と一緒に動き（ローカル）、粒は世界に置いていく。
        /// </summary>
        private static GameObject BuildShot(FxColor c)
        {
            Color tint = Tint(c);
            var root = new GameObject($"Shot_{c}");

            ParticleSystem core = Add(root, "Core", glow, true, false);
            Main(core, life: 0.12f, size: (0.55f, 0.65f), color: Color.Lerp(tint, Color.white, 0.6f));
            Rate(core, 45f);

            ParticleSystem halo = Add(root, "Halo", flare, true, false);
            Main(halo, life: 0.16f, size: (0.9f, 1.3f), color: new Color(tint.r, tint.g, tint.b, 0.75f));
            ParticleSystem.MainModule haloMain = halo.main;
            haloMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Rate(halo, 30f);

            ParticleSystem sparkles = Add(root, "Sparkles", point, true, true);
            Main(sparkles, life: 0.45f, size: (0.06f, 0.14f), color: tint);
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 22f;
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            ParticleSystem.MainModule sparkleMain = sparkles.main;
            sparkleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            FadeOut(sparkles, tint, shrink: true);

            var trailRenderer = root.AddComponent<TrailRenderer>();
            trailRenderer.sharedMaterial = trail;
            trailRenderer.time = 0.18f;
            trailRenderer.widthMultiplier = 0.35f;
            trailRenderer.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trailRenderer.startColor = new Color(tint.r, tint.g, tint.b, 0.9f);
            trailRenderer.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;

            AddLight(root, tint, range: 4f, intensity: 2.5f);
            return root;
        }

        /// <summary>溜め。まわりから光の粒が吸い込まれ、真ん中の光がふくらんでいく（狙っている間、撃ち出す所に付ける）。</summary>
        private static GameObject BuildCharge(FxColor c)
        {
            Color tint = Tint(c);
            var root = new GameObject($"Charge_{c}");

            ParticleSystem gather = Add(root, "Gather", point, true, false);
            Main(gather, life: 0.35f, size: (0.05f, 0.11f), color: tint);
            Rate(gather, 60f);
            ParticleSystem.ShapeModule shape = gather.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.9f;
            shape.radiusThickness = 0f;
            ParticleSystem.MainModule gatherMain = gather.main;
            // 外側の球の表面から中心へ、寿命でちょうど届く速さで向かう。
            gatherMain.startSpeed = -0.9f / 0.35f;
            FadeIn(gather, tint);

            ParticleSystem core = Add(root, "Core", glow, true, false);
            Main(core, life: 0.2f, size: (0.25f, 0.35f), color: Color.Lerp(tint, Color.white, 0.4f));
            Rate(core, 30f);
            ParticleSystem.SizeOverLifetimeModule grow = core.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.6f));

            AddLight(root, tint, range: 2.5f, intensity: 1.5f);
            return root;
        }

        /// <summary>撃ち出し。一瞬の閃光と、前へ散る火花。</summary>
        private static GameObject BuildMuzzle(FxColor c)
        {
            Color tint = Tint(c);
            var root = new GameObject($"Muzzle_{c}");

            // 小さな敵（Wizard）の体より大きくならないよう、閃光も火花も控えめにする。
            ParticleSystem burst = Add(root, "Flash", flash, false, false);
            Main(burst, life: 0.1f, size: (0.6f, 0.75f), color: Color.Lerp(tint, Color.white, 0.5f));
            Burst(burst, 1);
            FadeOut(burst, Color.Lerp(tint, Color.white, 0.5f), shrink: false);

            ParticleSystem sparks = Add(root, "Sparks", point, false, true);
            Main(sparks, life: 0.2f, size: (0.03f, 0.06f), color: tint);
            Burst(sparks, 10);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            Stretch(sparks);
            FadeOut(sparks, tint, shrink: false);
            return root;
        }

        // ---- 当たり ----

        /// <summary>当たり。白い閃光・広がる輪・四方へ散る火花。近接の当たりにも弾の当たりにも使う。</summary>
        private static GameObject BuildImpact(FxColor c)
        {
            Color tint = Tint(c);
            var root = new GameObject($"Impact_{c}");

            ParticleSystem burst = Add(root, "Flash", flash, false, false);
            Main(burst, life: 0.15f, size: (1.3f, 1.6f), color: Color.Lerp(tint, Color.white, 0.6f));
            Burst(burst, 1);
            FadeOut(burst, Color.Lerp(tint, Color.white, 0.6f), shrink: false);

            ParticleSystem ring = Add(root, "Ring", circle, false, false);
            Main(ring, life: 0.3f, size: (0.4f, 0.4f), color: tint);
            Burst(ring, 1);
            ParticleSystem.SizeOverLifetimeModule ringSize = ring.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 0f, 12f), new Keyframe(1f, 5.5f, 0f, 0f)));
            FadeOut(ring, tint, shrink: false);

            ParticleSystem sparks = Add(root, "Sparks", point, false, true);
            Main(sparks, life: 0.35f, size: (0.05f, 0.1f), color: tint);
            Burst(sparks, 22);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            sparkMain.gravityModifier = 0.6f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            Stretch(sparks);
            FadeOut(sparks, tint, shrink: false);

            AddLight(root, tint, range: 3f, intensity: 2f);
            return root;
        }

        // ---- 予兆・土煙 ----

        /// <summary>
        /// 近接の予兆。頭の上に赤い十字の光がぱっと灯って消える（溜め始めに出す）。
        /// 溜めの長さに関係なく 0.4 秒ほどで消える。
        /// </summary>
        private static GameObject BuildGlint()
        {
            Color tint = Tint(FxColor.Red);
            var root = new GameObject("Glint");

            for (int i = 0; i < 2; i++)
            {
                ParticleSystem star = Add(root, i == 0 ? "Star" : "Star45", flare, false, false);
                Main(star, life: 0.4f, size: (0.9f, 0.9f), color: i == 0 ? Color.Lerp(tint, Color.white, 0.3f) : tint);
                ParticleSystem.MainModule main = star.main;
                main.startRotation = i == 0 ? 0f : Mathf.PI * 0.25f;
                Burst(star, 1);
                ParticleSystem.SizeOverLifetimeModule size = star.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.25f, 1.2f), new Keyframe(1f, 0f)));
                ParticleSystem.RotationOverLifetimeModule spin = star.rotationOverLifetime;
                spin.enabled = true;
                spin.z = Mathf.PI;
            }
            return root;
        }

        /// <summary>着地の土煙。足元から輪になって横へ広がり、ふくらみながら薄れる。</summary>
        private static GameObject BuildDust()
        {
            var color = new Color(0.62f, 0.56f, 0.5f, 0.7f);
            var root = new GameObject("Dust");

            ParticleSystem dust = Add(root, "Dust", smoke, false, true);
            Main(dust, life: 0.7f, size: (0.5f, 0.8f), color: color);
            Burst(dust, 16);
            ParticleSystem.MainModule main = dust.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            // 円は XY 面に出るので、寝かせて床に沿わせる。
            shape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.15f;
            drag.limit = 0f;
            ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 2f));
            FadeOut(dust, color, shrink: false);
            return root;
        }

        // ---- 部品 ----

        /// <summary>
        /// 粒を 1 つ足す。loop なら Play の間ずっと出す（弾・溜め）、そうでなければ Burst で 1 回だけ。
        /// world なら出た粒は世界に置いていく（跡を残す）。
        /// </summary>
        private static ParticleSystem Add(GameObject parent, string name, Material material, bool loop, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.duration = loop ? 1f : 0.5f;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void Main(ParticleSystem particles, float life, (float min, float max) size, Color color)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = life;
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startColor = color;
        }

        private static void Rate(ParticleSystem particles, float perSecond)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = perSecond;
        }

        private static void Burst(ParticleSystem particles, int count)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        /// <summary>寿命の終わりへ向かって薄れる（shrink なら小さくもなる）。</summary>
        private static void FadeOut(ParticleSystem particles, Color color, bool shrink)
        {
            ParticleSystem.ColorOverLifetimeModule over = particles.colorOverLifetime;
            over.enabled = true;
            over.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) },
            };
            if (!shrink) return;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        /// <summary>出始めは薄く、中心に近づくほど濃くなる（吸い込まれる粒）。</summary>
        private static void FadeIn(ParticleSystem particles, Color color)
        {
            ParticleSystem.ColorOverLifetimeModule over = particles.colorOverLifetime;
            over.enabled = true;
            over.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.6f, 1f) },
            };
        }

        /// <summary>速さの向きへ伸ばして描く（火花の筋）。</summary>
        private static void Stretch(ParticleSystem particles)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.04f;
        }

        private static void AddLight(GameObject root, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(root.transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private static Material Load(string name, List<string> log)
        {
            string path = $"{Materials}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) log.Add($"効果の材質が無い: {path}");
            return material;
        }

        private static void Save(GameObject root, string path, List<string> log)
        {
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"効果: {path}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
