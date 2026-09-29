using TpsDungeon.Items;
using TpsDungeon.Items.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに近接攻撃（MeleeAttacker）を組み込む。素手の武器とエフェクトもここで入れる
    /// （先に「プレースホルダの武器を生成」で素手の武器を作っておくこと）。
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

                var fists = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PlaceholderWeaponAssetGenerator.FistsPath);
                if (fists == null) Debug.LogWarning($"素手の武器が無い: {PlaceholderWeaponAssetGenerator.FistsPath}（プレースホルダの武器を生成すると作られる）");
                serialized.FindProperty("unarmedWeapon").objectReferenceValue = fists;
                serialized.FindProperty("criticalHitEffect").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadEffect(PlaceholderWeaponAssetGenerator.CriticalHitEffectPath);
                serialized.FindProperty("criticalHitEffectScale").floatValue = 0.4f;
                // 素材は閃光が 17 m ほどに広がる。爆発のエンチャントの半径は 2.5 m。
                serialized.FindProperty("explosionEffect").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadEffect(PlaceholderWeaponAssetGenerator.ExplosionEffectPath);
                serialized.FindProperty("explosionEffectScale").floatValue = 0.5f;
                serialized.FindProperty("explosionSound").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadSound(PlaceholderWeaponAssetGenerator.ExplosionSoundPath);
                serialized.FindProperty("explosionSoundVolume").floatValue = 0.8f;
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
