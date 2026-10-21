using UnityEngine;

namespace TpsDungeon.Audio.Runtime
{
    /// <summary>
    /// このシーンにいる間だけ流す BGM。着いたら（Start）フェードして流し、シーンを出るとき（OnDestroy）に、まだこの曲が流れていればフェードして止める。
    /// 先に止めておきたい側（死後の場所から戻る演出など）は、GameAudio.StopBgm を呼べばよい（同じ曲でなくなっていれば何もしない）。
    /// シーンのどこかに 1 つ置く。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Scene BGM")]
    public sealed class SceneBgm : MonoBehaviour
    {
        [SerializeField, Tooltip("このシーンで流す曲。")]
        private AudioClip clip;

        [SerializeField, Min(0f), Tooltip("流し始めのフェードの秒数。")]
        private float fadeInSeconds = 3f;

        [SerializeField, Min(0f), Tooltip("シーンを出るときのフェードの秒数。")]
        private float fadeOutSeconds = 1f;

        public AudioClip Clip
        {
            get => clip;
            set => clip = value;
        }

        private void Start()
        {
            GameAudio audio = GameAudio.Instance;
            if (audio != null && clip != null) audio.PlayBgm(clip, fadeInSeconds);
        }

        private void OnDestroy()
        {
            GameAudio audio = GameAudio.Instance;
            if (audio != null && clip != null && audio.CurrentBgm == clip) audio.StopBgm(fadeOutSeconds);
        }
    }
}
