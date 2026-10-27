using System.Collections.Generic;
using TpsDungeon.Items;
using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Combat.Editor
{
    /// <summary>
    /// 手に持つ武器の位置合わせを Play 中にその場でできるようにする。
    /// Scene ビューで手の武器（HeldWeaponGrip）を動かすと、その位置と向きを武器種（WeaponTypeDefinition）に書き戻す。
    /// 武器種はアセットなので Play を止めても残り、止めるときに保存する。
    /// 逆向き（武器種の数値を変えたら手の武器が動く）は HeldWeaponGrip 自身がやる。
    /// </summary>
    [InitializeOnLoad]
    public static class HeldWeaponGripSync
    {
        private const float PositionStep = 0.0001f;
        private const float EulerStep = 0.1f;
        private const float MovedAngle = 0.01f;

        private static readonly HashSet<WeaponTypeDefinition> Touched = new HashSet<WeaponTypeDefinition>();

        static HeldWeaponGripSync()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying) return;

            foreach (HeldWeaponGrip grip in Object.FindObjectsByType<HeldWeaponGrip>(FindObjectsSortMode.None))
            {
                if (grip.Type == null) continue;

                Transform t = grip.transform;
                bool moved = t.localPosition != grip.AppliedPosition;
                bool turned = Quaternion.Angle(t.localRotation, Quaternion.Euler(grip.AppliedEuler)) > MovedAngle;
                if (!moved && !turned) continue;

                Write(grip.Type, grip.OtherHand,
                    moved ? Round(t.localPosition, PositionStep) : grip.AppliedPosition,
                    turned ? Round(Signed(t.localEulerAngles), EulerStep) : grip.AppliedEuler);
                grip.Apply();
            }
        }

        /// <summary>武器種の持ち位置を書き換える（Undo で戻せる）。otherHand ならいつもと逆の手の値（otherHand〜）を書く。</summary>
        public static void Write(WeaponTypeDefinition type, bool otherHand, Vector3 position, Vector3 euler)
        {
            var serialized = new SerializedObject(type);
            serialized.FindProperty(PositionProperty(otherHand)).vector3Value = position;
            serialized.FindProperty(EulerProperty(otherHand)).vector3Value = euler;
            if (serialized.ApplyModifiedProperties()) Touched.Add(type);
        }

        public static string PositionProperty(bool otherHand) => otherHand ? "otherHandLocalPosition" : "heldLocalPosition";

        public static string EulerProperty(bool otherHand) => otherHand ? "otherHandLocalEuler" : "heldLocalEuler";

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode) return;

            foreach (WeaponTypeDefinition type in Touched)
            {
                if (type == null) continue;
                AssetDatabase.SaveAssetIfDirty(type);
                Debug.Log($"手に持つ位置を保存した: {type.name} 位置 {type.HeldLocalPosition:F4} 向き {type.HeldLocalEuler:F1}"
                    + $"　逆の手 位置 {type.OtherHandLocalPosition:F4} 向き {type.OtherHandLocalEuler:F1}", type);
            }
            Touched.Clear();
        }

        /// <summary>0〜360 のオイラー角を -180〜180 にする（インスペクタで読みやすいように）。</summary>
        private static Vector3 Signed(Vector3 euler) =>
            new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));

        private static Vector3 Round(Vector3 v, float step) =>
            new Vector3(Mathf.Round(v.x / step) * step, Mathf.Round(v.y / step) * step, Mathf.Round(v.z / step) * step);
    }

    /// <summary>手の武器を選ぶと、書き戻し先の武器種の持ち位置をその場で編集できる。</summary>
    [CustomEditor(typeof(HeldWeaponGrip))]
    public sealed class HeldWeaponGripEditor : UnityEditor.Editor
    {
        private SerializedObject typeObject;

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            var grip = (HeldWeaponGrip)target;
            WeaponTypeDefinition type = grip.Type;
            if (type == null)
            {
                EditorGUILayout.HelpBox("武器種が無いので原点に置いている。", MessageType.Info);
                return;
            }

            if (typeObject == null || typeObject.targetObject != type) typeObject = new SerializedObject(type);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("書き戻す武器種", type, typeof(WeaponTypeDefinition), false);

            typeObject.Update();
            string hand = (grip.OffHand ? "オフハンド・" : "メインハンド・") + (grip.LeftHand ? "左手" : "右手");
            EditorGUILayout.PropertyField(typeObject.FindProperty(HeldWeaponGripSync.PositionProperty(grip.OtherHand)), new GUIContent($"位置（{hand}）"));
            EditorGUILayout.PropertyField(typeObject.FindProperty(HeldWeaponGripSync.EulerProperty(grip.OtherHand)), new GUIContent($"向き（{hand}）"));
            typeObject.ApplyModifiedProperties();

            if (GUILayout.Button("原点に戻す")) HeldWeaponGripSync.Write(type, grip.OtherHand, Vector3.zero, Vector3.zero);

            EditorGUILayout.HelpBox(
                "Scene ビューで動かす・回すか、ここの数値を変えると、武器種に書き戻る（Play を止めても残る）。" +
                "軸は持っている手の骨のローカル。振りの途中は一時停止とコマ送りで確かめる。", MessageType.None);
        }
    }

    /// <summary>
    /// 操作しているキャラの手の物を選び、Scene ビューで寄って、手の骨のローカル軸で動かせるようにする（Play 中だけ）。
    /// メインハンド（選んでいる物）とオフハンド（左手の盾）は持ち位置を別々に持つので、メニューも分ける。
    /// </summary>
    public static class HeldWeaponGripMenu
    {
        private const string MenuRoot = "Tools/TPS Dungeon/デバッグ/持ち位置の調整/";
        private const string MainHandPath = MenuRoot + "メインハンドの持ち物を選ぶ";
        private const string OffHandPath = MenuRoot + "オフハンドの盾を選ぶ";

        [MenuItem(MainHandPath, false, 4)]
        public static void SelectMainHand() => Select(false);

        [MenuItem(OffHandPath, false, 5)]
        public static void SelectOffHand() => Select(true);

        [MenuItem(MainHandPath, true, 4)]
        [MenuItem(OffHandPath, true, 5)]
        private static bool CanSelect() => EditorApplication.isPlaying;

        private static void Select(bool offHand)
        {
            HeldWeaponGrip grip = Find(offHand);
            if (grip == null)
            {
                Debug.LogWarning(offHand
                    ? "操作しているキャラがオフハンドに盾を持っていない（盾を持てる物を選び、ほかの盾をホットバーに入れる）"
                    : "操作しているキャラがメインハンドに何も持っていない（見た目のある物をホットバーで選ぶ）");
                return;
            }

            Selection.activeGameObject = grip.gameObject;
            Tools.current = Tool.Transform;
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        // 仲間も物を持っているので、操作しているキャラ（パーティーの先頭）の手から探す。パーティーが居ないシーンではシーン全体から。
        private static HeldWeaponGrip Find(bool offHand)
        {
            GameObject leader = PartyRoster.Leader;
            HeldWeaponGrip[] grips = leader != null
                ? leader.GetComponentsInChildren<HeldWeaponGrip>()
                : Object.FindObjectsByType<HeldWeaponGrip>(FindObjectsSortMode.None);
            foreach (HeldWeaponGrip grip in grips)
            {
                if (grip.OffHand == offHand) return grip;
            }

            return null;
        }
    }
}
