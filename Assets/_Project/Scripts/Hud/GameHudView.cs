using TpsDungeon.Combat;
using TpsDungeon.Items;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;
using TpsDungeon.UiKit;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 常時表示の HUD（左下 レベル・HP・経験値、下中央ホットバーと選んでいるアイテムの名前、右下マップ）と、マップキーで開く大きな地図。
    /// GameHud.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    /// 表示は PlayerHealth / CharacterProgression / PlayerHotbar / PlayerInventory / MeleeAttacker / RangedAttacker / PlayerMapToggle / 生成済みフロアの状態を写すだけで、入力は扱わない。
    /// 次に振れる・撃てるまでの待ち（武器種の待ち・持ち替えの待ち）は、選んでいる枠の暗幕で見せる。
    /// 遠距離の武器（弓など）を持っている間は、画面の中央に照準を出す。
    /// 経験値は数字を出さず、帯の伸びだけで見せる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Game HUD View")]
    public sealed class GameHudView : MonoBehaviour
    {
        // 残りがこの割合を切ったら HP を警告色にする。
        private const float LowHpFraction = 0.3f;

        // レベルアップで経験値の帯を端まで伸ばしてから、空に戻して余りを伸ばし直すまでの間（ミリ秒）。USS の .exp__fill の伸びる時間に合わせる。
        private const long ExpLevelUpHoldMs = 380;

        // 待ちが明けたときに枠を光らせておく時間（ミリ秒）。
        private const long CooldownReadyFlashMs = 120;

        // フロアが無いシーンで毎フレーム探さないための間隔（秒）。
        private const float FloorSearchInterval = 1f;

        [SerializeField, Tooltip("HP の出どころ。未設定なら親から探す。")]
        private PlayerHealth health;

        [SerializeField, Tooltip("レベルと経験値の出どころ。未設定なら親から探す。")]
        private CharacterProgression progression;

        [SerializeField, Tooltip("ホットバーの選択の出どころ。未設定なら親から探す。")]
        private PlayerHotbar hotbar;

        [SerializeField, Tooltip("ホットバーの枠に入っているアイテムの出どころ。未設定なら親から探す。")]
        private PlayerInventory inventory;

        [SerializeField, Tooltip("次に振れるまでの待ちの出どころ。未設定なら親から探す。")]
        private MeleeAttacker attacker;

        [SerializeField, Tooltip("次に撃てるまでの待ちと、照準を出すか（遠距離の武器を持っているか）の出どころ。未設定なら親から探す。")]
        private RangedAttacker ranged;

        [SerializeField, Tooltip("大きな地図を開いているかの出どころ。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("マップ上の現在地と向きに使う Transform。未設定なら PlayerHealth の GameObject。")]
        private Transform player;

        private UIDocument document;
        private VisualElement hpRoot;
        private VisualElement hpFill;
        private VisualElement hpTrail;
        private int lastHp = -1;
        private bool beating;
        private Label hpValue;
        private VisualElement levelRoot;
        private Label levelValue;
        private VisualElement expRoot;
        private VisualElement expFill;

        // 獲得でレベルが上がった（LeveledUp は ExpChanged より先に来る）。保存の読み込みで上がったときは演出しない。
        private bool levelUpPending;

        // レベルアップの伸ばし直しの予約。続けて上がったら前の予約は捨てる。
        private IVisualElementScheduledItem expRefill;
        private VisualElement[] slots;
        private Label itemName;

        // 選んでいる枠に出している待ちの割合。明けた瞬間を拾うために覚えておく。
        private int cooldownSlot = -1;
        private float shownCooldown;

        // 今出しているアイテム名。空の枠を選んでいる間は null。
        private string shownItemName;
        private VisualElement minimapFrame;
        private VisualElement crosshair;
        private bool crosshairShown;
        private VisualElement mapOverlay;

        // 右下の小さい地図と、開いたときの大きな地図。同じ内容を縮尺（USS の --map-pixels-per-cell）だけ変えて描く。
        private MinimapElement[] maps;

        private FloorBuilder floorBuilder;
        private FloorExploration exploration;
        private float nextFloorSearchTime;

        private void Reset()
        {
            health = GetComponentInParent<PlayerHealth>();
            progression = GetComponentInParent<CharacterProgression>();
            hotbar = GetComponentInParent<PlayerHotbar>();
            inventory = GetComponentInParent<PlayerInventory>();
            attacker = GetComponentInParent<MeleeAttacker>();
            ranged = GetComponentInParent<RangedAttacker>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            if (progression == null) progression = GetComponentInParent<CharacterProgression>();
            if (hotbar == null) hotbar = GetComponentInParent<PlayerHotbar>();
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (attacker == null) attacker = GetComponentInParent<MeleeAttacker>();
            if (ranged == null) ranged = GetComponentInParent<RangedAttacker>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
            if (player == null && health != null) player = health.transform;
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            // HUD はクリックを受けない。上に重なる設定画面などの操作を邪魔しないように。
            root.pickingMode = PickingMode.Ignore;
            hpRoot = root.Q<VisualElement>("hp");
            hpFill = root.Q<VisualElement>("hp-fill");
            hpTrail = root.Q<VisualElement>("hp-trail");
            hpValue = root.Q<Label>("hp-value");
            levelRoot = root.Q<VisualElement>("level");
            levelValue = root.Q<Label>("level-value");
            expRoot = root.Q<VisualElement>("exp");
            expFill = root.Q<VisualElement>("exp-fill");
            levelUpPending = false;
            minimapFrame = root.Q<VisualElement>("minimap");
            mapOverlay = root.Q<VisualElement>("map-overlay");
            UiTransitions.HideImmediately(mapOverlay);
            crosshair = root.Q<VisualElement>("crosshair");
            UiTransitions.HideImmediately(crosshair);
            crosshairShown = false;
            lastHp = -1;
            beating = false;
            maps = root.Query<MinimapElement>().ToList().ToArray();
            slots = BuildSlots(root.Q<VisualElement>("hotbar"));
            itemName = root.Q<Label>("hotbar-name");
            UiTransitions.HideImmediately(itemName);
            shownItemName = null;
            cooldownSlot = -1;
            shownCooldown = 0f;

            if (health != null) health.Changed += OnHealthChanged;
            if (progression != null) progression.ExpChanged += OnExpChanged;
            if (progression != null) progression.LeveledUp += OnLeveledUp;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            if (mapToggle != null) mapToggle.Changed += OnMapToggleChanged;
            RefreshHealth();
            RefreshProgression();
            RefreshHotbar();
            RefreshHotbarItems();
            RefreshMapOverlay();
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (progression != null) progression.ExpChanged -= OnExpChanged;
            if (progression != null) progression.LeveledUp -= OnLeveledUp;
            expRefill?.Pause();
            expRefill = null;
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            if (mapToggle != null) mapToggle.Changed -= OnMapToggleChanged;
            UnbindFloor();
        }

        private void LateUpdate()
        {
            // フロアはプレイヤーより後に生成されることがあるので、見つかるまでときどき探す。
            if (floorBuilder == null && Time.unscaledTime >= nextFloorSearchTime)
            {
                nextFloorSearchTime = Time.unscaledTime + FloorSearchInterval;
                BindFloor(FindAnyObjectByType<FloorBootstrap>());
            }
            UpdateMinimap();
            RefreshCooldown();
            RefreshCrosshair();
        }

        private static VisualElement[] BuildSlots(VisualElement container)
        {
            if (container == null) return null;

            container.Clear();
            var result = new VisualElement[PlayerHotbar.SlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                var slot = ItemSlot.Create($"slot-{i + 1}", (i + 1).ToString());
                slot.pickingMode = PickingMode.Ignore;
                container.Add(slot);
                result[i] = slot;
            }
            return result;
        }

        private void OnHealthChanged(PlayerHealth _) => RefreshHealth();

        private void OnExpChanged(CharacterProgression _) => RefreshProgression();

        private void OnLeveledUp(CharacterProgression _, int from, int to) => levelUpPending = true;

        private void OnHotbarChanged(PlayerHotbar _) => RefreshHotbar();

        private void OnInventoryChanged(PlayerInventory _) => RefreshHotbarItems();

        private void OnMapToggleChanged(PlayerMapToggle _) => RefreshMapOverlay();

        /// <summary>大きな地図を開いている間は、同じものを描いている右下の小さい地図を隠す。</summary>
        private void RefreshMapOverlay()
        {
            bool open = mapToggle != null && mapToggle.IsOpen;
            // 大きな地図は拡がりながら現れ、右下の小さい地図は入れ替わりにフェードで退く（GameHud.uss）。
            UiTransitions.SetShown(mapOverlay, open);
            UiTransitions.SetShown(minimapFrame, !open);
        }

        private void RefreshHealth()
        {
            if (hpRoot == null) return;

            // 体力の持ち主がいない（確認用シーンなど）なら HP 欄ごと隠す。
            hpRoot.style.display = health != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (health == null) return;

            float fraction = health.Fraction;
            if (hpFill != null) hpFill.style.width = Length.Percent(fraction * 100f);

            // 削れ跡は USS の遅れ付きトランジションで、本体を後からゆっくり追いかける。
            if (hpTrail != null) hpTrail.style.width = Length.Percent(fraction * 100f);

            // 減った瞬間だけ小さく揺らす。
            if (lastHp >= 0 && health.CurrentHp < lastHp) UiTransitions.Flash(hpRoot, "hp--hit", 80);
            lastHp = health.CurrentHp;

            SetBeating(fraction < LowHpFraction);
            if (hpValue != null) hpValue.text = HpText(health.CurrentHp, health.MaxHp);
            hpRoot.EnableInClassList("hp--low", fraction < LowHpFraction);
        }

        /// <summary>
        /// レベルの数字と経験値の帯。レベルが上がったときは帯を端まで伸ばし、数字を光らせてから、
        /// 空に戻して今のレベル内の分だけ伸ばし直す。リセットや保存の読み込みで変わったときはそのまま合わせる。
        /// </summary>
        private void RefreshProgression()
        {
            bool has = progression != null;
            if (levelRoot != null) levelRoot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (expRoot != null) expRoot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (!has) return;

            bool leveledUp = levelUpPending;
            levelUpPending = false;

            if (levelValue != null) levelValue.text = LevelText(progression.Level);
            if (expRoot != null) expRoot.EnableInClassList("exp--max", progression.IsMaxLevel);
            if (expFill == null) return;

            if (!leveledUp)
            {
                // 伸ばし直しを待っている間の獲得は、予約の側が最新の値で伸ばすので任せる。
                if (expRefill == null) SetExpWidth(CurrentExpFraction());
                return;
            }

            if (levelRoot != null) UiTransitions.Flash(levelRoot, "level--up", 260);
            SetExpWidth(1f);
            expRefill?.Pause();
            expRefill = expFill.schedule.Execute(RefillExpAfterLevelUp).StartingIn(ExpLevelUpHoldMs);
        }

        private void RefillExpAfterLevelUp()
        {
            expRefill = null;
            if (expFill == null || progression == null) return;

            // 減っていく様子を見せないよう、トランジションを切って空に戻し、次のフレームで余りまで伸ばす。
            expFill.AddToClassList("exp__fill--instant");
            SetExpWidth(0f);
            expFill.schedule.Execute(() =>
            {
                expFill.RemoveFromClassList("exp__fill--instant");
                if (progression != null) SetExpWidth(CurrentExpFraction());
            }).StartingIn(16);
        }

        private float CurrentExpFraction() => progression.IsMaxLevel ? 1f : ExpFraction(progression.Exp, progression.ExpToNext);

        private void SetExpWidth(float fraction)
        {
            expFill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }

        /// <summary>残りが少ない間は枠と帯を鼓動させる。</summary>
        private void SetBeating(bool beat)
        {
            if (beat == beating) return;

            beating = beat;
            if (beat) UiTransitions.Pulse(hpRoot, "hp--beat", 520);
            else UiTransitions.StopPulse(hpRoot, "hp--beat");
        }

        private void RefreshHotbar()
        {
            if (slots == null) return;

            int selected = hotbar != null ? hotbar.SelectedIndex : -1;
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList(ItemSlot.SelectedClass, i == selected);
            RefreshItemName();
        }

        /// <summary>
        /// 次に振れるまでの待ちを、選んでいる枠の暗幕に写す。ほかの枠は空ける。
        /// 炎の杖で吐いている間は、使った量を同じ暗幕で写す（待ちとは逆に、空から暗くなっていく）。
        /// 同じ枠のまま待ちが明けたら、枠を短く光らせて振れるようになったと知らせる。
        /// </summary>
        private void RefreshCooldown()
        {
            if (slots == null) return;

            int selected = hotbar != null ? hotbar.SelectedIndex : -1;
            float fraction = Mathf.Max(attacker != null && attacker.isActiveAndEnabled ? attacker.CooldownFraction : 0f,
                ranged != null && ranged.isActiveAndEnabled ? ranged.CooldownFraction : 0f);
            for (int i = 0; i < slots.Length; i++) ItemSlot.SetCooldown(slots[i], i == selected ? fraction : 0f);

            if (selected == cooldownSlot && shownCooldown > 0f && fraction <= 0f && selected >= 0)
                UiTransitions.Flash(slots[selected], ItemSlot.ReadyClass, CooldownReadyFlashMs);
            cooldownSlot = selected;
            shownCooldown = fraction;
        }

        /// <summary>遠距離の武器を持っている間だけ照準を出す。大きな地図を開いている間は隠す。</summary>
        private void RefreshCrosshair()
        {
            if (crosshair == null) return;

            bool show = ranged != null && ranged.isActiveAndEnabled && ranged.HeldWeapon != null && (mapToggle == null || !mapToggle.IsOpen);
            if (show == crosshairShown) return;

            crosshairShown = show;
            UiTransitions.SetShown(crosshair, show);
        }

        /// <summary>ホットバーの枠にアイテムの絵を入れる。ホットバーの枠はインベントリの先頭の枠。</summary>
        private void RefreshHotbarItems()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                ItemInstance item = inventory != null ? inventory.Inventory[i] : null;
                ItemSlot.SetItem(slots[i], item?.Icon, RankColor(item));
            }

            RefreshItemName();
        }

        /// <summary>
        /// 選んでいる枠のアイテム名をホットバーの上に出す。空の枠なら消す。
        /// 選び直したり中身が入れ替わったりして名前が変わったときだけ、小さく沈んで浮き直す。
        /// </summary>
        private void RefreshItemName()
        {
            if (itemName == null) return;

            string name = ItemNameText(SelectedItem());
            if (name == shownItemName) return;

            bool wasShown = shownItemName != null;
            shownItemName = name;
            if (name == null)
            {
                // 文字は消さずにおき、フェードアウトの間も前の名前を見せる。
                UiTransitions.Hide(itemName);
                return;
            }

            itemName.text = name;
            UiTransitions.Show(itemName);
            if (wasShown) UiTransitions.Flash(itemName, "hotbar-name--pop", 70);
        }

        private ItemInstance SelectedItem()
        {
            if (hotbar == null || inventory == null) return null;
            return inventory.Inventory[hotbar.SelectedIndex];
        }

        private void BindFloor(FloorBootstrap bootstrap)
        {
            if (bootstrap == null) return;

            floorBuilder = bootstrap.GetComponent<FloorBuilder>();
            if (floorBuilder == null) return;

            floorBuilder.Built += OnFloorBuilt;
            if (floorBuilder.CurrentLayout != null) OnFloorBuilt(floorBuilder.CurrentLayout);
        }

        private void UnbindFloor()
        {
            if (floorBuilder != null) floorBuilder.Built -= OnFloorBuilt;
            floorBuilder = null;
            exploration = null;
            if (maps == null) return;
            foreach (var m in maps) m.SetExploration(null);
        }

        /// <summary>作り直されたら探索状況も白紙に戻す。</summary>
        private void OnFloorBuilt(FloorLayout layout)
        {
            exploration = layout != null ? new FloorExploration(layout) : null;
            if (maps == null) return;
            foreach (var m in maps) m.SetExploration(exploration);
        }

        private void UpdateMinimap()
        {
            if (maps == null) return;
            if (player == null)
            {
                foreach (var m in maps) m.ClearPlayer();
                return;
            }

            if (floorBuilder == null || exploration == null)
            {
                // フロアが無いシーンでは向きだけ出す。
                foreach (var m in maps) m.SetPlayer(Vector2.zero, player.eulerAngles.y);
                return;
            }

            int previousRoom = exploration.CurrentRoom;
            bool firstVisit = exploration.Visit(floorBuilder.WorldToCell(player.position));
            if (firstVisit || exploration.CurrentRoom != previousRoom)
            {
                foreach (var m in maps) m.MarkDirtyRepaint();
            }

            var local = floorBuilder.transform.InverseTransformPoint(player.position);
            float cellSize = floorBuilder.CellSize;
            var cell = new Vector2(local.x / cellSize, local.z / cellSize);
            float yaw = player.eulerAngles.y - floorBuilder.transform.eulerAngles.y;
            foreach (var m in maps) m.SetPlayer(cell, yaw);
        }

        public static string HpText(int current, int max) => $"{current} / {max}";

        public static string LevelText(int level) => level.ToString();

        /// <summary>経験値の帯の伸び（0〜1）。次までが 0（最大レベル）なら満タン。</summary>
        public static float ExpFraction(int exp, int expToNext) => expToNext > 0 ? Mathf.Clamp01((float)exp / expToNext) : 1f;

        /// <summary>ホットバーの上に出す名前。アイテムが無ければ null（何も出さない）。</summary>
        private static Color? RankColor(ItemInstance item) => item != null && item.TryGetRankColor(out Color c) ? c : (Color?)null;

        public static string ItemNameText(ItemInstance item) => item != null ? item.DisplayName : null;
    }
}
