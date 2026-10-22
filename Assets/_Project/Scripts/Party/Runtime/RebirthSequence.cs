using System.Collections;
using TpsDungeon.Audio.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所から転生の渦で地上へ戻る演出。GameFlow が子に作り、死後の場所から地上へ移るときに暗転の代わりに回す。
    /// - 入るとき（<see cref="Leave"/>）: 水に潜る音を鳴らし、死後の場所の曲を消していく。視界が渦に吸い込まれるように歪み（レンズの歪み・色ずれ・ぼけ）、
    ///   最後は白く飛ぶ。
    /// - 着いたとき（<see cref="Arrive"/>）: 白い光の中から、歪みとぼけが引いていき、地上の景色がはっきりしてくる。
    /// 歪みとぼけは自前の Global Volume、白は一番手前の Canvas。地上ではカメラの post-processing が切れているので、演出の間だけ入れる。時間は実時間。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Rebirth Sequence")]
    public sealed class RebirthSequence : MonoBehaviour
    {
        // 暗転（ScreenFade 30000）より手前。白で覆っている間に裏で暗転と読み込みを済ませる。
        private const int SortingOrder = 30500;

        [Header("入るとき")]
        [SerializeField, Tooltip("渦に入ったときに鳴らす音（水に潜る音）。")]
        private AudioClip diveSound;

        [SerializeField, Range(0f, 1f), Tooltip("潜る音の大きさ。")]
        private float diveVolume = 0.9f;

        [SerializeField, Min(0.01f), Tooltip("歪みが強まりきるまでの秒数。")]
        private float distortSeconds = 2.2f;

        [SerializeField, Min(0.01f), Tooltip("白く飛ぶまでの秒数（歪みの途中から重ねる）。")]
        private float whiteOutSeconds = 1.2f;

        [SerializeField, Min(0f), Tooltip("死後の場所の曲を消していく秒数。")]
        private float bgmFadeSeconds = 2.5f;

        [Header("着いたとき")]
        [SerializeField, Min(0f), Tooltip("真っ白なまま待つ秒数。")]
        private float holdSeconds = 0.4f;

        [SerializeField, Min(0.01f), Tooltip("白が引いていく秒数。")]
        private float whiteInSeconds = 1.8f;

        [SerializeField, Min(0.01f), Tooltip("歪みとぼけが引ききるまでの秒数。")]
        private float clearSeconds = 3f;

        [Header("歪み")]
        [SerializeField, Range(-1f, 1f), Tooltip("強まりきったときのレンズの歪み（負で中心へ吸い込まれる）。")]
        private float lensDistortion = -0.55f;

        [SerializeField, Range(0f, 1f), Tooltip("強まりきったときの色ずれ。")]
        private float chromaticAberration = 1f;

        [SerializeField, Min(0f), Tooltip("強まりきったときのピントの合う距離（m）。これより奥がぼける。")]
        private float blurFocusEnd = 1.5f;

        private Canvas canvas;
        private CanvasGroup white;
        private Volume volume;
        private VolumeProfile profile;
        private UniversalAdditionalCameraData postTarget;
        private bool postPrevious;

        public AudioClip DiveSound
        {
            get => diveSound;
            set => diveSound = value;
        }

        /// <summary>parent の子に作る。初めは何も見せない。</summary>
        public static RebirthSequence Create(Transform parent)
        {
            var holder = new GameObject("[Rebirth Sequence]");
            holder.transform.SetParent(parent, false);
            var rebirth = holder.AddComponent<RebirthSequence>();
            rebirth.Build();
            return rebirth;
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.enabled = false;
            white = gameObject.AddComponent<CanvasGroup>();
            white.alpha = 0f;
            white.blocksRaycasts = false;
            white.interactable = false;

            var sheet = new GameObject("White");
            sheet.transform.SetParent(transform, false);
            var rect = sheet.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = sheet.AddComponent<Image>();
            // 真っ白より少し青みを帯びた光。
            image.color = new Color(0.93f, 0.97f, 1f);
            image.raycastTarget = false;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Rebirth (generated)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Add<LensDistortion>();
            profile.Add<ChromaticAberration>();
            var dof = profile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(true);

            var volumeHolder = new GameObject("Rebirth Volume");
            volumeHolder.transform.SetParent(transform, false);
            volume = volumeHolder.AddComponent<Volume>();
            volume.isGlobal = true;
            // 死後の場所の Volume（0）と目覚め（11）より上。
            volume.priority = 12f;
            volume.weight = 0f;
            volume.sharedProfile = profile;
        }

        private void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }

        /// <summary>渦に入る。潜る音を鳴らし、視界を歪ませて白く飛ばす。終わったときは真っ白（このあと裏で暗転と読み込みをしてよい）。</summary>
        public IEnumerator Leave()
        {
            ApplyValues();
            canvas.enabled = true;
            white.alpha = 0f;
            EnablePostProcessing();

            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                if (diveSound != null) audio.PlaySe(diveSound, diveVolume);
                audio.StopBgm(bgmFadeSeconds);
            }

            float whiteFrom = Mathf.Max(0f, distortSeconds - whiteOutSeconds);
            float startedAt = Time.unscaledTime;
            for (float t = 0f; t < distortSeconds; t = Time.unscaledTime - startedAt)
            {
                // 始めはゆっくり、だんだん速く吸い込まれる。
                float k = t / distortSeconds;
                volume.weight = k * k;
                white.alpha = Mathf.SmoothStep(0f, 1f, (t - whiteFrom) / whiteOutSeconds);
                yield return null;
            }
            volume.weight = 1f;
            white.alpha = 1f;
        }

        /// <summary>着いたシーンで、白い光の中から歪みとぼけを引かせて景色を見せる。終わったら全部外す。</summary>
        public IEnumerator Arrive()
        {
            canvas.enabled = true;
            white.alpha = 1f;
            volume.weight = 1f;
            // 移った先のカメラ（地上へ持ち越したもの）にも効かせる。読み込みで設定が変わっていることがあるので入れ直す。
            postTarget = null;
            EnablePostProcessing();

            float startedAt = Time.unscaledTime;
            float end = holdSeconds + Mathf.Max(whiteInSeconds, clearSeconds);
            for (float t = 0f; t < end; t = Time.unscaledTime - startedAt)
            {
                float u = t - holdSeconds;
                white.alpha = 1f - Mathf.SmoothStep(0f, 1f, u / whiteInSeconds);
                volume.weight = 1f - Mathf.SmoothStep(0f, 1f, u / clearSeconds);
                yield return null;
            }

            Finish();
        }

        /// <summary>
        /// 白い幕を出すか（歪みはそのまま）。白く飛ばしたあとに UI Toolkit の画面（持ち越しの選択）を出す間だけ隠す。
        /// 幕は uGUI のキャンバスで、UI Toolkit の画面より上に描かれることがあるため。
        /// </summary>
        public bool WhiteVisible
        {
            get => canvas.enabled;
            set => canvas.enabled = value;
        }

        /// <summary>途中でも全部外す。</summary>
        public void Finish()
        {
            white.alpha = 0f;
            canvas.enabled = false;
            volume.weight = 0f;
            if (postTarget != null) postTarget.renderPostProcessing = postPrevious;
            postTarget = null;
        }

        private void ApplyValues()
        {
            if (profile.TryGet(out LensDistortion lens))
            {
                lens.intensity.Override(lensDistortion);
                lens.scale.Override(1f);
            }
            if (profile.TryGet(out ChromaticAberration chroma)) chroma.intensity.Override(chromaticAberration);
            if (profile.TryGet(out DepthOfField dof)) dof.gaussianEnd.Override(blurFocusEnd);
        }

        private void EnablePostProcessing()
        {
            if (postTarget != null) return;
            Camera main = Camera.main;
            if (main == null) return;
            postTarget = main.GetUniversalAdditionalCameraData();
            postPrevious = postTarget.renderPostProcessing;
            postTarget.renderPostProcessing = true;
        }
    }
}
