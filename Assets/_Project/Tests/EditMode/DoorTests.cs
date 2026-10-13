using NUnit.Framework;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 扉板が閉じきったときだけ当たることと、ドアのプレハブが実寸（スケール無し）で作られ、扉板を回しても歪まないことを確かめる。
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

        private const string DoorPrefabPath = "Assets/_Project/Prefabs/Parts/Door_Basic.prefab";

        [Test]
        public void DoorPrefab_IsBuiltAtRealSizeWithoutScale()
        {
            // 素材の模様の密度を壁と揃えるため、ドアはどこもスケールせず実寸のメッシュで持つ。
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.IsNotNull(prefab, $"{DoorPrefabPath} が無い");

            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
            {
                AssertVector(Vector3.one, t.localScale, $"{t.name} はスケールしない");
            }
        }

        [TestCase(100f)]
        [TestCase(-100f)]
        [TestCase(45f)]
        public void OpenedLeaf_StaysInsideItsSizeAndIsNotSheared(float angle)
        {
            // 以前はルートを非一様スケールしていたため、角度 0 以外で扉板が斜めに歪んでいた。
            // 置いた向きで回しても、扉板の当たりの箱が大きさを保ち、軸が直交したままであることを確かめる。
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.IsNotNull(prefab, $"{DoorPrefabPath} が無い");

            var root = Object.Instantiate(prefab);
            try
            {
                root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var hinge = root.GetComponent<Door>().Hinge;
                hinge.localRotation = Quaternion.Euler(0f, angle, 0f);

                var leaf = hinge.Find("Leaf");
                var box = leaf.GetComponent<BoxCollider>();
                Vector3 x = leaf.TransformVector(Vector3.right * box.size.x);
                Vector3 y = leaf.TransformVector(Vector3.up * box.size.y);
                Vector3 z = leaf.TransformVector(Vector3.forward * box.size.z);

                Assert.AreEqual(box.size.x, x.magnitude, 0.0001f, "幅が変わらない");
                Assert.AreEqual(box.size.y, y.magnitude, 0.0001f, "高さが変わらない");
                Assert.AreEqual(box.size.z, z.magnitude, 0.0001f, "厚みが変わらない");
                Assert.AreEqual(0f, Vector3.Dot(x.normalized, z.normalized), 0.0001f, "幅と厚みの軸が直交したまま");
                Assert.AreEqual(0f, Vector3.Dot(x.normalized, y.normalized), 0.0001f, "幅と高さの軸が直交したまま");
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
