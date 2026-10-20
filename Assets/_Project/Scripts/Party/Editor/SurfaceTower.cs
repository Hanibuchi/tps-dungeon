using System;
using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
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
    /// 地上のダンジョン（[Surface]/Castle）を、上へ行くほど太くそびえる塔として建てる。何度実行しても同じ形になる（乱数のシードは固定）。
    /// - 土台（幅 62 m・高さ 14 m）と大門は今までどおり ProBuilder の箱で、当たりもここだけに付ける。
    /// - 土台の上に、ダンジョンの道順（DungeonRoute）の層を下から積む。1 つの層は、その層の階（ランダムな階 × randomFloorCount＋ボス階）を 1 階ずつ重ねた段で、
    ///   各階は中と同じ生成器（FloorLayoutGenerator・BossFloorLayout）で部屋の並びを作り、部屋の四角をその階の足元へ縮めて押し出す。中の部屋の並びがそのまま外から見える。
    ///   段の高さは各層の FloorConfig.elevation から決め、素材はその層のテーマの壁。
    /// - 足元は上の階ほど広げる（横と奥へ大きく、広場の側へは少しだけ）。層の境目には段々の持ち送りを付けて張り出しを支える。
    /// - 部屋の外向きの面に窓（中の階の窓と同じ形の暗い窓）、てっぺんに小塔と尖り屋根。
    /// - 部屋・窓などは数が多いので、層ごとに 1 つのメッシュにまとめて <see cref="MeshFolder"/> に書き出す（UV は中の壁と同じく 1 m = 1）。上の段には当たりを付けない。
    /// </summary>
    public static class SurfaceTower
    {
        private const string MaterialsFolder = "Assets/_Project/Materials/Placeholder";
        private const string MeshFolder = "Assets/_Project/Scenes/Surface";
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";
        private const string RoofMaterialPath = "Assets/ThirdParty/Material/YughuesFreePavementsMaterials/Materials/M_YFPM_OrientalFan.mat";
        private const string StoneMaterialPath = "Assets/ThirdParty/Material/YughuesFreePavementsMaterials/Materials/M_YFPM_JaggedSquare.mat";
        private const string BannerPath = "Assets/ThirdParty/3D Model/RPGPP_LT/Prefabs/Props/Banners/rpgpp_lt_banner_01";
        private const string FirstFloorScene = "L1_Random";
        private const int Seed = 20261018;

        // 土台（広場の北の端に正面が来る）。村の配置（SurfaceScenery.CastleFoot）と合わせてある。
        private const float Front = 30f;
        private const float BaseWidth = 62f;
        private const float BaseDepth = 30f;
        private const float BaseHeight = 14f;

        // 一番上の階の足元。土台からここへ向かって広げる。
        private const float TopWidth = 136f;
        private const float TopDepth = 82f;
        private const float TopOverhang = 14f; // 広場の側へのせり出し
        // 広がり方。1 より大きいと上の方で急に広がる（ラッパの形）。
        private const float Flare = 1.35f;
        // 窓。中の階の窓付きの壁（幅 5 m・高さ 5 m に、幅 25 %・高さ 60〜80 % の穴）と同じ寸法で、壁 1 枚分（5 m）ごとに 1 つ。
        private const float WindowCell = 5f;
        private const float WindowWidth = 1.25f;
        private const float WindowSill = 3f;
        private const float WindowTop = 4f;
        // 窓の枠の太さと、面からの出っ張り（窓台はこの倍出す）。中の階の窓の枠と同じ。
        private const float FrameWidth = 0.14f;
        private const float FrameDepth = 0.1f;

        // 大門の開口（扉の板）と、門構えの半分の幅（小塔の外の端）。小塔は広場の板敷きの道の外に置く。
        private const float GateWidth = 5f;
        private const float GateHeight = 6f;
        private const float GatehouseHalfWidth = 12f;

        // 一番上の層の上にどれだけ積むか（elevation の差が取れないので）。
        private const float TopLayerHeight = 30f;

        [MenuItem("Tools/TPS Dungeon/地上のダンジョンの塔を建て直す")]
        public static void RebuildFromMenu() => Debug.Log(Rebuild());

        /// <summary>開いている地上のシーンの [Surface]/Castle だけを建て直し、NavMesh を焼き直して保存する。村や手で直した所には触らない。</summary>
        public static string Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SurfaceSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "シーンを開き直せなかった。";
                scene = EditorSceneManager.OpenScene(SurfaceSceneBuilder.ScenePath, OpenSceneMode.Single);
            }

            GameObject surface = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "[Surface]") surface = root;
            }
            if (surface == null) return $"{scene.path} に [Surface] が無い。先に「地上シーンを作る」を実行すること。";

            Transform old = surface.transform.Find("Castle");
            int index = old != null ? old.GetSiblingIndex() : -1;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            string result = Build(surface.transform, out Transform castle);
            if (index >= 0) castle.SetSiblingIndex(index);
            castle.localPosition = new Vector3(0f, SurfaceScenery.CastleSink, 0f);

            SurfaceScenery.BakeNavMesh(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return result;
        }

        /// <summary>world の下に Castle を作る（保存も NavMesh もしない）。</summary>
        internal static string Build(Transform world, out Transform castle)
        {
            castle = new GameObject("Castle").transform;
            castle.SetParent(world, false);

            var materials = new Materials();
            BuildBase(castle, materials);

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null || route.Layers.Count == 0)
            {
                Debug.LogWarning($"{RoutePath} が無いので、塔は土台だけにした。");
                return "地上のダンジョンは土台だけを建てた（道順が無い）。";
            }

            var random = new Random(Seed);
            List<Storey> storeys = PlanStoreys(route, materials);
            int total = storeys.Count;
            int done = 0;
            int boxes = 0;
            for (int layer = 0; layer < route.Layers.Count; layer++)
            {
                var builder = new MeshBuilder();
                Rect previous = done == 0 ? BaseFoot() : Foot(done - 1, total);
                Corbel(builder, materials.Foundation, previous, Foot(done, total), storeys[done].Bottom);
                foreach (Storey storey in storeys)
                {
                    if (storey.Layer != layer) continue;
                    BuildStorey(builder, materials, storey, Foot(done, total), random);
                    done++;
                }
                if (layer == route.Layers.Count - 1)
                {
                    Storey last = storeys[total - 1];
                    BuildCrown(builder, materials, Foot(total - 1, total), last.Bottom + last.Height, random);
                }
                boxes += builder.Boxes;
                builder.Save(castle, $"Tower_L{layer + 1}", $"{MeshFolder}/Tower_L{layer + 1}.asset");
            }

            Storey top = storeys[total - 1];
            return $"地上のダンジョンを建てた（{route.Layers.Count} 層・{total} 階分、高さ {top.Bottom + top.Height:0} m、箱 {boxes} 個）。";
        }

        // --- 土台と大門 ---

        private static void BuildBase(Transform castle, Materials m)
        {
            SurfaceSceneBuilder.Box(castle, "Base", m.Foundation, new Vector3(0f, BaseHeight * 0.5f, Front + BaseDepth * 0.5f), new Vector3(BaseWidth, BaseHeight, BaseDepth));

            // 1F の大門。扉の板そのものが門で、照準を合わせて入る。まわりの門構えは BuildGatehouse。
            GameObject gate = SurfaceSceneBuilder.Box(castle, "Gate_1F", m.DoorLeaf, new Vector3(0f, GateHeight * 0.5f, Front - 0.2f), new Vector3(GateWidth, GateHeight, 0.4f));
            SurfaceSceneBuilder.AddGate(gate, FirstFloorScene, true, "城へ入る", "まだ開いていない", "城の大門", "第 1 層「玄関」へ入る。");

            // 土台の飾り（当たり無し）。正面の窓 2 列と、上の縁の帯。
            var detail = new MeshBuilder();
            for (int row = 0; row < 2; row++)
            {
                float y = row * WindowCell + (WindowSill + WindowTop) * 0.5f - 0.5f; // 上の段は持ち送りに隠れないよう少し下げる
                for (float x = -BaseWidth * 0.5f + WindowCell * 0.5f; x <= BaseWidth * 0.5f - WindowCell * 0.5f; x += WindowCell)
                {
                    if (Mathf.Abs(x) < GatehouseHalfWidth + 1f) continue; // 門構えのまわり
                    Window(detail, m, new Vector3(x, y, Front), Vector3.back, Vector3.right);
                }
            }
            detail.Box(m.Trim, new Vector3(0f, BaseHeight - 0.4f, Front + BaseDepth * 0.5f), new Vector3(BaseWidth + 1f, 0.8f, BaseDepth + 1f));
            BuildGatehouse(castle, detail, m);
            detail.Save(castle, "Base_Detail", $"{MeshFolder}/Tower_Base.asset");
        }

        /// <summary>
        /// 大門のまわりの門構え。土台の正面から広場へ張り出し、左右に胸壁の小塔、真ん中に石のアーチ（扉の上は半円）と胸壁。上の階の張り出しが入口のすぐ上まで来るので、屋根は載せない。
        /// 人の届く高さの柱と小塔は ProBuilder の箱で当たりを付け、アーチから上と飾りは detail のメッシュ（当たり無し）。
        /// 扉はアーチの奥（Gate_1F）で、上の半円の板と鉄の帯で両開きのアーチ形の扉に見せる。
        /// 張り出しは板敷きの道（x が ±5 m ほどまで）に掛からないよう、小塔を道の外に置く。
        /// </summary>
        private static void BuildGatehouse(Transform castle, MeshBuilder b, Materials m)
        {
            const float porchFront = Front - 5f;       // アーチの前の面
            const float porchTop = 12f;
            const float archRadius = GateWidth * 0.5f;  // 扉の幅いっぱいの半円
            const float archRing = 0.8f;                // アーチの石の厚み
            float half = GatehouseHalfWidth;
            float pier = half - 5.5f;                   // 柱の外の端（その外が小塔）

            // アーチの両脇の柱（当たりあり）。
            float pierWidth = pier - archRadius;
            foreach (float side in new[] { -1f, 1f })
            {
                SurfaceSceneBuilder.Box(castle, side < 0f ? "Gate_Pier_L" : "Gate_Pier_R", m.Foundation,
                    new Vector3(side * (archRadius + pierWidth * 0.5f), porchTop * 0.5f, (porchFront + Front) * 0.5f), new Vector3(pierWidth, porchTop, Front - porchFront));
            }

            // アーチの石（半円に 9 個、真ん中は要石で大きく）と、その上を柱の高さまで埋める壁。
            const int stones = 9;
            Vector2 center = new Vector2(0f, GateHeight);
            for (int i = 0; i < stones; i++)
            {
                float a0 = Mathf.PI * i / stones;
                float a1 = Mathf.PI * (i + 1) / stones;
                bool key = i == stones / 2;
                float outer = archRadius + (key ? archRing + 0.5f : archRing);
                b.Extrude(m.Trim, ArcSegment(center, archRadius, outer, a0, a1), porchFront - (key ? 0.35f : 0.2f), Front);

                // 石の外側から上の壁（柱と柱のあいだ）。
                Vector2 p0 = center + Polar(archRadius + archRing, a0);
                Vector2 p1 = center + Polar(archRadius + archRing, a1);
                b.Extrude(m.Foundation, new[] { p0, p1, new Vector2(p1.x, porchTop), new Vector2(p0.x, porchTop) }, porchFront, Front);
            }

            // 門構えの上の縁と胸壁。
            var porch = Rect.MinMaxRect(-pier, porchFront, pier, Front);
            b.Box(m.Trim, Center(porch, porchTop + 0.3f), new Vector3(porch.width + 0.6f, 0.6f, porch.height + 0.6f));
            Battlements(b, m, porch, porchTop + 0.6f);

            // 扉の上の半円の板と、扉の鉄の帯・合わせ目（両開きに見せる）。
            var tympanum = new List<Vector2>();
            for (int i = 0; i <= 12; i++) tympanum.Add(center + Polar(archRadius, Mathf.PI * i / 12));
            b.Extrude(m.DoorLeaf, tympanum, Front - 0.35f, Front - 0.05f);
            foreach (float y in new[] { 1.1f, 3.2f, 5.3f })
                b.Box(m.Dark, new Vector3(0f, y, Front - 0.44f), new Vector3(GateWidth, 0.22f, 0.08f));
            b.Box(m.Dark, new Vector3(0f, GateHeight * 0.5f + 0.6f, Front - 0.44f), new Vector3(0.08f, GateHeight + 1.2f, 0.09f));

            // 左右の小塔（当たりあり）。根元の台座・上の縁・胸壁・窓。
            const float towerHeight = 13f;
            float towerDepth = Front - (porchFront - 1.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                var foot = Rect.MinMaxRect(Mathf.Min(side * pier, side * half), porchFront - 1.5f, Mathf.Max(side * pier, side * half), Front);
                SurfaceSceneBuilder.Box(castle, side < 0f ? "Gate_Tower_L" : "Gate_Tower_R", m.Foundation,
                    Center(foot, towerHeight * 0.5f), new Vector3(foot.width, towerHeight, towerDepth));
                b.Box(m.Trim, Center(foot, 0.5f), new Vector3(foot.width + 0.5f, 1f, towerDepth + 0.5f));
                b.Box(m.Trim, Center(foot, towerHeight + 0.3f), new Vector3(foot.width + 0.8f, 0.6f, towerDepth + 0.8f));
                var top = Rect.MinMaxRect(foot.xMin - 0.4f, foot.yMin - 0.4f, foot.xMax + 0.4f, foot.yMax + 0.4f);
                Battlements(b, m, top, towerHeight + 0.6f);

                // 正面は旗の上に 1 つ、外側の面に 2 つ。
                Window(b, m, new Vector3(foot.center.x, 9.5f, foot.yMin), Vector3.back, Vector3.right);
                foreach (float y in new[] { 4.5f, 9.5f })
                    Window(b, m, new Vector3(side < 0f ? foot.xMin : foot.xMax, y, foot.center.y), side < 0f ? Vector3.left : Vector3.right, side < 0f ? Vector3.forward : Vector3.back);

                // 小塔の正面に旗。
                Banner(castle, side < 0f ? "a" : "b", new Vector3(foot.center.x, 6.6f, foot.yMin - 0.1f));
            }
        }

        private static Vector2 Polar(float radius, float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        /// <summary>中心 center・半径 inner〜outer・角度 a0〜a1 の扇形の帯（反時計回り）。</summary>
        private static Vector2[] ArcSegment(Vector2 center, float inner, float outer, float a0, float a1) => new[]
        {
            center + Polar(inner, a0), center + Polar(outer, a0), center + Polar(outer, a1), center + Polar(inner, a1),
        };

        /// <summary>旗を掛ける。素材の旗は原点が上の端なので、at は吊るす所。</summary>
        private static void Banner(Transform castle, string variant, Vector3 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BannerPath}{variant}.prefab");
            if (prefab == null) return;
            var banner = (GameObject)PrefabUtility.InstantiatePrefab(prefab, castle);
            banner.name = "Banner_" + variant;
            banner.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, 180f, 0f));
            banner.transform.localScale = Vector3.one * 4f;
        }

        // --- 段の割り付け ---

        /// <summary>塔の 1 階分。どの層か、下端の高さと高さ、部屋の並び、壁の素材。</summary>
        private sealed class Storey
        {
            public int Layer;
            public float Bottom;
            public float Height;
            public FloorLayout Layout;
            public Material Wall;
        }

        /// <summary>
        /// 層ごとに、ランダムな階 × randomFloorCount とボス階を下から並べる。層の段の下端は elevation の少し下（1 層目は土台の上）、
        /// 上端は次の層の段の下端で、層の中の階は高さを等分する。
        /// </summary>
        private static List<Storey> PlanStoreys(DungeonRoute route, Materials m)
        {
            var result = new List<Storey>();
            int count = route.Layers.Count;
            var bottoms = new float[count + 1];
            for (int i = 0; i < count; i++)
            {
                FloorConfig config = route.Layers[i].randomConfig;
                float elevation = config != null ? config.elevation : 20f + 30f * i;
                bottoms[i] = i == 0 ? BaseHeight : Mathf.Max(bottoms[i - 1] + 12f, elevation - 6f);
            }
            bottoms[count] = bottoms[count - 1] + TopLayerHeight;

            for (int i = 0; i < count; i++)
            {
                DungeonRoute.Layer layer = route.Layers[i];
                var layouts = new List<FloorLayout>();
                for (int f = 0; f < Mathf.Max(0, layer.randomFloorCount); f++)
                {
                    FloorLayout layout = TryLayout(layer.randomConfig, false, Seed + i * 101 + f);
                    if (layout != null) layouts.Add(layout);
                }
                if (layer.HasBoss)
                {
                    FloorLayout layout = TryLayout(layer.bossConfig, true, Seed + i * 101 + 50);
                    if (layout != null) layouts.Add(layout);
                }
                if (layouts.Count == 0) continue;

                Material wall = layer.randomConfig != null && layer.randomConfig.theme != null && layer.randomConfig.theme.wall != null
                    ? layer.randomConfig.theme.wall
                    : m.Foundation;
                float height = (bottoms[i + 1] - bottoms[i]) / layouts.Count;
                for (int f = 0; f < layouts.Count; f++)
                {
                    result.Add(new Storey { Layer = i, Bottom = bottoms[i] + height * f, Height = height, Layout = layouts[f], Wall = wall });
                }
            }
            return result;
        }

        private static FloorLayout TryLayout(FloorConfig config, bool boss, int seed)
        {
            if (config == null) return null;
            try
            {
                return boss
                    ? BossFloorLayout.Generate(config.BuildBossParams(), seed)
                    : FloorLayoutGenerator.Generate(config.BuildParams(), seed);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{config.name} の部屋の並びを作れなかったので、その階は飛ばした: {e.Message}");
                return null;
            }
        }

        /// <summary>土台の上面の四角（x と z）。</summary>
        private static Rect BaseFoot() => Rect.MinMaxRect(-BaseWidth * 0.5f, Front, BaseWidth * 0.5f, Front + BaseDepth);

        /// <summary>index 階目（全 total 階のうち）の足元の四角。土台から一番上へ、上ほど急に広げる。</summary>
        private static Rect Foot(int index, int total) => FootAt((index + 1f) / total);

        /// <summary>高さ height（メートル）の塔の足元のだいたいの四角。塔のまわりのもや（MistBuilder）が塔の面に沿わせるのに使う。</summary>
        internal static Rect FootAtHeight(float height)
        {
            float top = TopHeight();
            return height <= BaseHeight ? BaseFoot() : FootAt(Mathf.Clamp01((height - BaseHeight) / (top - BaseHeight)));
        }

        /// <summary>塔の屋上のだいたいの高さ（一番上の層の段の上端）。道順が無ければ今の高さ。</summary>
        internal static float TopHeight()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            FloorConfig last = route != null && route.Layers.Count > 0 ? route.Layers[route.Layers.Count - 1].randomConfig : null;
            return last != null ? last.elevation - 6f + TopLayerHeight : 174f;
        }

        /// <summary>土台の上から屋上まで 0〜1 の fraction の所の足元の四角。上ほど急に広げる。</summary>
        private static Rect FootAt(float fraction)
        {
            float t = Mathf.Pow(fraction, Flare);
            float width = Mathf.Lerp(BaseWidth + 4f, TopWidth, t);
            float depth = Mathf.Lerp(BaseDepth + 4f, TopDepth, t);
            float front = Front - Mathf.Lerp(1.5f, TopOverhang, t);
            return Rect.MinMaxRect(-width * 0.5f, front, width * 0.5f, front + depth);
        }

        // --- 1 階分 ---

        /// <summary>
        /// 1 階分。床の板と中の芯の箱を置き、部屋の並びを足元の四角へ縮めて部屋ごとに押し出す。
        /// 並びは階ごとに裏返して向きを散らす。外周に接する部屋の辺は外へ出したり引っ込めたりして輪郭をギザギザにする。
        /// </summary>
        private static void BuildStorey(MeshBuilder b, Materials m, Storey storey, Rect foot, Random random)
        {
            float y = storey.Bottom;
            float h = storey.Height;

            // 床の板（階の境目の横の線）と、部屋の隙間から見える芯。
            b.Box(m.Trim, Center(foot, y + 0.3f), new Vector3(foot.width * 0.8f, 0.6f, foot.height * 0.8f));
            b.Box(storey.Wall, Center(foot, y + h * 0.5f), new Vector3(foot.width * 0.7f, h, foot.height * 0.7f));

            FloorLayout layout = storey.Layout;
            GridRect all = layout.RoomsBounds;
            bool flipX = random.NextDouble() < 0.5;
            bool flipZ = random.NextDouble() < 0.5;
            foreach (RoomInstance room in layout.Rooms)
            {
                GridRect r = room.Bounds;
                float u0 = (float)(r.X - all.X) / all.Width;
                float u1 = (float)(r.X + r.Width - all.X) / all.Width;
                float v0 = (float)(r.Y - all.Y) / all.Height;
                float v1 = (float)(r.Y + r.Height - all.Y) / all.Height;
                if (flipX) (u0, u1) = (1f - u1, 1f - u0);
                if (flipZ) (v0, v1) = (1f - v1, 1f - v0);

                float xMin = Mathf.Lerp(foot.xMin, foot.xMax, u0);
                float xMax = Mathf.Lerp(foot.xMin, foot.xMax, u1);
                float zMin = Mathf.Lerp(foot.yMin, foot.yMax, v0);
                float zMax = Mathf.Lerp(foot.yMin, foot.yMax, v1);

                // 中の部屋の並びは隙間が多いので、部屋を真ん中から少し膨らませて埋める。
                float grow = 0.12f * Mathf.Max(xMax - xMin, zMax - zMin);
                xMin -= grow; xMax += grow; zMin -= grow; zMax += grow;

                // 外周の辺は出し入れして輪郭をギザギザにする。
                xMin = u0 <= 0.001f ? xMin - Range(random, -1.5f, 3f) : xMin;
                xMax = u1 >= 0.999f ? xMax + Range(random, -1.5f, 3f) : xMax;
                zMin = v0 <= 0.001f ? zMin - Range(random, -1.5f, 2f) : zMin;
                zMax = v1 >= 0.999f ? zMax + Range(random, -1.5f, 3f) : zMax;
                if (xMax - xMin < 2f || zMax - zMin < 2f) continue;

                // たまに 1 階分より高く（上の階を突き抜ける）、たまに低くする。
                double roll = random.NextDouble();
                float roomHeight = roll < 0.12 ? h * 1.6f : roll < 0.3 ? h * 0.75f : h - 0.5f;
                var rect = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
                float bottom = y + 0.6f;
                b.Box(storey.Wall, Center(rect, bottom + roomHeight * 0.5f), new Vector3(rect.width, roomHeight, rect.height));
                // 部屋の上の縁。
                b.Box(m.Trim, Center(rect, bottom + roomHeight - 0.2f), new Vector3(rect.width + 0.5f, 0.4f, rect.height + 0.5f));
                if (roomHeight < h && random.NextDouble() < 0.4) Battlements(b, m, rect, bottom + roomHeight);

                RoomWindows(b, m, rect, bottom, roomHeight, foot, random);
            }
        }

        /// <summary>部屋の 4 面のうち、芯の外に出ている面に窓を並べる。</summary>
        private static void RoomWindows(MeshBuilder b, Materials m, Rect rect, float bottom, float height, Rect foot, Random random)
        {
            Rect core = Shrink(foot, 0.15f);
            var faces = new (Vector3 normal, Vector3 along, float plane, float from, float to, bool outside)[]
            {
                (Vector3.back, Vector3.right, rect.yMin, rect.xMin, rect.xMax, rect.yMin < core.yMin),
                (Vector3.forward, Vector3.left, rect.yMax, rect.xMin, rect.xMax, rect.yMax > core.yMax),
                (Vector3.left, Vector3.forward, rect.xMin, rect.yMin, rect.yMax, rect.xMin < core.xMin),
                (Vector3.right, Vector3.back, rect.xMax, rect.yMin, rect.yMax, rect.xMax > core.xMax),
            };
            foreach (var face in faces)
            {
                if (!face.outside) continue;
                float span = face.to - face.from;
                int columns = Mathf.FloorToInt(span / WindowCell);
                if (columns <= 0) continue;
                float step = span / columns;
                for (int row = 0; ; row++)
                {
                    float y = bottom + row * WindowCell + (WindowSill + WindowTop) * 0.5f;
                    if (y + (WindowTop - WindowSill) * 0.5f + 0.4f > bottom + height) break;
                    for (int c = 0; c < columns; c++)
                    {
                        if (random.NextDouble() > 0.6) continue;
                        float along = face.from + step * (c + 0.5f);
                        Vector3 at = face.normal.x != 0f ? new Vector3(face.plane, y, along) : new Vector3(along, y, face.plane);
                        Window(b, m, at, face.normal, face.along);
                    }
                }
            }
        }

        /// <summary>
        /// 面の上の点 at（窓の真ん中）に、外向き normal の窓を付ける。
        /// 形は中の階の窓（PlaceholderMapAssetGenerator の窓付きの壁）に揃え、幅 5 m・高さ 5 m の壁の真ん中の上寄りに開いた横長の穴に、扉と同じ木の枠と窓台を付ける。
        /// </summary>
        private static void Window(MeshBuilder b, Materials m, Vector3 at, Vector3 normal, Vector3 along)
        {
            Vector3 x = Abs(along);
            Vector3 z = Abs(normal);
            float w = WindowWidth;
            float h = WindowTop - WindowSill;
            // 穴は面のすぐ前の薄い板。枠のほうが出ているので奥まって見える。
            b.Box(m.Dark, at + normal * 0.025f, x * w + Vector3.up * h + z * 0.05f);

            // 枠は穴のまわりに、面から FrameDepth だけ出す。穴の板の縁と面が重なってチラつかないよう、少し内側へ寄せて縁を枠の中に隠す。
            const float f = FrameWidth;
            const float inset = 0.02f;
            Vector3 face = at + normal * (FrameDepth * 0.5f);
            Vector3 jamb = x * f + Vector3.up * (h + f * 2f) + z * FrameDepth;
            b.Box(m.DoorLeaf, face + along * ((w + f) * 0.5f - inset), jamb);
            b.Box(m.DoorLeaf, face - along * ((w + f) * 0.5f - inset), jamb);
            // 上の横木は左右と前の面が重ならないよう少し薄く。
            b.Box(m.DoorLeaf, face + Vector3.up * ((h + f) * 0.5f - inset) - normal * 0.005f, x * (w + f * 2f) + Vector3.up * f + z * (FrameDepth - 0.01f));
            b.Box(m.DoorLeaf, at + normal * FrameDepth + Vector3.down * ((h + f) * 0.5f - inset), x * (w + f * 2f + 0.2f) + Vector3.up * f + z * (FrameDepth * 2f));
        }

        /// <summary>屋上の縁に胸壁（間をあけた小さな箱）を並べる。</summary>
        private static void Battlements(MeshBuilder b, Materials m, Rect rect, float top)
        {
            const float merlon = 1.1f;
            const float gap = 1.1f;
            for (float x = rect.xMin + merlon * 0.5f; x <= rect.xMax - merlon * 0.5f; x += merlon + gap)
            {
                b.Box(m.Trim, new Vector3(x, top + 0.5f, rect.yMin + 0.3f), new Vector3(merlon, 1f, 0.6f));
                b.Box(m.Trim, new Vector3(x, top + 0.5f, rect.yMax - 0.3f), new Vector3(merlon, 1f, 0.6f));
            }
            for (float z = rect.yMin + merlon * 1.5f; z <= rect.yMax - merlon * 1.5f; z += merlon + gap)
            {
                b.Box(m.Trim, new Vector3(rect.xMin + 0.3f, top + 0.5f, z), new Vector3(0.6f, 1f, merlon));
                b.Box(m.Trim, new Vector3(rect.xMax - 0.3f, top + 0.5f, z), new Vector3(0.6f, 1f, merlon));
            }
        }

        /// <summary>層の境目の持ち送り。下の階の足元 below から上の階の足元 above へ、下が細い段々を 4 段重ねる。</summary>
        private static void Corbel(MeshBuilder b, Material trim, Rect below, Rect above, float top)
        {
            const int steps = 4;
            const float stepHeight = 1.1f;
            for (int i = 0; i < steps; i++)
            {
                float t = 1f - (float)i / steps;
                var rect = Rect.MinMaxRect(
                    Mathf.Lerp(below.xMin, above.xMin, t) - 0.4f, Mathf.Lerp(below.yMin, above.yMin, t) - 0.4f,
                    Mathf.Lerp(below.xMax, above.xMax, t) + 0.4f, Mathf.Lerp(below.yMax, above.yMax, t) + 0.4f);
                b.Box(trim, Center(rect, top - stepHeight * (i + 0.5f)), new Vector3(rect.width, stepHeight, rect.height));
            }
        }

        // --- てっぺん ---

        /// <summary>一番上の屋上。屋根の板、真ん中の天守と尖り屋根、四隅と散らばった小塔。</summary>
        private static void BuildCrown(MeshBuilder b, Materials m, Rect foot, float top, Random random)
        {
            b.Box(m.Trim, Center(foot, top + 0.4f), new Vector3(foot.width * 0.95f, 0.8f, foot.height * 0.95f));
            float roof = top + 0.8f;

            // 天守。
            var keep = Rect.MinMaxRect(-14f, foot.center.y - 12f, 14f, foot.center.y + 12f);
            Turret(b, m, keep, roof, 26f, 30f, random);
            var upper = Shrink(keep, 0.25f);
            Turret(b, m, upper, roof + 26f, 12f, 22f, random);

            // 四隅の小塔は下の階まで垂らし、下を段々に細くして壁から生えているように見せる。
            const float corner = 11f;
            foreach (var (x, z) in new[] { (foot.xMin + 2f, foot.yMin + 2f), (foot.xMax - 2f, foot.yMin + 2f), (foot.xMin + 2f, foot.yMax - 2f), (foot.xMax - 2f, foot.yMax - 2f) })
            {
                var rect = Rect.MinMaxRect(x - corner * 0.5f, z - corner * 0.5f, x + corner * 0.5f, z + corner * 0.5f);
                float bottom = roof - 22f;
                Turret(b, m, rect, bottom, 22f + Range(random, 0f, 10f), corner * 1.4f, random);
                for (int i = 1; i <= 4; i++)
                {
                    Rect step = Shrink(rect, 0.1f * i);
                    b.Box(m.Foundation, Center(step, bottom - 1.2f * (i - 0.5f)), new Vector3(step.width, 1.2f, step.height));
                }
            }

            // 屋上に散らばる小塔。
            int placed = 0;
            for (int tries = 0; tries < 200 && placed < 9; tries++)
            {
                float size = Range(random, 5f, 9f);
                float x = Range(random, foot.xMin + size, foot.xMax - size);
                float z = Range(random, foot.yMin + size, foot.yMax - size);
                if (Mathf.Abs(x) < 20f && Mathf.Abs(z - foot.center.y) < 18f) continue; // 天守のまわり
                if (Mathf.Abs(x) > foot.width * 0.5f - corner && (z < foot.yMin + corner * 1.5f || z > foot.yMax - corner * 1.5f)) continue;
                var rect = Rect.MinMaxRect(x - size * 0.5f, z - size * 0.5f, x + size * 0.5f, z + size * 0.5f);
                Turret(b, m, rect, roof, Range(random, 6f, 18f), size * Range(random, 1.1f, 1.8f), random);
                placed++;
            }
        }

        /// <summary>四角い塔（壁・窓・上の縁）と、その上の四角錐の屋根。</summary>
        private static void Turret(MeshBuilder b, Materials m, Rect rect, float bottom, float height, float roofHeight, Random random)
        {
            b.Box(m.Foundation, Center(rect, bottom + height * 0.5f), new Vector3(rect.width, height, rect.height));
            b.Box(m.Trim, Center(rect, bottom + height - 0.3f), new Vector3(rect.width + 1f, 0.6f, rect.height + 1f));
            RoomWindows(b, m, rect, bottom, height, Rect.MinMaxRect(rect.center.x, rect.center.y, rect.center.x, rect.center.y), random);
            b.Pyramid(m.Roof, Center(rect, bottom + height), rect.width + 1.2f, rect.height + 1.2f, roofHeight);
        }

        // --- 補助 ---

        private static Vector3 Center(Rect rect, float y) => new Vector3(rect.center.x, y, rect.center.y);

        private static Rect Shrink(Rect rect, float fraction)
        {
            float dx = rect.width * fraction;
            float dz = rect.height * fraction;
            return Rect.MinMaxRect(rect.xMin + dx, rect.yMin + dz, rect.xMax - dx, rect.yMax - dz);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static float Range(Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>塔に使う素材。</summary>
        private sealed class Materials
        {
            public readonly Material Foundation = Load(StoneMaterialPath) ?? Load($"{MaterialsFolder}/Placeholder_Wall.mat");
            public readonly Material Trim = Load($"{MaterialsFolder}/Placeholder_Wall.mat");
            public readonly Material Door = Load($"{MaterialsFolder}/Placeholder_Door.mat");
            public readonly Material DoorLeaf = Load($"{MaterialsFolder}/Placeholder_DoorLeaf.mat");
            public readonly Material Dark = Load($"{MaterialsFolder}/Placeholder_Void.mat") ?? Load($"{MaterialsFolder}/Placeholder_Ceiling.mat");
            public readonly Material Roof = Load(RoofMaterialPath) ?? Load($"{MaterialsFolder}/Placeholder_Door.mat");

            private static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>箱と四角錐を素材ごとのサブメッシュへ溜めて、1 つのメッシュにする。UV は面ごとの平面投影で 1 m = 1（中の壁と同じ）。</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Material> materials = new List<Material>();
            private readonly List<List<int>> triangles = new List<List<int>>();

            public int Boxes { get; private set; }

            public void Box(Material material, Vector3 center, Vector3 size)
            {
                if (material == null) return;
                Vector3 e = size * 0.5f;
                List<int> list = Triangles(material);
                Quad(list, center, Vector3.right, e.x, Vector3.up, e.y, Vector3.back, e.z);
                Quad(list, center, Vector3.left, e.x, Vector3.up, e.y, Vector3.forward, e.z);
                Quad(list, center, Vector3.forward, e.z, Vector3.up, e.y, Vector3.right, e.x);
                Quad(list, center, Vector3.back, e.z, Vector3.up, e.y, Vector3.left, e.x);
                Quad(list, center, Vector3.right, e.x, Vector3.forward, e.z, Vector3.up, e.y);
                Quad(list, center, Vector3.right, e.x, Vector3.back, e.z, Vector3.down, e.y);
                Boxes++;
            }

            /// <summary>底面の中心 baseCenter・底の幅 width × 奥行き depth・高さ height の四角錐（底は塞がない）。</summary>
            public void Pyramid(Material material, Vector3 baseCenter, float width, float depth, float height)
            {
                if (material == null) return;
                List<int> list = Triangles(material);
                Vector3 apex = baseCenter + Vector3.up * height;
                float x = width * 0.5f, z = depth * 0.5f;
                var corners = new[]
                {
                    baseCenter + new Vector3(-x, 0f, -z), baseCenter + new Vector3(x, 0f, -z),
                    baseCenter + new Vector3(x, 0f, z), baseCenter + new Vector3(-x, 0f, z),
                };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = corners[i];
                    Vector3 c = corners[(i + 1) % 4];
                    Vector3 normal = Vector3.Cross(apex - a, c - a).normalized;
                    // 面の傾きに沿って UV を取る（屋根の模様が斜面に沿って並ぶ）。
                    Vector3 across = (c - a).normalized;
                    Vector3 up = Vector3.Cross(normal, across);
                    int s = vertices.Count;
                    foreach (Vector3 p in new[] { a, apex, c })
                    {
                        vertices.Add(p);
                        normals.Add(normal);
                        uvs.Add(new Vector2(Vector3.Dot(p, across), Vector3.Dot(p, up)));
                    }
                    list.Add(s);
                    list.Add(s + 1);
                    list.Add(s + 2);
                }
                Boxes++;
            }

            /// <summary>
            /// xy の面の凸な多角形 outline を、z = front から back（front &lt; back）まで押し出す。前の面は -Z、後ろの面は +Z を向く。
            /// UV は前後の面が (x, y)、側面が (辺に沿った長さ, z)。
            /// </summary>
            public void Extrude(Material material, IList<Vector2> outline, float front, float back)
            {
                if (material == null || outline.Count < 3) return;
                List<int> list = Triangles(material);
                var points = new List<Vector2>(outline);
                float area = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i], c = points[(i + 1) % points.Count];
                    area += a.x * c.y - c.x * a.y;
                }
                if (area < 0f) points.Reverse();

                // 前後の面（扇に割る）。反時計回りの (0, i, i+1) は +Z を向く。
                foreach (bool isFront in new[] { true, false })
                {
                    float z = isFront ? front : back;
                    Vector3 normal = isFront ? Vector3.back : Vector3.forward;
                    int s = vertices.Count;
                    foreach (Vector2 p in points)
                    {
                        vertices.Add(new Vector3(p.x, p.y, z));
                        normals.Add(normal);
                        uvs.Add(new Vector2(p.x, p.y));
                    }
                    for (int i = 1; i + 1 < points.Count; i++)
                    {
                        list.Add(s);
                        list.Add(s + (isFront ? i + 1 : i));
                        list.Add(s + (isFront ? i : i + 1));
                    }
                }

                // 側面。反時計回りの辺 a → b の外向きは (dy, -dx)。
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = points[i], c = points[(i + 1) % points.Count];
                    Vector2 edge = c - a;
                    if (edge.sqrMagnitude < 1e-8f) continue;
                    Vector3 normal = new Vector3(edge.y, -edge.x, 0f).normalized;
                    Vector2 dir = edge.normalized;
                    int s = vertices.Count;
                    foreach (Vector3 p in new[] { new Vector3(a.x, a.y, front), new Vector3(c.x, c.y, front), new Vector3(c.x, c.y, back), new Vector3(a.x, a.y, back) })
                    {
                        vertices.Add(p);
                        normals.Add(normal);
                        uvs.Add(new Vector2(p.x * dir.x + p.y * dir.y, p.z));
                    }
                    list.Add(s);
                    list.Add(s + 1);
                    list.Add(s + 2);
                    list.Add(s);
                    list.Add(s + 2);
                    list.Add(s + 3);
                }
                Boxes++;
            }

            /// <summary>中心から normal の向きに distance 離れた面。u・v は面の上の向き（v × u が表の向きになるよう渡す）。</summary>
            private void Quad(List<int> list, Vector3 center, Vector3 u, float uExtent, Vector3 v, float vExtent, Vector3 normal, float distance)
            {
                Vector3 c = center + normal * distance;
                int s = vertices.Count;
                Vector3 du = u * uExtent;
                Vector3 dv = v * vExtent;
                foreach (Vector3 p in new[] { c - du - dv, c - du + dv, c + du + dv, c + du - dv })
                {
                    vertices.Add(p);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)));
                }
                list.Add(s);
                list.Add(s + 1);
                list.Add(s + 2);
                list.Add(s);
                list.Add(s + 2);
                list.Add(s + 3);
            }

            private List<int> Triangles(Material material)
            {
                int index = materials.IndexOf(material);
                if (index >= 0) return triangles[index];
                materials.Add(material);
                triangles.Add(new List<int>());
                return triangles[triangles.Count - 1];
            }

            /// <summary>メッシュを path に書き出し（あれば中身を差し替える）、parent の下に name で置く。</summary>
            public GameObject Save(Transform parent, string name, string path)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh();
                    AssetDatabase.CreateAsset(mesh, path);
                }
                mesh.Clear();
                mesh.name = name;
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                // 圧縮しないとテキストのアセットが層ごとに数 MB になる。
                MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
                EditorUtility.SetDirty(mesh);

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                return go;
            }
        }
    }
}
