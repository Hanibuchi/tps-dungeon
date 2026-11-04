using TpsDungeon.Enemies;
using TpsDungeon.Party;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 上中央のボスの名前と HP（GameHud.uxml の #boss）。ボス戦（<see cref="BossEncounter.Current"/>）の間だけ出す。
    /// 段階の境目（BossPhases の phase2_hp・phase3_hp）に区切りの線を引き、段階が上がるとバーの色を変えて枠を光らせる。
    /// 削れ跡は HP のバーと同じく、本体より遅れて追いつく（USS の transition）。
    /// </summary>
    public sealed class BossBar
    {
        private const string StageClassPrefix = "boss--stage";
        private const string HitClass = "boss--hit";
        private const string RoarClass = "boss--roar";

        private readonly VisualElement root;
        private readonly Label nameLabel;
        private readonly VisualElement fill;
        private readonly VisualElement trail;
        private readonly VisualElement tick2;
        private readonly VisualElement tick3;

        private EnemyHealth shownBoss;
        private int shownHp = -1;
        private int shownStage;

        public BossBar(VisualElement hudRoot)
        {
            root = hudRoot?.Q<VisualElement>("boss");
            if (root == null) return;
            nameLabel = root.Q<Label>("boss-name");
            fill = root.Q<VisualElement>("boss-fill");
            trail = root.Q<VisualElement>("boss-trail");
            tick2 = root.Q<VisualElement>("boss-tick-2");
            tick3 = root.Q<VisualElement>("boss-tick-3");
            UiTransitions.HideImmediately(root);
        }

        /// <summary>毎フレーム呼ぶ。</summary>
        public void Refresh()
        {
            if (root == null) return;

            BossEncounter encounter = BossEncounter.Current;
            EnemyHealth boss = encounter != null ? encounter.Boss : null;
            if (boss != shownBoss) Bind(encounter, boss);
            if (boss == null) return;

            int hp = boss.CurrentHp;
            if (hp != shownHp)
            {
                if (shownHp >= 0 && hp < shownHp) UiTransitions.Flash(root, HitClass, 90);
                shownHp = hp;
                float fraction = Mathf.Clamp01((float)hp / boss.MaxHp);
                fill.style.width = Length.Percent(fraction * 100f);
                trail.style.width = Length.Percent(fraction * 100f);
            }

            int stage = encounter.Phases != null ? encounter.Phases.Stage : 1;
            if (stage != shownStage) SetStage(stage, shownStage > 0);
        }

        private void Bind(BossEncounter encounter, EnemyHealth boss)
        {
            shownBoss = boss;
            shownHp = -1;
            if (boss == null)
            {
                UiTransitions.Hide(root);
                return;
            }

            BossPhases phases = encounter.Phases;
            nameLabel.text = phases != null ? phases.DisplayName : boss.name;
            PlaceTick(tick2, phases != null ? phases.Phase2Hp : -1f);
            PlaceTick(tick3, phases != null ? phases.Phase3Hp : -1f);

            // 出た瞬間は満タンから。削れ跡も動かさずに合わせる。
            fill.style.width = Length.Percent(100f);
            trail.style.width = Length.Percent(100f);
            shownStage = 0;
            SetStage(phases != null ? phases.Stage : 1, false);
            UiTransitions.Show(root);
        }

        private void SetStage(int stage, bool announce)
        {
            for (int i = 1; i <= BossPhaseRules.MaxStage; i++) root.EnableInClassList(StageClassPrefix + i, i == stage);
            shownStage = stage;
            if (announce) UiTransitions.Flash(root, RoarClass, 600);
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
