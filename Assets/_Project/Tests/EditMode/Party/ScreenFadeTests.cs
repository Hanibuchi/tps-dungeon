using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Party.Tests
{
    /// <summary>移るときの暗転用の幕が、初めは透明で、指定の暗さまで動くことを確かめる。</summary>
    public sealed class ScreenFadeTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void StartsClear_AndReachesTheTarget()
        {
            host = new GameObject("ScreenFadeTests");
            var fade = ScreenFade.Create(host.transform);
            Assert.AreEqual(0f, fade.Alpha, "初めは見える");

            var canvas = fade.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, 1000, "HUD より手前");

            Run(fade.FadeTo(1f, 0f));
            Assert.AreEqual(1f, fade.Alpha, "真っ暗");
            Run(fade.FadeTo(0f, 0f));
            Assert.AreEqual(0f, fade.Alpha, "見える");
        }

        private static void Run(System.Collections.IEnumerator routine)
        {
            for (int i = 0; i < 1000 && routine.MoveNext(); i++) { }
        }
    }
}
