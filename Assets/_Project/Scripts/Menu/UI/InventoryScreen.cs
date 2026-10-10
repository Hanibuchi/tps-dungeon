using System.Collections.Generic;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
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
    /// 左にパーティーの並び（先頭が操作しているキャラ）を札で出し、札の中にその人の手持ち（ホットバーの 4 枠）を出す。
    /// 真ん中は共有のバッグ。右の情報欄は大きさを固定してあり、説明の長さでパネルが伸び縮みしない（収まらない分は切る）。
    /// 操作（マウス）:
    /// - 枠をドラッグで枠から枠へ移す（埋まっていれば入れ替え）。札の枠に落とせばその人が装備する。
    ///   札の枠の外（名前のあたり）に落とすと、その人の手持ちの空きへ入れる。パネルの外で離すと、先頭の足元に捨てる。
    /// - 札（枠の外か空の枠）をドラッグして別の札に落とすと、その位置へ並べ替える。先頭に落とせばその人を操作するようになる。
    ///   ドラッグ中は札の写しがポインタについて動き、ほかの札は離したときの並びへ滑る（番号と「操作中」も先の並びで出す）。
    ///   一覧の外で離すと並びはそのまま。
    /// - クイック移動キー（既定 Shift）＋クリックで、手持ちの物はバッグの空きへ、バッグの物は先頭の手持ちの空きへ移す。
    /// - 枠に合わせて捨てるキー（既定 O）を押すと、足元に捨てる。
    /// - 枠や札に合わせると右の情報欄に説明が出る。
    /// 開閉キー（Player/Inventory）か Esc（UI/Cancel）で閉じる。
    /// 下の操作案内は、クイック移動キーを押している間だけ「クリックで移動」に変わる（仲間の札の案内の行は残し、パネルの大きさが変わらないようにする）。
    /// パーティーが居ないシーンでは、親か先頭の持ち物を札 1 枚で出す（並べ替えはできない）。
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
        private const string CardDragSourceClass = "party-card--drag-source";
        private const string CardDropTargetClass = "party-card--drop-target";
        private const string CardDeniedClass = "party-card--denied";
        private const string CardGhostClass = "party-card--ghost";
        private const string CardSettlingClass = "party-card--settling";

        /// <summary>札を押してからこれだけ動いたらドラッグとみなす（パネルの px）。</summary>
        private const float CardDragThreshold = 6f;

        /// <summary>札の 1 列の段数。これを超えたら 2 列目へ折り返す（Inventory.uss の .party-list の高さと揃える）。</summary>
        private const int CardsPerColumn = 6;

        /// <summary>札 1 列の幅（札の幅 186px ＋ 左右の余白 3px ずつ。Inventory.uss の .party-card と揃える）。</summary>
        private const float CardColumnWidth = 192f;

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

        [SerializeField, Tooltip("押しながらクリックで手持ちとバッグの間を移すアクション（UI マップ側）。")]
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

        /// <summary>札 1 枚。パーティーが居ないときは Member が null で、Inventory だけを持つ。</summary>
        private sealed class Card
        {
            public VisualElement Element;
            public PartyMember Member;
            public PlayerInventory Inventory;
            public PlayerHotbar Hotbar;
            public int FirstSlot;
        }

        private UIDocument document;
        private VisualElement root;
        private VisualElement scrim;
        private VisualElement panel;
        private VisualElement partyList;
        private VisualElement bagContainer;
        private VisualElement details;
        private VisualElement detailsIcon;
        private Label detailsTitle;
        private Label detailsBody;
        private Label hint;
        private VisualElement ghost;

        private readonly List<VisualElement> slots = new List<VisualElement>();
        private readonly List<SlotRef> slotRefs = new List<SlotRef>();
        private readonly List<Card> cards = new List<Card>();

        private GamePauser pauser;

        // 再生中にスクリプトを再コンパイルすると、シリアライズされないこのフィールドだけ消えて Awake も呼ばれ直さない。
        // そのときも止まらないよう、使うときに作る。
        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);
        private InputAction toggleAction;
        private InputAction cancelAction;
        private InputAction quickMoveAction;
        private InputAction dropAction;

        private PartyGroup party;
        private PlayerInventory soloInventory;

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

        // 札のドラッグ中の見せ方。ドラッグを始めたときの各札の位置（partyList の中。translate は layout に効かないので動かしても変わらない）、
        // 離したら入る番号、ポインタについて動く写しと、押した点の札の中での位置。
        private readonly List<Vector2> cardSlotPositions = new List<Vector2>();
        private int cardPreviewTo = -1;
        private VisualElement cardGhost;
        private Vector2 cardGrabOffset;

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
            partyList = root.Q<VisualElement>("party-list");
            bagContainer = root.Q<VisualElement>("bag");
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
            Unbind();

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
            if (IsOpen || Bag == null) return;

            openedFrame = Time.frameCount;
            Pauser.Pause();

            // UI マップに切り替えると開閉と捨てるのアクションも止まるので、これだけ効かせ直す（キー設定に従わせるため）。
            toggleAction?.Enable();
            dropAction?.Enable();

            Rebuild();
            RefreshHint(force: true);
            hoverIndex = -1;
            hoverCard = -1;
            RefreshDetails();

            UiTransitions.Show(scrim);
            UiTransitions.Show(panel);
            Play(UiSound.Open);
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
            Play(UiSound.Close);
        }

        // ---- 見せる相手 ----------------------------------------------------

        /// <summary>パーティー（無ければ親か先頭の持ち物）を見直し、変わっていれば作り直す。</summary>
        private void Rebind()
        {
            PartyGroup current = PartyGroup.Current;
            PlayerInventory solo = null;
            if (current == null)
            {
                solo = inventory;
                if (solo == null && PartyRoster.Leader != null) solo = PartyRoster.Leader.GetComponent<PlayerInventory>();
            }

            if (current == party && solo == soloInventory) return;

            Unbind();
            party = current;
            soloInventory = solo;
            if (party != null) party.Changed += OnPartyChanged;
            Rebuild();
        }

        /// <summary>
        /// 結んでいた相手の知らせを外すだけで、画面は作り直さない。
        /// シーンを抜けるときはキャラやパーティーが先に壊れていることがあるので、ここでは触らない。
        /// </summary>
        private void Unbind()
        {
            if (party != null) party.Changed -= OnPartyChanged;
            UnwatchCards();
            party = null;
            soloInventory = null;
        }

        private Inventory Bag => party != null ? party.Bag : soloInventory != null ? soloInventory.Bag : null;

        /// <summary>先頭（パーティーが居なければ 1 人）の持ち物。捨てた物はこの人の足元に置き、バッグからの Shift＋クリックはこの人の手持ちへ入れる。</summary>
        private PlayerInventory LeadInventory
        {
            get
            {
                if (party != null) return party.Leader != null ? party.Leader.Inventory : null;
                return soloInventory;
            }
        }

        private void OnRosterChanged() => Rebind();

        private void OnPartyChanged(PartyGroup _) => Rebuild();

        private void OnItemsChanged(PlayerInventory _)
        {
            Inventory bag = Bag;
            int expected = (bag != null ? bag.Count : 0) + cards.Count * PlayerHotbar.SlotCount;
            if (expected != slots.Count) Rebuild();
            else RefreshSlots();
            RefreshDetails();
        }

        private void OnHotbarChanged(PlayerHotbar _) => RefreshSlots();

        private void UnwatchCards()
        {
            foreach (Card card in cards)
            {
                if (card.Inventory != null) card.Inventory.Changed -= OnItemsChanged;
                if (card.Hotbar != null) card.Hotbar.Changed -= OnHotbarChanged;
            }
        }

        // ---- 作り直し ------------------------------------------------------

        /// <summary>バッグの枠と仲間の札（とその中の手持ちの枠）を作り直す。</summary>
        private void Rebuild()
        {
            if (bagContainer == null || partyList == null) return;

            CancelDrag();
            CancelCardDrag();
            UnwatchCards();
            bagContainer.Clear();
            partyList.Clear();
            slots.Clear();
            slotRefs.Clear();
            cards.Clear();

            Inventory bag = Bag;
            if (bag != null)
            {
                for (int i = 0; i < bag.Count; i++)
                {
                    VisualElement slot = CreateSlot(new SlotRef(bag, i), null);
                    bagContainer.Add(slot);
                }
            }

            if (party != null)
            {
                for (int i = 0; i < party.Members.Count; i++)
                {
                    PartyMember member = party.Members[i];
                    if (member != null) AddCard(member, member.Inventory, member.Hotbar);
                }
            }
            else if (soloInventory != null)
            {
                AddCard(null, soloInventory, soloInventory.GetComponent<PlayerHotbar>());
            }

            // 列の数だけの幅にする。1 列で足りるときに 2 列目の空きを残さない（パネルは中央寄せなので、そのぶん詰まる）。
            partyList.style.width = ColumnCount(cards.Count) * CardColumnWidth;

            RefreshSlots();
            RefreshCards();
        }

        /// <summary>count 枚の札を並べる列の数（最低 1）。</summary>
        public static int ColumnCount(int count) => Mathf.Max(1, (count + CardsPerColumn - 1) / CardsPerColumn);

        private VisualElement CreateSlot(SlotRef slotRef, string number)
        {
            int index = slots.Count;
            var slot = ItemSlot.Create($"slot-{index}", number);
            slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(index, e));
            slot.RegisterCallback<PointerEnterEvent>(_ => OnSlotHover(index, true));
            slot.RegisterCallback<PointerLeaveEvent>(_ => OnSlotHover(index, false));
            slots.Add(slot);
            slotRefs.Add(slotRef);
            return slot;
        }

        private void AddCard(PartyMember member, PlayerInventory owner, PlayerHotbar hotbar)
        {
            int index = cards.Count;
            var card = new Card { Member = member, Inventory = owner, Hotbar = hotbar, FirstSlot = slots.Count };

            VisualElement element = CreateCardElement($"party-card-{index}", out VisualElement hand);
            Inventory items = owner != null ? owner.Inventory : null;
            for (int k = 0; k < PlayerHotbar.SlotCount; k++)
            {
                VisualElement slot = CreateSlot(new SlotRef(items, k), (k + 1).ToString());
                slot.AddToClassList("party-card__slot");
                hand.Add(slot);
            }

            element.RegisterCallback<PointerDownEvent>(e => OnCardPointerDown(index, e));
            element.RegisterCallback<PointerEnterEvent>(_ => OnCardHover(index, true));
            element.RegisterCallback<PointerLeaveEvent>(_ => OnCardHover(index, false));

            card.Element = element;
            if (owner != null) owner.Changed += OnItemsChanged;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            partyList.Add(element);
            cards.Add(card);
        }

        /// <summary>札の外形（色見本・番号・名前・Lv・操作中の印）を作る。手持ちの枠は hand に足す。</summary>
        private static VisualElement CreateCardElement(string elementName, out VisualElement hand)
        {
            var element = new VisualElement { name = elementName };
            element.AddToClassList(CardClass);

            var swatch = new VisualElement { name = "swatch", pickingMode = PickingMode.Ignore };
            swatch.AddToClassList("party-card__swatch");
            element.Add(swatch);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("party-card__body");

            // 上の段：番号・名前・Lv・操作中の印。下の段：手持ちの 4 枠（装備の枠そのもの）。
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("party-card__header");
            var number = new Label { name = "number", pickingMode = PickingMode.Ignore };
            number.AddToClassList("party-card__number");
            var name = new Label { name = "name", pickingMode = PickingMode.Ignore };
            name.AddToClassList("party-card__name");
            name.AddToClassList("rpg-bold");
            var level = new Label { name = "level", pickingMode = PickingMode.Ignore };
            level.AddToClassList("party-card__level");
            var badge = new Label { name = "badge", text = "操作中", pickingMode = PickingMode.Ignore };
            badge.AddToClassList("party-card__badge");
            header.Add(number);
            header.Add(name);
            header.Add(level);
            header.Add(badge);
            body.Add(header);

            hand = new VisualElement { pickingMode = PickingMode.Ignore };
            hand.AddToClassList("party-card__hand");
            body.Add(hand);
            element.Add(body);
            return element;
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ItemInstance item = slotRefs[i].Item;
                ItemSlot.SetItem(slots[i], item?.Icon, RankColor(item));
            }

            // 手に持っている枠に印を灯す。
            foreach (Card card in cards)
            {
                int held = card.Hotbar != null ? card.Hotbar.SelectedIndex : -1;
                for (int k = 0; k < PlayerHotbar.SlotCount; k++)
                {
                    int index = card.FirstSlot + k;
                    if (index < slots.Count) slots[index].EnableInClassList(ItemSlot.SelectedClass, k == held);
                }
            }
        }

        private void RefreshCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                FillCard(cards[i].Element, cards[i].Member, i);
            }
        }

        /// <summary>札に、place 番目に居る member の名前・Lv・色と、番号・操作中の印を出す。</summary>
        private static void FillCard(VisualElement element, PartyMember member, int place)
        {
            SetCardPlace(element, member, place);

            if (member == null)
            {
                element.Q<Label>("name").text = "手持ち";
                element.Q<Label>("level").text = string.Empty;
                return;
            }

            element.Q<Label>("name").text = member.DisplayName;
            element.Q<Label>("level").text = member.Progression != null ? $"Lv {member.Progression.Level}" : string.Empty;

            var appearance = member.GetComponent<CharacterAppearance>();
            Color? accent = appearance != null ? appearance.AccentColor : null;
            element.Q<VisualElement>("swatch").style.backgroundColor = accent ?? new Color(0.55f, 0.47f, 0.33f);
        }

        /// <summary>札の番号と操作中の印だけを place 番目として出し直す。</summary>
        private static void SetCardPlace(VisualElement element, PartyMember member, int place)
        {
            element.EnableInClassList(CardLeaderClass, place == 0 && member != null);
            element.Q<Label>("number").text = (place + 1).ToString();
        }

        // ---- 仲間の札（並べ替え） ------------------------------------------

        private void OnCardPointerDown(int index, PointerDownEvent e)
        {
            if (!IsOpen || e.button != 0 || dragFrom >= 0 || cardPressed >= 0) return;
            if (party == null || cards[index].Member == null) return;
            e.StopPropagation();

            cardPressed = index;
            cardDragging = false;
            cardPressPosition = e.position;
            dragPointerId = e.pointerId;
            root.CapturePointer(e.pointerId);
        }

        private void OnCardHover(int index, bool entered)
        {
            if (entered && dragFrom < 0 && cardPressed < 0 && cards[index].Member != null) Play(UiSound.Hover);
            if (entered) hoverCard = index;
            else if (hoverCard == index) hoverCard = -1;
            RefreshDetails();
        }

        private void BeginCardDrag(Vector2 position)
        {
            cardDragging = true;
            if (cardPressed < 0 || cardPressed >= cards.Count) return;

            cardSlotPositions.Clear();
            foreach (Card card in cards) cardSlotPositions.Add(card.Element.layout.position);

            VisualElement source = cards[cardPressed].Element;
            source.AddToClassList(CardDragSourceClass);
            Play(UiSound.Pick);
            cardGrabOffset = cardPressPosition - source.worldBound.position;
            cardGhost = CreateCardGhost(cards[cardPressed]);
            MoveCardGhost(position);

            cardPreviewTo = cardPressed;
            SetCardPreview(PreviewTargetAt(position));
        }

        /// <summary>札の写し（見た目だけで触れない）。パネルの最後に足して、ほかの何よりも上に描く。</summary>
        private VisualElement CreateCardGhost(Card card)
        {
            VisualElement ghostCard = CreateCardElement("party-card-ghost", out VisualElement hand);
            ghostCard.AddToClassList(CardGhostClass);
            Inventory items = card.Inventory != null ? card.Inventory.Inventory : null;
            int held = card.Hotbar != null ? card.Hotbar.SelectedIndex : -1;
            for (int k = 0; k < PlayerHotbar.SlotCount; k++)
            {
                VisualElement slot = ItemSlot.Create(null, (k + 1).ToString());
                slot.AddToClassList("party-card__slot");
                ItemInstance item = items != null ? items[k] : null;
                ItemSlot.SetItem(slot, item?.Icon, RankColor(item));
                slot.EnableInClassList(ItemSlot.SelectedClass, k == held);
                hand.Add(slot);
            }

            FillCard(ghostCard, card.Member, cardPressed);
            ghostCard.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            panel.Add(ghostCard);
            return ghostCard;
        }

        private void MoveCardGhost(Vector2 position)
        {
            if (cardGhost == null) return;

            // 絶対配置の left/top はパネルの枠線の内側から測るので、そのぶん引く。
            Vector2 local = panel.WorldToLocal(position - cardGrabOffset);
            cardGhost.style.left = local.x - panel.resolvedStyle.borderLeftWidth;
            cardGhost.style.top = local.y - panel.resolvedStyle.borderTopWidth;
        }

        /// <summary>
        /// ポインタの位置で離したら掴んだ札が入る番号。札の一覧（の少し外まで）の上なら一番近い札の位置、外なら元の位置（並びは変えない）。
        /// 動いている札の見た目ではなく、ドラッグを始めたときの位置で決めるので、札が滑ってもぶれない。
        /// </summary>
        private int PreviewTargetAt(Vector2 position)
        {
            if (partyList == null || cardSlotPositions.Count == 0) return cardPressed;

            Rect area = partyList.worldBound;
            const float reach = 24f;
            area.xMin -= reach;
            area.yMin -= reach;
            area.xMax += reach;
            area.yMax += reach;
            if (!area.Contains(position)) return cardPressed;

            Vector2 local = partyList.WorldToLocal(position);
            int best = cardPressed;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < cardSlotPositions.Count; i++)
            {
                Vector2 size = cards[i].Element.layout.size;
                float distance = (cardSlotPositions[i] + size * 0.5f - local).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>掴んだ札を to 番目に入れたときの並びへ、札を滑らせ、番号と操作中の印を出し直す。</summary>
        private void SetCardPreview(int to)
        {
            if (to == cardPreviewTo || to < 0) return;
            cardPreviewTo = to;
            Play(UiSound.Reorder);

            for (int i = 0; i < cards.Count && i < cardSlotPositions.Count; i++)
            {
                int place = PreviewIndex(i, cardPressed, to);
                Vector2 offset = cardSlotPositions[place] - cardSlotPositions[i];
                cards[i].Element.style.translate = new Translate(offset.x, offset.y);
                SetCardPlace(cards[i].Element, cards[i].Member, place);
            }

            if (cardGhost != null) SetCardPlace(cardGhost, cards[cardPressed].Member, to);
            RefreshDetails();
        }

        /// <summary>
        /// from 番目の人を to 番目へ移したとき、今 index 番目に居る人が何番目になるか（<see cref="PartyOrder{T}.Move"/> と同じ規則）。
        /// </summary>
        public static int PreviewIndex(int index, int from, int to)
        {
            if (index == from) return to;
            if (from < to && index > from && index <= to) return index - 1;
            if (to < from && index >= to && index < from) return index + 1;
            return index;
        }

        /// <summary>枠の物のドラッグ中、離すとその人の手持ちへ入る札を灯す。</summary>
        private void SetDropCard(int index)
        {
            if (index == dropCard) return;

            if (dropCard >= 0 && dropCard < cards.Count) cards[dropCard].Element.RemoveFromClassList(CardDropTargetClass);
            dropCard = index;
            if (dropCard >= 0 && dropCard < cards.Count) cards[dropCard].Element.AddToClassList(CardDropTargetClass);
        }

        private void CancelCardDrag()
        {
            if (cardPressed < 0) return;
            EndCardDrag();
        }

        private void EndCardDrag()
        {
            int pointerId = dragPointerId;
            if (cardDragging)
            {
                foreach (Card card in cards)
                {
                    card.Element.RemoveFromClassList(CardDragSourceClass);
                    card.Element.style.translate = StyleKeyword.Null;
                }

                RefreshCards();
            }

            cardGhost?.RemoveFromHierarchy();
            cardGhost = null;
            cardSlotPositions.Clear();
            cardPreviewTo = -1;
            SetDropCard(-1);
            cardPressed = -1;
            cardDragging = false;
            dragPointerId = -1;
            if (root != null && pointerId >= 0 && root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
            RefreshDetails();
        }

        /// <summary>
        /// 落とした札を、写しの居た位置 from（パネル座標）から今の位置へ滑り込ませる。
        /// 最初の 1 コマは transition を切ってずらして置き、次のコマで戻すと動いて見える。
        /// </summary>
        private static void SettleCard(VisualElement element, Vector2 slotWorld, Vector2 from)
        {
            Vector2 offset = from - slotWorld;
            if (offset.sqrMagnitude < 1f) return;

            element.AddToClassList(CardSettlingClass);
            element.style.translate = new Translate(offset.x, offset.y);
            element.schedule.Execute(() =>
            {
                element.RemoveFromClassList(CardSettlingClass);
                element.schedule.Execute(() => element.style.translate = StyleKeyword.Null);
            });
        }

        /// <summary>パネル座標 position の下にある札の番号。無ければ -1。</summary>
        private int CardAt(Vector2 position)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Element.worldBound.Contains(position)) return i;
            }

            return -1;
        }

        // ---- 枠のクリックとドラッグ ----------------------------------------

        private void OnSlotPointerDown(int index, PointerDownEvent e)
        {
            if (!IsOpen || e.button != 0 || dragFrom >= 0 || cardPressed >= 0) return;

            SlotRef slotRef = slotRefs[index];
            // 空の枠は札の並べ替えに任せる（札まで伝える）。
            if (slotRef.Item == null) return;

            e.StopPropagation();

            if (IsHeld(quickMoveAction))
            {
                if (QuickMove(slotRef))
                {
                    Play(slotRef.Items == Bag ? UiSound.ToHand : UiSound.ToBag);
                }
                else
                {
                    UiTransitions.Flash(slots[index], DeniedClass, 160);
                    Play(UiSound.Denied);
                }

                return;
            }

            BeginDrag(index, e.pointerId, e.position);
        }

        /// <summary>手持ちの物はバッグの空きへ、バッグの物は先頭の手持ちの空きへ移す。</summary>
        private bool QuickMove(SlotRef slotRef)
        {
            Inventory bag = Bag;
            if (slotRef.Items == bag)
            {
                PlayerInventory lead = LeadInventory;
                return lead != null && InventoryTransfer.QuickMove(bag, slotRef.Index, lead.Inventory, bag);
            }

            return InventoryTransfer.QuickMove(slotRef.Items, slotRef.Index, slotRef.Items, bag);
        }

        private void BeginDrag(int index, int pointerId, Vector2 position)
        {
            dragFrom = index;
            dragPointerId = pointerId;
            slots[index].AddToClassList(DragSourceClass);
            Play(UiSound.Pick);

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
                if (!cardDragging && Vector2.Distance(e.position, cardPressPosition) >= CardDragThreshold) BeginCardDrag(e.position);
                if (cardDragging)
                {
                    MoveCardGhost(e.position);
                    SetCardPreview(PreviewTargetAt(e.position));
                }

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
                int onto = dragging ? PreviewTargetAt(e.position) : -1;
                // 写しは少し大きくしてあるので、worldBound ではなくポインタから大きさを戻す前の左上を出す。
                Vector2 ghostWorld = (Vector2)e.position - cardGrabOffset;
                Vector2 slotWorld = onto >= 0 && onto < cardSlotPositions.Count
                    ? partyList.LocalToWorld(cardSlotPositions[onto])
                    : Vector2.zero;
                EndCardDrag();
                if (!dragging) return;
                if (onto < 0)
                {
                    Play(UiSound.Back); // 一覧の外で離した。並びはそのまま
                    return;
                }

                // 並びが変わると札は作り直される（位置は見せていた並びと同じなので跳ばない）。落とした札だけ写しの位置から滑り込ませる。
                if (onto != pressed && party != null) party.Move(pressed, onto);
                Play(UiSound.Place);
                if (onto < cards.Count) SettleCard(cards[onto].Element, slotWorld, ghostWorld);
                return;
            }

            if (dragFrom < 0) return;

            int from = dragFrom;
            int to = SlotAt(e.position);
            int card = to < 0 ? CardAt(e.position) : -1;
            bool outside = panel != null && !panel.worldBound.Contains(e.position);
            EndDrag();

            SlotRef source = slotRefs[from];
            if (to == from)
            {
                Play(UiSound.Back); // 元の枠で離した
            }
            else if (to >= 0)
            {
                SlotRef target = slotRefs[to];
                bool moved = InventoryTransfer.Move(source.Items, source.Index, target.Items, target.Index);
                Play(!moved ? UiSound.Denied : target.Items == Bag ? UiSound.ToBag : UiSound.ToHand);
            }
            else if (card >= 0)
            {
                GiveToCard(source, card);
            }
            else if (outside)
            {
                Play(LeadInventory != null && LeadInventory.Drop(source.Items, source.Index) ? UiSound.Discard : UiSound.Denied);
            }
            else
            {
                Play(UiSound.Back); // パネルの中の何も無いところで離した。元に戻る
            }
        }

        /// <summary>枠の物を、card 番目の札の人の手持ちの空きへ入れる。空きが無ければ札を赤く灯す。</summary>
        private void GiveToCard(SlotRef source, int card)
        {
            Inventory hand = cards[card].Inventory != null ? cards[card].Inventory.Inventory : null;
            if (hand == null || hand == source.Items)
            {
                Play(UiSound.Back);
                return;
            }

            int to = InventoryTransfer.FirstEmpty(hand);
            if (to >= 0 && InventoryTransfer.Move(source.Items, source.Index, hand, to))
            {
                Play(UiSound.ToHand);
                return;
            }

            UiTransitions.Flash(cards[card].Element, CardDeniedClass, 160);
            Play(UiSound.Denied);
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
            if (LeadInventory != null && LeadInventory.Drop(slotRef.Items, slotRef.Index)) Play(UiSound.Discard);
        }

        private static void Play(UiSound sound) => GameAudio.Instance?.PlayUi(sound);

        // ---- 情報欄 --------------------------------------------------------

        private void OnSlotHover(int index, bool entered)
        {
            // 空の枠まで鳴らすと、なぞるだけでうるさいので物の入った枠だけ。
            if (entered && dragFrom < 0 && cardPressed < 0 && slotRefs[index].Item != null) Play(UiSound.Hover);
            if (entered) hoverIndex = index;
            else if (hoverIndex == index) hoverIndex = -1;
            RefreshDetails();
        }

        /// <summary>ドラッグ中は掴んでいるもの（札なら離したときの位置）、そうでなければ合わせている枠のもの（か仲間）を情報欄に出す。</summary>
        private void RefreshDetails()
        {
            if (details == null) return;

            // 札を並べ替えている間は、掴んだ人を離したときの位置で出す。
            if (cardDragging && cardPressed >= 0 && cardPressed < cards.Count && cards[cardPressed].Member != null)
            {
                ShowMemberDetails(cards[cardPressed].Member, cardPreviewTo >= 0 ? cardPreviewTo : cardPressed);
                return;
            }

            int index = dragFrom >= 0 ? dragFrom : hoverIndex;
            ItemInstance item = index >= 0 && index < slotRefs.Count ? slotRefs[index].Item : null;

            if (item == null && dragFrom < 0 && hoverCard >= 0 && hoverCard < cards.Count && cards[hoverCard].Member != null)
            {
                ShowMemberDetails(cards[hoverCard].Member, hoverCard);
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
                hint.text = QuickMoveHint(party != null);
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

        /// <summary>
        /// クイック移動キーを押している間の案内。仲間が居るときは札の案内の行を残して、普段と同じ 2 行にする
        /// （行が減るとパネルが縮み、中央寄せなので上下にも動く。空の行は詰められて高さを保てない）。
        /// </summary>
        public static string QuickMoveHint(bool party) => party ? QuickMoveHintText + "\n" + PartyHintText : QuickMoveHintText;

        public const string PartyHintText = "札の枠にドラッグで装備　札をドラッグで並べ替え（先頭が操作するキャラ）";

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
