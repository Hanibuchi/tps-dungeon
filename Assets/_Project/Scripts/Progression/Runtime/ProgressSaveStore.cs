using System;
using System.IO;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// パーティの進行（レベル・経験値・現在 HP）の保存先。ファイルの読み書きをこのクラスに閉じ込めてある。
    /// 設定と違ってラン中の進行なので、PlayerPrefs ではなく persistentDataPath の JSON に書く。
    /// セーブ全体の仕組みができたら、ここを差し替えるか PartyProgression.CaptureSaveData を直接使えばよい。
    /// </summary>
    public static class ProgressSaveStore
    {
        public const string FileName = "progress.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static string ToJson(PartyProgressSaveData data) => JsonUtility.ToJson(data, true);

        /// <summary>JSON を読む。空・壊れている・形が違うときは false。</summary>
        public static bool TryFromJson(string json, out PartyProgressSaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                data = JsonUtility.FromJson<PartyProgressSaveData>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (data == null) return false;
            if (data.members == null) data.members = new System.Collections.Generic.List<CharacterProgressSaveData>();
            return true;
        }

        /// <summary>書き出す。途中で落ちても前の保存が壊れないよう、一時ファイルに書いてから置き換える。</summary>
        public static void Save(PartyProgressSaveData data, string path = null)
        {
            if (data == null) return;
            path ??= DefaultPath;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(data));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <summary>読み込む。ファイルが無いか読めなければ false（壊れていたら警告を出す）。</summary>
        public static bool TryLoad(out PartyProgressSaveData data, string path = null)
        {
            data = null;
            path ??= DefaultPath;
            if (!File.Exists(path)) return false;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"進行の保存を読めなかった: {path}\n{e.Message}");
                return false;
            }

            if (TryFromJson(json, out data)) return true;

            Debug.LogWarning($"進行の保存が壊れているので読まなかった: {path}");
            return false;
        }

        public static bool Exists(string path = null) => File.Exists(path ?? DefaultPath);

        /// <summary>保存を消す。</summary>
        public static void Delete(string path = null)
        {
            path ??= DefaultPath;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
