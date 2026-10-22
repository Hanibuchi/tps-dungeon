using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 階を確かめるためのウィンドウ（Tools/TPS Dungeon/デバッグ/階のデバッグ）。移る操作は Play 中に使う。
    /// - 道順の好きな階・地上・死後の場所へ移る。階の中だけでなく、地上や死後の場所からでも移れる。
    /// - 階の中では、1 つ下・上の階へ移る、ボス部屋の閉ざされた扉を開けられるようにする。
    /// 「下り階段で降りられる」は道順（DungeonRoute）の設定そのもので、Play していなくても切り替えられる（アセットに保存される）。
    /// 「ショートカットの解放」は層ごとのショートカット（ボス階の井戸と地上のロープ）の解放状態で、Play していなくても切り替えられる
    /// （本物の保存 shortcuts.json に書く。ゲームでは魂の強化で解放する）。
    /// </summary>
    public sealed class FloorDebugWindow : EditorWindow
    {
        private Label status;
        private Button downButton;
        private Button upButton;
        private Button unlockButton;
        private PopupField<DungeonFloor> floorPopup;
        private Button jumpButton;
        private Button surfaceButton;
        private Button afterlifeButton;
        private Toggle allowDownToggle;
        private VisualElement shortcutToggles;
        private readonly List<Toggle> shortcutLayerToggles = new List<Toggle>();

        [MenuItem("Tools/TPS Dungeon/デバッグ/階のデバッグ", false, 0)]
        public static void Open()
        {
            var window = GetWindow<FloorDebugWindow>();
            window.titleContent = new GUIContent("階のデバッグ");
            window.minSize = new Vector2(260f, 200f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            status = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 } };
            root.Add(status);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            downButton = new Button(GoDown) { text = "↓ 下の階へ", style = { flexGrow = 1 } };
            upButton = new Button(GoUp) { text = "↑ 上の階へ", style = { flexGrow = 1 } };
            row.Add(downButton);
            row.Add(upButton);
            root.Add(row);

            unlockButton = new Button(Unlock) { text = "閉ざされた扉を開けられるようにする" };
            root.Add(unlockButton);

            var jumpRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            floorPopup = new PopupField<DungeonFloor>(new List<DungeonFloor> { default }, 0, FloorText, FloorText) { style = { flexGrow = 1 } };
            jumpButton = new Button(Jump) { text = "移る" };
            jumpRow.Add(floorPopup);
            jumpRow.Add(jumpButton);
            root.Add(jumpRow);

            var placeRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            surfaceButton = new Button(GoSurface) { text = "地上へ（城の門の前）", style = { flexGrow = 1 } };
            afterlifeButton = new Button(GoAfterlife)
            {
                text = "死後の場所へ",
                tooltip = "ただ移るだけ（レベルはソウルに変わらない）。全滅と同じに送るのは「魂の燭台のデバッグ」から。",
                style = { flexGrow = 1 },
            };
            placeRow.Add(surfaceButton);
            placeRow.Add(afterlifeButton);
            root.Add(placeRow);

            allowDownToggle = new Toggle("下り階段で降りられる")
            {
                tooltip = "道順（DungeonRoute）の設定。入れると下り階段に「降りる」が出て、1 つ下の階（1 層 1 階なら地上）へ降りられる。アセットに保存されるので、確かめ終えたら外すこと。",
                style = { marginTop = 8 },
            };
            allowDownToggle.RegisterValueChangedCallback(e => SetAllowGoingDown(e.newValue));
            root.Add(allowDownToggle);

            root.Add(new Label("ショートカットの解放") { style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold } });
            shortcutToggles = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            root.Add(shortcutToggles);

            root.schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        private static FloorBootstrap Bootstrap() =>
            EditorApplication.isPlaying ? Object.FindFirstObjectByType<FloorBootstrap>() : null;

        private static DungeonRoute Route()
        {
            var bootstrap = Bootstrap();
            if (bootstrap != null && bootstrap.Route != null) return bootstrap.Route;
            return AssetDatabase.LoadAssetAtPath<DungeonRoute>(FloorSceneBuilder.RoutePath);
        }

        private string FloorText(DungeonFloor floor)
        {
            var route = Route();
            return route != null && route.Contains(floor) ? $"{route.Label(floor)}（{route.SceneOf(floor)}）" : "-";
        }

        private void Refresh()
        {
            if (status == null) return;
            var route = Route();
            var bootstrap = Bootstrap();
            var floor = bootstrap != null ? bootstrap.CurrentFloor : null;
            var builder = bootstrap != null ? bootstrap.GetComponent<FloorBuilder>() : null;
            int locked = 0;
            if (builder != null)
            {
                foreach (var door in builder.LockedDoors)
                {
                    if (door != null && door.IsLocked) locked++;
                }
            }

            string scene = SceneManager.GetActiveScene().name;
            status.text = !EditorApplication.isPlaying ? "移る操作は Play 中に使える。"
                : bootstrap == null ? (scene == SceneTravel.SurfaceScene ? "今いる所: 地上"
                    : scene == SceneTravel.AfterlifeScene ? "今いる所: 死後の場所"
                    : $"今いる所: {scene}（階ではない）")
                : floor.HasValue ? $"今いる階: {route.Label(floor.Value)}（{floor.Value}）\n閉ざされた扉: {locked} 枚"
                : $"道順に載っていない階（{bootstrap.gameObject.scene.name}）。";

            bool onRoute = floor.HasValue && route != null;
            bool canTravel = EditorApplication.isPlaying && !SceneTravel.Traveling;
            downButton.SetEnabled(onRoute);
            upButton.SetEnabled(onRoute && route.Next(floor.Value).HasValue);
            unlockButton.SetEnabled(locked > 0);

            var choices = new List<DungeonFloor>();
            if (route != null)
            {
                for (var f = route.First; f.HasValue; f = route.Next(f.Value)) choices.Add(f.Value);
            }
            if (choices.Count > 0 && !SameChoices(floorPopup.choices, choices))
            {
                floorPopup.choices = choices;
                if (!choices.Contains(floorPopup.value)) floorPopup.SetValueWithoutNotify(choices[0]);
            }
            floorPopup.SetEnabled(route != null && choices.Count > 0);
            jumpButton.SetEnabled(canTravel && choices.Count > 0);
            surfaceButton.SetEnabled(canTravel && scene != SceneTravel.SurfaceScene);
            afterlifeButton.SetEnabled(canTravel && scene != SceneTravel.AfterlifeScene);

            allowDownToggle.SetEnabled(route != null);
            if (route != null) allowDownToggle.SetValueWithoutNotify(route.AllowGoingDown);

            RefreshShortcutToggles(route);
        }

        /// <summary>道順の層ごとに、ショートカットの解放のトグルを並べる（層の数が変わったら並べ直す）。</summary>
        private void RefreshShortcutToggles(DungeonRoute route)
        {
            int count = route != null ? route.Layers.Count : 0;
            if (shortcutLayerToggles.Count != count)
            {
                shortcutToggles.Clear();
                shortcutLayerToggles.Clear();
                for (int i = 0; i < count; i++)
                {
                    int layer = i;
                    var toggle = new Toggle($"{layer + 1} 層")
                    {
                        tooltip = $"第 {layer + 1} 層のショートカット（ボス階の井戸と地上のロープ）を解放する。本物の保存（shortcuts.json）に書く。",
                        style = { marginRight = 8 },
                    };
                    toggle.labelElement.style.minWidth = 0;
                    toggle.RegisterValueChangedCallback(e => SetShortcutUnlocked(layer, e.newValue));
                    shortcutToggles.Add(toggle);
                    shortcutLayerToggles.Add(toggle);
                }
            }
            for (int i = 0; i < shortcutLayerToggles.Count; i++) shortcutLayerToggles[i].SetValueWithoutNotify(ShortcutUnlocks.IsUnlocked(i));
        }

        private static void SetShortcutUnlocked(int layer, bool unlocked)
        {
            ShortcutUnlocks.SetUnlocked(layer, unlocked);
            Debug.Log($"[Dungeon] デバッグ: 第 {layer + 1} 層のショートカットを{(unlocked ? "解放した" : "閉じた")}");
        }

        private static bool SameChoices(List<DungeonFloor> a, List<DungeonFloor> b)
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void GoDown()
        {
            var bootstrap = Bootstrap();
            if (bootstrap == null || !bootstrap.CurrentFloor.HasValue) return;

            var previous = bootstrap.Route.Previous(bootstrap.CurrentFloor.Value);
            if (previous.HasValue)
            {
                Debug.Log($"[Dungeon] デバッグ: {bootstrap.Route.Label(previous.Value)} へ下りる");
                bootstrap.GoToFloor(previous.Value);
            }
            else
            {
                Debug.Log("[Dungeon] デバッグ: 地上へ下りる（城の門の前に立つ）");
                FloorBootstrap.GoDownToSurface();
            }
        }

        private void GoUp()
        {
            var bootstrap = Bootstrap();
            if (bootstrap == null || !bootstrap.CurrentFloor.HasValue) return;

            var next = bootstrap.Route.Next(bootstrap.CurrentFloor.Value);
            if (!next.HasValue) return;
            Debug.Log($"[Dungeon] デバッグ: {bootstrap.Route.Label(next.Value)} へ上がる");
            bootstrap.GoToFloor(next.Value);
        }

        /// <summary>選んだ階へ移る。階の中なら階から（下りか上りかで立つ所が変わる）、地上や死後の場所からは、その階の始まりに立つ。</summary>
        private void Jump()
        {
            if (!EditorApplication.isPlaying || SceneTravel.Traveling) return;
            var route = Route();
            DungeonFloor target = floorPopup.value;
            if (route == null || !route.Contains(target)) return;

            Debug.Log($"[Dungeon] デバッグ: {route.Label(target)} へ移る");
            var bootstrap = Bootstrap();
            if (bootstrap != null && bootstrap.CurrentFloor.HasValue)
            {
                bootstrap.GoToFloor(target);
                return;
            }
            DungeonProgress.Pending = target;
            DungeonProgress.PendingFromAbove = false;
            SceneTravel.Go(route.SceneOf(target));
        }

        private void GoSurface()
        {
            if (!EditorApplication.isPlaying || SceneTravel.Traveling) return;
            Debug.Log("[Dungeon] デバッグ: 地上へ移る（城の門の前に立つ）");
            FloorBootstrap.GoDownToSurface();
        }

        private void GoAfterlife()
        {
            if (!EditorApplication.isPlaying || SceneTravel.Traveling) return;
            Debug.Log("[Dungeon] デバッグ: 死後の場所へ移る");
            SceneTravel.Go(SceneTravel.AfterlifeScene);
        }

        private void Unlock()
        {
            var bootstrap = Bootstrap();
            var builder = bootstrap != null ? bootstrap.GetComponent<FloorBuilder>() : null;
            if (builder == null) return;
            builder.UnlockDoors();
            Debug.Log($"[Dungeon] デバッグ: 閉ざされた扉を開けられるようにした（{builder.LockedDoors.Count} 枚）");
        }

        private void SetAllowGoingDown(bool allow)
        {
            var route = Route();
            if (route == null || route.AllowGoingDown == allow) return;

            Undo.RecordObject(route, "下り階段で降りられる");
            route.AllowGoingDown = allow;
            EditorUtility.SetDirty(route);
            AssetDatabase.SaveAssetIfDirty(route);

            // Play 中なら今の階の階段にもすぐ効かせる。
            var bootstrap = Bootstrap();
            if (bootstrap != null) bootstrap.ReconnectStairs();
        }
    }
}
