using TpsDungeon.Items;
using TpsDungeon.Items.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに攻撃（近接の実行役 MeleeAttacker・遠距離の実行役 RangedAttacker と、入力を渡す PlayerMeleeInput）を組み込む。素手の武器とエフェクトもここで入れる
    /// （先に「プレースホルダの武器を生成」で素手の武器を作っておくこと）。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class PlayerCombatSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";

        [MenuItem("Tools/TPS Dungeon/Player/攻撃（近接・遠距離）を組み込む")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var attacker = root.GetComponent<MeleeAttacker>();
                if (attacker == null) attacker = root.AddComponent<MeleeAttacker>();

                var ranged = root.GetComponent<RangedAttacker>();
                if (ranged == null) ranged = root.AddComponent<RangedAttacker>();
                // 左手の弓を構えている間、カメラを左肩へ寄せて引き絞りを見せる。
                if (root.GetComponent<RangedCameraShoulder>() == null) root.AddComponent<RangedCameraShoulder>();

                var input = root.GetComponent<PlayerMeleeInput>();
                if (input == null) input = root.AddComponent<PlayerMeleeInput>();
                var serializedInput = new SerializedObject(input);
                serializedInput.FindProperty("playerInput").objectReferenceValue = root.GetComponent<PlayerInput>();
                serializedInput.ApplyModifiedPropertiesWithoutUndo();

                var serialized = new SerializedObject(attacker);
                serialized.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();

                var fists = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PlaceholderWeaponAssetGenerator.FistsPath);
                if (fists == null) Debug.LogWarning($"素手の武器が無い: {PlaceholderWeaponAssetGenerator.FistsPath}（プレースホルダの武器を生成すると作られる）");
                serialized.FindProperty("unarmedWeapon").objectReferenceValue = fists;
                serialized.FindProperty("criticalHitEffect").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadEffect(PlaceholderWeaponAssetGenerator.CriticalHitEffectPath);
                serialized.FindProperty("criticalHitEffectScale").floatValue = 0.4f;
                serialized.FindProperty("criticalHitSound").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadSound(PlaceholderWeaponAssetGenerator.CriticalHitSoundPath);
                serialized.FindProperty("criticalHitSoundVolume").floatValue = 0.7f;
                // 素材は閃光が 17 m ほどに広がる。爆発のエンチャントの半径は 2.5 m。
                serialized.FindProperty("explosionEffect").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadEffect(PlaceholderWeaponAssetGenerator.ExplosionEffectPath);
                serialized.FindProperty("explosionEffectScale").floatValue = 0.5f;
                serialized.FindProperty("explosionSound").objectReferenceValue =
                    PlaceholderWeaponAssetGenerator.LoadSound(PlaceholderWeaponAssetGenerator.ExplosionSoundPath);
                serialized.FindProperty("explosionSoundVolume").floatValue = 0.8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                WriteSharedEffects(serialized, ranged);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"攻撃（近接・遠距離）を組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>クリティカルと爆発の見た目と音は、遠距離（RangedAttacker）も近接と同じ物を使う。</summary>
        private static void WriteSharedEffects(SerializedObject melee, RangedAttacker ranged)
        {
            var serialized = new SerializedObject(ranged);
            serialized.FindProperty("animator").objectReferenceValue = melee.FindProperty("animator").objectReferenceValue;
            serialized.FindProperty("switchCooldown").floatValue = melee.FindProperty("switchCooldown").floatValue;
            foreach (string name in new[] { "criticalHitEffect", "criticalHitSound", "explosionEffect", "explosionSound" })
                serialized.FindProperty(name).objectReferenceValue = melee.FindProperty(name).objectReferenceValue;
            foreach (string name in new[] { "criticalHitEffectScale", "criticalHitSoundVolume", "explosionEffectScale", "explosionSoundVolume" })
                serialized.FindProperty(name).floatValue = melee.FindProperty(name).floatValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
