using NUnit.Framework;
using TpsDungeon.Menu.UI;

namespace TpsDungeon.Menu.Tests
{
    /// <summary>インベントリ画面の下に出す操作案内の文言。</summary>
    public class InventoryHintTests
    {
        [Test]
        public void 通常の案内は割り当てたキーを出す()
        {
            Assert.AreEqual("ドラッグで移動　Left Shift＋クリックで移動　Q で捨てる　Tab で閉じる",
                InventoryScreen.HintText("Left Shift", "Q", "Tab"));
        }

        [Test]
        public void 仲間の札は6人までは1列_7人からは2列()
        {
            Assert.AreEqual(1, InventoryScreen.ColumnCount(0));
            Assert.AreEqual(1, InventoryScreen.ColumnCount(1));
            Assert.AreEqual(1, InventoryScreen.ColumnCount(6));
            Assert.AreEqual(2, InventoryScreen.ColumnCount(7));
            Assert.AreEqual(2, InventoryScreen.ColumnCount(12));
        }

        [Test]
        public void 札の並べ替えの見せ方は並びの移し方と同じ()
        {
            // 0 番を 3 番へ: 1〜3 番が 1 つ前へ詰まる。
            Assert.AreEqual(3, InventoryScreen.PreviewIndex(0, 0, 3));
            Assert.AreEqual(0, InventoryScreen.PreviewIndex(1, 0, 3));
            Assert.AreEqual(2, InventoryScreen.PreviewIndex(3, 0, 3));
            Assert.AreEqual(4, InventoryScreen.PreviewIndex(4, 0, 3));

            // 3 番を先頭へ: 0〜2 番が 1 つ後ろへずれる。
            Assert.AreEqual(0, InventoryScreen.PreviewIndex(3, 3, 0));
            Assert.AreEqual(1, InventoryScreen.PreviewIndex(0, 3, 0));
            Assert.AreEqual(3, InventoryScreen.PreviewIndex(2, 3, 0));
            Assert.AreEqual(4, InventoryScreen.PreviewIndex(4, 3, 0));

            // 動かさないときはそのまま。
            Assert.AreEqual(2, InventoryScreen.PreviewIndex(2, 1, 1));
        }

        [Test]
        public void クイック移動キーを押している間の案内()
        {
            Assert.AreEqual("クリックで移動", InventoryScreen.QuickMoveHintText);
        }
    }
}
