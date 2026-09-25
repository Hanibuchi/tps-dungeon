using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 本物の素材が揃うまで、拾う・インベントリを確かめるための仮アイテム一式をコードから作る。
    /// - アイテムの定義（ItemDefinition）: Assets/_Project/Items/
    /// - 枠に出す絵（64px の PNG を手続きで描く）: Assets/_Project/Items/Icons/
    /// - 床に落ちている拾える物（プリミティブを組んだプレハブ）: Assets/_Project/Prefabs/Items/
    /// 何度実行しても同じ結果になる（既存アセットは上書きされる）。
    /// </summary>
    public static class PlaceholderItemAssetGenerator
    {
        public const string ItemsFolder = "Assets/_Project/Items";
        private const string IconsFolder = ItemsFolder + "/Icons";
        private const string PrefabsFolder = "Assets/_Project/Prefabs/Items";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";

        private const int IconSize = 64;

        // 照準で狙いやすいよう、見た目より一回り大きい判定を張る。
        private static readonly Vector3 PickupVolume = new Vector3(0.5f, 0.5f, 0.5f);

        private static readonly Color Outline = new Color32(18, 14, 11, 255);

        private sealed class Spec
        {
            public string Id;
            public string Name;
            public string Description;
            public Action<GameObject, Func<string, Color, Material>> BuildModel;
            public (Color color, Func<Vector2, float> shape)[] IconLayers;
        }

        [MenuItem("Tools/TPS Dungeon/プレースホルダのアイテムを生成")]
        public static void Generate()
        {
            EnsureFolder(ItemsFolder);
            EnsureFolder(IconsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(MaterialsFolder);

            foreach (Spec spec in Specs())
            {
                Texture2D icon = WriteIcon(spec);
                ItemDefinition definition = LoadOrCreate<ItemDefinition>($"{ItemsFolder}/{spec.Id}.asset");
                ItemPickup prefab = BuildPickupPrefab(spec, definition);

                var serialized = new SerializedObject(definition);
                serialized.FindProperty("id").stringValue = spec.Id;
                serialized.FindProperty("displayName").stringValue = spec.Name;
                serialized.FindProperty("description").stringValue = spec.Description;
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("worldPrefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"仮アイテムを作った: {ItemsFolder}");
        }

        /// <summary>生成器が作ったアイテムの定義を全部読む。確認用シーンに置くときに使う。</summary>
        public static List<ItemDefinition> LoadAll()
        {
            var result = new List<ItemDefinition>();
            foreach (Spec spec in Specs())
            {
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{spec.Id}.asset");
                if (definition != null) result.Add(definition);
            }

            return result;
        }

        private static IEnumerable<Spec> Specs()
        {
            Color red = new Color32(196, 44, 40, 255);
            Color redLight = new Color32(244, 140, 120, 255);
            Color glass = new Color32(200, 214, 220, 255);
            Color cork = new Color32(140, 96, 56, 255);
            Color gold = new Color32(214, 170, 92, 255);
            Color goldLight = new Color32(248, 220, 150, 255);
            Color wood = new Color32(110, 72, 40, 255);
            Color flame = new Color32(236, 120, 40, 255);
            Color flameCore = new Color32(252, 214, 110, 255);
            Color gem = new Color32(70, 170, 214, 255);
            Color gemLight = new Color32(190, 236, 250, 255);

            yield return new Spec
            {
                Id = "Potion",
                Name = "回復薬",
                Description = "赤く澄んだ薬。一息に飲めば、浅い傷ならたちまち塞がるという。",
                BuildModel = (root, mat) =>
                {
                    Part(root, PrimitiveType.Sphere, mat("Placeholder_ItemPotion", red), new Vector3(0f, 0.12f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
                    Part(root, PrimitiveType.Cylinder, mat("Placeholder_ItemGlass", glass), new Vector3(0f, 0.25f, 0f), new Vector3(0.07f, 0.05f, 0.07f));
                    Part(root, PrimitiveType.Cylinder, mat("Placeholder_ItemCork", cork), new Vector3(0f, 0.31f, 0f), new Vector3(0.08f, 0.025f, 0.08f));
                },
                IconLayers = new[]
                {
                    (glass, Box(new Vector2(32, 44), new Vector2(6, 9))),
                    (cork, Box(new Vector2(32, 55), new Vector2(8, 4))),
                    (red, Circle(new Vector2(32, 25), 17)),
                    (redLight, Circle(new Vector2(25, 30), 4.5f)),
                },
            };

            yield return new Spec
            {
                Id = "OldKey",
                Name = "古びた鍵",
                Description = "錆の浮いた真鍮の鍵。この迷宮のどこかに、これで開く扉があるはずだ。",
                BuildModel = (root, mat) =>
                {
                    Material brass = mat("Placeholder_ItemBrass", gold);
                    Part(root, PrimitiveType.Cylinder, brass, new Vector3(-0.12f, 0.03f, 0f), new Vector3(0.12f, 0.02f, 0.12f));
                    Part(root, PrimitiveType.Cube, brass, new Vector3(0.05f, 0.03f, 0f), new Vector3(0.24f, 0.03f, 0.04f));
                    Part(root, PrimitiveType.Cube, brass, new Vector3(0.14f, 0.03f, -0.04f), new Vector3(0.03f, 0.03f, 0.06f));
                    Part(root, PrimitiveType.Cube, brass, new Vector3(0.08f, 0.03f, -0.035f), new Vector3(0.03f, 0.03f, 0.05f));
                },
                IconLayers = new[]
                {
                    (gold, Subtract(Circle(new Vector2(19, 45), 12), Circle(new Vector2(19, 45), 5.5f))),
                    (gold, Rotated(Box(new Vector2(37, 27), new Vector2(18, 3.5f)), new Vector2(37, 27), -45f)),
                    (gold, Rotated(Box(new Vector2(46, 12), new Vector2(3, 6)), new Vector2(46, 12), -45f)),
                    (gold, Rotated(Box(new Vector2(52, 18), new Vector2(3, 5)), new Vector2(52, 18), -45f)),
                    (goldLight, Circle(new Vector2(14, 51), 2.5f)),
                },
            };

            yield return new Spec
            {
                Id = "Torch",
                Name = "松明",
                Description = "油を染ませた布を巻いた棒。暗がりをほんの少しだけ押し返してくれる。",
                BuildModel = (root, mat) =>
                {
                    Part(root, PrimitiveType.Cylinder, mat("Placeholder_ItemWood", wood), new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.25f, 0.05f), Quaternion.Euler(0f, 0f, 80f));
                    Part(root, PrimitiveType.Sphere, mat("Placeholder_ItemFlame", flame), new Vector3(0.26f, 0.07f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
                },
                IconLayers = new[]
                {
                    (wood, Rotated(Box(new Vector2(28, 22), new Vector2(4, 19)), new Vector2(28, 22), 20f)),
                    (flame, Circle(new Vector2(36, 45), 11)),
                    (flame, Circle(new Vector2(38, 55), 6)),
                    (flameCore, Circle(new Vector2(36, 44), 5.5f)),
                },
            };

            yield return new Spec
            {
                Id = "BlueGem",
                Name = "蒼い宝石",
                Description = "冷たい光を宿した石。地上の商人に見せれば、きっと高く買い取ってくれる。",
                BuildModel = (root, mat) =>
                {
                    Part(root, PrimitiveType.Cube, mat("Placeholder_ItemGem", gem), new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.Euler(45f, 0f, 45f));
                },
                IconLayers = new[]
                {
                    (gem, Rhombus(new Vector2(32, 32), 17, 24)),
                    (gemLight, Rhombus(new Vector2(27, 39), 5, 7)),
                },
            };
        }

        // ---- 拾える物のプレハブ ------------------------------------------

        private static ItemPickup BuildPickupPrefab(Spec spec, ItemDefinition definition)
        {
            var root = new GameObject($"Pickup_{spec.Id}");

            var volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = PickupVolume;
            volume.center = new Vector3(0f, PickupVolume.y * 0.5f, 0f);

            var pickup = root.AddComponent<ItemPickup>();
            pickup.Definition = definition;

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            spec.BuildModel(model, Material);

            string path = $"{PrefabsFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ItemPickup>();
        }

        private static void Part(GameObject parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
        {
            Part(parent, type, material, position, scale, Quaternion.identity);
        }

        /// <summary>見た目だけの部品。当たり判定はルートの箱に任せるので、プリミティブのコライダは外す。</summary>
        private static void Part(GameObject parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent.transform, false);
            part.transform.SetLocalPositionAndRotation(position, rotation);
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- 絵 ----------------------------------------------------------

        /// <summary>
        /// 形を符号付き距離（内側が負、ピクセル単位）で重ねて描く。全体の外周に暗い縁を付け、境目は 1px でぼかす。
        /// 座標は左下が原点。
        /// </summary>
        private static Texture2D WriteIcon(Spec spec)
        {
            var pixels = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);

                    float union = float.MaxValue;
                    foreach (var layer in spec.IconLayers) union = Mathf.Min(union, layer.shape(p));

                    Color c = Over(Color.clear, Outline, Coverage(union - 2f));
                    foreach (var layer in spec.IconLayers) c = Over(c, layer.color, Coverage(layer.shape(p)));
                    pixels[y * IconSize + x] = c;
                }
            }

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = $"{IconsFolder}/Icon_{spec.Id}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        private static Color Over(Color under, Color color, float alpha)
        {
            if (alpha <= 0f) return under;

            float a = alpha + under.a * (1f - alpha);
            if (a <= 0f) return Color.clear;
            Color rgb = (color * alpha + under * under.a * (1f - alpha)) / a;
            rgb.a = a;
            return rgb;
        }

        private static Func<Vector2, float> Circle(Vector2 center, float radius)
        {
            return p => Vector2.Distance(p, center) - radius;
        }

        private static Func<Vector2, float> Box(Vector2 center, Vector2 half)
        {
            return p =>
            {
                Vector2 d = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
                return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            };
        }

        private static Func<Vector2, float> Rhombus(Vector2 center, float halfWidth, float halfHeight)
        {
            float scale = halfWidth * halfHeight / Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            return p => (Mathf.Abs(p.x - center.x) / halfWidth + Mathf.Abs(p.y - center.y) / halfHeight - 1f) * scale;
        }

        private static Func<Vector2, float> Rotated(Func<Vector2, float> shape, Vector2 pivot, float degrees)
        {
            float rad = -degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return p =>
            {
                Vector2 d = p - pivot;
                return shape(pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos));
            };
        }

        private static Func<Vector2, float> Subtract(Func<Vector2, float> shape, Func<Vector2, float> hole)
        {
            return p => Mathf.Max(shape(p), -hole(p));
        }

        // ---- 共通 --------------------------------------------------------

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
