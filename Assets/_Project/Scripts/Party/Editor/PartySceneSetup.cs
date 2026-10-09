using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Items;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player.Editor;
using TpsDungeon.Progression;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PartyGroup = TpsDungeon.Party.Party;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 確認用シーン（Player_Debug）にパーティーを仮配置する。何度実行しても同じ結果になる（仲間は置き直す）。
    /// - パーティーの操作台（Party.prefab）を置き、シーンの主人公（Character Variant）を先頭にする。
    /// - 仲間を n 人、先頭の後ろに置く。名前・見た目（角・耳・しっぽ）・最初の装備は仲間ごとに変える。
    /// - フロアに FloorNavMesh を付け、生成の後に NavMesh を焼くようにする。
    /// 先に「Tools/TPS Dungeon/Party/パーティーを組み込む」でプレハブを分けておくこと。
    /// </summary>
    public static class PartySceneSetup
    {
        private const string MembersRootName = "[Party Members]";
        private const string WeaponFolder = "Assets/_Project/Items/Weapons/";

        private static readonly string[] Names = { "リオ", "ミナ", "カイ", "セナ", "ユウ", "ハル", "ノア", "レン", "ソラ", "アオイ", "ルカ", "ナギ" };

        // 仲間ごとの最初の装備（武器のアセット名）。近接・遠距離・お守り・盾を混ぜる。
        private static readonly string[][] Kits =
        {
            new[] { "Weapon_01_2C_IronSword", "Weapon_09_2C_HunterBow", "Weapon_27_0E_WoodenShield" },
            new[] { "Weapon_04_3B_SteelGreatsword", "Weapon_28_1D_ThrowingKnife" },
            new[] { "Weapon_09_4A_SoldierLongbow", "Weapon_19_0E_ChargedStaff", "Weapon_17_3B_WorldTreeBranchStaff" },
            new[] { "Weapon_05_1D_StoneHammer", "Weapon_21_1D_EmberStaff", "Weapon_26_0E_RustyBell" },
            new[] { "Weapon_02_2C_GaleRapier", "Weapon_11_2C_ArrowRainBow" },
            new[] { "Weapon_20_1D_StoneSpikeStaff", "Weapon_01_0E_RustySword" },
            new[] { "Weapon_30_2C_WoodenDollWhistle", "Weapon_05_3B_IronMace" },
            new[] { "Weapon_09_0E_OldBow" },
            new[] { "Weapon_04_1D_ChippedGreatsword", "Weapon_27_1D_IronRoundShield" },
            new[] { "Weapon_19_3B_ThunderStaff", "Weapon_28_0E_ThrowingStone" },
            new[] { "Weapon_01_4A_BlueSteelRapier", "Weapon_09_2C_HunterBow", "Weapon_26_1D_OldTalisman" },
        };

        [MenuItem("Tools/TPS Dungeon/Party/確認シーンにパーティーを置く（仲間 4 人）")]
        public static void PlaceFour() => Debug.Log(Place(4));

        [MenuItem("Tools/TPS Dungeon/Party/確認シーンにパーティーを置く（仲間 11 人・12 人パーティー）")]
        public static void PlaceEleven() => Debug.Log(Place(PartyGroup.MaxMembers - 1));

        /// <summary>確認用シーンに、先頭＋followers 人のパーティーを置いて保存する。何をしたかを返す。</summary>
        public static string Place(int followers)
        {
            followers = Mathf.Clamp(followers, 0, PartyGroup.MaxMembers - 1);

            var partyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.PartyPrefabPath);
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.CharacterPrefabPath);
            if (partyPrefab == null || characterPrefab == null)
                return "先に Tools/TPS Dungeon/Party/パーティーを組み込む を実行すること。";

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != PlayerTestSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "シーンを開き直せなかった。";
                scene = EditorSceneManager.OpenScene(PlayerTestSceneBuilder.ScenePath, OpenSceneMode.Single);
            }

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject leader = roots.FirstOrDefault(r => PrefabUtility.GetCorrespondingObjectFromSource(r) == characterPrefab
                                                          || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == PartySetup.CharacterPrefabPath);
            if (leader == null) return $"{scene.path} に主人公（{PartySetup.CharacterPrefabPath}）が無い。";

            GameObject partyObject = roots.FirstOrDefault(r => r.GetComponent<PartyGroup>() != null);
            if (partyObject == null)
            {
                partyObject = (GameObject)PrefabUtility.InstantiatePrefab(partyPrefab, scene);
                partyObject.name = "Party";
            }

            GameObject membersRoot = roots.FirstOrDefault(r => r.name == MembersRootName);
            if (membersRoot != null) Object.DestroyImmediate(membersRoot);
            membersRoot = new GameObject(MembersRootName);
            SceneManager.MoveGameObjectToScene(membersRoot, scene);

            var members = new List<PartyMember> { Configure(leader, 0) };
            Transform lead = leader.transform;
            for (int i = 1; i <= followers; i++)
            {
                var follower = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, scene);
                follower.transform.SetParent(membersRoot.transform, true);
                follower.transform.SetPositionAndRotation(lead.position - lead.forward * (1.6f * i), lead.rotation);
                members.Add(Configure(follower, i));
            }

            var party = partyObject.GetComponent<PartyGroup>();
            var serializedParty = new SerializedObject(party);
            SerializedProperty list = serializedParty.FindProperty("members");
            list.arraySize = members.Count;
            for (int i = 0; i < members.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
            CinemachineCamera follow = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == scene);
            serializedParty.FindProperty("followCamera").objectReferenceValue = follow;
            serializedParty.ApplyModifiedPropertiesWithoutUndo();

            FloorBuilder floor = Object.FindObjectsByType<FloorBuilder>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.scene == scene);
            if (floor != null && floor.GetComponent<FloorNavMesh>() == null) floor.gameObject.AddComponent<FloorNavMesh>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"{scene.path} に {members.Count} 人のパーティーを置いた（先頭 {leader.name}、仲間 {followers} 人）。";
        }

        /// <summary>index 番目のメンバーの名前・見た目・保存の ID・最初の装備を入れる。</summary>
        private static PartyMember Configure(GameObject character, int index)
        {
            character.name = index == 0 ? character.name : $"Character ({Names[index]})";

            var member = character.GetComponent<PartyMember>();
            var serializedMember = new SerializedObject(member);
            serializedMember.FindProperty("displayName").stringValue = Names[index % Names.Length];
            SerializedProperty items = serializedMember.FindProperty("startingItems");
            string[] kit = index > 0 ? Kits[(index - 1) % Kits.Length] : new string[0];
            items.arraySize = kit.Length;
            for (int i = 0; i < kit.Length; i++)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WeaponFolder + kit[i] + ".asset");
                if (weapon == null) Debug.LogWarning($"武器が見つからない: {kit[i]}");
                items.GetArrayElementAtIndex(i).objectReferenceValue = weapon;
            }

            serializedMember.ApplyModifiedPropertiesWithoutUndo();

            var appearance = character.GetComponent<CharacterAppearance>();
            if (appearance != null)
            {
                var serializedLook = new SerializedObject(appearance);
                CharacterLook look = CharacterLook.ForIndex(index);
                serializedLook.FindProperty("look.horn").intValue = look.horn;
                serializedLook.FindProperty("look.ear").intValue = look.ear;
                serializedLook.FindProperty("look.tail").intValue = look.tail;
                serializedLook.ApplyModifiedPropertiesWithoutUndo();
            }

            var progression = character.GetComponent<CharacterProgression>();
            if (progression != null)
            {
                var serializedProgression = new SerializedObject(progression);
                serializedProgression.FindProperty("progressId").stringValue = index == 0 ? CharacterProgression.PlayerId : $"member_{index}";
                serializedProgression.ApplyModifiedPropertiesWithoutUndo();
            }

            return member;
        }
    }
}
