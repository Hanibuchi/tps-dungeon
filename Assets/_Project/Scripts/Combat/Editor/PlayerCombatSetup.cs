using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに近接攻撃（MeleeAttacker）を組み込む。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class PlayerCombatSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";

        [MenuItem("Tools/TPS Dungeon/Player/近接攻撃を組み込む")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var attacker = root.GetComponent<MeleeAttacker>();
                if (attacker == null) attacker = root.AddComponent<MeleeAttacker>();

                var serialized = new SerializedObject(attacker);
                serialized.FindProperty("playerInput").objectReferenceValue = root.GetComponent<PlayerInput>();
                serialized.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"近接攻撃を組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
