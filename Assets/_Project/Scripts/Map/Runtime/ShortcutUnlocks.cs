using System;
using System.IO;
using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 層ごとのショートカットの解放状態の保存先（persistentDataPath の shortcuts.json）。最初に要ったときに読み、変えるたびに書く。
    /// 解放は永続アップグレードで層ごとにする（いまはまだ買う場所が無いので、「階のデバッグ」ウィンドウで切り替える）。
    /// 変わったら <see cref="Changed"/> で知らせ、ボス階の扉と井戸・地上のロープが開き閉めし直す。
    /// </summary>
    public static class ShortcutUnlocks
    {
        public const string FileName = "shortcuts.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        private static string overridePath;
        private static ShortcutUnlockSet current;

        /// <summary>解放状態が変わった（引数は層。0 始まり）。</summary>
        public static event Action<int> Changed;

        /// <summary>保存先を差し替える（テスト用）。null か空なら既定の場所に戻す。読み込んだ状態も捨てる。</summary>
        public static string PathOverride
        {
            get => overridePath;
            set
            {
                overridePath = value;
                current = null;
            }
        }

        // null も空文字も「既定の場所」。エディタのドメインリロードをまたぐと、null だった文字列が空文字になって戻ってくるため。
        private static string FilePath => string.IsNullOrEmpty(overridePath) ? DefaultPath : overridePath;

        /// <summary>その層のショートカットを解放してあるか（0 始まり）。</summary>
        public static bool IsUnlocked(int layer) => Load().IsUnlocked(layer);

        /// <summary>その層のショートカットを解放する（unlocked が偽なら閉じる）。変わったら書き出して知らせる。</summary>
        public static void SetUnlocked(int layer, bool unlocked)
        {
            var set = Load();
            if (!set.Set(layer, unlocked)) return;
            Save(set);
            Changed?.Invoke(layer);
        }

        /// <summary>読み込み直す（ファイルを外で書き換えたとき）。</summary>
        public static void Reload() => current = null;

        private static ShortcutUnlockSet Load()
        {
            if (current != null) return current;
            current = new ShortcutUnlockSet();
            string path = FilePath;
            if (!File.Exists(path)) return current;

            try
            {
                var loaded = JsonUtility.FromJson<ShortcutUnlockSet>(File.ReadAllText(path));
                if (loaded != null) current = loaded;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"ショートカットの解放状態を読めなかった: {path}\n{e.Message}");
            }
            current.Normalize();
            return current;
        }

        /// <summary>書き出す。途中で落ちても前の保存が壊れないよう、一時ファイルに書いてから置き換える。</summary>
        private static void Save(ShortcutUnlockSet set)
        {
            string path = FilePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(set, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の購読を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            current = null;
        }
    }
}
