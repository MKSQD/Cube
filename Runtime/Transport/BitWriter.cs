using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public class BitWriter {
        // All goodness from yojimbo network library. All bugs belong to us.

        ulong _scratch = 0;
        int _scratchBits = 0;
        int _wordIndex = 0;
        int _bitsWritten = 0;
        int _numBits => _data.Length * 32;
        Memory<uint> _data;

        public ReadOnlySpan<byte> DataWritten {
            get {
                Assert.IsTrue(_scratchBits == 0, "call FlushBits first");
                return MemoryMarshal.AsBytes(_data.Span).Slice(0, BytesWritten);
            }
        }

        public Memory<uint> RawData => _data;

        public int AlignBits => (8 - (_bitsWritten % 8)) % 8;

        public int BitsWritten => _bitsWritten;

        public int BitsAvailable => _numBits - _bitsWritten;

        public int BytesWritten {
            get {
                Assert.IsTrue(_scratchBits == 0, "call FlushBits first");
                return (_bitsWritten + 7) / 8;
            }
        }

        public BitWriter(int numWords = 16) {
            _data = new uint[numWords];
        }

        public BitWriter(Memory<uint> preallocatedMemory) {
            Assert.IsTrue(preallocatedMemory.Length > 0);
            _data = preallocatedMemory;
        }

        void WriteBits(uint value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(_bitsWritten + bits <= _numBits, "exhausted");
            Assert.IsTrue(((ulong)value) <= (((ulong)(1) << bits) - 1));

            _scratch |= (ulong)(value) << _scratchBits;
            _scratchBits += bits;

            if (_scratchBits >= 32) {
                Assert.IsTrue(_wordIndex < _data.Length);
                _data.Span[_wordIndex] = host_to_network((uint)_scratch & 0xFFFFFFFF);
                _scratch >>= 32;
                _scratchBits -= 32;
                _wordIndex++;
            }

            _bitsWritten += bits;
        }

        public void WriteAlign() {
            int remainderBits = _bitsWritten % 8;
            if (remainderBits != 0) {
                uint zero = 0;
                WriteBits(zero, 8 - remainderBits);
                Assert.IsTrue((_bitsWritten % 8) == 0);
            }
        }

        void WriteBytes(Span<byte> data) {
            Assert.IsTrue(AlignBits == 0);
            Assert.IsTrue(_bitsWritten + data.Length * 8 <= _numBits);
            Assert.IsTrue((_bitsWritten % 32) == 0 || (_bitsWritten % 32) == 8 || (_bitsWritten % 32) == 16 || (_bitsWritten % 32) == 24);

            int headBytes = (4 - (_bitsWritten % 32) / 8) % 4;
            if (headBytes > data.Length)
                headBytes = data.Length;
            for (int i = 0; i < headBytes; ++i)
                WriteBits(data[i], 8);
            if (headBytes == data.Length)
                return;

            FlushBits();

            Assert.IsTrue(AlignBits == 0);

            int numWords = (data.Length - headBytes) / 4;
            if (numWords > 0) {
                Assert.IsTrue((_bitsWritten % 32) == 0);

                data.Slice(headBytes, numWords * 4).CopyTo(MemoryMarshal.AsBytes(_data.Span.Slice(_wordIndex)));
                //memcpy(&m_data[m_wordIndex], data + headBytes, numWords * 4);
                _bitsWritten += numWords * 32;
                _wordIndex += numWords;
                _scratch = 0;
            }

            Assert.IsTrue(AlignBits == 0);

            int tailStart = headBytes + numWords * 4;
            int tailBytes = data.Length - tailStart;
            Assert.IsTrue(tailBytes >= 0 && tailBytes < 4);
            for (int i = 0; i < tailBytes; ++i)
                WriteBits(data[tailStart + i], 8);

            Assert.IsTrue(AlignBits == 0);

            Assert.IsTrue(headBytes + numWords * 4 + tailBytes == data.Length);
        }

        public void FlushBits() {
            if (_scratchBits != 0) {
                Assert.IsTrue(_scratchBits <= 32);
                Assert.IsTrue(_wordIndex < _data.Length);
                _data.Span[_wordIndex] = host_to_network((uint)(_scratch & 0xFFFFFFFF));
                _scratch >>= 32;
                _scratchBits = 0;
                _wordIndex++;
            }
        }

        public void Clear() {
            _scratch = 0;
            _scratchBits = 0;
            _wordIndex = 0;
            _bitsWritten = 0;
        }


        public void WriteBool(bool val) => WriteBits((uint)(val ? 0x00000001 : 0x00000000), 1);
        public void WriteByte(byte val) => WriteBits((uint)val, 8);

        public void WriteByte(int val) {
            Assert.IsTrue(val >= 0);
            Assert.IsTrue(val < 255);
            WriteByte((byte)val);
        }

        public void WriteUShort(ushort val) => WriteBits(val, 16);
        public void WriteFloat(float val) => WriteBits(BitUtil.CastFloatToUInt(val), 32);


        public void WriteLossyFloat(float val, float min, float max, float precision = 0.1f) {
            val = Mathf.Clamp(val, min, max);

            var inv = 1 / precision;
            WriteIntInRange((int)(val * inv), (int)(min * inv), (int)(max * inv));
        }

        public static float QuantizeFloat(float val, float min, float max, float precision = 0.1f) {
            val = Mathf.Clamp(val, min, max);

            var inv = 1 / precision;

            var bits = BitUtil.ComputeRequiredIntBits((int)(min * inv), (int)(max * inv));
            var mask = (uint)((1L << bits) - 1);
            var data = (uint)((int)(val * inv) - (int)(min * inv)) & mask;

            return (data + (int)(min * inv)) * precision;
        }




        /// <summary>
        /// Write float in the range [-1,1] with 2 bytes.
        /// </summary>
        public void WriteNormalised(float val) {
            Assert.IsTrue(val > -1.01f && val < 1.01f);

            val = Mathf.Clamp(val, -1f, 1f);
            WriteUShort((ushort)((val + 1f) * 32767.5f));
        }

        public void WriteInt(int val) => WriteBits((uint)val, 32);

        public void WriteIntInRange(int val, int minInclusive, int maxInclusive) {
#if UNITY_EDITOR
            if (val < minInclusive || val > maxInclusive) {

                Debug.LogWarning("Clamped value " + val + " to (" + minInclusive + "," + maxInclusive + ")");


            }
#endif
            val = Mathf.Clamp(val, minInclusive, maxInclusive);

            var bits = BitUtil.ComputeRequiredIntBits(minInclusive, maxInclusive);
            var data = (uint)(val - minInclusive) & (uint)((1L << bits) - 1);
            WriteBits(data, bits);
        }

        public void WriteUInt(uint val) => WriteBits(val, 32);

        public void WriteString(string val) {
            if (val.Length <= 32) {
                WriteBool(true);
                WriteIntInRange(val.Length, 0, 32);
            } else if (val.Length <= 256) {
                WriteBool(false);
                WriteBool(true);
                WriteIntInRange(val.Length, 0, 256);
            } else {
                WriteBool(false);
                WriteBool(false);
                WriteUShort((ushort)val.Length);
            }

            for (int i = 0; i < val.Length; ++i) {
                WriteIntInRange((int)val[i], 0x0020, 0x007E);
            }
        }

        public void WriteVector2(Vector2 val) {
            WriteFloat(val.x);
            WriteFloat(val.y);
        }

        public void WriteVector3(Vector3 val) {
            WriteFloat(val.x);
            WriteFloat(val.y);
            WriteFloat(val.z);
        }

        public void WriteNormalised(Vector3 val) {
            WriteNormalised(val.x);
            WriteNormalised(val.y);
            WriteNormalised(val.z);
        }

        public void WriteQuaternion(Quaternion val) {
            int largest = 0;
            float a, b, c;

            float abs_w = Mathf.Abs(val.w);
            float abs_x = Mathf.Abs(val.x);
            float abs_y = Mathf.Abs(val.y);
            float abs_z = Mathf.Abs(val.z);

            float largest_value = abs_x;

            if (abs_y > largest_value) {
                largest = 1;
                largest_value = abs_y;
            }
            if (abs_z > largest_value) {
                largest = 2;
                largest_value = abs_z;
            }
            if (abs_w > largest_value) {
                largest = 3;
                largest_value = abs_w;
            }
            if (val[largest] >= 0f) {
                a = val[(largest + 1) % 4];
                b = val[(largest + 2) % 4];
                c = val[(largest + 3) % 4];
            } else {
                a = -val[(largest + 1) % 4];
                b = -val[(largest + 2) % 4];
                c = -val[(largest + 3) % 4];
            }

            // serialize
            const float minimum = -1f / 1.414214f;        // note: 1.0f / sqrt(2)
            const float maximum = +1f / 1.414214f;
            const float delta = maximum - minimum;
            const uint maxIntegerValue = (1 << 10) - 1; // 10 bits
            const float maxIntegerValueF = maxIntegerValue;
            float normalizedValue;
            uint integerValue;

            uint sentData = ((uint)largest) << 30;
            // a
            normalizedValue = Mathf.Clamp01((a - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | ((integerValue & maxIntegerValue) << 20);
            // b
            normalizedValue = Mathf.Clamp01((b - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | ((integerValue & maxIntegerValue) << 10);
            // c
            normalizedValue = Mathf.Clamp01((c - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | (integerValue & maxIntegerValue);

            WriteUInt(sentData);
        }

        public void WriteSerializable(ISerializable obj) {
            obj.Serialize(this);
        }

        // #todo
        static uint host_to_network(uint value) => value;
    }
}
