using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵の動き（NavMeshAgent・<see cref="EnemyMotor"/>・種類ごとの <see cref="EnemyBrain"/>）をプレハブに付ける。MonsterBuilder から呼ぶ。
    /// 敵の種類（Notion の「敵種」の 01〜07）ごとに作り方を 1 つ用意してあり、数はモンスターごとに渡す。1 層を想定した仮の値。
    /// 見た目と音は ThirdParty の素材を名前で引く。見つからなければ空のままにしてログに出す。
    /// </summary>
    public static class EnemyBehaviourSetup
    {
        private const string HovlPrefabs = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Prefabs/";
        private const string ArmsSounds = "Assets/ThirdParty/Sound/SoundEffect-Lab/Arms/";

        // 爆発の素材は、そのままの大きさで半径 7 m ほど（粒の大きさ込み、Simulate で測った）。
        private const string ExplosionEffect = HovlPrefabs + "Hits and explosions/Explosion.prefab";
        private const float ExplosionEffectRadius = 7f;
        // 半球の煙がその場で渦を巻き続ける。灰色なので緑に染めて毒霧にする。そのままの大きさで半径 6 m ほど。
        private const string PoisonCloudEffect = HovlPrefabs + "Smoke effects/Smoke hemisphere loop.prefab";
        private const float PoisonCloudEffectRadius = 6f;

        // 遠距離の敵の弾。光る球に尾を引かせたものをここで作る（Hovl の粒の素材は細かい火花で、飛ぶ弾に見えないため）。
        private const string ShotFolder = "Assets/_Project/Prefabs/Enemies/Shots";
        private const string ShotMaterialFolder = "Assets/_Project/Materials/Enemies";
        private const float ShotSize = 0.26f;

        /// <summary>浮く敵の上下の揺れ幅（m、片側）。</summary>
        private const float HoverBob = 0.45f;

        /// <summary>敵の声と攻撃の音（効果音ラボの「戦闘」から取ったもの）。MonsterBuilder もここから引く。</summary>
        public const string MonsterSounds = "Assets/ThirdParty/Sound/SoundEffect-Lab/Monster/";

        private const string SwingSound = ArmsSounds + "パンチ素振り.mp3";
        private const string HitSound = MonsterSounds + "打撃2.mp3";
        private const string HeavyHitSound = MonsterSounds + "重いキック1.mp3";
        private const string LeapSound = MonsterSounds + "ジャンプ.mp3";
        private const string LandSound = MonsterSounds + "ジャンプの着地.mp3";
        // 浮遊近距離の体当たり。鋭く空を切る音。
        private const string DiveSound = ArmsSounds + "手裏剣を投げる.mp3";
        private const string ChargeSound = MonsterSounds + "パワーチャージ.mp3";
        // 自爆の導火。だんだん遅くなって止まる電子音で、爆発までの残りを知らせる（2.9 秒）。
        private const string FuseSound = MonsterSounds + "電子ルーレットが徐々に止まる.mp3";

        /// <summary>導火から爆発までの秒数。導火の音（<see cref="FuseSound"/>）が鳴り終わってから爆発するよう、音より少し長くする。</summary>
        private const float FuseTime = 3f;
        private const string ExplosionSound = MonsterSounds + "爆発3.mp3";
        private const string GasSound = MonsterSounds + "毒魔法2.mp3";
        private const string WindShotSound = MonsterSounds + "風魔法1.mp3";
        private const string MagicShotSound = ArmsSounds + "火炎魔法1.mp3";
        private const string ShotHitSound = MonsterSounds + "ショット命中.mp3";

        /// <summary>1 体ぶんの動きの作り方。</summary>
        public sealed class Behaviour
        {
            /// <summary>敵種の名前（ログ用）。</summary>
            public string Label;
            public Type Brain;
            /// <summary>移動の速さ（m/秒）。</summary>
            public float Speed;
            /// <summary>浮く高さ（m）。0 なら歩く。</summary>
            public float Hover;
            public int Damage;
            public float Cooldown;
            /// <summary>攻撃で流す Animator のパラメータ。null なら Brain の既定（Attack）。</summary>
            public string AttackAnimation;
            /// <summary>Brain のシリアライズするフィールドへ入れる数。</summary>
            public (string field, float value)[] Numbers = new (string, float)[0];
            /// <summary>Brain のシリアライズするフィールドへ入れるアセット（パス）。</summary>
            public List<(string field, string path)> Assets = new List<(string, string)>();

            /// <summary>Brain のアセットを 1 つ差し替える（無ければ足す）。モンスターごとに声や音を変えるのに使う。</summary>
            public Behaviour With(string field, string path)
            {
                Assets.RemoveAll(a => a.field == field);
                Assets.Add((field, path));
                return this;
            }
        }

        // ---- 敵種（Notion「敵種」）----

        /// <summary>
        /// 01 単純近距離: 単純に近づいて攻撃。attackAnimation は武器を持っているモンスターなら "Weapon"（武器を振る。Big のグループにある）。
        /// </summary>
        public static Behaviour Melee(float speed, int damage, float range, float hitRadius, string attackAnimation = null) => new Behaviour
        {
            Label = "01 単純近距離",
            Brain = typeof(MeleeEnemyBrain),
            Speed = speed,
            Damage = damage,
            AttackAnimation = attackAnimation,
            Cooldown = 1.4f,
            Numbers = new[] { ("attackRange", range), ("hitRadius", hitRadius), ("windup", 0.5f), ("recovery", 0.5f) },
            Assets = { ("attackClip", SwingSound), ("hitClip", HitSound) },
        };

        /// <summary>02 飛びつき近距離: 飛びついて攻撃。</summary>
        public static Behaviour Leap(float speed, int damage, float leapRange) => new Behaviour
        {
            Label = "02 飛びつき近距離",
            Brain = typeof(LeapEnemyBrain),
            Speed = speed,
            Damage = damage,
            Cooldown = 2.2f,
            Numbers = new[] { ("leapRange", leapRange), ("crouchTime", 0.6f), ("leapSpeed", 9f), ("leapApex", 1.4f), ("landRecovery", 0.9f) },
            Assets = { ("attackClip", LeapSound), ("hitClip", HeavyHitSound), ("landClip", LandSound) },
        };

        /// <summary>03 浮遊近距離: 浮遊して近距離攻撃。ふだんは hover の高さを不規則に上下し、殴るときだけ降りてくる。</summary>
        public static Behaviour FloatingMelee(float speed, float hover, int damage) => new Behaviour
        {
            Label = "03 浮遊近距離",
            Brain = typeof(MeleeEnemyBrain),
            Speed = speed,
            Hover = hover,
            Damage = damage,
            Cooldown = 1.2f,
            Numbers = new[] { ("attackRange", 1.5f), ("hitRadius", 1f), ("windup", 0.4f), ("recovery", 0.4f) },
            Assets = { ("attackClip", DiveSound), ("hitClip", HitSound) },
        };

        /// <summary>04 自爆: 近づいて自爆。</summary>
        public static Behaviour Bomber(float speed, int damage, float blastRadius) => new Behaviour
        {
            Label = "04 自爆",
            Brain = typeof(BomberEnemyBrain),
            Speed = speed,
            Damage = damage,
            Numbers = new[] { ("fuseRange", 2.2f), ("fuseTime", FuseTime), ("blastRadius", blastRadius), ("explosionEffectScale", blastRadius / ExplosionEffectRadius) },
            Assets = { ("explosionEffect", ExplosionEffect), ("windupClip", FuseSound), ("attackClip", ExplosionSound) },
        };

        /// <summary>05 毒霧: 近づくと毒霧を出す。damage は霧の 1 刻み（0.5 秒）のダメージ。</summary>
        public static Behaviour PoisonGas(float speed, int damage, float cloudRadius) => new Behaviour
        {
            Label = "05 毒霧",
            Brain = typeof(PoisonGasEnemyBrain),
            Speed = speed,
            Damage = damage,
            Cooldown = 6f,
            Numbers = new[] { ("emitRange", 2.5f), ("cloudRadius", cloudRadius), ("cloudDuration", 5f), ("tickInterval", 0.5f),
                ("cloudEffectRadius", PoisonCloudEffectRadius) },
            Assets = { ("cloudEffect", PoisonCloudEffect), ("attackClip", GasSound) },
        };

        /// <summary>06 浮遊遠距離: 浮遊して遠距離攻撃。</summary>
        public static Behaviour FloatingRanged(float speed, float hover, int damage, ShotColor color) => Ranged(speed, damage, color, WindShotSound,
            "06 浮遊遠距離", hover, minRange: 5f, maxRange: 13f);

        /// <summary>07 遠距離: 射線が通る所まで来て遠距離攻撃。</summary>
        public static Behaviour Ranged(float speed, int damage, ShotColor color) => Ranged(speed, damage, color, MagicShotSound,
            "07 遠距離", 0f, minRange: 4f, maxRange: 12f);

        /// <summary>弾の色。名前は Hovl の Sparks の色（当たると同じ色の火花が散る）。</summary>
        public enum ShotColor { Blue, Pink }

        private static Color GlowOf(ShotColor color) => color == ShotColor.Blue
            ? new Color(0.35f, 0.75f, 1.6f, 1f)
            : new Color(1.6f, 0.35f, 1.2f, 1f);

        private static string ShotPath(ShotColor color) => $"{ShotFolder}/EnemyShot_{color}.prefab";

        private static Behaviour Ranged(float speed, int damage, ShotColor color, string shotSound, string label, float hover,
            float minRange, float maxRange) => new Behaviour
        {
            Label = label,
            Brain = typeof(RangedEnemyBrain),
            Speed = speed,
            Hover = hover,
            Damage = damage,
            Cooldown = 2f,
            Numbers = new[]
            {
                ("minRange", minRange), ("maxRange", maxRange), ("windup", 0.7f), ("projectileSpeed", 12f),
                ("projectileEffectScale", 1f), ("impactEffectScale", 0.3f),
            },
            Assets =
            {
                ("windupClip", ChargeSound),
                ("projectileEffect", ShotPath(color)),
                ("impactEffect", $"{HovlPrefabs}Sparks/Sparks explode {color.ToString().ToLowerInvariant()}.prefab"),
                ("attackClip", shotSound),
                ("hitClip", ShotHitSound),
            },
        };

        /// <summary>
        /// 作ってあるプレハブの Brain に、音（名前が Clip で終わるフィールド）だけを入れ直す。Animator やほかの値には触らない。
        /// </summary>
        public static void WriteSounds(GameObject root, Behaviour behaviour, List<string> log)
        {
            var component = root.GetComponent(behaviour.Brain);
            if (component == null)
            {
                log.Add($"{root.name}: {behaviour.Brain.Name} が無いので音を入れなかった（作り直しが要る）");
                return;
            }
            var brain = new SerializedObject(component);
            WriteAssets(brain, behaviour, root.name, true, log);
            brain.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteAssets(SerializedObject brain, Behaviour behaviour, string owner, bool soundsOnly, List<string> log)
        {
            foreach (var (field, path) in behaviour.Assets)
            {
                if (soundsOnly && !field.EndsWith("Clip", StringComparison.Ordinal)) continue;
                SerializedProperty p = brain.FindProperty(field);
                if (p == null) throw new ArgumentException($"{behaviour.Brain.Name} に {field} が無い");
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) log.Add($"{owner}: 見つからないので空のままにした: {path}");
                p.objectReferenceValue = asset;
            }
        }

        // ---- 弾の見た目 ----

        /// <summary>弾の見た目（光る球と尾）を色ごとに作り直す。敵のプレハブより先に呼ぶ。</summary>
        public static void WriteShots(List<string> log)
        {
            EnsureFolder(ShotFolder);
            EnsureFolder(ShotMaterialFolder);

            // 尾は頂点色で色を付ける（矢の雨の円と同じ Sprites/Default）。
            Material trailMaterial = LoadOrCreateMaterial($"{ShotMaterialFolder}/EnemyShotTrail.mat", "Sprites/Default");

            foreach (ShotColor color in Enum.GetValues(typeof(ShotColor)))
            {
                Color glow = GlowOf(color);
                Material orb = LoadOrCreateMaterial($"{ShotMaterialFolder}/EnemyShot_{color}.mat", "Universal Render Pipeline/Lit");
                Color baseColor = new Color(Mathf.Min(1f, glow.r), Mathf.Min(1f, glow.g), Mathf.Min(1f, glow.b), 1f);
                orb.SetColor("_BaseColor", baseColor);
                orb.EnableKeyword("_EMISSION");
                orb.SetColor("_EmissionColor", glow * 1.5f);
                // None だと検証し直しで _EMISSION が外れて光らなくなる。
                orb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(orb);

                var root = new GameObject($"EnemyShot_{color}");
                try
                {
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.name = "Orb";
                    Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.transform.SetParent(root.transform, false);
                    ball.transform.localScale = Vector3.one * ShotSize;
                    var renderer = ball.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = orb;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    var trail = root.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = trailMaterial;
                    trail.time = 0.2f;
                    trail.widthMultiplier = ShotSize * 0.8f;
                    trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                    trail.startColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
                    trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    trail.receiveShadows = false;

                    PrefabUtility.SaveAsPrefabAsset(root, ShotPath(color));
                    log.Add($"弾: {ShotPath(color)}");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shaderName)) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // ---- プレハブに付ける ----

        /// <summary>root に動きを付ける。radius と height は当たり判定（CapsuleCollider）の大きさ。</summary>
        public static void Assign(GameObject root, Behaviour behaviour, float radius, float height, List<string> log)
        {
            // NavMesh はフロアの生成の後に焼くので、置いた時点では無い。切っておき、焼けてから EnemyMotor が入れる
            // （入れたままだと「NavMesh が無いので agent を作れない」の警告が敵の数だけ出る）。
            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = behaviour.Hover;
            agent.speed = behaviour.Speed;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

            var motor = new SerializedObject(root.AddComponent<EnemyMotor>());
            motor.FindProperty("moveSpeed").floatValue = behaviour.Speed;
            motor.FindProperty("hoverHeight").floatValue = behaviour.Hover;
            // 浮く敵は不規則に上下して狙いにくくする（揺れ幅は片側 m）。歩く敵は揺らさない。
            motor.FindProperty("hoverBob").floatValue = behaviour.Hover > 0f ? HoverBob : 0f;
            motor.ApplyModifiedPropertiesWithoutUndo();

            var brain = new SerializedObject(root.AddComponent(behaviour.Brain));
            brain.FindProperty("damage").intValue = behaviour.Damage;
            brain.FindProperty("cooldown").floatValue = behaviour.Cooldown;
            if (behaviour.AttackAnimation != null) brain.FindProperty("attackAnimation").stringValue = behaviour.AttackAnimation;
            foreach (var (field, value) in behaviour.Numbers)
            {
                SerializedProperty p = brain.FindProperty(field);
                if (p == null) throw new ArgumentException($"{behaviour.Brain.Name} に {field} が無い");
                p.floatValue = value;
            }
            WriteAssets(brain, behaviour, root.name, false, log);
            brain.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
