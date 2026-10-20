using System.IO;
using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとのショートカットの解放状態（ShortcutUnlockSet）と、その保存（ShortcutUnlocks）を確かめる。</summary>
    public sealed class ShortcutUnlockTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "tps-dungeon-shortcut-unlock-tests.json");
            if (File.Exists(path)) File.Delete(path);
            ShortcutUnlocks.PathOverride = path;
        }

        [TearDown]
        public void TearDown()
        {
            ShortcutUnlocks.PathOverride = null;
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Set_UnlocksAndLocksOneLayerAtATime()
        {
            var set = new ShortcutUnlockSet();
            Assert.IsFalse(set.IsUnlocked(0));
            Assert.IsTrue(set.Set(2, true));
            Assert.IsFalse(set.Set(2, true), "同じ値なら変わらない");
            Assert.IsTrue(set.Set(0, true));
            CollectionAssert.AreEqual(new[] { 0, 2 }, set.unlockedLayers, "小さい順");
            Assert.IsTrue(set.Set(2, false));
            Assert.IsFalse(set.IsUnlocked(2));
            Assert.IsFalse(set.Set(-1, true), "負の層は無い");
        }

        [Test]
        public void Normalize_DropsNegativeAndDuplicateLayers()
        {
            var set = JsonUtility.FromJson<ShortcutUnlockSet>("{\"version\":1,\"unlockedLayers\":[3,-1,3,1]}");
            set.Normalize();
            CollectionAssert.AreEqual(new[] { 1, 3 }, set.unlockedLayers);
        }

        [Test]
        public void Store_WritesToTheFileAndTellsWhoListens()
        {
            int heard = -1;
            void OnChanged(int layer) => heard = layer;
            ShortcutUnlocks.Changed += OnChanged;
            try
            {
                Assert.IsFalse(ShortcutUnlocks.IsUnlocked(1), "保存が無ければどれも閉じている");
                ShortcutUnlocks.SetUnlocked(1, true);
                Assert.AreEqual(1, heard);
                Assert.IsTrue(File.Exists(path));

                ShortcutUnlocks.Reload();
                Assert.IsTrue(ShortcutUnlocks.IsUnlocked(1), "読み直しても残る");
                Assert.IsFalse(ShortcutUnlocks.IsUnlocked(0));

                heard = -1;
                ShortcutUnlocks.SetUnlocked(1, true);
                Assert.AreEqual(-1, heard, "変わらなければ知らせない");
            }
            finally
            {
                ShortcutUnlocks.Changed -= OnChanged;
            }
        }

        [Test]
        public void Store_IgnoresABrokenFile()
        {
            File.WriteAllText(path, "{ これは壊れている");
            ShortcutUnlocks.Reload();
            Assert.IsFalse(ShortcutUnlocks.IsUnlocked(0));
            ShortcutUnlocks.SetUnlocked(0, true);
            ShortcutUnlocks.Reload();
            Assert.IsTrue(ShortcutUnlocks.IsUnlocked(0), "壊れていても書き直せる");
        }
    }
}
