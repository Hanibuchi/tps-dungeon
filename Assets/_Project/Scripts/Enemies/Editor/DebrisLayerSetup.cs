using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>死体・落とし物のレイヤー（<see cref="DebrisLayer"/>）を用意し、床や壁（Default）とだけ当たるようにする。</summary>
    public static class DebrisLayerSetup
    {
        public const int LayerIndex = 10;

        [MenuItem("Tools/TPS Dungeon/敵/死体・落とし物のレイヤーを用意する", false, 406)]
        public static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(LayerIndex);
            if (slot.stringValue != DebrisLayer.Name)
            {
                if (!string.IsNullOrEmpty(slot.stringValue))
                    throw new System.InvalidOperationException($"レイヤー {LayerIndex} は既に「{slot.stringValue}」に使われている。");
                slot.stringValue = DebrisLayer.Name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }

            // Default（床・壁）と Ignore Raycast（見えない仕切りの壁）とだけ当たる。パーティー・死体どうし・落とし物どうし・壊せる草花とは当たらない。
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(LayerIndex, i, i != 0 && i != 2);
            AssetDatabase.SaveAssets();
            Debug.Log($"レイヤー {LayerIndex}「{DebrisLayer.Name}」を用意した（Default と Ignore Raycast とだけ当たる）");
        }
    }
}
