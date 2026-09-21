using System.Collections;
using TpsDungeon.Audio.Authoring;
using TpsDungeon.Audio.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace TpsDungeon.Audio.Runtime
{
    /// <summary>
    /// 音を鳴らす窓口。シーンをまたいで 1 つだけ生き残る。
    /// Resources/Audio/GameAudio.prefab から自動で立ち上がるので、
    /// シーン側に何か置く必要はない。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Game Audio")]
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Resources 以下のプレハブの場所。自動起動で使う。</summary>
        public const string ResourcePath = "Audio/GameAudio";

        [SerializeField, Tooltip("ミキサーと既定音量の設定。")]
        private GameAudioConfig config;

        [SerializeField, Min(1), Tooltip("同時に鳴らせる SE の数。足りなくなると一番古い音を止めて使い回す。")]
        private int seVoiceCount = 8;

        private AudioSource[] bgmSources;
        private AudioSource[] seSources;
        private int activeBgmIndex;
        private int nextSeIndex;
        private Coroutine bgmFade;

        /// <summary>唯一のインスタンス。起動前や終了後は null。</summary>
        public static GameAudio Instance { get; private set; }

        /// <summary>音量の読み書きはここ経由。</summary>
        public AudioVolumeController Volumes { get; private set; }

        public GameAudioConfig Config => config;

        /// <summary>今流れている BGM。何も流れていなければ null。</summary>
        public AudioClip CurrentBgm => bgmSources != null ? bgmSources[activeBgmIndex].clip : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("GameAudio のプレハブが見つからない: Resources/" + ResourcePath);
                return;
            }

            Instantiate(prefab).name = prefab.name;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (config == null)
            {
                Debug.LogError("GameAudioConfig が設定されていないので音を鳴らせない。", this);
                return;
            }

            BuildSources();
            Volumes = new AudioVolumeController(config);
            TransitionTo(AudioSnapshotId.Default, 0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Volumes?.Flush();
        }

        private void OnApplicationQuit()
        {
            Volumes?.Flush();
        }

        // ---- BGM -----------------------------------------------------------

        /// <summary>BGM を差し替える。同じ曲なら何もしない。clip が null なら止める。</summary>
        public void PlayBgm(AudioClip clip, float fadeSeconds = -1f)
        {
            if (bgmSources == null) return;
            if (clip == null)
            {
                StopBgm(fadeSeconds);
                return;
            }

            if (CurrentBgm == clip && bgmSources[activeBgmIndex].isPlaying) return;

            int nextIndex = 1 - activeBgmIndex;
            AudioSource next = bgmSources[nextIndex];
            next.clip = clip;
            next.volume = 0f;
            next.Play();

            activeBgmIndex = nextIndex;
            StartFade(FadeSecondsOr(fadeSeconds), nextIndex);
        }

        /// <summary>BGM をフェードアウトさせて止める。</summary>
        public void StopBgm(float fadeSeconds = -1f)
        {
            if (bgmSources == null) return;
            StartFade(FadeSecondsOr(fadeSeconds), -1);
        }

        private float FadeSecondsOr(float fadeSeconds)
        {
            return fadeSeconds >= 0f ? fadeSeconds : config.BgmCrossFadeSeconds;
        }

        private void StartFade(float seconds, int targetIndex)
        {
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(FadeBgm(seconds, targetIndex));
        }

        /// <summary>targetIndex の音源を 1 へ、それ以外を 0 へ持っていく。-1 なら全部止める。</summary>
        private IEnumerator FadeBgm(float seconds, int targetIndex)
        {
            var from = new float[bgmSources.Length];
            for (int i = 0; i < bgmSources.Length; i++) from[i] = bgmSources[i].volume;

            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
                for (int i = 0; i < bgmSources.Length; i++)
                {
                    bgmSources[i].volume = Mathf.Lerp(from[i], i == targetIndex ? 1f : 0f, t);
                }

                yield return null;
            }

            for (int i = 0; i < bgmSources.Length; i++)
            {
                bgmSources[i].volume = i == targetIndex ? 1f : 0f;
                if (i == targetIndex) continue;

                bgmSources[i].Stop();
                bgmSources[i].clip = null;
            }

            bgmFade = null;
        }

        // ---- SE ------------------------------------------------------------

        /// <summary>画面のどこで鳴っても同じに聞こえる SE。UI 音や通知向け。</summary>
        public void PlaySe(AudioClip clip, float volumeScale = 1f)
        {
            AudioSource source = TakeSeSource();
            if (source == null || clip == null) return;

            source.spatialBlend = 0f;
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        /// <summary>場所のある SE。足音や着弾音向け。</summary>
        public void PlaySeAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            AudioSource source = TakeSeSource();
            if (source == null || clip == null) return;

            source.transform.position = position;
            source.spatialBlend = 1f;
            source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        /// <summary>空いている音源を返す。全部埋まっていたら一番古いものを奪う。</summary>
        private AudioSource TakeSeSource()
        {
            if (seSources == null) return null;

            for (int i = 0; i < seSources.Length; i++)
            {
                int index = (nextSeIndex + i) % seSources.Length;
                if (seSources[index].isPlaying) continue;

                nextSeIndex = (index + 1) % seSources.Length;
                return seSources[index];
            }

            AudioSource oldest = seSources[nextSeIndex];
            nextSeIndex = (nextSeIndex + 1) % seSources.Length;
            oldest.Stop();
            return oldest;
        }

        // ---- スナップショット -----------------------------------------------

        /// <summary>ミキサーの状態を切り替える。ポーズやフロア移動の演出用。</summary>
        public void TransitionTo(AudioSnapshotId id, float seconds = -1f)
        {
            if (config == null) return;

            AudioMixerSnapshot snapshot = config.GetSnapshot(id);
            if (snapshot == null)
            {
                Debug.LogWarning("スナップショットが設定されていない: " + id, this);
                return;
            }

            snapshot.TransitionTo(seconds >= 0f ? seconds : config.SnapshotTransitionSeconds);
        }

        // ---- 組み立て -------------------------------------------------------

        /// <summary>
        /// AudioSource を実行時に組み立てる。プレハブに何十個も並べるより、
        /// 出力先グループの付け間違いが起きない。
        /// </summary>
        private void BuildSources()
        {
            bgmSources = new AudioSource[2];
            for (int i = 0; i < bgmSources.Length; i++)
            {
                bgmSources[i] = CreateSource("BGM " + (i + 1), config.BgmGroup);
                bgmSources[i].loop = true;
                bgmSources[i].volume = 0f;
                bgmSources[i].spatialBlend = 0f;
            }

            seSources = new AudioSource[Mathf.Max(1, seVoiceCount)];
            for (int i = 0; i < seSources.Length; i++)
            {
                seSources[i] = CreateSource("SE " + (i + 1), config.SeGroup);
                seSources[i].loop = false;
                seSources[i].volume = 1f;
                seSources[i].spatialBlend = 0f;
                seSources[i].rolloffMode = AudioRolloffMode.Linear;
                seSources[i].maxDistance = 30f;
            }
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup group)
        {
            var holder = new GameObject(sourceName);
            holder.transform.SetParent(transform, false);

            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
