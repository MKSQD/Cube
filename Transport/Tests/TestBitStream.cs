using NUnit.Framework;
using UnityEngine;

namespace Cube.Transport.Tests {
    public class TestBitStream {
        [Test]
        public void Test_LossyFloat() {
            var bs = new BitStream();

            for (int i = -32; i < 32; ++i) {
                bs.WriteLossyFloat(i, -32, 32, 1);
                Assert.AreEqual(i, bs.ReadLossyFloat(-32, 32, 1));
            }

            bs.WriteLossyFloat(1.5f, 0, 3, 0.5f);
            Assert.AreEqual(1.5f, bs.ReadLossyFloat(0, 3, 0.5f));
        }

        [Test]
        public void Test_QuantizeFloat() {
            var bs = new BitStream();

            bs.WriteLossyFloat(10, -32, 32, 1);
            Assert.AreEqual(BitStream.QuantizeFloat(10, -32, 32, 1), bs.ReadLossyFloat(-32, 32, 1));

            bs.WriteLossyFloat(-10, -32, 32, 1);
            Assert.AreEqual(BitStream.QuantizeFloat(-10, -32, 32, 1), bs.ReadLossyFloat(-32, 32, 1));

            bs.WriteLossyFloat(10, -32, 32, 0.5f);
            Assert.AreEqual(BitStream.QuantizeFloat(10, -32, 32, 0.5f), bs.ReadLossyFloat(-32, 32, 0.5f));

            bs.WriteLossyFloat(0.125f, -32, 32, 0.5f);
            Assert.AreEqual(BitStream.QuantizeFloat(0.125f, -32, 32, 0.5f), bs.ReadLossyFloat(-32, 32, 0.5f));
        }

        [Test]
        public void Test_IntInRange() {
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

            bs.Write(true); // Make bitstream unaligned
            bs.ReadBool();

            bs.WriteIntInRange(0, 0, 3);
            Assert.AreEqual(0, bs.ReadIntInRange(0, 3));

            bs.WriteIntInRange(1, 0, 3);
            Assert.AreEqual(1, bs.ReadIntInRange(0, 3));

            bs.WriteIntInRange(2, 0, 3);
            Assert.AreEqual(2, bs.ReadIntInRange(0, 3));

            bs.WriteIntInRange(3, 0, 3);
            Assert.AreEqual(3, bs.ReadIntInRange(0, 3));
        }

        [Test]
        public void Test_WriteRead_String() {
            var strings = new string[] { "",
                "f",
                "foo",
                "1.4.0",
                "foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo" };
            foreach (var str in strings) {
                var bs = new BitStream();
                bs.Write(str);
                Assert.AreEqual(bs.ReadString(), str);

                var bs2 = new BitStream();
                bs2.Write(true);
                bs2.Write(str);
                bs2.ReadBool();
                Assert.AreEqual(bs2.ReadString(), str);

                var bs3 = new BitStream();
                bs3.Write(true);
                bs3.Write(true);
                bs3.Write(str);
                bs3.ReadBool();
                bs3.ReadBool();
                Assert.AreEqual(bs3.ReadString(), str);
            }
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
            Assert.AreEqual(0.5f, bs.ReadNormalisedFloat(), 0.001f);

            bs.WriteNormalised(1);
            Assert.AreEqual(1, bs.ReadNormalisedFloat());
        }

        [Test]
        public void Test_WriteRead_BitStream() {
            var bs = new BitStream();

            bs.Write(42);
            Assert.AreEqual(42, bs.ReadInt());

            var bs2 = new BitStream();
            bs2.Write(true);
            bs2.Write(false);
            bs2.Write(42);
            bs.Write(bs2);

            Assert.AreEqual(true, bs.ReadBool());
            Assert.AreEqual(false, bs.ReadBool());
            Assert.AreEqual(42, bs.ReadInt());
        }
    }
}
