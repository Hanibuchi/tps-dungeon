using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 右上の「セーブ中」の札（GameHud.uxml の #saving）。オートセーブのたびに右から滑り込み、
    /// 金の菱形が回り、文字の後ろの点が順に灯る。最後に書いてから <see cref="ShownMs"/> 経つと、菱形を一度光らせて（保存できた印）抜けていく。
    /// 見せている間にまた書いたら、消えるのを延ばす。時間はどれも実時間なので、ポーズ中でも動く。
    /// 書くのは一瞬で終わるが、着いたときの保存は暗転の裏（明けるのに 0.75 秒）なので、明けても見えるだけ残す。
    /// </summary>
    public sealed class SaveIndicator
    {
        /// <summary>最後に書いてから「保存できた」に変わるまで（ミリ秒）。</summary>
        public const long ShownMs = 1600;

        /// <summary>保存できた印（菱形が光る）を見せてから抜け始めるまで（ミリ秒）。</summary>
        private const long DoneMs = 420;

        /// <summary>菱形が 1 回転する時間（秒）。</summary>
        private const float SpinSeconds = 1.1f;

        /// <summary>点が 1 つ進む間隔（秒）。</summary>
        private const float DotStepSeconds = 0.28f;

        private const string DoneClass = "saving--done";
        private const string DotOnClass = "saving__dot--on";

        private readonly VisualElement root;
        private readonly VisualElement gem;
        private readonly VisualElement[] dots;
        private IVisualElementScheduledItem spin;
        private IVisualElementScheduledItem finish;
        private IVisualElementScheduledItem leave;

        public SaveIndicator(VisualElement hudRoot)
        {
            root = hudRoot?.Q<VisualElement>("saving");
            if (root == null) return;

            gem = root.Q<VisualElement>("saving-gem");
            dots = root.Query<VisualElement>(className: "saving__dot").ToList().ToArray();
            UiTransitions.HideImmediately(root);
        }

        /// <summary>書いた。出ていなければ出し、出ていれば消えるのを延ばす。</summary>
        public void Saved()
        {
            if (root == null) return;

            leave?.Pause();
            leave = null;
            root.RemoveFromClassList(DoneClass);
            if (!UiTransitions.IsShown(root)) UiTransitions.Show(root);
            StartSpin();

            finish?.Pause();
            finish = root.schedule.Execute(Finish).StartingIn(ShownMs);
        }

        /// <summary>止めて隠す（HUD が無効になるとき）。</summary>
        public void Stop()
        {
            if (root == null) return;

            finish?.Pause();
            leave?.Pause();
            spin?.Pause();
            finish = leave = spin = null;
            root.RemoveFromClassList(DoneClass);
            UiTransitions.HideImmediately(root);
        }

        private void Finish()
        {
            finish = null;
            spin?.Pause();
            spin = null;
            if (gem != null) gem.style.rotate = new Rotate(45f);
            foreach (VisualElement dot in dots) dot.AddToClassList(DotOnClass);
            root.AddToClassList(DoneClass);

            leave = root.schedule.Execute(() =>
            {
                leave = null;
                UiTransitions.Hide(root, () => root.RemoveFromClassList(DoneClass));
            }).StartingIn(DoneMs);
        }

        private void StartSpin()
        {
            if (spin != null) return;

            float started = Time.unscaledTime;
            spin = root.schedule.Execute(() =>
            {
                float elapsed = Time.unscaledTime - started;
                // 菱形は 45 度（ひし形の向き）から回し始め、ゆっくり速く、を繰り返す。
                float turn = elapsed / SpinSeconds;
                float eased = Mathf.Floor(turn) + Ease(turn - Mathf.Floor(turn));
                if (gem != null) gem.style.rotate = new Rotate(45f + eased * 180f);

                int lit = Mathf.FloorToInt(elapsed / DotStepSeconds) % (dots.Length + 1);
                for (int i = 0; i < dots.Length; i++) dots[i].EnableInClassList(DotOnClass, i < lit);
            }).Every(16);
        }

        // 1 回転の中で、止まりかけてからまた速く回る（砂時計を返すような動き）。
        private static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
