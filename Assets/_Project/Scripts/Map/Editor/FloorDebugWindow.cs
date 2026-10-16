using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 階を確かめるためのウィンドウ（Tools/TPS Dungeon/階のデバッグ）。Play 中の階で使う。
    /// 1 つ下・上の階へ移る、道順の好きな階へ移る、ボス部屋の閉ざされた扉を開けられるようにする。
    /// 「下り階段で降りられる」は道順（DungeonRoute）の設定そのもので、Play していなくても切り替えられる（アセットに保存される）。
    /// </summary>
    public sealed class FloorDebugWindow : EditorWindow
    {
        private Label status;
        private Button downButton;
        private Button upButton;
        private Button unlockButton;
        private PopupField<DungeonFloor> floorPopup;
        private Button jumpButton;
        private Toggle allowDownToggle;

        [MenuItem("Tools/TPS Dungeon/階のデバッグ")]
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

            allowDownToggle = new Toggle("下り階段で降りられる")
            {
                tooltip = "道順（DungeonRoute）の設定。入れると下り階段に「降りる」が出て、1 つ下の階（1 層 1 階なら地上）へ降りられる。アセットに保存されるので、確かめ終えたら外すこと。",
                style = { marginTop = 8 },
            };
            allowDownToggle.RegisterValueChangedCallback(e => SetAllowGoingDown(e.newValue));
            root.Add(allowDownToggle);

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

            status.text = !EditorApplication.isPlaying ? "Play 中の階で使える。"
                : bootstrap == null ? "このシーンに階（FloorBootstrap）が無い。"
                : floor.HasValue ? $"今いる階: {route.Label(floor.Value)}（{floor.Value}）\n閉ざされた扉: {locked} 枚"
                : $"道順に載っていない階（{bootstrap.gameObject.scene.name}）。";

            bool onRoute = floor.HasValue && route != null;
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
            floorPopup.SetEnabled(onRoute);
            jumpButton.SetEnabled(onRoute && choices.Count > 0);

            allowDownToggle.SetEnabled(route != null);
            if (route != null) allowDownToggle.SetValueWithoutNotify(route.AllowGoingDown);
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
                Debug.Log("[Dungeon] デバッグ: 地上へ下りる");
                SceneTravel.Go(SceneTravel.SurfaceScene);
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

        private void Jump()
        {
            var bootstrap = Bootstrap();
            if (bootstrap == null || !bootstrap.CurrentFloor.HasValue) return;
            Debug.Log($"[Dungeon] デバッグ: {bootstrap.Route.Label(floorPopup.value)} へ移る");
            bootstrap.GoToFloor(floorPopup.value);
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
