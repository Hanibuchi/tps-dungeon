using System;
using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Enemies.Editor;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上（Surface）の景色を RPG Poly Pack Lite（ThirdParty/3D Model/RPGPP_LT）で置く。何度実行しても同じ配置になる（乱数のシードは固定）。
    /// - 歩ける範囲（地面の 100 m 四方）には村を置く。大門へ続く道、武器屋と銀行（入口は SceneGate）、民家・井戸・荷車・露店などの小物、端に木と岩。
    ///   当たりは見た目のメッシュそのまま（凸にしない）で、建物・木・岩・大きめの小物に付け（<see cref="RefitColliders"/>）、[Surface]/Village に入れて NavMesh に含める。
    ///   屋根に登ったり露店の屋根の下に入ったりできる。草花・茂みと小さな物には付けない。
    /// - 歩ける範囲の外（[Scenery]）は見せるだけ。見えない壁のすぐ外に森の帯、その先に草地・丘の輪・山・空のドーム・雲を置き、フォグで遠くを霞ませる。
    ///   当たりは付けず、NavMesh にも入れない。
    /// - 地面は素材の草地のタイル（凹凸が 0.3 m ほどある）で、歩ける範囲のタイルにはそのまま MeshCollider を付けて床にする。
    ///   平らな [Surface]/Plaza/Ground（ProBuilder の板）は描画も当たりも止める（平らな当たりと凹凸のある見た目がずれて、物もキャラも浮いて見えたため）。
    /// - 物は置く所の真下の見た目の地面の高さに合わせる。足元（大きさに応じた円）の一番低い所に置くので、坂でも端が浮かない。城の箱も少し沈める。
    /// 城（[Surface]/Castle）と見えない壁（[Bounds]）とパーティーには触らない。置き直すのは Village・[Scenery]・光と霧だけ。
    /// </summary>
    public static class SurfaceScenery
    {
        private const string Pack = "Assets/ThirdParty/3D Model/RPGPP_LT/Prefabs/";
        private const string NavMeshPath = "Assets/_Project/Scenes/Surface/NavMesh-Surface.asset";
        private const int Seed = 20261013;

        // 歩ける範囲（見えない壁の内側）。+Z が城。
        private const float MinX = -50f, MaxX = 50f, MinZ = -31f, MaxZ = 69f;

        // 城の 1 段目の足元（この中には物を置かない）と大門の前。
        private static readonly Rect CastleFoot = Rect.MinMaxRect(-33f, 26f, 33f, 64f);

        // 草地のタイルの上面は高さ 0.42〜0.46 に並ぶので、その分下げて地面の当たり（y = 0）に揃える。
        private const float GrassSink = -0.43f;

        // 置いた物を地面へ少しめり込ませる量。0 だと影の境目で浮いて見える。
        private const float Embed = 0.04f;

        // 城の箱を沈める量（地面の凹みの深い所に合わせる）。
        private const float CastleSink = -0.35f;

        [MenuItem("Tools/TPS Dungeon/地上の景色を置き直す")]
        public static void RebuildFromMenu() => Debug.Log(Rebuild());

        /// <summary>開いている地上のシーンに景色を置き直し、NavMesh を焼き直して保存する。</summary>
        public static string Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SurfaceSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "シーンを開き直せなかった。";
                scene = EditorSceneManager.OpenScene(SurfaceSceneBuilder.ScenePath, OpenSceneMode.Single);
            }

            GameObject surface = FindRoot(scene, "[Surface]");
            if (surface == null) return $"{scene.path} に [Surface] が無い。先に「地上シーンを作る」を実行すること。";

            string result = Build(scene, surface.transform);
            BakeNavMesh(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return result;
        }

        /// <summary>景色を置く（保存も NavMesh もしない）。生成器からも呼ぶ。</summary>
        internal static string Build(Scene scene, Transform surface)
        {
            var placer = new Placer(new Random(Seed));

            // 前回の分を消す（古い仮の拠点 Hub も）。
            DestroyChild(surface, "Village");
            DestroyChild(surface, "Hub");
            GameObject oldScenery = FindRoot(scene, "[Scenery]");
            if (oldScenery != null) Object.DestroyImmediate(oldScenery);

            Transform ground = surface.Find("Plaza/Ground");
            if (ground != null && ground.TryGetComponent(out MeshRenderer groundRenderer)) groundRenderer.enabled = false;
            if (ground != null && ground.TryGetComponent(out MeshCollider groundCollider)) groundCollider.enabled = false;

            // 城の箱の底は y = 0 の平らな面なので、凹んだ所で浮かないよう地面の凹凸の分だけ沈める。
            Transform castle = surface.Find("Castle");
            if (castle != null) castle.localPosition = new Vector3(0f, CastleSink, 0f);

            var village = new GameObject("Village").transform;
            village.SetParent(surface, false);

            var scenery = new GameObject("[Scenery]").transform;
            SceneManager.MoveGameObjectToScene(scenery.gameObject, scene);
            scenery.SetSiblingIndex(surface.GetSiblingIndex() + 1);

            BuildGround(placer, Group(village, "Ground"), Group(scenery, "Ground"));
            placer.Snap = true;
            BuildRoads(placer, Group(village, "Roads"));
            BuildBuildings(placer, Group(village, "Buildings"));
            BuildProps(placer, Group(village, "Props"));
            BuildInnerNature(placer, Group(village, "Nature"));
            BuildForest(placer, Group(scenery, "Forest"));
            placer.Snap = false;
            BuildFarLand(placer, Group(scenery, "Land"));
            BuildSky(placer, Group(scenery, "Sky"));
            placer.RemoveSamplingColliders();
            RefitColliders(village);
            MakePlantsBreakable(village);
            PlaceSpawnOnGround(surface, placer);
            ApplyLighting(scene);

            return $"地上の景色を置いた（村 {CountChildren(village)} 個、外の景色 {CountChildren(scenery)} 個）。";
        }

        // --- 地面・道 ---

        /// <summary>
        /// 草地を敷く。歩ける範囲のタイル（walkable、[Surface] の下）は当たりを付けて床にし、NavMesh にも入れる。
        /// 外のタイル（far）には、物を地面の高さに合わせるあいだだけ当たりを付けて、置き終えたら外す。
        /// </summary>
        private static void BuildGround(Placer p, Transform walkable, Transform far)
        {
            // 歩ける範囲とそのまわり。高さは変えず横だけ 1.6 倍（凹凸 0.45 m のまま）。
            Transform parent = walkable;
            for (float x = -66f; x <= 66f; x += 26f)
            {
                for (float z = -47f; z <= 85f; z += 26f)
                {
                    string tile = p.Chance(0.5f) ? "Nature/Terrain/rpgpp_lt_terrain_grass_01" : "Nature/Terrain/rpgpp_lt_terrain_grass_02";
                    GameObject inner = p.Place(tile, parent, new Vector3(x + p.Range(-2f, 2f), GrassSink, z + p.Range(-2f, 2f)), p.Quarter() + p.Range(-8f, 8f), new Vector3(1.6f, 1f, 1.6f), Collide.Ground, true);
                    p.AddGround(inner, false);
                }
            }

            // タイルは縁が不揃いで隙間ができるので、全体の下に 1 枚、ごく大きく伸ばして敷いておく（隙間から下が見えない）。
            // 当たりも付けたままにし、タイルの隙間に落ちても少し低い所で止まるようにする。
            GameObject under = p.Place("Nature/Terrain/rpgpp_lt_terrain_grass_01", far, new Vector3(0f, -0.75f, 19f), 0f, new Vector3(45f, 1f, 45f), Collide.Ground, true);
            p.AddGround(under, false);

            // 遠くは大きなタイルで、少し下に敷く（近くのタイルと重なっても下に隠れる）。
            const float step = 48f;
            for (float x = -330f; x <= 330f; x += step)
            {
                for (float z = -330f; z <= 330f; z += step)
                {
                    if (x > -70f && x < 70f && z > -50f && z < 88f) continue;
                    string tile = p.Chance(0.5f) ? "Nature/Terrain/rpgpp_lt_terrain_grass_01" : "Nature/Terrain/rpgpp_lt_terrain_grass_02";
                    GameObject outer = p.Place(tile, far, new Vector3(x + p.Range(-4f, 4f), -0.55f, z + p.Range(-4f, 4f)), p.Quarter() + p.Range(-10f, 10f), new Vector3(3.3f, 1.2f, 3.3f), Collide.Ground, true);
                    p.AddGround(outer, true);
                }
            }

            Physics.SyncTransforms();
        }

        private static void BuildRoads(Placer p, Transform parent)
        {
            // スポーン地点から大門へ南北にまっすぐ。土の道（長さ 11 m）を 90 度回して縦に並べる。
            int i = 0;
            for (float z = -28f; z <= 22f; z += 8.5f, i++)
                Path(p, parent, new Vector3(p.Range(-0.4f, 0.4f), z), 90f + p.Range(-5f, 5f), i);

            // 大門の前は板敷き。
            for (float x = -4.5f; x <= 2.5f; x += 4.2f)
            {
                for (float z = 23f; z <= 27.5f; z += 2.7f)
                {
                    string plank = p.Chance(0.5f) ? "Exterior/Wood_path/rpgpp_lt_wood_path_01a" : "Exterior/Wood_path/rpgpp_lt_wood_path_01b";
                    p.Place(plank, parent, new Vector3(x, 0.02f, z + 0.9f), p.Range(-3f, 3f), Vector3.one, Collide.None, true);
                }
            }

            // 武器屋・銀行（西）と民家（東）への枝道。
            Path(p, parent, new Vector3(-8.5f, 6f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(-15.5f, 6f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(-8.5f, -13f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(-15.5f, -13f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(9f, -14f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(9f, 8f), p.Range(-4f, 4f), i++);
            Path(p, parent, new Vector3(17.5f, 8f), p.Range(-4f, 4f), i++);

            // 井戸のまわりの砂地。
            // 砂地は広いので、真ん中の高さに合わせる（一番低い所に合わせると埋もれる）。
            p.Place("Nature/Terrain/rpgpp_lt_terrain_sand_01", parent, new Vector3(-8f, -0.2f, 0f), 20f, new Vector3(1.1f, 1f, 1.1f), Collide.None, true, footprint: 0f);
            p.Place("Nature/Terrain/rpgpp_lt_terrain_sand_01", parent, new Vector3(0f, -0.2f, -22f), 70f, new Vector3(1.2f, 1f, 1f), Collide.None, true, footprint: 0f);

            p.Block(Rect.MinMaxRect(-4f, -31f, 4f, 29f)); // 道
            p.Block(Rect.MinMaxRect(-21f, 3.5f, -3f, 8.5f));
            p.Block(Rect.MinMaxRect(-21f, -15.5f, -3f, -10.5f));
            p.Block(Rect.MinMaxRect(3f, -16.5f, 15f, -11.5f));
            p.Block(Rect.MinMaxRect(3f, 5.5f, 23f, 10.5f));
            p.Block(CastleFoot);
        }

        private static void Path(Placer p, Transform parent, Vector2 at, float yaw, int index)
        {
            string tile = index % 3 == 0 ? "Nature/Terrain/rpgpp_lt_terrain_path_01a" : "Nature/Terrain/rpgpp_lt_terrain_path_01b";
            // 道も凹凸があるので当たりを付けて床にする（草地より少し高い所を歩く）。
            GameObject path = p.Place(tile, parent, new Vector3(at.x, 0f, at.y), yaw, Vector3.one, Collide.Ground, true, footprint: 0.4f);
            p.AddGround(path, false);
        }

        // --- 建物 ---

        private static void BuildBuildings(Placer p, Transform parent)
        {
            // 向きは扉が向く方角で決める（90 で東、-90 で西）。
            GameObject shop = PlaceBuilding(p, "rpgpp_lt_building_02", parent, new Vector3(-25f, 0f, 6f), 90f);
            AddDoorGate(shop, "武器屋", "武器を売り買いできる。（準備中）");
            GameObject bank = PlaceBuilding(p, "rpgpp_lt_building_04", parent, new Vector3(-25f, 0f, -13f), 90f);
            AddDoorGate(bank, "銀行", "持ち物を預けられる。（準備中）");

            // 東の民家（入れない）。
            PlaceBuilding(p, "rpgpp_lt_building_01", parent, new Vector3(25f, 0f, -14f), -90f);
            PlaceBuilding(p, "rpgpp_lt_building_05", parent, new Vector3(30f, 0f, 8f), -90f);
            PlaceBuilding(p, "rpgpp_lt_building_03", parent, new Vector3(-36f, 0f, 20f), 90f);

            p.Block(Rect.MinMaxRect(-31f, 0f, -19f, 12f));
            p.Block(Rect.MinMaxRect(-31f, -19f, -19f, -7f));
            p.Block(Rect.MinMaxRect(17f, -20f, 33f, -8f));
            p.Block(Rect.MinMaxRect(22f, 1f, 38f, 15f));
            p.Block(Rect.MinMaxRect(-43f, 13f, -29f, 27f));
        }

        /// <summary>
        /// 建物の扉が向く向き（建物のローカルで、+Z からの角度）。building_04 だけは扉が -X の長い側にあり、ほかは +Z の妻側。
        /// </summary>
        public static float DoorYaw(string prefabName) => prefabName.EndsWith("building_04") ? -90f : 0f;

        /// <summary>扉が doorYaw の方角（ワールド）を向くように建物を置く。</summary>
        private static GameObject PlaceBuilding(Placer p, string prefabName, Transform parent, Vector3 at, float doorYaw)
        {
            return p.Place("Buildings/Bld_closed/" + prefabName, parent, at, doorYaw - DoorYaw(prefabName), Vector3.one, Collide.None, true);
        }

        /// <summary>建物の扉の前に照準用のトリガーを張り、SceneGate（閉じている）を付ける。扉は <see cref="DoorYaw"/> の向きの面の真ん中。</summary>
        private static void AddDoorGate(GameObject building, string title, string body)
        {
            var door = new GameObject("Door");
            door.transform.SetParent(building.transform, false);
            PlaceDoor(building, door.transform);
            var trigger = door.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 2.6f, 0.8f);

            var gate = door.AddComponent<SceneGate>();
            var serialized = new SerializedObject(gate);
            serialized.FindProperty("sceneName").stringValue = string.Empty;
            serialized.FindProperty("open").boolValue = false;
            serialized.FindProperty("openLabel").stringValue = "入る";
            serialized.FindProperty("closedLabel").stringValue = "準備中";
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>扉のトリガーを、建物の扉の面の真ん中の少し外に置き、その面に向ける。</summary>
        public static void PlaceDoor(GameObject building, Transform door)
        {
            Bounds local = LocalBounds(building);
            float yaw = DoorYaw(SourceName(building));
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float reach = Mathf.Abs(dir.x) * local.extents.x + Mathf.Abs(dir.z) * local.extents.z + 0.3f;
            Vector3 at = local.center + dir * reach;
            door.localPosition = new Vector3(at.x, 1.3f, at.z);
            door.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // --- 小物 ---

        private static void BuildProps(Placer p, Transform parent)
        {
            const string c = "Props/Containers/rpgpp_lt_";

            // 武器屋の前の露店。
            p.Place("Exterior/Wood_path/rpgpp_lt_awning_standing_01a", parent, new Vector3(-16.5f, 0f, 13f), 90f, Vector3.one, Collide.None, true);
            GameObject crate = p.Place(c + "crate_01", parent, new Vector3(-14.8f, 0f, 14.5f), 10f, Vector3.one, Collide.None, true);
            PlaceOn(p, crate, c + "crate_02", parent, new Vector3(-14.9f, 0.4f, 14.4f), -15f);
            p.Place(c + "basket_01", parent, new Vector3(-16.2f, 0f, 11.4f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "barrel_01", parent, new Vector3(-18.6f, 0f, 15.2f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "barrel_02", parent, new Vector3(-19.6f, 0f, 14.5f), 40f, Vector3.one, Collide.None, true);
            p.Place(c + "vase_01", parent, new Vector3(-19.4f, 0f, 10.3f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "vase_02", parent, new Vector3(-19.0f, 0f, 9.8f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "sack_02_set", parent, new Vector3(-19.6f, 0f, 1.2f), 80f, Vector3.one, Collide.None, true);
            p.Place("Props/Shields/rpgpp_lt_shield_wall_01a", parent, new Vector3(-20.2f, 2.2f, 3.4f), 90f, Vector3.one, Collide.None, true);
            p.Place("Props/Shields/rpgpp_lt_shield_wall_01b", parent, new Vector3(-20.2f, 2.2f, 8.6f), 90f, Vector3.one, Collide.None, true);

            // 銀行の前。
            p.Place("Props/Benches/rpgpp_lt_bench_wood_03", parent, new Vector3(-19.4f, 0f, -17.5f), 90f, Vector3.one, Collide.None, true);
            p.Place(c + "box_wood_01", parent, new Vector3(-19.8f, 0.4f, -8.8f), 15f, Vector3.one, Collide.None, true);
            p.Place(c + "package_01", parent, new Vector3(-18.6f, 0.3f, -8.2f), -20f, Vector3.one, Collide.None, true);
            p.Place(c + "jug_01", parent, new Vector3(-19.5f, 0f, -18.5f), 0f, Vector3.one, Collide.None, true);

            // 井戸の広場。
            p.Place("Props/Misc/rpgpp_lt_well_01", parent, new Vector3(-8f, 0f, 0f), 25f, Vector3.one, Collide.None, true);
            p.Place(c + "trough_01", parent, new Vector3(-10.8f, 0f, -2.6f), 70f, Vector3.one, Collide.None, true);
            p.Place(c + "bucket_01", parent, new Vector3(-6.2f, 0f, 1.6f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "bowl_metal_01", parent, new Vector3(-9.6f, 0f, 2.6f), 0f, Vector3.one, Collide.None, true);
            p.Place("Props/Benches/rpgpp_lt_bench_wood_01", parent, new Vector3(-5f, 0f, -4.5f), 70f, Vector3.one, Collide.None, true);
            p.Block(Rect.MinMaxRect(-13f, -5f, -4f, 4f));

            // 道沿い。
            p.Place("Props/Benches/rpgpp_lt_bench_wood_02", parent, new Vector3(4.5f, 0f, 14f), 90f, Vector3.one, Collide.None, true);
            p.Place("Props/Misc/rpgpp_lt_stones_01", parent, new Vector3(5.2f, 0f, -4f), 30f, Vector3.one, Collide.None, true);

            // スポーン地点のそばの荷車と薪。
            p.Place("Props/Misc/rpgpp_lt_wagon_01", parent, new Vector3(7.5f, 0f, -22f), 25f, Vector3.one, Collide.None, true);
            p.Place("Props/Wood/rpgpp_lt_log_wood_02a", parent, new Vector3(11f, 0f, -26.5f), 5f, Vector3.one, Collide.None, true);
            p.Place("Props/Wood/rpgpp_lt_log_wood_02b", parent, new Vector3(12f, 0f, -25.2f), 80f, Vector3.one, Collide.None, true);
            p.Place("Props/Wood/rpgpp_lt_log_wood_01", parent, new Vector3(-9f, 0f, -26f), 60f, Vector3.one, Collide.None, true);
            p.Place(c + "sack_01", parent, new Vector3(5.6f, 0f, -20.4f), 110f, Vector3.one, Collide.None, true);
            p.Block(Rect.MinMaxRect(4f, -29f, 14f, -18f));
            p.Block(Rect.MinMaxRect(-12f, -29f, -6f, -23f));

            // 東の民家（手前）の庭。
            GameObject table = p.Place("Props/Tables/rpgpp_lt_table_01", parent, new Vector3(16.5f, 0f, -21f), 90f, Vector3.one, Collide.None, true);
            p.Place("Props/Chairs/rpgpp_lt_chair_01a", parent, new Vector3(15.3f, 0f, -21.6f), 90f, Vector3.one, Collide.None, true);
            p.Place("Props/Chairs/rpgpp_lt_chair_01b", parent, new Vector3(17.7f, 0f, -20.4f), -90f, Vector3.one, Collide.None, true);
            PlaceOn(p, table, c + "jug_01", parent, new Vector3(16.4f, 0.98f, -21.3f), 0f);
            PlaceOn(p, table, c + "vase_03", parent, new Vector3(16.7f, 0.98f, -20.5f), 0f);
            p.Place("Props/Hangers/rpgpp_lt_hanger_clothes_01", parent, new Vector3(25f, 0f, -24.5f), 0f, Vector3.one, Collide.None, true);
            p.Place("Props/Misc/rpgpp_lt_bird_house_01", parent, new Vector3(18.5f, 0f, -8f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "bathtub_wood_01", parent, new Vector3(31.5f, 0f, -24f), 0f, Vector3.one, Collide.None, true);
            p.Block(Rect.MinMaxRect(14f, -27f, 34f, -20f));

            // 東の民家（奥）の前の露店と物置。
            p.Place("Exterior/Wood_path/rpgpp_lt_awning_standing_01b", parent, new Vector3(19.5f, 0f, 15.5f), -90f, Vector3.one, Collide.None, true);
            p.Place(c + "barrel_01", parent, new Vector3(21.5f, 0f, 17.5f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "sack_open_01", parent, new Vector3(18f, 0f, 17.2f), 0f, Vector3.one, Collide.None, true);
            p.Place(c + "sack_02", parent, new Vector3(18.9f, 0f, 17.8f), 30f, Vector3.one, Collide.None, true);
            p.Place(c + "basket_02", parent, new Vector3(20.8f, 0f, 13.2f), 0f, Vector3.one, Collide.None, true);
            p.Place("Exterior/Wood_path/rpgpp_lt_shed_wood_01", parent, new Vector3(43f, 0f, -4f), -90f, Vector3.one, Collide.None, true);
            p.Place("Exterior/Wood_path/rpgpp_lt_shed_wood_02", parent, new Vector3(43f, 0f, 20f), -90f, Vector3.one, Collide.None, true);
            p.Place("Props/Items/rpgpp_lt_ladder_01", parent, new Vector3(40.6f, 0f, -6.5f), -90f, Vector3.one, Collide.None, true);
            p.Place("Props/Items/rpgpp_lt_rake_01", parent, new Vector3(41f, 0f, -1f), 20f, Vector3.one, Collide.None, true);
            p.Place("Props/Items/rpgpp_lt_broom_01", parent, new Vector3(40.8f, 0f, 17f), -40f, Vector3.one, Collide.None, true);
            p.Place("Props/Hangers/rpgpp_lt_hanger_wood_01", parent, new Vector3(37f, 0f, 24f), 0f, Vector3.one, Collide.None, true);
            p.Place("Props/Hangers/rpgpp_lt_hanger_wood_02", parent, new Vector3(-30f, 0f, 27f), 15f, Vector3.one, Collide.None, true);
            p.Block(Rect.MinMaxRect(16f, 11f, 24f, 20f));
            p.Block(Rect.MinMaxRect(39f, -8f, 47f, 0f));
            p.Block(Rect.MinMaxRect(39f, 16f, 47f, 24f));

            // 西の端の畑（柵で囲む）。
            BuildGarden(p, parent, new Vector2(-44f, -24f), 4, 3);
        }

        /// <summary>台（下の物）の上に載せる。高さ position.y は台の根元からの高さで、地面ではなく台に合わせる。</summary>
        private static void PlaceOn(Placer p, GameObject stand, string path, Transform parent, Vector3 position, float yaw)
        {
            bool snap = p.Snap;
            p.Snap = false;
            position.y += stand.transform.localPosition.y;
            p.Place(path, parent, position, yaw, Vector3.one, Collide.None, true);
            p.Snap = snap;
        }

        /// <summary>左下の角 corner から、柵 1 枚（3 m）を単位に w×h の囲いを作り、中に草花を植える。</summary>
        private static void BuildGarden(Placer p, Transform parent, Vector2 corner, int w, int h)
        {
            const float len = 3f;
            for (int i = 0; i < w; i++)
            {
                if (i == w / 2) continue; // 南側に出入口
                Fence(p, parent, new Vector3(corner.x + len * (i + 0.5f), 0f, corner.y), 0f);
            }
            for (int i = 0; i < w; i++) Fence(p, parent, new Vector3(corner.x + len * (i + 0.5f), 0f, corner.y + len * h), 180f);
            for (int i = 0; i < h; i++) Fence(p, parent, new Vector3(corner.x, 0f, corner.y + len * (i + 0.5f)), 90f);
            for (int i = 0; i < h; i++) Fence(p, parent, new Vector3(corner.x + len * w, 0f, corner.y + len * (i + 0.5f)), -90f);

            for (int i = 0; i < 14; i++)
            {
                var at = new Vector3(corner.x + p.Range(0.8f, len * w - 0.8f), 0f, corner.y + p.Range(0.8f, len * h - 0.8f));
                string plant = p.Pick("Nature/Vegetation/Plants/rpgpp_lt_plant_01", "Nature/Vegetation/Plants/rpgpp_lt_plant_02", "Nature/Vegetation/Flowers/rpgpp_lt_flower_03");
                p.Place(plant, parent, at, p.Range(0f, 360f), Vector3.one * p.Range(0.9f, 1.2f), Collide.None, true);
            }
            p.Block(Rect.MinMaxRect(corner.x - 1f, corner.y - 1f, corner.x + len * w + 1f, corner.y + len * h + 1f));
        }

        private static void Fence(Placer p, Transform parent, Vector3 at, float yaw)
        {
            string fence = p.Chance(0.5f) ? "Exterior/Wood_path/rpgpp_lt_fence_wood_01a" : "Exterior/Wood_path/rpgpp_lt_fence_wood_01b";
            p.Place(fence, parent, at, yaw + p.Range(-3f, 3f), Vector3.one, Collide.None, true);
        }

        // --- 歩ける範囲の木・岩・草花 ---

        private static void BuildInnerNature(Placer p, Transform parent)
        {
            // 木は端の帯（壁から 9 m 以内）と、城の両脇から裏にかけて。
            int trees = 0;
            for (int tries = 0; tries < 2000 && trees < 46; tries++)
            {
                var at = new Vector2(p.Range(MinX + 2f, MaxX - 2f), p.Range(MinZ + 2f, MaxZ - 2f));
                bool edge = at.x < MinX + 9f || at.x > MaxX - 9f || at.y < MinZ + 6f || at.y > MaxZ - 9f;
                bool besideCastle = at.y > 22f && Mathf.Abs(at.x) > 34f;
                if (!edge && !besideCastle) continue;
                if (at.y < -18f && Mathf.Abs(at.x) < 14f) continue; // スポーン地点のまわりは開ける
                if (!p.IsFree(at, 2.2f)) continue;

                p.Place(Tree(p), parent, new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(0.9f, 1.35f), Collide.None, true);
                p.Reserve(at, 2.6f);
                trees++;
            }

            // 岩は端に少し。
            int rocks = 0;
            for (int tries = 0; tries < 1000 && rocks < 12; tries++)
            {
                var at = new Vector2(p.Range(MinX + 1.5f, MaxX - 1.5f), p.Range(MinZ + 1.5f, MaxZ - 1.5f));
                bool edge = at.x < MinX + 12f || at.x > MaxX - 12f || at.y < MinZ + 8f || at.y > MaxZ - 10f;
                if (!edge || !p.IsFree(at, 2f)) continue;
                string rock = p.Pick("Nature/Rocks/rpgpp_lt_rock_01", "Nature/Rocks/rpgpp_lt_rock_02", "Nature/Rocks/rpgpp_lt_rock_03");
                p.Place(rock, parent, new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(0.7f, 1.2f), Collide.None, true);
                p.Reserve(at, 2.2f);
                rocks++;
            }

            // 草・花・茂み・小石（当たり無し）。道の上には置かない。
            for (int i = 0; i < 260; i++)
            {
                var at = new Vector2(p.Range(MinX + 0.5f, MaxX - 0.5f), p.Range(MinZ + 0.5f, MaxZ - 0.5f));
                if (!p.IsFree(at, 0.6f)) continue;
                string plant = p.Pick(
                    "Nature/Vegetation/Grass/rpgpp_lt_grass_small_01a", "Nature/Vegetation/Grass/rpgpp_lt_grass_small_01b",
                    "Nature/Vegetation/Grass/rpgpp_lt_grass_small_01a", "Nature/Vegetation/Grass/rpgpp_lt_grass_small_01b",
                    "Nature/Vegetation/Flowers/rpgpp_lt_flower_01", "Nature/Vegetation/Flowers/rpgpp_lt_flower_02",
                    "Nature/Vegetation/Plants/rpgpp_lt_plant_01", "Nature/Vegetation/Plants/rpgpp_lt_plant_02",
                    "Nature/Rocks/rpgpp_lt_rock_small_01", "Nature/Rocks/rpgpp_lt_rocks_tiny_01");
                p.Place(plant, parent, new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(0.8f, 1.3f), Collide.None, true);
            }

            // 茂みは端に寄せる（当たり無し。視線を少しさえぎるだけ）。
            for (int i = 0; i < 40; i++)
            {
                var at = new Vector2(p.Range(MinX + 1f, MaxX - 1f), p.Range(MinZ + 1f, MaxZ - 1f));
                bool edge = at.x < MinX + 6f || at.x > MaxX - 6f || at.y < MinZ + 4f || at.y > MaxZ - 6f;
                if (!edge || !p.IsFree(at, 1.5f)) continue;
                p.Place(p.Chance(0.5f) ? "Nature/Vegetation/Bushes/rpgpp_lt_bush_01" : "Nature/Vegetation/Bushes/rpgpp_lt_bush_02", parent,
                    new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(0.8f, 1.2f), Collide.None, true);
            }
        }

        private static string Tree(Placer p) => p.Pick(
            "Nature/Vegetation/Trees/rpgpp_lt_tree_pine_01", "Nature/Vegetation/Trees/rpgpp_lt_tree_pine_01",
            "Nature/Vegetation/Trees/rpgpp_lt_tree_01", "Nature/Vegetation/Trees/rpgpp_lt_tree_02");

        // --- 歩ける範囲の外 ---

        /// <summary>見えない壁のすぐ外を森にする。壁際ほど密で、外へ行くほどまばら。</summary>
        private static void BuildForest(Placer p, Transform parent)
        {
            const float cell = 4.6f;
            for (float x = MinX - 70f; x <= MaxX + 70f; x += cell)
            {
                for (float z = MinZ - 70f; z <= MaxZ + 70f; z += cell)
                {
                    var at = new Vector2(x + p.Range(-1.8f, 1.8f), z + p.Range(-1.8f, 1.8f));
                    float d = DistanceOutside(at);
                    if (d < 1.2f) continue;

                    float density = d < 22f ? 0.82f : Mathf.Lerp(0.55f, 0.08f, Mathf.InverseLerp(22f, 70f, d));
                    if (!p.Chance(density)) continue;

                    float scale = p.Range(1.0f, 1.6f) * (d < 10f ? 1f : 1.15f);
                    p.Place(Tree(p), parent, new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * scale, Collide.None, true);

                    if (d < 14f && p.Chance(0.35f))
                    {
                        var bush = new Vector3(at.x + p.Range(-2f, 2f), 0f, at.y + p.Range(-2f, 2f));
                        p.Place(p.Chance(0.5f) ? "Nature/Vegetation/Bushes/rpgpp_lt_bush_01" : "Nature/Vegetation/Bushes/rpgpp_lt_bush_02", parent,
                            bush, p.Range(0f, 360f), Vector3.one * p.Range(0.9f, 1.4f), Collide.None, true);
                    }
                    else if (p.Chance(0.06f))
                    {
                        string rock = p.Pick("Nature/Rocks/rpgpp_lt_rock_01", "Nature/Rocks/rpgpp_lt_rock_02", "Nature/Rocks/rpgpp_lt_rock_03");
                        p.Place(rock, parent, new Vector3(at.x + 1.5f, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(1f, 1.8f), Collide.None, true);
                    }
                }
            }
        }

        /// <summary>丘の輪・山・遠くの林。</summary>
        private static void BuildFarLand(Placer p, Transform parent)
        {
            var center = new Vector2(0f, (MinZ + MaxZ) * 0.5f);

            const int hills = 18;
            for (int i = 0; i < hills; i++)
            {
                float angle = (i + p.Range(-0.3f, 0.3f)) / hills * Mathf.PI * 2f;
                float radius = p.Range(160f, 220f);
                var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float s = p.Range(1.1f, 1.7f);
                p.Place(p.Chance(0.5f) ? "Nature/Terrain/rpgpp_lt_hill_small_01" : "Nature/Terrain/rpgpp_lt_hill_small_02", parent,
                    new Vector3(at.x, -1.5f, at.y), p.Range(0f, 360f), new Vector3(s, p.Range(0.9f, 1.5f), s), Collide.None, true);
            }

            // 山は城の向こう（北）を厚めに。
            float[] mountainAngles = { 60f, 80f, 95f, 112f, 130f, 165f, 200f, 250f, 290f, 330f, 15f };
            foreach (float deg in mountainAngles)
            {
                float angle = (deg + p.Range(-6f, 6f)) * Mathf.Deg2Rad;
                bool north = deg > 50f && deg < 140f;
                float radius = north ? p.Range(270f, 320f) : p.Range(300f, 360f);
                var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float s = north ? p.Range(1.5f, 2.0f) : p.Range(1.1f, 1.6f);
                p.Place("Nature/Terrain/rpgpp_lt_mountain_01", parent, new Vector3(at.x, -2f, at.y), p.Range(0f, 360f),
                    new Vector3(s, s * p.Range(0.8f, 1.2f), s), Collide.None, true);
            }

            // 丘の手前の林（森の帯と丘のあいだを埋める）。
            for (int i = 0; i < 160; i++)
            {
                float angle = p.Range(0f, Mathf.PI * 2f);
                float radius = p.Range(125f, 170f);
                var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                p.Place(Tree(p), parent, new Vector3(at.x, 0f, at.y), p.Range(0f, 360f), Vector3.one * p.Range(1.3f, 2f), Collide.None, true);
            }
        }

        private static void BuildSky(Placer p, Transform parent)
        {
            p.Place("Nature/Sky/rpgpp_lt_sky_01", parent, new Vector3(0f, -90f, 19f), 0f, Vector3.one * 12f, Collide.None, false);

            for (int i = 0; i < 16; i++)
            {
                float angle = (i + p.Range(-0.4f, 0.4f)) / 16f * Mathf.PI * 2f;
                float radius = p.Range(260f, 400f);
                var at = new Vector3(Mathf.Cos(angle) * radius, p.Range(90f, 170f), 19f + Mathf.Sin(angle) * radius);
                float s = p.Range(1.3f, 2.0f);
                p.Place(p.Chance(0.5f) ? "Nature/Sky/rpgpp_lt_cloud_01" : "Nature/Sky/rpgpp_lt_cloud_02", parent, at, p.Range(0f, 360f),
                    new Vector3(s, s * p.Range(0.6f, 1.4f), s), Collide.None, false);
            }
        }

        /// <summary>光は素材のデモに合わせて斜め後ろからの暖色、霧で遠くを空の色に溶かす。カメラは遠くの山まで写るよう遠景を延ばす。</summary>
        private static void ApplyLighting(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional) continue;
                    light.transform.rotation = Quaternion.Euler(48f, 330f, 0f);
                    light.color = new Color(0.98f, 0.95f, 0.83f);
                    light.intensity = 1.2f;
                    light.shadows = LightShadows.Soft;
                }

                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera")) camera.farClipPlane = 1200f;
                }
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.24f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.88f, 0.94f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 650f;
        }

        /// <summary>スポーン地点を足元の地面の少し上に合わせる（地面が平らでなくなったので）。</summary>
        private static void PlaceSpawnOnGround(Transform surface, Placer p)
        {
            Transform spawn = surface.Find("PlayerSpawn");
            if (spawn == null) return;
            Vector3 at = spawn.position;
            spawn.position = new Vector3(at.x, p.GroundY(at.x, at.z) + 0.05f, at.z);
        }

        // --- 補助 ---

        /// <summary>歩ける範囲の外側へどれだけ離れているか（内側なら 0）。</summary>
        private static float DistanceOutside(Vector2 at)
        {
            float dx = Mathf.Max(MinX - at.x, 0f, at.x - MaxX);
            float dz = Mathf.Max(MinZ - at.y, 0f, at.y - MaxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private enum Collide { None, Ground }

        /// <summary>乱数と、置いた所（物を重ねないための円と矩形）を持って置いていく。</summary>
        private sealed class Placer
        {
            private readonly Random random;
            private readonly List<Rect> blocked = new List<Rect>();
            private readonly List<(Vector2 at, float radius)> reserved = new List<(Vector2, float)>();
            private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

            private readonly HashSet<Collider> ground = new HashSet<Collider>();
            private readonly List<Collider> sampling = new List<Collider>();
            private readonly RaycastHit[] hits = new RaycastHit[64];

            public Placer(Random random) => this.random = random;

            /// <summary>真なら、置く物の y を「その場の地面からの高さ」とみなして地面に合わせる。</summary>
            public bool Snap { get; set; }

            /// <summary>instance の当たりを地面として覚える。temporary なら置き終えたら外す（見せるだけの外の地面）。</summary>
            public void AddGround(GameObject instance, bool temporary)
            {
                foreach (Collider c in instance.GetComponentsInChildren<Collider>())
                {
                    ground.Add(c);
                    if (temporary) sampling.Add(c);
                }
                Physics.SyncTransforms();
            }

            public void RemoveSamplingColliders()
            {
                foreach (Collider c in sampling)
                {
                    ground.Remove(c);
                    if (c != null) Object.DestroyImmediate(c);
                }
                sampling.Clear();
            }

            /// <summary>(x, z) の見た目の地面の高さ。地面が無ければ 0。</summary>
            public float GroundY(float x, float z)
            {
                int count = Physics.RaycastNonAlloc(new Vector3(x, 100f, z), Vector3.down, hits, 200f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.NegativeInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (ground.Contains(hits[i].collider) && hits[i].point.y > best) best = hits[i].point.y;
                }
                return float.IsNegativeInfinity(best) ? 0f : best;
            }

            /// <summary>(x, z) を中心に半径 radius の円の中で一番低い地面（真ん中と円周 8 点）。</summary>
            public float LowestGround(float x, float z, float radius)
            {
                float low = GroundY(x, z);
                if (radius <= 0.05f) return low;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    low = Mathf.Min(low, GroundY(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
                }
                return low;
            }

            public float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            public bool Chance(float p) => random.NextDouble() < p;
            public float Quarter() => 90f * random.Next(4);
            public string Pick(params string[] options) => options[random.Next(options.Length)];

            public void Block(Rect rect) => blocked.Add(rect);
            public void Reserve(Vector2 at, float radius) => reserved.Add((at, radius));

            public bool IsFree(Vector2 at, float radius)
            {
                foreach (Rect r in blocked)
                {
                    if (at.x > r.xMin - radius && at.x < r.xMax + radius && at.y > r.yMin - radius && at.y < r.yMax + radius) return false;
                }
                foreach (var (c, rr) in reserved)
                {
                    if ((c - at).sqrMagnitude < (rr + radius) * (rr + radius)) return false;
                }
                return true;
            }

            /// <summary>
            /// prefab を置く。<see cref="Snap"/> のときは position.y を地面からの高さとして扱い、足元の円（footprint × 見た目の短い辺の半分）の一番低い地面に合わせる。
            /// </summary>
            public GameObject Place(string path, Transform parent, Vector3 position, float yaw, Vector3 scale, Collide collide, bool isStatic, float footprint = 0.7f)
            {
                if (!prefabs.TryGetValue(path, out GameObject prefab))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + path + ".prefab");
                    if (prefab == null) throw new InvalidOperationException($"素材が無い: {Pack}{path}.prefab");
                    prefabs[path] = prefab;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.localScale = scale;
                if (Snap)
                {
                    Bounds b = LocalBounds(instance);
                    float radius = Mathf.Clamp(Mathf.Min(b.size.x * scale.x, b.size.z * scale.z) * 0.5f * footprint, 0f, 5f);
                    Vector3 world = parent.TransformPoint(new Vector3(position.x, 0f, position.z));
                    position.y += LowestGround(world.x, world.z, radius) - Embed - parent.position.y;
                }
                instance.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                AddCollider(instance, collide);
                if (isStatic) SetStatic(instance);
                return instance;
            }
        }

        private static void AddCollider(GameObject instance, Collide collide)
        {
            if (collide == Collide.Ground) AddMeshColliders(instance);
        }

        /// <summary>
        /// 見た目のメッシュから当たりを付ける。convex でなければメッシュそのまま（露店の屋根の下に入れる）。
        /// convex なら外側を包む凸の形（屋根は板を重ねた段々なので、そのままだと段に引っかかって登れない。包めば平らな坂になる）。
        /// </summary>
        private static void AddMeshColliders(GameObject instance, bool convex = false)
        {
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var mesh = filter.gameObject.AddComponent<MeshCollider>();
                mesh.sharedMesh = filter.sharedMesh;
                mesh.convex = convex;
            }
        }

        [MenuItem("Tools/TPS Dungeon/地上の草花を壊せるようにする")]
        public static void MakePlantsBreakableFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject surface = FindRoot(scene, "[Surface]");
            Transform village = surface != null ? surface.transform.Find("Village") : null;
            if (village == null)
            {
                Debug.LogError("開いているシーンに [Surface]/Village が無い。地上のシーンを開いてから実行すること。");
                return;
            }

            int count = MakePlantsBreakable(village);
            BakeNavMesh(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"地上の草花 {count} 本を攻撃で壊せるようにした（壊すと {BreakablePlantSetup.SpawnTablePath} の表で敵が出る）。シーンは保存していない。");
        }

        /// <summary>村の草・花・草木（茂みは除く）を、攻撃で壊せて敵が出てくることのある草花にする。配置は変えない。壊せるようにした数を返す。</summary>
        internal static int MakePlantsBreakable(Transform village)
        {
            PlantSpawnTable table = BreakablePlantSetup.EnsureAssets();
            int count = 0;
            foreach (Transform group in village)
            {
                if (group.name == "Ground" || group.name == "Roads") continue;
                foreach (Transform item in group)
                {
                    if (!BreakablePlantSetup.IsPlant(item.gameObject)) continue;
                    BreakablePlantSetup.MakeBreakable(item.gameObject, table);
                    count++;
                }
            }
            return count;
        }

        // 草花・茂みは通り抜ける。
        private static readonly string[] Passable = { "grass", "flower", "plant", "bush" };

        [MenuItem("Tools/TPS Dungeon/地上の当たり判定を付け直す")]
        public static void RefitFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject surface = FindRoot(scene, "[Surface]");
            Transform village = surface != null ? surface.transform.Find("Village") : null;
            if (village == null)
            {
                Debug.LogError("開いているシーンに [Surface]/Village が無い。地上のシーンを開いてから実行すること。");
                return;
            }

            int count = RefitColliders(village);
            BakeNavMesh(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"地上の当たり判定を付け直した（{count} 個にメッシュの当たり）。NavMesh も焼き直した。シーンは保存していない。");
        }

        /// <summary>
        /// 村の物の当たりを見た目のメッシュどおりに付け直す（手で置き直した物・足した物にも効く）。地面と道（Ground・Roads）と扉のトリガーは触らない。
        /// 閉じた建物（中に入れない）は凸に包んで、屋根を平らな坂にする（<see cref="AddMeshColliders"/>）。
        /// 草花・茂みと、小さい物（高さ 0.45 m 未満か、幅 0.5 m 未満）には付けない。メッシュの当たりを付けた数を返す。
        /// </summary>
        internal static int RefitColliders(Transform village)
        {
            int count = 0;
            foreach (Transform group in village)
            {
                if (group.name == "Ground" || group.name == "Roads") continue;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(group.gameObject))
                {
                    if (Refit(group.gameObject)) count++;
                    continue;
                }
                foreach (Transform item in group)
                {
                    if (Refit(item.gameObject)) count++;
                }
            }
            return count;
        }

        private static bool Refit(GameObject item)
        {
            // 壊せる草花は、すり抜けられるレイヤーの箱を自分で持つ（BreakablePlantSetup）。
            if (item.GetComponent<BreakablePlant>() != null) return false;

            foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger) Object.DestroyImmediate(c);
            }

            if (!IsSolid(item)) return false;
            AddMeshColliders(item, convex: SourceName(item).Contains("building_"));
            return true;
        }

        private static string SourceName(GameObject item)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item);
            return (source != null ? source : item).name;
        }

        private static bool IsSolid(GameObject item)
        {
            string name = SourceName(item);
            foreach (string soft in Passable)
            {
                if (name.Contains(soft)) return false;
            }

            Bounds b = LocalBounds(item);
            Vector3 s = item.transform.lossyScale;
            float height = b.size.y * s.y;
            float width = Mathf.Max(b.size.x * s.x, b.size.z * s.z);
            return height >= 0.45f && width >= 0.5f;
        }

        /// <summary>instance の見た目の大きさ（instance のローカル空間）。</summary>
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
                    // InverseTransformPoint はスケールも戻すので、ここで掛け直さない（コライダはローカル空間で張る）。
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return bounds;
        }

        private static void SetStatic(GameObject instance)
        {
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        private static void BakeNavMesh(GameObject surfaceRoot)
        {
            var surface = surfaceRoot.GetComponent<NavMeshSurface>();
            if (surface == null) return;

            // 味方と、すり抜けられる壊せる草花（Breakable）は焼かない。
            surface.layerMask = BreakableLayer.Exclude(AllyLayer.Exclude(~0));
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        private static int CountChildren(Transform root)
        {
            int count = 0;
            foreach (Transform group in root)
            {
                count += group.childCount;
            }
            return count;
        }
    }
}
