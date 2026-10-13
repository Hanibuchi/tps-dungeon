using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>草花を壊したときの敵の抽選：上から確率を足していき、残りは外れ。合計が 1 を超えたら比にする。</summary>
    public sealed class PlantSpawnTableTests
    {
        [Test]
        public void PicksByCumulativeChance()
        {
            var chances = new[] { 0.1f, 0.2f };
            Assert.AreEqual(0, PlantSpawnTable.Pick(chances, 0f));
            Assert.AreEqual(0, PlantSpawnTable.Pick(chances, 0.099f));
            Assert.AreEqual(1, PlantSpawnTable.Pick(chances, 0.1f), "ちょうど境目は次の組");
            Assert.AreEqual(1, PlantSpawnTable.Pick(chances, 0.299f));
            Assert.AreEqual(-1, PlantSpawnTable.Pick(chances, 0.3f), "合計の残りは何も出ない");
            Assert.AreEqual(-1, PlantSpawnTable.Pick(chances, 0.99f));
        }

        [Test]
        public void SkipsZeroAndNegative()
        {
            var chances = new[] { 0f, -0.5f, 0.25f };
            Assert.AreEqual(2, PlantSpawnTable.Pick(chances, 0f));
            Assert.AreEqual(-1, PlantSpawnTable.Pick(chances, 0.25f));
        }

        [Test]
        public void ScalesDownWhenTotalExceedsOne()
        {
            var chances = new[] { 1f, 1f };
            Assert.AreEqual(0, PlantSpawnTable.Pick(chances, 0.49f));
            Assert.AreEqual(1, PlantSpawnTable.Pick(chances, 0.5f));
            Assert.AreEqual(1, PlantSpawnTable.Pick(chances, 0.999f), "合計 1 を超えたら外れは無い");
        }

        [Test]
        public void EmptyOrNothingNeverSpawns()
        {
            Assert.AreEqual(-1, PlantSpawnTable.Pick(new float[0], 0f));
            Assert.AreEqual(-1, PlantSpawnTable.Pick(new[] { 0f }, 0f));
        }

        [Test]
        public void RollIgnoresEntriesWithoutEnemy()
        {
            var table = ScriptableObject.CreateInstance<PlantSpawnTable>();
            var enemy = new GameObject("enemy");
            try
            {
                table.SetEntries(new[]
                {
                    new PlantSpawnTable.Entry { enemy = null, chance = 0.5f },
                    new PlantSpawnTable.Entry { enemy = enemy, chance = 0.5f },
                });
                Assert.AreSame(enemy, table.Roll(0f), "敵の空いた組は 0 とみなすので、最初から次の組");
                Assert.IsNull(table.Roll(0.6f));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(table);
            }
        }
    }
}
