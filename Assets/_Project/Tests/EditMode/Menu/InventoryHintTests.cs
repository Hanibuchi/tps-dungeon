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
        public void クイック移動キーを押している間の案内()
        {
            Assert.AreEqual("クリックで移動", InventoryScreen.QuickMoveHintText);
        }
    }
}
