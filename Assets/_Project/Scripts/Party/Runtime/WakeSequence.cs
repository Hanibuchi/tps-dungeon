using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所で目を覚ます演出。GameFlow が子に作り、全滅から着いたときに明転の代わりに回す。
    /// - 上下のまぶた（縁がぼけた黒い板）を、何度か瞬きしながら開いていく。
    /// - 開き始めは強くぼけて暗く、瞬きを重ねるうちに視界がはっきりしてくる（自前の Global Volume の被写界深度と露出の重みを下げる）。
    /// - カメラの post-processing は演出の間だけ入れる（死後の場所では元から入っている）。
    /// 時間は実時間。まぶたの開き具合とぼけ具合は、下の鍵（秒, 値）を順につないだ曲線。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Wake Sequence")]
    public sealed class WakeSequence : MonoBehaviour
    {
        // まぶたより手前のものは暗転（ScreenFade 30000）だけ。HUD やメニューよりは手前。
        private const int SortingOrder = 29000;

        // まぶた 1 枚の、黒く塗りつぶす部分と、縁のぼけの高さ（画面の高さに対する割合）。閉じきると上下の黒が真ん中で重なる。
        private const float LidSolid = 0.5f;
        private const float LidEdge = 0.22f;

        [SerializeField, Tooltip("まぶたの閉じ具合（1 で閉じきり、0 で開ききり）。(秒, 値) を順につなぐ。")]
        private Vector2[] lidKeys =
        {
            new Vector2(0f, 1f),
            new Vector2(0.7f, 1f),
            new Vector2(1.3f, 0.55f),   // うっすら開く
            new Vector2(1.6f, 0.62f),
            new Vector2(1.85f, 1f),     // 閉じる
            new Vector2(2.3f, 1f),
            new Vector2(2.9f, 0.3f),    // さっきより開く
            new Vector2(3.15f, 0.35f),
            new Vector2(3.35f, 0.92f),  // 素早く瞬き
            new Vector2(3.5f, 0.92f),
            new Vector2(4.3f, 0f),      // 開ききる
        };

        [SerializeField, Tooltip("視界のぼけと暗さ（1 で一番ぼけて暗く、0 ではっきり）。(秒, 値) を順につなぐ。")]
        private Vector2[] blurKeys =
        {
            new Vector2(0f, 1f),
            new Vector2(1.85f, 1f),
            new Vector2(2.9f, 0.6f),
            new Vector2(3.5f, 0.45f),
            new Vector2(5.5f, 0f),
        };

        [SerializeField, Min(0f), Tooltip("一番ぼけたときのピントの合う距離（m）。これより奥がぼける。")]
        private float blurFocusEnd = 0.6f;

        [SerializeField, Tooltip("一番暗いときの露出（EV）。")]
        private float darkExposure = -1.2f;

        private RectTransform topLid;
        private RectTransform bottomLid;
        private Canvas canvas;
        private Volume volume;
        private VolumeProfile profile;
        private Texture2D lidTexture;
        private UniversalAdditionalCameraData postTarget;
        private bool postPrevious;

        /// <summary>演出の途中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>parent の子に作る。初めは何も見せない。</summary>
        public static WakeSequence Create(Transform parent)
        {
            var holder = new GameObject("[Wake Sequence]");
            holder.transform.SetParent(parent, false);
            var wake = holder.AddComponent<WakeSequence>();
            wake.Build();
            return wake;
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.enabled = false;

            lidTexture = BuildLidTexture();
            topLid = BuildLid("Top Lid", flip: false);
            bottomLid = BuildLid("Bottom Lid", flip: true);

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Wake (generated)";
            profile.hideFlags = HideFlags.DontSave;
            var dof = profile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(true);
            profile.Add<ColorAdjustments>();

            var volumeHolder = new GameObject("Wake Volume");
            volumeHolder.transform.SetParent(transform, false);
            volume = volumeHolder.AddComponent<Volume>();
            volume.isGlobal = true;
            // 死後の場所の Volume（0）より上に重ねる。全滅の演出（10）とは同時に効かない。
            volume.priority = 11f;
            volume.weight = 0f;
            volume.sharedProfile = profile;
        }

        /// <summary>縦 1 列の、上が黒く下の縁へ向かって透ける絵。上のまぶたはそのまま、下のまぶたは上下を返して貼る。</summary>
        private static Texture2D BuildLidTexture()
        {
            const int height = 128;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "Lid (generated)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float edge = LidEdge / (LidSolid + LidEdge);
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float a = Mathf.SmoothStep(0f, 1f, v / edge);
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }
            texture.Apply();
            return texture;
        }

        private RectTransform BuildLid(string name, bool flip)
        {
            var lid = new GameObject(name);
            lid.transform.SetParent(transform, false);
            var rect = lid.AddComponent<RectTransform>();
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = lid.AddComponent<RawImage>();
            image.texture = lidTexture;
            image.color = Color.black;
            image.raycastTarget = false;
            if (flip) image.uvRect = new Rect(0f, 1f, 1f, -1f);
            return rect;
        }

        private void OnDestroy()
        {
            if (profile != null) Destroy(profile);
            if (lidTexture != null) Destroy(lidTexture);
        }

        /// <summary>
        /// まぶたを閉じきった姿で出す。暗転を外す前に呼べば、明るくなった瞬間に景色が見えてしまわない。
        /// </summary>
        public void Close()
        {
            canvas.enabled = true;
            SetLids(1f);
            SetBlur(1f);
        }

        /// <summary>瞬きしながら目を開ける。終わったら全部外す。</summary>
        public IEnumerator Play()
        {
            IsPlaying = true;
            Close();
            EnablePostProcessing();

            if (profile.TryGet(out DepthOfField dof)) dof.gaussianEnd.Override(blurFocusEnd);
            if (profile.TryGet(out ColorAdjustments color)) color.postExposure.Override(darkExposure);

            float end = Mathf.Max(LastTime(lidKeys), LastTime(blurKeys));
            float startedAt = Time.unscaledTime;
            for (float t = 0f; t < end; t = Time.unscaledTime - startedAt)
            {
                SetLids(Evaluate(lidKeys, t));
                SetBlur(Evaluate(blurKeys, t));
                yield return null;
            }

            Finish();
        }

        /// <summary>途中でも外す（シーンを移るときなど）。</summary>
        public void Finish()
        {
            SetLids(0f);
            SetBlur(0f);
            canvas.enabled = false;
            if (postTarget != null) postTarget.renderPostProcessing = postPrevious;
            postTarget = null;
            IsPlaying = false;
        }

        private void SetLids(float closed)
        {
            float cover = Mathf.Clamp01(closed) * (LidSolid + LidEdge);
            topLid.anchorMin = new Vector2(0f, 1f - cover);
            topLid.anchorMax = Vector2.one;
            bottomLid.anchorMin = Vector2.zero;
            bottomLid.anchorMax = new Vector2(1f, cover);
        }

        private void SetBlur(float amount) => volume.weight = Mathf.Clamp01(amount);

        private void EnablePostProcessing()
        {
            if (postTarget != null) return;
            Camera main = Camera.main;
            if (main == null) return;
            postTarget = main.GetUniversalAdditionalCameraData();
            postPrevious = postTarget.renderPostProcessing;
            postTarget.renderPostProcessing = true;
        }

        private static float LastTime(Vector2[] keys) => keys != null && keys.Length > 0 ? keys[keys.Length - 1].x : 0f;

        /// <summary>(秒, 値) の鍵を、隣どうしなめらかにつないだ t 秒の値。</summary>
        private static float Evaluate(Vector2[] keys, float t)
        {
            if (keys == null || keys.Length == 0) return 0f;
            if (t <= keys[0].x) return keys[0].y;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].x) continue;
                Vector2 a = keys[i - 1];
                Vector2 b = keys[i];
                float k = b.x > a.x ? (t - a.x) / (b.x - a.x) : 1f;
                return Mathf.Lerp(a.y, b.y, Mathf.SmoothStep(0f, 1f, k));
            }
            return keys[keys.Length - 1].y;
        }
    }
}
