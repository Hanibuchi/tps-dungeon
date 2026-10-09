using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// キャラの見た目（<see cref="CharacterAppearance"/>）のインスペクタに、角・耳・しっぽの置き方を合わせる道具を足す。
    /// 「合わせる」を押している間、シーンビューに選んだ小物（角・耳は左）のハンドルを出し、動かすとカタログの値を書き換える。
    /// 値はカタログのアセットに入るので全員に効き、右の角・耳は左右反転で付く。Undo も効く。
    /// ハンドルは W で位置、E で向き、R で大きさ（ツールの切り替えに合わせる）。
    /// Play 中に動かしても、カタログのアセットなので Play を抜けても値は残る。
    /// </summary>
    [CustomEditor(typeof(CharacterAppearance))]
    public sealed class CharacterAppearanceEditor : UnityEditor.Editor
    {
        private static readonly string[] PartLabels = { "角（左）", "耳（左）", "しっぽ" };
        private static readonly string[] PartFields = { "hornPlacement", "earPlacement", "tailPlacement" };

        // 選び直しても続けて合わせられるよう、エディタを開いている間は覚えておく。
        private static int part;
        private static bool adjusting;

        private void OnDisable()
        {
            Tools.hidden = false;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var appearance = (CharacterAppearance)target;
            CharacterAccessoryCatalog catalog = appearance.Catalog;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("小物の置き方を合わせる", EditorStyles.boldLabel);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("カタログが無いので合わせられない。", MessageType.Info);
                return;
            }

            part = GUILayout.Toolbar(part, PartLabels);
            bool next = GUILayout.Toggle(adjusting, adjusting ? "シーンビューで合わせている（押すとやめる）" : "シーンビューで合わせる", "Button");
            if (next != adjusting)
            {
                adjusting = next;
                SceneView.RepaintAll();
            }

            Tools.hidden = adjusting;

            var serializedCatalog = new SerializedObject(catalog);
            serializedCatalog.Update();
            EditorGUILayout.PropertyField(serializedCatalog.FindProperty(PartFields[part]), new GUIContent(PartLabels[part] + "の置き方"), true);
            serializedCatalog.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(
                "値はカタログ（" + AssetDatabase.GetAssetPath(catalog) + "）に入り、全員に効く。角・耳は左を合わせれば右は左右反転で付く。\n" +
                "シーンビューのハンドル: W で位置、E で向き、R で大きさ。Ctrl/Cmd+Z で戻せる。\n" +
                "このキャラに付いていない小物は、上の Look で色の番号（1 以上）を入れると見える。",
                MessageType.None);
        }

        private void OnSceneGUI()
        {
            if (!adjusting) return;

            var appearance = (CharacterAppearance)target;
            CharacterAccessoryCatalog catalog = appearance.Catalog;
            if (catalog == null) return;

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty property = serializedCatalog.FindProperty(PartFields[part]);
            CharacterAccessoryCatalog.Placement placement = Read(property);
            if (!appearance.TryGetWorldPose(placement, out Vector3 position, out Quaternion rotation)) return;

            float size = HandleUtility.GetHandleSize(position);
            Handles.Label(position + Vector3.up * size * 0.3f, PartLabels[part]);

            EditorGUI.BeginChangeCheck();
            CharacterAccessoryCatalog.Placement changed = placement;
            switch (Tools.current)
            {
                case Tool.Rotate:
                    Quaternion newRotation = Handles.RotationHandle(rotation, position);
                    if (EditorGUI.EndChangeCheck()) changed = appearance.ToPlacement(placement, position, newRotation);
                    else return;
                    break;
                case Tool.Scale:
                    float scale = placement.scale <= 0f ? 1f : placement.scale;
                    float newScale = Handles.ScaleSlider(scale, position, rotation * Vector3.up, rotation, size, 0f);
                    if (EditorGUI.EndChangeCheck()) changed.scale = Mathf.Max(0.01f, newScale);
                    else return;
                    break;
                default:
                    Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity;
                    Vector3 newPosition = Handles.PositionHandle(position, handleRotation);
                    if (EditorGUI.EndChangeCheck()) changed = appearance.ToPlacement(placement, newPosition, rotation);
                    else return;
                    break;
            }

            // SerializedObject で書くと Undo に入り、カタログの OnValidate で全員が付け直す。
            Write(property, changed);
            serializedCatalog.ApplyModifiedProperties();
        }

        private static CharacterAccessoryCatalog.Placement Read(SerializedProperty property)
        {
            return new CharacterAccessoryCatalog.Placement(
                (HumanBodyBones)property.FindPropertyRelative("bone").intValue,
                property.FindPropertyRelative("position").vector3Value,
                property.FindPropertyRelative("euler").vector3Value,
                property.FindPropertyRelative("scale").floatValue);
        }

        private static void Write(SerializedProperty property, CharacterAccessoryCatalog.Placement placement)
        {
            // 丸めると、ゆっくり動かしたときに 1 コマぶんの動きが消えて止まってしまうので、そのまま書く。
            property.FindPropertyRelative("position").vector3Value = placement.position;
            property.FindPropertyRelative("euler").vector3Value = placement.euler;
            property.FindPropertyRelative("scale").floatValue = placement.scale;
        }
    }
}
