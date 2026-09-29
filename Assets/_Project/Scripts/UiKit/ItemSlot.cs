using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// アイテムの枠 1 つ分の要素を組み立てる。HUD のホットバーとインベントリ画面で同じ見た目にするため共有する。
    /// 見た目は Theme.uss の .slot*。選んでいる枠は <see cref="SelectedClass"/> を付ける。
    /// 武器などレア度のあるものは、絵の後ろにランクの色の光を敷く（<see cref="SetItem"/>）。中心が濃く、外へ向かって地に溶ける。
    /// </summary>
    public static class ItemSlot
    {
        public const string SelectedClass = "slot--selected";
        public const string IconName = "slot-icon";
        public const string RankName = "slot-rank";

        /// <summary>ランクの光のいちばん濃いところ（中心）の不透明度。リニア色空間では半透明が強く出るので控えめに。</summary>
        public const float RankGlowPeak = 0.8f;

        /// <summary>ランクの光が消えきる半径。枠の半幅を 1 とする。1 で枠の辺の中ほど、角はもう少し外なので必ず地に溶ける。</summary>
        public const float RankGlowRadius = 0.95f;

        private const int GlowSize = 64;

        private static Texture2D glow;

        /// <summary>枠を作る。number が空なら番号を出さない。枠の中身はクリックを受けない（枠自体の pickingMode は呼び出し側で決める）。</summary>
        public static VisualElement Create(string name, string number)
        {
            var slot = new VisualElement { name = name };
            slot.AddToClassList("slot");

            // ランクの下地。絵と線の後ろに敷く。
            var rank = new VisualElement { name = RankName, pickingMode = PickingMode.Ignore };
            rank.AddToClassList("slot__rank");
            slot.Add(rank);

            // 内側の細い線。
            var inner = new VisualElement { pickingMode = PickingMode.Ignore };
            inner.AddToClassList("slot__inner");
            slot.Add(inner);

            var icon = new VisualElement { name = IconName, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("slot__icon");
            slot.Add(icon);

            if (!string.IsNullOrEmpty(number))
            {
                var label = new Label(number) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("slot__number");
                slot.Add(label);
            }

            // 選んだときに上に灯る宝石。
            var gem = new VisualElement { pickingMode = PickingMode.Ignore };
            gem.AddToClassList("slot__gem");
            slot.Add(gem);

            return slot;
        }

        /// <summary>枠に絵とランクの下地を入れる。rank が null なら下地を消す（枠の地のまま）。</summary>
        public static void SetItem(VisualElement slot, Texture2D icon, Color? rank)
        {
            SetIcon(slot, icon);
            SetRankGlow(slot?.Q<VisualElement>(RankName), rank);
        }

        /// <summary>
        /// element の背景に、中心がランクの色で外へ向かって薄れていく光を敷く。背景色（USS の地）はそのまま下に残る。
        /// rank が null なら光を外す。
        /// </summary>
        public static void SetRankGlow(VisualElement element, Color? rank)
        {
            if (element == null) return;

            if (!rank.HasValue)
            {
                element.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                element.style.unityBackgroundImageTintColor = new StyleColor(StyleKeyword.Null);
                return;
            }

            Color c = rank.Value;
            element.style.backgroundImage = new StyleBackground(Glow);
            element.style.unityBackgroundImageTintColor = new StyleColor(new Color(c.r, c.g, c.b, 1f));
        }

        /// <summary>白い丸い光。色はティントで付ける。一度作って使い回す。</summary>
        private static Texture2D Glow
        {
            get
            {
                if (glow != null) return glow;

                glow = new Texture2D(GlowSize, GlowSize, TextureFormat.RGBA32, false)
                {
                    name = "ItemSlotRankGlow",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[GlowSize * GlowSize];
                for (int y = 0; y < GlowSize; y++)
                for (int x = 0; x < GlowSize; x++)
                    pixels[y * GlowSize + x] = new Color(1f, 1f, 1f, GlowAlpha(x, y, GlowSize));

                glow.SetPixels32(pixels);
                glow.Apply(false, true);
                return glow;
            }
        }

        /// <summary>光の画素 (x, y) の不透明度。中心で <see cref="RankGlowPeak"/>、<see cref="RankGlowRadius"/> でなめらかに 0。</summary>
        public static float GlowAlpha(int x, int y, int size)
        {
            float half = size * 0.5f;
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / RankGlowRadius);
            // 中心寄りに色を集めたいので、なめらかな曲線をもう一度かけて裾を細らせる。
            float s = t * t * (3f - 2f * t);
            return RankGlowPeak * s * s;
        }

        /// <summary>枠に絵を入れる。null なら空にする。</summary>
        public static void SetIcon(VisualElement slot, Texture2D icon)
        {
            VisualElement element = slot?.Q<VisualElement>(IconName);
            if (element == null) return;

            element.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        }
    }
}
