using System;
using System.IO;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// ラン中の状態（<see cref="RunSaveData"/>）の保存先。persistentDataPath の run.json に書く。
    /// ファイルの読み書きをこのクラスに閉じ込めてある（ProgressSaveStore と同じ作り）。
    /// </summary>
    public static class RunSaveStore
    {
        public const string FileName = "run.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>保存先を差し替える（テスト用）。null か空なら既定の場所。</summary>
        public static string PathOverride { get; set; }

        // null も空文字も「既定の場所」。エディタのドメインリロードをまたぐと、null だった文字列が空文字になって戻ってくるため。
        public static string FilePath => string.IsNullOrEmpty(PathOverride) ? DefaultPath : PathOverride;

        public static string ToJson(RunSaveData data) => JsonUtility.ToJson(data, true);

        /// <summary>JSON を読む。空・壊れている・形が違うときは false。</summary>
        public static bool TryFromJson(string json, out RunSaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                data = JsonUtility.FromJson<RunSaveData>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (data == null) return false;
            data.Normalize();
            return true;
        }

        /// <summary>書き出す。途中で落ちても前の保存が壊れないよう、一時ファイルに書いてから置き換える。</summary>
        public static void Save(RunSaveData data)
        {
            if (data == null) return;
            string path = FilePath;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string temp = path + ".tmp";
                File.WriteAllText(temp, ToJson(data));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"ラン中の状態を保存できなかった: {path}\n{e.Message}");
            }
        }

        /// <summary>読み込む。ファイルが無いか読めなければ false（壊れていたら警告を出す）。</summary>
        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            string path = FilePath;
            if (!File.Exists(path)) return false;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"ラン中の状態を読めなかった: {path}\n{e.Message}");
                return false;
            }

            if (TryFromJson(json, out data)) return true;

            Debug.LogWarning($"ラン中の状態の保存が壊れているので読まなかった: {path}");
            return false;
        }

        public static bool Exists => File.Exists(FilePath);

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PathOverride = null;
    }
}
