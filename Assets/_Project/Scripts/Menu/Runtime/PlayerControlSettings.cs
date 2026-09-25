using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Menu
{
    /// <summary>
    /// 保存してある操作の設定（マウス感度・上下反転・なめらかスクロールの無視・キー設定）をプレイヤーの入力に当てる窓口。
    /// プレイヤーのルート（PlayerInput と同じ GameObject）に付ける。設定画面はここ経由で値を変える。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Control Settings")]
    public sealed class PlayerControlSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("設定を当てる PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("視点操作のアクション名。")]
        private string lookActionName = "Look";

        [SerializeField, Tooltip("なめらかスクロールの無視を当てるホットバー。未設定ならこの GameObject から探す。")]
        private PlayerHotbar hotbar;

        private bool loaded;

        public float MouseSensitivity { get; private set; } = LookSettings.DefaultSensitivity;

        public bool InvertY { get; private set; }

        /// <summary>マウスホイールを 1 ノッチにつき 1 枠ずつ送るか（Mos などのなめらかスクロール向け）。</summary>
        public bool DiscreteScroll { get; private set; }

        /// <summary>キー設定を書き換える対象。PlayerInput が無ければ null。</summary>
        public InputActionAsset Actions => playerInput != null ? playerInput.actions : null;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
            hotbar = GetComponent<PlayerHotbar>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (hotbar == null) hotbar = GetComponent<PlayerHotbar>();
        }

        // PlayerInput は OnEnable で自分用のアクションを用意し直すことがあるので、全員の OnEnable が済んだ後で当てる。
        private void Start()
        {
            Load();
        }

        /// <summary>保存から読み直して当てる。何度呼んでもよい。</summary>
        public void Load()
        {
            InputActionAsset actions = Actions;
            if (actions == null)
            {
                Debug.LogWarning("PlayerInput が無いので操作の設定を当てられない", this);
                return;
            }

            // エディタではアセットの上書きが前回の再生から残っていることがある。まっさらにしてから読む。
            actions.RemoveAllBindingOverrides();
            string json = ControlSettingsStore.LoadBindingOverrides();
            if (!string.IsNullOrEmpty(json)) actions.LoadBindingOverridesFromJson(json);

            MouseSensitivity = ControlSettingsStore.LoadMouseSensitivity();
            InvertY = ControlSettingsStore.LoadInvertY();
            ApplyLook();
            DiscreteScroll = ControlSettingsStore.LoadDiscreteScroll();
            ApplyScroll();
            loaded = true;
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = LookSettings.ClampSensitivity(value);
            ControlSettingsStore.SaveMouseSensitivity(MouseSensitivity);
            ApplyLook();
        }

        public void SetInvertY(bool value)
        {
            InvertY = value;
            ControlSettingsStore.SaveInvertY(value);
            ApplyLook();
        }

        public void SetDiscreteScroll(bool value)
        {
            DiscreteScroll = value;
            ControlSettingsStore.SaveDiscreteScroll(value);
            ApplyScroll();
        }

        /// <summary>操作タブの設定（感度・上下反転・なめらかスクロールの無視）を既定に戻す。</summary>
        public void ResetControls()
        {
            SetMouseSensitivity(LookSettings.DefaultSensitivity);
            SetInvertY(false);
            SetDiscreteScroll(false);
        }

        /// <summary>今のキー設定を覚える。ディスクへの書き出しは <see cref="Flush"/>。</summary>
        public void SaveBindings()
        {
            InputActionAsset actions = Actions;
            if (actions == null) return;
            ControlSettingsStore.SaveBindingOverrides(actions.SaveBindingOverridesAsJson());
        }

        /// <summary>ディスクに書き出す。設定画面を閉じたときに呼ぶ。</summary>
        public void Flush()
        {
            if (loaded) ControlSettingsStore.Flush();
        }

        private void ApplyScroll()
        {
            if (hotbar != null) hotbar.DiscreteScroll = DiscreteScroll;
        }

        private void ApplyLook()
        {
            InputAction look = Actions != null ? Actions.FindAction(lookActionName) : null;
            if (!LookSettings.Apply(look, MouseSensitivity, InvertY))
            {
                Debug.LogWarning($"アクション '{lookActionName}' にマウス用のバインドが無いので感度を当てられない", this);
            }
        }
    }
}
