using NUnit.Framework;
using UnityEngine;

namespace Cube.Replication.Tests {
    public class TestTransformHistory {
        [Test]
        public void Test_empty() {
            var t = new TransformHistory(32);

            t.Sample(0, out Vector3 p, out Quaternion q);

            Assert.IsTrue(p == Vector3.zero);
            Assert.IsTrue(q == Quaternion.identity);
        }

        [Test]
        public void Test_1_entry() {
            var t = new TransformHistory(32);
            t.Add(1, new Pose(new Vector3(1, 0, 0), Quaternion.identity));

            {
                t.Sample(0, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(1, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(2, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
        }

        [Test]
        public void Test_2_entries() {
            var t = new TransformHistory(32);
            t.Add(1, new Pose(new Vector3(1, 0, 0), Quaternion.identity));
            t.Add(2, new Pose(new Vector3(2, 0, 0), Quaternion.identity));

            {
                t.Sample(0, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(1, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(1.5f, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(1.5f, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(2, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(2, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
            {
                t.Sample(3, out Vector3 p, out Quaternion q);
                Assert.AreEqual(new Vector3(2, 0, 0), p);
                Assert.AreEqual(Quaternion.identity, q);
            }
        }
    }
}
