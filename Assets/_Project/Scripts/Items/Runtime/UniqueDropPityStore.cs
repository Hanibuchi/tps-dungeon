using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ユニーク武器の天井の数え（敵 × ユニークごとの「出ないまま倒した数」）。ゲームを閉じても持ち越す。
    /// 進行と同じく persistentDataPath の JSON（<see cref="FileName"/>）に書く。初めて使うときに読み、変わるたびに書く。
    /// セーブ全体の仕組みができたら、ここを差し替えればよい。
    /// </summary>
    public static class UniqueDropPityStore
    {
        public const string FileName = "drop_pity.json";

        [Serializable]
        private sealed class SaveData
        {
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string enemy;
            public string weapon;
            public int misses;
        }

        private static Dictionary<(string, string), int> counts;

        /// <summary>
        /// 古い保存の敵の読み替え。敵の id を EnemyIdentity にする前はプレハブのファイル名で数えていた。
        /// そのあと仮の 8 体を Notion「敵一覧」の敵に作り替えたので、そのときの id（pink_blob など）も今の id にする。
        /// </summary>
        private static readonly Dictionary<string, string> LegacyEnemyIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Blob_PinkBlob", "lava_slime" }, { "Blob_GreenBlob", "poison_slime" }, { "Blob_Wizard", "skeleton_mage" },
            { "Blob_Mushnub", "bomb_mushroom" }, { "Flying_Ghost", "cursed_lantern" }, { "Flying_Hywirl", "will_o_wisp" },
            { "Big_Frog", "miasma_frog" }, { "Big_Orc", "goblin" },
            { "pink_blob", "lava_slime" }, { "green_blob", "poison_slime" }, { "wizard", "skeleton_mage" },
            { "mushnub", "bomb_mushroom" }, { "ghost", "cursed_lantern" }, { "hywirl", "will_o_wisp" },
            { "frog", "miasma_frog" }, { "orc", "goblin" },
        };

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>enemy が weapon を出さないまま倒された数。</summary>
        public static int Misses(string enemy, string weapon)
        {
            EnsureLoaded();
            return counts.TryGetValue((enemy, weapon), out int misses) ? misses : 0;
        }

        /// <summary>数えを書き換える。変わったら真（書き出すのは <see cref="Save"/>）。</summary>
        public static bool SetMisses(string enemy, string weapon, int misses)
        {
            EnsureLoaded();
            misses = Math.Max(0, misses);
            if (Misses(enemy, weapon) == misses) return false;

            if (misses == 0) counts.Remove((enemy, weapon));
            else counts[(enemy, weapon)] = misses;
            return true;
        }

        /// <summary>書き出す。途中で落ちても前の保存が壊れないよう、一時ファイルに書いてから置き換える。</summary>
        public static void Save()
        {
            EnsureLoaded();
            var data = new SaveData();
            foreach (KeyValuePair<(string, string), int> pair in counts)
            {
                data.entries.Add(new Entry { enemy = pair.Key.Item1, weapon = pair.Key.Item2, misses = pair.Value });
            }

            string path = DefaultPath;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"ユニークの天井の数えを保存できなかった: {path}\n{e.Message}");
            }
        }

        /// <summary>数えを全部 0 にして保存も消す（デバッグ用）。</summary>
        public static void Clear()
        {
            counts = new Dictionary<(string, string), int>();
            if (File.Exists(DefaultPath)) File.Delete(DefaultPath);
        }

        private static void EnsureLoaded()
        {
            if (counts != null) return;

            counts = new Dictionary<(string, string), int>();
            string path = DefaultPath;
            if (!File.Exists(path)) return;

            SaveData data = null;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"ユニークの天井の数えを読めなかった: {path}\n{e.Message}");
            }

            if (data?.entries == null) return;
            foreach (Entry entry in data.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.enemy) || string.IsNullOrEmpty(entry.weapon) || entry.misses <= 0) continue;
                string enemy = LegacyEnemyIds.TryGetValue(entry.enemy, out string renamed) ? renamed : entry.enemy;
                var key = (enemy, entry.weapon);
                counts[key] = Math.Max(entry.misses, counts.TryGetValue(key, out int already) ? already : 0);
            }
        }
    }
}
