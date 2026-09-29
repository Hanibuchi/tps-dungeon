using System.Collections.Generic;
using TpsDungeon.Items;
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

                Write(grip.Type,
                    moved ? Round(t.localPosition, PositionStep) : grip.AppliedPosition,
                    turned ? Round(Signed(t.localEulerAngles), EulerStep) : grip.AppliedEuler);
                grip.Apply();
            }
        }

        /// <summary>武器種の持ち位置を書き換える（Undo で戻せる）。</summary>
        public static void Write(WeaponTypeDefinition type, Vector3 position, Vector3 euler)
        {
            var serialized = new SerializedObject(type);
            serialized.FindProperty("heldLocalPosition").vector3Value = position;
            serialized.FindProperty("heldLocalEuler").vector3Value = euler;
            if (serialized.ApplyModifiedProperties()) Touched.Add(type);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode) return;

            foreach (WeaponTypeDefinition type in Touched)
            {
                if (type == null) continue;
                AssetDatabase.SaveAssetIfDirty(type);
                Debug.Log($"手に持つ位置を保存した: {type.name} 位置 {type.HeldLocalPosition:F4} 向き {type.HeldLocalEuler:F1}", type);
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
            EditorGUILayout.PropertyField(typeObject.FindProperty("heldLocalPosition"), new GUIContent("位置"));
            EditorGUILayout.PropertyField(typeObject.FindProperty("heldLocalEuler"), new GUIContent("向き"));
            typeObject.ApplyModifiedProperties();

            if (GUILayout.Button("原点に戻す")) HeldWeaponGripSync.Write(type, Vector3.zero, Vector3.zero);

            EditorGUILayout.HelpBox(
                "Scene ビューで動かす・回すか、ここの数値を変えると、武器種に書き戻る（Play を止めても残る）。" +
                "軸は右手の骨のローカル。振りの途中は一時停止とコマ送りで確かめる。", MessageType.None);
        }
    }

    public static class HeldWeaponGripMenu
    {
        private const string MenuPath = "Tools/TPS Dungeon/Player/手の武器を選ぶ";

        /// <summary>手の武器を選び、Scene ビューで寄って、手の骨のローカル軸で動かせるようにする。</summary>
        [MenuItem(MenuPath)]
        public static void SelectHeldWeapon()
        {
            var grip = Object.FindAnyObjectByType<HeldWeaponGrip>();
            if (grip == null)
            {
                Debug.LogWarning("手に武器を持っていない（見た目のある武器をホットバーで持つ）");
                return;
            }

            Selection.activeGameObject = grip.gameObject;
            Tools.current = Tool.Transform;
            Tools.pivotRotation = PivotRotation.Local;
            Tools.pivotMode = PivotMode.Pivot;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanSelectHeldWeapon() => EditorApplication.isPlaying;
    }
}
