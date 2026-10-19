using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 階を組むと、FloorConfig の Volume の profile が Global Volume として一緒に置かれ、その上に層の色味（FloorMood）の Volume が重なることを確かめる。
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
            Assert.AreEqual(2, volumes.Length, "共通の Volume と、層の色味を重ねる Volume");
            Volume shared = System.Array.Find(volumes, v => v.sharedProfile == profile);
            Assert.IsNotNull(shared, "FloorConfig の profile の Volume がある");
            Assert.IsTrue(shared.isGlobal);
            Assert.IsNotNull(shared.GetComponent<FloorPostProcessing>(), "カメラの post-processing を有効にする役も付く");

            Volume mood = System.Array.Find(volumes, v => v != shared);
            Assert.IsTrue(mood.isGlobal);
            Assert.Greater(mood.priority, shared.priority, "層の色味は共通の Volume の上に重ねる");
            Assert.IsTrue(mood.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments color));
            Assert.IsTrue(color.colorFilter.overrideState);
            Assert.IsFalse(color.postExposure.overrideState, "露出は共通の Volume の値を使う（上書きしない）");
            Assert.AreEqual(config.mood.filter, color.colorFilter.value);
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
