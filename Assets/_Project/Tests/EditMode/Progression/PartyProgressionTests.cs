using System.IO;
using NUnit.Framework;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>経験値の配分、ゲームオーバーのリセット、保存データの書き出しと復元。</summary>
    public sealed class PartyProgressionTests
    {
        private GameObject playerGo;
        private GameObject companionGo;
        private PartyProgression party;
        private CharacterProgression player;
        private CharacterProgression companion;
        private PlayerHealth playerHealth;
        private CharacterGrowthProfile playerProfile;
        private CharacterGrowthProfile companionProfile;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            savePath = Path.Combine(Path.GetTempPath(), $"progress-test-{System.Guid.NewGuid():N}", ProgressSaveStore.FileName);

            playerProfile = ScriptableObject.CreateInstance<CharacterGrowthProfile>();
            companionProfile = ScriptableObject.CreateInstance<CharacterGrowthProfile>();
            // 仲間は主人公と別のパラメータを持てる。
            companionProfile.Configure(new GrowthCurve(80, 5, 1, 12, 0.5, 1, 10, 2, 99));

            playerGo = new GameObject("Player");
            playerHealth = playerGo.AddComponent<PlayerHealth>();
            player = playerGo.AddComponent<CharacterProgression>();
            player.Configure(playerProfile, "player");
            party = playerGo.AddComponent<PartyProgression>();
            party.AutoSave = false;
            party.SavePath = savePath;

            companionGo = new GameObject("Companion");
            companionGo.AddComponent<PlayerHealth>();
            companion = companionGo.AddComponent<CharacterProgression>();
            companion.Configure(companionProfile, "companion.knight");

            // エディタのテストでは OnEnable が走らないので手で登録する。
            party.Register(player);
            party.Register(companion);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(companionGo);
            Object.DestroyImmediate(playerProfile);
            Object.DestroyImmediate(companionProfile);
            string directory = Path.GetDirectoryName(savePath);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void GrantExp_GivesEveryoneTheSameAmount()
        {
            Assert.AreEqual(55, party.GrantExp(55));
            Assert.AreEqual(player.TotalExp, companion.TotalExp);
            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(3, companion.Level);
        }

        [Test]
        public void GrantExp_AppliesMultiplier()
        {
            party.SetExpMultiplier(2f);
            Assert.AreEqual(100, party.GrantExp(50));
            Assert.AreEqual(100, player.TotalExp);
            Assert.AreEqual(100, companion.TotalExp);
        }

        [Test]
        public void CompanionGrowsByItsOwnProfile()
        {
            party.GrantExp(2850); // Lv10
            Assert.AreEqual(10, player.Level);
            Assert.AreEqual(10, companion.Level);
            Assert.AreEqual(129, player.MaxHp);
            Assert.AreEqual(80 + 5 * 9, companion.MaxHp);
        }

        [Test]
        public void EnemyReward_GrantsOnlyOnce()
        {
            var enemy = new GameObject("Enemy").AddComponent<EnemyExpReward>();
            try
            {
                Assert.AreEqual(5, enemy.Grant(party));
                Assert.AreEqual(0, enemy.Grant(party), "同じ敵から 2 回はもらえない");
                Assert.AreEqual(5, player.TotalExp);
            }
            finally
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void ResetForNewRun_ResetsEveryoneAndTheSave()
        {
            party.AutoSave = true;
            party.GrantExp(5000);
            playerHealth.Damage(10);
            party.Save();

            party.ResetForNewRun();

            Assert.AreEqual(1, player.Level);
            Assert.AreEqual(0, player.Exp);
            Assert.AreEqual(1, companion.Level);
            Assert.AreEqual(100, playerHealth.CurrentHp);

            Assert.IsTrue(ProgressSaveStore.TryLoad(out var saved, savePath));
            Assert.AreEqual(1, saved.Find("player").level, "保存データのレベルも戻る");
            Assert.AreEqual(0, saved.Find("player").exp);
            Assert.AreEqual(1, saved.Find("companion.knight").level);
        }

        [Test]
        public void SaveData_RoundTripsThroughJson()
        {
            party.GrantExp(1234);
            playerHealth.Damage(25);
            int level = player.Level, exp = player.Exp, hp = playerHealth.CurrentHp;
            int companionLevel = companion.Level;

            string json = ProgressSaveStore.ToJson(party.CaptureSaveData());
            party.ResetForNewRun();
            Assert.IsTrue(ProgressSaveStore.TryFromJson(json, out var data));
            party.RestoreSaveData(data);

            Assert.AreEqual(level, player.Level);
            Assert.AreEqual(exp, player.Exp);
            Assert.AreEqual(hp, playerHealth.CurrentHp);
            Assert.AreEqual(companionLevel, companion.Level);
        }

        [Test]
        public void SaveData_RoundTripsThroughFile()
        {
            party.GrantExp(777);
            party.Save();
            int level = player.Level, exp = player.Exp;

            party.ResetForNewRun();
            Assert.IsTrue(party.Load());

            Assert.AreEqual(level, player.Level);
            Assert.AreEqual(exp, player.Exp);
        }

        [Test]
        public void Restore_WaitsForCompanionThatJoinsLater()
        {
            var data = new PartyProgressSaveData();
            data.members.Add(new CharacterProgressSaveData { id = "companion.archer", level = 7, exp = 3, currentHp = 50 });

            party.RestoreSaveData(data);

            var archerGo = new GameObject("Archer");
            try
            {
                var archerHealth = archerGo.AddComponent<PlayerHealth>();
                var archer = archerGo.AddComponent<CharacterProgression>();
                archer.Configure(companionProfile, "companion.archer");
                Assert.AreEqual(1, archer.Level);

                party.Register(archer);

                Assert.AreEqual(7, archer.Level);
                Assert.AreEqual(3, archer.Exp);
                Assert.AreEqual(50, archerHealth.CurrentHp);
            }
            finally
            {
                Object.DestroyImmediate(archerGo);
            }
        }

        [Test]
        public void Capture_KeepsCompanionsThatAreNotHere()
        {
            var data = new PartyProgressSaveData();
            data.members.Add(new CharacterProgressSaveData { id = "companion.archer", level = 7 });
            party.RestoreSaveData(data);

            var captured = party.CaptureSaveData();

            Assert.IsNotNull(captured.Find("player"));
            Assert.AreEqual(7, captured.Find("companion.archer").level, "まだ合流していない仲間の進行も消さない");
        }

        [Test]
        public void Store_BrokenFile_IsNotLoaded()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(savePath, "{ this is not json");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("壊れている"));
            Assert.IsFalse(ProgressSaveStore.TryLoad(out _, savePath));
        }

        [Test]
        public void Store_EmptyPath_MeansDefaultLocation()
        {
            // ドメインリロードをまたぐと null の文字列が空文字で戻るので、空でも既定の場所として扱う（Path の例外にしない）。
            Assert.AreEqual(ProgressSaveStore.Exists(null), ProgressSaveStore.Exists(string.Empty));
        }

        [Test]
        public void Store_MissingFile_IsNotLoaded()
        {
            Assert.IsFalse(ProgressSaveStore.TryLoad(out _, savePath));
        }

        [Test]
        public void Store_OverwritesExistingSave()
        {
            party.Save();
            party.GrantExp(10);
            party.Save();

            Assert.IsTrue(ProgressSaveStore.TryLoad(out var data, savePath));
            Assert.AreEqual(2, data.Find("player").level);
            Assert.IsFalse(File.Exists(savePath + ".tmp"));
        }
    }
}
