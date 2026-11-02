using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>敵の体のレイヤー（<see cref="EnemyLayer"/>）を用意し、物理では Default と同じものに当たるようにする。</summary>
    public static class EnemyLayerSetup
    {
        public const int LayerIndex = 11;

        [MenuItem("Tools/TPS Dungeon/敵/敵のレイヤーを用意する", false, 407)]
        public static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(LayerIndex);
            if (slot.stringValue != EnemyLayer.Name)
            {
                if (!string.IsNullOrEmpty(slot.stringValue))
                    throw new System.InvalidOperationException($"レイヤー {LayerIndex} は既に「{slot.stringValue}」に使われている。");
                slot.stringValue = EnemyLayer.Name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }

            // 今まで敵が居た Default と同じものに当たる（床・壁・パーティー・敵どうし）。死体・落とし物（Debris）は Default とも当たるが、
            // 生きている敵とは当たらないようにする（死体が足元に絡まない）。壊せる草花（Breakable）は何とも当たらない。
            int debris = DebrisLayer.Index;
            for (int i = 0; i < 32; i++)
            {
                bool ignore = i == LayerIndex ? Physics.GetIgnoreLayerCollision(0, 0) : Physics.GetIgnoreLayerCollision(0, i);
                if (i == debris) ignore = true;
                Physics.IgnoreLayerCollision(LayerIndex, i, ignore);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"レイヤー {LayerIndex}「{EnemyLayer.Name}」を用意した（Default と同じものに当たる。死体・落とし物とは当たらない）");
        }
    }
}
