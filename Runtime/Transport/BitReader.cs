using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public sealed class BitReader {
        // All goodness from yojimbo network library. All bugs belong to us.

        public ReadOnlyMemory<uint> Data => _data;

        ulong _scratch = 0;
        int _scratchBits = 0;
        int _bitsRead = 0;
        int _wordIndex = 0;
        readonly ReadOnlyMemory<uint> _data;
        int NumBits => _numBytes * 8;

        readonly int _numBytes;
        public int NumBytes => _numBytes;

        public int BitsRead => _bitsRead;
        int GetAlignBits => (8 - _bitsRead % 8) % 8;

        public BitReader(ReadOnlySpan<byte> data, Memory<uint> preallocatedMemory) {
            var newSpan = MemoryMarshal.AsBytes(preallocatedMemory.Span);
            data.CopyTo(newSpan);

            _data = preallocatedMemory;
            _numBytes = data.Length;
        }

        public BitReader(BitWriter writer) {
            writer.FlushBits();

            _data = writer.RawData;
            _numBytes = writer.BytesWritten;
        }

        public BitReader(ReadOnlyMemory<uint> data, int bytes) {
            _data = data;
            _numBytes = bytes;
        }

        /// <summary>
        /// Does a DEEP clone of the exact state.
        /// </summary>
        public BitReader Clone() {
            var bs = new BitReader(Data[..((NumBytes / 4) + 1)], NumBytes);
            bs.SkipBits(BitsRead);
            return bs;
        }

        public bool WouldReadPastEnd(int bits) => _bitsRead + bits > NumBits;

        public void SkipBits(int bits) {
            ReadBits(bits);
        }

        public uint ReadBits(int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(_bitsRead + bits <= NumBits, "exhausted");

            _bitsRead += bits;

            Assert.IsTrue(_scratchBits >= 0 && _scratchBits <= 64);

            if (_scratchBits < bits) {
                Assert.IsTrue(_wordIndex < _data.Length, "exhausted");
                _scratch |= (ulong)(network_to_host(_data.Span[_wordIndex])) << _scratchBits;
                _scratchBits += 32;
                _wordIndex++;
            }

            Assert.IsTrue(_scratchBits >= bits);

            uint output = (uint)(_scratch & (((ulong)(1) << bits) - 1));

            _scratch >>= bits;
            _scratchBits -= bits;

            return output;
        }

        public bool ReadAlign() {
            int remainderBits = _bitsRead % 8;
            if (remainderBits != 0) {
                uint value = ReadBits(8 - remainderBits);
                Assert.IsTrue(_bitsRead % 8 == 0);
                if (value != 0)
                    return false;
            }
            return true;
        }

        void ReadBytes(Span<byte> data) {
            Assert.IsTrue(GetAlignBits == 0);
            Assert.IsTrue(_bitsRead + data.Length * 8 <= NumBits);
            Assert.IsTrue((_bitsRead % 32) == 0 || (_bitsRead % 32) == 8 || (_bitsRead % 32) == 16 || (_bitsRead % 32) == 24);

            int headBytes = (4 - (_bitsRead % 32) / 8) % 4;
            if (headBytes > data.Length) {
                headBytes = data.Length;
            }
            for (int i = 0; i < headBytes; ++i) {
                data[i] = (byte)ReadBits(8);
            }
            if (headBytes == data.Length)
                return;

            Assert.IsTrue(GetAlignBits == 0);

            int numWords = (data.Length - headBytes) / 4;
            if (numWords > 0) {
                Assert.IsTrue((_bitsRead % 32) == 0);

                _data.Span.Slice(_wordIndex, numWords).CopyTo(MemoryMarshal.Cast<byte, uint>(data[headBytes..]));
                _bitsRead += numWords * 32;
                _wordIndex += numWords;
                _scratchBits = 0;
            }

            Assert.IsTrue(GetAlignBits == 0);

            int tailStart = headBytes + numWords * 4;
            int tailBytes = data.Length - tailStart;
            Assert.IsTrue(tailBytes >= 0 && tailBytes < 4);
            for (int i = 0; i < tailBytes; ++i) {
                data[tailStart + i] = (byte)ReadBits(8);
            }

            Assert.IsTrue(GetAlignBits == 0);

            Assert.IsTrue(headBytes + numWords * 4 + tailBytes == data.Length);
        }

        public void Read(ref bool val) => val = ReadBool();
        public bool ReadBool() => ReadBits(1) != 0;

        public void Read(ref byte val) => val = ReadByte();
        public byte ReadByte() => (byte)ReadBits(8);

        public void Read(ref ushort val) => val = ReadUShort();
        public ushort ReadUShort() => (ushort)ReadBits(16);

        public void Read(ref float val) => val = ReadFloat();
        public float ReadFloat() => BitUtil.CastUIntToFloat(ReadBits(32));

        public float ReadLossyFloat(float min, float max, float precision = 0.1f) {
            var inv = 1 / precision;
            var val = ReadIntInRange((int)(min * inv), (int)(max * inv));
            return val * precision;
        }

        public float ReadNormalisedFloat() {
            var val = ReadUShort();
            var result = (val / 32767.5f) - 1f;
            result = Mathf.Clamp(result, 0, 1);
            return result;
        }

        public void Read(ref int val) => val = ReadInt();
        public int ReadInt() => (int)ReadBits(32);

        public int ReadIntInRange(int minInclusive, int maxInclusive) {
            var bits = BitUtil.ComputeRequiredIntBits(minInclusive, maxInclusive);
            var result = (int)(ReadBits(bits) + minInclusive);
            if (result > maxInclusive)
                throw new Exception("value outside valid range");

            return result;
        }


        public void Read(ref uint val) => val = ReadUInt();

        public uint ReadUInt() => ReadBits(32);

        public string ReadString() {
            int length;

            var under32 = ReadBool();
            if (under32) {
                length = ReadIntInRange(0, 32);
                if (length == 0)
                    return string.Empty;
            } else {
                var under256 = ReadBool();
                if (under256) {
                    length = ReadIntInRange(0, 256);
                } else {
                    length = ReadUShort();
                }
            }

            Assert.IsTrue(length > 0);
            if (length > 4096) // Abitary limit of 4kb
                throw new Exception("value outside valid range");

            var chars = new char[length];
            for (int i = 0; i < length; ++i) {
                chars[i] = (char)ReadIntInRange(0x0020, 0x007E);
            }
            return new string(chars);
        }

        public void Read(ref Vector2 val) {
            val = ReadVector2();
        }

        public Vector2 ReadVector2() {
            var val = new Vector2 {
                x = ReadFloat(),
                y = ReadFloat()
            };
            return val;
        }

        public void Read(ref Vector3 val) {
            val = ReadVector3();
        }

        public Vector3 ReadVector3() {
            var val = new Vector3 {
                x = ReadFloat(),
                y = ReadFloat(),
                z = ReadFloat()
            };
            return val;
        }

        public void Read(ref Quaternion val) {
            val = ReadQuaternion();
        }


        public Quaternion ReadQuaternion() {
            uint readData = ReadUInt();

            int largest = (int)(readData >> 30);
            float a, b, c;

            const float minimum = -1f / 1.414214f;        // note: 1.0f / sqrt(2)
            const float maximum = +1f / 1.414214f;
            const float delta = maximum - minimum;
            const uint maxIntegerValue = (1 << 10) - 1; // 10 bits
            const float maxIntegerValueF = (float)maxIntegerValue;
            uint integerValue;
            float normalizedValue;
            // a
            integerValue = (readData >> 20) & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            a = (normalizedValue * delta) + minimum;
            // b
            integerValue = (readData >> 10) & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            b = (normalizedValue * delta) + minimum;
            // c
            integerValue = readData & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            c = (normalizedValue * delta) + minimum;

            Quaternion result = Quaternion.identity;
            float d = Mathf.Sqrt(1f - a * a - b * b - c * c);
            result[largest] = d;
            result[(largest + 1) % 4] = a;
            result[(largest + 2) % 4] = b;
            result[(largest + 3) % 4] = c;

            return result;
        }

        public void Read(ref IBitSerializable obj) {
            ReadSerializable(obj);
        }

        public void ReadSerializable(IBitSerializable obj) {
            obj.Deserialize(this);
        }

        // #todo
        static uint network_to_host(uint value) => value;
    }
}
