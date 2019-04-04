using NUnit.Framework;
using UnityEngine;

namespace Cube.Networking.Transport.Tests {
    public class TestBitStream {
        [Test]
        public void Test_CompressDecompress_Float() {
            var bs = new BitStream();

            for (int i = -32; i < 32; ++i) {
                bs.CompressFloat(i, -32, 32);
                Assert.AreEqual(i, bs.DecompressFloat(-32, 32));
            }

            bs.CompressFloat(1.5f, 0, 3, 0.5f);
            Assert.AreEqual(1.5f, bs.DecompressFloat(0, 3, 0.5f));
        }

        [Test]
        public void Test_CompressDecompress_Int() {
            var bs = new BitStream();

            for (int i = -32; i < 32; ++i) {
                bs.CompressInt(i, -32, 32);
                Assert.AreEqual(i, bs.DecompressInt(-32, 32));
            }

            bs.CompressInt(-33, -32, 32);
            Assert.AreEqual(-32, bs.DecompressInt(-32, 32));

            bs.CompressInt(33, -32, 32);
            Assert.AreEqual(32, bs.DecompressInt(-32, 32));

            bs.CompressInt(15, 0, 32);
            Assert.AreEqual(15, bs.DecompressInt(0, 32));
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
    }
}
