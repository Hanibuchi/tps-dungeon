using TpsDungeon.Audio.Runtime;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// レベルが上がったときに効果音を鳴らす。主人公（CharacterProgression と同じ GameObject）に付ける。
    /// 仲間は主人公と同時に上がるので、仲間には付けない（付けると重なって鳴る）。
    /// 保存の読み込みやゲームオーバーのリセットでは鳴らない（どちらもレベルアップとして知らせないため）。
    /// 音は画面のどこでも同じに聞こえる SE として GameAudio から出すので、設定画面の SE 音量が効く。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterProgression))]
    [AddComponentMenu("TPS Dungeon/Level Up Sound")]
    public sealed class LevelUpSound : MonoBehaviour
    {
        [SerializeField, Tooltip("レベルアップの効果音。未設定なら鳴らさない。")]
        private AudioClip clip;

        [SerializeField, Range(0f, 1f), Tooltip("この音だけの音量（SE 音量に掛かる）。")]
        private float volume = 1f;

        [SerializeField, Min(0f), Tooltip("続けて上がったときに重ねて鳴らさない間隔（秒）。デバッグで 10 レベル一気に上げたときなど用。")]
        private float minInterval = 0.2f;

        private CharacterProgression progression;
        private float lastPlayedTime = float.NegativeInfinity;

        public AudioClip Clip => clip;

        private void Awake()
        {
            progression = GetComponent<CharacterProgression>();
        }

        private void OnEnable()
        {
            if (progression != null) progression.LeveledUp += OnLeveledUp;
        }

        private void OnDisable()
        {
            if (progression != null) progression.LeveledUp -= OnLeveledUp;
        }

        private void OnLeveledUp(CharacterProgression _, int from, int to)
        {
            // ポーズ中でも鳴るように、実時間で間隔を測る。
            float now = Time.unscaledTime;
            if (now - lastPlayedTime < minInterval) return;

            lastPlayedTime = now;
            Play();
        }

        /// <summary>今すぐ鳴らす。</summary>
        public void Play()
        {
            if (clip == null) return;
            GameAudio.Instance?.PlaySe(clip, volume);
        }
    }
}
