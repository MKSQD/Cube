using NUnit.Framework;
using UnityEngine;

namespace Cube.Transport.Tests {
    public class TestBitStream {
        [Test]
        public void Test_CompressDecompress_Float() {
            var bs = new BitStream();

            for (int i = -32; i < 32; ++i) {
                bs.WriteLossyFloat(i, -32, 32, 1);
                Assert.AreEqual(i, bs.ReadLossyFloat(-32, 32, 1));
            }

            bs.WriteLossyFloat(1.5f, 0, 3, 0.5f);
            Assert.AreEqual(1.5f, bs.ReadLossyFloat(0, 3, 0.5f));
        }

        [Test]
        public void Test_CompressDecompress_Int() {
            var bs = new BitStream();

            for (int i = -32; i < 32; ++i) {
                bs.WriteIntInRange(i, -32, 32);
                Assert.AreEqual(i, bs.ReadIntInRange(-32, 32));
            }

            bs.WriteIntInRange(-33, -32, 32);
            Assert.AreEqual(-32, bs.ReadIntInRange(-32, 32));

            bs.WriteIntInRange(33, -32, 32);
            Assert.AreEqual(32, bs.ReadIntInRange(-32, 32));

            bs.WriteIntInRange(15, 0, 32);
            Assert.AreEqual(15, bs.ReadIntInRange(0, 32));
        }

        [Test]
        public void Test_WriteRead_Int() {
            var bs = new BitStream();

            bs.Write(42);
            Assert.AreEqual(42, bs.ReadInt());
        }

        [Test]
        public void Test_WriteRead_Float() {
            var bs = new BitStream();

            bs.Write(17.123456789f);
            Assert.AreEqual(17.123456789f, bs.ReadFloat());
        }

        [Test]
        public void Test_WriteRead_Vector2() {
            var bs = new BitStream();

            var pos = new Vector2(1.25f, 2.5f);
            bs.Write(pos);
            Assert.AreEqual(pos, bs.ReadVector2());
        }

        [Test]
        public void Test_WriteRead_Vector3() {
            var bs = new BitStream();

            var pos = new Vector3(1.25f, 2.5f, 3.75f);
            bs.Write(pos);
            Assert.AreEqual(pos, bs.ReadVector3());
        }

        [Test]
        public void Test_WriteRead_NormalisedFloat() {
            var bs = new BitStream();

            bs.WriteNormalised(0);
            Assert.AreEqual(0, bs.ReadNormalisedFloat());

            bs.WriteNormalised(0.5f);
            Assert.AreEqual(0.5f, bs.ReadNormalisedFloat());

            bs.WriteNormalised(1);
            Assert.AreEqual(1, bs.ReadNormalisedFloat());
        }
    }
}
