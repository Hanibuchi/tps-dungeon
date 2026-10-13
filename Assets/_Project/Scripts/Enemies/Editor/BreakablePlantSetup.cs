using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 攻撃で壊せる草花（<see cref="BreakablePlant"/>）に要る物をそろえ、置いてある草花を壊せるようにする。
    /// - レイヤー Breakable（9 番）を作り、物理ではどのレイヤーとも当たらないようにする（歩いてすり抜けられる。攻撃の重なり判定には映る）。
    /// - 飛び散るかけら（Effect_PlantChips）：薄い板の粒が回りながら噴き上がって落ち、地面で跳ねて縮んで消える。色は植物ごとに BreakablePlant が塗る。
    /// - 出てくる敵の表（PlantSpawnTable_Surface）：既にあれば中身は変えない（手で直した確率を守る）。
    /// - 音：壊れる音は効果音ラボ「パンチの衣擦れ1」、敵が出る音は「ジャンプ」。ThirdParty は git に上げないので、無ければ空のまま（鳴らない）。
    /// </summary>
    public static class BreakablePlantSetup
    {
        public const int LayerIndex = 9;
        public const string SpawnTablePath = "Assets/_Project/Settings/Map/PlantSpawnTable_Surface.asset";

        private const string EffectFolder = "Assets/_Project/Prefabs/Map/Effects";
        private const string ChipEffectPath = EffectFolder + "/Effect_PlantChips.prefab";
        private const string ChipMaterialPath = "Assets/_Project/Materials/Effects/Mat_PlantChip.mat";
        private const string SpawnDustPath = "Assets/_Project/Prefabs/Enemies/Effects/Dust.prefab";
        private const string BreakSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Map/パンチの衣擦れ1.mp3";
        private const string SpawnSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Monster/ジャンプ.mp3";
        private const string EnemyFolder = "Assets/_Project/Prefabs/Enemies/";

        // 草むらから出てきそうな、小さめの敵。合計 12%。
        private static readonly (string enemy, float chance)[] DefaultSpawns =
        {
            ("Blob_GreenBlob", 0.05f),
            ("Blob_Mushnub", 0.04f),
            ("Blob_PinkBlob", 0.02f),
            ("Big_Frog", 0.01f),
        };

        // 壊せる草花（素材のプレハブ名にこれを含む物）。茂み（bush）は大きいので含めない。
        private static readonly string[] PlantNames = { "grass_small", "flower_", "plant_" };

        [MenuItem("Tools/TPS Dungeon/Enemies/壊せる草花の素材をそろえる")]
        public static void EnsureAssetsFromMenu()
        {
            EnsureAssets();
            Debug.Log("壊せる草花のレイヤー・かけら・出てくる敵の表をそろえた。");
        }

        /// <summary>素材のプレハブ名から、壊せる草花か。</summary>
        public static bool IsPlant(GameObject item)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item);
            string name = (source != null ? source : item).name;
            foreach (string plant in PlantNames)
            {
                if (name.Contains(plant)) return true;
            }
            return false;
        }

        /// <summary>レイヤー・かけら・表をそろえて、表を返す。</summary>
        public static PlantSpawnTable EnsureAssets()
        {
            EnsureLayer();
            EnsureChipEffect();
            return EnsureSpawnTable();
        }

        /// <summary>
        /// item（草花のプレハブの実体）を壊せるようにする。当たり（すり抜けられるレイヤーの箱）・HP 1 の置物の体力・BreakablePlant を付ける。
        /// 何度呼んでも同じになる。
        /// </summary>
        public static void MakeBreakable(GameObject item, PlantSpawnTable table)
        {
            foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger) Object.DestroyImmediate(c);
            }

            item.layer = LayerIndex;
            // 壊れるときに倒れて潰れる（Transform を動かす）ので、静的なまとめ描き（Static Batching）から外す。
            foreach (Transform t in item.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            Bounds b = LocalBounds(item);
            var box = item.AddComponent<BoxCollider>();
            // 小さな草でも当てやすいよう、少し大きめに。
            Vector3 size = new Vector3(Mathf.Max(b.size.x, 0.6f), Mathf.Max(b.size.y, 0.6f), Mathf.Max(b.size.z, 0.6f));
            box.size = size;
            box.center = new Vector3(b.center.x, Mathf.Max(b.min.y, 0f) + size.y * 0.5f, b.center.z);

            EnemyHealth health = item.GetComponent<EnemyHealth>();
            if (health == null) health = item.AddComponent<EnemyHealth>();
            var serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHp").intValue = 1;
            serializedHealth.FindProperty("prop").boolValue = true;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            BreakablePlant plant = item.GetComponent<BreakablePlant>();
            if (plant == null) plant = item.AddComponent<BreakablePlant>();
            (Color primary, Color secondary) = PlantColors(item);
            var serialized = new SerializedObject(plant);
            serialized.FindProperty("chipEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ChipEffectPath);
            serialized.FindProperty("primaryColor").colorValue = primary;
            serialized.FindProperty("secondaryColor").colorValue = secondary;
            serialized.FindProperty("breakSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSoundPath);
            serialized.FindProperty("spawnTable").objectReferenceValue = table;
            serialized.FindProperty("spawnEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnDustPath);
            serialized.FindProperty("spawnEffectScale").floatValue = 1.4f;
            serialized.FindProperty("spawnSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SpawnSoundPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- レイヤー ---

        private static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(LayerIndex);
            if (slot.stringValue != BreakableLayer.Name)
            {
                if (!string.IsNullOrEmpty(slot.stringValue))
                    throw new System.InvalidOperationException($"レイヤー {LayerIndex} は既に「{slot.stringValue}」に使われている。");
                slot.stringValue = BreakableLayer.Name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }

            // 物理ではどのレイヤーとも当たらない。
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(LayerIndex, i, true);
        }

        // --- かけら ---

        private static void EnsureChipEffect()
        {
            Material material = EnsureChipMaterial();
            EnsureFolder(EffectFolder);

            var root = new GameObject("Effect_PlantChips");
            try
            {
                var system = root.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.duration = 1f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.4f);
                // 葉や花びらの薄い板。
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 1.1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 64;
                main.playOnAwake = true;
                main.stopAction = ParticleSystemStopAction.Destroy;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 30) });

                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 38f;
                shape.radius = 0.25f;

                ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
                spin.enabled = true;
                spin.separateAxes = true;
                spin.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
                spin.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
                spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

                // ひらひら舞いながら落ちる（空気に抜かれる）。
                ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 1.6f;
                drag.multiplyDragByParticleSize = false;

                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));

                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.dampen = 0.6f;
                collision.bounce = 0.2f;
                collision.lifetimeLoss = 0f;
                collision.radiusScale = 0.5f;
                collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.collidesWith = ~((1 << LayerIndex) | (1 << 8));

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                renderer.alignment = ParticleSystemRenderSpace.Local;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;

                PrefabUtility.SaveAsPrefabAsset(root, ChipEffectPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material EnsureChipMaterial()
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(ChipMaterialPath));
            var material = AssetDatabase.LoadAssetAtPath<Material>(ChipMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(material, ChipMaterialPath);
            }

            // 粒の色（頂点色）を出す、不透明の材質。薄い板は横から見ると陰で暗く沈むので、少し明るく掛ける。
            material.SetColor("_BaseColor", new Color(1.35f, 1.35f, 1.35f, 1f));
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", 0f);
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        // --- 表 ---

        private static PlantSpawnTable EnsureSpawnTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<PlantSpawnTable>(SpawnTablePath);
            if (table != null) return table;

            EnsureFolder(System.IO.Path.GetDirectoryName(SpawnTablePath));
            table = ScriptableObject.CreateInstance<PlantSpawnTable>();
            var entries = new List<PlantSpawnTable.Entry>();
            foreach ((string enemy, float chance) in DefaultSpawns)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + enemy + ".prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"敵のプレハブが無いので表に入れなかった: {enemy}");
                    continue;
                }
                entries.Add(new PlantSpawnTable.Entry { enemy = prefab, chance = chance });
            }
            table.SetEntries(entries);
            AssetDatabase.CreateAsset(table, SpawnTablePath);
            AssetDatabase.SaveAssets();
            return table;
        }

        // --- 色 ---

        /// <summary>
        /// 植物のメッシュの UV で地の絵を拾い、いちばん多い色（花なら花びら）と次に多い色（葉・茎）を返す。
        /// 1 色しか無ければ（草）、その色と少し明るい色。
        /// </summary>
        private static (Color primary, Color secondary) PlantColors(GameObject item)
        {
            var fallback = new Color(0.38f, 0.5f, 0.19f);
            MeshFilter filter = item.GetComponentInChildren<MeshFilter>();
            Renderer renderer = item.GetComponentInChildren<Renderer>();
            Texture texture = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
            if (filter == null || filter.sharedMesh == null || texture == null) return (fallback, fallback * 1.15f);

            Texture2D readable = Readable(texture);
            try
            {
                var counts = new Dictionary<Color32, int>();
                foreach (Vector2 uv in filter.sharedMesh.uv)
                {
                    Color32 c = readable.GetPixelBilinear(uv.x, uv.y);
                    var key = new Color32((byte)(c.r / 16 * 16), (byte)(c.g / 16 * 16), (byte)(c.b / 16 * 16), 255);
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                var sorted = new List<KeyValuePair<Color32, int>>(counts);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                if (sorted.Count == 0) return (fallback, fallback * 1.15f);

                Color primary = sorted[0].Key;
                Color secondary = sorted.Count > 1 && sorted[1].Value * 6 >= sorted[0].Value ? (Color)sorted[1].Key : primary * 1.2f;
                secondary.a = 1f;
                return (primary, secondary);
            }
            finally
            {
                Object.DestroyImmediate(readable);
            }
        }

        private static Texture2D Readable(Texture texture)
        {
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // --- 補助 ---

        private static Bounds LocalBounds(GameObject instance)
        {
            Transform root = instance.transform;
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return bounds;
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
