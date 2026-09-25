using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// アイテムの枠 1 つ分の要素を組み立てる。HUD のホットバーとインベントリ画面で同じ見た目にするため共有する。
    /// 見た目は Theme.uss の .slot*。選んでいる枠は <see cref="SelectedClass"/> を付ける。
    /// </summary>
    public static class ItemSlot
    {
        public const string SelectedClass = "slot--selected";
        public const string IconName = "slot-icon";

        /// <summary>枠を作る。number が空なら番号を出さない。枠の中身はクリックを受けない（枠自体の pickingMode は呼び出し側で決める）。</summary>
        public static VisualElement Create(string name, string number)
        {
            var slot = new VisualElement { name = name };
            slot.AddToClassList("slot");

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

        /// <summary>枠に絵を入れる。null なら空にする。</summary>
        public static void SetIcon(VisualElement slot, Texture2D icon)
        {
            VisualElement element = slot?.Q<VisualElement>(IconName);
            if (element == null) return;

            element.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        }
    }
}
