using System.Collections.Generic;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Audio.UI;
using TpsDungeon.Party;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 魂の強化の画面。死後の場所の燭台（<see cref="UpgradeAltar"/>）を調べると開く。SoulUpgrade.uxml を UIDocument に差して、パーティーの操作台の子に置く。
    /// - 真ん中に最初の強化があり、そこからマインドマップのように枝分かれする（並びは <see cref="RadialTreeLayout"/>）。
    ///   前の強化を解放していない強化は、この先の分かれ方が分かるように薄く出す（遠いほど薄く、<see cref="MaxGhostSteps"/> 段より先は出さない）。
    ///   買った・買える強化は光らせ、ソウルが足りない強化は暗くする。
    /// - ドラッグで動かし、ホイールでカーソルの所を中心に拡大・縮小する。強化に乗せると説明が出て、クリックで解放する。
    /// - 解放すると、右上のソウルから光の玉が強化へ飛び、強化が光って弾け、その先へ枝が伸びて新しい強化が現れる。
    ///   ソウルが足りなければ強化が震える。
    /// - 死んでから初めて開いたときは、先にレベルがソウルに変わる演出（<see cref="SoulHarvestView"/>）を見せる。
    /// 開いている間はゲームを止める（<see cref="GamePauser"/>）。Esc か閉じるボタンで閉じる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Soul Upgrade Screen")]
    public sealed class SoulUpgradeScreen : MonoBehaviour
    {
        private const string NodeClass = "soul-node";
        private const string RootClass = "soul-node--root";
        private const string OwnedClass = "soul-node--owned";
        private const string AvailableClass = "soul-node--available";
        private const string LockedClass = "soul-node--locked";
        private const string GlowClass = "soul-node--glow";
        private const string BurstClass = "soul-node--burst";
        private const string HoverClass = "soul-node--hover";
        private const string GhostClass = "soul-node--ghost";
        /// <summary>まだ見えない強化の濃さの段（soul-node--ghost-1 〜 -N。N より先は 0）。</summary>
        private const string GhostLevelClass = "soul-node--ghost-";
        private const int MaxGhostSteps = 5;
        private static readonly float[] GhostAlpha = { 1f, 0.32f, 0.2f, 0.12f, 0.07f, 0.04f, 0f };

        private const float NodeRadius = 30f;
        private const float RootRadius = 42f;
        private const float MinZoom = 0.35f;
        private const float MaxZoom = 1.6f;
        private const float DragThreshold = 6f;

        private sealed class NodeView
        {
            public SoulUpgradeNode Node;
            public VisualElement Element;
            public VisualElement Body;
            public Label Cost;
            public Vector2 Position;
            public int Depth;
            public SoulTreeLinks.Link Link;
            public bool Revealed;
            public bool Pulsing;
        }

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定なら親から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("操作の設定の窓口。未設定なら親から探す。")]
        private PlayerControlSettings controls;

        [SerializeField, Tooltip("開くときに閉じる大きな地図。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("閉じるアクション（UI マップ側）。")]
        private string cancelActionName = "UI/Cancel";

        [SerializeField, Tooltip("ゲーム中に使うアクションマップ。")]
        private string gameplayMapName = "Player";

        [SerializeField, Tooltip("開いている間に使うアクションマップ。")]
        private string menuMapName = "UI";

        private UIDocument document;
        private VisualElement root;
        private VisualElement screen;
        private VisualElement viewport;
        private VisualElement world;
        private SoulMotes motes;
        private VisualElement counter;
        private Label counterValue;
        private VisualElement tooltip;
        private Label tooltipTitle;
        private Label tooltipEffect;
        private Label tooltipBody;
        private Label tooltipState;
        private Label tooltipCost;
        private VisualElement tooltipPrice;
        private SoulTreeLinks links;
        private SoulFx fx;
        private SoulHarvestView harvest;
        private InputAction cancelAction;
        private GamePauser pauser;

        private readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        private readonly List<UiTween.Handle> tweens = new List<UiTween.Handle>();
        private SoulUpgradeTree builtFor;
        private float treeRadius;

        // 木の面の位置と拡大。target へなめらかに寄せる。
        private Vector2 pan;
        private float zoom = 1f;
        private Vector2 targetPan;
        private float targetZoom = 1f;
        private IVisualElementScheduledItem cameraTicker;

        private bool pressed;
        private bool dragging;
        private Vector2 pressPosition;
        private NodeView pressedNode;
        private NodeView hovered;
        private long shownSouls;
        private int openedFrame = -1;

        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);

        public bool IsOpen => Pauser.IsPaused;

        private void Reset()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            controls = GetComponentInParent<PlayerControlSettings>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (controls == null) controls = GetComponentInParent<PlayerControlSettings>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void OnEnable()
        {
            UpgradeAltar.Opened += OnAltarOpened;

            root = document.rootVisualElement;
            if (root == null) return;
            // 画面いっぱいに広げ、閉じている間はクリックを素通しする。
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;

            screen = root.Q<VisualElement>("soul-screen");
            viewport = root.Q<VisualElement>("viewport");
            world = root.Q<VisualElement>("world");
            motes = root.Q<SoulMotes>("motes");
            counter = root.Q<VisualElement>("souls");
            counterValue = root.Q<Label>("souls-value");
            root.Q<SoulGlyph>("souls-icon")?.SetFlame();
            tooltip = root.Q<VisualElement>("tooltip");
            tooltipTitle = root.Q<Label>("tooltip-title");
            tooltipEffect = root.Q<Label>("tooltip-effect");
            tooltipBody = root.Q<Label>("tooltip-body");
            tooltipState = root.Q<Label>("tooltip-state");
            tooltipCost = root.Q<Label>("tooltip-cost");
            tooltipPrice = root.Q<VisualElement>("tooltip-price");
            fx = new SoulFx(root.Q<VisualElement>("fx"));
            harvest = new SoulHarvestView(root.Q<VisualElement>("harvest"), fx, SetCounter, () => counter);

            var closeButton = root.Q<Button>("close-button");
            if (closeButton != null) closeButton.clicked += Close;
            UiSoundHooks.Silence(closeButton);
            var centerButton = root.Q<Button>("center-button");
            if (centerButton != null) centerButton.clicked += Recenter;
            UiSoundHooks.Attach(root);

            if (viewport != null)
            {
                viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
                viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
                viewport.RegisterCallback<PointerCaptureOutEvent>(_ => EndPress());
                viewport.RegisterCallback<WheelEvent>(OnWheel);
            }

            UiTransitions.HideImmediately(screen);
            UiTransitions.HideImmediately(tooltip);
            MetaUpgrades.Changed += OnUpgradesChanged;
        }

        private void OnDisable()
        {
            UpgradeAltar.Opened -= OnAltarOpened;
            MetaUpgrades.Changed -= OnUpgradesChanged;
            if (IsOpen) Close();
            UiSoundHooks.Detach(document.rootVisualElement);
        }

        private void Start()
        {
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            cancelAction = actions != null ? actions.FindAction(cancelActionName) : null;
            if (cancelAction == null) Debug.LogWarning($"アクション '{cancelActionName}' が無いので、Esc で魂の強化の画面を閉じられない", this);
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame()) Close();
        }

        private void LateUpdate() => Pauser.KeepCursorFree();

        private void OnAltarOpened(UpgradeAltar _) => Open();

        // ---- 開け閉め -------------------------------------------------------

        public void Open()
        {
            if (IsOpen || screen == null || !GamePauser.CanPause) return;

            openedFrame = Time.frameCount;
            Pauser.Pause();
            GameAudio.Instance?.PlayUi(UiSound.Open);
            BuildIfNeeded();
            UiTransitions.Show(screen);
            if (motes != null) motes.Running = true;
            cameraTicker ??= screen.schedule.Execute(TickCamera).Every(16);
            cameraTicker.Resume();
            HideTooltip();

            if (MetaUpgrades.HasHarvest)
            {
                SoulHarvest pending = MetaUpgrades.PendingHarvest;
                HideTree();
                screen.AddToClassList("soul-screen--harvest");
                harvest.Play(pending, MetaUpgrades.Souls - pending.Total, () =>
                {
                    screen.RemoveFromClassList("soul-screen--harvest");
                    MetaUpgrades.ClearHarvest();
                    PlayIntro();
                });
            }
            else
            {
                SetCounter(MetaUpgrades.Souls);
                PlayIntro();
            }
        }

        public void Close()
        {
            if (!IsOpen) return;

            // 演出の途中で閉じても、内訳は見たことにする。
            if (harvest.IsPlaying) harvest.Skip();
            StopTweens();
            fx.Clear();
            EndPress();
            HideTooltip();
            UiTransitions.Hide(screen, () =>
            {
                if (motes != null) motes.Running = false;
                cameraTicker?.Pause();
            });
            GameAudio.Instance?.PlayUi(UiSound.Close);
            Pauser.Resume();
        }

        private void StopTweens()
        {
            foreach (UiTween.Handle tween in tweens) tween.Stop();
            tweens.Clear();
        }

        private void SetCounter(long value)
        {
            shownSouls = value;
            if (counterValue != null) counterValue.text = ShortNumber.Format(value);
        }

        private void OnUpgradesChanged()
        {
            // 画面の外で変わった（デバッグで足した・消した）ときだけ、そのまま合わせ直す。開いている間の買い物は演出が合わせる。
            if (IsOpen) return;
            builtFor = null;
        }

        // ---- 木を作る -------------------------------------------------------

        private void BuildIfNeeded()
        {
            SoulUpgradeTree tree = MetaUpgrades.Tree;
            if (builtFor == tree && views.Count > 0)
            {
                RefreshAll();
                return;
            }

            builtFor = tree;
            world.Clear();
            views.Clear();
            links = new SoulTreeLinks();
            world.Add(links);

            Dictionary<string, RadialTreeLayout.Point> points = RadialTreeLayout.Layout(tree);
            treeRadius = 0f;
            foreach (SoulUpgradeNode node in tree.Nodes)
            {
                if (!points.TryGetValue(node.id, out RadialTreeLayout.Point point)) continue;
                var position = new Vector2(point.X, point.Y);
                treeRadius = Mathf.Max(treeRadius, position.magnitude);
                views[node.id] = CreateNode(node, position, tree.Depth(node.id));
            }

            foreach (NodeView view in views.Values)
            {
                if (string.IsNullOrEmpty(view.Node.parent) || !views.TryGetValue(view.Node.parent, out NodeView parent)) continue;
                view.Link = links.Add(parent.Position, view.Position);
            }

            // 線を強化の下に描く。
            links.SendToBack();
            RefreshAll();
        }

        private NodeView CreateNode(SoulUpgradeNode node, Vector2 position, int depth)
        {
            bool isRoot = depth == 0;
            float radius = isRoot ? RootRadius : NodeRadius;

            var element = new VisualElement { name = $"soul-node-{node.id}" };
            element.AddToClassList(NodeClass);
            if (isRoot) element.AddToClassList(RootClass);
            element.style.left = position.x - radius;
            element.style.top = position.y - radius;
            element.style.width = radius * 2f;
            element.style.height = radius * 2f;

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("soul-node__body");
            var halo = new VisualElement { pickingMode = PickingMode.Ignore };
            halo.AddToClassList("soul-node__halo");
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("soul-node__ring");
            var glyph = new SoulGlyph();
            glyph.AddToClassList("soul-node__glyph");
            if (isRoot) glyph.SetFlame();
            else glyph.SetEffect(node.effect);
            var burst = new VisualElement { pickingMode = PickingMode.Ignore };
            burst.AddToClassList("soul-node__burst");
            body.Add(halo);
            body.Add(ring);
            ring.Add(glyph);
            body.Add(burst);
            element.Add(body);

            var cost = new Label { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("soul-node__cost");
            cost.AddToClassList("rpg-deco-bold");
            element.Add(cost);

            var view = new NodeView { Node = node, Element = element, Body = body, Cost = cost, Position = position, Depth = depth };
            element.userData = view;
            element.RegisterCallback<PointerEnterEvent>(_ => OnNodeHover(view, true));
            element.RegisterCallback<PointerLeaveEvent>(_ => OnNodeHover(view, false));
            world.Add(element);
            return view;
        }

        /// <summary>全部の強化の姿（解放した・買える・まだ買えない・まだ見えない）と、枝の色を今の状態に合わせる。現れる演出はしない。</summary>
        private void RefreshAll()
        {
            foreach (NodeView view in views.Values) RefreshNode(view);
        }

        private void RefreshNode(NodeView view)
        {
            string id = view.Node.id;
            int steps = Mathf.Min(MetaUpgrades.StepsBeyondVisible(id), MaxGhostSteps + 1);
            bool visible = steps == 0;
            SoulUpgradeCheck check = MetaUpgrades.Check(id);
            bool owned = check == SoulUpgradeCheck.Owned;
            bool available = check == SoulUpgradeCheck.Ok;

            // まだ見えない強化は触れない（説明も値段も出さない）。
            view.Element.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
            view.Element.EnableInClassList(GhostClass, !visible);
            for (int i = 1; i <= MaxGhostSteps + 1; i++) view.Element.EnableInClassList(GhostLevelClass + i, steps == i);
            view.Element.EnableInClassList(OwnedClass, owned);
            view.Element.EnableInClassList(AvailableClass, available);
            view.Element.EnableInClassList(LockedClass, visible && !owned && !available);
            view.Cost.text = owned || !visible ? string.Empty : ShortNumber.Format(view.Node.cost);

            if (available && !view.Pulsing)
            {
                UiTransitions.Pulse(view.Element, GlowClass, 900);
                view.Pulsing = true;
            }
            else if (!available && view.Pulsing)
            {
                UiTransitions.StopPulse(view.Element, GlowClass);
                view.Pulsing = false;
            }

            if (view.Link != null)
            {
                view.Link.Hidden = steps > MaxGhostSteps;
                view.Link.State = owned ? SoulTreeLinks.State.Owned : visible ? SoulTreeLinks.State.Next : SoulTreeLinks.State.Ghost;
                view.Link.Dim = visible && !owned && !available;
                view.Link.Alpha = GhostAlpha[Mathf.Max(steps, 0)];
            }
        }

        private void HideTree()
        {
            foreach (NodeView view in views.Values)
            {
                view.Revealed = false;
                view.Element.RemoveFromClassList(UiTransitions.ShownClass);
                if (view.Link != null) view.Link.Progress = 0f;
            }
            links?.MarkDirtyRepaint();
        }

        /// <summary>開くとき。根から外へ、段ごとに枝が伸びて強化が現れる。カメラは少し引いた所から寄る。</summary>
        private void PlayIntro()
        {
            HideTree();
            float fit = FitZoom();
            pan = targetPan = Vector2.zero;
            zoom = fit * 0.78f;
            targetZoom = fit;

            foreach (NodeView view in views.Values)
            {
                // 遠すぎて今は出さない強化も、ここで出しておく（濃さ 0 の段にあり、解放して近づくと濃さの切り替えで現れる）。
                if (MetaUpgrades.StepsBeyondVisible(view.Node.id) < 0) continue;
                long linkDelay = 120 + (view.Depth - 1) * 150L;
                if (view.Depth == 0) Reveal(view, 80);
                else
                {
                    GrowLink(view, linkDelay, 260);
                    Reveal(view, linkDelay + 200);
                }
            }
        }

        /// <summary>見えている強化がおおよそ画面に収まる拡大。</summary>
        private float FitZoom()
        {
            float radius = RootRadius;
            foreach (NodeView view in views.Values)
            {
                if (MetaUpgrades.IsVisible(view.Node.id)) radius = Mathf.Max(radius, view.Position.magnitude + NodeRadius + 40f);
            }
            Rect area = viewport != null ? viewport.contentRect : new Rect(0, 0, 1280, 720);
            float half = Mathf.Max(200f, Mathf.Min(area.width, area.height) * 0.5f - 60f);
            // 端の強化は少しはみ出してよい（全部を収めると小さくなりすぎて値段が読めない）。
            return Mathf.Clamp(half / radius * 1.2f, 0.55f, 1f);
        }

        private void Reveal(NodeView view, long delayMs)
        {
            view.Revealed = true;
            tweens.Add(UiTween.After(view.Element, delayMs, () => view.Element.AddToClassList(UiTransitions.ShownClass)));
        }

        private void GrowLink(NodeView view, long delayMs, long durationMs)
        {
            if (view.Link == null) return;
            SoulTreeLinks.Link link = view.Link;
            link.Progress = 0f;
            tweens.Add(UiTween.Run(links, durationMs, t => link.Progress = t, null, UiTween.EaseOutCubic, delayMs));
        }

        // ---- 解放 ------------------------------------------------------------

        private void Buy(NodeView view)
        {
            if (harvest.IsPlaying) return;

            string id = view.Node.id;
            long before = MetaUpgrades.Souls;
            SoulUpgradeCheck check = MetaUpgrades.TryBuy(id);
            switch (check)
            {
                case SoulUpgradeCheck.Ok:
                    PlayUnlock(view, before, MetaUpgrades.Souls);
                    break;
                case SoulUpgradeCheck.NotEnough:
                    Shake(view);
                    UiTransitions.Flash(counter, "soul-counter--short", 420);
                    GameAudio.Instance?.PlayUi(UiSound.Denied);
                    break;
                case SoulUpgradeCheck.Owned:
                    UiTransitions.Flash(view.Element, "soul-node--poke", 180);
                    break;
            }
        }

        /// <summary>
        /// 解放したときの演出。
        /// ソウルの玉が右上から強化へ飛び（数は回りながら減る）、届くと強化が光って弾け、前の強化からの枝が金に染まる。
        /// そのあと先の強化へ枝が伸び、伸び切ると先の強化が跳ねて現れる。
        /// </summary>
        private void PlayUnlock(NodeView view, long before, long after)
        {
            HideTooltip();
            Vector2 from = counter.worldBound.center;
            Vector2 to = view.Element.worldBound.center;
            const long fly = 460;
            // 払った量が多いほど玉を多く出し、出ていく 1 粒ごとに鳴らす。多いときは間を詰めて長くなりすぎないようにする。
            int orbs = Mathf.Clamp(Mathf.RoundToInt((before - after) / 8f), 3, 24);
            long gap = System.Math.Clamp(900L / orbs, 40L, 70L);
            for (int i = 0; i < orbs; i++)
            {
                fx.Orb(from, to, i * gap, fly, null, 11f);
                tweens.Add(UiTween.After(counter, i * gap, () => GameAudio.Instance?.PlayUi(UiSound.SoulSpend)));
            }

            tweens.Add(UiTween.Run(counter, fly + orbs * gap, t => SetCounter(before + (long)System.Math.Round((after - before) * (double)t)), () => SetCounter(after)));

            // 前の強化からの枝を、玉が届くのに合わせて金で塗り直す。
            if (view.Link != null)
            {
                SoulTreeLinks.Link link = view.Link;
                tweens.Add(UiTween.After(links, fly - 160, () =>
                {
                    link.State = SoulTreeLinks.State.Owned;
                    link.Progress = 0f;
                }));
                GrowLink(view, fly - 160, 300);
            }

            long arrive = fly + orbs * gap;
            tweens.Add(UiTween.After(view.Element, arrive, () =>
            {
                // 先の強化は現れる演出で出すので、ここで見えるようになった強化は隠しておく。
                foreach (string child in MetaUpgrades.Tree.Children(view.Node.id))
                {
                    if (!views.TryGetValue(child, out NodeView next)) continue;
                    next.Revealed = false;
                    next.Element.RemoveFromClassList(UiTransitions.ShownClass);
                    if (next.Link != null) next.Link.Progress = 0f;
                }
                RefreshAll();
                UiTransitions.Flash(view.Element, BurstClass, 60);
                fx.Ring(to, view.Depth == 0 ? RootRadius * 2f * zoom : NodeRadius * 2f * zoom, "soul-ring--gold");
                GameAudio.Instance?.PlayUi(UiSound.Unlock);
                if (hovered == view) ShowTooltip(view);
                RevealChildren(view, 260);
            }));
        }

        private void RevealChildren(NodeView view, long delayMs)
        {
            IReadOnlyList<string> children = MetaUpgrades.Tree.Children(view.Node.id);
            if (children.Count == 0) return;

            int i = 0;
            foreach (string child in children)
            {
                if (!views.TryGetValue(child, out NodeView next)) continue;
                long at = delayMs + i * 90L;
                GrowLink(next, at, 420);
                Reveal(next, at + 360);
                i++;
            }
            tweens.Add(UiTween.After(view.Element, delayMs + 360, () => GameAudio.Instance?.PlayUi(UiSound.Reveal)));
        }

        /// <summary>足りないとき、強化を横に震わせる。</summary>
        private void Shake(NodeView view)
        {
            VisualElement body = view.Body;
            tweens.Add(UiTween.Run(body, 380, t =>
            {
                float x = Mathf.Sin(t * Mathf.PI * 7f) * 7f * (1f - t);
                body.style.translate = new Translate(x, 0f);
            }, () => body.style.translate = StyleKeyword.Null, UiTween.Linear));
        }

        // ---- 説明 ------------------------------------------------------------

        private void OnNodeHover(NodeView view, bool entered)
        {
            if (dragging || harvest.IsPlaying) return;
            view.Element.EnableInClassList(HoverClass, entered);
            if (entered)
            {
                hovered = view;
                ShowTooltip(view);
                GameAudio.Instance?.PlayUi(UiSound.Hover);
            }
            else if (hovered == view)
            {
                hovered = null;
                HideTooltip();
            }
        }

        private void ShowTooltip(NodeView view)
        {
            if (tooltip == null) return;

            SoulUpgradeNode node = view.Node;
            SoulUpgradeCheck check = MetaUpgrades.Check(node.id);
            tooltipTitle.text = node.name;
            tooltipEffect.text = SoulUpgradeEffects.Describe(node.effect, node.value);
            tooltipBody.text = node.description;
            tooltipBody.style.display = string.IsNullOrEmpty(node.description) ? DisplayStyle.None : DisplayStyle.Flex;
            tooltipState.text = check switch
            {
                SoulUpgradeCheck.Owned => "解放済み",
                SoulUpgradeCheck.Ok => "クリックで解放",
                SoulUpgradeCheck.NotEnough => $"ソウルが {ShortNumber.Format(node.cost - MetaUpgrades.Souls)} 足りない",
                _ => string.Empty,
            };
            tooltip.EnableInClassList("soul-tooltip--owned", check == SoulUpgradeCheck.Owned);
            tooltip.EnableInClassList("soul-tooltip--short", check == SoulUpgradeCheck.NotEnough);
            tooltipPrice.style.display = check == SoulUpgradeCheck.Owned ? DisplayStyle.None : DisplayStyle.Flex;
            tooltipCost.text = ShortNumber.Format(node.cost);

            PlaceTooltip(view);
            UiTransitions.Show(tooltip);
        }

        /// <summary>強化の右に出す。右が足りなければ左に。</summary>
        private void PlaceTooltip(NodeView view)
        {
            Rect area = root.contentRect;
            Rect node = view.Element.worldBound;
            Vector2 center = root.WorldToLocal(node.center);
            float half = node.width * 0.5f;
            const float width = 280f;
            const float gap = 18f;
            bool right = center.x + half + gap + width < area.width - 16f;
            float left = right ? center.x + half + gap : center.x - half - gap - width;
            tooltip.EnableInClassList("soul-tooltip--left", !right);
            tooltip.style.left = Mathf.Clamp(left, 16f, Mathf.Max(16f, area.width - width - 16f));
            tooltip.style.top = Mathf.Clamp(center.y - 60f, 90f, Mathf.Max(90f, area.height - 260f));
        }

        private void HideTooltip()
        {
            if (tooltip != null) UiTransitions.Hide(tooltip);
        }

        // ---- 動かす・拡大 -----------------------------------------------------

        private void OnPointerDown(PointerDownEvent e)
        {
            if (harvest.IsPlaying) return;
            pressed = true;
            dragging = false;
            pressPosition = e.position;
            pressedNode = FindNode(e.target as VisualElement);
            viewport.CapturePointer(e.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!pressed) return;
            if (!dragging && ((Vector2)e.position - pressPosition).magnitude > DragThreshold)
            {
                dragging = true;
                pressedNode = null;
                HideTooltip();
                viewport.AddToClassList("soul-viewport--dragging");
            }
            if (!dragging) return;

            pan += (Vector2)e.deltaPosition;
            pan = ClampPan(pan, zoom);
            targetPan = pan;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!pressed) return;
            NodeView node = dragging ? null : pressedNode;
            if (viewport.HasPointerCapture(e.pointerId)) viewport.ReleasePointer(e.pointerId);
            EndPress();
            if (node != null && e.button == 0) Buy(node);
        }

        private void EndPress()
        {
            pressed = false;
            dragging = false;
            pressedNode = null;
            viewport?.RemoveFromClassList("soul-viewport--dragging");
        }

        private static NodeView FindNode(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.parent)
            {
                if (e.userData is NodeView view) return view;
            }
            return null;
        }

        /// <summary>カーソルの所を中心に拡大・縮小する（目標を変え、毎フレーム寄せる）。</summary>
        private void OnWheel(WheelEvent e)
        {
            if (harvest.IsPlaying) return;
            float next = Mathf.Clamp(targetZoom * Mathf.Exp(-e.delta.y * 0.08f), MinZoom, MaxZoom);
            Vector2 cursor = viewport.WorldToLocal(e.mousePosition) - viewport.contentRect.center;
            // カーソルの下にある木の面の点が、拡大のあとも同じ所に来るように。
            Vector2 under = (cursor - targetPan) / targetZoom;
            targetPan = ClampPan(cursor - under * next, next);
            targetZoom = next;
            e.StopPropagation();
        }

        private void Recenter()
        {
            targetPan = Vector2.zero;
            targetZoom = FitZoom();
        }

        private Vector2 ClampPan(Vector2 value, float scale)
        {
            float limit = (treeRadius + 120f) * scale;
            return Vector2.ClampMagnitude(value, Mathf.Max(200f, limit));
        }

        private void TickCamera()
        {
            if (world == null) return;
            const float sharpness = 0.22f;
            if (!dragging) pan = Vector2.Lerp(pan, targetPan, sharpness);
            zoom = Mathf.Lerp(zoom, targetZoom, sharpness);
            if (Mathf.Abs(zoom - targetZoom) < 0.0005f) zoom = targetZoom;
            world.style.translate = new Translate(pan.x, pan.y);
            world.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            if (hovered != null && tooltip != null && UiTransitions.IsShown(tooltip)) PlaceTooltip(hovered);
        }
    }
}
