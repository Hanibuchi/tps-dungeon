using System.Collections.Generic;
using TpsDungeon.Interaction;
using TpsDungeon.Items;
using TpsDungeon.Player;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// Tab（ゲームパッドは Y）で開くインベントリ画面。開いている間はポーズと同じくゲームを止める（<see cref="GamePauser"/>）。
    /// Inventory.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    ///
    /// 操作（マウス）:
    /// - ドラッグで枠から枠へ移す（埋まっていれば入れ替え）。パネルの外で離すと足元に捨てる。
    /// - クイック移動キー（既定 Shift）＋クリックで、バッグ ⇔ ホットバーの空き枠へ移す。
    /// - 枠に合わせて捨てるキー（既定 O）を押すと、足元に捨てる。
    /// - 枠に合わせると右の情報欄にそのアイテムの説明が出る。
    /// 開閉キー（Player/Inventory）か Esc（UI/Cancel）で閉じる。
    /// 下の操作案内は、クイック移動キーを押している間だけ「クリックで移動」に変わる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Inventory Screen")]
    public sealed class InventoryScreen : MonoBehaviour
    {
        private const string DragSourceClass = "slot--drag-source";
        private const string DropTargetClass = "slot--drop-target";
        private const string DeniedClass = "slot--denied";
        private const string DetailsEmptyClass = "inventory__details--empty";

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定なら親から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("持ち物。未設定なら親から探す。")]
        private PlayerInventory inventory;

        [SerializeField, Tooltip("ホットバーのどの枠を選んでいるか。未設定なら親から探す。")]
        private PlayerHotbar hotbar;

        [SerializeField, Tooltip("閉じるときに設定を保存する相手。未設定なら親から探す。")]
        private PlayerControlSettings controls;

        [SerializeField, Tooltip("開くときに閉じる大きな地図。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("開け閉めするアクション（ゲーム中のマップ側）。開いている間もこれだけは効かせる。")]
        private string toggleActionName = "Player/Inventory";

        [SerializeField, Tooltip("閉じるアクション（UI マップ側）。")]
        private string cancelActionName = "UI/Cancel";

        [SerializeField, Tooltip("押しながらクリックでホットバーとバッグの間を移すアクション（UI マップ側）。")]
        private string quickMoveActionName = "UI/QuickMove";

        [SerializeField, Tooltip("合わせている枠のものを捨てるアクション（UI マップ側）。")]
        private string dropActionName = "UI/Drop";

        [SerializeField, Tooltip("ゲーム中に使うアクションマップ。")]
        private string gameplayMapName = "Player";

        [SerializeField, Tooltip("開いている間に使うアクションマップ。")]
        private string menuMapName = "UI";


        private UIDocument document;
        private VisualElement root;
        private VisualElement scrim;
        private VisualElement panel;
        private VisualElement bagContainer;
        private VisualElement hotbarContainer;
        private VisualElement details;
        private VisualElement detailsIcon;
        private Label detailsTitle;
        private Label detailsBody;
        private Label hint;
        private VisualElement ghost;
        private readonly List<VisualElement> slots = new List<VisualElement>();

        private GamePauser pauser;

        // 再生中にスクリプトを再コンパイルすると、シリアライズされないこのフィールドだけ消えて Awake も呼ばれ直さない。
        // そのときも止まらないよう、使うときに作る。
        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);
        private InputAction toggleAction;
        private InputAction cancelAction;
        private InputAction quickMoveAction;
        private InputAction dropAction;

        private int openedFrame = -1;
        private int hoverIndex = -1;
        private int dragFrom = -1;
        private int dragPointerId = -1;
        private int dropTarget = -1;

        private bool quickMoveHintShown;

        /// <summary>開いているか。</summary>
        public bool IsOpen => pauser != null && pauser.IsPaused;

        private void Reset()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            inventory = GetComponentInParent<PlayerInventory>();
            hotbar = GetComponentInParent<PlayerHotbar>();
            controls = GetComponentInParent<PlayerControlSettings>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (hotbar == null) hotbar = GetComponentInParent<PlayerHotbar>();
            if (controls == null) controls = GetComponentInParent<PlayerControlSettings>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void OnEnable()
        {
            root = document.rootVisualElement;
            if (root == null) return;

            scrim = root.Q<VisualElement>("scrim");
            panel = root.Q<VisualElement>("inventory-panel");
            bagContainer = root.Q<VisualElement>("bag");
            hotbarContainer = root.Q<VisualElement>("hotbar");
            details = root.Q<VisualElement>("details");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsTitle = root.Q<Label>("details-title");
            detailsBody = root.Q<Label>("details-body");
            hint = root.Q<Label>("hint");
            ghost = root.Q<VisualElement>("drag-ghost");

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            root.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            if (inventory != null) inventory.Changed += OnInventoryChanged;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;

            BuildSlots();
            UiTransitions.HideImmediately(scrim);
            UiTransitions.HideImmediately(panel);
        }

        private void OnDisable()
        {
            // シーンを抜けるなどで開いたまま消えても、時間やカーソルを止めたままにしない。
            if (IsOpen) Close();

            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;

            if (root != null)
            {
                root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                root.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }
        }

        // PlayerInput は OnEnable でアクションを用意し直すことがあるので、掴むのは全員の OnEnable の後。
        private void Start()
        {
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            if (actions == null)
            {
                Debug.LogWarning("PlayerInput が無いのでインベントリを開けない", this);
                return;
            }

            toggleAction = FindAction(actions, toggleActionName);
            cancelAction = FindAction(actions, cancelActionName);
            quickMoveAction = FindAction(actions, quickMoveActionName);
            dropAction = FindAction(actions, dropActionName);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                if (toggleAction != null && toggleAction.WasPressedThisFrame()) Open();
                return;
            }

            // 開いたのと同じ押下で閉じないように。
            if (Time.frameCount == openedFrame) return;

            bool closePressed = (toggleAction != null && toggleAction.WasPressedThisFrame())
                                || (cancelAction != null && cancelAction.WasPressedThisFrame());
            if (closePressed)
            {
                Close();
                return;
            }

            if (dropAction != null && dropAction.WasPressedThisFrame()) DropHovered();
            RefreshHint();
        }

        private void LateUpdate()
        {
            Pauser.KeepCursorFree();
        }

        /// <summary>インベントリを開いてゲームを止める。</summary>
        public void Open()
        {
            if (IsOpen || inventory == null) return;

            openedFrame = Time.frameCount;
            Pauser.Pause();

            // UI マップに切り替えると開閉のアクションも止まるので、これだけ効かせ直す（閉じるキーもキー設定に従わせるため）。
            toggleAction?.Enable();

            RefreshSlots();
            RefreshHint(force: true);
            hoverIndex = -1;
            RefreshDetails();

            UiTransitions.Show(scrim);
            UiTransitions.Show(panel);
        }

        /// <summary>インベントリを閉じてゲームに戻る。</summary>
        public void Close()
        {
            if (!IsOpen) return;

            CancelDrag();
            hoverIndex = -1;
            UiTransitions.Hide(panel);
            UiTransitions.Hide(scrim);
            Pauser.Resume();
        }

        // ---- 枠 ------------------------------------------------------------

        /// <summary>インベントリの枠の数に合わせて枠を作り直す。バッグが広がったときもここを通る。</summary>
        private void BuildSlots()
        {
            if (bagContainer == null || hotbarContainer == null) return;

            CancelDrag();
            bagContainer.Clear();
            hotbarContainer.Clear();
            slots.Clear();
            if (inventory == null) return;

            Inventory items = inventory.Inventory;
            for (int i = 0; i < items.Count; i++)
            {
                bool isHotbar = items.IsHotbar(i);
                var slot = ItemSlot.Create($"slot-{i}", isHotbar ? (i + 1).ToString() : null);
                int index = i;
                slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(index, e));
                slot.RegisterCallback<PointerEnterEvent>(_ => OnSlotHover(index, true));
                slot.RegisterCallback<PointerLeaveEvent>(_ => OnSlotHover(index, false));
                (isHotbar ? hotbarContainer : bagContainer).Add(slot);
                slots.Add(slot);
            }

            RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (inventory == null) return;

            Inventory items = inventory.Inventory;
            int selected = hotbar != null ? hotbar.SelectedIndex : -1;
            for (int i = 0; i < slots.Count; i++)
            {
                ItemDefinition item = items[i];
                ItemSlot.SetIcon(slots[i], item != null ? item.Icon : null);
                slots[i].EnableInClassList(ItemSlot.SelectedClass, items.IsHotbar(i) && i == selected);
            }
        }

        private void OnInventoryChanged(PlayerInventory _)
        {
            if (inventory.Inventory.Count != slots.Count) BuildSlots();
            else RefreshSlots();
            RefreshDetails();
        }

        private void OnHotbarChanged(PlayerHotbar _) => RefreshSlots();

        // ---- クリックとドラッグ --------------------------------------------

        private void OnSlotPointerDown(int index, PointerDownEvent e)
        {
            if (!IsOpen || e.button != 0 || dragFrom >= 0) return;

            Inventory items = inventory.Inventory;
            if (items[index] == null) return;

            e.StopPropagation();

            if (IsHeld(quickMoveAction))
            {
                if (!items.QuickMove(index)) UiTransitions.Flash(slots[index], DeniedClass, 160);
                return;
            }

            BeginDrag(index, e.pointerId, e.position);
        }

        private void BeginDrag(int index, int pointerId, Vector2 position)
        {
            dragFrom = index;
            dragPointerId = pointerId;
            slots[index].AddToClassList(DragSourceClass);

            if (ghost != null)
            {
                ItemDefinition item = inventory.Inventory[index];
                ghost.style.backgroundImage = item != null && item.Icon != null ? new StyleBackground(item.Icon) : new StyleBackground(StyleKeyword.None);
                ghost.style.display = DisplayStyle.Flex;
                ghost.BringToFront();
                MoveGhost(position);
            }

            root.CapturePointer(pointerId);
            RefreshDetails();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (dragFrom < 0 || e.pointerId != dragPointerId) return;

            MoveGhost(e.position);
            SetDropTarget(SlotAt(e.position));
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (dragFrom < 0 || e.pointerId != dragPointerId) return;

            int from = dragFrom;
            int to = SlotAt(e.position);
            bool outside = panel != null && !panel.worldBound.Contains(e.position);
            EndDrag();

            if (to >= 0) inventory.Inventory.Move(from, to);
            else if (outside) inventory.Drop(from);
        }

        // ウィンドウからフォーカスが外れるなどで掴みが切れたら、何もせず元に戻す。
        private void OnPointerCaptureOut(PointerCaptureOutEvent e)
        {
            if (dragFrom >= 0) EndDrag();
        }

        private void CancelDrag()
        {
            if (dragFrom >= 0) EndDrag();
        }

        private void EndDrag()
        {
            int pointerId = dragPointerId;
            if (dragFrom >= 0 && dragFrom < slots.Count) slots[dragFrom].RemoveFromClassList(DragSourceClass);
            SetDropTarget(-1);
            dragFrom = -1;
            dragPointerId = -1;

            if (ghost != null) ghost.style.display = DisplayStyle.None;
            if (root != null && pointerId >= 0 && root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
            RefreshDetails();
        }

        private void MoveGhost(Vector2 position)
        {
            if (ghost == null) return;

            // ルートは画面いっぱいなので、パネル座標をそのまま使える。絵の中心をポインタに合わせる。
            Vector2 local = root.WorldToLocal(position);
            ghost.style.left = local.x - 28f;
            ghost.style.top = local.y - 28f;
        }

        private void SetDropTarget(int index)
        {
            if (index == dragFrom) index = -1;
            if (index == dropTarget) return;

            if (dropTarget >= 0 && dropTarget < slots.Count) slots[dropTarget].RemoveFromClassList(DropTargetClass);
            dropTarget = index;
            if (dropTarget >= 0) slots[dropTarget].AddToClassList(DropTargetClass);
        }

        /// <summary>パネル座標 position の下にある枠の番号。無ければ -1。</summary>
        private int SlotAt(Vector2 position)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].worldBound.Contains(position)) return i;
            }

            return -1;
        }

        private static bool IsHeld(InputAction action) => action != null && action.IsPressed();

        /// <summary>合わせている枠のものを足元に捨てる（捨てるキー）。ドラッグ中は捨てない。</summary>
        private void DropHovered()
        {
            if (dragFrom >= 0 || hoverIndex < 0 || inventory.Inventory[hoverIndex] == null) return;
            inventory.Drop(hoverIndex);
        }

        // ---- 情報欄 --------------------------------------------------------

        private void OnSlotHover(int index, bool entered)
        {
            if (entered) hoverIndex = index;
            else if (hoverIndex == index) hoverIndex = -1;
            RefreshDetails();
        }

        /// <summary>ドラッグ中は掴んでいるもの、そうでなければ合わせている枠のものを情報欄に出す。</summary>
        private void RefreshDetails()
        {
            if (details == null || inventory == null) return;

            int index = dragFrom >= 0 ? dragFrom : hoverIndex;
            ItemDefinition item = inventory.Inventory[index];

            // 何も合わせていないときは枠だけ残して文字は出さない。
            details.EnableInClassList(DetailsEmptyClass, item == null);
            if (detailsTitle != null) detailsTitle.text = item != null ? item.DisplayName : string.Empty;
            if (detailsBody != null) detailsBody.text = item != null ? item.Description : string.Empty;
            if (detailsIcon != null)
            {
                detailsIcon.style.backgroundImage = item != null && item.Icon != null
                    ? new StyleBackground(item.Icon)
                    : new StyleBackground(StyleKeyword.None);
            }
        }

        /// <summary>
        /// 操作の案内。クイック移動キーを押している間は「クリックで移動」だけにする。
        /// キー設定で変えたキーを出すので、開くとき（force）は必ず作り直す。
        /// </summary>
        private void RefreshHint(bool force = false)
        {
            if (hint == null) return;

            bool quickMove = IsHeld(quickMoveAction);
            if (!force && quickMove == quickMoveHintShown) return;
            quickMoveHintShown = quickMove;

            if (quickMove)
            {
                hint.text = QuickMoveHintText;
                return;
            }

            string scheme = playerInput != null ? playerInput.currentControlScheme : null;
            hint.text = HintText(Key(quickMoveAction, scheme), Key(dropAction, scheme), Key(toggleAction, scheme));
        }

        private static string Key(InputAction action, string scheme)
        {
            return action != null ? InteractionPromptView.KeyCapText(PlayerInteractor.BindingDisplay(action, scheme)) : "?";
        }

        public const string QuickMoveHintText = "クリックで移動";

        public static string HintText(string quickMoveKey, string dropKey, string closeKey)
        {
            return $"ドラッグで移動　{quickMoveKey}＋クリックで移動　{dropKey} で捨てる　{closeKey} で閉じる";
        }

        private InputAction FindAction(InputActionAsset actions, string actionName)
        {
            InputAction action = actions.FindAction(actionName);
            if (action == null) Debug.LogWarning($"アクション '{actionName}' が {actions.name} に無い", this);
            return action;
        }
    }
}
