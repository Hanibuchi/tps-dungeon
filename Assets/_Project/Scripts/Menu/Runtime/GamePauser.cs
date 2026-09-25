using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Menu
{
    /// <summary>
    /// 画面を開いている間ゲームを止める手順。ポーズメニューとインベントリ画面で共有する。
    /// - timeScale を 0 にする
    /// - PlayerInput を UI マップに切り替えて、移動・視点・ホットバーなどの入力を止める
    ///   （マウスの視点移動は deltaTime を掛けないので、timeScale だけでは止まらない）
    /// - キャラが握っている入力を離させる
    /// - 大きな地図を閉じる
    /// - カーソルの固定を外す（<see cref="KeepCursorFree"/> を LateUpdate で呼び続ける）
    /// - 音を Paused スナップショットでこもらせる
    /// </summary>
    public sealed class GamePauser
    {
        private readonly PlayerInput playerInput;
        private readonly PlayerControlSettings controls;
        private readonly PlayerMapToggle mapToggle;
        private readonly string gameplayMapName;
        private readonly string menuMapName;

        private float timeScaleBeforePause = 1f;
        private AudioSnapshotId snapshotBeforePause = AudioSnapshotId.Dungeon;

        public GamePauser(PlayerInput playerInput, PlayerControlSettings controls, PlayerMapToggle mapToggle,
            string gameplayMapName, string menuMapName)
        {
            this.playerInput = playerInput;
            this.controls = controls;
            this.mapToggle = mapToggle;
            this.gameplayMapName = gameplayMapName;
            this.menuMapName = menuMapName;
        }

        /// <summary>止めているか。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>ゲームを止める。既に止めていれば何もしない。</summary>
        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;

            if (mapToggle != null) mapToggle.SetOpen(false);

            if (playerInput != null)
            {
                playerInput.SwitchCurrentActionMap(menuMapName);
                ReleaseCharacterInput();
            }

            UnlockCursor();

            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                snapshotBeforePause = audio.CurrentSnapshot;
                audio.TransitionTo(AudioSnapshotId.Paused);
            }
        }

        /// <summary>ゲームに戻る。止めていなければ何もしない。変えた設定はここで保存する。</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            controls?.Flush();
            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                audio.Volumes?.Flush();
                audio.TransitionTo(snapshotBeforePause);
            }

            if (playerInput != null) playerInput.SwitchCurrentActionMap(gameplayMapName);
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Starter Assets はウィンドウにフォーカスが戻るとカーソルを固定し直すので、止めている間は LateUpdate で外し続ける。
        /// </summary>
        public void KeepCursorFree()
        {
            if (IsPaused && Cursor.lockState != CursorLockMode.None) UnlockCursor();
        }

        /// <summary>
        /// キャラが握っている入力を離させる。マップを切り替えても、止める直前の移動や視点の値が残り、
        /// 再開した瞬間に歩き出すことがあるため。
        /// StarterAssetsInputs の公開メソッドを名前で呼ぶので、Starter Assets のアセンブリには依存しない。
        /// </summary>
        private void ReleaseCharacterInput()
        {
            GameObject target = playerInput.gameObject;
            target.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("LookInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("JumpInput", false, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
