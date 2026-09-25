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
            Assert.AreEqual("ドラッグで移動　Left Shift＋クリックで移動　O で捨てる　Tab で閉じる",
                InventoryScreen.HintText("Left Shift", "O", "Tab"));
        }

        [Test]
        public void クイック移動キーを押している間の案内()
        {
            Assert.AreEqual("クリックで移動", InventoryScreen.QuickMoveHintText);
        }
    }
}
