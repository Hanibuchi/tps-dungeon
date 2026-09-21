using TpsDungeon.Audio.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace TpsDungeon.Audio.Authoring
{
    /// <summary>
    /// 音まわりの配線をひとまとめにした設定。ミキサーと露出パラメータ名、
    /// スナップショット、既定音量をここで持つ。
    /// GameAudio プレハブから参照する想定で、シーンごとに差し替えるものではない。
    /// </summary>
    [CreateAssetMenu(fileName = "GameAudioConfig", menuName = "TPS Dungeon/Audio/Game Audio Config")]
    public sealed class GameAudioConfig : ScriptableObject
    {
        [Header("ミキサー")]
        [SerializeField, Tooltip("音量を流し込む先の AudioMixer。")]
        private AudioMixer mixer;

        [SerializeField, Tooltip("BGM 用 AudioSource の出力先グループ。")]
        private AudioMixerGroup bgmGroup;

        [SerializeField, Tooltip("SE 用 AudioSource の出力先グループ。")]
        private AudioMixerGroup seGroup;

        [Header("露出パラメータ名")]
        [SerializeField, Tooltip("全体音量。ミキサー側で露出させた名前と一致している必要がある。")]
        private string masterVolumeParameter = "MasterVolume";

        [SerializeField, Tooltip("BGM 音量。")]
        private string bgmVolumeParameter = "BgmVolume";

        [SerializeField, Tooltip("SE 音量。")]
        private string seVolumeParameter = "SeVolume";

        [Header("スナップショット")]
        [SerializeField, Tooltip("素の状態。")]
        private AudioMixerSnapshot defaultSnapshot;

        [SerializeField, Tooltip("ダンジョン内。残響が乗る。")]
        private AudioMixerSnapshot dungeonSnapshot;

        [SerializeField, Tooltip("ポーズ中。全体がこもる。")]
        private AudioMixerSnapshot pausedSnapshot;

        [Header("既定値")]
        [SerializeField, Range(0f, 1f), Tooltip("初回起動時の全体音量。")]
        private float defaultMasterVolume = 0.8f;

        [SerializeField, Range(0f, 1f), Tooltip("初回起動時の BGM 音量。")]
        private float defaultBgmVolume = 0.7f;

        [SerializeField, Range(0f, 1f), Tooltip("初回起動時の SE 音量。")]
        private float defaultSeVolume = 0.8f;

        [Header("再生")]
        [SerializeField, Min(0f), Tooltip("BGM を切り替えるときのクロスフェード秒数。")]
        private float bgmCrossFadeSeconds = 1.5f;

        [SerializeField, Min(0f), Tooltip("スナップショットを切り替えるときの既定の遷移秒数。")]
        private float snapshotTransitionSeconds = 0.4f;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup BgmGroup => bgmGroup;
        public AudioMixerGroup SeGroup => seGroup;
        public float BgmCrossFadeSeconds => bgmCrossFadeSeconds;
        public float SnapshotTransitionSeconds => snapshotTransitionSeconds;

        /// <summary>そのチャンネルに対応する、ミキサー側の露出パラメータ名。</summary>
        public string GetVolumeParameter(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Bgm: return bgmVolumeParameter;
                case AudioChannel.Se: return seVolumeParameter;
                default: return masterVolumeParameter;
            }
        }

        /// <summary>保存された値が無いときに使う音量。</summary>
        public float GetDefaultVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Bgm: return defaultBgmVolume;
                case AudioChannel.Se: return defaultSeVolume;
                default: return defaultMasterVolume;
            }
        }

        /// <summary>対応するスナップショット。未設定なら null。</summary>
        public AudioMixerSnapshot GetSnapshot(AudioSnapshotId id)
        {
            switch (id)
            {
                case AudioSnapshotId.Dungeon: return dungeonSnapshot;
                case AudioSnapshotId.Paused: return pausedSnapshot;
                default: return defaultSnapshot;
            }
        }
    }
}
