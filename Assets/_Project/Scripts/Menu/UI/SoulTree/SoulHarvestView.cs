using System;
using System.Collections.Generic;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Party;
using TpsDungeon.Progression;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;
using PartyGroup = TpsDungeon.Party.Party;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 死んでから初めて魂の強化の画面を開いたときの演出。死んだときのレベルがソウルに変わっていく様子を見せる。
    /// 1. 倒れた仲間の札が並ぶ（色の炎・名前・死んだときの Lv）。
    /// 2. 札ごとに Lv が 1 まで数え戻り、その間に札から光の玉がこぼれて真ん中の合計へ吸い込まれる。札の「+ソウル」と合計は増えていく。
    /// 3. 合計が右上の手持ちのソウルへ飛んで入り、手持ちの数が増える。
    /// クリックすると最後まで飛ばす。終わったら（飛ばしても）onDone を呼ぶ。内訳を消す（ClearHarvest）のは呼ぶ側。
    /// </summary>
    internal sealed class SoulHarvestView
    {
        private const string CardClass = "soul-harvest-card";

        private sealed class Card
        {
            public VisualElement Element;
            public Label Level;
            public Label Souls;
            public SoulHarvestMember Member;
            public int Shown;
        }

        private readonly VisualElement root;
        private readonly VisualElement cards;
        private readonly VisualElement total;
        private readonly Label totalValue;
        private readonly SoulFx fx;
        private readonly Action<long> setCounter;
        private readonly Func<VisualElement> counterTarget;
        private readonly List<Card> cardList = new List<Card>();
        private readonly List<UiTween.Handle> tweens = new List<UiTween.Handle>();

        private Action onDone;
        private long soulsBefore;
        private int harvestTotal;
        private int finishedCards;

        public SoulHarvestView(VisualElement root, SoulFx fx, Action<long> setCounter, Func<VisualElement> counterTarget)
        {
            this.root = root;
            this.fx = fx;
            this.setCounter = setCounter;
            this.counterTarget = counterTarget;
            cards = root?.Q<VisualElement>("harvest-cards");
            total = root?.Q<VisualElement>("harvest-total");
            totalValue = root?.Q<Label>("harvest-total-value");
            root?.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!IsPlaying) return;
                e.StopPropagation();
                Skip();
            });
            UiTransitions.HideImmediately(root);
        }

        public bool IsPlaying { get; private set; }

        /// <summary>演出を始める。soulsBefore は死ぬ前の手持ち（内訳を足す前）。</summary>
        public void Play(SoulHarvest harvest, long soulsBefore, Action onDone)
        {
            if (root == null || harvest == null)
            {
                onDone?.Invoke();
                return;
            }

            Stop();
            IsPlaying = true;
            this.onDone = onDone;
            this.soulsBefore = soulsBefore;
            harvestTotal = harvest.Total;
            finishedCards = 0;
            setCounter(soulsBefore);

            BuildCards(harvest);
            totalValue.text = "0";
            total.RemoveFromClassList("soul-harvest__total--fly");
            total.style.translate = StyleKeyword.Null;
            total.style.scale = StyleKeyword.Null;
            total.style.opacity = StyleKeyword.Null;

            UiTransitions.Show(root);
            UiTransitions.Stagger(cards, 110, 260);

            // 札が並び終わってから、1 枚ずつ少しずらして数え始める。
            for (int i = 0; i < cardList.Count; i++)
            {
                Card card = cardList[i];
                long delay = 900 + 260 * i + 110 * i;
                tweens.Add(UiTween.After(root, delay, () => Count(card)));
            }
            if (cardList.Count == 0) tweens.Add(UiTween.After(root, 600, Bank));
        }

        /// <summary>最後まで飛ばす。</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            Stop();
            fx.Clear();
            foreach (Card card in cardList)
            {
                card.Level.text = $"Lv {GrowthCurve.MinLevel}";
                card.Souls.text = $"+{card.Member.souls}";
            }
            setCounter(soulsBefore + harvestTotal);
            Finish();
        }

        private void Stop()
        {
            foreach (UiTween.Handle tween in tweens) tween.Stop();
            tweens.Clear();
        }

        private void BuildCards(SoulHarvest harvest)
        {
            cards.Clear();
            cardList.Clear();
            foreach (SoulHarvestMember member in harvest.members)
            {
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList(CardClass);

                var medal = new VisualElement { pickingMode = PickingMode.Ignore };
                medal.AddToClassList("soul-harvest-card__medal");
                Color? accent = AccentOf(member.id);
                if (accent.HasValue) medal.style.borderTopColor = medal.style.borderBottomColor =
                    medal.style.borderLeftColor = medal.style.borderRightColor = accent.Value;
                var flame = new SoulGlyph();
                flame.AddToClassList("soul-harvest-card__flame");
                if (accent.HasValue) flame.style.color = Color.Lerp(accent.Value, Color.white, 0.35f);
                medal.Add(flame);
                element.Add(medal);

                var name = new Label(string.IsNullOrEmpty(member.name) ? member.id : member.name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("soul-harvest-card__name");
                name.AddToClassList("rpg-bold");
                element.Add(name);

                var level = new Label($"Lv {member.level}") { pickingMode = PickingMode.Ignore };
                level.AddToClassList("soul-harvest-card__level");
                level.AddToClassList("rpg-deco-bold");
                element.Add(level);

                var souls = new Label("+0") { pickingMode = PickingMode.Ignore };
                souls.AddToClassList("soul-harvest-card__souls");
                souls.AddToClassList("rpg-deco-bold");
                element.Add(souls);

                cards.Add(element);
                cardList.Add(new Card { Element = element, Level = level, Souls = souls, Member = member });
            }
        }

        /// <summary>その仲間の色（角・耳・しっぽの色）。見つからなければ null。</summary>
        private static Color? AccentOf(string progressId)
        {
            PartyGroup party = PartyGroup.Current;
            if (party == null) return null;
            foreach (PartyMember member in party.Members)
            {
                if (member == null || member.Progression == null || member.Progression.ProgressId != progressId) continue;
                var appearance = member.GetComponent<CharacterAppearance>();
                return appearance != null ? appearance.AccentColor : null;
            }
            return null;
        }

        /// <summary>札の Lv を 1 まで数え戻し、その分のソウルを玉にして合計へ飛ばす。</summary>
        private void Count(Card card)
        {
            int from = Mathf.Max(GrowthCurve.MinLevel, card.Member.level);
            int levels = from - GrowthCurve.MinLevel;
            int souls = card.Member.souls;
            long duration = levels <= 0 ? 250 : Math.Min(2200, 300 + 45L * levels);
            card.Element.AddToClassList("soul-harvest-card--burning");

            int lastLevel = from;
            int orbsSent = 0;
            int orbs = Mathf.Clamp(Mathf.CeilToInt(souls / 3f), souls > 0 ? 3 : 0, 26);
            tweens.Add(UiTween.Run(card.Element, duration, t =>
            {
                int level = Mathf.RoundToInt(Mathf.Lerp(from, GrowthCurve.MinLevel, t));
                if (level != lastLevel)
                {
                    lastLevel = level;
                    card.Level.text = $"Lv {level}";
                    UiTransitions.Flash(card.Level, "soul-harvest-card__level--tick", 90);
                }

                // 玉は数え戻す間に均して出す。
                int due = Mathf.FloorToInt(t * orbs);
                for (; orbsSent < due; orbsSent++) SendOrb(card, souls, orbs);
            }, () =>
            {
                for (; orbsSent < orbs; orbsSent++) SendOrb(card, souls, orbs);
                card.Level.text = $"Lv {GrowthCurve.MinLevel}";
                card.Element.RemoveFromClassList("soul-harvest-card--burning");
                card.Element.AddToClassList("soul-harvest-card--spent");
                // 玉が届き終わる頃に、この札は終わり。
                tweens.Add(UiTween.After(root, souls > 0 ? 700 : 100, () =>
                {
                    card.Shown = souls;
                    card.Souls.text = $"+{souls}";
                    RefreshTotal();
                    if (++finishedCards >= cardList.Count) tweens.Add(UiTween.After(root, 650, Bank));
                }));
            }, UiTween.EaseInOutCubic));
        }

        /// <summary>札から合計へ玉を 1 つ。届いたら札と合計の数を、玉の分だけ進める。</summary>
        private void SendOrb(Card card, int souls, int orbs)
        {
            int index = card.Shown;
            Vector2 from = card.Level.worldBound.center;
            Vector2 to = totalValue.worldBound.center;
            int share = Mathf.CeilToInt(souls / (float)Mathf.Max(1, orbs));
            fx.Orb(from, to, 0, 560, () =>
            {
                card.Shown = Mathf.Min(souls, card.Shown + share);
                card.Souls.text = $"+{card.Shown}";
                RefreshTotal();
                UiTransitions.Flash(total, "soul-harvest__total--bump", 120);
                GameAudio.Instance?.PlayUi(UiSound.SoulGain);
            }, 10f);
        }

        private void RefreshTotal()
        {
            int sum = 0;
            foreach (Card card in cardList) sum += card.Shown;
            totalValue.text = sum.ToString();
        }

        /// <summary>合計を右上の手持ちへ飛ばす。</summary>
        private void Bank()
        {
            RefreshTotal();
            VisualElement counter = counterTarget();
            if (counter == null || total == null)
            {
                setCounter(soulsBefore + harvestTotal);
                Finish();
                return;
            }

            Vector2 from = total.worldBound.center;
            Vector2 to = counter.worldBound.center;
            Vector2 delta = to - from;
            total.AddToClassList("soul-harvest__total--fly");
            tweens.Add(UiTween.Run(total, 650, t =>
            {
                total.style.translate = new Translate(delta.x * t, delta.y * t);
                float s = Mathf.Lerp(1f, 0.35f, t);
                total.style.scale = new Scale(new Vector3(s, s, 1f));
                total.style.opacity = 1f - t * 0.7f;
            }, () =>
            {
                total.style.opacity = 0f;
                fx.Ring(to, 90f, "soul-ring--gold");
                GameAudio.Instance?.PlayUi(UiSound.SoulBank);
                long before = soulsBefore;
                long after = soulsBefore + harvestTotal;
                tweens.Add(UiTween.Run(root, 600, t => setCounter((long)Mathf.Lerp(before, after, t)), () =>
                {
                    setCounter(after);
                    tweens.Add(UiTween.After(root, 450, Finish));
                }));
            }, UiTween.EaseInCubic));
        }

        private void Finish()
        {
            IsPlaying = false;
            UiTransitions.ClearStagger(cards);
            UiTransitions.Hide(root);
            Action done = onDone;
            onDone = null;
            done?.Invoke();
        }
    }
}
