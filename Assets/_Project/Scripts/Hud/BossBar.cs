using TpsDungeon.Enemies;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Party;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 上中央のボスの名前と HP（GameHud.uxml の #boss）。ボス戦（<see cref="BossEncounter.Current"/>）の間だけ出す。
    ///   ・肩書き（「第 N 層の主」）と名前、HP のバー（削れ跡は本体より遅れて追いつく）
    ///   ・段階の境目（BossPhases の phase2_hp・phase3_hp）に目盛りの菱形を置き、越えた目盛りは灯す
    ///   ・バーの下の 3 つの菱形で今の段階を示す。段階が上がると枠の角と紋章を光らせ、段階 3 ではバーが脈打つ
    ///   ・倒したら肩書きを「討伐」にして名前を沈め、少し見せてから消す。死んで階ごと消えたら（ボスも消える）すぐ消す
    /// </summary>
    public sealed class BossBar
    {
        private const string StageClassPrefix = "boss--stage";
        private const string HitClass = "boss--hit";
        private const string RoarClass = "boss--roar";
        private const string BeatClass = "boss--beat";
        private const string DefeatedClass = "boss--defeated";
        private const string StageOnClass = "boss__stage--on";
        private const string TickPassedClass = "boss__tick--passed";

        /// <summary>倒してからバーを消すまで（ミリ秒）。</summary>
        private const long DefeatedShownMs = 1800;

        /// <summary>段階 3 の脈の間隔（ミリ秒）。</summary>
        private const long BeatPeriodMs = 520;

        private readonly VisualElement root;
        private readonly Label titleLabel;
        private readonly Label nameLabel;
        private readonly VisualElement fill;
        private readonly VisualElement trail;
        private readonly VisualElement tick2;
        private readonly VisualElement tick3;
        private readonly VisualElement[] stagePips;

        private EnemyHealth shownBoss;
        private BossPhases shownPhases;
        private int shownHp = -1;
        private int shownStage;
        private IVisualElementScheduledItem hide;

        public BossBar(VisualElement hudRoot)
        {
            root = hudRoot?.Q<VisualElement>("boss");
            if (root == null) return;
            titleLabel = root.Q<Label>("boss-title");
            nameLabel = root.Q<Label>("boss-name");
            fill = root.Q<VisualElement>("boss-fill");
            trail = root.Q<VisualElement>("boss-trail");
            tick2 = root.Q<VisualElement>("boss-tick-2");
            tick3 = root.Q<VisualElement>("boss-tick-3");
            stagePips = new VisualElement[BossPhaseRules.MaxStage];
            for (int i = 0; i < stagePips.Length; i++) stagePips[i] = root.Q<VisualElement>($"boss-stage-{i + 1}");
            UiTransitions.HideImmediately(root);
        }

        /// <summary>毎フレーム呼ぶ。</summary>
        public void Refresh()
        {
            if (root == null) return;

            BossEncounter encounter = BossEncounter.Current;
            EnemyHealth boss = encounter != null && encounter.Boss != null ? encounter.Boss : null;

            // Unity の == は消えた物を null と同じに扱うので、参照で比べる（死んで階ごと消えたときにバーが残らないように）。
            if (!ReferenceEquals(boss, shownBoss))
            {
                if (boss != null) Bind(encounter, boss);
                else Unbind();
            }
            if (boss == null) return;

            int hp = boss.CurrentHp;
            if (hp != shownHp)
            {
                if (shownHp >= 0 && hp < shownHp) UiTransitions.Flash(root, HitClass, 90);
                shownHp = hp;
                SetFraction((float)hp / boss.MaxHp);
            }

            int stage = shownPhases != null ? shownPhases.Stage : 1;
            if (stage != shownStage) SetStage(stage, shownStage > 0);
        }

        private void Bind(BossEncounter encounter, EnemyHealth boss)
        {
            hide?.Pause();
            hide = null;
            shownBoss = boss;
            shownPhases = encounter.Phases;
            shownHp = -1;

            root.RemoveFromClassList(DefeatedClass);
            titleLabel.text = TitleText();
            nameLabel.text = shownPhases != null ? shownPhases.DisplayName : boss.name;
            PlaceTick(tick2, shownPhases != null ? shownPhases.Phase2Hp : -1f);
            PlaceTick(tick3, shownPhases != null ? shownPhases.Phase3Hp : -1f);

            // 出た瞬間は満タンから。削れ跡も動かさずに合わせる。
            SetFraction(1f);
            shownStage = 0;
            SetStage(shownPhases != null ? shownPhases.Stage : 1, false);
            UiTransitions.Show(root);
        }

        /// <summary>ボスが居なくなった。倒したなら少し見せてから、消えた（階ごと）ならすぐ消す。</summary>
        private void Unbind()
        {
            bool defeated = shownBoss != null && shownBoss.IsDead;
            shownBoss = null;
            shownPhases = null;
            shownHp = -1;
            UiTransitions.StopPulse(root, BeatClass);

            if (!defeated)
            {
                UiTransitions.HideImmediately(root);
                return;
            }

            SetFraction(0f);
            titleLabel.text = "討伐";
            root.AddToClassList(DefeatedClass);
            UiTransitions.Flash(root, RoarClass, 600);
            hide?.Pause();
            hide = root.schedule.Execute(() =>
            {
                hide = null;
                UiTransitions.Hide(root);
            }).StartingIn(DefeatedShownMs);
        }

        private void SetFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.style.width = Length.Percent(fraction * 100f);
            trail.style.width = Length.Percent(fraction * 100f);
        }

        private void SetStage(int stage, bool announce)
        {
            for (int i = 1; i <= BossPhaseRules.MaxStage; i++) root.EnableInClassList(StageClassPrefix + i, i == stage);
            for (int i = 0; i < stagePips.Length; i++) stagePips[i]?.EnableInClassList(StageOnClass, i < stage);
            tick2?.EnableInClassList(TickPassedClass, stage >= 2);
            tick3?.EnableInClassList(TickPassedClass, stage >= 3);

            if (stage >= BossPhaseRules.MaxStage) UiTransitions.Pulse(root, BeatClass, BeatPeriodMs);
            else UiTransitions.StopPulse(root, BeatClass);

            shownStage = stage;
            if (announce) UiTransitions.Flash(root, RoarClass, 600);
        }

        /// <summary>肩書き。道順の上で今いる層（無ければ空）。</summary>
        private static string TitleText()
        {
            var floor = DungeonProgress.Current;
            return floor.HasValue ? $"第 {floor.Value.Layer + 1} 層の主" : "";
        }

        private static void PlaceTick(VisualElement tick, float fraction)
        {
            if (tick == null) return;
            bool shown = fraction > 0f && fraction < 1f;
            tick.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown) tick.style.left = Length.Percent(fraction * 100f);
        }
    }
}
