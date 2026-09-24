using NUnit.Framework;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 扉板が閉じきったときだけ当たることと、ドアを壁の大きさに合わせても扉板が開口に収まり、回しても歪まないことを確かめる。
    /// </summary>
    public sealed class DoorTests
    {
        [TestCase(false, false, true, TestName = "閉じきっている → 当たる")]
        [TestCase(true, false, false, TestName = "開ききっている → 当たらない")]
        [TestCase(true, true, false, TestName = "開いている途中 → 当たらない")]
        [TestCase(false, true, false, TestName = "閉じている途中 → 当たらない")]
        public void LeafIsSolidOnlyWhenFullyClosed(bool isOpen, bool isSwinging, bool expected)
        {
            Assert.AreEqual(expected, Door.IsLeafSolid(isOpen, isSwinging));
        }

        [Test]
        public void Fit_ScalesOnlyTheFrameAndSizesLeafInMeters()
        {
            var (root, door, frame, hinge, leaf) = CreateDoor();
            try
            {
                // FloorBuilder と同じく幅 cellSize=5、高さ wallHeight=3 に合わせる。
                door.Fit(5f, 3f);

                AssertVector(Vector3.one, root.transform.lossyScale, "ルートはスケールしない");
                AssertVector(new Vector3(5f, 3f, 1f), frame.lossyScale, "枠だけが (幅, 高さ, 1) になる");
                AssertVector(Vector3.one, hinge.lossyScale, "Hinge はスケールしない");
                AssertVector(new Vector3(-1f, 0f, 0f), hinge.position, "Hinge は開口（幅 2m）の左端");
                AssertVector(new Vector3(0f, 1.125f, 0f), leaf.position, "閉じた扉板は開口の真ん中");
                AssertVector(new Vector3(2f, 2.25f, 0.08f), leaf.lossyScale, "扉板は開口と同じ 2m x 2.25m");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(100f)]
        [TestCase(-100f)]
        [TestCase(45f)]
        public void OpenedLeaf_IsNotSheared(float angle)
        {
            // 以前はルートを非一様スケールしていたため、角度 0 以外で扉板が斜めに歪んでいた。
            // 扉板の軸が回した後も直交し、長さも変わらないことを確かめる。
            var (root, door, _, hinge, leaf) = CreateDoor();
            try
            {
                root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                door.Fit(5f, 3f);
                hinge.localRotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 x = leaf.TransformVector(Vector3.right);
                Vector3 y = leaf.TransformVector(Vector3.up);
                Vector3 z = leaf.TransformVector(Vector3.forward);

                Assert.AreEqual(2f, x.magnitude, 0.0001f, "幅が変わらない");
                Assert.AreEqual(2.25f, y.magnitude, 0.0001f, "高さが変わらない");
                Assert.AreEqual(0.08f, z.magnitude, 0.0001f, "厚みが変わらない");
                Assert.AreEqual(0f, Vector3.Dot(x.normalized, z.normalized), 0.0001f, "幅と厚みの軸が直交したまま");
                Assert.AreEqual(0f, Vector3.Dot(x.normalized, y.normalized), 0.0001f, "幅と高さの軸が直交したまま");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static (GameObject root, Door door, Transform frame, Transform hinge, Transform leaf) CreateDoor()
        {
            var root = new GameObject("Door");
            var frame = new GameObject("Frame").transform;
            frame.SetParent(root.transform, false);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(hinge, false);

            var door = root.AddComponent<Door>();
            var serialized = new SerializedObject(door);
            serialized.FindProperty("frame").objectReferenceValue = frame;
            serialized.FindProperty("hinge").objectReferenceValue = hinge;
            serialized.FindProperty("leaf").objectReferenceValue = leaf;
            serialized.FindProperty("openingWidth").floatValue = 0.4f;
            serialized.FindProperty("openingHeight").floatValue = 0.75f;
            serialized.FindProperty("leafThickness").floatValue = 0.08f;
            serialized.FindProperty("leafGap").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return (root, door, frame, hinge, leaf);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), 0.0001f, $"{message}: expected {expected}, actual {actual}");
        }
    }
}
