using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// ショートカットの見た目を組む。ボス階の「ロープの垂れた、底の抜けた井戸」と、地上の「空から垂れたロープ」の目印。
    /// どちらも ProBuilder の箱と円柱で実寸に作る（スケールしない）。素材は Materials/Shortcut に無いときだけ作り、あれば手で調節した値を残す。
    /// 調べると移る所（ShortcutPoint）は、見た目の一部（井戸の枠とロープ・ロープと台座）にだけ付け、床などを見ても案内が出ないようにする。
    /// </summary>
    public static class ShortcutArt
    {
        private const string MaterialsFolder = "Assets/_Project/Materials/Shortcut";

        // 井戸（ボス階）。原点はセルの中心の床の高さ。
        private const float WellHole = 1.5f;          // 穴（正方形）の一辺
        private const float WellDepth = 7f;           // 竪穴の深さ。底は暗がりで見えない
        private const float CurbThickness = 0.35f;
        private const float CurbHeight = 0.85f;
        private const float PostSize = 0.18f;
        private const float BeamHeight = 2.5f;
        private const float RopeRadius = 0.06f;
        private const float FloorThickness = 0.1f;
        private const float WallThickness = 0.15f;

        // 地上のロープ。
        public const float SkyRopeTop = 60f;          // 吊り元の高さ。見上げても端が見えにくいよう高くする
        private const float DaisRadius = 1.4f;
        private const float DaisHeight = 0.18f;

        /// <summary>層ごとの旗の色（第 1 層から）。足りなければ最後の色。</summary>
        private static readonly Color[] LayerColors =
        {
            new Color(0.75f, 0.2f, 0.18f),
            new Color(0.2f, 0.45f, 0.8f),
            new Color(0.3f, 0.65f, 0.3f),
            new Color(0.62f, 0.32f, 0.72f),
            new Color(0.88f, 0.68f, 0.18f),
        };

        // ---- ボス階の井戸 ----

        /// <summary>
        /// ボス階のショートカットの部屋の真ん中に置く井戸を path に作る。原点はセルの中心の床の高さ。
        /// 部屋の床の代わりに正方形の穴の空いた床を持ち（FloorFeature.replacesRoomFloor）、穴の下に深い竪穴を掘って底を暗がりに沈める。
        /// 穴のまわりに石の井戸枠、南北に柱を立てて横木と滑車を渡し、そこから太いロープを竪穴の闇の中へ垂らす。
        /// 開いている（その層を解放してある）ときだけ、枠の内側の縁が淡く光り、穴のまわりを冷たい光で照らす。
        /// 調べると地上へ降りる（子の Well の ShortcutPoint。層は FloorBootstrap が入れる）。閉じている間は案内も出さない。
        /// </summary>
        public static GameObject BuildWellPrefab(string path, FloorTheme theme, float cellSize, Material floorMaterial)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var feature = root.AddComponent<FloorFeature>();
            feature.replacesRoomFloor = true;
            feature.depthBelowFloor = WellDepth + 0.2f;

            float h = WellHole * 0.5f;
            var holeMin = new Vector2(-h, -h);
            var holeMax = new Vector2(h, h);
            Material stone = theme != null && theme.wall != null ? theme.wall : Stone();
            Material capStone = theme != null && theme.foundation != null ? theme.foundation : stone;
            Material voidMaterial = theme != null && theme.stairVoid != null ? theme.stairVoid : stone;

            HoledSlab(root.transform, "Floor", floorMaterial != null ? floorMaterial : stone, cellSize, holeMin, holeMax, -FloorThickness, FloorThickness);

            // 竪穴と底。底は真っ黒にして、上からは闇が続いて見えるようにする。
            var shaft = new GameObject("Shaft");
            shaft.transform.SetParent(root.transform, false);
            float shaftHeight = WellDepth - FloorThickness;
            float shaftY = -FloorThickness - shaftHeight * 0.5f;
            Box(shaft.transform, "North", stone, new Vector3(0f, shaftY, h + WallThickness * 0.5f), new Vector3(WellHole + WallThickness * 2f, shaftHeight, WallThickness), true);
            Box(shaft.transform, "South", stone, new Vector3(0f, shaftY, -h - WallThickness * 0.5f), new Vector3(WellHole + WallThickness * 2f, shaftHeight, WallThickness), true);
            Box(shaft.transform, "East", stone, new Vector3(h + WallThickness * 0.5f, shaftY, 0f), new Vector3(WallThickness, shaftHeight, WellHole), true);
            Box(shaft.transform, "West", stone, new Vector3(-h - WallThickness * 0.5f, shaftY, 0f), new Vector3(WallThickness, shaftHeight, WellHole), true);
            Box(shaft.transform, "Bottom", voidMaterial, new Vector3(0f, -WellDepth - 0.1f, 0f), new Vector3(WellHole + WallThickness * 2f, 0.2f, WellHole + WallThickness * 2f), true);
            AddDarkness(root.transform, new Vector3(0f, -WellDepth * 0.5f, 0f), new Vector3(WellHole + 0.04f, WellDepth, WellHole + 0.04f));

            // 調べられるのは井戸枠・柱・ロープと、穴の上（見えない当たり）。床を見ても案内が出ないよう、床とは別の子にまとめる。
            var well = new GameObject("Well");
            well.transform.SetParent(root.transform, false);

            // 井戸枠。穴を囲む石の低い壁 4 枚と、その上の笠石。
            float outer = WellHole + CurbThickness * 2f;
            float curbY = CurbHeight * 0.5f;
            var curb = new GameObject("Curb");
            curb.transform.SetParent(well.transform, false);
            Box(curb.transform, "North", stone, new Vector3(0f, curbY, h + CurbThickness * 0.5f), new Vector3(outer, CurbHeight, CurbThickness), true);
            Box(curb.transform, "South", stone, new Vector3(0f, curbY, -h - CurbThickness * 0.5f), new Vector3(outer, CurbHeight, CurbThickness), true);
            Box(curb.transform, "East", stone, new Vector3(h + CurbThickness * 0.5f, curbY, 0f), new Vector3(CurbThickness, CurbHeight, WellHole), true);
            Box(curb.transform, "West", stone, new Vector3(-h - CurbThickness * 0.5f, curbY, 0f), new Vector3(CurbThickness, CurbHeight, WellHole), true);
            // 笠石。枠の上に少し張り出して載せる。
            float capY = CurbHeight + 0.06f;
            float capZ = h + CurbThickness * 0.5f;
            Box(curb.transform, "CapNorth", capStone, new Vector3(0f, capY, capZ), new Vector3(outer + 0.1f, 0.12f, CurbThickness + 0.1f), false);
            Box(curb.transform, "CapSouth", capStone, new Vector3(0f, capY, -capZ), new Vector3(outer + 0.1f, 0.12f, CurbThickness + 0.1f), false);
            Box(curb.transform, "CapEast", capStone, new Vector3(capZ, capY, 0f), new Vector3(CurbThickness + 0.1f, 0.12f, WellHole), false);
            Box(curb.transform, "CapWest", capStone, new Vector3(-capZ, capY, 0f), new Vector3(CurbThickness + 0.1f, 0.12f, WellHole), false);

            // 南北の柱と、その上に渡す横木。横木の真ん中に滑車を吊り、そこからロープを竪穴の底の闇まで垂らす。
            float postZ = h + CurbThickness * 0.5f;
            Box(well.transform, "PostNorth", Wood(), new Vector3(0f, (CurbHeight + BeamHeight + 0.3f) * 0.5f, postZ), new Vector3(PostSize, BeamHeight + 0.3f - CurbHeight, PostSize), true);
            Box(well.transform, "PostSouth", Wood(), new Vector3(0f, (CurbHeight + BeamHeight + 0.3f) * 0.5f, -postZ), new Vector3(PostSize, BeamHeight + 0.3f - CurbHeight, PostSize), true);
            Box(well.transform, "Beam", Wood(), new Vector3(0f, BeamHeight, 0f), new Vector3(PostSize * 0.9f, PostSize * 0.9f, postZ * 2f + PostSize * 1.6f), false);
            var pulley = Cylinder(well.transform, "Pulley", Iron(), new Vector3(0f, BeamHeight - 0.24f, 0f), 0.16f, 0.08f, 10);
            pulley.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            float ropeTop = BeamHeight - 0.24f;
            float ropeBottom = -WellDepth + 0.6f;
            Cylinder(well.transform, "Rope", Rope(), new Vector3(0f, (ropeTop + ropeBottom) * 0.5f, 0f), RopeRadius, ropeTop - ropeBottom, 8);
            // 穴は竪穴の底から枠の上の高さまで、見えない当たりで埋めておく。跳んでも、仲間が寄ってきても落ちない。
            // ShortcutPoint と同じ子にあるので、照準が穴の中に向いても調べられる。
            var plug = new GameObject("Plug");
            plug.transform.SetParent(well.transform, false);
            var block = plug.AddComponent<BoxCollider>();
            float plugTop = BeamHeight;
            float plugBottom = -WellDepth;
            block.center = new Vector3(0f, (plugTop + plugBottom) * 0.5f, 0f);
            block.size = new Vector3(WellHole, plugTop - plugBottom, WellHole);

            // 開いているときだけ出す光。枠の内側の縁の光る帯と、穴の上の冷たい灯り。
            var glow = new GameObject("OpenGlow");
            glow.transform.SetParent(well.transform, false);
            float rim = CurbHeight + 0.125f;
            float inner = h - 0.03f;
            Box(glow.transform, "RimNorth", Glow(), new Vector3(0f, rim, inner), new Vector3(WellHole, 0.03f, 0.05f), false);
            Box(glow.transform, "RimSouth", Glow(), new Vector3(0f, rim, -inner), new Vector3(WellHole, 0.03f, 0.05f), false);
            Box(glow.transform, "RimEast", Glow(), new Vector3(inner, rim, 0f), new Vector3(0.05f, 0.03f, WellHole), false);
            Box(glow.transform, "RimWest", Glow(), new Vector3(-inner, rim, 0f), new Vector3(0.05f, 0.03f, WellHole), false);
            AddLight(glow.transform, "Light", new Vector3(0f, 1.1f, 0f), new Color(0.45f, 0.85f, 0.95f), 0.9f, 4.5f, flicker: false);
            foreach (var renderer in glow.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;

            var point = well.AddComponent<ShortcutPoint>();
            Configure(point, ShortcutDirection.ToSurface, null, "ロープを伝って降りる", "",
                "第 {0} 層の井戸",
                "底の見えない井戸。垂れたロープを伝って降りれば、地上へ戻れる。",
                new[] { glow }, new GameObject[0]);
            glow.SetActive(false);

            // 地上のロープを登ってきたパーティーが立つ所。井戸の西（部屋の扉の側）で、扉の方を向く。
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetLocalPositionAndRotation(new Vector3(-(h + CurbThickness + 0.9f), 0.05f, 0f), Quaternion.Euler(0f, -90f, 0f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---- 地上のロープ ----

        /// <summary>
        /// 地上のショートカットの目印を parent の下に作る（原点は地面）。layer は 0 始まり。ropeTop はロープの吊り元の高さ（真上に塔が張り出していればその下面）。
        /// 空の高い所から太いロープが垂れ、足元に丸い石の台座、その脇に層の番号（Ⅰ〜Ⅴ）を刻んだ石板と、層の色の旗。
        /// ロープは開いている（その層を解放してある）ときだけ垂れていて、台座の縁が淡く光る。閉じている間はロープが無く、調べても何も出ない。
        /// 調べると、その層のボス階のショートカットの部屋へ登る。
        /// </summary>
        public static GameObject BuildSurfaceRope(Transform parent, string name, int layer, DungeonRoute route, float ropeTop = SkyRopeTop)
        {
            ropeTop = Mathf.Max(DaisHeight + 3f, ropeTop);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Color color = LayerColors[Mathf.Clamp(layer, 0, LayerColors.Length - 1)];

            // 台座。低い石の円盤（上に乗れる）と、縁の光る輪（開いているときだけ）。
            var dais = Cylinder(root.transform, "Dais", Stone(), new Vector3(0f, DaisHeight * 0.5f - 0.02f, 0f), DaisRadius, DaisHeight, 24);
            dais.AddComponent<MeshCollider>();
            Cylinder(root.transform, "DaisInner", StoneDark(), new Vector3(0f, DaisHeight - 0.01f, 0f), DaisRadius - 0.25f, 0.02f, 24);
            // 開いているときだけ、台座の縁の内側が淡く光る。
            var ring = Cylinder(root.transform, "OpenRing", Glow(), new Vector3(0f, DaisHeight - 0.015f, 0f), DaisRadius - 0.13f, 0.02f, 24);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 石板。台座の南に立て、北（台座）を背にして層の番号を刻む。
            var stele = new GameObject("Stele");
            stele.transform.SetParent(root.transform, false);
            stele.transform.SetLocalPositionAndRotation(new Vector3(1.2f, 0f, -DaisRadius - 0.35f), Quaternion.Euler(0f, 20f, 0f));
            Box(stele.transform, "Stone", Stone(), new Vector3(0f, 0.65f, 0f), new Vector3(0.9f, 1.3f, 0.25f), true);
            Box(stele.transform, "Top", Stone(), new Vector3(0f, 1.34f, 0f), new Vector3(1f, 0.1f, 0.32f), false);
            Numeral(stele.transform, layer + 1, new Vector3(0f, 0.75f, -0.13f), 0.55f, Gold());

            // 旗。台座の西に立てる杭と、層の色の布。
            var flag = new GameObject("Flag");
            flag.transform.SetParent(root.transform, false);
            flag.transform.localPosition = new Vector3(-DaisRadius - 0.4f, 0f, -0.4f);
            Box(flag.transform, "Pole", Wood(), new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 0.1f), true);
            Box(flag.transform, "Cloth", Flag(layer, color), new Vector3(0.45f, 2.75f, 0f), new Vector3(0.8f, 0.6f, 0.03f), false);
            Box(flag.transform, "Tail", Flag(layer, color), new Vector3(0.62f, 2.32f, 0f), new Vector3(0.46f, 0.26f, 0.03f), false);

            // ロープ。吊り元（空の高い所）を軸にゆっくり揺らし、台座の上まで垂らす。開いているときだけ出す。
            var anchor = new GameObject("RopeAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0f, ropeTop, 0f);
            anchor.AddComponent<RopeSway>();
            float ropeLength = ropeTop - DaisHeight - 0.15f;
            Cylinder(anchor.transform, "Rope", Rope(), new Vector3(0f, -ropeLength * 0.5f, 0f), RopeRadius * 1.3f, ropeLength, 8);
            Knot(anchor.transform, new Vector3(0f, -ropeTop + DaisHeight + 0.2f, 0f));

            // 調べる所。台座の上、ロープの下の方（揺れない当たり）。当たりも開いているときだけ置き、閉じている間は照準を遮らない。
            var reach = new GameObject("Reach");
            reach.transform.SetParent(root.transform, false);
            var volume = new GameObject("Volume");
            volume.transform.SetParent(reach.transform, false);
            var trigger = volume.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.6f, 0f);
            trigger.size = new Vector3(1.4f, 3.2f, 1.4f);
            var point = reach.AddComponent<ShortcutPoint>();
            Configure(point, ShortcutDirection.ToBossFloor, route, "ロープを登る", "",
                "第 {0} 層への近道",
                "空から垂れたロープ。登った先は、ダンジョンの第 {0} 層のボス階に通じている。",
                new[] { anchor, ring, volume }, new GameObject[0]);
            // 置いたときは閉じた見た目にしておく（実行中は ShortcutPoint が解放状態に合わせる）。
            anchor.SetActive(false);
            ring.SetActive(false);
            volume.SetActive(false);
            var serialized = new SerializedObject(point);
            serialized.FindProperty("layer").intValue = layer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void Configure(ShortcutPoint point, ShortcutDirection direction, DungeonRoute route, string openLabel, string closedLabel,
            string title, string description, GameObject[] openVisuals, GameObject[] closedVisuals)
        {
            var serialized = new SerializedObject(point);
            serialized.FindProperty("direction").enumValueIndex = (int)direction;
            serialized.FindProperty("route").objectReferenceValue = route;
            serialized.FindProperty("openLabel").stringValue = openLabel;
            serialized.FindProperty("closedLabel").stringValue = closedLabel;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = description;
            SetArray(serialized.FindProperty("openVisuals"), openVisuals);
            SetArray(serialized.FindProperty("closedVisuals"), closedVisuals);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            static void SetArray(SerializedProperty property, GameObject[] values)
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary>ロープの先の結び目（ロープより太い短い円柱 2 段）。</summary>
        private static void Knot(Transform parent, Vector3 at)
        {
            Cylinder(parent, "Knot", Rope(), at, RopeRadius * 2.4f, 0.16f, 8);
            Cylinder(parent, "KnotTail", Rope(), at + Vector3.down * 0.16f, RopeRadius * 1.6f, 0.14f, 8);
        }

        /// <summary>ローマ数字（1〜5）を、太さのある棒で組む。center は字の真ん中、height は字の高さ。棒は -Z へ少し出す。</summary>
        private static void Numeral(Transform parent, int value, Vector3 center, float height, Material material)
        {
            var group = new GameObject("Numeral");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = center;
            float bar = height * 0.16f;
            float gap = height * 0.32f;
            string glyphs = value switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => new string('I', Mathf.Clamp(value, 1, 3)) };
            float width = 0f;
            foreach (char g in glyphs) width += g == 'V' ? height * 0.7f : bar;
            width += gap * (glyphs.Length - 1);
            float x = -width * 0.5f;
            foreach (char g in glyphs)
            {
                if (g == 'I')
                {
                    Box(group.transform, "I", material, new Vector3(x + bar * 0.5f, 0f, -0.02f), new Vector3(bar, height, 0.04f), false);
                    x += bar + gap;
                    continue;
                }
                float w = height * 0.7f;
                float slant = Mathf.Atan2(w * 0.5f - bar * 0.5f, height) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(height * height + (w * 0.5f) * (w * 0.5f));
                var left = Box(group.transform, "VLeft", material, new Vector3(x + w * 0.25f, 0f, -0.02f), new Vector3(bar, length, 0.04f), false);
                left.transform.localRotation = Quaternion.Euler(0f, 0f, slant);
                var right = Box(group.transform, "VRight", material, new Vector3(x + w * 0.75f, 0f, -0.02f), new Vector3(bar, length, 0.04f), false);
                right.transform.localRotation = Quaternion.Euler(0f, 0f, -slant);
                x += w + gap;
            }
        }

        // ---- 部品 ----

        private static GameObject Box(Transform parent, string name, Material material, Vector3 center, Vector3 size, bool collider)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = center;
            if (material != null) mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            if (collider) mesh.gameObject.AddComponent<BoxCollider>().size = size;
            return mesh.gameObject;
        }

        /// <summary>縦向き（Y 軸）の円柱。center が真ん中。当たりは持たない。</summary>
        private static GameObject Cylinder(Transform parent, string name, Material material, Vector3 center, float radius, float height, int sides)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0);
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = center;
            if (material != null) mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            return mesh.gameObject;
        }

        /// <summary>一辺 size の板に XZ の矩形 holeMin〜holeMax の穴を空けたもの（箱 4 枚。当たり付き）。</summary>
        private static void HoledSlab(Transform parent, string name, Material material, float size, Vector2 holeMin, Vector2 holeMax, float bottom, float thickness)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            float h = size * 0.5f;
            float y = bottom + thickness * 0.5f;
            Piece("South", -h, h, -h, holeMin.y);
            Piece("North", -h, h, holeMax.y, h);
            Piece("West", -h, holeMin.x, holeMin.y, holeMax.y);
            Piece("East", holeMax.x, h, holeMin.y, holeMax.y);

            void Piece(string pieceName, float minX, float maxX, float minZ, float maxZ)
            {
                if (maxX - minX < 0.001f || maxZ - minZ < 0.001f) return;
                Box(group.transform, pieceName, material, new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX, thickness, maxZ - minZ), true);
            }
        }

        /// <summary>竪穴の暗がり（階段と同じ DarknessVolume のシェーダ）。床から下へ測って、すぐに暗くなり始め、3 m ほどで真っ黒になる。</summary>
        private static void AddDarkness(Transform parent, Vector3 center, Vector3 size)
        {
            var material = LoadOrCreate("Well_Darkness", () =>
            {
                var shader = Shader.Find("TpsDungeon/Darkness Volume");
                var m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.black);
                if (m.HasProperty("_FadeStart")) m.SetFloat("_FadeStart", 0.4f);
                if (m.HasProperty("_FadeEnd")) m.SetFloat("_FadeEnd", 3.2f);
                return m;
            });
            var volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Darkness";
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            volume.transform.SetParent(parent, false);
            volume.transform.localPosition = center;
            volume.transform.localScale = size;
            var renderer = volume.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range, bool flicker)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (flicker) holder.AddComponent<FlickerLight>();
        }

        // ---- 素材（無いときだけ作る） ----

        private static Material Rope() => Lit("Shortcut_Rope", new Color(0.55f, 0.42f, 0.26f), 0.1f);
        private static Material Wood() => Lit("Shortcut_Wood", new Color(0.36f, 0.24f, 0.15f), 0.15f);
        private static Material Iron() => Lit("Shortcut_Iron", new Color(0.22f, 0.22f, 0.24f), 0.45f, metallic: 0.7f);
        private static Material Stone() => Lit("Shortcut_Stone", new Color(0.58f, 0.56f, 0.53f), 0.1f);
        private static Material StoneDark() => Lit("Shortcut_StoneDark", new Color(0.36f, 0.35f, 0.34f), 0.1f);
        private static Material Gold() => Lit("Shortcut_Gold", new Color(0.85f, 0.66f, 0.25f), 0.55f, metallic: 0.8f);
        private static Material Glow() => Emissive("Shortcut_Glow", new Color(0.3f, 0.75f, 0.85f), new Color(0.25f, 0.7f, 0.9f) * 0.7f);
        private static Material Flag(int layer, Color color) => Lit($"Shortcut_Flag_L{layer + 1}", color, 0.05f);

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f) => LoadOrCreate(name, () =>
        {
            var m = new Material(LitShader());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        });

        /// <summary>光る素材。URP の発光は GI フラグが None だと検証し直しで _EMISSION が外れるので、Baked にしておく。</summary>
        private static Material Emissive(string name, Color color, Color emission) => LoadOrCreate(name, () =>
        {
            var m = new Material(LitShader());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            return m;
        });

        private static Shader LitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? shader : Shader.Find("Standard");
        }

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        private static Material LoadOrCreate(string name, System.Func<Material> create)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(MaterialsFolder);
                material = create();
                AssetDatabase.CreateAsset(material, path);
            }
            Cache[name] = material;
            return material;
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
