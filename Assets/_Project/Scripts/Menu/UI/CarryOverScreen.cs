using System.Collections;
using System.Collections.Generic;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Audio.UI;
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
    /// 転生の渦に入ったあと、持ち越す武器を選ぶ画面（<see cref="ICarryOverPicker"/>）。CarryOver.uxml を UIDocument に差して、パーティーの操作台の子に置く。
    /// 並びはインベントリ画面と同じ（仲間の札の 4 枠と、共有のバッグ）。物の入った枠をクリックで選ぶ・外し、上限（魂の強化の「形見」）まで選べる。
    /// 白く飛んだ画面から白が引いて出てきて、決めると選ばなかった物が砕けて消え、また白へ溶けて終わる（そのまま地上へ移る）。
    /// 決めるまでは閉じられない。開いている間はゲームを止める（<see cref="GamePauser"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Carry Over Screen")]
    public sealed class CarryOverScreen : MonoBehaviour, ICarryOverPicker
    {
        private const string KeptClass = "carry-slot--kept";
        private const string EmptyClass = "carry-slot--empty";
        private const string ItemClass = "carry-slot--item";
        private const string LostClass = "carry-slot--lost";
        private const string FullClass = "carry--full";
        private const string LeavingClass = "carry-screen--leaving";

        private sealed class Slot
        {
            public VisualElement Element;
            public ItemInstance Item;
        }

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定なら親から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("操作の設定の窓口。未設定なら親から探す。")]
        private PlayerControlSettings controls;

        [SerializeField, Tooltip("開くときに閉じる大きな地図。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("ゲーム中に使うアクションマップ。")]
        private string gameplayMapName = "Player";

        [SerializeField, Tooltip("開いている間に使うアクションマップ。")]
        private string menuMapName = "UI";

        private UIDocument document;
        private VisualElement screen;
        private VisualElement panel;
        private VisualElement partyList;
        private VisualElement bag;
        private VisualElement details;
        private VisualElement detailsIcon;
        private Label detailsTitle;
        private Label detailsBody;
        private Label count;
        private Button confirm;
        private GamePauser pauser;

        private readonly List<Slot> slots = new List<Slot>();
        private readonly HashSet<ItemInstance> kept = new HashSet<ItemInstance>();
        private int limit;
        private bool confirmed;

        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (controls == null) controls = GetComponentInParent<PlayerControlSettings>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void OnEnable()
        {
            GameFlow.CarryOverPicker = this;

            VisualElement root = document.rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;

            screen = root.Q<VisualElement>("carry-screen");
            panel = root.Q<VisualElement>("carry-panel");
            partyList = root.Q<VisualElement>("party-list");
            bag = root.Q<VisualElement>("bag");
            details = root.Q<VisualElement>("details");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsTitle = root.Q<Label>("details-title");
            detailsBody = root.Q<Label>("details-body");
            count = root.Q<Label>("count");
            confirm = root.Q<Button>("confirm-button");
            if (confirm != null) confirm.clicked += Confirm;
            UiSoundHooks.Silence(confirm);
            UiSoundHooks.Attach(root);

            UiTransitions.HideImmediately(screen);
            UiTransitions.HideImmediately(panel);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GameFlow.CarryOverPicker, this)) GameFlow.CarryOverPicker = null;
            if (Pauser.IsPaused) Pauser.Resume();
            UiSoundHooks.Detach(document.rootVisualElement);
        }

        private void LateUpdate() => Pauser.KeepCursorFree();

        public IEnumerator Pick(PartyGroup party, int max, ICollection<ItemInstance> keep)
        {
            if (screen == null || party == null)
            {
                yield break;
            }

            limit = Mathf.Max(0, max);
            kept.Clear();
            confirmed = false;
            Pauser.Pause();
            Build(party);
            RefreshCount();
            ShowDetails(null);
            screen.RemoveFromClassList(LeavingClass);
            UiTransitions.Show(screen);
            UiTransitions.Show(panel);
            GameAudio.Instance?.PlayUi(UiSound.Open);

            while (!confirmed) yield return null;

            // 選ばなかった物を砕いて消し、白へ戻す。
            int i = 0;
            foreach (Slot slot in slots)
            {
                if (slot.Item == null || kept.Contains(slot.Item)) continue;
                VisualElement element = slot.Element;
                UiTween.After(element, 40L * i++, () => element.AddToClassList(LostClass));
            }
            GameAudio.Instance?.PlayUi(UiSound.Discard);
            yield return WaitRealtime(0.45f + 0.04f * i);

            screen.AddToClassList(LeavingClass);
            yield return WaitRealtime(0.75f);

            foreach (ItemInstance item in kept) keep.Add(item);
            UiTransitions.HideImmediately(panel);
            UiTransitions.HideImmediately(screen);
            screen.RemoveFromClassList(LeavingClass);
            Pauser.Resume();
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        private void Build(PartyGroup party)
        {
            slots.Clear();
            partyList.Clear();
            bag.Clear();

            for (int i = 0; i < party.Members.Count; i++)
            {
                PartyMember member = party.Members[i];
                if (member == null) continue;

                VisualElement card = InventoryScreen.CreateCardElement($"carry-card-{i}", out VisualElement hand);
                InventoryScreen.FillCard(card, member, i);
                Inventory items = member.Inventory != null ? member.Inventory.Inventory : null;
                for (int k = 0; k < PlayerHotbar.SlotCount; k++)
                {
                    VisualElement slot = CreateSlot(items != null ? items[k] : null, (k + 1).ToString());
                    slot.AddToClassList("party-card__slot");
                    hand.Add(slot);
                }
                partyList.Add(card);
            }

            Inventory shared = party.Bag;
            for (int i = 0; i < shared.Count; i++) bag.Add(CreateSlot(shared[i], null));
        }

        private VisualElement CreateSlot(ItemInstance item, string number)
        {
            VisualElement element = ItemSlot.Create($"carry-slot-{slots.Count}", number);
            ItemSlot.SetItem(element, item?.Icon, InventoryScreen.RankColor(item));
            element.AddToClassList(item != null ? ItemClass : EmptyClass);

            var mark = new Label("残す") { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("carry-slot__mark");
            mark.AddToClassList("rpg-bold");
            element.Add(mark);

            var slot = new Slot { Element = element, Item = item };
            element.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0) Toggle(slot);
            });
            element.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(slot.Item));
            element.RegisterCallback<PointerLeaveEvent>(_ => ShowDetails(null));
            slots.Add(slot);
            return element;
        }

        private void Toggle(Slot slot)
        {
            if (confirmed || slot.Item == null) return;

            if (kept.Remove(slot.Item))
            {
                slot.Element.RemoveFromClassList(KeptClass);
                GameAudio.Instance?.PlayUi(UiSound.Back);
            }
            else if (kept.Count >= limit)
            {
                UiTransitions.Flash(count, "carry__count--denied", 300);
                GameAudio.Instance?.PlayUi(UiSound.Denied);
                return;
            }
            else
            {
                kept.Add(slot.Item);
                slot.Element.AddToClassList(KeptClass);
                GameAudio.Instance?.PlayUi(UiSound.Pick);
            }
            RefreshCount();
        }

        private void RefreshCount()
        {
            if (count != null)
            {
                count.text = $"残す {kept.Count} / {limit}";
                count.EnableInClassList("carry__count--full", kept.Count >= limit);
            }
            panel?.EnableInClassList(FullClass, kept.Count >= limit);
        }

        private void ShowDetails(ItemInstance item)
        {
            if (details == null) return;
            details.EnableInClassList("inventory__details--empty", item == null);
            if (detailsTitle != null) detailsTitle.text = item != null ? item.DisplayName : string.Empty;
            if (detailsBody != null) detailsBody.text = item != null ? item.DetailText() : string.Empty;
            if (detailsIcon != null)
            {
                detailsIcon.style.backgroundImage = item != null && item.Icon != null
                    ? new StyleBackground(item.Icon)
                    : new StyleBackground(StyleKeyword.None);
                ItemSlot.SetRankGlow(detailsIcon.parent, InventoryScreen.RankColor(item));
            }
        }

        private void Confirm()
        {
            if (confirmed) return;
            confirmed = true;
            GameAudio.Instance?.PlayUi(UiSound.Click);
        }
    }
}
