using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>セーブ枠（<see cref="SaveSlot"/>）をエディタから消す。消すと次の再生は初めから（地上・Lv1・ソウル 0）。</summary>
    public static class SaveSlotMenu
    {
        [MenuItem("Tools/TPS Dungeon/セーブ/セーブデータを消す", false, 600)]
        public static void DeleteAll()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("再生中はセーブを消さない（止めるときにまた書かれるため）。再生を止めてから消す。");
                return;
            }
            if (!SaveSlot.Exists)
            {
                Debug.Log("セーブデータは無い");
                return;
            }
            if (!EditorUtility.DisplayDialog("セーブデータを消す", "ソウル・魂の強化・レベル・持ち物・居場所を全部消します。", "消す", "やめる")) return;

            SaveSlot.DeleteAll();
            Debug.Log($"セーブデータを消した: {Application.persistentDataPath}");
        }

        [MenuItem("Tools/TPS Dungeon/セーブ/セーブの場所を開く", false, 601)]
        public static void Reveal() => EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
