using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Progression.Editor
{
    /// <summary>
    /// 成長パラメータのアセットを用意し、キャラのプレハブにレベルの仕組み（CharacterProgression・レベルアップの効果音）を、
    /// パーティーの操作台のプレハブ（あれば）にパーティーの経験値（PartyProgression）とデバッグ入力を組み込む。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す。アセットの値は上書きしない）。
    /// </summary>
    public static class ProgressionSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string PartyPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
        private const string ProfileFolder = "Assets/_Project/Progression";
        public const string PlayerProfilePath = ProfileFolder + "/PlayerGrowth.asset";
        public const string CompanionTemplatePath = ProfileFolder + "/CompanionGrowth_Template.asset";

        // 仮の効果音（効果音ラボ「ひらめく2」）。既に別の音が入っていれば上書きしない。
        private const string LevelUpClipPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/Character/ひらめく2.mp3";

        [MenuItem("Tools/TPS Dungeon/Progression/主人公に組み込む")]
        public static void Setup()
        {
            var playerProfile = EnsureProfile(PlayerProfilePath);
            EnsureProfile(CompanionTemplatePath);

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<PlayerHealth>() == null) root.AddComponent<PlayerHealth>();

                var progression = root.GetComponent<CharacterProgression>();
                if (progression == null) progression = root.AddComponent<CharacterProgression>();
                var serialized = new SerializedObject(progression);
                serialized.FindProperty("profile").objectReferenceValue = playerProfile;
                serialized.FindProperty("progressId").stringValue = CharacterProgression.PlayerId;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var sound = root.GetComponent<LevelUpSound>();
                if (sound == null) sound = root.AddComponent<LevelUpSound>();
                var serializedSound = new SerializedObject(sound);
                var clipProperty = serializedSound.FindProperty("clip");
                if (clipProperty.objectReferenceValue == null)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(LevelUpClipPath);
                    if (clip == null) Debug.LogWarning($"レベルアップの効果音が見つからないので空のままにした: {LevelUpClipPath}");
                    clipProperty.objectReferenceValue = clip;
                    serializedSound.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"レベルの仕組みを組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath) != null)
            {
                var party = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
                try
                {
                    if (party.GetComponent<PartyProgression>() == null) party.AddComponent<PartyProgression>();
                    if (party.GetComponent<ProgressionDebugInput>() == null) party.AddComponent<ProgressionDebugInput>();
                    PrefabUtility.SaveAsPrefabAsset(party, PartyPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(party);
                }
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/TPS Dungeon/Progression/成長表をログに出す")]
        public static void LogTable()
        {
            var profile = Selection.activeObject as CharacterGrowthProfile;
            if (profile == null) profile = AssetDatabase.LoadAssetAtPath<CharacterGrowthProfile>(PlayerProfilePath);
            var curve = profile != null ? profile.Curve : GrowthCurve.Default;
            string source = profile != null ? AssetDatabase.GetAssetPath(profile) : "GrowthCurve.Default";
            Debug.Log($"成長表（{source}）\n{GrowthTable.Format(curve)}");
        }

        private static CharacterGrowthProfile EnsureProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterGrowthProfile>(path);
            if (profile != null) return profile;

            if (!AssetDatabase.IsValidFolder(ProfileFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Progression");
            profile = ScriptableObject.CreateInstance<CharacterGrowthProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
    }
}
