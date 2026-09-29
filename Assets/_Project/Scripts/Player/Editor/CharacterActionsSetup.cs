using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Player.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに全身の動きの窓口（CharacterActions）を組み込む。
    /// Animator 側の Action 層は CharacterAnimatorBuilder が作る。何度実行しても同じ結果になる。
    /// </summary>
    public static class CharacterActionsSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";

        [MenuItem("Tools/TPS Dungeon/Player/全身の動きを組み込む")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var actions = root.GetComponent<CharacterActions>();
                if (actions == null) actions = root.AddComponent<CharacterActions>();

                var serialized = new SerializedObject(actions);
                serialized.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"全身の動きを組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
