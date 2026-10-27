using System.IO;
using TpsDungeon.Items;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Progression;

namespace TpsDungeon.Party
{
    /// <summary>
    /// セーブ枠（1 つだけ）。中身は persistentDataPath の次のファイルで、どれも書くのはそれぞれの持ち主:
    /// - run.json … 居場所・並び・持ち物（<see cref="RunSaveStore"/>、書くのは <see cref="GameSave"/>）
    /// - progress.json … レベル・経験値・HP（<see cref="ProgressSaveStore"/>）
    /// - meta.json … ソウル・魂の強化（<see cref="MetaUpgrades"/>）
    /// - shortcuts.json … 層ごとの近道（<see cref="ShortcutUnlocks"/>）
    /// - drop_pity.json … ユニークの天井（<see cref="UniqueDropPityStore"/>）
    /// ここではまとめて「有るか」「消す」だけを扱う。
    /// </summary>
    public static class SaveSlot
    {
        public static bool Exists =>
            RunSaveStore.Exists
            || ProgressSaveStore.Exists()
            || File.Exists(MetaUpgrades.DefaultPath)
            || File.Exists(ShortcutUnlocks.DefaultPath)
            || File.Exists(UniqueDropPityStore.DefaultPath);

        /// <summary>セーブを全部消し、読み込んである状態も捨てる。次に始めると初めから。</summary>
        public static void DeleteAll()
        {
            RunSaveStore.Delete();
            ProgressSaveStore.Delete();
            DeleteFile(MetaUpgrades.DefaultPath);
            MetaUpgrades.Reload();
            DeleteFile(ShortcutUnlocks.DefaultPath);
            ShortcutUnlocks.Reload();
            UniqueDropPityStore.Clear();
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
