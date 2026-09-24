using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 常時表示の HUD（左下 HP・下中央ホットバー・右下マップ）と、マップキーで開く大きな地図。
    /// GameHud.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    /// 表示は PlayerHealth / PlayerHotbar / PlayerMapToggle / 生成済みフロアの状態を写すだけで、入力は扱わない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Game HUD View")]
    public sealed class GameHudView : MonoBehaviour
    {
        // 残りがこの割合を切ったら HP を警告色にする。
        private const float LowHpFraction = 0.3f;

        // フロアが無いシーンで毎フレーム探さないための間隔（秒）。
        private const float FloorSearchInterval = 1f;

        [SerializeField, Tooltip("HP の出どころ。未設定なら親から探す。")]
        private PlayerHealth health;

        [SerializeField, Tooltip("ホットバーの選択の出どころ。未設定なら親から探す。")]
        private PlayerHotbar hotbar;

        [SerializeField, Tooltip("大きな地図を開いているかの出どころ。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("マップ上の現在地と向きに使う Transform。未設定なら PlayerHealth の GameObject。")]
        private Transform player;

        private UIDocument document;
        private VisualElement hpRoot;
        private VisualElement hpFill;
        private Label hpValue;
        private VisualElement[] slots;
        private VisualElement minimapFrame;
        private VisualElement mapOverlay;

        // 右下の小さい地図と、開いたときの大きな地図。同じ内容を縮尺（USS の --map-pixels-per-cell）だけ変えて描く。
        private MinimapElement[] maps;

        private FloorBuilder floorBuilder;
        private FloorExploration exploration;
        private float nextFloorSearchTime;

        private void Reset()
        {
            health = GetComponentInParent<PlayerHealth>();
            hotbar = GetComponentInParent<PlayerHotbar>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            if (hotbar == null) hotbar = GetComponentInParent<PlayerHotbar>();
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
            hpValue = root.Q<Label>("hp-value");
            minimapFrame = root.Q<VisualElement>("minimap");
            mapOverlay = root.Q<VisualElement>("map-overlay");
            maps = root.Query<MinimapElement>().ToList().ToArray();
            slots = BuildSlots(root.Q<VisualElement>("hotbar"));

            if (health != null) health.Changed += OnHealthChanged;
            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (mapToggle != null) mapToggle.Changed += OnMapToggleChanged;
            RefreshHealth();
            RefreshHotbar();
            RefreshMapOverlay();
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
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
        }

        private static VisualElement[] BuildSlots(VisualElement container)
        {
            if (container == null) return null;

            container.Clear();
            var result = new VisualElement[PlayerHotbar.SlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                var slot = new VisualElement { name = $"slot-{i + 1}", pickingMode = PickingMode.Ignore };
                slot.AddToClassList("slot");

                var icon = new VisualElement { name = "slot-icon", pickingMode = PickingMode.Ignore };
                icon.AddToClassList("slot__icon");
                slot.Add(icon);

                var number = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                number.AddToClassList("slot__number");
                slot.Add(number);

                container.Add(slot);
                result[i] = slot;
            }
            return result;
        }

        private void OnHealthChanged(PlayerHealth _) => RefreshHealth();

        private void OnHotbarChanged(PlayerHotbar _) => RefreshHotbar();

        private void OnMapToggleChanged(PlayerMapToggle _) => RefreshMapOverlay();

        /// <summary>大きな地図を開いている間は、同じものを描いている右下の小さい地図を隠す。</summary>
        private void RefreshMapOverlay()
        {
            bool open = mapToggle != null && mapToggle.IsOpen;
            if (mapOverlay != null) mapOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (minimapFrame != null) minimapFrame.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshHealth()
        {
            if (hpRoot == null) return;

            // 体力の持ち主がいない（確認用シーンなど）なら HP 欄ごと隠す。
            hpRoot.style.display = health != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (health == null) return;

            float fraction = health.Fraction;
            if (hpFill != null) hpFill.style.width = Length.Percent(fraction * 100f);
            if (hpValue != null) hpValue.text = HpText(health.CurrentHp, health.MaxHp);
            hpRoot.EnableInClassList("hp--low", fraction < LowHpFraction);
        }

        private void RefreshHotbar()
        {
            if (slots == null) return;

            int selected = hotbar != null ? hotbar.SelectedIndex : -1;
            for (int i = 0; i < slots.Length; i++) slots[i].EnableInClassList("slot--selected", i == selected);
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
    }
}
