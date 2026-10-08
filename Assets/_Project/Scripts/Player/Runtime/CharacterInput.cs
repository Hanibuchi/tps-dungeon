using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player
{
    /// <summary>
    /// 移動・視点・ジャンプ・ダッシュの入力の値。Starter Assets の StarterAssetsInputs と同じ役で、
    /// PlayerInput（SendMessages）から OnMove などを受け取る。読むのは先頭のキャラの <see cref="CharacterMotor"/>。
    /// パーティーの操作台（PlayerInput と同じ GameObject）に 1 つだけ付ける。キャラ側には付けない。
    /// GamePauser は名前で MoveInput などを呼んで、止める直前の値を離させる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Input")]
    public sealed class CharacterInput : MonoBehaviour
    {
        [Header("入力の値")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("移動")]
        [Tooltip("スティックの倒し具合で歩く速さを変えるか。")]
        public bool analogMovement;

        [Header("カーソル")]
        [Tooltip("ウィンドウにフォーカスが戻ったらカーソルを固定するか。")]
        public bool cursorLocked = true;

        [Tooltip("マウスの動きで視点を回すか。")]
        public bool cursorInputForLook = true;

        [Tooltip("カメラの回転感度。")]
        public float lookSensitivity = 1f;

        private PlayerInput playerInput;

        /// <summary>今の操作がキーボードとマウスか。マウスの視点移動は deltaTime を掛けないので、CharacterMotor が見る。</summary>
        public bool IsCurrentDeviceMouse
        {
            get
            {
                if (playerInput == null) playerInput = GetComponent<PlayerInput>();
                return playerInput != null && playerInput.currentControlScheme == "KeyboardMouse";
            }
        }

        public void OnMove(InputValue value) => MoveInput(value.Get<Vector2>());

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook) LookInput(value.Get<Vector2>());
        }

        public void OnJump(InputValue value) => JumpInput(value.isPressed);

        public void OnSprint(InputValue value) => SprintInput(value.isPressed);

        public void MoveInput(Vector2 newMoveDirection) => move = newMoveDirection;

        public void LookInput(Vector2 newLookDirection) => look = newLookDirection * lookSensitivity;

        public void JumpInput(bool newJumpState) => jump = newJumpState;

        public void SprintInput(bool newSprintState) => sprint = newSprintState;

        private void OnApplicationFocus(bool hasFocus) => SetCursorState(cursorLocked);

        private static void SetCursorState(bool locked) => Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
