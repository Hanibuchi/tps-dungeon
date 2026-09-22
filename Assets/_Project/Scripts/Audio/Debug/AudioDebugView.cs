using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Audio.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Audio.DebugTools
{
    /// <summary>
    /// 音まわりの配線をキー操作で鳴らし分けて、耳で確かめるためのビュー。
    /// BGM のクロスフェード、SE の距離減衰、スナップショット 3 種の違いは
    /// 実際に鳴らさないと判断できないので、その導線をここにまとめてある。
    /// プレイヤーや UI が揃うまでの確認手段なので、製品版のシーンには残さない想定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Audio Debug View")]
    public sealed class AudioDebugView : MonoBehaviour
    {
        [Header("クリップ")]
        [SerializeField, Tooltip("1 本目の BGM。")]
        private AudioClip bgmClipA;

        [SerializeField, Tooltip("2 本目の BGM。PlayBgm は同じクリップを無視するので、クロスフェードの確認には 2 本要る。")]
        private AudioClip bgmClipB;

        [SerializeField, Tooltip("位置を持たない SE。UI 音の確認用。")]
        private AudioClip se2dClip;

        [SerializeField, Tooltip("位置を持つ SE。距離減衰の確認用。")]
        private AudioClip se3dClip;

        [SerializeField, Tooltip("往復エミッタが連射する SE。")]
        private AudioClip seFootstepClip;

        [Header("3D エミッタ")]
        [SerializeField, Tooltip("距離の基準。未設定ならシーンの AudioListener から拾う。")]
        private Transform listenerAnchor;

        [SerializeField, Tooltip("往復させるマーカー。")]
        private Transform emitter;

        [SerializeField, Min(0f), Tooltip("ここまでは減衰しない距離。GameAudio 側の AudioSource の minDistance と揃えること。")]
        private float minDistance = 1f;

        [SerializeField, Min(0.01f), Tooltip("ここで無音になる距離。GameAudio 側の maxDistance と揃えること。")]
        private float maxDistance = 30f;

        [SerializeField, Min(0.01f), Tooltip("エミッタが動く速さ（m/秒）。")]
        private float emitterSpeed = 8f;

        [SerializeField, Min(0.05f), Tooltip("エミッタが SE を鳴らす間隔（秒）。")]
        private float fireInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip("「近くで鳴らす」キーが使う距離。")]
        private float nearDistance = 2f;

        [SerializeField, Min(0f), Tooltip("「遠くで鳴らす」キーが使う距離。")]
        private float farDistance = 28f;

        [Header("キー割り当て")]
        [SerializeField, Tooltip("BGM A を流す。")]
        private Key bgmAKey = Key.Digit1;

        [SerializeField, Tooltip("BGM B へクロスフェードする。")]
        private Key bgmBKey = Key.Digit2;

        [SerializeField, Tooltip("BGM を止める。")]
        private Key bgmStopKey = Key.Digit3;

        [SerializeField, Tooltip("2D の SE を鳴らす。")]
        private Key se2dKey = Key.Digit4;

        [SerializeField, Tooltip("近い位置で 3D の SE を鳴らす。")]
        private Key seNearKey = Key.Digit5;

        [SerializeField, Tooltip("遠い位置で 3D の SE を鳴らす。")]
        private Key seFarKey = Key.Digit6;

        [SerializeField, Tooltip("往復エミッタを切り替える。")]
        private Key emitterKey = Key.Digit7;

        [SerializeField, Tooltip("Default スナップショットへ。")]
        private Key snapshotDefaultKey = Key.Digit8;

        [SerializeField, Tooltip("Dungeon スナップショットへ。")]
        private Key snapshotDungeonKey = Key.Digit9;

        [SerializeField, Tooltip("Paused スナップショットへ。")]
        private Key snapshotPausedKey = Key.Digit0;

        [SerializeField, Tooltip("音量を既定値に戻す。")]
        private Key resetVolumeKey = Key.R;

        [SerializeField, Tooltip("保存された音量を消す。")]
        private Key clearSavedKey = Key.C;

        [SerializeField, Tooltip("オーバーレイの表示を切り替える。")]
        private Key toggleOverlayKey = Key.H;

        [Header("表示")]
        [SerializeField, Tooltip("画面左上に状態と操作を表示する。")]
        private bool showOverlay = true;

        [SerializeField, Tooltip("シーンビューにリスナーの到達範囲とエミッタを描く。")]
        private bool drawGizmos = true;

        private AudioSettingsPanel panel;
        private bool emitterRunning;
        private float emitterDistance;
        private float nextFireTime;

        /// <summary>
        /// AudioSource の Linear rolloff と同じ式。オーバーレイに出す「約 N%」の根拠。
        /// 耳で聞いた印象と数字を突き合わせられるように、表示側でも同じ計算をする。
        /// </summary>
        public static float LinearRolloffFactor(float distance, float minDistance, float maxDistance)
        {
            if (maxDistance <= minDistance) return distance <= minDistance ? 1f : 0f;
            if (distance <= minDistance) return 1f;
            if (distance >= maxDistance) return 0f;
            return 1f - (distance - minDistance) / (maxDistance - minDistance);
        }

        private void Start()
        {
            // 距離の基準はここで一度だけ決める。毎フレーム探しにいかないように。
            if (listenerAnchor == null)
            {
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null) listenerAnchor = listener.transform;
            }

            // GameAudio はプレハブから自動で立ち上がる。立っていないなら配線が済んでいない。
            if (GameAudio.Instance == null)
            {
                Debug.LogError("GameAudio が起動していない。Tools > TPS Dungeon > Audio のメニューを上から実行すること。", this);
                return;
            }

            // 設定パネルは GameAudio プレハブの子なので、この時点では必ず立っている。
            panel = FindFirstObjectByType<AudioSettingsPanel>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[toggleOverlayKey].wasPressedThisFrame) showOverlay = !showOverlay;
            if (keyboard[emitterKey].wasPressedThisFrame) emitterRunning = !emitterRunning;

            // Instance はキャッシュしない。掴んでしまうと未起動から復帰できなくなる。
            GameAudio audio = GameAudio.Instance;
            if (audio == null) return;

            // PlayBgm は clip が null だと停止扱いになる。未設定のまま押して
            // 「再生キーなのに止まる」と誤解しないよう、ここで弾いておく。
            if (keyboard[bgmAKey].wasPressedThisFrame && bgmClipA != null) audio.PlayBgm(bgmClipA);
            if (keyboard[bgmBKey].wasPressedThisFrame && bgmClipB != null) audio.PlayBgm(bgmClipB);
            if (keyboard[bgmStopKey].wasPressedThisFrame) audio.StopBgm();

            if (keyboard[se2dKey].wasPressedThisFrame) audio.PlaySe(se2dClip);
            if (keyboard[seNearKey].wasPressedThisFrame) PlayAtDistance(audio, nearDistance);
            if (keyboard[seFarKey].wasPressedThisFrame) PlayAtDistance(audio, farDistance);

            if (keyboard[snapshotDefaultKey].wasPressedThisFrame) audio.TransitionTo(AudioSnapshotId.Default);
            if (keyboard[snapshotDungeonKey].wasPressedThisFrame) audio.TransitionTo(AudioSnapshotId.Dungeon);
            if (keyboard[snapshotPausedKey].wasPressedThisFrame) audio.TransitionTo(AudioSnapshotId.Paused);

            if (keyboard[resetVolumeKey].wasPressedThisFrame) audio.Volumes?.ResetToDefaults();
            if (keyboard[clearSavedKey].wasPressedThisFrame) AudioSettingsStore.Clear();

            UpdateEmitter(audio);
        }

        /// <summary>リスナーから一定距離だけ離れた場所で 3D の SE を鳴らす。</summary>
        private void PlayAtDistance(GameAudio audio, float distance)
        {
            Vector3 origin = listenerAnchor != null ? listenerAnchor.position : Vector3.zero;
            audio.PlaySeAt(se3dClip, origin + Vector3.right * distance);
        }

        /// <summary>
        /// エミッタを往復させながら SE を連射する。
        /// PlaySeAt は鳴らした時点の座標を音源に焼き込むので、鳴っている音はエミッタに追従しない。
        /// だから「動く音源」ではなく「距離を変えながら短い音を繰り返す」形にしてある。
        /// </summary>
        private void UpdateEmitter(GameAudio audio)
        {
            if (!emitterRunning) return;

            if (listenerAnchor == null) return;

            // BGM のフェードが unscaledDeltaTime で動くので、こちらも時間の尺度を合わせておく。
            emitterDistance = Mathf.PingPong(Time.unscaledTime * emitterSpeed, maxDistance);
            Vector3 position = listenerAnchor.position + Vector3.right * emitterDistance;
            if (emitter != null) emitter.position = position;

            if (Time.unscaledTime < nextFireTime) return;

            nextFireTime = Time.unscaledTime + fireInterval;
            audio.PlaySeAt(seFootstepClip, position);
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            GUILayout.BeginArea(new Rect(12f, 12f, 480f, 460f), GUI.skin.box);

            GameAudio audio = GameAudio.Instance;
            if (audio == null)
            {
                GUILayout.Label("<b>GameAudio: 未起動</b>", style);
                GUILayout.Label("Tools > TPS Dungeon > Audio の 3 メニューを上から実行してから再生すること。", style);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("<b>GameAudio: 起動済み</b>", style);
            GUILayout.Label($"BGM: {(audio.CurrentBgm != null ? audio.CurrentBgm.name : "停止中")}", style);
            GUILayout.Label($"スナップショット: <b>{audio.CurrentSnapshot}</b>", style);
            GUILayout.Label($"設定パネル: {PanelState()}", style);
            GUILayout.Label(VolumeLine(audio), style);
            GUILayout.Label(EmitterLine(), style);

            string missing = MissingClips();
            if (missing != null) GUILayout.Label($"<b>未設定のクリップ:</b> {missing}", style);

            GUILayout.Space(6f);
            GUILayout.Label($"[{bgmAKey}] BGM A 再生　[{bgmBKey}] BGM B へクロスフェード　[{bgmStopKey}] 停止", style);
            GUILayout.Label($"[{se2dKey}] SE 2D　[{seNearKey}] SE 3D 近 {nearDistance:0}m　[{seFarKey}] SE 3D 遠 {farDistance:0}m", style);
            GUILayout.Label($"[{emitterKey}] 往復エミッタ（足音を {fireInterval:0.0} 秒ごとに連射）", style);
            GUILayout.Label($"[{snapshotDefaultKey}] Default　[{snapshotDungeonKey}] Dungeon 残響　[{snapshotPausedKey}] Paused こもる", style);
            GUILayout.Label($"[{resetVolumeKey}] 音量を既定値に戻す", style);
            GUILayout.Label($"[{clearSavedKey}] 保存を全消し（メモリ上の値は変わらないので次回再生から効く）", style);
            GUILayout.Label($"[{toggleOverlayKey}] この表示を消す", style);
            GUILayout.Label("[Escape] 音量設定パネル（開くと Paused / 閉じると Dungeon になる）", style);

            GUILayout.Space(6f);
            GUILayout.Label("エディタのフォーカスが外れると音が止まる（Run In Background: off）", style);

            GUILayout.EndArea();
        }

        private string PanelState()
        {
            if (panel == null) return "見つからない";
            return panel.IsOpen ? "開" : "閉";
        }

        private static string VolumeLine(GameAudio audio)
        {
            AudioVolumeController volumes = audio.Volumes;
            if (volumes == null) return "音量: 未初期化";

            return "音量  " + Describe(volumes, AudioChannel.Master, "Master")
                   + " / " + Describe(volumes, AudioChannel.Bgm, "BGM")
                   + " / " + Describe(volumes, AudioChannel.Se, "SE");
        }

        private static string Describe(AudioVolumeController volumes, AudioChannel channel, string label)
        {
            float value = volumes.GetVolume(channel);
            return $"{label} {Mathf.RoundToInt(value * 100f)}% ({AudioVolumeMath.ToDecibels(value):0.0} dB)";
        }

        private string EmitterLine()
        {
            if (!emitterRunning) return "3D エミッタ: OFF";

            float factor = LinearRolloffFactor(emitterDistance, minDistance, maxDistance);
            return $"3D エミッタ: ON　距離 {emitterDistance:0.0}m → 約 {Mathf.RoundToInt(factor * 100f)}%";
        }

        /// <summary>
        /// 未設定のクリップを並べる。PlaySe も PlaySeAt も clip が null だと黙って返るので、
        /// これが無いと「押したのに鳴らない」の原因が画面から分からなくなる。
        /// </summary>
        private string MissingClips()
        {
            var names = new System.Collections.Generic.List<string>();
            if (bgmClipA == null) names.Add("BGM A");
            if (bgmClipB == null) names.Add("BGM B");
            if (se2dClip == null) names.Add("SE 2D");
            if (se3dClip == null) names.Add("SE 3D");
            if (seFootstepClip == null) names.Add("足音");

            return names.Count == 0 ? null : string.Join(", ", names);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // ここでは Anchor プロパティを使わない。編集中の描画のたびにシーンを走査したくない。
            if (!drawGizmos || listenerAnchor == null) return;

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(listenerAnchor.position, maxDistance);

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(listenerAnchor.position, minDistance);

            if (emitter == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
            Gizmos.DrawLine(listenerAnchor.position, emitter.position);
            Gizmos.DrawWireSphere(emitter.position, 0.6f);
        }
#endif
    }
}
