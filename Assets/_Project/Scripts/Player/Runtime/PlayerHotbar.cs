using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player
{
    /// <summary>
    /// 画面下のホットバー（アイテム欄）のどの枠を選んでいるか。
    /// マウスホイール（ゲームパッドは LB/RB）で隣へ、数字キーで直接選ぶ。
    /// なめらかスクロールを無視する設定（<see cref="DiscreteScroll"/>）では、ホイール 1 ノッチで 1 枠送る。
    /// 枠の中身はインベントリの先頭の枠（PlayerInventory）が持ち、ここは選択位置だけを持つ。
    /// プレイヤーのルート（PlayerInput と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Hotbar")]
    public sealed class PlayerHotbar : MonoBehaviour
    {
        public const int SlotCount = 4;

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("隣の枠へ送るアクション名。正の値で右、負の値で左。")]
        private string scrollActionName = "HotbarScroll";

        [SerializeField, Tooltip("枠を直接選ぶアクション名の頭。末尾に 1 から始まる枠番号が付く。")]
        private string slotActionPrefix = "HotbarSlot";

        [SerializeField, Min(0f), Tooltip("なめらかスクロールを無視するとき、ホイールの値がこれより長く途切れたら次の回しとみなす（秒）。")]
        private float scrollGestureGap = ScrollGesture.DefaultGap;

        private InputAction scrollAction;
        private readonly InputAction[] slotActions = new InputAction[SlotCount];
        private ScrollGesture scrollGesture;

        /// <summary>
        /// マウスホイールを 1 ノッチにつき 1 枠ずつ送るか（なめらかスクロールを無視する）。
        /// オフなら値が来るたびに 1 枠送る。設定画面（PlayerControlSettings）が切り替える。
        /// </summary>
        public bool DiscreteScroll { get; set; }

        /// <summary>マウスホイールの向きを逆にするか。ゲームパッドの LB/RB には効かない。設定画面が切り替える。</summary>
        public bool InvertScroll { get; set; }

        /// <summary>選んでいる枠（0 始まり）。</summary>
        public int SelectedIndex { get; private set; }

        public event Action<PlayerHotbar> Changed;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            scrollGesture = new ScrollGesture(scrollGestureGap);
        }

        private void OnEnable()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning("PlayerInput が無いのでホットバーを切り替えられない", this);
                return;
            }

            scrollAction = FindAction(scrollActionName);
            for (int i = 0; i < SlotCount; i++) slotActions[i] = FindAction(slotActionPrefix + (i + 1));
        }

        private void OnDisable()
        {
            scrollAction = null;
            Array.Clear(slotActions, 0, slotActions.Length);
        }

        private void Update()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slotActions[i] != null && slotActions[i].WasPressedThisFrame())
                {
                    Select(i);
                    return;
                }
            }

            if (scrollAction == null) return;

            // ホイールは 1 ノッチで 120 などの大きな値が来るので、向きだけ見る。
            // なめらかスクロールを無視するときは、同じ値が続いて performed が来ないフレームも回しの続きなので毎フレーム読む。
            float value = scrollAction.ReadValue<float>();
            bool fromMouse = scrollAction.activeControl?.device is Mouse;
            if (InvertScroll && fromMouse) value = -value;

            if (DiscreteScroll && fromMouse)
            {
                Cycle(scrollGesture.Step(value, Time.unscaledTime));
            }
            else if (scrollAction.WasPerformedThisFrame())
            {
                Cycle(ScrollStep(value));
            }
        }

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, SlotCount - 1);
            if (index == SelectedIndex) return;

            SelectedIndex = index;
            Changed?.Invoke(this);
        }

        /// <summary>step 枠ぶん隣へ送る。端を越えたら反対の端に回り込む。</summary>
        public void Cycle(int step)
        {
            if (step == 0) return;
            Select(Wrap(SelectedIndex + step, SlotCount));
        }

        /// <summary>
        /// アクションの値を 1 枠ぶんの向きにする。正なら右、負なら左。
        /// ホイールは手前に回すと右へ送りたいので、バインド側で invert を掛けてある。
        /// </summary>
        public static int ScrollStep(float value)
        {
            if (value > 0f) return 1;
            if (value < 0f) return -1;
            return 0;
        }

        public static int Wrap(int index, int count)
        {
            int r = index % count;
            return r < 0 ? r + count : r;
        }

        private InputAction FindAction(string actionName)
        {
            var action = playerInput.actions.FindAction(actionName);
            if (action == null) Debug.LogWarning($"アクション '{actionName}' が {playerInput.actions.name} に無い", this);
            return action;
        }
    }
}
