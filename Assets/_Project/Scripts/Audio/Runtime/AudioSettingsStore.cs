using TpsDungeon.Audio.Data;
using UnityEngine;

namespace TpsDungeon.Audio.Runtime
{
    /// <summary>
    /// 音量設定の保存先。今は PlayerPrefs だが、セーブ機構ができたら
    /// ここだけ差し替えれば済むように、PlayerPrefs の利用をこのクラスに閉じ込めてある。
    /// </summary>
    public static class AudioSettingsStore
    {
        private const string KeyPrefix = "Audio.";

        /// <summary>"Audio.MasterVolume" のような保存キー。</summary>
        public static string KeyFor(AudioChannel channel)
        {
            return KeyPrefix + channel + "Volume";
        }

        /// <summary>保存された音量。未保存なら fallback をそのまま返す。</summary>
        public static float Load(AudioChannel channel, float fallback)
        {
            return AudioVolumeMath.ClampNormalized(PlayerPrefs.GetFloat(KeyFor(channel), fallback));
        }

        /// <summary>音量を覚える。ディスクへの書き出しは <see cref="Flush"/> のタイミング。</summary>
        public static void Save(AudioChannel channel, float normalized)
        {
            PlayerPrefs.SetFloat(KeyFor(channel), AudioVolumeMath.ClampNormalized(normalized));
        }

        /// <summary>ディスクに書き出す。スライダーを動かすたびに呼ぶ必要はない。</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        /// <summary>保存を全部消す。初回起動の挙動を確かめたいときのデバッグ用。</summary>
        public static void Clear()
        {
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                PlayerPrefs.DeleteKey(KeyFor(channel));
            }

            Flush();
        }
    }
}
