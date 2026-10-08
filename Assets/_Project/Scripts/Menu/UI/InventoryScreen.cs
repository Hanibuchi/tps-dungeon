using System.Collections.Generic;
using TpsDungeon.Interaction;
using TpsDungeon.Items;
using TpsDungeon.Party;
using TpsDungeon.Player;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using PartyGroup = TpsDungeon.Party.Party;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// Tab（ゲームパッドは Y）で開くインベントリ画面。開いている間はポーズと同じくゲームを止める（<see cref="GamePauser"/>）。
    /// Inventory.uxml を UIDocument に差して、パーティーの操作台（かプレイヤー）の子に置いて使う。
    ///
    /// 左にパーティーの並び（先頭が操作しているキャラ）、真ん中に共有のバッグと、選んでいる人の手元（ホットバーの 4 枠）を出す。
    /// 操作（マウス）:
    /// - 仲間の札をドラッグして別の札に落とすと、その位置へ並べ替える。先頭に落とせばその人を操作するようになる。
    /// - 仲間の札をクリックすると、その人の手元を出す（装備を替えられる）。
    /// - 枠をドラッグで枠から枠へ移す（埋まっていれば入れ替え）。仲間の札に落とすとその人の手元の空きへ入れる。
    ///   パネルの外で離すと、先頭の足元に捨てる。
    /// - クイック移動キー（既定 Shift）＋クリックで、バッグ ⇔ 手元の空き枠へ移す。
    /// - 枠に合わせて捨てるキー（既定 O）を押すと、足元に捨てる。
    /// - 枠や札に合わせると右の情報欄に説明が出る。
    /// 開閉キー（Player/Inventory）か Esc（UI/Cancel）で閉じる。
    /// 下の操作案内は、クイック移動キーを押している間だけ「クリックで移動」に変わる。
    /// パーティーが居ないシーンでは、親か先頭の持ち物だけを出し、並びの欄は隠す。
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
        private const string CardClass = "party-card";
        private const string CardLeaderClass = "party-card--leader";
        private const string CardSelectedClass = "party-card--selected";
        private const string CardDragSourceClass = "party-card--drag-source";
        private const string CardDropTargetClass = "party-card--drop-target";
        private const string CardDeniedClass = "party-card--denied";

        /// <summary>札を押してからこれだけ動いたらドラッグとみなす（パネルの px）。</summary>
        private const float CardDragThreshold = 6f;

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定なら親から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("パーティーが居ないときに出す持ち物。未設定なら親か、パーティーの先頭から探す。")]
        private PlayerInventory inventory;

        [SerializeField, Tooltip("閉じるときに設定を保存する相手。未設定なら親から探す。")]
        private PlayerControlSettings controls;

        [SerializeField, Tooltip("開くときに閉じる大きな地図。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("開け閉めするアクション（ゲーム中のマップ側）。開いている間もこれだけは効かせる。")]
        private string toggleActionName = "Player/Inventory";

        [SerializeField, Tooltip("閉じるアクション（UI マップ側）。")]
        private string cancelActionName = "UI/Cancel";

        [SerializeField, Tooltip("押しながらクリックで手元とバッグの間を移すアクション（UI マップ側）。")]
        private string quickMoveActionName = "UI/QuickMove";

        [SerializeField, Tooltip("合わせている枠のものを捨てるアクション（ゲーム中のマップ側。ゲーム中は手に持っているものを捨てる）。開いている間もこれを効かせる。")]
        private string dropActionName = "Player/Drop";

        [SerializeField, Tooltip("ゲーム中に使うアクションマップ。")]
        private string gameplayMapName = "Player";

        [SerializeField, Tooltip("開いている間に使うアクションマップ。")]
        private string menuMapName = "UI";

        /// <summary>画面の 1 枠が指している持ち物の枠。</summary>
        private readonly struct SlotRef
        {
            public readonly Inventory Items;
            public readonly int Index;

            public SlotRef(Inventory items, int index)
            {
                Items = items;
                Index = index;
            }

            public ItemInstance Item => Items != null ? Items[Index] : null;
        }

        private UIDocument document;
        private VisualElement root;
        private VisualElement scrim;
        private VisualElement panel;
        private VisualElement partyColumn;
        private VisualElement partyList;
        private VisualElement bagContainer;
        private VisualElement hotbarContainer;
        private Label handCaption;
        private VisualElement details;
        private VisualElement detailsIcon;
        private Label detailsTitle;
        private Label detailsBody;
        private Label hint;
        private VisualElement ghost;

        private readonly List<VisualElement> slots = new List<VisualElement>();
        private readonly List<SlotRef> slotRefs = new List<SlotRef>();
        private readonly List<VisualElement> cards = new List<VisualElement>();

        private GamePauser pauser;

        // 再生中にスクリプトを再コンパイルすると、シリアライズされないこのフィールドだけ消えて Awake も呼ばれ直さない。
        // そのときも止まらないよう、使うときに作る。
        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);
        private InputAction toggleAction;
        private InputAction cancelAction;
        private InputAction quickMoveAction;
        private InputAction dropAction;

        private PartyGroup party;
        private PartyMember selected;
        private PlayerInventory shownInventory;
        private PlayerHotbar shownHotbar;

        private int openedFrame = -1;
        private int hoverIndex = -1;
        private int hoverCard = -1;
        private int dragFrom = -1;
        private int dragPointerId = -1;
        private int dropTarget = -1;
        private int dropCard = -1;

        // 仲間の札のドラッグ。押した札（cardPressed）が閾値を越えて動いたら cardDragging。
        private int cardPressed = -1;
        private bool cardDragging;
        private Vector2 cardPressPosition;

        private bool quickMoveHintShown;

        /// <summary>開いているか。</summary>
        public bool IsOpen => pauser != null && pauser.IsPaused;

        private void Reset()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            inventory = GetComponentInParent<PlayerInventory>();
            controls = GetComponentInParent<PlayerControlSettings>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (controls == null) controls = GetComponentInParent<PlayerControlSettings>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void OnEnable()
        {
            root = document.rootVisualElement;
            if (root == null) return;

            scrim = root.Q<VisualElement>("scrim");
            panel = root.Q<VisualElement>("inventory-panel");
            partyColumn = root.Q<VisualElement>("party");
            partyList = root.Q<VisualElement>("party-list");
            bagContainer = root.Q<VisualElement>("bag");
            hotbarContainer = root.Q<VisualElement>("hotbar");
            handCaption = root.Q<Label>("hand-caption");
            details = root.Q<VisualElement>("details");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsTitle = root.Q<Label>("details-title");
            detailsBody = root.Q<Label>("details-body");
            hint = root.Q<Label>("hint");
            ghost = root.Q<VisualElement>("drag-ghost");

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            root.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            PartyRoster.Changed += OnRosterChanged;

            UiTransitions.HideImmediately(scrim);
            UiTransitions.HideImmediately(panel);
            Rebind();
        }

        private void OnDisable()
        {
            // シーンを抜けるなどで開いたまま消えても、時間やカーソルを止めたままにしない。
            if (IsOpen) Close();

            PartyRoster.Changed -= OnRosterChanged;
            ShowInventoryOf(null);

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
            Rebind();

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
            Rebind();
            if (IsOpen || shownInventory == null) return;

            openedFrame = Time.frameCount;
            Pauser.Pause();

            // UI マップに切り替えると開閉と捨てるのアクションも止まるので、これだけ効かせ直す（キー設定に従わせるため）。
            toggleAction?.Enable();
            dropAction?.Enable();

            // 開くたびに、操作しているキャラの手元から見せる。
            if (party != null) Select(party.Leader);
            RebuildCards();
            RebuildSlots();
            RefreshHint(force: true);
            hoverIndex = -1;
            hoverCard = -1;
            RefreshDetails();

            UiTransitions.Show(scrim);
            UiTransitions.Show(panel);
        }

        /// <summary>インベントリを閉じてゲームに戻る。</summary>
        public void Close()
        {
            if (!IsOpen) return;

            CancelDrag();
            CancelCardDrag();
            hoverIndex = -1;
            hoverCard = -1;
            UiTransitions.Hide(panel);
            UiTransitions.Hide(scrim);
            Pauser.Resume();
        }

        // ---- 見せる相手 ----------------------------------------------------

        /// <summary>パーティー（無ければ親か先頭の持ち物）を見直す。</summary>
        private void Rebind()
        {
            PartyGroup current = PartyGroup.Current;
            if (current != party)
            {
                if (party != null) party.Changed -= OnPartyChanged;
                party = current;
                if (party != null) party.Changed += OnPartyChanged;
            }

            if (partyColumn != null) partyColumn.style.display = party != null ? DisplayStyle.Flex : DisplayStyle.None;

            if (party != null)
            {
                if (selected == null || party.IndexOf(selected) < 0) Select(party.Leader);
                else ShowInventoryOf(selected.Inventory);
                return;
            }

            selected = null;
            PlayerInventory fallback = inventory;
            if (fallback == null && PartyRoster.Leader != null) fallback = PartyRoster.Leader.GetComponent<PlayerInventory>();
            ShowInventoryOf(fallback);
        }

        private void Select(PartyMember member)
        {
            selected = member;
            ShowInventoryOf(member != null ? member.Inventory : null);
            RefreshCards();
        }

        private void ShowInventoryOf(PlayerInventory target)
        {
            if (target == shownInventory) return;

            if (shownInventory != null) shownInventory.Changed -= OnInventoryChanged;
            if (shownHotbar != null) shownHotbar.Changed -= OnHotbarChanged;
            shownInventory = target;
            shownHotbar = target != null ? target.GetComponent<PlayerHotbar>() : null;
            if (shownInventory != null) shownInventory.Changed += OnInventoryChanged;
            if (shownHotbar != null) shownHotbar.Changed += OnHotbarChanged;

            RebuildSlots();
        }

        private Inventory Hand => shownInventory != null ? shownInventory.Inventory : null;

        private Inventory Bag => party != null ? party.Bag : shownInventory != null ? shownInventory.Bag : null;

        /// <summary>捨てた物を置くキャラの持ち物（先頭）。</summary>
        private PlayerInventory DropOwner
        {
            get
            {
                if (party != null && party.Leader != null) return party.Leader.Inventory;
                return shownInventory;
            }
        }

        private void OnRosterChanged() => Rebind();

        private void OnPartyChanged(PartyGroup _)
        {
            if (selected != null && party.IndexOf(selected) < 0) Select(party.Leader);
            RebuildCards();
        }

        private void OnInventoryChanged(PlayerInventory _)
        {
            if (Hand == null || Bag == null || Hand.Count + Bag.Count != slots.Count) RebuildSlots();
            else RefreshSlots();
            RefreshDetails();
        }

        private void OnHotbarChanged(PlayerHotbar _) => RefreshSlots();

        // ---- 仲間の札 ------------------------------------------------------

        private void RebuildCards()
        {
            if (partyList == null) return;

            CancelCardDrag();
            partyList.Clear();
            cards.Clear();
            if (party == null) return;

            for (int i = 0; i < party.Members.Count; i++)
            {
                VisualElement card = CreateCard(i);
                partyList.Add(card);
                cards.Add(card);
            }

            RefreshCards();
        }

        private VisualElement CreateCard(int index)
        {
            var card = new VisualElement { name = $"party-card-{index}" };
            card.AddToClassList(CardClass);

            var swatch = new VisualElement { name = "swatch", pickingMode = PickingMode.Ignore };
            swatch.AddToClassList("party-card__swatch");
            card.Add(swatch);

            var number = new Label { name = "number", pickingMode = PickingMode.Ignore };
            number.AddToClassList("party-card__number");
            card.Add(number);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("party-card__text");
            var name = new Label { name = "name", pickingMode = PickingMode.Ignore };
            name.AddToClassList("party-card__name");
            name.AddToClassList("rpg-bold");
            var level = new Label { name = "level", pickingMode = PickingMode.Ignore };
            level.AddToClassList("party-card__level");
            text.Add(name);
            text.Add(level);
            card.Add(text);

            var badge = new Label { name = "badge", text = "操作中", pickingMode = PickingMode.Ignore };
            badge.AddToClassList("party-card__badge");
            card.Add(badge);

            card.RegisterCallback<PointerDownEvent>(e => OnCardPointerDown(index, e));
            card.RegisterCallback<PointerEnterEvent>(_ => OnCardHover(index, true));
            card.RegisterCallback<PointerLeaveEvent>(_ => OnCardHover(index, false));
            return card;
        }

        private void RefreshCards()
        {
            if (party == null) return;

            for (int i = 0; i < cards.Count && i < party.Members.Count; i++)
            {
                PartyMember member = party.Members[i];
                VisualElement card = cards[i];
                card.EnableInClassList(CardLeaderClass, i == 0);
                card.EnableInClassList(CardSelectedClass, member == selected);
                card.Q<Label>("number").text = (i + 1).ToString();
                card.Q<Label>("name").text = member.DisplayName;
                card.Q<Label>("level").text = member.Progression != null ? $"Lv {member.Progression.Level}" : string.Empty;

                var appearance = member.GetComponent<CharacterAppearance>();
                Color? accent = appearance != null ? appearance.AccentColor : null;
                card.Q<VisualElement>("swatch").style.backgroundColor = accent ?? new Color(0.55f, 0.47f, 0.33f);
            }

            if (handCaption != null)
                handCaption.text = selected != null ? $"手 元 — {selected.DisplayName}" : "手 元";
        }

        private void OnCardPointerDown(int index, PointerDownEvent e)
        {
            if (!IsOpen || e.button != 0 || dragFrom >= 0 || cardPressed >= 0) return;
            e.StopPropagation();

            cardPressed = index;
            cardDragging = false;
            cardPressPosition = e.position;
            dragPointerId = e.pointerId;
            root.CapturePointer(e.pointerId);
        }

        private void OnCardHover(int index, bool entered)
        {
            if (entered) hoverCard = index;
            else if (hoverCard == index) hoverCard = -1;
            RefreshDetails();
        }

        private void BeginCardDrag()
        {
            cardDragging = true;
            if (cardPressed >= 0 && cardPressed < cards.Count) cards[cardPressed].AddToClassList(CardDragSourceClass);
        }

        private void SetDropCard(int index)
        {
            if (cardDragging && index == cardPressed) index = -1;
            if (index == dropCard) return;

            if (dropCard >= 0 && dropCard < cards.Count) cards[dropCard].RemoveFromClassList(CardDropTargetClass);
            dropCard = index;
            if (dropCard >= 0 && dropCard < cards.Count) cards[dropCard].AddToClassList(CardDropTargetClass);
        }

        private void CancelCardDrag()
        {
            if (cardPressed < 0) return;
            EndCardDrag();
        }

        private void EndCardDrag()
        {
            int pointerId = dragPointerId;
            if (cardPressed >= 0 && cardPressed < cards.Count) cards[cardPressed].RemoveFromClassList(CardDragSourceClass);
            SetDropCard(-1);
            cardPressed = -1;
            cardDragging = false;
            dragPointerId = -1;
            if (root != null && pointerId >= 0 && root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
        }

        /// <summary>パネル座標 position の下にある札の番号。無ければ -1。</summary>
        private int CardAt(Vector2 position)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].worldBound.Contains(position)) return i;
            }

            return -1;
        }

        // ---- 枠 ------------------------------------------------------------

        /// <summary>手元とバッグの枠の数に合わせて枠を作り直す。バッグが広がったときもここを通る。</summary>
        private void RebuildSlots()
        {
            if (bagContainer == null || hotbarContainer == null) return;

            CancelDrag();
            bagContainer.Clear();
            hotbarContainer.Clear();
            slots.Clear();
            slotRefs.Clear();

            Inventory hand = Hand;
            Inventory bag = Bag;
            if (bag != null)
            {
                for (int i = 0; i < bag.Count; i++) AddSlot(bagContainer, new SlotRef(bag, i), null);
            }

            if (hand != null)
            {
                for (int i = 0; i < hand.Count; i++) AddSlot(hotbarContainer, new SlotRef(hand, i), (i + 1).ToString());
            }

            RefreshSlots();
            RefreshCards();
        }

        private void AddSlot(VisualElement container, SlotRef slotRef, string number)
        {
            int index = slots.Count;
            var slot = ItemSlot.Create($"slot-{index}", number);
            slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(index, e));
            slot.RegisterCallback<PointerEnterEvent>(_ => OnSlotHover(index, true));
            slot.RegisterCallback<PointerLeaveEvent>(_ => OnSlotHover(index, false));
            container.Add(slot);
            slots.Add(slot);
            slotRefs.Add(slotRef);
        }

        private void RefreshSlots()
        {
            Inventory hand = Hand;
            int selectedSlot = shownHotbar != null ? shownHotbar.SelectedIndex : -1;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotRef slotRef = slotRefs[i];
                ItemInstance item = slotRef.Item;
                ItemSlot.SetItem(slots[i], item?.Icon, RankColor(item));
                slots[i].EnableInClassList(ItemSlot.SelectedClass, slotRef.Items == hand && slotRef.Index == selectedSlot);
            }
        }

        // ---- クリックとドラッグ --------------------------------------------

        private void OnSlotPointerDown(int index, PointerDownEvent e)
        {
            if (!IsOpen || e.button != 0 || dragFrom >= 0 || cardPressed >= 0) return;

            SlotRef slotRef = slotRefs[index];
            if (slotRef.Item == null) return;

            e.StopPropagation();

            if (IsHeld(quickMoveAction))
            {
                if (!InventoryTransfer.QuickMove(slotRef.Items, slotRef.Index, Hand, Bag)) UiTransitions.Flash(slots[index], DeniedClass, 160);
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
                ItemInstance item = slotRefs[index].Item;
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
            if (e.pointerId != dragPointerId) return;

            if (cardPressed >= 0)
            {
                if (!cardDragging && Vector2.Distance(e.position, cardPressPosition) >= CardDragThreshold) BeginCardDrag();
                if (cardDragging) SetDropCard(CardAt(e.position));
                return;
            }

            if (dragFrom < 0) return;

            MoveGhost(e.position);
            int slot = SlotAt(e.position);
            SetDropTarget(slot);
            SetDropCard(slot < 0 ? CardAt(e.position) : -1);
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != dragPointerId) return;

            if (cardPressed >= 0)
            {
                int pressed = cardPressed;
                bool dragging = cardDragging;
                int onto = CardAt(e.position);
                EndCardDrag();

                if (!dragging) Select(party != null && pressed < party.Members.Count ? party.Members[pressed] : null);
                else if (onto >= 0 && onto != pressed) party.Move(pressed, onto);
                return;
            }

            if (dragFrom < 0) return;

            int from = dragFrom;
            int to = SlotAt(e.position);
            int card = to < 0 ? CardAt(e.position) : -1;
            bool outside = panel != null && !panel.worldBound.Contains(e.position);
            EndDrag();

            SlotRef source = slotRefs[from];
            if (to >= 0)
            {
                SlotRef target = slotRefs[to];
                InventoryTransfer.Move(source.Items, source.Index, target.Items, target.Index);
            }
            else if (card >= 0)
            {
                GiveToMember(source, card);
            }
            else if (outside)
            {
                DropOwner?.Drop(source.Items, source.Index);
            }
        }

        /// <summary>枠の物を、card 番目の仲間の手元の空きへ入れる。空きが無ければ札を赤く灯す。</summary>
        private void GiveToMember(SlotRef source, int card)
        {
            if (party == null || card >= party.Members.Count) return;

            Inventory hand = party.Members[card].Inventory.Inventory;
            if (hand == source.Items) return;

            int to = InventoryTransfer.FirstEmpty(hand);
            if (to < 0 || !InventoryTransfer.Move(source.Items, source.Index, hand, to))
                UiTransitions.Flash(cards[card], CardDeniedClass, 160);
        }

        // ウィンドウからフォーカスが外れるなどで掴みが切れたら、何もせず元に戻す。
        private void OnPointerCaptureOut(PointerCaptureOutEvent e)
        {
            if (dragFrom >= 0) EndDrag();
            if (cardPressed >= 0) EndCardDrag();
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
            SetDropCard(-1);
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
            if (dragFrom >= 0 || cardPressed >= 0 || hoverIndex < 0 || hoverIndex >= slotRefs.Count) return;

            SlotRef slotRef = slotRefs[hoverIndex];
            if (slotRef.Item == null) return;
            DropOwner?.Drop(slotRef.Items, slotRef.Index);
        }

        // ---- 情報欄 --------------------------------------------------------

        private void OnSlotHover(int index, bool entered)
        {
            if (entered) hoverIndex = index;
            else if (hoverIndex == index) hoverIndex = -1;
            RefreshDetails();
        }

        /// <summary>ドラッグ中は掴んでいるもの、そうでなければ合わせている枠のもの（か仲間）を情報欄に出す。</summary>
        private void RefreshDetails()
        {
            if (details == null) return;

            int index = dragFrom >= 0 ? dragFrom : hoverIndex;
            ItemInstance item = index >= 0 && index < slotRefs.Count ? slotRefs[index].Item : null;

            if (item == null && dragFrom < 0 && hoverCard >= 0 && party != null && hoverCard < party.Members.Count)
            {
                ShowMemberDetails(party.Members[hoverCard], hoverCard);
                return;
            }

            // 何も合わせていないときは枠だけ残して文字は出さない。
            details.EnableInClassList(DetailsEmptyClass, item == null);
            if (detailsTitle != null) detailsTitle.text = item != null ? item.DisplayName : string.Empty;
            if (detailsBody != null) detailsBody.text = item != null ? item.DetailText() : string.Empty;
            SetDetailsIcon(item != null ? item.Icon : null, RankColor(item));
        }

        private void ShowMemberDetails(PartyMember member, int index)
        {
            details.EnableInClassList(DetailsEmptyClass, false);
            if (detailsTitle != null) detailsTitle.text = member.DisplayName;
            if (detailsBody != null) detailsBody.text = MemberDetailText(member, index);
            SetDetailsIcon(null, null);
        }

        private void SetDetailsIcon(Texture2D icon, Color? rank)
        {
            if (detailsIcon == null) return;

            detailsIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            // 絵を収める窪みに、ランクの色の光を敷く。
            ItemSlot.SetRankGlow(detailsIcon.parent, rank);
        }

        private static string MemberDetailText(PartyMember member, int index)
        {
            var lines = new List<string>();
            lines.Add(index == 0 ? "操作中（パーティーの先頭）" : $"列の {index + 1} 番目");
            if (member.Progression != null) lines.Add($"Lv {member.Progression.Level}");
            if (member.Health != null) lines.Add($"HP {member.Health.CurrentHp} / {member.Health.MaxHp}");

            Inventory hand = member.Inventory != null ? member.Inventory.Inventory : null;
            if (hand != null)
            {
                lines.Add(string.Empty);
                for (int i = 0; i < hand.Count; i++) lines.Add($"{i + 1}. {(hand[i] != null ? hand[i].DisplayName : "—")}");
            }

            return string.Join("\n", lines);
        }

        private static Color? RankColor(ItemInstance item) => item != null && item.TryGetRankColor(out Color c) ? c : (Color?)null;

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
            string text = HintText(Key(quickMoveAction, scheme), Key(dropAction, scheme), Key(toggleAction, scheme));
            hint.text = party != null ? text + "\n" + PartyHintText : text;
        }

        private static string Key(InputAction action, string scheme)
        {
            return action != null ? InteractionPromptView.KeyCapText(PlayerInteractor.BindingDisplay(action, scheme)) : "?";
        }

        public const string QuickMoveHintText = "クリックで移動";

        public const string PartyHintText = "仲間の札をドラッグで並べ替え（先頭が操作するキャラ）　札をクリックでその人の手元を出す";

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
