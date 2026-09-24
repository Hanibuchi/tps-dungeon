using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player
{
    /// <summary>
    /// マップキー（M / ゲームパッドの Select）で大きな地図を開け閉めする。開いているかどうかだけを持ち、描くのは HUD。
    /// プレイヤーのルート（PlayerInput と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Map Toggle")]
    public sealed class PlayerMapToggle : MonoBehaviour
    {
        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("PlayerInput のアクションアセット内の、地図を開け閉めするアクション名。")]
        private string mapActionName = "Map";

        private InputAction mapAction;

        /// <summary>大きな地図を開いているか。</summary>
        public bool IsOpen { get; private set; }

        public event Action<PlayerMapToggle> Changed;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning("PlayerInput が無いので地図を開けない", this);
                return;
            }

            mapAction = playerInput.actions.FindAction(mapActionName);
            if (mapAction == null) Debug.LogWarning($"アクション '{mapActionName}' が {playerInput.actions.name} に無い", this);
        }

        private void OnDisable()
        {
            mapAction = null;
            SetOpen(false);
        }

        private void Update()
        {
            if (mapAction != null && mapAction.WasPressedThisFrame()) Toggle();
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;

            IsOpen = open;
            Changed?.Invoke(this);
        }
    }
}
