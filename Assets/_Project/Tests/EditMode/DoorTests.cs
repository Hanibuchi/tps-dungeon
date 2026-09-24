using NUnit.Framework;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 扉が相手と反対側へ開くことと、非一様スケールのルートの下でも扉板が歪まず開口に収まることを確かめる。
    /// </summary>
    public sealed class DoorTests
    {
        private const float OpenAngle = 100f;

        [Test]
        public void OpenAngle_FromFront_SwingsToBack()
        {
            float angle = Door.OpenAngleAwayFrom(Vector3.zero, Vector3.forward, new Vector3(0.3f, 0f, 1.5f), OpenAngle);
            Assert.AreEqual(OpenAngle, angle, "表（forward 側）から開けたら +Y 回転で裏へ開く");
        }

        [Test]
        public void OpenAngle_FromBack_SwingsToFront()
        {
            float angle = Door.OpenAngleAwayFrom(Vector3.zero, Vector3.forward, new Vector3(-0.3f, 0f, -1.5f), OpenAngle);
            Assert.AreEqual(-OpenAngle, angle, "裏から開けたら -Y 回転で表へ開く");
        }

        [Test]
        public void OpenAngle_FollowsDoorRotation()
        {
            // 東向きに置いたドア（FloorBuilder は 90 度単位で回す）の、東側から開ける。
            var forward = Quaternion.Euler(0f, 90f, 0f) * Vector3.forward;
            float angle = Door.OpenAngleAwayFrom(new Vector3(10f, 0f, 5f), forward, new Vector3(11.5f, 0f, 5f), OpenAngle);
            Assert.AreEqual(OpenAngle, angle);
        }

        [Test]
        public void OpenedLeaf_LandsOnTheFarSide()
        {
            // 符号の取り違えを実際の回転で確かめる。表から開けた扉板の先端は裏（-z）にあるはず。
            float angle = Door.OpenAngleAwayFrom(Vector3.zero, Vector3.forward, Vector3.forward * 2f, OpenAngle);
            Vector3 tip = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
            Assert.Less(tip.z, 0f);
        }

        [Test]
        public void FitLeafToOpening_UnderNonUniformScale_KeepsHingeUnscaledAndFillsOpening()
        {
            var root = new GameObject("Door");
            try
            {
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(root.transform, false);
                var leaf = new GameObject("Leaf").transform;
                leaf.SetParent(hinge, false);

                var door = root.AddComponent<Door>();
                var serialized = new SerializedObject(door);
                serialized.FindProperty("hinge").objectReferenceValue = hinge;
                serialized.FindProperty("leaf").objectReferenceValue = leaf;
                serialized.FindProperty("openingWidth").floatValue = 0.4f;
                serialized.FindProperty("openingHeight").floatValue = 0.75f;
                serialized.FindProperty("leafGap").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // FloorBuilder と同じく (cellSize, wallHeight, 1) にスケールする。
                root.transform.localScale = new Vector3(5f, 3f, 1f);
                door.FitLeafToOpening();

                AssertVector(Vector3.one, hinge.lossyScale, "Hinge の世界スケールは 1（回しても歪まない）");
                AssertVector(new Vector3(-1f, 0f, 0f), hinge.position, "Hinge は開口（幅 2m）の左端");
                Assert.AreEqual(2f, leaf.lossyScale.x, 0.0001f, "扉板の幅は開口と同じ 2m");
                Assert.AreEqual(2.25f, leaf.lossyScale.y, 0.0001f, "扉板の高さは開口と同じ 2.25m");
                AssertVector(new Vector3(0f, 1.125f, 0f), leaf.position, "閉じた扉板は開口の真ん中");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), 0.0001f, $"{message}: expected {expected}, actual {actual}");
        }
    }
}
