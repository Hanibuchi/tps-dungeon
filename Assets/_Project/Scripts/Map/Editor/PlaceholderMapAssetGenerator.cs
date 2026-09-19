using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 手作りの部屋テンプレートが揃うまでの間、生成器を動かして確認できるようにするための
    /// プレースホルダ一式（部屋 Prefab・共通パーツ・カタログ・FloorConfig）をコードから作る。
    /// 本物のテンプレートができたらカタログの中身を差し替えればよく、このスクリプトは不要になる。
    /// 何度実行しても同じ結果になる（既存アセットは上書きされる）。
    /// </summary>
    public static class PlaceholderMapAssetGenerator
    {
        private const string RoomsFolder = "Assets/_Project/Prefabs/Rooms/L1";
        private const string PartsFolder = "Assets/_Project/Prefabs/Parts";
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string SettingsFolder = "Assets/_Project/Settings/Map";

        private const float CellSize = 3f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.15f;
        private const float FloorThickness = 0.1f;

        private readonly struct RoomSpec
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            public readonly RoomTag Tags;
            public readonly bool AllowRotation;
            public readonly float Weight;
            public readonly string MaterialKey;

            public RoomSpec(string name, int width, int height, RoomTag tags, bool allowRotation, float weight, string materialKey)
            {
                Name = name;
                Width = width;
                Height = height;
                Tags = tags;
                AllowRotation = allowRotation;
                Weight = weight;
                MaterialKey = materialKey;
            }
        }

        /// <summary>
        /// 第1層「玄関」の部屋構成に名前を寄せたプレースホルダ。
        /// FloorConfig の minLeafSize(14) から leafPadding(1) を引いた 12x12 に収まるサイズにしてある。
        /// </summary>
        private static readonly RoomSpec[] Rooms =
        {
            new RoomSpec("Room_EntranceHall_12x10", 12, 10, RoomTag.Normal, true, 0.5f, "Floor"),
            new RoomSpec("Room_Chapel_10x8", 10, 8, RoomTag.Normal, true, 0.8f, "Floor"),
            new RoomSpec("Room_Guardpost_8x6", 8, 6, RoomTag.Normal, true, 1.0f, "Floor"),
            new RoomSpec("Room_Antechamber_6x6", 6, 6, RoomTag.Normal, true, 1.0f, "Floor"),
            new RoomSpec("Room_Shop_8x8", 8, 8, RoomTag.Shop, false, 1.0f, "Shop"),
            new RoomSpec("Room_Stair_6x6", 6, 6, RoomTag.Stair, true, 1.0f, "Stair"),
        };

        [MenuItem("Tools/TPS Dungeon/プレースホルダのマップ一式を生成")]
        public static void Generate()
        {
            EnsureFolder(RoomsFolder);
            EnsureFolder(PartsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(SettingsFolder);

            var materials = new Dictionary<string, Material>
            {
                ["Floor"] = Material("Placeholder_Floor", new Color(0.55f, 0.53f, 0.50f)),
                ["Wall"] = Material("Placeholder_Wall", new Color(0.35f, 0.34f, 0.33f)),
                ["Door"] = Material("Placeholder_Door", new Color(0.65f, 0.35f, 0.15f)),
                ["Corridor"] = Material("Placeholder_Corridor", new Color(0.42f, 0.41f, 0.40f)),
                ["Shop"] = Material("Placeholder_Shop", new Color(0.85f, 0.72f, 0.18f)),
                ["Stair"] = Material("Placeholder_Stair", new Color(0.35f, 0.55f, 0.75f)),
                ["StairUp"] = Material("Placeholder_StairUp", new Color(0.25f, 0.85f, 0.35f)),
                ["StairDown"] = Material("Placeholder_StairDown", new Color(0.95f, 0.45f, 0.15f)),
            };

            var parts = BuildParts(materials);
            var roomPrefabs = new List<GameObject>();
            foreach (var spec in Rooms) roomPrefabs.Add(BuildRoomPrefab(spec, materials[spec.MaterialKey]));

            var catalog = BuildCatalog(roomPrefabs);
            BuildFloorConfig(catalog, parts);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"プレースホルダのマップ一式を生成した: 部屋 {roomPrefabs.Count} 種類 + 共通パーツ {parts.Count} 種類");
        }

        private static Dictionary<string, GameObject> BuildParts(Dictionary<string, Material> materials)
        {
            var parts = new Dictionary<string, GameObject>
            {
                // 壁とドアは FloorBuilder が localScale の x と y を (cellSize, wallHeight) に上書きするので、
                // ルートは 1x1 の単位で作り、厚みだけ子の z スケールで持たせる。
                ["Wall"] = BuildPanelPrefab("Wall_Basic", materials["Wall"]),
                ["Door"] = BuildPanelPrefab("Door_Basic", materials["Door"]),
                ["CorridorFloor"] = BuildTilePrefab("Corridor_Floor", materials["Corridor"]),
                ["StairUp"] = BuildMarkerPrefab("Stair_Up", materials["StairUp"], PrimitiveType.Cube),
                ["StairDown"] = BuildMarkerPrefab("Stair_Down", materials["StairDown"], PrimitiveType.Cube),
                ["ShopMarker"] = BuildMarkerPrefab("Shop_Marker", materials["Shop"], PrimitiveType.Cylinder),
            };
            return parts;
        }

        /// <summary>壁・ドア用。ルートのスケールが (cellSize, wallHeight, 1) に上書きされる前提。</summary>
        private static GameObject BuildPanelPrefab(string name, Material material)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            // ルートの原点を床面に置きたいので、板は上方向に半分ずらす。
            mesh.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            mesh.transform.localScale = new Vector3(1f, 1f, WallThickness);
            Paint(mesh, material);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>廊下の床タイル。FloorBuilder はスケールを触らないので実寸で作る。</summary>
        private static GameObject BuildTilePrefab(string name, Material material)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            mesh.transform.localScale = new Vector3(CellSize, FloorThickness, CellSize);
            Paint(mesh, material);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        private static GameObject BuildMarkerPrefab(string name, Material material, PrimitiveType primitive)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(primitive);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            mesh.transform.localScale = new Vector3(1.4f, primitive == PrimitiveType.Cylinder ? 0.75f : 1.5f, 1.4f);
            Paint(mesh, material);

            return SavePrefab(root, $"{PartsFolder}/{name}.prefab");
        }

        /// <summary>
        /// 部屋 Prefab。床とドア候補だけを持ち、外周壁は持たない（FloorBuilder が建てる）。
        /// ローカル原点はセル (0,0) の角。
        /// </summary>
        private static GameObject BuildRoomPrefab(RoomSpec spec, Material material)
        {
            var root = new GameObject(spec.Name);
            var template = root.AddComponent<RoomTemplate>();
            template.Configure(new Vector2Int(spec.Width, spec.Height), CellSize, spec.Tags, spec.AllowRotation, spec.Weight);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(
                spec.Width * CellSize * 0.5f, -FloorThickness * 0.5f, spec.Height * CellSize * 0.5f);
            floor.transform.localScale = new Vector3(spec.Width * CellSize, FloorThickness, spec.Height * CellSize);
            Paint(floor, material);

            var socketsRoot = new GameObject("DoorSockets");
            socketsRoot.transform.SetParent(root.transform, false);

            foreach (var data in RoomTemplateFactory.CreateDefaultSockets(spec.Width, spec.Height))
            {
                var socketObject = new GameObject($"Socket_{data.Facing}_{data.LocalCell.X}_{data.LocalCell.Y}");
                socketObject.transform.SetParent(socketsRoot.transform, false);

                var socket = socketObject.AddComponent<DoorSocket>();
                socket.Configure(new Vector2Int(data.LocalCell.X, data.LocalCell.Y), data.Facing);
                socket.SnapToCell(CellSize);
            }

            return SavePrefab(root, $"{RoomsFolder}/{spec.Name}.prefab");
        }

        private static RoomTemplateCatalog BuildCatalog(List<GameObject> roomPrefabs)
        {
            string path = $"{SettingsFolder}/L1_RoomCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<RoomTemplateCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomTemplateCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            var entries = new List<RoomTemplateCatalog.Entry>();
            foreach (var prefab in roomPrefabs) entries.Add(new RoomTemplateCatalog.Entry { prefab = prefab });
            catalog.SetEntries(entries);

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static FloorConfig BuildFloorConfig(RoomTemplateCatalog catalog, Dictionary<string, GameObject> parts)
        {
            string path = $"{SettingsFolder}/Floor_L1_Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FloorConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.widthInCells = 64;
            config.heightInCells = 64;
            config.cellSize = CellSize;
            config.minLeafSize = 14;
            config.maxDepth = 4;
            config.leafPadding = 1;
            config.extraEdgeCount = 2;
            config.corridorTurnPenalty = 4;
            config.includeShop = true;
            config.roomCatalog = catalog;
            config.wallPrefab = parts["Wall"];
            config.doorPrefab = parts["Door"];
            config.corridorFloorPrefab = parts["CorridorFloor"];
            config.stairUpPrefab = parts["StairUp"];
            config.stairDownPrefab = parts["StairDown"];
            config.shopMarkerPrefab = parts["ShopMarker"];

            EditorUtility.SetDirty(config);
            return config;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(DefaultShader());
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader DefaultShader()
        {
            // このプロジェクトは URP なので Lit を使う。見つからないときだけ組み込みにフォールバックする。
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? shader : Shader.Find("Standard");
        }

        private static void Paint(GameObject target, Material material)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
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
