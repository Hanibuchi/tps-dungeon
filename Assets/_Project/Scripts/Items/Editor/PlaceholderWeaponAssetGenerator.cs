using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Gen = TpsDungeon.Items.Editor.PlaceholderItemAssetGenerator;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 武器まわりのデータ一式をコードから作る。数値は仮で、ここを直して作り直すか、できたアセットを直接いじって調整する。
    /// - エンチャントの付き方・エンチャント 22 種・武器種 01（片手近距離）: Assets/_Project/Items/Weapons/
    /// - ランクの色: Assets/_Project/Resources/Weapons/（ゲーム中に WeaponRankTable.Default で引くため Resources に置く）
    /// - 武器 3 本（Notion の武器一覧 DB で武器種＝01 のもの）と、拾える物・手に持つ見た目のプレハブ
    /// - 枠と情報欄の絵は、手に持つ見た目のモデルを斜めから撮って作る（背景は透明）
    /// 見た目は ThirdParty の FreeSwords があればそれを、無ければプリミティブの剣を使う。
    /// 何度実行しても同じ結果になる（既存アセットは上書き、GUID は保つ）。
    /// </summary>
    public static class PlaceholderWeaponAssetGenerator
    {
        public const string WeaponsFolder = Gen.ItemsFolder + "/Weapons";
        private const string EnchantmentsFolder = WeaponsFolder + "/Enchantments";
        public const string RankTablePath = "Assets/_Project/Resources/" + WeaponRankTable.ResourcePath + ".asset";
        private const string OldRankTablePath = WeaponsFolder + "/WeaponRankTable.asset";
        public const string RollSettingsPath = WeaponsFolder + "/EnchantmentRollSettings.asset";
        public const string OneHandedTypePath = WeaponsFolder + "/WeaponType_01_OneHanded.asset";

        private const string FreeSwords = "Assets/ThirdParty/3D Model/Blink/Weapons/FreeSwords/Prefabs/";

        /// <summary>見た目の剣の長さ（m）。素材の大きさはまちまちなので、これに合わせて縮める。</summary>
        private const float SwordLength = 0.9f;

        private static readonly Vector3 PickupVolume = new Vector3(0.35f, 0.3f, 1.0f);

        /// <summary>モデルを撮った絵の大きさ（px）。枠と情報欄は 64px なので、高解像度の画面でも粗くならないよう倍で撮る。</summary>
        private const int IconSize = 128;

        /// <summary>背景の透明が残らないときに撮り直す、抜き取り用の背景色。</summary>
        private static readonly Color KeyColor = new Color(1f, 0f, 1f, 1f);

        private sealed class WeaponSpec
        {
            public string Id;
            public string Name;
            public WeaponRank Rank;
            public float Strength;
            public string Description;
            public string ModelPrefab;
            public Color Blade;
            public Color Hilt;
        }

        [MenuItem("Tools/TPS Dungeon/プレースホルダの武器を生成")]
        public static void Generate()
        {
            Gen.EnsureFolder(WeaponsFolder);
            Gen.EnsureFolder(EnchantmentsFolder);
            Gen.EnsureFolder(Gen.IconsFolder);
            Gen.EnsureFolder(Gen.PrefabsFolder);

            WriteRankTable();
            EnchantmentRollSettings roll = WriteRollSettings();
            Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments = WriteEnchantments();
            WeaponTypeDefinition oneHanded = WriteOneHandedType(enchantments, roll);

            foreach (WeaponSpec spec in Weapons()) WriteWeapon(spec, oneHanded);

            AssetDatabase.SaveAssets();
            Debug.Log($"仮の武器データを作った: {WeaponsFolder}");
        }

        /// <summary>生成器が作った武器を全部読む。確認用シーンに置くときに使う。</summary>
        public static List<WeaponDefinition> LoadAll()
        {
            var result = new List<WeaponDefinition>();
            foreach (WeaponSpec spec in Weapons())
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponsFolder}/{spec.Id}.asset");
                if (weapon != null) result.Add(weapon);
            }

            return result;
        }

        private static IEnumerable<WeaponSpec> Weapons()
        {
            yield return new WeaponSpec
            {
                Id = "Weapon_RustySword", Name = "錆びた片手剣", Rank = WeaponRank.E, Strength = 5f,
                Description = "刃こぼれだらけだが、振れば斬れる。",
                ModelPrefab = "Sword1_Bronze.prefab",
                Blade = new Color32(150, 110, 80, 255), Hilt = new Color32(90, 60, 40, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_IronSword", Name = "鉄の片手剣", Rank = WeaponRank.C, Strength = 21f,
                Description = "兵士が腰に下げていた、ありふれた造りの剣。",
                ModelPrefab = "Sword15_Iron.prefab",
                Blade = new Color32(190, 196, 204, 255), Hilt = new Color32(80, 60, 50, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_BlueSteelRapier", Name = "蒼鋼の細剣", Rank = WeaponRank.A, Strength = 37f,
                Description = "薄く鍛えられた刃が、風を裂いて敵を刻む。",
                ModelPrefab = "Sword13_Blue.prefab",
                Blade = new Color32(110, 170, 230, 255), Hilt = new Color32(50, 60, 110, 255),
            };
        }

        // ---- 共通のデータ ----------------------------------------------

        private static WeaponRankTable WriteRankTable()
        {
            // 前は Items/Weapons に置いていた。GUID を保ったまま Resources へ移す。
            Gen.EnsureFolder(RankTablePath.Substring(0, RankTablePath.LastIndexOf('/')));
            if (AssetDatabase.LoadAssetAtPath<WeaponRankTable>(OldRankTablePath) != null
                && AssetDatabase.LoadAssetAtPath<WeaponRankTable>(RankTablePath) == null)
            {
                string error = AssetDatabase.MoveAsset(OldRankTablePath, RankTablePath);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"ランク表を移せなかった: {error}");
            }

            var table = Gen.LoadOrCreate<WeaponRankTable>(RankTablePath);
            // Notion の色設定（ユニーク=orange、S=yellow、A=purple、B=blue、C=green、D=brown、E=gray）を仮採用。
            var entries = new (WeaponRank rank, string hex)[]
            {
                (WeaponRank.Unique, "#D9730D"),
                (WeaponRank.S, "#CB912F"),
                (WeaponRank.A, "#9065B0"),
                (WeaponRank.B, "#337EA9"),
                (WeaponRank.C, "#448361"),
                (WeaponRank.D, "#9F6B53"),
                (WeaponRank.E, "#787774"),
            };

            var serialized = new SerializedObject(table);
            SerializedProperty list = serialized.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                ColorUtility.TryParseHtmlString(entries[i].hex, out Color color);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("rank").enumValueIndex = RankEnumIndex(entries[i].rank);
                entry.FindPropertyRelative("color").colorValue = color;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private static int RankEnumIndex(WeaponRank rank) => Array.IndexOf(Enum.GetValues(typeof(WeaponRank)), rank);

        private static EnchantmentRollSettings WriteRollSettings()
        {
            var settings = Gen.LoadOrCreate<EnchantmentRollSettings>(RollSettingsPath);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("continueChance").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static Dictionary<EnchantmentKind, EnchantmentDefinition> WriteEnchantments()
        {
            // 効果量は仮。割合は 0.15 で +15%。効果を実装しているのは片手近距離に付く 9 種だけ。
            var specs = new (EnchantmentKind kind, string name, float amount, float secondary, string description)[]
            {
                (EnchantmentKind.DamageUp, "ダメージ増加", 0.15f, 0f, "与えるダメージが 15% 上がる。"),
                (EnchantmentKind.CritChance, "クリティカル率", 0.05f, 0f, "クリティカルの出る確率が 5% 上がる。"),
                (EnchantmentKind.DropUp, "ドロップ増加", 0.10f, 0f, "倒した敵が武器を落とす確率が 10% 上がる。"),
                (EnchantmentKind.RapidFire, "速射", 0.10f, 0f, "攻撃の速さが 10% 上がる。"),
                (EnchantmentKind.ProjectileCount, "数", 1f, 0f, "飛び道具の数が 1 つ増える。"),
                (EnchantmentKind.Size, "サイズ", 0.15f, 0f, "攻撃の届く範囲が 15% 広がる。"),
                (EnchantmentKind.Duration, "持続時間", 0.20f, 0f, "効果の続く時間が 20% 延びる。"),
                (EnchantmentKind.Pierce, "貫通", 1f, 0f, "飛び道具が敵を 1 体多く貫く。"),
                (EnchantmentKind.Multishot, "多重", 1f, 0f, "一度に放つ数が 1 つ増える。"),
                (EnchantmentKind.HealUp, "回復量増加", 0.20f, 0f, "回復する量が 20% 増える。"),
                (EnchantmentKind.Homing, "ホーミング", 1f, 0f, "飛び道具が敵を追う。"),
                (EnchantmentKind.ChargeTimeDown, "チャージ時間減少", 0.15f, 0f, "溜めにかかる時間が 15% 縮む。"),
                (EnchantmentKind.Stun, "スタン", 0.25f, 0f, "敵をスタン・気絶させやすくなる（一撃の重さ 25% 増しで判定）。"),
                (EnchantmentKind.ProjectileSpeed, "弾速", 0.20f, 0f, "飛び道具が 20% 速く飛ぶ。"),
                (EnchantmentKind.Knockback, "ノックバック", 2f, 0f, "当てた敵を押し出す勢いが増す。"),
                (EnchantmentKind.Explosion, "爆発", 0.40f, 2.5f, "当てた所で爆発し、周りの敵にダメージの 40% を与える。"),
                (EnchantmentKind.ComboBonus, "コンボボーナス", 0.10f, 0f, "コンボの段が進むごとにダメージが 10% 上がる。"),
                (EnchantmentKind.MoveSpeed, "移動速度", 0.10f, 0f, "歩く速さが 10% 上がる。"),
                (EnchantmentKind.Exp, "経験値", 0.10f, 0f, "得られる経験値が 10% 増える。"),
                (EnchantmentKind.MaxHp, "体力増加", 0.10f, 0f, "最大 HP が 10% 増える。"),
                (EnchantmentKind.Defense, "防御力", 0.10f, 0f, "受けるダメージが 10% 減る。"),
                (EnchantmentKind.Regen, "自然回復", 1f, 0f, "HP が毎秒 1 ずつ回復する。"),
            };

            var result = new Dictionary<EnchantmentKind, EnchantmentDefinition>();
            foreach (var spec in specs)
            {
                var definition = Gen.LoadOrCreate<EnchantmentDefinition>($"{EnchantmentsFolder}/Enchant_{spec.kind}.asset");
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("kind").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(EnchantmentKind)), spec.kind);
                serialized.FindProperty("displayName").stringValue = spec.name;
                serialized.FindProperty("description").stringValue = spec.description;
                serialized.FindProperty("amount").floatValue = spec.amount;
                serialized.FindProperty("secondaryAmount").floatValue = spec.secondary;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                result[spec.kind] = definition;
            }

            return result;
        }

        private static WeaponTypeDefinition WriteOneHandedType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(OneHandedTypePath);
            var serialized = new SerializedObject(type);
            serialized.FindProperty("id").stringValue = "01";
            serialized.FindProperty("displayName").stringValue = "片手近距離";
            serialized.FindProperty("animatorWeaponType").intValue = 1; // CharacterAnimatorBuilder.Weapon.OneHanded
            serialized.FindProperty("canUseShield").boolValue = true;

            var allowed = new[]
            {
                EnchantmentKind.DamageUp, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.Size,
                EnchantmentKind.RapidFire, EnchantmentKind.Stun, EnchantmentKind.Knockback, EnchantmentKind.Explosion,
                EnchantmentKind.ComboBonus,
            };
            SerializedProperty list = serialized.FindProperty("allowedEnchantments");
            list.arraySize = allowed.Length;
            for (int i = 0; i < allowed.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[allowed[i]];

            serialized.FindProperty("enchantmentRoll").objectReferenceValue = roll;
            serialized.FindProperty("characterAttackWeight").floatValue = 1f;
            serialized.FindProperty("baseCritChance").floatValue = 0.05f;
            serialized.FindProperty("baseCritMultiplier").floatValue = 1.5f;

            // 素早い 3 振りと、溜めの長い重い 4 段目。値は仮。
            var steps = new (float weight, float duration, float hit, Vector3 size, Vector3 center, float knockback)[]
            {
                (1f, 0.45f, 0.18f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f),
                (1f, 0.45f, 0.18f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f),
                (1f, 0.50f, 0.20f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f),
                (2f, 0.80f, 0.40f, new Vector3(2.0f, 1.2f, 1.8f), new Vector3(0f, 1f, 1.2f), 3f),
            };
            SerializedProperty combo = serialized.FindProperty("comboSteps");
            combo.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                SerializedProperty step = combo.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("damageWeight").floatValue = steps[i].weight;
                step.FindPropertyRelative("duration").floatValue = steps[i].duration;
                step.FindPropertyRelative("hitTime").floatValue = steps[i].hit;
                step.FindPropertyRelative("hitboxSize").vector3Value = steps[i].size;
                step.FindPropertyRelative("hitboxCenter").vector3Value = steps[i].center;
                step.FindPropertyRelative("knockback").floatValue = steps[i].knockback;
            }

            serialized.FindProperty("comboChainGrace").floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        // ---- 武器 --------------------------------------------------------

        private static void WriteWeapon(WeaponSpec spec, WeaponTypeDefinition type)
        {
            var weapon = Gen.LoadOrCreate<WeaponDefinition>($"{WeaponsFolder}/{spec.Id}.asset");

            var iconSpec = new Gen.Spec
            {
                Id = spec.Id,
                Name = spec.Name,
                Description = spec.Description,
            };
            Texture2D icon = WriteModelIcon(spec);

            // 床に落ちているときは寝かせる。
            iconSpec.BuildModel = (root, mat) =>
            {
                root.transform.localPosition = new Vector3(0f, 0.05f, -SwordLength * 0.5f);
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                BuildSword(root, spec, mat);
            };
            ItemPickup pickup = Gen.BuildPickupPrefab(iconSpec, weapon);
            FitPickupVolume(pickup);
            GameObject held = BuildHeldPrefab(spec);

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("description").stringValue = spec.Description;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("worldPrefab").objectReferenceValue = pickup;
            serialized.FindProperty("rank").enumValueIndex = RankEnumIndex(spec.Rank);
            serialized.FindProperty("strength").floatValue = spec.Strength;
            serialized.FindProperty("weaponType").objectReferenceValue = type;
            serialized.FindProperty("heldModel").objectReferenceValue = held;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>拾える物の判定を剣の形に合わせる。</summary>
        private static void FitPickupVolume(ItemPickup pickup)
        {
            string path = AssetDatabase.GetAssetPath(pickup);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var volume = root.GetComponent<BoxCollider>();
                volume.size = PickupVolume;
                volume.center = new Vector3(0f, PickupVolume.y * 0.5f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>手に持つ見た目。柄の根元が原点で、刃は +Y に伸びる。当たり判定は持たない。</summary>
        private static GameObject BuildHeldPrefab(WeaponSpec spec)
        {
            var root = new GameObject($"Held_{spec.Id}");
            BuildSword(root, spec, Gen.Material);

            string path = $"{Gen.PrefabsFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 剣を parent の下に作る。柄の根元が原点、刃は +Y。
        /// FreeSwords の素材があれば、いちばん長い向きを +Y に向けて長さを揃えて入れる。無ければプリミティブで組む。
        /// </summary>
        private static void BuildSword(GameObject parent, WeaponSpec spec, Func<string, Color, Material> material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(FreeSwords + spec.ModelPrefab);
            if (source != null && TryPlaceModel(parent, source)) return;

            Material blade = material($"Placeholder_{spec.Id}_Blade", spec.Blade);
            Material hilt = material($"Placeholder_{spec.Id}_Hilt", spec.Hilt);
            Gen.Part(parent, PrimitiveType.Cylinder, hilt, new Vector3(0f, 0.08f, 0f), new Vector3(0.035f, 0.08f, 0.035f));
            Gen.Part(parent, PrimitiveType.Cube, hilt, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.03f, 0.04f));
            Gen.Part(parent, PrimitiveType.Cube, blade, new Vector3(0f, 0.17f + (SwordLength - 0.17f) * 0.5f, 0f),
                new Vector3(0.06f, SwordLength - 0.17f, 0.012f));
        }

        private static bool TryPlaceModel(GameObject parent, GameObject source)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            if (!TryMeasure(model, out Bounds bounds))
            {
                Object.DestroyImmediate(model);
                return false;
            }

            // いちばん長い軸を +Y に。どちらの端が柄かは素材によるので、向きは確かめて武器種の持ち方で合わせる。
            Vector3 size = bounds.size;
            Quaternion toUp = size.y >= size.x && size.y >= size.z ? Quaternion.identity
                : size.x >= size.z ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.Euler(-90f, 0f, 0f);
            float length = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = length > 1e-4f ? SwordLength / length : 1f;

            var pivot = new GameObject("Model");
            pivot.transform.SetParent(parent.transform, false);
            model.transform.SetParent(pivot.transform, false);
            model.transform.localRotation = toUp;
            model.transform.localScale = Vector3.one * scale;

            // 回したあとの下端を原点へ。
            TryMeasure(model, out Bounds rotated, pivot.transform);
            model.transform.localPosition = new Vector3(-rotated.center.x, -rotated.min.y, -rotated.center.z);
            return true;
        }

        /// <summary>レンダラーを合わせた境界。space を渡すとその座標系で測る。</summary>
        private static bool TryMeasure(GameObject model, out Bounds bounds, Transform space = null)
        {
            bounds = default;
            bool any = false;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = (space != null ? space.worldToLocalMatrix : Matrix4x4.identity) * filter.transform.localToWorldMatrix;
                Vector3 min = local.min;
                Vector3 max = local.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }

            return any;
        }

        // ---- モデルを撮った絵 ----------------------------------------------

        /// <summary>
        /// 手に持つ見た目と同じ剣をプレビュー用のシーンに置き、斜め 45° に立てて正面から撮る。背景は透明。
        /// 刃の平たい面がカメラを向くよう、横幅の広い向きを画面の横に合わせてから少しひねる。
        /// </summary>
        private static Texture2D WriteModelIcon(WeaponSpec spec)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var model = new GameObject("IconModel");
            var rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(model.transform, false);
                BuildSword(pivot, spec, Gen.Material);
                SceneManager.MoveGameObjectToScene(model, scene);

                // 刃の平たい面を画面に向ける（横幅の広い軸を X に）。少しひねって立体に見せ、45° 傾けて右上へ刃を伸ばす。
                Bounds upright = RendererBounds(model);
                float face = upright.size.z > upright.size.x ? 90f : 0f;
                pivot.transform.localRotation = Quaternion.Euler(0f, face, 0f);
                model.transform.rotation = Quaternion.Euler(0f, 0f, -45f) * Quaternion.Euler(0f, 25f, 0f);

                Bounds bounds = RendererBounds(model);
                var cameraObject = new GameObject("IconCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.transform.SetPositionAndRotation(bounds.center - Vector3.forward * 5f, Quaternion.identity);
                camera.targetTexture = rt;

                AddLight(scene, Quaternion.Euler(35f, -30f, 0f), 1.2f);
                AddLight(scene, Quaternion.Euler(-20f, 150f, 0f), 0.5f);

                Texture2D texture = Capture(camera, rt, Color.clear);
                // URP が背景の透明を残さなかったら、抜き取り色で撮り直して色で抜く。
                if (texture.GetPixel(0, 0).a > 0.5f)
                {
                    Object.DestroyImmediate(texture);
                    texture = Capture(camera, rt, KeyColor);
                    KeyOut(texture);
                }

                Texture2D icon = Gen.SaveIcon(texture, spec.Id);
                Object.DestroyImmediate(texture);
                return icon;
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddLight(Scene scene, Quaternion rotation, float intensity)
        {
            var go = new GameObject("IconLight");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        private static Texture2D Capture(Camera camera, RenderTexture rt, Color background)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        /// <summary>抜き取り色に近い画素を透明にする。縁はにじんだ分だけ半透明に。</summary>
        private static void KeyOut(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                // 抜き取り色（マゼンタ）らしさ = 赤と青が緑よりどれだけ強いか。
                float key = Mathf.Clamp01(Mathf.Min(c.r, c.b) - c.g);
                float alpha = 1f - Mathf.SmoothStep(0.35f, 0.85f, key);
                pixels[i] = new Color(c.r, c.g, c.b, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.5f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
