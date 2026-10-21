using System;
using System.Collections;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 全員が倒れたときの演出。GameFlow が子に作り、全滅を見つけたら <see cref="Play"/> を回してから死後の場所へ送る。
    /// - 一瞬スローにして、ゆっくり元の速さへ戻す。
    /// - 音を Dead のスナップショットへ（SE がこもる）移し、死亡の曲へ切り替える。
    /// - 画面の色を抜いて周りから暗くする（自前の Global Volume の重みを上げる。共通の Volume のアセットには触らない）。
    ///   地上ではカメラの post-processing が切れているので、演出の間だけ入れる。
    /// - 死亡用のカメラに切り替え、最後に倒れた人の上へゆっくり回りながら引き上げる。
    /// - 「力尽きた」を出す知らせを送り、少ししたらキーで飛ばせるようにする。放っておいても <see cref="autoAdvanceSeconds"/> で終わる。
    /// 時間はすべて実時間（スローに左右されない）。元へ戻すのは <see cref="Restore"/>（死後の場所へ移る暗転の間に GameFlow が呼ぶ）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Wipe Sequence")]
    public sealed class WipeSequence : MonoBehaviour
    {
        [Header("音")]
        [SerializeField, Tooltip("全滅したときに流す曲。")]
        private AudioClip deathBgm;

        [SerializeField, Min(0f), Tooltip("死亡の曲へ切り替えるまでの秒数。")]
        private float bgmDelay = 1f;

        [SerializeField, Min(0f), Tooltip("死亡の曲へのクロスフェードの秒数。")]
        private float bgmFadeSeconds = 2f;

        [SerializeField, Min(0f), Tooltip("Dead のスナップショットへ移る秒数（SE がこもっていく）。")]
        private float muffleSeconds = 1.2f;

        [Header("スロー")]
        [SerializeField, Range(0.05f, 1f), Tooltip("倒れた瞬間の速さ（timeScale）。")]
        private float slowScale = 0.3f;

        [SerializeField, Min(0f), Tooltip("元の速さへ戻すまでの秒数。")]
        private float slowRecoverSeconds = 1.2f;

        [Header("画面")]
        [SerializeField, Min(0.01f), Tooltip("色が抜けて暗くなりきるまでの秒数。")]
        private float fadeToGraySeconds = 2f;

        [SerializeField, Range(-100f, 0f), Tooltip("暗くなりきったときの彩度。")]
        private float saturation = -80f;

        [SerializeField, Tooltip("暗くなりきったときの露出（EV）。")]
        private float postExposure = -0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("暗くなりきったときの周りの暗さ。")]
        private float vignette = 0.45f;

        [Header("カメラ")]
        [SerializeField, Tooltip("体（腰）からの水平の距離（m）。始まり → 終わり。")]
        private Vector2 cameraDistance = new Vector2(2.2f, 3.4f);

        [SerializeField, Tooltip("体（腰）からの高さ（m）。始まり → 終わり。")]
        private Vector2 cameraHeight = new Vector2(1.2f, 2.6f);

        [SerializeField, Min(0f), Tooltip("体（腰）よりどれだけ下を狙うか（m）。下を狙うほど体は画面の上に映り、下の「力尽きた」の帯に隠れない。")]
        private float cameraAimDrop = 0.9f;

        [SerializeField, Min(0.01f), Tooltip("引き上げきるまでの秒数。")]
        private float cameraRiseSeconds = 4f;

        [SerializeField, Tooltip("体の周りを回る速さ（度/秒）。")]
        private float cameraOrbitSpeed = 8f;

        [Header("文字と進み方")]
        [SerializeField, Min(0f), Tooltip("「力尽きた」を出すまでの秒数。")]
        private float gameOverDelay = 1.8f;

        [SerializeField, Min(0f), Tooltip("キーで飛ばせるようになるまでの秒数。")]
        private float skippableAfter = 3f;

        [SerializeField, Min(0f), Tooltip("放っておいたとき、死後の場所へ送るまでの秒数。")]
        private float autoAdvanceSeconds = 7f;

        // 死亡用のカメラは、パーティーのカメラより優先して映す。
        private const int CameraPriority = 100;

        private Volume volume;
        private VolumeProfile profile;
        private CinemachineCamera deathCamera;
        private UniversalAdditionalCameraData postTarget;
        private bool postPrevious;
        private Transform focus;
        private float orbitYaw;
        private float startedAt;
        private bool skippable;
        private bool skipRequested;

        /// <summary>演出の途中か（始まってから元へ戻すまで）。</summary>
        public bool IsPlaying { get; private set; }

        public AudioClip DeathBgm
        {
            get => deathBgm;
            set => deathBgm = value;
        }

        /// <summary>全滅して演出が始まった。</summary>
        public static event Action Started;

        /// <summary>「力尽きた」を出すころ合い。</summary>
        public static event Action GameOverShown;

        /// <summary>キーで飛ばせるようになった。</summary>
        public static event Action SkipReady;

        /// <summary>演出を終えて元へ戻した（死後の場所へ移る暗転の間）。</summary>
        public static event Action Ended;

        /// <summary>parent の子に作る。初めは何も効かせない。</summary>
        public static WipeSequence Create(Transform parent)
        {
            var holder = new GameObject("[Wipe Sequence]");
            holder.transform.SetParent(parent, false);
            var sequence = holder.AddComponent<WipeSequence>();
            sequence.Build();
            return sequence;
        }

        private void Build()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Wipe (generated)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Add<ColorAdjustments>();
            profile.Add<Vignette>().smoothness.Override(0.5f);

            var volumeHolder = new GameObject("Wipe Volume");
            volumeHolder.transform.SetParent(transform, false);
            volume = volumeHolder.AddComponent<Volume>();
            volume.isGlobal = true;
            // 階の共通の Volume（0）と層の色味（1）より上に重ねる。
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = profile;

            var cameraHolder = new GameObject("Wipe Camera");
            cameraHolder.transform.SetParent(transform, false);
            deathCamera = cameraHolder.AddComponent<CinemachineCamera>();
            deathCamera.Priority = CameraPriority;
            cameraHolder.SetActive(false);
        }

        private void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }

        /// <summary>
        /// 演出を回す。fallen は最後に倒れた人（カメラが映す）。飛ばされるか時間が来たら終わる（元へは戻さない。<see cref="Restore"/> で戻す）。
        /// </summary>
        public IEnumerator Play(PartyMember fallen)
        {
            IsPlaying = true;
            skippable = false;
            skipRequested = false;
            startedAt = Time.unscaledTime;
            Started?.Invoke();

            GameAudio audio = GameAudio.Instance;
            if (audio != null) audio.TransitionTo(AudioSnapshotId.Dead, muffleSeconds);

            // 値は始めるたびに入れ直す（Inspector で調節した値がそのまま効くように）。
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.saturation.Override(saturation);
                color.postExposure.Override(postExposure);
            }
            if (profile.TryGet(out Vignette vig)) vig.intensity.Override(vignette);

            Time.timeScale = slowScale;
            EnablePostProcessing();
            StartCamera(fallen);

            bool bgmStarted = false;
            bool shown = false;
            while (true)
            {
                float t = Time.unscaledTime - startedAt;

                // スローは始めの一瞬だけ。滑らかに元の速さへ戻す。
                Time.timeScale = Mathf.Lerp(slowScale, 1f, Mathf.SmoothStep(0f, 1f, slowRecoverSeconds > 0f ? t / slowRecoverSeconds : 1f));
                volume.weight = Mathf.SmoothStep(0f, 1f, t / fadeToGraySeconds);
                UpdateCamera(t);

                if (!bgmStarted && t >= bgmDelay)
                {
                    bgmStarted = true;
                    if (audio != null && deathBgm != null) audio.PlayBgm(deathBgm, bgmFadeSeconds);
                }

                if (!shown && t >= gameOverDelay)
                {
                    shown = true;
                    GameOverShown?.Invoke();
                }

                if (!skippable && t >= skippableAfter)
                {
                    skippable = true;
                    SkipReady?.Invoke();
                }

                if (skippable && (skipRequested || AnyPressed())) break;
                if (t >= autoAdvanceSeconds) break;
                yield return null;
            }
        }

        /// <summary>演出を飛ばす。飛ばせるようになっていれば次のフレームで終わる。それより前に呼んでも覚えておかない（キーと同じ）。</summary>
        public void Skip()
        {
            if (skippable) skipRequested = true;
        }

        /// <summary>スロー・音・画面・カメラを元へ戻す。画面が暗い間（シーンを移る直前）に呼ぶ。</summary>
        public void Restore()
        {
            if (!IsPlaying) return;
            IsPlaying = false;

            Time.timeScale = 1f;
            volume.weight = 0f;
            if (postTarget != null) postTarget.renderPostProcessing = postPrevious;
            postTarget = null;

            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                audio.StopBgm(0f);
                audio.TransitionTo(AudioSnapshotId.Default, 0f);
            }

            StopCamera();
            Ended?.Invoke();
        }

        private void EnablePostProcessing()
        {
            Camera main = Camera.main;
            if (main == null) return;
            postTarget = main.GetUniversalAdditionalCameraData();
            postPrevious = postTarget.renderPostProcessing;
            postTarget.renderPostProcessing = true;
        }

        private void StartCamera(PartyMember fallen)
        {
            focus = null;
            if (fallen != null)
            {
                var animator = fallen.GetComponentInChildren<Animator>();
                focus = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                if (focus == null) focus = fallen.transform;
            }
            if (focus == null) return;

            // 今見ている向きの後ろから始める（切り替えた瞬間に反対側へ回り込まないように）。
            Camera main = Camera.main;
            Vector3 back = main != null ? main.transform.position - focus.position : -fallen.transform.forward;
            back.y = 0f;
            orbitYaw = back.sqrMagnitude > 1e-4f ? Mathf.Atan2(back.x, back.z) * Mathf.Rad2Deg : fallen.transform.eulerAngles.y + 180f;

            UpdateCamera(0f);
            deathCamera.gameObject.SetActive(true);
            deathCamera.PreviousStateIsValid = false;
        }

        private void UpdateCamera(float t)
        {
            if (focus == null || deathCamera == null) return;

            float k = Mathf.SmoothStep(0f, 1f, t / cameraRiseSeconds);
            float yaw = orbitYaw + cameraOrbitSpeed * t;
            float distance = Mathf.Lerp(cameraDistance.x, cameraDistance.y, k);
            float height = Mathf.Lerp(cameraHeight.x, cameraHeight.y, k);

            Vector3 target = focus.position;
            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance + Vector3.up * height;

            // 壁や天井に埋まらないよう、体から置きたい所へレイを飛ばして手前で止める。
            const float margin = 0.3f;
            if (Physics.SphereCast(target, 0.2f, offset.normalized, out RaycastHit hit, offset.magnitude, BlockMask, QueryTriggerInteraction.Ignore))
            {
                offset = offset.normalized * Mathf.Max(0.5f, hit.distance - margin);
            }

            Transform t2 = deathCamera.transform;
            t2.position = target + offset;
            t2.rotation = Quaternion.LookRotation(target - Vector3.up * cameraAimDrop - t2.position, Vector3.up);
        }

        private void StopCamera()
        {
            if (deathCamera == null || !deathCamera.gameObject.activeSelf) return;

            // 戻すときはブレンドせずに切る（画面は暗い）。明るくなったときに体の上から普段の視点へ滑ってこないように。
            deathCamera.gameObject.SetActive(false);
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (brain != null) brain.ResetState();
            focus = null;
        }

        /// <summary>カメラを止める物。パーティーの体（Ally）・死体（Debris）・壊せる草花（Breakable）は素通しする。</summary>
        private static int BlockMask
        {
            get
            {
                int mask = ~0;
                foreach (string layer in new[] { "Ally", "Debris", "Breakable", "Enemy" })
                {
                    int index = LayerMask.NameToLayer(layer);
                    if (index >= 0) mask &= ~(1 << index);
                }
                return mask;
            }
        }

        private static bool AnyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            Gamepad pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }
    }
}
