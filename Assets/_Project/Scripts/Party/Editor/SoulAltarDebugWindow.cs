using System.Collections.Generic;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Menu.UI;
using TpsDungeon.Progression;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 魂の強化を確かめるためのウィンドウ（Tools/TPS Dungeon/デバッグ/魂の燭台のデバッグ）。
    /// - ソウルを足す・0 にする、強化を払わずに解放する・戻す（1 つずつ、全部）。Play していなくても使え、本物の保存（meta.json）に書く。
    /// - Play 中は、強化の画面を開く、全滅と同じに死後の場所へ送る（レベルがソウルに変わる）、経験値ブーストを始める。
    /// </summary>
    public sealed class SoulAltarDebugWindow : EditorWindow
    {
        private Label status;
        private IntegerField amount;
        private Button openButton;
        private Button wipeButton;
        private Button boostButton;
        private ScrollView list;
        private readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
        private SoulUpgradeTree builtFor;

        [MenuItem("Tools/TPS Dungeon/デバッグ/魂の燭台のデバッグ", false, 1)]
        public static void Open()
        {
            var window = GetWindow<SoulAltarDebugWindow>();
            window.titleContent = new GUIContent("魂の燭台のデバッグ");
            window.minSize = new Vector2(300f, 360f);
        }

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

        /// <summary>Play で書き換わった保存を、Play を抜けたら読み直す（古い中身で上書きしないように）。</summary>
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode) MetaUpgrades.Reload();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            status = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 } };
            root.Add(status);

            var soulRow = Row();
            amount = new IntegerField("ソウル") { value = 100, style = { flexGrow = 1 } };
            amount.labelElement.style.minWidth = 50;
            soulRow.Add(amount);
            soulRow.Add(new Button(() => AddSouls(amount.value)) { text = "足す" });
            soulRow.Add(new Button(() => AddSouls(-amount.value)) { text = "引く" });
            soulRow.Add(new Button(() => SetSouls(0)) { text = "0 にする" });
            root.Add(soulRow);

            var allRow = Row();
            allRow.Add(new Button(PurchaseAll) { text = "全部解放", style = { flexGrow = 1 } });
            allRow.Add(new Button(ClearPurchases) { text = "全部戻す", tooltip = "解放した強化を全部戻す。ソウルはそのまま。", style = { flexGrow = 1 } });
            allRow.Add(new Button(ResetAll) { text = "全部消す", tooltip = "強化もソウルも、まだ見せていない内訳も消す。", style = { flexGrow = 1 } });
            root.Add(allRow);

            root.Add(new Label("Play 中") { style = { marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold } });
            var playRow = Row();
            openButton = new Button(OpenScreen) { text = "強化の画面を開く", style = { flexGrow = 1 } };
            wipeButton = new Button(SendAsWiped)
            {
                text = "全滅と同じに死後の場所へ",
                tooltip = "今のレベルをソウルに変えて Lv1 に戻し、死後の場所へ送る。燭台を開くと、ソウルに変わる演出が出る。",
                style = { flexGrow = 1 },
            };
            playRow.Add(openButton);
            playRow.Add(wipeButton);
            root.Add(playRow);
            boostButton = new Button(StartBoost) { text = "経験値ブーストを始める（レベルが上がったときと同じ）" };
            root.Add(boostButton);

            root.Add(new Label("強化（チェックで払わずに解放・外すと先の強化ごと戻す）") { style = { marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold } });
            list = new ScrollView { style = { flexGrow = 1 } };
            root.Add(list);

            root.schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        private static VisualElement Row() => new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

        private void Refresh()
        {
            if (status == null) return;

            SoulUpgradeTree tree = MetaUpgrades.Tree;
            if (builtFor != tree) BuildList(tree);

            int owned = 0;
            foreach (SoulUpgradeNode node in tree.Nodes)
            {
                bool isOwned = MetaUpgrades.IsPurchased(node.id);
                if (isOwned) owned++;
                if (toggles.TryGetValue(node.id, out Toggle toggle)) toggle.SetValueWithoutNotify(isOwned);
            }

            SoulHarvest harvest = MetaUpgrades.PendingHarvest;
            status.text = $"ソウル: {MetaUpgrades.Souls:N0}　解放: {owned} / {tree.Count}"
                + $"\nまだ見せていない内訳: {(harvest != null ? $"あり（{harvest.members.Count} 人・{harvest.Total} ソウル）" : "なし")}"
                + (MetaUpgrades.IsExpBoostActive ? $"\n経験値ブースト: 残り {MetaUpgrades.ExpBoostRemaining:0} 秒" : "");

            bool playing = EditorApplication.isPlaying;
            openButton.SetEnabled(playing && Object.FindAnyObjectByType<SoulUpgradeScreen>() != null);
            wipeButton.SetEnabled(playing && GameFlow.Instance != null && !SceneTravel.Traveling);
            boostButton.SetEnabled(playing && MetaUpgrades.ExpBoostDuration > 0f);
        }

        /// <summary>強化を木の順（前の強化の下に字下げ）で並べる。</summary>
        private void BuildList(SoulUpgradeTree tree)
        {
            builtFor = tree;
            list.Clear();
            toggles.Clear();
            if (tree.Root != null) AddRows(tree, tree.Root);
        }

        private void AddRows(SoulUpgradeTree tree, string id)
        {
            if (!tree.TryGet(id, out SoulUpgradeNode node)) return;

            string effect = SoulUpgradeEffects.Describe(node.effect, node.value);
            var toggle = new Toggle($"{node.name}　{effect}　{node.cost}")
            {
                tooltip = $"{node.id}: {node.description}",
                style = { marginLeft = tree.Depth(id) * 14 },
            };
            toggle.labelElement.style.minWidth = 0;
            toggle.labelElement.style.flexGrow = 1;
            toggle.RegisterValueChangedCallback(e => MetaUpgrades.SetPurchased(id, e.newValue));
            list.Add(toggle);
            toggles[id] = toggle;

            foreach (string child in tree.Children(id)) AddRows(tree, child);
        }

        private static void AddSouls(int value)
        {
            MetaUpgrades.AddSouls(value);
            Debug.Log($"[Soul] デバッグ: ソウル {(value >= 0 ? "+" : "")}{value}（手持ち {MetaUpgrades.Souls}）");
        }

        private static void SetSouls(long value)
        {
            MetaUpgrades.SetSouls(value);
            Debug.Log($"[Soul] デバッグ: ソウルを {value} にした");
        }

        private static void PurchaseAll()
        {
            MetaUpgrades.PurchaseAll();
            Debug.Log("[Soul] デバッグ: 強化を全部解放した");
        }

        private static void ClearPurchases()
        {
            MetaUpgrades.ClearPurchases();
            Debug.Log("[Soul] デバッグ: 強化を全部戻した");
        }

        private static void ResetAll()
        {
            MetaUpgrades.ResetAll();
            Debug.Log("[Soul] デバッグ: 魂の強化の保存を全部消した");
        }

        private static void OpenScreen()
        {
            var screen = Object.FindAnyObjectByType<SoulUpgradeScreen>();
            if (screen != null) screen.Open();
        }

        private static void SendAsWiped()
        {
            if (GameFlow.Instance == null) return;
            Debug.Log("[Soul] デバッグ: 全滅と同じに死後の場所へ送る");
            GameFlow.Instance.Travel(SceneTravel.AfterlifeScene, revive: true);
        }

        private static void StartBoost()
        {
            MetaUpgrades.StartExpBoost();
            Debug.Log($"[Soul] デバッグ: 経験値ブーストを始めた（{MetaUpgrades.ExpBoostDuration:0} 秒）");
        }
    }
}
