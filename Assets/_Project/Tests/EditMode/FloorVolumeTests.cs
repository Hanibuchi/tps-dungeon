using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 階を組むと、FloorConfig の Volume の profile が Global Volume として一緒に置かれることを確かめる。
    /// どのシーンで階を作っても（L1_Random でも確認用シーンでも）同じ明るさになるように。
    /// アセットは書き換えないよう、FloorConfig は複製して使い、profile もその場で作る。
    /// </summary>
    public sealed class FloorVolumeTests
    {
        private const string ConfigPath = "Assets/_Project/Settings/Map/Floor_L1_Config.asset";

        private GameObject host;
        private FloorConfig config;
        private VolumeProfile profile;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<FloorConfig>(ConfigPath);
            Assert.IsNotNull(source, $"{ConfigPath} が無い");
            config = Object.Instantiate(source);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            host = new GameObject("FloorVolumeTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Build_PlacesAGlobalVolumeWithTheConfigProfile()
        {
            config.volumeProfile = profile;
            var builder = host.AddComponent<FloorBuilder>();
            builder.Build(config, 7);

            var volumes = builder.GeneratedRoot.GetComponentsInChildren<Volume>(true);
            Assert.AreEqual(1, volumes.Length, "Volume は 1 つ");
            Assert.IsTrue(volumes[0].isGlobal);
            Assert.AreSame(profile, volumes[0].sharedProfile);
            Assert.IsNotNull(volumes[0].GetComponent<FloorPostProcessing>(), "カメラの post-processing を有効にする役も付く");
        }

        [Test]
        public void Build_WithoutProfile_PlacesNoVolume()
        {
            config.volumeProfile = null;
            var builder = host.AddComponent<FloorBuilder>();
            builder.Build(config, 7);

            Assert.AreEqual(0, builder.GeneratedRoot.GetComponentsInChildren<Volume>(true).Length);
        }
    }
}
